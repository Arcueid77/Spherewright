using Newtonsoft.Json;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeConstructionCadenceTests
{
    [Fact]
    public void ReplaysCapturedEightUpsDrainWithoutRefundingTicksOrRenewingOldFailure()
    {
        var start=StructuredConstructionControlTests.Control().StartedAtUtc;
        // Sanitized real cut:402 paid tail ticks,51 elapsed ticks,6.827s wall,
        // fixedDelta=.125 and one returning drone. Old receipt stays old.
        var old=StructuredConstructionControlTests.Control();old.TailGameTicks=402;
        old.RequestStop("construction_complete_draining",200,start.AddSeconds(2),false);
        Assert.True(old.StopOutcomeUnknown(251,start.AddSeconds(8.827)));
        var original=JsonConvert.SerializeObject(old);
        var next=StructuredConstructionControlTests.Control();next.TailGameTicks=402;next.NativeFixedDeltaTimeSeconds=.125;
        next.RequestStop("construction_complete_draining",200,start.AddSeconds(2),false);
        Assert.False(next.StopOutcomeUnknown(251,start.AddSeconds(8.827)));
        Assert.True(next.StopOutcomeUnknown(602,start.AddSeconds(9)));
        Assert.True(next.StopOutcomeUnknown(251,start.AddSeconds(54)));
        Assert.Equal(402,next.TailGameTicks);Assert.Equal(original,JsonConvert.SerializeObject(old));
    }

    [Fact]
    public void PollingDuringDrainRenewsOnlyObservationWithinTheOriginalLatchedDeadline()
    {
        var c=StructuredConstructionControlTests.Control();c.TailGameTicks=402;c.NativeFixedDeltaTimeSeconds=.125;
        c.RequestStop("construction_complete_draining",200,c.StartedAtUtc.AddSeconds(2),false);
        var started=c.DrainStartedAtUtc;
        Assert.True(c.RenewObservationLease(c.StartedAtUtc.AddSeconds(35)));
        Assert.Equal(started!.Value.AddSeconds(52),c.ObservationLeaseExpiresAtUtc);
        Assert.False(c.RenewObservationLease(started.Value.AddSeconds(52)));
        Assert.Equal(started,c.DrainStartedAtUtc);
        Assert.False(c.PhysicalIdleVerified);
    }

    [Theory]
    [InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(0)][InlineData(-.1)][InlineData(.251)]
    public void UnknownOrUnboundedCadenceCannotCreateANewClock(double delta)
        =>Assert.Null(NativeConstructionCadencePolicy.WallBudget(402,delta));
}
