using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

// A separately confirmed task, never a refund or a renewal of the old journey.
public static class VoyagerReturnOnlyRecoveryPolicy
{
    public const string ScopeBlocker = "voyager_return_only_task";
    public const string ScopeMessage = "This separately authorized recovery permits only the exact original-home return, its cancellation and normal Save; other writes remain blocked.";
    public const long MaximumGameTicks = 54000;
    public const int MaximumWallSeconds = 1200;
    // Stop stays INSIDE the new total allowance, including an entered601 tail.
    public const int StopAndTailGameTicks = VoyagerControlDeadlinePolicy.MaximumStopGameTicks + 1;
    public const int MinimumTotalGameTicks = 3600 + StopAndTailGameTicks;

    public static long FlightTicks(long? remaining, long originalReturnLimit)
        => remaining.HasValue && remaining.Value >= StopAndTailGameTicks && originalReturnLimit >= 3600
            ? Math.Min(originalReturnLimit, remaining.Value - StopAndTailGameTicks) : -1;

    public static NativeResumeFailureSnapshot? RequestFailure(VoyagerReturnOnlyRecoveryRequest r, DateTimeOffset now)
    {
        var e = new NativeResumeDecisionEvidence("return_only_recovery_request", null, now, true);
        e.Number("requested_checkpoint_tick", r.ExpectedCheckpointGameTick);
        e.Number("requested_maximum_ticks", r.MaximumGameTicks);
        e.Number("remaining_wall_seconds", (r.DeadlineUtc - now).TotalSeconds);
        e.NotReached("exact_checkpoint_file", "original_journey_binding", "native_load", "healthy_takeover", "native_return", "home_save");
        var checks = new (string Name, bool Value)[] {
            ("explicit_progress_loss_acceptance", r.AcceptUnstoredProgressLoss),
            ("original_journey_id", Guid.TryParse(r.ExpectedJourneyId, out _)),
            ("original_flight_action", !string.IsNullOrWhiteSpace(r.ExpectedFlightActionId)),
            ("exact_checkpoint_id", !string.IsNullOrWhiteSpace(r.ExpectedCheckpointId)),
            ("checkpoint_tick_observed", r.ExpectedCheckpointGameTick.HasValue),
            ("checkpoint_tick_nonnegative", r.ExpectedCheckpointGameTick >= 0),
            ("checkpoint_file_fingerprint", !string.IsNullOrWhiteSpace(r.ExpectedCheckpointFileFingerprint)),
            ("new_tick_cap_minimum", r.MaximumGameTicks >= MinimumTotalGameTicks),
            ("new_tick_cap_maximum", r.MaximumGameTicks <= MaximumGameTicks),
            ("new_deadline_utc", r.DeadlineUtc.Offset == TimeSpan.Zero),
            ("new_window_not_expired", r.DeadlineUtc > now),
            ("new_window_finite", r.DeadlineUtc - now <= TimeSpan.FromSeconds(MaximumWallSeconds)),
        };
        e.NotReached(checks.Select(c => c.Name).ToArray());
        foreach (var check in checks)
            if (!e.Require(check.Name, check.Value)) return e.Failure(BridgeErrorCodes.InvalidRequest);
        return null;
    }

    public static long? RemainingTicks(long start, long maximum, long? current)
    {
        if (start < 0 || maximum < 3600 || maximum > MaximumGameTicks || !current.HasValue || current < start) return null;
        var remaining = (decimal)maximum - ((decimal)current.Value - start);
        return remaining >= 0 ? (long)remaining : 0L;
    }

    public static bool HasMatchingEcho(VoyagerReturnOnlyRecoveryRequest r, PreparedFlightCheckpointReloadPlan p)
        => p.ReturnOnlyTask is { } e && p.CheckpointId == r.ExpectedCheckpointId && p.SavedGameTick == r.ExpectedCheckpointGameTick
            && e.ExpectedJourneyId == r.ExpectedJourneyId && e.ExpectedFlightActionId == r.ExpectedFlightActionId
            && e.ExpectedCheckpointId == r.ExpectedCheckpointId && e.ExpectedCheckpointGameTick == r.ExpectedCheckpointGameTick
            && e.ExpectedCheckpointFileFingerprint == r.ExpectedCheckpointFileFingerprint && e.DeadlineUtc == r.DeadlineUtc
            && e.MaximumGameTicks == r.MaximumGameTicks && e.AcceptUnstoredProgressLoss && r.AcceptUnstoredProgressLoss;

    public static List<WriteBlocker> ActionBlockers(IEnumerable<WriteBlocker> blockers, string actionKind, bool returnLeg)
    {
        var permitted = actionKind == NormalActionKinds.Save || actionKind == NormalActionKinds.CancelVoyagerFlight
            || actionKind == NormalActionKinds.InterplanetaryFlight && returnLeg;
        return blockers.Where(b => !permitted || b.Code != ScopeBlocker || b.Message != ScopeMessage).ToList();
    }
}
