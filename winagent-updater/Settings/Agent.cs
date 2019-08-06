using Newtonsoft.Json;
using System.Collections.Generic;

namespace Winagent.Updater.Settings
{
    public class Agent
    {
        [JsonProperty(PropertyName = "autoUpdates")]
        public AutoUpdater AutoUpdates { set; get; }

        [JsonProperty(PropertyName = "inputPlugins")]
        public List<InputPlugin> InputPlugins { set; get; }

        [JsonProperty(PropertyName = "eventLogs")]
        public List<EventLog> EventLogs { set; get; }
    }
}
