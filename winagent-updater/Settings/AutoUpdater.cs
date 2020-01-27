using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winagent.Updater.Models;

namespace Winagent.Updater.Settings
{
    public class AutoUpdater
    {
        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; }

        [JsonProperty(PropertyName = "source")]
        public string Source { get; set; }

        [JsonProperty(PropertyName = "uri")]
        public Uri Uri { get; set; }

        [JsonProperty(PropertyName = "schedule")]
        public Schedule Schedule { get; set; }

        [JsonProperty(PropertyName = "additionalUpdates")]
        public List<string> AdditionalUpdates { get; set; }
    }
}
