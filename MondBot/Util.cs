using Telegram.Bot.Types;

namespace MondBot
{
    static class Util
    {
        public static string GetUsername(this Message message)
        {
            return message.From.Username ?? message.From.Id.ToString();
        }
    }
}
