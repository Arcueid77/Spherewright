using Spherewright.Contracts.Players;

namespace Spherewright.Contracts.Actions;

public sealed class PrepareVoyagerFlightRequest
{
    public int PlanetId { get; set; }
    public string ExpectedRouteStateHash { get; set; } = string.Empty;
    public string ExpectedPlayerStateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
    // Empty starts one bounded outbound journey. A return must name the same journey.
    public string? JourneyId { get; set; }
}

public sealed class PrepareCancelVoyagerFlightRequest
{
    public int PlanetId { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
}

// Copied on the Unity main thread, including while no local planet exists.
// The action's original PlanetId remains its origin; these actual IDs are separate.
public sealed class VoyagerFlightReadback
{
    public string JourneyId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public bool IsReturnLeg { get; set; }
    public int OriginStarId { get; set; }
    public int DestinationStarId { get; set; }
    public int? SelectedDestinationPlanetId { get; set; }
    public int? ActualStarId { get; set; }
    public int? ActualPlanetId { get; set; }
    public long CapturedAtGameTick { get; set; }
    public long FlightDeadlineGameTick { get; set; }
    public System.DateTimeOffset JourneyDeadlineUtc { get; set; }
    public System.DateTimeOffset? EffectiveJourneyDeadlineUtc { get; set; }
    public long WarpGameTicks { get; set; }
    public long MaximumWarpGameTicks { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public double? LoadedEnergyJoules { get; set; }
    public double ReturnEnergyFloorJoules { get; set; }
    public int NativeWarperCount { get; set; }
    public int PackageWarperCount { get; set; }
    public int NativeWarpersConsumed { get; set; }
    public float? NativeWarpState { get; set; }
    public bool NativeWarpCommand { get; set; }
    public bool EntryAttempted { get; set; }
    public bool StopRequested { get; set; }
    public bool NativeExitObserved { get; set; }
    public bool BindingVerified { get; set; }
    public System.DateTimeOffset ControlLeaseExpiresAtUtc { get; set; }
    public long? StopDeadlineGameTick { get; set; }
    public System.DateTimeOffset? StopDeadlineUtc { get; set; }
    public double? RemainingDistanceMeters { get; set; }
    public string? FailureReason { get; set; }
    public VoyagerLandingHandoffReadback? FirstLandingHandoff { get; set; }
}

// One detached first-attempt observation, never a later-current reconstruction.
// Terrain/ground diagnostics do not certify physics or grant execution.
public sealed class VoyagerLandingHandoffReadback
{
    public long CapturedAtGameTick { get; set; }
    public bool Proved { get; set; }
    public string? BlockerCode { get; set; }
    public string? CaptureStage { get; set; }
    public string? CaptureExceptionType { get; set; }
    public NativePlanetTerrainPhysicsSnapshot? Terrain { get; set; }
    public NativePlayerGroundContactSnapshot? GroundContact { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public double? LoadedEnergyJoules { get; set; }
    public double? PerNativeTickEnergyJoules { get; set; }

    public VoyagerLandingHandoffReadback Copy() => new()
    {
        CapturedAtGameTick = CapturedAtGameTick, Proved = Proved, BlockerCode = BlockerCode,
        CaptureStage = CaptureStage, CaptureExceptionType = CaptureExceptionType,
        Terrain = Terrain?.Copy(), GroundContact = GroundContact?.Copy(),
        CoreEnergyJoules = CoreEnergyJoules, LoadedEnergyJoules = LoadedEnergyJoules,
        PerNativeTickEnergyJoules = PerNativeTickEnergyJoules,
    };
}
