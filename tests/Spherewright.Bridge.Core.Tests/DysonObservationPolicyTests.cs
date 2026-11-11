using Spherewright.Bridge.Core.Celestial;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class DysonObservationPolicyTests
{
    [Fact]
    public void UnavailableCoverageKeepsTotalsUnknown()
    {
        var snapshot = new DysonStateSnapshot();
        DysonObservationPolicy.Complete(snapshot);
        Assert.Null(snapshot.StructurePoints);
        Assert.Null(snapshot.MaximumCellPoints);
        Assert.Null(snapshot.ReceiverGeneratedPowerWatts);
        Assert.Empty(snapshot.DesignStateHash);
        Assert.True(snapshot.ReadOnly);
        Assert.False(snapshot.Executable);
        Assert.False(snapshot.CommitAllowedNow);
    }

    [Fact]
    public void CompleteTopologyCountsEachFrameAndShellOnce()
    {
        var snapshot = ValidSnapshot();
        DysonObservationPolicy.Complete(snapshot);
        Assert.True(snapshot.StructureCoverageComplete);
        Assert.Equal(45L, snapshot.StructurePoints);
        Assert.Equal(150L, snapshot.MaximumStructurePoints);
        Assert.Equal(3L, snapshot.OrderedStructurePoints);
        Assert.Equal(7L, snapshot.CellPoints);
        Assert.Equal(40L, snapshot.MaximumCellPoints);
        Assert.Equal(250L, snapshot.ReceiverGeneratedEnergyPerTick);
        Assert.Equal(15000d, snapshot.ReceiverGeneratedPowerWatts);
        Assert.NotEmpty(snapshot.DesignStateHash);
    }

    [Fact]
    public void IncompleteStructurePreservesIndependentReceiverEvidence()
    {
        var snapshot = ValidSnapshot();
        snapshot.StructureCoverageComplete = false;
        DysonObservationPolicy.Complete(snapshot);
        Assert.Null(snapshot.StructurePoints);
        Assert.Empty(snapshot.DesignStateHash);
        Assert.Equal(15000d, snapshot.ReceiverGeneratedPowerWatts);
    }

    [Fact]
    public void PhotonCapacityDoesNotBecomeGeneratedElectricity()
    {
        var snapshot = ValidSnapshot();
        var receiver = snapshot.Receivers[0];
        receiver.ProductItemId = 1208;
        receiver.ProductCount = 20;
        receiver.GeneratedEnergyPerTick = 0;
        DysonObservationPolicy.Complete(snapshot);
        Assert.Equal("photon", receiver.Mode);
        Assert.True(snapshot.ReceiverCoverageComplete);
        Assert.Equal(0d, snapshot.ReceiverGeneratedPowerWatts);
        Assert.Equal(1000, receiver.CapacityEnergyPerTick);
    }

    [Fact]
    public void ElectricityModeAcceptsAnUnusedZeroPhotonHeatField()
    {
        var snapshot = ValidSnapshot();
        snapshot.Receivers[0].ProductHeat = 0;
        DysonObservationPolicy.Complete(snapshot);
        Assert.True(snapshot.ReceiverCoverageComplete);
        Assert.Equal(15000d, snapshot.ReceiverGeneratedPowerWatts);
    }

    [Fact]
    public void AnObservedZeroCellShellKeepsItsExactZeroTotal()
    {
        var snapshot = ValidSnapshot();
        snapshot.Layers[0].Shells[0].CellPoints = 0;
        snapshot.Layers[0].Shells[0].MaximumCellPoints = 0;
        DysonObservationPolicy.Complete(snapshot);
        Assert.True(snapshot.StructureCoverageComplete);
        Assert.Equal(0L, snapshot.MaximumCellPoints);
    }

    [Fact]
    public void ProgressAndTickChangesPreserveTheDesignHash()
    {
        var snapshot = ValidSnapshot();
        DysonObservationPolicy.Complete(snapshot);
        var hash = snapshot.DesignStateHash;
        snapshot.Layers[0].Nodes[0].StructurePoints++;
        snapshot.Layers[0].Frames[0].StructurePointsA++;
        snapshot.Layers[0].Shells[0].CellPoints++;
        snapshot.CapturedAtGameTick++;
        snapshot.Receivers[0].CapturedAtGameTick++;
        snapshot.Receivers[0].GeneratedEnergyPerTick++;
        DysonObservationPolicy.Complete(snapshot);
        Assert.Equal(hash, snapshot.DesignStateHash);
        snapshot.Layers[0].Nodes[0].Position.X++;
        DysonObservationPolicy.Complete(snapshot);
        Assert.NotEqual(hash, snapshot.DesignStateHash);
    }

    [Theory]
    [InlineData("session")] [InlineData("star")] [InlineData("layer")] [InlineData("radius")]
    public void DesignHashBindsItsSourceAndScope(string change)
    {
        var snapshot = ValidSnapshot();
        DysonObservationPolicy.Complete(snapshot);
        var hash = snapshot.DesignStateHash;
        switch (change)
        {
            case "session": snapshot.SessionId = "another-synthetic-session"; break;
            case "star": snapshot.StarId++; snapshot.Receivers[0].StarId++; break;
            case "layer": snapshot.SelectedLayerId = 1; snapshot.SelectedLayerPresent = true; break;
            case "radius": snapshot.Layers[0].OrbitRadius++; break;
        }
        DysonObservationPolicy.Complete(snapshot);
        Assert.NotEqual(hash, snapshot.DesignStateHash);
    }

    [Theory]
    [InlineData("duplicate_node")] [InlineData("duplicate_frame")] [InlineData("duplicate_edge")] [InlineData("dangling_frame")]
    [InlineData("open_shell")] [InlineData("duplicate_shell")] [InlineData("negative_progress")]
    [InlineData("excess_progress")] [InlineData("nonfinite_position")] [InlineData("nonfinite_radius")]
    [InlineData("null_layer")] [InlineData("null_node")] [InlineData("null_frame")] [InlineData("null_shell")]
    public void InvalidTopologyClearsPreviouslyComputedTotals(string fault)
    {
        var snapshot = ValidSnapshot();
        DysonObservationPolicy.Complete(snapshot);
        var layer = snapshot.Layers[0];
        switch (fault)
        {
            case "duplicate_node": layer.Nodes.Add(layer.Nodes[0]); break;
            case "duplicate_frame": layer.Frames.Add(layer.Frames[0]); break;
            case "duplicate_edge": layer.Frames.Add(new DysonFrameSnapshot { FrameId = 4,
                NodeAId = 1, NodeBId = 2, MaximumStructurePoints = 20 }); break;
            case "dangling_frame": layer.Frames[0].NodeAId = 99; break;
            case "open_shell": layer.Shells[0].FrameIds.RemoveAt(0); break;
            case "duplicate_shell": layer.Shells.Add(layer.Shells[0]); break;
            case "negative_progress": layer.Nodes[0].StructurePoints = -1; break;
            case "excess_progress": layer.Frames[0].StructurePointsA = 21; break;
            case "nonfinite_position": layer.Nodes[0].Position.Y = float.NaN; break;
            case "nonfinite_radius": layer.OrbitRadius = double.PositiveInfinity; break;
            case "null_layer": snapshot.Layers.Add(null!); break;
            case "null_node": layer.Nodes.Add(null!); break;
            case "null_frame": layer.Frames.Add(null!); break;
            case "null_shell": layer.Shells.Add(null!); break;
        }
        DysonObservationPolicy.Complete(snapshot);
        Assert.False(snapshot.StructureCoverageComplete);
        Assert.Null(snapshot.StructurePoints);
        Assert.Null(snapshot.MaximumCellPoints);
        Assert.Empty(snapshot.DesignStateHash);
    }

    [Fact]
    public void CellTotalOverflowLeavesAllStructureTotalsUnknown()
    {
        var snapshot = ValidSnapshot();
        snapshot.Layers[0].Shells[0].MaximumCellPoints = long.MaxValue;
        snapshot.Layers[0].Shells.Add(new DysonShellSnapshot
        {
            ShellId = 2, NodeIds = new() { 1, 2, 3 }, FrameIds = new() { 1, 2, 3 },
            CellPoints = 1, MaximumCellPoints = long.MaxValue,
        });
        DysonObservationPolicy.Complete(snapshot);
        Assert.False(snapshot.StructureCoverageComplete);
        Assert.Null(snapshot.StructurePoints);
        Assert.Null(snapshot.MaximumCellPoints);
    }

    [Theory]
    [InlineData("different_star")] [InlineData("different_tick")] [InlineData("excess_generation")]
    [InlineData("photon_electricity")] [InlineData("disconnected_generation")] [InlineData("nonfinite_warmup")]
    [InlineData("duplicate_receiver")] [InlineData("unbound_receiver")] [InlineData("null_receiver")]
    [InlineData("missing_sphere")]
    public void InvalidReceiverEvidenceNeverBecomesPower(string fault)
    {
        var snapshot = ValidSnapshot();
        var receiver = snapshot.Receivers[0];
        switch (fault)
        {
            case "different_star": receiver.StarId++; break;
            case "different_tick": receiver.CapturedAtGameTick--; break;
            case "excess_generation": receiver.GeneratedEnergyPerTick = 1001; break;
            case "photon_electricity": receiver.ProductItemId = 1208; break;
            case "disconnected_generation": receiver.NetworkId = 0; break;
            case "nonfinite_warmup": receiver.Warmup = float.NaN; break;
            case "duplicate_receiver": snapshot.Receivers.Add(receiver); break;
            case "unbound_receiver": receiver.NativeBindingVerified = false; break;
            case "null_receiver": snapshot.Receivers.Add(null!); break;
            case "missing_sphere": snapshot.SphereAvailable = false; break;
        }
        DysonObservationPolicy.Complete(snapshot);
        Assert.Equal(fault != "missing_sphere", snapshot.StructureCoverageComplete);
        Assert.False(snapshot.ReceiverCoverageComplete);
        Assert.Null(snapshot.ReceiverGeneratedPowerWatts);
    }

    [Fact]
    public void RayReceiverDetailKeepsExistingFactoryActionHashesStable()
    {
        var entity = new FactoryEntitySnapshot { SessionId = "synthetic", PlanetId = 104, ObjectId = 42, ItemId = 2208 };
        var hash = CanonicalStateHash.Factory(entity);
        entity.RayReceiver = ValidSnapshot().Receivers[0];
        Assert.Equal(hash, CanonicalStateHash.Factory(entity));
    }

    private static DysonStateSnapshot ValidSnapshot()
    {
        var layer = new DysonLayerSnapshot { LayerId = 1, OrbitRadius = 10000, OrbitRotation = new() { W = 1 } };
        for (var id = 1; id <= 3; id++)
        {
            layer.Nodes.Add(new DysonNodeSnapshot { NodeId = id, Position = new() { X = id * 1000 },
                StructurePoints = 10, MaximumStructurePoints = 30, OrderedStructurePoints = 1 });
            layer.Frames.Add(new DysonFrameSnapshot { FrameId = id, NodeAId = id, NodeBId = id % 3 + 1,
                StructurePointsA = 3, StructurePointsB = 2, MaximumStructurePoints = 20 });
        }
        layer.Shells.Add(new DysonShellSnapshot { ShellId = 1, NodeIds = new() { 1, 2, 3 },
            FrameIds = new() { 1, 2, 3 }, CellPoints = 7, MaximumCellPoints = 40 });
        return new DysonStateSnapshot
        {
            SessionId = "synthetic-session", PlanetId = 104, StarId = 1, CapturedAtGameTick = 6000,
            SphereAvailable = true, NativeBindingVerified = true, StructureCoverageComplete = true,
            ReceiverCoverageComplete = true, Layers = new() { layer },
            Receivers = new()
            {
                new RayReceiverStateSnapshot { NativeBindingVerified = true, PlanetId = 104, StarId = 1,
                    EntityId = 42, ItemId = 2208, GeneratorComponentId = 5, NetworkId = 1, CapturedAtGameTick = 6000,
                    CapacityEnergyPerTick = 1000, GeneratedEnergyPerTick = 250, Warmup = 0.5f, SunlightStrength = 1,
                    ProductHeat = 120000000 },
            },
        };
    }
}
