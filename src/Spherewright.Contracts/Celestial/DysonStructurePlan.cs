using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Celestial;

public static class DysonStructureKinds
{
    public const string Node = "node";
    public const string Frame = "frame";
    public const string Shell = "shell";
}

public sealed class DysonStructureRequest
{
    public string Kind { get; set; } = string.Empty;
    public int LayerId { get; set; }
    public int PrototypeId { get; set; }
    public Vector3Snapshot? LocalPosition { get; set; }
    public int NodeAId { get; set; }
    public int NodeBId { get; set; }
    public bool Euler { get; set; }
    public List<int> BoundaryNodeIds { get; set; } = new();
    public bool PreviewOnly { get; set; }

    public DysonStructureRequest Copy() => new()
    {
        Kind = Kind, LayerId = LayerId, PrototypeId = PrototypeId,
        LocalPosition = LocalPosition is null ? null : new Vector3Snapshot
        {
            X = LocalPosition.X, Y = LocalPosition.Y, Z = LocalPosition.Z,
        },
        NodeAId = NodeAId, NodeBId = NodeBId, Euler = Euler,
        BoundaryNodeIds = BoundaryNodeIds.ToList(), PreviewOnly = PreviewOnly,
    };
}

public sealed class DysonStructurePlan
{
    public int EvidenceVersion { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int PlanetId { get; set; }
    public int StarId { get; set; }
    public DysonStructureRequest Structure { get; set; } = new();
    public string BeforeDesignStateHash { get; set; } = string.Empty;
    public string BindingHash { get; set; } = string.Empty;
    public string NativeOperation { get; set; } = string.Empty;
    public int PlannedObjectId { get; set; }
    public float UnlockedNodeLatitudeDegrees { get; set; }
    public bool NativeChecksComplete { get; set; }
    public int RequiredStructurePoints { get; set; }
    public int RequiredCellPoints { get; set; }
    public int RequiredCarrierRockets => RequiredStructurePoints;
    public int RequiredSolarSails => RequiredCellPoints;
    public bool ConsumesInventoryOnCommit => false;
}

public sealed class DysonStructureReadback
{
    public int EvidenceVersion { get; set; }
    public int StarId { get; set; }
    public int LayerId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int ObjectId { get; set; }
    public int PlannedObjectId { get; set; }
    public bool NativeOperationInvoked { get; set; }
    public string PlanBindingHash { get; set; } = string.Empty;
    public string BeforeDesignStateHash { get; set; } = string.Empty;
    public string AfterDesignStateHash { get; set; } = string.Empty;
    public bool ExactNativeObjectVerified { get; set; }
    public bool ExistingStructurePreserved { get; set; }
    public bool InventoryPreserved { get; set; }
    public int AddedMaximumStructurePoints { get; set; }
    public int AddedMaximumCellPoints { get; set; }
}
