using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeMechaBackgroundCostPolicyTests
{
    [Theory]
    [InlineData("shield_charging")] [InlineData("laser_charging")] [InlineData("old_ammo_chamber")]
    [InlineData("filtered_ammo")] [InlineData("old_round_rof")] [InlineData("old_round_cost")]
    [InlineData("ground_fleet")] [InlineData("space_fleet")]
    public void WalkAndEmptyReplicatorDoNotExcludeIndependentNativeCombatDebits(string branch)
    {
        var f = Idle();
        switch (branch)
        {
            case "shield_charging": f.EnergyShieldCapacity = 1000; break;
            case "laser_charging": f.LaserActive = true; break;
            case "old_ammo_chamber": f.AmmoItemId = 1601; break;
            case "filtered_ammo": f.AmmoStorageEmptyAndUnfiltered = false; break;
            case "old_round_rof": f.AmmoRateOfFire = 60; break;
            case "old_round_cost": f.AmmoEnergyCost = 1000; break;
            case "ground_fleet": f.GroundFleetCount = 1; break;
            case "space_fleet": f.SpaceFleetCount = 1; break;
        }
        Assert.False(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f));
    }

    [Theory]
    [InlineData("shield_capacity")] [InlineData("shield_switch")] [InlineData("laser")]
    [InlineData("ammo_chamber")] [InlineData("ammo_slot")] [InlineData("ammo_rof")]
    [InlineData("ammo_cost")] [InlineData("ammo_storage")] [InlineData("ground_fleet")] [InlineData("space_fleet")]
    public void MissingIndependentCostEvidenceDoesNotBecomeZeroOrDisabled(string missing)
    {
        var f = Idle();
        switch (missing)
        {
            case "shield_capacity": f.EnergyShieldCapacity = null; break;
            case "shield_switch": f.EnergyShieldRechargeEnabled = null; break;
            case "laser": f.LaserActive = null; break;
            case "ammo_chamber": f.AmmoItemId = null; break;
            case "ammo_slot": f.AmmoSelectSlot = null; break;
            case "ammo_rof": f.AmmoRateOfFire = null; break;
            case "ammo_cost": f.AmmoEnergyCost = null; break;
            case "ammo_storage": f.AmmoStorageEmptyAndUnfiltered = null; break;
            case "ground_fleet": f.GroundFleetCount = null; break;
            case "space_fleet": f.SpaceFleetCount = null; break;
        }
        Assert.False(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f));
        Assert.False(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(null));
    }

    [Theory]
    [InlineData(0L, true)] [InlineData(0L, false)] [InlineData(1000L, false)]
    public void OnlyCapacityZeroOrAnAlreadyDisabledRechargeBranchExcludesShieldDebit(long capacity, bool enabled)
    {
        var f = Idle(); f.EnergyShieldCapacity = capacity; f.EnergyShieldRechargeEnabled = enabled;
        Assert.True(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f));
        Assert.Equal(enabled, f.EnergyShieldRechargeEnabled); // Policy never toggles Native/user settings.
    }

    [Fact]
    public void AFullShieldCannotRenewALocalCertificateAfterDamageOrAWeaponSelection()
    {
        var f = Idle();
        f.EnergyShieldCapacity = 1_000_000; // Native recharge remains enabled, regardless of current shield energy.
        Assert.False(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f));
        f.EnergyShieldRechargeEnabled = false;
        Assert.True(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f));
        f.AmmoItemId = 1601; // Slot may remain zero while the old chamber survives.
        Assert.False(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void AnActuallyEmptyUnfilteredNativeSlotNeedsNoSwitchChangeToHaveZeroFireCost(int selectedSlot)
    {
        var f = Idle(); f.AmmoSelectSlot = selectedSlot;
        Assert.True(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f));
        Assert.Equal(selectedSlot, f.AmmoSelectSlot);
        // Native ROF0 returns before AmmoFireProcedure can LoadAmmo. If a
        // future filter or item appears, the same certificate immediately ends.
        f.AmmoStorageEmptyAndUnfiltered = false;
        Assert.False(NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f));
    }

    [Theory]
    [InlineData("shield")] [InlineData("laser")] [InlineData("ammo")]
    public void IndependentNativeDebitCannotSpendTheFloorBehindAValidOrdinaryWalkOrDroneBudget(string branch)
    {
        const double ordinaryMargin = 1000;
        var independent = Idle();
        double nativeDebit;
        if (branch == "shield")
        {
            independent.EnergyShieldCapacity = 1_000_000;
            nativeDebit = 600_000d * (1f / 60f) * 1d * 1d; // Weapon reactor power * dt * core ratio * recharge speed.
        }
        else if (branch == "laser")
        {
            independent.LaserActive = true;
            nativeDebit = 600_000d * (1f / 60f) * 1d * 1.8d;
        }
        else
        {
            independent.AmmoItemId = 1601; independent.AmmoRateOfFire = 120; independent.AmmoEnergyCost = 20_000;
            nativeDebit = 20_000; // TickAmmo's paid original round is independent of Walk/forge state.
        }
        Assert.True(nativeDebit > ordinaryMargin);
        var walk = VoyagerLocalWalkReservePolicy.Evaluate(new VoyagerLocalWalkFacts
        {
            ModeTransitionsExcluded = true, WalkSpeed = 6, WalkPowerWatts = 100, MiningPowerWatts = 0,
            OtherWorkPowerWatts = 0, ReactorConsumptionRatio = 1, RtsSpeed = 0, ManualSpeed = 0,
            SoftLandingRecover = 1, IndependentMechaCosts = independent,
        });
        var construction = NativeConstructionDroneReservePolicy.Evaluate(new NativeConstructionDroneFacts
        {
            MaximumMechaDrones = 1, NativeSpeedMetersPerSecond = 6, NativeEnergyPerMeter = 1,
            NativeEjectEnergyJoules = 1, NativeRepairEnergyPerTick = 0, OtherMechaPowerUpperWatts = 0,
            AnchorRadiusMeters = 200, MaximumTargetDistanceMeters = 16, NativePendingJumpTicks = 0,
            IndependentMechaCosts = independent,
        });
        Assert.False(walk.Proved); Assert.Null(walk.PerNativeTickEnergyJoules);
        Assert.False(construction.Proved);
    }

    internal static NativeMechaBackgroundCostFacts Idle() => new()
    {
        EnergyShieldCapacity = 0, EnergyShieldRechargeEnabled = true, LaserActive = false,
        AmmoItemId = 0, AmmoSelectSlot = 0, AmmoRateOfFire = 0, AmmoEnergyCost = 0,
        AmmoStorageEmptyAndUnfiltered = true,
        GroundFleetCount = 0, SpaceFleetCount = 0,
    };
}
