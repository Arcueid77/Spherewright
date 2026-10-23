using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    private bool ReturnOnlyNativeEvidenceMatches(VoyagerRemoteResumeEvidence evidence)
    {
        if (evidence.ReturnOnlySource is not { } source || VoyagerJourney is not { } j
            || j.ReturnOnlyTask?.CurrentContinuation is not { } run
            || run.ContextFingerprint != source.Fingerprint || run.SourceFingerprint != evidence.SourceFingerprint
            || run.SourceGameTick != evidence.SourceGameTick || run.DeadlineUtc != evidence.RecoveryProposal?.DeadlineUtc
            || run.State is not ("pending_native_load" or "adopted" or "completed_health")) return false;
        j.ReturnOnlyTask.Continuations.RemoveAt(j.ReturnOnlyTask.Continuations.Count - 1);
        return PluginJson.Serialize(j) == source.JourneyJson;
    }

    // Reads the fixed Native LastExit and current protected credential. The old
    // checkpoint is a historical witness only, even when retired or expired.
    internal bool TryOpenReturnOnlyContinuationSource(OwnedWorldResumeTicketStore credentials, string token,
        DateTimeOffset? proposedWallDeadline, out VoyagerReturnOnlyContinuationSource? evidence,
        out NativeResumeFailureSnapshot? failure)
    {
        evidence = null; failure = null;
        var e = new NativeResumeDecisionEvidence("return_only_continuation_capture", null, DateTimeOffset.UtcNow, true);
        e.NotReached("protected_credential", "original_task", "fixed_lastexit_file", "primary_file", "original_journal",
            "owner_commit_confirmation", "native_load", "healthy_takeover");
        OwnedSaveRecoveryLease? source = null, primary = null;
        FileStream? journal = null;
        try
        {
            if (!credentials.TryGetSavedWorldOpenProvenance(token, out var ticket, out var provenance, out _)
                || ticket is null)
            { e.Require("protected_credential", false); failure = e.Failure("return_only_continuation_credential_unproved"); return false; }
            e.Require("protected_credential", true);
            var j = VoyagerJourney; var task = j?.ReturnOnlyTask; var cp = _currentTicket;
            if (!e.Require("original_task", j is not null && task is not null && ValidVoyagerJourney(j)))
            { failure = e.Failure("return_only_continuation_task_unproved"); return false; }
            if (PluginJson.Serialize(ReadVoyagerRecord()) != PluginJson.Serialize(j)
                || !CurrentContinuationCheckpointMatchesDisk())
            { e.Require("durable_task_context", false); failure = e.Failure("return_only_continuation_context_changed"); return false; }
            source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), ticket.OwnedSaveName);
            e.Require("fixed_lastexit_file", true);
            primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.OwnedSaveName), ticket.OwnedSaveName);
            e.Require("primary_file", true);
            journal = credentials.OpenReauthorizationJournalLease(ticket, provenance, savedWorldOpenOnly: true);
            e.Require("original_journal", true);
            var f = new VoyagerReturnOnlyContinuationFacts
            {
                ExactOwnedNative = source.Prefix.MatchesExpectedIdentity && primary.Prefix.MatchesExpectedIdentity
                    && source.Prefix.Peaceful && primary.Prefix.Peaceful && j!.OwnedSaveName == ticket.OwnedSaveName
                    && j.GameVersion == ticket.GameVersion && ticket.GameVersion == _gameVersion
                    && source.Prefix.GameVersion == _gameVersion && primary.Prefix.GameVersion == _gameVersion,
                ExactTaskCredentialSession = task!.CurrentSessionId == ticket.SourceSessionId
                    && ticket.ExpectedPlanetId == j!.RemotePlanetId,
                OriginalCheckpointWitness = cp is not null && IsStructurallyValid(cp)
                    && cp.CheckpointId == task.CheckpointId && cp.CheckpointFileFingerprint == task.CheckpointFingerprint
                    && cp.ReloadActionId == task.TaskId && !cp.ReloadPending
                    && (cp.LifecycleState == FlightCheckpointLifecycleStates.Recovered
                        || cp.LifecycleState == FlightCheckpointLifecycleStates.Retired
                            && cp.RetiredAtGameTick >= cp.SavedGameTick && cp.RetiredAtGameTick <= source.Prefix.GameTick),
                JournalBound = ticket.GameplayJournalCheckpoint is not null,
                NoPendingRecovery = !HasUnreconciledReload && task.CurrentContinuation?.State is not
                    ("pending_native_load" or "adopted" or "outcome_unknown"),
                RemoteReturnPhase = j!.ReturnOnlyScopeActive && j.Phase == "remote_landed" && j.ReturnLeg,
                // Open succeeds only after the entire bounded file was hashed.
                CompleteSourceFiles = true,
                SourceGameTick = source.Prefix.GameTick,
                PrimaryGameTick = primary.Prefix.GameTick,
                CredentialMinimumGameTick = ticket.MinimumGameTick,
                TaskStartedAtGameTick = task.StartedAtGameTick,
                OriginalMaximumGameTicks = task.MaximumGameTicks,
                OriginalReturnLimitGameTicks = j.ReturnReserveGameTicks,
                OriginalDeadlineUtc = task.CurrentDeadlineUtc,
                ProposedWallDeadlineUtc = proposedWallDeadline,
                NowUtc = DateTimeOffset.UtcNow,
            };
            if (task.PreservationReceipt is { } receipt && receipt.GameTick > ticket.MinimumGameTick)
            {
                f.PreservedBoundaryGameTick = receipt.GameTick;
                f.PreservedBoundaryProved = receipt.SessionId == ticket.SourceSessionId && receipt.ActionId == task.CurrentActionId
                    && receipt.PrimaryFingerprint == primary.Fingerprint && receipt.GameTick == primary.Prefix.GameTick;
            }
            var decision = VoyagerReturnOnlyContinuationPolicy.Evaluate(f);
            if (!decision.SourceEligible) { failure = decision.FirstFailure; return false; }
            evidence = new(j!, cp!, ticket, provenance, PluginJson.Serialize(j), PluginJson.Serialize(cp),
                source, primary, journal, decision);
            source = primary = null; journal = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
            || ex is System.Security.SecurityException || ex is Newtonsoft.Json.JsonException || ex is InvalidDataException)
        {
            // A file/lease failure supplies no Native observation or false tick.
            e.Require("file_capture_complete", null);
            failure = e.Failure("return_only_continuation_files_unproved");
            failure.Observations["capture_exception_type"] = ex.GetType().Name;
            return false;
        }
        finally { source?.Dispose(); primary?.Dispose(); journal?.Dispose(); }
    }

    internal bool TryRegisterReturnOnlyContinuation(VoyagerReturnOnlyContinuationSource source, string actionId,
        bool explicitlyConfirmed, string digest, DateTimeOffset confirmedAt, out string rejection)
    {
        rejection = "return_only_continuation_context_changed";
        var now = DateTimeOffset.UtcNow;
        if (source.IsDisposed || !explicitlyConfirmed || digest != source.Fingerprint || !Guid.TryParse(actionId, out _)
            || confirmedAt == default || confirmedAt.Offset != TimeSpan.Zero || confirmedAt > source.DeadlineUtc
            || confirmedAt > now || now - confirmedAt > TimeSpan.FromSeconds(2)
            || VoyagerJourney is not { } current || PluginJson.Serialize(current) != source.JourneyJson
            || _currentTicket is null || PluginJson.Serialize(_currentTicket) != source.CheckpointJson) return false;
        var next = PluginJson.Deserialize<VoyagerJourneyRecord>(source.JourneyJson)!;
        if (actionId == next.ReturnOnlyTask!.TaskId || actionId == next.ReturnOnlyTask.OriginalFlightActionId
            || !CurrentContinuationCheckpointMatchesDisk()) return false;
        if (next.ReturnOnlyTask!.Continuations.Count >= 16) return false;
        next.ReturnOnlyTask.Continuations.Add(new()
        {
            ActionId = actionId,
            SourceGameTick = source.Source.Prefix.GameTick,
            SourceFingerprint = source.Source.Fingerprint,
            ContextFingerprint = source.Fingerprint,
            ConfirmedAtUtc = confirmedAt,
            DeadlineUtc = source.DeadlineUtc,
            NewWallAuthority = source.RequiresNewWallAuthority
        });
        return PersistReturnOnlyContinuation(next, out rejection);
    }

    internal bool TryBindReturnOnlyContinuationSession(string actionId, string sessionId, long currentTick, out string rejection)
    {
        rejection = "return_only_continuation_session_unproved";
        var j = VoyagerJourney; var run = j?.ReturnOnlyTask?.CurrentContinuation;
        if (run is null || run.ActionId != actionId || run.State != "pending_native_load" || !Guid.TryParse(sessionId, out _)
            || j!.ReturnOnlyTask!.SessionId == sessionId
            || j.ReturnOnlyTask.Continuations.Any(r => r.SessionId == sessionId)
            || DateTimeOffset.UtcNow >= run.DeadlineUtc || currentTick < run.SourceGameTick
            || (decimal)currentTick - run.SourceGameTick > 3600) return false;
        var next = PluginJson.Deserialize<VoyagerJourneyRecord>(PluginJson.Serialize(j))!;
        next.ReturnOnlyTask!.CurrentContinuation!.SessionId = sessionId;
        next.ReturnOnlyTask.CurrentContinuation.AdoptedAtGameTick = currentTick;
        next.ReturnOnlyTask.CurrentContinuation.State = "adopted";
        return PersistReturnOnlyContinuation(next, out rejection);
    }

    internal bool TryFinishReturnOnlyContinuation(string actionId, bool healthProved, NativeResumeFailureSnapshot? failure,
        long? tick, out string rejection)
    {
        rejection = "return_only_continuation_terminal_unproved";
        var j = VoyagerJourney; var r = j?.ReturnOnlyTask?.CurrentContinuation;
        if (r is null || r.ActionId != actionId || r.State is not ("pending_native_load" or "adopted")) return false;
        if (!healthProved && failure is null) return false;
        if (healthProved && (r.State != "adopted" || failure is not null || !tick.HasValue || tick < r.AdoptedAtGameTick)) return false;
        var next = PluginJson.Deserialize<VoyagerJourneyRecord>(PluginJson.Serialize(j))!;
        var run = next.ReturnOnlyTask!.CurrentContinuation!;
        run.State = healthProved ? "completed_health" : "failed_health";
        run.TerminalAtGameTick = tick; run.FirstFailure = failure?.Copy();
        return PersistReturnOnlyContinuation(next, out rejection);
    }

    internal void RecordReturnOnlyPreloadConsumptionFailure(string actionId, NativeResumeFailureSnapshot failure)
    {
        var j = VoyagerJourney; var run = j?.ReturnOnlyTask?.CurrentContinuation;
        if (run?.ActionId != actionId || run.State != "pending_native_load") return;
        // Native dispatch was not reached; partial durable consumption is
        // unknown. Keep this original attempt closed to rekeyed retries.
        run.State = "outcome_unknown"; run.FirstFailure = failure.Copy();
        PersistReturnOnlyContinuation(j!, out _);
    }

    private bool PersistReturnOnlyContinuation(VoyagerJourneyRecord next, out string rejection)
    {
        rejection = "return_only_continuation_record_size_exceeded";
        if (System.Text.Encoding.UTF8.GetByteCount(PluginJson.Serialize(next)) > 65536) return false;
        return TryPersistVoyagerJourneyCore(next, out rejection, false, false, false, false, true);
    }

    internal bool TryRecordReturnOnlyPreservation(string sessionId, string actionId, long tick, string primaryFingerprint,
        string stablePauseFingerprint, out string rejection)
    {
        rejection = "return_only_preservation_receipt_unproved";
        var j = VoyagerJourney; var task = j?.ReturnOnlyTask;
        if (j?.ReturnOnlyScopeActive != true || j.Phase != "remote_landed" || task!.CurrentSessionId != sessionId
            || task.CurrentActionId != actionId || tick < task.StartedAtGameTick
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(primaryFingerprint)
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(stablePauseFingerprint)) return false;
        var next = PluginJson.Deserialize<VoyagerJourneyRecord>(PluginJson.Serialize(j))!;
        next.ReturnOnlyTask!.PreservationReceipt = new()
        {
            SessionId = sessionId,
            ActionId = actionId,
            GameTick = tick,
            PrimaryFingerprint = primaryFingerprint,
            StablePauseFingerprint = stablePauseFingerprint,
            SavedAtUtc = DateTimeOffset.UtcNow
        };
        return PersistReturnOnlyContinuation(next, out rejection);
    }

    private bool CurrentContinuationCheckpointMatchesDisk()
    {
        try
        {
            using var stream = new FileStream(_ticketPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 65536) return false;
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true);
            var cp = PluginJson.Deserialize<FlightCheckpointTicket>(reader.ReadToEnd());
            return cp is not null && IsStructurallyValid(cp) && PluginJson.Serialize(cp) == PluginJson.Serialize(_currentTicket);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Newtonsoft.Json.JsonException)
        { return false; }
    }

    private static bool ValidReturnOnlyContinuationTransition(VoyagerReturnOnlyTaskRecord? old, VoyagerReturnOnlyTaskRecord? next)
    {
        if (old is null || next is null || old.Continuations is null || next.Continuations is null) return false;
        var a = PluginJson.Deserialize<VoyagerReturnOnlyTaskRecord>(PluginJson.Serialize(old))!;
        var b = PluginJson.Deserialize<VoyagerReturnOnlyTaskRecord>(PluginJson.Serialize(next))!;
        a.Continuations.Clear(); b.Continuations.Clear();
        a.PreservationReceipt = b.PreservationReceipt = null;
        if (PluginJson.Serialize(a) != PluginJson.Serialize(b)) return false; // Original grant is immutable.
        var sameRuns = PluginJson.Serialize(old.Continuations) == PluginJson.Serialize(next.Continuations);
        if (sameRuns && PluginJson.Serialize(old.PreservationReceipt) != PluginJson.Serialize(next.PreservationReceipt))
            return next.PreservationReceipt is { } receipt && receipt.SessionId == next.CurrentSessionId
                && receipt.ActionId == next.CurrentActionId && receipt.GameTick >= (old.PreservationReceipt?.GameTick ?? next.StartedAtGameTick);
        if (PluginJson.Serialize(old.PreservationReceipt) != PluginJson.Serialize(next.PreservationReceipt)) return false;
        var count = old.Continuations.Count;
        if (next.Continuations.Count == count + 1)
            return (count == 0 || old.CurrentContinuation!.State is "completed_health" or "failed_health")
                && next.Continuations.Take(count).Select(PluginJson.Serialize)
                    .SequenceEqual(old.Continuations.Select(PluginJson.Serialize))
                && next.CurrentContinuation!.State == "pending_native_load";
        if (count == 0 || next.Continuations.Count != count
            || !next.Continuations.Take(count - 1).Select(PluginJson.Serialize)
                .SequenceEqual(old.Continuations.Take(count - 1).Select(PluginJson.Serialize))) return false;
        var before = old.CurrentContinuation!; var after = next.CurrentContinuation!;
        if (before.ActionId != after.ActionId || before.SourceGameTick != after.SourceGameTick
            || before.SourceFingerprint != after.SourceFingerprint || before.ContextFingerprint != after.ContextFingerprint
            || before.ConfirmedAtUtc != after.ConfirmedAtUtc || before.DeadlineUtc != after.DeadlineUtc
            || before.NewWallAuthority != after.NewWallAuthority) return false;
        if (before.State == "pending_native_load") return after.State is "adopted" or "failed_health" or "outcome_unknown";
        return before.State == "adopted" && (after.State is "completed_health" or "failed_health" or "outcome_unknown")
            && before.SessionId == after.SessionId && before.AdoptedAtGameTick == after.AdoptedAtGameTick;
    }

    private static bool ValidReturnOnlyContinuations(VoyagerReturnOnlyTaskRecord task)
    {
        if (task.Continuations is null || task.Continuations.Count > 16) return false;
        if (task.PreservationReceipt is { } receipt && (!Guid.TryParse(receipt.SessionId, out _)
            || !Guid.TryParse(receipt.ActionId, out _) || receipt.GameTick < task.StartedAtGameTick || receipt.SavedAtUtc == default || receipt.SavedAtUtc.Offset != TimeSpan.Zero
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(receipt.PrimaryFingerprint)
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(receipt.StablePauseFingerprint))) return false;
        var ids = new HashSet<string>(new[] { task.TaskId, task.OriginalFlightActionId }, StringComparer.Ordinal);
        var floor = task.StartedAtGameTick; var deadline = task.DeadlineUtc;
        var sessions = new HashSet<string>(new[] { task.SessionId }, StringComparer.Ordinal);
        for (var index = 0; index < task.Continuations.Count; index++)
        {
            var r = task.Continuations[index];
            if (r is null || r.SessionId is null || !Guid.TryParse(r.ActionId, out _) || !ids.Add(r.ActionId) || r.SourceGameTick < floor
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.SourceFingerprint)
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.ContextFingerprint)
                || r.ConfirmedAtUtc == default || r.ConfirmedAtUtc.Offset != TimeSpan.Zero || r.DeadlineUtc.Offset != TimeSpan.Zero
                || r.DeadlineUtc <= r.ConfirmedAtUtc || !r.NewWallAuthority && r.DeadlineUtc != deadline || r.NewWallAuthority
                    && r.DeadlineUtc - r.ConfirmedAtUtc > TimeSpan.FromSeconds(VoyagerReturnOnlyContinuationPolicy.MaximumWallSeconds)
                || r.State is not ("pending_native_load" or "adopted" or "completed_health" or "failed_health" or "outcome_unknown")) return false;
            if (r.State == "pending_native_load")
            { if (r.SessionId.Length != 0 || r.AdoptedAtGameTick.HasValue || r.TerminalAtGameTick.HasValue || r.FirstFailure is not null) return false; }
            else if (r.State is "adopted" or "completed_health")
            { if (!Guid.TryParse(r.SessionId, out _) || r.AdoptedAtGameTick < r.SourceGameTick || !r.AdoptedAtGameTick.HasValue) return false; }
            if (r.State == "completed_health" && (r.FirstFailure is not null || !r.TerminalAtGameTick.HasValue
                || r.TerminalAtGameTick < r.AdoptedAtGameTick)) return false;
            if (r.State == "adopted" && (r.FirstFailure is not null || r.TerminalAtGameTick.HasValue)) return false;
            if (r.State == "failed_health" && r.FirstFailure is null) return false;
            if (r.SessionId.Length != 0 && (!Guid.TryParse(r.SessionId, out _) || !sessions.Add(r.SessionId)
                || !r.AdoptedAtGameTick.HasValue || r.AdoptedAtGameTick < r.SourceGameTick)) return false;
            if (r.TerminalAtGameTick is { } terminal && terminal < r.SourceGameTick) return false;
            if (index < task.Continuations.Count - 1 && r.State is not ("completed_health" or "failed_health")) return false;
            floor = r.TerminalAtGameTick ?? r.AdoptedAtGameTick ?? r.SourceGameTick;
            deadline = r.DeadlineUtc;
        }
        return true;
    }
}

