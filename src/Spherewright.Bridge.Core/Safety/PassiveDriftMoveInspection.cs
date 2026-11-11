using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// One immutable server observation. Default capture serves an explicitly
// opted-in Drift Move. Hover capture serves separately retained low-speed
// inventory interactions through the same bounded inspection contract.
// It never renews its lifetime at prepare or commit and cannot choose a target.
public sealed class PassiveDriftMoveInspection
{
    public const double MaximumAgeSeconds = 2d;
    public const double MaximumDisplacementMetres = 0.05d;
    public const double MaximumSpeedMetresPerSecond = 0.15d;

    private readonly string _sessionId;
    private readonly int _planetId;
    private readonly float _x, _y, _z;
    private readonly double _capturedAtSeconds;
    private readonly double _coreEnergyCapacity;
    private readonly long _capturedAtGameTick;
    private readonly bool _allowHover;

    private PassiveDriftMoveInspection(PlayerStateSnapshot player, double nowSeconds, DateTimeOffset utcNow, bool allowHover)
    {
        StateHash = player.StateHash;
        _sessionId = player.SessionId;
        _planetId = player.PlanetId;
        _x = player.Position.X; _y = player.Position.Y; _z = player.Position.Z;
        _capturedAtSeconds = nowSeconds;
        _coreEnergyCapacity = player.CoreEnergyCapacity;
        _capturedAtGameTick = player.CapturedAtGameTick;
        _allowHover = allowHover;
        ExpiresAtUtc = utcNow.AddSeconds(MaximumAgeSeconds);
    }

    public string StateHash { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    public static PassiveDriftMoveInspection? Capture(
        PlayerStateSnapshot player, double nowSeconds, DateTimeOffset utcNow, bool allowHover = false) =>
        IsFinite(nowSeconds) && nowSeconds >= 0d && Eligible(player, allowHover)
            && string.Equals(player.StateHash, CanonicalStateHash.PlayerAction(player), StringComparison.Ordinal)
            ? new PassiveDriftMoveInspection(player, nowSeconds, utcNow, allowHover) : null;

    public bool Allows(string expectedInspectionHash, PlayerStateSnapshot current, double nowSeconds)
    {
        var age = nowSeconds - _capturedAtSeconds;
        if (!IsFinite(age) || age < 0d || age > MaximumAgeSeconds || !Eligible(current, _allowHover)
            || !string.Equals(expectedInspectionHash, StateHash, StringComparison.Ordinal)
            || !string.Equals(current.SessionId, _sessionId, StringComparison.Ordinal)
            || current.PlanetId != _planetId || current.CoreEnergyCapacity != _coreEnergyCapacity)
            return false;

        var dx = (double)current.Position.X - _x;
        var dy = (double)current.Position.Y - _y;
        var dz = (double)current.Position.Z - _z;
        if (dx * dx + dy * dy + dz * dz > MaximumDisplacementMetres * MaximumDisplacementMetres)
            return false;

        return string.Equals(StateHash,
            CanonicalStateHash.PlayerActionAtObservedPosition(current, Origin()), StringComparison.Ordinal);
    }

    public MoveStateBindingSnapshot Describe() => new MoveStateBindingSnapshot
    {
        InspectionStateHash = StateHash,
        InspectionCapturedAtGameTick = _capturedAtGameTick,
        OriginPosition = Origin(),
        MaximumDisplacementMetres = MaximumDisplacementMetres,
        MaximumSpeedMetresPerSecond = MaximumSpeedMetresPerSecond,
        MaximumAgeSeconds = MaximumAgeSeconds,
        ExpiresAtUtc = ExpiresAtUtc,
    };

    private Vector3Snapshot Origin() => new Vector3Snapshot { X = _x, Y = _y, Z = _z };

    private static bool Eligible(PlayerStateSnapshot player, bool allowHover) =>
        !string.IsNullOrWhiteSpace(player.SessionId) && player.PlanetId > 0
        && player.StateHashVersion == CanonicalStateHash.Version
        && player.IsAlive && player.IsOnPlanet && !player.IsSailing
        && ((!player.IsFlying && string.Equals(player.MovementState, "Drift", StringComparison.Ordinal))
            || allowHover && player.IsFlying && string.Equals(player.MovementState, "Fly", StringComparison.Ordinal))
        && IsFinite(player.Position.X) && IsFinite(player.Position.Y) && IsFinite(player.Position.Z)
        && IsFinite(player.Speed) && player.Speed >= 0f && player.Speed <= MaximumSpeedMetresPerSecond
        && IsFinite(player.CoreEnergy) && player.CoreEnergy > 0d
        && IsFinite(player.CoreEnergyCapacity) && player.CoreEnergyCapacity > 0d
        && IsFinite(player.ReactorEnergy) && player.ReactorEnergy >= 0d
        && player.HandcraftQueue.Count == 0
        && player.ConstructionDrones.Working == 0
        && player.ConstructionDrones.PendingBuildTargets == 0
        && player.ConstructionDrones.PendingRepairTargets == 0;

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
