using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using Humanizer;
using Humanizer.Localisation;
using MondBot.Shared;

namespace MondBot.Master
{
    internal class DiscordBot : IDisposable
    {
        private const string Service = "discord";

        private readonly CommandDispatcher<DiscordChannel> _commandDispatcher;
        private readonly DiscordClient _bot;
        private readonly InteractivityModule _interactivity;
        private readonly Stopwatch _uptime;

        public DiscordBot()
        {
            _commandDispatcher = new CommandDispatcher<DiscordChannel>
            {
                { "h", DoHelp },
                { "help", DoHelp },

                { "i", DoInfo },
                { "info", DoInfo },

                { "r", DoRun },
                { "run", DoRun },

                { "f", DoMethod },
                { "fun", DoMethod },
                { "func", DoMethod },

                { "v", DoView },
                { "view", DoView },
            };

            var config = new DiscordConfig
            {
                AutoReconnect = true,
                DiscordBranch = Branch.Stable,
                LargeThreshold = 2000,
                Token = Settings.Instance.DiscordToken,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = false
            };

            _bot = new DiscordClient(config);

            _interactivity = _bot.UseInteractivity();
            _uptime = new Stopwatch();

            _bot.DebugLogger.LogMessageReceived += (o, e) =>
                Console.WriteLine($"[{e.Timestamp}] [{e.Application}] [{e.Level}] {e.Message}");

            _bot.Ready += async args =>
            {
                _uptime.Restart();
                await _bot.UpdateStatusAsync(new Game { Name = "+h for help" });
            };

            _bot.MessageCreated += MessageReceived;
        }

        public void Dispose() => _bot.Dispose();

        public Task Start() => _bot.ConnectAsync();

        private Task MessageReceived(MessageCreateEventArgs args)
        {
            if (args.Author.IsBot)
                return Task.CompletedTask;

            Task.Run(async () =>
            {
                try
                {
                    await _commandDispatcher.Dispatch("+",
                        args.Channel,
                        args.Author.Id.ToString("G"),
                        args.Author.Username,
                        args.Message.Content);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Discord message error: " + e);
                }
            });

            return Task.CompletedTask;
        }

        private async Task DoHelp(DiscordChannel room, string userid, string username, string arguments)
        {
            var embed = new DiscordEmbed
            {
                Description = "I run [Mond](https://github.com/Rohansi/Mond) code for you!",
                Thumbnail = new DiscordEmbedThumbnail { Url = "http://i.imgur.com/zbqVSaz.png" },
                Fields = new List<DiscordEmbedField>
                {
                    new DiscordEmbedField
                    {
                        Name = "Commands",
                        Value = "`+run <code>` - run a script\n\n`+func <named fun/seq>` - save a function to the database\n\n`+view <name>` - view the value of a variable or function\n\nThese can also be shortened to single letters, for example `+r` is the same as `+run`."
                    },
                    new DiscordEmbedField
                    {
                        Name = "Documentation",
                        Value = "[Language](https://github.com/Rohansi/Mond/wiki)\n[MondBot extras](https://bitbucket.org/rohans/mondbot/src/master/MondHost/Libraries/)"
                    }
                }
            };

            await room.SendMessageAsync("", embed: embed);
        }

        private async Task DoInfo(DiscordChannel room, string userid, string username, string arguments)
        {
            var embed = new DiscordEmbed
            {
                Thumbnail = new DiscordEmbedThumbnail { Url = "http://i.imgur.com/zbqVSaz.png" },
                Fields = new List<DiscordEmbedField>
                {
                    new DiscordEmbedField
                    {
                        Name = "Creator",
                        Value = "<@85484593636970496>"
                    },
                    new DiscordEmbedField
                    {
                        Name = "Uptime",
                        Value = _uptime.Elapsed.Humanize(3, true, minUnit: TimeUnit.Second)
                    },
                    new DiscordEmbedField
                    {
                        Name = "Source Code",
                        Value = "[BitBucket](https://bitbucket.org/rohans/mondbot/)"
                    },
                    new DiscordEmbedField
                    {
                        Name = "Library",
                        Value = "[DSharpPlus](https://github.com/NaamloosDT/DSharpPlus/)"
                    }
                }
            };

            await room.SendMessageAsync("", embed: embed);
        }

        private async Task DoRun(DiscordChannel room, string userid, string username, string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return;

            var (image, output) = await Common.RunScript(Service, userid, username, arguments);

            var description = "Finished with no output.";
            if (!string.IsNullOrWhiteSpace(output))
                description = CodeBlock(output);
            else if (image != null)
                description = "";

            if (image != null)
            {
                var stream = new MemoryStream(image);
                await room.SendFileAsync(stream, "photo.png", description);
                return; // image with output!
            }

            await SendMessage(room, description);
        }

        private async Task DoMethod(DiscordChannel room, string userid, string username, string arguments)
        {
            var (result, isCode) = await Common.AddMethod(Service, userid, username, arguments);
            await SendMessage(room, result, isCode);
        }

        private async Task DoView(DiscordChannel room, string userid, string username, string arguments)
        {
            var name = arguments.Trim();
            var data = await Common.ViewVariable(name);

            if (data == null)
            {
                await SendMessage(room, $"Variable {CodeField(name)} doesn't exist!");
                return;
            }

            string Header(int page, int total) =>
                $"({page}/{total}) **Variable: {CodeField(name)}**\n";

            var pages = _interactivity.GeneratePagesInStrings(data, Header, CodeBlock);
            await _interactivity.SendPaginatedMessage(room, pages, TimeSpan.FromMinutes(2));
        }

        private static async Task SendMessage(DiscordChannel room, string text, bool isCode = false)
        {
            if (isCode)
            {
                await room.SendMessageAsync(CodeBlock(text));
            }
            else
            {
                await room.SendMessageAsync(text);
            }
        }

        private static string CodeField(string text) => "`" + text.Replace('`', '´') + "`";
        private static string CodeBlock(string text) => "```" + text.Replace("```", "´´´") + "```";
    }
}
