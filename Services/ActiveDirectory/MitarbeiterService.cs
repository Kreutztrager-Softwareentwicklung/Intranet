using System.DirectoryServices;
using Microsoft.Extensions.Caching.Memory;

namespace Intranet2.Services.ActiveDirectory
{
    public class MitarbeiterService
    {
        private const string CacheKey = "ActiveDirectory_Mitarbeiter";

        private readonly string _domain;
        private readonly string _mitarbeiterOU;
        private readonly int _cacheMinutes;

        private readonly IMemoryCache _cache;
        private readonly ILogger<MitarbeiterService> _logger;


        public MitarbeiterService(IConfiguration configuration, IMemoryCache cache, ILogger<MitarbeiterService> logger)
        {
            _domain = configuration["ActiveDirectory:Domain"] ?? throw new InvalidOperationException("ActiveDirectory:Domain wurde nicht konfiguriert.");

            _mitarbeiterOU = configuration["ActiveDirectory:MitarbeiterOU"] ?? throw new InvalidOperationException("ActiveDirectory:MitarbeiterOU wurde nicht konfiguriert.");

            _cacheMinutes = int.TryParse(configuration["ActiveDirectory:CacheMinutes"], out int cacheMinutes) ? cacheMinutes : 10;

            _cache = cache;
            _logger = logger;
        }

