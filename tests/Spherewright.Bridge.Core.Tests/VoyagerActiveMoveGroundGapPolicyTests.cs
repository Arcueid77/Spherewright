using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerActiveMoveGroundGapPolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OnlyEightActualMissingContactWorkTicksPrecedeTheOriginalStop()
    {
        var state = new VoyagerActiveMoveGroundGapPolicy(100);
        var facts = Good();
        for (var tick = 101L; tick <= 108; tick++)
        {
            facts.OriginalJourney!.CurrentGameTick = tick;
            Assert.Equal(VoyagerMoveGroundGapStep.ContinueGap, state.Observe(facts));
        }
        facts.OriginalJourney!.CurrentGameTick = 109;
        Assert.Equal(VoyagerMoveGroundGapStep.Stop, state.Observe(facts));
        Assert.Equal(101L, state.GapStartedGameTick);
        facts.GroundSphereHit = true;
        facts.OriginalJourney.CurrentGameTick = 110;
        Assert.Equal(VoyagerMoveGroundGapStep.Stop, state.Observe(facts));
        Assert.True(state.StopLatched); // A later physical recovery cannot revive the failed task.
    }

    [Fact]
    public void OnlyAFreshConsecutivePositiveSphereClearsTheGap()
    {
        var state = new VoyagerActiveMoveGroundGapPolicy(100);
        var facts = Good();
        Assert.Equal(VoyagerMoveGroundGapStep.ContinueGap, state.Observe(facts));
        facts.GroundSphereHit = true;
        Assert.Equal(VoyagerMoveGroundGapStep.Duplicate, state.Observe(facts));
        Assert.Equal(101L, state.GapStartedGameTick);
        facts.OriginalJourney!.CurrentGameTick = 102;
        Assert.Equal(VoyagerMoveGroundGapStep.Grounded, state.Observe(facts));
        Assert.Null(state.GapStartedGameTick);
        facts.GroundSphereHit = false;
        facts.OriginalJourney.CurrentGameTick = 103;
        Assert.Equal(VoyagerMoveGroundGapStep.ContinueGap, state.Observe(facts));
        Assert.Equal(103L, state.GapStartedGameTick);
    }

    [Fact]
    public void DuplicateQueriesCannotCreateOrResetActualNativeWorkTicks()
    {
        var state = new VoyagerActiveMoveGroundGapPolicy(100);
        var facts = Good();
        for (var tick = 101L; tick <= 108; tick++)
        {
            facts.OriginalJourney!.CurrentGameTick = tick;
            Assert.Equal(VoyagerMoveGroundGapStep.ContinueGap, state.Observe(facts));
            Assert.Equal(VoyagerMoveGroundGapStep.Duplicate, state.Observe(facts));
            Assert.Equal(101L, state.GapStartedGameTick);
        }
        facts.OriginalJourney!.CurrentGameTick = 109;
        Assert.Equal(VoyagerMoveGroundGapStep.Stop, state.Observe(facts));
    }

    [Theory]
    [InlineData(100L)]
    [InlineData(103L)]
    public void ReversedOrSkippedGapObservationIsStickyUnknown(long nextTick)
    {
        var state = new VoyagerActiveMoveGroundGapPolicy(100);
        var facts = Good();
        Assert.Equal(VoyagerMoveGroundGapStep.ContinueGap, state.Observe(facts));
        facts.OriginalJourney!.CurrentGameTick = nextTick;
        Assert.Equal(VoyagerMoveGroundGapStep.Rejected, state.Observe(facts));
        facts.OriginalJourney.CurrentGameTick = 102;
        facts.GroundSphereHit = true;
        Assert.Equal(VoyagerMoveGroundGapStep.Rejected, state.Observe(facts));
    }

    [Fact]
    public void AnUnobservedFirstMissingTickCannotBorrowTheLegacyTwoTickGap()
    {
        var facts = Good(); facts.OriginalJourney!.CurrentGameTick = 102;
        Assert.Equal(VoyagerMoveGroundGapStep.Rejected, new VoyagerActiveMoveGroundGapPolicy(100).Observe(facts));
        facts.GroundSphereHit = true;
        Assert.Equal(VoyagerMoveGroundGapStep.Grounded, new VoyagerActiveMoveGroundGapPolicy(100).Observe(facts));
    }

    [Theory]
    [InlineData("unadmitted_move")]
    [InlineData("other_native_premise")]
    [InlineData("unobserved_contact")]
    [InlineData("lease")]
    [InlineData("journey")]
    public void AnyUnprovedIndependentPremiseRefusesAndCannotRecoverInPlace(string fault)
    {
        var facts = Good();
        switch (fault)
        {
            case "unadmitted_move": facts.ExactActiveStrictMove = false; break;
            case "other_native_premise": facts.NonGroundNativePremisesProved = false; break;
            case "unobserved_contact": facts.GroundSphereHit = null; break;
            case "lease": facts.ControlLeaseOpen = false; break;
            case "journey": facts.OriginalJourney = null; break;
        }
        var state = new VoyagerActiveMoveGroundGapPolicy(100);
        Assert.Equal(VoyagerMoveGroundGapStep.Rejected, state.Observe(facts));
        Assert.Equal(VoyagerMoveGroundGapStep.Rejected, state.Observe(Good()));
    }

    [Fact]
    public void FourteenTicksAreFundedWithoutBorrowingFutureGeneration()
    {
        var facts = Good(); facts.CoreEnergyJoules = 140;
        facts.OriginalJourney!.AvailableLoadedEnergyJoules = 1140;
        Assert.True(VoyagerActiveMoveGroundGapPolicy.CanFund(facts, 14));
        Assert.Equal(VoyagerMoveGroundGapStep.ContinueGap, new VoyagerActiveMoveGroundGapPolicy(100).Observe(facts));
        facts.CoreEnergyJoules = 130;
        facts.OriginalJourney.AvailableLoadedEnergyJoules = 1130;
        Assert.False(VoyagerActiveMoveGroundGapPolicy.CanFund(facts, 14));
        Assert.True(VoyagerActiveMoveGroundGapPolicy.CanFund(facts, 13));
        Assert.Equal(VoyagerMoveGroundGapStep.Stop, new VoyagerActiveMoveGroundGapPolicy(100).Observe(facts));
    }

    [Theory]
    [InlineData("loaded")]
    [InlineData("core")]
    [InlineData("cost")]
    [InlineData("floor")]
    [InlineData("overflow")]
    public void UnfundedOrNonfiniteInputsCannotPayEitherWorkOrTheStop(string fault)
    {
        var f = Good();
        switch (fault)
        {
            case "loaded": f.OriginalJourney!.AvailableLoadedEnergyJoules = 1000; break;
            case "core": f.CoreEnergyJoules = double.NaN; break;
            case "cost": f.PerNativeTickEnergyJoules = null; break;
            case "floor": f.OriginalJourney!.LockedReturnFloorJoules = -1; break;
            case "overflow": f.PerNativeTickEnergyJoules = double.MaxValue; break;
        }
        Assert.False(VoyagerActiveMoveGroundGapPolicy.CanFund(f, 14));
        Assert.False(VoyagerActiveMoveGroundGapPolicy.CanFund(f, 13));
        Assert.Equal(VoyagerMoveGroundGapStep.Stop, new VoyagerActiveMoveGroundGapPolicy(100).Observe(f));
    }

    [Fact]
    public void ActiveOrderUsesCopiedNumericBudgetWithoutPretendingToBeIdle()
    {
        var f = Good(); var j = f.OriginalJourney!;
        Assert.False(j.NativePlayerIdle);
        Assert.False(VoyagerLocalActionBudgetPolicy.Evaluate(j).Proved);
        Assert.True(VoyagerActiveMoveGroundGapPolicy.OriginalBudgetAllows(f, 1, 13, 1d + 1d / 60d));
        Assert.False(j.NativePlayerIdle);
        Assert.Equal(100L, j.SpentGameTicks);
        Assert.Equal(10000L, j.MaximumGameTicks);
        Assert.Equal(3600L, j.ReturnReserveGameTicks);
        Assert.Equal(1000d, j.LockedReturnFloorJoules);
    }

    [Theory]
    [InlineData("binding")]
    [InlineData("warper_unknown")]
    [InlineData("warper_missing")]
    [InlineData("landed_unknown")]
    [InlineData("before_landing")]
    [InlineData("spent")]
    [InlineData("return")]
    [InlineData("utc")]
    public void GroundLossCannotHideAnyOriginalJourneyReserveFailure(string fault)
    {
        var f = Good(); var j = f.OriginalJourney!;
        switch (fault)
        {
            case "binding": j.ExactRemoteBinding = false; break;
            case "warper_unknown": j.NativeWarperCount = null; break;
            case "warper_missing": j.NativeWarperCount = 1; break;
            case "landed_unknown": j.LastLandedGameTick = null; break;
            case "before_landing": j.LastLandedGameTick = j.CurrentGameTick + 1; break;
            case "spent": j.SpentGameTicks = -1; break;
            case "return": j.ReturnReserveGameTicks = 3599; break;
            case "utc": j.JourneyDeadlineUtc = Now.AddSeconds(60); break;
        }
        Assert.False(VoyagerActiveMoveGroundGapPolicy.OriginalBudgetAllows(f, 1, 13, 1d + 1d / 60d));
        Assert.Equal(VoyagerMoveGroundGapStep.Stop, new VoyagerActiveMoveGroundGapPolicy(100).Observe(f));
    }

    [Fact]
    public void ExactOriginalSpentElapsedWorkTailAndReturnAreAllCountedOnce()
    {
        var f = Good(); var j = f.OriginalJourney!;
        j.MaximumGameTicks = j.SpentGameTicks + j.CurrentGameTick - j.LastLandedGameTick!.Value + 1 + 13 + j.ReturnReserveGameTicks;
        Assert.True(VoyagerActiveMoveGroundGapPolicy.OriginalBudgetAllows(f, 1, 13, 1d + 1d / 60d));
        j.MaximumGameTicks--;
        Assert.False(VoyagerActiveMoveGroundGapPolicy.OriginalBudgetAllows(f, 1, 13, 1d + 1d / 60d));
        j.SpentGameTicks = long.MaxValue; j.CurrentGameTick = long.MaxValue;
        Assert.False(VoyagerActiveMoveGroundGapPolicy.OriginalBudgetAllows(f, 1, 13, 1d + 1d / 60d));
    }

    [Fact]
    public void OriginalWorkExpiryStartsStopWithoutChangingItsReserve()
    {
        var f = Good(); f.OriginalWorkWindowOpen = false;
        var state = new VoyagerActiveMoveGroundGapPolicy(100);
        Assert.Equal(VoyagerMoveGroundGapStep.Stop, state.Observe(f));
        Assert.True(VoyagerActiveMoveGroundGapPolicy.OriginalBudgetAllows(f, 0, 13, 1));
        Assert.True(VoyagerActiveMoveGroundGapPolicy.CanFund(f, 13));
        f.OriginalWorkWindowOpen = true; f.OriginalJourney!.CurrentGameTick++;
        Assert.Equal(VoyagerMoveGroundGapStep.Stop, state.Observe(f));
    }

    [Fact]
    public void StopAtRemainingOneFundsTheFailureCurrent601RatherThanAProfileRenewal()
    {
        for (var elapsed = 0L; elapsed < 12; elapsed++)
            Assert.False(VoyagerActiveMoveGroundGapPolicy.MustPauseBeforeCurrentTick(200, 200 + elapsed, Now,
                Now.AddSeconds(elapsed / 60d), Now.AddSeconds(1)));
        Assert.True(VoyagerActiveMoveGroundGapPolicy.MustPauseBeforeCurrentTick(200, 212, Now,
            Now.AddSeconds(.2), Now.AddSeconds(1)));
        // Twelve earlier601 + the current failure601 is the unchanged13P.
        var f = Good(); f.CoreEnergyJoules = 10; f.OriginalJourney!.AvailableLoadedEnergyJoules = 1010;
        Assert.True(VoyagerActiveMoveGroundGapPolicy.CanFund(f, 1));
        Assert.False(VoyagerActiveMoveGroundGapPolicy.CanFund(f, 2));
    }

    [Fact]
    public void StopClockReversalOrWallExpiryCannotRestartTheTail()
    {
        Assert.True(VoyagerActiveMoveGroundGapPolicy.MustPauseBeforeCurrentTick(200, 199, Now, Now, Now.AddSeconds(1)));
        Assert.True(VoyagerActiveMoveGroundGapPolicy.MustPauseBeforeCurrentTick(200, 201, Now, Now.AddTicks(-1), Now.AddSeconds(1)));
        Assert.True(VoyagerActiveMoveGroundGapPolicy.MustPauseBeforeCurrentTick(200, 201, Now, Now.AddSeconds(1), Now.AddSeconds(1)));
        var state = new VoyagerActiveMoveGroundGapPolicy(100);
        state.LatchStop("first_stop");
        state.LatchStop("later_stop");
        Assert.Equal("first_stop", state.BlockerCode);
        state.Reject("physical_tail_unknown");
        Assert.Equal(VoyagerMoveGroundGapStep.Rejected, state.Observe(Good()));
    }

    private static VoyagerActiveMoveGroundGapFacts Good() => new()
    {
        ExactActiveStrictMove = true,
        NonGroundNativePremisesProved = true,
        GroundSphereHit = false,
        ControlLeaseOpen = true,
        OriginalWorkWindowOpen = true,
        CoreEnergyJoules = 10000,
        PerNativeTickEnergyJoules = 10,
        OriginalJourney = new VoyagerLocalActionBudgetFacts
        {
            ExactRemoteBinding = true,
            NativePlayerIdle = false,
            CurrentGameTick = 101,
            LastLandedGameTick = 50,
            SpentGameTicks = 100,
            MaximumGameTicks = 10000,
            ReturnReserveGameTicks = 3600,
            NowUtc = Now,
            JourneyDeadlineUtc = Now.AddMinutes(10),
            AvailableLoadedEnergyJoules = 11000,
            LockedReturnFloorJoules = 1000,
            NativeWarperCount = 2,
            OriginalExtraWarperReserve = 1,
        },
    };
}
