using System.Collections.Generic;
using Newtonsoft.Json;

namespace Winagent.Updater.Models
{
    class GitLabRelease : IRelease
    {
        #region Nested class to identify assets
        public class GitLabAsset : IAsset
        {
            [JsonProperty(PropertyName = "name")]
            public string Filename { get; set; }

            [JsonProperty(PropertyName = "url")]
            public string Url { get; set; }
        }
        #endregion

        #region Nested class to identify asset lists
        public class AssetList
        {
            [JsonProperty(PropertyName = "links")]
            public List<GitLabAsset> Files { get; set; }

            [JsonProperty(PropertyName = "count")]
            public string Count { get; set; }
        }
        #endregion

        [JsonProperty(PropertyName = "name")]
        public string Version { get; set; }

        [JsonProperty(PropertyName = "assets")]
        public AssetList Assets { private get; set; }

        public List<IAsset> Files
        {
            get => new List<IAsset>(Assets.Files);
        }
    }
}
