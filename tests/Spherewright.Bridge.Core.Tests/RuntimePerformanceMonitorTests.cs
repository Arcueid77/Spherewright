using Spherewright.Bridge.Core.Diagnostics;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class RuntimePerformanceMonitorTests
{
    [Fact]
    public void NoIntervals_AreUnknownRatherThanZero()
    {
        long now = 100;
        var monitor = new RuntimePerformanceMonitor(8, 2, 1.5, () => now, 1000);
        var initial = monitor.Capture(0);
        Assert.Equal("warming_up", initial.State);
        Assert.Null(initial.SampleAgeMs);
        Assert.Null(initial.UpdateInterval);
        Assert.Null(initial.BridgePump);
        monitor.RecordCompletedUpdate(100, 103, 2);
        now = 120;
        var first = monitor.Capture(3);
        Assert.Equal(1, first.CompletedUpdates);
        Assert.Equal(0, first.SampleCount);
        Assert.Equal(17d, first.SampleAgeMs);
        Assert.Equal(3, first.PendingMainThreadRequests);
        Assert.Equal(2, first.LastProcessedMainThreadRequests);
    }

    [Fact]
    public void NearestRankPercentile_UsesAllCompletedIntervalsAndKeepsLongStalls()
    {
        long now = 0;
        var monitor = new RuntimePerformanceMonitor(8, 2, 1.5, () => now, 1000);
        monitor.RecordCompletedUpdate(0, 0, 0);
        long started = 0;
        for (var i = 1; i <= 20; i++)
        {
            started += i * 1000;
            monitor.RecordCompletedUpdate(started, started + i, i % 3);
        }
        now = started + 25;
        var result = monitor.Capture(4);
        Assert.Equal(20, result.SampleCount);
        Assert.Equal(210000, result.WindowDurationMs);
        Assert.Equal(10500, result.UpdateInterval!.MeanMs);
        Assert.Equal(19000, result.UpdateInterval.P95Ms);
        Assert.Equal(20000, result.UpdateInterval.MaxMs);
        Assert.Equal(10.5, result.BridgePump!.MeanMs);
        Assert.Equal(19, result.BridgePump.P95Ms);
        Assert.Equal(20, result.BridgePump.MaxMs);
        Assert.Equal(5d, result.SampleAgeMs);
        Assert.Equal("warming_up", result.State);
    }

    [Fact]
    public void RollingWindow_EvictsOldSamplesAndReturnedObjectsAreIndependent()
    {
        long now = 100;
        var monitor = new RuntimePerformanceMonitor(8, 2, 1.5, () => now, 1000, 3);
        foreach (var start in new long[] { 0, 10, 30, 60, 100 }) monitor.RecordCompletedUpdate(start, start, 0);
        var first = monitor.Capture(0);
        Assert.Equal("ready", first.State);
        Assert.Equal(3, first.SampleCount);
        Assert.Equal(90, first.WindowDurationMs);
        Assert.Equal(30, first.UpdateInterval!.MeanMs);
        first.UpdateInterval.MeanMs = 999;
        now = 200;
        var second = monitor.Capture(1);
        Assert.Equal(30, second.UpdateInterval!.MeanMs);
        Assert.Equal(100d, second.SampleAgeMs);
        Assert.Equal(0, first.PendingMainThreadRequests);
        Assert.Equal(1, second.PendingMainThreadRequests);
    }

    [Fact]
    public void InvalidMeasurements_DoNotReplaceLastValidSample()
    {
        var monitor = new RuntimePerformanceMonitor(8, 2, 1.5, () => 10, 1000);
        monitor.RecordCompletedUpdate(1, 5, 1);
        Assert.Throws<ArgumentException>(() => monitor.RecordCompletedUpdate(4, 6, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.RecordCompletedUpdate(6, 5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.RecordCompletedUpdate(6, 7, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.Capture(9));
        Assert.Equal(1, monitor.Capture(0).CompletedUpdates);
    }

    [Fact]
    public async Task ConcurrentReader_ReceivesCoherentBoundedSnapshots()
    {
        var monitor = new RuntimePerformanceMonitor(8, 2, 1.5, () => 100000, 1000);
        var writer = Task.Run(() => { for (var i = 0; i < 10000; i++) monitor.RecordCompletedUpdate(i * 2, i * 2 + 1, 1); });
        for (var i = 0; i < 100; i++)
        {
            var sample = monitor.Capture(i % 8);
            Assert.InRange(sample.SampleCount, 0, 600);
            if (sample.SampleCount > 0)
            {
                Assert.Equal(sample.SampleCount * 2, sample.WindowDurationMs);
                Assert.Equal(2, sample.UpdateInterval!.MeanMs);
                Assert.Equal(1, sample.BridgePump!.MeanMs);
            }
        }
        await writer;
        Assert.Equal(600, monitor.Capture(0).SampleCount);
    }
}
