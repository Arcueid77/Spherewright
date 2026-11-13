namespace Spherewright.Contracts.Actions;

public sealed class WindTurbineDismantlePreview
{
    public int EvidenceVersion { get; set; } = 1;
    public long CapturedAtGameTick { get; set; }
    public int TargetEntityId { get; set; }
    public int PowerNodeId { get; set; }
    public int GeneratorId { get; set; }
    public int PowerNetworkId { get; set; }
    public int ExpectedBuildingItemId { get; set; } = 2203;
    public int ExpectedRecoveryCount { get; set; } = 1;
    public long GenerationCapacityLostPerTick { get; set; }
    public long GenerationCapacityLostWatts { get; set; }
    public string PowerTopologyHash { get; set; } = string.Empty;
    public bool Complete { get; set; }
    public bool PreviewOnly { get; set; }
    public bool NativeDismantleAvailable { get; set; }
    public bool PlayerSettled { get; set; }
    public bool TargetWithinReach { get; set; }
    public bool InventoryRoomAvailable { get; set; }
    public List<int> AffectedConsumerEntityIds { get; set; } = new List<int>();
    public List<int> DisconnectedConsumerEntityIds { get; set; } = new List<int>();
    public List<WindPowerNetworkSnapshot> ProjectedNetworks { get; set; } = new List<WindPowerNetworkSnapshot>();
    public List<string> Blockers { get; set; } = new List<string>();
}

public sealed class WindPowerNetworkSnapshot
{
    public int? NativeNetworkId { get; set; }
    public List<int> NodeEntityIds { get; set; } = new List<int>();
    public List<int> GeneratorEntityIds { get; set; } = new List<int>();
    public List<int> AccumulatorEntityIds { get; set; } = new List<int>();
    public List<int> ExchangerEntityIds { get; set; } = new List<int>();
    public List<int> ConsumerEntityIds { get; set; } = new List<int>();
}

public sealed class WindTurbineDismantleReadback
{
    public int EvidenceVersion { get; set; } = 1;
    public int TargetEntityId { get; set; }
    public string PreparedPowerTopologyHash { get; set; } = string.Empty;
    public string ResultPowerTopologyHash { get; set; } = string.Empty;
    public bool TargetAbsent { get; set; }
    public bool ExactBuildingRecovery { get; set; }
    public bool InventoryIncPreserved { get; set; }
    public bool SurvivingEntitiesPreserved { get; set; }
    public bool FactoryConnectionsPreserved { get; set; }
    public bool PowerConnectivityVerified { get; set; }
    public bool UnrelatedPowerNetworksPreserved { get; set; }
    public List<int> DisconnectedConsumerEntityIds { get; set; } = new List<int>();
    public List<WindPowerNetworkSnapshot> ResultNetworks { get; set; } = new List<WindPowerNetworkSnapshot>();
}
