using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.Transport;
using Spherewright.Plugin.Security;
using System.Text;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    internal bool TryGrantRecoveredReturnOnlyTask(FlightCheckpointTicket checkpoint,
        VoyagerReturnOnlyRecoveryRequest request, string originalJourneyJson, string reloadActionId,
        string adoptedSessionId, long? currentTick, DateTimeOffset now, out string rejection,
        out NativeResumeFailureSnapshot? failure)
    {
        rejection = "The separately confirmed return-only task does not bind this exact recovered return and original ledger.";
        failure = null;
        var r = VoyagerJourney;
        var e = new NativeResumeDecisionEvidence("recovered_return_only_task", currentTick, now, true);
        e.Number("new_tick_cap", request.MaximumGameTicks);
        e.Number("source_tick", checkpoint.SavedGameTick);
        e.Number("new_remaining_ticks", r?.ReturnOnlyRemaining(request, currentTick));
        e.Number("original_spent_ticks", r?.SpentGameTicks);
        e.Number("original_maximum_ticks", r?.MaximumGameTicks);
        e.Number("new_remaining_wall_seconds", (request.DeadlineUtc - now).TotalSeconds);
        e.NotReached("original_ledger_archived", "task_record_persisted", "native_return", "home_save");
        VoyagerJourneyRecord? source;
        try { source = PluginJson.Deserialize<VoyagerJourneyRecord>(originalJourneyJson); }
        catch (Newtonsoft.Json.JsonException)
        {
            e.Require("original_journey_parse", false);
            failure = e.Failure(BridgeErrorCodes.StaleState); return false;
        }
        var task = r?.ReturnOnlyTask;
        // Re-observing the SAME action may reconcile metadata, never mint a new task.
        if (task is not null)
        {
            var matches = task.TaskId == reloadActionId && task.SessionId == adoptedSessionId
                && task.OriginalJourneyJson == originalJourneyJson && task.CheckpointId == checkpoint.CheckpointId
                && task.CheckpointFingerprint == checkpoint.CheckpointFileFingerprint
                && task.DeadlineUtc == request.DeadlineUtc && task.MaximumGameTicks == request.MaximumGameTicks;
            if (!e.Require("same_original_task_proof", matches))
            { failure = e.Failure(BridgeErrorCodes.StaleState); return false; }
            if (TryPersistVoyagerJourney(r!, out rejection)) return true;
            e.Require("task_record_persisted", null);
            failure = e.Failure(BridgeErrorCodes.ActionOutcomeUnknown); return false;
        }
        failure = VoyagerReturnOnlyRecoveryPolicy.RequestFailure(request, now);
        if (failure is not null)
        {
            failure.Stage = "recovered_return_only_task"; failure.CapturedAtGameTick = currentTick;
            foreach (var value in e.Failure("").Values) failure.Values[value.Key] = value.Value;
            return false;
        }
        var checks = new (string Name, bool? Value)[] {
            ("current_journey_observed", r is not null),
            ("source_journey_observed", source is not null),
            ("source_snapshot_bounded", originalJourneyJson.Length <= 16384),
            ("source_journey_valid", source is null ? null : ValidVoyagerJourney(source)),
            ("no_prior_new_task", source is null ? null : source.ReturnOnlyTask is null),
            ("recovered_remote_phase", r is null ? null : r.Phase == "remote_landed" && r.ReturnLeg),
            ("current_checkpoint_binding", r is null ? null : r.ActiveCheckpointId == checkpoint.CheckpointId && r.ActiveActionId == checkpoint.LastAttemptActionId),
            ("source_checkpoint_binding", source is null ? null : source.ActiveCheckpointId == checkpoint.CheckpointId && source.ActiveActionId == checkpoint.LastAttemptActionId && source.ReturnLeg),
            ("original_terms_preserved", source is null || r is null ? null : SameOriginalReturnTerms(source, r)),
            ("old_allowance_conservatively_forfeited", r is null ? null : r.SpentGameTicks >= r.MaximumGameTicks && r.SpentGameTicksIsConservative),
            ("source_tick_preserved", r is null ? null : r.LastLandedGameTick == checkpoint.SavedGameTick),
            ("durable_checkpoint_observed", _currentTicket is not null),
            ("exact_durable_checkpoint", _currentTicket is null ? null : PluginJson.Serialize(checkpoint) == PluginJson.Serialize(_currentTicket)),
            ("reload_reconciled", !checkpoint.ReloadPending && checkpoint.LifecycleState == FlightCheckpointLifecycleStates.Recovered),
            ("exact_original_reload_action", checkpoint.ReloadActionId == reloadActionId && Guid.TryParse(reloadActionId, out _)),
            ("adopted_session_identity", Guid.TryParse(adoptedSessionId, out _)),
            ("exact_approved_source", CheckpointMatchesReturnTask(checkpoint, request)),
            ("remaining_tick_observed", r is null ? null : r.ReturnOnlyRemaining(request, currentTick).HasValue),
            ("minimum_return_ticks", r is null ? null : VoyagerReturnOnlyRecoveryPolicy.FlightTicks(
                r.ReturnOnlyRemaining(request, currentTick), r.ReturnReserveGameTicks) >= 3600),
        };
        e.NotReached(checks.Select(c => c.Name).ToArray());
        foreach (var check in checks)
            if (!e.Require(check.Name, check.Value))
            { failure = e.Failure(BridgeErrorCodes.StaleState); rejection += " " + failure.FirstFailedPredicate; return false; }
        r!.ReturnOnlyTask = new VoyagerReturnOnlyTaskRecord {
            TaskId = reloadActionId, SessionId = adoptedSessionId, OriginalJourneyJson = originalJourneyJson,
            OriginalFlightActionId = checkpoint.LastAttemptActionId!, CheckpointId = checkpoint.CheckpointId,
            CheckpointFingerprint = checkpoint.CheckpointFileFingerprint, StartedAtGameTick = checkpoint.SavedGameTick,
            MaximumGameTicks = request.MaximumGameTicks, IssuedAtUtc = now, DeadlineUtc = request.DeadlineUtc,
        };
        try
        {
            // Preserve the exact prepared content independently of the active
            // record, so a later normally completed new journey cannot erase it.
            var archive = Path.Combine(Path.GetDirectoryName(_ticketPath)!, "voyager-return-source-"
                + Guid.Parse(r.JourneyId).ToString("N") + "-" + Guid.Parse(reloadActionId).ToString("N") + ".json");
            var bytes = new UTF8Encoding(false).GetBytes(originalJourneyJson);
            if (File.Exists(archive))
            {
                if (new FileInfo(archive).Length != bytes.Length || !File.ReadAllBytes(archive).SequenceEqual(bytes))
                    throw new InvalidDataException("The original return ledger archive changed.");
            }
            else WindowsCurrentUserSecurity.WriteSecureNewFile(archive, bytes);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            rejection = "The original return ledger could not be preserved (" + exception.GetType().Name + ").";
            e.Require("original_ledger_archived", null);
            failure = e.Failure(BridgeErrorCodes.ActionOutcomeUnknown); return false;
        }
        e.Require("original_ledger_archived", true);
        if (TryPersistVoyagerJourneyCore(r, out rejection, false, false, false, true)) return true;
        e.Require("task_record_persisted", null);
        failure = e.Failure(BridgeErrorCodes.ActionOutcomeUnknown);
        return false;
    }

    internal static bool CheckpointMatchesReturnTask(FlightCheckpointTicket c, VoyagerReturnOnlyRecoveryRequest r)
        => c.VoyagerJourneyId == r.ExpectedJourneyId && c.LastAttemptActionId == r.ExpectedFlightActionId
            && c.CheckpointId == r.ExpectedCheckpointId && c.SavedGameTick == r.ExpectedCheckpointGameTick
            && c.CheckpointFileFingerprint == r.ExpectedCheckpointFileFingerprint;

    private static bool SameOriginalReturnTerms(VoyagerJourneyRecord old, VoyagerJourneyRecord next)
        => old.JourneyId == next.JourneyId && old.OwnedSaveName == next.OwnedSaveName && old.GameVersion == next.GameVersion
            && old.HomePlanetId == next.HomePlanetId && old.HomeStarId == next.HomeStarId
            && old.RemotePlanetId == next.RemotePlanetId && old.DestinationStarId == next.DestinationStarId
            && old.StartedAtGameTick == next.StartedAtGameTick && old.MaximumGameTicks == next.MaximumGameTicks
            && old.ReturnReserveGameTicks == next.ReturnReserveGameTicks && old.IssuedAtUtc == next.IssuedAtUtc
            && old.DeadlineUtc == next.DeadlineUtc && old.ReturnEnergyFloorJoules == next.ReturnEnergyFloorJoules
            && old.WithdrawalReserveJoules == next.WithdrawalReserveJoules
            && old.ReturnCoreEnergyMinimumJoules == next.ReturnCoreEnergyMinimumJoules
            && old.ExtraWarperReserve == next.ExtraWarperReserve && old.SpentGameTicks <= next.SpentGameTicks;

    private static bool ValidReturnOnlyTask(VoyagerJourneyRecord r)
    {
        var t = r.ReturnOnlyTask;
        if (t is null) return true;
        if (!Guid.TryParse(t.TaskId, out _) || !Guid.TryParse(t.SessionId, out _) || t.StartedAtGameTick < 0
            || t.MaximumGameTicks < VoyagerReturnOnlyRecoveryPolicy.MinimumTotalGameTicks
            || t.MaximumGameTicks > VoyagerReturnOnlyRecoveryPolicy.MaximumGameTicks
            || t.IssuedAtUtc == default || t.DeadlineUtc <= t.IssuedAtUtc
            || t.DeadlineUtc - t.IssuedAtUtc > TimeSpan.FromSeconds(VoyagerReturnOnlyRecoveryPolicy.MaximumWallSeconds)
            || string.IsNullOrEmpty(t.CheckpointId) || string.IsNullOrEmpty(t.CheckpointFingerprint)
            || string.IsNullOrEmpty(t.OriginalFlightActionId) || !r.ReturnLeg
            || string.IsNullOrWhiteSpace(t.OriginalJourneyJson) || t.OriginalJourneyJson.Length > 16384) return false;
        try
        {
            var original = PluginJson.Deserialize<VoyagerJourneyRecord>(t.OriginalJourneyJson);
            return original is not null && original.ReturnOnlyTask is null && original.ReturnLeg
                && original.ActiveCheckpointId == t.CheckpointId && original.ActiveActionId == t.OriginalFlightActionId
                && SameOriginalReturnTerms(original, r) && ValidReturnOnlyContinuations(t);
        }
        catch (Newtonsoft.Json.JsonException) { return false; }
    }
}

