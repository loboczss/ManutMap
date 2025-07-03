using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;
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
        private readonly DatalogService _datalogService = new DatalogService();
        private MapService _mapService;
        private JArray _manutList;
        private Dictionary<string, string> _datalogMap;
        private readonly Dictionary<string, List<string>> _regionalRotas = new()
        {
            {"Cruzeiro do Sul", new List<string>{"01","02","03","04","05","14","15","16","17","30","32","34","36","39","40","42","06"}},
            {"Tarauacá", new List<string>{"07","08","09","10","21","22","23","24","33","38","37","50","51","66","67","71"}},
            {"Sena Madureira", new List<string>{"11","12","52","54","55","57","58","59","63","65"}},
            {"Mato Grosso", new List<string>{"501","502","503","504","505","506","507","508","509","510","511","513","514"}}
        };

        private readonly DispatcherTimer _debounceTimer;
        private readonly DispatcherTimer _updateTimer;
        private bool _sidebarVisible = true;

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
            _updateTimer.Tick += async (_, __) => await SyncAndRefresh();

            _mapService = new MapService(MapView);
            _mapService.InitializeAsync();

            this.Loaded += async (_, __) =>
            {
                LoadLocalAndPopulate();
                await SyncAndRefresh();
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
            ColorOpenCombo.SelectionChanged += FiltersChanged;
            ColorClosedCombo.SelectionChanged += FiltersChanged;
            ChbColorPrev.Checked += FiltersChanged;
            ChbColorPrev.Unchecked += FiltersChanged;
            ChbColorCorr.Checked += FiltersChanged;
            ChbColorCorr.Unchecked += FiltersChanged;
            ChbColorServ.Checked += FiltersChanged;
            ChbColorServ.Unchecked += FiltersChanged;
            ColorTipoPrevCombo.SelectionChanged += FiltersChanged;
            ColorTipoCorrCombo.SelectionChanged += FiltersChanged;
            ColorTipoServCombo.SelectionChanged += FiltersChanged;
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

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            SyncButton.IsEnabled = false;
            await SyncAndRefresh();
            SyncButton.IsEnabled = true;
        }

        private async Task SyncAndRefresh()
        {
            _manutList = await _spService.DownloadLatestJsonAsync();

            _datalogMap = await _datalogService.GetAllDatalogFoldersAsync();
            AnnotateDatalogInfo();

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
                ColorOpen = (ColorOpenCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "#FF0000",
                ColorClosed = (ColorClosedCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "#008000",
                ColorPrevOn = ChbColorPrev.IsChecked == true,
                ColorCorrOn = ChbColorCorr.IsChecked == true,
                ColorServOn = ChbColorServ.IsChecked == true,
                ColorServicoPreventiva = (ColorTipoPrevCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "#0000FF",
                ColorServicoCorretiva = (ColorTipoCorrCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "#FFA500",
                ColorServicoOutros = (ColorTipoServCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "#008080",
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

            _mapService.AddMarkersSelective(filteredResult,
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

        private void AnnotateDatalogInfo()
        {
            if (_manutList == null || _datalogMap == null) return;

            foreach (var obj in _manutList.OfType<JObject>())
            {
                string num = (obj["NUMOS"]?.ToString() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(num)) continue;

                if (_datalogMap.TryGetValue(num, out var url))
                {
                    obj["TEMDATALOG"] = true;
                    obj["FOLDERURL"] = url;
                }
                else
                {
                    obj["TEMDATALOG"] = false;
                    obj["FOLDERURL"] = null;
                }
            }
        }

        public void UpdateDatalogMap(IEnumerable<OsInfo> infos)
        {
            if (infos == null) return;
            if (_datalogMap == null)
                _datalogMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var info in infos)
            {
                if (info.TemDatalog && !string.IsNullOrWhiteSpace(info.FolderUrl))
                {
                    _datalogMap[info.NumOS] = info.FolderUrl!;

                    if (_manutList != null)
                    {
                        foreach (var obj in _manutList.OfType<JObject>())
                        {
                            string num = (obj["NUMOS"]?.ToString() ?? string.Empty).Trim();
                            if (num.Equals(info.NumOS, StringComparison.OrdinalIgnoreCase))
                            {
                                obj["TEMDATALOG"] = true;
                                obj["FOLDERURL"] = info.FolderUrl;
                            }
                        }
                    }
                }
            }
        }

        private async void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manutList == null) return;

            var criteria = GetCurrentCriteria();
            var filtered = _filterSvc.Apply(_manutList, criteria);

            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"manutencoes_{DateTime.Now:yyyyMMddHHmmss}.csv",
                DefaultExt = "csv"
            };
            if (dialog.ShowDialog() == true)
            {
                _fileService.SaveCsv(filtered, dialog.FileName, criteria.LatLonField);
            }
            var html = Helpers.MapHtmlHelper.GetHtmlWithData(filtered,
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

            var fileName = $"mapa_{DateTime.Now:yyyyMMddHHmmss}.html";
            var link = await _spService.UploadHtmlAndShareAsync(fileName, html);
            if (!string.IsNullOrEmpty(link))
            {
                Clipboard.SetText(link);
                MessageBox.Show("Link copiado para a área de transferência:\n" + link,
                                "Link Compartilhado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarVisible)
            {
                SidebarColumn.Width = new GridLength(0);
                SidebarBorder.Visibility = Visibility.Collapsed;
                ToggleSidebarButton.Content = "►";
            }
            else
            {
                SidebarColumn.Width = new GridLength(340);
                SidebarBorder.Visibility = Visibility.Visible;
                ToggleSidebarButton.Content = "◄";
            }

            _sidebarVisible = !_sidebarVisible;
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SigfiFilterCombo.SelectedIndex = 0;
            TipoFilterCombo.SelectedIndex = 0;
            NumOsFilterBox.Text = string.Empty;
            IdSigfiFilterBox.Text = string.Empty;

            RegionalFilterCombo.SelectedIndex = 0;
            PopulateComboBox(RotaFilterCombo, "ROTA");
            RotaFilterCombo.SelectedIndex = 0;

            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;

            ChbOpen.IsChecked = true;
            ChbClosed.IsChecked = true;

            ColorOpenCombo.SelectedIndex = 0;
            ColorClosedCombo.SelectedIndex = 0;

            ChbColorPrev.IsChecked = false;
            ChbColorCorr.IsChecked = false;
            ChbColorServ.IsChecked = false;

            ColorTipoPrevCombo.SelectedIndex = 0;
            ColorTipoCorrCombo.SelectedIndex = 0;
            ColorTipoServCombo.SelectedIndex = 0;

            LatLonFieldCombo.SelectedIndex = 0;

            ApplyFilters();
        }

        private void DatalogButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new DatalogWindow();
            win.Owner = this;
            win.Show();
        }
    }
}