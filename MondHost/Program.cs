using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MondBot;

namespace MondHost
{
    class Program
    {
        static void Main()
        {
            AsyncMain().Wait();
        }

        static async Task AsyncMain()
        {
            var socket = new TcpClient();
            socket.NoDelay = true;

            await socket.ConnectAsync(IPAddress.Loopback, 35555);

            var stream = socket.GetStream();
            var sendStream = new BinaryWriter(stream, new UTF8Encoding(false)); //  { AutoFlush = true };
            var receiveStream = new BinaryReader(stream, new UTF8Encoding(false));

            var workerId = Process.GetCurrentProcess().Id;
            await sendStream.WriteInt32Async(workerId);

            var response = await receiveStream.ReadStringAsync();
            if (response != "OK")
                return;

            var worker = new Worker();

            worker.Run("", "", "", "Image.clear(Color(0, 0, 0));"); // warmup

            while (true)
            {
                GC.Collect();

                var service = await receiveStream.ReadStringAsync();
                if (service == null)
                    return;

                var userid = await receiveStream.ReadStringAsync();
                if (userid == null)
                    return;

                var username = await receiveStream.ReadStringAsync();
                if (username == null)
                    return;

                var source = await receiveStream.ReadStringAsync();
                if (source == null)
                    return;

                var result = worker.Run(service, userid, username, source);

                await sendStream.WriteStringAsync(result.Output);
                await sendStream.WriteBytesAsync(result.Image);
            }
        }
    }
}
