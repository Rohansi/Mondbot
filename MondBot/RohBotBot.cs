using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MondBot
{
    class RohBotBot : IDisposable
    {
        private const string Service = "rohbot";

        private readonly CommandDispatcher<string> _commandDispatcher;
        private readonly RohBotClient _client;

        public RohBotBot()
        {
            _commandDispatcher = new CommandDispatcher<string>
            {
                { "help", DoHelp },
                { "h", DoHelp },

                { "r", DoRun },
                { "run", DoRun },

                { "f", DoMethod },
                { "fun", DoMethod },
                { "func", DoMethod },
                { "function", DoMethod },

                { "v", DoView },
                { "view", DoView },

                // backwards compat
                { "m", DoMethod },
                { "method", DoMethod },
            };

            _client = new RohBotClient();

            _client.MessageReceived += MessageReceived;
        }

        public void Dispose() => _client.Dispose();

        private Task MessageReceived(string chat, string userid, string username, string message) =>
            _commandDispatcher.Dispatch("+", chat, userid, username, message);

        private Task DoHelp(string from, string userid, string username, string arguments)
        {
            return SendMessage(from, @"I run Mond code for you!
+run <code> - run a script
+func <named fun/seq> - save a function to the database
+view <name> - view the value of a variable or function");
        }

        private async Task DoRun(string from, string userid, string username, string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return;
    
            var (image, output) = await Common.RunScript(Service, userid, username, arguments);

            var description = "Finished with no output.";
            if (!string.IsNullOrWhiteSpace(output))
                description = output;
            else if (image != null)
                description = "";

            if (image != null)
                description += "\n\n" + await UploadImage(image);

            await SendMessage(from, description.Trim());
        }

        private async Task DoMethod(string from, string userid, string username, string arguments)
        {
            var (result, _) = await Common.AddMethod(Service, userid, username, arguments);
            await SendMessage(from, result);
        }

        private async Task DoView(string from, string userid, string username, string arguments)
        {
            var name = arguments.Trim();
            var data = await Common.ViewVariable(name);

            if (data == null)
            {
                await SendMessage(from, $"Variable '{name}' doesn't exist!");
                return;
            }

            await SendMessage(from, $"Variable: '{name}'\n{data.Truncated()}");
        }

        private Task SendMessage(string to, string message)
        {
            if (message.StartsWith("/"))
                message = "/" + message;

            _client.Send(to, message);
            return Task.CompletedTask;
        }

        private static async Task<string> UploadImage(byte[] pngData)
        {
            try
            {
                var imageStream = new MemoryStream(pngData);
                var fileContent = new StreamContent(imageStream);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

                var multipartContent = new MultipartFormDataContent();
                multipartContent.Add(fileContent, "file", "photo.png");

                var result = await new HttpClient()
                    .PostAsync("https://vgy.me/upload", multipartContent);

                var resultObj = JObject.Parse(await result.Content.ReadAsStringAsync());
                var imageLink = resultObj["image"].ToObject<string>();

                return imageLink;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to upload image: {0}", e);
                return "<failed to upload image>";
            }
        }
    }
}
