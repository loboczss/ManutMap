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
using System.Text.RegularExpressions;
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
        private const string DriveDatalog2 = "DataLog";
        private const string DriveDatalog3 = "DataLogsAC";
        private static readonly string[] DriveDatalogAll =
        {
            DriveDatalog,
            DriveDatalog2,
            DriveDatalog3
        };

        // Apenas a unidade DatalogGERAL armazena pastas de instalação.
        // Utilizamos esse array específico para acelerar buscas por instalações.
        private static readonly string[] DriveInstalacao =
        {
            DriveDatalog
        };

        private static readonly string[] DatalogFileSuffixes =
        {
            "_CON.csv",
            "_CON.xls",
            "_CON.xlsx"
        };
        private const string DriveJson = "ArquivosJSON";
        private const string InstalacaoJsonName = "Instalacao_AC.json";
        private const string ListIdDatalog = "5b66bbc3-23d2-42d9-827a-e6b77765e8e0";
        private const string ListIdDatalogAC = "f5904f07-a47e-4955-8a9d-5807ef6e2179";

        private static readonly Dictionary<string, string> DriveListMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [DriveDatalog2] = ListIdDatalog,
            [DriveDatalog3] = ListIdDatalogAC
        };

        private static readonly string CachePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "datalog_folders_cache.json");

        private readonly Dictionary<string, string> _cacheInst = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _cacheManut = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (string Url, bool IsInst)> _folderCache
            = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _cacheDateInst;
        private DateTime _cacheDateManut;
        private bool _cacheLoaded;

        private void RebuildFolderCache()
        {
            _folderCache.Clear();
            foreach (var kv in _cacheManut)
                AddFolderKeys(_folderCache, kv.Key, kv.Value, false);
            foreach (var kv in _cacheInst)
                AddFolderKeys(_folderCache, kv.Key, kv.Value, true);
        }

        private void LoadCache()
        {
            if (_cacheLoaded) return;
            if (!File.Exists(CachePath))
            {
                _cacheDateInst = DateTime.MinValue;
                _cacheDateManut = DateTime.MinValue;
                _cacheLoaded = true;
                return;
            }

            var obj = JObject.Parse(File.ReadAllText(CachePath));
            _cacheDateInst = obj.Value<DateTime?>("lastUpdateInst") ?? DateTime.MinValue;
            _cacheDateManut = obj.Value<DateTime?>("lastUpdateManut") ?? DateTime.MinValue;

            if (obj["foldersInst"] is JObject inst)
            {
                foreach (var p in inst.Properties())
                    _cacheInst[p.Name] = p.Value?.ToString() ?? string.Empty;
            }

            if (obj["foldersManut"] is JObject man)
            {
                foreach (var p in man.Properties())
                    _cacheManut[p.Name] = p.Value?.ToString() ?? string.Empty;
            }

            RebuildFolderCache();
            _cacheLoaded = true;
        }

        private async Task SaveCacheAsync()
        {
            var obj = new JObject
            {
                ["lastUpdateInst"] = _cacheDateInst,
                ["lastUpdateManut"] = _cacheDateManut,
                ["foldersInst"] = JObject.FromObject(_cacheInst),
                ["foldersManut"] = JObject.FromObject(_cacheManut)
            };

            if (File.Exists(CachePath))
            {
                try
                {
                    File.Delete(CachePath);
                }
                catch
                {
                    // Se a exclusão falhar continuamos a criar o novo arquivo,
                    // pois File.WriteAllTextAsync irá sobrescrever o conteúdo.
                }
            }

            await File.WriteAllTextAsync(CachePath, obj.ToString());
        }

        private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
        {
            const int maxAttempts = 5;
            int delayMs = 1000;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (ServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < maxAttempts)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                }
            }
            // última tentativa sem captura específica
            return await action();
        }

        public Dictionary<string, string> GetCachedDatalogFolders()
        {
            LoadCache();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _folderCache)
                dict[kv.Key] = kv.Value.Url;
            return dict;
        }


        private readonly GraphServiceClient _graph;

        public DatalogService()
        {
            _graph = new GraphServiceClient(
                new ClientSecretCredential(TenantId, ClientId, ClientSecret),
                new[] { "https://graph.microsoft.com/.default" });
        }

        private async Task<List<DriveItem>> GetNewRootFoldersAsync(string driveId,
                                                                   string driveName,
                                                                   IProgress<int>? folderProgress = null)
        {
            LoadCache();
            DateTime since = _cacheDateInst <= _cacheDateManut ? _cacheDateInst : _cacheDateManut;
            if (since == DateTime.MinValue)
                return await GetAllRootFoldersAsync(driveId, driveName, folderProgress);

            var todas = await GetAllFoldersRecursiveAsync(driveId, "root", 2, folderProgress);
            return todas.Where(i =>
                                   i.CreatedDateTime.HasValue &&
                                   i.CreatedDateTime.Value.UtcDateTime >= since)
                         .ToList();
        }

        private static readonly Regex OsDigitsRegex = new("(?i)[A-Z]{0,2}(\\d{5,})");
        private static readonly Regex OsFullPrefixRegex = new("(?i)^([A-Z]{2}\\d{5,})");
        private static readonly Regex OsFullSuffixRegex = new("(?i)^(\\d{5,})_([A-Z]{2})(?:_|$)");

        // Nome de pasta de instalação no formato "AC55984_instalacao"
        private static bool IsInstalacaoFolder(string name)
        {
            return name.EndsWith("_instalacao", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOsFolderName(string name)
        {
            return name.Contains('_') || OsDigitsRegex.IsMatch(name);
        }

        private static void AddFolderKeys(IDictionary<string, string> dict, string name, string url)
        {
            bool isInst = IsInstalacaoFolder(name);

            dict[name] = url;

            string? osKey = null;
            var m = OsFullPrefixRegex.Match(name);
            if (m.Success)
                osKey = m.Groups[1].Value;
            else
            {
                m = OsFullSuffixRegex.Match(name);
                if (m.Success)
                    osKey = m.Groups[2].Value + m.Groups[1].Value;
            }
            if (!string.IsNullOrEmpty(osKey))
            {
                if (isInst)
                    dict[osKey] = url;
                else
                    dict.TryAdd(osKey, url);
            }

            int idx = name.IndexOf('_');
            if (idx > 0)
            {
                string prefix = name[..idx];
                if (isInst)
                    dict[prefix] = url;
                else
                    dict.TryAdd(prefix, url);

                if (idx == name.Length - 1)
                {
                    string trimmed = prefix.TrimEnd('_');
                    if (isInst)
                        dict[trimmed] = url;
                    else
                        dict.TryAdd(trimmed, url);
                }
            }

            foreach (Match m2 in OsDigitsRegex.Matches(name))
            {
                if (isInst)
                {
                    dict[m2.Value] = url;
                    dict[m2.Groups[1].Value] = url;
                }
                else
                {
                    dict.TryAdd(m2.Value, url);
                    dict.TryAdd(m2.Groups[1].Value, url);
                }
            }
        }

        private static void AddFolderKeys(IDictionary<string, (string Url, bool IsInst)> dict,
                                          string name,
                                          string url,
                                          bool isInst)
        {
            dict[name] = (url, isInst);

            string? osKey = null;
            var m = OsFullPrefixRegex.Match(name);
            if (m.Success)
                osKey = m.Groups[1].Value;
            else
            {
                m = OsFullSuffixRegex.Match(name);
                if (m.Success)
                    osKey = m.Groups[2].Value + m.Groups[1].Value;
            }
            if (!string.IsNullOrEmpty(osKey))
            {
                if (isInst)
                    dict[osKey] = (url, true);
                else
                    dict.TryAdd(osKey, (url, false));
            }

            int idx = name.IndexOf('_');
            if (idx > 0)
            {
                string prefix = name[..idx];
                if (isInst)
                    dict[prefix] = (url, true);
                else
                    dict.TryAdd(prefix, (url, false));

                if (idx == name.Length - 1)
                {
                    string trimmed = prefix.TrimEnd('_');
                    if (isInst)
                        dict[trimmed] = (url, true);
                    else
                        dict.TryAdd(trimmed, (url, false));
                }
            }

            foreach (Match m2 in OsDigitsRegex.Matches(name))
            {
                if (isInst)
                {
                    dict[m2.Value] = (url, true);
                    dict[m2.Groups[1].Value] = (url, true);
                }
                else
                {
                    dict.TryAdd(m2.Value, (url, false));
                    dict.TryAdd(m2.Groups[1].Value, (url, false));
                }
            }
        }

        private void MergeFolders(string name, string url, bool isInst)
        {
            LoadCache();
            var dict = isInst ? _cacheInst : _cacheManut;
            AddFolderKeys(dict, name, url);
            AddFolderKeys(_folderCache, name, url, isInst);
        }

        private static int CalcPercent(int completed, int total)
        {
            if (total <= 0) return 100;
            return (int)(completed * 100.0 / total);
        }

        private async Task EnsureCacheUpdatedAsync(IProgress<(int Percent, string Message)>? progress = null,
                                                  IProgress<int>? folderProgress = null)
        {
            LoadCache();
            bool updatedInst = false;
            bool updatedManut = false;
            int total = DriveDatalogAll.Length;
            int completed = 0;
            var site = await ExecuteWithRetryAsync(() =>
                _graph.Sites[$"{Domain}:/sites/{SitePath}"].GetAsync());

            var tasks = DriveDatalogAll.Select(async driveName =>
            {
                try
                {
                    progress?.Report((CalcPercent(completed, total), $"{driveName}: buscando"));
                    string driveId = await GetDriveId(site.Id, driveName);
                    var novos = await GetNewRootFoldersAsync(driveId, driveName, folderProgress);
                    return (driveName, driveId, novos);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Falha em drive {driveName}: {ex.Message}");
                    return (driveName, string.Empty, new List<DriveItem>());
                }
            }).ToArray();

            try
            {
                foreach (var task in tasks)
                {
                    bool driveUpdated = false;
                    var (driveName, driveId, novos) = await task;
                    completed++;
                    foreach (var item in novos)
                    {
                        if (string.IsNullOrEmpty(driveId))
                            break;
                        if (!await FolderHasDatalogAsync(driveId, item.Id!))
                            continue;
                        string name = item.Name!.Trim();
                        string url = item.WebUrl ??
                                      $"https://{Domain}/sites/{SitePath}/{driveName}/{name}";
                        bool isInst = IsInstalacaoFolder(name);
                        MergeFolders(name, url, isInst);
                        driveUpdated = true;
                        if (isInst) updatedInst = true; else updatedManut = true;
                    }
                    if (driveUpdated)
                        await SaveCacheAsync();
                    progress?.Report((CalcPercent(completed, total), $"{driveName}: processado"));
                }

                if (updatedInst)
                    _cacheDateInst = DateTime.UtcNow;
                if (updatedManut)
                    _cacheDateManut = DateTime.UtcNow;

                if (updatedInst || updatedManut)
                {
                    await SaveCacheAsync();
                }
                else if (!File.Exists(CachePath))
                {
                    // Garante a criação do arquivo de cache mesmo quando nenhum
                    // datalog novo é encontrado.
                    await SaveCacheAsync();
                }
            }
            finally
            {
                await SaveCacheAsync();
            }
        }

        public async Task<List<OsInfo>> BuscarAsync(DateTime ini,
                                                    DateTime fim,
                                                    string? termo,
                                                    int tipoFiltro,
                                                    string? regional)
        {
            var site = await ExecuteWithRetryAsync(() =>
                _graph.Sites[$"{Domain}:/sites/{SitePath}"].GetAsync());

            // When searching for installation folders (combo index 0)
            if (tipoFiltro == 0)
            {
                var dict = new Dictionary<string, (string Url, DateTime Date)>(StringComparer.OrdinalIgnoreCase);
                foreach (var driveName in DriveInstalacao)
                {
                    string dataDrive = await GetDriveId(site.Id, driveName);
                    var inst = await GetPastasInstalacaoAsync(dataDrive, driveName, termo, regional);
                    foreach (var item in inst)
                        dict[item.Name] = (item.Url, item.Date);
                }

                string jsonDriveIdInst = await GetDriveId(site.Id, DriveJson);
                var rotaMap = await GetInstalacaoRotaMapAsync(jsonDriveIdInst);

                return dict.OrderBy(k => k.Key)
                           .Select(kvp =>
                           {
                               string id = kvp.Key.Split('_')[0];
                               rotaMap.TryGetValue(id, out var rota);
                               if (string.IsNullOrWhiteSpace(rota)) rota = "-";
                               return new OsInfo
                               {
                                   NumOS = kvp.Key,
                                   IdSigfi = kvp.Key,
                                   Rota = rota,
                                   Data = kvp.Value.Date,
                                   TemDatalog = true,
                                   FolderUrl = kvp.Value.Url
                               };
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
                if (_folderCache.TryGetValue(inf.NumOS, out var val))
                {
                    if (tipoFiltro == 0)
                    {
                        if (val.IsInst)
                        {
                            inf.TemDatalog = true;
                            inf.FolderUrl = val.Url;
                        }
                    }
                    else
                    {
                        if (!val.IsInst)
                        {
                            inf.TemDatalog = true;
                            inf.FolderUrl = val.Url;
                        }
                    }
                }

            return mapa.Values.OrderBy(i => i.NumOS).ToList();
        }

        private async Task<string> GetDriveId(string siteId, string driveName)
        {
            if (DriveListMap.TryGetValue(driveName, out var listId))
            {
                var drv = await ExecuteWithRetryAsync(() =>
                    _graph.Sites[siteId].Lists[listId].Drive.GetAsync());
                if (drv != null)
                    return drv.Id!;
            }

            var drives = await ExecuteWithRetryAsync(() =>
                _graph.Sites[siteId].Drives.GetAsync());
            var drive = drives.Value.FirstOrDefault(d => d.Name.Equals(driveName,
                StringComparison.OrdinalIgnoreCase));
            if (drive == null)
                throw new InvalidOperationException($"Drive '{driveName}' n\u00e3o encontrado.");

            return drive.Id!;
        }

        private async Task<List<DriveItem>> GetAllRootFoldersAsync(string driveId,
                                                                   string driveName,
                                                                   IProgress<int>? folderProgress = null)
        {
            // Utiliza busca recursiva para garantir que todas as pastas sejam
            // visitadas independentemente da estrutura do drive. O 
            // DataLogsAC já dependia dessa busca, mas os outros drives podem
            // possuir subpastas organizadas por regional. Para evitar pastas
            // faltantes, sempre percorremos duas camadas de pastas.
            return await GetAllFoldersRecursiveAsync(driveId, "root", 2, folderProgress);
        }

        private async Task<List<DriveItem>> GetAllFoldersRecursiveAsync(string driveId,
                                                                       string itemId,
                                                                       int depth,
                                                                       IProgress<int>? folderProgress = null)
        {
            var result = new List<DriveItem>();
            int progressBuffer = 0;

            var page = await ExecuteWithRetryAsync(() =>
                _graph.Drives[driveId].Items[itemId].Children.GetAsync());
            var folders = new List<DriveItem>();

            if (page?.Value == null)
                return result;

            foreach (var i in page.Value.Where(i => i.Folder != null))
            {
                progressBuffer++; if (progressBuffer >= 100) { folderProgress?.Report(progressBuffer); progressBuffer = 0; }
                if (IsOsFolderName(i.Name!))
                    result.Add(i);
                else
                    folders.Add(i);
            }

            while (page?.OdataNextLink is string next)
            {
                try
                {
                    var req = new RequestInformation
                    {
                        HttpMethod = Method.GET,
                        UrlTemplate = next,
                        PathParameters = new Dictionary<string, object>()
                    };
                    page = await ExecuteWithRetryAsync(() =>
                        _graph.RequestAdapter.SendAsync(
                            req, DriveItemCollectionResponse.CreateFromDiscriminatorValue));
                    if (page?.Value == null)
                    {
                        Console.WriteLine("Página nula recebida, encerrando loop.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Falha ao obter página: {ex.Message}");
                    break;
                }

                foreach (var i in page.Value.Where(i => i.Folder != null))
                {
                    progressBuffer++; if (progressBuffer >= 100) { folderProgress?.Report(progressBuffer); progressBuffer = 0; }
                    if (IsOsFolderName(i.Name!))
                        result.Add(i);
                    else
                        folders.Add(i);
                }
            }

            if (progressBuffer > 0)
                folderProgress?.Report(progressBuffer);

            if (depth > 0)
            {
                foreach (var f in folders)
                {
                    result.AddRange(await GetAllFoldersRecursiveAsync(driveId, f.Id!, depth - 1, folderProgress));
                }
            }

            return result;
        }

        private async Task<bool> FolderHasDatalogRecursiveAsync(string driveId,
                                                                string folderId,
                                                                int depth)
        {
            var page = await ExecuteWithRetryAsync(() =>
                _graph.Drives[driveId].Items[folderId].Children.GetAsync());

            if (page?.Value == null)
                return false;

            while (true)
            {
                foreach (var c in page.Value)
                {
                    if (c.File != null)
                    {
                        string name = c.Name?.Trim() ?? string.Empty;
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            return true;
                        foreach (var suf in DatalogFileSuffixes)
                        {
                            if (name.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                    else if (depth > 0 && c.Folder != null)
                    {
                        if (await FolderHasDatalogRecursiveAsync(driveId, c.Id!, depth - 1))
                            return true;
                    }
                }

                if (page?.OdataNextLink is string next)
                {
                    try
                    {
                        var req = new RequestInformation
                        {
                            HttpMethod = Method.GET,
                            UrlTemplate = next,
                            PathParameters = new Dictionary<string, object>()
                        };
                        page = await ExecuteWithRetryAsync(() =>
                            _graph.RequestAdapter.SendAsync(
                                req, DriveItemCollectionResponse.CreateFromDiscriminatorValue));
                        if (page?.Value == null)
                        {
                            Console.WriteLine("Página nula recebida, encerrando loop.");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Falha ao obter página: {ex.Message}");
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            return false;
        }

        private Task<bool> FolderHasDatalogAsync(string driveId, string folderId)
        {
            // Busca datalog em até duas camadas de subpastas para garantir que
            // arquivos armazenados em estruturas diferenciadas sejam encontrados.
            return FolderHasDatalogRecursiveAsync(driveId, folderId, 2);
        }

        private async Task<List<DriveItem>> GetLatestJsonsAsync(string driveId)
        {
            var pg = await ExecuteWithRetryAsync(() =>
                _graph.Drives[driveId].Items["root"].Children.GetAsync());
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
            using var st = await ExecuteWithRetryAsync(() =>
                _graph.Drives[driveId].Items[itemId].Content.GetAsync());
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
                                                                             string driveName,
                                                                             DateTime ini,
                                                                             DateTime fim,
                                                                             bool buscaUnica,
                                                                             string? termo,
                                                                             int tipoFiltro,
                                                                             string? regionalSel)
        {
            var all = await GetAllRootFoldersAsync(driveId, driveName);
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
                if (!await FolderHasDatalogAsync(driveId, it.Id!))
                    continue;
                string name = it.Name!.Trim();
                string url = it.WebUrl ??
                              $"https://{Domain}/sites/{SitePath}/{driveName}/{name}";

                AddFolderKeys(dict, name, url);
            }
            return dict;
        }

        public async Task<List<(string Name, string Url, DateTime Date)>> GetPastasInstalacaoAsync(string driveId,
                                             string driveName,
                                             string? idSigfi,
                                             string? regional)
        {
            var all = await GetAllRootFoldersAsync(driveId, driveName);

            var q = all.Where(i => IsInstalacaoFolder(i.Name!));

            if (!string.IsNullOrWhiteSpace(idSigfi))
                q = q.Where(i => i.Name!.StartsWith(idSigfi, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(regional))
                q = q.Where(i => i.Name!.StartsWith(regional, StringComparison.OrdinalIgnoreCase));

            var list = new List<(string Name, string Url, DateTime Date)>();
            foreach (var i in q)
            {
                if (!await FolderHasDatalogAsync(driveId, i.Id!))
                    continue;
                list.Add((
                    i.Name!.Trim(),
                    i.WebUrl ??
                    $"https://{Domain}/sites/{SitePath}/{driveName}/{i.Name!.Trim()}",
                    (i.LastModifiedDateTime ?? i.CreatedDateTime ?? DateTimeOffset.MinValue)
                        .UtcDateTime));
            }
            return list;
        }

        private async Task<Dictionary<string, string>> GetInstalacaoRotaMapAsync(string driveId)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var st = await ExecuteWithRetryAsync(() =>
                    _graph.Drives[driveId].Root.ItemWithPath(InstalacaoJsonName).Content.GetAsync());
                using var sr = new StreamReader(st, Encoding.UTF8);
                var root = JToken.Parse(await sr.ReadToEndAsync());

                // Só usamos DescendantsAndSelf() se for um JContainer
                if (root is JContainer container)
                {
                    foreach (var obj in container.DescendantsAndSelf().OfType<JObject>())
                    {
                        string id = obj.Value<string>("IDSERVICOSCONJ")?.Trim() ?? string.Empty;
                        string rota = obj.Value<string>("ROTA")?.Trim() ?? string.Empty;

                        if (!string.IsNullOrEmpty(id) &&
                            !string.IsNullOrEmpty(rota) &&
                            !dict.ContainsKey(id))
                        {
                            dict[id] = rota;
                        }
                    }
                }
            }
            catch
            {
                // Ignorado — retorna dicionário vazio em caso de erro
            }

            return dict;
        }


        public async Task<Dictionary<string, string>> GetAllDatalogFoldersAsync(IProgress<(int Percent, string Message)>? progress = null,
                                                                               IProgress<int>? folderProgress = null)
        {
            progress?.Report((0, "atualizando cache"));
            await EnsureCacheUpdatedAsync(progress, folderProgress);
            LoadCache();
            progress?.Report((100, "concluído"));
            return GetCachedDatalogFolders();
        }

        public async Task<Dictionary<string, string>> GetAllDatalogFoldersAsync(DatalogTipo tipo)
        {
            await EnsureCacheUpdatedAsync();
            LoadCache();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, val) in _folderCache)
            {
                if (tipo == DatalogTipo.Instalacao)
                {
                    if (val.IsInst)
                        dict[key] = val.Url;
                }
                else
                {
                    if (!val.IsInst)
                        dict[key] = val.Url;
                }
            }
            return dict;
        }

        public Task<int> CountAllDatalogFoldersAsync()
        {
            LoadCache();
            return Task.FromResult(_cacheInst.Count + _cacheManut.Count);
        }


        public async Task<Dictionary<string, string>> GetDatalogFoldersPeriodAsync(DateTime ini, DateTime fim)
        {
            var site = await ExecuteWithRetryAsync(() =>
                _graph.Sites[$"{Domain}:/sites/{SitePath}"].GetAsync());
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var driveName in DriveDatalogAll)
            {
                string driveId = await GetDriveId(site.Id, driveName);
                var partial = await GetPastasPeriodoAsync(driveId, driveName, ini, fim, false, null, -1, null);
                foreach (var kv in partial)
                    dict[kv.Key] = kv.Value;
            }

            return dict;
        }
    }
}
