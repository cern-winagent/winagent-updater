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
            // client.Authenticator = new HttpBasicAuthenticator(username, password);

            // var request = new RestRequest("resource/{id}", Method.POST);
            var request = new RestRequest("plugin/releases/latest", Method.GET);

            // Async request
            client.ExecuteAsync(request, response => {
                GitHubRelease release = Newtonsoft.Json.JsonConvert.DeserializeObject<GitHubRelease>(response.Content);
                Console.WriteLine(release.Version);
                if (true)
                {
                    foreach (GitHubRelease.GitHubAsset file in release.Files)
                    {
                        var downloadRequest = new RestRequest("https://github.com/cern-winagent/plugin/releases/download/v1.0.0/plugin.dll", Method.GET);

                        downladClient.DownloadData(downloadRequest).SaveAs(file.Filename);
                    }
                }
                Console.WriteLine(release.Files[0].Filename);

                var dictionary = release.Files.ToDictionary(x => x.Filename, x => x.Url);
                //Console.WriteLine(dictionary.Keys);
                //foreach (string a in response.Data.Assets)
                //Console.WriteLine(a);
                foreach (KeyValuePair<string, string> kvp in dictionary)
                {
                    //textBox3.Text += ("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
                    Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
                }
            });
                       
            //https://github.com/cern-winagent/plugin/releases/latest

            return 0;
        }
    }
}