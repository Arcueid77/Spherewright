using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    private bool FailedLandingNativeEvidenceMatches(VoyagerRemoteResumeEvidence evidence)
    {
        if (evidence.FailedLandingSource is not { IsDisposed: false } source || VoyagerJourney is not { } j) return false;
        if (evidence.JourneyJson == source.JourneyJson && PluginJson.Serialize(j) == source.JourneyJson) return true;
        if (j.FailedLandingAdmissions.LastOrDefault() is not { } run
            || run.ContextFingerprint != source.Fingerprint || run.SourceFingerprint != evidence.SourceFingerprint
            || run.SourceGameTick != evidence.SourceGameTick || run.DeadlineUtc != evidence.RecoveryProposal?.DeadlineUtc
            || run.State is not ("pending_native_load" or "adopted")) return false;
        j.FailedLandingAdmissions.RemoveAt(j.FailedLandingAdmissions.Count - 1);
        return PluginJson.Serialize(j) == source.JourneyJson;
    }

    internal bool TryRegisterFailedLandingAdmission(VoyagerFailedLandingSource source, string actionId,
        bool confirmed, string confirmationDigest, DateTimeOffset now, out string rejection)
    {
        rejection = "failed_landing_registration_unproved";
        if (source.IsDisposed || !confirmed || !Guid.TryParse(actionId, out _)
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(confirmationDigest)
            || now == default || now.Offset != TimeSpan.Zero || now >= source.Proposal.DeadlineUtc
            || source.Proposal.DeadlineUtc - now > TimeSpan.FromHours(1)) return false;
        if (PluginJson.Serialize(VoyagerJourney) != source.JourneyJson)
        { rejection = "failed_landing_registration_memory_journey_changed"; return false; }
        if (!FailedLandingJourneyMatchesDisk(source.JourneyJson))
        { rejection = "failed_landing_registration_durable_journey_changed"; return false; }
        if (PluginJson.Serialize(_currentTicket) != source.CheckpointJson || !CurrentContinuationCheckpointMatchesDisk())
        { rejection = "failed_landing_registration_checkpoint_changed"; return false; }
        var next = source.Journey;
        if (next.FailedLandingAdmissions.Count >= 16 || next.FailedLandingAdmissions.Any(r => r.ActionId == actionId)
            || next.FailedLandingAdmissions.LastOrDefault()?.State is "pending_native_load" or "adopted" or "outcome_unknown") return false;
        next.FailedLandingAdmissions.Add(new()
        {
            ActionId = actionId, OriginalFlightActionId = next.ActiveActionId,
            CheckpointId = next.ActiveCheckpointId,
            OriginalCheckpointStateHash = CanonicalStateHash.Combine("failed-landing-original-checkpoint-v1", source.CheckpointJson),
            OriginalCheckpointJson = source.CheckpointJson,
            SourceFingerprint = source.Source.Fingerprint, PrimaryFingerprint = source.Primary.Fingerprint,
            ContextFingerprint = source.Fingerprint, SourceGameTick = source.SavedAccountingAnchorGameTick,
            ConservedSpentGameTicks = source.ConservedSpentGameTicks, ConfirmationDigest = confirmationDigest,
            SourceSessionId = source.Ticket.SourceSessionId,
            ConfirmedAtUtc = now, DeadlineUtc = source.Proposal.DeadlineUtc,
            MaximumChargeGameTicks = source.Proposal.MaximumChargeGameTicks,
        });
        // Only the Journey needs an atomic replacement. Source/checkpoint/Journal
        // leases stay held through Native loading; this main-thread handoff is
        // immediately followed by the Store's exact durable comparison.
        source.ReleaseJourneyLeaseForRegistration();
        return PersistFailedLandingAdmission(next, out rejection);
    }

    internal bool TryBindFailedLandingAdmissionSession(string actionId, string sessionId, long tick, out string rejection)
    {
        rejection = "failed_landing_loaded_session_unproved";
        var j = VoyagerJourney; var r = j?.FailedLandingAdmissions.LastOrDefault();
        if (j is null || r is null || r.ActionId != actionId || r.State != "pending_native_load"
            || !Guid.TryParse(sessionId, out _) || sessionId == r.SourceSessionId || j.FailedLandingAdmissions.Any(x => x.SessionId == sessionId)
            || DateTimeOffset.UtcNow >= r.DeadlineUtc || tick < r.SourceGameTick
            || (decimal)tick - r.SourceGameTick > r.MaximumChargeGameTicks) return false;
        r.SessionId = sessionId; r.AdoptedAtGameTick = tick; r.State = "adopted";
        return PersistFailedLandingAdmission(j, out rejection);
    }

    internal bool TryFinishFailedLandingAdmission(string actionId, bool healthProved,
        NativeResumeFailureSnapshot? failure, long? observedOwnedTick, out string rejection)
    {
        rejection = "failed_landing_terminal_admission_unproved";
        var j = VoyagerJourney; var r = j?.FailedLandingAdmissions.LastOrDefault();
        if (j is null || r is null || r.ActionId != actionId || r.State is not ("pending_native_load" or "adopted")
            || healthProved && (r.State != "adopted" || failure is not null || !observedOwnedTick.HasValue)
            || !healthProved && failure is null) return false;
        if (observedOwnedTick.HasValue)
        {
            var elapsed = (decimal)observedOwnedTick.Value - r.SourceGameTick;
            if (elapsed < 0 || observedOwnedTick < r.AdoptedAtGameTick
                || healthProved && elapsed > r.MaximumChargeGameTicks + VoyagerLocalWalkReservePolicy.StopGameTicks
                || r.ConservedSpentGameTicks + elapsed > long.MaxValue) return false;
            if (healthProved && (DateTimeOffset.UtcNow >= r.DeadlineUtc
                || r.ConservedSpentGameTicks + elapsed + j.ReturnReserveGameTicks + 1 + VoyagerLocalWalkReservePolicy.StopGameTicks > j.MaximumGameTicks)) return false;
            // Every actual tick is conserved on success AND failure. A missing
            // Native observation is unknown, never a zero-tick refund.
            j.SpentGameTicks = (long)(r.ConservedSpentGameTicks + elapsed);
            j.LastAccountedGameTick = observedOwnedTick.Value;
            r.TerminalAtGameTick = observedOwnedTick;
        }
        r.State = healthProved ? "completed_health" : observedOwnedTick.HasValue ? "failed_health" : "outcome_unknown";
        r.FirstFailure = failure?.Copy();
        if (healthProved)
        {
            j.Phase = "remote_landed";
            // This is a NEW successful admission, not an old flight witness.
            j.LastLandedGameTick = observedOwnedTick;
        }
        return PersistFailedLandingAdmission(j, out rejection);
    }

    private bool PersistFailedLandingAdmission(VoyagerJourneyRecord next, out string rejection)
    {
        rejection = "failed_landing_record_size_exceeded";
        if (System.Text.Encoding.UTF8.GetByteCount(PluginJson.Serialize(next)) > 65536) return false;
        return TryPersistVoyagerJourneyCore(next, out rejection, false, false, false, false, false, true);
    }

    private bool FailedLandingJourneyMatchesDisk(string expected)
    {
        try { return PluginJson.Serialize(ReadVoyagerRecord()) == expected; }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Newtonsoft.Json.JsonException)
        { return false; }
    }

    private static bool ValidFailedLandingAdmissions(VoyagerJourneyRecord j)
    {
        var runs = j.FailedLandingAdmissions;
        if (runs is null || runs.Count > 16 || runs.Any(r => r is null)
            || runs.Select(r => r.ActionId).Distinct().Count() != runs.Count) return false;
        for (var i = 0; i < runs.Count; i++)
        {
            var r = runs[i];
            if (!Guid.TryParse(r.ActionId, out _) || !Guid.TryParse(r.OriginalFlightActionId, out _)
                || !Guid.TryParse(r.CheckpointId, out _) || r.SourceGameTick < 0 || r.ConservedSpentGameTicks < 0
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.SourceFingerprint)
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.PrimaryFingerprint)
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.ContextFingerprint)
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.OriginalCheckpointStateHash)
                || string.IsNullOrEmpty(r.OriginalCheckpointJson)
                || r.OriginalCheckpointStateHash != CanonicalStateHash.Combine("failed-landing-original-checkpoint-v1", r.OriginalCheckpointJson)
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.ConfirmationDigest)
                || r.MaximumChargeGameTicks < 1 || r.MaximumChargeGameTicks > 18000
                || string.IsNullOrEmpty(r.SourceSessionId) || r.ConfirmedAtUtc.Offset != TimeSpan.Zero || r.DeadlineUtc.Offset != TimeSpan.Zero
                || r.ConfirmedAtUtc == default || r.DeadlineUtc <= r.ConfirmedAtUtc
                || r.DeadlineUtc - r.ConfirmedAtUtc > TimeSpan.FromHours(1)) return false;
            if (i < runs.Count - 1 && r.State is not ("completed_health" or "failed_health")) return false;
            switch (r.State)
            {
                case "pending_native_load":
                    if (r.SessionId != string.Empty || r.AdoptedAtGameTick.HasValue || r.TerminalAtGameTick.HasValue || r.FirstFailure is not null) return false;
                    break;
                case "adopted":
                case "completed_health":
                    if (!Guid.TryParse(r.SessionId, out _) || r.AdoptedAtGameTick < r.SourceGameTick
                        || !r.AdoptedAtGameTick.HasValue || r.FirstFailure is not null
                        || r.State == "adopted" && r.TerminalAtGameTick.HasValue
                        || r.State == "completed_health" && (!r.TerminalAtGameTick.HasValue || r.TerminalAtGameTick < r.AdoptedAtGameTick)) return false;
                    break;
                case "failed_health":
                    if (r.FirstFailure is null || !r.TerminalAtGameTick.HasValue || r.TerminalAtGameTick < r.SourceGameTick) return false;
                    break;
                case "outcome_unknown":
                    if (r.FirstFailure is null || r.TerminalAtGameTick.HasValue) return false;
                    break;
                default: return false;
            }
        }
        return true;
    }

    private static bool ValidFailedLandingAdmissionTransition(VoyagerJourneyRecord old, VoyagerJourneyRecord next)
    {
        var before = old.FailedLandingAdmissions; var after = next.FailedLandingAdmissions;
        if (old.Phase != "recovery_required" || old.ReturnLeg || old.LastLandedGameTick.HasValue) return false;
        var a = PluginJson.Deserialize<VoyagerJourneyRecord>(PluginJson.Serialize(old))!;
        var b = PluginJson.Deserialize<VoyagerJourneyRecord>(PluginJson.Serialize(next))!;
        a.FailedLandingAdmissions.Clear(); b.FailedLandingAdmissions.Clear();
        if (after.Count == before.Count + 1)
            return after.Last().State == "pending_native_load" && after.Last().OriginalFlightActionId == old.ActiveActionId
                && after.Last().CheckpointId == old.ActiveCheckpointId && after.Last().SourceGameTick == old.LastAccountedGameTick
                && after.Last().ConservedSpentGameTicks == old.SpentGameTicks
                && (before.Count == 0 || before.Last().State is "completed_health" or "failed_health")
                && after.Take(before.Count).Select(PluginJson.Serialize).SequenceEqual(before.Select(PluginJson.Serialize))
                && PluginJson.Serialize(a) == PluginJson.Serialize(b);
        if (before.Count == 0 || after.Count != before.Count
            || !after.Take(before.Count - 1).Select(PluginJson.Serialize).SequenceEqual(before.Take(before.Count - 1).Select(PluginJson.Serialize))) return false;
        var x = before.Last(); var y = after.Last();
        var immutableX = PluginJson.Deserialize<VoyagerFailedLandingAdmissionRun>(PluginJson.Serialize(x))!;
        var immutableY = PluginJson.Deserialize<VoyagerFailedLandingAdmissionRun>(PluginJson.Serialize(y))!;
        immutableX.State = immutableY.State = "";
        immutableX.SessionId = immutableY.SessionId = "";
        immutableX.AdoptedAtGameTick = immutableY.AdoptedAtGameTick = null;
        immutableX.TerminalAtGameTick = immutableY.TerminalAtGameTick = null;
        immutableX.FirstFailure = immutableY.FirstFailure = null;
        if (PluginJson.Serialize(immutableX) != PluginJson.Serialize(immutableY)) return false;
        if (x.State == "pending_native_load" && y.State == "adopted")
            return PluginJson.Serialize(a) == PluginJson.Serialize(b);
        if (x.State is not ("pending_native_load" or "adopted") || y.State is not ("completed_health" or "failed_health" or "outcome_unknown")) return false;
        if (x.State == "adopted" && (x.SessionId != y.SessionId || x.AdoptedAtGameTick != y.AdoptedAtGameTick)) return false;
        if (y.TerminalAtGameTick is { } tick)
        {
            var spent = (decimal)x.ConservedSpentGameTicks + tick - x.SourceGameTick;
            if (spent > long.MaxValue || spent < old.SpentGameTicks || next.SpentGameTicks != spent || next.LastAccountedGameTick != tick) return false;
            b.SpentGameTicks = a.SpentGameTicks; b.LastAccountedGameTick = a.LastAccountedGameTick;
        }
        if (y.State == "completed_health")
        {
            if (x.State != "adopted" || next.Phase != "remote_landed" || next.LastLandedGameTick != y.TerminalAtGameTick) return false;
            b.Phase = a.Phase; b.LastLandedGameTick = a.LastLandedGameTick;
        }
        return PluginJson.Serialize(a) == PluginJson.Serialize(b);
    }
}

internal sealed partial class VoyagerJourneyRecord
{
    public List<VoyagerFailedLandingAdmissionRun> FailedLandingAdmissions { get; set; } = new();
    public bool ShouldSerializeFailedLandingAdmissions() => FailedLandingAdmissions?.Count > 0;
}

internal sealed class VoyagerFailedLandingAdmissionRun
{
    public string ActionId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string SourceSessionId { get; set; } = "";
    public string OriginalFlightActionId { get; set; } = "";
    public string CheckpointId { get; set; } = "";
    public string OriginalCheckpointStateHash { get; set; } = "";
    public string OriginalCheckpointJson { get; set; } = "";
    public long SourceGameTick { get; set; }
    public long ConservedSpentGameTicks { get; set; }
    public string SourceFingerprint { get; set; } = "";
    public string PrimaryFingerprint { get; set; } = "";
    public string ContextFingerprint { get; set; } = "";
    public string ConfirmationDigest { get; set; } = "";
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
    public long MaximumChargeGameTicks { get; set; }
    public string State { get; set; } = "pending_native_load";
    public long? AdoptedAtGameTick { get; set; }
    public long? TerminalAtGameTick { get; set; }
    public NativeResumeFailureSnapshot? FirstFailure { get; set; }
}
