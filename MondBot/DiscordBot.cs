using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;

namespace MondBot
{
    internal class DiscordBot : IDisposable
    {
        private delegate Task CommandHandler(DiscordChannel from, string username, string arguments);

        private readonly Dictionary<string, CommandHandler> _commandHandlers;
        private readonly DiscordClient _bot;

        public DiscordBot()
        {
            _commandHandlers = new Dictionary<string, CommandHandler>
            {
                { "h", DoHelp },
                { "help", DoHelp },
                { "r", DoRun },
                { "run", DoRun },
                { "m", DoMethod },
                { "method", DoMethod },
                { "v", DoView },
                { "view", DoView }
            };

            var config = new DiscordConfig()
            {
                AutoReconnect = true,
                DiscordBranch = Branch.Stable,
                LargeThreshold = 2000,
                Token = Settings.Instance.DiscordToken,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = false
            };

            _bot = new DiscordClient(config);
            _bot.SetSocketImplementation<WebSocketSharpClient>();

            _bot.DebugLogger.LogMessageReceived += (o, e) =>
                Console.WriteLine($"[{e.Timestamp}] [{e.Application}] [{e.Level}] {e.Message}");

            _bot.Ready += args => _bot.UpdateStatusAsync("+h for help");

            _bot.MessageCreated += MessageReceived;
        }

        public void Dispose()
        {
            _bot.Dispose();
        }

        public async Task Start()
        {
            await _bot.ConnectAsync();
        }

        private async Task MessageReceived(MessageCreateEventArgs args)
        {
            try
            {
                if (args.Author.IsBot)
                    return;

                if (!TryParseCommand(args.Message.Content, out var command, out var arguments))
                    return;

                if (!_commandHandlers.TryGetValue(command, out var handler))
                    return;

                await handler(args.Channel, args.Author.Username, arguments);
            }
            catch (Exception e)
            {
                Console.WriteLine("Discord message error: " + e);
            }
        }

        private async Task DoHelp(DiscordChannel from, string username, string arguments)
        {
            var embed = new DiscordEmbed
            {
                Description = "I run [Mond](https://github.com/Rohansi/Mond) code for you! My source code can be found on [BitBucket](https://bitbucket.org/rohans/mondbot).",
                Thumbnail = new DiscordEmbedThumbnail { Url = "http://i.imgur.com/zbqVSaz.png" },
                Fields = new List<DiscordEmbedField>
                {
                    new DiscordEmbedField
                    {
                        Name = "Commands",
                        Value = "+run <code>\n+method <name> <code>\n+view <name>"
                    },
                    new DiscordEmbedField
                    {
                        Name = "Documentation",
                        Value = "[Language](https://github.com/Rohansi/Mond/wiki)\n[MondBot extras](https://bitbucket.org/rohans/mondbot/src/master/MondHost/?at=master) (files with Library in the name)"
                    }
                }
            };

            await from.SendMessageAsync("", embed: embed);
        }

        private async Task DoRun(DiscordChannel from, string username, string arguments)
        {
            var (image, output) = await Common.RunScript(username, arguments);

            if (image != null)
            {
                output = string.IsNullOrWhiteSpace(output) ? "" : CodeBlock(output);

                var stream = new MemoryStream(image);
                await from.SendFileAsync(stream, "photo.png", output);
                return; // image with output!
            }

            if (!string.IsNullOrWhiteSpace(output))
                await SendMessage(from, output, true);
            else
                await SendMessage(from, "Finished with no output.");
        }

        private async Task DoMethod(DiscordChannel from, string username, string arguments)
        {
            var (result, isCode) = await Common.AddMethod(username, arguments);
            await SendMessage(from, result, isCode);
        }

        private async Task DoView(DiscordChannel from, string username, string arguments)
        {
            var data = await Common.ViewVariable(arguments.Trim());

            if (data == null)
            {
                await SendMessage(from, "Variable doesn't exist!");
                return;
            }

            await SendMessage(from, data, true);
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

        private static string CodeBlock(string text) =>
            "```\n" + text.Replace("`", "\\`") + "\n```";

        private static readonly Regex CommandRegex = new Regex(@"^\+([a-z]+) ?(.*)?$", RegexOptions.Singleline);
        private static bool TryParseCommand(string text, out string command, out string arguments)
        {
            command = null;
            arguments = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!text.StartsWith("+"))
                return false;

            var match = CommandRegex.Match(text);

            if (!match.Success)
                return false;

            command = match.Groups[1].Value;
            arguments = match.Groups[2].Value;
            return true;
        }
    }
}
