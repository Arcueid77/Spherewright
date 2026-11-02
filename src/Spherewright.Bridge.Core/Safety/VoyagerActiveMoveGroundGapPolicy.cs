namespace Spherewright.Bridge.Core.Safety;

public enum VoyagerMoveGroundGapStep
{
    Grounded,
    ContinueGap,
    Stop,
    Duplicate,
    Rejected,
}

// Copied facts for one already strictly admitted Move. NativePlayerIdle is not
// fabricated: an active order is not idle, even when its costs are bounded.
public sealed class VoyagerActiveMoveGroundGapFacts
{
    public bool ExactActiveStrictMove { get; set; }
    public bool NonGroundNativePremisesProved { get; set; }
    public bool? GroundSphereHit { get; set; }
    // Supplied only after all actual/frame modes are proved Walk or Drift.
    // A sphere hit while Native still reports Drift is not strict Walk idle.
    public bool NativeDriftModeObserved { get; set; }
    public bool ControlLeaseOpen { get; set; }
    public bool OriginalWorkWindowOpen { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public double? PerNativeTickEnergyJoules { get; set; }
    public VoyagerLocalActionBudgetFacts? OriginalJourney { get; set; }
    public double StopWallPatienceSeconds { get; set; } = 1d;
}

// In-memory sequence only; neither a renewed action nor recovery authority.
public sealed class VoyagerActiveMoveGroundGapPolicy
{
    public const int MaximumGapWorkGameTicks = 8;
    // Native DetermineDrift requires >.8 seconds in float1/60 increments
    // outside water before restoring Walk (48 ticks from a fresh transition).
    // Retain the original eight-tick contact-only bound; a proved Drift mode
    // may use at most64 consecutive work ticks of the SAME finite Move.
    // This is bounded work, not a promise of landing or a longer stop tail.
    public const int MaximumDriftTransitionWorkGameTicks = 64;
    public const double MaximumStopWallPatienceSeconds = 5d;
    // Native logic ticks and Unity's scheduling/physics cadence need not run
    // at60 wall frames/s. This is bounded patience for the SAME13 paid ticks.
    public static double? StopWallPatience(double nativeFixedDeltaSeconds)
    {
        if (!Finite(nativeFixedDeltaSeconds) || nativeFixedDeltaSeconds <= 0 || nativeFixedDeltaSeconds > .25d) return null;
        if (nativeFixedDeltaSeconds <= 1d / 60d + .000001d) return 1d;
        return Math.Min(MaximumStopWallPatienceSeconds,
            1d + Math.Ceiling(VoyagerLocalWalkReservePolicy.StopGameTicks * nativeFixedDeltaSeconds));
    }
    public long LastObservedGameTick { get; private set; }
    public long? GapStartedGameTick { get; private set; }
    public bool StopLatched { get; private set; }
    public bool Rejected { get; private set; }
    public string? BlockerCode { get; private set; }

    public VoyagerActiveMoveGroundGapPolicy(long strictStartedGameTick)
    {
        if (strictStartedGameTick < 0) throw new ArgumentOutOfRangeException(nameof(strictStartedGameTick));
        LastObservedGameTick = strictStartedGameTick;
    }

    public VoyagerMoveGroundGapStep Observe(VoyagerActiveMoveGroundGapFacts facts)
    {
        if (Rejected) return VoyagerMoveGroundGapStep.Rejected;
        if (StopLatched) return VoyagerMoveGroundGapStep.Stop;
        if (facts is null || !facts.ExactActiveStrictMove || !facts.NonGroundNativePremisesProved
            || !facts.ControlLeaseOpen || !facts.GroundSphereHit.HasValue || facts.OriginalJourney is null)
            return Reject("voyager_move_ground_gap_native_premises_unproved");
        var tick = facts.OriginalJourney.CurrentGameTick;
        if (tick == LastObservedGameTick) return VoyagerMoveGroundGapStep.Duplicate;
        if (tick < LastObservedGameTick || ((GapStartedGameTick.HasValue || !facts.GroundSphereHit.Value || facts.NativeDriftModeObserved)
            && (LastObservedGameTick == long.MaxValue || tick != LastObservedGameTick + 1)))
            return Reject("voyager_move_ground_gap_sequence_unproved");
        LastObservedGameTick = tick;
        if (facts.GroundSphereHit.Value && !facts.NativeDriftModeObserved)
        {
            GapStartedGameTick = null; // Only an actual fresh query clears it.
            return VoyagerMoveGroundGapStep.Grounded;
        }
        GapStartedGameTick ??= tick;
        var maximumWorkTicks = facts.NativeDriftModeObserved
            ? MaximumDriftTransitionWorkGameTicks : MaximumGapWorkGameTicks;
        if (tick - GapStartedGameTick.Value >= maximumWorkTicks
            || !facts.OriginalWorkWindowOpen || !OriginalBudgetAllows(facts, 1,
                VoyagerLocalWalkReservePolicy.StopGameTicks, facts.StopWallPatienceSeconds + 1d / 60d)
            || !CanFund(facts, 1 + VoyagerLocalWalkReservePolicy.StopGameTicks))
        {
            LatchStop("voyager_move_ground_gap_work_ended");
            return VoyagerMoveGroundGapStep.Stop;
        }
        return VoyagerMoveGroundGapStep.ContinueGap;
    }

