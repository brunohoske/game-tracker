using System.Diagnostics.CodeAnalysis;
using GameTracker.Core.Common;
using GameTracker.Core.Domain;

namespace GameTracker.Core.Detection;

/// <summary>
/// Catches games no rule knows about (any folder, any source): a process whose window is in the foreground,
/// covers the whole monitor and has game runtime libraries loaded (see <see cref="IForegroundAppProbe.HasGameRuntime"/>)
/// is reported as a suggestion for the user to confirm.
/// Only the foreground process is inspected, and each process at most once per <see cref="RecheckInterval"/>,
/// so the cost stays negligible. Not thread-safe: owned by the tracking loop.
/// </summary>
public sealed class BehaviorGameDetector(IForegroundAppProbe probe, TimeProvider time)
{
    /// <summary>A process may load its renderer or input some seconds after going fullscreen: look again later.</summary>
    public static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<int, DateTimeOffset> _rejectedAt = [];
    private readonly string _windowsDirectory = PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

    /// <param name="snapshot">Current processes.</param>
    /// <param name="isKnown">Whether a process is already recognised by the regular rules.</param>
    public bool TryDetect(
        IReadOnlyList<RunningProcess> snapshot,
        Func<int, bool> isKnown,
        out int processId,
        [NotNullWhen(true)] out DetectedGame? game)
    {
        game = null;
        if (probe.GetFullscreenForegroundProcessId() is not { } foregroundId || isKnown(foregroundId))
        {
            processId = 0;
            return false;
        }

        processId = foregroundId;
        var process = snapshot.FirstOrDefault(p => p.ProcessId == foregroundId);
        if (process?.ExecutablePath is not { } path || IsNeverAGame(process.Name, path))
            return false;

        var now = time.GetUtcNow();
        if (_rejectedAt.TryGetValue(foregroundId, out var rejectedAt) && now - rejectedAt < RecheckInterval)
            return false;

        if (!probe.HasGameRuntime(foregroundId))
        {
            _rejectedAt[foregroundId] = now;
            return false;
        }

        _rejectedAt.Remove(foregroundId);
        game = new DetectedGame(
            GameKeys.ForExecutable(path),
            probe.GetDisplayName(path),
            GameSource.Detected,
            ExternalId: null,
            PathUtil.DirectoryOf(path),
            path);
        return true;
    }

    /// <summary>Drops bookkeeping of processes that exited.</summary>
    public void Prune(IReadOnlyList<RunningProcess> snapshot)
    {
        if (_rejectedAt.Count == 0)
            return;
        var alive = snapshot.Select(p => p.ProcessId).ToHashSet();
        foreach (var id in _rejectedAt.Keys.Where(id => !alive.Contains(id)).ToArray())
            _rejectedAt.Remove(id);
    }

    private bool IsNeverAGame(string processName, string path) =>
        DetectionDefaults.NonGameFullscreenApps.Contains(processName)
        || PathUtil.IsUnder(PathUtil.Normalize(path), _windowsDirectory);
}
