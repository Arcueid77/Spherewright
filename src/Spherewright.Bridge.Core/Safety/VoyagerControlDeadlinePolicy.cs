namespace Spherewright.Bridge.Core.Safety;

// Copied clock/kinematic proof only. Null is unavailable, not an unlimited clock.
// Stop deadlines are latched once by the owner on its first stop request. Reads
// may renew the caller lease but must pass those same original stop deadlines.
public sealed class VoyagerControlDeadlineFacts
{
    public long? CurrentGameTick { get; set; }
    public long? LastControlledTick { get; set; }
    public long? FlightDeadlineTick { get; set; }
    public DateTimeOffset? NowUtc { get; set; }
    public DateTimeOffset? JourneyDeadlineUtc { get; set; }
    public DateTimeOffset? ControlLeaseExpiresAtUtc { get; set; }
    public long? WarpGameTicks { get; set; }
    public long? MaximumWarpGameTicks { get; set; }
    public bool StopRequested { get; set; }
    public long? StopDeadlineGameTick { get; set; }
    public DateTimeOffset? StopDeadlineUtc { get; set; }
    // The native owner proves exit and low speed; zero energy alone cannot set this.
    public bool NativeStopSettled { get; set; }
}

public sealed class VoyagerControlDeadlineDecision
{
    public bool ShouldRequestStop { get; internal set; }
    public bool ShouldExitWarp { get; internal set; }
    public bool StopOutcomeUnknown { get; internal set; }
    public string? StopReasonCode { get; internal set; }
}

public static class VoyagerControlDeadlinePolicy
{
    public const int ControlLeaseSeconds = 30;
    public const int MaximumControlGapGameTicks = 2;
    public const int WarpExitLeadGameTicks = VoyagerWarpControlPolicy.MaximumNativeDecayGameTicks + MaximumControlGapGameTicks;
    public const int MaximumStopGameTicks = 1800;
    public const int StopCleanupSeconds = 30;

    // Pure evaluation: never reads a clock, renews a lease, changes a deadline,
    // declares arrival/recovery or performs a native control operation.
    public static VoyagerControlDeadlineDecision Evaluate(VoyagerControlDeadlineFacts facts)
    {
        if (facts is null) throw new ArgumentNullException(nameof(facts));
        var reason = StopReason(facts);
        var requestStop = facts.StopRequested || reason is not null;
        var invalidWarpClock = !Nonnegative(facts.WarpGameTicks)
            || !facts.MaximumWarpGameTicks.HasValue || facts.MaximumWarpGameTicks.Value <= 0;
        var exitWarp = requestStop || invalidWarpClock
            || facts.MaximumWarpGameTicks!.Value - facts.WarpGameTicks!.Value <= WarpExitLeadGameTicks;
        return new VoyagerControlDeadlineDecision
        {
            ShouldRequestStop = requestStop,
            ShouldExitWarp = exitWarp,
            StopOutcomeUnknown = facts.StopRequested && !facts.NativeStopSettled && !StopWindowProvedOpen(facts),
            StopReasonCode = reason ?? (facts.StopRequested ? "voyager_stop_requested" : null),
        };
    }

    private static string? StopReason(VoyagerControlDeadlineFacts facts)
    {
        if (!Nonnegative(facts.CurrentGameTick) || !Nonnegative(facts.LastControlledTick)
            || !Nonnegative(facts.FlightDeadlineTick) || !Nonnegative(facts.WarpGameTicks)
            || !facts.MaximumWarpGameTicks.HasValue || facts.MaximumWarpGameTicks.Value <= 0)
            return "voyager_clock_unavailable_or_invalid";
        if (facts.CurrentGameTick!.Value < facts.LastControlledTick!.Value)
            return "voyager_game_tick_reversed";
        if (facts.CurrentGameTick.Value - facts.LastControlledTick.Value > MaximumControlGapGameTicks)
            return "voyager_control_gap_exceeded";
        if (!UtcAvailable(facts.NowUtc) || !UtcAvailable(facts.JourneyDeadlineUtc)
            || !UtcAvailable(facts.ControlLeaseExpiresAtUtc))
            return "voyager_utc_clock_unavailable";
        if (facts.CurrentGameTick.Value >= facts.FlightDeadlineTick!.Value)
            return "voyager_flight_deadline_reached";
        if (facts.NowUtc!.Value >= facts.JourneyDeadlineUtc!.Value)
            return "voyager_journey_deadline_reached";
        if (facts.NowUtc.Value >= facts.ControlLeaseExpiresAtUtc!.Value)
            return "voyager_control_lease_expired";
        if (facts.ControlLeaseExpiresAtUtc.Value - facts.NowUtc.Value > TimeSpan.FromSeconds(ControlLeaseSeconds))
            return "voyager_control_lease_exceeds_bound";
        return null;
    }

    private static bool StopWindowProvedOpen(VoyagerControlDeadlineFacts facts)
    {
        if (!Nonnegative(facts.CurrentGameTick) || !Nonnegative(facts.StopDeadlineGameTick)
            || !UtcAvailable(facts.NowUtc) || !UtcAvailable(facts.StopDeadlineUtc)) return false;
        var tick = facts.CurrentGameTick!.Value;
        var tickDeadline = facts.StopDeadlineGameTick!.Value;
        var now = facts.NowUtc!.Value;
        var utcDeadline = facts.StopDeadlineUtc!.Value;
        if (tick >= tickDeadline || now >= utcDeadline) return false;
        // A supplied future deadline cannot silently enlarge either cleanup bound.
        return tickDeadline - tick <= MaximumStopGameTicks
            && utcDeadline - now <= TimeSpan.FromSeconds(StopCleanupSeconds);
    }

    private static bool Nonnegative(long? value) => value.HasValue && value.Value >= 0;
    private static bool UtcAvailable(DateTimeOffset? value) => value.HasValue && value.Value != DateTimeOffset.MinValue;
}
