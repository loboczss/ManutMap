using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using ManutMap.Services;
using ManutMap.Models;

namespace ManutMap
{
    public partial class FuncionarioWindow : Window
    {
        private readonly SharePointService _spService = new SharePointService();
        private Dictionary<string, FuncionarioInfo> _funcionarios = new();

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
                _funcionarios = await _spService.DownloadFuncionariosInfoAsync();
                ResultText.Text = string.Empty;
            }
            catch
            {
                ResultText.Text = "Erro ao carregar lista";
            }
            SearchButton.IsEnabled = true;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var input = SearchBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                ResultText.Text = string.Empty;
                return;
            }

            SearchButton.IsEnabled = false;
            ResultText.Text = "Buscando...";

            var info = await Task.Run(() =>
            {
                FuncionarioInfo? local = null;
                switch (FieldCombo.SelectedIndex)
                {
                    case 0: // Matricula
                        var key = input.TrimStart('0');
                        _funcionarios.TryGetValue(key, out local);
                        break;
                    case 1: // Nome
                        local = _funcionarios.Values.FirstOrDefault(f =>
                            f.Nome.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
                        break;
                    case 2: // Funcao
                        local = _funcionarios.Values.FirstOrDefault(f =>
                            f.Funcao.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
                        break;
                    case 3: // Escala
                        local = _funcionarios.Values.FirstOrDefault(f =>
                            f.Escala.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
                        break;
                    case 4: // Departamento
                        local = _funcionarios.Values.FirstOrDefault(f =>
                            f.Departamento.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
                        break;
                    case 5: // Cidade
                        local = _funcionarios.Values.FirstOrDefault(f =>
                            f.Cidade.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
                        break;
                    case 6: // Contratacao
                        local = _funcionarios.Values.FirstOrDefault(f =>
                            f.Contratacao.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
                        break;
                }

                return local;
            });

            if (info != null)
            {
                ResultText.Text = $"Matrícula: {info.Matricula}\n" +
                                 $"Nome: {info.Nome}\n" +
                                 $"Função: {info.Funcao}\n" +
                                 $"Escala: {info.Escala}\n" +
                                 $"Departamento: {info.Departamento}\n" +
                                 $"Cidade: {info.Cidade}\n" +
                                 $"Contratação: {info.Contratacao}";
            }
            else
            {
                ResultText.Text = "Funcionário não encontrado";
            }

            SearchButton.IsEnabled = true;
        }

        private async void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (FieldCombo.SelectedIndex != 1)
            {
                SuggestionsList.Visibility = Visibility.Collapsed;
                return;
            }

            var text = SearchBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                SuggestionsList.Visibility = Visibility.Collapsed;
                return;
            }

            var suggestions = await Task.Run(() => _funcionarios.Values
                .Where(f => f.Nome.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Nome)
                .Distinct()
                .Take(5)
                .ToList());

            SuggestionsList.ItemsSource = suggestions;
            SuggestionsList.Visibility = suggestions.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SuggestionsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SuggestionsList.SelectedItem is string nome)
            {
                SearchBox.Text = nome;
                SuggestionsList.Visibility = Visibility.Collapsed;
                SearchButton_Click(sender, e);
            }
        }
    }
}
