namespace GameTracker.App.Services;

/// <summary>
/// Ensures a single running instance. A second launch signals the first one (which then shows its window) and exits.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\GameTracker.SingleInstance";
    private const string ActivationEventName = @"Local\GameTracker.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private readonly RegisteredWaitHandle _registration;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activation)
    {
        _mutex = mutex;
        _activation = activation;
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activation, (_, _) => ActivationRequested?.Invoke(this, EventArgs.Empty), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Raised on a thread-pool thread when another instance asks this one to show up.</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>Returns the guard when this is the first instance; otherwise signals the running one and returns null.</summary>
    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            if (EventWaitHandle.TryOpenExisting(ActivationEventName, out var existing))
            {
                using (existing)
                    existing.Set();
            }
            return null;
        }

        return new SingleInstanceGuard(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName));
    }

    public void Dispose()
    {
        _registration.Unregister(null);
        _activation.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
