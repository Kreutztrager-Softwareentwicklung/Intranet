using Intranet2.Datenbank.Data;
using Intranet2.Datenbank.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Intranet2.Services.ActiveDirectory;

namespace Intranet2.Pages.Umfragen
{
    public class IndexModel : PageModel
    {
        private readonly DataContext _context;
        private readonly MitarbeiterService _mitarbeiterService;

        public IndexModel(DataContext context, MitarbeiterService mitarbeiterService)
        {
            _context = context;
            _mitarbeiterService = mitarbeiterService;
        }

        // UMFRAGEN
        public List<Umfrage> AktiveUmfragen { get; set; } = new();

        public List<Umfrage> VergangeneUmfragen { get; set; } = new();

        public Dictionary<string, string> ErstellerNamen { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string GetErstellerName(string? windowsBenutzername)
        {
            if (string.IsNullOrWhiteSpace(windowsBenutzername))
            {
                return "Nicht hinterlegt";
            }

            if (ErstellerNamen.TryGetValue(windowsBenutzername, out string? name))
            {
                return name;
            }

            return windowsBenutzername;
        }

        // SEITE LADEN
        public async Task OnGetAsync()
        {
            DateTime jetzt = DateTime.Now;

            List<Umfrage> umfragen = await _context.Umfragen.AsNoTracking().Include(u => u.Optionen).ThenInclude(o => o.Stimmen).Where(u => u.IstAktiv)

                    // Noch nicht gestartete Umfragen
                    // werden Mitarbeitern nicht angezeigt.
                    .Where(u => u.StartetAm <= jetzt).OrderByDescending(u => u.StartetAm).ToListAsync();

            // LAUFENDE UMFRAGEN
            AktiveUmfragen = umfragen.Where(u => !u.EndetAm.HasValue || u.EndetAm.Value >= jetzt).ToList();

            // VERGANGENE UMFRAGEN
            VergangeneUmfragen = umfragen.Where(u => u.EndetAm.HasValue && u.EndetAm.Value < jetzt).ToList();

            // Windows-Benutzernamen der Ersteller sammeln
            var benutzernamen = umfragen
                .Select(u => u.ErstelltVon)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Anzeigenamen aus der Benutzertabelle laden
            var benutzer = await _context.Benutzer.AsNoTracking().Where(b => benutzernamen.Contains(b.WindowsBenutzername))
                .Select(b => new
                {
                    b.WindowsBenutzername,
                    b.Name
                })
                .ToListAsync();

            // Zuordnung für die Anzeige
            ErstellerNamen = benutzer.ToDictionary(
                b => b.WindowsBenutzername,
                b => b.Name,
                StringComparer.OrdinalIgnoreCase);

            // VOLLSTÄNDIGE ERSTELLERNAMEN AUS DEM AD ÜBERNEHMEN

            // Mitarbeiter einmal aus dem AD-Cache laden
            var adMitarbeiter = _mitarbeiterService.GetMitarbeiter();

            // Nach Windows-Benutzernamen durchsuchbares Dictionary
            var adNamen = adMitarbeiter
                .Where(m =>
                    !string.IsNullOrWhiteSpace(m.SamAccountName) &&
                    !string.IsNullOrWhiteSpace(m.Anzeigename))
                .GroupBy(
                    m => m.SamAccountName,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    gruppe => gruppe.Key,
                    gruppe => gruppe.First().Anzeigename,
                    StringComparer.OrdinalIgnoreCase);


            // Ersteller sämtlicher Umfragen prüfen
            foreach (var umfrage in umfragen)
            {
                if (string.IsNullOrWhiteSpace(umfrage.ErstelltVon))
                {
                    continue;
                }

                string windowsBenutzername = umfrage.ErstelltVon.Trim();

                // KREUZTRAEGER\mustermann -> mustermann
                string samAccountName = windowsBenutzername.Split('\\').Last();

                // Falls ein vollständiger AD-Name vorhanden ist:
                // Datenbank-Anzeigenamen überschreiben
                if (adNamen.TryGetValue(samAccountName, out string? vollstaendigerName))
                {
                    ErstellerNamen[windowsBenutzername] = vollstaendigerName;
                }
            }
        }
    }
}