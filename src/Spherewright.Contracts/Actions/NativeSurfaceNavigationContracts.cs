using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Actions;

public sealed class NativeSurfaceNavigationPlan
{
    public int EvidenceVersion { get; set; } = 1;
    public string NativeOperation { get; set; } = "PlayerNavigation.Navigate";
    public int PlanetId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string PlayerActionStateHash { get; set; } = string.Empty;
    public Vector3Snapshot Target { get; set; } = new();
    public double ArcDistanceMetres { get; set; }
    public double NativeFlyThresholdMetres { get; set; }
    public double NativeSailThresholdMetres { get; set; }
    public float ArrivalToleranceMetres { get; set; }
    public long MaximumGameTicks { get; set; }
    public long LandingReserveGameTicks { get; set; }
    public double EnergyUpperPerTickJoules { get; set; }
    public double RequiredLoadedEnergyJoules { get; set; }
    public double ProtectedEnergyFloorJoules { get; set; }
    public string BindingHash { get; set; } = string.Empty;
}

public sealed class NativeSurfaceNavigationReadback
{
    public int EvidenceVersion { get; set; } = 1;
    public string PlanBindingHash { get; set; } = string.Empty;
    public bool NativeNavigationStarted { get; set; }
    public bool NativeFlyObserved { get; set; }
    public bool NativeArrivalObserved { get; set; }
    public bool GroundedStopVerified { get; set; }
    public bool NativeNavigationReleased { get; set; }
    public long NativeGameTicks { get; set; }
    public double? FinalDistanceMetres { get; set; }
    public double? FinalSurfaceDistanceMetres { get; set; }
    public string MovementState { get; set; } = string.Empty;
}
