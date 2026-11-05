using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class GeneratorColdStartFuelPolicyTests
{
    private static GeneratorColdStartFuelFacts Cold() => new()
    {
        BuildingItemId = 2211, RequestedCount = 8, RequestedFuelItemId = 1802, PackageCount = 108, ExactNativeComponent = true,
        NativeFuelAdmitted = true, ZeroIncPackageFuel = true, EmptyPlayerHand = true, WithinBuildArea = true,
    };
    [Fact]
    public void CapturedExhaustedNativeGeneratorRetainsItsLastFuelIdentityWithoutRetainingHeatOrGeneration()
    {
        var observed = Spherewright.Bridge.Core.Factory.FuelPowerStatePolicy.Capture(2211, 2,
            0, 0, 0, 0, 1802, 0, 0, true, 250000, 250000, 0, 0);
        Assert.Equal("observed", observed.State);
        Assert.Equal(1802, observed.LoadedFuelItemId); Assert.True(observed.LoadedFuelProductive);
        var f = Cold(); f.CurrentFuelId = observed.LoadedFuelItemId!.Value;
        f.LoadedFuelEnergy = observed.LoadedFuelEnergyJoules!.Value;
        f.BufferedFuelHeat = observed.BufferedFuelHeatPerItemJoules!.Value;
        f.CapacityEnergyPerTick = observed.CapacityEnergyPerTick!.Value;
        f.GeneratedEnergyPerTick = observed.GeneratedEnergyPerTick!.Value;
        Assert.Null(GeneratorColdStartFuelPolicy.Blocker(f));
        Assert.Equal(1802, f.CurrentFuelId); // Admission never clears Native identity.
        Assert.Equal(0, f.LoadedFuelEnergy); Assert.Equal(0, f.BufferedFuelCount);
    }
    [Theory]
    [InlineData(2204)][InlineData(2211)]
    public void ColdNormalFuelGeneratorCanAcceptAnExactBoundedPackageTransfer(int item)
    {
        var f = Cold(); f.BuildingItemId = item;
        Assert.Null(GeneratorColdStartFuelPolicy.Blocker(f));
        Assert.Equal(108, f.PackageCount); Assert.Equal(0, f.LoadedFuelEnergy);
    }
    [Theory]
    [InlineData("wrong_generator")][InlineData("component")][InlineData("fuel")][InlineData("inc")]
    [InlineData("hand")][InlineData("range")][InlineData("shortage")][InlineData("count0")][InlineData("count11")]
    [InlineData("old_fuel")][InlineData("buffer")][InlineData("buffer_inc")][InlineData("hot_fuel")][InlineData("heat")]
    [InlineData("wrong_retained_fuel")][InlineData("negative_retained_fuel")][InlineData("buffer_heat")]
    [InlineData("capacity")][InlineData("generation")][InlineData("missing_requested_fuel")]
    public void AColdStartCannotBorrowAnotherGeneratorOrAHotOrUnprovedSource(string fault)
    {
        var f = Cold();
        switch (fault)
        {
            case "wrong_generator": f.BuildingItemId = 2203; break;
            case "component": f.ExactNativeComponent = false; break;
            case "fuel": f.NativeFuelAdmitted = false; break;
            case "inc": f.ZeroIncPackageFuel = false; break;
            case "hand": f.EmptyPlayerHand = false; break;
            case "range": f.WithinBuildArea = false; break;
            case "shortage": f.PackageCount = 7; break;
            case "count0": f.RequestedCount = 0; break;
            case "count11": f.RequestedCount = 11; break;
            case "old_fuel": f.BufferedFuelId = 1802; break;
            case "buffer": f.BufferedFuelCount = 1; break;
            case "buffer_inc": f.BufferedFuelInc = 1; break;
            case "hot_fuel": f.CurrentFuelId = 1802; f.LoadedFuelEnergy = 1; break;
            case "heat": f.LoadedFuelEnergy = 1; break;
            case "wrong_retained_fuel": f.CurrentFuelId = 1801; break;
            case "negative_retained_fuel": f.CurrentFuelId = -1; break;
            case "buffer_heat": f.BufferedFuelHeat = 1; break;
            case "capacity": f.CapacityEnergyPerTick = 1; break;
            case "generation": f.GeneratedEnergyPerTick = 1; break;
            case "missing_requested_fuel": f.RequestedFuelItemId = 0; break;
        }
        Assert.NotNull(GeneratorColdStartFuelPolicy.Blocker(f));
    }
}
