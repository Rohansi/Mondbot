using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;

namespace MondBot
{
    internal class DiscordBot : IDisposable
    {
        private const string Service = "discord";

        private readonly CommandDispatcher<DiscordChannel> _commandDispatcher;
        private readonly DiscordClient _bot;

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
                { "function", DoMethod },

                { "v", DoView },
                { "view", DoView },

                // backwards compat
                { "m", DoMethod },
                { "method", DoMethod },
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
            _bot.SetWebSocketClient<WebSocketSharpClient>();

            _bot.DebugLogger.LogMessageReceived += (o, e) =>
                Console.WriteLine($"[{e.Timestamp}] [{e.Application}] [{e.Level}] {e.Message}");

            _bot.Ready += args => _bot.UpdateStatusAsync("+h for help");

            _bot.MessageCreated += MessageReceived;
        }

        public void Dispose() => _bot.Dispose();

        public Task Start() => _bot.ConnectAsync();

        private async Task MessageReceived(MessageCreateEventArgs args)
        {
            try
            {
                if (args.Author.IsBot)
                    return;

                await _commandDispatcher.Dispatch("+", args.Channel,
                    args.Author.Id.ToString("G"),
                    args.Author.Username,
                    args.Message.Content);
            }
            catch (Exception e)
            {
                Console.WriteLine("Discord message error: " + e);
            }
        }

        private async Task DoHelp(DiscordChannel from, string userid, string username, string arguments)
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

            await from.SendMessageAsync("", embed: embed);
        }

        private async Task DoInfo(DiscordChannel from, string userid, string username, string arguments)
        {
            var embed = new DiscordEmbed
            {
                Thumbnail = new DiscordEmbedThumbnail { Url = "http://i.imgur.com/zbqVSaz.png" },
                Fields = new List<DiscordEmbedField>
                {
                    new DiscordEmbedField
                    {
                        Name = "Creator",
                        Value = "Rohan#2847"
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

            await from.SendMessageAsync("", embed: embed);
        }

        private async Task DoRun(DiscordChannel from, string userid, string username, string arguments)
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
                await from.SendFileAsync(stream, "photo.png", description);
                return; // image with output!
            }

            await SendMessage(from, description);
        }

        private async Task DoMethod(DiscordChannel from, string userid, string username, string arguments)
        {
            var (result, isCode) = await Common.AddMethod(Service, userid, username, arguments);
            await SendMessage(from, result, isCode);
        }

        private async Task DoView(DiscordChannel from, string userid, string username, string arguments)
        {
            var name = arguments.Trim();
            var data = await Common.ViewVariable(name);

            if (data == null)
            {
                await SendMessage(from, $"Variable {CodeField(name)} doesn't exist!");
                return;
            }

            await SendMessage(from, $"**Variable: {CodeField(name)}**\n{CodeBlock(data)}");
        }

        private static async Task SendMessage(DiscordChannel to, string text, bool isCode = false)
        {
            if (isCode)
            {
                await to.SendMessageAsync(CodeBlock(text));
            }
            else
            {
                await to.SendMessageAsync(text);
            }
        }

        private static string CodeField(string text) => "`" + text.Replace('`', '´') + "`";
        private static string CodeBlock(string text) => "```\n" + text.Replace("```", "´´´") + "\n```";
    }
}
