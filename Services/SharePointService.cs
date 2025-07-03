using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Newtonsoft.Json.Linq;

namespace ManutMap.Services
{
    public class SharePointService
    {
        private readonly GraphServiceClient _client;
        private const string TenantId = "3b08e64e-b3be-402b-bb26-1fa4f91cf61f";
        private const string ClientId = "3cffac6a-f9d9-42d1-9065-4054fcd40163";
        private const string ClientSecret = "JFd8Q~hHgTYYo0P0EjAM8mpe3xm3.5vTfCHRFc.T";
        private const string SiteHostname = "oneengenharia.sharepoint.com";
        private const string SitePath = "/sites/OneEngenharia";
        private const string LibraryName = "ArquivosJSON";
        // Prefixo utilizado pelos arquivos de manutenção
        private const string FilePrefix = "Manutencao_";

        public DateTime LastUpdate { get; private set; }

        public SharePointService()
        {
            var cred = new ClientSecretCredential(TenantId, ClientId, ClientSecret);
            _client = new GraphServiceClient(cred);
        }

        public async Task<JArray> DownloadLatestJsonAsync()
        {
            var site = await _client.Sites[$"{SiteHostname}:{SitePath}:"].GetAsync();
            var drives = await _client.Sites[site.Id].Drives.GetAsync();
            var drive = drives.Value.FirstOrDefault(d =>
                d.Name.Equals(LibraryName, StringComparison.OrdinalIgnoreCase));
            if (drive == null) throw new InvalidOperationException($"Biblioteca '{LibraryName}' não encontrada.");

            var pg = await _client.Drives[drive.Id].Items["root"].Children.GetAsync();
            var all = pg.Value.ToList();

            while (pg.OdataNextLink is string next)
            {
                var req = new Microsoft.Kiota.Abstractions.RequestInformation
                {
                    HttpMethod = Microsoft.Kiota.Abstractions.Method.GET,
                    UrlTemplate = next,
                    PathParameters = new Dictionary<string, object>()
                };
                pg = await _client.RequestAdapter.SendAsync(
                        req, DriveItemCollectionResponse.CreateFromDiscriminatorValue);
                all.AddRange(pg!.Value);
            }

            var jsonFiles = all.Where(f => f.File != null &&
                                           f.Name.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase) &&
                                           f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(f => f.Name)
                                .ToList();

            var merged = new JArray();
            foreach (var file in jsonFiles)
            {
                using var stream = await _client.Drives[drive.Id].Items[file.Id].Content.GetAsync();
                using var sr = new StreamReader(stream, Encoding.UTF8);
                var root = JObject.Parse(await sr.ReadToEndAsync());
                if (root["manutencoes"] is JArray arr)
                    merged.Merge(arr);
            }

            var mergedLocal = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manutencoes_latest.json");
            var mergedRoot = new JObject { ["manutencoes"] = merged };
            File.WriteAllText(mergedLocal, mergedRoot.ToString());

            LastUpdate = DateTime.Now;

            return merged;
        }

        public async Task<string> UploadHtmlAndShareAsync(string fileName, string htmlContent)
        {
            var site = await _client.Sites[$"{SiteHostname}:{SitePath}:"].GetAsync();
            var drives = await _client.Sites[site.Id].Drives.GetAsync();
            var drive = drives.Value.FirstOrDefault(d =>
                d.Name.Equals(LibraryName, StringComparison.OrdinalIgnoreCase));
            if (drive == null) throw new InvalidOperationException($"Biblioteca '{LibraryName}' não encontrada.");

            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(htmlContent));
            var uploaded = await _client.Drives[drive.Id].Root.ItemWithPath(fileName).Content.PutAsync(ms);

            var body = new Microsoft.Graph.Drives.Item.Items.Item.CreateLink.CreateLinkPostRequestBody
            {
                Type = "view",
                Scope = "anonymous"
            };
            var item = await _client.Drives[drive.Id].Items[uploaded.Id].GetAsync();
            return item?.WebUrl;
        }
    }
}
