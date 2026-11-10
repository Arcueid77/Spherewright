namespace Spherewright.Contracts.Actions;

public sealed class NativeBeltInsertionPlan
{
    public int EvidenceVersion { get; set; } = 1;
    public int SelectedObjectId { get; set; }
    public int PathId { get; set; }
    public int PathLengthCells { get; set; }
    public List<int> BeltObjectIds { get; set; } = new();
    public int ItemId { get; set; }
    public int Count { get; set; } = 1;
    public int Inc { get; set; }
    public bool EmptyIndependentPath { get; set; }
    public string NativeOperation { get; set; } = "CargoTraffic.PutItemOnBelt";
}
