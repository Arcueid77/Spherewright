using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Celestial;

namespace Spherewright.Bridge.Core.Safety;

public sealed class NativeConstructionDroneFacts
{
    public int? MaximumMechaDrones { get; set; }
    public double? NativeSpeedMetersPerSecond { get; set; }
    public double? NativeEnergyPerMeter { get; set; }
    public double? NativeEjectEnergyJoules { get; set; }
    public double? NativeRepairEnergyPerTick { get; set; }
    public double? OtherMechaPowerUpperWatts { get; set; }
    public double? AnchorRadiusMeters { get; set; }
    public double? MaximumTargetDistanceMeters { get; set; }
    public int? NativePendingJumpTicks { get; set; }
    public NativeMechaBackgroundCostFacts? IndependentMechaCosts { get; set; }
}

public sealed class NativeConstructionDroneReserve
{
    public bool Proved { get; internal set; }
    public string? BlockerCode { get; internal set; }
    public long TailGameTicks { get; internal set; }
    public double PowerUpperWatts { get; internal set; }
    public double TailAndNextTickEnergyJoules { get; internal set; }
}

public sealed class NativeConstructionDroneMotionFacts
{
    public int Stage { get; set; }
    public float Progress { get; set; }
    public float SpeedMetersPerSecond { get; set; }
    public UniversalPositionSnapshot Position { get; set; } = new();
    public Vector3Snapshot Target { get; set; } = new();
    public Vector3Snapshot Initial { get; set; } = new();
    public Vector3Snapshot Eject { get; set; } = new();
}

public static class NativeConstructionDroneReservePolicy
{
    public const double MaximumTargetDistanceMeters = 16d;
    public const double MaximumDroneDistanceMeters = 28d;
    public const double MaximumAnchorDisplacementMeters = .01d;

