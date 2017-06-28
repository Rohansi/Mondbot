using System.Collections.Generic;

namespace MondBot.Shared
{
    public static class Util
    {
        private static readonly Dictionary<char, string> OperatorChars = new Dictionary<char, string>
        {
            { '.', "Dot" },
            { '=', "Equals" },
            { '+', "Plus" },
            { '-', "Minus" },
            { '*', "Asterisk" },
            { '/', "Slash" },
            { '%', "Percent" },
            { '&', "Ampersand" },
            { '|', "Pipe" },
            { '^', "Caret" },
            { '~', "Tilde" },
            { '<', "LeftAngle" },
            { '>', "RightAngle" },
            { '!', "Bang" },
            { '?', "Question" },
            { '@', "At" },
            { '#', "Hash" },
            { '$', "Dollar" },
            { '\\', "Backslash" },
        };

        public static bool TryConvertOperatorName(string op, out string name)
        {
            var result = "op_";

            foreach (var ch in op)
            {
                if (!OperatorChars.TryGetValue(ch, out var charName))
                {
                    name = null;
                    return false;
                }

                result += charName;
            }

            name = result;
            return true;
        }
    }
}
