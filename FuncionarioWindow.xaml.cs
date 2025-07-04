using System;
using System.Collections.Generic;
using System.Windows;
using ManutMap.Services;

namespace ManutMap
{
    public partial class FuncionarioWindow : Window
    {
        private readonly SharePointService _spService = new SharePointService();
        private Dictionary<string, string> _funcionarios = new();

        public FuncionarioWindow()
        {
            InitializeComponent();
            Loaded += FuncionarioWindow_Loaded;
        }

        private async void FuncionarioWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SearchButton.IsEnabled = false;
            ResultText.Text = "Carregando...";
            try
            {
                _funcionarios = await _spService.DownloadFuncionariosAsync();
                ResultText.Text = string.Empty;
            }
            catch
            {
                ResultText.Text = "Erro ao carregar lista";
            }
            SearchButton.IsEnabled = true;
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var input = MatriculaBox.Text?.Trim() ?? string.Empty;
            input = input.TrimStart('0');
            if (_funcionarios.TryGetValue(input, out var nome))
            {
                ResultText.Text = nome;
            }
            else
            {
                ResultText.Text = "Matrícula não encontrada";
            }
        }
    }
}
