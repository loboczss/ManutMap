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
            {"Sena Madureira", new List<string>{"11","12","52","54","55","57","58","59","63","65"}}
        };

        public List<JObject> Apply(JArray source, FilterCriteria c)
        {
            return source.OfType<JObject>()
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

                    var dtRec = item["DTAHORARECLAMACAO"]?.ToString();
                    var dtCon = item["DTCONCLUSAO"]?.ToString();
                    bool isOpen = !string.IsNullOrWhiteSpace(dtRec) && string.IsNullOrWhiteSpace(dtCon);
                    bool isClosed = !string.IsNullOrWhiteSpace(dtCon);

                    if (isOpen && !c.ShowOpen) return false;
                    if (isClosed && !c.ShowClosed) return false;

                    if (c.StartDate.HasValue || c.EndDate.HasValue)
                    {
                        DateTime dt;
                        if ((isOpen && DateTime.TryParse(dtRec, out dt)) ||
                            (isClosed && DateTime.TryParse(dtCon, out dt)))
                        {
                            if (c.StartDate.HasValue && dt < c.StartDate.Value) return false;
                            if (c.EndDate.HasValue && dt > c.EndDate.Value) return false;
                        }
                        else return false;
                    }

                    return true;
                })
                .ToList();
        }
    }
}
