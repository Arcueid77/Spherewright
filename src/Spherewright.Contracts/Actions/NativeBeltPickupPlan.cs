using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Actions;

public sealed class NativeBeltPickupPlan
{
    public int EvidenceVersion { get; set; } = 1;
    public int SelectedObjectId { get; set; }
    public int PathId { get; set; }
    public List<int> BeltObjectIds { get; set; } = new();
    public List<int> InserterObjectIds { get; set; } = new();
    public List<BeltCargoItemSnapshot> Items { get; set; } = new();
    public string NativeOperation { get; set; } = "CargoTraffic.PickupBeltItems";
}
