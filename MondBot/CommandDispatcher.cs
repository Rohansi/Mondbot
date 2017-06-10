using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MondBot
{
    class CommandDispatcher<TRoom, TUser>
        : IEnumerable<KeyValuePair<string, CommandDispatcher<TRoom, TUser>.CommandHandler>>
    {
        public delegate Task<(string userid, string username)> UserIdentifierReader(TUser user);
        public delegate Task CommandHandler(TRoom room, TUser user, string arguments);

        private readonly Dictionary<string, CommandHandler> _handlers;
        private readonly UserIdentifierReader _userReader;

        public CommandDispatcher(UserIdentifierReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            _handlers = new Dictionary<string, CommandHandler>();
            _userReader = reader;
        }

        public Task Dispatch(string prefix, TRoom room, TUser user, string message)
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
            return handler(room, user, arguments);
        }

        public Task<(string userid, string username)> GetUserIdentifiers(TUser user) => _userReader(user);

        public void Add(string key, CommandHandler handler) =>
            _handlers.Add(key, handler);

        public IEnumerator<KeyValuePair<string, CommandHandler>> GetEnumerator() =>
            _handlers.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
