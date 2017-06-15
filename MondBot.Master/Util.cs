using System.Linq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MondBot.Master
{
    static class Util
    {
        public static string GetUsername(this Message message)
        {
            return message.From.Username ?? message.From.Id.ToString();
        }

        public static PhotoSize GetPhoto(this Message message)
        {
            if (message.Type != MessageType.PhotoMessage)
                return null;

            return message.Photo
                .OrderByDescending(p => p.Width * p.Height)
                .First();
        }

        public static string Truncated(this string text)
        {
            const int cutoff = 1400;

            if (text.Length < cutoff)
                return text;

            return text.Substring(0, cutoff);
        }
    }
}
