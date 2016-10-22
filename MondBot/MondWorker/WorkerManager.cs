using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MondBot
{
    class WorkerManager : IDisposable
    {
        public const int MaxWorkerProcesses = 4;
        public const int MinWorkerProcesses = 2;

        private static readonly bool RunningOnMono = Type.GetType("Mono.Runtime") != null;
        private const bool CpuLimit = true;

        private readonly List<Worker> _workers;
        private readonly ConcurrentQueue<Worker> _idleWorkers;

        private readonly CancellationTokenSource _cts;

        private readonly Stopwatch _timeSinceLastSpawn;
        private readonly Stopwatch _timeSinceLastGet;
        private readonly Stopwatch _timeSinceLastBlock;

        public WorkerManager()
        {
            _workers = new List<Worker>();
            _idleWorkers = new ConcurrentQueue<Worker>();

            _cts = new CancellationTokenSource();

            _timeSinceLastSpawn = Stopwatch.StartNew();
            _timeSinceLastGet = Stopwatch.StartNew();
            _timeSinceLastBlock = Stopwatch.StartNew();

            WorkerListener(_cts.Token);
            WorkerKiller(_cts.Token);
        }

        public void Dispose()
        {
            _cts.Cancel();

            // clear idle workers
            while (_idleWorkers.Count > 0)
            {
                Worker worker;
                _idleWorkers.TryDequeue(out worker);
            }

            // clear and dispose workers
            lock (_workers)
            {
                foreach (var worker in _workers)
                {
                    worker.Dispose();
                }

                _workers.Clear();
            }
        }

        public async Task<Worker> Get()
        {
            if (_cts.IsCancellationRequested)
                throw new RunException("Disposed");

            _timeSinceLastGet.Restart();

        start:

            // check if any are idle
            Worker worker;
            if (_idleWorkers.TryDequeue(out worker))
                return worker;

            int count;

            lock (_workers)
            {
                count = _workers.Count;
            }

            // start a new one if under limit
            if (count < MaxWorkerProcesses)
                Spawn();

            // wait for a free worker
            while (true)
            {
                if (_cts.IsCancellationRequested)
                    throw new RunException("Disposed");

                lock (_workers)
                {
                    if (_timeSinceLastSpawn.Elapsed > TimeSpan.FromSeconds(5) && _workers.Count < MaxWorkerProcesses)
                        goto start;
                }

                _timeSinceLastBlock.Restart();

                await Task.Delay(50);

                if (_idleWorkers.TryDequeue(out worker))
                    return worker;
            }
        }

        public void Enqueue(Worker worker)
        {
            if (worker == null || worker.IsDead)
                return;

            _idleWorkers.Enqueue(worker);
        }

        private void Spawn()
        {
            lock (_workers)
            {
                // fake the worker count while spawning
                _workers.Add(null);
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "MondHost.exe",
                    UseShellExecute = false,
                };

                if (RunningOnMono)
                {
                    startInfo.FileName = "mono";
                    startInfo.Arguments = "MondHost.exe";
                    startInfo.EnvironmentVariables.Add("MONO_GC_PARAMS", "max-heap-size=80M,soft-heap-limit=30M,nursery-size=4M");
                }

                var process = Process.Start(startInfo);
                if (process == null)
                    throw new RunException("No Process");

                if (CpuLimit && RunningOnMono)
                {
                    try
                    {
                        var limitStartInfo = new ProcessStartInfo
                        {
                            FileName = "cpulimit",
                            Arguments = string.Format("-z -l 33 -p {0:G}", process.Id),
                            UseShellExecute = false,
                        };

                        var limitProcess = Process.Start(limitStartInfo);

                        if (limitProcess == null)
                            throw new Exception();

                        // if the limitProcess dies we need to kill the worker too
                        limitProcess.Exited += (sender, args) => process.Kill();
                    }
                    catch (Exception e)
                    {
                        process.Kill();
                        throw new RunException("CpuLimit Failed", e);
                    }
                }

                // kill the new process if its not added to _workers in a few seconds
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));

                    lock (_workers)
                    {
                        _workers.Remove(null);

                        if (_workers.Find(w => w.Process.Id == process.Id) != null)
                            return;
                    }

                    try
                    {
                        process.Kill();
                    }
                    catch { }
                });

                _timeSinceLastSpawn.Restart();
            }
            catch
            {
                lock (_workers)
                {
                    // if something went wrong, remove the fake worker
                    _workers.Remove(null);
                }

                throw;
            }
        }

        private async void WorkerListener(CancellationToken cancellationToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 35555);
            listener.Start();

            while (true)
            {
                try
                {
                    var socket = await listener.AcceptTcpClientAsync();
                    socket.NoDelay = true;

                    var stream = socket.GetStream();
                    var sendStream = new StreamWriter(stream) { AutoFlush = true };
                    var receiveStream = new StreamReader(stream);

                    var workerIdTask = receiveStream.ReadLineAsync();
                    var completed = await Task.WhenAny(workerIdTask, Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken));

                    if (completed != workerIdTask)
                    {
                        Console.WriteLine("WorkerListener: Timed Out");
                        socket.Close();
                        continue;
                    }

                    var workerIdStr = workerIdTask.Result;

                    int workerId;
                    if (!int.TryParse(workerIdStr, out workerId))
                    {
                        Console.WriteLine("WorkerListener: Invalid WorkerId");
                        socket.Close();
                        continue;
                    }

                    var process = Process.GetProcessById(workerId);
                    var worker = new Worker(process, socket);

                    lock (_workers)
                    {
                        _workers.Add(worker);
                        _idleWorkers.Enqueue(worker);
                    }

                    await sendStream.WriteLineAsync("OK");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private async void WorkerKiller(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);

                lock (_workers)
                {
                    _workers.RemoveAll(w => w != null && w.IsDead);
                }

                // don't go under minimum
                if (_workers.Count <= MinWorkerProcesses)
                    continue;

                // if we had recent activity, dont kill any
                if (_timeSinceLastGet.Elapsed < TimeSpan.FromSeconds(30))
                    continue;

                // if a worker was created recently, don't kill any
                if (_timeSinceLastSpawn.Elapsed < TimeSpan.FromMinutes(5))
                    continue;

                // if we're blocked on workers, don't kill any
                if (_timeSinceLastBlock.Elapsed < TimeSpan.FromMinutes(1))
                    continue;

                Worker worker;
                if (!_idleWorkers.TryDequeue(out worker))
                    continue;

                worker.Dispose();

                lock (_workers)
                {
                    _workers.Remove(worker);
                }
            }
        }
    }
}
