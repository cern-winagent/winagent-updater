using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Winagent.Updater.Settings
{
    public class OutputPlugin
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "settings")]
        public JObject Settings { get; set; }

        [JsonProperty(PropertyName = "schedule")]
        public Schedule Schedule { get; set; }
    }
}
