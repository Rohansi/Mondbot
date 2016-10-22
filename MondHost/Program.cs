﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
            var sendStream = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            var receiveStream = new StreamReader(stream, new UTF8Encoding(false));

            var workerId = Process.GetCurrentProcess().Id;
            await sendStream.WriteLineAsync(workerId.ToString("G"));

            var response = await receiveStream.ReadLineAsync();
            if (response != "OK")
                return;

            var worker = new Worker();

            while (true)
            {
                GC.Collect();

                var source = await receiveStream.ReadLineAsync();
                if (source == null)
                    return;

                var result = worker.Run(source);

                await sendStream.WriteLineAsync(result);
            }
        }
    }
}
