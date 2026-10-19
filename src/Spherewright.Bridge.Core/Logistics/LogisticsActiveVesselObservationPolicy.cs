using Spherewright.Contracts.Logistics;

namespace Spherewright.Bridge.Core.Logistics;

/// <summary>Validation of copied Native scalars only; no simulation or history.</summary>
public static class LogisticsActiveVesselObservationPolicy
{
    public const int MaximumRecords = 32;

    public static string? ValidateOwnedRemoteStationRequest(
        bool directReadOnlyRoute,
        int planetId,
        int objectId,
        bool hasGeometry,
        bool hasMaterialSelection)
    {
        if (!directReadOnlyRoute) return "owned_remote_station_read_only";
        if (planetId <= 0 || objectId <= 0) return "owned_remote_station_positive_identity_required";
        if (hasGeometry || hasMaterialSelection) return "owned_remote_station_details_only";
        return null;
    }

    public static LogisticsActiveVesselObservation Capture(
        bool isInterstellar,
        int ownerGId,
        int nativeWorkingVesselCount,
        int nativeArrayLength,
        IReadOnlyList<LogisticsActiveVesselFacts>? copiedPrefix,
        long capturedAtGameTick)
    {
        LogisticsActiveVesselObservation Unavailable(string reason) => new()
        {
            CapturedAtGameTick = capturedAtGameTick,
            MaximumRecords = MaximumRecords,
            ReasonCode = reason,
        };

        if (capturedAtGameTick < 0 || nativeWorkingVesselCount < 0 || nativeArrayLength < -1)
            return Unavailable("active_vessel_counter_invalid");
        if (!isInterstellar)
        {
            if (nativeWorkingVesselCount != 0) return Unavailable("non_interstellar_active_vessel_counter");
            return new()
            {
                State = "not_applicable", Complete = true,
                CapturedAtGameTick = capturedAtGameTick, MaximumRecords = MaximumRecords,
                NativeWorkingVesselCount = 0,
            };
        }
        if (ownerGId <= 0) return Unavailable("active_vessel_owner_identity_unavailable");
        if (nativeWorkingVesselCount > 0 && nativeArrayLength < nativeWorkingVesselCount)
            return Unavailable("active_vessel_pool_incomplete");

        var requiredPrefix = Math.Min(nativeWorkingVesselCount, MaximumRecords);
        if ((copiedPrefix?.Count ?? 0) != requiredPrefix)
            return Unavailable("active_vessel_prefix_incomplete");
        var result = new LogisticsActiveVesselObservation
        {
            State = nativeWorkingVesselCount > MaximumRecords ? "partial" : "observed",
            CapturedAtGameTick = capturedAtGameTick,
            MaximumRecords = MaximumRecords,
            NativeWorkingVesselCount = nativeWorkingVesselCount,
            Complete = nativeWorkingVesselCount <= MaximumRecords,
            Truncated = nativeWorkingVesselCount > MaximumRecords,
            ReasonCode = nativeWorkingVesselCount > MaximumRecords ? "active_vessel_limit" : null,
        };
        var shipIndices = new HashSet<int>();
        foreach (var vessel in copiedPrefix ?? Array.Empty<LogisticsActiveVesselFacts>())
        {
            if (vessel is null || vessel.ShipIndex < 0 || vessel.ShipIndex >= nativeArrayLength
                || !shipIndices.Add(vessel.ShipIndex) || vessel.OtherGId <= 0 || vessel.OtherGId == ownerGId
                || vessel.ItemId < 0 || vessel.ItemCount < 0 || (vessel.ItemCount > 0 && vessel.ItemId == 0)
                || vessel.Inc < 0 || vessel.WarperCount < 0 || vessel.Stage < -2 || vessel.Stage > 2
                || (vessel.Direction != -1 && vessel.Direction != 1)
                || !IsFinite(vessel.T) || !IsFinite(vessel.USpeed) || vessel.USpeed < 0
                || !IsFinite(vessel.WarpState))
                return Unavailable("active_vessel_scalar_or_identity_invalid");

            result.Vessels.Add(new LogisticsActiveVesselSnapshot
            {
                OwnerGId = ownerGId, ShipIndex = vessel.ShipIndex, Gene = vessel.Gene,
                OtherGId = vessel.OtherGId, ItemId = vessel.ItemId, ItemCount = vessel.ItemCount,
                Inc = vessel.Inc, Stage = vessel.Stage, Direction = vessel.Direction,
                T = vessel.T, USpeed = vessel.USpeed, WarpState = vessel.WarpState,
                WarperCount = vessel.WarperCount,
            });
        }
        return result;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>Copied facts; never pass Unity objects into the policy.</summary>
public sealed class LogisticsActiveVesselFacts
{
    public int ShipIndex { get; set; }
    public int Gene { get; set; }
    public int OtherGId { get; set; }
    public int ItemId { get; set; }
    public int ItemCount { get; set; }
    public int Inc { get; set; }
    public int Stage { get; set; }
    public int Direction { get; set; }
    public float T { get; set; }
    public float USpeed { get; set; }
    public float WarpState { get; set; }
    public int WarperCount { get; set; }
}
