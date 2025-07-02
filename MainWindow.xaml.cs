using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using ManutMap.Models;
using ManutMap.Services;

namespace ManutMap
{
    public partial class MainWindow : Window
    {
        private readonly SharePointService _spService = new SharePointService();
        private readonly FileService _fileService = new FileService();
        private readonly FilterService _filterSvc = new FilterService();
        private MapService _mapService;
        private JArray _manutList;
        private readonly Dictionary<string, List<string>> _regionalRotas = new()
        {
            {"Cruzeiro do Sul", new List<string>{"01","02","03","04","05","14","15","16","17","30","32","34","36","39","40","42","06"}},
            {"Tarauacá", new List<string>{"07","08","09","10","21","22","23","24","33","38","37","50","51","66","67","71"}},
            {"Sena Madureira", new List<string>{"11","12","52","54","55","57","58","59","63","65"}},
            {"Mato Grosso", new List<string>{"500","501","502","503","504","505","506","507","508","509","510","511","512","513","514"}}
        };

        private readonly DispatcherTimer _debounceTimer;
        private readonly DispatcherTimer _updateTimer;

        public MainWindow()
        {
            InitializeComponent();

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _debounceTimer.Tick += DebouncedApplyFilters;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30)
            };
            _updateTimer.Tick += async (_, __) => await DownloadAndRefresh();

            _mapService = new MapService(MapView);
            _mapService.InitializeAsync();

            this.Loaded += async (_, __) =>
            {
                LoadLocalAndPopulate();
                await DownloadAndRefresh();
                _updateTimer.Start();
            };

