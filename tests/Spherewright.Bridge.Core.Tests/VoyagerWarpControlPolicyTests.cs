using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerWarpControlPolicyTests
{
    [Fact]
    public void OnlyCompleteFreshFactsClaimOneEntryWithoutMutatingNativeCopies()
    {
        var facts = Facts();
        var result = VoyagerWarpControlPolicy.EvaluateEntry(VoyagerWarpControlState.Ready, facts);
        Assert.True(result.Allowed);
        Assert.Empty(result.Blockers);
        Assert.Equal(3, result.RequiredWarpers);
        Assert.Equal(3000d, result.RequiredEnergyJoules);
        Assert.True(result.NextState.EntryAttempted);
        Assert.False(VoyagerWarpControlState.Ready.EntryAttempted);
        Assert.False(result.NextState.StopLatched);
        Assert.Equal(3, facts.NativeWarperCount);
        Assert.Equal(2000d, facts.CoreEnergyJoules);
        Assert.False(facts.WarpCommand);
        Assert.Equal(0f, facts.WarpState);
    }

    [Theory]
    [InlineData(999.999d, false)]
    [InlineData(1000d, false)]
    [InlineData(1000.0000000001d, true)]
    public void NativeCoreThresholdIsStrict(double coreEnergy, bool allowed)
    {
        var facts = Facts();
        facts.CoreEnergyJoules = coreEnergy;
        var result = Entry(facts);
        Assert.Equal(allowed, result.Allowed);
        Assert.Equal(!allowed, Has(result, "native_warp_start_energy_insufficient"));
    }

    [Theory]
    [InlineData(0, 1, 1, 1, false)]
    [InlineData(2, 1, 1, 1, false)]
    [InlineData(3, 1, 1, 1, true)]
    [InlineData(3, 2, 1, 1, false)]
    [InlineData(1, 1, 0, 0, true)]
    public void RealWarpersProtectRemainingLegReturnAndExtras(int available, int leg, int returning, int extra, bool allowed)
    {
        var facts = Facts();
        facts.NativeWarperCount = available;
        facts.CurrentLegRemainingEntries = leg;
        facts.ReturnRemainingEntries = returning;
        facts.ExtraWarperReserve = extra;
        var result = Entry(facts);
        Assert.Equal(allowed, result.Allowed);
        Assert.Equal(leg + returning + extra, result.RequiredWarpers);
        Assert.Equal(available, facts.NativeWarperCount);
    }

    [Theory]
    [InlineData(2999.999d, false)]
    [InlineData(3000d, true)]
    public void LoadedEnergyMustCoverLockedCurrentLegPlusUntouchableReturnFloor(double availableEnergy, bool allowed)
    {
        var facts = Facts();
        facts.AvailableEnergyJoules = availableEnergy;
        var result = Entry(facts);
        Assert.Equal(allowed, result.Allowed);
        Assert.Equal(!allowed, Has(result, "fuel_return_reserve_insufficient"));
    }

    [Theory]
    [InlineData(-0.01d, false)]
    [InlineData(0.25d, true)]
    [InlineData(0.2500001d, false)]
    public void HeadingToleranceIsFiniteNonnegativeAndInclusive(double heading, bool allowed)
    {
        var facts = Facts();
        facts.HeadingErrorDegrees = heading;
        Assert.Equal(allowed, Entry(facts).Allowed);
    }

    [Theory]
    [InlineData(false, true, VoyagerWarpPathEnvelope.Clear)]
    [InlineData(true, false, VoyagerWarpPathEnvelope.Clear)]
    [InlineData(true, true, VoyagerWarpPathEnvelope.Unknown)]
    [InlineData(true, true, VoyagerWarpPathEnvelope.Blocked)]
    public void HiddenOrUnknownInformationCannotMasqueradeAsClear(bool visible, bool legal, VoyagerWarpPathEnvelope envelope)
    {
        var facts = Facts();
        facts.TargetNativelyVisible = visible;
        facts.PathEnvelopeLegallyObservable = legal;
        facts.PathEnvelope = envelope;
        var result = Entry(facts);
        Assert.False(result.Allowed);
        Assert.True(Has(result, "lawful_clear_path_envelope_required"));
    }

    [Fact]
    public void ClearanceUsesFutureFullWarpAndStrictlyExceedsItsExitBound()
    {
        var facts = Facts();
        var bound = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(1f, facts.MaxWarpSpeed, facts.OrdinarySpeedUpperBound);
        facts.ClearPathDistanceMeters = bound.DistanceUpperBoundMeters;
        Assert.True(Has(Entry(facts), "warp_exit_clearance_insufficient"));
        facts.ClearPathDistanceMeters += 1d;
        Assert.True(Entry(facts).Allowed);
        Assert.Equal(0f, facts.WarpState);
        Assert.Equal(15, bound.NativeDecayGameTicks);
    }

    [Theory]
    [InlineData("owned", "owned_binding_required")]
    [InlineData("exclusive", "exclusive_movement_binding_required")]
    [InlineData("stale", "fresh_entry_facts_required")]
    [InlineData("dead", "native_sailing_entry_conditions_required")]
    [InlineData("not_sailing", "native_sailing_entry_conditions_required")]
    [InlineData("disabled", "native_sailing_entry_conditions_required")]
    [InlineData("guide", "native_sailing_entry_conditions_required")]
    [InlineData("local_planet", "native_sailing_entry_conditions_required")]
    [InlineData("thruster", "warp_technology_required")]
    [InlineData("command", "native_warp_exit_required_before_entry")]
    [InlineData("decaying", "native_warp_exit_required_before_entry")]
    [InlineData("unlocked_budget", "invalid_locked_remaining_energy")]
    [InlineData("no_return_floor", "invalid_locked_remaining_energy")]
    public void IndependentNativeOrOwnershipGatesFailClosed(string fault, string blocker)
    {
        var facts = Facts();
        switch (fault)
        {
            case "owned": facts.OwnedBindingVerified = false; break;
            case "exclusive": facts.ExclusiveBindingVerified = false; break;
            case "stale": facts.FactsFresh = false; break;
            case "dead": facts.IsAlive = false; break;
            case "not_sailing": facts.IsSailing = false; break;
            case "disabled": facts.ControllerEnabled = false; break;
            case "guide": facts.NativeGuideRunning = true; break;
            case "local_planet": facts.LocalPlanetId = 1; break;
            case "thruster": facts.ThrusterLevel = 2; break;
            case "command": facts.WarpCommand = true; break;
            case "decaying": facts.WarpState = 0.01f; break;
            case "unlocked_budget": facts.RemainingBudgetLocked = false; break;
            case "no_return_floor": facts.ReturnEnergyFloorJoules = 0d; break;
            default: throw new ArgumentException(nameof(fault));
        }
        var result = Entry(facts);
        Assert.False(result.Allowed);
        Assert.True(Has(result, blocker));
        Assert.False(result.NextState.EntryAttempted);
    }

    [Fact]
    public void ClaimedEntryCannotDispatchTwiceEvenIfNativeOutcomeWasUnknownOrWarpHasAlreadyExited()
    {
        var facts = Facts();
        var first = Entry(facts);
        Assert.True(first.Allowed);
        facts.NativeWarperCount = 2;
        var replay = VoyagerWarpControlPolicy.EvaluateEntry(first.NextState, facts);
        Assert.False(replay.Allowed);
        Assert.True(Has(replay, "warp_entry_already_attempted"));
        Assert.True(replay.NextState.EntryAttempted);
        Assert.False(replay.NextState.StopLatched);
        Assert.Equal(2, facts.NativeWarperCount);
        Assert.Equal(0f, facts.WarpState);
    }

    [Fact]
    public void StopRequestedAtEntryIsStickyEvenBeforeAnyWarperWasConsumed()
    {
        var facts = Facts();
        facts.StopRequested = true;
        var stopped = Entry(facts);
        Assert.False(stopped.Allowed);
        Assert.True(stopped.NextState.StopLatched);
        facts.StopRequested = false;
        var attemptedReentry = VoyagerWarpControlPolicy.EvaluateEntry(stopped.NextState, facts);
        Assert.True(Has(attemptedReentry, "warp_stop_latched"));
        Assert.False(attemptedReentry.Allowed);
        Assert.Equal(3, facts.NativeWarperCount);
    }

    [Fact]
    public void CancelRequestsOnlyOwnedCommandClearAndCannotSettleUntilExitAndNormalBrakingAreObserved()
    {
        var facts = Facts();
        var claimed = Entry(facts).NextState;
        var request = VoyagerWarpControlPolicy.EvaluateStop(claimed, true, true, true, 1f, 1000d);
        Assert.True(request.ClearOwnedWarpCommand);
        Assert.False(request.NativeExitObserved);
        Assert.False(request.StopSettled);
        Assert.True(request.NextState.StopLatched);
        var decay = VoyagerWarpControlPolicy.EvaluateStop(request.NextState, true, false, false, 0.5f, 1000d);
        Assert.False(decay.ClearOwnedWarpCommand);
        Assert.False(decay.NativeExitObserved);
        Assert.False(decay.StopSettled);
        var braking = VoyagerWarpControlPolicy.EvaluateStop(decay.NextState, true, false, false, 0f, 1000d);
        Assert.True(braking.NativeExitObserved);
        Assert.True(braking.NormalBrakingRequired);
        Assert.False(braking.StopSettled);
        var stopped = VoyagerWarpControlPolicy.EvaluateStop(braking.NextState, true, false, false, 0f, 0.1d);
        Assert.True(stopped.StopSettled);
        Assert.False(stopped.NormalBrakingRequired);
        Assert.False(stopped.FlightSucceeded);
        Assert.False(VoyagerWarpControlPolicy.EvaluateEntry(stopped.NextState, facts).Allowed);
        Assert.Equal(3, facts.NativeWarperCount); // Pure cancel neither consumes nor refunds a native item.
        Assert.Equal(2000d, facts.CoreEnergyJoules);
    }

    [Theory]
    [InlineData("launch", false, 0f, 30d)]
    [InlineData("sail", false, 0f, 1000d)]
    [InlineData("warp_entry", true, 0.01f, 1000d)]
    [InlineData("warp_cruise", true, 1f, 1000d)]
    [InlineData("warp_exit", false, 0.5f, 1000d)]
    [InlineData("approach", false, 0f, 500d)]
    [InlineData("braking", false, 0f, 50d)]
    [InlineData("landing", false, 0f, 5d)]
    public void InterruptionDuringAnyInFlightConditionCannotBecomeFlightSuccess(string phase, bool command, float warp, double ordinary)
    {
        var state = phase is "launch" or "sail" ? VoyagerWarpControlState.Ready : Entry(Facts()).NextState;
        var result = VoyagerWarpControlPolicy.EvaluateStop(state, true, true, command, warp, ordinary);
        Assert.True(result.NextState.StopLatched);
        Assert.False(result.StopSettled);
        Assert.False(result.FlightSucceeded);
        Assert.True(result.NormalBrakingRequired);
        if (command || warp != 0f) Assert.False(result.NativeExitObserved);
        Assert.False(VoyagerWarpControlPolicy.EvaluateEntry(result.NextState, Facts()).Allowed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void BindingLossNeverClearsAnotherPlayersCommand(bool ownedBinding, bool mayClear)
    {
        var state = Entry(Facts()).NextState;
        var result = VoyagerWarpControlPolicy.EvaluateStop(state, ownedBinding, true, true, 1f, 1000d);
        Assert.Equal(mayClear, result.ClearOwnedWarpCommand);
        Assert.False(result.StopSettled);
        Assert.True(result.NextState.StopLatched);
    }

    [Fact]
    public void UnclaimedExternalWarpCannotBeClearedAndNoStopRequestIsNotASettledCancellation()
    {
        var external = VoyagerWarpControlPolicy.EvaluateStop(VoyagerWarpControlState.Ready, true, true, true, 1f, 0d);
        Assert.False(external.ClearOwnedWarpCommand);
        Assert.Contains(external.Blockers, item => item.Code == "warp_command_not_owned");
        var idle = VoyagerWarpControlPolicy.EvaluateStop(VoyagerWarpControlState.Ready, true, false, false, 0f, 0d);
        Assert.True(idle.NativeExitObserved);
        Assert.False(idle.StopSettled);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-0.001f)]
    [InlineData(1.001f)]
    public void InvalidWarpObservationLatchesStopAndNeverPretendsToExit(float warp)
    {
        var state = Entry(Facts()).NextState;
        var result = VoyagerWarpControlPolicy.EvaluateStop(state, true, false, true, warp, 0d);
        Assert.True(result.NextState.StopLatched);
        Assert.True(result.ClearOwnedWarpCommand);
        Assert.False(result.NativeExitObserved);
        Assert.False(result.StopSettled);
        Assert.False(result.FlightSucceeded);
        Assert.DoesNotContain("NaN", JsonSerializer.Serialize(result));
        Assert.True(result.NextState.EntryAttempted);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01d)]
    [InlineData(0.1000001d)]
    public void UnknownOrUnstoppedOrdinaryVelocityCannotSettle(double speed)
    {
        var state = Entry(Facts()).NextState;
        var result = VoyagerWarpControlPolicy.EvaluateStop(state, true, true, false, 0f, speed);
        Assert.False(result.StopSettled);
        Assert.False(result.FlightSucceeded);
        Assert.True(result.NextState.StopLatched);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.001f, 1)]
    [InlineData(0.5f, 8)]
    [InlineData(1f, 15)]
    public void NativeFloatDecayIsBoundedByFifteenAdvancingTicks(float warp, int ticks)
    {
        var result = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(warp, 2400000d, 1000d, 0);
        Assert.True(result.Computed);
        Assert.Equal(ticks, result.NativeDecayGameTicks);
        Assert.InRange(result.NativeDecayGameTicks, 0, 15);
        Assert.Equal(1000d * ticks / 60d, result.OrdinaryCoastUpperBoundMeters, precision: 9);
    }

    [Fact]
    public void FullNativeExitUsesExponentialSpeedAndDoesNotTeleportOrBrakeOrdinaryVelocity()
    {
        var result = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(1f, 2400000d, 1000d, 0);
        Assert.Equal(15, result.NativeDecayGameTicks);
        Assert.InRange(result.WarpDistanceUpperBoundMeters, 67750d, 67800d);
        Assert.Equal(250d, result.OrdinaryCoastUpperBoundMeters);
        Assert.True(result.DistanceUpperBoundMeters > result.WarpDistanceUpperBoundMeters + 250d);
        Assert.True(result.DistanceUpperBoundMeters < (2400000d + 1000d) * 15d / 60d);
    }

    [Fact]
    public void DeclaredLatencyIncludesPossibleContinuedNativeRiseBeforeCommandClear()
    {
        var immediate = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(0f, 2400000d, 1000d, 0);
        Assert.True(immediate.Computed);
        Assert.Equal(0d, immediate.DistanceUpperBoundMeters);
        var delayed = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(0f, 2400000d, 1000d, 120);
        Assert.True(delayed.Computed);
        Assert.True(delayed.NativeDecayGameTicks > 0);
        Assert.True(delayed.WarpDistanceUpperBoundMeters > 4800000d);
        Assert.True(delayed.OrdinaryCoastUpperBoundMeters > 2000d);
        var defaultLatency = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(1f, 2400000d, 1000d);
        Assert.Equal(2, defaultLatency.ControlLatencyGameTicks);
        Assert.Equal(15, defaultLatency.NativeDecayGameTicks);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public void ExitRejectsUnboundedOrNegativeControlLatency(int latency)
    {
        var result = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(1f, 2400000d, 1000d, latency);
        Assert.False(result.Computed);
        Assert.Equal("invalid_warp_exit_inputs", result.BlockerCode);
        Assert.Equal(0d, result.DistanceUpperBoundMeters);
    }

    [Fact]
    public void ExitOverflowPublishesNoPartialNumericBound()
    {
        var result = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(1f, double.MaxValue, double.MaxValue, 120);
        Assert.False(result.Computed);
        Assert.Equal("warp_exit_bound_overflow", result.BlockerCode);
        Assert.Equal(0d, result.WarpDistanceUpperBoundMeters);
        Assert.Equal(0d, result.OrdinaryCoastUpperBoundMeters);
        Assert.Equal(0d, result.DistanceUpperBoundMeters);
        Assert.DoesNotContain("Infinity", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("core")]
    [InlineData("threshold")]
    [InlineData("available")]
    [InlineData("leg")]
    [InlineData("return")]
    [InlineData("heading")]
    [InlineData("distance")]
    [InlineData("warp_speed")]
    [InlineData("ordinary_speed")]
    public void EveryEntryNumericFactRejectsNonfiniteValuesWithoutLeakingJson(string field)
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var facts = Facts();
            switch (field)
            {
                case "core": facts.CoreEnergyJoules = value; break;
                case "threshold": facts.NativeStartThresholdJoules = value; break;
                case "available": facts.AvailableEnergyJoules = value; break;
                case "leg": facts.LockedRemainingLegEnergyJoules = value; break;
                case "return": facts.ReturnEnergyFloorJoules = value; break;
                case "heading": facts.HeadingErrorDegrees = value; break;
                case "distance": facts.ClearPathDistanceMeters = value; break;
                case "warp_speed": facts.MaxWarpSpeed = value; break;
                case "ordinary_speed": facts.OrdinarySpeedUpperBound = value; break;
                default: throw new ArgumentException(nameof(field));
            }
            var result = Entry(facts);
            Assert.False(result.Allowed);
            Assert.False(result.NextState.EntryAttempted);
            Assert.NotEmpty(result.Blockers);
            var json = JsonSerializer.Serialize(result);
            Assert.DoesNotContain("NaN", json);
            Assert.DoesNotContain("Infinity", json);
        }
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-0.001f)]
    [InlineData(1.001f)]
    public void NativeWarpStateMustBeFiniteAndWithinBoundsForBothEntryAndExit(float warp)
    {
        var facts = Facts();
        facts.WarpState = warp;
        Assert.True(Has(Entry(facts), "native_warp_exit_required_before_entry"));
        var bound = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(warp, facts.MaxWarpSpeed, facts.OrdinarySpeedUpperBound);
        Assert.False(bound.Computed);
        Assert.Equal("invalid_warp_exit_inputs", bound.BlockerCode);
        Assert.Equal(0d, bound.DistanceUpperBoundMeters);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, -1)]
    [InlineData(1, 1, 19)]
    public void MissingCurrentEntryOrInvalidFutureReservationsFailClosed(int currentLeg, int returning, int extras)
    {
        var facts = Facts();
        facts.CurrentLegRemainingEntries = currentLeg;
        facts.ReturnRemainingEntries = returning;
        facts.ExtraWarperReserve = extras;
        var result = Entry(facts);
        Assert.False(result.Allowed);
        Assert.Null(result.RequiredWarpers);
        Assert.True(Has(result, "invalid_remaining_warper_reserve"));
    }

    [Fact]
    public void RemainingReservationOverflowOrInvalidCountsCannotClaimEntry()
    {
        var facts = Facts();
        facts.CurrentLegRemainingEntries = int.MaxValue;
        facts.ReturnRemainingEntries = int.MaxValue;
        Assert.True(Has(Entry(facts), "invalid_remaining_warper_reserve"));
        facts = Facts();
        facts.NativeWarperCount = -1;
        Assert.True(Has(Entry(facts), "invalid_remaining_warper_reserve"));
        facts = Facts();
        facts.LockedRemainingLegEnergyJoules = double.MaxValue;
        facts.ReturnEnergyFloorJoules = double.MaxValue;
        var overflow = Entry(facts);
        Assert.True(Has(overflow, "invalid_locked_remaining_energy"));
        Assert.Null(overflow.RequiredEnergyJoules);
        Assert.DoesNotContain("Infinity", JsonSerializer.Serialize(overflow));
    }

    private static VoyagerWarpEntryDecision Entry(VoyagerWarpEntryFacts facts) =>
        VoyagerWarpControlPolicy.EvaluateEntry(VoyagerWarpControlState.Ready, facts);

    private static bool Has(VoyagerWarpEntryDecision result, string code) => result.Blockers.Any(item => item.Code == code);

    private static VoyagerWarpEntryFacts Facts() => new VoyagerWarpEntryFacts
    {
        OwnedBindingVerified = true,
        ExclusiveBindingVerified = true,
        FactsFresh = true,
        IsAlive = true,
        IsSailing = true,
        ControllerEnabled = true,
        LocalPlanetId = 0,
        ThrusterLevel = 3,
        CoreEnergyJoules = 2000d,
        NativeStartThresholdJoules = 1000d,
        NativeWarperCount = 3,
        CurrentLegRemainingEntries = 1,
        ReturnRemainingEntries = 1,
        ExtraWarperReserve = 1,
        RemainingBudgetLocked = true,
        AvailableEnergyJoules = 5000d,
        LockedRemainingLegEnergyJoules = 1000d,
        ReturnEnergyFloorJoules = 2000d,
        HeadingErrorDegrees = 0.1d,
        TargetNativelyVisible = true,
        PathEnvelopeLegallyObservable = true,
        PathEnvelope = VoyagerWarpPathEnvelope.Clear,
        ClearPathDistanceMeters = 1000000d,
        MaxWarpSpeed = 2400000d,
        OrdinarySpeedUpperBound = 1000d,
    };
}
