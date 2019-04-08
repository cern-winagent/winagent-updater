using System.Collections.Generic;
using Newtonsoft.Json;

namespace winagent_updater
{
    class GitHubRelease
    {
        #region Nested class to identify assets
        public class GitHubAsset
        {
            [JsonProperty(PropertyName = "name")]
            public string Filename { get; set; }
        
            [JsonProperty(PropertyName = "browser_download_url")]
            public string Url { get; set; }
        }
        #endregion

        [JsonProperty(PropertyName = "tag_name")]
        public string Version { get; set; }

        [JsonProperty(PropertyName = "assets")]
        public List<GitHubAsset> Files { get; set; }
    }
}
