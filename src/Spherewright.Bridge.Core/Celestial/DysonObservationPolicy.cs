using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Celestial;

public static class DysonObservationPolicy
{
    public const int MaximumLayers = 10;
    public const int MaximumNodes = 256;
    public const int MaximumFrames = 512;
    public const int MaximumShells = 128;
    public const int MaximumShellBoundaryNodes = 4096;
    public const int MaximumPoolSlots = 32768;
    public const int MaximumReceivers = 256;

    public static void Complete(DysonStateSnapshot snapshot)
    {
        snapshot.CoverageIssues ??= new List<string>();
        snapshot.StructurePoints = null;
        snapshot.MaximumStructurePoints = null;
        snapshot.OrderedStructurePoints = null;
        snapshot.CellPoints = null;
        snapshot.MaximumCellPoints = null;
        snapshot.DesignStateHash = string.Empty;
        snapshot.ReceiverGeneratedEnergyPerTick = null;

        var sourceValid = !string.IsNullOrWhiteSpace(snapshot.SessionId) && snapshot.PlanetId > 0
            && snapshot.StarId > 0 && snapshot.CapturedAtGameTick >= 0;
        if (sourceValid && snapshot.StructureCoverageComplete && snapshot.SphereAvailable && snapshot.NativeBindingVerified)
        {
            if (ValidLayers(snapshot))
                CompleteStructure(snapshot);
            else
                snapshot.StructureCoverageComplete = false;
        }
        else
        {
            snapshot.StructureCoverageComplete = false;
        }

        if (!snapshot.StructureCoverageComplete)
            AddIssue(snapshot, "dyson_structure_coverage_incomplete");

        if (sourceValid && snapshot.ReceiverCoverageComplete && ValidReceivers(snapshot))
        {
            try
            {
                snapshot.ReceiverGeneratedEnergyPerTick = snapshot.Receivers.Sum(receiver => receiver.GeneratedEnergyPerTick);
            }
            catch (OverflowException)
            {
                snapshot.ReceiverCoverageComplete = false;
            }
        }
        else
        {
            snapshot.ReceiverCoverageComplete = false;
        }

        if (!snapshot.ReceiverCoverageComplete)
            AddIssue(snapshot, "ray_receiver_coverage_incomplete");
    }

    public static bool ValidReceiver(RayReceiverStateSnapshot receiver) =>
        receiver is not null && receiver.NativeBindingVerified && receiver.PlanetId > 0 && receiver.StarId > 0
        && receiver.EntityId > 0 && receiver.GeneratorComponentId > 0 && receiver.ItemId > 0
        && receiver.NetworkId >= 0 && receiver.CapturedAtGameTick >= 0
        && receiver.CapacityEnergyPerTick >= 0 && receiver.GeneratedEnergyPerTick >= 0
        && receiver.GeneratedEnergyPerTick <= receiver.CapacityEnergyPerTick
        && (receiver.NetworkId > 0 || receiver.GeneratedEnergyPerTick == 0)
        && Finite(receiver.Warmup) && receiver.Warmup >= 0 && receiver.Warmup <= 1
        && Finite(receiver.SunlightStrength) && receiver.SunlightStrength >= 0 && receiver.SunlightStrength <= 1
        && receiver.ProductItemId >= 0 && Finite(receiver.ProductCount)
        && receiver.ProductCount >= 0 && receiver.ProductCount <= 20
        && receiver.ProductHeat >= 0 && (receiver.ProductItemId == 0 || receiver.ProductHeat > 0)
        && receiver.CatalystItemId >= 0
        && receiver.CatalystCount >= 0 && receiver.CatalystCount <= 2
        && receiver.CatalystIncPoints >= 0 && (long)receiver.CatalystIncPoints <= (long)receiver.CatalystCount * 10
        && receiver.ActiveCatalystItemId >= 0 && receiver.CatalystPoints >= 0 && receiver.CatalystPoints <= 3600
        && (receiver.ProductItemId == 0 || receiver.GeneratedEnergyPerTick == 0);

