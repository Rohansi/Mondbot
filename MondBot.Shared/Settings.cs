using System.IO;
using Newtonsoft.Json;

namespace MondBot.Shared
{
    public class Settings
    {
        private static Settings _instance;

        public static Settings Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("Settings.json"));
                return _instance;
            }
        }
        [JsonProperty(Required = Required.Always)]
        public string DbAddress { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int DbPort { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string DbName { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string DbUsername { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string DbPassword { get; set; }
    }
}
