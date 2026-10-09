using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GameTracker.Infrastructure.Detection;

internal static unsafe partial class NativeMethods
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int MaxPath = 1024;

    /// <summary>
    /// Full executable path of a process. Uses the "limited information" right, which also works for
    /// most elevated processes; returns null for protected ones.
    /// </summary>
    public static string? TryGetExecutablePath(int processId)
    {
        using var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle.IsInvalid)
            return null;

        var buffer = stackalloc char[MaxPath];
        var size = MaxPath;
        return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, char* exeName, ref int size);
}
