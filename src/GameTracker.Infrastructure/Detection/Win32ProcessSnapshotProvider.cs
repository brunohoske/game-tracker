using System.Diagnostics;
using GameTracker.Core.Detection;

namespace GameTracker.Infrastructure.Detection;

/// <summary>
/// Lists running processes. Executable paths are resolved once per process and cached, so a steady-state
/// poll costs a single process enumeration.
/// </summary>
internal sealed class Win32ProcessSnapshotProvider : IProcessSnapshotProvider
{
    private readonly Dictionary<int, RunningProcess> _cache = [];
    private readonly HashSet<int> _alive = [];
    private readonly int _currentProcessId = Environment.ProcessId;

    public IReadOnlyList<RunningProcess> GetRunningProcesses()
    {
        var processes = Process.GetProcesses();
        var result = new List<RunningProcess>(processes.Length);
        _alive.Clear();

        foreach (var process in processes)
        {
            using (process)
            {
                var id = process.Id;
                if (id <= 4 || id == _currentProcessId)
                    continue; // Idle, System and ourselves

                string name;
                try
                {
                    name = process.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    continue; // exited meanwhile
                }

                // The name check guards against a process id being reused by another program.
                if (!_cache.TryGetValue(id, out var cached) || !string.Equals(cached.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    cached = new RunningProcess(id, name, NativeMethods.TryGetExecutablePath(id));
                    _cache[id] = cached;
                }

                _alive.Add(id);
                result.Add(cached);
            }
        }

        if (_cache.Count > _alive.Count)
        {
            foreach (var id in _cache.Keys.Where(id => !_alive.Contains(id)).ToArray())
                _cache.Remove(id);
        }

        return result;
    }
}
