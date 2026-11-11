using Spherewright.Bridge.Core.Celestial;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal static class DysonDesignPreviewReader
{
    public static DysonDesignPreview Capture(DysonSphereLayer? layer, DysonStateSnapshot state, DysonDesignPreviewRequest request)
    {
        var result = new DysonDesignPreview { BeforeDesignStateHash = state.DesignStateHash };
        if (DysonDesignPreviewPolicy.ValidateRequest(state.SelectedLayerId, request) is { } invalid)
            return Failed(result, invalid);
        if (!state.StructureCoverageComplete || !state.NativeBindingVerified || state.SelectedLayerPresent != true
            || state.DesignStateHash != request.ExpectedDesignStateHash || layer is null
            || state.UnlockedNodeLatitudeDegrees is null || state.Layers.Count != 1)
            return Failed(result, "dyson_design_preview_current_layer_or_hash_unproved");
        if (DysonStructureGeometry.ValidateExistingGeometry(layer) is { } geometryError)
            return Failed(result, geometryError);
        if (!DysonDesignPreviewPolicy.ExistingReferencesBound(request, state.Layers[0].Nodes.Select(node => node.NodeId)))
            return Failed(result, "dyson_design_preview_positive_references_require_original_existing_nodes");
        foreach (var kind in request.Steps.Select(step => step.Structure.Kind).Distinct())
            if (DysonStructureGeometry.NextObjectId(layer, kind) <= 0)
                return Failed(result, "dyson_design_preview_native_allocator_unproved");

        // Constructor only binds read references. Never call Init, ResetNew,
        // NewDyson*, renderer methods or game notifications on this detached graph.
        var graph = CopyGeometry(layer, state.Layers[0]);
        var aliases = new Dictionary<int, int>();
        foreach (var step in request.Steps)
        {
            var structure = step.Structure.Copy();
            structure.PreviewOnly = true;
            structure.NodeAId = Resolve(structure.NodeAId, aliases);
            structure.NodeBId = Resolve(structure.NodeBId, aliases);
            structure.BoundaryNodeIds = structure.BoundaryNodeIds.Select(id => Resolve(id, aliases)).ToList();
            if (!DysonStructureGeometry.TryGetPrototype(structure, out _, out _))
                return Failed(result, "dyson_design_preview_native_prototype_unavailable");
            var id = DysonStructureGeometry.NextObjectId(graph, structure.Kind);
            if (id <= 0) return Failed(result, "dyson_design_preview_pool_limit");
            var plan = new DysonStructurePlan
            {
                EvidenceVersion = 1, SessionId = state.SessionId, PlanetId = state.PlanetId, StarId = state.StarId,
                Structure = structure, BeforeDesignStateHash = state.DesignStateHash, PlannedObjectId = id,
                NativeOperation = DysonStructurePolicy.NativeOperation(structure.Kind),
                UnlockedNodeLatitudeDegrees = state.UnlockedNodeLatitudeDegrees.Value,
            };
            var latitude = 0;
            string? blocker;
            switch (structure.Kind)
            {
                case DysonStructureKinds.Node:
                    blocker = DysonStructureGeometry.ValidateNode(graph, structure, 90);
                    if (blocker is not null) return Failed(result, blocker);
                    var position = structure.LocalPosition!;
                    var node = new DysonNode
                    {
                        id = id, layerId = layer.id, protoId = structure.PrototypeId,
                        pos = new Vector3(position.X, position.Y, position.Z), spMax = 30,
                    };
                    latitude = DysonStructureGeometry.RequiredLatitudeDegrees(node.pos);
                    graph.nodePool[id] = node;
                    if (graph.nodeRecycleCursor > 0) graph.nodeRecycleCursor--; else graph.nodeCursor++;
                    aliases[step.LocalId] = id;
                    plan.RequiredStructurePoints = 30;
                    break;
                case DysonStructureKinds.Frame:
                    blocker = DysonStructureGeometry.ValidateFrame(graph, structure, 90, out var frame);
                    if (blocker is not null || frame is null) return Failed(result, blocker ?? "dyson_design_preview_frame_unproved");
                    frame.id = id;
                    frame.layerId = layer.id;
                    frame.protoId = structure.PrototypeId + DysonSphereSegmentRenderer.nodeProtoCount;
                    frame.spMax = checked(frame.segCount * 10);
                    latitude = Math.Max(DysonStructureGeometry.RequiredLatitudeDegrees(frame.nodeA.pos),
                        DysonStructureGeometry.RequiredLatitudeDegrees(frame.nodeB.pos));
                    frame.nodeA.frames.Add(frame);
                    frame.nodeB.frames.Add(frame);
                    graph.framePool[id] = frame;
                    if (graph.frameRecycleCursor > 0) graph.frameRecycleCursor--; else graph.frameCursor++;
                    plan.RequiredStructurePoints = frame.spMax;
                    break;
                case DysonStructureKinds.Shell:
                    blocker = DysonStructureGeometry.ValidateShell(graph, structure, out var shell, id);
                    if (blocker is not null || shell is null) return Failed(result, blocker ?? "dyson_design_preview_shell_unproved");
                    graph.shellPool[id] = shell;
                    if (graph.shellRecycleCursor > 0) graph.shellRecycleCursor--; else graph.shellCursor++;
                    plan.RequiredCellPoints = shell.cellPointMax;
                    break;
                default:
                    return Failed(result, "dyson_design_preview_kind_unavailable");
            }
            if (graph.nodeCount > DysonObservationPolicy.MaximumNodes || graph.frameCount > DysonObservationPolicy.MaximumFrames
                || graph.shellCount > DysonObservationPolicy.MaximumShells
                || graph.shellPool.Take(graph.shellCursor).Where(shell => shell is not null && shell.id > 0)
                    .Sum(shell => shell.nodes.Count) > DysonObservationPolicy.MaximumShellBoundaryNodes)
                return Failed(result, "dyson_design_preview_observation_budget_exceeded");
            result.RequiredNodeLatitudeDegrees = Math.Max(result.RequiredNodeLatitudeDegrees, latitude);
            plan.NativeChecksComplete = latitude <= Mathf.RoundToInt(plan.UnlockedNodeLatitudeDegrees);
            plan.BindingHash = DysonStructurePolicy.BindingHash(plan);
            result.Steps.Add(new DysonDesignStepPreview { LocalId = step.LocalId, Plan = plan });
        }
        var rockets = result.Steps.Sum(step => (long)step.Plan.RequiredStructurePoints);
        var sails = result.Steps.Sum(step => (long)step.Plan.RequiredCellPoints);
        if (rockets > int.MaxValue || sails > int.MaxValue) return Failed(result, "dyson_design_preview_cost_overflow");
        result.GeometryComplete = true;
        result.TechnologyAllowsNow = result.Steps.All(step => step.Plan.NativeChecksComplete);
        result.RequiredCarrierRockets = (int)rockets;
        result.RequiredSolarSails = (int)sails;
        var fields = new List<object?> { state.SessionId, state.PlanetId, state.StarId, state.SelectedLayerId, state.DesignStateHash };
        foreach (var step in result.Steps) fields.AddRange(new object?[] { step.LocalId, step.Plan.BindingHash });
        result.CandidateHash = CanonicalStateHash.Combine("native-dyson-design-preview-v1", fields.ToArray());
        if (!result.TechnologyAllowsNow) result.Issues.Add("dyson_design_preview_native_stress_technology_required");
        return result;
    }

    private static DysonSphereLayer CopyGeometry(DysonSphereLayer source, DysonLayerSnapshot snapshot)
    {
        var extra = DysonDesignPreviewPolicy.MaximumSteps + 1;
        var graph = new DysonSphereLayer(source.dysonSphere)
        {
            id = source.id, orbitRadius = source.orbitRadius,
            nodeCursor = source.nodeCursor, frameCursor = source.frameCursor, shellCursor = source.shellCursor,
            nodeRecycleCursor = source.nodeRecycleCursor, frameRecycleCursor = source.frameRecycleCursor,
            shellRecycleCursor = source.shellRecycleCursor,
            nodePool = new DysonNode[source.nodeCursor + extra], framePool = new DysonFrame[source.frameCursor + extra],
            shellPool = new DysonShell[source.shellCursor + extra],
            nodeRecycle = source.nodeRecycle?.Take(source.nodeRecycleCursor).ToArray() ?? Array.Empty<int>(),
            frameRecycle = source.frameRecycle?.Take(source.frameRecycleCursor).ToArray() ?? Array.Empty<int>(),
            shellRecycle = source.shellRecycle?.Take(source.shellRecycleCursor).ToArray() ?? Array.Empty<int>(),
        };
        graph.nodeCapacity = graph.nodePool.Length;
        graph.frameCapacity = graph.framePool.Length;
        graph.shellCapacity = graph.shellPool.Length;
        foreach (var observed in snapshot.Nodes)
            graph.nodePool[observed.NodeId] = new DysonNode
            {
                id = observed.NodeId, layerId = source.id, protoId = observed.PrototypeId,
                pos = new Vector3(observed.Position.X, observed.Position.Y, observed.Position.Z), spMax = observed.MaximumStructurePoints,
            };
        foreach (var observed in snapshot.Frames)
        {
            var frame = new DysonFrame
            {
                id = observed.FrameId, layerId = source.id, protoId = observed.PrototypeId,
                nodeA = graph.nodePool[observed.NodeAId], nodeB = graph.nodePool[observed.NodeBId],
                euler = observed.Euler, spMax = observed.MaximumStructurePoints,
            };
            graph.framePool[frame.id] = frame;
            frame.nodeA.frames.Add(frame);
            frame.nodeB.frames.Add(frame);
        }
        foreach (var observed in snapshot.Shells)
        {
            var shell = new DysonShell(graph) { id = observed.ShellId, layerId = source.id, protoId = observed.PrototypeId };
            shell.polygon.AddRange(source.shellPool[observed.ShellId].polygon);
            shell.nodes.AddRange(observed.NodeIds.Select(id => graph.nodePool[id]));
            shell.frames.AddRange(observed.FrameIds.Select(id => graph.framePool[id]));
            graph.shellPool[shell.id] = shell;
        }
        return graph;
    }

    private static int Resolve(int id, IReadOnlyDictionary<int, int> aliases) => id < 0 ? aliases[id] : id;

    private static DysonDesignPreview Failed(DysonDesignPreview result, string reason)
    {
        result.Issues.Add(reason);
        return result;
    }
}
