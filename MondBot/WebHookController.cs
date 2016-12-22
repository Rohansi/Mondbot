using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MondBot
{
    public class WebHookController : ApiController
    {
        private static TelegramBotClient Bot => Program.Bot;

        public async Task<HttpResponseMessage> Get()
        {
            var result = await RunModule.Run("Rohansi", @"
const red = Color(255, 0, 0);
const green = Color(0, 255, 0);
const blue = Color(0, 0, 255);
const black = Color(0, 0, 0);
const white = Color(255, 255, 255);

Image.clear(black);
Image.drawRectangle(20, 20, Image.getWidth() - 40, Image.getHeight() - 40, red, 10);
Image.drawString(""Hello, world!"", 100, 100, white);
Image.fillEllipse(100, 200, 75, 100, blue);
Image.drawLine(125, 290, 100, 400, white, 5);
");


            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(result.Image);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        }

        public async Task<IHttpActionResult> Post(Update update)
        {
            if (update.Type == UpdateType.MessageUpdate)
            {
                await OnMessageHandler(update.Message);
            }
            return Ok();
        }

        private static async Task OnMessageHandler(Message message)
        {
            try
            {
                if (message.Chat.Type == ChatType.Private)
                {
                    await OnPrivateMessageHandler(message);
                }
                else
                {
                    await OnGroupMessageHandler(message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static async Task OnPrivateMessageHandler(Message message)
        {
            if (await HandleCommands(message))
                return;

            if (message.Type != MessageType.PhotoMessage)
            {
                await Bot.SendTextMessageAsync(message.Chat.Id, "send pics plz");
                return;
            }

            var photo = message.GetPhoto();
            var image = await Bot.GetFileAsync(photo.FileId);

            var imageData = new byte[image.FileStream.Length];
            await image.FileStream.ReadAsync(imageData, 0, imageData.Length);

            var cmd =
                new SqlCommand(
                    @"INSERT INTO mondbot.images (chat_id, sender_id, date, image, file_id) VALUES (:chatId, :senderId, :date, :image, :fileId);")
                {
                    ["chatId"] = message.Chat.Id,
                    ["senderId"] = message.From.Id,
                    ["date"] = message.Date,
                    ["image"] = imageData,
                    ["fileId"] = photo.FileId
                };

            using (cmd)
            {
                await cmd.ExecuteNonQuery();
            }

            await Bot.SendTextMessageAsync(message.Chat.Id, "thanks");
        }

        private static async Task OnGroupMessageHandler(Message message)
        {
            await HandleCommands(message);
        }

        private static async Task<bool> HandleCommands(Message message)
        {
            if (message.Type != MessageType.TextMessage)
                return false;

            var commandEntity = message.Entities.FirstOrDefault(e => e.Type == MessageEntityType.BotCommand);
            if (commandEntity == null)
                return false;

            var text = message.Text;
            var commandText = CleanupCommand(text.Substring(commandEntity.Offset, commandEntity.Length));
            var remainingText = text.Substring(commandEntity.Offset + commandEntity.Length);

            switch (commandText)
            {
                case "help":
                case "f1":
                    await Bot.SendTextMessageAsync(message.Chat.Id, "NO HELP");
                    break;

                case "boop":
                    await SendRandomPhoto(message.Chat.Id);
                    break;

                case "count":
                    await SendPhotoCount(message.Chat.Id);
                    break;

                case "run":
                    await RunMondScript(message, remainingText);
                    break;

                case "method":
                    await AddMondMethod(message, remainingText);
                    break;

                case "view":
                    await ViewMondVariable(message, remainingText);
                    break;

                default:
                    return false;
            }

            return true;
        }

        private static async Task SendRandomPhoto(long chatId)
        {
            var cmd =
                new SqlCommand(
                    @"SELECT * FROM mondbot.images OFFSET floor(random() * (SELECT COUNT(*) FROM mondbot.images)) LIMIT 1;");
            using (cmd)
            {
                var result = (await cmd.Execute()).SingleOrDefault();

                if (result == null)
                {
                    await Bot.SendTextMessageAsync(chatId, "I need more images!");
                    return;
                }

                if (result.file_id != null)
                {
                    await Bot.SendPhotoAsync(chatId, result.file_id);
                }
                else
                {
                    var stream = new MemoryStream((byte[])result.image);
                    var message = await Bot.SendPhotoAsync(chatId, new FileToSend("photo.jpg", stream));

                    var photo = message.GetPhoto();
                    if (photo == null)
                    {
                        Console.WriteLine("Failed to upgrade row! No file ID given.");
                        return;
                    }

                    var update =
                        new SqlCommand(@"UPDATE mondbot.images SET file_id = :fileId WHERE image_id = :imageId;")
                        {
                            ["imageId"] = result.image_id,
                            ["fileId"] = photo.FileId
                        };
                    using (update)
                    {
                        await update.ExecuteNonQuery();
                    }

                    Console.WriteLine("Updated row with file ID!");
                }
            }
        }

        private static async Task SendPhotoCount(long chatId)
        {
            var cmd = new SqlCommand(@"SELECT COUNT(*) FROM mondbot.images;");
            using (cmd)
            {
                var result = (long)await cmd.ExecuteScalar();
                await Bot.SendTextMessageAsync(chatId, $"There are {result} pictures in my database.");
            }
        }

        private static async Task RunMondScript(Message message, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return;

            var result = await RunModule.Run(message.GetUsername(), code + ";");

            if (result.Image != null && result.Image.Length > 0)
            {
                try
                {
                    var stream = new MemoryStream(result.Image);
                    await Bot.SendPhotoAsync(message.Chat.Id, new FileToSend("photo.png", stream), replyToMessageId: message.MessageId);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to send image: {e}");
                }
            }

            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                var resultEncoded = WebUtility.HtmlEncode(result.Output);
                var resultHtml = "<pre>" + resultEncoded + "</pre>";
                await Bot.SendTextMessageAsync(message.Chat.Id, resultHtml, parseMode: ParseMode.Html, replyToMessageId: message.MessageId);
            }
        }

        private static readonly Regex AddCommandRegex = new Regex(@"^\s*(\w+|[\.\=\+\-\*\/\%\&\|\^\~\<\>\!\?]+)\s+(.+)$", RegexOptions.Singleline);
        private static async Task AddMondMethod(Message message, string parameters)
        {
            var match = AddCommandRegex.Match(parameters);
            if (!match.Success)
            {
                await Bot.SendTextMessageAsync(message.Chat.Id, "Usage: /method <name> <code>", replyToMessageId: message.MessageId);
                return;
            }

            var name = match.Groups[1].Value;
            var code = match.Groups[2].Value;

            var result = (await RunModule.Run(message.GetUsername(), $"print({code});")).Output;

            if (result.StartsWith("ERROR:") || result.StartsWith("EXCEPTION:") || result.StartsWith("mondbox"))
            {
                var resultEncoded = "<pre>" + WebUtility.HtmlEncode(result) + "</pre>";
                await Bot.SendTextMessageAsync(message.Chat.Id, resultEncoded, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
                return;
            }

            if (result != "function")
            {
                await Bot.SendTextMessageAsync(message.Chat.Id, "Method code must actually be a method!", replyToMessageId: message.MessageId);
                return;
            }

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

            await Bot.SendTextMessageAsync(message.Chat.Id, "Successfully updated method!", replyToMessageId: message.MessageId);
        }

        private static async Task ViewMondVariable(Message message, string name)
        {
            var cmd = new SqlCommand(@"SELECT * FROM mondbot.variables WHERE name = :name;")
            {
                ["name"] = name.Trim()
            };

            using (cmd)
            {
                var result = (await cmd.Execute()).SingleOrDefault();

                if (result == null)
                {
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Variable doesn't exist!", replyToMessageId: message.MessageId);
                    return;
                }

                string data = result.data;
                var dataEncoded = "<pre>" + WebUtility.HtmlEncode(data) + "</pre>";
                await Bot.SendTextMessageAsync(message.Chat.Id, dataEncoded, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
            }
        }

        private static readonly Regex CommandRegex = new Regex(@"[/]+([a-z]+)");
        private static string CleanupCommand(string command)
        {
            return CommandRegex.Match(command).Groups[1].Value.ToLower();
        }
    }
}
