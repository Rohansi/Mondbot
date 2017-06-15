﻿using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace MondBot
{
    class Program
    {
        public static TelegramBotClient TelegramBot { get; private set; }

        public static void Main(string[] args)
        {
            RunModule.Initialize();

#if !DEBUG
            var telegramThread = new Thread(RunTelegramBot);
            telegramThread.Start();
#endif

            using (var rohbot = new RohBotBot())
#if !DEBUG
            using (var discord = new DiscordBot())
#endif
            {
#if !DEBUG
                discord.Start().Wait();
#endif
                Thread.Sleep(-1);
            }
        }

        private static void RunTelegramBot()
        {
            TelegramBot = new TelegramBotClient(Settings.Instance.Token);

            var me = TelegramBot.GetMeAsync().Result;
            Console.WriteLine(me.Username);

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Startup>()
                .UseUrls("http://localhost:8134/")
                .Build();

            using (host)
            {
                host.Start();
                
                TelegramBot.SetWebhookAsync("https://toronto.rohbot.net/mondbot/WebHook").Wait();

                Console.WriteLine("Telegram Started");

                Thread.Sleep(-1);
            }
        }
        
        public class Startup
        {
            public IConfigurationRoot Configuration { get; }

            public Startup(IHostingEnvironment env)
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(env.ContentRootPath)
                    .AddEnvironmentVariables();

                Configuration = builder.Build();
            }

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddMvc();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
            {
                loggerFactory.AddConsole(Configuration.GetSection("Logging"));

                app.UseMvc();
            }
        }
    }
}
