
using Intranet2.Services.ActiveDirectory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Intranet2.Pages.Ansprechpartner
{
    public class MitarbeiterSucheApiModel : PageModel
    {
        private readonly MitarbeiterService _mitarbeiterService;

        public MitarbeiterSucheApiModel(MitarbeiterService mitarbeiterService)
        {
            _mitarbeiterService = mitarbeiterService;
        }

        // GET /Ansprechpartner/MitarbeiterSucheApi?q=jan&modus=standort
        public IActionResult OnGet(string q, string modus = "standort")
        {
            // =====================================================
            // SUCHBEGRIFF PRÜFEN
            // =====================================================

            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return new JsonResult(new List<object>());
            }

            string suchbegriff = q.Trim().ToLowerInvariant();

            // =====================================================
            // MITARBEITER SUCHEN
            // =====================================================

            var treffer = _mitarbeiterService
                .GetSuchbareMitarbeiter()

                .Where(m =>

                    // Vor- und Nachname
                    (m.Anzeigename?.ToLowerInvariant().Contains(suchbegriff) ?? false) ||

                    (m.FirstName?.ToLowerInvariant().Contains(suchbegriff) ?? false) ||

                    (m.LastName?.ToLowerInvariant().Contains(suchbegriff) ?? false) ||

                    // Position
                    (m.Title?.ToLowerInvariant().Contains(suchbegriff) ?? false) ||

                    // Hauptabteilung
                    (m.Department?.ToLowerInvariant().Contains(suchbegriff) ?? false) ||

                    // Alle Unterabteilungen durchsuchen
                    m.Unterabteilungen.Any(z =>
                        z.Name.Contains(
                            suchbegriff,
                            StringComparison.OrdinalIgnoreCase)) ||

                    // Niederlassung
                    (m.Niederlassung?.ToLowerInvariant().Contains(suchbegriff) ?? false)
                )

                // Maximal 20 Suchergebnisse
                .Take(20)

                // =================================================
                // DATEN FÜR DIE JAVASCRIPT-SUCHE BEREITSTELLEN
                // =================================================

                .Select(m => new
                {
                    // Name und Position
                    anzeigename = m.Anzeigename,
                    title = m.Title,

                    // Hauptabteilung
                    department = m.Department,

                    // Bisherige Eigenschaft beibehalten
                    unterabteilung = m.Unterabteilung,

                    // =================================================
                    // NEU: ALLE UNTERABTEILUNGEN MIT LEITUNGSFUNKTION
                    // =================================================

                    unterabteilungen = m.Unterabteilungen
                        .Select(z => new
                        {
                            name = z.Name,
                            istLeitung = z.IstLeitung
                        })
                        .ToList(),

                    // Niederlassung
                    niederlassung = m.Niederlassung,

                    // Kontaktdaten
                    telefon = m.TelephoneNumber,
                    mobil = m.Mobile,
                    email = m.Email,

                    // Windows-Benutzername
                    samAccountName = m.SamAccountName,

                    // Bisherige allgemeine Leitungskennung beibehalten
                    istLeitung = m.IstLeitung,

                    // Initial für den Avatar
                    initial = m.Anzeigename?.Length > 0
                        ? m.Anzeigename[0].ToString().ToUpperInvariant()
                        : "?"
                })

                .ToList();

            // =====================================================
            // SUCHERGEBNIS ALS JSON ZURÜCKGEBEN
            // =====================================================

            return new JsonResult(treffer);
        }
    }
}