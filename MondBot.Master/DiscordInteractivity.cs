using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.ObjectModel;
using DSharpPlus;

// ripped from DSharpPlus, modified to suit my needs

namespace MondBot.Master
{
    public static class InteractivityExtension
    {
        public static InteractivityModule UseInteractivity(this DiscordClient c)
        {
            if (c.GetModule<InteractivityModule>() != null)
                throw new Exception("Interactivity module is already enabled for this client!");

            var m = new InteractivityModule();
            c.AddModule(m);
            return m;
        }

        public static IReadOnlyDictionary<int, InteractivityModule> UseInteractivity(this DiscordShardedClient c)
        {
            var modules = new Dictionary<int, InteractivityModule>();

            foreach (var shard in c.ShardClients.Select(xkvp => xkvp.Value))
            {
                var m = shard.GetModule<InteractivityModule>() ?? shard.UseInteractivity();

                modules.Add(shard.ShardId, m);
            }

            return new ReadOnlyDictionary<int, InteractivityModule>(modules);
        }

        public static InteractivityModule GetInteractivityModule(this DiscordClient c)
        {
            return c.GetModule<InteractivityModule>();
        }

        public static IReadOnlyDictionary<int, InteractivityModule> GetInteractivityModule(this DiscordShardedClient c)
        {
            var modules = new Dictionary<int, InteractivityModule>();

            foreach (var shard in c.ShardClients.Select(xkvp => xkvp.Value))
                modules.Add(shard.ShardId, shard.GetModule<InteractivityModule>());

            return new ReadOnlyDictionary<int, InteractivityModule>(modules);
        }

        public static IEnumerable<string> Split(this string str, int chunkSize)
        {
            var len = str.Length;
            var i = 0;

            while (i < len)
            {
                var size = Math.Min(len - i, chunkSize);
                yield return str.Substring(i, size);
                i += size;
            }
        }
    }

    public class InteractivityModule : IModule
    {
        public class PaginatedMessage
        {
            public IReadOnlyList<Page> Pages { get; internal set; }
            public int CurrentIndex { get; internal set; }
            public TimeSpan Timeout { get; internal set; }
        }

        public DiscordClient Client { get; private set; }

        public void Setup(DiscordClient client)
        {
            Client = client;
        }

        public async Task SendPaginatedMessage(
            DiscordChannel channel,
            IReadOnlyList<Page> pages,
            TimeSpan timeout,
            TimeoutBehaviour timeoutBehaviour = TimeoutBehaviour.Default)
        {
            if (pages.Count == 0)
                throw new ArgumentException("You need to provide at least 1 page!");

            var m = await Client.SendMessageAsync(channel, pages[0].Content);

            if (pages.Count == 1)
                return; // can't navigate with only 1 page

            var tsc = new TaskCompletionSource<string>();
            var ct = new CancellationTokenSource(timeout);
            ct.Token.Register(() => tsc.TrySetResult(null));
            
            var pm = new PaginatedMessage
            {
                CurrentIndex = 0,
                Pages = pages,
                Timeout = timeout
            };

            async Task AddReactions(DiscordMessage msg)
            {
                await msg.CreateReactionAsync(DiscordEmoji.FromUnicode(Client, "⏮"));
                await Task.Delay(500, ct.Token);
                await msg.CreateReactionAsync(DiscordEmoji.FromUnicode(Client, "◀"));
                await Task.Delay(500, ct.Token);
                await msg.CreateReactionAsync(DiscordEmoji.FromUnicode(Client, "▶"));
                await Task.Delay(500, ct.Token);
                await msg.CreateReactionAsync(DiscordEmoji.FromUnicode(Client, "⏭"));
            }
            
            await AddReactions(m);

            async Task AllReactionsRemoved(MessageReactionRemoveAllEventArgs e)
            {
                if (e.Message.Id != m.Id)
                    return;

                await AddReactions(m);
            }

            Client.MessageReactionRemoveAll += AllReactionsRemoved;

            async Task ReactionHandlerCommon(ulong messageId, ulong userId, string emoji)
            {
                if (messageId != m.Id || userId == Client.CurrentUser.Id)
                    return;

                try
                {
                    var original = pm.CurrentIndex;

                    switch (emoji)
                    {
                        case "⏮":
                            pm.CurrentIndex = 0;
                            break;
                        case "◀":
                            if (pm.CurrentIndex != 0)
                                pm.CurrentIndex--;
                            break;
                        case "▶":
                            if (pm.CurrentIndex != pm.Pages.Count - 1)
                                pm.CurrentIndex++;
                            break;
                        case "⏭":
                            pm.CurrentIndex = pm.Pages.Count - 1;
                            break;
                    }

                    if (pm.CurrentIndex < 0) pm.CurrentIndex = 0;
                    if (pm.CurrentIndex >= pm.Pages.Count) pm.CurrentIndex = pm.Pages.Count - 1;

                    if (pm.CurrentIndex != original)
                        await m.EditAsync(pm.Pages[pm.CurrentIndex].Content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            Task ReactionAdded(MessageReactionAddEventArgs e) =>
                ReactionHandlerCommon(e.Message.Id, e.User.Id, e.Emoji.Name);

            Task ReactionRemoved(MessageReactionRemoveEventArgs e) =>
                ReactionHandlerCommon(e.Message.Id, e.User.Id, e.Emoji.Name);

            Client.MessageReactionAdd += ReactionAdded;
            Client.MessageReactionRemove += ReactionRemoved;

            await tsc.Task;

            switch (timeoutBehaviour)
            {
                case TimeoutBehaviour.Default:
                case TimeoutBehaviour.Ignore:
                    break;
                case TimeoutBehaviour.Delete:
                    await m.DeleteAsync();
                    break;
            }

            Client.MessageReactionRemoveAll -= AllReactionsRemoved;
            Client.MessageReactionAdd -= ReactionAdded;
            Client.MessageReactionRemove -= ReactionRemoved;
        }
        
        public List<Page> GeneratePagesInStrings(
            string input,
            Func<int, int, string> header = null,
            Func<string, string> wrapper = null)
        {
            header = header ?? ((page, total) => null);
            wrapper = wrapper ?? (s => s);

            var headerLength = header(0, 0)?.Length ?? 0;

            var pageContents = input.Split(1950 - headerLength).ToList();
            var count = pageContents.Count;

            return pageContents
                .Select((s, i) => new Page(header(i + 1, count) + wrapper(s)))
                .ToList();
        }
    }

    public enum TimeoutBehaviour
    {
        Default, // ignore
        Ignore,
        Delete
    }

    public class Page
    {
        public string Content { get; }

        public Page(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentNullException(nameof(content));

            Content = content;
        }
    }
}