internal sealed partial class VoyagerJourneyRecord
{
    internal long? ReturnOnlyRemaining(VoyagerReturnOnlyRecoveryRequest request, long? current)
        => VoyagerReturnOnlyRecoveryPolicy.RemainingTicks(request.ExpectedCheckpointGameTick ?? -1, request.MaximumGameTicks, current);

    internal long? RemainingReturnOnlyTicks(string? session, long? current)
        => ReturnOnlyTask is { } t && t.CurrentSessionId == session
            ? VoyagerReturnOnlyRecoveryPolicy.RemainingTicks(t.StartedAtGameTick, t.MaximumGameTicks, current) : null;

    internal bool ReturnOnlyScopeActive => ReturnOnlyTask is not null && Phase != "completed"
        && Phase != "recovered_closed" && Phase != "prelaunch_failed";
}

internal sealed class VoyagerReturnOnlyTaskRecord
{
    public string TaskId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string OriginalFlightActionId { get; set; } = string.Empty;
    public string OriginalJourneyJson { get; set; } = string.Empty;
    public string CheckpointId { get; set; } = string.Empty;
    public string CheckpointFingerprint { get; set; } = string.Empty;
    public long StartedAtGameTick { get; set; }
    public long MaximumGameTicks { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
    public List<VoyagerReturnOnlyContinuationRun> Continuations { get; set; } = new();
    public VoyagerReturnOnlyPreservationReceipt? PreservationReceipt {get;set;}
    public bool ShouldSerializePreservationReceipt()=>PreservationReceipt is not null;
    public bool ShouldSerializeContinuations() => Continuations?.Count > 0;
    internal VoyagerReturnOnlyContinuationRun? CurrentContinuation => Continuations?.LastOrDefault();
    internal string CurrentSessionId => CurrentContinuation is { } r ? r.SessionId : SessionId;
    internal string CurrentActionId => CurrentContinuation?.ActionId ?? TaskId;
    internal DateTimeOffset CurrentDeadlineUtc => CurrentContinuation?.DeadlineUtc ?? DeadlineUtc;
}
