using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using Humanizer;
using Humanizer.Localisation;
using MondBot.Shared;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext.Attributes;

namespace MondBot.Master
{
    internal class DiscordBot : IDisposable
    {
        public InteractivityModule Interactivity { get; }
        public Stopwatch Uptime { get; }

        private readonly DiscordClient _bot;

        public DiscordBot()
        {
            var config = new DiscordConfiguration
            {
                AutoReconnect = true,
                LargeThreshold = 2000,
                Token = Settings.Instance.DiscordToken,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = false,
                LogLevel = LogLevel.Debug
            };

            _bot = new DiscordClient(config);

            var commands = _bot.UseCommandsNext(new CommandsNextConfiguration
            {
                CaseSensitive = false,
                EnableDefaultHelp = false,
                EnableDms = true,
                EnableMentionPrefix = true,
                StringPrefix = "+",
                Dependencies = new DependencyCollectionBuilder().AddInstance(this).Build()
            });
            
            commands.RegisterCommands<DiscordCommands>();

            commands.CommandErrored += args =>
            {
                Console.WriteLine($"Command error: command={args.Command}: {args.Exception}");
                return Task.CompletedTask;
            };

            Interactivity = _bot.UseInteractivity();
            Uptime = new Stopwatch();

            _bot.DebugLogger.LogMessageReceived += (o, e) =>
                Console.WriteLine($"[{e.Timestamp}] [{e.Application}] [{e.Level}] {e.Message}");

            _bot.Ready += async args =>
            {
                Uptime.Restart();
                await _bot.UpdateStatusAsync(new Game { Name = "+h for help" });
            };
        }

        public void Dispose() => _bot.Dispose();

        public Task Start() => _bot.ConnectAsync();
    }

    internal sealed class DiscordCommands
    {
        private const string Service = "discord";

        private readonly DiscordBot _bot;
        private InteractivityModule Interactivity => _bot.Interactivity;

        public DiscordCommands(DiscordBot bot)
        {
            _bot = bot;
        }

        [Command("help")]
        [Aliases("h")]
        [Description("Get command help")]
        public async Task DoHelp(CommandContext e)
        {
            var embed = new DiscordEmbedBuilder
            {
                Description = "I run [Mond](https://github.com/Rohansi/Mond) code for you!",
                ThumbnailUrl = "http://i.imgur.com/zbqVSaz.png",
            }.AddField(
                "Commands",
                "`+run <code>` - run a script\n\n`+fun <named fun/seq>` - save a function to the database\n\n`+view <name>` - view the value of a variable or function\n\nThese can also be shortened to single letters, for example `+r` is the same as `+run`."
            ).AddField(
                "Documentation",
                "[Language](https://github.com/Rohansi/Mond/wiki)\n[MondBot extras](https://bitbucket.org/rohans/mondbot/src/master/MondBot.Slave/Libraries/)"
            ).Build();

            await e.RespondAsync("", embed: embed);
        }

        [Command("info")]
        [Description("Get more information about this bot")]
        public async Task DoInfo(CommandContext e)
        {
            var embed = new DiscordEmbedBuilder
            {
                ThumbnailUrl = "http://i.imgur.com/zbqVSaz.png"
            }.AddField(
                "Creator",
                "<@85484593636970496>"
            ).AddField(
                "Process Uptime",
                MasterProgram.Uptime.Elapsed.Humanize(3, true, minUnit: TimeUnit.Second)
            ).AddField(
                "Socket Uptime",
                _bot.Uptime.Elapsed.Humanize(3, true, minUnit: TimeUnit.Second)
            ).AddField(
                "Source Code",
                "[BitBucket](https://bitbucket.org/rohans/mondbot/)"
            ).AddField(
                "Library",
                "[DSharpPlus](https://github.com/NaamloosDT/DSharpPlus/)"
            );

            await e.RespondAsync("", embed: embed);
        }

        [Command("run")]
        [Aliases("r")]
        [Description("Run a script")]
        public async Task DoRun(CommandContext e, [RemainingText] string code)
        {
            var (userid, username) = ExtractUser(e.User);
            var (image, output) = await Common.RunScript(Service, userid, username, code);

            var description = "Finished with no output.";
            if (!string.IsNullOrWhiteSpace(output))
                description = CodeBlock(output);
            else if (image != null)
                description = "";

            if (image != null)
            {
                var stream = new MemoryStream(image);
                await e.RespondWithFileAsync(stream, "photo.png", description);
                return; // image with output!
            }

            await SendMessage(e, description);
        }

        [Command("fun")]
        [Aliases("f")]
        [Description("Save a function")]
        public async Task DoMethod(CommandContext e, [RemainingText] string code)
        {
            var (userid, username) = ExtractUser(e.User);
            var (result, isCode) = await Common.AddMethod(Service, userid, username, code);
            await SendMessage(e, result, isCode);
        }

        [Command("view")]
        [Aliases("v")]
        [Description("View the value of a variable or function")]
        public async Task DoView(CommandContext e, [RemainingText] string name)
        {
            var data = await Common.ViewVariable(name.Trim());

            if (data == null)
            {
                await SendMessage(e, $"Variable {CodeField(name)} doesn't exist!");
                return;
            }

            string Header(int page, int total) =>
                $"({page}/{total}) **Variable: {CodeField(name)}**\n";

            var pages = Interactivity.GeneratePagesInStrings(data, Header, CodeBlock);

            if (pages.Count == 1)
            {
                await SendMessage(e, pages[0].Content);
            }
            else
            {
                await Interactivity.SendPaginatedMessage(e.Channel, e.User, pages, TimeSpan.FromMinutes(2), TimeoutBehaviour.Ignore);
            }
        }

        private static (string userid, string user) ExtractUser(DiscordUser user) => (user.Id.ToString("G"), user.Username);

        private static async Task SendMessage(CommandContext e, string text, bool isCode = false)
        {
            if (isCode)
            {
                await e.RespondAsync(CodeBlock(text));
            }
            else
            {
                await e.RespondAsync(text);
            }
        }

        private static string CodeField(string text) => "`" + text.Replace('`', '´') + "`";
        private static string CodeBlock(string text) => "```\n" + text.Replace("```", "´´´") + "```";
    }
}