internal sealed class VoyagerReturnOnlyContinuationRun
{
    public string ActionId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public long SourceGameTick { get; set; }
    public string SourceFingerprint { get; set; } = "";
    public string ContextFingerprint { get; set; } = "";
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
    public bool NewWallAuthority { get; set; }
    public string State { get; set; } = "pending_native_load";
    public long? AdoptedAtGameTick { get; set; }
    public long? TerminalAtGameTick { get; set; }
    public NativeResumeFailureSnapshot? FirstFailure { get; set; }
}

internal sealed class VoyagerReturnOnlyPreservationReceipt
{
    public string SessionId { get; set; } = "";
    public string ActionId { get; set; } = "";
    public long GameTick { get; set; }
    public string PrimaryFingerprint { get; set; } = "";
    public string StablePauseFingerprint { get; set; } = "";
    public DateTimeOffset SavedAtUtc { get; set; }
}

internal sealed class VoyagerReturnOnlyContinuationSource : IDisposable
{
    internal VoyagerReturnOnlyContinuationSource(VoyagerJourneyRecord journey, FlightCheckpointTicket checkpoint,
        OwnedWorldResumeTicket ticket, string credentialProvenance, string journeyJson, string checkpointJson,
        OwnedSaveRecoveryLease source, OwnedSaveRecoveryLease primary, FileStream journal,
        VoyagerReturnOnlyContinuationDecision decision)
    {
        JourneyJson = journeyJson; CheckpointJson = checkpointJson;
        Ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(PluginJson.Serialize(ticket))!;
        Journey = PluginJson.Deserialize<VoyagerJourneyRecord>(journeyJson)!;
        Checkpoint = PluginJson.Deserialize<FlightCheckpointTicket>(checkpointJson)!;
        CredentialProvenance = credentialProvenance; Source = source; Primary = primary; Journal = journal;
        RemainingGameTicks = decision.RemainingGameTicks!.Value; DeadlineUtc = decision.EffectiveDeadlineUtc!.Value;
        RequiresNewWallAuthority = decision.RequiresNewWallAuthority;
        Fingerprint = CanonicalStateHash.Combine("return-only-continuation-source-v1", journeyJson, checkpointJson,
            credentialProvenance, source.Fingerprint, primary.Fingerprint, source.Prefix.GameTick,
            RemainingGameTicks, DeadlineUtc, RequiresNewWallAuthority);
    }
    internal string JourneyJson { get; }
    internal string CheckpointJson { get; }
    internal VoyagerJourneyRecord Journey { get; }
    internal FlightCheckpointTicket Checkpoint { get; }
    internal OwnedWorldResumeTicket Ticket { get; }
    internal string CredentialProvenance { get; }
    internal string Fingerprint { get; }
    internal long RemainingGameTicks { get; }
    internal DateTimeOffset DeadlineUtc { get; }
    internal bool RequiresNewWallAuthority { get; }
    internal OwnedSaveRecoveryLease Source { get; }
    internal OwnedSaveRecoveryLease Primary { get; }
    internal FileStream Journal { get; }
    internal bool IsDisposed { get; private set; }
    public void Dispose() { if (IsDisposed) return; IsDisposed = true; Source.Dispose(); Primary.Dispose(); Journal.Dispose(); }
}
