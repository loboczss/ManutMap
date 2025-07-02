using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Graph;
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
        // Padrões dos arquivos a serem baixados
        private static readonly string[] FileNameFilters = new[]
        {
            "Manutencao_AC2025",
            "Manutencao_AC2024",
            "Manutencao_AC2023",
            "Manutencao_MT"
        };

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

            var children = await _client.Drives[drive.Id].Items["root"].Children.GetAsync();

            var merged = new JArray();
            foreach (var filter in FileNameFilters)
            {
                var target = children.Value
                    .Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                                f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastModifiedDateTime)
                    .FirstOrDefault();

                if (target == null) continue;

                using var stream = await _client.Drives[drive.Id].Items[target.Id].Content.GetAsync();
                var localName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{filter}_latest.json");
                using (var fs = new FileStream(localName, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    await stream.CopyToAsync(fs);
                }

                var json = File.ReadAllText(localName);
                var root = JObject.Parse(json);
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
            var link = await _client.Drives[drive.Id].Items[uploaded.Id].CreateLink.PostAsync(body);
            return link?.Link?.WebUrl;
        }
    }
}
