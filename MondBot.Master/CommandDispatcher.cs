using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MondBot.Master
{
    class CommandDispatcher<TRoom> : IEnumerable<KeyValuePair<string, CommandDispatcher<TRoom>.CommandHandler>>
    {
        public delegate Task CommandHandler(TRoom room, string userid, string username, string arguments);

        private readonly Dictionary<string, CommandHandler> _handlers;

        public CommandDispatcher()
        {
            _handlers = new Dictionary<string, CommandHandler>();
        }

        public Task Dispatch(string prefix, TRoom room, string userid, string username, string message)
        {
            if (!message.StartsWith(prefix))
                return Task.CompletedTask;

            var split = message.Substring(prefix.Length).Split(null, 2);

            if (split.Length == 0)
                return Task.CompletedTask;

            var command = split[0];

            if (!_handlers.TryGetValue(command, out var handler))
                return Task.CompletedTask;

            var arguments = split.Length == 1 ? "" : split[1];
            return handler(room, userid, username, arguments);
        }

        public void Add(string key, CommandHandler handler) =>
            _handlers.Add(key, handler);

        public IEnumerator<KeyValuePair<string, CommandHandler>> GetEnumerator() =>
            _handlers.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
