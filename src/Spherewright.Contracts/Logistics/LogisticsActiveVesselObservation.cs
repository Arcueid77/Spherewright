namespace Spherewright.Contracts.Logistics;

/// <summary>One bounded tick of Native active ships, not delivery/cycle proof.</summary>
public sealed class LogisticsActiveVesselObservation
{
    // observed | partial | unavailable | not_applicable
    public string State { get; set; } = "unavailable";

    public string Coverage { get; set; } = "native_active_work_ship_data";

    public string? ReasonCode { get; set; }

    public long CapturedAtGameTick { get; set; }

    public int MaximumRecords { get; set; } = 32;

    // Null when the counter/pool identity cannot be trusted.
    public int? NativeWorkingVesselCount { get; set; }

    public bool Complete { get; set; }

    public bool Truncated { get; set; }

    public List<LogisticsActiveVesselSnapshot> Vessels { get; set; } = new();
}

/// <summary>
/// Raw copied Native scalars. OwnerGId/ShipIndex/Gene distinguish dispatches only
/// within a finite observation window; they are not permanent trip identities.
/// </summary>
public sealed class LogisticsActiveVesselSnapshot
{
    public int OwnerGId { get; set; }

    // The Native carrier's shipIndex, NOT its compressed active-array index.
    public int ShipIndex { get; set; }

    public int Gene { get; set; }

    public int OtherGId { get; set; }

    public int ItemId { get; set; }

    public int ItemCount { get; set; }

    public int Inc { get; set; }

    public int Stage { get; set; }

    // Native +1 is away from owner, -1 returns to owner; not supply/demand.
    public int Direction { get; set; }

    // Stage-local Native animation parameter, not total route progress or ETA.
    public float T { get; set; }

    public float USpeed { get; set; }

    public float WarpState { get; set; }

    public int WarperCount { get; set; }
}
