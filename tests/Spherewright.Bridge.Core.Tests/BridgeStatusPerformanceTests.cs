using Spherewright.Bridge.Core.Abstractions;
using Spherewright.Bridge.Core.Diagnostics;
using Spherewright.Plugin.Hosting;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class BridgeStatusPerformanceTests
{
    [Fact]
    public async Task Capture_ReadsCurrentQueueWithoutEnqueueingOrExecutingWork()
    {
        long now = 30;
        using var dispatcher = new BoundedMainThreadDispatcher(8);
        var monitor = new RuntimePerformanceMonitor(8, 2, 1.5, () => now, 1000);
        var provider = new BridgeStatusSnapshotProvider("bridge", "0.4.0", "native", false, monitor, dispatcher);
        var executed = false;
        dispatcher.TryEnqueue(() => executed = true, out var completion);
        monitor.RecordCompletedUpdate(0, 1, 0);
        monitor.RecordCompletedUpdate(20, 22, 0);
        var first = provider.Capture();
        Assert.Equal(1, first.RuntimePerformance!.PendingMainThreadRequests);
        Assert.Equal(20, first.RuntimePerformance.UpdateInterval!.MeanMs);
        Assert.False(executed);
        Assert.False(completion.IsCompleted);
        Assert.Equal(1, dispatcher.Pump(2, TimeSpan.FromSeconds(1)));
        Assert.True(await completion);
        now = 50;
        provider.UpdateGameLoadedOnMainThread(true);
        var second = provider.Capture();
        Assert.True(second.GameLoaded);
        Assert.Equal(0, second.RuntimePerformance!.PendingMainThreadRequests);
        Assert.Equal(28d, second.RuntimePerformance.SampleAgeMs);
        Assert.Equal(1, first.RuntimePerformance.PendingMainThreadRequests);
    }
}
