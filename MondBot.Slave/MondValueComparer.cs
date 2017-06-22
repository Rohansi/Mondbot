using System.Collections.Generic;
using System.Linq;
using Mond;

namespace MondBot.Slave
{
    class MondValueComparer : IEqualityComparer<MondValue>
    {
        private const string Serialize = "__serialize";

        private readonly MondState _state;

        public MondValueComparer(MondState state) => _state = state;

        public bool Equals(MondValue x, MondValue y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                return false;

            if (x.Type == MondValueType.Array)
            {
                return y.Type == MondValueType.Array &&
                       x.Array.Count == y.Array.Count &&
                       x.Array.SequenceEqual(y.Array, this);
            }

            if (x.Type == MondValueType.Object)
            {
                if (y.Type != MondValueType.Object)
                    return false;

                var xSerialize = MondUtil.TrySerialize(_state, x, out var xSerialized);
                var ySerialize = MondUtil.TrySerialize(_state, y, out var ySerialized);

                if (xSerialize && ySerialize)
                    return Equals(xSerialized, ySerialized);

                if (!xSerialize && !ySerialize)
                {
                    return x.Object.Count == y.Object.Count &&
                           !x.Object.Except(y.Object, new MondValueObjectComparer(this)).Any();
                }

                return false; // hidden state possible, must save?
            }

            return x == y;
        }

        public int GetHashCode(MondValue obj) => obj.GetHashCode();
    }

    class MondValueObjectComparer : IEqualityComparer<KeyValuePair<MondValue, MondValue>>
    {
        private readonly MondValueComparer _comparer;

        public MondValueObjectComparer(MondValueComparer comparer) => _comparer = comparer;

        public bool Equals(KeyValuePair<MondValue, MondValue> x, KeyValuePair<MondValue, MondValue> y)
        {
            return _comparer.Equals(x.Key, y.Key) && _comparer.Equals(x.Value, y.Value);
        }

        public int GetHashCode(KeyValuePair<MondValue, MondValue> obj) => obj.GetHashCode();
    }
}
