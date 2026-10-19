namespace Spherewright.Bridge.Core.Safety;

// Copied facts for the narrow ordinary RTS Walk/Mine branch. Neither this
// policy nor a pre-player hook certifies Native Fly, Drift, jump or user input.
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
    public NativeMechaBackgroundCostFacts? IndependentMechaCosts { get; set; }
}

public sealed class VoyagerLocalWalkReserve
{
    public bool Proved { get; internal set; }
    public string? BlockerCode { get; internal set; }
    public double? PerNativeTickEnergyJoules { get; internal set; }
    public double? WorkPowerUpperWatts { get; internal set; }
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
        if (!f.ModeTransitionsExcluded || !NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(f.IndependentMechaCosts)
            || !Known(f.WalkSpeed) || !Known(f.WalkPowerWatts)
            || !Known(f.MiningPowerWatts) || !Known(f.OtherWorkPowerWatts) || !Known(f.ReactorConsumptionRatio)
            || !Known(f.RtsSpeed) || !Known(f.ManualSpeed) || !Known(f.SoftLandingRecover)
            || f.WalkSpeed < 1d || f.WalkSpeed > 60d || f.ReactorConsumptionRatio <= 0d
            || f.SoftLandingRecover > 1d || f.ManualSpeed != 0d || f.RtsSpeed > f.WalkSpeed + .01d)
            return new VoyagerLocalWalkReserve { BlockerCode = "native_rts_walk_mode_or_tail_unproved" };
        var velocityUpper = f.WalkSpeed!.Value + .01d;
        // Slerp of an RTS velocity bounded by walkSpeed and a tangent target
        // of walkSpeed cannot grow beyond that bound (plus arithmetic slack).
        // After the exact owned order is aborted with no queued orders, Native
        // MoveTowards subtracts walkSpeed/10 each tick. Eleven ticks cover the
        // .01m/s slack even at the supported minimum1m/s. Mining is deliberately
        // overcharged throughout the tail; Native clears it before any debit
        // once the exact Mine order is absent.
        var walkPower = f.WalkPowerWatts!.Value * (velocityUpper / f.WalkSpeed.Value)
            * Math.Max(1d, velocityUpper / 6d) * f.ReactorConsumptionRatio!.Value;
        var totalPower = walkPower + (f.MiningPowerWatts!.Value + f.OtherWorkPowerWatts!.Value)
            * f.ReactorConsumptionRatio.Value;
        var perTick = totalPower * NativeTickSeconds;
        var stop = perTick * StopGameTicks;
        var workPower = perTick * 60d;
        if (!Finite(totalPower) || !Finite(perTick) || !Finite(stop) || !Finite(workPower))
            return new VoyagerLocalWalkReserve { BlockerCode = "native_rts_walk_energy_overflow" };
        return new VoyagerLocalWalkReserve { Proved = true, PerNativeTickEnergyJoules = perTick,
            WorkPowerUpperWatts = workPower, StopEnergyJoules = stop };
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