    public void LatchStop(string reason)
    {
        StopLatched = true;
        BlockerCode ??= reason;
    }

    public VoyagerMoveGroundGapStep Reject(string reason)
    {
        Rejected = true;
        BlockerCode ??= reason;
        return VoyagerMoveGroundGapStep.Rejected;
    }

    public static bool CanFund(VoyagerActiveMoveGroundGapFacts f, int ticks)
    {
        var j = f?.OriginalJourney;
        if (j is null || ticks < 1 || ticks > 1 + VoyagerLocalWalkReservePolicy.StopGameTicks
            || !Known(f!.CoreEnergyJoules) || f.CoreEnergyJoules <= 0 || !Known(f.PerNativeTickEnergyJoules)
            || !Known(j.AvailableLoadedEnergyJoules) || !Known(j.LockedReturnFloorJoules)) return false;
        var cost = f.PerNativeTickEnergyJoules!.Value * ticks;
        var loaded = j.LockedReturnFloorJoules!.Value + cost;
        return Finite(cost) && Finite(loaded) && f.CoreEnergyJoules >= cost && j.AvailableLoadedEnergyJoules >= loaded;
    }

    public static bool OriginalBudgetAllows(VoyagerActiveMoveGroundGapFacts f, int workTicks, int tailTicks,
        double requiredCleanupWallSeconds)
    {
        var j = f?.OriginalJourney;
        // Inspect the original numeric/identity facts, never NativePlayerIdle.
        if (j is null || !f!.ExactActiveStrictMove || !f.NonGroundNativePremisesProved || !f.ControlLeaseOpen
            || !j.ExactRemoteBinding || !j.LastLandedGameTick.HasValue || j.LastLandedGameTick < 0
            || j.CurrentGameTick < j.LastLandedGameTick || j.SpentGameTicks < 0 || j.MaximumGameTicks < 1
            || j.ReturnReserveGameTicks < 3600 || j.ReturnReserveGameTicks > 108000
            || j.OriginalExtraWarperReserve < 0 || j.OriginalExtraWarperReserve > 100
            || !j.NativeWarperCount.HasValue || j.NativeWarperCount < 1 + j.OriginalExtraWarperReserve
            || workTicks < 0 || workTicks > 1 || tailTicks < 1 || tailTicks > VoyagerLocalWalkReservePolicy.StopGameTicks
            || j.NowUtc == default || j.JourneyDeadlineUtc == default || !Finite(requiredCleanupWallSeconds)
            || !Finite(f.StopWallPatienceSeconds) || f.StopWallPatienceSeconds < 1d
            || f.StopWallPatienceSeconds > MaximumStopWallPatienceSeconds
            || requiredCleanupWallSeconds > f.StopWallPatienceSeconds + 1d / 60d) return false;
        var ticks = (decimal)j.SpentGameTicks + j.CurrentGameTick - j.LastLandedGameTick.Value
            + workTicks + tailTicks + j.ReturnReserveGameTicks;
        return ticks <= j.MaximumGameTicks
            && (j.JourneyDeadlineUtc - j.NowUtc).TotalSeconds > j.ReturnReserveGameTicks / 60d + requiredCleanupWallSeconds;
    }

    // At arm T, T..T+11 may drain and T+12 is the final funded failure601.
    // Pausing for the first time at T+13 would instead admit a fourteenth601.
    public static bool MustPauseBeforeCurrentTick(long armTick, long currentTick, DateTimeOffset armUtc,
        DateTimeOffset nowUtc, DateTimeOffset deadlineUtc) =>
        armTick < 0 || currentTick < armTick || (decimal)currentTick - armTick >= VoyagerLocalWalkReservePolicy.StopGameTicks - 1
        || armUtc == default || nowUtc < armUtc || nowUtc >= deadlineUtc;

    private static bool Known(double? value) => value.HasValue && Finite(value.Value);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}
