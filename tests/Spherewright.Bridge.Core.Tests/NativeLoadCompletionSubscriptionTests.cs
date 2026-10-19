using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Real production subscription/selection; synthetic events do not prove Unity.
public sealed class NativeLoadCompletionSubscriptionTests
{
    [Fact]
    public void SelectsTheOnlyNewFreshLoaderByReference()
    {
        var old = new Loader(); var next = new Loader();
        Assert.Same(next, NativeLoadCompletionSubscription<Loader>.SelectUniqueNew(new[] { old }, new[] { old, next }, l => l.Frame == 0));
    }

    [Theory]
    [InlineData("missing")] [InlineData("ambiguous")] [InlineData("already_running")] [InlineData("old_only")]
    public void MissingAmbiguousOrAlreadyRunningLoaderCannotBecomeACompletionProof(string fault)
    {
        var old = new Loader(); var next = new Loader { Frame = fault == "already_running" ? 1 : 0 };
        var after = fault switch { "missing" => Array.Empty<Loader>(), "ambiguous" => new[] { next, new Loader() },
            "old_only" => new[] { old }, _ => new[] { old, next } };
        Assert.Throws<InvalidOperationException>(() => NativeLoadCompletionSubscription<Loader>.SelectUniqueNew(new[] { old }, after, l => l.Frame == 0));
    }

    [Fact]
    public void ExactEventRunsSynchronouslyOnceAndUnsubscribesBeforeTheCallback()
    {
        var source = new Loader(); var foreign = new Loader(); var calls = 0;
        using var subscription = Subscribe(source, (loader, proved, error) =>
        {
            Assert.Same(source, loader); Assert.True(proved); Assert.Null(error);
            Assert.Null(source.Complete); calls++;
        });
        foreign.Fire(); Assert.Equal(0, calls);
        source.Frame = 10; source.Enabled = false;
        var queued = source.Complete!;
        source.Fire(); queued(); // Even a queued duplicate cannot admit twice.
        Assert.Equal(1, calls); Assert.True(subscription.CompletionObserved);
    }

    [Fact]
    public void CancelledSubscriptionCannotAcknowledgeADelayedOldLoader()
    {
        var source = new Loader(); var calls = 0;
        var subscription = Subscribe(source, (_, _, _) => calls++);
        var queued = source.Complete!; subscription.Dispose();
        source.Frame = 10; source.Enabled = false; queued();
        Assert.Equal(0, calls); Assert.False(subscription.CompletionObserved); Assert.Null(source.Complete);
    }

    [Theory]
    [InlineData(9, false)] [InlineData(10, true)]
    public void ARealEventWithoutTheExactNativeCompletedStateIsRejected(int frame, bool enabled)
    {
        var source = new Loader { Frame = frame, Enabled = enabled }; var calls = 0;
        using var subscription = Subscribe(source, (_, proved, _) => { Assert.False(proved); calls++; });
        source.Fire(); Assert.Equal(1, calls);
    }

    [Fact]
    public void PredicateCaptureFailureCarriesOnlyTheExceptionType()
    {
        var source = new Loader();
        using var subscription = new NativeLoadCompletionSubscription<Loader>(source,
            h => source.Complete += h, h => source.Complete -= h,
            _ => throw new InvalidOperationException("private synthetic detail must not escape"),
            (_, proved, error) => { Assert.False(proved); Assert.Equal(nameof(InvalidOperationException), error); });
        source.Fire();
    }

    private static NativeLoadCompletionSubscription<Loader> Subscribe(Loader source, Action<Loader, bool, string?> callback) =>
        new(source, h => source.Complete += h, h => source.Complete -= h,
            l => l.Frame == 10 && !l.Enabled, callback);

    private sealed class Loader
    {
        internal int Frame;
        internal bool Enabled = true;
        internal Action? Complete;
        internal void Fire() => Complete?.Invoke();
    }
}
