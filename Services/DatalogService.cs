using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManutMap.Models;

namespace ManutMap.Services
{
    public class DatalogService
    {
        private const string TenantId = "3b08e64e-b3be-402b-bb26-1fa4f91cf61f";
        private const string ClientId = "3cffac6a-f9d9-42d1-9065-4054fcd40163";
        private const string ClientSecret = "JFd8Q~hHgTYYo0P0EjAM8mpe3xm3.5vTfCHRFc.T";

        private const string Domain = "oneengenharia.sharepoint.com";
        private const string SitePath = "OneEngenharia";
        private const string DriveDatalog = "DatalogGERAL";
        private const string DriveJson = "ArquivosJSON";

        private static readonly string CachePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "datalog_folders_cache.json");

        private Dictionary<string, string>? _folderCache;
        private DateTime _cacheDate;

        private void LoadCache()
        {
            if (_folderCache != null) return;
            if (!File.Exists(CachePath))
            {
                _folderCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _cacheDate = DateTime.MinValue;
                return;
            }

            var obj = JObject.Parse(File.ReadAllText(CachePath));
            _cacheDate = obj.Value<DateTime?>("lastUpdate") ?? DateTime.MinValue;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (obj["folders"] is JObject folders)
            {
                foreach (var p in folders.Properties())
                    dict[p.Name] = p.Value?.ToString() ?? string.Empty;
            }
            _folderCache = dict;
        }

        private void SaveCache()
        {
            if (_folderCache == null) return;
            var obj = new JObject
            {
                ["lastUpdate"] = _cacheDate,
                ["folders"] = JObject.FromObject(_folderCache)
            };
            File.WriteAllText(CachePath, obj.ToString());
        }

        public Dictionary<string, string> GetCachedDatalogFolders()
        {
            LoadCache();
            return _folderCache ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }


        private readonly GraphServiceClient _graph;

        public DatalogService()
        {
            _graph = new GraphServiceClient(
                new ClientSecretCredential(TenantId, ClientId, ClientSecret),
                new[] { "https://graph.microsoft.com/.default" });
        }

        private async Task<List<DriveItem>> GetNewRootFoldersAsync(string driveId)
        {
            if (_cacheDate == DateTime.MinValue)
                return await GetAllRootFoldersAsync(driveId);

            // Graph API does not always allow filtering by createdDateTime.
            // If the request fails, fallback to fetching all folders and filter
            // locally.
            try
            {
                var list = new List<DriveItem>();
                var page = await _graph.Drives[driveId].Items["root"].Children.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"createdDateTime ge {_cacheDate:yyyy-MM-ddTHH:mm:ssZ}";
                });

                list.AddRange(page.Value.Where(i => i.Folder != null));

                while (page.OdataNextLink is string next)
                {
                    var req = new RequestInformation
                    {
                        HttpMethod = Method.GET,
                        UrlTemplate = next,
                        PathParameters = new Dictionary<string, object>()
                    };
                    page = await _graph.RequestAdapter.SendAsync(req, DriveItemCollectionResponse.CreateFromDiscriminatorValue);
                    list.AddRange(page!.Value.Where(i => i.Folder != null));
                }

