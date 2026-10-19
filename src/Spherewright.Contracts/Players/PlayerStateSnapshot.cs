using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Players;

public sealed class PlayerStateSnapshot
{
    public string SessionId { get; set; } = string.Empty;

    public int PlanetId { get; set; }

    public long CapturedAtGameTick { get; set; }

    public string StateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;

    public Vector3Snapshot Position { get; set; } = new Vector3Snapshot();

    public string MovementState { get; set; } = string.Empty;

    public bool IsAlive { get; set; }

    public bool IsOnPlanet { get; set; }

    public bool IsFlying { get; set; }

    public bool IsSailing { get; set; }

    public float Speed { get; set; }

    public double CoreEnergy { get; set; }

    public double CoreEnergyCapacity { get; set; }

    public double ReactorEnergy { get; set; }

    public int ReactorItemId { get; set; }

    public string? ReactorItemName { get; set; }

    public int ReactorItemInc { get; set; }

    public bool AutoReplenishFuel { get; set; }

    public int FuelStorageSlotCount { get; set; }

    public int FuelStorageOccupiedSlotCount { get; set; }

    public List<PlayerInventoryItem> FuelStorage { get; set; } = new List<PlayerInventoryItem>();

    public float BuildArea { get; set; }

    public int InventorySlotCount { get; set; }

    public int InventoryOccupiedSlotCount { get; set; }

    public List<PlayerInventoryItem> Inventory { get; set; } = new List<PlayerInventoryItem>();

    public PlayerInventoryItem? InHandItem { get; set; }

    public bool AutoManageResearchItems { get; set; }

    public double MechaResearchPower { get; set; }

    public List<MechaResearchItemSnapshot> MechaResearchItemBuffer { get; set; } = new List<MechaResearchItemSnapshot>();

    public List<HandcraftTaskSnapshot> HandcraftQueue { get; set; } = new List<HandcraftTaskSnapshot>();

    public ConstructionDroneSnapshot ConstructionDrones { get; set; } = new ConstructionDroneSnapshot();

    public NativePlayerPhysicsSnapshot? NativePhysics { get; set; }
}

public sealed class MechaResearchItemSnapshot
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int PointCount { get; set; }

    public int WholeItemCount { get; set; }

    public int RemainderPoints { get; set; }
}

public sealed class PlayerInventoryItem
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public int Inc { get; set; }

    public int SlotCount { get; set; }
}

public sealed class HandcraftTaskSnapshot
{
    public int QueueIndex { get; set; }

    public int RecipeId { get; set; }

    public string RecipeName { get; set; } = string.Empty;

    public int RemainingCraftCount { get; set; }

    public int Progress { get; set; }

    public int ProgressRequired { get; set; }

    public int ParentTaskIndex { get; set; }

    public bool IngredientsReserved { get; set; }

    public List<PlayerItemAmount> Inputs { get; set; } = new List<PlayerItemAmount>();

    public List<PlayerItemAmount> Outputs { get; set; } = new List<PlayerItemAmount>();
}

public sealed class PlayerItemAmount
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public int BufferedCount { get; set; }
}

public sealed class ConstructionDroneSnapshot
{
    public bool Enabled { get; set; }

    public bool ConstructionEnabled { get; set; }

    public int Total { get; set; }

    public int Alive { get; set; }

    public int Idle { get; set; }

    public int Working { get; set; }

    public int PendingBuildTargets { get; set; }

    public int PendingRepairTargets { get; set; }
}
