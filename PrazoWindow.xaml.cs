using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace ManutMap
{
    public partial class PrazoWindow : Window
    {
        public class CorretivaInfo
        {
            public string NumOS { get; set; } = string.Empty;
            public string IdSigfi { get; set; } = string.Empty;
            public string Cliente { get; set; } = string.Empty;
            public DateTime Abertura { get; set; }
            public int DiasRestantes { get; set; }
        }

        public class PreventivaInfo
        {
            public string NumOS { get; set; } = string.Empty;
            public string IdSigfi { get; set; } = string.Empty;
            public string Cliente { get; set; } = string.Empty;
            public DateTime Ultima { get; set; }
            public DateTime Proxima { get; set; }
            public int DiasRestantes { get; set; }
        }

        public PrazoWindow(JArray dados)
        {
            InitializeComponent();
            GridCorretivas.ItemsSource = GetCorretivas(dados);
            GridPreventivas.ItemsSource = GetPreventivas(dados);
        }

        private static List<CorretivaInfo> GetCorretivas(JArray dados)
        {
            var list = new List<CorretivaInfo>();
            var pt = CultureInfo.GetCultureInfo("pt-BR");
            foreach (JObject obj in dados.OfType<JObject>())
            {
                var tipo = obj["TIPO"]?.ToString().Trim();
                if (!string.Equals(tipo, "CORRETIVA", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(obj["DTCONCLUSAO"]?.ToString()))
                    continue;
                var dtStr = obj["DTAHORARECLAMACAO"]?.ToString();
                if (DateTime.TryParse(dtStr, pt, DateTimeStyles.None, out var dt))
                {
                    int diasRest = 5 - (int)(DateTime.Today - dt.Date).TotalDays;
                    list.Add(new CorretivaInfo
                    {
                        NumOS = obj["NUMOS"]?.ToString() ?? string.Empty,
                        IdSigfi = obj["IDSIGFI"]?.ToString() ?? string.Empty,
                        Cliente = obj["NOMECLIENTE"]?.ToString() ?? string.Empty,
                        Abertura = dt,
                        DiasRestantes = diasRest
                    });
                }
            }
            return list.OrderBy(i => i.DiasRestantes).ToList();
        }

        private static List<PreventivaInfo> GetPreventivas(JArray dados)
        {
            var dict = new Dictionary<string, (DateTime dt, string cliente, string numOs, string idSigfi)>();
            var pt = CultureInfo.GetCultureInfo("pt-BR");
            foreach (JObject obj in dados.OfType<JObject>())
            {
                var tipo = obj["TIPO"]?.ToString().Trim();
                if (!string.Equals(tipo, "PREVENTIVA", StringComparison.OrdinalIgnoreCase))
                    continue;
                var dtStr = obj["DTCONCLUSAO"]?.ToString();
                if (string.IsNullOrWhiteSpace(dtStr))
                    continue;
                if (!DateTime.TryParse(dtStr, pt, DateTimeStyles.None, out var dt))
                    continue;
                string id = obj["IDSIGFI"]?.ToString()?.Trim() ?? string.Empty;
                if (id == string.Empty)
                    id = obj["NOMECLIENTE"]?.ToString()?.Trim() ?? string.Empty;
                if (id == string.Empty)
                    continue;
                string cliente = obj["NOMECLIENTE"]?.ToString() ?? id;
                string numOs = obj["NUMOS"]?.ToString() ?? string.Empty;
                string idSigfi = obj["IDSIGFI"]?.ToString() ?? string.Empty;
                if (!dict.ContainsKey(id) || dict[id].dt < dt)
                    dict[id] = (dt, cliente, numOs, idSigfi);
            }
            var list = new List<PreventivaInfo>();
            foreach (var kv in dict)
            {
                var proxima = kv.Value.dt.AddMonths(4);
                int dias = (int)Math.Ceiling((proxima.Date - DateTime.Today).TotalDays);
                list.Add(new PreventivaInfo
                {
                    NumOS = kv.Value.numOs,
                    IdSigfi = kv.Value.idSigfi,
                    Cliente = kv.Value.cliente,
                    Ultima = kv.Value.dt,
                    Proxima = proxima,
                    DiasRestantes = dias
                });
            }
            return list.OrderBy(i => i.DiasRestantes).ToList();
        }

        private void GridCorretivas_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is CorretivaInfo info)
            {
                e.Row.Background = info.DiasRestantes <= 0 ? Brushes.IndianRed :
                                    info.DiasRestantes <= 2 ? Brushes.Orange :
                                    Brushes.LightGreen;
            }
        }

        private void GridPreventivas_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is PreventivaInfo info)
            {
                e.Row.Background = info.DiasRestantes <= 0 ? Brushes.IndianRed :
                                    info.DiasRestantes <= 30 ? Brushes.Orange :
                                    Brushes.LightGreen;
            }
        }

        private void MapaCorretiva_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id && Owner is MainWindow mw)
            {
                mw.ShowClientOnMap(id);
                mw.Activate();
            }
        }

        private void MapaPreventiva_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id && Owner is MainWindow mw)
            {
                mw.ShowClientOnMap(id);
                mw.Activate();
            }
        }
    }
}
