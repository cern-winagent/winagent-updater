using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Winagent.Updater.Settings
{
    public class InputPlugin
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "settings")]
        public JObject Settings { get; set; }

        [JsonProperty(PropertyName = "outputPlugins")]
        public List<OutputPlugin> OutputPlugins { get; set; }
    }
}
