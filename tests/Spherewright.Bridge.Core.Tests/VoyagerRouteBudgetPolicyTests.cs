using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerRouteBudgetPolicyTests
{
    [Fact]
    public void StrictIdleCanAdmitBelowTheIndependentFreshReturnCoreMinimum()
    {
        const double core = 33_000_000, returnCore = 1_280_000_000;
        var cost = VoyagerLocalWalkReservePolicy.Evaluate(new VoyagerLocalWalkFacts
        {
            ModeTransitionsExcluded = true, IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
            WalkSpeed = 12, WalkPowerWatts = 1000, MiningPowerWatts = 200, OtherWorkPowerWatts = 100,
            ReactorConsumptionRatio = 2, RtsSpeed = 0, ManualSpeed = 0, SoftLandingRecover = 1,
            IncludePotentialWaterDrift = true, NativeThrustPowerPerAcceleration = 300, NativeGravityUpperBound = 50,
        });
        Assert.True(cost.Proved); Assert.True(13 * cost.PerNativeTickEnergyJoules < core); Assert.True(core < returnCore);
        var now = DateTimeOffset.UtcNow;
        var facts = new VoyagerRemoteIdleAdmissionFacts { CoreEnergyJoules = core, MovementReserve = cost,
            JourneyBudget = new() { ExactRemoteBinding = true, NativePlayerIdle = true, CurrentGameTick = 100,
                LastLandedGameTick = 100, SpentGameTicks = 10, MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000,
                WorkGameTicks = 1, TailGameTicks = 13, NowUtc = now, JourneyDeadlineUtc = now.AddHours(1),
                AvailableLoadedEnergyJoules = 10_000_000_000, LockedReturnFloorJoules = 5_000_000_000,
                WorkPowerUpperWatts = cost.WorkPowerUpperWatts, TailAndNextTickEnergyJoules = cost.StopEnergyJoules!.Value,
                NativeWarperCount = 3, OriginalExtraWarperReserve = 1 } };
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(facts).Proved);
        facts.CoreEnergyJoules = 2 * cost.PerNativeTickEnergyJoules;
        Assert.False(VoyagerRemoteIdleAdmissionPolicy.Evaluate(facts).Proved);
        var returning = ReturnFacts(); returning.CoreCapacityJoules = 1_600_000_000;
        returning.CoreEnergyJoules = core; returning.LockedReturnCoreMinimumJoules = returnCore;
        var freshReturn = VoyagerRouteBudgetPolicy.Evaluate(returning, ReturnRequest());
        Assert.False(freshReturn.BudgetSufficient); Assert.False(freshReturn.CoreBufferSufficient);
        Assert.Equal(returnCore, freshReturn.ReturnCoreEnergyMinimumJoules);
        Assert.Contains(freshReturn.Blockers, b => b.Code == "core_buffer_or_generation_insufficient");
    }

    [Fact]
    public void SufficientFiniteReserveRemainsReadOnlyAndPreservesRealItemCounts()
    {
        var input = Facts();
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.True(result.BudgetComputed);
        Assert.True(result.BudgetSufficient);
        Assert.Empty(result.Blockers);
        Assert.False(result.Prepared);
        Assert.False(result.Executable);
        Assert.False(result.CommitAllowedNow);
        Assert.Equal(3, result.RequiredWarpers);
        Assert.Equal(3, result.AvailableWarpers);
        Assert.Equal(3, input.NativeWarperCount);
        Assert.Equal(1d, result.DistanceLightYears);
        Assert.Equal(result.OutboundEnergyBudgetJoules + result.ReturnEnergyBudgetJoules + result.WithdrawalReserveJoules,
            result.RequiredEnergyJoules);
        Assert.True(result.LandingReserveJoules > 0d);
        Assert.True(result.WithdrawalReserveJoules >= input.CoreCapacityJoules);
    }

    [Fact]
    public void StoredFuelHeatCannotReplaceCorePowerOrCapacity()
    {
        var input = Facts();
        input.CoreCapacityJoules = input.CoreEnergyJoules = 200000d;
        input.CoreGenerationLowerBoundWatts = 0d;
        input.WarpKeepingPowerPerSpeed = 100d;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.True(result.StoredEnergySufficient);
        Assert.True(result.NativeWarpEntryEnergySufficient);
        Assert.False(result.CoreBufferSufficient);
        Assert.Contains(result.Blockers, item => item.Code == "core_buffer_or_generation_insufficient");
        Assert.False(result.BudgetSufficient);
    }

    [Fact]
    public void MissingLoadedFuelBlocksBothLegAndWithdrawalBudget()
    {
        var input = Facts();
        input.ReactorEnergyJoules = input.FuelStorageEnergyJoules = 0d;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.False(result.StoredEnergySufficient);
        Assert.Contains(result.Blockers, item => item.Code == "fuel_energy_reserve_insufficient");
        Assert.True(result.ReturnEnergyBudgetJoules > 0d);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public void PackageWarpersCountOnlyThroughEnabledNativeReplenishment(bool autoReplenish, int available)
    {
        var input = Facts();
        input.NativeWarperCount = 1;
        input.PackageWarperCount = 2;
        input.AutoReplenishWarper = autoReplenish;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.Equal(available, result.AvailableWarpers);
        Assert.Equal(autoReplenish, result.WarperReserveSufficient);
        Assert.Equal(1, input.NativeWarperCount);
        Assert.Equal(2, input.PackageWarperCount);
    }

    [Fact]
    public void HiddenDestinationAndStaleBindingsHaveIndependentBlockers()
    {
        var input = Facts();
        input.DestinationVisible = false;
        input.PlayerStateHash = "new-player";
        input.CatalogStateHash = "new-catalogue";
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.True(result.BudgetComputed);
        Assert.False(result.BudgetSufficient);
        Assert.Contains(result.Blockers, item => item.Code == "destination_not_natively_visible");
        Assert.Contains(result.Blockers, item => item.Code == "stale_player_state");
        Assert.Contains(result.Blockers, item => item.Code == "stale_catalog_state");
    }

    [Fact]
    public void SameStarAndGasDestinationsCannotBecomeVoyagerLandingPlans()
    {
        var input = Facts();
        input.DestinationStarId = input.OriginStarId;
        input.DestinationIsGasGiant = true;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.Contains(result.Blockers, item => item.Code == "cross_star_route_identity_invalid");
        Assert.Contains(result.Blockers, item => item.Code == "destination_not_landable");
    }

    [Fact]
    public void NativeEntryGateIsStrictAndSeparateFromConsumptionCoefficient()
    {
        var input = Facts();
        var threshold = input.WarpStartPowerPerSpeed * input.MaxWarpSpeed;
        input.CoreEnergyJoules = threshold;
        input.EnergyConsumptionCoefficient = 2d;
        var equal = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.False(equal.NativeWarpEntryEnergySufficient);
        Assert.Equal(threshold, equal.WarpStartEnergyThresholdJoules);
        input.CoreEnergyJoules = threshold + 1d;
        var above = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.True(above.NativeWarpEntryEnergySufficient);
        Assert.Equal(threshold, above.WarpStartEnergyThresholdJoules);
        Assert.Equal(equal.WarpStartEnergyUpperBoundJoules, above.WarpStartEnergyUpperBoundJoules);
    }

    [Fact]
    public void OnlyDeclaredWarpTicksConsumeWarpBudgetAndTheStartRampIsChargedOncePerLeg()
    {
        var input = Facts();
        var shortRequest = Request();
        var longRequest = Request();
        longRequest.MaximumFlightGameTicks = 72000;
        longRequest.ReturnReserveGameTicks = 36000;
        var shorter = VoyagerRouteBudgetPolicy.Evaluate(input, shortRequest);
        var longer = VoyagerRouteBudgetPolicy.Evaluate(input, longRequest);
        var keeping = input.WarpKeepingPowerPerSpeed * input.MaxWarpSpeed * input.EnergyConsumptionCoefficient;
        Assert.Equal(keeping, shorter.WarpKeepingPowerUpperBoundWatts);
        Assert.Equal(shorter.RequiredEnergyJoules, longer.RequiredEnergyJoules);
        Assert.Equal(shorter.CoreBufferRequiredJoules, longer.CoreBufferRequiredJoules);
        longRequest.MaximumWarpGameTicksPerLeg = 3600;
        var moreWarp = VoyagerRouteBudgetPolicy.Evaluate(input, longRequest);
        Assert.Equal(keeping * 60d, moreWarp.RequiredEnergyJoules - shorter.RequiredEnergyJoules, precision: 5);
        Assert.Equal(shorter.WarpStartEnergyUpperBoundJoules, moreWarp.WarpStartEnergyUpperBoundJoules);
        Assert.True(shorter.WarpStartEnergyUpperBoundJoules > 0d);
        Assert.True(shorter.WarpStartEnergyUpperBoundJoules < shorter.WarpStartEnergyThresholdJoules);
    }

    [Fact]
    public void InsufficientTechnologyAndBusyOrDeadPlayerFailClosed()
    {
        var input = Facts();
        input.ThrusterLevel = 2;
        input.IsAlive = false;
        input.PlayerOrderIdle = false;
        input.NativeGuideRunning = true;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.False(result.TechnologyAllowsWarp);
        Assert.Contains(result.Blockers, item => item.Code == "warp_technology_required");
        Assert.Contains(result.Blockers, item => item.Code == "grounded_live_player_required");
        Assert.Contains(result.Blockers, item => item.Code == "native_movement_channel_busy");
        Assert.Contains(result.Blockers, item => item.Code == "native_guide_energy_mode_active");
    }

    [Theory]
    [InlineData(7199, 3600)]
    [InlineData(108001, 54000)]
    [InlineData(7200, 3599)]
    [InlineData(7200, 3601)]
    public void UnboundedOrUnusableLegWindowsDoNotProducePartialBudgets(long total, long returning)
    {
        var request = Request();
        request.MaximumFlightGameTicks = total;
        request.ReturnReserveGameTicks = returning;
        var result = VoyagerRouteBudgetPolicy.Evaluate(Facts(), request);
        Assert.False(result.BudgetComputed);
        Assert.False(result.BudgetSufficient);
        Assert.Single(result.Blockers, item => item.Code == "invalid_voyager_request");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0.79d)]
    [InlineData(1.01d)]
    public void RatioMustBeFiniteAndWithinDepartureRange(double ratio)
    {
        var request = Request();
        request.MinimumCoreEnergyRatio = ratio;
        var result = VoyagerRouteBudgetPolicy.Evaluate(Facts(), request);
        Assert.False(result.BudgetComputed);
        Assert.False(result.BudgetSufficient);
        Assert.NotNull(VoyagerRouteBudgetPolicy.ValidateRequest(request));
        Assert.DoesNotContain("NaN", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public void NumericNativeFailureNeverLeaksNonFiniteWireValuesOrClaimsZeroCost()
    {
        var input = Facts();
        input.DistanceMeters = double.PositiveInfinity;
        input.CoreGenerationLowerBoundWatts = double.NaN;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.False(result.BudgetComputed);
        Assert.Contains(result.Blockers, item => item.Code == "invalid_native_budget_inputs");
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("Infinity", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NaN", json, StringComparison.Ordinal);
    }

    [Fact]
    public void FiniteOverflowIsRejectedWithoutPartialArithmeticCredit()
    {
        var input = Facts();
        input.WarpKeepingPowerPerSpeed = double.MaxValue;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.False(result.BudgetComputed);
        Assert.Contains(result.Blockers, item => item.Code == "native_budget_overflow");
        Assert.False(result.Prepared);
        Assert.DoesNotContain("Infinity", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(19)]
    public void WarperReserveIsBounded(int extra)
    {
        var request = Request();
        request.ExtraWarperReserve = extra;
        Assert.NotNull(VoyagerRouteBudgetPolicy.ValidateRequest(request));
    }

    [Fact]
    public void OptimisticMaximumSpeedLowerBoundCanRejectButCannotApproveTravelTime()
    {
        var input = Facts();
        input.DistanceMeters = 1e12d;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.Contains(result.Blockers, item => item.Code == "route_exceeds_even_maximum_speed_horizon");
        Assert.True(result.MinimumFlightGameTicksAtMaximumSpeed > result.MaximumFlightGameTicks);
        Assert.False(result.Executable);
    }

    [Fact]
    public void ReturnWindowIsReservedInsideTotalRatherThanAddedAfterIt()
    {
        var request = Request();
        request.MaximumFlightGameTicks = 7200;
        request.ReturnReserveGameTicks = 3600;
        var result = VoyagerRouteBudgetPolicy.Evaluate(Facts(), request);
        Assert.Equal(3600, result.OutboundMaximumGameTicks);
        Assert.Equal(result.MaximumFlightGameTicks, result.OutboundMaximumGameTicks + result.ReturnReserveGameTicks);
    }

    [Fact]
    public void RouteHashIsDeterministicAndBindsEveryBudgetRelevantFact()
    {
        var baseline = VoyagerRouteBudgetPolicy.Evaluate(Facts(), Request()).RouteStateHash;
        Assert.StartsWith("sha256:", baseline, StringComparison.Ordinal);
        Assert.Equal(71, baseline.Length);
        Assert.Equal(baseline, VoyagerRouteBudgetPolicy.Evaluate(Facts(), Request()).RouteStateHash);
        var changes = new Action<VoyagerRouteBudgetInput>[]
        {
            value => value.CoreEnergyJoules -= 1d,
            value => value.FuelStorageEnergyJoules -= 1d,
            value => value.ReactorEnergyJoules -= 1d,
            value => value.NativeWarperCount -= 1,
            value => value.PackageWarperCount += 1,
            value => value.AutoReplenishWarper = true,
            value => value.ThrusterLevel += 1,
            value => value.CoreGenerationLowerBoundWatts += 1d,
            value => value.WarpStartPowerPerSpeed += 1d,
            value => value.WarpKeepingPowerPerSpeed += 1d,
            value => value.EnergyConsumptionCoefficient += 0.1d,
            value => value.ThrustPowerPerAcceleration += 1d,
            value => value.DistanceMeters += 1d,
            value => value.NativeVersion = "different-native-version",
            value => value.CapturedAtGameTick += 1L,
            value => value.DestinationVisible = false,
            value => value.DestinationIsStarOnly = true,
        };
        foreach (var change in changes)
        {
            var changed = Facts();
            change(changed);
            Assert.NotEqual(baseline, VoyagerRouteBudgetPolicy.Evaluate(changed, Request()).RouteStateHash);
        }
        var changedRequest = Request();
        changedRequest.ReturnReserveGameTicks -= 1L;
        Assert.NotEqual(baseline, VoyagerRouteBudgetPolicy.Evaluate(Facts(), changedRequest).RouteStateHash);
        changedRequest = Request();
        changedRequest.MaximumFlightGameTicks += 1L;
        Assert.NotEqual(baseline, VoyagerRouteBudgetPolicy.Evaluate(Facts(), changedRequest).RouteStateHash);
        changedRequest = Request();
        changedRequest.ExtraWarperReserve += 1;
        Assert.NotEqual(baseline, VoyagerRouteBudgetPolicy.Evaluate(Facts(), changedRequest).RouteStateHash);
        changedRequest = Request();
        changedRequest.DestinationStarId = 2;
        Assert.NotEqual(baseline, VoyagerRouteBudgetPolicy.Evaluate(Facts(), changedRequest).RouteStateHash);
        changedRequest = Request();
        changedRequest.MaximumWarpGameTicksPerLeg += 1;
        Assert.NotEqual(baseline, VoyagerRouteBudgetPolicy.Evaluate(Facts(), changedRequest).RouteStateHash);
        changedRequest = Request();
        changedRequest.MaximumArrivalOffsetMeters -= 1d;
        var changedOffsetHash = VoyagerRouteBudgetPolicy.Evaluate(Facts(), changedRequest).RouteStateHash;
        Assert.NotEmpty(changedOffsetHash);
        Assert.NotEqual(baseline, changedOffsetHash);
    }

    [Fact]
    public void WarpBufferAndDeclaredLocalReserveNeverClaimQualifiedNewLandingControl()
    {
        var result = VoyagerRouteBudgetPolicy.Evaluate(Facts(), Request());
        Assert.Equal("warp_entries_and_finite_warp_ticks_only", result.CoreBufferScope);
        Assert.False(result.LocalFlightReserveIsControllerProof);
        Assert.Equal("finite_native_warp_duration_and_explicit_return_recharge_v2", result.BudgetBasis);
        Assert.True(result.RequiresReturnCoreRecharge);
        Assert.True(result.ReturnCoreEnergyMinimumJoules >= 0.8d * result.CoreCapacityJoules);
    }

    [Fact]
    public void StarOnlyRouteUsesVisibleStarAndArrivalEnvelopeWithoutInventingPlanetOrResourceKnowledge()
    {
        var input = Facts();
        input.DestinationPlanetId = 0;
        input.DestinationIsStarOnly = true;
        var request = Request();
        request.DestinationPlanetId = 0;
        request.DestinationStarId = 2;
        request.MaximumArrivalOffsetMeters = 100000d;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.True(result.BudgetComputed);
        Assert.True(result.BudgetSufficient);
        Assert.True(result.DestinationIsStarOnly);
        Assert.Equal(0, result.DestinationPlanetId);
        Assert.Equal(2, result.DestinationStarId);
        Assert.Equal(100000d, result.MaximumArrivalOffsetMeters);
        Assert.True(result.MinimumFlightGameTicksAtMaximumSpeed > VoyagerRouteBudgetPolicy.Evaluate(Facts(), Request()).MinimumFlightGameTicksAtMaximumSpeed);
        Assert.True(result.RequiresReturnCoreRecharge);
        Assert.False(result.Prepared);
        Assert.False(result.Executable);
        Assert.False(result.CommitAllowedNow);
        Assert.False(result.LocalFlightReserveIsControllerProof);
    }

    [Fact]
    public void StarOnlyArrivalEnvelopeCanRejectTheWarpDurationEvenWhenTheWholeHorizonFits()
    {
        var input = Facts();
        input.DestinationPlanetId = 0;
        input.DestinationIsStarOnly = true;
        var request = Request();
        request.DestinationPlanetId = 0;
        request.DestinationStarId = 2;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.True(result.BudgetComputed);
        Assert.True(result.MinimumFlightGameTicksAtMaximumSpeed < result.MaximumFlightGameTicks);
        Assert.Contains(result.Blockers, item => item.Code == "route_exceeds_finite_warp_duration");
        Assert.False(result.BudgetSufficient);
        request.MaximumWarpGameTicksPerLeg = 3600;
        var longerWarp = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.True(longerWarp.BudgetSufficient);
        Assert.Equal(3600, longerWarp.MaximumWarpGameTicksPerLeg);
    }

    [Fact]
    public void KnownPlanetKeepsExactKnownDistanceWhileStillBindingTheDeclaredOffset()
    {
        var small = Request();
        small.MaximumArrivalOffsetMeters = 100000d;
        var large = Request();
        large.MaximumArrivalOffsetMeters = 3000000d;
        var smallResult = VoyagerRouteBudgetPolicy.Evaluate(Facts(), small);
        var largeResult = VoyagerRouteBudgetPolicy.Evaluate(Facts(), large);
        Assert.Equal(smallResult.MinimumFlightGameTicksAtMaximumSpeed, largeResult.MinimumFlightGameTicksAtMaximumSpeed);
        Assert.Equal(smallResult.RequiredEnergyJoules, largeResult.RequiredEnergyJoules);
        Assert.NotEqual(smallResult.RouteStateHash, largeResult.RouteStateHash);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 2, true)]
    [InlineData(201, 0, true)]
    [InlineData(201, 2, true)]
    [InlineData(-1, 2, false)]
    [InlineData(201, -1, false)]
    [InlineData(104, 2, false)]
    public void RequestSupportsExplicitStarOrDifferentPlanetWithoutInferringHiddenIdentity(int planet, int star, bool valid)
    {
        var request = Request();
        request.DestinationPlanetId = planet;
        request.DestinationStarId = star;
        Assert.Equal(valid, VoyagerRouteBudgetPolicy.ValidateRequest(request) is null);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ExplicitDestinationStarMustMatchResolvedDifferentSystem(int requestedStar)
    {
        var request = Request();
        request.DestinationStarId = requestedStar;
        var result = VoyagerRouteBudgetPolicy.Evaluate(Facts(), request);
        Assert.Contains(result.Blockers, item => item.Code == "cross_star_route_identity_invalid");
        Assert.False(result.BudgetSufficient);
    }

    [Fact]
    public void StarOnlyCannotReuseOriginSystemOrResolvePlanetBeforeItIsSelected()
    {
        var request = Request();
        request.DestinationPlanetId = 0;
        request.DestinationStarId = 1;
        var input = Facts();
        input.DestinationPlanetId = 0;
        input.DestinationStarId = 1;
        input.DestinationIsStarOnly = true;
        var sameStar = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.Contains(sameStar.Blockers, item => item.Code == "cross_star_route_identity_invalid");
        request.DestinationStarId = 2;
        input.DestinationStarId = 2;
        input.DestinationPlanetId = 201;
        var prematurePlanet = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.Contains(prematurePlanet.Blockers, item => item.Code == "cross_star_route_identity_invalid");
    }

    [Theory]
    [InlineData(179, false)]
    [InlineData(180, true)]
    [InlineData(3600, true)]
    [InlineData(3601, false)]
    public void NativeWarpWindowIsFiniteAndFitsBothWholeLegs(long warpTicks, bool valid)
    {
        var request = Request();
        request.MaximumFlightGameTicks = 7200;
        request.ReturnReserveGameTicks = 3600;
        request.MaximumWarpGameTicksPerLeg = warpTicks;
        Assert.Equal(valid, VoyagerRouteBudgetPolicy.ValidateRequest(request) is null);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(99999d)]
    [InlineData(3000001d)]
    public void InvalidArrivalOffsetCannotProduceBudgetOrNonfiniteJson(double offset)
    {
        var request = Request();
        request.MaximumArrivalOffsetMeters = offset;
        var result = VoyagerRouteBudgetPolicy.Evaluate(Facts(), request);
        Assert.False(result.BudgetComputed);
        Assert.Empty(result.RouteStateHash);
        Assert.Contains(result.Blockers, item => item.Code == "invalid_voyager_request");
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("NaN", json);
        Assert.DoesNotContain("Infinity", json);
    }

    [Fact]
    public void TwoLegsRequireFreshReturnCoreRechargeInsteadOfOneUninterruptedDoubleBuffer()
    {
        var input = Facts();
        input.CoreCapacityJoules = input.CoreEnergyJoules = 300000d;
        input.WarpKeepingPowerPerSpeed = 0.05d;
        input.CoreGenerationLowerBoundWatts = 1000d;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.True(result.BudgetSufficient);
        Assert.True(result.CoreBufferSufficient);
        Assert.True(result.CoreBufferRequiredJoules < input.CoreCapacityJoules);
        Assert.True(result.CoreBufferRequiredJoules * 2d > input.CoreCapacityJoules);
        Assert.True(result.RequiresReturnCoreRecharge);
        Assert.Equal(240000d, result.ReturnCoreEnergyMinimumJoules);
        Assert.True(result.ReturnCoreEnergyMinimumJoules >= result.CoreBufferRequiredJoules);
        Assert.False(result.Prepared);
        Assert.False(result.Executable);
        Assert.False(result.CommitAllowedNow);
    }

    [Fact]
    public void ReturnCoreFloorRetainsStricterDeclaredRatioAndNativeEntryGate()
    {
        var input = Facts();
        input.CoreCapacityJoules = input.CoreEnergyJoules = 300000d;
        input.WarpKeepingPowerPerSpeed = 0.05d;
        input.CoreGenerationLowerBoundWatts = 1000d;
        var request = Request();
        request.MinimumCoreEnergyRatio = 0.95d;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.Equal(285000d, result.ReturnCoreEnergyMinimumJoules);
        Assert.True(result.ReturnCoreEnergyMinimumJoules > result.WarpStartEnergyThresholdJoules);
        input.CoreCapacityJoules = input.CoreEnergyJoules = 100000d;
        var impossible = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.False(impossible.CoreBufferSufficient);
        Assert.True(impossible.ReturnCoreEnergyMinimumJoules > input.CoreCapacityJoules);
    }

    [Fact]
    public void MaximumSpeedTickConversionCannotWrapAtTheSignedRoundTripLimit()
    {
        var input = Facts();
        input.MaxWarpSpeed = 59d;
        input.MaxSailSpeed = 1d;
        input.DistanceMeters = long.MaxValue / 2d;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, Request());
        Assert.False(result.BudgetComputed);
        Assert.Null(result.MinimumFlightGameTicksAtMaximumSpeed);
        Assert.Contains(result.Blockers, item => item.Code == "native_budget_overflow");
        Assert.DoesNotContain("Infinity", JsonSerializer.Serialize(result));
    }
    [Fact]
    public void ThreeInitialWarpersLeaveExactlyEnoughForTheSingleProtectedReturn()
    {
        var outbound = VoyagerRouteBudgetPolicy.Evaluate(Facts(), Request());
        Assert.True(outbound.BudgetSufficient);
        Assert.Equal(3, outbound.RequiredWarpers);
        var input = ReturnFacts();
        input.FuelStorageEnergyJoules = input.LockedReturnEnergyFloorJoules
            - input.CoreEnergyJoules - input.ReactorEnergyJoules;
        var returning = VoyagerRouteBudgetPolicy.Evaluate(input, ReturnRequest());
        Assert.True(returning.BudgetSufficient);
        Assert.True(returning.IsReturnLeg);
        Assert.Equal(2, returning.RequiredWarpers);
        Assert.Equal(2, returning.AvailableWarpers);
        Assert.Equal(0d, returning.ReturnEnergyBudgetJoules);
        Assert.Equal(input.LockedReturnEnergyFloorJoules, returning.RequiredEnergyJoules);
        Assert.Equal(returning.RequiredEnergyJoules, returning.AvailableEnergyJoules);
        Assert.Equal(input.ReturnLegMaximumGameTicks, returning.OutboundMaximumGameTicks);
        Assert.Equal(input.RemainingJourneyGameTicks, returning.MaximumFlightGameTicks);
        Assert.Equal(0, returning.ReturnReserveGameTicks);
        Assert.False(returning.RequiresReturnCoreRecharge);
        Assert.False(returning.Prepared);
        Assert.False(returning.Executable);
    }

    [Theory]
    [InlineData("not-verified")]
    [InlineData("expired-reader-proof")]
    [InlineData("different-journey")]
    [InlineData("missing-protected-id")]
    public void ClientJourneyIdCannotSubstituteForFreshProtectedReturnProof(string fault)
    {
        var input = ReturnFacts();
        if (fault == "different-journey") input.ReturnJourneyId = OtherJourneyId;
        else if (fault == "missing-protected-id") input.ReturnJourneyId = string.Empty;
        else input.ReturnJourneyVerified = false; // The reader must clear this for expired/wrong-world/direction proof.
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, ReturnRequest());
        Assert.False(result.BudgetSufficient);
        Assert.Contains(result.Blockers, b => b.Code == "return_journey_not_verified");
        Assert.False(result.CommitAllowedNow);
    }

    [Fact]
    public void ReturnJourneyIdentifierMustBeRealAndRequestOnlyCannotCreateReturnAuthority()
    {
        var request = ReturnRequest();
        request.JourneyId = "not-a-journey-guid";
        Assert.NotNull(VoyagerRouteBudgetPolicy.ValidateRequest(request));
        var result = VoyagerRouteBudgetPolicy.Evaluate(Facts(), ReturnRequest());
        Assert.False(result.BudgetComputed);
        Assert.False(result.BudgetSufficient);
        Assert.Contains(result.Blockers, b => b.Code == "return_journey_reserve_invalid");
    }

    [Theory]
    [InlineData(3599, 3600)]
    [InlineData(3600, 3599)]
    [InlineData(108001, 108001)]
    [InlineData(3600, 108001)]
    public void ReturnCannotExtendOrSpendAnInsufficientOriginalWholeJourneyHorizon(long leg, long remaining)
    {
        var input = ReturnFacts();
        input.ReturnLegMaximumGameTicks = leg;
        input.RemainingJourneyGameTicks = remaining;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, ReturnRequest());
        Assert.False(result.BudgetComputed);
        Assert.Contains(result.Blockers, b => b.Code == "return_journey_reserve_invalid");
    }

    [Fact]
    public void ReturnNeedsOnlyItsOriginalSingleLegInsteadOfASecondRoundTrip()
    {
        var input = ReturnFacts();
        input.ReturnLegMaximumGameTicks = input.RemainingJourneyGameTicks = 3600;
        var request = ReturnRequest();
        request.MaximumFlightGameTicks = 3600;
        request.ReturnReserveGameTicks = 0;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.True(result.BudgetSufficient);
        Assert.Equal(3600, result.OutboundMaximumGameTicks);
        Assert.Equal(3600, result.MaximumFlightGameTicks);
        Assert.Equal(0, result.ReturnReserveGameTicks);
        request.MaximumWarpGameTicksPerLeg = 3601;
        Assert.Contains(VoyagerRouteBudgetPolicy.Evaluate(input, request).Blockers,
            b => b.Code == "return_journey_reserve_invalid");
    }

    [Fact]
    public void ReturnNeverLowersTheOriginalFuelWithdrawalCoreOrExtraWarperFloors()
    {
        var input = ReturnFacts();
        input.LockedReturnEnergyFloorJoules = 9e9d;
        input.LockedWithdrawalReserveJoules = 1.25e9d;
        input.LockedReturnCoreMinimumJoules = 0.95e9d;
        input.OriginalExtraWarperReserve = 2;
        input.NativeWarperCount = 3;
        var request = ReturnRequest();
        request.ExtraWarperReserve = 0;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        Assert.True(result.BudgetSufficient);
        Assert.Equal(3, result.RequiredWarpers);
        Assert.Equal(9e9d, result.RequiredEnergyJoules);
        Assert.Equal(1.25e9d, result.WithdrawalReserveJoules);
        Assert.Equal(0.95e9d, result.ReturnCoreEnergyMinimumJoules);
        input.CoreEnergyJoules = 0.9e9d;
        Assert.False(VoyagerRouteBudgetPolicy.Evaluate(input, request).CoreBufferSufficient);
        input.FuelStorageEnergyJoules = 8e9d - input.CoreEnergyJoules - input.ReactorEnergyJoules;
        Assert.False(VoyagerRouteBudgetPolicy.Evaluate(input, request).StoredEnergySufficient);
        request.ExtraWarperReserve = 3;
        Assert.Equal(4, VoyagerRouteBudgetPolicy.Evaluate(input, request).RequiredWarpers);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void InvalidProtectedReturnFloorsCannotProducePartialBudgetCredit(double invalid)
    {
        var input = ReturnFacts();
        input.LockedReturnEnergyFloorJoules = invalid;
        var result = VoyagerRouteBudgetPolicy.Evaluate(input, ReturnRequest());
        Assert.False(result.BudgetComputed);
        Assert.False(result.BudgetSufficient);
        Assert.DoesNotContain("NaN", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("Infinity", JsonSerializer.Serialize(result));
    }

    [Fact]
    public void ReturnHashBindsTheRequestAndEveryCopiedProtectedProofField()
    {
        var baseline = VoyagerRouteBudgetPolicy.Evaluate(ReturnFacts(), ReturnRequest()).RouteStateHash;
        Assert.NotEmpty(baseline);
        var changes = new Action<VoyagerRouteBudgetInput>[]
        {
            v => v.ReturnJourneyVerified = false,
            v => v.ReturnJourneyId = OtherJourneyId,
            v => v.ReturnLegMaximumGameTicks -= 1,
            v => v.RemainingJourneyGameTicks += 1,
            v => v.LockedReturnEnergyFloorJoules += 1d,
            v => v.LockedWithdrawalReserveJoules += 1d,
            v => v.LockedReturnCoreMinimumJoules += 1d,
            v => v.OriginalExtraWarperReserve += 1,
        };
        foreach (var change in changes)
        {
            var input = ReturnFacts();
            change(input);
            Assert.NotEqual(baseline, VoyagerRouteBudgetPolicy.Evaluate(input, ReturnRequest()).RouteStateHash);
        }
        var request = ReturnRequest();
        request.JourneyId = OtherJourneyId;
        Assert.NotEqual(baseline, VoyagerRouteBudgetPolicy.Evaluate(ReturnFacts(), request).RouteStateHash);
    }

    private const string JourneyId = "f82bf319-1ca1-49ab-a788-bdd60a0371d6";
    private const string OtherJourneyId = "44ff351b-0562-4e23-8585-0c3524d04641";

    private static PreviewVoyagerRouteRequest ReturnRequest() => new()
    {
        PlanetId = 201, DestinationPlanetId = 104, DestinationStarId = 1, JourneyId = JourneyId,
        ExpectedPlayerStateHash = "player", ExpectedCatalogStateHash = "catalogue",
    };

    private static VoyagerRouteBudgetInput ReturnFacts()
    {
        var outward = VoyagerRouteBudgetPolicy.Evaluate(Facts(), Request());
        var input = Facts();
        input.OriginPlanetId = 201;
        input.OriginStarId = 2;
        input.DestinationPlanetId = 104;
        input.DestinationStarId = 1;
        input.NativeWarperCount = 2;
        input.ReturnJourneyVerified = true;
        input.ReturnJourneyId = JourneyId;
        input.ReturnLegMaximumGameTicks = input.RemainingJourneyGameTicks = 18000;
        input.LockedReturnEnergyFloorJoules = outward.ReturnEnergyBudgetJoules + outward.WithdrawalReserveJoules;
        input.LockedWithdrawalReserveJoules = outward.WithdrawalReserveJoules;
        input.LockedReturnCoreMinimumJoules = outward.ReturnCoreEnergyMinimumJoules;
        input.OriginalExtraWarperReserve = 1;
        return input;
    }

    private static PreviewVoyagerRouteRequest Request() => new()
    {
        PlanetId = 104, DestinationPlanetId = 201, ExpectedPlayerStateHash = "player", ExpectedCatalogStateHash = "catalogue",
    };

    private static VoyagerRouteBudgetInput Facts() => new()
    {
        SessionId = "synthetic-owned-session", OriginPlanetId = 104, OriginStarId = 1,
        DestinationPlanetId = 201, DestinationStarId = 2, PlayerStateHash = "player", CatalogStateHash = "catalogue",
        DestinationVisible = true, DistanceMeters = 2400000d, IsAlive = true, IsGrounded = true, ControllerEnabled = true,
        PlayerOrderIdle = true, BuildUiIdle = true, ThrusterLevel = 3, MaxSailSpeed = 1000d, MaxWarpSpeed = 100000d,
        CoreCapacityJoules = 1e9d, CoreEnergyJoules = 1e9d, ReactorEnergyJoules = 1e6d, FuelStorageEnergyJoules = 1e12d,
        CoreGenerationLowerBoundWatts = 1e7d, WarpKeepingPowerPerSpeed = 0.01d, WarpStartPowerPerSpeed = 1d,
        EnergyConsumptionCoefficient = 1d, ThrustPowerPerAcceleration = 20d, NativeWarperCount = 3,
    };
}
