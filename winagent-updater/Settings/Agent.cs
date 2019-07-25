using Newtonsoft.Json;
using System.Collections.Generic;

namespace winagent_updater.Settings
{
    class Agent
    {
        [JsonProperty(PropertyName = "autoUpdates")]
        public AutoUpdater AutoUpdates { set; get; }

        [JsonProperty(PropertyName = "inputPlugins")]
        public List<InputPlugin> InputPlugins { set; get; }

        [JsonProperty(PropertyName = "eventLogs")]
        public List<EventLog> EventLogs { set; get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this); ;
        }
    }
}
