using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class PausedOwnedSaveGuardTests
{
    [Theory]
    [InlineData("unknown_pause")] [InlineData("running")] [InlineData("world")]
    [InlineData("identity")] [InlineData("version")] [InlineData("owner")]
    [InlineData("reason")] [InlineData("tick")] [InlineData("revision")]
    [InlineData("out_of_order")]
    public void AChangedOrUnknownObservationInvalidatesThePreparedBinding(string fault)
    {
        var guard = new PausedOwnedSaveGuard(); var o = Observation();
        guard.Observe(o); o.RenderFrame++; guard.Observe(o);
        Assert.True(guard.TryBind(o, out var binding, out _));
        var stableTick = o.GameTick;
        switch (fault)
        {
            case "unknown_pause": o.NativePaused = null; break;
            case "running": o.NativePaused = false; break;
            case "world": o.World = new(); break;
            case "identity": o.OwnedSaveName = "different-synthetic-owned-world"; break;
            case "version": o.NativeVersion = "0.10.35.00000"; break;
            case "owner": o.QuarantineActionId = "different-synthetic-action"; break;
            case "reason": o.QuarantineReason = "different-original-blocker"; break;
            case "tick": o.GameTick--; break;
            case "revision": o.Revision++; break;
            case "out_of_order": o.RenderFrame -= 2; break;
        }
        Assert.False(guard.Validate(binding!, o, out var rejection)); Assert.NotEmpty(rejection);
        var first = binding!.FirstFailure!;
        Assert.Equal(o.GameTick, first.CapturedAtGameTick);
        Assert.Equal(o.NativePaused.HasValue ? o.NativePaused.Value ? "true" : "false" : "unknown", first.Observations["native_pause"]);
        Assert.Equal("not_reached", first.Predicates["native_save"]);
        Assert.Null(first.Values["original_journey_remaining_ticks"]);
        Assert.False(first.SafetyCertified);
        o.NativePaused = true; o.GameTick = stableTick; o.RenderFrame += 3;
        guard.Observe(o); o.RenderFrame++; guard.Observe(o);
        Assert.False(guard.Validate(binding!, o, out _)); // Cannot revive an old plan after a new stable cut.
        Assert.Equal(first.CapturedAtGameTick, binding.FirstFailure!.CapturedAtGameTick);
        first.Values.Clear(); Assert.NotEmpty(binding.FirstFailure.Values);
    }

    [Fact]
    public void DuplicateRenderSamplesAreNotATailProofAndMutableInputsCannotAlterTheCapturedCut()
    {
        var guard = new PausedOwnedSaveGuard(); var o = Observation();
        guard.Observe(o); guard.Observe(o);
        Assert.False(guard.TryBind(o, out _, out _));
        o.GameTick++; o.RenderFrame++; guard.Observe(o);
        Assert.False(guard.TryBind(o, out _, out _));
        o.RenderFrame++; guard.Observe(o); Assert.True(guard.TryBind(o, out var binding, out _));
        var captured = binding!.GameTick; o.GameTick++;
        Assert.Equal(captured, binding.GameTick); Assert.False(guard.Validate(binding, o, out _));
    }

    [Fact]
    public void MatchingQuarantineCodeDoesNotRemoveAnotherBlockerOrAnAmbiguousDuplicate()
    {
        var guard = new PausedOwnedSaveGuard(); var o = Observation(); guard.Observe(o);
        o.RenderFrame++; guard.Observe(o); Assert.True(guard.TryBind(o, out var binding, out _));
        var exact = new WriteBlocker { Code = BridgeErrorCodes.WriteSubsystemQuarantined, Message = o.QuarantineReason! };
        var other = new WriteBlocker { Code = exact.Code, Message = "different-journey-load-error" };
        Assert.Empty(binding!.RemainingBlockers(new[] { exact }));
        Assert.Same(other, Assert.Single(binding.RemainingBlockers(new[] { exact, other })));
        Assert.Equal(2, binding.RemainingBlockers(new[] { exact, exact }).Count);
    }

    private static PausedOwnedSaveObservation Observation() => new()
    {
        World = new object(), SessionId = "synthetic-session", OwnedSaveName = "synthetic-owned-world",
        NativeVersion = "0.10.35.29151", PlanetId = 201, Revision = 9, GameTick = 12521, RenderFrame = 10,
        NativePaused = true, QuarantineActionId = "synthetic-completed-resume",
        QuarantineReason = "remote_idle_return_wall_reserve_insufficient",
    };
}