        // ALLE MITARBEITER AUS DEM AD LADEN
        public List<Mitarbeiter> GetMitarbeiter()
        {
            if (_cache.TryGetValue(CacheKey, out List<Mitarbeiter>? cachedMitarbeiter))
            {
                return cachedMitarbeiter ?? new List<Mitarbeiter>();
            }

            var mitarbeiter = new List<Mitarbeiter>();

            try
            {
                // LDAP-Pfad:
                //
                // LDAP://OU=Mitarbeiter,
                // DC=kreutztraeger,
                // DC=com

                string ldapPath = $"LDAP://{_mitarbeiterOU},DC={_domain.Replace(".", ",DC=")}";


                using var entry = new DirectoryEntry(ldapPath);


                using var searcher = new DirectorySearcher(entry);


                // Gesamte OU=Mitarbeiter inklusive
                // aller Unter-OUs durchsuchen.
                searcher.SearchScope = SearchScope.Subtree;


                // Gepagete Suche verwenden.
                searcher.PageSize = 1000;


                // Nur aktive Benutzer
                searcher.Filter = "(&(objectClass=user)" + "(objectCategory=person)" + "(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";


                // BENÖTIGTE AD-FELDER
                searcher.PropertiesToLoad.Add("displayName");

                searcher.PropertiesToLoad.Add("givenName");

                searcher.PropertiesToLoad.Add("sn");

                searcher.PropertiesToLoad.Add("mail");

                searcher.PropertiesToLoad.Add("sAMAccountName");

                searcher.PropertiesToLoad.Add("title");

                searcher.PropertiesToLoad.Add("department");

                searcher.PropertiesToLoad.Add("description");

                searcher.PropertiesToLoad.Add("telephoneNumber");

                searcher.PropertiesToLoad.Add("mobile");

                searcher.PropertiesToLoad.Add("streetAddress");

                searcher.PropertiesToLoad.Add("postalCode");

                searcher.PropertiesToLoad.Add("l");

                searcher.PropertiesToLoad.Add("physicalDeliveryOfficeName");

                searcher.PropertiesToLoad.Add("employeeID");


                using SearchResultCollection results = searcher.FindAll();


                foreach (SearchResult result in results)
                {
                    var mitarbeiterEintrag = new Mitarbeiter
                        {
                            DisplayName = GetProperty(result, "displayName"),

                            FirstName = GetProperty(result, "givenName"),

                            LastName = GetProperty(result, "sn"),

                            Email = GetProperty(result, "mail"),

                            SamAccountName = GetProperty(result, "sAMAccountName"),

                            Title = GetProperty(result, "title"),

                            Department = GetProperty(result, "department"),

                            Description = GetProperty(result, "description"),

                            TelephoneNumber = GetProperty(result, "telephoneNumber"),

                            Mobile = GetProperty(result, "mobile"),

                            StreetAddress = GetProperty(result, "streetAddress"),

                            PostalCode = GetProperty(result, "postalCode"),

                            City = GetProperty(result, "l"),

                            Office = GetProperty(result, "physicalDeliveryOfficeName"),

                            EmployeeID = GetProperty(result, "employeeID")
                        };


                    // Benutzer ohne Namen nicht anzeigen.
                    if (!string.IsNullOrWhiteSpace(mitarbeiterEintrag.DisplayName))
                    {
                        mitarbeiter.Add(mitarbeiterEintrag);
                    }
                }


                // SORTIEREN
                mitarbeiter = mitarbeiter
                        .OrderBy(m => m.Niederlassung)
                        .ThenBy(m => m.Title)
                        .ThenBy(m => m.LastName)
                        .ThenBy(m => m.FirstName)
                        .ToList();


                // CACHE
                _cache.Set(CacheKey, mitarbeiter, TimeSpan.FromMinutes(_cacheMinutes));


                _logger.LogInformation("{Anzahl} Mitarbeiter wurden aus dem Active Directory geladen.", mitarbeiter.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mitarbeiter konnten nicht aus dem Active Directory geladen werden.");
            }


            return mitarbeiter;
        }

        // MITARBEITER FÜR DIE ANSPRECHPARTNER-SUCHE
        public List<Mitarbeiter> GetSuchbareMitarbeiter()
        {
            return GetMitarbeiter()

                // Funktionskonten wie Alarmhandy oder
                // Service Rufbereitschaft nicht anzeigen
                .Where(m => !IstFunktionskonto(m))

                // Nur Mitarbeiter mit einem verwendbaren Namen
                .Where(m => !string.IsNullOrWhiteSpace(m.Anzeigename))

                // Alphabetisch
                .OrderBy(m => m.BereinigterNachname)
                .ThenBy(m => m.BereinigterVorname)

                .ToList();
        }

        // MITARBEITER EINER NIEDERLASSUNG LADEN
        public List<Mitarbeiter> GetMitarbeiterFuerNiederlassung(string niederlassung)
        {
            if (string.IsNullOrWhiteSpace(niederlassung))
            {
                return new List<Mitarbeiter>();
            }

            return GetMitarbeiter()

                // Nur Mitarbeiter der ausgewählten Niederlassung
                .Where(m =>
                    string.Equals(m.Niederlassung, niederlassung.Trim(), StringComparison.OrdinalIgnoreCase))

                // Funktionskonten nicht anzeigen
                .Where(m => !IstFunktionskonto(m))

                // -------------------------------------------------
                // 1. Jan-Peter Nissen immer zuerst, dann Andreas Betke
                // -------------------------------------------------
                .OrderByDescending(m => string.Equals(m.Anzeigename, "Jan-Peter Nissen", StringComparison.OrdinalIgnoreCase))

                // -------------------------------------------------
                // 2. Andreas Betke im Stammhaus Bremen als Zweiter
                // -------------------------------------------------
                .ThenByDescending(m => string.Equals(m.Anzeigename, "Andreas Betke", StringComparison.OrdinalIgnoreCase))

                // -------------------------------------------------
                // 2. Danach alle Leitungen
                // -------------------------------------------------
                .ThenByDescending(m => m.IstLeitung)

                // -------------------------------------------------
                // 3. Danach Hauptabteilung
                // -------------------------------------------------
                //.ThenBy(m => m.Department)

                // -------------------------------------------------
                // 4. Danach Unterabteilung
                // -------------------------------------------------
                //.ThenBy(m => m.Unterabteilung)

                // -------------------------------------------------
                // 5. Danach alphabetisch
                // -------------------------------------------------
                .ThenBy(m => m.BereinigterNachname)
                .ThenBy(m => m.BereinigterVorname)

                .ToList();
        }

        // FUNKTIONSKONTEN NICHT ALS MITARBEITER ANZEIGEN
        private static bool IstFunktionskonto(Mitarbeiter mitarbeiter)
        {
            if (string.IsNullOrWhiteSpace(mitarbeiter.DisplayName))
            {
                return false;
            }

            string displayName = mitarbeiter.DisplayName.Trim();

            // Service-Rufbereitschaft explizit ausschließen
            if (string.Equals(displayName, "Service Rufbereitschaft", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Alarmhandys weiterhin ausschließen
            if (displayName.Contains("Alarmhandy", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        // MITARBEITER NACH NIEDERLASSUNG GRUPPIEREN
        public List<NiederlassungGruppe> GetMitarbeiterNachNiederlassung()
        {
            return GetMitarbeiter()

                // Funktionskonten nicht anzeigen
                .Where(m => !IstFunktionskonto(m))

                // Mitarbeiter ohne Niederlassung auslassen
                .Where(m =>
                    !string.IsNullOrWhiteSpace(m.Niederlassung))

                // Nach Niederlassung gruppieren
                .GroupBy(
                    m => m.Niederlassung.Trim(),
                    StringComparer.OrdinalIgnoreCase)

                .Select(gruppe => new NiederlassungGruppe
                {
                    Name = gruppe.Key,

                    Mitarbeiter = gruppe

                        // Leitung immer ganz oben
                        .OrderByDescending(m => m.IstLeitung)

                        // Danach Abteilung
                        .ThenBy(m => m.Department)

                        // Danach Unterabteilung
                        .ThenBy(m => m.Unterabteilung)

                        // Danach Name
                        .ThenBy(m => m.LastName)
                        .ThenBy(m => m.FirstName)

                        .ToList()
                })

                // Niederlassungen alphabetisch
                .OrderBy(g => g.Name)

                .ToList();
        }

        // MITARBEITER NACH ABTEILUNG GRUPPIEREN
        public List<AbteilungGruppe> GetMitarbeiterNachAbteilung()
        {
            return GetMitarbeiter()

                // Funktionskonten nicht anzeigen
                .Where(m => !IstFunktionskonto(m))

                // Mitarbeiter ohne Abteilung auslassen
                .Where(m => !string.IsNullOrWhiteSpace(m.Department))

                // Nach Abteilung gruppieren
                .GroupBy(m => m.Department.Trim(), StringComparer.OrdinalIgnoreCase)

                // Gruppe erstellen
                .Select(gruppe => new AbteilungGruppe
                {
                    Name = gruppe.Key,

                    Mitarbeiter = gruppe
                        .OrderBy(m => m.Title)
                        .ThenBy(m => m.LastName)
                        .ThenBy(m => m.FirstName)
                        .ToList()
                })

                // Abteilungen alphabetisch sortieren
                .OrderBy(g => g.Name)

                .ToList();
        }

        // MITARBEITER EINER ABTEILUNG LADEN
        public List<Mitarbeiter> GetMitarbeiterFuerAbteilung(string abteilung)
        {
            return GetMitarbeiter()

                // Funktionskonten nicht anzeigen
                .Where(m => !IstFunktionskonto(m))

                // Nur Mitarbeiter mit Abteilung
                .Where(m => !string.IsNullOrWhiteSpace(m.Department))

                // Gewählte Abteilung
                .Where(m => string.Equals(m.Department.Trim(), abteilung.Trim(), StringComparison.OrdinalIgnoreCase))

                // Sortierung
                .OrderBy(m => m.Title)
                .ThenBy(m => m.LastName)
                .ThenBy(m => m.FirstName)
                .ToList();
        }


        // =====================================================
        // UNTERABTEILUNGEN EINER HAUPTABTEILUNG LADEN
        // =====================================================
        public List<UnterabteilungGruppe> GetUnterabteilungenFuerAbteilung(string abteilung)
        {
            if (string.IsNullOrWhiteSpace(abteilung))
            {
                return new List<UnterabteilungGruppe>();
            }

            // -------------------------------------------------
            // Mitarbeiter der Hauptabteilung laden
            // -------------------------------------------------

            var mitarbeiter = GetMitarbeiter()

                .Where(m => !IstFunktionskonto(m))

                .Where(m =>
                    !string.IsNullOrWhiteSpace(m.Department) &&
                    string.Equals(
                        m.Department.Trim(),
                        abteilung.Trim(),
                        StringComparison.OrdinalIgnoreCase))

                .ToList();


            // -------------------------------------------------
            // Alle Unterabteilungszuordnungen zusammenführen
            // -------------------------------------------------
            //
            // Ein Mitarbeiter mit drei Unterabteilungen erzeugt
            // hier drei Zuordnungen.
            //
            // -------------------------------------------------

            var zuordnungen = mitarbeiter

                .SelectMany(m =>
                    m.Unterabteilungen.Select(z => new
                    {
                        Mitarbeiter = m,
                        Zuordnung = z
                    }))

                .ToList();


            // -------------------------------------------------
            // Nach Unterabteilung gruppieren
            // -------------------------------------------------

            return zuordnungen

                .GroupBy(
                    eintrag => eintrag.Zuordnung.Name,
                    StringComparer.OrdinalIgnoreCase)

                .Select(gruppe => new UnterabteilungGruppe
                {
                    Name = gruppe.Key,

                    Mitarbeiter = gruppe

                        // Leitung nur für diese Unterabteilung
                        // an die erste Stelle setzen.

                        .OrderByDescending(e =>
                            e.Zuordnung.IstLeitung)

                        // Danach alphabetisch sortieren.

                        .ThenBy(e => e.Mitarbeiter.LastName)
                        .ThenBy(e => e.Mitarbeiter.FirstName)

                        .Select(e => e.Mitarbeiter)

                        .ToList()
                })

                // Unterabteilungen alphabetisch sortieren.
                .OrderBy(g => g.Name)

                .ToList();
        }


        // =====================================================
        // MITARBEITER EINER UNTERABTEILUNG LADEN
        // =====================================================

        public List<Mitarbeiter> GetMitarbeiterFuerUnterabteilung(string abteilung, string unterabteilung)
        {
            if (string.IsNullOrWhiteSpace(abteilung) || string.IsNullOrWhiteSpace(unterabteilung))
            {
                return new List<Mitarbeiter>();
            }

            return GetMitarbeiter()

                // ---------------------------------------------
                // Funktionskonten ausschließen
                // ---------------------------------------------

                .Where(m => !IstFunktionskonto(m))

                // ---------------------------------------------
                // Hauptabteilung prüfen
                // ---------------------------------------------

                .Where(m =>
                    !string.IsNullOrWhiteSpace(m.Department) &&
                    string.Equals(
                        m.Department.Trim(),
                        abteilung.Trim(),
                        StringComparison.OrdinalIgnoreCase))

                // ---------------------------------------------
                // Mitgliedschaft in der gewählten Unterabteilung
                // ---------------------------------------------

                .Where(m =>
                    m.IstInUnterabteilung(unterabteilung))

                // ---------------------------------------------
                // Leitung DIESER Unterabteilung zuerst
                // ---------------------------------------------

                .OrderByDescending(m =>
                    m.IstLeitungInUnterabteilung(unterabteilung))

                // ---------------------------------------------
                // Danach alphabetisch
                // ---------------------------------------------

                .ThenBy(m => m.LastName)
                .ThenBy(m => m.FirstName)

                .ToList();
        }

        // AKTUELLEN MITARBEITER ÜBER WINDOWS-BENUTZERNAMEN FINDEN
        public Mitarbeiter? GetMitarbeiterFuerBenutzername(string windowsBenutzername)
        {
            if (string.IsNullOrWhiteSpace(windowsBenutzername))
            {
                return null;
            }

            // Aus z. B. KREUZTRAEGER\schoen wird schoen
            string samAccountName = windowsBenutzername.Contains("\\") ? windowsBenutzername.Split('\\').Last() : windowsBenutzername;

            return GetMitarbeiter().FirstOrDefault(m => string.Equals(m.SamAccountName, samAccountName, StringComparison.OrdinalIgnoreCase));
        }

        // AD-EIGENSCHAFT LESEN
        private static string GetProperty(SearchResult result, string propertyName)
        {
            if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
            {
                return result.Properties[propertyName][0] ?.ToString() ?? string.Empty;
            }

            return string.Empty;
        }
    }
}