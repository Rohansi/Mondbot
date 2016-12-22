using System;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using Owin;
using Telegram.Bot;

namespace MondBot
{
    class Program
    {
        public static TelegramBotClient Bot { get; private set; }

        public static void Main(string[] args)
        {
            Bot = new TelegramBotClient(Settings.Instance.Token);

            var me = Bot.GetMeAsync().Result;
            Console.WriteLine(me.Username);

            using (WebApp.Start<Startup>("http://+:8134"))
            {
                // Register WebHook
                Bot.SetWebhookAsync("https://toronto.rohbot.net/mondbot/WebHook").Wait();

                Console.WriteLine("Server Started");

                // Stop Server after <Enter>
                Console.ReadLine();

                // Unregister WebHook
                Bot.SetWebhookAsync().Wait();
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
