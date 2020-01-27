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

using Winagent.Updater.Models;

namespace Winagent.Updater.Model
{
    class Updater
    {
        // List to store the path to each file
        // Initialized with default files to be updated
        static IEnumerable<Assembly> assemblies = new List<Assembly>()
        {
            new Assembly(name: "winagent", type: Assembly.AssemblyType.Executable),
            new Assembly(name: "winagent-updater", type: Assembly.AssemblyType.Executable),
            new Assembly(name: "plugin", type: Assembly.AssemblyType.Dependency)
        };

        static Settings.Agent settings;

        static void Main(string[] args)
        {
            // Capture general errors
            try
            {
                // Read settings file
                 settings = GetSettings();

                // Add plugins to be updated
                assemblies = assemblies.Concat(GetPlugins(settings.InputPlugins, settings.EventLogs, settings.AutoUpdates.AdditionalUpdates));
                                
                // Main functionality
                var updates = CheckUpdates();

                // If there are updates to be done
                if (updates.Count > 0)
                {
                    Update(updates);
                }
            }
            catch (Exception e)
            {
                // EventID 0 => General Error
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
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
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                {
                    System.Text.StringBuilder message = new System.Text.StringBuilder("Auto-update execution finished");
                    eventLog.Source = "WinagentUpdater";
                    eventLog.WriteEntry(message.ToString(), EventLogEntryType.Information, 8, 1);
                }
            }
        }

