using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Threading;
using RestSharp;
using RestSharp.Extensions;

namespace winagent_updater
{
    class Updater
    {
        static void Main(string[] args)
        {
            while (true)
            {
                // Delay before the first update
                // This avoids errors when the service starts
                Thread.Sleep(10000);

                // Main functionality
                CheckUpdates();

                // Delay betweeen each check
                Thread.Sleep(30000);
            }
        }

        // TODO: Check if the process is present before stop it

        static void CheckUpdates()
        {
            // Client pointing the cern-winagent repo
            var client = new RestClient("https://api.github.com/repos/cern-winagent/");

            // Request to the latest release
            var request = new RestRequest("plugin/releases/latest", Method.GET);
            
            // Request
            IRestResponse response = client.Execute(request);
            if (response.IsSuccessful)
            {
                // Capture general errors
                try
                {
                    // Consume response
                    ProcessResponse(response.Content);
                }
                catch
                {
                    using (EventLog eventLog = new EventLog("General error"))
                    {
                        // EventID 2 => General error
                        eventLog.Source = "Winagent Updater";
                        eventLog.WriteEntry("General error", EventLogEntryType.Error, 2, 1);
                        eventLog.WriteEntry("Response StatusCode: " + response.StatusCode, EventLogEntryType.Error, 2, 1);
                        eventLog.WriteEntry("Error Message: " + response.ErrorMessage, EventLogEntryType.Error, 2, 1);
                        eventLog.WriteEntry("Error Exception: " + response.ErrorException, EventLogEntryType.Error, 2, 1);
                        eventLog.WriteEntry("Content: " + response.Content, EventLogEntryType.Error, 2, 1);
                    }
                }
            }
            else
            {
                using (EventLog eventLog = new EventLog("Request failed"))
                {
                    // EventID 1 => Request failed
                    eventLog.Source = "Winagent Updater";
                    eventLog.WriteEntry("Request failed", EventLogEntryType.Error, 1, 1);
                    eventLog.WriteEntry(response.ErrorMessage, EventLogEntryType.Error, 1, 1);
                }
            }
        }

        private static void ProcessResponse(string responseContent)
        {

            // Get Info
            GitHubRelease release = Newtonsoft.Json.JsonConvert.DeserializeObject<GitHubRelease>(responseContent);
            Console.WriteLine(release.Version);

            // Compare Versions
            // Latest Version
            var latestVersion = new Version(release.Version);

            // CurrentVersion
            // TODO: Get the correct version
            var currentVersion = new Version("0.0.1");

            // If latestVersion is grather than currentVersion
            if(latestVersion.CompareTo(currentVersion) > 0)
            {
                // Create dictionary with filenames and download URLs
                var dictionary = release.Files.ToDictionary(x => x.Filename, x => x.Url);

                // TODO: Catch if it does not exist
                // Get the service by name
                ServiceController serviceController = new ServiceController("Winagent");
                
                // Stop the service
                serviceController.Stop();
                
                // Download files
                Download(dictionary);

                // Check integrity
                Checksum(dictionary);

                // Start the service
                serviceController.Start();
            }
        }

        private static void Download(Dictionary<string,string> dictionary)
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
        }

        private static Boolean Checksum(Dictionary<string,string> dictionary)
        {

            // Check checksum
            foreach (KeyValuePair<string, string> kvp in dictionary)
            {
                // If it's not a checksumfile
                if (Path.GetExtension(kvp.Key) != ".sha1")
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

        private static string CalculateChecksum(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            using (BufferedStream bs = new BufferedStream(fs))
            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                return BitConverter.ToString(cryptoProvider.ComputeHash(bs)).Replace("-", string.Empty).ToLower();
            }
        }

        private static string ReadChecksum(string filePath)
        {
            return File.ReadAllText(filePath).Split(' ')[0];
        }
    }
}