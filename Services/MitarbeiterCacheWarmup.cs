using Intranet2.Services.ActiveDirectory;
using Intranet2.Services.Fotos;

namespace Intranet2.Services
{
    public class MitarbeiterCacheWarmup : BackgroundService
    {
        private readonly MitarbeiterService _mitarbeiterService;
        private readonly MitarbeiterFotoService _fotoService;
        private readonly ILogger<MitarbeiterCacheWarmup> _logger;

        private static readonly TimeSpan WiederholungsIntervall = TimeSpan.FromHours(7);

        public MitarbeiterCacheWarmup(
            MitarbeiterService mitarbeiterService,
            MitarbeiterFotoService fotoService,
            ILogger<MitarbeiterCacheWarmup> logger)
        {
            _mitarbeiterService = mitarbeiterService;
            _fotoService = fotoService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Beim Start sofort ausführen – OHNE CancellationToken
            // damit der Warmup nicht abgebrochen wird
            WaermeCache();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(WiederholungsIntervall, stoppingToken);
                    WaermeCache();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private void WaermeCache()
        {
            // Läuft auf einem eigenen Thread – KEIN CancellationToken
            // damit er garantiert bis zum Ende durchläuft
            Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Cache-Warmup: Starte um {Zeit}...", DateTime.Now.ToString("HH:mm:ss"));

                    // 1. Mitarbeiter aus AD laden
                    var mitarbeiter = _mitarbeiterService.GetMitarbeiter();
                    _logger.LogInformation("Cache-Warmup: {Anzahl} Mitarbeiter aus AD geladen.", mitarbeiter.Count);

                    // 2. Ordnerindex laden
                    _logger.LogInformation("Cache-Warmup: Lade Foto-Ordnerindex...");
                    var index = _fotoService.GetOrdnerIndex();
                    _logger.LogInformation("Cache-Warmup: {Anzahl} Foto-Ordner indexiert.", index.Count);

                    // 3. Alle Fotos parallel vorwärmen – KEIN CancellationToken!
                    _logger.LogInformation("Cache-Warmup: Wärme Fotos vor...");
                    int count = 0;
                    mitarbeiter.AsParallel()
                        .ForAll(m =>
                        {
                            _fotoService.GetFotoUrl(m.BereinigterNachname, m.BereinigterVorname);
                            Interlocked.Increment(ref count);
                        });

                    _logger.LogInformation("Cache-Warmup: {Anzahl} Fotos vorgewärmt.", count);
                    _logger.LogInformation("Cache-Warmup: Abgeschlossen um {Zeit}.", DateTime.Now.ToString("HH:mm:ss"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cache-Warmup fehlgeschlagen.");
                }
            });
        }
    }
}
