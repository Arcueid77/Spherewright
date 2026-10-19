namespace Spherewright.Bridge.Core.Safety;

// Copied costs for bounded RTS Walk/Mine and optional potential water Drift.
// An energy bound is not a physical-stop, Fly, jump or user-input certificate.
public sealed class VoyagerLocalWalkFacts
{
    public bool ModeTransitionsExcluded { get; set; }
    public double? WalkSpeed { get; set; }
    public double? WalkPowerWatts { get; set; }
    public double? MiningPowerWatts { get; set; }
    public double? OtherWorkPowerWatts { get; set; }
    public double? ReactorConsumptionRatio { get; set; }
    public double? RtsSpeed { get; set; }
    public double? ManualSpeed { get; set; }
    public double? SoftLandingRecover { get; set; }
    public bool IncludePotentialWaterDrift { get; set; }
    public double? NativeThrustPowerPerAcceleration { get; set; }
    // A bound for every position in the bound local-star system, not a sample
    // of current gravity. The Native consumer must prove the complete inputs.
    public double? NativeGravityUpperBound { get; set; }
    public NativeMechaBackgroundCostFacts? IndependentMechaCosts { get; set; }
    public NativeMechaRechargeFacts? BoundedRechargeCosts { get; set; }
}

public sealed class VoyagerLocalWalkReserve
{
    public bool Proved { get; internal set; }
    public string? BlockerCode { get; internal set; }
    public double? PerNativeTickEnergyJoules { get; internal set; }
    public double? WorkPowerUpperWatts { get; internal set; }
    public NativeMechaRechargeCost? BudgetedRecharge { get; internal set; }
    // Thirteen charged ticks cover RTS decay only. Potential Drift thrust can
    // continue afterward; its consumer must separately observe dry Walk/idle
    // or use the authorized Native pause path on failure, never assume stop.
    public double? StopEnergyJoules { get; internal set; }
}

public static class VoyagerLocalWalkReservePolicy
{
    public const int StopGameTicks = 13; // current1 + declining RTS tail11 + spare1
    public const double NativeTickSeconds = .01666666753590107; // exact copied float1/60

    // Native Drift tests the radial segment [p-20u,p+10u]. A fresh absence
    // query of layer4 over the whole larger ball excludes every point of that
    // segment, not merely the current feet. This is a same-tick geometric
    // bound; it deliberately makes no linear13/60s position prediction.
    public static bool WaterRayFitsQueriedBall(double nativePlanetRadius, double actualPlayerRadius) =>
        Finite(nativePlanetRadius) && nativePlanetRadius >= 120d && nativePlanetRadius <= 6000d
        && Finite(actualPlayerRadius) && actualPlayerRadius >= nativePlanetRadius - 10d
        && actualPlayerRadius <= nativePlanetRadius + 20d
        && Math.Max(actualPlayerRadius + 10d, Math.Abs(actualPlayerRadius - 20d)) + .01d < nativePlanetRadius + 40d;

