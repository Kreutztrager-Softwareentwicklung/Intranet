
using System.ComponentModel.DataAnnotations;

namespace Intranet2.Datenbank.Models
{
    public class Umfrage
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(300)]
        public string Frage { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Beschreibung { get; set; }

        public DateTime StartetAm { get; set; } = DateTime.UtcNow;

        public DateTime? EndetAm { get; set; }

        public bool IstAktiv { get; set; } = true;

        // Nur bei aktivierter Einstellung dürfen Ersteller
        // und Administratoren die einzelnen Stimmen sehen.
        public bool NamentlicheAuswertung { get; set; } = false;

        public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;

        [MaxLength(256)]
        public string? ErstelltVon { get; set; }

        public ICollection<UmfrageOption> Optionen { get; set; } = new List<UmfrageOption>();

        public ICollection<UmfrageStimme> Stimmen { get; set; } = new List<UmfrageStimme>();
    }
}