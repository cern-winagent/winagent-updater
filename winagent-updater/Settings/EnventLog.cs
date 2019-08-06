using Newtonsoft.Json;
using System.Collections.Generic;

namespace Winagent.Updater.Settings
{
    public class EventLog
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        
        [JsonProperty(PropertyName = "outputPlugins")]
        public List<OutputPlugin> OutputPlugins { get; set; }
    }
}
