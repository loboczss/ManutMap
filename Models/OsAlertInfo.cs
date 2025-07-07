using System;

namespace ManutMap.Models
{
    public class OsAlertInfo
    {
        public string NumOS { get; set; } = string.Empty;
        public string IdSigfi { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string Rota { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public DateTime Conclusao { get; set; }
        public int DiasSemDatalog { get; set; }
    }
}