        /// <summary>
        /// Parse settings
        /// </summary>
        /// <exception cref="FileNotFoundException">Thrown when the settings file could not be found</exception>
        /// <exception cref="Newtonsoft.Json.JsonSerializationException">Thrown when the content of the settings file is incorrect</exception>
        /// <exception cref="Exception">Thrown when a different error occurs</exception>
        internal static Settings.Agent GetSettings(string path = @"config.json")
        {
            try
            {
                // Content of the configuration file "settings.json"
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Settings.Agent>(File.ReadAllText(path));
            }
            catch (FileNotFoundException fnfe)
            {
                // EventID 6 => Could not find settings path
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                {
                    System.Text.StringBuilder message = new System.Text.StringBuilder(String.Format("The specified path \"{0}\" does not appear to be valid", path));
                    message.Append(Environment.NewLine);
                    message.Append(fnfe.ToString());

                    eventLog.Source = "WinagentUpdater";
                    eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 6, 1);
                }
            }
            catch (Newtonsoft.Json.JsonSerializationException jse)
            {
                // EventID 7 => Error in settings file
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                {
                    System.Text.StringBuilder message = new System.Text.StringBuilder(String.Format("The agent could not parse the config file, please check the syntax", path));
                    message.Append(Environment.NewLine);
                    message.Append(jse.ToString());

                    eventLog.Source = "WinagentUpdater";
                    eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 7, 1);
                }
            }
            catch (Exception e)
            {
                // EventID 8 => Error while parsing the settings file
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                {
                    System.Text.StringBuilder message = new System.Text.StringBuilder(String.Format("An undefined error occurred while parsing the config file", path));
                    message.Append(Environment.NewLine);
                    message.Append(e.ToString());

                    eventLog.Source = "WinagentUpdater";
                    eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 8, 1);
                }
            }
            return null;
        }

        // Get the plugins to be updated from the settings file
        static IEnumerable<Assembly> GetPlugins(List<Settings.InputPlugin> scheduler, List<Settings.EventLog> eventLogs, List<string> additionalUpdates)
        {
            // List to store unique plugins
            HashSet<Assembly> plugins = new HashSet<Assembly>();

            // Add all the input plugins configured
            foreach (Settings.InputPlugin input in scheduler)
            {
                plugins.Add(new Assembly
                (
                    name: input.Name.ToLower(),
                    type: Assembly.AssemblyType.Plugin
                ));

                // Add all the output plugins configured for each input
                foreach (Settings.OutputPlugin output in input.OutputPlugins)
                {
                    plugins.Add(new Assembly
                    (
                        name: output.Name.ToLower(),
                        type: Assembly.AssemblyType.Plugin
                    ));
                }
            }

            // Add all the output plugins configured for each EventLog
            foreach (Settings.EventLog eventLog in eventLogs)
            {
                foreach (Settings.OutputPlugin output in eventLog.OutputPlugins)
                {
                    plugins.Add(new Assembly
                    (
                        name: output.Name.ToLower(),
                        type: Assembly.AssemblyType.Plugin
                    ));
                }
            }

            // Add all the plugins configured manually
            if (additionalUpdates != null)
            {
                foreach (string plugin in additionalUpdates)
                {
                    plugins.Add(new Assembly
                    (
                        name: plugin.ToLower(),
                        type: Assembly.AssemblyType.Plugin
                    ));
                }
            }

            return plugins;
        }

        static IDictionary<string, string> CheckUpdates()
        {
            // Dictionary to store filenames and download URLs
            IDictionary<string, string> toUpdate = new Dictionary<string, string>();

            // Client pointing the cern-winagent repo
            RestClient apiClient = new RestClient(settings.AutoUpdates.Uri.GetLeftPart(UriPartial.Authority));

            // Pair {plugin/repo name, local folder path}
            foreach (Assembly plugin in assemblies)
            {
                // Request last release [path of URI]
                // As the GitLab URI has both encoded and decoded part, it is needed to use it 
                // as string in the request, so it is not automatically decoded/encoded 
                var request = new RestRequest(settings.AutoUpdates.Uri.ToString(), Method.GET);
                // Add segment removing the extension to get repo name
                request.AddUrlSegment("plugin", plugin.Name);

                // Request
                IRestResponse response = apiClient.Execute(request);
                if (response.IsSuccessful)
                {
                    // Capture general errors
                    try
                    {
                        // Consume response
                        // Get Info
                        IRelease release;
                        switch (settings.AutoUpdates.Source)
                        {
                            case "github":
                                release = Newtonsoft.Json.JsonConvert.DeserializeObject<GitHubRelease>(response.Content);
                                break;

                            case "gitlab":

                            default:
                                release = Newtonsoft.Json.JsonConvert.DeserializeObject<GitLabRelease>(response.Content);
                                break;
                        }

                        // Compare Versions
                        // Latest Remote Version
                        var latestVersion = new Version(release.Version);

                        // CurrentVersion
                        Version currentVersion;
                        if (File.Exists(plugin.Path))
                        {
                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(plugin.Path);
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
                                    x => Path.GetDirectoryName(plugin.Path) + @"\" + x.Filename, x => x.Url
                                )
                            ).ToDictionary(x => x.Key, x => x.Value);
                        }
                    }
                    catch (Exception e)
                    {
                        // EventID 2 => General Request Error
                        using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                        {
                            System.Text.StringBuilder message = new System.Text.StringBuilder("General Request Error");
                            message.Append(Environment.NewLine);
                            message.Append("Plugin: ");
                            message.Append(plugin.Name);
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
                    using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                    {
                        System.Text.StringBuilder message = new System.Text.StringBuilder("Request failed: ");
                        message.Append(response.StatusCode);
                        message.Append(Environment.NewLine);
                        message.Append("Plugin: ");
                        message.Append(plugin.Name);
                        message.Append(Environment.NewLine);
                        message.Append("RequestURL: ");
                        message.Append(apiClient.BuildUri(request));
                        message.Append(Environment.NewLine);
                        message.Append("Response Content: ");
                        message.Append(response.Content);

                        eventLog.Source = "WinagentUpdater";
                        eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 1, 1);
                    }
                }
            }

            return toUpdate;
        }
       
        private static void Update(IDictionary<string, string> toUpdate)
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
                    using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
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

                        // Files are being copied while winagent.exe is still in use (serviceController.WaitForStatus is not actually waiting)
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
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
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
            // RestClient needs an URI, it's ignored if a full URI is specified in the request
            // <seealso> https://github.com/restsharp/RestSharp/issues/606
            // This solution is general for both of the current sources (gitlab/github)
            var downladClient = new RestClient("https://bar.foo");

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
                    using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
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
                    using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
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
                    try
                    {
                        File.Copy(@".\tmp\" + Path.GetFileName(file.Key), file.Key, true);

                        // EventID 7 => Application updated
                        using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                        {
                            System.Text.StringBuilder message = new System.Text.StringBuilder("Application updated");
                            message.Append(Environment.NewLine);
                            message.Append(file.Key);
                            eventLog.Source = "WinagentUpdater";
                            eventLog.WriteEntry(message.ToString(), EventLogEntryType.Information, 7, 1);
                        }
                    }
                    catch(Exception e)
                    {
                        // EventID 9 => Application updated
                        using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                        {
                            System.Text.StringBuilder message = new System.Text.StringBuilder("An error ocurred while copying the file");
                            message.Append(Environment.NewLine);
                            message.Append(file.Key);
                            eventLog.Source = "WinagentUpdater";
                            eventLog.WriteEntry(message.ToString(), EventLogEntryType.Error, 9, 1);
                        }
                    }
                }
                else
                {
                    File.Delete(file.Key);
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