    public static VoyagerLocalWalkReserve Evaluate(VoyagerLocalWalkFacts f)
    {
        if (f is null) throw new ArgumentNullException(nameof(f));
        if (!f.ModeTransitionsExcluded || !Known(f.WalkSpeed) || !Known(f.WalkPowerWatts)
            || !Known(f.MiningPowerWatts) || !Known(f.OtherWorkPowerWatts) || !Known(f.ReactorConsumptionRatio)
            || !Known(f.RtsSpeed) || !Known(f.ManualSpeed) || !Known(f.SoftLandingRecover)
            || f.WalkSpeed < 1d || f.WalkSpeed > 60d || f.ReactorConsumptionRatio <= 0d
            || f.SoftLandingRecover > 1d || f.ManualSpeed != 0d || f.RtsSpeed > f.WalkSpeed + .01d)
            return new VoyagerLocalWalkReserve { BlockerCode = "native_rts_walk_mode_or_tail_unproved" };
        NativeMechaRechargeCost? recharge = null;
        if (!NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f.IndependentMechaCosts))
        {
            var unsupported = NativeMechaRechargeCostPolicy.BackgroundBlocker(f.IndependentMechaCosts);
            if (unsupported is not null) return new VoyagerLocalWalkReserve { BlockerCode = unsupported };
            if (!ReferenceEquals(f.IndependentMechaCosts, f.BoundedRechargeCosts?.Background))
                return new VoyagerLocalWalkReserve { BlockerCode = "native_independent_cost_facts_mismatch" };
            recharge = NativeMechaRechargeCostPolicy.Evaluate(f.BoundedRechargeCosts);
            if (!recharge.Proved) return new VoyagerLocalWalkReserve { BlockerCode = recharge.BlockerCode };
        }
        var velocityUpper = (f.WalkSpeed!.Value + .01d) * (f.IncludePotentialWaterDrift ? 2d : 1d);
        // Slerp of an RTS velocity bounded by walkSpeed and a tangent target
        // of walkSpeed cannot grow beyond that bound (plus arithmetic slack).
        // Optional Drift deliberately charges up to twice this speed for the
        // one-tick mode transition and all Native horizontal speed multipliers.
        // After the exact owned order is aborted with no queued orders, Native
        // MoveTowards subtracts walkSpeed/10 each tick. Eleven ticks cover the
        // .01m/s slack even at the supported minimum1m/s. Mining is deliberately
        // overcharged throughout the tail; Native clears it before any debit
        // once the exact Mine order is absent.
        var walkPower = f.WalkPowerWatts!.Value * (velocityUpper / f.WalkSpeed.Value)
            * Math.Max(1d, velocityUpper / 6d) * f.ReactorConsumptionRatio!.Value;
        var totalPower = walkPower + (f.MiningPowerWatts!.Value + f.OtherWorkPowerWatts!.Value)
            * f.ReactorConsumptionRatio.Value;
        if (f.IncludePotentialWaterDrift)
        {
            var driftPower = CalculatePotentialDriftPowerUpperBound(f.NativeThrustPowerPerAcceleration,
                f.NativeGravityUpperBound, f.ReactorConsumptionRatio);
            if (!driftPower.HasValue)
                return new VoyagerLocalWalkReserve { BlockerCode = "native_potential_drift_cost_unproved" };
            totalPower += driftPower.Value;
        }
        // Native shield/laser refill is independent of energyConsumptionCoef.
        var perTick = totalPower * NativeTickSeconds + (recharge?.PerNativeTickEnergyUpperJoules ?? 0d);
        var stop = perTick * StopGameTicks;
        var workPower = perTick * 60d;
        if (!Finite(totalPower) || !Finite(perTick) || !Finite(stop) || !Finite(workPower))
            return new VoyagerLocalWalkReserve { BlockerCode = "native_rts_walk_energy_overflow" };
        return new VoyagerLocalWalkReserve { Proved = true, PerNativeTickEnergyJoules = perTick,
            WorkPowerUpperWatts = workPower, StopEnergyJoules = stop, BudgetedRecharge = recharge };
    }

    public static double? CalculateNativeGravityUpperBound(IReadOnlyList<double>? planetRadii,
        double? starMass, double? starPhysicsRadius)
    {
        if (planetRadii is null || planetRadii.Count < 1 || planetRadii.Count > 64
            || !Known(starMass) || starMass <= 0d || !Known(starPhysicsRadius) || starPhysicsRadius <= 0d) return null;
        var planetMaximum = 0d;
        foreach (var radius in planetRadii)
        {
            if (!Finite(radius) || radius < 120d || radius > 6000d) return null;
            planetMaximum = Math.Max(planetMaximum, Math.Sqrt(radius) * 3.5d);
        }
        // Preserve Native's multiplication order and refuse intermediate
        // overflow, even when an algebraically rearranged expression is finite.
        var starNumerator = 64000000000000d * starMass!.Value;
        if (!Finite(starNumerator)) return null;
        starNumerator *= 1.3538551990520382E-06;
        if (!Finite(starNumerator)) return null;
        starNumerator *= 4d;
        var radiusSquared = starPhysicsRadius!.Value * starPhysicsRadius.Value;
        if (!Finite(starNumerator) || !Finite(radiusSquared) || radiusSquared <= 0d) return null;
        var starMaximum = starNumerator / radiusSquared;
        if (!Finite(starMaximum)) return null;

        // ApplyGravity is a positive weighted average: each planet contributes
        // at most 3.5*sqrt(R), while the star has weight1 and the bound above.
        // R<=6000 and distance>1 keep y<4.4, so 64 weights sum to <2.6e6.
        // With finite Native normalization/intermediates (consumer-proved),
        // binary64 accumulation error is far below the factor4 slack. The +1
        // also avoids relying on a relative error bound near zero. This does
        // not infer valid Native positions, system identity or mode predicates.
        var upper = 4d * (planetMaximum + starMaximum + 1d);
        if (!Finite(upper) || !PotentialDriftForceUpper(upper).HasValue) return null;
        return upper;
    }

    // Vertical thrust only; Evaluate separately charges horizontal movement
    // and all existing mining/replication/research work. gravityUpper already
    // contains the global factor4 when obtained from the helper above.
    public static double? CalculatePotentialDriftPowerUpperBound(double? thrustPowerPerAcceleration,
        double? gravityUpper, double? reactorConsumptionRatio)
    {
        if (!Known(thrustPowerPerAcceleration) || !Known(gravityUpper)
            || !Known(reactorConsumptionRatio) || reactorConsumptionRatio <= 0d) return null;
        var force = PotentialDriftForceUpper(gravityUpper!.Value);
        if (!force.HasValue) return null;
        var power = thrustPowerPerAcceleration!.Value * reactorConsumptionRatio!.Value;
        if (!Finite(power)) return null;
        power *= force.Value;
        return Finite(power) ? power : null;
    }

    private static double? PotentialDriftForceUpper(double gravityUpper)
    {
        // Native caps the altitude term at1000f, casts gravity to float, then
        // adds in float. These monotone operations supply the exact rounding
        // widening needed here; a finite double alone is insufficient.
        var nativeGravity = (float)gravityUpper;
        var nativeForce = 1000f + nativeGravity;
        return float.IsNaN(nativeGravity) || float.IsInfinity(nativeGravity)
            || float.IsNaN(nativeForce) || float.IsInfinity(nativeForce) ? null : (double)nativeForce;
    }

    public static bool CanFund(double? core, double? loaded, double? originalReturnFloor, double? perTickEnergy, int remainingTicks)
    {
        if (!Known(core) || core <= 0d || !Known(loaded) || !Known(originalReturnFloor) || !Known(perTickEnergy)
            || remainingTicks < 1 || remainingTicks > StopGameTicks) return false;
        var margin = perTickEnergy!.Value * remainingTicks;
        var required = originalReturnFloor!.Value + margin;
        return Finite(margin) && Finite(required) && core >= margin && loaded >= required;
    }

    private static bool Known(double? value) => value.HasValue && Finite(value.Value);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}
