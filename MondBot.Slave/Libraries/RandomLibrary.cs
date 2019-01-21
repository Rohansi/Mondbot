using System;
using System.Collections.Generic;
using Mond;
using Mond.Binding;
using Mond.Libraries;

namespace MondBot.Slave.Libraries
{
    [MondModule("Random")]
    static class BetterRandomModule
    {
        private static readonly Random Random;

        static BetterRandomModule() => Random = new Random(Environment.TickCount);

        [MondFunction("__call")]
        public static MondValue Call(MondState state, MondValue self, params MondValue[] args) => state["Random"];

        [MondFunction("next")]
        public static int Next() => Random.Next();

        [MondFunction("next")]
        public static int Next(int maxValue) => Random.Next(maxValue);

        [MondFunction("next")]
        public static int Next(int minValue, int maxValue) => Random.Next(minValue, maxValue);

        [MondFunction("nextDouble")]
        public static double NextDouble() => Random.NextDouble();
    }

    class ModifiedCoreLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new RequireLibrary(state);
            yield return new ErrorLibrary(state);
            yield return new CharLibrary(state);
            yield return new MathLibrary(state);
            yield return new BetterRandomLibrary(state);
            yield return new OperatorLibrary(state);
        }
    }

    class BetterRandomLibrary : IMondLibrary
    {
        private readonly MondState _state;

        public BetterRandomLibrary(MondState state)
        {
            _state = state;
        }

        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var randomModule = MondModuleBinder.Bind(typeof(BetterRandomModule), _state);
            yield return new KeyValuePair<string, MondValue>("Random", randomModule);
        }
    }
}
