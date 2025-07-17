using System;
using System.Collections.Generic;
using System.Linq;
using ManutMap.Models;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ManutMap.Services
{
    public class FilterService
    {
        private readonly Dictionary<string, List<string>> _regionalRotas = new()
        {
            {"Cruzeiro do Sul", new List<string>{"01","02","03","04","05","14","15","16","17","30","32","34","36","39","40","42","06"}},
            {"Tarauacá", new List<string>{"07","08","09","10","21","22","23","24","33","38","37","50","51","66","67","71"}},
            {"Sena Madureira", new List<string>{"11","12","52","54","55","57","58","59","63","65"}},
            {"Mato Grosso", new List<string>{"501","502","503","504","505","506","507","508","509","510","511","513","514"}}
        };

        private static string GetClientId(JObject obj)
        {
            string id = obj["IDSIGFI"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(id))
                id = obj["NOMECLIENTE"]?.ToString()?.Trim() ?? string.Empty;
            return id;
        }

        public List<JObject> Apply(JArray source, FilterCriteria c)
        {
            var filtered = source.OfType<JObject>()
                .Where(item =>
                {
                    var status = (item["STATUS"]?.ToString() ?? string.Empty).Trim();
                    if (status.Equals("CANCELADO", StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (c.Sigfi != "Todos")
                    {
                        var tipoSigfi = (item["TIPODESIGFI"]?.ToString() ?? "").Trim();
                        if (!tipoSigfi.Equals(c.Sigfi, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    if (!string.IsNullOrEmpty(c.NumOs))
                    {
                        var numOs = item["NUMOS"]?.ToString() ?? string.Empty;
                        if (numOs.IndexOf(c.NumOs, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }
                    if (!string.IsNullOrEmpty(c.IdSigfi))
                    {
                        var idSigfi = item["IDSIGFI"]?.ToString() ?? string.Empty;
                        if (idSigfi.IndexOf(c.IdSigfi, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }
                    var rota = (item["ROTA"]?.ToString() ?? string.Empty).Trim();
                    if (c.Regional != "Todos")
                    {
                        if (!_regionalRotas.TryGetValue(c.Regional, out var rotas) || !rotas.Contains(rota))
                            return false;
                    }
                    if (c.Rota != "Todos")
                    {
                        if (!rota.Equals(c.Rota, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    if (c.TipoServico != "Todos")
                    {
                        var tipo = (item["TIPO"]?.ToString() ?? "").Trim();
                        if (!tipo.Equals(c.TipoServico, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }

                    if (c.Empresa != "Todos")
                    {
                        var emp = (item["EMPRESA"]?.ToString() ?? string.Empty).Trim();
                        if (!emp.Equals(c.Empresa, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }

                    if (!string.IsNullOrEmpty(c.FuncionarioTermo))
                    {
                        var desc = item["DESCADICIONALEXEC"]?.ToString() ?? string.Empty;
                        if (c.FuncionarioCampo == 0)
                        {
                            bool ok = false;
                            foreach (Match m in Regex.Matches(desc, "#(\\d+)"))
                            {
                                var mat = m.Groups[1].Value.TrimStart('0');
                                if (mat.IndexOf(c.FuncionarioTermo.TrimStart('0'), StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    ok = true;
                                    break;
                                }
                            }
                            if (!ok) return false;
                        }
                        else
                        {
                            bool ok = false;
                            foreach (Match m in Regex.Matches(desc, "#(\\d+)"))
                            {
                                var mat = m.Groups[1].Value.TrimStart('0');
                                if (c.FuncionarioMap != null && c.FuncionarioMap.TryGetValue(mat, out var nome))
                                {
                                    if (nome.IndexOf(c.FuncionarioTermo, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        ok = true;
                                        break;
                                    }
                                }
                            }
                            if (!ok)
                            {
                                var nomes = item["FUNCIONARIOS"]?.ToString() ?? string.Empty;
                                if (nomes.IndexOf(c.FuncionarioTermo, StringComparison.OrdinalIgnoreCase) < 0)
                                    return false;
                            }
                        }
                    }

                    if (c.OnlyDatalog)
                    {
                        var tem = item["TEMDATALOG"]?.ToObject<bool>() ?? false;
                        if (!tem) return false;
                    }

                    if (c.OnlyInstalacao)
                    {
                        var coord = item["LATLONCONF"]?.ToString();
                        if (string.IsNullOrWhiteSpace(coord)) return false;
                    }

                    string dtRec = null;
                    string dtCon;
                    bool isOpen;
                    bool isClosed;

                    if (c.OnlyInstalacao)
                    {
                        dtCon = item["CONCLUSAO"]?.ToString();
                        isOpen = false;
                        isClosed = !string.IsNullOrWhiteSpace(dtCon);
                    }
                    else
                    {
                        dtRec = item["DTAHORARECLAMACAO"]?.ToString();
                        dtCon = item["DTCONCLUSAO"]?.ToString();
                        isOpen = !string.IsNullOrWhiteSpace(dtRec) && string.IsNullOrWhiteSpace(dtCon);
                        isClosed = !string.IsNullOrWhiteSpace(dtCon);
                    }

                    if (isOpen && !c.ShowOpen) return false;
                    if (isClosed && !c.ShowClosed) return false;

                    if (c.StartDate.HasValue || c.EndDate.HasValue)
                    {
                        DateTime dt;
                        var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
                        bool parsed = false;
                        if (isOpen && dtRec != null)
                            parsed = DateTime.TryParse(dtRec, pt, System.Globalization.DateTimeStyles.None, out dt);
                        else if (isClosed)
                            parsed = DateTime.TryParse(dtCon, pt, System.Globalization.DateTimeStyles.None, out dt);

                        if (parsed)
                        {
                            if (c.StartDate.HasValue && dt < c.StartDate.Value) return false;
                            if (c.EndDate.HasValue && dt > c.EndDate.Value) return false;
                        }
                        else return false;
                    }

                    if (c.PrazoDias > 0 && c.TipoPrazo != "Todos")
                    {
                        bool has = false;
                        bool ok = false;

                        if (item["CORR_DIAS"] != null && int.TryParse(item["CORR_DIAS"].ToString(), out var corr))
                        {
                            has = true;
                            if (CheckPrazo(corr, c))
                                ok = true;
                        }
                        if (item["PREV_DIAS"] != null && int.TryParse(item["PREV_DIAS"].ToString(), out var prev))
                        {
                            has = true;
                            if (CheckPrazo(prev, c))
                                ok = true;
                        }

                        if (has && !ok)
                            return false;
                    }

                    return true;
                })
                .ToList();

            filtered = filtered
                .GroupBy(o => (o["NUMOS"]?.ToString() ?? string.Empty).Trim(),
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (c.PreventivasPorRota > 0)
            {
                filtered = filtered
                    .Where(o =>
                    {
                        var tipo = (o["TIPO"]?.ToString() ?? string.Empty).Trim();
                        if (!string.Equals(tipo, "PREVENTIVA", StringComparison.OrdinalIgnoreCase))
                            return false;

                        var dtRec = o["DTAHORARECLAMACAO"]?.ToString();
                        var dtCon = o["DTCONCLUSAO"]?.ToString();
                        bool isOpen = !string.IsNullOrWhiteSpace(dtRec) && string.IsNullOrWhiteSpace(dtCon);
                        if (!isOpen)
                            return false;

                        var tipoCausa = o["TIPOCAUSA"]?.ToString() ?? string.Empty;
                        var m = Regex.Match(tipoCausa, @"(\d+)\s*A$", RegexOptions.IgnoreCase);
                        if (!m.Success)
                            return false;

                        if (int.TryParse(m.Groups[1].Value, out var num))
                            return num == c.PreventivasPorRota;

                        return false;
                    })
                    .ToList();
            }

            if (c.PreventivasConcluidasPorRota > 0)
            {
                filtered = filtered
                    .Where(o =>
                    {
                        var tipo = (o["TIPO"]?.ToString() ?? string.Empty).Trim();
                        if (!string.Equals(tipo, "PREVENTIVA", StringComparison.OrdinalIgnoreCase))
                            return false;

                        var dtCon = o["DTCONCLUSAO"]?.ToString();
                        bool isClosed = !string.IsNullOrWhiteSpace(dtCon);
                        if (!isClosed)
                            return false;

                        var tipoCausa = o["TIPOCAUSA"]?.ToString() ?? string.Empty;
                        var m = Regex.Match(tipoCausa, @"(\d+)\s*A$", RegexOptions.IgnoreCase);
                        if (!m.Success)
                            return false;

                        if (int.TryParse(m.Groups[1].Value, out var num))
                            return num == c.PreventivasConcluidasPorRota;

                        return false;
                    })
                    .ToList();
            }

            if (c.SingleClientMarker)
            {
                filtered = filtered
                    .GroupBy(GetClientId, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var first = (JObject)g.First().DeepClone();
                        var list = g.Select(o => (o["NUMOS"]?.ToString() ?? string.Empty).Trim())
                                     .Where(s => !string.IsNullOrWhiteSpace(s))
                                     .Distinct(StringComparer.OrdinalIgnoreCase);
                        first["NUMOS_LIST"] = string.Join(", ", list);
                        return first;
                    })
                    .ToList();
            }

            return filtered;
        }

        private static bool CheckPrazo(int dias, FilterCriteria c)
        {
            return c.TipoPrazo switch
            {
                "Restantes" => dias >= 0 && dias <= c.PrazoDias,
                "Vencidos" => dias < 0 && -dias <= c.PrazoDias,
                "Exato" => Math.Abs(dias) == c.PrazoDias,
                _ => true
            };
        }
    }
}
