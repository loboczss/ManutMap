using System.Globalization;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace ManutMap
{
    public partial class OsDetailWindow : Window
    {
        public OsDetailWindow(JObject data)
        {
            InitializeComponent();

            var culture = new CultureInfo("pt-BR");

            DescExecText.Text = $"DESCADICIONALEXEC: {data["DESCADICIONALEXEC"]?.ToString() ?? "-"}";

            NumOsText.Text = data["NUMOS"]?.ToString() ?? "-";
            IdSigfiText.Text = data["IDSIGFI"]?.ToString() ?? "-";
            RotaText.Text = data["ROTA"]?.ToString() ?? "-";
            TipoText.Text = data["TIPO"]?.ToString() ?? "-";
            TipoSigfiText.Text = data["TIPODESIGFI"]?.ToString() ?? "-";
            NomeText.Text = data["NOMECLIENTE"]?.ToString() ?? "-";
            ReclamanteText.Text = data["RECLAMANTE"]?.ToString() ?? "-";

            AberturaText.Text = FormatDate(data["DTAHORARECLAMACAO"]?.ToString(), culture);
            ConclusaoText.Text = FormatDate(data["DTCONCLUSAO"]?.ToString(), culture);
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
    }
}
