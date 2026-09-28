using System.ComponentModel.DataAnnotations;
using Intranet2.Datenbank.Data;
using Intranet2.Datenbank.Models;
using Intranet2.Sicherheit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Intranet2.Pages.Admin
{
    [Authorize(Policy = Berechtigungen.UmfragenVerwalten)]
    public class UmfragenModel : PageModel
    {
        private readonly DataContext _context;

        public UmfragenModel(DataContext context) { _context = context; }

        public List<Umfrage> Umfragen { get; set; } = new();

        [BindProperty]
        public UmfrageFormular Formular { get; set; } = new();

        public bool BearbeitungHatStimmen { get; set; }

        [TempData]
        public string? Meldung { get; set; }

        // SEITE LADEN
        public async Task<IActionResult> OnGetAsync(int? bearbeitenId)
        {
            await LadeUmfragenAsync();

            if (!bearbeitenId.HasValue)
            {
                Formular.StartetAm = DateTime.Now;

                Formular.IstAktiv = true;

                return Page();
            }

            Umfrage? umfrage = await _context.Umfragen.AsNoTracking().Include(u => u.Optionen).FirstOrDefaultAsync(u => u.Id == bearbeitenId.Value);

            if (umfrage == null)
            {
                return NotFound();
            }

            BearbeitungHatStimmen = await _context.UmfrageStimmen.AnyAsync(s => s.UmfrageId == umfrage.Id);

            Formular = new UmfrageFormular
                {
                    Id = umfrage.Id,

                    Frage = umfrage.Frage,

                    Beschreibung = umfrage.Beschreibung,

                    StartetAm = umfrage.StartetAm,

                    EndetAm = umfrage.EndetAm,

                    IstAktiv = umfrage.IstAktiv,

                    NamentlicheAuswertung = umfrage.NamentlicheAuswertung,

                    Optionen = umfrage.Optionen.OrderBy(o => o.Sortierung).Select(o => o.Text).ToList()
                };

            return Page();
        }

        // SPEICHERN
        public async Task<IActionResult> OnPostSpeichernAsync()
        {
            List<string> optionen = Formular.Optionen.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();

            if (optionen.Count < 2)
            {
                ModelState.AddModelError("Formular.Optionen", "Bitte mindestens zwei Antwortmöglichkeiten angeben.");
            }

            if (optionen.Distinct(StringComparer.OrdinalIgnoreCase).Count() != optionen.Count)
            {
                ModelState.AddModelError("Formular.Optionen", "Antwortmöglichkeiten dürfen nicht doppelt vorkommen.");
            }

            if (Formular.EndetAm.HasValue && Formular.EndetAm.Value <= Formular.StartetAm)
            {
                ModelState.AddModelError("Formular.EndetAm", "Das Enddatum muss nach dem Startdatum liegen.");
            }

            if (!ModelState.IsValid)
            {
                Formular.Optionen = optionen.Count > 0 ? optionen : Formular.Optionen;

                if (Formular.Id != 0)
                {
                    BearbeitungHatStimmen = await _context.UmfrageStimmen.AnyAsync(s => s.UmfrageId == Formular.Id);
                }

                await LadeUmfragenAsync();

                return Page();
            }

            // NEUE UMFRAGE
            if (Formular.Id == 0)
            {
                Umfrage umfrage = new Umfrage
                    {
                        Frage = Formular.Frage.Trim(),

                        Beschreibung = string.IsNullOrWhiteSpace(Formular.Beschreibung) ? null : Formular.Beschreibung.Trim(),

                        StartetAm = Formular.StartetAm,

                        EndetAm = Formular.EndetAm,

                        IstAktiv = Formular.IstAktiv,

                        NamentlicheAuswertung = Formular.NamentlicheAuswertung,

                        ErstelltAm = DateTime.UtcNow,

                        ErstelltVon = User.Identity?.Name
                    };


                for (int i = 0; i < optionen.Count; i++)
                {
                    umfrage.Optionen.Add(new UmfrageOption
                    { 
                        Text = optionen[i],
                        
                        Sortierung = i
                    });
                }

                _context.Umfragen.Add(umfrage);

                await _context.SaveChangesAsync();

                Meldung = $"Die Umfrage „{umfrage.Frage}“ wurde erstellt.";
            }

            // BEARBEITEN
            else
            {
                Umfrage? umfrage = await _context.Umfragen.Include(u => u.Optionen).FirstOrDefaultAsync(u => u.Id == Formular.Id);

                if (umfrage == null)
                {
                    return NotFound();
                }

                bool hatStimmen = await _context.UmfrageStimmen.AnyAsync(s => s.UmfrageId == umfrage.Id);


                // Nach der ersten Stimmabgabe darf die Einstellung
                // nicht mehr verändert werden.

                if (hatStimmen && Formular.NamentlicheAuswertung != umfrage.NamentlicheAuswertung)
                {
                    Formular.NamentlicheAuswertung = umfrage.NamentlicheAuswertung;

                    ModelState.Remove("Formular.NamentlicheAuswertung");

                    ModelState.AddModelError(string.Empty, "Die namentliche Auswertung kann nach der ersten Stimme nicht mehr geändert werden.");

                    BearbeitungHatStimmen = true;

                    await LadeUmfragenAsync();

                    return Page();
                }

                umfrage.Frage = Formular.Frage.Trim();

                umfrage.Beschreibung = string.IsNullOrWhiteSpace(Formular.Beschreibung) ? null : Formular.Beschreibung.Trim();

                umfrage.StartetAm = Formular.StartetAm;

                umfrage.EndetAm = Formular.EndetAm;

                umfrage.IstAktiv = Formular.IstAktiv;

                // Nur vor der ersten Stimmabgabe veränderbar.
                if (!hatStimmen)
                {
                    umfrage.NamentlicheAuswertung = Formular.NamentlicheAuswertung;
                }

                // Antwortmöglichkeiten nur ändern,
                // solange noch niemand abgestimmt hat.
                if (!hatStimmen)
                {
                    _context.UmfrageOptionen.RemoveRange(umfrage.Optionen);

                    for (int i = 0; i < optionen.Count; i++)
                    {
                        _context.UmfrageOptionen.Add(new UmfrageOption
                        {
                                UmfrageId = umfrage.Id,

                                Text = optionen[i],

                                Sortierung = i
                        });
                    }
                }

                await _context.SaveChangesAsync();

                Meldung = $"Die Umfrage „{umfrage.Frage}“ wurde gespeichert.";
            }

            return RedirectToPage();
        }

        // LÖSCHEN
        public async Task<IActionResult> OnPostLoeschenAsync(int id)
        {
            Umfrage? umfrage = await _context.Umfragen.FindAsync(id);

            if (umfrage == null)
            {
                return NotFound();
            }

            string frage = umfrage.Frage;

            _context.Umfragen.Remove(umfrage);

            await _context.SaveChangesAsync();

            Meldung = $"Die Umfrage „{frage}“ wurde gelöscht.";

            return RedirectToPage();
        }

        // LISTE LADEN
        private async Task LadeUmfragenAsync()
        {
            Umfragen = await _context.Umfragen.AsNoTracking().Include(u => u.Optionen).Include(u => u.Stimmen).OrderByDescending(u => u.StartetAm).ToListAsync();
        }

        // FORMULAR
        public class UmfrageFormular
        {
            public int Id { get; set; }

            [Required(ErrorMessage = "Bitte eine Frage eingeben.")]
            [MaxLength(300)]
            public string Frage { get; set; } = string.Empty;

            [MaxLength(1000)]
            public string? Beschreibung { get; set; }

            [Required]
            public DateTime StartetAm { get; set; } = DateTime.Now;

            public DateTime? EndetAm { get; set; }

            public bool IstAktiv { get; set; } = true;

            public bool NamentlicheAuswertung { get; set; } = false;

            public List<string> Optionen { get; set; } = new() { string.Empty, string.Empty };
        }
    }
}