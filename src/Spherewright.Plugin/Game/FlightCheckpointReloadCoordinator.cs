using System.ComponentModel;
using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

internal sealed class FlightCheckpointReloadCoordinator
{
    private const string IdempotencyScope = "reload-flight-checkpoint";
    private readonly bool _writesConfigured;
    private readonly GameSessionTracker _sessions;
    private readonly FlightCheckpointStore _tickets;
    private readonly NormalGameActionCoordinator _normalActions;
    private readonly PreparedPlanStore<ReloadPlanPayload> _plans;
    private readonly IdempotencyCache<FlightCheckpointReloadResult> _idempotency;
    private readonly Dictionary<string, ReloadAction> _actions =
        new Dictionary<string, ReloadAction>(StringComparer.Ordinal);

    public FlightCheckpointReloadCoordinator(
        bool writesConfigured,
        int planLifetimeSeconds,
        int idempotencyRetentionMinutes,
        int idempotencyCapacity,
        GameSessionTracker sessions,
        FlightCheckpointStore tickets,
        NormalGameActionCoordinator normalActions)
    {
        _writesConfigured = writesConfigured;
        _sessions = sessions;
        _tickets = tickets;
        _normalActions = normalActions;
        _plans = new PreparedPlanStore<ReloadPlanPayload>(TimeSpan.FromSeconds(planLifetimeSeconds), 4);
        _idempotency = new IdempotencyCache<FlightCheckpointReloadResult>(
            idempotencyCapacity,
            TimeSpan.FromMinutes(idempotencyRetentionMinutes));
    }

