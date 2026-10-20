using System.Text.Json;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class ConstructionDroneNativeObservationContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void LegacyAndUnavailableObservationsDoNotInventNativeZeros()
    {
        var old = JsonSerializer.Deserialize<ConstructionDroneSnapshot>("{\"total\":3,\"idle\":3}", Options)!;
        Assert.Equal(3, old.Total);
        Assert.Null(old.NativeSpeedMetersPerSecond);
        Assert.Null(old.NativeEnergyPerMeterJoules);
        Assert.Null(old.NativeEjectEnergyJoules);
        Assert.Null(old.NativeRepairEnergyPerTickJoules);
        Assert.Null(old.NativePendingJumpTicks);
        var json = JsonSerializer.Serialize(old, Options);
        Assert.Contains("\"nativeSpeedMetersPerSecond\":null", json);
        Assert.Contains("\"nativePendingJumpTicks\":null", json);
    }

    [Fact]
    public void NativeUnitsAndObservedZeroRoundTripAsDetachedValues()
    {
        var native = new ConstructionDroneSnapshot
        {
            Total = 3, NativeSpeedMetersPerSecond = 20,
            NativeEnergyPerMeterJoules = 15000, NativeEjectEnergyJoules = 100000,
            NativeRepairEnergyPerTickJoules = 8000, NativePendingJumpTicks = 0,
        };
        var json = JsonSerializer.Serialize(native, Options);
        var copied = JsonSerializer.Deserialize<ConstructionDroneSnapshot>(json, Options)!;
        Assert.Equal(json, JsonSerializer.Serialize(copied, Options));
        Assert.Equal(20d, copied.NativeSpeedMetersPerSecond);
        Assert.Equal(15000d, copied.NativeEnergyPerMeterJoules);
        Assert.Equal(100000d, copied.NativeEjectEnergyJoules);
        Assert.Equal(8000d, copied.NativeRepairEnergyPerTickJoules);
        Assert.Equal(480000d, copied.NativeRepairEnergyPerTickJoules!.Value * 60d);
        Assert.Equal(0, copied.NativePendingJumpTicks);
        native.NativeRepairEnergyPerTickJoules = null;
        native.NativePendingJumpTicks = 5;
        Assert.Equal(8000d, copied.NativeRepairEnergyPerTickJoules);
        Assert.Equal(0, copied.NativePendingJumpTicks);
    }

    [Fact]
    public void NonfiniteNativeCostsCannotCrossStrictJson()
    {
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new ConstructionDroneSnapshot
            { NativeSpeedMetersPerSecond = double.NaN }, Options));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new ConstructionDroneSnapshot
            { NativeEnergyPerMeterJoules = double.PositiveInfinity }, Options));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new ConstructionDroneSnapshot
            { NativeEjectEnergyJoules = double.NegativeInfinity }, Options));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new ConstructionDroneSnapshot
            { NativeRepairEnergyPerTickJoules = double.NaN }, Options));
    }
}
