namespace Spherewright.Contracts.Actions;

public sealed class NativeStorageConfigurationPlan
{
    public int EvidenceVersion { get; set; } = 1;
    public int SelectedObjectId { get; set; }
    public int TargetObjectId { get; set; }
    public int RootObjectId { get; set; }
    public int StorageItemId { get; set; }
    public List<int> LayerObjectIds { get; set; } = new();
    public List<int> AttachmentObjectIds { get; set; } = new();
    public bool PreservesOtherLayers { get; set; }
}
