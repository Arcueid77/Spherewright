namespace Spherewright.Contracts.Actions;

public sealed class PrepareSupplyMechaWarpersRequest
{
    public int PlanetId { get; set; }
    public int Count { get; set; } = 20;
    public string ExpectedPlayerStateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
}

// Immediate native-call boundary, before later ordinary consumption can change stock.
// Nullable after fields remain unknown if the native call or its readback did not finish.
public sealed class MechaWarperSupplyReadback
{
    public long CapturedAtGameTick { get; set; }
    public string EvidenceScope { get; set; } = "synchronous_native_empty_warper_chamber_supply";
    public int ItemId { get; set; } = 1210;
    public int RequestedCount { get; set; }
    public int? ActualStored { get; set; }
    public int PackageCountBefore { get; set; }
    public int PackageIncBefore { get; set; }
    public int ChamberCountBefore { get; set; }
    public int ChamberIncBefore { get; set; }
    public bool AutoReplenishWarperBefore { get; set; }
    public int? PackageCountAfter { get; set; }
    public int? PackageIncAfter { get; set; }
    public int? ChamberCountAfter { get; set; }
    public int? ChamberIncAfter { get; set; }
    public bool? AutoReplenishWarperAfter { get; set; }
    public bool? NativeReturnedSuccess { get; set; }
    public bool ReadbackComplete { get; set; }
    public bool ConservationVerified { get; set; }
}
