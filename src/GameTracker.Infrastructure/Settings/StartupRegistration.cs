using Microsoft.Win32;

namespace GameTracker.Infrastructure.Settings;

/// <summary>Registers the app in HKCU\...\Run so it starts (minimized to the tray) with Windows.</summary>
public sealed class StartupRegistration
{
    public const string MinimizedArgument = "--minimized";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GameTracker";

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled && Environment.ProcessPath is { } executable)
            key.SetValue(ValueName, $"\"{executable}\" {MinimizedArgument}");
        else if (key.GetValue(ValueName) is not null)
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
