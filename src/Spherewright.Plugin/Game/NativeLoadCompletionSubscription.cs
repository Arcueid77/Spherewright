namespace Spherewright.Plugin.Game;

// One exact Native loader's event, not a later Update/poll approximation.
// Delegates keep this lifecycle testable without executing Unity or a load.
internal sealed class NativeLoadCompletionSubscription<T> : IDisposable where T : class
{
    private readonly Action<Action> _unsubscribe;
    private readonly Action _handler;
    private bool _disposed;
    public bool CompletionObserved { get; private set; }

    public NativeLoadCompletionSubscription(T source, Action<Action> subscribe, Action<Action> unsubscribe,
        Func<T, bool> completed, Action<T, bool, string?> onCompletion)
    {
        _unsubscribe = unsubscribe;
        _handler = () =>
        {
            if (_disposed || CompletionObserved) return;
            CompletionObserved = true;
            var proved = false;
            string? captureType = null;
            try { Dispose(); } // No duplicate/reentrant event can publish a second proof.
            catch (Exception exception) { captureType = "unsubscribe:" + exception.GetType().Name; }
            try { proved = completed(source); }
            catch (Exception exception) { captureType = exception.GetType().Name; }
            onCompletion(source, proved && captureType is null, captureType);
        };
        subscribe(_handler);
    }

    public static T SelectUniqueNew(IReadOnlyList<T> before, IReadOnlyList<T> after, Func<T, bool> fresh)
    {
        T? selected = null;
        foreach (var candidate in after)
        {
            if (before.Any(old => ReferenceEquals(old, candidate))) continue;
            if (selected is not null || !fresh(candidate))
                throw new InvalidOperationException("remote_resume_exact_new_loader_unproved");
            selected = candidate;
        }
        return selected ?? throw new InvalidOperationException("remote_resume_exact_new_loader_missing");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _unsubscribe(_handler);
    }
}

// A single call-boundary latch, not an admission controller. The caller must
// re-run its complete strict gate even when synchronization returns normally.
internal sealed class NativeLoadedTransformSyncAttempt
{
    public bool Attempted { get; private set; }
    public string? TrySynchronize(string? blocker, bool exactLoadedSourceBoundary,
        Func<bool> beforeUnchanged, Action synchronize, Func<bool> afterUnchanged)
    {
        if (blocker != "remote_idle_ground_contact_unproved" || !exactLoadedSourceBoundary)
            return blocker ?? "remote_resume_ground_sync_boundary_unproved";
        if (Attempted) return "remote_resume_ground_sync_already_attempted";
        try
        {
            if (!beforeUnchanged()) return "remote_resume_ground_sync_boundary_changed";
            Attempted = true; // Even a partial/throwing Native call is never repeated.
            synchronize();
            return afterUnchanged() ? null : "remote_resume_ground_sync_state_changed";
        }
        catch (Exception exception) { return "remote_resume_ground_sync_failed:" + exception.GetType().Name; }
    }
}
