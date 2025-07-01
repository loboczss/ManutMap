using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ManutMap.Helpers;

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
    }
}
