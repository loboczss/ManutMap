using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        public void SaveCsv(IEnumerable<JObject> data, string path, string latLonField = "LATLON")
        {
            if (data == null) return;

            var headers = new SortedSet<string>(data.SelectMany(o => o.Properties().Select(p => p.Name)));
            headers.Remove(latLonField);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers) + ",Latitude,Longitude");

            foreach (var obj in data)
            {
                string lat = string.Empty, lon = string.Empty;
                var coord = obj[latLonField]?.ToString();
                if (!string.IsNullOrWhiteSpace(coord))
                {
                    var m = Regex.Matches(coord, "-?\\d+(?:[.,]\\d+)?");
                    if (m.Count >= 2)
                    {
                        lat = m[0].Value.Replace(',', '.');
                        lon = m[1].Value.Replace(',', '.');
                    }
                }

                var values = headers.Select(h => EscapeCsv(obj[h]?.ToString()));
                sb.AppendLine(string.Join(",", values) + $",{EscapeCsv(lat)},{EscapeCsv(lon)}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            if (value == null) return string.Empty;
            if (value.Contains("\"")) value = value.Replace("\"", "\"\"");
            if (value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
                return $"\"{value}\"";
            return value;
        }
    }
}
