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

using winagent_updater.Models;

namespace winagent_updater
{
    class Updater
    {
        // List to store the path to each file
        // Initialized with default files to be updated
        static IEnumerable<Assembly> assemblies = new List<Assembly>()
        {
            new Assembly{
                Name = "winagent",
                Type = Assembly.AssemblyType.Executable
            },
            new Assembly{
                Name = "winagent-updater",
                Type = Assembly.AssemblyType.Executable
            },
            new Assembly{
                Name = "plugin",
                Type = Assembly.AssemblyType.Dependency
            },
        };
        
        static string source;

        static void Main(string[] args)
        {
            // Capture general errors
            try
            {
                // Read settings file
                Settings.Agent settings = GetSettings();

                // Add plugins to be updated
                assemblies = assemblies.Concat(GetPlugins(settings.InputPlugins, settings.EventLogs));

                // Check remote
                source = settings.AutoUpdates.Source;

                // Main functionality
                var updates = CheckUpdates(source, settings.AutoUpdates.Uri);
                foreach (KeyValuePair<string, string> a in updates)
                {
                    Console.WriteLine(a.Key);
                    Console.WriteLine(a.Value);

                }
                // If there are updates to be done
                if (updates.Count > 0)
                {
///////////////////                    //Update(updates);
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
                // EventID 6 => Service not started
                using (EventLog eventLog = new EventLog("Application"))
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
                using (EventLog eventLog = new EventLog("Application"))
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
                using (EventLog eventLog = new EventLog("Application"))
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
        static IEnumerable<Assembly> GetPlugins(List<Settings.InputPlugin> scheduler, List<Settings.EventLog> eventLogs)
        {
            // List to store unique plugins
            HashSet<Assembly> plugins = new HashSet<Assembly>();

            foreach (Settings.InputPlugin input in scheduler)
            {
                plugins.Add(new Assembly
                {
                    Name = input.Name.ToLower(),
                    Type = Assembly.AssemblyType.Plugin
                });
                foreach (Settings.OutputPlugin output in input.OutputPlugins)
                {
                    plugins.Add(new Assembly
                    {
                        Name = output.Name.ToLower(),
                        Type = Assembly.AssemblyType.Plugin
                    });
                }
            }

            foreach (Settings.EventLog eventLog in eventLogs)
            {
                foreach (Settings.OutputPlugin output in eventLog.OutputPlugins)
                {
                    plugins.Add(new Assembly
                    {
                        Name = output.Name.ToLower(),
                        Type = Assembly.AssemblyType.Plugin
                    });
                }
            }

            return plugins;
        }

        static IDictionary<string, string> CheckUpdates(string source, Uri uri)
        {
            // Dictionary to store filenames and download URLs
            IDictionary<string, string> toUpdate = new Dictionary<string, string>();

            // Client pointing the cern-winagent repo
            RestClient apiClient = new RestClient(uri.GetLeftPart(UriPartial.Authority));

            // Pair {plugin/repo name, local folder path}
            foreach (Assembly plugin in assemblies)
            {
                // Request last release [path of URI]
                // As the GitLab URI has both encoded and decoded part, it is needed to use it 
                // as string in the request, so it is not automatically decoded/encoded 
                var request = new RestRequest(uri.ToString(), Method.GET);
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
                        switch (source)
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
                        message.Append(apiClient.BuildUri(request));

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