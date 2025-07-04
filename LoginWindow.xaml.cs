using System;
using System.Collections.Generic;
using System.Windows;
using ManutMap.Services;

namespace ManutMap
{
    public partial class LoginWindow : Window
    {
        private readonly SharePointService _spService = new SharePointService();
        private Dictionary<string, string> _funcionarios = new();

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += LoginWindow_Loaded;
        }

        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoginButton.IsEnabled = false;
            StatusText.Text = "Carregando...";
            try
            {
                _funcionarios = await _spService.DownloadFuncionariosAsync();
                StatusText.Text = string.Empty;
            }
            catch
            {
                StatusText.Text = "Erro ao carregar lista";
            }
            LoginButton.IsEnabled = true;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var input = MatriculaBox.Text?.Trim() ?? string.Empty;
            input = input.TrimStart('0');
            if (_funcionarios.TryGetValue(input, out _))
            {
                var main = new MainWindow();
                main.Show();
                Close();
            }
            else
            {
                StatusText.Text = "Matrícula não encontrada";
            }
        }

        private void MatriculaBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
        }

    }
}
