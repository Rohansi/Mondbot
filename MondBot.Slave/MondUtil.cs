using Mond;

namespace MondBot.Slave
{
    internal static class MondUtil
    {
        private const string Serialize = "__serialize";

        public static bool TrySerialize(MondState state, MondValue value, out MondValue serialized)
        {
            bool canSerialize = value[Serialize];

            if (!canSerialize)
            {
                serialized = MondValue.Undefined;
                return false;
            }

            serialized = JsonModule.Serialize(state, value);
            return true;
        }
    }
}
