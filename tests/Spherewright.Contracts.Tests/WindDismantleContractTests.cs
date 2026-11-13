using System.Text.Json;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class WindDismantleContractTests
{
    [Fact]
    public void LegacyDismantleRequestsRetainExecutablePreparationDefaults()
    {
        var request = JsonSerializer.Deserialize<PrepareDismantleRequest>("{\"ObjectId\":31,\"PlanetId\":102}");
        Assert.NotNull(request);
        Assert.False(request.PreviewOnly);
        Assert.Equal(31, request.ObjectId);
    }

    [Fact]
    public void LegacyActionResultsHaveAnAbsentWindReadback()
    {
        var result = JsonSerializer.Deserialize<ActionResultSnapshot>("{\"ActionKind\":\"dismantle\",\"Terminal\":true,\"Succeeded\":true}");
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Null(result.WindTurbineDismantleReadback);
    }

    [Fact]
    public void PowerImpactAndNativeRecoverySurviveContractSerialization()
    {
        var value = new ActionResultSnapshot
        {
            ActionKind = NormalActionKinds.Dismantle,
            WindTurbineDismantleReadback = new WindTurbineDismantleReadback
            {
                TargetEntityId = 31, TargetAbsent = true, ExactBuildingRecovery = true,
                PowerConnectivityVerified = true, UnrelatedPowerNetworksPreserved = true,
                PreparedPowerTopologyHash = "sha256:prepared", ResultPowerTopologyHash = "sha256:observed",
                DisconnectedConsumerEntityIds = new List<int> { 45 },
                ResultNetworks = new List<WindPowerNetworkSnapshot>
                {
                    new WindPowerNetworkSnapshot { NativeNetworkId = 7, NodeEntityIds = new List<int> { 32, 33 } },
                },
            },
        };
        var copy = JsonSerializer.Deserialize<ActionResultSnapshot>(JsonSerializer.Serialize(value));
        var proof = Assert.IsType<WindTurbineDismantleReadback>(copy?.WindTurbineDismantleReadback);
        Assert.True(proof.ExactBuildingRecovery);
        Assert.True(proof.UnrelatedPowerNetworksPreserved);
        Assert.Equal(new[] { 45 }, proof.DisconnectedConsumerEntityIds);
        Assert.Equal(7, Assert.Single(proof.ResultNetworks).NativeNetworkId);
        Assert.Equal(new[] { 32, 33 }, proof.ResultNetworks[0].NodeEntityIds);
    }
}
