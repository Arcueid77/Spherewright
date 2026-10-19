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
        var energy = new NativeLocalControlEnergyObservation { Basis = "conditional_native_walk_drift_tick_upper_v2", PerNativeTickEnergyUpperJoules = 12000,
            CostInputsProved = true, IndependentDebitsExcluded = false, IndependentDebitsBudgeted = true,
            IndependentEnergyPerNativeTickUpperJoules = 500, ShieldRechargeEnergyPerNativeTickUpperJoules = 300,
            LaserRechargeEnergyPerNativeTickUpperJoules = 200, Blockers = new() { "native_paused_not_execution_ready" } };
        var json = JsonSerializer.Serialize(energy, Options);
        var copy = JsonSerializer.Deserialize<NativeLocalControlEnergyObservation>(json, Options)!;
        energy.Blockers.Clear();
        Assert.Equal("conditional_native_walk_drift_tick_upper_v2", copy.Basis);
        Assert.Equal(12000, copy.PerNativeTickEnergyUpperJoules); Assert.True(copy.CostInputsProved);
        Assert.False(copy.IndependentDebitsExcluded); Assert.True(copy.IndependentDebitsBudgeted);
        Assert.Equal(500, copy.IndependentEnergyPerNativeTickUpperJoules);
        Assert.Equal(300, copy.ShieldRechargeEnergyPerNativeTickUpperJoules); Assert.Equal(200, copy.LaserRechargeEnergyPerNativeTickUpperJoules);
        Assert.Single(copy.Blockers); Assert.False(copy.ExecutionPremisesProved);
        Assert.False(JsonSerializer.Deserialize<NativeLocalControlEnergyObservation>("{\"executionPremisesProved\":true}", Options)!.ExecutionPremisesProved);
        var legacy = JsonSerializer.Deserialize<NativeLocalControlEnergyObservation>("{\"independentDebitsExcluded\":true}", Options)!;
        Assert.True(legacy.IndependentDebitsExcluded); Assert.False(legacy.IndependentDebitsBudgeted);
        Assert.Equal("conditional_native_walk_drift_tick_upper_v1", legacy.Basis); Assert.Null(legacy.IndependentEnergyPerNativeTickUpperJoules);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void NonFinitePHasNoValidStrictJsonRepresentation(double value) =>
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativeLocalControlEnergyObservation { PerNativeTickEnergyUpperJoules = value }, Options));

    [Theory]
    [InlineData("sum")] [InlineData("shield")] [InlineData("laser")]
    public void IndependentContributionsAlsoRemainFiniteJsonObservations(string field)
    {
        var energy = new NativeLocalControlEnergyObservation { IndependentDebitsBudgeted = true };
        if (field == "sum") energy.IndependentEnergyPerNativeTickUpperJoules = double.NaN;
        if (field == "shield") energy.ShieldRechargeEnergyPerNativeTickUpperJoules = double.NaN;
        if (field == "laser") energy.LaserRechargeEnergyPerNativeTickUpperJoules = double.NaN;
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(energy, Options));
    }
}
