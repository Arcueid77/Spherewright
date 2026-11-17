namespace Spherewright.Bridge.Core.Safety;

// Scheduling patience for the SAME funded Native game ticks. No tick grant,
// simulation step, clock renewal or energy/generation credit.
public static class NativeConstructionCadencePolicy
{
    public static double? WallBudget(long ticks,double nativeFixedDeltaSeconds)
        => ticks is >=1 and <=7200 && !double.IsNaN(nativeFixedDeltaSeconds) && !double.IsInfinity(nativeFixedDeltaSeconds)
            && nativeFixedDeltaSeconds>0 && nativeFixedDeltaSeconds<=.25d
                ? 1d+Math.Ceiling(ticks*Math.Max(1d/60d,nativeFixedDeltaSeconds)) : null;

    // Loader-time cadence may still be the menu cadence, before Native applies
    // the saved world's slower step. Reserve the already-supported .25s upper
    // cadence once at arming; the fixed tick count and original owner deadline
    // remain unchanged and this clock is never renewed by observation.
    public static double? SavedTailWallBudget(long ticks, double initialFixedDeltaSeconds) =>
        WallBudget(ticks, initialFixedDeltaSeconds).HasValue ? WallBudget(ticks, .25d) : null;
}
