using System;

namespace ManutMap.Models
{
    public class FilterCriteria
    {
        public string Sigfi { get; set; } = "Todos";
        public string NumOs { get; set; }
        public string IdSigfi { get; set; }
        public string Regional { get; set; } = "Todos";
        public string Rota { get; set; } = "Todos";
        public string TipoServico { get; set; } = "Todos";

        // Quantidade de preventivas por rota (0 = todas)
        public int PreventivasPorRota { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public bool ShowOpen { get; set; } = true;
        public bool ShowClosed { get; set; } = true;

        public string ColorOpen { get; set; } = "#FF0000";
        public string ColorClosed { get; set; } = "#008000";

        // Configurações de cor por tipo de serviço
        public bool ColorPrevOn { get; set; }
        public bool ColorCorrOn { get; set; }
        public bool ColorServOn { get; set; }
        public string ColorServicoPreventiva { get; set; } = "#0000FF";
        public string ColorServicoCorretiva { get; set; } = "#FFA500";
        public string ColorServicoOutros { get; set; } = "#008080";

        // NOVO: qual campo de coordenada usar
        // "LATLON" ou "LATLONCON"
        public string LatLonField { get; set; } = "LATLON";

        // Novo: tipo de marcador (circle, blue, red ...)
        public string MarkerStyle { get; set; } = "circle";

        // Exibir apenas itens com datalog disponível
        public bool OnlyDatalog { get; set; }

        // Novo: habilitar agrupamento de marcadores
        public bool UseClusters { get; set; } = true;

        // Filtro de prazos
        public int PrazoDias { get; set; }
        public string TipoPrazo { get; set; } = "Todos";
    }
}

