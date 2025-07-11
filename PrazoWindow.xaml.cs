using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace ManutMap
{
    public partial class PrazoWindow : Window
    {
        private readonly JArray _dados;
        private List<CorretivaInfo> _allCorretivas;
        private List<PreventivaInfo> _allPreventivas;
        private readonly Dictionary<string, List<string>> _regionalRotas = new()
        {
            {"Cruzeiro do Sul", new List<string>{"01","02","03","04","05","14","15","16","17","30","32","34","36","39","40","42","06"}},
            {"Tarauacá", new List<string>{"07","08","09","10","21","22","23","24","33","38","37","50","51","66","67","71"}},
            {"Sena Madureira", new List<string>{"11","12","52","54","55","57","58","59","63","65"}},
            {"Mato Grosso", new List<string>{"501","502","503","504","505","506","507","508","509","510","511","513","514"}}
        };
        public class CorretivaInfo
        {
            public string NumOS { get; set; } = string.Empty;
            public string IdSigfi { get; set; } = string.Empty;
            public string Cliente { get; set; } = string.Empty;
            public string Rota { get; set; } = string.Empty;
            public DateTime Abertura { get; set; }
            public int DiasRestantes { get; set; }
        }

        public class PreventivaInfo
        {
            public string NumOS { get; set; } = string.Empty;
            public string IdSigfi { get; set; } = string.Empty;
            public string Cliente { get; set; } = string.Empty;
            public string Rota { get; set; } = string.Empty;
            public DateTime Ultima { get; set; }
            public DateTime Proxima { get; set; }
            public int DiasRestantes { get; set; }
        }

        public PrazoWindow(JArray dados)
        {
            InitializeComponent();
            _dados = dados;
            _allCorretivas = GetCorretivas(dados);
            _allPreventivas = GetPreventivas(dados);

            PopulateCombos();
            ApplyFilters();
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
                    int diasRest = 3 - (int)(DateTime.Today - dt.Date).TotalDays;
                    list.Add(new CorretivaInfo
                    {
                        NumOS = obj["NUMOS"]?.ToString() ?? string.Empty,
                        IdSigfi = obj["IDSIGFI"]?.ToString() ?? string.Empty,
                        Cliente = obj["NOMECLIENTE"]?.ToString() ?? string.Empty,
                        Rota = obj["ROTA"]?.ToString() ?? string.Empty,
                        Abertura = dt,
                        DiasRestantes = diasRest
                    });
                }
            }
            return list.OrderBy(i => i.DiasRestantes).ToList();
        }

        private static List<PreventivaInfo> GetPreventivas(JArray dados)
        {
            var dict = new Dictionary<string, (DateTime dt, string cliente, string numOs, string idSigfi, string rota)>();
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
                string rota = obj["ROTA"]?.ToString() ?? string.Empty;
                if (!dict.ContainsKey(id) || dict[id].dt < dt)
                    dict[id] = (dt, cliente, numOs, idSigfi, rota);
            }
            var list = new List<PreventivaInfo>();
            foreach (var kv in dict)
            {
                var proxima = kv.Value.dt.AddMonths(6);
                int dias = (int)Math.Ceiling((proxima.Date - DateTime.Today).TotalDays);

                // Ignore clients more than 60 days overdue
                if (dias < -60) continue;

                list.Add(new PreventivaInfo
                {
                    NumOS = kv.Value.numOs,
                    IdSigfi = kv.Value.idSigfi,
                    Cliente = kv.Value.cliente,
                    Rota = kv.Value.rota,
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
            if (sender is Button btn && btn.Tag is CorretivaInfo info && Owner is MainWindow mw)
            {
                mw.ShowClientOnMap(info.IdSigfi, info.NumOS);
                mw.Activate();
            }
        }

        private void MapaPreventiva_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PreventivaInfo info && Owner is MainWindow mw)
            {
                mw.ShowClientOnMap(info.IdSigfi, info.NumOS);
                mw.Activate();
            }
        }

        private void PopulateCombos()
        {
            RegionalFilterCombo.Items.Clear();
            RegionalFilterCombo.Items.Add("Todos");
            foreach (var r in _regionalRotas.Keys)
                RegionalFilterCombo.Items.Add(r);
            RegionalFilterCombo.SelectedIndex = 0;

            PopulateRotaCombo();

            TipoPrazoCombo.Items.Clear();
            TipoPrazoCombo.Items.Add("Todos");
            TipoPrazoCombo.Items.Add("Restantes");
            TipoPrazoCombo.Items.Add("Vencidos");
            TipoPrazoCombo.Items.Add("Exato");
            TipoPrazoCombo.SelectedIndex = 0;
        }

        private void PopulateRotaCombo()
        {
            RotaFilterCombo.Items.Clear();
            RotaFilterCombo.Items.Add("Todos");

            string reg = RegionalFilterCombo.SelectedItem as string ?? "Todos";
            IEnumerable<string> rotas;
            if (reg != "Todos" && _regionalRotas.TryGetValue(reg, out var rts))
                rotas = rts;
            else
                rotas = _dados.OfType<JObject>().Select(o => o["ROTA"]?.ToString()?.Trim())
                               .Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s);

            foreach (var r in rotas)
                RotaFilterCombo.Items.Add(r);
            RotaFilterCombo.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            IEnumerable<CorretivaInfo> cor = _allCorretivas;
            IEnumerable<PreventivaInfo> prev = _allPreventivas;

            string reg = RegionalFilterCombo.SelectedItem as string ?? "Todos";
            string rota = RotaFilterCombo.SelectedItem as string ?? "Todos";
            string tipoPrazo = TipoPrazoCombo.SelectedItem as string ?? "Todos";
            int diasFiltro = 0;
            int.TryParse(DiasTextBox.Text, out diasFiltro);

            if (reg != "Todos" && _regionalRotas.TryGetValue(reg, out var rts))
            {
                cor = cor.Where(c => rts.Contains(c.Rota));
                prev = prev.Where(p => rts.Contains(p.Rota));
            }
            if (rota != "Todos")
            {
                cor = cor.Where(c => c.Rota == rota);
                prev = prev.Where(p => p.Rota == rota);
            }

            if (tipoPrazo == "Restantes" && diasFiltro > 0)
            {
                cor = cor.Where(c => c.DiasRestantes >= 0 && c.DiasRestantes <= diasFiltro);
                prev = prev.Where(p => p.DiasRestantes >= 0 && p.DiasRestantes <= diasFiltro);
            }
            else if (tipoPrazo == "Vencidos" && diasFiltro > 0)
            {
                cor = cor.Where(c => c.DiasRestantes < 0 && -c.DiasRestantes <= diasFiltro);
                prev = prev.Where(p => p.DiasRestantes < 0 && -p.DiasRestantes <= diasFiltro);
            }
            else if (tipoPrazo == "Exato")
            {
                cor = cor.Where(c => Math.Abs(c.DiasRestantes) == diasFiltro);
                prev = prev.Where(p => Math.Abs(p.DiasRestantes) == diasFiltro);
            }

            GridCorretivas.ItemsSource = cor.ToList();
            GridPreventivas.ItemsSource = prev.ToList();
        }

        private void RegionalChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateRotaCombo();
            ApplyFilters();
        }

        private void FiltersChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void DiasTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void ShowFilteredButton_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is not MainWindow mw) return;
            var cor = GridCorretivas.ItemsSource as IEnumerable<CorretivaInfo> ?? Enumerable.Empty<CorretivaInfo>();
            var prev = GridPreventivas.ItemsSource as IEnumerable<PreventivaInfo> ?? Enumerable.Empty<PreventivaInfo>();
            var ids = cor.Select(c => c.IdSigfi).Concat(prev.Select(p => p.IdSigfi)).Distinct();
            mw.ShowClientsOnMap(ids);
            mw.Activate();
        }
    }
}
