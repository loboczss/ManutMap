using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ManutMap.Services
{
    public class AtualizadorService
    {
        private const string ApiUrl = "https://api.github.com/repos/loboczss/ManutMap/releases/latest";

        private static string InstallDir => AppDomain.CurrentDomain.BaseDirectory;

        public async Task<(Version LocalVersion, Version RemoteVersion)> GetVersionsAsync()
        {
            Version localVer = new Version(0, 0, 0, 0);
            try
            {
                localVer = Assembly.GetExecutingAssembly().GetName().Version ?? localVer;
            }
            catch
            {
                // fallback to file lookup in caso de erro inesperado
                var dllPath = Path.Combine(InstallDir, "ManutMap.dll");
                var exePath = Path.Combine(InstallDir, "ManutMap.exe");
                string asmPath = File.Exists(dllPath) ? dllPath : exePath;
                if (File.Exists(asmPath))
                {
                    try
                    {
                        localVer = AssemblyName.GetAssemblyName(asmPath).Version;
                    }
                    catch (BadImageFormatException)
                    {
                        // Executável não possui metadados. Mantém versão padrão.
                    }
                }
            }

            Version remoteVer = localVer;
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "ManutMap");
                var json = await http.GetStringAsync(ApiUrl);
                var obj = JObject.Parse(json);
                var tag = ((string?)obj["tag_name"] ?? string.Empty).Trim();
                tag = new string(tag.TrimStart('v').TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
                if (Version.TryParse(tag, out var parsed))
                    remoteVer = parsed;
            }
            catch
            {
                // sem internet ou erro → remote = local
            }

            return (localVer, remoteVer);
        }

        public async Task<string?> DownloadLatestReleaseAsync()
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "ManutMap");
            var json = await http.GetStringAsync(ApiUrl);
            var obj = JObject.Parse(json);
            var asset = ((JArray?)obj["assets"])?.FirstOrDefault();
            var url = (string?)asset?["browser_download_url"];
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var fileName = (string?)asset?["name"] ?? Path.GetFileName(url);
            string dest = Path.Combine(Path.GetTempPath(), fileName);
            var data = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(dest, data);
            return dest;
        }

        public string CreateUpdateBatch(string zipPath)
        {
            string batchPath = Path.Combine(Path.GetTempPath(), "ManutMapUpdate.bat");
            string installDir = InstallDir.TrimEnd('\n', '\r', '\\');

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("set ZIP=\"" + zipPath + "\"");
            sb.AppendLine("set INSTALL=\"" + installDir + "\"");
            sb.AppendLine("set TEMP_DIR=%TEMP%\\ManutMapUpdate");
            sb.AppendLine("if exist \"%TEMP_DIR%\" rmdir /s /q \"%TEMP_DIR%\"");
            sb.AppendLine("mkdir \"%TEMP_DIR%\"");
            sb.AppendLine("powershell -NoLogo -NoProfile -Command \"Expand-Archive -Path '%ZIP%' -DestinationPath '%TEMP_DIR%' -Force\"");
            sb.AppendLine("xcopy \"%TEMP_DIR%\\*\" \"%INSTALL%\\\" /E /Y");
            sb.AppendLine("set DESKTOP=%USERPROFILE%\\Desktop");
            sb.AppendLine("powershell -NoLogo -NoProfile -Command \"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%DESKTOP%\\ManutMap.lnk');$s.TargetPath='%INSTALL%\\ManutMap.exe';$s.Save()\"");
            sb.AppendLine("rmdir /s /q \"%TEMP_DIR%\"");
            sb.AppendLine("start \"\" \"%INSTALL%\\ManutMap.exe\"");

            File.WriteAllText(batchPath, sb.ToString(), Encoding.UTF8);
            return batchPath;
        }
    }
}
