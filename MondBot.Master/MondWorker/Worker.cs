using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MondBot.Shared;

namespace MondBot.Master
{
    class Worker : IDisposable
    {
        public Process Process { get; private set; }
        private TcpClient _socket;

        private bool _isNew;

        public bool IsDead => Process == null || Process.HasExited;

        public Worker(Process process, TcpClient socket)
        {
            Process = process;
            _socket = socket;

            _isNew = true;
        }

        public void Dispose()
        {
            if (Process == null || _socket == null)
                return;

            _socket.Dispose();
            _socket = null;

            Kill();

            Process.Dispose();
            Process = null;
        }

        public void Kill()
        {
            if (IsDead)
                return;

            Console.WriteLine("KILL");

            try
            {
                Process.Kill();
            }
            catch { }
        }

        public async Task<RunResult> Run(string source)
        {
            Console.WriteLine("Run on {0}", Process.Id);

            var stream = _socket.GetStream();
            var sendStream = new BinaryWriter(stream, new UTF8Encoding(false)); //  { AutoFlush = true };
            var receiveStream = new BinaryReader(stream, new UTF8Encoding(false));

            // send parameters and source code to run
            await sendStream.WriteStringAsync(source);

#if !DEBUG
            var timeout = _isNew ? 12 : 10;
#else
            var timeout = 1000000;
#endif

            _isNew = false;

            Exception HostDied(Exception e) =>
                new RunException("Host Process Died", e);

            // wait for output or timeout
            Task<RunResult> result;
            Task completed;

            try
            {
                result = ReadResult(receiveStream);
                completed = await Task.WhenAny(result, Task.Delay(TimeSpan.FromSeconds(timeout)));
            }
            catch (Exception e)
            {
                throw HostDied(e);
            }

            // if output didn't complete then we timed out
            if (completed != result)
                throw new RunException("Timed Out");

            if (result.IsFaulted)
                throw new RunException("Out of Memory?");

            if (Process.HasExited)
                throw HostDied(null);
            
            return result.Result;
        }

        private async Task<RunResult> ReadResult(BinaryReader receiveStream)
        {
            var output = await receiveStream.ReadStringAsync();
            var image = await receiveStream.ReadBytesAsync();
            return new RunResult(output, image);
        }
    }
}