                return list;
            }
            catch
            {
                var all = await GetAllRootFoldersAsync(driveId);
                return all.Where(i =>
                        i.Folder != null &&
                        i.CreatedDateTime.HasValue &&
                        i.CreatedDateTime.Value.UtcDateTime >= _cacheDate)
                       .ToList();
            }
        }

        private void MergeFolders(IEnumerable<DriveItem> items)
        {
            LoadCache();
            foreach (var it in items)
            {
                string name = it.Name!.Trim();
                string url = it.WebUrl ??
                              $"https://{Domain}/sites/{SitePath}/{DriveDatalog}/{name}";

                _folderCache![name] = url;

                int idx = name.IndexOf('_');
                if (idx > 0)
                {
                    string prefix = name[..idx];
                    _folderCache.TryAdd(prefix, url);

                    if (idx == name.Length - 1)
                        _folderCache.TryAdd(prefix.TrimEnd('_'), url);
                }
            }
        }

        private async Task EnsureCacheUpdatedAsync()
        {
            var site = await _graph.Sites[$"{Domain}:/sites/{SitePath}"].GetAsync();
            string driveId = await GetDriveId(site.Id, DriveDatalog);

            var novos = await GetNewRootFoldersAsync(driveId);
            if (novos.Count > 0)
            {
                MergeFolders(novos);
                _cacheDate = DateTime.UtcNow;
                SaveCache();
            }
            else if (_folderCache == null)
            {
                // no cache existed and no folders found
                _folderCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public async Task<List<OsInfo>> BuscarAsync(DateTime ini,
                                                    DateTime fim,
                                                    string? termo,
                                                    int tipoFiltro,
                                                    string? regional)
        {
            var site = await _graph.Sites[$"{Domain}:/sites/{SitePath}"].GetAsync();

            // When searching for installation folders (combo index 0)
            if (tipoFiltro == 0)
            {
                string dataDrive = await GetDriveId(site.Id, DriveDatalog);
                var inst = await GetPastasInstalacaoAsync(dataDrive, termo, regional);
                return inst.OrderBy(i => i.Name)
                           .Select(i => new OsInfo
                           {
                               NumOS = i.Name,
                               IdSigfi = i.Name,
                               Rota = "-",
                               Data = i.Date,
                               TemDatalog = true,
                               FolderUrl = i.Url
                           })
                           .ToList();
            }

            string jsonDriveId = await GetDriveId(site.Id, DriveJson);
            var jsonItens = await GetLatestJsonsAsync(jsonDriveId);

            var mapa = new Dictionary<string, OsInfo>(StringComparer.OrdinalIgnoreCase);

            // Map combo indexes to GetOsInfosAsync filters
            int infosFiltro = tipoFiltro switch
            {
                1 => 0, // OS
                2 => 1, // IDSIGFI
                3 => 2, // ROTA
                _ => tipoFiltro
            };

            foreach (var itm in jsonItens)
            {
                var infos = await GetOsInfosAsync(jsonDriveId, itm.Id!, ini, fim,
                                                termo, infosFiltro, termo != null, regional);
                foreach (var inf in infos)
                    mapa[inf.NumOS] = inf;
            }

            await EnsureCacheUpdatedAsync();
            LoadCache();

            foreach (var inf in mapa.Values)
                if (_folderCache!.TryGetValue(inf.NumOS, out var url))
                {
                    inf.TemDatalog = true;
                    inf.FolderUrl = url;
                }

            return mapa.Values.OrderBy(i => i.NumOS).ToList();
        }

        private async Task<string> GetDriveId(string siteId, string driveName)
        {
            var drives = await _graph.Sites[siteId].Drives.GetAsync();
            return drives.Value.First(d => d.Name == driveName).Id!;
        }

        private async Task<List<DriveItem>> GetAllRootFoldersAsync(string driveId)
        {
            var lista = new List<DriveItem>();

            var page = await _graph.Drives[driveId].Items["root"].Children.GetAsync();
            lista.AddRange(page.Value.Where(i => i.Folder != null));

            while (page.OdataNextLink is string next)
            {
                var req = new RequestInformation
                {
                    HttpMethod = Method.GET,
                    UrlTemplate = next,
                    PathParameters = new Dictionary<string, object>()
                };

                page = await _graph.RequestAdapter.SendAsync(
                           req, DriveItemCollectionResponse.CreateFromDiscriminatorValue);

                lista.AddRange(page!.Value.Where(i => i.Folder != null));
            }
            return lista;
        }

        private async Task<List<DriveItem>> GetLatestJsonsAsync(string driveId)
        {
            var pg = await _graph.Drives[driveId].Items["root"].Children.GetAsync();
            var list = new List<DriveItem>();

            foreach (var suf in JsonFileConstants.JsonSuffixes)
            {
                var arquivos = pg.Value
                    .Where(f => f.File != null &&
                                f.Name.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastModifiedDateTime)
                    .ToList();

                if (arquivos.Count > 0)
                    list.Add(arquivos.First());
            }

            if (list.Count == 0)
                throw new InvalidOperationException("Nenhum JSON de manutenção encontrado.");

            return list;
        }

        private async Task<List<OsInfo>> GetOsInfosAsync(string driveId,
                                                         string itemId,
                                                         DateTime ini,
                                                         DateTime fim,
                                                         string? termo,
                                                         int tipoFiltro,
                                                         bool buscaUnica,
                                                         string? regionalSel)
        {
            using var st = await _graph.Drives[driveId].Items[itemId].Content.GetAsync();
            using var sr = new StreamReader(st, Encoding.UTF8);
            var root = JObject.Parse(await sr.ReadToEndAsync());

            JArray? arr = root["manutencoes"] as JArray;
            if (arr == null)
            {
                var prop = root.Properties()
                    .FirstOrDefault(p =>
                        string.Equals(p.Name, "manutencoes",
                                      StringComparison.OrdinalIgnoreCase));
                arr = prop?.Value as JArray;
            }
            if (arr == null)
                return new List<OsInfo>();

            var list = new List<OsInfo>();

            foreach (var t in arr)
            {
                string num = t.Value<string>("NUMOS")?.Trim() ?? "";
                string sigfi = t.Value<string>("IDSIGFI")?.Trim() ?? "";
                string rota = t.Value<string>("ROTA")?.Trim() ?? "-";
                if (num == "") continue;

                if (regionalSel != null &&
                    !num.StartsWith(regionalSel, StringComparison.OrdinalIgnoreCase))
                    continue;

                DateTime? dataJson = null;

                if (!buscaUnica)
                {
                    var dtStr = t.Value<string>("DATACONCLUSAO");
                    if (string.IsNullOrWhiteSpace(dtStr)) continue;

                    var pt = CultureInfo.GetCultureInfo("pt-BR");
                    var dt = DateTime.Parse(dtStr, pt, DateTimeStyles.AssumeUniversal);
                    if (dt < ini || dt > fim) continue;
                    dataJson = dt;
                }
                else
                {
                    bool ok = tipoFiltro switch
                    {
                        0 => num.Equals(termo!, StringComparison.OrdinalIgnoreCase),
                        1 => sigfi.Equals(termo!, StringComparison.OrdinalIgnoreCase),
                        2 => rota.Equals(termo!, StringComparison.OrdinalIgnoreCase),
                        _ => false
                    };
                    if (!ok) continue;

                    var pt = CultureInfo.GetCultureInfo("pt-BR");
                    if (DateTime.TryParse(t.Value<string>("DATACONCLUSAO"),
                                          pt,
                                          DateTimeStyles.AssumeUniversal, out var dtTmp))
                        dataJson = dtTmp;
                }

                list.Add(new OsInfo
                {
                    NumOS = num,
                    IdSigfi = sigfi,
                    Rota = rota,
                    Data = dataJson
                });
            }
            return list;
        }

        private async Task<Dictionary<string, string>> GetPastasPeriodoAsync(string driveId,
                                                                             DateTime ini,
                                                                             DateTime fim,
                                                                             bool buscaUnica,
                                                                             string? termo,
                                                                             int tipoFiltro,
                                                                             string? regionalSel)
        {
            var all = await GetAllRootFoldersAsync(driveId);
            var q = all.AsEnumerable();

            if (!buscaUnica)
                q = q.Where(i => i.CreatedDateTime >= ini && i.CreatedDateTime <= fim);

            if (regionalSel != null)
                q = q.Where(i => i.Name!.StartsWith(regionalSel, StringComparison.OrdinalIgnoreCase));

            if (buscaUnica && tipoFiltro == 2 && termo != null)
                q = q.Where(i => i.Name!.Contains(termo, StringComparison.OrdinalIgnoreCase));

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var it in q)
            {
                string name = it.Name!.Trim();
                string url = it.WebUrl ??
                              $"https://{Domain}/sites/{SitePath}/{DriveDatalog}/{name}";

                dict[name] = url;

                int idx = name.IndexOf('_');
                if (idx > 0)
                {
                    string prefix = name[..idx];
                    dict.TryAdd(prefix, url);

                    if (idx == name.Length - 1)
                        dict.TryAdd(prefix.TrimEnd('_'), url);
                }
            }
            return dict;
        }

        public async Task<List<(string Name, string Url, DateTime Date)>> GetPastasInstalacaoAsync(string driveId,
                                             string? idSigfi,
                                             string? regional)
        {
            var all = await GetAllRootFoldersAsync(driveId);

            var q = all.Where(i => i.Name!.EndsWith("_instalacao",
                                       StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(idSigfi))
                q = q.Where(i => i.Name!.StartsWith(idSigfi, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(regional))
                q = q.Where(i => i.Name!.StartsWith(regional, StringComparison.OrdinalIgnoreCase));

            return q.Select(i => (
                        i.Name!.Trim(),
                        i.WebUrl ??
                        $"https://{Domain}/sites/{SitePath}/{DriveDatalog}/{i.Name!.Trim()}",
                        (i.LastModifiedDateTime ?? i.CreatedDateTime ?? DateTimeOffset.MinValue)
                            .UtcDateTime))
                    .ToList();
        }

        public async Task<Dictionary<string, string>> GetAllDatalogFoldersAsync()
        {
            await EnsureCacheUpdatedAsync();
            LoadCache();
            return _folderCache!;
        }

        public async Task<Dictionary<string, string>> GetDatalogFoldersPeriodAsync(DateTime ini, DateTime fim)
        {
            var site = await _graph.Sites[$"{Domain}:/sites/{SitePath}"].GetAsync();
            string driveId = await GetDriveId(site.Id, DriveDatalog);
            return await GetPastasPeriodoAsync(driveId, ini, fim, false, null, -1, null);
        }
    }
}
