using Intranet2.Datenbank.Data;
using Intranet2.Datenbank.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Intranet2.Services.ActiveDirectory;

namespace Intranet2.Pages.Umfragen
{
    public class DetailsModel : PageModel
    {
        private readonly DataContext _context;
        private readonly MitarbeiterService _mitarbeiterService;

        public DetailsModel(DataContext context, MitarbeiterService mitarbeiterService)
        {
            _context = context;
            _mitarbeiterService = mitarbeiterService;
        }

        public Umfrage Umfrage { get; set; } = null!;

        public string UmfrageErstellerName { get; set; } = "Nicht hinterlegt";

        public string? UmfrageErstellerTeamsLink { get; set; }

        public bool IstBeendet { get; set; }

        public int? EigeneOptionId { get; set; }

        public int GesamtStimmen { get; set; }

        [TempData]
        public string? Meldung { get; set; }

        // SEITE LADEN
        public async Task<IActionResult> OnGetAsync(int id)
        {
            DateTime jetzt = DateTime.Now;

            Umfrage? umfrage = await _context.Umfragen.AsNoTracking().Include(u => u.Optionen).ThenInclude(o => o.Stimmen).FirstOrDefaultAsync(u => u.Id == id && u.IstAktiv && u.StartetAm <= jetzt);

            if (umfrage == null)
            {
                return NotFound();
            }

            Umfrage = umfrage;

            // ERSTELLER DER UMFRAGE ERMITTELN
            if (!string.IsNullOrWhiteSpace(umfrage.ErstelltVon))
            {
                string erstellerWindowsBenutzername = umfrage.ErstelltVon;

                // Ersteller aus der Benutzertabelle laden
                var ersteller = await _context.Benutzer
                    .AsNoTracking()
                    .Where(b => b.WindowsBenutzername == erstellerWindowsBenutzername)
                    .Select(b => new
                    {
                        b.Name,
                        b.Email
                    })
                    .FirstOrDefaultAsync();

                // Mitarbeiter im Active Directory suchen
                Mitarbeiter? mitarbeiter = _mitarbeiterService.GetMitarbeiterFuerBenutzername(erstellerWindowsBenutzername);

                // Vollständigen Vor- und Nachnamen verwenden
                UmfrageErstellerName = !string.IsNullOrWhiteSpace(mitarbeiter?.Anzeigename) ? mitarbeiter.Anzeigename
                    : !string.IsNullOrWhiteSpace(ersteller?.Name) ? ersteller.Name : erstellerWindowsBenutzername;

                // E-Mail bevorzugt aus dem Active Directory laden
                string? email = !string.IsNullOrWhiteSpace(mitarbeiter?.Email) ? mitarbeiter.Email : ersteller?.Email;

                // Teams-Link erstellen
                if (!string.IsNullOrWhiteSpace(email))
                {
                    UmfrageErstellerTeamsLink = "https://teams.microsoft.com/l/chat/0/0?users=" + Uri.EscapeDataString(email.Trim());
                }
            }

            IstBeendet = umfrage.EndetAm.HasValue && umfrage.EndetAm.Value < jetzt;

            GesamtStimmen = umfrage.Optionen.Sum( o => o.Stimmen.Count);

            string? windowsBenutzername = User.Identity?.Name;

            if (!string.IsNullOrWhiteSpace(windowsBenutzername))
            {
                UmfrageStimme? eigeneStimme = umfrage.Optionen.SelectMany(o => o.Stimmen).FirstOrDefault(s => string.Equals( s.WindowsBenutzername, windowsBenutzername, StringComparison.OrdinalIgnoreCase));

                EigeneOptionId = eigeneStimme?.UmfrageOptionId;
            }

            return Page();
        }

        // ABSTIMMEN
        public async Task<IActionResult> OnPostAbstimmenAsync(int umfrageId, int optionId)
        {
            string? windowsBenutzername = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(windowsBenutzername))
            {
                return Forbid();
            }

            DateTime jetzt = DateTime.Now;

            Umfrage? umfrage = await _context.Umfragen.AsNoTracking().FirstOrDefaultAsync(u => u.Id == umfrageId && u.IstAktiv && u.StartetAm <= jetzt && (!u.EndetAm.HasValue || u.EndetAm.Value >= jetzt));

            if (umfrage == null)
            {
                Meldung = "Diese Umfrage ist bereits beendet.";

                return RedirectToPage(new { id = umfrageId });
            }

            bool optionGueltig = await _context.UmfrageOptionen.AnyAsync(o => o.Id == optionId && o.UmfrageId == umfrageId);

            if (!optionGueltig)
            {
                return BadRequest();
            }

            bool bereitsAbgestimmt = await _context.UmfrageStimmen.AnyAsync(s => s.UmfrageId == umfrageId && s.WindowsBenutzername == windowsBenutzername);

            if (bereitsAbgestimmt)
            {
                Meldung = "Du hast bei dieser Umfrage bereits abgestimmt.";

                return RedirectToPage(new { id = umfrageId });
            }

            UmfrageStimme stimme = new UmfrageStimme
                {
                    UmfrageId = umfrageId,

                    UmfrageOptionId = optionId,

                    WindowsBenutzername = windowsBenutzername,

                    AbgestimmtAm = DateTime.UtcNow
                };

            _context.UmfrageStimmen.Add(stimme);

            try
            {
                await _context.SaveChangesAsync();

                Meldung = "Deine Stimme wurde gespeichert.";
            }
            catch (DbUpdateException)
            {
                Meldung = "Du hast bei dieser Umfrage bereits abgestimmt.";
            }

            return RedirectToPage(new { id = umfrageId });
        }
    }
}