using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;

namespace Spherewright.Bridge.Core.Celestial;

public static class DysonStructurePolicy
{
    public const int MaximumBoundaryNodes = 32;

    public static int NextObjectId(int cursor, int capacity, int poolLength, IReadOnlyList<int>? recycle, int recycleCount)
    {
        if (cursor < 1 || cursor >= capacity || capacity != poolLength || recycle is null
            || recycleCount < 0 || recycleCount >= cursor || recycleCount > recycle.Count
            || cursor + (recycleCount == 0 ? 1 : 0) > DysonObservationPolicy.MaximumPoolSlots)
            return 0;
        var id = recycleCount == 0 ? cursor : recycle[recycleCount - 1];
        return id >= 1 && (recycleCount == 0 ? id == cursor : id < cursor) ? id : 0;
    }

    public static string? ValidateRequest(PrepareConfigureBuildingRequest request)
    {
        if (request.Mode != BuildingConfigurationModes.DysonStructure || request.EntityId != 0
            || request.RecipeId != 0 || request.TechId != 0 || request.FilterItemId != 0
            || request.EjectorOrbitId != -1 || !string.IsNullOrEmpty(request.StorageOperation)
            || request.StorageBannedGridCount != -1 || request.StationStorageIndex != -1
            || request.StationBeltSlotIndex != -1 || request.StationBeltStorageIndex != -1
            || request.StationItemId != 0 || request.StationMaximumCount != 0
            || request.StationMaximumChargePowerWatts != 0 || request.StationVesselDeliveryMinimumPercent != -1
            || request.StationLocalLogic != LogisticsStorageLogics.None
            || request.StationRemoteLogic != LogisticsStorageLogics.None
            || !string.IsNullOrEmpty(request.ExpectedStationConfigurationStateHash))
            return "dyson_structure_exclusive_parameters_required";
        if (request.StateHashVersion != 1 || request.PlanetId <= 0 || string.IsNullOrEmpty(request.ExpectedFactoryStateHash))
            return "dyson_structure_current_design_hash_required";
        return ValidateStructure(request.DysonStructure);
    }

    public static string? ValidateStructure(DysonStructureRequest? structure)
    {
        if (structure is null || structure.LayerId < 1 || structure.LayerId > DysonObservationPolicy.MaximumLayers
            || structure.PrototypeId < 0 || structure.PrototypeId > 255 || structure.BoundaryNodeIds is null)
            return "dyson_structure_invalid_layer_or_prototype";
        switch (structure.Kind)
        {
            case DysonStructureKinds.Node:
                var position = structure.LocalPosition;
                if (position is null || !Finite(position.X) || !Finite(position.Y) || !Finite(position.Z)
                    || (double)position.X * position.X + (double)position.Y * position.Y + (double)position.Z * position.Z < 1
                    || structure.NodeAId != 0 || structure.NodeBId != 0 || structure.Euler || structure.BoundaryNodeIds.Count != 0)
                    return "dyson_node_requires_only_finite_layer_local_position";
                break;
            case DysonStructureKinds.Frame:
                if (structure.LocalPosition is not null || structure.NodeAId <= 0 || structure.NodeBId <= 0
                    || structure.NodeAId == structure.NodeBId || structure.BoundaryNodeIds.Count != 0)
                    return "dyson_frame_requires_two_distinct_existing_nodes";
                break;
            case DysonStructureKinds.Shell:
                if (structure.LocalPosition is not null || structure.NodeAId != 0 || structure.NodeBId != 0 || structure.Euler
                    || structure.BoundaryNodeIds.Count < 3 || structure.BoundaryNodeIds.Count > MaximumBoundaryNodes
                    || structure.BoundaryNodeIds.Any(id => id <= 0)
                    || structure.BoundaryNodeIds.Distinct().Count() != structure.BoundaryNodeIds.Count)
                    return "dyson_shell_requires_unique_ordered_boundary_nodes";
                break;
            default:
                return "dyson_structure_kind_must_be_node_frame_or_shell";
        }
        return null;
    }

    public static string NativeOperation(string kind) => kind switch
    {
        DysonStructureKinds.Node => "DysonSphereLayer.NewDysonNode",
        DysonStructureKinds.Frame => "DysonSphereLayer.NewDysonFrame",
        DysonStructureKinds.Shell => "DysonSphereLayer.NewDysonShell",
        _ => string.Empty,
    };