    private static bool ValidLayers(DysonStateSnapshot snapshot)
    {
        if (snapshot.Layers is null || snapshot.Layers.Any(layer => layer is null) || snapshot.Layers.Count > MaximumLayers
            || snapshot.SelectedLayerId < 0 || snapshot.SelectedLayerId > MaximumLayers
            || snapshot.Layers.Select(layer => layer.LayerId).Distinct().Count() != snapshot.Layers.Count)
            return false;

        var nodeCount = 0;
        var frameCount = 0;
        var shellCount = 0;
        var boundaryNodeCount = 0;
        foreach (var layer in snapshot.Layers)
        {
            if (layer.LayerId < 1 || layer.LayerId > MaximumLayers
                || (snapshot.SelectedLayerId != 0 && layer.LayerId != snapshot.SelectedLayerId)
                || !Finite(layer.OrbitRadius) || layer.OrbitRadius <= 0
                || !ValidRotation(layer.OrbitRotation) || layer.GeneratedEnergyPerTick < 0
                || layer.Nodes is null || layer.Frames is null || layer.Shells is null
                || layer.Nodes.Count > MaximumNodes || layer.Frames.Count > MaximumFrames
                || layer.Shells.Count > MaximumShells)
                return false;
            nodeCount += layer.Nodes.Count;
            frameCount += layer.Frames.Count;
            shellCount += layer.Shells.Count;
            if (nodeCount > MaximumNodes || frameCount > MaximumFrames || shellCount > MaximumShells)
                return false;

            var nodes = new HashSet<int>();
            foreach (var node in layer.Nodes)
            {
                if (node is null || node.NodeId <= 0 || !nodes.Add(node.NodeId) || node.PrototypeId < 0
                    || !ValidPosition(node.Position) || node.StructurePoints < 0
                    || node.MaximumStructurePoints <= 0 || node.StructurePoints > node.MaximumStructurePoints
                    || node.OrderedStructurePoints < 0 || node.OrderedCellPoints < 0)
                    return false;
            }

            var frames = new Dictionary<int, DysonFrameSnapshot>();
            var edges = new HashSet<string>();
            foreach (var frame in layer.Frames)
            {
                if (frame is null || frame.FrameId <= 0 || frames.ContainsKey(frame.FrameId) || frame.PrototypeId < 0
                    || frame.NodeAId == frame.NodeBId || !nodes.Contains(frame.NodeAId) || !nodes.Contains(frame.NodeBId)
                    || !edges.Add(Edge(frame.NodeAId, frame.NodeBId))
                    || frame.StructurePointsA < 0 || frame.StructurePointsB < 0 || frame.MaximumStructurePoints <= 0
                    || (long)frame.StructurePointsA + frame.StructurePointsB > frame.MaximumStructurePoints)
                    return false;
                frames.Add(frame.FrameId, frame);
            }

            var shells = new HashSet<int>();
            foreach (var shell in layer.Shells)
            {
                if (shell is null || shell.ShellId <= 0 || !shells.Add(shell.ShellId) || shell.PrototypeId < 0
                    || shell.CellPoints < 0 || shell.MaximumCellPoints < 0 || shell.CellPoints > shell.MaximumCellPoints
                    || shell.NodeIds is null || shell.FrameIds is null || shell.NodeIds.Count < 3
                    || shell.NodeIds.Count > MaximumNodes || shell.FrameIds.Count != shell.NodeIds.Count
                    || shell.NodeIds.Distinct().Count() != shell.NodeIds.Count
                    || shell.FrameIds.Distinct().Count() != shell.FrameIds.Count
                    || !ValidBoundary(shell, nodes, frames))
                    return false;
                boundaryNodeCount += shell.NodeIds.Count;
                if (boundaryNodeCount > MaximumShellBoundaryNodes) return false;
            }
        }

        return true;
    }

    private static bool ValidBoundary(DysonShellSnapshot shell, HashSet<int> nodes,
        Dictionary<int, DysonFrameSnapshot> frames)
    {
        var boundary = new HashSet<string>();
        for (var index = 0; index < shell.NodeIds.Count; index++)
        {
            var first = shell.NodeIds[index];
            var second = shell.NodeIds[(index + 1) % shell.NodeIds.Count];
            if (!nodes.Contains(first) || !nodes.Contains(second)) return false;
            boundary.Add(Edge(first, second));
        }

        foreach (var frameId in shell.FrameIds)
        {
            if (!frames.TryGetValue(frameId, out var frame) || !boundary.Remove(Edge(frame.NodeAId, frame.NodeBId)))
                return false;
        }
        return boundary.Count == 0;
    }

    private static string Edge(int first, int second) => first < second ? first + "/" + second : second + "/" + first;

