using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Threading;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Extensions;

namespace winagent_updater
{
    class Updater
    {
        // String list initialized with default plugin
        static IEnumerable<string> plugins = new List<string>() { "plugin" };

        static void Main(string[] args)
        {
            // Delay before the first update
            // This avoids errors when the service starts
            Thread.Sleep(10000);

            // Add plugins to be updated
            plugins.Concat(GetPlugins());

            while (true)
            {
                // Main functionality
                CheckUpdates();

                // Delay betweeen each check
                Thread.Sleep(30000);
            }
        }

        
        // Get the plugins to be updated from the config file
        static IEnumerable<string> GetPlugins()
        {
            JObject config = JObject.Parse(File.ReadAllText(@"config.json"));
            HashSet<string> plugins = new HashSet<string>();

            foreach (JProperty input in ((JObject)config["input"]).Properties())
            {
                plugins.Add(input.Name);
                foreach (JProperty output in ((JObject)input.Value).Properties())
                {
                    plugins.Add(output.Name);
                }
            }

            return plugins;
        }


        static void CheckUpdates()
        {
            // Client pointing the cern-winagent repo
            var client = new RestClient("https://api.github.com/repos/cern-winagent/");

            foreach (string plugin in plugins) {
                // Request to the latest release
                var request = new RestRequest(plugin + "/releases/latest", Method.GET);

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
                        using (EventLog eventLog = new EventLog("Application"))
                        {
                            // EventID 2 => General error
                            System.Text.StringBuilder message = new System.Text.StringBuilder("General Error");
                            message.Append(Environment.NewLine);
                            message.Append("Plugin: ");
                            message.Append(plugin);
                            message.Append(Environment.NewLine);
                            message.Append("Response StatusCode: ");
                            message.Append(response.StatusCode);
                            message.Append(Environment.NewLine);
                            message.Append("Error Message: ");
                            message.Append(response.ErrorMessage);
                            message.Append(Environment.NewLine);
                            message.Append("Content: ");
                            message.Append(response.Content);

                            eventLog.Source = "WinagentUpdater";
                            eventLog.WriteEntry("General error", EventLogEntryType.Error, 2, 1);
                            eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 2, 1);
                        }
                    }
                }
                else
                {
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        // EventID 1 => Request failed
                        System.Text.StringBuilder message = new System.Text.StringBuilder("Request failed: ");
                        message.Append(response.StatusCode);
                        message.Append(Environment.NewLine);
                        message.Append("Plugin: ");
                        message.Append(plugin);

                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 1, 1);
                    }
                }
            }
        }

        private static void ProcessResponse(string responseContent)
        {

            // Get Info
            GitHubRelease release = Newtonsoft.Json.JsonConvert.DeserializeObject<GitHubRelease>(responseContent);

            // Compare Versions
            // Latest Version

            //TODO: Remove test version
            //var latestVersion = new Version(release.Version);
            var latestVersion = new Version("5.0.0");

            // CurrentVersion
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(@"plugin.dll");
            var currentVersion = new Version(versionInfo.FileVersion);
            
            // If latestVersion is grather than currentVersion
            if(latestVersion.CompareTo(currentVersion) > 0)
            {
                // Create dictionary with filenames and download URLs
                var dictionary = release.Files.ToDictionary(x => x.Filename, x => x.Url);

                /* TODO: For a 5 days test (every 40 seconds) it failed with
                 *  Service not found: 
                 *  System.InvalidOperationException: Cannot stop Winagent service on computer '.'. ---System.ComponentModel.Win32Exception: The service has not been started
                 *      --- End of inner exception stack trace ---
                 *      at System.ServiceProcess.ServiceController.Stop()
                 *     at winagent_updater.Updater.ProcessResponse(String responseContent)
                 *
                 * EVEN WITH THE TRY-CATCH
                 */

                try
                {
                    // Get the service by name
                    ServiceController serviceController = new ServiceController("Winagent");
                
                    // Stop the service
                    serviceController.Stop();
                
                    // Download files
                    Download(dictionary);

                    // TODO: Use checksum for something [it returns bool]
                    // Check integrity
                    Checksum(dictionary);

                    // Start the service
                    serviceController.Start();
                }
                catch (Exception e)
                {
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        // EventID 3 => Service not started
                        System.Text.StringBuilder message = new System.Text.StringBuilder("Service not started: ");
                        message.Append(Environment.NewLine);
                        message.Append(e.ToString());

                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 3, 1);
                    }
                }
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
                // Save in the right folder

                try
                {
                    downladClient.DownloadData(downloadRequest).SaveAs(kvp.Key);
                }
                catch(ArgumentNullException ane)
                {
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        // EventID 4 => Could not write the file
                        System.Text.StringBuilder message = new System.Text.StringBuilder("File could not be saved: ");
                        message.Append(Environment.NewLine);
                        message.Append(ane.ToString());

                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 4, 1);
                    }
                }
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