using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class GeneratorColdStartFuelPolicyTests
{
    private static GeneratorColdStartFuelFacts Cold() => new()
    {
        BuildingItemId = 2211, RequestedCount = 8, PackageCount = 108, ExactNativeComponent = true,
        NativeFuelAdmitted = true, ZeroIncPackageFuel = true, EmptyPlayerHand = true, WithinBuildArea = true,
    };
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
            case "hot_fuel": f.CurrentFuelId = 1802; break;
            case "heat": f.LoadedFuelEnergy = 1; break;
        }
        Assert.NotNull(GeneratorColdStartFuelPolicy.Blocker(f));
    }
}
