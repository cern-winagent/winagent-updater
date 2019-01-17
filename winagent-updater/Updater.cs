using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

                    // Download attempts (5)
                    // Download if it fails
                    int retries = 0;
                    if (!Download(dictionary) && retries < 5)
                    {
                        retries++;
                        Console.WriteLine("Failed. Retrying");
                        Download(dictionary);
                    }
                    else
                    {
                        Console.WriteLine("Downloaded");
                    }
                }
            });

            return 0;
        }

        static private Boolean Download(Dictionary<string,string> dictionary)
        {
            var downladClient = new RestClient("https://github.com/cern-winagent/");

            // For each pair -> Download
            foreach (KeyValuePair<string, string> kvp in dictionary)
            {
                // Download from URL
                var downloadRequest = new RestRequest(kvp.Value, Method.GET);

                // Save as filename
                downladClient.DownloadData(downloadRequest).SaveAs(kvp.Key);
            }

            // Check checksum
            foreach (KeyValuePair<string, string> kvp in dictionary)
            {
                // If it's not a checksumfile
                if (Path.GetExtension(kvp.Key)!=".sha1")
                {
                    // Calculate and compare with the pertinent checksum
                    if (CalculateChecksum(kvp.Key) != ReadChecksum(kvp.Key + ".sha1"))
                    {
                        // Fail if checksums doesn't match
                        return false;
                    }
                }
            }
            
            return true;
        }

        static private string CalculateChecksum(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            using (BufferedStream bs = new BufferedStream(fs))
            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                return BitConverter.ToString(cryptoProvider.ComputeHash(bs)).Replace("-", string.Empty).ToLower();
            }
        }

        static private string ReadChecksum(string filePath)
        {
            return File.ReadAllText(filePath).Split(' ')[0];
        }
    }
}