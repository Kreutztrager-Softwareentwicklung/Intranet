namespace Intranet2.Services.ActiveDirectory
{
    public class Mitarbeiter
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SamAccountName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        // Hauptabteilung aus dem AD-Feld "Abteilung"
        public string Department { get; set; } = string.Empty;

        // Unterabteilung aus dem AD-Feld "Beschreibung"
        public string Description { get; set; } = string.Empty;

        public string TelephoneNumber { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string StreetAddress { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Office { get; set; } = string.Empty;
        public string EmployeeID { get; set; } = string.Empty;

        public string BereinigterVorname
        {
            get
            {
                return BereinigeName(FirstName);
            }
        }


        public string BereinigterNachname
        {
            get
            {
                return BereinigeName(LastName);
            }
        }


        public string Anzeigename
        {
            get
            {
                string vorname = BereinigterVorname;
                string nachname = BereinigterNachname;

                if (!string.IsNullOrWhiteSpace(vorname) ||
                    !string.IsNullOrWhiteSpace(nachname))
                {
                    return $"{vorname} {nachname}".Trim();
                }

                return BereinigeName(DisplayName);
            }
        }


        private static string BereinigeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string bereinigt = name.Trim();

            string[] titel = { "Prof. Dr.", "Dipl.-Wirt.-Ing.", "Dipl.-Kfm.", "Dipl.-Ing.", "M.Sc.", "B.Sc.", "M.A.", "B.A.", "MBA", "Prof.", "Dr.", "Ing." };

            foreach (string titelEintrag in titel)
            {
                bereinigt = bereinigt.Replace(titelEintrag, "", StringComparison.OrdinalIgnoreCase);
            }

            bereinigt = bereinigt
                .Replace(",", "")
                .Replace(";", "")
                .Replace("(", "")
                .Replace(")", "");

            // Mehrfache Leerzeichen entfernen
            while (bereinigt.Contains("  "))
            {
                bereinigt = bereinigt.Replace("  ", " ");
            }

            return bereinigt.Trim();
        }


        // Niederlassung automatisch bestimmen
        public string Niederlassung
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(City))
                {
                    return City.Trim();
                }

                return string.Empty;
            }
        }


        // =====================================================
        // UNTERABTEILUNGEN AUS DEM AD-FELD "BESCHREIBUNG"
        // =====================================================
        //
        // Beispiele:
        //
        // E-Montage
        //
        // E-Montage / E-Engineering
        //
        // E-Montage (Leitung) / E-Engineering
        //
        // E-Montage (Leitung) / E-Engineering (Leitung)
        //
        // Alte Schreibweise wird ebenfalls berücksichtigt:
        // E-Montage / Leitung
        //
        // =====================================================

        public List<UnterabteilungZuordnung> Unterabteilungen
        {
            get
            {
                var zuordnungen = new List<UnterabteilungZuordnung>();

                if (string.IsNullOrWhiteSpace(Description))
                {
                    return zuordnungen;
                }

                // Einzelne Unterabteilungen am Schrägstrich trennen.
                string[] teile = Description.Split(
                    '/',
                    StringSplitOptions.TrimEntries |
                    StringSplitOptions.RemoveEmptyEntries);

                const string leitungsSuffix = "(Leitung)";

                foreach (string teil in teile)
                {
                    // ---------------------------------------------
                    // Bisherige Schreibweise unterstützen:
                    // E-Montage / Leitung
                    // ---------------------------------------------

                    if (string.Equals(
                        teil,
                        "Leitung",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (zuordnungen.Count > 0)
                        {
                            zuordnungen[^1].IstLeitung = true;
                        }

                        continue;
                    }

                    // ---------------------------------------------
                    // Neue Schreibweise erkennen:
                    // E-Montage (Leitung)
                    // ---------------------------------------------

                    bool istLeitung = teil.EndsWith(
                        leitungsSuffix,
                        StringComparison.OrdinalIgnoreCase);

                    string name = istLeitung
                        ? teil[..^leitungsSuffix.Length].Trim()
                        : teil.Trim();

                    // Leere Unterabteilungen ignorieren.
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    // ---------------------------------------------
                    // Doppelte Unterabteilungen vermeiden
                    // ---------------------------------------------

                    UnterabteilungZuordnung? vorhanden =
                        zuordnungen.FirstOrDefault(z =>
                            string.Equals(
                                z.Name,
                                name,
                                StringComparison.OrdinalIgnoreCase));

                    if (vorhanden != null)
                    {
                        // Falls derselbe Bereich mehrfach vorkommt,
                        // bleibt eine vorhandene Leitungsfunktion erhalten.

                        if (istLeitung)
                        {
                            vorhanden.IstLeitung = true;
                        }

                        continue;
                    }

                    // ---------------------------------------------
                    // Neue Zuordnung hinzufügen
                    // ---------------------------------------------

                    zuordnungen.Add(new UnterabteilungZuordnung
                    {
                        Name = name,
                        IstLeitung = istLeitung
                    });
                }

                return zuordnungen;
            }
        }


        // =====================================================
        // BISHERIGE EIGENSCHAFT ZUR KOMPATIBILITÄT
        // =====================================================
        //
        // Liefert weiterhin die erste Unterabteilung.
        //
        // Bestehende Seiten, die Unterabteilung verwenden,
        // bleiben damit grundsätzlich funktionsfähig.
        //
        // =====================================================

        public string Unterabteilung
        {
            get
            {
                return Unterabteilungen.FirstOrDefault()?.Name
                    ?? string.Empty;
            }
        }


        // =====================================================
        // ALLGEMEINE LEITUNGSFUNKTION
        // =====================================================
        //
        // True, wenn der Mitarbeiter in mindestens einer
        // Unterabteilung Leitung ist.
        //
        // =====================================================

        public bool IstLeitung
        {
            get
            {
                return Unterabteilungen.Any(z => z.IstLeitung);
            }
        }


        // =====================================================
        // LEITUNGSFUNKTION EINER BESTIMMTEN UNTERABTEILUNG
        // =====================================================
        public bool IstLeitungInUnterabteilung(string unterabteilung)
        {
            if (string.IsNullOrWhiteSpace(unterabteilung))
            {
                return false;
            }

            return Unterabteilungen.Any(z =>
                z.IstLeitung &&
                string.Equals(
                    z.Name,
                    unterabteilung.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }


        // =====================================================
        // ZUGEHÖRIGKEIT ZU EINER UNTERABTEILUNG
        // =====================================================
        public bool IstInUnterabteilung(string unterabteilung)
        {
            if (string.IsNullOrWhiteSpace(unterabteilung))
            {
                return false;
            }

            return Unterabteilungen.Any(z =>
                string.Equals(
                    z.Name,
                    unterabteilung.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    public class UnterabteilungZuordnung
    {
        public string Name { get; set; } = string.Empty;

        public bool IstLeitung { get; set; }
    }


    public class NiederlassungGruppe
    {
        public string Name { get; set; } = string.Empty;
        public List<Mitarbeiter> Mitarbeiter { get; set; } = new();
    }


    public class AbteilungGruppe
    {
        public string Name { get; set; } = string.Empty;
        public List<Mitarbeiter> Mitarbeiter { get; set; } = new();
    }


    public class UnterabteilungGruppe
    {
        public string Name { get; set; } = string.Empty;
        public List<Mitarbeiter> Mitarbeiter { get; set; } = new();
    }
}