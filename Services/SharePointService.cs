using System;
using System.IO;
using System.Linq;
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
        private const string FileNameFilter = "Manutencao_AC2025";

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
            var target = children.Value.FirstOrDefault(f =>
                f.Name.Contains(FileNameFilter, StringComparison.OrdinalIgnoreCase) &&
                f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (target == null) throw new InvalidOperationException($"Nenhum arquivo com '{FileNameFilter}' encontrado.");

            using var stream = await _client.Drives[drive.Id].Items[target.Id].Content.GetAsync();
            var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manutencoes_latest.json");
            // Use a scoped FileStream so it's closed before reading the file
            using (var fs = new FileStream(local, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                await stream.CopyToAsync(fs);
            }

            var json = File.ReadAllText(local);
            var root = JObject.Parse(json);
            return (JArray)root["manutencoes"];
        }
    }
}
