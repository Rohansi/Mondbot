using System;
using System.Collections.Generic;
using System.Linq;
using DSharpPlus.Interactivity;

namespace MondBot.Master
{
    static class DiscordInteractivityExtensions
    {
        public static List<Page> GeneratePagesInStrings(
            this InteractivityModule module,
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
                .Select((s, i) => new Page { Content = header(i + 1, count) + wrapper(s) })
                .ToList();
        }
    }
}