    private static bool ValidReceivers(DysonStateSnapshot snapshot) =>
        snapshot.Receivers is not null && snapshot.Receivers.Count <= MaximumReceivers
        && snapshot.Receivers.All(receiver => ValidReceiver(receiver) && receiver.StarId == snapshot.StarId
            && receiver.CapturedAtGameTick == snapshot.CapturedAtGameTick
            && (receiver.GeneratedEnergyPerTick == 0 || (snapshot.SphereAvailable && snapshot.NativeBindingVerified)))
        && snapshot.Receivers.Select(receiver => receiver.PlanetId + "/" + receiver.EntityId).Distinct().Count()
            == snapshot.Receivers.Count;

    private static void CompleteStructure(DysonStateSnapshot snapshot)
    {
        try
        {
            snapshot.StructurePoints = snapshot.Layers.Sum(layer => layer.Nodes.Sum(node => (long)node.StructurePoints)
                + layer.Frames.Sum(frame => (long)frame.StructurePointsA + frame.StructurePointsB));
            snapshot.MaximumStructurePoints = snapshot.Layers.Sum(layer => layer.Nodes.Sum(node => (long)node.MaximumStructurePoints)
                + layer.Frames.Sum(frame => (long)frame.MaximumStructurePoints));
            snapshot.OrderedStructurePoints = snapshot.Layers.Sum(layer => layer.Nodes.Sum(node => (long)node.OrderedStructurePoints));
            snapshot.CellPoints = snapshot.Layers.Sum(layer => layer.Shells.Sum(shell => shell.CellPoints));
            snapshot.MaximumCellPoints = snapshot.Layers.Sum(layer => layer.Shells.Sum(shell => shell.MaximumCellPoints));
            snapshot.DesignStateHash = DesignHash(snapshot);
        }
        catch (OverflowException)
        {
            snapshot.StructureCoverageComplete = false;
            snapshot.StructurePoints = snapshot.MaximumStructurePoints = snapshot.OrderedStructurePoints = null;
            snapshot.CellPoints = snapshot.MaximumCellPoints = null;
        }
    }

    private static string DesignHash(DysonStateSnapshot snapshot)
    {
        var fields = new List<object?> { snapshot.SessionId, snapshot.StarId, snapshot.SelectedLayerId };
        foreach (var layer in snapshot.Layers.OrderBy(layer => layer.LayerId))
        {
            fields.AddRange(new object?[] { "layer", layer.LayerId, layer.OrbitRadius,
                layer.OrbitRotation.X, layer.OrbitRotation.Y, layer.OrbitRotation.Z, layer.OrbitRotation.W });
            foreach (var node in layer.Nodes.OrderBy(node => node.NodeId))
                fields.AddRange(new object?[] { "node", node.NodeId, node.PrototypeId,
                    node.Position.X, node.Position.Y, node.Position.Z, node.MaximumStructurePoints });
            foreach (var frame in layer.Frames.OrderBy(frame => frame.FrameId))
                fields.AddRange(new object?[] { "frame", frame.FrameId, frame.PrototypeId,
                    frame.NodeAId, frame.NodeBId, frame.Euler, frame.MaximumStructurePoints });
            foreach (var shell in layer.Shells.OrderBy(shell => shell.ShellId))
            {
                fields.AddRange(new object?[] { "shell", shell.ShellId, shell.PrototypeId, shell.MaximumCellPoints });
                fields.Add("nodes");
                fields.AddRange(shell.NodeIds.Cast<object?>());
                fields.Add("frames");
                fields.AddRange(shell.FrameIds.Cast<object?>());
            }
        }
        return CanonicalStateHash.Combine("dyson-design-v1", fields.ToArray());
    }

    private static bool ValidPosition(Vector3Snapshot position) => position is not null
        && Finite(position.X) && Finite(position.Y) && Finite(position.Z)
        && ((double)position.X * position.X + (double)position.Y * position.Y + (double)position.Z * position.Z) > 0;

    private static bool ValidRotation(QuaternionSnapshot rotation) => rotation is not null
        && Finite(rotation.X) && Finite(rotation.Y) && Finite(rotation.Z) && Finite(rotation.W)
        && ((double)rotation.X * rotation.X + (double)rotation.Y * rotation.Y
            + (double)rotation.Z * rotation.Z + (double)rotation.W * rotation.W) > 0;

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static void AddIssue(DysonStateSnapshot snapshot, string issue)
    {
        if (!snapshot.CoverageIssues.Contains(issue)) snapshot.CoverageIssues.Add(issue);
    }
}
