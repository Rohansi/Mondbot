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
            yield return new ErrorLibrary();
            yield return new CharLibrary();
            yield return new MathLibrary();
            yield return new BetterRandomLibrary();
        }
    }

    class BetterRandomLibrary : IMondLibrary
    {
        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var randomClass = MondClassBinder.Bind<BetterRandomClass>();
            yield return new KeyValuePair<string, MondValue>("Random", randomClass);
        }
    }

    [MondClass("Random")]
    class BetterRandomClass
    {
        private static int _count;
        private readonly Random _random;

        [MondConstructor]
        public BetterRandomClass()
        {
            _random = new Random(Environment.TickCount + Interlocked.Increment(ref _count));
        }

        [MondConstructor]
        public BetterRandomClass(int seed)
        {
            _random = new Random(seed);
        }

        [MondFunction("next")]
        public int Next()
        {
            return _random.Next();
        }

        [MondFunction("next")]
        public int Next(int maxValue)
        {
            return _random.Next(maxValue);
        }

        [MondFunction("next")]
        public int Next(int minValue, int maxValue)
        {
            return _random.Next(minValue, maxValue);
        }

        [MondFunction("nextDouble")]
        public double NextDouble()
        {
            return _random.NextDouble();
        }
    }
}
