using System;
using System.Collections.Generic;
using System.Threading;
using Mond;
using Mond.Binding;
using Mond.Libraries;

namespace MondHost
{
    class ModifiedCoreLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new CharLibrary();
            yield return new MathLibrary();
            yield return new BetterRandomLibrary(state);
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

    [MondModule("Random")]
    static class BetterRandomModule
    {
        private static readonly Random Random;

        static BetterRandomModule()
        {
            Random = new Random(Environment.TickCount);
        }

        [MondFunction("__call")]
        public static MondValue Call(MondState state, MondValue self, params MondValue[] args)
        {
            return state["Random"]; // backwards compat
        }

        [MondFunction("next")]
        public static int Next()
        {
            return Random.Next();
        }

        [MondFunction("next")]
        public static int Next(int maxValue)
        {
            return Random.Next(maxValue);
        }

        [MondFunction("next")]
        public static int Next(int minValue, int maxValue)
        {
            return Random.Next(minValue, maxValue);
        }

        [MondFunction("nextDouble")]
        public static double NextDouble()
        {
            return Random.NextDouble();
        }
    }
}
