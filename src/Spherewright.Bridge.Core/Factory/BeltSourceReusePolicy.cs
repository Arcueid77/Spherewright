using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Bridge.Core.Factory;

/// <summary>Narrow non-removing SOURCE cover subset. Partitioning is not placement approval:
/// the cover remains in the full native check/create list and ALL new points keep occupancy checks.</summary>
public static class BeltSourceReusePolicy
{
    public const string Recovery = "Do not retry this site or omit its bound endpoint. Inspect the exact source, "
        + "native path and occupancy evidence; moving closer only helps an explicit OutOfReach rejection.";

    public static bool Supports(int sourceId, int sourceItem, int newItem, int sourceSlot,
        int destinationId, float tilt, bool closed, int outputObjectId, int incomingBelts) =>
        sourceId > 0 && sourceId <= BeltBuildOccupancyPolicy.MaximumFactorySlots
        && (sourceItem == 2001 || sourceItem == 2002 || sourceItem == 2003) && newItem == sourceItem
        && sourceSlot == 0 && destinationId == 0 && tilt == 0 && !closed
        && outputObjectId == 0 && incomingBelts >= 0 && incomingBelts <= 1;

    public static bool TrySeparateNewPoints(IReadOnlyList<Vector3Snapshot>? fullPath,
        Vector3Snapshot? source, out List<Vector3Snapshot> newPoints)
    {
        newPoints = new List<Vector3Snapshot>();
        if (fullPath is null || fullPath.Count < 3 || fullPath.Count >= BeltBuildOccupancyPolicy.MaximumPathPoints
            || !Valid(source) || !Valid(fullPath[0]) || DistanceSquared(source!, fullPath[0]) > .0001) return false;
        var radius = Math.Sqrt(DistanceSquared(source!, new Vector3Snapshot()));
        var candidates = new List<Vector3Snapshot>();
        for (var i = 1; i < fullPath.Count; i++)
        {
            var point = fullPath[i];
            if (!Valid(point) || Math.Abs(Math.Sqrt(DistanceSquared(point, new Vector3Snapshot())) - radius) > .05)
                return false; // No raised/tilted path or vertical-cover removal branch in this subset.
            candidates.Add(new Vector3Snapshot { X = point.X, Y = point.Y, Z = point.Z });
        }
        newPoints = candidates;
        return true;
    }

    public static bool IsNonRemovingCover(int expectedId, int actualId, bool willRemove) =>
        expectedId > 0 && expectedId <= BeltBuildOccupancyPolicy.MaximumFactorySlots && actualId == expectedId && !willRemove;

    public static bool HasUniqueSourceCentre(int sourceId, Vector3Snapshot source,
        IReadOnlyList<BeltBuildObstacle> allBelts)
    {
        if (sourceId <= 0 || !Valid(source) || allBelts is null || allBelts.Count > BeltBuildOccupancyPolicy.MaximumFactorySlots) return false;
        var found = false;
        foreach (var belt in allBelts)
        {
            if (!Valid(belt.Position)) return false;
            var distance = DistanceSquared(source, belt.Position);
            if (belt.ObjectId == sourceId)
            {
                if (found || distance > .0001) return false;
                found = true;
            }
            else if (distance <= .0625) return false;
        }
        return found;
    }

    public static bool ProvesOnlyOutputChanged(IReadOnlyList<FactoryConnectionSnapshot>? before,
        IReadOnlyList<FactoryConnectionSnapshot>? after, int expectedNewObjectId)
    {
        if (expectedNewObjectId == 0 || expectedNewObjectId == int.MinValue
            || Math.Abs(expectedNewObjectId) > BeltBuildOccupancyPolicy.MaximumFactorySlots
            || !Complete(before) || !Complete(after) || before![0].OtherObjectId != 0) return false;
        for (var slot = 0; slot < 16; slot++)
        {
            var a = before![slot]; var b = after![slot];
            if (slot == 0)
            {
                if (!b.IsOutput || b.OtherObjectId != expectedNewObjectId || b.OtherSlot != 1) return false;
            }
            else if (a.IsOutput != b.IsOutput || a.OtherObjectId != b.OtherObjectId || a.OtherSlot != b.OtherSlot) return false;
        }
        return true;
    }

