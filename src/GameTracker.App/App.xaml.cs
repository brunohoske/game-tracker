using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GameTracker.App.Services;
using GameTracker.App.ViewModels;
using GameTracker.Core.Settings;
using GameTracker.Infrastructure;
using GameTracker.Infrastructure.Logging;
using GameTracker.Infrastructure.Persistence;
using GameTracker.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameTracker.App;

public partial class App : Application
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    private SingleInstanceGuard? _instanceGuard;
    private IHost? _host;
    private ILogger<App>? _logger;
    private bool _isShuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Hardware rendering loads the GPU driver, which keeps ~100+ MB of native memory for the lifetime of the
        // process even after the window closes. This UI is simple enough that software rendering is smooth, and it
        // keeps an always-on tray app light (and off the GPU while games run).
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        _instanceGuard = SingleInstanceGuard.TryAcquire();
        if (_instanceGuard is null)
        {
            Shutdown(); // the running instance was asked to show itself
            return;
        }

        try
        {
            _host = BuildHost();
            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            _host.Services.GetRequiredService<DatabaseInitializer>().Initialize();
            _host.Services.GetRequiredService<StartupRegistration>()
                .Apply(_host.Services.GetRequiredService<ISettingsStore>().Current.StartWithWindows);

            await _host.StartAsync();

            var windows = _host.Services.GetRequiredService<WindowManager>();
            var tray = _host.Services.GetRequiredService<TrayIconService>();
            tray.ExitRequested += async (_, _) => await ExitAsync();
            tray.Show();

            _instanceGuard.ActivationRequested += (_, _) => Dispatcher.BeginInvoke(windows.ShowMain);

            if (e.Args.Contains(StartupRegistration.MinimizedArgument, StringComparer.OrdinalIgnoreCase))
                WindowManager.SetEfficiencyMode(true);
            else
                windows.ShowMain();

            _logger.LogInformation("GameTracker started");
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Startup failed");
            MessageBox.Show($"Não foi possível iniciar o GameTracker.\n\n{ex.Message}", "GameTracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>Windows logoff/shutdown: close open sessions before the process is killed.</summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        StopHost();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopHost();
        _host?.Dispose();
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    private async Task ExitAsync()
    {
        if (_isShuttingDown || _host is null)
            return;
        _isShuttingDown = true;

        _host.Services.GetRequiredService<WindowManager>().CloseMain();
        _host.Services.GetRequiredService<TrayIconService>().Dispose();
        await _host.StopAsync(ShutdownTimeout);
        Shutdown();
    }

    private void StopHost()
    {
        if (_host is null || _isShuttingDown)
            return;
        _isShuttingDown = true;

        // Hosted services run on the thread pool, so blocking here cannot deadlock the dispatcher.
        Task.Run(() => _host.StopAsync(ShutdownTimeout)).GetAwaiter().GetResult();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI exception");
        e.Handled = true; // keep tracking alive; the error is logged
    }

    private static IHost BuildHost()
    {
        var paths = new AppPaths();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);
        builder.Logging.AddProvider(new FileLoggerProvider(paths.LogsDirectory));
        builder.Logging.AddDebug();

        builder.Services.AddGameTrackerInfrastructure(paths);

        // Shell (application lifetime)
        builder.Services.AddSingleton<WindowManager>();
        builder.Services.AddSingleton<TrayIconService>();

        // UI (one scope per main window, disposed when the window closes)
        builder.Services.AddScoped<OverlayHost>();
        builder.Services.AddScoped<IDialogService, DialogService>();
        builder.Services.AddScoped<MainViewModel>();
        builder.Services.AddScoped<GamesViewModel>();
        builder.Services.AddScoped<TimeCapsuleViewModel>();
        builder.Services.AddTransient<GameDetailViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddScoped<Func<GameDetailViewModel>>(sp => sp.GetRequiredService<GameDetailViewModel>);
        builder.Services.AddScoped<Func<SettingsViewModel>>(sp => sp.GetRequiredService<SettingsViewModel>);

        return builder.Build();
    }
}
