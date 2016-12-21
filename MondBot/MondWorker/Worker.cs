using System;
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

        public bool IsDead
        {
            get { return Process == null || Process.HasExited; }
        }

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

            _socket.Close();
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

        public async Task<string> Run(string username, string source)
        {
            Console.WriteLine("Run on {0}", Process.Id);

            var stream = _socket.GetStream();
            var sendStream = new BinaryWriter(stream, new UTF8Encoding(false)); //  { AutoFlush = true };
            var receiveStream = new BinaryReader(stream, new UTF8Encoding(false));

            // send username and source
            await sendStream.WriteStringAsync(username);
            await sendStream.WriteStringAsync(source);

            var timeout = _isNew ? 17 : 15;
            _isNew = false;

            // wait for output or timeout
            var output = receiveStream.ReadStringAsync();
            var completed = await Task.WhenAny(output, Task.Delay(TimeSpan.FromSeconds(timeout)));

            // if output didn't complete then we timed out
            if (completed != output)
                throw new RunException("Timed Out");

            var result = output.Result;
            
            if (Process.HasExited)
            {
                throw new RunException("Host Process Died"); // TODO: does this still work
            }
            
            return result;
        }
    }
}
