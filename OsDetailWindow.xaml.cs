using System.Globalization;
using System.Windows;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ManutMap.Services;

namespace ManutMap
{
    public partial class OsDetailWindow : Window
    {
        private readonly SharePointService _spService = new SharePointService();
        private readonly List<string> _matriculas = new();

        public OsDetailWindow(JObject data)
        {
            InitializeComponent();

            Loaded += OsDetailWindow_Loaded;

            var culture = new CultureInfo("pt-BR");

            var descExec = data["DESCADICIONALEXEC"]?.ToString() ?? string.Empty;
            DescExecText.Text = $"DESCADICIONALEXEC: {descExec}";

            ExtractMatriculas(descExec);

            NumOsText.Text = data["NUMOS"]?.ToString() ?? "-";
            IdSigfiText.Text = data["IDSIGFI"]?.ToString() ?? "-";
            RotaText.Text = data["ROTA"]?.ToString() ?? "-";
            TipoText.Text = data["TIPO"]?.ToString() ?? "-";
            TipoSigfiText.Text = data["TIPODESIGFI"]?.ToString() ?? "-";
            NomeText.Text = data["NOMECLIENTE"]?.ToString() ?? "-";
            ReclamanteText.Text = data["RECLAMANTE"]?.ToString() ?? "-";

            AberturaText.Text = FormatDate(data["DTAHORARECLAMACAO"]?.ToString(), culture);
            ConclusaoText.Text = FormatDate(data["DTCONCLUSAO"]?.ToString(), culture);

            FuncionariosText.Text = string.Empty;
        }

        private static string FormatDate(string? value, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(value)) return "-";
            if (DateTime.TryParse(value, culture, DateTimeStyles.None, out var dt) ||
                DateTime.TryParse(value, out dt))
            {
                return dt.ToString("dd/MM/yyyy", culture);
            }
            return value;
        }

        private void ExtractMatriculas(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (Match m in Regex.Matches(text, "#(\\d+)"))
            {
                var val = m.Groups[1].Value.TrimStart('0');
                if (!string.IsNullOrWhiteSpace(val) && !_matriculas.Contains(val))
                    _matriculas.Add(val);
            }
        }

        private async void OsDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FuncionariosText.Text = "Carregando...";
            try
            {
                var mapa = await _spService.DownloadFuncionariosAsync();
                var nomes = new List<string>();
                foreach (var mat in _matriculas)
                {
                    if (mapa.TryGetValue(mat, out var nome))
                        nomes.Add(nome);
                }
                FuncionariosText.Text = nomes.Count > 0 ? string.Join(", ", nomes) : "-";
            }
            catch
            {
                FuncionariosText.Text = "Erro ao carregar";
            }
        }
    }
}
