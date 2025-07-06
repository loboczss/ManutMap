using System;
using System.Collections.Generic;
using System.Linq;
using ManutMap.Models;
using Newtonsoft.Json.Linq;

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

        public List<JObject> Apply(JArray source, FilterCriteria c)
        {
            var filtered = source.OfType<JObject>()
                .Where(item =>
                {
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

                    if (c.OnlyDatalog)
                    {
                        var tem = item["TEMDATALOG"]?.ToObject<bool>() ?? false;
                        if (!tem) return false;
                    }

                    var dtRec = item["DTAHORARECLAMACAO"]?.ToString();
                    var dtCon = item["DTCONCLUSAO"]?.ToString();
                    bool isOpen = !string.IsNullOrWhiteSpace(dtRec) && string.IsNullOrWhiteSpace(dtCon);
                    bool isClosed = !string.IsNullOrWhiteSpace(dtCon);

                    if (isOpen && !c.ShowOpen) return false;
                    if (isClosed && !c.ShowClosed) return false;

                    if (c.StartDate.HasValue || c.EndDate.HasValue)
                    {
                        DateTime dt;
                        var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
                        if ((isOpen && DateTime.TryParse(dtRec, pt, System.Globalization.DateTimeStyles.None, out dt)) ||
                            (isClosed && DateTime.TryParse(dtCon, pt, System.Globalization.DateTimeStyles.None, out dt)))
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
                            ok |= CheckPrazo(corr, c);
                        }
                        if (item["PREV_DIAS"] != null && int.TryParse(item["PREV_DIAS"].ToString(), out var prev))
                        {
                            has = true;
                            ok |= CheckPrazo(prev, c);
                        }

                        if (!has || !ok)
                            return false;
                    }

                    return true;
                })
                .ToList();

            return filtered
                .GroupBy(o => (o["NUMOS"]?.ToString() ?? string.Empty).Trim(),
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
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
