using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;
using Spherewright.Bridge.Core.Celestial;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal static class DysonStructureGeometry
{
    public static string? ValidateExistingGeometry(DysonSphereLayer layer)
    {
        if (ExistingDysonNodes(layer).Any(node => !ValidDysonLayerPosition(node.pos, layer.orbitRadius)))
            return "dyson_structure_existing_node_geometry_unproved";
        foreach (var frame in ExistingDysonFrames(layer))
        {
            var segments = frame.GetSegments();
            if (segments is null || segments.Count < 2 || segments.Count > 41
                || segments.Any(point => !ValidDysonLayerPosition(point, layer.orbitRadius)))
                return "dyson_structure_existing_frame_geometry_unproved";
        }
        if (ExistingDysonShells(layer).Any(shell => shell.polygon is null || shell.polygon.Count < 3
            || shell.polygon.Count > 384 || shell.polygon.Any(point => !ValidDysonLayerPosition((Vector3)point, layer.orbitRadius))))
            return "dyson_structure_existing_shell_geometry_unproved";
        return null;
    }

    public static bool TryGetPrototype(DysonStructureRequest structure, out Mesh? mesh, out Material? material)
    {
        mesh = null;
        material = null;
        if (structure.PrototypeId < 0 || structure.PrototypeId > 255) return false;
        switch (structure.Kind)
        {
            case DysonStructureKinds.Node when structure.PrototypeId < DysonSphereSegmentRenderer.nodeProtoCount:
                mesh = DysonSphereSegmentRenderer.GetNodeProtoMesh(structure.PrototypeId);
                material = DysonSphereSegmentRenderer.GetNodeProtoMaterial(structure.PrototypeId);
                break;
            case DysonStructureKinds.Frame when structure.PrototypeId < DysonSphereSegmentRenderer.frameProtoCount:
                mesh = DysonSphereSegmentRenderer.GetFrameProtoMesh(structure.PrototypeId);
                material = DysonSphereSegmentRenderer.GetFrameProtoMaterial(structure.PrototypeId);
                break;
            case DysonStructureKinds.Shell:
                material = Resources.Load<Material>("Dyson Sphere/Materials/dyson-shell-unlit-" + structure.PrototypeId);
                break;
        }
        return material != null && (structure.Kind == DysonStructureKinds.Shell || mesh != null);
    }

    public static int NextObjectId(DysonSphereLayer layer, string kind)
    {
        int id;
        switch (kind)
        {
            case DysonStructureKinds.Node:
                id = DysonStructurePolicy.NextObjectId(layer.nodeCursor, layer.nodeCapacity, layer.nodePool.Length,
                    layer.nodeRecycle, layer.nodeRecycleCursor);
                return id > 0 && (layer.nodePool[id] is null || layer.nodePool[id].id == 0) ? id : 0;
            case DysonStructureKinds.Frame:
                id = DysonStructurePolicy.NextObjectId(layer.frameCursor, layer.frameCapacity, layer.framePool.Length,
                    layer.frameRecycle, layer.frameRecycleCursor);
                return id > 0 && (layer.framePool[id] is null || layer.framePool[id].id == 0) ? id : 0;
            case DysonStructureKinds.Shell:
                id = DysonStructurePolicy.NextObjectId(layer.shellCursor, layer.shellCapacity, layer.shellPool.Length,
                    layer.shellRecycle, layer.shellRecycleCursor);
                return id > 0 && (layer.shellPool[id] is null || layer.shellPool[id].id == 0) ? id : 0;
            default:
                return 0;
        }
    }

    // DSP 0.10.35.29151 brush thresholds. Native DysonFrame supplies existing
    // curves and exact rocket costs. Detached DysonShell supplies cell geometry.
    private const float DysonNodeSeparationSquared = 0.0051122503f;
    private const float DysonFrameClearanceSquared = 0.0027562499f;

    private static IEnumerable<DysonNode> ExistingDysonNodes(DysonSphereLayer layer)
        => layer.nodePool.Take(layer.nodeCursor).Where(node => node is not null && node.id > 0);

    private static IEnumerable<DysonFrame> ExistingDysonFrames(DysonSphereLayer layer)
        => layer.framePool.Take(layer.frameCursor).Where(frame => frame is not null && frame.id > 0);

    private static IEnumerable<DysonShell> ExistingDysonShells(DysonSphereLayer layer)
        => layer.shellPool.Take(layer.shellCursor).Where(shell => shell is not null && shell.id > 0);

    public static int RequiredLatitudeDegrees(Vector3 position)
        => Mathf.RoundToInt(Mathf.Asin(Mathf.Clamp01(Mathf.Abs(position.normalized.y))) * 57.29578f);

    private static bool DysonLatitudeAllowed(Vector3 position, float maximumDegrees)
        => RequiredLatitudeDegrees(position) <= Mathf.RoundToInt(maximumDegrees);

    public static string? ValidateNode(DysonSphereLayer layer, DysonStructureRequest structure, float latitude)
    {
        var position = ToVector(structure.LocalPosition!);
        if (!ValidDysonLayerPosition(position, layer.orbitRadius)) return "dyson_node_position_must_match_layer_radius";
        if (!DysonLatitudeAllowed(position, latitude)) return "dyson_node_native_stress_exceeded";
        var direction = position.normalized;
        if (ExistingDysonNodes(layer).Any(node => (node.pos.normalized - direction).sqrMagnitude < DysonNodeSeparationSquared))
            return "dyson_node_too_close_to_existing_node";
        if (ExistingDysonShells(layer).Any(shell => shell.IsPointInShell(direction))) return "dyson_node_inside_shell";
        foreach (var frame in ExistingDysonFrames(layer))
        {
            var segments = frame.GetSegments();
            for (var i = 1; i < segments.Count; i++)
                if (UIDysonBrush_Node.PointToSegmentSqr(segments[i - 1].normalized, segments[i].normalized, direction)
                    < DysonFrameClearanceSquared) return "dyson_node_too_close_to_frame";
        }
        return null;
    }

    public static string? ValidateFrame(DysonSphereLayer layer, DysonStructureRequest structure,
        float latitude, out DysonFrame? candidate)
    {
        candidate = null;
        var nodeA = ExistingDysonNodes(layer).SingleOrDefault(node => node.id == structure.NodeAId);
        var nodeB = ExistingDysonNodes(layer).SingleOrDefault(node => node.id == structure.NodeBId);
        if (nodeA is null || nodeB is null || !ValidDysonLayerPosition(nodeA.pos, layer.orbitRadius)
            || !ValidDysonLayerPosition(nodeB.pos, layer.orbitRadius)) return "dyson_frame_exact_existing_nodes_required";
        if (DysonNode.FrameBetween(nodeA, nodeB) is not null) return "dyson_frame_already_exists";
        if (!DysonLatitudeAllowed(nodeA.pos, latitude) || !DysonLatitudeAllowed(nodeB.pos, latitude))
            return "dyson_frame_native_stress_exceeded";
        var begin = nodeA.pos.normalized;
        var end = nodeB.pos.normalized;
        if ((end - begin).magnitude > 0.518f) return "dyson_frame_native_length_exceeded";
        var segments = DysonBrushFrameSegments(begin, end, structure.Euler);
        foreach (var node in ExistingDysonNodes(layer))
        {
            if (node.id == nodeA.id || node.id == nodeB.id) continue;
            for (var i = 1; i < segments.Count; i++)
                if (UIDysonBrush_Node.PointToSegmentSqr(segments[i - 1], segments[i], node.pos.normalized)
                    < DysonFrameClearanceSquared) return "dyson_frame_too_close_to_other_node";
        }
        var midpoint = segments.Count >= 3 ? segments[segments.Count / 2] : Vector3.Lerp(begin, end, 0.5f).normalized;
        if (ExistingDysonShells(layer).Any(shell => shell.IsPointInShell(midpoint))) return "dyson_frame_inside_shell";
        foreach (var frame in ExistingDysonFrames(layer))
        {
            var existing = frame.GetSegments();
            for (var i = 1; i < segments.Count; i++)
            {
                var a = segments[i - 1];
                var b = segments[i];
                for (var j = 1; j < existing.Count; j++)
                {
                    var c = existing[j - 1].normalized;
                    var d = existing[j].normalized;
                    var atBegin = (a - begin).sqrMagnitude < DysonFrameClearanceSquared * 2;
                    var atEnd = (b - end).sqrMagnitude < DysonFrameClearanceSquared * 2;
                    var nearA = (c - existing[0].normalized).sqrMagnitude < DysonFrameClearanceSquared;
                    var nearB = (d - existing[existing.Count - 1].normalized).sqrMagnitude < DysonFrameClearanceSquared;
                    var sharedBegin = atBegin && ((nodeA == frame.nodeA && nearA) || (nodeA == frame.nodeB && nearB));
                    var sharedEnd = atEnd && ((nodeB == frame.nodeA && nearA) || (nodeB == frame.nodeB && nearB));
                    if ((!sharedBegin && UIDysonBrush_Node.PointToSegmentSqr(c, d, a) < DysonFrameClearanceSquared)
                        || (!sharedEnd && UIDysonBrush_Node.PointToSegmentSqr(c, d, b) < DysonFrameClearanceSquared))
                        return "dyson_frame_too_close_to_existing_frame";
                    if (!sharedBegin && !sharedEnd && DysonArcsCross(a, b, c, d)) return "dyson_frame_crosses_existing_frame";
                }
            }
        }
        candidate = new DysonFrame { nodeA = nodeA, nodeB = nodeB, euler = structure.Euler };
        return null;
    }

    private static List<Vector3> DysonBrushFrameSegments(Vector3 begin, Vector3 end, bool euler)
    {
        if (!euler) return new List<Vector3> { begin, end };
        var segmentCount = 1;
        var previous = begin;
        var previousSample = 0;
        for (var i = 1; i < 16; i++)
        {
            var point = Maths.Elerp(begin, end, i / 16f);
            var plane = new Plane(Vector3.zero, previous, point);
            for (var j = previousSample + 1; j < i; j++)
            {
                if (Mathf.Abs(plane.GetDistanceToPoint(Maths.Elerp(begin, end, j / 16f))) <= 0.013125f) continue;
                previous = point;
                previousSample = i;
                segmentCount++;
                break;
            }
        }
        var result = new List<Vector3>(segmentCount + 1);
        for (var i = 0; i <= segmentCount; i++) result.Add(Maths.Elerp(begin, end, i / (float)segmentCount));
        return result;
    }

    public static string? ValidateShell(DysonSphereLayer layer, DysonStructureRequest structure,
        out DysonShell? candidate, int plannedObjectId = 0)
    {
        candidate = null;
        var ids = structure.BoundaryNodeIds;
        var nodes = ids.Select(id => ExistingDysonNodes(layer).SingleOrDefault(node => node.id == id)).ToList();
        if (nodes.Any(node => node is null || !ValidDysonLayerPosition(node.pos, layer.orbitRadius)))
            return "dyson_shell_existing_boundary_nodes_required";
        var center = nodes.Aggregate(Vector3.zero, (sum, node) => sum + node!.pos).normalized;
        if (center.sqrMagnitude < 0.5f || nodes.Any(node => (center - node!.pos.normalized).sqrMagnitude > 0.26832402f * 0.6f))
            return "dyson_shell_native_cycle_size_exceeded";
        var shell = new DysonShell(layer)
        {
            id = plannedObjectId, protoId = structure.PrototypeId, layerId = layer.id,
            randSeed = layer.id * 10000 + plannedObjectId,
        };
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i]!;
            var next = nodes[(i + 1) % nodes.Count]!;
            var frame = DysonNode.FrameBetween(node, next);
            if (frame is null) return "dyson_shell_missing_boundary_frame";
            shell.nodeIndexMap[node.id] = i;
            shell.nodes.Add(node);
            shell.frames.Add(frame);
            var segments = frame.GetSegments();
            if (ReferenceEquals(node, frame.nodeA))
                shell.polygon.AddRange(segments.Take(segments.Count - 1).Select(point => (VectorLF3)point));
            else
                shell.polygon.AddRange(segments.Skip(1).Reverse().Select(point => (VectorLF3)point));
        }
        // Native model arrays hold two complete copies of the boundary in768 slots.
        if (shell.polygon.Count > 384 || !ValidDysonShellBoundary(shell.polygon))
            return "dyson_shell_degenerate_crossed_or_oversized_polygon";
        if (!shell.IsPointInShell(center)) return "dyson_shell_center_outside_cycle";
        if (ExistingDysonNodes(layer).Any(node => !ids.Contains(node.id) && shell.IsPointInShell(node.pos)))
            return "dyson_shell_contains_other_node";
        foreach (var frame in ExistingDysonFrames(layer).Where(frame => !shell.frames.Contains(frame)))
        {
            var segments = frame.GetSegments();
            for (var i = 1; i < segments.Count; i++)
                if (shell.IsPointInShell((segments[i - 1] + segments[i]).normalized))
                    return "dyson_shell_contains_other_frame";
        }
        foreach (var existing in ExistingDysonShells(layer))
        {
            if (existing.nodes.All(node => ids.Contains(node.id))) return "dyson_shell_already_exists";
            var existingCenter = existing.nodes.Aggregate(Vector3.zero, (sum, node) => sum + node.pos).normalized;
            if (existing.IsPointInShell(center) || shell.IsPointInShell(existingCenter)) return "dyson_shell_overlaps_existing_shell";
        }
        // GenerateGeometry only fills this detached shell's geometry arrays and
        // Native scratch maps. It creates no mesh, material, world object or progress.
        shell.GenerateGeometry();
        if (shell.vertexCount <= 0 || shell.vertexCount > 65500 || shell.cpPerVertex <= 0
            || (long)shell.vertexCount * shell.cpPerVertex > int.MaxValue)
            return "dyson_shell_native_geometry_or_cell_cost_unproved";
        candidate = shell;
        return null;
    }

    private static bool ValidDysonShellBoundary(List<VectorLF3> polygon)
    {
        var points = polygon.Select(point => ((Vector3)point).normalized).ToArray();
        var normalSum = Vector3.zero;
        for (var i = 0; i < points.Length; i++)
        {
            var next = (i + 1) % points.Length;
            normalSum += Vector3.Cross(points[i], points[next]);
            for (var j = i + 1; j < points.Length; j++)
            {
                var end = (j + 1) % points.Length;
                if (j == next || end == i) continue;
                if (DysonArcsCross(points[i], points[next], points[j], points[end])
                    || UIDysonBrush_Node.PointToSegmentSqr(points[i], points[next], points[j]) < 1e-10f
                    || UIDysonBrush_Node.PointToSegmentSqr(points[j], points[end], points[i]) < 1e-10f) return false;
            }
        }
        return normalSum.sqrMagnitude > 1e-12f;
    }

    private static bool DysonArcsCross(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var ab = Vector3.Cross(a, b).normalized;
        var cd = Vector3.Cross(c, d).normalized;
        return (double)(Vector3.Dot(a, cd) * Vector3.Dot(b, cd)) < -1e-9
            && (double)(Vector3.Dot(c, ab) * Vector3.Dot(d, ab)) < -1e-9;
    }

    private static bool ValidDysonLayerPosition(Vector3 position, float radius)
        => !float.IsNaN(position.magnitude) && !float.IsInfinity(position.magnitude)
            && Mathf.Abs(position.magnitude - radius) <= Mathf.Max(0.01f, radius * 1e-6f);

    private static Vector3 ToVector(Vector3Snapshot position) => new(position.X, position.Y, position.Z);
}
