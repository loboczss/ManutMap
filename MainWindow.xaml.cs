using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
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

        private readonly DispatcherTimer _debounceTimer;

        public MainWindow()
        {
            InitializeComponent();

            // --- CORREÇÃO 1: INICIALIZAÇÃO DO TIMER MOVIDA PARA CIMA ---
            // Isso garante que o timer exista antes que qualquer evento seja disparado.
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _debounceTimer.Tick += DebouncedApplyFilters;

            // O resto da inicialização continua normalmente
            _mapService = new MapService(MapView);
            _mapService.InitializeAsync();

            this.Loaded += (_, __) => LoadLocalAndPopulate();

            // Associa todos os eventos de filtro a um único handler
            SigfiFilterCombo.SelectionChanged += FiltersChanged;
            TipoFilterCombo.SelectionChanged += FiltersChanged;
            NumOsFilterBox.TextChanged += FiltersChanged;
            IdSigfiFilterBox.TextChanged += FiltersChanged;
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

            PopulateComboBox(SigfiFilterCombo, "TIPODESIGFI");
            PopulateComboBox(TipoFilterCombo, "TIPO");
            PopulateComboBox(RotaFilterCombo, "ROTA");

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

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadButton.IsEnabled = false;
            _manutList = await _spService.DownloadLatestJsonAsync();

            PopulateComboBox(SigfiFilterCombo, "TIPODESIGFI");
            PopulateComboBox(TipoFilterCombo, "TIPO");
            PopulateComboBox(RotaFilterCombo, "ROTA");

            ApplyFilters();
            DownloadButton.IsEnabled = true;
        }

        private void FiltersChanged(object sender, RoutedEventArgs e)
        {
            // --- CORREÇÃO 2: VERIFICAÇÃO DE SEGURANÇA ---
            // Impede o erro caso o evento dispare antes da hora.
            if (_debounceTimer == null) return;

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebouncedApplyFilters(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_manutList == null) return;

            var criteria = new FilterCriteria
            {
                Sigfi = (SigfiFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todos",
                TipoServico = (TipoFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todos",
                NumOs = NumOsFilterBox.Text.Trim(),
                IdSigfi = IdSigfiFilterBox.Text.Trim(),
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

            var filteredResult = _filterSvc.Apply(_manutList, criteria);

            if (criteria.ColorByTipoSigfi)
            {
                _mapService.AddMarkersByTipoSigfi(filteredResult, criteria.ShowOpen, criteria.ShowClosed, criteria.ColorPreventiva, criteria.ColorCorretiva, criteria.LatLonField);
            }
            else
            {
                _mapService.AddMarkers(filteredResult, criteria.ShowOpen, criteria.ShowClosed, criteria.ColorOpen, criteria.ColorClosed, criteria.LatLonField);
            }

            StatsTextBlock.Text = GerarTextoEstatisticas(filteredResult, criteria);
        }

        private string GerarTextoEstatisticas(IEnumerable<JObject> dadosFiltrados, FilterCriteria criteria)
        {
            if (dadosFiltrados == null) return "Carregando dados...";

            int prevAbertas = dadosFiltrados.Count(item => item["TIPO"]?.ToString() == "preventiva" && string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));
            int prevConcluidas = dadosFiltrados.Count(item => item["TIPO"]?.ToString() == "preventiva" && !string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));

            int corrAbertas = dadosFiltrados.Count(item => item["TIPO"]?.ToString() == "corretiva" && string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));
            int corrConcluidas = dadosFiltrados.Count(item => item["TIPO"]?.ToString() == "corretiva" && !string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));

            int servAbertos = dadosFiltrados.Count(item => item["TIPO"]?.ToString() == "servicos" && string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));
            int servConcluidos = dadosFiltrados.Count(item => item["TIPO"]?.ToString() == "servicos" && !string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString()));

            var sb = new StringBuilder();
            string tipoFiltro = criteria.TipoServico.ToLowerInvariant();

            if (tipoFiltro == "todos" || tipoFiltro == "preventiva")
            {
                sb.AppendLine($"Preventivas: {prevAbertas} abertas, {prevConcluidas} concluídas.");
            }
            if (tipoFiltro == "todos" || tipoFiltro == "corretiva")
            {
                sb.AppendLine($"Corretivas: {corrAbertas} abertas, {corrConcluidas} concluídas.");
            }
            if (tipoFiltro == "todos" || tipoFiltro == "servicos")
            {
                sb.AppendLine($"Serviços: {servAbertos} abertos, {servConcluidos} concluídos.");
            }

            sb.Append($"Total Exibido: {dadosFiltrados.Count()}");

            return sb.ToString();
        }
    }
}