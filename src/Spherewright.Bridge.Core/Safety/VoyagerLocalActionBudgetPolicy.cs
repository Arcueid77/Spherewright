namespace Spherewright.Bridge.Core.Safety;

// Copied server/native facts. This neither changes a journey nor grants a client
// authority to claim a return reserve. Core recharge remains a departure gate.
public sealed class VoyagerLocalActionBudgetFacts
{
    public bool ExactRemoteBinding { get; set; }
    public bool NativePlayerIdle { get; set; }
    public long CurrentGameTick { get; set; }
    public long? LastLandedGameTick { get; set; }
    public long SpentGameTicks { get; set; }
    public long MaximumGameTicks { get; set; }
    public long ReturnReserveGameTicks { get; set; }
    public long WorkGameTicks { get; set; }
    public long TailGameTicks { get; set; }
    public DateTimeOffset NowUtc { get; set; }
    public DateTimeOffset JourneyDeadlineUtc { get; set; }
    public double? AvailableLoadedEnergyJoules { get; set; }
    public double? LockedReturnFloorJoules { get; set; }
    public double? WorkPowerUpperWatts { get; set; }
    public double TailAndNextTickEnergyJoules { get; set; }
    public int? NativeWarperCount { get; set; }
    public int OriginalExtraWarperReserve { get; set; }
}

public sealed class VoyagerLocalActionBudgetDecision
{
    public bool Proved { get; internal set; }
    public string? BlockerCode { get; internal set; }
    public double? RequiredLoadedEnergyJoules { get; internal set; }
}

public static class VoyagerLocalActionBudgetPolicy
{
    // A committed local action receives only its original work allowance. A
    // paused/slow game must not consume the wall time already owed to return.
    public static DateTimeOffset? WorkDeadlineUtc(DateTimeOffset nowUtc, DateTimeOffset journeyDeadlineUtc,
        long workGameTicks, long returnReserveGameTicks, long stopTailGameTicks = 0)
    {
        if (nowUtc == default || journeyDeadlineUtc == default || workGameTicks < 1 || workGameTicks > 7200
            || returnReserveGameTicks < 3600 || returnReserveGameTicks > 108000
            || stopTailGameTicks < 0 || stopTailGameTicks > 7200) return null;
        try
        {
            // Integer ticks avoid runtime-specific AddSeconds rounding granting
            // any part of the originally reserved return/cleanup wall interval.
            var byWork = nowUtc.AddTicks((long)Math.Floor(workGameTicks * (double)TimeSpan.TicksPerSecond / 60d));
            var reservedTicks = (long)Math.Ceiling((returnReserveGameTicks + (double)stopTailGameTicks) * TimeSpan.TicksPerSecond / 60d);
            var byReturn = journeyDeadlineUtc.AddTicks(-reservedTicks);
            var result = byWork < byReturn ? byWork : byReturn;
            return result > nowUtc ? result : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    public static bool LocalWindowStillOpen(long startedGameTick, long currentGameTick, long deadlineGameTick,
        DateTimeOffset startedAtUtc, DateTimeOffset nowUtc, DateTimeOffset? deadlineUtc) =>
        startedGameTick >= 0 && currentGameTick >= startedGameTick && currentGameTick < deadlineGameTick
        && startedAtUtc != default && nowUtc >= startedAtUtc && deadlineUtc.HasValue && nowUtc < deadlineUtc.Value;

    public static bool MatchesOwnedJourneyIdentity(string? journeyOwnedSaveName, string? journeyGameVersion,
        bool currentSessionOwned, string? currentOwnedSaveName, string? currentSessionSaveName, string? currentGameVersion) =>
        currentSessionOwned && !string.IsNullOrWhiteSpace(journeyOwnedSaveName) && !string.IsNullOrWhiteSpace(journeyGameVersion)
        && string.Equals(journeyOwnedSaveName, currentOwnedSaveName, StringComparison.Ordinal)
        && string.Equals(journeyOwnedSaveName, currentSessionSaveName, StringComparison.Ordinal)
        && string.Equals(journeyGameVersion, currentGameVersion, StringComparison.Ordinal);

    public static VoyagerLocalActionBudgetDecision Evaluate(VoyagerLocalActionBudgetFacts f)
    {
        if (f is null) throw new ArgumentNullException(nameof(f));
        if (!f.ExactRemoteBinding || !f.NativePlayerIdle) return Blocked("remote_local_binding_or_idle_unproved");
        if (!f.LastLandedGameTick.HasValue || f.LastLandedGameTick < 0 || f.CurrentGameTick < f.LastLandedGameTick
            || f.SpentGameTicks < 0 || f.MaximumGameTicks < 1 || f.ReturnReserveGameTicks < 3600
            || f.WorkGameTicks < 1 || f.WorkGameTicks > 7200 || f.TailGameTicks < 0 || f.TailGameTicks > 7200
            || f.OriginalExtraWarperReserve < 0 || f.OriginalExtraWarperReserve > 100)
            return Blocked("remote_local_bounds_invalid");
        if (!f.NativeWarperCount.HasValue || f.NativeWarperCount < 1 + f.OriginalExtraWarperReserve)
            return Blocked("remote_local_original_warper_reserve_missing");
        var requiredTicks = (decimal)f.SpentGameTicks + f.CurrentGameTick - f.LastLandedGameTick.Value
            + f.WorkGameTicks + f.TailGameTicks + f.ReturnReserveGameTicks;
        var wallSeconds = (f.JourneyDeadlineUtc - f.NowUtc).TotalSeconds;
        if (requiredTicks > f.MaximumGameTicks
            || wallSeconds <= (f.WorkGameTicks + (double)f.TailGameTicks + f.ReturnReserveGameTicks) / 60d)
            return Blocked("remote_local_original_return_time_insufficient");
        if (!f.AvailableLoadedEnergyJoules.HasValue || !f.LockedReturnFloorJoules.HasValue || !f.WorkPowerUpperWatts.HasValue
            || !Finite(f.AvailableLoadedEnergyJoules.Value) || !Finite(f.LockedReturnFloorJoules.Value)
            || !Finite(f.WorkPowerUpperWatts.Value) || !Finite(f.TailAndNextTickEnergyJoules))
            return Blocked("remote_local_energy_unknown");
        var required = f.LockedReturnFloorJoules.Value + f.WorkPowerUpperWatts.Value * f.WorkGameTicks / 60d
            + f.TailAndNextTickEnergyJoules;
        if (!Finite(required)) return Blocked("remote_local_energy_overflow");
        return new VoyagerLocalActionBudgetDecision
        {
            Proved = f.AvailableLoadedEnergyJoules.Value > required,
            RequiredLoadedEnergyJoules = required,
            BlockerCode = f.AvailableLoadedEnergyJoules.Value > required ? null : "remote_local_original_return_floor_insufficient",
        };
    }

    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0d;
    private static VoyagerLocalActionBudgetDecision Blocked(string code) => new VoyagerLocalActionBudgetDecision { BlockerCode = code };
}
