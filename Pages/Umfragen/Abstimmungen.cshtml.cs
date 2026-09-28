
using Intranet2.Datenbank.Data;
using Intranet2.Services.ActiveDirectory;
using Intranet2.Sicherheit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Intranet2.Pages.Umfragen
{
    [Authorize]
    public class AbstimmungenModel : PageModel
    {
        private readonly DataContext _context;

        private readonly MitarbeiterService _mitarbeiterService;

        public AbstimmungenModel(
            DataContext context,
            MitarbeiterService mitarbeiterService)
        {
            _context = context;

            _mitarbeiterService = mitarbeiterService;
        }

        public int UmfrageId { get; private set; }

        public string Frage { get; private set; } = string.Empty;

        public List<Einzelstimme> Stimmen { get; private set; } = new();


        // =====================================================
        // MODELL FÜR EINE EINZELNE STIMME
        // =====================================================

        public class Einzelstimme
        {
            public string Name { get; set; } = string.Empty;

            public string WindowsBenutzername { get; set; } = string.Empty;

            public string Antwort { get; set; } = string.Empty;

            public DateTime AbgestimmtAmUtc { get; set; }
        }


        // =====================================================
        // SEITE LADEN
        // =====================================================

        public async Task<IActionResult> OnGetAsync(int id)
        {
            // -------------------------------------------------
            // 1. Umfrage laden
            // -------------------------------------------------

            var umfrage = await _context.Umfragen
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,

                    u.Frage,

                    u.ErstelltVon,

                    u.NamentlicheAuswertung
                })
                .FirstOrDefaultAsync();


            // Keine Einzelstimmen bei Umfragen ohne Freigabe.

            if (umfrage == null || !umfrage.NamentlicheAuswertung)
            {
                return NotFound();
            }


            // -------------------------------------------------
            // 2. Angemeldeten Benutzer ermitteln
            // -------------------------------------------------

            string? aktuellerBenutzername = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(aktuellerBenutzername))
            {
                return Forbid();
            }


            // -------------------------------------------------
            // 3. Benutzerstatus und Rolle direkt aus DB prüfen
            // -------------------------------------------------
            //
            // Wichtig:
            // Nicht ausschließlich auf zwischengespeicherte
            // Rollen-Claims verlassen.
            // -------------------------------------------------

            var aktuellerBenutzer = await _context.Benutzer
                .AsNoTracking()
                .Where(b =>
                    b.WindowsBenutzername == aktuellerBenutzername &&
                    b.IstAktiv)
                .Select(b => new
                {
                    b.Rolle
                })
                .FirstOrDefaultAsync();


            if (aktuellerBenutzer == null)
            {
                return Forbid();
            }


            // -------------------------------------------------
            // 4. Berechtigung prüfen
            // -------------------------------------------------

            bool istErsteller =
                !string.IsNullOrWhiteSpace(umfrage.ErstelltVon) &&
                string.Equals(
                    aktuellerBenutzername,
                    umfrage.ErstelltVon,
                    StringComparison.OrdinalIgnoreCase);


            bool istAdmin =
                aktuellerBenutzer.Rolle == Rollen.Admin;


            // Nur Ersteller und Administratoren.

            if (!istAdmin && !istErsteller)
            {
                return Forbid();
            }


            // -------------------------------------------------
            // 5. Browser-Cache für sensible Daten deaktivieren
            // -------------------------------------------------

            Response.Headers["Cache-Control"] = "no-store";

            Response.Headers["Pragma"] = "no-cache";


            UmfrageId = umfrage.Id;

            Frage = umfrage.Frage;


            // -------------------------------------------------
            // 6. Einzelne Abstimmungen laden
            // -------------------------------------------------
            //
            // Erst nach erfolgreicher Berechtigungsprüfung.
            // -------------------------------------------------

            var stimmen = await _context.UmfrageStimmen
                .AsNoTracking()
                .Where(s => s.UmfrageId == id)
                .OrderBy(s => s.AbgestimmtAm)
                .Select(s => new
                {
                    s.WindowsBenutzername,

                    Antwort = s.UmfrageOption.Text,

                    s.AbgestimmtAm
                })
                .ToListAsync();


            if (stimmen.Count == 0)
            {
                return Page();
            }


            // -------------------------------------------------
            // 7. Windows-Benutzernamen sammeln
            // -------------------------------------------------

            var namenDerStimmenden = stimmen
                .Select(s => s.WindowsBenutzername)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();


            // -------------------------------------------------
            // 8. Anzeigenamen aus der Benutzertabelle laden
            // -------------------------------------------------

            var benutzer = await _context.Benutzer
                .AsNoTracking()
                .Where(b =>
                    namenDerStimmenden.Contains(b.WindowsBenutzername))
                .Select(b => new
                {
                    b.WindowsBenutzername,

                    b.Name
                })
                .ToListAsync();


            var datenbankNamen = benutzer
                .GroupBy(
                    b => b.WindowsBenutzername,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Name,
                    StringComparer.OrdinalIgnoreCase);


            // -------------------------------------------------
            // 9. Namen aus Active Directory laden
            // -------------------------------------------------
            //
            // AD nur einmal abfragen, nicht für jede Stimme.
            // -------------------------------------------------

            var adNamen = _mitarbeiterService
                .GetMitarbeiter()

                .Where(m =>
                    !string.IsNullOrWhiteSpace(m.SamAccountName) &&
                    !string.IsNullOrWhiteSpace(m.Anzeigename))

                .GroupBy(
                    m => m.SamAccountName,
                    StringComparer.OrdinalIgnoreCase)

                .ToDictionary(
                    g => g.Key,
                    g => g.First().Anzeigename,
                    StringComparer.OrdinalIgnoreCase);


            // -------------------------------------------------
            // 10. Abstimmungen zusammenführen
            // -------------------------------------------------

            foreach (var stimme in stimmen)
            {
                string konto = stimme.WindowsBenutzername;

                // DOMÄNE\benutzer -> benutzer
                string samAccountName = konto.Split('\\').Last();

                string name = konto;

                // Namen aus der Datenbank verwenden.
                if (datenbankNamen.TryGetValue(konto, out string? datenbankName) && !string.IsNullOrWhiteSpace(datenbankName))
                {
                    name = datenbankName;
                }


                // Vollständigen AD-Namen bevorzugen.

                if (adNamen.TryGetValue(samAccountName, out string? adName) && !string.IsNullOrWhiteSpace(adName))
                {
                    name = adName;
                }


                Stimmen.Add(new Einzelstimme
                {
                    Name = name,

                    WindowsBenutzername = konto,

                    Antwort = stimme.Antwort,

                    AbgestimmtAmUtc = stimme.AbgestimmtAm
                });
            }


            return Page();
        }
    }
}