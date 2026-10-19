using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeMechaRechargeCostPolicyTests
{
    [Fact]
    public void FuelSwapOrAutomaticBagFuelIsCoveredByTheWholeCatalogAndBothNativeIncTables()
    {
        var f = Facts();
        f.ItemCatalog = new NativeRechargeItemFacts?[] { Item(1, 0), Item(2, 5, true), Item(3, 8) };
        f.CurrentSelection = f.ItemCatalog[0]; f.CurrentReactorItemId = 1;
        var cost = NativeMechaRechargeCostPolicy.Evaluate(f); Assert.True(cost.Proved);
        foreach (var item in f.ItemCatalog)
        for (var inc = 0; inc <= 10; inc++)
        {
            var actualWeapon = NativeWeaponPower(f, item, inc);
            Assert.True(cost.WeaponPowerUpperWatts >= actualWeapon);
            // Native can consume the old chamber before CombatStats. Selecting
            // any new loaded or auto-bag fuel must still fit the original P.
            var actual = NativeStep(actualWeapon, 3, 1_000_000, 1_000_000, shieldMissing: 1_000_000, laserMissing: 1_000_000);
            Assert.True(cost.PerNativeTickEnergyUpperJoules >= actual.Debit);
        }
        Assert.True(cost.WeaponPowerUpperWatts > NativeWeaponPower(f, f.CurrentSelection, 0));
    }

    [Fact]
    public void CurrentSelectOutsideCatalogAndTheNoPrototypeHalfPowerFallbackAreBothIncluded()
    {
        var f = Facts(); f.CurrentSelection = Item(99, 20, true); f.CurrentReactorItemId = 99;
        var outside = NativeMechaRechargeCostPolicy.Evaluate(f);
        Assert.True(outside.Proved); Assert.True(outside.WeaponPowerUpperWatts >= NativeWeaponPower(f, f.CurrentSelection, 10));
        f.CurrentSelection = null; f.CurrentReactorItemId = 0; f.ItemCatalog = new[] { Item(1, -1) };
        var noPrototype = NativeMechaRechargeCostPolicy.Evaluate(f);
        Assert.True(noPrototype.Proved); Assert.True(noPrototype.WeaponPowerUpperWatts >= .5 * f.ReactorPowerGenerationWatts);
    }

    [Fact]
    public void NativeFloatAdditionHappensBeforeDoubleAndIncZeroDoesNotUseTableZero()
    {
        var f = Facts(); f.ReactorPowerGenerationWatts = 1;
        f.ItemCatalog = new[] { Item(1, 16777216f) }; f.CurrentSelection = f.ItemCatalog[0];
        f.IncTableMilli = f.AccTableMilli = new double[11];
        var d = NativeMechaRechargeCostPolicy.Evaluate(f);
        Assert.True(d.Proved);
        // float(2^24 + 1f) == 2^24; a double-first formula would add1.
        Assert.InRange(d.WeaponPowerUpperWatts!.Value, 16777216d, 16777217.001d);
        var g = Facts(); ((double[])g.IncTableMilli!)[0] = ((double[])g.AccTableMilli!)[0] = 1000;
        var unchanged = NativeMechaRechargeCostPolicy.Evaluate(g);
        Assert.True(unchanged.Proved);
        Assert.Equal(NativeMechaRechargeCostPolicy.Evaluate(Facts()).WeaponPowerUpperWatts, unchanged.WeaponPowerUpperWatts);
    }

    [Fact]
    public void FullBuffersDisabledSwitchesAndCooldownsDoNotGiveFreeFutureRecharge()
    {
        var f = Facts(); var paid = NativeMechaRechargeCostPolicy.Evaluate(f);
        Assert.True(paid.Proved); Assert.True(paid.PerNativeTickEnergyUpperJoules > 0);
        var full = NativeStep(paid.WeaponPowerUpperWatts!.Value, 2, 1e9, 1e9, 0, 0);
        Assert.Equal(0, full.Debit);
        var damaged = NativeStep(paid.WeaponPowerUpperWatts.Value, 3, 1e9, 1e9, 1_000_000_000L, 1_000_000_000L);
        Assert.True(damaged.Debit > 0); Assert.True(damaged.Debit <= paid.PerNativeTickEnergyUpperJoules);
        f.Background!.EnergyShieldRechargeEnabled = false; f.Background.LaserActive = false;
        Assert.Equal(paid.PerNativeTickEnergyUpperJoules, NativeMechaRechargeCostPolicy.Evaluate(f).PerNativeTickEnergyUpperJoules);
    }

    [Theory]
    [InlineData(.25)] [InlineData(1)] [InlineData(4)]
    public void RechargeIsAddedOnceAfterTheMovementConsumptionCoefficient(double coefficient)
    {
        var f = Facts(); var cost = NativeMechaRechargeCostPolicy.Evaluate(f);
        var move = Movement(f); move.ReactorConsumptionRatio = coefficient;
        var positive = VoyagerLocalWalkReservePolicy.Evaluate(move);
        Assert.True(positive.Proved); Assert.True(positive.BudgetedRecharge!.Proved);
        move.IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(); move.BoundedRechargeCosts = null;
        var zero = VoyagerLocalWalkReservePolicy.Evaluate(move); Assert.True(zero.Proved);
        Assert.InRange(positive.PerNativeTickEnergyJoules!.Value - zero.PerNativeTickEnergyJoules!.Value,
            cost.PerNativeTickEnergyUpperJoules!.Value - 1e-7, cost.PerNativeTickEnergyUpperJoules.Value + 1e-7);
        Assert.Equal(13 * positive.PerNativeTickEnergyJoules, positive.StopEnergyJoules);
    }

    [Theory]
    [InlineData("missing")] [InlineData("stability")] [InlineData("core_ratio")] [InlineData("capacity")]
    [InlineData("generation")] [InlineData("speed")] [InlineData("inc_range")] [InlineData("unobserved_select")]
    [InlineData("select_mismatch")] [InlineData("catalog_missing")] [InlineData("catalog_null_entry")]
    [InlineData("catalog_duplicate")] [InlineData("catalog_bound")] [InlineData("table_short")]
    [InlineData("table_nan")] [InlineData("table_negative")] [InlineData("table_bound")]
    [InlineData("proto_nan")] [InlineData("proto_factor_negative")] [InlineData("overflow")] [InlineData("unsafe_long")]
    public void UnknownMalformedOrUnstableMetadataNeverDefaultsToZero(string fault)
    {
        var f = Facts();
        switch (fault)
        {
            case "stability": f.StableCostInputs = false; break;
            case "core_ratio": f.CoreEnergyJoules = f.CoreCapacityJoules + 1; break;
            case "capacity": f.CoreCapacityJoules = 0; break;
            case "generation": f.ReactorPowerGenerationWatts = null; break;
            case "speed": f.ShieldRechargeSpeed = float.NaN; break;
            case "inc_range": f.CurrentReactorItemInc = 11; break;
            case "unobserved_select": f.CurrentSelectionObserved = false; break;
            case "select_mismatch": f.CurrentSelection = Item(99, 1); break;
            case "catalog_missing": f.ItemCatalog = null; break;
            case "catalog_null_entry": f.ItemCatalog = new NativeRechargeItemFacts?[] { null }; break;
            case "catalog_duplicate": f.ItemCatalog = new[] { Item(1, 0), Item(1, 0) }; break;
            case "catalog_bound": f.ItemCatalog = new NativeRechargeItemFacts[NativeMechaRechargeCostPolicy.MaximumItemCount + 1]; break;
            case "table_short": f.IncTableMilli = new double[10]; break;
            case "table_nan": ((double[])f.AccTableMilli!)[10] = double.NaN; break;
            case "table_negative": ((double[])f.IncTableMilli!)[0] = -1; break;
            case "table_bound": f.AccTableMilli = new double[65]; break;
            case "proto_nan": f.ItemCatalog = new[] { Item(1, float.NaN) }; break;
            case "proto_factor_negative": f.ItemCatalog = new[] { Item(1, -2) }; break;
            case "overflow": f.ReactorPowerGenerationWatts = double.MaxValue; break;
            case "unsafe_long": f.ReactorPowerGenerationWatts = 1e19; break;
        }
        var d = NativeMechaRechargeCostPolicy.Evaluate(fault == "missing" ? null : f);
        Assert.False(d.Proved); Assert.NotEmpty(d.BlockerCode!); Assert.Null(d.PerNativeTickEnergyUpperJoules);
    }

    [Theory]
    [InlineData("chamber")] [InlineData("storage")] [InlineData("rof")] [InlineData("ground")]
    [InlineData("space")] [InlineData("missing_ground")]
    public void BoundedRechargeDoesNotAdmitAmmoOrConfiguredFleets(string fault)
    {
        var f = Facts();
        if (fault == "chamber") f.Background!.AmmoItemId = 1601;
        if (fault == "storage") f.Background!.AmmoStorageEmptyAndUnfiltered = false;
        if (fault == "rof") f.Background!.AmmoRateOfFire = 1;
        if (fault == "ground") f.Background!.GroundFleetCount = 1;
        if (fault == "space") f.Background!.SpaceFleetCount = 1;
        if (fault == "missing_ground") f.Background!.GroundFleetCount = null;
        Assert.False(NativeMechaRechargeCostPolicy.Evaluate(f).Proved);
        Assert.False(VoyagerLocalWalkReservePolicy.Evaluate(Movement(f)).Proved);
    }

    [Fact]
    public void OriginalZeroCostPolicyAndOrdinaryClientsKeepTheirOriginalLimitedMeaning()
    {
        var original = NativeMechaBackgroundCostPolicyTests.Idle();
        Assert.True(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(original));
        original.LaserActive = true;
        Assert.False(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(original));
        var f = Movement(Facts()); f.BoundedRechargeCosts = null;
        Assert.False(VoyagerLocalWalkReservePolicy.Evaluate(f).Proved);
        f.IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle();
        Assert.True(VoyagerLocalWalkReservePolicy.Evaluate(f).Proved);
    }

    [Theory]
    [InlineData("chamber", "native_independent_ammo_chamber_unobserved")]
    [InlineData("storage", "native_independent_ammo_storage_unobserved")]
    [InlineData("rof", "native_independent_ammo_fire_cost_unobserved")]
    [InlineData("energy", "native_independent_ammo_fire_cost_unobserved")]
    [InlineData("ground", "native_independent_ground_fleet_unobserved")]
    [InlineData("space", "native_independent_space_fleet_unobserved")]
    public void MissingChildFactsNeverClaimAnObservedNonzeroOrNonemptyValue(string field, string expected)
    {
        var f = Facts();
        if (field == "chamber") f.Background!.AmmoItemId = null;
        if (field == "storage") f.Background!.AmmoStorageEmptyAndUnfiltered = null;
        if (field == "rof") f.Background!.AmmoRateOfFire = null;
        if (field == "energy") f.Background!.AmmoEnergyCost = null;
        if (field == "ground") f.Background!.GroundFleetCount = null;
        if (field == "space") f.Background!.SpaceFleetCount = null;
        Assert.Equal(expected, NativeMechaRechargeCostPolicy.Evaluate(f).BlockerCode);
    }

    [Fact]
    public void ShieldUnclampedCurrentTimesSixtyHasLongHeadroomAndImportedSpeedIsNotReplacedByThree()
    {
        var f = Facts(); f.ReactorPowerGenerationWatts = 1e15; f.ShieldRechargeSpeed = 4;
        var d = NativeMechaRechargeCostPolicy.Evaluate(f); Assert.True(d.Proved);
        var unrounded = d.WeaponPowerUpperWatts!.Value * VoyagerLocalWalkReservePolicy.NativeTickSeconds * 4;
        var displayCurrent = checked((long)(unrounded + .01) * 60L);
        Assert.True(displayCurrent > 0); Assert.True(d.ShieldRechargeEnergyPerNativeTickUpperJoules >= unrounded);
        f.ReactorPowerGenerationWatts = 1e18;
        Assert.Equal("native_independent_recharge_arithmetic_unproved", NativeMechaRechargeCostPolicy.Evaluate(f).BlockerCode);
    }

    [Fact]
    public void ConstructionOptInRequiresThisSamePaidBackgroundAndChargesBOnlyOnceOverTailPlusTwo()
    {
        var recharge = Facts(); var b = NativeMechaRechargeCostPolicy.Evaluate(recharge);
        var f = Drone(recharge); Assert.False(NativeConstructionDroneReservePolicy.Evaluate(f).Proved);
        f.AllowBudgetedIndependentRecharge = true;
        Assert.Equal("native_independent_recharge_not_in_background_budget", NativeConstructionDroneReservePolicy.Evaluate(f).BlockerCode);
        var addedPower = b.PerNativeTickEnergyUpperJoules!.Value * 60;
        f.OtherMechaPowerUpperWatts = 100 + addedPower;
        var positive = NativeConstructionDroneReservePolicy.Evaluate(f); Assert.True(positive.Proved);
        f.IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(); f.OtherMechaPowerUpperWatts = 100;
        f.AllowBudgetedIndependentRecharge = false; f.BoundedRechargeCosts = null;
        var legacy = NativeConstructionDroneReservePolicy.Evaluate(f); Assert.True(legacy.Proved);
        Assert.Equal(legacy.TailGameTicks, positive.TailGameTicks);
        var expected = b.PerNativeTickEnergyUpperJoules.Value * (positive.TailGameTicks + 2);
        Assert.InRange(positive.TailAndNextTickEnergyJoules - legacy.TailAndNextTickEnergyJoules, expected - .001, expected + .001);
        f = Drone(recharge); f.AllowBudgetedIndependentRecharge = true; f.OtherMechaPowerUpperWatts = addedPower;
        f.IndependentMechaCosts = Facts().Background;
        Assert.Equal("native_independent_cost_facts_mismatch", NativeConstructionDroneReservePolicy.Evaluate(f).BlockerCode);
        f = Drone(recharge); f.AllowBudgetedIndependentRecharge = true; f.OtherMechaPowerUpperWatts = addedPower;
        recharge.StableCostInputs = false;
        Assert.Equal("native_independent_cost_stability_unproved", NativeConstructionDroneReservePolicy.Evaluate(f).BlockerCode);
    }

    internal static NativeMechaRechargeFacts Facts()
    {
        var background = NativeMechaBackgroundCostPolicyTests.Idle();
        background.EnergyShieldCapacity = 100_000_000; background.LaserActive = true;
        return new() { Background = background, StableCostInputs = true, CoreEnergyJoules = 33_000_000,
            CoreCapacityJoules = 1_600_000_000, ReactorPowerGenerationWatts = 800_000, ShieldRechargeSpeed = 2,
            CurrentReactorItemId = 1, CurrentReactorItemInc = 0, CurrentSelectionObserved = true,
            CurrentSelection = Item(1, 0), ItemCatalog = new[] { Item(1, 0) },
            IncTableMilli = Enumerable.Range(0, 11).Select(i => i / 10d).ToArray(),
            AccTableMilli = Enumerable.Range(0, 11).Select(i => i / 5d).ToArray() };
    }
    private static NativeRechargeItemFacts Item(int id, float inc, bool productive = false) =>
        new() { ItemId = id, ReactorInc = inc, Productive = productive };
    private static double NativeWeaponPower(NativeMechaRechargeFacts f, NativeRechargeItemFacts? item, int inc)
    {
        if (item is null) return .5 * f.ReactorPowerGenerationWatts!.Value;
        double multiplier = item.ReactorInc + 1f;
        if (inc > 0) multiplier *= 1 + (item.Productive ? f.IncTableMilli![inc] : f.AccTableMilli![inc]);
        return f.ReactorPowerGenerationWatts!.Value * multiplier;
    }
    private static (double Debit, long Display) NativeStep(double weapon, float speed, double core, double capacity, long shieldMissing, long laserMissing)
    {
        var before = core; var ratio = core / capacity;
        var rawShield = weapon * VoyagerLocalWalkReservePolicy.NativeTickSeconds * ratio * speed;
        var shield = (long)(Math.Min(rawShield, Math.Min(shieldMissing, core)) + .01);
        if (shield > 0) core -= shield;
        var laser = (long)(Math.Min(weapon * VoyagerLocalWalkReservePolicy.NativeTickSeconds * Math.Min(ratio * .5 + .7, 1) * 1.8,
            Math.Min(laserMissing, core)) + .01);
        if (laser > 0) core -= laser;
        return (before - core, checked((long)(rawShield + .01) * 60));
    }
    private static VoyagerLocalWalkFacts Movement(NativeMechaRechargeFacts recharge) => new()
    {
        ModeTransitionsExcluded = true, WalkSpeed = 12, WalkPowerWatts = 1000, MiningPowerWatts = 200,
        OtherWorkPowerWatts = 100, ReactorConsumptionRatio = 1, RtsSpeed = 0, ManualSpeed = 0, SoftLandingRecover = 1,
        IncludePotentialWaterDrift = true, NativeThrustPowerPerAcceleration = 300, NativeGravityUpperBound = 50,
        IndependentMechaCosts = recharge.Background, BoundedRechargeCosts = recharge,
    };
    private static NativeConstructionDroneFacts Drone(NativeMechaRechargeFacts recharge) => new()
    {
        MaximumMechaDrones = 1, NativeSpeedMetersPerSecond = 6, NativeEnergyPerMeter = 1, NativeEjectEnergyJoules = 1,
        NativeRepairEnergyPerTick = 0, OtherMechaPowerUpperWatts = 100, AnchorRadiusMeters = 200,
        MaximumTargetDistanceMeters = 16, NativePendingJumpTicks = 0,
        IndependentMechaCosts = recharge.Background, BoundedRechargeCosts = recharge,
    };
}