    // DSP29151's ordinary return is normalized chord interpolation followed
    // by radial interpolation. For D <= q/4 and D >= 2s, its extra radial
    // displacement is <= s/4, where s=speed/60, q=eject radius. The bounded
    // float/anchor allowance below leaves a closure of at least s/4. For
    // D<2s the radial difference halves and the angular difference contracts;
    // the copied-step guard additionally verifies D' <= .75D + .04. That
    // recurrence is below the native .5m recycle test within 32 ticks.
    // The runtime checks every copied drone and the held anchor before the
    // native construction tick; neither a moving player nor an unobserved
    // drone may inherit this certificate. Stage1/cached launches get 64 ticks.
    public static NativeConstructionDroneReserve Evaluate(NativeConstructionDroneFacts f)
    {
        if (f is null) throw new ArgumentNullException(nameof(f));
        if (!f.MaximumMechaDrones.HasValue || !f.NativeSpeedMetersPerSecond.HasValue
            || !f.NativeEnergyPerMeter.HasValue || !f.NativeEjectEnergyJoules.HasValue
            || !f.NativeRepairEnergyPerTick.HasValue || !f.OtherMechaPowerUpperWatts.HasValue
            || !f.AnchorRadiusMeters.HasValue || !f.MaximumTargetDistanceMeters.HasValue || !f.NativePendingJumpTicks.HasValue)
            return Blocked("native_drone_cost_or_geometry_unknown");
        if (f.NativePendingJumpTicks > 0)
            return Blocked("native_pending_jump_not_in_construction_tail");
        if (!NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f.IndependentMechaCosts))
            return Blocked("native_independent_mecha_costs_not_excluded");
        var n = f.MaximumMechaDrones.Value;
        var speed = f.NativeSpeedMetersPerSecond.Value;
        var rate = f.NativeEnergyPerMeter.Value;
        var eject = f.NativeEjectEnergyJoules.Value;
        var repair = f.NativeRepairEnergyPerTick.Value;
        var background = f.OtherMechaPowerUpperWatts.Value;
        var radius = f.AnchorRadiusMeters.Value;
        var reach = f.MaximumTargetDistanceMeters.Value;
        if (n < 1 || n > 32 || !Finite(speed) || speed < 6d || speed > 120d || !Finite(rate) || !Finite(eject)
            || !Finite(repair) || !Finite(background) || !Finite(radius) || radius < 120d || radius > 6000d
            || !Finite(reach) || reach > MaximumTargetDistanceMeters
            || MaximumDroneDistanceMeters > (radius + 2.8d) / 4d)
            return Blocked("native_drone_finite_local_envelope_unproved");
        var ticks = 64d + Math.Ceiling(4d * MaximumDroneDistanceMeters / speed * 60d) + 32d;
        var power = n * (speed * rate + repair * 60d) + background;
        // Eject energy is a conservative allowance, not a claim that DSP's
        // mecha EjectMechaDrone path actually debits the battle-base eject cost.
        var margin = power * (ticks + 2d) / 60d + n * eject;
        if (!Finite(ticks) || ticks > 7200d || !Finite(power) || !Finite(margin))
            return Blocked("native_drone_tail_or_energy_bound_exceeded");
        return new NativeConstructionDroneReserve { Proved = true, TailGameTicks = (long)ticks,
            PowerUpperWatts = power, TailAndNextTickEnergyJoules = margin };
    }

    // Distances and a predicted ordinary Native step are copied/calculated on
    // the Unity thread. They are observations, not commands or a replacement
    // drone simulation. The .01m arithmetic slack covers the bounded float
    // operations at radii <=6000m; the separate .01m anchor bound is mandatory.
    public static bool CopiedDroneStepPreservesEnvelope(int stage, double speedMetersPerSecond,
        double anchorDisplacementMeters, double droneDistanceMeters, double targetDistanceMeters,
        double nextDroneDistanceMeters, bool nativeWouldRecycle = false)
    {
        if (stage < 1 || stage > 4 || !Finite(speedMetersPerSecond) || speedMetersPerSecond < 6d
            || speedMetersPerSecond > 120d || !Finite(anchorDisplacementMeters)
            || anchorDisplacementMeters > MaximumAnchorDisplacementMeters
            || !Finite(droneDistanceMeters) || !Finite(targetDistanceMeters) || !Finite(nextDroneDistanceMeters)
            || droneDistanceMeters > MaximumDroneDistanceMeters || nextDroneDistanceMeters > MaximumDroneDistanceMeters)
            return false;
        if (stage <= 3 && targetDistanceMeters > MaximumTargetDistanceMeters) return false;
        if (stage != 4 || droneDistanceMeters < .5d || nativeWouldRecycle) return true;
        var step = speedMetersPerSecond / 60d;
        return droneDistanceMeters >= 2d * step
            ? nextDroneDistanceMeters + .01d <= droneDistanceMeters - step / 4d
            : nextDroneDistanceMeters + .01d <= .75d * droneDistanceMeters + .04d;
    }

    // Scalar transcription of the audited DSP29151 branches. This cannot call
    // Native code or mutate a shared craft/factory/mecha, even accidentally.
    public static UniversalPositionSnapshot? PredictCopiedOrdinaryStep(NativeConstructionDroneMotionFacts f)
    {
        if (f is null) throw new ArgumentNullException(nameof(f));
        if (f.Stage < 1 || f.Stage > 4 || !Finite(f.SpeedMetersPerSecond) || f.SpeedMetersPerSecond < 6f
            || f.SpeedMetersPerSecond > 120f || !PositionFinite(f.Position) || !VectorFinite(f.Target)
            || !VectorFinite(f.Initial) || !VectorFinite(f.Eject)
            || !Finite(f.Progress) || f.Progress > 1f) return null;
        const float dt = 1f / 60f;
        if (f.Stage == 1)
        {
            var progress = Math.Min(1f, f.Progress + dt * Math.Max(1f, Math.Min(3f, f.SpeedMetersPerSecond * .35f)));
            var t = 1f - (1f - progress) * (1f - progress) * (1f - progress);
            var point = Add(f.Eject, Scale(Subtract(f.Initial, f.Eject), t));
            return new UniversalPositionSnapshot { X = point.X, Y = point.Y, Z = point.Z };
        }
        if (f.Stage == 3) return CopyPosition(f.Position);
        var position = new Vector3Snapshot { X = (float)f.Position.X, Y = (float)f.Position.Y, Z = (float)f.Position.Z };
        var delta = Subtract(f.Target, position);
        var distance = Length(delta);
        if (distance < .5f) return CopyPosition(f.Position);
        var alpha = Math.Min(1f, f.SpeedMetersPerSecond * dt / distance);
        var currentRadius = Length(position);
        var radial = (Length(f.Target) - currentRadius) / Math.Max(distance / f.SpeedMetersPerSecond, .03333333f) * dt;
        var step = Scale(delta, alpha);
        // The original Native coordinates, addition and normalization stay
        // double; only Native's own casts/delta/radial factors use float.
        var x = f.Position.X + step.X; var y = f.Position.Y + step.Y; var z = f.Position.Z + step.Z;
        var chordLength = Math.Sqrt(x * x + y * y + z * z);
        if (chordLength < 1e-5f) return null;
        var radius = currentRadius + radial;
        var result = new UniversalPositionSnapshot { X = x / chordLength * radius, Y = y / chordLength * radius, Z = z / chordLength * radius };
        return PositionFinite(result) ? result : null;
    }

    public static bool CopiedStepWouldNativeRecycle(NativeConstructionDroneMotionFacts f) => f.Stage == 4
        && PositionFinite(f.Position) && VectorFinite(f.Target)
        && Length(new Vector3Snapshot { X = f.Target.X - (float)f.Position.X,
            Y = f.Target.Y - (float)f.Position.Y, Z = f.Target.Z - (float)f.Position.Z }) < .5f;

    public static bool CanFundNativeTail(double? availableCoreJoules, double? loadedJoules, double? returnFloorJoules, double? tailJoules)
    {
        if (!availableCoreJoules.HasValue || !loadedJoules.HasValue || !returnFloorJoules.HasValue || !tailJoules.HasValue
            || !Finite(availableCoreJoules.Value) || !Finite(loadedJoules.Value) || !Finite(returnFloorJoules.Value) || !Finite(tailJoules.Value)) return false;
        var required = returnFloorJoules.Value + tailJoules.Value;
        return Finite(required) && availableCoreJoules > 0d && availableCoreJoules >= tailJoules && loadedJoules >= required;
    }

    private static bool PositionFinite(UniversalPositionSnapshot? p) => p is not null
        && !double.IsNaN(p.X) && !double.IsInfinity(p.X) && !double.IsNaN(p.Y) && !double.IsInfinity(p.Y)
        && !double.IsNaN(p.Z) && !double.IsInfinity(p.Z);
    private static UniversalPositionSnapshot CopyPosition(UniversalPositionSnapshot p) => new() { X = p.X, Y = p.Y, Z = p.Z };

    private static bool VectorFinite(Vector3Snapshot? v) => v is not null
        && !float.IsNaN(v.X) && !float.IsInfinity(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
        && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
    private static float Length(Vector3Snapshot v) => (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
    private static Vector3Snapshot Add(Vector3Snapshot a, Vector3Snapshot b) => new() { X = a.X + b.X, Y = a.Y + b.Y, Z = a.Z + b.Z };
    private static Vector3Snapshot Subtract(Vector3Snapshot a, Vector3Snapshot b) => new() { X = a.X - b.X, Y = a.Y - b.Y, Z = a.Z - b.Z };
    private static Vector3Snapshot Scale(Vector3Snapshot a, float s) => new() { X = a.X * s, Y = a.Y * s, Z = a.Z * s };

    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0d;
    private static NativeConstructionDroneReserve Blocked(string code) => new NativeConstructionDroneReserve { BlockerCode = code };
}
