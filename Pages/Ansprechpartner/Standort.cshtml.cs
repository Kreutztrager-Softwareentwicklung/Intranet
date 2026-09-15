using Intranet2.Services.ActiveDirectory;
using Intranet2.Services.Fotos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Intranet2.Pages.Ansprechpartner
{
    public class StandortModel : PageModel
    {
        private readonly MitarbeiterService _mitarbeiterService;
        private readonly MitarbeiterFotoService _fotoService;

        // Ab dieser Anzahl wird paginiert
        private const int SeitenGroesse = 30;

        public StandortModel(
            MitarbeiterService mitarbeiterService,
            MitarbeiterFotoService fotoService)
        {
            _mitarbeiterService = mitarbeiterService;
            _fotoService = fotoService;
        }

        public string Niederlassung { get; set; } = string.Empty;
        public string HervorgehobenerBenutzername { get; set; } = string.Empty;
        public List<Mitarbeiter> Mitarbeiter { get; set; } = new();
        public Dictionary<string, string?> Fotos { get; set; } = new();
        public int GesamtAnzahl { get; set; }
        public int AktuelleSeite { get; set; } = 1;
        public bool HatMehrSeiten { get; set; }

        public IActionResult OnGet(string niederlassung, string? person = null, int seite = 1)
        {
            if (string.IsNullOrWhiteSpace(niederlassung))
                return RedirectToPage("/Ansprechpartner/Ansprechpartner");

            Niederlassung = niederlassung;
            AktuelleSeite = seite < 1 ? 1 : seite;

            var alle = _mitarbeiterService.GetMitarbeiterFuerNiederlassung(niederlassung);
            GesamtAnzahl = alle.Count;

            // Gesuchte Person immer auf Seite 1 ganz oben
            if (!string.IsNullOrWhiteSpace(person))
            {
                HervorgehobenerBenutzername = person.Trim();
                alle = alle
                    .OrderByDescending(m => string.Equals(
                        m.SamAccountName,
                        HervorgehobenerBenutzername,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                AktuelleSeite = 1;
            }

            // Paginierung – nur wenn mehr als SeitenGroesse Mitarbeiter
            if (GesamtAnzahl > SeitenGroesse)
            {
                Mitarbeiter = alle
                    .Skip((AktuelleSeite - 1) * SeitenGroesse)
                    .Take(SeitenGroesse)
                    .ToList();
                HatMehrSeiten = AktuelleSeite * SeitenGroesse < GesamtAnzahl;
            }
            else
            {
                // Kleine Niederlassungen: alle auf einmal
                Mitarbeiter = alle;
                HatMehrSeiten = false;
            }

            Fotos = Mitarbeiter.ToDictionary(
                m => m.SamAccountName,
                m => _fotoService.GetFotoUrl(m.BereinigterNachname, m.BereinigterVorname));

            return Page();
        }
    }
}
