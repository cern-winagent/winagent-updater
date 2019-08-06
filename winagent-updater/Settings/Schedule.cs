using Newtonsoft.Json;

namespace Winagent.Updater.Settings
{
    public class Schedule
    {
        [JsonProperty(PropertyName = "hours")]
        public int Hours { get; set; }

        [JsonProperty(PropertyName = "minutes")]
        public int Minutes { get; set; }

        [JsonProperty(PropertyName = "seconds")]
        public int Seconds { get; set; }

        // Calculates time in ms
        public int GetTime()
        {
            return Hours * 3600000 + Minutes * 60000 + Seconds * 1000;
        }
    }
}
