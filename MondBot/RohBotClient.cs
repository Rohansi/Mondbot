﻿using System;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSocketSharp;

namespace MondBot
{
    class RohBotClient : IDisposable
    {
        public delegate Task MessageReceivedHandler(string chat, string userid, string username, string message);

        private readonly WebSocket _socket;

        public event MessageReceivedHandler MessageReceived;

        public RohBotClient()
        {
            _socket = new WebSocket("wss://rohbot.net/ws/");
            
            _socket.OnOpen += SocketOpened;
            _socket.OnMessage += SocketReceivedMessage;
            _socket.OnClose += SocketClosed;
            _socket.OnError += SocketErrored;

            _socket.ConnectAsync();
        }

        public void Dispose()
        {
            _socket.OnClose -= SocketClosed;
            _socket.OnError -= SocketErrored;

            _socket.Close();
        }

        public void Send(string chat, string message)
        {
            try
            {
                _socket.Send(JsonConvert.SerializeObject(new
                {
                    Type = "sendMessage",
                    Target = chat,
                    Content = message
                }));
            }
            catch (Exception e)
            {
                Log("Send failed: {0}", e);
            }
        }

        private void SocketOpened(object sender, EventArgs args)
        {
            _socket.Send(JsonConvert.SerializeObject(new
            {
                Type = "auth",
                Method = "login",
                Username = Settings.Instance.RohBotUsername,
                Password = Settings.Instance.RohBotPassword
            }));
        }

        private void SocketReceivedMessage(object sender, MessageEventArgs args)
        {
            if (!args.IsText)
                return;

            var obj = JsonConvert.DeserializeObject<dynamic>(args.Data);

            switch ((string)obj.Type)
            {
                case "authResponse":
                    var success = (bool)obj.Success;
                    Log("Got AuthResponse, success={0}", success);
                    break;

                case "sysMessage":
                    Log("SysMessage: {0}", (string)obj.Content);
                    break;

                case "message":
                    var type = (string)obj.Line.Type;
                    if (type != "chat")
                        break;

                    var chat = (string)obj.Line.Chat;
                    var userid = (string)obj.Line.SenderId;
                    var username = WebUtility.HtmlDecode((string)obj.Line.Sender);
                    var message = WebUtility.HtmlDecode((string)obj.Line.Content).Replace("\r", "");

                    if (username == Settings.Instance.RohBotUsername)
                        break;

                    Task.Run(async () =>
                    {
                        try
                        {
                            var handler = MessageReceived;
                            if (handler != null)
                                await handler.Invoke(chat, userid, username, message);
                        }
                        catch (Exception e)
                        {
                            Log("Message handler threw exception: {0}", e);
                        }
                    });
                    
                    break;
            }
        }

        private void SocketClosed(object sender, CloseEventArgs args)
        {
            _socket.Connect();
        }

        private void SocketErrored(object sender, ErrorEventArgs args)
        {
            Log("WebSocket error: {0} {1}", args.Message, args.Exception);
        }

        private static void Log(string format, params object[] args)
        {
            Console.WriteLine("[RohBot] " + string.Format(format, args));
        }
    }
}
