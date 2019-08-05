using System.Collections.Generic;
using Newtonsoft.Json;

namespace Winagent.Updater.Models
{
    class GitHubRelease : IRelease
    {
        #region Nested class to identify assets
        public class GitHubAsset : IAsset
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
        public List<GitHubAsset> Assets{ get; set; }

        public List<IAsset> Files
        {
            get => new List<IAsset>(Assets);
        }
    }
}
