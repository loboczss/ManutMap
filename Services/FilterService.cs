using System;
using System.Collections.Generic;
using System.Linq;
using ManutMap.Models;
using Newtonsoft.Json.Linq;

namespace ManutMap.Services
{
    public class FilterService
    {
        public List<JObject> Apply(JArray source, FilterCriteria c)
        {
            return source.OfType<JObject>()
                .Where(item =>
                {
                    if (c.Sigfi != "Todos" &&
                        !item["TIPODESIGFI"]?.ToString().Trim()
                             .Equals(c.Sigfi, StringComparison.OrdinalIgnoreCase) == true)
                        return false;
                    if (!string.IsNullOrEmpty(c.NumOs) &&
                        item["NUMOS"]?.ToString()
                             .IndexOf(c.NumOs, StringComparison.OrdinalIgnoreCase) < 0)
                        return false;
                    if (!string.IsNullOrEmpty(c.IdSigfi) &&
                        item["IDSIGFI"]?.ToString()
                             .IndexOf(c.IdSigfi, StringComparison.OrdinalIgnoreCase) < 0)
                        return false;
                    if (!string.IsNullOrEmpty(c.Rota) &&
                        item["ROTA"]?.ToString()
                             .IndexOf(c.Rota, StringComparison.OrdinalIgnoreCase) < 0)
                        return false;
                    if (c.TipoServico != "Todos" &&
                        !item["TIPO"]?.ToString().Trim()
                             .Equals(c.TipoServico, StringComparison.OrdinalIgnoreCase) == true)
                        return false;

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
                            if (dt < c.StartDate || dt > c.EndDate) return false;
                        }
                        else return false;
                    }

                    return true;
                })
                .ToList();
        }
    }
}
