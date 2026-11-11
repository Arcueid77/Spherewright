using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareDysonStructureOnMainThread(
        string? sessionId, PrepareConfigureBuildingRequest request, SessionState session)
    {
        if (DysonStructurePolicy.ValidateRequest(request) is { } reason) return InvalidPlan(reason);
        var captured = CaptureDysonStructurePlan(sessionId!, request);
        if (!captured.Success || captured.Value is null)
            return GameCallResult<PreparedNormalAction>.Failed(captured.Error!);
        var binding = captured.Value;
        const string completion = "One ordinary Native Dyson design operation, exact object/cost readback, existing structure and package preservation.";
        if (binding.Plan.Structure.PreviewOnly)
            return GameCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction
            {
                Prepared = false, CommitAllowedNow = false, ActionKind = NormalActionKinds.ConfigureBuilding,
                ExpectedStateHash = binding.Plan.BindingHash, PlannedDysonStructure = binding.Plan,
                CompletionCondition = "Read-only Native geometry and cost preview.",
            });
        var payload = NormalActionPlanPayload.ForDysonStructure(binding);
        var result = AddPreparedPlan(payload, session, 1, completion);
        if (result.Success && result.Value is not null) result.Value.PlannedDysonStructure = binding.Plan;
        return result;
    }

    private GameCallResult<NativeDysonStructureBinding> CaptureDysonStructurePlan(
        string sessionId, PrepareConfigureBuildingRequest request)
    {
        var structure = request.DysonStructure!.Copy();
        var observed = _reader.GetDysonStateOnMainThread(sessionId,
            new GetDysonStateRequest { PlanetId = request.PlanetId, LayerId = structure.LayerId });
        if (!observed.Success || observed.Value is null)
            return GameCallResult<NativeDysonStructureBinding>.Failed(observed.Error!);
        var state = observed.Value;
        var star = GameMain.localStar;
        var sphere = star is not null && GameMain.data?.dysonSpheres is { } spheres
            && star.index >= 0 && star.index < spheres.Length ? spheres[star.index] : null;
        var layer = sphere?.layersIdBased?[structure.LayerId];
        if (!state.StructureCoverageComplete || !state.NativeBindingVerified || state.SelectedLayerPresent != true
            || state.Layers.Count != 1 || state.DesignStateHash != request.ExpectedFactoryStateHash
            || state.UnlockedNodeLatitudeDegrees is null || sphere is null || layer is null
            || !ReferenceEquals(sphere.gameData, GameMain.data) || !ReferenceEquals(sphere.starData, star)
            || !ReferenceEquals(layer.dysonSphere, sphere) || layer.id != structure.LayerId
            || GameMain.history is null || sphere.modelRenderer is null)
            return DysonStructureFailure("dyson_structure_existing_layer_hash_or_native_binding_changed");
        var current = state.Layers[0];
        if (DysonStructureGeometry.ValidateExistingGeometry(layer) is { } geometryError)
            return DysonStructureFailure(geometryError);
        if ((structure.Kind == DysonStructureKinds.Node && current.Nodes.Count >= DysonObservationPolicy.MaximumNodes)
            || (structure.Kind == DysonStructureKinds.Frame && current.Frames.Count >= DysonObservationPolicy.MaximumFrames)
            || (structure.Kind == DysonStructureKinds.Shell && (current.Shells.Count >= DysonObservationPolicy.MaximumShells
                || current.Shells.Sum(shell => shell.NodeIds.Count) + structure.BoundaryNodeIds.Count
                    > DysonObservationPolicy.MaximumShellBoundaryNodes)))
            return DysonStructureFailure("dyson_structure_observation_budget_would_be_exceeded");

        var plan = new DysonStructurePlan
        {
            EvidenceVersion = 1, SessionId = sessionId, PlanetId = request.PlanetId, StarId = state.StarId,
            Structure = structure, BeforeDesignStateHash = state.DesignStateHash,
            NativeOperation = DysonStructurePolicy.NativeOperation(structure.Kind),
            PlannedObjectId = DysonStructureGeometry.NextObjectId(layer, structure.Kind),
            UnlockedNodeLatitudeDegrees = state.UnlockedNodeLatitudeDegrees.Value,
        };
        if (plan.PlannedObjectId <= 0) return DysonStructureFailure("dyson_structure_native_allocator_unproved");
        if (!DysonStructureGeometry.TryGetPrototype(structure, out var mesh, out var material))
            return DysonStructureFailure("dyson_structure_native_render_prototype_unavailable");
        string? blocker;
        switch (structure.Kind)
        {
            case DysonStructureKinds.Node:
                blocker = DysonStructureGeometry.ValidateNode(layer, structure, plan.UnlockedNodeLatitudeDegrees);
                plan.RequiredStructurePoints = 30;
                break;
            case DysonStructureKinds.Frame:
                blocker = DysonStructureGeometry.ValidateFrame(layer, structure, plan.UnlockedNodeLatitudeDegrees, out var frame);
                if (frame is not null) plan.RequiredStructurePoints = checked(frame.segCount * 10);
                break;
            case DysonStructureKinds.Shell:
                blocker = DysonStructureGeometry.ValidateShell(layer, structure, out var shell, plan.PlannedObjectId);
                if (shell is not null) plan.RequiredCellPoints = shell.cellPointMax;
                break;
            default:
                return DysonStructureFailure("dyson_structure_kind_unavailable");
        }
        if (blocker is not null) return DysonStructureFailure(blocker);
        plan.NativeChecksComplete = true;
        plan.BindingHash = DysonStructurePolicy.BindingHash(plan);
        return GameCallResult<NativeDysonStructureBinding>.Succeeded(new NativeDysonStructureBinding
        {
            Plan = plan, Observation = state, Layer = layer, World = GameMain.data!, History = GameMain.history,
            Renderer = sphere.modelRenderer, PrototypeMesh = mesh, PrototypeMaterial = material!,
            NodePool = layer.nodePool, FramePool = layer.framePool, ShellPool = layer.shellPool,
            Nodes = current.Nodes.Select(node => layer.nodePool[node.NodeId]).ToArray(),
            Frames = current.Frames.Select(frame => layer.framePool[frame.FrameId]).ToArray(),
            Shells = current.Shells.Select(shell => layer.shellPool[shell.ShellId]).ToArray(),
            NativeNodePrototypeCount = DysonSphereSegmentRenderer.nodeProtoCount,
        });
    }

    private BridgeError? RevalidateDysonStructureOnMainThread(NormalActionPlanPayload payload)
    {
        var expected = payload.DysonStructureBinding;
        if (expected is null || expected.Plan.Structure.PreviewOnly) return Stale("dyson_structure_executable_binding_missing");
        var captured = CaptureDysonStructurePlan(payload.SessionId, DysonStructureConfigurationRequest(expected.Plan));
        return captured.Success && captured.Value is { } current && SameDysonStructureBinding(expected, current)
            ? null : captured.Error ?? Stale("dyson_structure_native_binding_changed");
    }

    private static PrepareConfigureBuildingRequest DysonStructureConfigurationRequest(DysonStructurePlan plan) => new()
    {
        PlanetId = plan.PlanetId, Mode = BuildingConfigurationModes.DysonStructure,
        ExpectedFactoryStateHash = plan.BeforeDesignStateHash, DysonStructure = plan.Structure.Copy(),
    };

    private static bool SameDysonStructureBinding(NativeDysonStructureBinding expected, NativeDysonStructureBinding current)
        => expected.Plan.BindingHash == current.Plan.BindingHash
            && ReferenceEquals(expected.World, current.World) && ReferenceEquals(expected.History, current.History)
            && ReferenceEquals(expected.Layer, current.Layer) && ReferenceEquals(expected.Renderer, current.Renderer)
            && ReferenceEquals(expected.PrototypeMesh, current.PrototypeMesh)
            && ReferenceEquals(expected.PrototypeMaterial, current.PrototypeMaterial)
            && ReferenceEquals(expected.NodePool, current.NodePool) && ReferenceEquals(expected.FramePool, current.FramePool)
            && ReferenceEquals(expected.ShellPool, current.ShellPool) && expected.Nodes.SequenceEqual(current.Nodes)
            && expected.Frames.SequenceEqual(current.Frames) && expected.Shells.SequenceEqual(current.Shells)
            && expected.NativeNodePrototypeCount == current.NativeNodePrototypeCount;

    private void ExecuteDysonStructureOnMainThread(ActionRecord action)
    {
        var expected = action.Plan.DysonStructureBinding!;
        var captured = CaptureDysonStructurePlan(action.SessionId, DysonStructureConfigurationRequest(expected.Plan));
        if (!captured.Success || captured.Value is not { } binding || !SameDysonStructureBinding(expected, binding))
            throw new InvalidOperationException("The exact Native Dyson design changed before execution.");
        var plan = binding.Plan;
        var structure = plan.Structure;
        var before = binding.Observation;
        var layer = binding.Layer;
        var packageBefore = CapturePlayerPackageState(GameMain.mainPlayer);
        var readback = new DysonStructureReadback
        {
            EvidenceVersion = 1, StarId = plan.StarId, LayerId = structure.LayerId, Kind = structure.Kind,
            PlanBindingHash = plan.BindingHash, BeforeDesignStateHash = before.DesignStateHash,
            PlannedObjectId = plan.PlannedObjectId,
        };
        action.DysonStructureReadback = readback;
        readback.NativeOperationInvoked = true;
        var objectId = structure.Kind switch
        {
            DysonStructureKinds.Node => layer.NewDysonNode(structure.PrototypeId, ToVector(structure.LocalPosition!)),
            DysonStructureKinds.Frame => layer.NewDysonFrame(structure.PrototypeId, structure.NodeAId, structure.NodeBId, structure.Euler),
            DysonStructureKinds.Shell => layer.NewDysonShell(structure.PrototypeId, structure.BoundaryNodeIds.ToList()),
            _ => throw new InvalidOperationException("Unsupported Native Dyson structure kind."),
        };
        // Preserve the original allocation even when a later readback fails.
        action.TargetObjectId = objectId > 0 ? objectId : null;
        readback.ObjectId = objectId;
        if (structure.Kind == DysonStructureKinds.Shell && objectId > 0)
            GameMain.gameScenario.NotifyOnPlanDysonShell();
        var observed = _reader.GetDysonStateOnMainThread(action.SessionId,
            new GetDysonStateRequest { PlanetId = action.PlanetId, LayerId = structure.LayerId });
        if (!observed.Success || observed.Value is not { StructureCoverageComplete: true } after
            || objectId != plan.PlannedObjectId)
            throw new InvalidOperationException("The Native Dyson allocation has no complete immediate readback.");
        readback.AfterDesignStateHash = after.DesignStateHash;
        readback.AddedMaximumStructurePoints = checked((int)(after.MaximumStructurePoints!.Value - before.MaximumStructurePoints!.Value));
        readback.AddedMaximumCellPoints = checked((int)(after.MaximumCellPoints!.Value - before.MaximumCellPoints!.Value));
        readback.ExactNativeObjectVerified = VerifyAndRemoveNewDysonObject(after.Layers.Single(), plan, objectId,
            binding.NativeNodePrototypeCount);
        DysonObservationPolicy.Complete(after);
        readback.ExistingStructurePreserved = after.StructureCoverageComplete
            && after.DesignStateHash == before.DesignStateHash
            && DysonStructurePolicy.ProgressHash(after) == DysonStructurePolicy.ProgressHash(before);
        readback.InventoryPreserved = CapturePlayerPackageState(GameMain.mainPlayer) == packageBefore;
        if (!readback.ExactNativeObjectVerified || !readback.ExistingStructurePreserved || !readback.InventoryPreserved
            || readback.AddedMaximumStructurePoints != plan.RequiredStructurePoints
            || readback.AddedMaximumCellPoints != plan.RequiredCellPoints)
            throw new InvalidOperationException("The original Native Dyson operation did not prove its exact object, costs and preservation.");
        Complete(action, "Native Dyson design operation completed once with exact object, cost, topology and inventory readback.");
        action.AfterStateHash = readback.AfterDesignStateHash;
    }

    private static bool VerifyAndRemoveNewDysonObject(DysonLayerSnapshot layer, DysonStructurePlan plan, int objectId,
        int nativeNodePrototypeCount)
    {
        var structure = plan.Structure;
        switch (structure.Kind)
        {
            case DysonStructureKinds.Node:
                var node = layer.Nodes.SingleOrDefault(value => value.NodeId == objectId);
                if (node is null || node.PrototypeId != structure.PrototypeId || node.StructurePoints != 0
                    || node.OrderedStructurePoints != 0 || node.OrderedCellPoints != 0 || node.MaximumStructurePoints != 30
                    || !ToVector(node.Position).Equals(ToVector(structure.LocalPosition!))) return false;
                return layer.Nodes.Remove(node);
            case DysonStructureKinds.Frame:
                var frame = layer.Frames.SingleOrDefault(value => value.FrameId == objectId);
                if (frame is null || frame.PrototypeId != structure.PrototypeId + nativeNodePrototypeCount
                    || frame.NodeAId != structure.NodeAId || frame.NodeBId != structure.NodeBId || frame.Euler != structure.Euler
                    || frame.StructurePointsA != 0 || frame.StructurePointsB != 0
                    || frame.MaximumStructurePoints != plan.RequiredStructurePoints) return false;
                return layer.Frames.Remove(frame);
            case DysonStructureKinds.Shell:
                var shell = layer.Shells.SingleOrDefault(value => value.ShellId == objectId);
                if (shell is null || shell.PrototypeId != structure.PrototypeId || shell.CellPoints != 0
                    || shell.MaximumCellPoints != plan.RequiredCellPoints
                    || !shell.NodeIds.SequenceEqual(structure.BoundaryNodeIds)) return false;
                return layer.Shells.Remove(shell);
            default:
                return false;
        }
    }

    private static GameCallResult<NativeDysonStructureBinding> DysonStructureFailure(string reason)
        => GameCallResult<NativeDysonStructureBinding>.Failed(BridgeError.Create(BridgeErrorCodes.ActionRejected,
            reason, true, "Read the exact current layer and Native technology, geometry and prototype evidence before preparing again."));


    private sealed class NativeDysonStructureBinding
    {
        public DysonStructurePlan Plan { get; set; } = null!;
        public DysonStateSnapshot Observation { get; set; } = null!;
        public DysonSphereLayer Layer { get; set; } = null!;
        public GameData World { get; set; } = null!;
        public GameHistoryData History { get; set; } = null!;
        public object Renderer { get; set; } = null!;
        public Mesh? PrototypeMesh { get; set; }
        public Material PrototypeMaterial { get; set; } = null!;
        public DysonNode[] NodePool { get; set; } = null!;
        public DysonFrame[] FramePool { get; set; } = null!;
        public DysonShell[] ShellPool { get; set; } = null!;
        public DysonNode[] Nodes { get; set; } = null!;
        public DysonFrame[] Frames { get; set; } = null!;
        public DysonShell[] Shells { get; set; } = null!;
        public int NativeNodePrototypeCount { get; set; }
    }

    private sealed partial class NormalActionPlanPayload
    {
        public NativeDysonStructureBinding? DysonStructureBinding { get; set; }

        public static NormalActionPlanPayload ForDysonStructure(NativeDysonStructureBinding binding) => new()
        {
            ActionKind = NormalActionKinds.ConfigureBuilding, ConfigureMode = BuildingConfigurationModes.DysonStructure,
            SessionId = binding.Plan.SessionId, PlanetId = binding.Plan.PlanetId,
            ExpectedStateHash = binding.Plan.BindingHash, FactoryStateHash = binding.Plan.BeforeDesignStateHash,
            DysonStructureBinding = binding,
        };
    }
}
