using Mond;

namespace MondBot.Slave
{
    sealed class CacheEntry
    {
        private MondValue _current;

        public bool IsMethod { get; private set; }

        public MondValue Original { get; }

        public MondValue Current
        {
            get => _current;
            set
            {
                _current = value;
                IsMethod = false;
            }
        }

        public CacheEntry(MondState state, bool isMethod, MondValue original, MondValue value)
        {
            _current = value;
            IsMethod = isMethod;
            Original = !isMethod && ReferenceEquals(original, value) ? Clone(state, value) : original;
        }

        private static MondValue Clone(MondState state, MondValue value)
        {
            MondValue clone;

            switch (value.Type)
            {
                case MondValueType.Object:
                    clone = new MondValue(state);

                    foreach (var kv in value.Object)
                    {
                        clone.Object.Add(Clone(state, kv.Key), Clone(state, kv.Value));
                    }

                    clone.UserData = value.UserData;

                    clone.Prototype = Clone(state, value.Prototype);

                    if (value.IsLocked)
                        clone.Lock();
                    
                    return clone;

                case MondValueType.Array:
                    clone = new MondValue(MondValueType.Array);

                    foreach (var v in value.Array)
                    {
                        clone.Array.Add(Clone(state, v));
                    }

                    return clone;

                default:
                    return value;
            }
        }
    }
}
