﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MondBot
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

        public async Task<RunResult> Run(string service, string userid, string username, string source)
        {
            Console.WriteLine("Run on {0}", Process.Id);

            var stream = _socket.GetStream();
            var sendStream = new BinaryWriter(stream, new UTF8Encoding(false)); //  { AutoFlush = true };
            var receiveStream = new BinaryReader(stream, new UTF8Encoding(false));

            // send parameters and source code to run
            await sendStream.WriteStringAsync(service);
            await sendStream.WriteStringAsync(userid);
            await sendStream.WriteStringAsync(username);
            await sendStream.WriteStringAsync(source);

#if !DEBUG
            var timeout = _isNew ? 12 : 10;
#else
            var timeout = 1000000;
#endif

            _isNew = false;

            // wait for output or timeout
            var result = ReadResult(receiveStream);
            var completed = await Task.WhenAny(result, Task.Delay(TimeSpan.FromSeconds(timeout)));

            // if output didn't complete then we timed out
            if (completed != result)
                throw new RunException("Timed Out");
            
            if (Process.HasExited)
            {
                throw new RunException("Host Process Died"); // TODO: does this still work
            }
            
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
