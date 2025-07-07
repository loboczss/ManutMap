using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ManutMap.Helpers;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ManutMap.Services
{
    public class MapService
    {
        private readonly WebView2 _view;
        private readonly List<string> _pendingScripts = new();
        private bool _ready = false;

        public MapService(WebView2 view)
        {
            _view = view;
        }

        public async Task InitializeAsync()
        {
            await _view.EnsureCoreWebView2Async();
            _view.NavigationCompleted += (s, e) =>
            {
                _ready = true;
                foreach (var script in _pendingScripts)
                    _view.ExecuteScriptAsync(script);
                _pendingScripts.Clear();
            };
            _view.NavigateToString(MapHtmlHelper.GetHtml());
        }

        public void SetClustering(bool enabled)
        {
            var script = $"setClustering({enabled.ToString().ToLower()});";
            if (!_ready)
                _pendingScripts.Add(script);
            else
                _view.ExecuteScriptAsync(script);
        }

        public void AddMarkers(IEnumerable<JObject> data,
                               bool showOpen,
                               bool showClosed,
                               string colorOpen,
                               string colorClosed,
                               string latLonField = "LATLON")
        {
            var json = JsonConvert.SerializeObject(data);
            var script =
                $"addMarkers({json},{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()}," +
                $"'{colorOpen}','{colorClosed}','{latLonField}');";

            if (!_ready)
                _pendingScripts.Add(script);
            else
                _view.ExecuteScriptAsync(script);
        }

        public void AddMarkersSelective(IEnumerable<JObject> data,
                                         bool showOpen,
                                         bool showClosed,
                                         string colorOpen,
                                         string colorClosed,
                                         string colorPrev,
                                         string colorCorr,
                                         string colorServ,
                                         bool colorPrevOn,
                                         bool colorCorrOn,
                                         bool colorServOn,
                                         string latLonField = "LATLON")
        {
            var json = JsonConvert.SerializeObject(data);
            var script =
                $"addMarkersSelective({json},{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()}," +
                $"'{colorOpen}','{colorClosed}','{colorPrev}','{colorCorr}','{colorServ}'," +
                $"{colorPrevOn.ToString().ToLower()},{colorCorrOn.ToString().ToLower()},{colorServOn.ToString().ToLower()},'{latLonField}');";

            if (!_ready)
                _pendingScripts.Add(script);
            else
                _view.ExecuteScriptAsync(script);
        }

        public void AddMarkersByTipoSigfi(IEnumerable<JObject> data,
                                           bool showOpen,
                                           bool showClosed,
                                           string colorPrev,
                                           string colorCorr,
                                           string latLonField = "LATLON")
        {
            var json = JsonConvert.SerializeObject(data);
            var script =
                $"addMarkersByTipoSigfi({json},{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()}," +
                $"'{colorPrev}','{colorCorr}','{latLonField}');";

            if (!_ready)
                _pendingScripts.Add(script);
            else
                _view.ExecuteScriptAsync(script);
        }

        public void AddMarkersByTipoServico(IEnumerable<JObject> data,
                                           bool showOpen,
                                           bool showClosed,
                                           string colorPrev,
                                           string colorCorr,
                                           string colorServ,
                                           string latLonField = "LATLON")
        {
            var json = JsonConvert.SerializeObject(data);
            var script =
                $"addMarkersByTipoServico({json},{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()}," +
                $"'{colorPrev}','{colorCorr}','{colorServ}','{latLonField}');";

            if (!_ready)
                _pendingScripts.Add(script);
            else
                _view.ExecuteScriptAsync(script);
        }

        public void AddMarkersByTipoServicoIcon(IEnumerable<JObject> data,
                                               bool showOpen,
                                               bool showClosed,
                                               string latLonField = "LATLON")
        {
            var json = JsonConvert.SerializeObject(data);
            var script =
                $"addMarkersByTipoServicoIcon({json},{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()},'{latLonField}');";

            if (!_ready)
                _pendingScripts.Add(script);
            else
                _view.ExecuteScriptAsync(script);
        }

        public void AddMarkersCustomIcon(IEnumerable<JObject> data,
                                          bool showOpen,
                                          bool showClosed,
                                          string iconUrl,
                                          string latLonField = "LATLON")
        {
            var json = JsonConvert.SerializeObject(data);
            var script =
                $"addMarkersCustomIcon({json},{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()},'{iconUrl}','{latLonField}');";

            if (!_ready)
                _pendingScripts.Add(script);
            else
                _view.ExecuteScriptAsync(script);
        }
    }
}
