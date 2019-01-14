using System;
using System.Collections.Generic;
using System.Linq;
using RestSharp;
using RestSharp.Extensions;
namespace winagent_updater
{
    class Updater
    {
        static void Main(string[] args)
        {
            CheckUpdates();
            Console.ReadKey();
        }

        static int CheckUpdates()
        {
            var client = new RestClient("https://api.github.com/repos/cern-winagent/");
            var downladClient = new RestClient("https://github.com/cern-winagent/");

            // var request = new RestRequest("resource/{id}", Method.POST);
            var request = new RestRequest("plugin/releases/latest", Method.GET);

            // Async request
            client.ExecuteAsync(request, response => {

                // Get Info
                GitHubRelease release = Newtonsoft.Json.JsonConvert.DeserializeObject<GitHubRelease>(response.Content);
                Console.WriteLine(release.Version);

                // Compare Versions
                // Latest Version
                var latestVersion = new Version(release.Version);

                // CurrentVersion
                var currentVersion = new Version("0.0.1");

                // If latestVersion is grather than currentVersion
                if(latestVersion.CompareTo(currentVersion) > 0)
                {
                    // Create dictionary with filenames and download URLs
                    var dictionary = release.Files.ToDictionary(x => x.Filename, x => x.Url);

                    // For each pair
                    foreach (KeyValuePair<string, string> kvp in dictionary)
                    {
                        // Download from URL
                        var downloadRequest = new RestRequest(kvp.Value, Method.GET);

                        // Save as filename
                        downladClient.DownloadData(downloadRequest).SaveAs(kvp.Key);
                    }
                }
            });

            return 0;
        }
    }
}