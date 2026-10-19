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
