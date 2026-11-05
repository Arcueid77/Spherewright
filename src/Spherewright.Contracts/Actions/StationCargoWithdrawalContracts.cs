namespace Spherewright.Contracts.Actions;

public sealed class StationCargoWithdrawalPlan
{
    public int EvidenceVersion { get; set; } = 1;
    public int EntityId { get; set; }
    public int StorageIndex { get; set; }
    public int ItemId { get; set; }
    public int Count { get; set; }
    public string NativeOperation { get; set; } = string.Empty;
}

public sealed class StationCargoWithdrawalReadback
{
    public int EvidenceVersion { get; set; } = 1;
    public int EntityId { get; set; }
    public int StorageIndex { get; set; }
    public int ItemId { get; set; }
    public int RequestedCount { get; set; }
    public int StationCountBefore { get; set; }
    public int StationCountAfter { get; set; }
    public int PackageCountBefore { get; set; }
    public int PackageCountAfter { get; set; }
    public int StationIncBefore { get; set; }
    public int StationIncAfter { get; set; }
    public int PackageIncBefore { get; set; }
    public int PackageIncAfter { get; set; }
    public bool ExactNativeBindingVerified { get; set; }
    public bool StationOtherStatePreserved { get; set; }
    public bool PackageOtherStockPreserved { get; set; }
    public bool ConservationVerified { get; set; }
    public long CapturedAtGameTick { get; set; }
}
