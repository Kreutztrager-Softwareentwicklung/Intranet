using Intranet2.Services.ActiveDirectory;
using Intranet2.Services.Fotos;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Intranet2.Pages.Notfall
{
    public class ErsthelferModel : PageModel
    {
        private readonly MitarbeiterService _mitarbeiterService;
        private readonly MitarbeiterFotoService _fotoService;

        public ErsthelferModel(MitarbeiterService mitarbeiterService, MitarbeiterFotoService fotoService)
        {
            _mitarbeiterService = mitarbeiterService;
            _fotoService = fotoService;
        }

        public List<NiederlassungErsthelfer> Niederlassungen { get; private set; } = new();

        private static readonly string[] ErsthelferBenutzernamen =
        {
            "grashoff",
            "foertsch",
            "UWenzel",
            "heppner",
            "fecke",
            "alkontar",
            "gierth",
            "hohm",
            "sommer",
            "hotzanj",
            "friedrichs",
            "denk",
            "Gutting",
            "Hennings",
            "riemer",
            "freese",
            "lieske",
            "risthaus",
            "erlhoff",
            "kautz",
            "mindt",
            "heimbold"
        };

        public void OnGet()
        {
            var ersthelfer = new List<ErsthelferEintrag>();

            foreach (string benutzername in ErsthelferBenutzernamen)
            {
                Mitarbeiter? mitarbeiter = _mitarbeiterService.GetMitarbeiterFuerBenutzername(benutzername);

                if (mitarbeiter == null)
                {
                    continue;
                }

                string? fotoUrl = _fotoService.GetFotoUrl(mitarbeiter.BereinigterNachname, mitarbeiter.BereinigterVorname);

                ersthelfer.Add(new ErsthelferEintrag
                    {
                        Benutzername = mitarbeiter.SamAccountName,

                        Name = mitarbeiter.Anzeigename,

                        Telefon = mitarbeiter.TelephoneNumber,

                        Mobil = mitarbeiter.Mobile,

                        Email = mitarbeiter.Email,

                        Abteilung = mitarbeiter.Department,

                        Unterabteilung = mitarbeiter.Unterabteilung,

                        Niederlassung = mitarbeiter.Niederlassung,

                        FotoUrl = fotoUrl
                    });
            }

            Niederlassungen = ersthelfer
                    .Where(e => !string.IsNullOrWhiteSpace(e.Niederlassung))

                    .GroupBy(e => e.Niederlassung.Trim(), StringComparer.OrdinalIgnoreCase)

                    .Select(gruppe => new NiederlassungErsthelfer
                        {
                            Niederlassung = gruppe.Key,

                            Ersthelfer = gruppe.OrderBy(e => e.Name).ToList()
                        })

                    // Bremen zuerst
                    .OrderBy(g => string.Equals(g.Niederlassung, "Bremen", StringComparison.OrdinalIgnoreCase) ? 0 : 1)

                    // Danach alphabetisch
                    .ThenBy(g => g.Niederlassung).ToList();
        }

        public string GetNiederlassungAnzeigename(string niederlassung)
        {
            if (string.IsNullOrWhiteSpace(niederlassung))
            {
                return string.Empty;
            }

            string name = niederlassung.Trim();

            if (string.Equals(name, "Bremen", StringComparison.OrdinalIgnoreCase))
            {
                return "Stammhaus Bremen";
            }

            return $"Niederlassung {name}";
        }

        public class ErsthelferEintrag
        {
            public string Benutzername { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;

            public string Telefon { get; set; } = string.Empty;

            public string Mobil { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public string Abteilung { get; set; } = string.Empty;

            public string Unterabteilung { get; set; } = string.Empty;

            public string Niederlassung { get; set; } = string.Empty;

            public string? FotoUrl { get; set; }
        }

        public class NiederlassungErsthelfer
        {
            public string Niederlassung { get; set; } = string.Empty;

            public List<ErsthelferEintrag> Ersthelfer { get; set; } = new();
        }
    }
}