using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace MondBot.Master
{
    /*[Controller]
    public sealed class BotFrameworkController : Controller
    {
        private readonly HttpClient _client;

        public BotFrameworkController()
        {
            _client = new HttpClient();    
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] dynamic activity)
        {
            var activityType = (string)activity.type;
            if (activityType != "message")
                return Ok();

            // Get the conversation id so the bot answers.
            var conversationId = (string)activity.from.id;

            // Get a valid token 
            string token = await GetBotApiToken();
            
            // Set the toekn in the authorization header.
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // I'm using dynamic here to make the code simpler
            var message = new
            {
                type = "message/text",
                text = activity.text
            };

            var messageStr = JsonConvert.SerializeObject(message);

            // Post the message
            await _client.PostAsync(
                $"https://api.skype.net/v3/conversations/{conversationId}/activities",
                new StringContent(messageStr, Encoding.UTF8, "text/json"));

            return Ok();
        }

        /// <summary>
        /// Gets and caches a valid token so the bot can send messages.
        /// </summary>
        /// <returns>The token</returns>
        private async Task<string> GetBotApiToken()
        {
            // Check to see if we already have a valid token
            string token = memoryCache.Get("token")?.ToString();
            if (string.IsNullOrEmpty(token))
            {
                // we need to get a token.
                using (var client = new HttpClient())
                {
                    // Create the encoded content needed to get a token
                    var parameters = new Dictionary<string, string>
                    {
                        {"client_id", this.botCredentials.ClientId },
                        {"client_secret", this.botCredentials.ClientSecret },
                        {"scope", "https://graph.microsoft.com/.default" },
                        {"grant_type", "client_credentials" }
                    };
                    var content = new FormUrlEncodedContent(parameters);

                    // Post
                    var response = await client.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", content);

                    // Get the token response
                    var tokenResponse = await response.Content.ReadAsAsync<TokenResponse>();

                    token = tokenResponse.access_token;

                    // Cache the token for 15 minutes.
                    memoryCache.Set(
                        "token",
                        token,
                        new DateTimeOffset(DateTime.Now.AddMinutes(15)));
                }
            }

            return token;
        }
    }*/
}
