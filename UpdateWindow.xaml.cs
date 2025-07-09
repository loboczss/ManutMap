using System;
using System.Diagnostics;
using System.Windows;
using ManutMap.Services;

namespace ManutMap
{
    public partial class UpdateWindow : Window
    {
        private readonly AtualizadorService _service = new();

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
            Close();
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/loboczss/ManutMap/releases/latest")
            {
                UseShellExecute = true
            });
            Close();
        }
    }
}
