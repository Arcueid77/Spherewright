namespace Spherewright.Contracts.Actions;

public sealed class AssemblerInputReadback
{
    public int EvidenceVersion { get; set; } = 1;
    public int EntityId { get; set; }
    public int RecipeId { get; set; }
    public int ItemId { get; set; }
    public int RequestedCount { get; set; }
    public int InputCountBefore { get; set; }
    public int InputCountAfter { get; set; }
    public int InputIncBefore { get; set; }
    public int InputIncAfter { get; set; }
    public int PackageCountBefore { get; set; }
    public int PackageCountAfter { get; set; }
    public int PackageIncBefore { get; set; }
    public int PackageIncAfter { get; set; }
    public bool ExactNativeBindingVerified { get; set; }
    public bool OtherBuffersPreserved { get; set; }
    public bool OtherPackageStockPreserved { get; set; }
    public bool ConservationVerified { get; set; }
    public long CapturedAtGameTick { get; set; }
}
