using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MondBot
{
    internal static class Common
    {
        public static async Task<(byte[] image, string result)> RunScript(string username, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return (null, null);

            var result = await RunModule.Run(username, code + ";");

            var image = result.Image;
            if (result.Image != null && result.Image.Length == 0)
                image = null;

            return (image, result.Output);
        }

        private static readonly Regex AddCommandRegex = new Regex(@"^\s*(\w+|[\.\=\+\-\*\/\%\&\|\^\~\<\>\!\?]+)\s+(.+)$", RegexOptions.Singleline);
        public static async Task<(string result, bool isCode)> AddMethod(string username, string parameters)
        {
            var match = AddCommandRegex.Match(parameters);
            if (!match.Success)
                return ("Usage: /method <name> <code>", false);

            var name = match.Groups[1].Value;
            var code = match.Groups[2].Value;

            var result = (await RunModule.Run(username, $"print({code});")).Output;

            if (result.StartsWith("ERROR:") || result.StartsWith("EXCEPTION:") || result.StartsWith("mondbox"))
                return (result, true);

            if (result != "function")
                return ("Method code must actually be a method!", false);

            var cmd = new SqlCommand(@"INSERT INTO mondbot.variables (name, type, data) VALUES (:name, :type, :data)
                                       ON CONFLICT (name) DO UPDATE SET type = :type, data = :data;")
            {
                ["name"] = name,
                ["type"] = (int)VariableType.Method,
                ["data"] = code
            };

            using (cmd)
            {
                await cmd.ExecuteNonQuery();
            }

            return ("Successfully updated method!", false);
        }

        public static async Task<string> ViewVariable(string name)
        {
            var cmd = new SqlCommand(@"SELECT * FROM mondbot.variables WHERE name = :name;")
            {
                ["name"] = name.Trim()
            };

            using (cmd)
            {
                var result = (await cmd.Execute()).SingleOrDefault();

                if (result == null)
                    return null;

                return (string)result.data;
            }
        }

        private static readonly Regex CommandRegex = new Regex(@"[/]+([a-z]+)");
        public static string CleanupCommand(string command)
        {
            return CommandRegex.Match(command).Groups[1].Value.ToLower();
        }
    }
}
