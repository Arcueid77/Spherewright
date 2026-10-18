using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerRouteBudgetPolicyTests
{
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
    public void HorizonEnergyUsesKeepingPowerForEveryReservedTickAndStartRampOnlyOncePerLeg()
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
        Assert.Equal(keeping * 600d, longer.RequiredEnergyJoules - shorter.RequiredEnergyJoules, precision: 5);
        Assert.Equal(shorter.WarpStartEnergyUpperBoundJoules, longer.WarpStartEnergyUpperBoundJoules);
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
    }

    [Fact]
    public void WarpBufferAndDeclaredLocalReserveNeverClaimQualifiedNewLandingControl()
    {
        var result = VoyagerRouteBudgetPolicy.Evaluate(Facts(), Request());
        Assert.Equal("warp_entries_and_finite_warp_ticks_only", result.CoreBufferScope);
        Assert.False(result.LocalFlightReserveIsControllerProof);
        Assert.Contains("local_reserves", result.BudgetBasis, StringComparison.Ordinal);
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
