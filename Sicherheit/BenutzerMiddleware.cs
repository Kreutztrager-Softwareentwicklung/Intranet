using Intranet2.Datenbank.Data;
using Intranet2.Datenbank.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Intranet2.Sicherheit
{
    public class BenutzerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan BenutzerCacheDauer = TimeSpan.FromMinutes(5);

        private static readonly string[] _statischePfade =
            ["/css", "/js", "/lib", "/Images", "/uploads", "/favicon", "/mitarbeiterfotos"];

        public BenutzerMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context, DataContext db)
        {
            string path = context.Request.Path.Value ?? "";

            // Statische Dateien sofort durchlassen
            if (_statischePfade.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                string? windowsBenutzername = context.User.Identity.Name;

                if (!string.IsNullOrWhiteSpace(windowsBenutzername))
                {
                    await VerarbeiteBenutzerAsync(context, db, windowsBenutzername);

                    // Gesperrte Konten nicht an die nachfolgende Middleware oder die Razor Pages weiterleiten.

                    if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
                    {
                        return;
                    }
                }
            }

            await _next(context);
        }

        private async Task VerarbeiteBenutzerAsync(
            HttpContext context, DataContext db, string windowsBenutzername)
        {
            string cacheKey = $"Benutzer_{windowsBenutzername.ToLowerInvariant()}";

            // Benutzer aus Cache laden – kein DB-Zugriff bei jedem Request
            if (!_cache.TryGetValue(cacheKey, out Benutzer? benutzer) || benutzer == null)
            {
                benutzer = await db.Benutzer
                    .FirstOrDefaultAsync(b => b.WindowsBenutzername == windowsBenutzername);

                if (benutzer == null)
                    benutzer = await LegeBenutzerAnAsync(db, windowsBenutzername);

                if (benutzer == null)
                    return;

                // Benutzer 5 Minuten cachen
                _cache.Set(cacheKey, benutzer, BenutzerCacheDauer);
            }

            // Gesperrte Benutzer blockieren
            if (!benutzer.IstAktiv)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(
                    "Ihr Benutzerkonto ist für das Intranet deaktiviert.");
                return;
            }

            // Neue Marktplatz-Beiträge ebenfalls cachen (1 Minute reicht)
            string marktplatzCacheKey = $"NeueMarktplatzBeitraege_{benutzer.Id}";
            if (!_cache.TryGetValue(marktplatzCacheKey, out int neueBeitraege))
            {
                DateTime letzterBesuch = benutzer.LetzterMarktplatzBesuch ?? benutzer.RegisteredAt;
                neueBeitraege = await db.MarktplatzBeitraege.CountAsync(m => m.ErstelltAm > letzterBesuch && m.BenutzerId != benutzer.Id);

                _cache.Set(marktplatzCacheKey, neueBeitraege, TimeSpan.FromMinutes(1));
            }

            context.Items["NeueMarktplatzBeitraege"] = neueBeitraege;

            // Rollen als Claims hinzufügen
            var claims = new List<Claim> { new(ClaimTypes.Role, Rollen.Benutzer) };
            if (benutzer.Rolle == Rollen.Admin) claims.Add(new Claim(ClaimTypes.Role, Rollen.Admin));
            if (benutzer.Rolle == Rollen.Redaktion) claims.Add(new Claim(ClaimTypes.Role, Rollen.Redaktion));

            context.User.AddIdentity(new ClaimsIdentity(claims, "IntranetRollen"));
        }

        private static async Task<Benutzer?> LegeBenutzerAnAsync(DataContext db, string windowsBenutzername)
        {
            string name = windowsBenutzername.Contains('\\') ? windowsBenutzername.Split('\\').Last() : windowsBenutzername;

            var benutzer = new Benutzer
            {
                WindowsBenutzername = windowsBenutzername,
                Name = name,
                Email = null,
                Rolle = Rollen.Benutzer,
                IstAktiv = true,
                RegisteredAt = DateTime.UtcNow,
                LetzterMarktplatzBesuch = DateTime.UtcNow,
            };

            db.Benutzer.Add(benutzer);

            try
            {
                await db.SaveChangesAsync();

                db.BenutzerProtokolle.Add(new BenutzerProtokoll
                {
                    BenutzerId = benutzer.Id,
                    BenutzerName = benutzer.Name,
                    WindowsBenutzername = benutzer.WindowsBenutzername,
                    Aktion = "Neu angelegt",
                    Feld = null,
                    AlterWert = null,
                    NeuerWert = $"Rolle: {benutzer.Rolle}; Status: Aktiv",
                    AusgefuehrtVon = "System (erste Anmeldung)",
                    Zeitpunkt = DateTime.UtcNow
                });

                await db.SaveChangesAsync();
                return benutzer;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                return await db.Benutzer.FirstOrDefaultAsync(b => b.WindowsBenutzername == windowsBenutzername);
            }
        }
    }
}
