using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class WindTurbineDismantlePolicyTests
{
    [Fact]
    public void CompletedOrdinaryWindSnapshotIsSupported()
    {
        var wind = WindSnapshot();
        Assert.True(WindTurbineDismantlePolicy.SupportsSnapshot(wind));
        wind.Connections.Add(new() { OtherObjectId = 99 });
        Assert.False(WindTurbineDismantlePolicy.SupportsSnapshot(wind));
    }

    [Theory]
    [InlineData(2201)]
    [InlineData(2204)]
    [InlineData(2205)]
    [InlineData(2211)]
    [InlineData(2310)]
    public void OtherBuildingTypesRequireTheirOwnAdmission(int itemId)
    {
        var wind = WindSnapshot();
        wind.ItemId = itemId;
        Assert.False(WindTurbineDismantlePolicy.SupportsSnapshot(wind));
    }

    [Fact]
    public void IsolatedGeneratorRemovalPreservesTheOtherNetwork()
    {
        var before = new WindPowerState
        {
            Nodes = new List<WindPowerNode> { Node(1, 1, 1, new[] { 10 }), Node(20, 2, 20, new[] { 200 }) },
            UnpoweredConsumerIds = new List<int> { 5 },
        };
        var fingerprint = WindTurbineDismantlePolicy.Fingerprint(before);
        var result = WindTurbineDismantlePolicy.Project(before, 1);
        Assert.Equal(new[] { 10 }, result.DisconnectedConsumerIds);
        Assert.Equal(new[] { 5, 10 }, result.UnpoweredConsumerIds);
        Assert.Equal(new[] { 20 }, Assert.Single(result.Components).NodeIds);
        Assert.Equal(fingerprint, WindTurbineDismantlePolicy.Fingerprint(before));
    }

    [Fact]
    public void BridgeRemovalProjectsEverySurvivingComponent()
    {
        var before = Bridge();
        var result = WindTurbineDismantlePolicy.Project(before, 1);
        Assert.Equal(3, result.Components.Count);
        Assert.Equal(new[] { 10 }, result.DisconnectedConsumerIds);
        Assert.Equal(new[] { 2 }, result.Components[0].NodeIds);
        Assert.Equal(new[] { 3 }, result.Components[1].NodeIds);
        Assert.Equal(new[] { 20 }, result.Components[2].NodeIds);
        Assert.All(result.Nodes, node => Assert.DoesNotContain(1, node.ConnectedNodeIds));
    }

    [Fact]
    public void SharedNeighborCoverageRetainsTheConsumer()
    {
        var before = Bridge();
        before.Nodes[1].ConsumerIds.Add(10);
        var result = WindTurbineDismantlePolicy.Project(before, 1);
        Assert.Empty(result.DisconnectedConsumerIds);
        Assert.Contains(10, result.Components[0].ConsumerIds);
    }

    [Fact]
    public void ReassignedNativeNetworkIdsPreserveProjectedConnectivity()
    {
        var expected = WindTurbineDismantlePolicy.Project(Bridge(), 1);
        var actual = new WindPowerState { Nodes = expected.Nodes, UnpoweredConsumerIds = expected.UnpoweredConsumerIds };
        actual.Nodes[0].NetworkId = 7;
        actual.Nodes[1].NetworkId = 8;
        Assert.True(WindTurbineDismantlePolicy.Matches(expected, actual));
        actual.Nodes[0].GeneratorId = 99;
        Assert.False(WindTurbineDismantlePolicy.Matches(expected, actual));
    }

    [Theory]
    [InlineData("dangling")]
    [InlineData("nonreciprocal")]
    [InlineData("duplicate")]
    [InlineData("cross_network")]
    [InlineData("disconnected_network")]
    public void MalformedPowerTopologyIsRejectedBeforeRemoval(string mutation)
    {
        var before = Bridge();
        switch (mutation)
        {
            case "dangling": before.Nodes[0].ConnectedNodeIds.Add(999); break;
            case "nonreciprocal": before.Nodes[1].ConnectedNodeIds.Clear(); break;
            case "duplicate": before.Nodes.Add(before.Nodes[0]); break;
            case "cross_network": before.Nodes[1].NetworkId = 2; break;
            case "disconnected_network": before.Nodes[3].NetworkId = 1; break;
        }
        Assert.Throws<ArgumentException>(() => WindTurbineDismantlePolicy.Project(before, 1));
    }

    [Fact]
    public void AmbiguousConsumerAssignmentAfterSplitIsRejected()
    {
        var before = Bridge();
        before.Nodes[2].ConsumerIds.Add(11);
        Assert.Throws<ArgumentException>(() => WindTurbineDismantlePolicy.Project(before, 1));
    }

    [Fact]
    public void NativeNeighborUnlinkRuleIsProvedBeforeRemoval()
    {
        var before = Bridge();
        before.Nodes[0].ConnectedNodeIds.Remove(3);
        before.Nodes[2].ConnectedNodeIds.Remove(1);
        before.Nodes[1].ConnectedNodeIds.Add(3);
        before.Nodes[2].ConnectedNodeIds.Add(2);
        before.Nodes[2].ConsumerIds.Add(10);
        Assert.Throws<ArgumentException>(() => WindTurbineDismantlePolicy.Project(before, 1));
    }

    [Fact]
    public void LostConsumerOrSurvivingNodeCannotPassReadback()
    {
        var expected = WindTurbineDismantlePolicy.Project(Bridge(), 1);
        var actual = new WindPowerState { Nodes = expected.Nodes, UnpoweredConsumerIds = new List<int>() };
        actual.Nodes[0].NetworkId = 7;
        actual.Nodes[1].NetworkId = 8;
        Assert.False(WindTurbineDismantlePolicy.Matches(expected, actual));
        actual.UnpoweredConsumerIds = expected.UnpoweredConsumerIds;
        actual.Nodes = actual.Nodes.Where(node => node.NodeId != 20).ToList();
        Assert.False(WindTurbineDismantlePolicy.Matches(expected, actual));
    }

    [Theory]
    [InlineData("entity")]
    [InlineData("generator")]
    [InlineData("network")]
    [InlineData("consumer")]
    [InlineData("unpowered")]
    [InlineData("connection")]
    public void EveryPowerTopologyFieldChangesThePreparedFingerprint(string field)
    {
        var state = Bridge();
        var before = WindTurbineDismantlePolicy.Fingerprint(state);
        switch (field)
        {
            case "entity": state.Nodes[0].EntityId++; break;
            case "generator": state.Nodes[0].GeneratorId++; break;
            case "network": state.Nodes[0].NetworkId++; break;
            case "consumer": state.Nodes[0].ConsumerIds.Add(777); break;
            case "unpowered": state.UnpoweredConsumerIds.Add(777); break;
            case "connection": state.Nodes[0].ConnectedNodeIds.Remove(2); break;
        }
        Assert.NotEqual(before, WindTurbineDismantlePolicy.Fingerprint(state));
    }

    private static FactoryEntitySnapshot WindSnapshot() => new FactoryEntitySnapshot
    {
        ObjectId = 101, ObjectKind = FactoryObjectKinds.Entity, ItemId = 2203, ComponentKind = "power-generator",
    };

    private static WindPowerState Bridge() => new WindPowerState
    {
        Nodes = new List<WindPowerNode>
        {
            Node(1, 1, 1, new[] { 10 }, 2, 3),
            Node(2, 1, 2, new[] { 11 }, 1),
            Node(3, 1, 3, new[] { 12 }, 1),
            Node(20, 2, 20, new[] { 200 }),
        },
        UnpoweredConsumerIds = new List<int> { 5 },
    };

    private static WindPowerNode Node(int id, int network, int generator, IEnumerable<int> consumers, params int[] connections) => new WindPowerNode
    {
        NodeId = id, EntityId = 1000 + id, NetworkId = network, GeneratorId = generator,
        ConsumerIds = consumers.ToList(), ConnectedNodeIds = connections.ToList(),
    };
}
