using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Celestial;

// One bounded copy of the existing Native swarm. The reserved native orbit 30
// is disclosed separately and is never an ordinary design target.
public sealed class DysonSwarmOrbitState
{
    public bool NativeBindingVerified { get; set; }
    public bool CoverageComplete { get; set; }
    public int Cursor { get; set; }
    public int Capacity { get; set; }
    public float MinimumRadius { get; set; }
    public float MaximumRadius { get; set; }
    public float AvoidRadius { get; set; }
    public int ActiveSailCount { get; set; }
    public int InFlightSailCount { get; set; }
    public SwarmOrbitSnapshot? ReservedOrbit { get; set; }
    public List<SwarmOrbitSnapshot> Orbits { get; set; } = new();
    public string StateHash { get; set; } = string.Empty;
}

public sealed class DysonSwarmOrbitRequest
{
    public float Radius { get; set; }
    public float InclinationDegrees { get; set; }
    public float LongitudeDegrees { get; set; }
    public bool PreviewOnly { get; set; }
    public DysonSwarmOrbitRequest Copy() => new()
    { Radius = Radius, InclinationDegrees = InclinationDegrees, LongitudeDegrees = LongitudeDegrees, PreviewOnly = PreviewOnly };
}

public sealed class DysonSwarmOrbitPlan
{
    public int EvidenceVersion { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int PlanetId { get; set; }
    public int StarId { get; set; }
    public DysonSwarmOrbitRequest Orbit { get; set; } = new();
    public string BeforeStateHash { get; set; } = string.Empty;
    public string BindingHash { get; set; } = string.Empty;
    public int PlannedOrbitId { get; set; }
    public float NativeRadius { get; set; }
    public QuaternionSnapshot NativeRotation { get; set; } = new();
    public Vector3Snapshot NativeUp { get; set; } = new();
    public int ExistingActiveSails { get; set; }
    public int ExistingInFlightSails { get; set; }
    public bool NativeChecksComplete { get; set; }
    public bool ConsumesInventoryOnCommit => false;
}

public sealed class DysonSwarmOrbitReadback
{
    public int EvidenceVersion { get; set; }
    public int StarId { get; set; }
    public int PlannedOrbitId { get; set; }
    public int OrbitId { get; set; }
    public string PlanBindingHash { get; set; } = string.Empty;
    public string BeforeStateHash { get; set; } = string.Empty;
    public string AfterStateHash { get; set; } = string.Empty;
    public bool NativeOperationInvoked { get; set; }
    public bool ExactNewOrbitVerified { get; set; }
    public bool ExistingOrbitsAndSailCountsPreserved { get; set; }
    public bool InventoryPreserved { get; set; }
}
