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
        // List to store the path to each file
        // Initialized with default files to be updated
        static IEnumerable<string> plugins = new List<string>()
        {
            @".\winagent.exe",
            @".\plugin.dll",
            @".\winagent-updater.exe"
        };

        static void Main(string[] args)
        {
            // Capture general errors
            try
            {
                // Add plugins to be updated
                plugins = plugins.Concat(GetPlugins());

                // Main functionality
                var updates = CheckUpdates();

                // If there are updates to be done
                if(updates.Count > 0)
                {
                    Update(updates);
                }
            }
            catch (Exception e)
            {
                // EventID 0 => General Error
                using (EventLog eventLog = new EventLog("Application"))
                {
                    System.Text.StringBuilder message = new System.Text.StringBuilder("General Error");
                    message.Append(Environment.NewLine);
                    message.Append(e.ToString());
                    eventLog.Source = "WinagentUpdater";
                    eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 0, 1);
                }
            }
            finally
            {
                // EventID 8 => Execution finished
                using (EventLog eventLog = new EventLog("Application"))
                {
                    System.Text.StringBuilder message = new System.Text.StringBuilder("Auto-update execution finished");
                    eventLog.Source = "WinagentUpdater";
                    eventLog.WriteEntry(message.ToString(), EventLogEntryType.Information, 8, 1);
                }
            }
        }

        
        // Get the plugins to be updated from the config file
        static IEnumerable<string> GetPlugins()
        {
            JObject config = JObject.Parse(File.ReadAllText(@"config.json"));

            // List to store unique plugins
            HashSet<string> plugins = new HashSet<string>();

            foreach (JProperty input in ((JObject)config["plugins"]).Properties())
            {
                plugins.Add(@".\plugins\" + input.Name.ToLower() + ".dll");
                foreach (JProperty output in ((JObject)input.Value).Properties())
                {
                    plugins.Add(@".\plugins\" + output.Name.ToLower() + ".dll");
                }
            }
            
            // Return dictionary with the filename and path of the plugins
            //return plugins.ToDictionary(x => x + ".dll", x => @".\plugins\");
            return plugins;
        }


        static IDictionary<string,string> CheckUpdates()
        {
            // Dictionary to store filenames and download URLs
            IDictionary<string, string> toUpdate = new Dictionary<string, string>();

            // Client pointing the cern-winagent repo
            RestClient client = new RestClient("https://api.github.com/repos/cern-winagent/");

            // Pair {plugin/repo name, local folder path}
            foreach (string plugin in plugins) {
                // Request to the latest release
                // Removes the extension to get repo name
                var request = new RestRequest(Path.GetFileNameWithoutExtension(plugin) + "/releases/latest", Method.GET);

                // Request
                IRestResponse response = client.Execute(request);
                if (response.IsSuccessful)
                {
                    // Capture general errors
                    try
                    {
                        // Consume response
                        // Get Info
                        GitHubRelease release = Newtonsoft.Json.JsonConvert.DeserializeObject<GitHubRelease>(response.Content);

                        // Compare Versions
                        // Latest Remote Version
                        var latestVersion = new Version(release.Version);

                        // CurrentVersion
                        Version currentVersion;
                        if (File.Exists(plugin))
                        {
                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(plugin);
                            currentVersion = new Version(versionInfo.FileVersion);
                        }
                        else
                        {
                            currentVersion = new Version("0.0.0");
                        }

                        // If latestVersion is grather than currentVersion
                        if (latestVersion.CompareTo(currentVersion) > 0)
                        {
                            // Add plugin file {local file path, download url} to the dictionary
                            // Merge existing dictionarie with news (file + sha1)
                            // Concat does not return a Dictionary, so .ToDictionary 
                            toUpdate = toUpdate.Concat(
                                release.Files.ToDictionary(
                                    x => Path.GetDirectoryName(plugin) + @"\" + x.Filename, x => x.Url
                                )
                            ).ToDictionary(x => x.Key, x => x.Value);
                        }
                    }
                    catch (Exception e)
                    {
                        // EventID 2 => General Request Error
                        using (EventLog eventLog = new EventLog("Application"))
                        {
                            System.Text.StringBuilder message = new System.Text.StringBuilder("General Request Error");
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
                            message.Append("Exception: ");
                            message.Append(e.ToString());
                            message.Append(Environment.NewLine);
                            message.Append("Response Content: ");
                            message.Append(response.Content);

                            eventLog.Source = "WinagentUpdater";
                            eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 2, 1);
                        }
                    }
                }
                else
                {
                    // EventID 1 => Request failed
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        System.Text.StringBuilder message = new System.Text.StringBuilder("Request failed: ");
                        message.Append(response.StatusCode);
                        message.Append(Environment.NewLine);
                        message.Append("Plugin: ");
                        message.Append(plugin);
                        message.Append(Environment.NewLine);
                        message.Append("RequestURL: ");
                        message.Append(client.BuildUri(request));

                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 1, 1);
                    }
                }
            }

            return toUpdate;
        }

        public static void Update(IDictionary<string, string> toUpdate)
        {
            try
            {
                // Get the service by name
                ServiceController serviceController = new ServiceController("Winagent");

                // Download files
                DownloadFiles(toUpdate);

                // Check integrity
                if (Checksum(toUpdate) == false)
                {
                    // EventID 6 => Checksum failed
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        string message = "Checksum failed";
                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message, EventLogEntryType.Error, 6, 1);
                    }
                }
                else
                {
                    // Remove winagent-updater from dictionary so it is not copied
                    // The updater will be copied brefore the next update
                    toUpdate.Remove(@".\winagent-updater.exe");
                    toUpdate.Remove(@".\winagent-updater.exe.sha1");
                    
                    // If there is something to be updated...
                    if(toUpdate.Count > 0)
                    {
                        // Stop the service
                        if (serviceController.Status == ServiceControllerStatus.Running)
                        {
                            serviceController.Stop();
                            serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(25));
                        }

                        // Files are being copied while winagent.exe is still in use
                        // So wait
                        // TODO: This should be changed at some point
                        Thread.Sleep(5000);
                        CopyFiles(toUpdate);

                        if (serviceController.Status == ServiceControllerStatus.Stopped)
                        {
                            serviceController.Start();
                        }
                    }
                }
            }
            catch (InvalidOperationException ioe)
            {
                // EventID 3 => Service not started
                using (EventLog eventLog = new EventLog("Application"))
                {
                    System.Text.StringBuilder message = new System.Text.StringBuilder("Service not started");
                    message.Append(Environment.NewLine);
                    message.Append(ioe.ToString());

                    eventLog.Source = "WinagentUpdater";
                    eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 3, 1);
                }
            }
        }

        private static void DownloadFiles(IDictionary<string,string> toUpdate)
        {
            var downladClient = new RestClient("https://github.com/cern-winagent/");

            // For each pair -> Download
            foreach (KeyValuePair<string, string> file in toUpdate)
            {
                // Download from URL
                var downloadRequest = new RestRequest(file.Value, Method.GET);

                try
                {
                    // Save as filename
                    // Save in the right folder
                    downladClient.DownloadData(downloadRequest).SaveAs(@".\tmp\" + Path.GetFileName(file.Key));
                }
                catch(ArgumentNullException ane)
                {
                    // EventID 4 => Could not write the file
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        System.Text.StringBuilder message = new System.Text.StringBuilder("File could not be saved: ");
                        message.Append(Environment.NewLine);
                        message.Append(ane.ToString());

                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 4, 1);
                    }
                }
                catch (DirectoryNotFoundException dnf)
                {
                    // EventID 5 => "tmp" Directory not found
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        System.Text.StringBuilder message = new System.Text.StringBuilder("File could not be saved: 'tmp' directory not found");
                        message.Append(Environment.NewLine);
                        message.Append("The directory will be created autimatically");
                        message.Append(Environment.NewLine);
                        message.Append(dnf.ToString());

                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message.ToString(), EventLogEntryType.Information, 5, 1);
                    }

                    // Create 'tmp' directory
                    Directory.CreateDirectory("tmp");

                    // Retry download after the folder creation
                    downladClient.DownloadData(downloadRequest).SaveAs(@".\tmp\" + Path.GetFileName(file.Key));
                }
            }
        }

        private static void CopyFiles(IDictionary<string,string> toUpdate)
        {
            // Copy files from the 'tmp' directory
            // For each pair -> Download
            foreach (KeyValuePair<string, string> file in toUpdate)
            {
                // Do not copy .sha1 files
                if (Path.GetExtension(file.Key) != ".sha1")
                {
                    File.Copy(@".\tmp\" + Path.GetFileName(file.Key), file.Key, true);

                    // EventID 7 => Application updated
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        System.Text.StringBuilder message = new System.Text.StringBuilder("Application updated");
                        message.Append(Environment.NewLine);
                        message.Append(file.Key);
                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message.ToString(), EventLogEntryType.Information, 7, 1);
                    }
                }
            }
        }

        private static Boolean Checksum(IDictionary<string,string> dictionary)
        {
            // Check checksum
            foreach (KeyValuePair<string, string> kpv in dictionary)
            {
                string tempFile = @".\tmp\" + Path.GetFileName(kpv.Key);

                // If it's not a checksumfile
                if (Path.GetExtension(tempFile) != ".sha1")
                {
                    // Calculate and compare with the pertinent checksum
                    if (CalculateChecksum(tempFile) != ReadChecksum(tempFile + ".sha1"))
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
                return BitConverter.ToString(cryptoProvider.ComputeHash(bs)).Replace("-", string.Empty);
            }
        }

        private static string ReadChecksum(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                return reader.ReadLine();
            }
        }
    }
}