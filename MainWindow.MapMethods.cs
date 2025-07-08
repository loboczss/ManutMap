using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ManutMap
{
    public partial class MainWindow
    {
        public void ShowClientOnMap(string idSigfi, string? highlightOs = null)
        {
            if (_manutList == null || string.IsNullOrWhiteSpace(idSigfi)) return;

            var entries = _manutList
                .OfType<JObject>()
                .Where(o => string.Equals((o["IDSIGFI"]?.ToString() ?? string.Empty).Trim(),
                                        idSigfi.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(o =>
                {
                    var rec = o["DTAHORARECLAMACAO"]?.ToString();
                    var con = o["DTCONCLUSAO"]?.ToString();
                    return !string.IsNullOrWhiteSpace(rec) && string.IsNullOrWhiteSpace(con);
                })
                .ToList();
            if (entries.Count == 0) return;

            var osList = entries
                .Select(o => (o["NUMOS"]?.ToString() ?? string.Empty).Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var obj in entries)
            {
                obj["ALL_OS"] = string.Join(", ", osList);
                if (!string.IsNullOrWhiteSpace(highlightOs))
                    obj["HIGHLIGHT_OS"] = highlightOs;
            }

            var criteria = GetCurrentCriteria();
            _mapService.SetClustering(false);
            _mapService.AddMarkersSelective(entries,
                                            criteria.ShowOpen,
                                            criteria.ShowClosed,
                                            criteria.ColorOpen,
                                            criteria.ColorClosed,
                                            criteria.ColorServicoPreventiva,
                                            criteria.ColorServicoCorretiva,
                                            criteria.ColorServicoOutros,
                                            criteria.ColorPrevOn,
                                            criteria.ColorCorrOn,
                                            criteria.ColorServOn,
                                            criteria.LatLonField);
        }

        public void ShowClientsOnMap(IEnumerable<string> ids)
        {
            if (_manutList == null || ids == null) return;
            var set = new HashSet<string>(ids.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
            if (set.Count == 0) return;

            var entries = _manutList
                .OfType<JObject>()
                .Where(o => set.Contains((o["IDSIGFI"]?.ToString() ?? string.Empty).Trim()))
                .ToList();
            if (entries.Count == 0) return;

            var criteria = GetCurrentCriteria();
            _mapService.SetClustering(false);
            _mapService.AddMarkersSelective(entries,
                                            criteria.ShowOpen,
                                            criteria.ShowClosed,
                                            criteria.ColorOpen,
                                            criteria.ColorClosed,
                                            criteria.ColorServicoPreventiva,
                                            criteria.ColorServicoCorretiva,
                                            criteria.ColorServicoOutros,
                                            criteria.ColorPrevOn,
                                            criteria.ColorCorrOn,
                                            criteria.ColorServOn,
                                            criteria.LatLonField);
        }
    }
}
