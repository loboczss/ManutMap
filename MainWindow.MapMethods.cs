using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ManutMap
{
    public partial class MainWindow
    {
        public void ShowClientOnMap(string idSigfi)
        {
            if (_manutList == null || string.IsNullOrWhiteSpace(idSigfi)) return;

            var entries = _manutList
                .OfType<JObject>()
                .Where(o => string.Equals((o["IDSIGFI"]?.ToString() ?? string.Empty).Trim(),
                                        idSigfi.Trim(), StringComparison.OrdinalIgnoreCase))
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
