using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Actions;

// Captured around the synchronous native UI operation, before ordinary delivery resumes.
// A later inspect may legitimately report different inventory; neither view replaces the other.
public sealed class StorageConfigurationReadback
{
    public long CapturedAtGameTick { get; set; }
    public int EntityId { get; set; }
    public int? SelectedObjectId { get; set; }
    public bool? OtherLayersPreserved { get; set; }
    public List<int>? PreservedLayerObjectIds { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string EvidenceScope { get; set; } = "synchronous_native_configuration_boundary";
    public StorageConfigurationSnapshot ConfigurationBefore { get; set; } = new StorageConfigurationSnapshot();
    public StorageConfigurationSnapshot ConfigurationAfter { get; set; } = new StorageConfigurationSnapshot();
    // Nonempty grid contents in order, NOT native grid indices (empty grids are omitted).
    public List<FactoryBufferSnapshot> BuffersBefore { get; set; } = new List<FactoryBufferSnapshot>();
    public List<FactoryBufferSnapshot> BuffersAfter { get; set; } = new List<FactoryBufferSnapshot>();
    public int VerifiedConnectionCount { get; set; }
}
