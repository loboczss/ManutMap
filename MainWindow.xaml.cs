using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        public MainWindow()
        {
            InitializeComponent();

            // inicializa o mapa
            _mapService = new MapService(MapView);
            _mapService.InitializeAsync();

            // apenas quando a janela estiver carregada
            this.Loaded += (_, __) => LoadLocalAndPopulate();

            // associa eventos de filtro
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
        }

        private void LoadLocalAndPopulate()
        {
            // carrega do disco
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manutencoes_latest.json");
            _manutList = _fileService.LoadLocalJson(path) ?? new JArray();

            PopulateSigfiCombo();
            PopulateTipoCombo();
            PopulateRotaCombo();
            ApplyFilters();
        }

        private void PopulateSigfiCombo()
        {
            var tipos = _manutList
                .OfType<JObject>()
                .Select(o => o["TIPODESIGFI"]?.ToString().Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s);

            SigfiFilterCombo.Items.Clear();
            SigfiFilterCombo.Items.Add(new ComboBoxItem { Content = "Todos" });
            foreach (var t in tipos)
                SigfiFilterCombo.Items.Add(new ComboBoxItem { Content = t });
            SigfiFilterCombo.SelectedIndex = 0;
        }

        private void PopulateTipoCombo()
        {
            var tipos = _manutList
                .OfType<JObject>()
                .Select(o => o["TIPO"]?.ToString().Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s);

            TipoFilterCombo.Items.Clear();
            TipoFilterCombo.Items.Add(new ComboBoxItem { Content = "Todos" });
            foreach (var t in tipos)
                TipoFilterCombo.Items.Add(new ComboBoxItem { Content = t });
            TipoFilterCombo.SelectedIndex = 0;
        }

        private void PopulateRotaCombo()
        {
            var rotas = _manutList
                .OfType<JObject>()
                .Select(o => o["ROTA"]?.ToString().Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s);

            RotaFilterCombo.Items.Clear();
            RotaFilterCombo.Items.Add(new ComboBoxItem { Content = "Todos" });
            foreach (var r in rotas)
                RotaFilterCombo.Items.Add(new ComboBoxItem { Content = r });
            RotaFilterCombo.SelectedIndex = 0;
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadButton.IsEnabled = false;
            _manutList = await _spService.DownloadLatestJsonAsync();
            PopulateSigfiCombo();
            PopulateTipoCombo();
            PopulateRotaCombo();
            ApplyFilters();
            DownloadButton.IsEnabled = true;
        }

        private void FiltersChanged(object sender, RoutedEventArgs e)
        {
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

                        LatLonField = (LatLonFieldCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
                        ?? "LATLON"
            };

            var result = _filterSvc.Apply(_manutList, criteria);
            _mapService.AddMarkers(
                result,
                criteria.ShowOpen,
                criteria.ShowClosed,
                criteria.ColorOpen,
                criteria.ColorClosed,
                criteria.LatLonField
            );
        }
    }
}
