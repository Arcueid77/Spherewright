using System.ComponentModel;
using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed class OwnedWorldResumeCoordinator
{
    private const string IdempotencyScope = "resume-owned-world";
    private readonly bool _writesConfigured;
    private readonly GameSessionTracker _sessions;
    private readonly OwnedWorldResumeTicketStore _tickets;
    private readonly PreparedPlanStore<OwnedWorldResumePlanPayload> _plans;
    private readonly IdempotencyCache<OwnedWorldResumeResult> _idempotency;
    private readonly Dictionary<string, OwnedWorldResumeAction> _actions =
        new Dictionary<string, OwnedWorldResumeAction>(StringComparer.Ordinal);

    public OwnedWorldResumeCoordinator(
        bool writesConfigured,
        int planLifetimeSeconds,
        int idempotencyRetentionMinutes,
        int idempotencyCapacity,
        GameSessionTracker sessions,
        OwnedWorldResumeTicketStore tickets)
    {
        _writesConfigured = writesConfigured;
        _sessions = sessions;
        _tickets = tickets;
        _plans = new PreparedPlanStore<OwnedWorldResumePlanPayload>(
            TimeSpan.FromSeconds(planLifetimeSeconds),
            4);
        _idempotency = new IdempotencyCache<OwnedWorldResumeResult>(
            idempotencyCapacity,
            TimeSpan.FromMinutes(idempotencyRetentionMinutes));
    }

    public GameCallResult<PreparedOwnedWorldResumePlan> PrepareOnMainThread(PrepareOwnedWorldResumeRequest request)
    {
        var requestError = OwnedWorldRecoveryPolicy.ValidateRequest(request);
        if (requestError is not null)
            return GameCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, requestError, false,
                "Use a supported explicit recovery mode. Reauthorization requires a fresh disclosure before confirmation; arbitrary save selection is unavailable."));
        var readinessError = TestWorldCoordinator.ValidateMainMenuReady();
        if (readinessError is not null)
        {
            return GameCallResult<PreparedOwnedWorldResumePlan>.Failed(readinessError);
        }

        if (!TryGetTicket(request, request.ResumeToken, out var ticket, out var provenance, out var ticketRejection)
            || ticket is null)
        {
            return GameCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.SessionNotOwned,
                ticketRejection,
                false,
                "Use only the one-time restartResumeToken issued for the exact quarantined Spherewright-owned session."));
        }

        if (!TryResolvePayload(ticket, request, provenance, out var payload, out var lease, out var primaryLease, out var rejection))
        {
            return GameCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState,
                rejection,
                false,
                "Keep the ticket and inspect the exact normal shutdown or latest healthy owned-save evidence."));
        }

        lease?.Dispose(); // Prepare never keeps a handle or starts loading.
        primaryLease?.Dispose();
        PreparedPlan<OwnedWorldResumePlanPayload> plan;
        try
        {
            plan = _plans.Add(payload!.Fingerprint, payload);
        }
        catch (InvalidOperationException)
        {
            return GameCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.ServerBusy,
                "Too many owned-world resume plans are active.",
                true,
                "Wait for old plans to expire, then prepare the one-time resume again."));
        }

        var blockers = new List<WriteBlocker>();
        if (payload!.Reauthorizing)
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.UserConfirmationRequired,
                Message = payload.ActiveFixedAutosaveRecovery
                    ? "Verify this exact disclosed owned candidate is covered by explicit conversation authority; an existing matching grant needs no repeated question."
                    : "Display this plan's disclosure and wait for subsequent explicit conversation confirmation before commit.",
            });
        if (!_writesConfigured)
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.WritesDisabled,
                Message = "Owned-world resume is blocked because Safety.AllowWrites is false.",
            });
        }

        return GameCallResult<PreparedOwnedWorldResumePlan>.Succeeded(new PreparedOwnedWorldResumePlan
        {
            Prepared = true,
            PlanToken = plan.Token,
            ExpiresAtUtc = plan.ExpiresAtUtc,
            ExpectedPlanetId = ticket.ExpectedPlanetId,
            MinimumGameTick = payload!.MinimumGameTick,
            RecoveryMode = payload.Request.RecoveryMode,
            RecoveryEvidenceVersion = payload.VersionChangedPrimary ? OwnedWorldPrimaryMigrationPolicy.EvidenceVersionFor(ticket.GameVersion, _tickets.CurrentGameVersion)
                : payload.FixedAutosaveRecovery ? OwnedWorldAutosaveRecoveryPolicy.EvidenceVersionFor(payload.Request.RecoveryMode)
                : payload.Reauthorizing ? OwnedWorldReauthorizationPolicy.EvidenceVersion
                : payload.VerifiedRecovery ? OwnedWorldRecoveryPolicy.EvidenceVersion : 0,
            CandidateGameTick = payload.VerifiedRecovery || payload.Reauthorizing ? payload.MinimumGameTick : (long?)null,
            ExactEmbeddedIdentityVerified = payload.VerifiedRecovery || payload.Reauthorizing,
            UserConfirmationRequired = payload.Reauthorizing,
            SourceGameVersion = ticket.GameVersion,
            TargetGameVersion = _tickets.CurrentGameVersion,
            ConfirmationPrompt = payload.VersionChangedPrimary ? OwnedWorldPrimaryMigrationPolicy.ConfirmationPromptFor(ticket.GameVersion, _tickets.CurrentGameVersion)
                : payload.FixedAutosaveRecovery ? OwnedWorldAutosaveRecoveryPolicy.ConfirmationPromptFor(payload.Request.RecoveryMode)
                : payload.Reauthorizing ? OwnedWorldReauthorizationPolicy.ConfirmationPrompt : string.Empty,
            ConfirmationDigest = payload.Reauthorizing ? payload.Fingerprint : string.Empty,
            CommitAllowedNow = blockers.Count == 0,
            CommitBlockers = blockers,
            CompletionCondition = payload.VersionChangedPrimary
                ? $"Only the exact active-credential owned primary at the recorded tick may migrate from {ticket.GameVersion} to {_tickets.CurrentGameVersion} after subsequent explicit confirmation of this disclosure. Unchanged ticket replicas, full primary evidence and original durable Journal are rechecked and held read-only. Provenance is durably consumed before loading; the exact transition preserves Journal origin/history before normal primary resave and a target-version credential. No expiry edits, alternate source, fallback or replay."
                : payload.FixedAutosaveRecovery
                ? "Only the fixed native AutoSave0 at this exact verified tick may load under explicit conversation authority for this candidate. Identity, known progress floor, current version, file evidence and original Journal are rechecked; protected provenance is durably consumed before loading without editing expiry. Adoption and Journal continuity precede normal save to the original owned primary and a fresh credential. No fallback, slot selection or replay after interruption."
                : payload.Reauthorizing
                ? "Only the exact primary and original Journal at this checkpoint may continue after subsequent explicit consent. Commit revalidates all evidence, consumes expired provenance before loading, and normal save alone issues a fresh credential. No fallback or import."
                : payload.VerifiedRecovery
                ? "Only the explicitly approved fixed LastExit may load: exact embedded identity, current version, peaceful mode, durable Journal, full-file evidence and the candidate tick are bound and rechecked; no fallback. Adoption must cover the candidate tick before normal primary resave."
                : string.IsNullOrWhiteSpace(ticket.QuarantineActionId)
                ? "A healthy planned restart loads only the exact primary owned save named inside the protected ticket after its header proves the minimum game tick and its protected per-save journal proves the ticket's durable checkpoint; adoption still requires the embedded high-entropy owned name, planet, and peaceful mode. Sandbox state and resource multiplier are preserved and reported but do not gate adoption."
                : "Quarantine recovery loads only the fresh fixed LastExit slot after its header proves the minimum game tick and its protected per-save journal proves the ticket's durable checkpoint; adoption still requires the embedded high-entropy owned name, planet, and peaceful mode. Sandbox state and resource multiplier are preserved and reported but do not gate adoption.",
        });
    }

    public GameCallResult<OwnedWorldResumeResult> CommitOnMainThread(CommitOwnedWorldResumeRequest request)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
        {
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "A UUID idempotency key is required.",
                false,
                "Generate one UUID and reuse it for retries of this exact resume commit."));
        }

        var fingerprint = CanonicalStateHash.Combine("commit-resume-owned-world", request.PlanToken,
            request.UserConfirmedInConversation, request.ConfirmationDigest);
        if (_idempotency.TryGet(
            IdempotencyScope,
            request.IdempotencyKey,
            fingerprint,
            out var replay,
            out var conflict))
        {
            return GameCallResult<OwnedWorldResumeResult>.Succeeded(CloneAsReplay(replay!));
        }

        if (conflict)
        {
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyConflict,
                "The idempotency key is already bound to a different owned-world resume request.",
                false,
                "Reuse it only for the original resume commit."));
        }

        if (!_writesConfigured)
        {
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.WritesDisabled,
                "Owned-world resume is blocked because Safety.AllowWrites is false.",
                false,
                "Enable writes, restart DSP at the main menu, and prepare the one-time resume again."));
        }

        if (!_idempotency.HasCapacity(IdempotencyScope))
        {
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyCapacityExceeded,
                "The idempotency cache has no capacity for another owned-world resume action.",
                false,
                "Restart the Plugin at the main menu before preparing the one-time resume again; no LastExit load was started."));
        }

        if (!_plans.TryTake(request.PlanToken, out var plan, out var expired) || plan is null)
        {
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                expired ? BridgeErrorCodes.PlanExpired : BridgeErrorCodes.PlanNotFound,
                expired ? "The owned-world resume plan expired." : "The owned-world resume plan was not found or was already consumed.",
                true,
                "Prepare the exact one-time resume again and commit it once."));
        }

        var readinessError = TestWorldCoordinator.ValidateMainMenuReady();
        var payload = plan.Payload;
        if (payload.Reauthorizing
            ? !OwnedWorldReauthorizationPolicy.MatchesConfirmation(request.UserConfirmedInConversation,
                payload.Fingerprint, request.ConfirmationDigest)
            : request.UserConfirmedInConversation || !string.IsNullOrEmpty(request.ConfirmationDigest))
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.UserConfirmationRequired,
                "Protected recovery requires explicit conversation authority and the exact prepared disclosure digest; other resume modes do not accept this flag.",
                false, "Prepare a fresh disclosure. Use only an explicit grant matching this candidate, never a generic development request."));
        var rejection = "The one-time resume ticket changed after prepare.";
        OwnedSaveRecoveryLease? sourceLease = null;
        OwnedSaveRecoveryLease? primaryLease = null;
        if (readinessError is not null
            || !TryGetTicket(payload.Request, payload.Ticket.ResumeToken, out var currentTicket, out var provenance, out _)
            || currentTicket is null
            || !TryResolvePayload(currentTicket, payload.Request, provenance, out var currentPayload, out sourceLease, out primaryLease, out rejection)
            || !string.Equals(currentPayload!.Fingerprint, payload.Fingerprint, StringComparison.Ordinal))
        {
            sourceLease?.Dispose();
            primaryLease?.Dispose();
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState,
                readinessError?.Message ?? (string.IsNullOrWhiteSpace(rejection)
                    ? "The protected source evidence changed after prepare; no load was started." : rejection),
                false,
                "Do not load a save; return to an idle main menu and prepare from the exact current ticket."));
        }

        var action = new OwnedWorldResumeAction
        {
            Reauthorizing = payload.Reauthorizing,
            ActionId = Guid.NewGuid().ToString("D"),
            Ticket = currentTicket,
            ResumeSource = payload.ResumeSource,
            MinimumGameTick = payload.MinimumGameTick,
        };
        FileStream? journalLease = null;
        try
        {
            if (payload.Reauthorizing)
                journalLease = _tickets.OpenReauthorizationJournalLease(currentTicket, payload.Provenance,
                    payload.ActiveCredentialReauthorization, payload.VersionChangedPrimary);
            if (payload.Reauthorizing && !_tickets.TryConsumeReauthorization(currentTicket.ResumeToken,
                payload.Provenance, action.ActionId, payload.Fingerprint,
                payload.ActiveCredentialReauthorization, payload.VersionChangedPrimary))
                return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                    BridgeErrorCodes.StaleState,
                    "The exact provenance changed or durable consumption failed; no load was started.",
                    false, "Keep the current main menu and investigate; do not replay with another save."));
            _sessions.ExpectNextSessionToBeResumed(currentTicket, sourceLease, payload.Reauthorizing, journalLease,
                payload.FixedAutosaveRecovery, primaryLease);
            sourceLease = null; // Tracker owns the lease through adoption/Journal confirmation.
            journalLease = null;
            primaryLease = null;
            // The source is an internal enum bound by the prepared fingerprint,
            // never a caller-supplied path/name or an automatic fallback.
            DSPGame.StartGame(ResolveNativeResumeSource(payload.ResumeSource, currentTicket.OwnedSaveName));
        }
        catch (Exception exception)
        {
            _sessions.CancelExpectedResumedSession();
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionFailed,
                $"DSP rejected the exact protected owned-world resume through its normal loader ({exception.GetType().Name}).",
                false,
                "Inspect the local Spherewright and Unity logs; do not load another save."));
        }
        finally
        {
            sourceLease?.Dispose();
            journalLease?.Dispose();
            primaryLease?.Dispose();
        }

        var result = new OwnedWorldResumeResult
        {
            ActionId = action.ActionId,
            Accepted = true,
            IdempotentReplay = false,
            State = NormalActionStates.WaitingForGame,
        };
        if (!_idempotency.TryAdd(IdempotencyScope, request.IdempotencyKey, fingerprint, result))
        {
            return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyCapacityExceeded,
                "The idempotency cache reached capacity after DSP accepted the resume.",
                false,
                "Do not retry with a new key; poll session state and the returned action ID."));
        }

        _actions[action.ActionId] = action;
        return GameCallResult<OwnedWorldResumeResult>.Succeeded(result);
    }

    public bool TryGetActionResultOnMainThread(string actionId, out ActionResultSnapshot? result)
    {
        if (!_actions.TryGetValue(actionId, out var action))
        {
            result = null;
            return false;
        }

        var session = _sessions.CaptureOnMainThread();
        result = new ActionResultSnapshot
        {
            ActionId = action.ActionId,
            ActionKind = "resume-owned-game",
            State = NormalActionStates.WaitingForGame,
            Terminal = false,
            Succeeded = false,
            Message = action.ResumeSource == OwnedWorldResumeSourceKind.FixedAutoSave0
                ? "DSP accepted only the fixed AutoSave0 candidate; Spherewright is validating its owned provenance and original Journal."
                : action.ResumeSource == OwnedWorldResumeSourceKind.LastExit
                ? "DSP accepted the fixed LastExit load; Spherewright is validating the one-time owned-world provenance proof."
                : "DSP accepted the exact ticket-bound primary owned save; Spherewright is validating the one-time provenance proof.",
        };
        if (session.OwnedBySpherewright
            && session.LocalPlanetId == action.Ticket.ExpectedPlanetId
            && session.GameTick >= action.MinimumGameTick)
        {
            result.SessionId = session.SessionId;
            result.PlanetId = session.LocalPlanetId;
            if (string.Equals(session.OwnedSaveState, OwnedSaveStates.Saved, StringComparison.Ordinal)
                && session.LastOwnedSaveGameTick >= action.MinimumGameTick)
            {
                if (action.Reauthorizing
                    && (string.IsNullOrWhiteSpace(_tickets.CurrentResumeToken)
                        || !_tickets.TryGetActiveTicket(_tickets.CurrentResumeToken!, out var renewed, out _)
                        || renewed is null || renewed.ResumeToken == action.Ticket.ResumeToken
                        || renewed.OwnedSaveName != action.Ticket.OwnedSaveName
                        || renewed.SourceSessionId != session.SessionId
                        || renewed.MinimumGameTick != session.LastOwnedSaveGameTick
                        || renewed.GameVersion != _tickets.CurrentGameVersion))
                {
                    result.State = NormalActionStates.ActionFailed;
                    result.Terminal = true;
                    result.Message = "The owned world was normally saved, but no matching fresh restart credential was proven. Reconcile this saved state; never replay the consumed resume.";
                    return true;
                }
                result.State = NormalActionStates.Completed;
                result.Terminal = true;
                result.Succeeded = true;
                result.Message = action.ResumeSource == OwnedWorldResumeSourceKind.FixedAutoSave0
                    ? "The exact fixed AutoSave0 passed owned provenance and Journal checks and was normally saved under the original owned primary identity. Reconcile prior successful construction before new writes."
                    : action.ResumeSource == OwnedWorldResumeSourceKind.LastExit
                    ? "The exact owned LastExit payload passed provenance checks and was resaved under its high-entropy Spherewright name."
                    : "The exact ticket-bound primary owned save passed provenance checks and was resaved under the same high-entropy Spherewright name.";
            }
            else if (string.Equals(session.OwnedSaveState, OwnedSaveStates.SaveFailed, StringComparison.Ordinal))
            {
                result.State = NormalActionStates.ActionFailed;
                result.Terminal = true;
                result.Message = "The protected world was adopted but its normal primary resave failed; inspect the current world without replaying resume.";
            }
            else
            {
                result.Message = "The exact owned payload was adopted and is waiting for its high-entropy normal save to complete.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(_sessions.ResumeAdoptionError))
        {
            result.State = NormalActionStates.ActionFailed;
            result.Terminal = true;
            result.Message = _sessions.ResumeAdoptionError;
        }

        return true;
    }

    private bool TryGetTicket(PrepareOwnedWorldResumeRequest request, string token,
        out OwnedWorldResumeTicket? ticket, out string provenance, out string rejection)
    {
        provenance = string.Empty;
        if (request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary)
            return _tickets.TryGetVersionChangedPrimaryProvenance(token, out ticket, out provenance, out rejection);
        if (request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeFixedAutosave0)
            return _tickets.TryGetFixedAutosaveProvenance(token, out ticket, out provenance, out rejection);
        return request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeExpiredPrimary
            || request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeExpiredAutosave0
            ? _tickets.TryGetExpiredPrimaryProvenance(token, out ticket, out provenance, out rejection)
            : _tickets.TryGetActiveTicket(token, out ticket, out rejection);
    }

    private bool TryResolvePayload(OwnedWorldResumeTicket ticket, PrepareOwnedWorldResumeRequest request, string provenance,
        out OwnedWorldResumePlanPayload? payload, out OwnedSaveRecoveryLease? lease,
        out OwnedSaveRecoveryLease? primaryLease, out string rejection)
    {
        payload = null;
        lease = null;
        primaryLease = null;
        rejection = string.Empty;
        if (request.RecoveryMode == OwnedWorldResumeModes.Default)
        {
            if (!TryResolveResumeSource(ticket, out var source, out rejection)) return false;
            payload = new OwnedWorldResumePlanPayload(ticket, source, request, null, provenance, _sessions.Revision);
            return true;
        }
        if (IsLiveDspProcess(ticket.SourceProcessId))
        {
            rejection = "The source DSP process is still running; no recovery load is permitted.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(ticket.QuarantineActionId) || ticket.GameplayJournalCheckpoint is null)
        {
            rejection = "Verified recovery requires a healthy ticket with a verified durable Journal checkpoint.";
            return false;
        }
        try
        {
            // No caller-supplied path or autosave enumeration. Ticket/Journal already validated by the store.
            if (OwnedWorldAutosaveRecoveryPolicy.IsFixedAutosaveMode(request.RecoveryMode))
            {
                // This mode does not combine autosave recovery with version migration.
                if (!string.Equals(ticket.GameVersion, _tickets.CurrentGameVersion, StringComparison.Ordinal))
                {
                    rejection = "Fixed AutoSave0 recovery requires the recorded and current game versions to match.";
                    return false;
                }
                lease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.AutoSave0), ticket.OwnedSaveName);
                // The eventual overwrite target must itself be the exact original
                // owned primary, not merely a filename or an unreadable header.
                primaryLease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.OwnedSaveName), ticket.OwnedSaveName);
                if (primaryLease.Prefix.GameVersion != ticket.GameVersion || !primaryLease.Prefix.Peaceful)
                    throw new InvalidDataException("The protected primary changed native version or peaceful state.");
                rejection = OwnedWorldAutosaveRecoveryPolicy.ValidateCandidate(request, ticket.MinimumGameTick,
                    ticket.IssuedAtUtc, ticket.GameVersion, _tickets.CurrentGameVersion,
                    lease.Prefix, lease.WrittenAtUtc, primaryLease.Prefix.GameTick) ?? string.Empty;
                if (rejection.Length == 0)
                {
                    payload = new OwnedWorldResumePlanPayload(ticket, OwnedWorldResumeSourceKind.FixedAutoSave0,
                        request, lease, provenance, _sessions.Revision, primaryLease.Fingerprint);
                    return true;
                }
                lease.Dispose();
                lease = null;
                primaryLease.Dispose();
                primaryLease = null;
                return false;
            }
            if (request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeExpiredPrimary
                || request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary)
            {
                lease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.OwnedSaveName), ticket.OwnedSaveName);
                rejection = (request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary
                    ? OwnedWorldPrimaryMigrationPolicy.ValidateCandidate(ticket.MinimumGameTick,
                        ticket.GameVersion, _tickets.CurrentGameVersion, lease.Prefix)
                    : OwnedWorldReauthorizationPolicy.ValidateCandidate(ticket.MinimumGameTick, ticket.GameVersion, lease.Prefix))
                    ?? string.Empty;
                if (rejection.Length == 0)
                {
                    payload = new OwnedWorldResumePlanPayload(ticket, OwnedWorldResumeSourceKind.OwnedPrimary,
                        request, lease, provenance, _sessions.Revision);
                    return true;
                }
                lease.Dispose();
                lease = null;
                return false;
            }
            lease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), ticket.OwnedSaveName);
            TryReadSaveEvidence(ticket.OwnedSaveName, out _, out var primaryTick);
            rejection = OwnedWorldRecoveryPolicy.ValidateCandidate(request,
                string.IsNullOrWhiteSpace(ticket.QuarantineActionId), ticket.GameplayJournalCheckpoint is not null,
                ticket.MinimumGameTick, ticket.IssuedAtUtc, ticket.GameVersion,
                lease.Prefix, lease.WrittenAtUtc, primaryTick) ?? string.Empty;
            if (rejection.Length == 0)
            {
                payload = new OwnedWorldResumePlanPayload(ticket, OwnedWorldResumeSourceKind.LastExit, request, lease, provenance, _sessions.Revision);
                return true;
            }
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException
            || exception is UnauthorizedAccessException || exception is ArgumentException
            || exception is NotSupportedException || exception is System.Security.SecurityException
            || exception is System.Security.Cryptography.CryptographicException)
        {
            rejection = $"The exact recovery source could not prove bounded protected evidence ({exception.GetType().Name}).";
        }
        lease?.Dispose();
        lease = null;
        primaryLease?.Dispose();
        primaryLease = null;
        return false;
    }

    private static bool TryResolveResumeSource(
        OwnedWorldResumeTicket ticket,
        out OwnedWorldResumeSourceKind resumeSource,
        out string rejection)
    {
        resumeSource = OwnedWorldResumeSourceKind.None;
        rejection = string.Empty;
        if (IsLiveDspProcess(ticket.SourceProcessId))
        {
            rejection = "The source DSP process is still running; no restart source can yet prove a completed shutdown.";
            return false;
        }

        TryReadSaveEvidence(GameSave.LastExit, out var lastExitWrittenAt, out var lastExitGameTick);
        TryReadSaveEvidence(ticket.OwnedSaveName, out var ownedPrimaryWrittenAt, out var ownedPrimaryGameTick);
        resumeSource = OwnedWorldResumeSourceSelector.Select(
            !string.IsNullOrWhiteSpace(ticket.QuarantineActionId),
            ticket.MinimumGameTick,
            ticket.IssuedAtUtc,
            lastExitWrittenAt,
            lastExitGameTick,
            ownedPrimaryWrittenAt,
            ownedPrimaryGameTick,
            TimeSpan.FromSeconds(2));
        if (resumeSource == OwnedWorldResumeSourceKind.None)
        {
            rejection = string.IsNullOrWhiteSpace(ticket.QuarantineActionId)
                ? "The exact ticket-bound primary owned save is not fresh enough or its header is older than the planned-restart minimum game tick."
                : "DSP's fixed LastExit slot is not fresh enough or its header is older than the quarantine-recovery minimum game tick.";
            return false;
        }

        return true;
    }

    private static void TryReadSaveEvidence(
        string saveName,
        out DateTimeOffset? writtenAtUtc,
        out long? gameTick)
    {
        writtenAtUtc = null;
        gameTick = null;
        try
        {
            var path = GameSave.SavePath(saveName);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            GameSave.ReadHeader(saveName, false, out var header);
            if (header is null || header.gameTick < 0)
            {
                return;
            }

            writtenAtUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            gameTick = header.gameTick;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is NotSupportedException)
        {
            writtenAtUtc = null;
            gameTick = null;
        }
    }

    private static string ResolveNativeResumeSource(OwnedWorldResumeSourceKind source, string ownedSaveName)
    {
        switch (source)
        {
            case OwnedWorldResumeSourceKind.OwnedPrimary: return ownedSaveName;
            case OwnedWorldResumeSourceKind.LastExit: return GameSave.LastExit;
            case OwnedWorldResumeSourceKind.FixedAutoSave0: return GameSave.AutoSave0;
            default: throw new InvalidOperationException("No supported exact recovery source was prepared.");
        }
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

    private static OwnedWorldResumeResult CloneAsReplay(OwnedWorldResumeResult result) =>
        new OwnedWorldResumeResult
        {
            ActionId = result.ActionId,
            Accepted = result.Accepted,
            IdempotentReplay = true,
            State = result.State,
        };

    private sealed class OwnedWorldResumePlanPayload
    {
        public OwnedWorldResumePlanPayload(
            OwnedWorldResumeTicket ticket,
            OwnedWorldResumeSourceKind resumeSource,
            PrepareOwnedWorldResumeRequest request,
            OwnedSaveRecoveryLease? lease,
            string provenance,
            long revision,
            string primaryFingerprint = "")
        {
            Ticket = ticket;
            Provenance = provenance;
            ResumeSource = resumeSource;
            Request = new PrepareOwnedWorldResumeRequest
            {
                RecoveryMode = request.RecoveryMode,
                UserConfirmedInConversation = request.UserConfirmedInConversation,
                MinimumRecoveryGameTick = request.MinimumRecoveryGameTick,
                ExpectedRecoveryGameTick = request.ExpectedRecoveryGameTick,
            };
            MinimumGameTick = lease?.Prefix.GameTick ?? ticket.MinimumGameTick;
            Fingerprint = CanonicalStateHash.Combine(
                "resume-owned-world",
                provenance,
                revision,
                ticket.ResumeToken,
                ticket.OwnedSaveName,
                ticket.SourceSessionId,
                ticket.SourceProcessId,
                ticket.SourceBridgeInstanceId,
                ticket.GameVersion,
                ticket.ExpectedPlanetId,
                ticket.MinimumGameTick,
                ticket.GameplayJournalCheckpoint?.Version ?? 0,
                ticket.GameplayJournalCheckpoint?.JournalId ?? string.Empty,
                ticket.GameplayJournalCheckpoint?.TrackingMode ?? string.Empty,
                ticket.GameplayJournalCheckpoint?.HistoricalCoverageComplete ?? false,
                ticket.GameplayJournalCheckpoint?.TrackingStartedAtGameTick ?? 0L,
                ticket.GameplayJournalCheckpoint?.MinimumDurableThroughSequence ?? 0L,
                ticket.QuarantineActionId,
                ticket.IssuedAtUtc,
                ticket.ExpiresAtUtc,
                resumeSource,
                Request.RecoveryMode,
                Request.UserConfirmedInConversation,
                Request.MinimumRecoveryGameTick,
                Request.ExpectedRecoveryGameTick,
                lease?.Fingerprint ?? string.Empty,
                primaryFingerprint);
        }

        public OwnedWorldResumeTicket Ticket { get; }

        public OwnedWorldResumeSourceKind ResumeSource { get; }

        public PrepareOwnedWorldResumeRequest Request { get; }

        public bool VerifiedRecovery => Request.RecoveryMode == OwnedWorldResumeModes.VerifiedNewerLastExit;

        public bool FixedAutosaveRecovery => OwnedWorldAutosaveRecoveryPolicy.IsFixedAutosaveMode(Request.RecoveryMode);

        public bool ActiveFixedAutosaveRecovery => Request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeFixedAutosave0;

        public bool VersionChangedPrimary => Request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary;

        public bool ActiveCredentialReauthorization => ActiveFixedAutosaveRecovery || VersionChangedPrimary;

        public bool Reauthorizing => Request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeExpiredPrimary
            || FixedAutosaveRecovery || VersionChangedPrimary;

        public string Provenance { get; }

        public long MinimumGameTick { get; }

        public string Fingerprint { get; }
    }

    private sealed class OwnedWorldResumeAction
    {
        public bool Reauthorizing { get; set; }

        public string ActionId { get; set; } = string.Empty;

        public OwnedWorldResumeTicket Ticket { get; set; } = null!;

        public OwnedWorldResumeSourceKind ResumeSource { get; set; }

        public long MinimumGameTick { get; set; }
    }
}