            // Associa todos os eventos de filtro a um único handler
            SigfiFilterCombo.SelectionChanged += FiltersChanged;
            TipoFilterCombo.SelectionChanged += FiltersChanged;
            NumOsFilterBox.TextChanged += FiltersChanged;
            IdSigfiFilterBox.TextChanged += FiltersChanged;
            RegionalFilterCombo.SelectionChanged += RegionalChanged;
            RotaFilterCombo.SelectionChanged += FiltersChanged;
            StartDatePicker.SelectedDateChanged += FiltersChanged;
            EndDatePicker.SelectedDateChanged += FiltersChanged;
            ChbOpen.Checked += FiltersChanged;
            ChbOpen.Unchecked += FiltersChanged;
            ChbClosed.Checked += FiltersChanged;
            ChbClosed.Unchecked += FiltersChanged;
            TxtColorOpen.TextChanged += FiltersChanged;
            TxtColorClosed.TextChanged += FiltersChanged;
            ChbColorBySigfi.Checked += FiltersChanged;
            ChbColorBySigfi.Unchecked += FiltersChanged;
            TxtColorPrev.TextChanged += FiltersChanged;
            TxtColorCorr.TextChanged += FiltersChanged;
        }

        private void LoadLocalAndPopulate()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manutencoes_latest.json");
            _manutList = _fileService.LoadLocalJson(path) ?? new JArray();

            if (File.Exists(path))
                LastUpdatedText.Text = File.GetLastWriteTime(path).ToString("dd/MM/yyyy HH:mm");

            PopulateComboBox(SigfiFilterCombo, "TIPODESIGFI");
            PopulateComboBox(TipoFilterCombo, "TIPO");
            PopulateComboBox(RotaFilterCombo, "ROTA");
            PopulateComboBox(RegionalFilterCombo, _regionalRotas.Keys);

            ApplyFilters();
        }

        private void PopulateComboBox(ComboBox comboBox, string jsonField)
        {
            var items = _manutList
                .OfType<JObject>()
                .Select(o => o[jsonField]?.ToString().Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            comboBox.Items.Clear();
            comboBox.Items.Add(new ComboBoxItem { Content = "Todos" });
            foreach (var item in items)
            {
                comboBox.Items.Add(new ComboBoxItem { Content = item });
            }
            comboBox.SelectedIndex = 0;
        }

        private void PopulateComboBox(ComboBox comboBox, IEnumerable<string> items)
        {
            comboBox.Items.Clear();
            comboBox.Items.Add(new ComboBoxItem { Content = "Todos" });
            foreach (var item in items.OrderBy(i => i))
            {
                comboBox.Items.Add(new ComboBoxItem { Content = item });
            }
            comboBox.SelectedIndex = 0;
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadButton.IsEnabled = false;
            await DownloadAndRefresh();
            DownloadButton.IsEnabled = true;
        }

        private async Task DownloadAndRefresh()
        {
            _manutList = await _spService.DownloadLatestJsonAsync();

            PopulateComboBox(SigfiFilterCombo, "TIPODESIGFI");
            PopulateComboBox(TipoFilterCombo, "TIPO");
            PopulateComboBox(RotaFilterCombo, "ROTA");
            PopulateComboBox(RegionalFilterCombo, _regionalRotas.Keys);

            ApplyFilters();
            LastUpdatedText.Text = _spService.LastUpdate.ToString("dd/MM/yyyy HH:mm");
        }

        private void FiltersChanged(object sender, RoutedEventArgs e)
        {
            if (_debounceTimer == null) return;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebouncedApplyFilters(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            ApplyFilters();
        }

        private FilterCriteria GetCurrentCriteria()
        {
            return new FilterCriteria
            {
                Sigfi = (SigfiFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todos",
                TipoServico = (TipoFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todos",
                NumOs = NumOsFilterBox.Text.Trim(),
                IdSigfi = IdSigfiFilterBox.Text.Trim(),
                Regional = (RegionalFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todos",
                Rota = (RotaFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todos",
                StartDate = StartDatePicker.SelectedDate,
                EndDate = EndDatePicker.SelectedDate,
                ShowOpen = ChbOpen.IsChecked == true,
                ShowClosed = ChbClosed.IsChecked == true,
                ColorOpen = TxtColorOpen.Text.Trim(),
                ColorClosed = TxtColorClosed.Text.Trim(),
                ColorByTipoSigfi = ChbColorBySigfi.IsChecked == true,
                ColorPreventiva = TxtColorPrev.Text.Trim(),
                ColorCorretiva = TxtColorCorr.Text.Trim(),
                LatLonField = (LatLonFieldCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "LATLON"
            };
        }

        private void RegionalChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (RegionalFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
            if (selected == "Todos")
            {
                PopulateComboBox(RotaFilterCombo, "ROTA");
            }
            else if (_regionalRotas.TryGetValue(selected, out var rotas))
            {
                PopulateComboBox(RotaFilterCombo, rotas);
            }
            FiltersChanged(sender, e);
        }

        private void ApplyFilters()
        {
            if (_manutList == null) return;

            var criteria = GetCurrentCriteria();

            var filteredResult = _filterSvc.Apply(_manutList, criteria);

            if (criteria.ColorByTipoSigfi)
            {
                _mapService.AddMarkersByTipoSigfi(filteredResult, criteria.ShowOpen, criteria.ShowClosed, criteria.ColorPreventiva, criteria.ColorCorretiva, criteria.LatLonField);
            }
            else
            {
                _mapService.AddMarkers(filteredResult, criteria.ShowOpen, criteria.ShowClosed, criteria.ColorOpen, criteria.ColorClosed, criteria.LatLonField);
            }

            AtualizarPainelEstatisticas(filteredResult);
        }

        // MÉTODO DE ESTATÍSTICAS ATUALIZADO para a nova barra
        private void AtualizarPainelEstatisticas(IEnumerable<JObject> dadosFiltrados)
        {
            if (dadosFiltrados == null) return;

            int prevAbertas = dadosFiltrados.Count(item =>
                string.Equals(item["TIPO"]?.ToString().Trim(), "PREVENTIVA", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));
            int prevConcluidas = dadosFiltrados.Count(item =>
                string.Equals(item["TIPO"]?.ToString().Trim(), "PREVENTIVA", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));

            int corrAbertas = dadosFiltrados.Count(item =>
                string.Equals(item["TIPO"]?.ToString().Trim(), "CORRETIVA", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));
            int corrConcluidas = dadosFiltrados.Count(item =>
                string.Equals(item["TIPO"]?.ToString().Trim(), "CORRETIVA", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));

            int servAbertos = dadosFiltrados.Count(item =>
                string.Equals(item["TIPO"]?.ToString().Trim(), "SERVICOS", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));
            int servConcluidos = dadosFiltrados.Count(item =>
                string.Equals(item["TIPO"]?.ToString().Trim(), "SERVICOS", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));

            // Atualiza cada TextBlock individualmente
            PreventivasStatsText.Text = $"{prevAbertas} abertas, {prevConcluidas} concluídas";
            CorretivasStatsText.Text = $"{corrAbertas} abertas, {corrConcluidas} concluídas";
            ServicosStatsText.Text = $"{servAbertos} abertos, {servConcluidos} concluídos";
            TotalStatsText.Text = dadosFiltrados.Count().ToString();
        }

        private async void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manutList == null) return;

            var criteria = GetCurrentCriteria();
            var filtered = _filterSvc.Apply(_manutList, criteria);
            var html = Helpers.MapHtmlHelper.GetHtmlWithData(filtered,
                                                            criteria.ColorByTipoSigfi,
                                                            criteria.ShowOpen,
                                                            criteria.ShowClosed,
                                                            criteria.ColorOpen,
                                                            criteria.ColorClosed,
                                                            criteria.ColorPreventiva,
                                                            criteria.ColorCorretiva,
                                                            criteria.LatLonField);

            var fileName = $"mapa_{DateTime.Now:yyyyMMddHHmmss}.html";
            var link = await _spService.UploadHtmlAndShareAsync(fileName, html);
            if (!string.IsNullOrEmpty(link))
            {
                Clipboard.SetText(link);
                MessageBox.Show("Link copiado para a área de transferência:\n" + link,
                                "Link Compartilhado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}