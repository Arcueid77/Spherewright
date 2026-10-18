using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerControlDeadlinePolicyTests
{
    private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NormalControlledFlightKeepsItsOriginalDeadlinesAndDoesNotInferCompletion()
    {
        var facts = Facts(); var before = JsonSerializer.Serialize(facts);
        for (var i = 0; i < 8; i++)
        {
            var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
            Assert.False(result.ShouldRequestStop); Assert.False(result.ShouldExitWarp);
            Assert.False(result.StopOutcomeUnknown); Assert.Null(result.StopReasonCode);
        }
        Assert.Equal(before, JsonSerializer.Serialize(facts));
        Assert.Equal(17, VoyagerControlDeadlinePolicy.WarpExitLeadGameTicks);
    }

    [Theory]
    [InlineData(nameof(VoyagerControlDeadlineFacts.CurrentGameTick))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.LastControlledTick))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.FlightDeadlineTick))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.WarpGameTicks))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.MaximumWarpGameTicks))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.NowUtc))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.JourneyDeadlineUtc))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.ControlLeaseExpiresAtUtc))]
    public void MissingClockRequestsStopAndCannotTurnIntoUnlimitedFlight(string field)
    {
        var facts = Facts(); typeof(VoyagerControlDeadlineFacts).GetProperty(field)!.SetValue(facts, null);
        var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
        Assert.True(result.ShouldRequestStop); Assert.True(result.ShouldExitWarp);
        Assert.False(result.StopOutcomeUnknown); // Stop has not yet been latched by the owner.
        Assert.NotNull(result.StopReasonCode);
    }

    [Theory]
    [InlineData(nameof(VoyagerControlDeadlineFacts.CurrentGameTick))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.LastControlledTick))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.FlightDeadlineTick))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.WarpGameTicks))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.MaximumWarpGameTicks))]
    public void NegativeCopiedTicksStopWithoutArithmeticWraparound(string field)
    {
        var facts = Facts(); typeof(VoyagerControlDeadlineFacts).GetProperty(field)!.SetValue(facts, -1L);
        var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
        Assert.True(result.ShouldRequestStop); Assert.True(result.ShouldExitWarp);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void ActualControlCadenceAllowsPauseAndTwoTicksButNotAnUncontrolledGap(long gap, bool mustStop)
    {
        var facts = Facts(); facts.LastControlledTick = facts.CurrentGameTick - gap;
        Assert.Equal(mustStop, VoyagerControlDeadlinePolicy.Evaluate(facts).ShouldRequestStop);
    }

    [Fact]
    public void GameTickRewindStopsEvenWithFreshCallerLeaseAndFutureFlightDeadline()
    {
        var facts = Facts(); facts.CurrentGameTick = 498; facts.LastControlledTick = 499;
        var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
        Assert.True(result.ShouldRequestStop); Assert.True(result.ShouldExitWarp);
        Assert.Equal("voyager_game_tick_reversed", result.StopReasonCode);
    }

    [Theory]
    [InlineData(499, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void FlightDeadlineIsReachedAtItsExactGameTick(long deadline, bool mustStop)
    {
        var facts = Facts(); facts.FlightDeadlineTick = deadline;
        Assert.Equal(mustStop, VoyagerControlDeadlinePolicy.Evaluate(facts).ShouldRequestStop);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    public void SameTickPauseDoesNotStopUtcCallerLeaseFromExpiring(int elapsedSeconds, bool mustStop)
    {
        var facts = Facts(); facts.LastControlledTick = facts.CurrentGameTick;
        facts.NowUtc = Epoch.AddSeconds(elapsedSeconds);
        var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
        Assert.Equal(mustStop, result.ShouldRequestStop); Assert.Equal(mustStop, result.ShouldExitWarp);
    }

    [Fact]
    public void FreshReturnLegAndCallerLeaseCannotExtendTheAbsoluteJourneyDeadline()
    {
        var facts = Facts(); facts.JourneyDeadlineUtc = Epoch.AddMinutes(5);
        facts.NowUtc = Epoch.AddMinutes(5); facts.ControlLeaseExpiresAtUtc = facts.NowUtc.Value.AddSeconds(30);
        facts.FlightDeadlineTick = facts.CurrentGameTick + 36000;
        var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
        Assert.True(result.ShouldRequestStop); Assert.Equal("voyager_journey_deadline_reached", result.StopReasonCode);
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(31, true)]
    public void CopiedLeaseCannotClaimArbitrarilyMoreThanThirtySeconds(int seconds, bool mustStop)
    {
        var facts = Facts(); facts.ControlLeaseExpiresAtUtc = Epoch.AddSeconds(seconds);
        Assert.Equal(mustStop, VoyagerControlDeadlinePolicy.Evaluate(facts).ShouldRequestStop);
    }

    [Theory]
    [InlineData(18, false)]
    [InlineData(17, true)]
    [InlineData(16, true)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    public void WarpExitStartsWithNativeDecayAndControlLatencyStillInTheBudget(long remaining, bool exit)
    {
        var facts = Facts(); facts.WarpGameTicks = facts.MaximumWarpGameTicks - remaining;
        var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
        Assert.Equal(exit, result.ShouldExitWarp);
        Assert.False(result.ShouldRequestStop); // Exit may be followed by ordinary approach within the flight window.
    }

    [Fact]
    public void MaximumLongCountersDoNotOverflowTheRemainingWarpOrControlGap()
    {
        var facts = Facts(); facts.CurrentGameTick = long.MaxValue - 1;
        facts.LastControlledTick = long.MaxValue - 2; facts.FlightDeadlineTick = long.MaxValue;
        facts.MaximumWarpGameTicks = long.MaxValue; facts.WarpGameTicks = long.MaxValue - 18;
        Assert.False(VoyagerControlDeadlinePolicy.Evaluate(facts).ShouldExitWarp);
        facts.WarpGameTicks = long.MaxValue - 17;
        Assert.True(VoyagerControlDeadlinePolicy.Evaluate(facts).ShouldExitWarp);
    }

    [Theory]
    [InlineData(nameof(VoyagerControlDeadlineFacts.StopDeadlineGameTick))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.StopDeadlineUtc))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.CurrentGameTick))]
    [InlineData(nameof(VoyagerControlDeadlineFacts.NowUtc))]
    public void MissingCleanupClockCannotBeTreatedAsAProvedOpenStopWindow(string field)
    {
        var facts = Stopping(); typeof(VoyagerControlDeadlineFacts).GetProperty(field)!.SetValue(facts, null);
        var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
        Assert.True(result.ShouldRequestStop); Assert.True(result.ShouldExitWarp); Assert.True(result.StopOutcomeUnknown);
    }

    [Theory]
    [InlineData(1799, 29, false)]
    [InlineData(1800, 29, true)]
    [InlineData(1799, 30, true)]
    [InlineData(1800, 30, true)]
    public void FirstCleanupDeadlineWinsWithoutWaitingForTheOtherClock(int gameTicks, int seconds, bool unknown)
    {
        var facts = Stopping(); facts.CurrentGameTick += gameTicks; facts.LastControlledTick = facts.CurrentGameTick;
        facts.NowUtc = Epoch.AddSeconds(seconds); facts.ControlLeaseExpiresAtUtc = facts.NowUtc.Value.AddSeconds(30);
        Assert.Equal(unknown, VoyagerControlDeadlinePolicy.Evaluate(facts).StopOutcomeUnknown);
    }

    [Fact]
    public void RepeatedReadLeaseRenewalsCannotRenewLatchedCleanupDeadlines()
    {
        var facts = Stopping(); var tickDeadline = facts.StopDeadlineGameTick; var utcDeadline = facts.StopDeadlineUtc;
        for (var seconds = 0; seconds <= 30; seconds++)
        {
            facts.NowUtc = Epoch.AddSeconds(seconds); facts.ControlLeaseExpiresAtUtc = facts.NowUtc.Value.AddSeconds(30);
            var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
            Assert.True(result.ShouldRequestStop); Assert.Equal(seconds == 30, result.StopOutcomeUnknown);
            Assert.Equal(tickDeadline, facts.StopDeadlineGameTick); Assert.Equal(utcDeadline, facts.StopDeadlineUtc);
        }
    }

    [Theory]
    [InlineData(1801, 30)]
    [InlineData(1800, 31)]
    public void SuppliedCleanupBoundsCannotBeUnboundedOrSilentlyEnlarged(int ticks, int seconds)
    {
        var facts = Stopping(); facts.StopDeadlineGameTick = facts.CurrentGameTick + ticks;
        facts.StopDeadlineUtc = Epoch.AddSeconds(seconds);
        Assert.True(VoyagerControlDeadlinePolicy.Evaluate(facts).StopOutcomeUnknown);
    }

    [Fact]
    public void ZeroEnergyWithNonzeroNativeSpeedIsNotProvedStopped()
    {
        // The owner copied false from native exit/velocity proof despite empty energy.
        // The clock policy has no energy-to-stopped shortcut and never supplies that proof.
        var facts = Stopping(); facts.NativeStopSettled = false;
        facts.WarpGameTicks = facts.MaximumWarpGameTicks;
        Assert.False(VoyagerControlDeadlinePolicy.Evaluate(facts).StopOutcomeUnknown);
        facts.NowUtc = Epoch.AddSeconds(30); facts.ControlLeaseExpiresAtUtc = facts.NowUtc.Value.AddSeconds(30);
        Assert.True(VoyagerControlDeadlinePolicy.Evaluate(facts).StopOutcomeUnknown);
        facts.NativeStopSettled = true;
        Assert.False(VoyagerControlDeadlinePolicy.Evaluate(facts).StopOutcomeUnknown);
        Assert.True(facts.StopRequested);
    }

    [Fact]
    public void ActualSettledProofReturnsCleanupWithoutInventingAnUnknownOutcome()
    {
        var facts = Stopping(); facts.NowUtc = Epoch.AddSeconds(30); facts.NativeStopSettled = true;
        facts.StopDeadlineGameTick = null; // Stop is already natively proved; missing budget is not missing outcome proof.
        var result = VoyagerControlDeadlinePolicy.Evaluate(facts);
        Assert.True(result.ShouldRequestStop); Assert.True(result.ShouldExitWarp); Assert.False(result.StopOutcomeUnknown);
    }

    [Fact]
    public void DefaultClockValuesAndZeroWarpCapacityFailClosed()
    {
        var facts = Facts(); facts.NowUtc = DateTimeOffset.MinValue;
        Assert.True(VoyagerControlDeadlinePolicy.Evaluate(facts).ShouldRequestStop);
        facts = Facts(); facts.MaximumWarpGameTicks = 0;
        Assert.True(VoyagerControlDeadlinePolicy.Evaluate(facts).ShouldRequestStop);
        var missing = VoyagerControlDeadlinePolicy.Evaluate(new VoyagerControlDeadlineFacts { StopRequested = true });
        Assert.True(missing.ShouldExitWarp); Assert.True(missing.StopOutcomeUnknown);
    }

    private static VoyagerControlDeadlineFacts Facts() => new VoyagerControlDeadlineFacts
    {
        CurrentGameTick = 500, LastControlledTick = 499, FlightDeadlineTick = 36500,
        NowUtc = Epoch, JourneyDeadlineUtc = Epoch.AddHours(1), ControlLeaseExpiresAtUtc = Epoch.AddSeconds(30),
        WarpGameTicks = 100, MaximumWarpGameTicks = 1800,
    };

    private static VoyagerControlDeadlineFacts Stopping()
    {
        var facts = Facts(); facts.StopRequested = true;
        facts.StopDeadlineGameTick = facts.CurrentGameTick + 1800; facts.StopDeadlineUtc = Epoch.AddSeconds(30);
        return facts;
    }
}
