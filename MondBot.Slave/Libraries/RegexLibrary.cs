using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mond;
using Mond.Binding;
using Mond.Libraries;

namespace MondBot.Slave.Libraries
{
    [MondClass("Regex")]
    class RegexClass
    {
        private readonly string _pattern;
        private readonly bool _ignoreCase;
        private readonly bool _multiline;

        private readonly Regex _regex;

        [MondConstructor]
        public RegexClass(string pattern, bool ignoreCase = false, bool multiline = false)
        {
            var options = RegexOptions.ECMAScript;

            if (ignoreCase)
                options |= RegexOptions.IgnoreCase;

            if (multiline)
                options |= RegexOptions.Multiline;

            _pattern = pattern;
            _ignoreCase = ignoreCase;
            _multiline = multiline;

            _regex = new Regex(pattern, options);
        }

        [MondFunction("__serialize")]
        public MondValue Serialize(MondState state, params MondValue[] args)
        {
            var result = MondValue.Object(state);
            result["$ctor"] = "Regex";
            result["$args"] = MondValue.Array(new MondValue[] { _pattern, _ignoreCase, _multiline });
            return result;
        }

        [MondFunction("isMatch")]
        public bool IsMatch(string input, int startat = 0) => _regex.IsMatch(input, startat);

        [MondFunction("match")]
        public MondValue Match(string input, int startat = 0) =>
            ToMond(_regex.Match(input, startat));

        [MondFunction("match")]
        public MondValue Match(string input, int beginning, int length) =>
            ToMond(_regex.Match(input, beginning, length));

        [MondFunction("matches")]
        public MondValue Matches(string input, int startat = 0) =>
            ToMond(_regex.Matches(input, startat));

        [MondFunction("replace")]
        public string Replace(string input, string replacement, int count = -1, int startat = 0) =>
            _regex.Replace(input, replacement, count);

        [MondFunction("split")]
        public MondValue Split(string input, int count = -1, int startat = 0)
        {
            var items = _regex.Split(input, count, startat);
            return MondValue.Array(items.Select(MondValue.String));
        }

        private static MondValue ToMond(MatchCollection matchCollection)
        {
            var value = MondValue.Array();

            foreach (Match m in matchCollection)
            {
                value.AsList.Add(ToMond(m));
            }

            return value;
        }

        private static MondValue ToMond(Match match)
        {
            var result = MondValue.Object();
            result["index"] = match.Index;
            result["length"] = match.Length;
            result["success"] = match.Success;
            result["value"] = match.Value;
            return result;
        }
    }

    class RegexLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new RegexLibrary(state);
        }
    }

    class RegexLibrary : IMondLibrary
    {
        public MondState State { get; }

        public RegexLibrary(MondState state)
        {
            State = state;
        }

        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var regexClass = MondClassBinder.Bind<RegexClass>(State);
            yield return new KeyValuePair<string, MondValue>("Regex", regexClass);
        }
    }
}
