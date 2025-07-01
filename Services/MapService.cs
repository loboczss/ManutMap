using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ManutMap.Helpers;

namespace ManutMap.Services
{
    public class MapService
    {
        private readonly WebView2 _view;
        private readonly List<string> _pendingScripts = new();

        public MapService(WebView2 view)
        {
            _view = view;
            // Assina o evento CORRETO
            _view.CoreWebView2InitializationCompleted += OnWebView2Initialized;
        }

        public async Task InitializeAsync()
        {
            // Garante inicialização do WebView2
            await _view.EnsureCoreWebView2Async();
            _view.NavigateToString(MapHtmlHelper.GetHtml());
        }

        private void OnWebView2Initialized(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            foreach (var script in _pendingScripts)
                _view.CoreWebView2.ExecuteScriptAsync(script);
            _pendingScripts.Clear();
        }

        public void AddMarkers(IEnumerable<JObject> data,
                               bool showOpen,
                               bool showClosed,
                               string colorOpen,
                               string colorClosed)
        {
            var json = JsonConvert.SerializeObject(data);
            var script =
                $"addMarkers({json},{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()}," +
                $"'{colorOpen}','{colorClosed}');";

            if (_view.CoreWebView2 == null)
                _pendingScripts.Add(script);
            else
                _view.CoreWebView2.ExecuteScriptAsync(script);
        }
    }
}
