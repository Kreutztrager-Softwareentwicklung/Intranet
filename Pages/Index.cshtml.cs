using Intranet2.Datenbank.Data;
using Intranet2.Datenbank.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Intranet2.Pages
{
    public class IndexModel : PageModel
    {
        private readonly DataContext _context;

        public IndexModel(DataContext context) { _context = context; }

        // GROSSE NEWS
        public List<NewsBeitrag> NewsItems { get; set; } = new();

        // KURZMELDUNGEN
        public List<NewsBeitrag> Kurzmeldungen { get; set; } = new();

        // UMFRAGE
        public Umfrage? AktiveUmfrage { get; set; }

        public string UmfrageErstellerName { get; set; } = "Nicht hinterlegt";

        public int? EigeneOptionId { get; set; }

        public int GesamtStimmen { get; set; }

        [TempData]
        public string? UmfrageMeldung { get; set; }

        // SEITE LADEN
        public async Task OnGetAsync()
        {
            DateTime jetzt = DateTime.UtcNow;

            // GROSSE NEWS
            NewsItems = await _context.NewsBeitraege.AsNoTracking().Where(n => n.IstVeroeffentlicht).Where(n => n.VeroeffentlichtAm <= jetzt).Where(n => !n.IstKurzmeldung)
                .OrderByDescending(n => n.VeroeffentlichtAm).Take(3).ToListAsync();

            // KURZMELDUNGEN
            Kurzmeldungen = await _context.NewsBeitraege.AsNoTracking().Where(n => n.IstVeroeffentlicht).Where(n => n.VeroeffentlichtAm <= jetzt)
                .Where(n => n.IstKurzmeldung).OrderByDescending(n => n.VeroeffentlichtAm).Take(5).ToListAsync();

            // AKTIVE UMFRAGE
            AktiveUmfrage = await _context.Umfragen.AsNoTracking().Include(u => u.Optionen).ThenInclude(o => o.Stimmen).Where(u => u.IstAktiv)
                .Where(u => u.StartetAm <= jetzt).Where(u => !u.EndetAm.HasValue || u.EndetAm.Value >= jetzt).OrderByDescending(u => u.StartetAm).FirstOrDefaultAsync();

            if (AktiveUmfrage == null)
            {
                return;
            }

            // ERSTELLER DER AKTIVEN UMFRAGE ERMITTELN
            if (!string.IsNullOrWhiteSpace(AktiveUmfrage.ErstelltVon))
            {
                string erstellerWindowsBenutzername = AktiveUmfrage.ErstelltVon;

                string? anzeigename = await _context.Benutzer
                    .AsNoTracking()
                    .Where(b =>
                        b.WindowsBenutzername == erstellerWindowsBenutzername)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync();

                UmfrageErstellerName = !string.IsNullOrWhiteSpace(anzeigename) ? anzeigename : erstellerWindowsBenutzername;
            }

            GesamtStimmen = AktiveUmfrage.Optionen.Sum(o => o.Stimmen.Count);

            string? windowsBenutzername = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(windowsBenutzername))
            {
                return;
            }

            UmfrageStimme? eigeneStimme = AktiveUmfrage.Optionen.SelectMany(o => o.Stimmen).FirstOrDefault(s => string.Equals(s.WindowsBenutzername, windowsBenutzername, StringComparison.OrdinalIgnoreCase));

            EigeneOptionId = eigeneStimme ?.UmfrageOptionId;
        }

        // ABSTIMMEN
        public async Task<IActionResult> OnPostAbstimmenAsync(int umfrageId, int optionId)
        {
            string? windowsBenutzername = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(windowsBenutzername))
            {
                return Forbid();
            }

            DateTime jetzt = DateTime.UtcNow;

            bool umfrageAktiv = await _context.Umfragen.AnyAsync(u => u.Id == umfrageId && u.IstAktiv && u.StartetAm <= jetzt && (!u.EndetAm.HasValue || u.EndetAm.Value >= jetzt));

            if (!umfrageAktiv)
            {
                UmfrageMeldung = "Diese Umfrage ist nicht mehr aktiv.";

                return RedirectToPage();
            }

            bool optionGueltig = await _context.UmfrageOptionen.AnyAsync(o => o.Id == optionId && o.UmfrageId == umfrageId);

            if (!optionGueltig)
            {
                return BadRequest();
            }

            bool bereitsAbgestimmt = await _context.UmfrageStimmen.AnyAsync(s => s.UmfrageId == umfrageId && s.WindowsBenutzername == windowsBenutzername);

            if (bereitsAbgestimmt)
            {
                UmfrageMeldung = "Du hast bei dieser Umfrage bereits abgestimmt.";

                return RedirectToPage();
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

                UmfrageMeldung = "Deine Stimme wurde gespeichert.";
            }
            catch (DbUpdateException)
            {
                // Der eindeutige Datenbankindex schützt
                // zusätzlich vor doppelten Stimmen.
                UmfrageMeldung = "Du hast bei dieser Umfrage bereits abgestimmt.";
            }

            return RedirectToPage();
        }
    }
}