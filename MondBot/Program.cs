using System;
using System.Threading;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using Owin;
using Telegram.Bot;

namespace MondBot
{
    class Program
    {
        public static TelegramBotClient TelegramBot { get; private set; }

        public static void Main(string[] args)
        {
            RunModule.Initialize();

            var telegramThread = new Thread(RunTelegramBot);
            telegramThread.Start();

            using (var discord = new DiscordBot())
            {
                discord.Start().Wait();
                Thread.Sleep(-1);
            }
        }

        private static void RunTelegramBot()
        {
            TelegramBot = new TelegramBotClient(Settings.Instance.Token);

            var me = TelegramBot.GetMeAsync().Result;
            Console.WriteLine(me.Username);

            using (WebApp.Start<Startup>("http://+:8134"))
            {
                // Register WebHook
                TelegramBot.SetWebhookAsync("https://toronto.rohbot.net/mondbot/WebHook").Wait();

                Console.WriteLine("Server Started");

                // Stop Server after <Enter>
                Console.ReadLine();

                // Unregister WebHook
                TelegramBot.SetWebhookAsync().Wait();
            }
        }
        
        public class Startup
        {
            public void Configuration(IAppBuilder app)
            {
                var configuration = new HttpConfiguration();

                configuration.Routes.MapHttpRoute("WebHook", "{controller}");

                app.UseWebApi(configuration);
            }
        }
    }
}
