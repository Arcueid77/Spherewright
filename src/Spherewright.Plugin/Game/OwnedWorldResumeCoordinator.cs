using System.ComponentModel;
using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class OwnedWorldResumeCoordinator
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
        {
            var invalid = BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, requestError, false,
                "Use a supported explicit recovery mode. Reauthorization requires a fresh disclosure before confirmation; arbitrary save selection is unavailable.");
            if (request.RecoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery)
                invalid.NativeFirstFailure = VoyagerRemotePrimaryRecoveryPolicy.RequestFailure(request, DateTimeOffset.UtcNow);
            if (request.RecoveryMode == OwnedWorldResumeModes.ResumeReturnOnlyTask)
                invalid.NativeFirstFailure = VoyagerReturnOnlyContinuationPolicy.RequestFailure(request);
            return GameCallResult<PreparedOwnedWorldResumePlan>.Failed(invalid);
        }
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
            var error = BridgeError.Create(
                BridgeErrorCodes.StaleState,
                rejection,
                false,
                "Keep the ticket and inspect the exact normal shutdown or latest healthy owned-save evidence.");
            if (request.RecoveryMode == OwnedWorldResumeModes.ContinueCompletedRemoteLastExit)
                error.NativeFirstFailure = _sessions.CompletedContinuationFailure;
            if (request.RecoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery)
                error.NativeFirstFailure = _sessions.RemotePrimaryRecoveryFailure;
            if (request.RecoveryMode == OwnedWorldResumeModes.ResumeReturnOnlyTask)
                error.NativeFirstFailure = _returnOnlyContinuationFailure?.Copy();
            return GameCallResult<PreparedOwnedWorldResumePlan>.Failed(error);
        }

        lease?.Dispose(); // Prepare never keeps a handle or starts loading.
        primaryLease?.Dispose();
        payload!.RemoteEvidence?.Dispose();
        payload.RemoteJournalLease?.Dispose();
        payload.RemoteCredentialLease?.Dispose();
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
        if (payload!.RequiresConfirmation)
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.UserConfirmationRequired,
                Message = payload.ActiveFixedAutosaveRecovery || payload.FailedRemoteReauthorization || payload.CompletedRemoteContinuation || payload.LegacyLocalMoveUnknown
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
            RecoveryEvidenceVersion = payload.SavedWorldOpenOnly ? OwnedSavedWorldOpenPolicy.EvidenceVersion
                : payload.RemotePrimaryRecovery ? VoyagerRemotePrimaryRecoveryPolicy.EvidenceVersion
                : payload.CompletedRemoteContinuation ? VoyagerCompletedRemoteContinuationPolicy.EvidenceVersion
                : payload.FailedRemoteReauthorization ? VoyagerFailedRemoteReauthorizationPolicy.EvidenceVersion
                : payload.RemoteContinuation ? VoyagerRemoteResumePolicy.EvidenceVersion
                : payload.VersionChangedPrimary ? OwnedWorldPrimaryMigrationPolicy.EvidenceVersionFor(ticket.GameVersion, _tickets.CurrentGameVersion)
                : payload.FixedAutosaveRecovery ? OwnedWorldAutosaveRecoveryPolicy.EvidenceVersionFor(payload.Request.RecoveryMode)
                : payload.Reauthorizing ? OwnedWorldReauthorizationPolicy.EvidenceVersion
                : payload.VerifiedRecovery ? OwnedWorldRecoveryPolicy.EvidenceVersion : 0,
            CandidateGameTick = payload.VerifiedRecovery || payload.RequiresConfirmation ? payload.MinimumGameTick : (long?)null,
            SuccessfulFlightWitnessRetiredAtGameTick = payload.RemoteEvidence?.SuccessfulFlightWitnessRetiredAtGameTick,
            OriginalJourneyDeadlineUtc = payload.RemoteEvidence?.Journey.DeadlineUtc,
            EffectiveRecoveryDeadlineUtc = payload.RemoteEvidence?.RecoveryProposal?.DeadlineUtc,
            RecoveryScope = payload.ReturnOnlyContinuation ? VoyagerReturnOnlyContinuationPolicy.Scope
                : payload.RemoteEvidence?.RecoveryProposal?.Scope ?? payload.Request.RecoveryScope,
            MaximumChargeGameTicks = payload.RemoteEvidence?.RecoveryProposal?.MaximumChargeGameTicks ?? payload.Request.MaximumChargeGameTicks,
            MaximumChargeWallSeconds = payload.RemoteEvidence?.RecoveryProposal?.MaximumChargeWallSeconds ?? payload.Request.MaximumChargeWallSeconds,
            NativeChargeFeasibilityRequiresFreshCapture = !payload.CompletedRemoteContinuation && !payload.ReturnOnlyContinuation
                && payload.RemoteEvidence?.RecoveryProposal is not null,
            PriorFailedResumeProvenanceUsed = payload.RemoteEvidence?.PriorConsumedResumeTokenHash is not null,
            PriorFailedResumeV2ProvenanceUsed = payload.RemoteEvidence?.PriorV2ProvenanceUsed == true,
            PriorFailedResumeV3ProvenanceUsed = payload.RemoteEvidence?.PriorV3ProvenanceUsed == true,
            PriorActionTerminalFailureAttested = (payload.FailedRemoteReauthorization || payload.RemoteEvidence?.PriorConsumedResumeTokenHash is not null)
                && payload.Request.PriorActionKnownTerminalFailed == true,
            FailedRemoteLastExitReauthorizationUsed = payload.RemoteEvidence?.FailedRemoteLastExitReauthorization == true,
            CompletedRemoteLastExitContinuationUsed = payload.CompletedRemoteContinuation,
            RemotePrimaryRecoveryAuthorizationUsed = payload.RemotePrimaryRecovery,
            PriorActionTerminalCompletionAttested = payload.CompletedRemoteContinuation && payload.Request.PriorActionKnownTerminalCompleted == true,
            NewChargingProhibited = payload.SavedWorldOpenOnly || payload.CompletedRemoteContinuation || payload.ReturnOnlyContinuation,
            ReturnOnlyContinuation = payload.RemoteEvidence?.ReturnOnlySource is { } returnSource
                ? new ReturnOnlyContinuationPreview {
                    OriginalTaskId=returnSource.Journey.ReturnOnlyTask!.TaskId,
                    SourceGameTick=returnSource.Source.Prefix.GameTick,
                    OriginalMaximumGameTicks=returnSource.Journey.ReturnOnlyTask.MaximumGameTicks,
                    RemainingGameTicks=returnSource.RemainingGameTicks,
                    OriginalDeadlineUtc=returnSource.Journey.ReturnOnlyTask.DeadlineUtc,
                    EffectiveDeadlineUtc=returnSource.DeadlineUtc,NewWallAuthorityRequired=returnSource.RequiresNewWallAuthority,
                } : null,
            OpensSavedWorldOnly = payload.SavedWorldOpenOnly,
            PreviousAutomationRemainsStopped = payload.SavedWorldOpenOnly,
            DiscardedUnstoredProgressGameTicks = payload.SavedWorldOpenOnly || payload.RemotePrimaryRecovery
                ? Math.Max(0, payload.SavedOpenKnownProgressTick - payload.MinimumGameTick) : (long?)null,
            ConservedObservedProgressDebitGameTicks = payload.RemotePrimaryRecovery && payload.RemoteEvidence?.KnownPrimaryProgressTick is { } floor
                ? Math.Max(0, floor - payload.MinimumGameTick) : (long?)null,
            InheritedWindowConfirmedAtUtc = payload.RemoteEvidence?.InheritedWindowConfirmedAtUtc,
            RemoteRecoveryContextFingerprint = payload.RemoteEvidence?.RemoteRecoveryContextFingerprint ?? string.Empty,
            LegacyLocalMoveUnknownProvenanceUsed = payload.LegacyLocalMoveUnknown,
            LegacyLocalMoveUnknownEvidenceHash = payload.RemoteEvidence?.LegacyLocalMoveUnknownEvidenceHash ?? string.Empty,
            LegacyLocalMoveUnknownKindCallerAttested = payload.LegacyLocalMoveUnknown,
            ExactEmbeddedIdentityVerified = payload.VerifiedRecovery || payload.RequiresConfirmation,
            UserConfirmationRequired = payload.RequiresConfirmation,
            SourceGameVersion = ticket.GameVersion,
            TargetGameVersion = _tickets.CurrentGameVersion,
            ConfirmationPrompt = (payload.RemoteEvidence?.SuccessfulFlightWitnessRetiredAtGameTick is { } coveredTick
                ? $"The original successful-flight checkpoint was retired by primary coverage at tick {coveredTick}. It is a bound historical witness only and will never be loaded. " : string.Empty)
                + (payload.RemotePrimaryRecovery
                ? $"Review the exact complete owned primary at tick {payload.MinimumGameTick}. This is a NEW finite automation authorization, not ordinary file opening or continuation of an old action. Known progress floor: {payload.SavedOpenKnownProgressTick}; explicitly discarded progress: {Math.Max(0, payload.SavedOpenKnownProgressTick - payload.MinimumGameTick)} ticks. Total conserved observed-time debit: {Math.Max(0, payload.RemoteEvidence!.KnownPrimaryProgressTick!.Value - payload.MinimumGameTick)} ticks, including any previous approved loss. Historical completed/failed/unknown outcomes and every old consumption record remain unchanged. Native initialization, projection, contact, any permitted natural charging and strict healthy takeover must all succeed. "
                : payload.LegacyLocalMoveUnknown
                ? VoyagerLegacyLocalMoveUnknownPolicy.Disclosure + " "
                : payload.CompletedRemoteContinuation
                ? "The caller attests that the immediately prior resume was observed known terminally completed, not failed or unknown. Consumed replicas are authorization evidence, not a Native outcome certificate. This attempt inherits the identical absolute recovery window and its original clock origin; it cannot add a charging phase or renew any cap. "
                : payload.FailedRemoteReauthorization
                ? $"Review this fixed LastExit at tick {payload.MinimumGameTick} and the exact owner-supplied current context fingerprint. This is new explicit authority for the current zero-progress remote context, not an opening or extension of an old authorization. The caller attests only that the immediately prior action was observed known terminally failed; its two consumed records are not a Native outcome certificate. No ancestor is followed. "
                : payload.RemoteEvidence?.PriorConsumedResumeTokenHash is not null
                ? "The caller attests that the exact prior resume action was observed terminally failed without an unknown outcome. The fixed consumed records prove authorization/provenance, not a durable Native action outcome. This is one compatibility hop only; the old proposal is proof-only and grants no execution or renewed budget. " : string.Empty)
                + (payload.ReturnOnlyContinuation && payload.RemoteEvidence?.ReturnOnlySource is { } returnSourceDisclosure
                ? $"Resume only the existing return-only task from the exact current LastExit at tick {payload.MinimumGameTick}. Remaining original tick allowance: {returnSourceDisclosure.RemainingGameTicks}; no ticks, energy, items or charging are granted. Original task deadline: {returnSourceDisclosure.Journey.ReturnOnlyTask!.DeadlineUtc:O}; effective wall deadline: {returnSourceDisclosure.DeadlineUtc:O}. A proposed new wall window requires new explicit owner authority and this digest confirmation. Historical completed/failed/unknown results and consumed records remain unchanged. The checkpoint is proof-only and will not load. Native initialization/contact and fresh strict idle must succeed before normal Save and the original-home return; failure pauses and ends this attempt."
                : payload.CompletedRemoteContinuation && payload.RemoteEvidence?.RecoveryProposal is { } inheritedWindow
                ? $"Review the same absolute deadline {inheritedWindow.DeadlineUtc:O} and original window origin {payload.RemoteEvidence.InheritedWindowConfirmedAtUtc:O}. Strict funded first-frame/contact admission remains mandatory, without new charging. If either original window or return reserve is exhausted, do not load; this disclosure is not new budget authority."
                : payload.RemoteEvidence?.RecoveryProposal is { } recoveryWindow
                ? $"Confirm one finite attempt to continue the original bounded task until {recoveryWindow.DeadlineUtc:O}. The original deadline {payload.RemoteEvidence.Journey.DeadlineUtc:O} and all tick/energy/warper limits remain unchanged. Natural charging is capped at {recoveryWindow.MaximumChargeGameTicks} game ticks / {recoveryWindow.MaximumChargeWallSeconds} wall seconds within the five-minute adoption lease; Native P/g and feasibility remain unknown until fresh capture. No promised admission or flight, automatic renewal, checkpoint load or Resume of isolated RAM."
                : payload.RemoteContinuation
                ? "Confirm this exact fixed LastExit candidate after reviewing its disclosed tick and full-file proof. It retains the original successful flight, Journal and unchanged journey deadline/reserves; it does not load the successful pre-flight checkpoint."
                : payload.SavedWorldOpenOnly
                ? $"Open only the exact complete owned primary at tick {payload.MinimumGameTick}, pausing at Native load completion. Discarded progress: {Math.Max(0, payload.SavedOpenKnownProgressTick - payload.MinimumGameTick)} ticks, from known floor {payload.SavedOpenKnownProgressTick}. Existing applicable conversation authority may cover this exact disclosed choice. Old automation remains stopped; no automatic Save, Resume, charging, flight, task renewal or action replay."
                : payload.VersionChangedPrimary ? OwnedWorldPrimaryMigrationPolicy.ConfirmationPromptFor(ticket.GameVersion, _tickets.CurrentGameVersion)
                : payload.FixedAutosaveRecovery ? OwnedWorldAutosaveRecoveryPolicy.ConfirmationPromptFor(payload.Request.RecoveryMode)
                : payload.Reauthorizing ? OwnedWorldReauthorizationPolicy.ConfirmationPrompt : string.Empty),
            ConfirmationDigest = payload.RequiresConfirmation ? payload.Fingerprint : string.Empty,
            CommitAllowedNow = blockers.Count == 0,
            CommitBlockers = blockers,
            CompletionCondition = payload.ReturnOnlyContinuation
                ? "The exact current saved boundary and original return-only task are adopted; the original Native initialization/contact/idle chain proves control before a normal Save. Only the original-home return, cancellation and normal Save are permitted; old outcomes, tick grants and original deadlines remain unchanged."
                : payload.SavedWorldOpenOnly
                ? "Exact owned primary and durable Journal verified; Native load completed and paused at the saved tick. Reads are available; automated writes remain blocked. Opening does not change any original task result, Journey, spent amount or deadline, and does not resave the world."
                : payload.RemotePrimaryRecovery
                ? "Only the exact complete current primary may load after new finite authorization and this disclosure's digest confirmation. Original Journey/Journal/successful-outbound evidence and any matching LastExit are held; old outcomes remain unchanged. Discarded observed progress remains charged against the original finite ticks. Existing Native initialization, first-frame, contact, charge, strict idle and 2P/13P gates remain; healthy takeover, conservative debit persistence, normal primary Save and a matching fresh credential are all required for success. No old action or checkpoint is replayed."
                : payload.LegacyLocalMoveUnknown
                ? "Only this exact fixed LastExit and immutable caller-attested legacy local-Move capture may proceed under the disclosed authority and digest. Original outcome_unknown remains unknown; the raw hash is not Native authentication. Current source/primary/checkpoint/Journal and active credentials are rechecked before durable consumption. The existing first-frame and strict501 ground/scene/energy handoff still decides admission, without successful-checkpoint loading, isolated-RAM Resume, renewed ticks or rewritten original deadlines."
                : payload.CompletedRemoteContinuation
                ? "Only the exact fixed LastExit/current primary/Journal and immediate consumed authorization may continue after the disclosed confirmation. The prior completed result remains completed; no charging phase or deadline/cap renewal is permitted. Fresh strict Native contact/idle admission, normal save and a renewed credential are required. Exhausted authority rejects before Load."
                : payload.FailedRemoteReauthorization
                ? "Only the disclosed zero-progress fixed LastExit and independently supplied exact current-context fingerprint may load under applicable explicit authority and this plan's digest. The immediate consumed pair proves prior authorization, not its Native terminal outcome. Active credentials are durably consumed once; original ticks, elapsed accounting, energy/warper floors and checkpoint remain unchanged. Original strict contact/first-frame/2P/13P gates still apply. Normal owned primary save and a matching renewed credential are required for success; no replay or automatic renewal."
                : payload.RemoteEvidence?.RecoveryProposal is not null
                ? "Only this fixed LastExit, exact immutable one-shot UTC authorization and original task limits may continue after subsequent explicit confirmation. Native guarded charging remains pending until full original strict13 idle admission; the original return core minimum remains an independent fresh return-flight gate. Fresh positive route proof precedes flight. A failed attempt pauses and cannot rekey or renew."
                : payload.RemoteContinuation
                ? $"Only the fixed LastExit at tick {payload.MinimumGameTick} may continue this exact quarantined remote journey after subsequent explicit confirmation of this disclosure. Source, current primary, original successful checkpoint and Journal are held and rechecked. The old credential is durably consumed before normal loading; ownership and Journal confirmation precede a new strict idle owner and any normal primary resave. No automatic Resume, checkpoint rollback, renewed budget or deadline."
                : payload.VersionChangedPrimary
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
        if (payload.RequiresConfirmation
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
        OwnedWorldResumePlanPayload? currentPayload = null;
        if (payload.CompletedRemoteContinuation) _sessions.BeginCompletedContinuationDecision();
        if (readinessError is not null
            || !TryGetTicket(payload.Request, payload.Ticket.ResumeToken, out var currentTicket, out var provenance, out _)
            || currentTicket is null
            || !TryResolvePayload(currentTicket, payload.Request, provenance, out currentPayload, out sourceLease, out primaryLease, out rejection)
            || !string.Equals(currentPayload!.Fingerprint, payload.Fingerprint, StringComparison.Ordinal))
        {
            sourceLease?.Dispose();
            primaryLease?.Dispose();
            currentPayload?.RemoteEvidence?.Dispose();
            currentPayload?.RemoteJournalLease?.Dispose();
            currentPayload?.RemoteCredentialLease?.Dispose();
            var error = BridgeError.Create(
                BridgeErrorCodes.StaleState,
                readinessError?.Message ?? (string.IsNullOrWhiteSpace(rejection)
                    ? "The protected source evidence changed after prepare; no load was started." : rejection),
                false,
                "Do not load a save; return to an idle main menu and prepare from the exact current ticket.");
            if (payload.CompletedRemoteContinuation) error.NativeFirstFailure = _sessions.CompletedContinuationFailure;
            if (payload.RemotePrimaryRecovery) error.NativeFirstFailure = _sessions.RemotePrimaryRecoveryFailure;
            if (payload.ReturnOnlyContinuation) error.NativeFirstFailure = _returnOnlyContinuationFailure?.Copy();
            return GameCallResult<OwnedWorldResumeResult>.Failed(error);
        }

        var action = new OwnedWorldResumeAction
        {
            Reauthorizing = payload.Reauthorizing,
            FreshRequiresRenewedTicket = payload.Reauthorizing || payload.RemotePrimaryRecovery || payload.ReturnOnlyContinuation || payload.FailedRemoteReauthorization || payload.CompletedRemoteContinuation,
            FailedRemoteReauthorization = payload.FailedRemoteReauthorization,
            SavedWorldOpenOnly = payload.SavedWorldOpenOnly,
            ActionId = Guid.NewGuid().ToString("D"),
            Ticket = currentTicket,
            ResumeSource = payload.ResumeSource,
            MinimumGameTick = payload.MinimumGameTick,
            RemoteContinuation = payload.RemoteContinuation,
        };
        FileStream? journalLease = null;
        var nativeLoadAttempted = false;
        VoyagerRecoveryAuthorization? recoveryAuthorization = null;
        try
        {
            if(payload.ReturnOnlyContinuation)
            {
                var native=currentPayload!.RemoteEvidence!;
                if(native.ReturnOnlySource is not { } returnSource
                    ||!_sessions.RegisterReturnOnlyContinuation(returnSource,action.ActionId,
                        request.UserConfirmedInConversation,payload.Fingerprint,DateTimeOffset.UtcNow,out var registerRejection))
                    return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(BridgeErrorCodes.StaleState,
                        "return_only_continuation_registration_unproved",false,"No Native load was started; preserve the exact original task."));
                native.BindRegisteredReturnOnlyJourney(_sessions.ReturnOnlyContinuationJourneyJson());
            }
            if (payload.RemoteContinuation)
            {
                journalLease = currentPayload!.RemoteJournalLease;
                if (currentPayload.RemoteEvidence?.RecoveryProposal is { } proposal)
                    recoveryAuthorization = new VoyagerRecoveryAuthorization(action.ActionId, currentPayload.RemoteEvidence.Fingerprint,
                        payload.Fingerprint, DateTimeOffset.UtcNow, proposal,
                        currentPayload.RemoteEvidence.CompletedContinuationTokenHash ?? currentPayload.RemoteEvidence.PriorConsumedResumeTokenHash,
                        currentPayload.RemoteEvidence.InheritedWindowConfirmedAtUtc);
                var consumed = currentPayload.RemoteEvidence is not null && (payload.RemotePrimaryRecovery || payload.ReturnOnlyContinuation
                    ? recoveryAuthorization is not null && _tickets.TryConsumeReauthorization(currentTicket.ResumeToken,
                        payload.Provenance, action.ActionId, payload.Fingerprint, savedWorldOpenOnly: true,
                        recoveryAuthorization: recoveryAuthorization)
                    : _tickets.TryConsumeRemoteCredential(currentTicket.ResumeToken, currentPayload.RemoteCredentialFingerprint,
                        recoveryAuthorization, currentPayload.RemoteCredentialLease));
                if (currentPayload.RemoteEvidence is null || !consumed)
                {
                    currentPayload.RemoteEvidence?.Dispose();
                    journalLease?.Dispose(); sourceLease?.Dispose(); primaryLease?.Dispose();
                    var consumptionError=BridgeError.Create(BridgeErrorCodes.StaleState,
                        "remote_resume_durable_credential_consumption_failed", false,
                        "No load was dispatched; preserve the original candidate and credential evidence.");
                    if(payload.ReturnOnlyContinuation)
                        consumptionError.NativeFirstFailure=_sessions.RecordReturnOnlyPreloadConsumptionFailure(action.ActionId);
                    return GameCallResult<OwnedWorldResumeResult>.Failed(consumptionError);
                }
            }
            if (payload.Reauthorizing)
                journalLease = _tickets.OpenReauthorizationJournalLease(currentTicket, payload.Provenance,
                    payload.ActiveCredentialReauthorization, payload.VersionChangedPrimary, payload.SavedWorldOpenOnly);
            if (payload.Reauthorizing && !_tickets.TryConsumeReauthorization(currentTicket.ResumeToken,
                payload.Provenance, action.ActionId, payload.Fingerprint,
                payload.ActiveCredentialReauthorization, payload.VersionChangedPrimary, payload.SavedWorldOpenOnly))
                return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                    BridgeErrorCodes.StaleState,
                    "The exact provenance changed or durable consumption failed; no load was started.",
                    false, "Keep the current main menu and investigate; do not replay with another save."));
            _sessions.ExpectNextSessionToBeResumed(currentTicket, sourceLease, payload.Reauthorizing, journalLease,
                payload.FixedAutosaveRecovery, primaryLease, currentPayload!.RemoteEvidence,
                payload.RemoteContinuation ? action.ActionId : null, payload.ResumeSource, recoveryAuthorization,
                payload.SavedWorldOpenOnly, payload.SavedWorldOpenOnly ? action.ActionId : null,
                payload.SavedWorldOpenOnly ? payload.SavedOpenKnownProgressTick : (long?)null);
            sourceLease = null; // Tracker owns the lease through adoption/Journal confirmation.
            journalLease = null;
            primaryLease = null;
            if (payload.RemoteContinuation) _sessions.PrepareRemoteResumeNativeLoadObserver();
            if (payload.SavedWorldOpenOnly) _sessions.PrepareSavedWorldOpenNativeObserver();
            // The source is an internal enum bound by the prepared fingerprint,
            // never a caller-supplied path/name or an automatic fallback.
            nativeLoadAttempted = true;
            DSPGame.StartGame(ResolveNativeResumeSource(payload.ResumeSource, currentTicket.OwnedSaveName));
            if (payload.RemoteContinuation) _sessions.BindRemoteResumeNativeLoaderAfterDispatch();
            if (payload.SavedWorldOpenOnly) _sessions.BindSavedWorldOpenNativeObserver();
        }
        catch (Exception exception)
        {
            if ((payload.RemoteContinuation || payload.SavedWorldOpenOnly) && nativeLoadAttempted)
            {
                // The Native call was entered and may already have accepted or
                // published a loader. Never infer no dispatch or use a new key.
                action.NativeLoadObservationError = "remote_resume_postdispatch_loader_observation_failed:" + exception.GetType().Name;
                try
                {
                    if (payload.SavedWorldOpenOnly) _sessions.RecordSavedWorldOpenDispatchFailure(exception.GetType().Name);
                    else _sessions.RecordRemoteResumeNativeDispatchObservationFailure(exception.GetType().Name);
                }
                catch (Exception captureException) { action.NativeLoadObservationError += ":record:" + captureException.GetType().Name; }
            }
            else
            {
                _sessions.CancelExpectedResumedSession();
                currentPayload?.RemoteEvidence?.Dispose();
                return GameCallResult<OwnedWorldResumeResult>.Failed(BridgeError.Create(
                    BridgeErrorCodes.ActionFailed,
                    $"DSP rejected the exact protected owned-world resume through its normal loader ({exception.GetType().Name}).",
                    false,
                    "Inspect the local Spherewright and Unity logs; do not load another save."));
            }
        }
        finally
        {
            sourceLease?.Dispose();
            journalLease?.Dispose();
            primaryLease?.Dispose();
            currentPayload?.RemoteCredentialLease?.Dispose();
        }

        var result = new OwnedWorldResumeResult
        {
            ActionId = action.ActionId,
            Accepted = true,
            IdempotentReplay = false,
            State = action.NativeLoadObservationError is null ? NormalActionStates.WaitingForGame : NormalActionStates.OutcomeUnknown,
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
        if ((action.RemoteContinuation || action.SavedWorldOpenOnly) && action.TerminalResult is { } terminal)
        {
            result = CloneResumeReadback(terminal);
            return true;
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
        if (action.NativeLoadObservationError is not null)
        {
            result.State = NormalActionStates.OutcomeUnknown;
            result.Message = action.NativeLoadObservationError;
            if (action.SavedWorldOpenOnly) result.NativeFirstFailure = _sessions.SavedWorldOpenFirstFailure;
            else if (action.RemoteContinuation) result.NativeFirstFailure = _sessions.RemoteResumeFailureForAction(action.ActionId);
            return true;
        }
        if (action.SavedWorldOpenOnly)
        {
            if (_sessions.SavedWorldOpenCompleted(action.ActionId))
            {
                result.State = NormalActionStates.Completed; result.Terminal = true; result.Succeeded = true;
                result.SessionId = _sessions.SavedWorldOpenSessionId; result.PlanetId = action.Ticket.ExpectedPlanetId;
                result.Message = "The exact saved owned world opened paused. Previous automation remains stopped; no automatic Save or task continuation was performed.";
            }
            else if (!string.IsNullOrEmpty(_sessions.ResumeAdoptionError))
            {
                result.State = NormalActionStates.ActionFailed; result.Terminal = true;
                result.Message = _sessions.ResumeAdoptionError;
                result.NativeFirstFailure = _sessions.SavedWorldOpenFirstFailure;
            }
            if (result.Terminal) action.TerminalResult = CloneResumeReadback(result);
            return true;
        }
        if (action.RemoteContinuation && !string.IsNullOrWhiteSpace(_sessions.ResumeAdoptionError))
        {
            result.NativeFirstFailure = _sessions.RemoteResumeFailureForAction(action.ActionId);
            result.State = NormalActionStates.ActionFailed;
            result.Terminal = true;
            result.Message = _sessions.ResumeAdoptionError;
            if (session.OwnedBySpherewright)
            { result.SessionId = session.SessionId; result.PlanetId = session.LocalPlanetId; }
            RememberRemoteReauthorizationTerminal(action, result);
            return true; // Reconcile this accepted load; never replay the consumed capability.
        }
        if (session.OwnedBySpherewright
            && session.LocalPlanetId == action.Ticket.ExpectedPlanetId
            && session.GameTick >= action.MinimumGameTick)
        {
            result.SessionId = session.SessionId;
            result.PlanetId = session.LocalPlanetId;
            if (string.Equals(session.OwnedSaveState, OwnedSaveStates.Saved, StringComparison.Ordinal)
                && session.LastOwnedSaveGameTick >= action.MinimumGameTick)
            {
                if (action.FreshRequiresRenewedTicket
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
                    RememberRemoteReauthorizationTerminal(action, result);
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

        RememberRemoteReauthorizationTerminal(action, result);
        return true;
    }

    private static void RememberRemoteReauthorizationTerminal(OwnedWorldResumeAction action, ActionResultSnapshot result)
    {
        if (action.RemoteContinuation && result.Terminal && action.TerminalResult is null)
            action.TerminalResult = CloneResumeReadback(result);
    }

    private static ActionResultSnapshot CloneResumeReadback(ActionResultSnapshot result) => new ActionResultSnapshot
    {
        ActionId = result.ActionId, ActionKind = result.ActionKind, State = result.State,
        Terminal = result.Terminal, Succeeded = result.Succeeded, Message = result.Message,
        SessionId = result.SessionId, PlanetId = result.PlanetId, NativeFirstFailure = result.NativeFirstFailure?.Copy(),
    };

    private bool TryGetTicket(PrepareOwnedWorldResumeRequest request, string token,
        out OwnedWorldResumeTicket? ticket, out string provenance, out string rejection)
    {
        provenance = string.Empty;
        if (request.RecoveryMode == OwnedWorldResumeModes.OpenSavedPrimary
            || request.RecoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery
            || request.RecoveryMode == OwnedWorldResumeModes.ResumeReturnOnlyTask)
            return _tickets.TryGetSavedWorldOpenProvenance(token, out ticket, out provenance, out rejection);
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
        if (request.RecoveryMode == OwnedWorldResumeModes.ResumeReturnOnlyTask)
            return TryResolveReturnOnlyPayload(ticket,request,provenance,out payload,out lease,out primaryLease,out rejection);
        if (request.RecoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery)
            return TryResolveRemotePrimaryPayload(ticket, request, provenance, out payload, out lease, out primaryLease, out rejection);
        if (request.RecoveryMode == OwnedWorldResumeModes.OpenSavedPrimary)
        {
            if (ticket.SourceProcessId != Process.GetCurrentProcess().Id && IsLiveDspProcess(ticket.SourceProcessId))
            { rejection = "Another source game process is still running; opening requires an idle menu with one game writer."; return false; }
            try
            {
                lease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.OwnedSaveName), ticket.OwnedSaveName);
                var lastExitPath = GameSave.SavePath(GameSave.LastExit);
                if (File.Exists(lastExitPath))
                {
                    using var prefixStream = new FileStream(lastExitPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var prefix = OwnedSavePrefixReader.Read(prefixStream, ticket.OwnedSaveName);
                    if (prefix.MatchesExpectedIdentity) primaryLease = OwnedSaveRecoveryLease.Open(lastExitPath, ticket.OwnedSaveName);
                }
                var knownProgress = Math.Max(ticket.MinimumGameTick, primaryLease?.Prefix.GameTick ?? 0);
                rejection = OwnedSavedWorldOpenPolicy.ValidateCandidate(request, knownProgress,
                    _tickets.CurrentGameVersion, lease.Prefix) ?? string.Empty;
                if (rejection.Length != 0) { lease.Dispose(); lease = null; primaryLease?.Dispose(); primaryLease = null; return false; }
                payload = new OwnedWorldResumePlanPayload(ticket, OwnedWorldResumeSourceKind.OwnedPrimary,
                    request, lease, provenance, _sessions.Revision, primaryLease?.Fingerprint ?? string.Empty,
                    savedOpenKnownProgressTick: knownProgress);
                return true;
            }
            catch (Exception exception)
            { lease?.Dispose(); lease = null; primaryLease?.Dispose(); primaryLease = null; rejection = "saved_primary_open_full_evidence_unavailable:" + exception.GetType().Name; return false; }
        }
        var hasRecoveryWindow = request.RecoveryDeadlineUtc.HasValue || !string.IsNullOrEmpty(request.RecoveryScope)
            || request.MaximumChargeGameTicks != 0 || request.MaximumChargeWallSeconds != 0d;
        var hasPriorFailedProof = OwnedWorldRecoveryPolicy.HasPriorFailedResumeWitness(request);
        var legacyLocalMoveUnknown = VoyagerLegacyLocalMoveUnknownPolicy.HasEvidence(request);
        var failedRemoteReauthorization = request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit;
        var completedRemoteContinuation = request.RecoveryMode == OwnedWorldResumeModes.ContinueCompletedRemoteLastExit;
        if (hasRecoveryWindow && (!request.RecoveryDeadlineUtc.HasValue
            || request.RecoveryMode != OwnedWorldResumeModes.Default && !failedRemoteReauthorization))
        { rejection = "remote_resume_recovery_window_default_only"; return false; }
        if (request.RecoveryMode == OwnedWorldResumeModes.Default || failedRemoteReauthorization || completedRemoteContinuation)
        {
            if (!TryResolveResumeSource(ticket, out var source, out rejection)) return false;
            if (source == OwnedWorldResumeSourceKind.LastExit && !string.IsNullOrWhiteSpace(ticket.QuarantineActionId)
                && _sessions.RequiresRemoteResumeEvidence)
            {
                FileStream? remoteJournal = null;
                VoyagerRemoteResumeEvidence? remoteEvidence = null;
                ConsumedRemoteResumeProof? priorConsumedProof = null;
                RemoteResumeCredentialLease? credentialLease = null;
                try
                {
                    lease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), ticket.OwnedSaveName);
                    primaryLease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.OwnedSaveName), ticket.OwnedSaveName);
                    remoteJournal = _tickets.OpenRemoteResumeJournalLease(ticket, out var journalFingerprint,
                        failedRemoteReauthorization || completedRemoteContinuation || legacyLocalMoveUnknown);
                    string credentialFingerprint;
                    if (failedRemoteReauthorization || completedRemoteContinuation)
                    {
                        if (!_tickets.TryOpenRemoteCredential(ticket.ResumeToken, out credentialLease, out rejection)) return false;
                        credentialFingerprint = credentialLease!.Fingerprint;
                        if (!_tickets.TryOpenImmediateRemoteConsumption(request.PriorConsumedResumeToken, ticket,
                            out priorConsumedProof, out rejection)) return false;
                    }
                    else if (legacyLocalMoveUnknown)
                    {
                        if (!_tickets.TryOpenRemoteCredential(ticket.ResumeToken, out credentialLease, out rejection)) return false;
                        credentialFingerprint = credentialLease!.Fingerprint;
                    }
                    else if (!_tickets.TryCaptureRemoteCredential(ticket.ResumeToken, out credentialFingerprint, out rejection)) return false;
                    if (!failedRemoteReauthorization && !completedRemoteContinuation && hasPriorFailedProof && !_tickets.TryOpenPriorFailedResumeProof(request.PriorConsumedResumeToken,
                        ticket.QuarantineActionId, out priorConsumedProof, out rejection,
                        requireV2Predecessor: !string.IsNullOrEmpty(request.PriorLegacySourceFingerprint),
                        requireV3Predecessor: !string.IsNullOrEmpty(request.PriorIntermediateSourceFingerprint))) return false;
                    var proposal = completedRemoteContinuation ? priorConsumedProof!.Authorization.Proposal
                        : request.RecoveryDeadlineUtc is { } proposedDeadline
                        ? new VoyagerRecoveryWindowProposal(proposedDeadline, request.RecoveryScope,
                            request.MaximumChargeGameTicks, request.MaximumChargeWallSeconds) : null;
                    if (!_sessions.TryOpenRemoteResumeEvidence(ticket, lease, primaryLease, out remoteEvidence, out rejection, proposal,
                        priorConsumedProof, request.PriorSourceFingerprint, request.PriorActionKnownTerminalFailed == true,
                        request.PriorLegacySourceFingerprint, request.PriorIntermediateSourceFingerprint,
                        failedRemoteReauthorization, request.ExpectedRemoteRecoveryContextFingerprint,
                        // Legacy raw captures bind the complete Journal file bytes; existing modes retain their text fingerprint.
                        legacyLocalMoveUnknown ? FlightCheckpointStore.HashResumeRecord(remoteJournal) : journalFingerprint,
                        request.LegacyLocalMoveUnknownEvidenceJson, request.ExpectedLegacyLocalMoveUnknownEvidenceHash,
                        credentialLease?.RawFingerprint ?? string.Empty, completedRemoteContinuation,
                        request.PriorActionKnownTerminalCompleted == true))
                        return false;
                    priorConsumedProof = null; // The fresh evidence now owns both prior consumption leases.
                    payload = new OwnedWorldResumePlanPayload(ticket, source, request, lease, provenance, _sessions.Revision,
                        primaryLease.Fingerprint, remoteEvidence, remoteJournal, journalFingerprint, credentialFingerprint, credentialLease);
                    remoteEvidence = null; remoteJournal = null; credentialLease = null;
                    return true;
                }
                catch (Exception exception)
                { rejection = "remote_resume_full_evidence_unavailable:" + exception.GetType().Name; return false; }
                finally
                {
                    remoteEvidence?.Dispose(); remoteJournal?.Dispose(); priorConsumedProof?.Dispose(); credentialLease?.Dispose();
                    if (payload is null) { lease?.Dispose(); lease = null; primaryLease?.Dispose(); primaryLease = null; }
                }
            }
            if (failedRemoteReauthorization) { rejection = "remote_reauthorization_active_quarantined_remote_last_exit_required"; return false; }
            if (completedRemoteContinuation) { rejection = "completed_remote_active_quarantined_last_exit_required"; return false; }
            if (legacyLocalMoveUnknown) { rejection = "legacy_local_move_active_quarantined_remote_last_exit_required"; return false; }
            if (hasRecoveryWindow) { rejection = "remote_resume_recovery_window_quarantined_remote_only"; return false; }
            if (hasPriorFailedProof) { rejection = "remote_resume_prior_failed_quarantined_remote_only"; return false; }
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

    private bool TryResolveRemotePrimaryPayload(OwnedWorldResumeTicket ticket, PrepareOwnedWorldResumeRequest request,
        string provenance, out OwnedWorldResumePlanPayload? payload, out OwnedSaveRecoveryLease? source,
        out OwnedSaveRecoveryLease? primary, out string rejection)
    {
        payload = null; source = null; primary = null; rejection = string.Empty;
        _sessions.BeginRemotePrimaryDecision();
        if (!_sessions.RequiresRemoteResumeEvidence)
        {
            rejection = "remote_primary_original_remote_journey_required";
            _sessions.RecordRemotePrimarySourceFailure(rejection, "original_remote_journey_present"); return false;
        }
        if (ticket.SourceProcessId != Process.GetCurrentProcess().Id && IsLiveDspProcess(ticket.SourceProcessId))
        {
            rejection = "The source DSP process is still running; no remote-primary recovery is permitted.";
            _sessions.RecordRemotePrimarySourceFailure(rejection, "single_source_process"); return false;
        }
        FileStream? journal = null;
        OwnedSaveRecoveryLease? progress = null;
        VoyagerRemoteResumeEvidence? evidence = null;
        try
        {
            source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.OwnedSaveName), ticket.OwnedSaveName);
            primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.OwnedSaveName), ticket.OwnedSaveName);
            var lastExitPath = GameSave.SavePath(GameSave.LastExit);
            if (File.Exists(lastExitPath))
            {
                using var prefix = new FileStream(lastExitPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (OwnedSavePrefixReader.Read(prefix, ticket.OwnedSaveName).MatchesExpectedIdentity)
                    progress = OwnedSaveRecoveryLease.Open(lastExitPath, ticket.OwnedSaveName);
            }
            var knownProgress = Math.Max(ticket.MinimumGameTick, progress?.Prefix.GameTick ?? 0);
            journal = _tickets.OpenReauthorizationJournalLease(ticket, provenance, savedWorldOpenOnly: true);
            var journalHash = FlightCheckpointStore.HashResumeRecord(journal);
            var proposal = new VoyagerRecoveryWindowProposal(request.RecoveryDeadlineUtc!.Value, request.RecoveryScope,
                request.MaximumChargeGameTicks, request.MaximumChargeWallSeconds);
            if (!_sessions.TryOpenRemoteResumeEvidence(ticket, source, primary, out evidence, out rejection, proposal,
                validatedJournalHash: journalHash, remotePrimaryRequest: request, progressLastExit: progress)) return false;
            progress = null; // The fresh evidence holds any matching fixed LastExit through admission.
            payload = new OwnedWorldResumePlanPayload(ticket, OwnedWorldResumeSourceKind.OwnedPrimary, request,
                source, provenance, _sessions.Revision, primary.Fingerprint, evidence, journal, journalHash,
                savedOpenKnownProgressTick: knownProgress);
            return true;
        }
        catch (Exception exception)
        {
            rejection = "remote_primary_full_evidence_unavailable:" + exception.GetType().Name;
            _sessions.RecordRemotePrimarySourceFailure(rejection, "complete_primary_source_and_journal_leases", exception.GetType().Name);
            return false;
        }
        finally
        {
            if (payload is null)
            { source?.Dispose(); source = null; primary?.Dispose(); primary = null; journal?.Dispose(); evidence?.Dispose(); progress?.Dispose(); }
        }
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
            string primaryFingerprint = "", VoyagerRemoteResumeEvidence? remoteEvidence = null,
            FileStream? remoteJournalLease = null, string remoteJournalFingerprint = "", string remoteCredentialFingerprint = "",
            RemoteResumeCredentialLease? remoteCredentialLease = null, long? savedOpenKnownProgressTick = null)
        {
            Ticket = ticket;
            SavedOpenKnownProgressTick = savedOpenKnownProgressTick ?? ticket.MinimumGameTick;
            Provenance = provenance;
            ResumeSource = resumeSource;
            RemoteEvidence = remoteEvidence;
            RemoteJournalLease = remoteJournalLease;
            RemoteCredentialFingerprint = remoteCredentialFingerprint;
            RemoteCredentialLease = remoteCredentialLease;
            Request = new PrepareOwnedWorldResumeRequest
            {
                RecoveryMode = request.RecoveryMode,
                UserConfirmedInConversation = request.UserConfirmedInConversation,
                AcceptUnstoredProgressLoss = request.AcceptUnstoredProgressLoss,
                MinimumRecoveryGameTick = request.MinimumRecoveryGameTick,
                ExpectedRecoveryGameTick = request.ExpectedRecoveryGameTick,
                RecoveryDeadlineUtc = request.RecoveryDeadlineUtc, RecoveryScope = request.RecoveryScope,
                MaximumChargeGameTicks = request.MaximumChargeGameTicks, MaximumChargeWallSeconds = request.MaximumChargeWallSeconds,
                PriorConsumedResumeToken = request.PriorConsumedResumeToken, PriorSourceFingerprint = request.PriorSourceFingerprint,
                PriorLegacySourceFingerprint = request.PriorLegacySourceFingerprint,
                PriorIntermediateSourceFingerprint = request.PriorIntermediateSourceFingerprint,
                PriorActionKnownTerminalFailed = request.PriorActionKnownTerminalFailed,
                PriorActionKnownTerminalCompleted = request.PriorActionKnownTerminalCompleted,
                ExpectedRemoteRecoveryContextFingerprint = request.ExpectedRemoteRecoveryContextFingerprint,
                LegacyLocalMoveUnknownEvidenceJson = request.LegacyLocalMoveUnknownEvidenceJson,
                ExpectedLegacyLocalMoveUnknownEvidenceHash = request.ExpectedLegacyLocalMoveUnknownEvidenceHash,
            };
            MinimumGameTick = lease?.Prefix.GameTick ?? ticket.MinimumGameTick;
            var fingerprint = CanonicalStateHash.Combine(
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
                primaryFingerprint, remoteEvidence?.Fingerprint ?? string.Empty, remoteJournalFingerprint, remoteCredentialFingerprint,
                Request.RecoveryDeadlineUtc, Request.RecoveryScope, Request.MaximumChargeGameTicks, Request.MaximumChargeWallSeconds);
            Fingerprint = ReturnOnlyContinuation ? remoteEvidence!.ReturnOnlySource!.Fingerprint : SavedWorldOpenOnly
                ? CanonicalStateHash.Combine("open-saved-primary-v1", fingerprint, SavedOpenKnownProgressTick, Request.AcceptUnstoredProgressLoss,
                    Request.MinimumRecoveryGameTick, Request.ExpectedRecoveryGameTick)
                : RemotePrimaryRecovery
                ? CanonicalStateHash.Combine("authorize-remote-primary-recovery-v1", fingerprint, SavedOpenKnownProgressTick,
                    Request.AcceptUnstoredProgressLoss, Request.MinimumRecoveryGameTick, Request.ExpectedRecoveryGameTick)
                : CompletedRemoteContinuation
                ? CanonicalStateHash.Combine("resume-owned-world-completed-remote-continuation-v1", fingerprint,
                    Request.ExpectedRemoteRecoveryContextFingerprint, Request.PriorConsumedResumeToken, Request.PriorActionKnownTerminalCompleted,
                    remoteCredentialLease?.RawFingerprint ?? string.Empty)
                : FailedRemoteReauthorization
                ? CanonicalStateHash.Combine("resume-owned-world-failed-remote-reauthorization-v1", fingerprint,
                    Request.ExpectedRemoteRecoveryContextFingerprint, Request.PriorConsumedResumeToken, Request.PriorActionKnownTerminalFailed,
                    remoteCredentialLease?.RawFingerprint ?? string.Empty)
                : !OwnedWorldRecoveryPolicy.HasPriorFailedResumeWitness(Request) ? fingerprint
                : CanonicalStateHash.Combine("resume-owned-world-prior-failed-v1", fingerprint,
                    Request.PriorConsumedResumeToken, Request.PriorSourceFingerprint, Request.PriorActionKnownTerminalFailed);
            if (!string.IsNullOrEmpty(Request.PriorLegacySourceFingerprint))
                Fingerprint = CanonicalStateHash.Combine("resume-owned-world-prior-failed-v2", Fingerprint,
                    Request.PriorLegacySourceFingerprint);
            if (!string.IsNullOrEmpty(Request.PriorIntermediateSourceFingerprint))
                Fingerprint = CanonicalStateHash.Combine("resume-owned-world-prior-failed-v3", Fingerprint,
                    Request.PriorIntermediateSourceFingerprint);
            if (LegacyLocalMoveUnknown)
                Fingerprint = CanonicalStateHash.Combine("resume-owned-world-legacy-local-move-unknown-v1", Fingerprint,
                    Request.ExpectedLegacyLocalMoveUnknownEvidenceHash);
        }

        public OwnedWorldResumeTicket Ticket { get; }
        public long SavedOpenKnownProgressTick { get; }

        public OwnedWorldResumeSourceKind ResumeSource { get; }

        public PrepareOwnedWorldResumeRequest Request { get; }

        public bool VerifiedRecovery => Request.RecoveryMode == OwnedWorldResumeModes.VerifiedNewerLastExit;
        public VoyagerRemoteResumeEvidence? RemoteEvidence { get; }
        public FileStream? RemoteJournalLease { get; }
        public string RemoteCredentialFingerprint { get; }
        public RemoteResumeCredentialLease? RemoteCredentialLease { get; }
        public bool FailedRemoteReauthorization => Request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit;
        public bool CompletedRemoteContinuation => Request.RecoveryMode == OwnedWorldResumeModes.ContinueCompletedRemoteLastExit;
        public bool SavedWorldOpenOnly => Request.RecoveryMode == OwnedWorldResumeModes.OpenSavedPrimary;
        public bool RemotePrimaryRecovery => Request.RecoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery;
        public bool ReturnOnlyContinuation => Request.RecoveryMode == OwnedWorldResumeModes.ResumeReturnOnlyTask;
        public bool RemoteContinuation => RemoteEvidence is not null;
        public bool LegacyLocalMoveUnknown => !string.IsNullOrEmpty(RemoteEvidence?.LegacyLocalMoveUnknownEvidenceHash);
        public bool RequiresConfirmation => Reauthorizing || RemoteContinuation;

        public bool FixedAutosaveRecovery => OwnedWorldAutosaveRecoveryPolicy.IsFixedAutosaveMode(Request.RecoveryMode);

        public bool ActiveFixedAutosaveRecovery => Request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeFixedAutosave0;

        public bool VersionChangedPrimary => Request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary;

        public bool ActiveCredentialReauthorization => ActiveFixedAutosaveRecovery || VersionChangedPrimary;

        public bool Reauthorizing => Request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeExpiredPrimary
            || FixedAutosaveRecovery || VersionChangedPrimary || SavedWorldOpenOnly;

        public string Provenance { get; }

        public long MinimumGameTick { get; }

        public string Fingerprint { get; }
    }

    private sealed class OwnedWorldResumeAction
    {
        public string? NativeLoadObservationError { get; set; }
        public bool RemoteContinuation { get; set; }
        public bool SavedWorldOpenOnly { get; set; }
        public bool Reauthorizing { get; set; }
        public bool FreshRequiresRenewedTicket { get; set; }
        public bool FailedRemoteReauthorization { get; set; }
        public ActionResultSnapshot? TerminalResult { get; set; }

        public string ActionId { get; set; } = string.Empty;

        public OwnedWorldResumeTicket Ticket { get; set; } = null!;

        public OwnedWorldResumeSourceKind ResumeSource { get; set; }

        public long MinimumGameTick { get; set; }
    }
}