    public static string ProgressHash(DysonStateSnapshot state)
    {
        var fields = new List<object?> { state.DesignStateHash };
        foreach (var layer in state.Layers.OrderBy(layer => layer.LayerId))
        {
            fields.Add(layer.LayerId);
            foreach (var node in layer.Nodes.OrderBy(node => node.NodeId))
                fields.AddRange(new object?[] { "node", node.NodeId, node.StructurePoints,
                    node.OrderedStructurePoints, node.OrderedCellPoints });
            foreach (var frame in layer.Frames.OrderBy(frame => frame.FrameId))
                fields.AddRange(new object?[] { "frame", frame.FrameId, frame.StructurePointsA, frame.StructurePointsB });
            foreach (var shell in layer.Shells.OrderBy(shell => shell.ShellId))
                fields.AddRange(new object?[] { "shell", shell.ShellId, shell.CellPoints });
        }
        return CanonicalStateHash.Combine("dyson-progress-v1", fields.ToArray());
    }

    public static string BindingHash(DysonStructurePlan plan, DysonStructureRequest? requestedStructure = null)
    {
        var structure = requestedStructure ?? plan.Structure;
        var fields = new List<object?>
        {
            plan.EvidenceVersion, plan.SessionId, plan.PlanetId, plan.StarId,
            plan.BeforeDesignStateHash, plan.NativeOperation, plan.PlannedObjectId, plan.UnlockedNodeLatitudeDegrees,
            plan.NativeChecksComplete, plan.RequiredStructurePoints, plan.RequiredCellPoints,
            structure.Kind, structure.LayerId, structure.PrototypeId, structure.LocalPosition?.X,
            structure.LocalPosition?.Y, structure.LocalPosition?.Z, structure.NodeAId, structure.NodeBId,
            structure.Euler, structure.PreviewOnly, structure.BoundaryNodeIds.Count,
        };
        fields.AddRange(structure.BoundaryNodeIds.Cast<object?>());
        return CanonicalStateHash.Combine("native-dyson-structure-v1", fields.ToArray());
    }

    public static bool ConfirmsPreparedEcho(string sessionId, PrepareConfigureBuildingRequest request,
        PreparedNormalAction? prepared)
    {
        if (ValidateRequest(request) is not null || prepared?.PlannedDysonStructure is not { } plan
            || ValidateStructure(plan.Structure) is not null || plan.EvidenceVersion != 1
            || plan.SessionId != sessionId || plan.PlanetId != request.PlanetId || plan.StarId <= 0
            || plan.PlannedObjectId <= 0
            || plan.BeforeDesignStateHash != request.ExpectedFactoryStateHash || !plan.NativeChecksComplete
            || !Finite(plan.UnlockedNodeLatitudeDegrees) || plan.UnlockedNodeLatitudeDegrees < 0
            || plan.UnlockedNodeLatitudeDegrees > 90 || plan.NativeOperation != NativeOperation(plan.Structure.Kind)
            || prepared.ActionKind != NormalActionKinds.ConfigureBuilding
            || prepared.CommitBlockers is null || prepared.CommitBlockers.Count != 0
            || plan.BindingHash != BindingHash(plan) || prepared.ExpectedStateHash != plan.BindingHash)
            return false;
        var original = plan.Structure;
        var matchesRequest = plan.BindingHash == BindingHash(plan, request.DysonStructure);
        if (!matchesRequest) return false;
        var costsMatch = original.Kind switch
        {
            DysonStructureKinds.Node => plan.RequiredStructurePoints == 30 && plan.RequiredCellPoints == 0,
            DysonStructureKinds.Frame => plan.RequiredStructurePoints > 0 && plan.RequiredStructurePoints % 20 == 0
                && plan.RequiredCellPoints == 0,
            DysonStructureKinds.Shell => plan.RequiredStructurePoints == 0 && plan.RequiredCellPoints > 0,
            _ => false,
        };
        return costsMatch && (original.PreviewOnly
            ? !prepared.Prepared && !prepared.CommitAllowedNow && string.IsNullOrEmpty(prepared.PlanToken)
            : prepared.Prepared && prepared.CommitAllowedNow && !string.IsNullOrEmpty(prepared.PlanToken));
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
