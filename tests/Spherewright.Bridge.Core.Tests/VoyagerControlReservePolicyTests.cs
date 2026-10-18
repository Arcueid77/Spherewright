using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerControlReservePolicyTests
{
    [Theory]
    [InlineData(1035339d, false)]
    [InlineData(1035340d, true)]
    [InlineData(1035341d, true)]
    public void LoadedEnergyMustCoverOriginalFloorAndEveryControlExitAndMechaCost(double loaded, bool proved)
    {
        var facts = Facts(); facts.AvailableLoadedEnergyJoules = loaded;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.Equal(proved, result.Proved);
        Assert.Equal(29040d, result.NextControlAndBrakingEnergyUpperJoules);
        Assert.Equal(5100d, result.NativeExitEnergyUpperJoules);
        Assert.Equal(35340d, result.RequiredMarginJoules);
        Assert.Equal(proved ? null : "voyager_control_return_floor_insufficient", result.BlockerCode);
        Assert.Equal(1000000d, facts.ProtectedReturnFloorJoules);
    }

    [Fact]
    public void NativeThreefoldSailCostRejectsTheFormerOnefoldReserveCounterexample()
    {
        var facts = Facts();
        // A onefold sail estimate leaves 15980 J above home reserve; native
        // threefold steering/acceleration/braking actually needs 35340 J.
        facts.AvailableLoadedEnergyJoules = 1015980d;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.False(result.Proved); Assert.Equal(35340d, result.RequiredMarginJoules);
        Assert.True(VoyagerControlReservePolicy.CanSpendBeforeFloor(1035340d, 1000000d, 30240d, 5100d));
        Assert.False(VoyagerControlReservePolicy.CanSpendBeforeFloor(1015980d, 1000000d, 30240d, 5100d));
    }

    [Fact]
    public void HighNativeConsumptionAndEnergyScaleCannotBorrowTheReturnFloor()
    {
        var facts = Facts(); facts.EnergyConsumptionCoefficient = 6d; facts.NativeEnergyScale = 2d;
        facts.AvailableLoadedEnergyJoules = 1100000d;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.False(result.Proved);
        Assert.Equal(174240d, result.NextControlAndBrakingEnergyUpperJoules);
        Assert.Equal(30600d, result.NativeExitEnergyUpperJoules);
        Assert.Equal(206040d, result.RequiredMarginJoules);
        facts.AvailableLoadedEnergyJoules = 1206040d;
        Assert.True(VoyagerControlReservePolicy.Evaluate(facts).Proved);
    }

    [Fact]
    public void LowNativeScaleKeepsConservativeOrdinaryControlButUsesTheNativeExitScale()
    {
        var facts = Facts(); facts.NativeEnergyScale = 0.25d; facts.AvailableLoadedEnergyJoules = 1031515d;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.True(result.Proved);
        Assert.Equal(29040d, result.NextControlAndBrakingEnergyUpperJoules);
        Assert.Equal(1275d, result.NativeExitEnergyUpperJoules);
        Assert.Equal(31515d, result.RequiredMarginJoules);
        facts.AvailableLoadedEnergyJoules -= 1d;
        Assert.False(VoyagerControlReservePolicy.Evaluate(facts).Proved);
    }

    [Fact]
    public void AFullSpeedWarpTailNeedsAllSeventeenNativeAndControlLatencyTicks()
    {
        var facts = Facts(); facts.MaxWarpSpeed = 3000000d; facts.WarpStartPowerPerSpeed = 10d;
        facts.WarpKeepingPowerPerSpeed = 2d; facts.AvailableLoadedEnergyJoules = 21430240d;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.Equal(17, VoyagerControlReservePolicy.NativeExitReserveGameTicks);
        Assert.True(result.Proved); Assert.Equal(20400000d, result.NativeExitEnergyUpperJoules);
        Assert.Equal(20430240d, result.RequiredMarginJoules);
        facts.AvailableLoadedEnergyJoules -= 1d;
        Assert.False(VoyagerControlReservePolicy.Evaluate(facts).Proved);
        // Even zero ordinary speed does not erase a still-present warp tail.
        facts.OrdinarySpeedUpperBound = 0d; facts.AvailableLoadedEnergyJoules = 21403600d;
        result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.True(result.Proved); Assert.Equal(20400000d, result.NativeExitEnergyUpperJoules);
        Assert.Equal(20403600d, result.RequiredMarginJoules);
    }

    [Fact]
    public void OnlyProvedNativeExitRemovesTailEnergyWhilePreservingControlAndMechaReserves()
    {
        var facts = Facts(); facts.AvailableLoadedEnergyJoules = 1030240d;
        Assert.False(VoyagerControlReservePolicy.Evaluate(facts).Proved);
        facts.HasNativeWarpTail = false;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.True(result.Proved); Assert.Equal(0d, result.NativeExitEnergyUpperJoules);
        Assert.Equal(30240d, result.RequiredMarginJoules);
        facts.HasNativeWarpTail = null;
        AssertUncomputed(VoyagerControlReservePolicy.Evaluate(facts), "voyager_control_reserve_facts_unavailable");
    }

    [Fact]
    public void CopiedOtherMechaWorkRetainsThirtySecondsWithoutDoubleApplyingItsCoefficient()
    {
        var facts = Facts(); facts.ExtraMechaPowerUpperBoundWatts = 4000d;
        facts.AvailableLoadedEnergyJoules = 1154140d;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.Equal(30, VoyagerControlReservePolicy.ExtraMechaWorkReserveSeconds);
        Assert.True(result.Proved); Assert.Equal(154140d, result.RequiredMarginJoules);
        facts.AvailableLoadedEnergyJoules -= 1d;
        Assert.False(VoyagerControlReservePolicy.Evaluate(facts).Proved);
    }

    public static IEnumerable<object[]> MissingFacts()
    {
        foreach (var property in typeof(VoyagerControlReserveFacts).GetProperties())
            yield return new object[] { property.Name };
    }

    [Theory]
    [MemberData(nameof(MissingFacts))]
    public void MissingCopiedFactCannotAuthorizeSpendingOrBecomeAZeroCost(string field)
    {
        var facts = Facts(); typeof(VoyagerControlReserveFacts).GetProperty(field)!.SetValue(facts, null);
        AssertUncomputed(VoyagerControlReservePolicy.Evaluate(facts), "voyager_control_reserve_facts_unavailable");
    }

    public static IEnumerable<object[]> InvalidFacts()
    {
        foreach (var property in typeof(VoyagerControlReserveFacts).GetProperties().Where(p => p.PropertyType == typeof(double?)))
            foreach (var invalid in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                yield return new object[] { property.Name, invalid };
    }

    [Theory]
    [MemberData(nameof(InvalidFacts))]
    public void InvalidCopiedQuantityCannotManufactureAReserveProof(string field, double invalid)
    {
        var facts = Facts(); typeof(VoyagerControlReserveFacts).GetProperty(field)!.SetValue(facts, invalid);
        AssertUncomputed(VoyagerControlReservePolicy.Evaluate(facts), "voyager_control_reserve_facts_invalid");
    }

    [Theory]
    [InlineData("scale")]
    [InlineData("speed")]
    [InlineData("control_product")]
    [InlineData("warp_tail")]
    [InlineData("other_work")]
    [InlineData("combined_margin")]
    [InlineData("floor_plus_margin")]
    public void AnyOverflowRetainsUnknownBudgetRatherThanPartialZeroCredit(string caseName)
    {
        var facts = Facts(); facts.AvailableLoadedEnergyJoules = double.MaxValue;
        switch (caseName)
        {
            case "scale": facts.ThrustPowerPerAcceleration = double.MaxValue; break;
            case "speed": facts.OrdinarySpeedUpperBound = double.MaxValue; break;
            case "control_product": facts.ThrustPowerPerAcceleration = 1e200d; facts.OrdinarySpeedUpperBound = 1e200d; break;
            case "warp_tail": facts.MaxWarpSpeed = double.MaxValue; break;
            case "other_work": facts.ExtraMechaPowerUpperBoundWatts = double.MaxValue; break;
            case "combined_margin":
                facts.ThrustPowerPerAcceleration = 1.4e306d; facts.EnergyConsumptionCoefficient = 1d;
                facts.OrdinarySpeedUpperBound = 0d; facts.HasNativeWarpTail = false;
                facts.ExtraMechaPowerUpperBoundWatts = 5e306d;
                break;
            case "floor_plus_margin":
                facts.ThrustPowerPerAcceleration = 0d; facts.HasNativeWarpTail = false;
                facts.ExtraMechaPowerUpperBoundWatts = 3e306d; facts.ProtectedReturnFloorJoules = 1.2e308d;
                break;
        }
        AssertUncomputed(VoyagerControlReservePolicy.Evaluate(facts), "voyager_control_reserve_overflow");
    }

    [Theory]
    [InlineData(1000509d, 500d, 10d, false)]
    [InlineData(1000510d, 500d, 10d, true)]
    [InlineData(1000511d, 500d, 10d, true)]
    [InlineData(1000000d, 0d, 0d, true)]
    [InlineData(999999d, 0d, 0d, false)]
    public void PaidBrakingMayUseOnlySpareEnergyAfterTheFloorAndRemainingNativeTail(double loaded, double brake, double tail, bool allowed)
    {
        Assert.Equal(allowed, VoyagerControlReservePolicy.CanSpendBeforeFloor(loaded, 1000000d, brake, tail));
    }

    public static IEnumerable<object[]> InvalidPayments()
    {
        for (var argument = 0; argument < 4; argument++)
            foreach (var invalid in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                yield return new object[] { argument, invalid };
    }

    [Theory]
    [MemberData(nameof(InvalidPayments))]
    public void InvalidExactPaymentOrExitProofCannotSpendFromTheReturnFloor(int argument, double invalid)
    {
        var values = new[] { 2000000d, 1000000d, 500d, 10d }; values[argument] = invalid;
        Assert.False(VoyagerControlReservePolicy.CanSpendBeforeFloor(values[0], values[1], values[2], values[3]));
    }

    [Theory]
    [InlineData(0d, double.MaxValue, double.MaxValue)]
    [InlineData(double.MaxValue, double.MaxValue, 0d)]
    public void OverflowingPaymentTotalNeverBecomesAffordable(double floor, double cost, double tail)
    {
        Assert.False(VoyagerControlReservePolicy.CanSpendBeforeFloor(double.MaxValue, floor, cost, tail));
    }

    [Fact]
    public void ACostAbsorbedByFloatingPointFloorAdditionIsStillNotSpareEnergy()
    {
        Assert.Equal(double.MaxValue, double.MaxValue + 1d);
        Assert.False(VoyagerControlReservePolicy.CanSpendBeforeFloor(double.MaxValue, double.MaxValue, 1d, 0d));
        Assert.False(VoyagerControlReservePolicy.CanSpendBeforeFloor(double.MaxValue, double.MaxValue, 0d, 1d));
        var facts = Facts(); facts.AvailableLoadedEnergyJoules = facts.ProtectedReturnFloorJoules = double.MaxValue;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.False(result.Proved); Assert.Equal(35340d, result.RequiredMarginJoules);
    }

    [Fact]
    public void RepeatedControlsStopBeforeTheFloorButLeaveTheDeclaredFiniteBrakingRoom()
    {
        var facts = Facts(); facts.AvailableLoadedEnergyJoules = 1070680d;
        Assert.True(VoyagerControlReservePolicy.Evaluate(facts).Proved);
        facts.AvailableLoadedEnergyJoules -= 20000d;
        Assert.True(VoyagerControlReservePolicy.Evaluate(facts).Proved);
        facts.AvailableLoadedEnergyJoules -= 20000d;
        Assert.False(VoyagerControlReservePolicy.Evaluate(facts).Proved);

        var loaded = facts.AvailableLoadedEnergyJoules!.Value;
        Assert.True(VoyagerControlReservePolicy.CanSpendBeforeFloor(loaded, 1000000d, 18000d, 5100d));
        loaded -= 18000d;
        Assert.True(VoyagerControlReservePolicy.CanSpendBeforeFloor(loaded, 1000000d, 7580d, 5100d));
        loaded -= 7580d;
        Assert.Equal(1005100d, loaded);
        Assert.False(VoyagerControlReservePolicy.CanSpendBeforeFloor(loaded, 1000000d, 1d, 5100d));
        Assert.True(VoyagerControlReservePolicy.CanSpendBeforeFloor(loaded, 1000000d, 0d, 5100d));
        loaded -= 5100d; // Only the real native tail may debit this reserved part.
        Assert.Equal(1000000d, loaded);
        Assert.False(VoyagerControlReservePolicy.CanSpendBeforeFloor(loaded, 1000000d, 1d, 0d));
    }

    [Fact]
    public void KnownZeroCostsDifferFromMissingFactsAndNeedNoBorrowingAtTheFloor()
    {
        var facts = Facts(); facts.ThrustPowerPerAcceleration = 0d; facts.HasNativeWarpTail = false;
        facts.ExtraMechaPowerUpperBoundWatts = 0d; facts.AvailableLoadedEnergyJoules = 1000000d;
        var result = VoyagerControlReservePolicy.Evaluate(facts);
        Assert.True(result.Proved); Assert.Equal(0d, result.RequiredMarginJoules);
        Assert.Equal(0d, result.NextControlAndBrakingEnergyUpperJoules);
        Assert.Equal(0d, result.NativeExitEnergyUpperJoules);
        Assert.True(VoyagerControlReservePolicy.CanSpendBeforeFloor(0d, 0d, 0d, 0d));
    }

    [Fact]
    public void PureReadsDoNotRefreshLoadedEnergyOrLowerTheOriginalJourneyFloor()
    {
        var facts = Facts(); facts.AvailableLoadedEnergyJoules = 1035339d;
        var before = JsonSerializer.Serialize(facts);
        for (var i = 0; i < 8; i++) Assert.False(VoyagerControlReservePolicy.Evaluate(facts).Proved);
        Assert.Equal(before, JsonSerializer.Serialize(facts));
    }

    private static void AssertUncomputed(VoyagerControlReserveDecision result, string blocker)
    {
        Assert.False(result.Proved); Assert.Equal(blocker, result.BlockerCode);
        Assert.Null(result.RequiredMarginJoules); Assert.Null(result.NativeExitEnergyUpperJoules);
        Assert.Null(result.NextControlAndBrakingEnergyUpperJoules);
        Assert.DoesNotContain("NaN", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("Infinity", JsonSerializer.Serialize(result));
    }

    private static VoyagerControlReserveFacts Facts() => new()
    {
        AvailableLoadedEnergyJoules = 2000000d, ProtectedReturnFloorJoules = 1000000d,
        ThrustPowerPerAcceleration = 20d, EnergyConsumptionCoefficient = 2d, NativeEnergyScale = 1d,
        OrdinarySpeedUpperBound = 100d, WarpStartPowerPerSpeed = 1d, WarpKeepingPowerPerSpeed = 0.5d,
        MaxWarpSpeed = 6000d, HasNativeWarpTail = true, ExtraMechaPowerUpperBoundWatts = 40d,
    };
}
