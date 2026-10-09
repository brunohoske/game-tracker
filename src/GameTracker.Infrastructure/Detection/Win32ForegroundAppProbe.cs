using System.Runtime.InteropServices;
using GameTracker.Core.Detection;
using Microsoft.Win32.SafeHandles;

namespace GameTracker.Infrastructure.Detection;

/// <summary>Win32 implementation of the behaviour checks used to suggest unknown games.</summary>
internal sealed unsafe partial class Win32ForegroundAppProbe : IForegroundAppProbe
{
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessVmRead = 0x0010;
    private const uint ListModulesAll = 0x03;
    private const uint MonitorDefaultToNearest = 0x02;

    /// <summary>3D APIs games render with (also loaded by ordinary apps, so never sufficient alone).</summary>
    private static readonly HashSet<string> GraphicsModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "d3d9.dll", "d3d11.dll", "d3d12.dll", "vulkan-1.dll", "opengl32.dll",
    };

    /// <summary>
    /// Game controller input and game runtimes. Measured on a regular desktop: no ordinary application
    /// loads these, while virtually every game loads at least one.
    /// </summary>
    private static readonly HashSet<string> GameModules = new(StringComparer.OrdinalIgnoreCase)
    {
        // Controller input
        "xinput1_1.dll", "xinput1_2.dll", "xinput1_3.dll", "xinput1_4.dll", "xinput9_1_0.dll",
        "dinput.dll", "dinput8.dll", "gameinput.dll",
        // Engines, store SDKs and middleware
        "unityplayer.dll", "steam_api.dll", "steam_api64.dll", "eossdk-win32-shipping.dll", "eossdk-win64-shipping.dll",
        "galaxy.dll", "galaxy64.dll", "fmod.dll", "fmod64.dll", "fmodstudio.dll", "fmodex.dll", "fmodex64.dll",
        "sdl2.dll", "sdl3.dll", "bink2w64.dll", "binkw32.dll", "binkw64.dll",
    };

    public int? GetFullscreenForegroundProcessId()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window))
            return null;

        if (!GetWindowRect(window, out var bounds))
            return null;

        var monitorInfo = new MonitorInfo { Size = (uint)sizeof(MonitorInfo) };
        if (!GetMonitorInfo(MonitorFromWindow(window, MonitorDefaultToNearest), ref monitorInfo))
            return null;

        var screen = monitorInfo.Monitor;
        var coversMonitor = bounds.Left <= screen.Left && bounds.Top <= screen.Top
                            && bounds.Right >= screen.Right && bounds.Bottom >= screen.Bottom;
        if (!coversMonitor)
            return null;

        _ = GetWindowThreadProcessId(window, out var processId);
        return processId == 0 ? null : (int)processId;
    }

    public bool HasGameRuntime(int processId)
    {
        // Processes we cannot read (elevated, anti-cheat protected) are not suggested: no evidence, no guess.
        using var process = OpenProcess(ProcessQueryInformation | ProcessVmRead, false, (uint)processId);
        if (process.IsInvalid)
            return false;

        var modules = new IntPtr[512];
        while (true)
        {
            var bufferSize = (uint)(modules.Length * IntPtr.Size);
            fixed (IntPtr* buffer = modules)
            {
                if (!EnumProcessModulesEx(process, buffer, bufferSize, out var needed, ListModulesAll))
                    return false;
                if (needed > bufferSize)
                {
                    modules = new IntPtr[needed / IntPtr.Size];
                    continue;
                }

                var count = (int)(needed / IntPtr.Size);
                var name = stackalloc char[260];
                bool hasGraphics = false, hasGameModule = false;
                for (var i = 0; i < count && !(hasGraphics && hasGameModule); i++)
                {
                    var length = GetModuleBaseName(process, modules[i], name, 260);
                    if (length == 0)
                        continue;
                    var module = new string(name, 0, (int)length);
                    hasGraphics |= GraphicsModules.Contains(module);
                    hasGameModule |= GameModules.Contains(module);
                }
                return hasGraphics && hasGameModule;
            }
        }
    }

    public string GetDisplayName(string executablePath) => ExecutableInfo.GuessGameName(executablePath);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr window, out Rect rect);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "K32EnumProcessModulesEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumProcessModulesEx(SafeProcessHandle process, IntPtr* modules, uint size, out uint needed, uint filterFlag);

    [LibraryImport("kernel32.dll", EntryPoint = "K32GetModuleBaseNameW", SetLastError = true)]
    private static partial uint GetModuleBaseName(SafeProcessHandle process, IntPtr module, char* baseName, uint size);
}
