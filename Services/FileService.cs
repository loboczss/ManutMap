using System.IO;
using Newtonsoft.Json.Linq;

namespace ManutMap.Services
{
    public class FileService
    {
        public JArray LoadLocalJson(string filename)
        {
            if (!File.Exists(filename)) return null;
            var json = File.ReadAllText(filename);
            var root = JObject.Parse(json);
            return (JArray)root["manutencoes"];
        }
    }
}