    public GameCallResult<PreparedFlightCheckpointReloadPlan> PrepareOnMainThread(
        PrepareFlightCheckpointReloadRequest request)
    {
        if (!_tickets.TryGetActiveTicket(request.ReloadToken, out var ticket, out var ticketRejection)
            || ticket is null)
        {
            return GameCallResult<PreparedFlightCheckpointReloadPlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.SessionNotOwned,
                ticketRejection,
                false,
                "Use only the reusable reload token exposed for the exact pre-flight checkpoint."));
        }

        if (!TryValidateReloadContext(ticket, out var rejection))
        {
            return GameCallResult<PreparedFlightCheckpointReloadPlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState,
                rejection,
                true,
                "Wait for a clear flight failure or an idle main menu, then prepare the same exact checkpoint again."));
        }

        var returnOnlyFailure = ValidateReturnOnlyTask(ticket, request.ReturnOnlyTask, out var originalJourneyJson);
        if (returnOnlyFailure is not null)
            return GameCallResult<PreparedFlightCheckpointReloadPlan>.Failed(ReturnOnlyError(returnOnlyFailure));

        var payload = new ReloadPlanPayload(ticket, request.ReturnOnlyTask, originalJourneyJson);
        PreparedPlan<ReloadPlanPayload> plan;
        try
        {
            plan = _plans.Add(payload.Fingerprint, payload);
        }
        catch (InvalidOperationException)
        {
            return GameCallResult<PreparedFlightCheckpointReloadPlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.ServerBusy,
                "Too many flight-checkpoint reload plans are active.",
                true,
                "Wait for old plans to expire, then prepare the same checkpoint again."));
        }

        var blockers = new List<WriteBlocker>();
        if (!_writesConfigured)
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.WritesDisabled,
                Message = "Flight-checkpoint reload is blocked because Safety.AllowWrites is false.",
            });
        }

        return GameCallResult<PreparedFlightCheckpointReloadPlan>.Succeeded(new PreparedFlightCheckpointReloadPlan
        {
            Prepared = true,
            PlanToken = plan.Token,
            ExpiresAtUtc = plan.ExpiresAtUtc,
            CheckpointId = ticket.CheckpointId,
            OriginPlanetId = ticket.OriginPlanetId,
            DestinationPlanetId = ticket.DestinationPlanetId,
            SavedGameTick = ticket.SavedGameTick,
            CommitAllowedNow = blockers.Count == 0,
            CommitBlockers = blockers,
            CompletionCondition = "DSP loads only the internally generated pre-flight save whose exact name and game tick match the protected reusable ticket; the embedded primary owned-save identity, origin planet, and peaceful mode must match before adoption. Sandbox state and resource multiplier are preserved and reported but do not gate adoption.",
            ReturnOnlyTask = payload.ReturnOnlyTask is null ? null
                : PluginJson.Deserialize<VoyagerReturnOnlyRecoveryRequest>(PluginJson.Serialize(payload.ReturnOnlyTask)),
        });
    }

    public GameCallResult<FlightCheckpointReloadResult> CommitOnMainThread(
        CommitFlightCheckpointReloadRequest request)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
        {
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "A UUID idempotency key is required.",
                false,
                "Generate one UUID and reuse it only for retries of this exact checkpoint reload commit."));
        }

        var fingerprint = "commit-reload-flight-checkpoint|" + request.PlanToken + "|" + request.UserConfirmedInConversation;
        if (_idempotency.TryGet(
            IdempotencyScope,
            request.IdempotencyKey,
            fingerprint,
            out var replay,
            out var conflict))
        {
            return GameCallResult<FlightCheckpointReloadResult>.Succeeded(CloneAsReplay(replay!));
        }

        if (conflict)
        {
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyConflict,
                "The idempotency key is already bound to another checkpoint reload.",
                false,
                "Reuse it only for the original reload or generate a new UUID after preparing a new retry."));
        }

        if (!_writesConfigured)
        {
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.WritesDisabled,
                "Flight-checkpoint reload is blocked because Safety.AllowWrites is false.",
                false,
                "Enable writes and prepare the exact checkpoint again."));
        }

        if (!_idempotency.HasCapacity(IdempotencyScope))
        {
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyCapacityExceeded,
                "The idempotency cache has no capacity for another flight-checkpoint reload.",
                false,
                "Restart the Plugin without loading another save, then use the same protected checkpoint token."));
        }

        if (!_plans.TryTake(request.PlanToken, out var plan, out var expired) || plan is null)
        {
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(
                expired ? BridgeErrorCodes.PlanExpired : BridgeErrorCodes.PlanNotFound,
                expired ? "The flight-checkpoint reload plan expired." : "The flight-checkpoint reload plan was not found or was already consumed.",
                true,
                "Prepare the same exact checkpoint again and commit it once."));
        }

        var payload = plan.Payload;
        if (payload.ReturnOnlyTask is not null && !request.UserConfirmedInConversation)
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(BridgeErrorCodes.InvalidRequest,
                "The new finite return-only task and loss of all unsaved progress require explicit owner confirmation.",
                false, "Do not load or grant time without the owner's separate approval."));
        var rejection = "The reusable flight-checkpoint ticket changed after prepare.";
        if (!_tickets.TryGetActiveTicket(payload.Ticket.ReloadToken, out var current, out _)
            || current is null
            || !string.Equals(new ReloadPlanPayload(current, payload.ReturnOnlyTask,
                payload.ReturnOnlyTask is null ? "" : PluginJson.Serialize(_tickets.VoyagerJourney)).Fingerprint,
                payload.Fingerprint, StringComparison.Ordinal)
            || !TryValidateReloadContext(current, out rejection))
        {
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState,
                rejection,
                true,
                "Do not load another save; inspect the exact checkpoint state and prepare it again."));
        }
        var returnOnlyFailure = ValidateReturnOnlyTask(current, payload.ReturnOnlyTask, out _);
        if (returnOnlyFailure is not null)
            return GameCallResult<FlightCheckpointReloadResult>.Failed(ReturnOnlyError(returnOnlyFailure));

        var action = new ReloadAction
        {
            ActionId = Guid.NewGuid().ToString("D"),
            Ticket = current,
            SourceSessionId = _sessions.CaptureOnMainThread().SessionId,
            SourceGameTick = _sessions.CaptureOnMainThread().GameTick,
            IdempotencyKey = request.IdempotencyKey,
            ReturnOnlyTask = payload.ReturnOnlyTask,
            OriginalJourneyJson = payload.OriginalJourneyJson,
        };
        OwnedSaveRecoveryLease? checkpointLease = null;
        try
        {
            checkpointLease = _tickets.OpenVerifiedCheckpointLease(current);
            _normalActions.NotifyFlightCheckpointReloadStartingOnMainThread(current);
            _sessions.ExpectNextSessionToBeLoadedFromFlightCheckpoint(current, checkpointLease);
            checkpointLease = null; // The session owns the read-only lease through native adoption.
        }
        catch (Exception exception)
        {
            checkpointLease?.Dispose();
            _sessions.CancelExpectedFlightCheckpointSession();
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionFailed,
                $"The exact checkpoint could not be armed before native dispatch ({exception.GetType().Name}); no native load was called.",
                false,
                "Keep the same checkpoint token, inspect local logs, and do not load another save."));
        }

        var result = new FlightCheckpointReloadResult
        {
            ActionId = action.ActionId,
            CheckpointId = current.CheckpointId,
            Accepted = true,
            IdempotentReplay = false,
            State = NormalActionStates.WaitingForGame,
        };
        if (!_idempotency.TryAdd(IdempotencyScope, request.IdempotencyKey, fingerprint, result))
        {
            _sessions.CancelExpectedFlightCheckpointSession();
            return GameCallResult<FlightCheckpointReloadResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyCapacityExceeded,
                "The original checkpoint reload could not be registered before native dispatch; no native load was called.",
                false,
                "Inspect the original request and preserve the checkpoint; no native load was started."));
        }

        _actions[action.ActionId] = action;
        if (!_tickets.TryMarkReloadStarted(current.CheckpointId, action.ActionId, out var pendingRejection))
        {
            action.PreDispatchFailure = pendingRejection;
            result.Accepted = false;
            result.State = NormalActionStates.ActionFailed;
            _sessions.CancelExpectedFlightCheckpointSession();
            return GameCallResult<FlightCheckpointReloadResult>.Succeeded(result);
        }
        action.DispatchEntered = true;
        try
        {
            DSPGame.StartGame(current.CheckpointSaveName);
        }
        catch (Exception exception)
        {
            action.DispatchOutcomeUnknown = true;
            action.UnknownReason = $"The original checkpoint load entered native dispatch and then threw ({exception.GetType().Name}); loading may have started. Reconcile only this action; never rekey or replay.";
            result.State = NormalActionStates.OutcomeUnknown;
            // Keep the exact source lease/expectation until native adoption, a
            // verified rejection, or the bounded wait. The durable pending marker
            // also fences fresh reloads after a process restart.
            _sessions.QuarantineWritesOnMainThread(action.ActionId, action.UnknownReason);
        }
        return GameCallResult<FlightCheckpointReloadResult>.Succeeded(result);
    }

    public bool TryGetActionResultOnMainThread(string actionId, out ActionResultSnapshot? result)
    {
        if (!_actions.TryGetValue(actionId, out var action))
        {
            result = null;
            return false;
        }

        if (action.CompletedSnapshot is not null)
        {
            result = action.CompletedSnapshot;
            return true;
        }

        var session = _sessions.CaptureOnMainThread();
        result = new ActionResultSnapshot
        {
            ActionId = action.ActionId,
            ActionKind = "reload-flight-checkpoint",
            State = action.DispatchOutcomeUnknown ? NormalActionStates.OutcomeUnknown : NormalActionStates.WaitingForGame,
            Terminal = false,
            Succeeded = false,
            FlightCheckpointId = action.Ticket.CheckpointId,
            FlightCheckpointReloadToken = action.Ticket.ReloadToken,
            FlightCheckpointGameTick = action.Ticket.SavedGameTick,
            IdempotencyKey = action.IdempotencyKey,
            Message = action.DispatchOutcomeUnknown ? action.UnknownReason
                : "DSP accepted the exact internally named pre-flight checkpoint; Spherewright is validating its reusable provenance proof.",
        };
        result.NativeFirstFailure = action.FirstFailure?.Copy();
        if (!action.DispatchEntered)
        {
            result.State = NormalActionStates.ActionFailed;
            result.Terminal = true;
            result.Message = action.PreDispatchFailure ?? "No native checkpoint load was dispatched.";
            return true;
        }
        if (session.GameLoaded
            && session.OwnedBySpherewright
            && string.Equals(session.SaveName, action.Ticket.OwnedSaveName, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(session.SessionId)
            && !string.Equals(session.SessionId, action.SourceSessionId, StringComparison.Ordinal)
            && session.CurrentSessionLoadedFromFlightCheckpoint
            && string.Equals(session.FlightCheckpointId, action.Ticket.CheckpointId, StringComparison.Ordinal)
            && session.LocalPlanetId == action.Ticket.OriginPlanetId
            && session.GameTick >= action.Ticket.SavedGameTick)
        {
            if (action.Ticket.ReloadPending
                && !_tickets.TryMarkReloadReconciled(action.Ticket.CheckpointId, action.ActionId, out var reconciliationRejection))
            {
                action.DispatchOutcomeUnknown = true;
                action.UnknownReason ??= reconciliationRejection
                    + " Preserve the original action; do not replay the native load.";
                result.State = NormalActionStates.OutcomeUnknown;
                result.Message = action.UnknownReason;
                _sessions.QuarantineWritesOnMainThread(action.ActionId, action.UnknownReason);
                if (action.ReturnOnlyTask is not null)
                {
                    RetainReturnOnlyMetadataFailure(action, result, session, "checkpoint_reload_reconciled", reconciliationRejection);
                    result.Message += " " + _sessions.PauseFailedReturnOnlyRecoveryOnMainThread(session.SessionId!,
                        action.Ticket.OwnedSaveName, action.Ticket.CheckpointId);
                }
                return true;
            }
            if (!_tickets.TryReconcileVoyagerRecovery(action.Ticket, out var journeyRejection, action.SourceGameTick))
            {
                action.DispatchOutcomeUnknown = true;
                action.UnknownReason ??= journeyRejection;
                result.State = NormalActionStates.OutcomeUnknown;
                result.Message = action.UnknownReason;
                _sessions.QuarantineWritesOnMainThread(action.ActionId, action.UnknownReason);
                if (action.ReturnOnlyTask is not null)
                {
                    RetainReturnOnlyMetadataFailure(action, result, session, "original_journey_reconciled", journeyRejection);
                    result.Message += " " + _sessions.PauseFailedReturnOnlyRecoveryOnMainThread(session.SessionId!,
                        action.Ticket.OwnedSaveName, action.Ticket.CheckpointId);
                }
                return true;
            }
            if (action.ReturnOnlyTask is not null && !_tickets.TryGrantRecoveredReturnOnlyTask(action.Ticket,
                action.ReturnOnlyTask, action.OriginalJourneyJson, action.ActionId, session.SessionId!, session.GameTick,
                DateTimeOffset.UtcNow, out var taskRejection, out var taskFailure))
            {
                action.DispatchOutcomeUnknown = true;
                action.UnknownReason ??= taskRejection;
                action.FirstFailure ??= taskFailure;
                result.NativeFirstFailure = action.FirstFailure?.Copy();
                result.State = NormalActionStates.OutcomeUnknown;
                result.Message = action.UnknownReason;
                _sessions.QuarantineWritesOnMainThread(action.ActionId, action.UnknownReason);
                result.Message += " " + _sessions.PauseFailedReturnOnlyRecoveryOnMainThread(session.SessionId!,
                    action.Ticket.OwnedSaveName, action.Ticket.CheckpointId);
                return true;
            }
            if (action.DispatchOutcomeUnknown && session.WriteQuarantineActionId == action.ActionId)
                _sessions.TryClearQuarantineOnMainThread(action.ActionId, action.UnknownReason!, out _);
            result.State = NormalActionStates.Completed;
            result.Terminal = true;
            result.Succeeded = true;
            result.SessionId = session.SessionId;
            result.PlanetId = session.LocalPlanetId;
            result.ReconciledFromOutcomeUnknown = action.DispatchOutcomeUnknown;
            result.ReconciledAtGameTick = action.DispatchOutcomeUnknown ? session.GameTick : null;
            result.Message = "The exact pre-flight checkpoint passed provenance checks and is ready to retry the same flight; the primary owned save was not replaced.";
            if (action.ReturnOnlyTask is not null)
                result.Message = "The exact interrupted return checkpoint was adopted. Original accounting remains conservatively exhausted and preserved; a separately confirmed finite task permits only return to the original home and normal Save.";
            action.CompletedSnapshot = result;
        }
        else if (!string.IsNullOrWhiteSpace(_sessions.FlightCheckpointAdoptionError))
        {
            // A failed adoption check or bounded wait does not prove that the
            // original native dispatch never loaded. Keep the original action
            // pending rather than turn uncertainty into permission to retry.
            action.DispatchOutcomeUnknown = true;
            action.UnknownReason ??= _sessions.FlightCheckpointAdoptionError
                + " Reconcile only the original checkpoint action; do not rekey or replay.";
            result.State = NormalActionStates.OutcomeUnknown;
            result.Message = action.UnknownReason;
            _sessions.QuarantineWritesOnMainThread(action.ActionId, action.UnknownReason);
        }

        return true;
    }

    private static void RetainReturnOnlyMetadataFailure(ReloadAction action, ActionResultSnapshot result,
        SessionState session, string predicate, string detail)
    {
        var e = new NativeResumeDecisionEvidence("return_only_recovery_persistence", session.GameTick, DateTimeOffset.UtcNow, true);
        e.Number("new_tick_cap", action.ReturnOnlyTask!.MaximumGameTicks);
        e.Number("source_checkpoint_tick", action.Ticket.SavedGameTick);
        e.Require(predicate, null); // A persistence failure does not prove its final disk state.
        e.NotReached("new_task_granted", "native_return", "home_save");
        var cut = e.Failure(BridgeErrorCodes.ActionOutcomeUnknown);
        cut.Observations["persistence_result"] = detail;
        action.FirstFailure ??= cut;
        result.NativeFirstFailure = action.FirstFailure.Copy();
    }

    private Spherewright.Contracts.Players.NativeResumeFailureSnapshot? ValidateReturnOnlyTask(
        FlightCheckpointTicket ticket, VoyagerReturnOnlyRecoveryRequest? request, out string originalJourneyJson)
    {
        originalJourneyJson = "";
        if (request is null) return null;
        var now = DateTimeOffset.UtcNow;
        var invalid = VoyagerReturnOnlyRecoveryPolicy.RequestFailure(request, now);
        if (invalid is not null) return invalid;
        var session = _sessions.CaptureOnMainThread();
        var j = _tickets.VoyagerJourney;
        var e = new NativeResumeDecisionEvidence("return_only_checkpoint_binding", session.GameTick, now, true);
        e.Number("checkpoint_tick", ticket.SavedGameTick);
        e.Number("original_spent_ticks", j?.SpentGameTicks);
        e.Number("original_maximum_ticks", j?.MaximumGameTicks);
        e.Number("new_tick_cap", request.MaximumGameTicks);
        e.Number("new_wall_seconds_remaining", (request.DeadlineUtc - now).TotalSeconds);
        var checks = new (string Name, bool? Value)[] {
            ("idle_main_menu", !session.GameLoaded),
            ("exact_approved_checkpoint", FlightCheckpointStore.CheckpointMatchesReturnTask(ticket, request)),
            ("original_journey_observed", j is not null),
            ("original_return_leg", j?.ReturnLeg),
            ("original_interrupted_phase", j is null ? null : j.Phase == "return" || j.Phase == "recovery_required" || j.Phase == "outcome_unknown"),
            ("no_previous_return_only_grant", j is null ? null : j.ReturnOnlyTask is null),
            ("original_journey_checkpoint", j is null ? null : j.JourneyId == ticket.VoyagerJourneyId && j.ActiveCheckpointId == ticket.CheckpointId && j.ActiveActionId == ticket.LastAttemptActionId),
            ("original_home_direction", j is null ? null : j.HomePlanetId == ticket.DestinationPlanetId && j.HomeStarId == ticket.DestinationStarId && j.RemotePlanetId == ticket.OriginPlanetId && j.DestinationStarId == ticket.OriginStarId),
            ("original_owned_native", j is null ? null : j.OwnedSaveName == ticket.OwnedSaveName && j.GameVersion == ticket.GameVersion),
            ("native_launch_was_armed", ticket.NativeLaunchArmed),
        };
        e.NotReached(checks.Select(c => c.Name).ToArray());
        e.NotReached("native_load", "healthy_takeover", "native_return", "home_save");
        foreach (var check in checks)
            if (!e.Require(check.Name, check.Value)) return e.Failure(BridgeErrorCodes.StaleState);
        originalJourneyJson = PluginJson.Serialize(j);
        return null;
    }

    private static BridgeError ReturnOnlyError(Spherewright.Contracts.Players.NativeResumeFailureSnapshot failure)
    {
        var error = BridgeError.Create(failure.Code, "The fresh return-only recovery premise failed: " + failure.FirstFailedPredicate,
            false, "Preserve the original checkpoint, action and ledger. Do not load or renew the old task.");
        error.NativeFirstFailure = failure;
        return error;
    }

    private bool TryValidateReloadContext(FlightCheckpointTicket ticket, out string rejection)
    {
        if (!_tickets.TryValidateCheckpointFile(ticket, out rejection))
        {
            return false;
        }

        var session = _sessions.CaptureOnMainThread();
        if (session.GameLoaded)
        {
            if (!session.OwnedBySpherewright
                || !string.Equals(session.SaveName, ticket.OwnedSaveName, StringComparison.Ordinal))
            {
                rejection = "An active game may be replaced only by a checkpoint bound to that exact owned save.";
                return false;
            }

            if (!FlightCheckpointStore.IsRecoveryRequired(ticket))
            {
                rejection = "The bound flight has not reached a persisted recovery-required state.";
                return false;
            }

            if (!session.CurrentSessionLoadedFromFlightCheckpoint
                && !_normalActions.HasRecoveryRequiredFlightOnMainThread(ticket.CheckpointId))
            {
                rejection = "The current process has no terminal failed-flight evidence for this checkpoint.";
                return false;
            }
        }
        else
        {
            var readiness = TestWorldCoordinator.ValidateMainMenuReady();
            if (readiness is not null)
            {
                rejection = readiness.Message;
                return false;
            }

            if (!FlightCheckpointStore.IsRecoveryRequired(ticket)
                && !(FlightCheckpointStore.IsAttemptInFlight(ticket)
                     && !IsLiveDspProcess(ticket.SourceProcessId)))
            {
                rejection = "The checkpoint is neither recovery-required nor an interrupted in-flight attempt from a terminated DSP process.";
                return false;
            }
        }

        return _normalActions.CanReloadFlightCheckpointOnMainThread(ticket, out rejection);
    }

    private static bool IsLiveDspProcess(int processId)
    {
        try
        {
            using (var process = Process.GetProcessById(processId))
            {
                return !process.HasExited
                    && string.Equals(process.ProcessName, "DSPGAME", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException
            || exception is Win32Exception)
        {
            return false;
        }
    }

    private static FlightCheckpointReloadResult CloneAsReplay(FlightCheckpointReloadResult result) =>
        new FlightCheckpointReloadResult
        {
            ActionId = result.ActionId,
            CheckpointId = result.CheckpointId,
            Accepted = result.Accepted,
            IdempotentReplay = true,
            State = result.State,
        };

    private sealed class ReloadPlanPayload
    {
        public ReloadPlanPayload(FlightCheckpointTicket ticket, VoyagerReturnOnlyRecoveryRequest? returnOnlyTask = null,
            string originalJourneyJson = "")
        {
            Ticket = ticket;
            ReturnOnlyTask = returnOnlyTask is null ? null
                : PluginJson.Deserialize<VoyagerReturnOnlyRecoveryRequest>(PluginJson.Serialize(returnOnlyTask));
            OriginalJourneyJson = originalJourneyJson;
            Fingerprint = CanonicalStateHash.Combine(
                "reload-flight-checkpoint",
                ticket.CheckpointId,
                ticket.ReloadToken,
                ticket.CheckpointSaveName,
                ticket.OwnedSaveName,
                ticket.SourceSessionId,
                ticket.SourceRevision,
                ticket.GameVersion,
                ticket.OriginPlanetId,
                ticket.DestinationPlanetId,
                ticket.OriginStarId,
                ticket.DestinationStarId,
                ticket.VoyagerJourneyId,
                ticket.NativeLaunchArmed,
                ticket.SavedGameTick,
                ticket.CheckpointFileFingerprint,
                ticket.LifecycleState,
                ticket.LastAttemptActionId,
                ticket.RecoveryRequiredAtGameTick,
                ticket.ReloadActionId,
                ticket.PlayerStateHash,
                ticket.StarSystemStateHash,
                ticket.IssuedAtUtc, PluginJson.Serialize(ReturnOnlyTask), OriginalJourneyJson);
        }

        public FlightCheckpointTicket Ticket { get; }

        public string Fingerprint { get; }
        public VoyagerReturnOnlyRecoveryRequest? ReturnOnlyTask { get; }
        public string OriginalJourneyJson { get; }
    }

    private sealed class ReloadAction
    {
        public string ActionId { get; set; } = string.Empty;

        public FlightCheckpointTicket Ticket { get; set; } = null!;

        public string? SourceSessionId { get; set; }
        public long? SourceGameTick { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public bool DispatchEntered { get; set; }
        public bool DispatchOutcomeUnknown { get; set; }
        public string? UnknownReason { get; set; }
        public string? PreDispatchFailure { get; set; }
        public ActionResultSnapshot? CompletedSnapshot { get; set; }
        public VoyagerReturnOnlyRecoveryRequest? ReturnOnlyTask { get; set; }
        public string OriginalJourneyJson { get; set; } = "";
        public Spherewright.Contracts.Players.NativeResumeFailureSnapshot? FirstFailure { get; set; }
    }
}
