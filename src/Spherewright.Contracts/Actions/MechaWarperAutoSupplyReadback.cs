namespace Spherewright.Contracts.Actions;

// Immediate checkbox-write boundary. Later normal native replenishment is separate.
public sealed class MechaWarperAutoSupplyReadback
{
    public long CapturedAtGameTick { get; set; }
    public string EvidenceScope { get; set; } = "synchronous_native_warper_auto_supply_enable";
    public bool EnabledBefore { get; set; }
    public bool? EnabledAfter { get; set; }
    public int PackageCountBefore { get; set; }
    public int PackageIncBefore { get; set; }
    public int ChamberCountBefore { get; set; }
    public int ChamberIncBefore { get; set; }
    public int? PackageCountAfter { get; set; }
    public int? PackageIncAfter { get; set; }
    public int? ChamberCountAfter { get; set; }
    public int? ChamberIncAfter { get; set; }
    public bool ReadbackComplete { get; set; }
    public bool PackageContentsPreserved { get; set; }
    public bool ConservationVerified { get; set; }
}