    public static bool SameEvidence(string? before, string? after) =>
        !string.IsNullOrEmpty(before) && string.Equals(before, after, StringComparison.Ordinal);

    public static string BindEmptyElevatedPathEvidence(string sourceBinding, string emptyPathBinding) =>
        CanonicalStateHash.Combine("belt-elevated-empty-source-cover-v1", sourceBinding, emptyPathBinding);

    public static bool MatchesPreparedCreationBinding(string? preparedBinding, string? sourceBinding,
        string? preparedEmptyPathBinding, string? currentEmptyPathBinding)
    {
        if (string.IsNullOrEmpty(sourceBinding)) return false;
        if (preparedEmptyPathBinding is null)
            return currentEmptyPathBinding is null && SameEvidence(preparedBinding, sourceBinding);
        return SameEvidence(preparedEmptyPathBinding, currentEmptyPathBinding)
            && SameEvidence(preparedBinding, BindEmptyElevatedPathEvidence(sourceBinding!, currentEmptyPathBinding!));
    }

    public static bool ConfirmsPlanEcho(PreparedNormalAction? plan, int itemId, int sourceId, int destinationId)
    {
        var echo = plan?.PlannedBeltPath;
        if (sourceId < 0 || sourceId > 65536 || destinationId < 0 || destinationId > 65536
            || plan is null || !plan.Prepared || plan.BuildKind != "belt" || echo is null
            || echo.NativeValidationMode != "full_path_stage1" || echo.NewObjectCount < 2 || echo.NewObjectCount >= 256
            || plan.PlannedPath is null || plan.PlannedPath.Count != echo.NewObjectCount || plan.SourceObjectId.GetValueOrDefault() != sourceId
            || plan.DestinationObjectId.GetValueOrDefault() != destinationId
            || plan.ItemBudget is null || plan.ItemBudget.Count != 1 || plan.ItemBudget[0] is null || plan.ItemBudget[0].ItemId != itemId
            || plan.ItemBudget[0].Count != echo.NewObjectCount || plan.ItemBudget[0].Direction != "construction-consumption") return false;
        if (sourceId > 0 && destinationId > 0 && sourceId == destinationId) return false;
        if (!ConfirmsUnpoweredSourceFeedEcho(echo, itemId, sourceId, destinationId)) return false;
        return echo.DestinationBindingMode switch
        {
            // Older echoes omit this field (whose contract default is none), but can still bind a device destination.
            "none" => !echo.ReusedDestinationObjectId.HasValue && echo.DestinationPreservationMode is null
                && (destinationId == 0 ? ConfirmsSourceOnlyEcho(echo, sourceId)
                    : IsGridOrLegacy(echo) && ConfirmsDeviceDestinationSourceEcho(echo, sourceId)),
            "native_device_port" => destinationId > 0 && !echo.ReusedDestinationObjectId.HasValue
                && echo.DestinationPreservationMode is null && IsGridOrLegacy(echo)
                && ConfirmsDeviceDestinationSourceEcho(echo, sourceId),
            "non_removing_belt_cover" => itemId == 2001 && destinationId > 0 && echo.ReusedDestinationObjectId == destinationId
                && echo.DestinationPreservationMode == BeltDestinationReusePolicy.PreservationMode
                && ConfirmsTargetCoverSourceEcho(echo, sourceId),
            _ => false,
        };
    }

