using System.Linq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MondBot
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
    }
}
