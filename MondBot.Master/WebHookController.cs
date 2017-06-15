using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MondBot.Master
{
    [Route("WebHook")]
    public class WebHookController : Controller
    {
        private const string Service = "telegram";

        private static TelegramBotClient Bot => MasterProgram.TelegramBot;

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Update update)
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

            /*if (message.Type != MessageType.PhotoMessage)
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

            await Bot.SendTextMessageAsync(message.Chat.Id, "thanks");*/
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

                /*case "boop":
                    await SendRandomPhoto(message.Chat.Id);
                    break;

                case "count":
                    await SendPhotoCount(message.Chat.Id);
                    break;*/

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

        /*private static async Task SendRandomPhoto(long chatId)
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
        }*/

        private static async Task RunMondScript(Message message, string code)
        {
            var (image, result) = await Common.RunScript(Service, message.From.Id.ToString("G"), message.GetUsername(), code);

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
            var (result, isCode) = await Common.AddMethod(Service, message.From.Id.ToString("G"), message.GetUsername(), parameters);
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

            await SendMessage(message, data.Truncated(), true);
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

        private static readonly Regex CommandRegex = new Regex(@"[/]+([a-z]+)");
        public static string CleanupCommand(string command)
        {
            return CommandRegex.Match(command).Groups[1].Value.ToLower();
        }
    }
}
