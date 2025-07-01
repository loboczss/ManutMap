using System;

namespace ManutMap.Models
{
    public class FilterCriteria
    {
        public string Sigfi { get; set; } = "Todos";
        public string NumOs { get; set; }
        public string IdSigfi { get; set; }
        public string Rota { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public bool ShowOpen { get; set; } = true;
        public bool ShowClosed { get; set; } = true;

        public string ColorOpen { get; set; } = "#FF0000";
        public string ColorClosed { get; set; } = "#008000";

        // NOVO: qual campo de coordenada usar
        // "LATLON" ou "LATLONCON"
        public string LatLonField { get; set; } = "LATLON";
    }
}

