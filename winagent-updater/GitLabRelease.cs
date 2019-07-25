using System.Collections.Generic;
using Newtonsoft.Json;

namespace winagent_updater
{
    class GitLabRelease
    {
        #region Nested class to identify assets
        public class GitLabAsset
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
        public AssetList Assets { get; set; }
    }
}
