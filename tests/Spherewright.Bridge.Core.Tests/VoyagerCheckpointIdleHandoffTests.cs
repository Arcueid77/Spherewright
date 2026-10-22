using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerCheckpointIdleHandoffTests
{
    [Fact]
    public void Missing501ExpiresWithoutInventingAnObservationAndCancellationCannotReplaceIt()
    {
        var now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var handoff = new VoyagerCheckpointIdleHandoff("synthetic-reload", 100, now);
        handoff.CheckObservationDeadline(null, now.AddSeconds(NativeResumeContactPolicy.MaximumWallSeconds + .01));
        var first = handoff.FirstFailure!;
        Assert.False(handoff.Admitted);
        Assert.Equal("not_observed", first.FactsState);
        Assert.Equal("native501_observed", first.FirstFailedPredicate);
        Assert.Null(first.CapturedAtGameTick);
        Assert.Empty(first.Observations);
        handoff.Reject(NativeResumeDecisionEvidence.UnobservedFailure("cancelled", "host_stop", 101, now));
        first.Code = "external_mutation";
        Assert.Equal("return_only_checkpoint_native501_not_observed", handoff.FirstFailure!.Code);
    }
}
