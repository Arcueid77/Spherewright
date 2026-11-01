namespace Spherewright.Contracts.Factory;

// Detail-only, captured on the game thread. None of these observations is a
// launch receipt or permission to advance the native simulation.
public sealed class EjectorStateSnapshot
{
    public bool NativeBindingVerified { get; set; }
    public int EjectorComponentId { get; set; }
    public int StarId { get; set; }
    public int SailItemId { get; set; }
    public int SailCount { get; set; }
    public int SailIncPoints { get; set; }
    public int OrbitId { get; set; }
    public int RuntimeOrbitId { get; set; }
    public bool AutomaticOrbitSelection { get; set; }
    public bool BoostEnabled { get; set; }
    public int Direction { get; set; }
    public int CycleProgressRaw { get; set; }
    public int ChargeDurationRaw { get; set; }
    public int CooldownDurationRaw { get; set; }
    public string NativeTargetState { get; set; } = string.Empty;
    public bool FiredAnimationActive { get; set; }
    public bool FiredAnimationIsLaunchReceipt => false;
    public bool SwarmAvailable { get; set; }
    public bool OrbitCoverageComplete { get; set; }
    public int? ActiveSailCount { get; set; }
    public int? InFlightSailCount { get; set; }
    public int? StarEjectorCount { get; set; }
    public bool StarEjectorCoverageComplete { get; set; }
    public List<SwarmOrbitSnapshot> Orbits { get; set; } = new();
    public string ConfigurationStateHash { get; set; } = string.Empty;
}

public sealed class SwarmOrbitSnapshot
{
    public int OrbitId { get; set; }
    public bool Enabled { get; set; }
    public float Radius { get; set; }
    public QuaternionSnapshot Rotation { get; set; } = new();
    public int SailCount { get; set; }
    public string StateHash { get; set; } = string.Empty;
}

public sealed class EjectorOrbitPlanSnapshot
{
    public int OrbitId { get; set; }
    public int StarId { get; set; }
    public string ConfigurationStateHash { get; set; } = string.Empty;
    public string OrbitStateHash { get; set; } = string.Empty;
}
