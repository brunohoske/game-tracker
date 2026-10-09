using System.Windows;
using GameTracker.App.ViewModels;
using GameTracker.App.Views;
using H.NotifyIcon.EfficiencyMode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameTracker.App.Services;

/// <summary>
/// Creates the main window on demand and fully releases it (and its view models) when closed, so the
/// app idles in the tray with a minimal footprint and Windows "efficiency mode" enabled.
/// </summary>
public sealed class WindowManager(IServiceScopeFactory scopeFactory, ILogger<WindowManager> logger)
{
    private MainWindow? _window;
    private IServiceScope? _scope;

    public void ShowMain()
    {
        if (_window is not null)
        {
            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Normal;
            _window.Activate();
            return;
        }

        SetEfficiencyMode(false);
        _scope = scopeFactory.CreateScope();
        _window = new MainWindow { DataContext = _scope.ServiceProvider.GetRequiredService<MainViewModel>() };
        _window.Closed += OnWindowClosed;
        _window.Show();
        _window.Activate();
    }

    public void CloseMain() => _window?.Close();

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _window!.Closed -= OnWindowClosed;
        _window = null;
        _scope?.Dispose();
        _scope = null;
        SetEfficiencyMode(true);

        // Still inside the window's own Closed event here: collect once the dispatcher is idle.
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ReleaseUiMemory();
            logger.LogInformation("Window closed; managed heap {HeapMb} MB, GC committed {CommittedMb} MB",
                GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024),
                GC.GetGCMemoryInfo().TotalCommittedBytes / (1024 * 1024));
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// The window, its visuals and decoded images are garbage now: collect them right away instead of
    /// holding that memory while the app idles in the tray for hours.
    /// </summary>
    private static void ReleaseUiMemory()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
    }

    /// <summary>EcoQoS + low priority while only the tray icon is alive (Windows 10 1709+).</summary>
    public static void SetEfficiencyMode(bool enabled)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
            EfficiencyModeUtilities.SetEfficiencyMode(enabled);
    }
}
