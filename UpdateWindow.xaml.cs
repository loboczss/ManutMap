using System;
using System.Diagnostics;
using System.Windows;
using ManutMap.Services;

namespace ManutMap
{
    public partial class UpdateWindow : Window
    {
        private readonly AtualizadorService _service = new();
        public bool UpdateInitiated { get; private set; }

        public UpdateWindow()
        {
            InitializeComponent();
            Loaded += UpdateWindow_Loaded;
        }

        private async void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Verificando atualiza\u00e7\u00f5es...";
            try
            {
                var (localVer, remoteVer) = await _service.GetVersionsAsync();
                VersionText.Text = $"Local: {localVer}  |  Remota: {remoteVer}";
                if (remoteVer > localVer)
                {
                    StatusText.Text = "Nova vers\u00e3o dispon\u00edvel.";
                    UpdateButton.Visibility = Visibility.Visible;
                }
                else
                {
                    StatusText.Text = "Voc\u00ea j\u00e1 est\u00e1 na vers\u00e3o mais recente.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erro ao verificar: {ex.Message}";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateInitiated = false;
            Close();
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = false;
            StatusText.Text = "Baixando atualização...";
            try
            {
                var file = await _service.DownloadLatestReleaseAsync();
                if (file != null)
                {
                    StatusText.Text = "Executando instalador...";
                    Process.Start(new ProcessStartInfo(file)
                    {
                        UseShellExecute = true
                    });
                    UpdateInitiated = true;
                    Close();
                }
                else
                {
                    StatusText.Text = "Arquivo de atualização não encontrado.";
                    UpdateButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Falha ao baixar atualização.";
                UpdateButton.IsEnabled = true;
            }
        }
    }
}
