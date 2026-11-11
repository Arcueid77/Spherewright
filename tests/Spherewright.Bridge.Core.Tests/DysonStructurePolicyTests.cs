using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class DysonStructurePolicyTests
{
    [Theory]
    [InlineData(1, 4, 4, 0, 0, 1)]
    [InlineData(3, 4, 4, 1, 2, 2)]
    [InlineData(4, 4, 4, 0, 0, 0)]
    [InlineData(3, 4, 8, 0, 0, 0)]
    [InlineData(3, 4, 4, 1, 3, 0)]
    [InlineData(3, 4, 4, 1, 0, 0)]
    [InlineData(32768, 65536, 65536, 0, 0, 0)]
    [InlineData(32768, 65536, 65536, 1, 7, 7)]
    public void AllocationMustStayWithinTheNativePoolAndObservableCursor(int cursor, int capacity,
        int length, int recycled, int recycledId, int expected)
        => Assert.Equal(expected, DysonStructurePolicy.NextObjectId(cursor, capacity, length, new[] { recycledId }, recycled));

    [Theory]
    [InlineData(DysonStructureKinds.Node, false)]
    [InlineData(DysonStructureKinds.Frame, false)]
    [InlineData(DysonStructureKinds.Frame, true)]
    [InlineData(DysonStructureKinds.Shell, false)]
    public void EachNativeKindBindsTheExactRequestAndCosts(string kind, bool euler)
    {
        var request = Request(kind);
        request.DysonStructure!.Euler = euler;
        var prepared = Prepared(request);
        Assert.Null(DysonStructurePolicy.ValidateRequest(request));
        Assert.True(DysonStructurePolicy.ConfirmsPreparedEcho("synthetic", request, prepared));
        Assert.Equal(prepared.PlannedDysonStructure!.RequiredStructurePoints,
            prepared.PlannedDysonStructure.RequiredCarrierRockets);
        Assert.False(prepared.PlannedDysonStructure.ConsumesInventoryOnCommit);
    }

    [Theory]
    [InlineData("missing")][InlineData("kind")][InlineData("layer")][InlineData("prototype")]
    [InlineData("position")][InlineData("foreign_frame")][InlineData("duplicate_cycle")]
    [InlineData("long_cycle")][InlineData("null_cycle")][InlineData("foreign_mode")]
    [InlineData("entity")][InlineData("recipe")][InlineData("station")][InlineData("version")]
    public void InvalidOrMixedParametersCannotBecomeAnExecutableRequest(string fault)
    {
        var request = Request(DysonStructureKinds.Shell);
        switch (fault)
        {
            case "missing": request.DysonStructure = null; break;
            case "kind": request.DysonStructure!.Kind = "delete"; break;
            case "layer": request.DysonStructure!.LayerId = 11; break;
            case "prototype": request.DysonStructure!.PrototypeId = -1; break;
            case "position": request.DysonStructure!.LocalPosition = new(); break;
            case "foreign_frame": request.DysonStructure!.NodeAId = 1; break;
            case "duplicate_cycle": request.DysonStructure!.BoundaryNodeIds = new() { 1, 2, 1 }; break;
            case "long_cycle": request.DysonStructure!.BoundaryNodeIds = Enumerable.Range(1, 33).ToList(); break;
            case "null_cycle": request.DysonStructure!.BoundaryNodeIds = null!; break;
            case "foreign_mode": request.Mode = BuildingConfigurationModes.Production; break;
            case "entity": request.EntityId = 2; break;
            case "recipe": request.RecipeId = 1; break;
            case "station": request.StationVesselDeliveryMinimumPercent = 1; break;
            case "version": request.StateHashVersion = 0; break;
        }
        Assert.NotNull(DysonStructurePolicy.ValidateRequest(request));
    }

    [Theory]
    [InlineData(float.NaN)][InlineData(float.PositiveInfinity)][InlineData(float.NegativeInfinity)]
    public void NonFiniteNodeCoordinatesAreRejected(float coordinate)
    {
        var request = Request(DysonStructureKinds.Node);
        request.DysonStructure!.LocalPosition!.X = coordinate;
        Assert.NotNull(DysonStructurePolicy.ValidateRequest(request));
    }

    [Theory]
    [InlineData("missing")][InlineData("session")][InlineData("planet")][InlineData("layer")]
    [InlineData("prototype")][InlineData("cost")][InlineData("native_checks")][InlineData("hash")]
    [InlineData("preview")][InlineData("operation")][InlineData("prepared")][InlineData("blocked")]
    public void StaleOrIncompleteEchoWithholdsTheOriginalToken(string fault)
    {
        var request = Request(DysonStructureKinds.Node);
        var prepared = Prepared(request);
        var plan = prepared.PlannedDysonStructure!;
        switch (fault)
        {
            case "missing": prepared.PlannedDysonStructure = null; break;
            case "session": plan.SessionId = "another"; break;
            case "planet": plan.PlanetId++; break;
            case "layer": plan.Structure.LayerId++; break;
            case "prototype": plan.Structure.PrototypeId++; break;
            case "cost": plan.RequiredStructurePoints++; break;
            case "native_checks": plan.NativeChecksComplete = false; break;
            case "hash": plan.BeforeDesignStateHash = "changed"; break;
            case "preview": plan.Structure.PreviewOnly = true; break;
            case "operation": plan.NativeOperation = "NewDysonFrame"; break;
            case "prepared": prepared.Prepared = false; break;
            case "blocked": prepared.CommitBlockers.Add(new() { Code = "SYNTHETIC" }); break;
        }
        Assert.False(DysonStructurePolicy.ConfirmsPreparedEcho("synthetic", request, prepared));
    }

    [Fact]
    public void PreviewIsRequestBoundAndCannotCarryACommitToken()
    {
        var request = Request(DysonStructureKinds.Shell);
        request.DysonStructure!.PreviewOnly = true;
        var prepared = Prepared(request);
        prepared.Prepared = false;
        prepared.CommitAllowedNow = false;
        prepared.PlanToken = string.Empty;
        Assert.True(DysonStructurePolicy.ConfirmsPreparedEcho("synthetic", request, prepared));
        prepared.PlanToken = "unexpected-capability";
        Assert.False(DysonStructurePolicy.ConfirmsPreparedEcho("synthetic", request, prepared));
    }

    [Fact]
    public void CopyDetachesMutableCoordinatesAndBoundaryOrder()
    {
        var structure = Request(DysonStructureKinds.Node).DysonStructure!;
        var copy = structure.Copy();
        structure.LocalPosition!.X++;
        structure.BoundaryNodeIds.Add(9);
        Assert.Equal(11000, copy.LocalPosition!.X);
        Assert.Empty(copy.BoundaryNodeIds);
    }

    [Fact]
    public void LiveProgressIsSeparateFromTheStaticDesignBinding()
    {
        var state = new DysonStateSnapshot { DesignStateHash = "same-design", Layers = new()
        {
            new() { LayerId = 1, Nodes = new() { new() { NodeId = 1, StructurePoints = 1 } } },
        }};
        var before = DysonStructurePolicy.ProgressHash(state);
        state.Layers[0].Nodes[0].OrderedCellPoints++;
        Assert.NotEqual(before, DysonStructurePolicy.ProgressHash(state));
        Assert.Equal("same-design", state.DesignStateHash);
    }

    private static PrepareConfigureBuildingRequest Request(string kind) => new()
    {
        PlanetId = 104, Mode = BuildingConfigurationModes.DysonStructure, ExpectedFactoryStateHash = "synthetic-design-hash",
        DysonStructure = new()
        {
            Kind = kind, LayerId = 1,
            LocalPosition = kind == DysonStructureKinds.Node ? new Vector3Snapshot { X = 11000 } : null,
            NodeAId = kind == DysonStructureKinds.Frame ? 1 : 0,
            NodeBId = kind == DysonStructureKinds.Frame ? 2 : 0,
            BoundaryNodeIds = kind == DysonStructureKinds.Shell ? new() { 1, 2, 3 } : new(),
        },
    };

    private static PreparedNormalAction Prepared(PrepareConfigureBuildingRequest request)
    {
        var plan = new DysonStructurePlan
        {
            EvidenceVersion = 1, SessionId = "synthetic", PlanetId = request.PlanetId, StarId = 1,
            Structure = request.DysonStructure!.Copy(), BeforeDesignStateHash = request.ExpectedFactoryStateHash,
            NativeOperation = DysonStructurePolicy.NativeOperation(request.DysonStructure.Kind),
            UnlockedNodeLatitudeDegrees = 15, NativeChecksComplete = true, PlannedObjectId = 4,
            RequiredStructurePoints = request.DysonStructure.Kind == DysonStructureKinds.Node ? 30
                : request.DysonStructure.Kind == DysonStructureKinds.Frame ? 40 : 0,
            RequiredCellPoints = request.DysonStructure.Kind == DysonStructureKinds.Shell ? 120 : 0,
        };
        plan.BindingHash = DysonStructurePolicy.BindingHash(plan);
        return new()
        {
            Prepared = true, CommitAllowedNow = true, ActionKind = NormalActionKinds.ConfigureBuilding,
            PlanToken = "synthetic-token", PlannedDysonStructure = plan, ExpectedStateHash = plan.BindingHash,
        };
    }
}
