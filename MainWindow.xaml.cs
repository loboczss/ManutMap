using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Windows.Media;
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
        private JArray _instalList;
        private Dictionary<string, string> _datalogMap;
        private Dictionary<string, string>? _funcMap;
        private List<RouteCount> _routeStats = new();
        private List<RouteCount> _recentRouteStats = new();
        private List<RouteCount> _clientRouteStats = new();
        public int MaxRouteCount { get; private set; }
        public int MaxRecentRouteCount { get; private set; }
        public int MaxClientRouteCount { get; private set; }
        private bool _statsDirty = true;
        private List<JObject>? _filteredCache;
        private readonly List<OsSimpleInfo> _osSemDatalog = new();
        private readonly List<OsAlertInfo> _osAlertaDatalog = new();
        private readonly Dictionary<string, List<string>> _regionalRotas = new()
        {
            {"Cruzeiro do Sul", new List<string>{"01","02","03","04","05","14","15","16","17","30","32","34","36","39","40","42","06"}},
            {"Tarauacá", new List<string>{"07","08","09","10","21","22","23","24","33","38","37","50","51","66","67","71"}},
            {"Sena Madureira", new List<string>{"11","12","52","54","55","57","58","59","63","65"}},
            {"Mato Grosso", new List<string>{"501","502","503","504","505","506","507","508","509","510","511","513","514"}}
        };

        private readonly Dictionary<string, string> _iconUrls = new()
        {
            {"blue",   "https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-blue.png"},
            {"red",    "https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-red.png"},
            {"green",  "https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-green.png"},
            {"orange", "https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-orange.png"},
            {"yellow", "https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-yellow.png"},
            {"violet", "https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-violet.png"},
            {"grey",   "https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-grey.png"},
            {"black",  "https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-black.png"}
        };

        private readonly DispatcherTimer _debounceTimer;
        private readonly DispatcherTimer _updateTimer;
        private bool _sidebarVisible = true;
        private int _foldersVisited = 0;

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

            this.Loaded += async (_, __) =>
            {
                try
                {
                    _mapService = new MapService(MapView);
                    await _mapService.InitializeAsync();

                    await LoadLocalAndPopulateAsync();
                    _datalogMap = _datalogService.GetCachedDatalogFolders();
                    AnnotateDatalogInfo();
                    AnnotatePrazoInfo();
                    AnnotatePrevOpenCount();
                    await AnnotateFuncionariosInfoAsync();
                    ApplyFilters();

                    _ = SyncAndRefresh();
                    _updateTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Erro ao inicializar os componentes:\n" + ex.Message,
                        "Erro",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
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
            ChbOnlyDatalog.Checked += FiltersChanged;
            ChbOnlyDatalog.Unchecked += FiltersChanged;
            ChbOnlyInst.Checked += FiltersChanged;
            ChbOnlyInst.Unchecked += FiltersChanged;
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
            MarkerStyleCombo.SelectionChanged += FiltersChanged;
            ChbSingleClient.Checked += FiltersChanged;
            ChbSingleClient.Unchecked += FiltersChanged;
            ChbCluster.Checked += FiltersChanged;
            ChbCluster.Unchecked += FiltersChanged;

            PrazoDiasTextBox.TextChanged += FiltersChanged;
            PrazoDiasTextBox.PreviewTextInput += PrazoDiasTextBox_PreviewTextInput;
            TipoPrazoCombo.SelectionChanged += FiltersChanged;
            PrevCountRotaCombo.SelectionChanged += FiltersChanged;
            PrevClosedCountCombo.SelectionChanged += FiltersChanged;
            FuncFieldCombo.SelectionChanged += FiltersChanged;
            FuncSearchBox.TextChanged += FiltersChanged;
            EmpresaFilterCombo.SelectionChanged += FiltersChanged;
        }

        private void LoadLocalAndPopulate()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manutencoes_latest.json");
            _manutList = _fileService.LoadLocalJson(path) ?? new JArray();
            _instalList = _spService.DownloadInstalacaoJsonAsync().GetAwaiter().GetResult();
            AppendInstalacaoData(_instalList);

            if (File.Exists(path))
                LastUpdatedText.Text = File.GetLastWriteTime(path).ToString("dd/MM/yyyy HH:mm");

            PopulateComboBox(SigfiFilterCombo, "TIPODESIGFI");
            PopulateComboBox(TipoFilterCombo, "TIPO");
            UpdateRotaCombo();
            PopulateComboBox(RegionalFilterCombo, _regionalRotas.Keys);

            AnnotatePrazoInfo();
            AnnotatePrevOpenCount();
            _ = AnnotateFuncionariosInfoAsync();

            ApplyFilters();
        }

        private async Task LoadLocalAndPopulateAsync()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manutencoes_latest.json");
            _manutList = await _fileService.LoadLocalJsonAsync(path) ?? new JArray();
            _instalList = await _spService.DownloadInstalacaoJsonAsync();
            AppendInstalacaoData(_instalList);

            if (File.Exists(path))
                LastUpdatedText.Text = File.GetLastWriteTime(path).ToString("dd/MM/yyyy HH:mm");

            PopulateComboBox(SigfiFilterCombo, "TIPODESIGFI");
            PopulateComboBox(TipoFilterCombo, "TIPO");
            UpdateRotaCombo();
            PopulateComboBox(RegionalFilterCombo, _regionalRotas.Keys);

            AnnotatePrazoInfo();
            AnnotatePrevOpenCount();
            await AnnotateFuncionariosInfoAsync();

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

        private void UpdateRotaCombo(bool updateEmpresa = true)
        {
            var regionalSel = (RegionalFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
            var empresaSel = (EmpresaFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
            if (ChbOnlyInst.IsChecked == true)
            {
                var rotas = _instalList
                    .OfType<JObject>()
                    .Where(o => empresaSel == "Todos" ||
                                string.Equals(o["EMPRESA"]?.ToString()?.Trim(), empresaSel, StringComparison.OrdinalIgnoreCase))
                    .Select(o => o["ROTA"]?.ToString()?.Trim())
                    .Where(s => !string.IsNullOrEmpty(s));

                if (regionalSel != "Todos" && _regionalRotas.TryGetValue(regionalSel, out var regionalRotas))
                    rotas = rotas.Where(r => regionalRotas.Contains(r));

                PopulateComboBox(RotaFilterCombo, rotas.Distinct());
            }
            else
            {
                if (regionalSel == "Todos")
                {
                    PopulateComboBox(RotaFilterCombo, "ROTA");
                }
                else if (_regionalRotas.TryGetValue(regionalSel, out var rotas))
                {
                    PopulateComboBox(RotaFilterCombo, rotas);
                }
                else
                {
                    PopulateComboBox(RotaFilterCombo, "ROTA");
                }
            }
            if (updateEmpresa)
                UpdateEmpresaCombo();
        }

        private void UpdateEmpresaCombo()
        {
            if (ChbOnlyInst.IsChecked == true)
            {
                var empresas = _instalList
                    .OfType<JObject>()
                    .Select(o => o["EMPRESA"]?.ToString()?.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct();
                PopulateComboBox(EmpresaFilterCombo, empresas);
                EmpresaPanel.Visibility = Visibility.Visible;
            }
            else
            {
                EmpresaPanel.Visibility = Visibility.Collapsed;
                EmpresaFilterCombo.Items.Clear();
                EmpresaFilterCombo.Items.Add(new ComboBoxItem { Content = "Todos" });
                EmpresaFilterCombo.SelectedIndex = 0;
            }
        }

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            SyncButton.IsEnabled = false;
            var progress = new Progress<(int Percent, string Message)>(p =>
            {
                UpdateProgress(p.Percent, p.Message);
            });
            ShowProgress();
            await SyncAndRefresh(progress);
            HideProgress();
            SyncButton.IsEnabled = true;
        }

        private void ShowProgress()
        {
            SyncProgressBar.IsIndeterminate = false;
            SyncProgressBar.Minimum = 0;
            SyncProgressBar.Maximum = 100;
            SyncProgressBar.Value = 0;
            SyncProgressBar.Visibility = Visibility.Visible;
            SyncProgressText.Visibility = Visibility.Visible;
            FolderProgressText.Text = "0 pastas visitadas";
            FolderProgressText.Visibility = Visibility.Visible;
        }

        private void UpdateProgress(int percent, string message)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            SyncProgressBar.Value = percent;
            SyncProgressText.Text = $"{percent}% - {message}";
        }

        private void HideProgress()
        {
            SyncProgressBar.Visibility = Visibility.Collapsed;
            SyncProgressText.Visibility = Visibility.Collapsed;
            FolderProgressText.Visibility = Visibility.Collapsed;
        }

        private void UpdateFolderProgress(int count)
        {
            FolderProgressText.Text = $"{count} pastas visitadas";
        }

        private async Task SyncAndRefresh(IProgress<(int Percent, string Message)>? progress = null)
        {
            progress?.Report((0, "Iniciando..."));

            progress?.Report((10, "Baixando dados..."));
            _manutList = await _spService.DownloadLatestJsonAsync();
            _instalList = await _spService.DownloadInstalacaoJsonAsync();
            AppendInstalacaoData(_instalList);

            _foldersVisited = 0;
            var folderProgress = new Progress<int>(n =>
            {
                _foldersVisited += n;
                UpdateFolderProgress(_foldersVisited);
            });

            var subProgress = new Progress<(int Percent, string Message)>(p =>
            {
                int percent = 40 + p.Percent * 30 / 100;
                progress?.Report((percent, $"Sincronizando datalog... {p.Message}"));
                UpdateFolderProgress(_foldersVisited);
            });

            _datalogMap = await _datalogService.GetAllDatalogFoldersAsync(subProgress, folderProgress);
            AnnotateDatalogInfo();
            AnnotatePrazoInfo();
            AnnotatePrevOpenCount();
            await AnnotateFuncionariosInfoAsync();

            progress?.Report((70, "Atualizando filtros..."));
            PopulateComboBox(SigfiFilterCombo, "TIPODESIGFI");
            PopulateComboBox(TipoFilterCombo, "TIPO");
            UpdateRotaCombo();
            PopulateComboBox(RegionalFilterCombo, _regionalRotas.Keys);

            progress?.Report((85, "Aplicando filtros..."));
            ApplyFilters();
            LastUpdatedText.Text = _spService.LastUpdate.ToString("dd/MM/yyyy HH:mm");
            progress?.Report((100, "Concluído"));
        }

        private void FiltersChanged(object sender, RoutedEventArgs e)
        {
            if (_debounceTimer == null) return;
            if (sender == ChbOnlyInst)
            {
                if (ChbOnlyInst.IsChecked == true)
                {
                    foreach (ComboBoxItem item in LatLonFieldCombo.Items)
                    {
                        if ((item.Content?.ToString() ?? "") == "LATLONCONF")
                        {
                            if (LatLonFieldCombo.SelectedItem != item)
                                LatLonFieldCombo.SelectedItem = item;
                            break;
                        }
                    }
                }
                UpdateRotaCombo();
            }
            else if (sender == EmpresaFilterCombo)
            {
                UpdateRotaCombo(false);
            }
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
                Empresa = (EmpresaFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todos",
                PreventivasPorRota = int.TryParse((PrevCountRotaCombo.SelectedItem as ComboBoxItem)?.Content.ToString(), out var pc) ? pc : 0,
                PreventivasConcluidasPorRota = int.TryParse((PrevClosedCountCombo.SelectedItem as ComboBoxItem)?.Content.ToString(), out var pcc) ? pcc : 0,
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
                LatLonField =
                    ChbOnlyInst.IsChecked == true
                        ? "LATLONCONF"
                        : (LatLonFieldCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "LATLON",
                MarkerStyle = (MarkerStyleCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "circle",
                OnlyDatalog = ChbOnlyDatalog.IsChecked == true,
                OnlyInstalacao = ChbOnlyInst.IsChecked == true,
                UseClusters = ChbCluster.IsChecked != false,
                SingleClientMarker = ChbSingleClient.IsChecked == true,
                PrazoDias = int.TryParse(PrazoDiasTextBox.Text, out var pd) ? pd : 0,
                TipoPrazo = (TipoPrazoCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todos",
                FuncionarioTermo = FuncSearchBox.Text.Trim(),
                FuncionarioCampo = FuncFieldCombo.SelectedIndex,
                FuncionarioMap = _funcMap
            };
        }

        private void RegionalChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (RegionalFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
            UpdateRotaCombo();
            FiltersChanged(sender, e);
        }

        private void ApplyFilters()
        {
            if (_manutList == null) return;

            var criteria = GetCurrentCriteria();

            _mapService.SetClustering(criteria.UseClusters);

            var filteredResult = _filterSvc.Apply(_manutList, criteria);

            if (criteria.MarkerStyle == "circle")
            {
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
            }
            else if (_iconUrls.TryGetValue(criteria.MarkerStyle, out var iconUrl))
            {
                _mapService.AddMarkersCustomIcon(filteredResult,
                                                 criteria.ShowOpen,
                                                 criteria.ShowClosed,
                                                 iconUrl,
                                                 criteria.LatLonField);
            }
            else
            {
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
            }

            _filteredCache = filteredResult.ToList();
            AtualizarPainelEstatisticas(_filteredCache);
            _statsDirty = true;
            if (MainTabControl.SelectedIndex == 1)
            {
                _statsDirty = false;
                UpdateStatsTab(_filteredCache);
            }
        }

        // MÉTODO DE ESTATÍSTICAS ATUALIZADO para a nova barra
        private void AtualizarPainelEstatisticas(IEnumerable<JObject> dadosFiltrados)
        {
            if (dadosFiltrados == null) return;

            int prevAbertas = 0;
            int prevConcluidas = 0;
            int corrAbertas = 0;
            int corrConcluidas = 0;
            int servAbertos = 0;
            int servConcluidos = 0;
            int datalogCount = 0;
            int totalCount = 0;
            var clients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in dadosFiltrados)
            {
                totalCount++;

                bool isConcluida = !string.IsNullOrWhiteSpace(item["DTCONCLUSAO"]?.ToString());
                string tipo = item["TIPO"]?.ToString().Trim() ?? string.Empty;

                if (string.Equals(tipo, "PREVENTIVA", StringComparison.OrdinalIgnoreCase))
                {
                    if (isConcluida) prevConcluidas++; else prevAbertas++;
                }
                else if (string.Equals(tipo, "CORRETIVA", StringComparison.OrdinalIgnoreCase))
                {
                    if (isConcluida) corrConcluidas++; else corrAbertas++;
                }
                else if (string.Equals(tipo, "SERVICOS", StringComparison.OrdinalIgnoreCase))
                {
                    if (isConcluida) servConcluidos++; else servAbertos++;
                }

                string cid = item["IDSIGFI"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(cid))
                    cid = item["NOMECLIENTE"]?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(cid))
                    clients.Add(cid);

                if (item["TEMDATALOG"]?.ToObject<bool>() == true)
                    datalogCount++;
            }

            double rate = totalCount > 0 ? datalogCount * 100.0 / totalCount : 0.0;

            // Atualiza cada TextBlock individualmente
            PreventivasStatsText.Text = $"{prevAbertas} abertas, {prevConcluidas} concluídas";
            CorretivasStatsText.Text = $"{corrAbertas} abertas, {corrConcluidas} concluídas";
            ServicosStatsText.Text = $"{servAbertos} abertos, {servConcluidos} concluídos";
            DatalogStatsText.Text = datalogCount.ToString();
            ClientesStatsText.Text = clients.Count.ToString();
            TotalStatsText.Text = totalCount.ToString();

            DatalogSummaryText.Text = $"{datalogCount} OS com Datalog (" + rate.ToString("0.#") + "%)";
     
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

            _statsDirty = true;
        }

        private void AnnotatePrazoInfo()
        {
            if (_manutList == null) return;

            var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

            var prevDict = new Dictionary<string, (DateTime dt, string rota)>();
            var corrDict = new Dictionary<string, int>();

            foreach (var obj in _manutList.OfType<JObject>())
            {
                string tipo = obj["TIPO"]?.ToString()?.Trim() ?? string.Empty;
                string id = obj["IDSIGFI"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(id))
                    id = obj["NOMECLIENTE"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (string.Equals(tipo, "PREVENTIVA", StringComparison.OrdinalIgnoreCase))
                {
                    var dtStr = obj["DTCONCLUSAO"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(dtStr) && DateTime.TryParse(dtStr, pt, System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        var rota = obj["ROTA"]?.ToString() ?? string.Empty;
                        if (!prevDict.ContainsKey(id) || prevDict[id].dt < dt)
                            prevDict[id] = (dt, rota);
                    }
                }
                else if (string.Equals(tipo, "CORRETIVA", StringComparison.OrdinalIgnoreCase))
                {
                    var rec = obj["DTAHORARECLAMACAO"]?.ToString();
                    var con = obj["DTCONCLUSAO"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(rec) && string.IsNullOrWhiteSpace(con) && DateTime.TryParse(rec, pt, System.Globalization.DateTimeStyles.None, out var dtRec))
                    {
                        int dias = 3 - (int)(DateTime.Today - dtRec.Date).TotalDays;
                        if (!corrDict.ContainsKey(id) || dias < corrDict[id])
                            corrDict[id] = dias;
                    }
                }
            }

            foreach (var obj in _manutList.OfType<JObject>())
            {
                string id = obj["IDSIGFI"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(id))
                    id = obj["NOMECLIENTE"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (prevDict.TryGetValue(id, out var info))
                {
                    var proxima = info.dt.AddMonths(6);
                    int dias = (int)Math.Ceiling((proxima.Date - DateTime.Today).TotalDays);

                    if (dias >= -60)
                    {
                        obj["PREV_ULTIMA"] = info.dt.ToString("yyyy-MM-dd");
                        obj["PREV_PROXIMA"] = proxima.ToString("yyyy-MM-dd");
                        obj["PREV_DIAS"] = dias;
                        obj["ROTA_LAST"] = info.rota;
                    }
                }

                if (corrDict.TryGetValue(id, out var cdias))
                {
                    obj["CORR_DIAS"] = cdias;
                }
            }
        }

        private void AnnotatePrevOpenCount()
        {
            if (_manutList == null) return;

            var countMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in _manutList.OfType<JObject>())
            {
                var tipo = obj["TIPO"]?.ToString()?.Trim() ?? string.Empty;
                if (!string.Equals(tipo, "PREVENTIVA", StringComparison.OrdinalIgnoreCase))
                    continue;

                var rec = obj["DTAHORARECLAMACAO"]?.ToString();
                var con = obj["DTCONCLUSAO"]?.ToString();
                bool isOpen = !string.IsNullOrWhiteSpace(rec) && string.IsNullOrWhiteSpace(con);
                if (!isOpen) continue;

                string id = obj["IDSIGFI"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(id))
                    id = obj["NOMECLIENTE"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (countMap.ContainsKey(id)) countMap[id]++; else countMap[id] = 1;
            }

            foreach (var obj in _manutList.OfType<JObject>())
            {
                string id = obj["IDSIGFI"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(id))
                    id = obj["NOMECLIENTE"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(id))
                    continue;

                obj["PREV_ABERTAS_CLIENTE"] = countMap.TryGetValue(id, out var c) ? c : 0;
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

            _statsDirty = true;
        }

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manutList == null) return;

            var win = new ShareDialog { Owner = this };
            if (win.ShowDialog() != true)
                return;

            var criteria = GetCurrentCriteria();
            var filtered = _filterSvc.Apply(_manutList, criteria);

            if (win.Selected == ShareDialog.ShareOption.Csv)
            {
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
                return;
            }

            if (win.Selected == ShareDialog.ShareOption.Html)
            {
                string? icon = null;
                if (criteria.MarkerStyle != "circle" && _iconUrls.TryGetValue(criteria.MarkerStyle, out var url))
                    icon = url;

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
                                                                criteria.LatLonField,
                                                                false,
                                                                icon,
                                                                criteria.UseClusters);

                var dialog = new SaveFileDialog
                {
                    Filter = "HTML Files (*.html)|*.html",
                    FileName = $"mapa_{DateTime.Now:yyyyMMddHHmmss}.html",
                    DefaultExt = "html"
                };
                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllText(dialog.FileName, html, Encoding.UTF8);
                }
            }
        }


        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SigfiFilterCombo.SelectedIndex = 0;
            TipoFilterCombo.SelectedIndex = 0;
            NumOsFilterBox.Text = string.Empty;
            IdSigfiFilterBox.Text = string.Empty;

            RegionalFilterCombo.SelectedIndex = 0;
            UpdateRotaCombo();
            RotaFilterCombo.SelectedIndex = 0;
            EmpresaFilterCombo.SelectedIndex = 0;

            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;

            ChbOpen.IsChecked = true;
            ChbClosed.IsChecked = true;
            ChbOnlyDatalog.IsChecked = false;
            ChbOnlyInst.IsChecked = false;

            ColorOpenCombo.SelectedIndex = 0;
            ColorClosedCombo.SelectedIndex = 0;

            ChbColorPrev.IsChecked = false;
            ChbColorCorr.IsChecked = false;
            ChbColorServ.IsChecked = false;

            ColorTipoPrevCombo.SelectedIndex = 0;
            ColorTipoCorrCombo.SelectedIndex = 0;
            ColorTipoServCombo.SelectedIndex = 0;

            MarkerStyleCombo.SelectedIndex = 0;
            LatLonFieldCombo.SelectedIndex = 0;

            PrazoDiasTextBox.Text = string.Empty;
            TipoPrazoCombo.SelectedIndex = 0;
            PrevCountRotaCombo.SelectedIndex = 0;
            PrevClosedCountCombo.SelectedIndex = 0;

            FuncSearchBox.Text = string.Empty;
            FuncFieldCombo.SelectedIndex = 0;

            ApplyFilters();
        }

        private void DatalogButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new DatalogWindow();
            win.Owner = this;
            win.Show();
        }

        private void SearchEmployeeButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new FuncionarioWindow();
            win.Owner = this;
            win.Show();
        }

        private void PrazoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manutList == null) return;
            var win = new PrazoWindow(_manutList);
            win.Owner = this;
            win.Show();
        }

        private void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new UpdateWindow
            {
                Owner = this
            };
            win.ShowDialog();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedIndex == 1 && _statsDirty)
            {
                _statsDirty = false;
                UpdateStatsTab(_filteredCache);
            }
        }

        private async void UpdateStatsTab(IEnumerable<JObject>? source = null)
        {
            if (source == null && _manutList == null) return;

            var all = source?.ToList() ?? _manutList!.OfType<JObject>().ToList();

            var stats = await Task.Run(() =>
            {
                var withDatalog = new List<JObject>();
                var osSem = new List<OsSimpleInfo>();
                var osAlert = new List<OsAlertInfo>();

                int prevCom = 0, prevSem = 0, corrCom = 0, corrSem = 0;
                var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

                foreach (var o in all)
                {
                    bool hasData = o["TEMDATALOG"]?.ToObject<bool>() == true;
                    string tipo = o["TIPO"]?.ToString()?.Trim() ?? string.Empty;

                    if (hasData)
                    {
                        withDatalog.Add(o);
                        if (string.Equals(tipo, "PREVENTIVA", StringComparison.OrdinalIgnoreCase)) prevCom++;
                        else if (string.Equals(tipo, "CORRETIVA", StringComparison.OrdinalIgnoreCase)) corrCom++;
                    }
                    else
                    {
                        if (string.Equals(tipo, "PREVENTIVA", StringComparison.OrdinalIgnoreCase)) prevSem++;
                        else if (string.Equals(tipo, "CORRETIVA", StringComparison.OrdinalIgnoreCase)) corrSem++;

                        osSem.Add(new OsSimpleInfo
                        {
                            NumOS = (o["NUMOS"]?.ToString() ?? string.Empty).Trim(),
                            IdSigfi = (o["IDSIGFI"]?.ToString() ?? string.Empty).Trim(),
                            Rota = (o["ROTA"]?.ToString() ?? string.Empty).Trim()
                        });

                        var dtStr = o["DTCONCLUSAO"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(dtStr) && DateTime.TryParse(dtStr, pt, System.Globalization.DateTimeStyles.None, out var dt))
                        {
                            int dias = (int)(DateTime.Today - dt.Date).TotalDays;
                            if (dias > 2 && dias <= 15)
                            {
                                var jobj = (JObject)o;
                                osAlert.Add(new OsAlertInfo
                                {
                                    NumOS = (o["NUMOS"]?.ToString() ?? string.Empty).Trim(),
                                    IdSigfi = (o["IDSIGFI"]?.ToString() ?? string.Empty).Trim(),
                                    Cliente = (o["NOMECLIENTE"]?.ToString() ?? string.Empty).Trim(),
                                    Rota = (o["ROTA"]?.ToString() ?? string.Empty).Trim(),
                                    Tipo = tipo,
                                    Conclusao = dt,
                                    DiasSemDatalog = dias,
                                    Raw = jobj
                                });
                            }
                        }
                    }
                }

                var routeStats = withDatalog
                    .GroupBy(o => (o["ROTA"]?.ToString() ?? "-").Trim())
                    .Select(g => new RouteCount(g.Key, g.Count()))
                    .OrderByDescending(g => g.Count)
                    .ToList();

                int maxRoute = routeStats.Count > 0 ? routeStats.Max(r => r.Count) : 0;

                var clientDict = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var o in all)
                {
                    var rota = (o["ROTA"]?.ToString() ?? "-").Trim();
                    var id = o["IDSIGFI"]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(id))
                        id = o["NOMECLIENTE"]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(id))
                        continue;
                    if (!clientDict.TryGetValue(rota, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        clientDict[rota] = set;
                    }
                    set.Add(id);
                }
                var clientRouteStats = clientDict.Select(kvp => new RouteCount(kvp.Key, kvp.Value.Count))
                                                .OrderByDescending(g => g.Count)
                                                .ToList();
                int maxClientRoute = clientRouteStats.Count > 0 ? clientRouteStats.Max(r => r.Count) : 0;

                var tipoData = new List<LabelValue>
                {
                    new LabelValue("Preventiva com dtlg", prevCom),
                    new LabelValue("Preventiva sem dtlg", prevSem),
                    new LabelValue("Corretiva com dtlg", corrCom),
                    new LabelValue("Corretiva sem dtlg", corrSem)
                };

                return (routeStats, maxRoute, osSem, osAlert, tipoData, clientRouteStats, maxClientRoute);
            });

            _osSemDatalog.Clear();
            _osAlertaDatalog.Clear();
            _osSemDatalog.AddRange(stats.osSem);
            _osAlertaDatalog.AddRange(stats.osAlert);

            _routeStats = stats.routeStats;
            MaxRouteCount = stats.maxRoute;

            _clientRouteStats = stats.clientRouteStats;
            MaxClientRouteCount = stats.maxClientRoute;

            if (DatalogAlertGrid != null)
            {
                DatalogAlertGrid.ItemsSource = null;
                DatalogAlertGrid.ItemsSource = _osAlertaDatalog;
            }


            try
            {
                var recent = await _datalogService.GetDatalogFoldersPeriodAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);
                var rotaMap = all
                    .Where(o => o["NUMOS"] != null && o["ROTA"] != null)
                    .ToDictionary(o => (o["NUMOS"]!.ToString() ?? string.Empty).Trim(),
                                  o => (o["ROTA"]!.ToString() ?? string.Empty).Trim(),
                                  StringComparer.OrdinalIgnoreCase);

                _recentRouteStats = recent.Keys
                    .Where(k => rotaMap.ContainsKey(k))
                    .GroupBy(k => rotaMap[k])
                    .Select(g => new RouteCount(g.Key, g.Count()))
                    .OrderByDescending(g => g.Count)
                    .ToList();
            }
            catch
            {
                _recentRouteStats = new List<RouteCount>();
            }

            MaxRecentRouteCount = _recentRouteStats.Count > 0 ? _recentRouteStats.Max(r => r.Count) : 0;



        }

        private void PrazoDiasTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void DatalogAlertGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is OsAlertInfo)
            {
                e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 235, 235));
                e.Row.Foreground = Brushes.Black;
            }
        }

        private void DatalogAlertGrid_RowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DatalogAlertGrid.SelectedItem is OsAlertInfo info && info.Raw != null)
            {
                var win = new OsDetailWindow(info.Raw)
                {
                    Owner = this
                };
                win.ShowDialog();
            }
        }

        private void AlertSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterAlertGrid();
        }

        private void AlertFieldCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterAlertGrid();
        }

        private void FilterAlertGrid()
        {
            if (_osAlertaDatalog == null || DatalogAlertGrid == null)
                return;

            var text = AlertSearchBox?.Text?.Trim() ?? string.Empty;
            int field = AlertFieldCombo?.SelectedIndex ?? 0;

            IEnumerable<OsAlertInfo> list = _osAlertaDatalog;

            if (!string.IsNullOrWhiteSpace(text))
            {
                list = field switch
                {
                    0 => list.Where(o => o.NumOS != null && o.NumOS.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0),
                    1 => list.Where(o => o.IdSigfi != null && o.IdSigfi.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0),
                    2 => list.Where(o => o.Cliente != null && o.Cliente.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0),
                    3 => list.Where(o => o.Rota != null && o.Rota.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0),
                    _ => list
                };
            }

            DatalogAlertGrid.ItemsSource = list.ToList();
        }

        private async Task AnnotateFuncionariosInfoAsync()
        {
            if (_manutList == null)
                return;

            _funcMap ??= await _spService.DownloadFuncionariosAsync();

            foreach (var obj in _manutList.OfType<JObject>())
            {
                var desc = obj["DESCADICIONALEXEC"]?.ToString() ?? string.Empty;
                var nomes = new List<string>();
                foreach (Match m in Regex.Matches(desc, "#(\\d+)"))
                {
                    var mat = m.Groups[1].Value.TrimStart('0');
                    if (_funcMap.TryGetValue(mat, out var nome) && !nomes.Contains(nome))
                        nomes.Add(nome);
                }
                obj["FUNCIONARIOS"] = nomes.Count > 0 ? string.Join(", ", nomes) : null;
            }
        }

        private void AppendInstalacaoData(JArray data)
        {
            if (data == null || _manutList == null) return;

            foreach (var obj in data.OfType<JObject>())
            {
                var id = (obj["IDSERVICOSCONJ"]?.ToString() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) continue;

                var novo = new JObject
                {
                    ["NUMOS"] = id,
                    ["IDSIGFI"] = id,
                    ["NOMECLIENTE"] = obj["NOMEDOCLIENTE"] ?? obj["NOMECLIENTE"],
                    ["LATLONCONF"] = obj["LATLONCONF"],
                    ["ROTA"] = obj["ROTA"],
                    ["CONCLUSAO"] = obj["CONCLUSAO"],
                    ["EMPRESA"] = obj["EMPRESA"],
                    ["TIPO"] = "INSTALACAO",
                    ["TIPODESIGFI"] = "INSTALACAO"
                };

                _manutList.Add(novo);
            }
        }

    }
}