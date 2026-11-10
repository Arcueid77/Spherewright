namespace Spherewright.Contracts.Actions;

public sealed class BeltPathReversalPlan
{
    public int EvidenceVersion { get; set; } = 1;
    public int SelectedObjectId { get; set; }
    public int PathId { get; set; }
    public int PathLengthCells { get; set; }
    public List<int> BeltObjectIds { get; set; } = new();
    public List<int> SorterObjectIds { get; set; } = new();
    public int PilerObjectId { get; set; }
    public List<int> PilerLayerObjectIds { get; set; } = new();
    public bool EmptyIndependentPath { get; set; }
    public string NativeOperation { get; set; } = "UIBeltWindow.OnReverseButtonClick";
}

public sealed class BeltPathReversalReadback
{
    public List<int> BeforeBeltObjectIds { get; set; } = new();
    public List<int> AfterBeltObjectIds { get; set; } = new();
    public int PilerObjectId { get; set; }
    public int BeforePilerMode { get; set; }
    public int AfterPilerMode { get; set; }
    public bool CompletePackageCountIncPreserved { get; set; }
    public bool OtherPilerLayersPreserved { get; set; }
}
