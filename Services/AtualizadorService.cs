using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
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

            Version remoteVer = localVer;
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "ManutMap");
                var json = await http.GetStringAsync(ApiUrl);
                var obj = JObject.Parse(json);
                remoteVer = new Version(((string)obj["tag_name"]).TrimStart('v'));
            }
            catch
            {
                // sem internet ou erro → remote = local
            }

            return (localVer, remoteVer);
        }
    }
}
