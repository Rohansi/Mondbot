using System;
using Mond;

namespace MondBot.Slave
{
    sealed class CacheEntry
    {
        private MondValue _current;

        public bool IsMethod { get; private set; }

        public MondValue? Original { get; }
        public bool Serialized { get; }

        public MondValue Current
        {
            get => _current;
            set
            {
                _current = value;
                IsMethod = false;
            }
        }

        public CacheEntry(MondState state, bool isMethod, MondValue? original, MondValue value)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _current = value;
            IsMethod = isMethod;

            if (original != null)
            {
                if (MondUtil.TrySerialize(state, original.Value, out var serialized))
                {
                    Original = serialized;
                    Serialized = true;
                }
                else
                {
                    Original = Clone(state, value);
                }
            }
        }

        private static MondValue Clone(MondState state, MondValue value)
        {
            MondValue clone;

            switch (value.Type)
            {
                case MondValueType.Object:
                    clone = MondValue.Object(state);

                    foreach (var kv in value.AsDictionary)
                    {
                        clone.AsDictionary.Add(Clone(state, kv.Key), Clone(state, kv.Value));
                    }

                    clone.UserData = value.UserData;

                    clone.Prototype = Clone(state, value.Prototype);

                    if (value.IsLocked)
                        clone.Lock();
                    
                    return clone;

                case MondValueType.Array:
                    clone = MondValue.Array();

                    foreach (var v in value.AsList)
                    {
                        clone.AsList.Add(Clone(state, v));
                    }

                    return clone;

                default:
                    return value;
            }
        }
    }
}
