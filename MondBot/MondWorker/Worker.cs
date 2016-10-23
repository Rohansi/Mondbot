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
            var sendStream = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            var receiveStream = new StreamReader(stream, new UTF8Encoding(false));

            // send username and source
            await sendStream.WriteLineAsync(Encode(username));
            await sendStream.WriteLineAsync(Encode(source));

            var timeout = _isNew ? 17 : 15;
            _isNew = false;

            // wait for output or timeout
            var output = receiveStream.ReadLineAsync();
            var completed = await Task.WhenAny(output, Task.Delay(TimeSpan.FromSeconds(timeout)));

            // if output didn't complete then we timed out
            if (completed != output)
                throw new RunException("Timed Out");

            var result = output.Result;
            
            if (Process.HasExited)
            {
                throw new RunException("Host Process Died"); // TODO: does this still work
            }

            return Decode(result);
        }

        private static string Encode(string input)
        {
            var sb = new StringBuilder(input.Length);

            foreach (var ch in input)
            {
                switch (ch)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        private static string Decode(string input)
        {
            var sb = new StringBuilder(input.Length);

            for (var i = 0; i < input.Length; i++)
            {
                var ch = input[i];

                if (ch != '\\')
                {
                    sb.Append(ch);
                    continue;
                }

                if (++i >= input.Length)
                    return sb.ToString(); // unexpected eof

                switch (input[i])
                {
                    case '\\':
                        sb.Append('\\');
                        break;

                    case 'r':
                        sb.Append('\r');
                        break;

                    case 'n':
                        sb.Append('\n');
                        break;

                    default:
                        throw new NotSupportedException("Decode: \\" + input[i]);
                }
            }

            return sb.ToString();
        }
    }
}
