using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace winagent_updater.Settings
{
    class EventLog
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        
        [JsonProperty(PropertyName = "outputPlugins")]
        public List<OutputPlugin> OutputPlugins { get; set; }
    }
}
