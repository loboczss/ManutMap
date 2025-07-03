using System;
using System.Linq;
using System.Windows;
using ManutMap.Models;
using ManutMap.Services;
using ManutMap.Helpers;

namespace ManutMap
{
    public partial class DatalogWindow : Window
    {
        private readonly DatalogService _service = new();
        public RelayCommand<string> CmdAbrir { get; }

        public DatalogWindow()
        {
            InitializeComponent();
            CmdAbrir = new RelayCommand<string>(u =>
            {
                if (!string.IsNullOrWhiteSpace(u))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u)
                    {
                        UseShellExecute = true
                    });
            });
            DataContext = this;
            CmbFiltro.SelectedIndex = 0;
            CmbRegional.SelectedIndex = 0;
        }

        private async void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            BtnBuscar.IsEnabled = false;
            GridOs.ItemsSource = null;
            TxtResumo.Text = "Consultando…";

            DateTime ini = DpInicio.SelectedDate ?? DateTime.Today;
            DateTime fim = (DpFim.SelectedDate ?? DateTime.Today).AddDays(1).AddTicks(-1);
            string? termo = string.IsNullOrWhiteSpace(TxtBusca.Text) ? null : TxtBusca.Text.Trim();
            int tipoFiltro = CmbFiltro.SelectedIndex;
            string? regionalSel = CmbRegional.SelectedIndex switch
            {
                1 => "AC",
                2 => "MT",
                _ => null
            };

            try
            {
                var lista = await _service.BuscarAsync(ini, fim, termo, tipoFiltro, regionalSel);
                if (Owner is MainWindow mainWin)
                    mainWin.UpdateDatalogMap(lista);
                TxtResumo.Text =
                    $"Total: {lista.Count} | Com datalog: {lista.Count(i => i.TemDatalog)} | Sem datalog: {lista.Count(i => !i.TemDatalog)}";
                GridOs.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnBuscar.IsEnabled = true; }
        }
    }
}
