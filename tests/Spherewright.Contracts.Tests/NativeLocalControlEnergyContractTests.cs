using System.Text.Json;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class NativeLocalControlEnergyContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void OptionalConditionalCostIsDetachedAndNeverDeserializesExecutionAuthority()
    {
        Assert.Null(JsonSerializer.Deserialize<PlayerStateSnapshot>("{}", Options)!.NativeLocalControlEnergy);
        var energy = new NativeLocalControlEnergyObservation { PerNativeTickEnergyUpperJoules = 12000,
            CostInputsProved = true, IndependentDebitsExcluded = true, Blockers = new() { "native_paused_not_execution_ready" } };
        var json = JsonSerializer.Serialize(energy, Options);
        var copy = JsonSerializer.Deserialize<NativeLocalControlEnergyObservation>(json, Options)!;
        energy.Blockers.Clear();
        Assert.Equal("conditional_native_walk_drift_tick_upper_v1", copy.Basis);
        Assert.Equal(12000, copy.PerNativeTickEnergyUpperJoules); Assert.True(copy.CostInputsProved);
        Assert.Single(copy.Blockers); Assert.False(copy.ExecutionPremisesProved);
        Assert.False(JsonSerializer.Deserialize<NativeLocalControlEnergyObservation>("{\"executionPremisesProved\":true}", Options)!.ExecutionPremisesProved);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void NonFinitePHasNoValidStrictJsonRepresentation(double value) =>
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativeLocalControlEnergyObservation { PerNativeTickEnergyUpperJoules = value }, Options));
}