    private static bool ConfirmsUnpoweredSourceFeedEcho(BeltPathPlanSnapshot echo, int itemId, int sourceId, int destinationId)
    {
        if (!echo.UnpoweredSourceMinerObjectId.HasValue && echo.UnpoweredSourceMinerStateHash is null) return true;
        var id = echo.UnpoweredSourceMinerObjectId.GetValueOrDefault();
        var hash = echo.UnpoweredSourceMinerStateHash;
        return itemId == 2001 && sourceId > 0 && destinationId > 0 && id > 0
            && id <= BeltBuildOccupancyPolicy.MaximumFactorySlots && id != sourceId && id != destinationId
            && echo.SourceBindingMode == "non_removing_belt_cover" && echo.ReusedSourceObjectId == sourceId
            && echo.SourcePreservationMode == BeltSourceRotationPolicy.PreservationMode
            && echo.DestinationBindingMode == "non_removing_belt_cover" && echo.ReusedDestinationObjectId == destinationId
            && echo.DestinationPreservationMode == BeltDestinationReusePolicy.PreservationMode
            && hash is not null && hash.Length == 71 && hash.StartsWith("sha256:", StringComparison.Ordinal)
            && hash.Skip(7).All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
    }

    private static bool ConfirmsSourceOnlyEcho(BeltPathPlanSnapshot echo, int sourceId) => echo.SourceBindingMode switch
    {
        // Preserve legacy free/device echoes, whose old response shape did not bind a source preservation field.
        "none" => sourceId == 0 && !echo.ReusedSourceObjectId.HasValue,
        "native_device_port" => sourceId > 0 && !echo.ReusedSourceObjectId.HasValue,
        "non_removing_belt_cover" => sourceId > 0 && echo.ReusedSourceObjectId == sourceId
            && echo.SourcePreservationMode == BeltSourceRotationPolicy.PreservationMode,
        _ => false,
    };

    private static bool ConfirmsDeviceDestinationSourceEcho(BeltPathPlanSnapshot echo, int sourceId) => echo.SourceBindingMode switch
    {
        "none" => sourceId == 0 && !echo.ReusedSourceObjectId.HasValue,
        "native_device_port" => sourceId > 0 && !echo.ReusedSourceObjectId.HasValue,
        _ => false,
    };

    private static bool IsGridOrLegacy(BeltPathPlanSnapshot echo) => echo.RoutingMode is null
        || echo.RoutingMode == BeltPathModes.NativeGrid;

    private static bool ConfirmsTargetCoverSourceEcho(BeltPathPlanSnapshot echo, int sourceId)
    {
        if (echo.RoutingMode == BeltPathModes.NativeGrid)
            return echo.SourceBindingMode switch
            {
                "none" => sourceId == 0 && !echo.ReusedSourceObjectId.HasValue && echo.SourcePreservationMode is null,
                "non_removing_belt_cover" => sourceId > 0 && echo.ReusedSourceObjectId == sourceId
                    && echo.SourcePreservationMode == BeltSourceRotationPolicy.PreservationMode,
                _ => false,
            };
        return echo.RoutingMode == BeltPathModes.NativeGeodesic
            && echo.SourceBindingMode == "non_removing_belt_cover" && sourceId > 0
            && echo.ReusedSourceObjectId == sourceId && echo.SourcePreservationMode == BeltSourceRotationPolicy.PreservationMode;
    }

    private static bool Complete(IReadOnlyList<FactoryConnectionSnapshot>? connections) => connections is not null
        && connections.Count == 16 && connections.Select((c, i) => c is not null && c.Slot == i
            && c.OtherObjectId != int.MinValue && Math.Abs(c.OtherObjectId) <= BeltBuildOccupancyPolicy.MaximumFactorySlots
            && c.OtherSlot >= 0 && c.OtherSlot < 16).All(valid => valid);
    private static bool Valid(Vector3Snapshot? p) => p is not null && Finite(p.X) && Finite(p.Y) && Finite(p.Z)
        && Math.Abs(p.X) <= 10000 && Math.Abs(p.Y) <= 10000 && Math.Abs(p.Z) <= 10000
        && DistanceSquared(p, new Vector3Snapshot()) >= 1;
    private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    private static double DistanceSquared(Vector3Snapshot a, Vector3Snapshot b) =>
        Math.Pow((double)a.X - b.X, 2) + Math.Pow((double)a.Y - b.Y, 2) + Math.Pow((double)a.Z - b.Z, 2);
}
