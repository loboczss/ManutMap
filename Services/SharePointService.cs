using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
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
        private const string FilePrefix = "Manutencao_";

        private static readonly string OfflinePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                                  "manutencoes_latest.json");

        private readonly string[] _filePatterns =
        {
            "Manutencao_AC2025",
            "Manutencao_AC2024",
            "Manutencao_AC2023",
            "Manutencao_MT"
        };

        private string? _driveId;

        public DateTime LastUpdate { get; private set; }

        public SharePointService()
        {
            var cred = new ClientSecretCredential(TenantId, ClientId, ClientSecret);
            _client = new GraphServiceClient(cred);
        }

        public async Task<JArray> DownloadLatestJsonAsync()
        {
            bool fromInternet = false;
            JArray dados = new JArray();

            if (HasInternet())
            {
                try
                {
                    var files = await DownloadJsonFilesInternalAsync();
                    if (files.Count > 0)
                    {
                        dados = CombineJsonFiles(files);
                        var root = new JObject { ["manutencoes"] = dados };
                        File.WriteAllText(OfflinePath, root.ToString());
                        LastUpdate = DateTime.Now;
                        fromInternet = true;
                    }
                }
                catch
                {
                    // fallback to cache
                }
            }

            if (!fromInternet)
            {
                if (File.Exists(OfflinePath))
                {
                    var cached = JObject.Parse(File.ReadAllText(OfflinePath, Encoding.UTF8));
                    dados = cached["manutencoes"] as JArray ?? new JArray();
                }
            }

            return dados;
        }

        private static bool HasInternet()
        {
            try
            {
                using var wc = new System.Net.WebClient();
                wc.DownloadString("https://www.google.com/generate_204");
                return true;
            }
            catch
            {
                try
                {
                    using var wc = new System.Net.WebClient();
                    wc.DownloadString("https://www.bing.com");
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private async Task<string> GetDriveIdAsync()
        {
            if (!string.IsNullOrEmpty(_driveId))
                return _driveId!;

            var site = await _client.Sites[$"{SiteHostname}:{SitePath}:"].GetAsync();
            var drives = await _client.Sites[site.Id].Drives.GetAsync();
            var drive = drives.Value.FirstOrDefault(d => d.Name.Equals(LibraryName, StringComparison.OrdinalIgnoreCase));
            if (drive == null)
                throw new InvalidOperationException($"Biblioteca '{LibraryName}' não encontrada.");

            _driveId = drive.Id;
            return _driveId!;
        }

        private async Task<Dictionary<string, string>> DownloadJsonFilesInternalAsync()
        {
            string driveId = await GetDriveIdAsync();

            var page = await _client.Drives[driveId].Items["root"].Children.GetAsync();
            var items = page.Value.ToList();

            while (page.OdataNextLink is string next)
            {
                var req = new RequestInformation
                {
                    HttpMethod = Method.GET,
                    UrlTemplate = next,
                    PathParameters = new Dictionary<string, object>()
                };
                page = await _client.RequestAdapter.SendAsync(req, DriveItemCollectionResponse.CreateFromDiscriminatorValue);
                items.AddRange(page!.Value);
            }

            var result = new Dictionary<string, string>();

            foreach (var pattern in _filePatterns)
            {
                var arquivos = items
                    .Where(f => f.File != null && f.Name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(f => f.LastModifiedDateTime)
                    .ToList();

                if (!arquivos.Any())
                    continue;

                var meta = arquivos.First();
                using var stream = await _client.Drives[driveId].Items[meta.Id].Content.GetAsync();
                using var sr = new StreamReader(stream, Encoding.UTF8);
                result[pattern] = await sr.ReadToEndAsync();
            }

            return result;
        }

        private static JArray CombineJsonFiles(Dictionary<string, string> arquivos)
        {
            var combinado = new JArray();

            foreach (var kv in arquivos)
            {
                try
                {
                    string conteudo = kv.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(conteudo))
                        continue;

                    var token = JToken.Parse(conteudo);

                    JArray? arr = null;
                    if (token is JArray array)
                        arr = array;
                    else if (token is JObject obj)
                    {
                        arr = obj["manutencoes"] as JArray ?? obj.Properties().FirstOrDefault()?.Value as JArray;
                    }

                    if (arr != null)
                        foreach (var item in arr)
                            combinado.Add(item);
                }
                catch
                {
                    // ignore invalid file
                }
            }

            return combinado;
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
