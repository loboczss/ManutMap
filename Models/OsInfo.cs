using System;

namespace ManutMap.Models
{
    public class OsInfo
    {
        public required string NumOS { get; init; }
        public required string IdSigfi { get; init; }
        public required string Rota { get; init; }
        public DateTime? Data { get; init; }
        public bool TemDatalog { get; set; }
        public string? FolderUrl { get; set; }
        public string Status => TemDatalog ? "Com datalog" : "Sem datalog";
    }
}
