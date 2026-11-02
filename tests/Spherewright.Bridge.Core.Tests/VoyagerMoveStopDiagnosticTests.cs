using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerMoveStopDiagnosticTests
{
    private static VoyagerMoveStopDiagnosticFacts Cut() => new()
    {
        GameTick = 112, ArmGameTick = 100, DeadlineGameTick = 113,
        NowUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        GroundSphereHit = true, ActualWalk = true, FrameWalk = true, CurrentOrderAbsent = true,
        CoreEnergy = 100_000_000,
    };
    [Theory]
    [InlineData("ground", "strict_ground_sphere")][InlineData("frame", "strict_frame_walk")]
    [InlineData("speed", "strict_speed_at_rest")][InlineData("rts", "strict_walk_rts_zero")]
    public void ReportsTheActualOriginalFailingStopPredicateWithoutPostPauseRecapture(string fault, string predicate)
    {
        var f = Cut();
        switch (fault)
        {
            case "ground": f.GroundSphereHit = false; break;
            case "frame": f.FrameWalk = false; break;
            case "speed": f.Speed = 1; break;
            case "rts": f.WalkRtsSquared = .01; break;
        }
        var result = VoyagerMoveStopDiagnostic.Capture(f);
        Assert.Equal("voyager_local_native_tail_unproved", result.Code);
        Assert.Equal(predicate, result.FirstFailedPredicate);
        Assert.Equal(112, result.CapturedAtGameTick);
        Assert.False(result.SafetyCertified);
        Assert.Equal("not_reached", result.Predicates["post_player_settlement"]);
        Assert.Equal("not_reached", result.Predicates["resume"]);
        Assert.Equal(100, f.ArmGameTick); Assert.Equal(113, f.DeadlineGameTick);
    }
    [Fact]
    public void MissingGroundQueryRemainsUnknownRatherThanFalseOrSuccessfulSettlement()
    {
        var f = Cut(); f.GroundSphereHit = null;
        var r = VoyagerMoveStopDiagnostic.Capture(f);
        Assert.Equal("unknown", r.Predicates["strict_ground_sphere"]);
        Assert.False(r.SafetyCertified);
    }
}
