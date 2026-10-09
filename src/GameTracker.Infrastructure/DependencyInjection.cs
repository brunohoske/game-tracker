using System.Net;
using GameTracker.Core.Detection;
using GameTracker.Core.Library;
using GameTracker.Core.Metadata;
using GameTracker.Core.Reporting;
using GameTracker.Core.Settings;
using GameTracker.Core.Tracking;
using GameTracker.Infrastructure.Detection;
using GameTracker.Infrastructure.Detection.Libraries;
using GameTracker.Infrastructure.Metadata;
using GameTracker.Infrastructure.Persistence;
using GameTracker.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameTracker.Infrastructure;

public static class DependencyInjection
{
    private const string UserAgent = "GameTracker/1.0 (+desktop playtime tracker)";

    public static IServiceCollection AddGameTrackerInfrastructure(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);

        // Persistence
        services.AddDbContextFactory<GameTrackerDbContext>(options => options.UseSqlite($"Data Source={paths.DatabasePath}"));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ITrackingStore, EfTrackingStore>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IGameLibrary, GameLibrary>();

        // Settings
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<StartupRegistration>();

        // Detection & tracking
        services.AddSingleton<IProcessSnapshotProvider, Win32ProcessSnapshotProvider>();
        services.AddSingleton<ILibraryScanner, SteamLibraryScanner>();
        services.AddSingleton<ILibraryScanner, EpicLibraryScanner>();
        services.AddSingleton<ILibraryScanner, GogLibraryScanner>();
        services.AddSingleton<ILibraryScanner, UbisoftLibraryScanner>();
        services.AddSingleton<GameDetector>();
        services.AddSingleton<IForegroundAppProbe, Win32ForegroundAppProbe>();
        services.AddSingleton<BehaviorGameDetector>();
        services.AddSingleton<SessionTracker>();
        services.AddSingleton<IActiveGamesMonitor>(sp => sp.GetRequiredService<SessionTracker>());
        services.AddSingleton<TrackingWorker>();
        services.AddSingleton<ILibraryChangeNotifier>(sp => sp.GetRequiredService<TrackingWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<TrackingWorker>());

        // Metadata
        services.AddSingleton(CreateHttpClient());
        services.AddSingleton<ImageStore>();
        services.AddSingleton<TwitchTokenProvider>();
        services.AddSingleton<IMetadataProvider, SteamStoreMetadataProvider>();
        services.AddSingleton<IMetadataProvider, IgdbMetadataProvider>();
        services.AddSingleton<MetadataEnrichmentService>();
        services.AddSingleton<IMetadataQueue>(sp => sp.GetRequiredService<MetadataEnrichmentService>());
        services.AddHostedService(sp => sp.GetRequiredService<MetadataEnrichmentService>());

        return services;
    }

    /// <summary>
    /// One long-lived client shared by all singletons; the pooled connection lifetime keeps DNS changes honoured.
    /// </summary>
    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            AutomaticDecompression = DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }
}
