using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MondBot
{
    public class WebHookController : ApiController
    {
        private static TelegramBotClient Bot => Program.TelegramBot;

        public async Task<HttpResponseMessage> Get(string type)
        {
            const string imageTest = @"
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

return Json.serialize(green);";

            const string rantTest = @"return Rant.run(""<verb> me pls"");";

            var result = await RunModule.Run("Rohansi", rantTest);

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (type == "image")
            {
                response.Content = new ByteArrayContent(result.Image);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            }
            else
            {
                response.Content = new StringContent(result.Output);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            }
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
            var commandText = Common.CleanupCommand(text.Substring(commandEntity.Offset, commandEntity.Length));
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
            var (image, result) = await Common.RunScript(message.GetUsername(), code);

            if (image != null)
            {
                try
                {
                    var stream = new MemoryStream(image);
                    await Bot.SendPhotoAsync(message.Chat.Id, new FileToSend("photo.png", stream), replyToMessageId: message.MessageId);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to send image: {e}");
                }
            }

            if (!string.IsNullOrWhiteSpace(result))
                await SendMessage(message, result, true);
        }

        private static async Task AddMondMethod(Message message, string parameters)
        {
            var (result, isCode) = await Common.AddMethod(message.GetUsername(), parameters);
            await SendMessage(message, result, isCode);
        }

        private static async Task ViewMondVariable(Message message, string name)
        {
            var data = await Common.ViewVariable(name);

            if (data == null)
            {
                await SendMessage(message, "Variable doesn't exist!");
                return;
            }

            await SendMessage(message, data, true);
        }

        private static async Task SendMessage(Message replyTo, string text, bool isCode = false)
        {
            if (isCode)
            {
                var resultEncoded = "<pre>" + WebUtility.HtmlEncode(text) + "</pre>";
                await Bot.SendTextMessageAsync(replyTo.Chat.Id, resultEncoded, replyToMessageId: replyTo.MessageId, parseMode: ParseMode.Html);
            }
            else
            {
                await Bot.SendTextMessageAsync(replyTo.Chat.Id, text, replyToMessageId: replyTo.MessageId);
            }
        }
    }
}
