using Intranet2.Datenbank.Data;
using Intranet2.Datenbank.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Intranet2.Services.ActiveDirectory;

namespace Intranet2.Pages
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

        // GROSSE NEWS
        public List<NewsBeitrag> NewsItems { get; set; } = new();

        // GEMEINSAMER STARTSEITEN-FEED
        public List<StartseitenFeedEintrag> FeedEintraege { get; set; } = new();

        public class StartseitenFeedEintrag
        {
            public NewsBeitrag? Kurzmeldung { get; set; }

            public Umfrage? Umfrage { get; set; }

            public DateTime Zeitpunkt { get; set; }

            public string ErstellerName { get; set; } = "Nicht hinterlegt";

            public int GesamtStimmen { get; set; }

            public int? EigeneOptionId { get; set; }
        }

        public string UmfrageErstellerName { get; set; } = "Nicht hinterlegt";

        public int? EigeneOptionId { get; set; }

        public int GesamtStimmen { get; set; }

        [TempData]
        public string? UmfrageMeldung { get; set; }

        // SEITE LADEN
        public async Task OnGetAsync()
        {
            DateTime jetzt = DateTime.Now;

            // GROSSE NEWS
            NewsItems = await _context.NewsBeitraege.AsNoTracking().Where(n => n.IstVeroeffentlicht).Where(n => n.VeroeffentlichtAm <= jetzt).Where(n => !n.IstKurzmeldung)
                .OrderByDescending(n => n.VeroeffentlichtAm).Take(3).ToListAsync();

            // 1. DIE DREI NEUESTEN KURZMELDUNGEN LADEN
            var kurzmeldungen = await _context.NewsBeitraege
                .AsNoTracking()
                .Where(n => n.IstVeroeffentlicht)
                .Where(n => n.IstKurzmeldung)
                .Where(n => n.VeroeffentlichtAm <= jetzt)
                .OrderByDescending(n => n.VeroeffentlichtAm)
                .ThenByDescending(n => n.Id)
                .Take(3)
                .ToListAsync();

            // 2. DIE DREI NEUESTEN LAUFENDEN UMFRAGEN LADEN
            var umfragen = await _context.Umfragen
                .AsNoTracking()
                .Where(u => u.IstAktiv)
                .Where(u => u.StartetAm <= jetzt)
                .Where(u => !u.EndetAm.HasValue || u.EndetAm.Value >= jetzt)
                .OrderByDescending(u => u.ErstelltAm)
                .ThenByDescending(u => u.Id)
                .Take(3)
                .Include(u => u.Optionen).ThenInclude(o => o.Stimmen)
                .ToListAsync();

            // 3. KURZMELDUNGEN IN DEN FEED ÜBERNEHMEN
            var feed = new List<StartseitenFeedEintrag>();

            foreach (var meldung in kurzmeldungen)
            {
                feed.Add(new StartseitenFeedEintrag
                {
                    Kurzmeldung = meldung,

                    Zeitpunkt = meldung.VeroeffentlichtAm
                });
            }

            // 4. UMFRAGEN IN DEN FEED ÜBERNEHMEN
            foreach (var umfrage in umfragen)
            {
                feed.Add(new StartseitenFeedEintrag
                {
                    Umfrage = umfrage,

                    Zeitpunkt = umfrage.ErstelltAm
                });
            }

            // 5. GEMEINSAM SORTIEREN UND AUF DREI BEGRENZEN
            FeedEintraege = feed
                .OrderByDescending(e => e.Zeitpunkt)
                .ThenByDescending(e => e.Umfrage?.Id ?? e.Kurzmeldung?.Id ?? 0)
                .Take(3)
                .ToList();

            // 6. ERSTELLER DER ANGEZEIGTEN UMFRAGEN LADEN
            var erstellerBenutzernamen = FeedEintraege
                .Where(e => e.Umfrage != null)
                .Select(e => e.Umfrage!.ErstelltVon)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Benutzer aus der Datenbank gemeinsam laden
            var benutzer = await _context.Benutzer
                .AsNoTracking()
                .Where(b => erstellerBenutzernamen.Contains(b.WindowsBenutzername))
                .Select(b => new
                {
                    b.WindowsBenutzername,
                    b.Name
                })
                .ToListAsync();

            // 7. ERSTELLERNAMEN UND ABSTIMMUNGEN ZUORDNEN
            string? windowsBenutzername = User.Identity?.Name;

            foreach (var eintrag in FeedEintraege)
            {
                if (eintrag.Umfrage == null)
                {
                    continue;
                }

                var umfrage = eintrag.Umfrage;

                // Vollständigen Erstellernamen ermitteln
                if (!string.IsNullOrWhiteSpace(umfrage.ErstelltVon))
                {
                    string erstellerWindowsBenutzername = umfrage.ErstelltVon;

                    var datenbankBenutzer = benutzer.FirstOrDefault(b => string.Equals(b.WindowsBenutzername, erstellerWindowsBenutzername, StringComparison.OrdinalIgnoreCase));

                    // Name bevorzugt aus Active Directory laden
                    var mitarbeiter = _mitarbeiterService.GetMitarbeiterFuerBenutzername(erstellerWindowsBenutzername);

                    eintrag.ErstellerName = !string.IsNullOrWhiteSpace(mitarbeiter?.Anzeigename) ? mitarbeiter.Anzeigename 
                        : !string.IsNullOrWhiteSpace(datenbankBenutzer?.Name) ? datenbankBenutzer.Name : erstellerWindowsBenutzername;
                }

                // Gesamtstimmen berechnen
                eintrag.GesamtStimmen = umfrage.Optionen.Sum(o => o.Stimmen.Count);

                // Eigene Abstimmung des angemeldeten Benutzers
                if (string.IsNullOrWhiteSpace(windowsBenutzername))
                {
                    continue;
                }

                var eigeneStimme = umfrage.Optionen
                    .SelectMany(o => o.Stimmen)
                    .FirstOrDefault(s => string.Equals(s.WindowsBenutzername, windowsBenutzername, StringComparison.OrdinalIgnoreCase));


                eintrag.EigeneOptionId = eigeneStimme?.UmfrageOptionId;
            }
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