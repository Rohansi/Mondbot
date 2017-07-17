using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MondBot.Shared;

namespace MondBot.Master
{
    internal static class Common
    {
        public static async Task<(byte[] image, string result)> RunScript(string service, string userid, string username, string code)
        {
            code = CleanupCode(code);

            if (string.IsNullOrWhiteSpace(code))
                return (null, null);

            var result = await RunModule.Run(service, userid, username, code + ";");

            var image = result.Image;
            if (result.Image != null && result.Image.Length == 0)
                image = null;

            return (image, result.Output);
        }

        //private static readonly Regex AddCommandRegex = new Regex(@"^\s*(\w+|[\.\=\+\-\*\/\%\&\|\^\~\<\>\!\?\@\#\$\\]+)\s+(.+)$", RegexOptions.Singleline);

        private static readonly Regex AddCommandRegex = new Regex(
            @"(?:^(?:\@[\w\.]+(?:\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\))?\s+)+|^)(?:fun|seq)(?:\s+(?<name>\w+)|\s*(?<name>\([\.\=\+\-\*\/\%\&\|\^\~\<\>\!\?\@\#\$\\]+\)))\s*\(",
            RegexOptions.Singleline);

        public static async Task<(string result, bool isCode)> AddMethod(string service, string userid, string username, string arguments)
        {
            var code = CleanupCode(arguments);

            var match = AddCommandRegex.Match(code);
            if (!match.Success)
                return ("Usage: +fun <named function>", false);

            var name = match.Groups["name"].Value;
            var testCode = $"{code};\nreturn {name};";
            
            var result = (await RunModule.Run(service, userid, username, testCode)).Output.Trim();

            if (result.StartsWith("ERROR:") || result.StartsWith("EXCEPTION:") || result.StartsWith("mondbox"))
                return (result, true);

            var methodName = name;

            if (name[0] == '(')
            {
                if (!Shared.Util.TryConvertOperatorName(name.Substring(1, name.Length - 2), out methodName))
                    return ("Failed to get method name of operator.", false);
            }

            var cmd = new SqlCommand(@"INSERT INTO mondbot.variables (name, type, data, version) VALUES (:name, :type, :data, 2)
                                       ON CONFLICT (name) DO UPDATE SET type = :type, data = :data, version = 2;")
            {
                ["name"] = methodName,
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
            if (string.IsNullOrEmpty(name))
                return null;

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

        private static string CleanupCode(string code)
        {
            return code.Replace("\r\n", "\n").Replace('\r', '\n').Trim().Trim('`').Trim();
        }
    }
}
