using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private VoyagerRemoteResumeEvidence? _pendingRemoteResumeEvidence;
    private string? _pendingRemoteResumeActionId;
    private bool _remoteResumeJournalConfirmed;
    private bool _remoteResumeAdmitted;
    private VoyagerRemoteResumeAdoption? _remoteResumeAdoption;
    private VoyagerRecoveryAuthorization? _pendingRecoveryAuthorization;
    private Func<VoyagerRemoteResumeAdoption, string?>? _remoteResumeAdmissionGuard;
    private Func<VoyagerRemoteResumeAdoption, string?>? _remoteResumePreflightGuard;
    private Func<VoyagerRemoteResumeAdoption, string?>? _remoteResumeUniverseGuard;
    private Func<VoyagerRemoteResumeAdoption, string?>? _remoteResumeInitializationGuard;
    private Func<string?>? _remoteResumeLoadCompleteGuard;
    private GameLoader[]? _remoteResumeLoadersBeforeDispatch;
    private NativeLoadCompletionSubscription<GameLoader>? _remoteResumeLoadSubscription;
    private GameLoader? _remoteResumeExpectedLoader;
    private GameData? _remoteResumeCompletedWorld;
    private bool _remoteResumeLoadCompleteObserved;
    private bool _remoteResumeLoadPreflightPassed;
    private bool _remoteResumeLoadPreflightRunning;
    private bool _remoteResumeInitializationPending;
    private bool _remoteResumeInitializationCompleting;
    private VoyagerRemoteResumeAdoption? _remoteResumeGroundDiagnosticAdoption;

    internal bool RequiresRemoteResumeEvidence => _flightCheckpoints.VoyagerJourney?.Phase == "remote_landed"
        || _flightCheckpoints.VoyagerJourneyLoadError is not null;
    internal bool HasPendingRemoteResumeAdoption => _pendingRemoteResumeEvidence is not null && !_remoteResumeAdmitted;
    internal string CurrentNativeGameVersion => _gameVersion;

    internal bool TryOpenRemoteResumeEvidence(OwnedWorldResumeTicket ticket, OwnedSaveRecoveryLease source,
        OwnedSaveRecoveryLease primary, out VoyagerRemoteResumeEvidence? evidence, out string rejection,
        VoyagerRecoveryWindowProposal? proposal = null, PriorFailedResumeProof? priorFailedProof = null,
        string priorSourceFingerprint = "", bool priorActionKnownTerminalFailed = false, string priorLegacySourceFingerprint = "",
        string priorIntermediateSourceFingerprint = "", bool failedRemoteReauthorization = false,
        string expectedRemoteContext = "", string validatedJournalHash = "", string legacyLocalMoveEvidenceJson = "",
        string legacyLocalMoveEvidenceHash = "", string legacyCredentialRawFingerprint = "") =>
        _flightCheckpoints.TryOpenRemoteResumeEvidence(ticket, source, primary, out evidence, out rejection, proposal,
            priorFailedProof, priorSourceFingerprint, priorActionKnownTerminalFailed, priorLegacySourceFingerprint,
            priorIntermediateSourceFingerprint, failedRemoteReauthorization, expectedRemoteContext, validatedJournalHash,
            legacyLocalMoveEvidenceJson, legacyLocalMoveEvidenceHash, legacyCredentialRawFingerprint);

    internal DateTimeOffset EffectiveVoyagerDeadline(VoyagerJourneyRecord journey)
    {
        var adoption = _remoteResumeAdoption;
        var authorization = adoption?.Authorization;
        if (authorization is null || !IsCurrentSessionOwned || !ReferenceEquals(GameMain.data, adoption!.World)
            || _sessionId != adoption.SessionId || authorization.ResumeActionId != adoption.ResumeActionId
            || authorization.EvidenceFingerprint != adoption.Evidence.Fingerprint) return journey.DeadlineUtc;
        var original = adoption.Evidence.Journey;
        // Phase/action/accounting may advance normally; immutable limits may not.
        return journey.Version == original.Version && journey.JourneyId == original.JourneyId && journey.OwnedSaveName == original.OwnedSaveName
            && journey.GameVersion == original.GameVersion && journey.IssuedAtUtc == original.IssuedAtUtc
            && journey.HomePlanetId == original.HomePlanetId && journey.HomeStarId == original.HomeStarId
            && journey.DestinationStarId == original.DestinationStarId && journey.RemotePlanetId == original.RemotePlanetId
            && journey.StartedAtGameTick == original.StartedAtGameTick && journey.SpentGameTicks >= original.SpentGameTicks
            && journey.LastAccountedGameTick >= original.LastAccountedGameTick
            && journey.DeadlineUtc == original.DeadlineUtc && journey.MaximumGameTicks == original.MaximumGameTicks
            && journey.ReturnReserveGameTicks == original.ReturnReserveGameTicks
            && journey.ReturnEnergyFloorJoules == original.ReturnEnergyFloorJoules
            && journey.WithdrawalReserveJoules == original.WithdrawalReserveJoules
            && journey.ReturnCoreEnergyMinimumJoules == original.ReturnCoreEnergyMinimumJoules
            && journey.ExtraWarperReserve == original.ExtraWarperReserve
            ? authorization.Proposal.DeadlineUtc : journey.DeadlineUtc;
    }

    internal void SetRemoteResumeAdmissionGuard(Func<VoyagerRemoteResumeAdoption, string?> guard,
        Func<VoyagerRemoteResumeAdoption, string?> preflight, Func<VoyagerRemoteResumeAdoption, string?> universe,
        Func<VoyagerRemoteResumeAdoption, string?> initialization)
    {
        if (_remoteResumeAdmissionGuard is not null) throw new InvalidOperationException("The remote resume guard is already configured.");
        _remoteResumeAdmissionGuard = guard ?? throw new ArgumentNullException(nameof(guard));
        _remoteResumePreflightGuard = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _remoteResumeUniverseGuard = universe ?? throw new ArgumentNullException(nameof(universe));
        _remoteResumeInitializationGuard = initialization ?? throw new ArgumentNullException(nameof(initialization));
    }

    internal void SetRemoteResumeLoadCompleteGuard(Func<string?> guard) =>
        _remoteResumeLoadCompleteGuard = guard ?? throw new ArgumentNullException(nameof(guard));

    internal void PrepareRemoteResumeNativeLoadObserver()
    {
        if (!HasPendingRemoteResumeAdoption || _remoteResumeLoadCompleteGuard is null
            || _remoteResumeLoadSubscription is not null || _remoteResumeLoadersBeforeDispatch is not null)
            throw new InvalidOperationException("remote_resume_native_loader_observer_unavailable");
        _remoteResumeLoadersBeforeDispatch = UnityEngine.Object.FindObjectsOfType<GameLoader>();
    }

    internal void BindRemoteResumeNativeLoaderAfterDispatch()
    {
        var before = _remoteResumeLoadersBeforeDispatch
            ?? throw new InvalidOperationException("remote_resume_native_loader_boundary_missing");
        var loader = NativeLoadCompletionSubscription<GameLoader>.SelectUniqueNew(before,
            UnityEngine.Object.FindObjectsOfType<GameLoader>(),
            candidate => candidate.frame == 0 && candidate.enabled && candidate.gameObject.activeInHierarchy);
        _remoteResumeLoadersBeforeDispatch = null;
        _remoteResumeExpectedLoader = loader;
        _remoteResumeLoadSubscription = new NativeLoadCompletionSubscription<GameLoader>(loader,
            handler => loader.onLoadComplete += handler, handler => loader.onLoadComplete -= handler,
            source => ReferenceEquals(source, loader) && source.frame == 10 && !source.enabled,
            OnRemoteResumeNativeLoadComplete);
    }

    private void OnRemoteResumeNativeLoadComplete(GameLoader source, bool completed, string? captureType)
    {
        // Native disables the exact loader and schedules Destroy before this
        // event. Destroy need not have removed it from Unity searches yet.
        if (!HasPendingRemoteResumeAdoption || !ReferenceEquals(source, _remoteResumeExpectedLoader)) return;
        _remoteResumeCompletedWorld = GameMain.data;
        try
        {
            if (_resumeAdoptionError is not null)
            { FailRemoteResumeAdmission("remote_resume_native_load_previously_unknown"); return; }
            if (!completed)
            {
                FailRemoteResumeAdmission("remote_resume_native_loader_completion_unproved"
                    + (captureType is null ? string.Empty : ":" + captureType));
                return;
            }
            _remoteResumeLoadCompleteObserved = true;
            string? blocker;
            _remoteResumeLoadPreflightRunning = true;
            try { blocker = _remoteResumeLoadCompleteGuard is null ? "remote_resume_native_loader_guard_unavailable"
                : _remoteResumeLoadCompleteGuard(); }
            finally { _remoteResumeLoadPreflightRunning = false; }
            if (blocker == NativeResumeInitializationPolicy.PendingCode)
            {
                if (_remoteResumeAdoption is null || _remoteResumeInitializationGuard is null
                    || !ReferenceEquals(GameMain.data, _remoteResumeAdoption.World) || GameMain.isPaused)
                { FailRemoteResumeAdmission("remote_resume_initialization_pause_binding_unproved"); return; }
                // Native Update still initializes local physics while Pause
                // prevents FixedUpdate from dispatching any player601.
                GameMain.Pause();
                if (!GameMain.isPaused)
                { FailRemoteResumeAdmission("remote_resume_initialization_pause_unproved"); return; }
                _remoteResumeInitializationPending = true;
                return;
            }
            if (blocker is not null) { FailRemoteResumeAdmission(blocker); return; }
            _remoteResumeLoadPreflightPassed = true;
        }
        catch (Exception exception)
        { FailRemoteResumeAdmission("remote_resume_native_loader_callback_failed:" + exception.GetType().Name); }
    }

    internal void RecordRemoteResumeNativeDispatchObservationFailure(string exceptionType)
    {
        // StartGame can publish its loader before throwing. Observe that exact
        // loader, if present, so its completion is paused rather than admitted.
        if (_remoteResumeLoadSubscription is null && _remoteResumeLoadersBeforeDispatch is not null)
        {
            try { BindRemoteResumeNativeLoaderAfterDispatch(); }
            catch (Exception) { } // The accepted action stays unknown; never infer no load.
        }
        FailRemoteResumeAdmission("remote_resume_postdispatch_loader_observation_failed:" + exceptionType);
    }

    internal string? PreflightRemoteResumeBeforeFirstPlayerOnMainThread()
    {
        var blocker = CaptureRemoteResumeAdoption(out var adoption);
        if (blocker is not null) return blocker;
        return _remoteResumePreflightGuard is null ? "remote_resume_loader_strict_preflight_unavailable"
            : _remoteResumePreflightGuard(adoption!);
    }

    internal bool ExactRemoteResumeLoadPreflight(VoyagerRemoteResumeAdoption adoption) =>
        _remoteResumeLoadPreflightRunning && _remoteResumeLoadCompleteObserved && !_remoteResumeLoadPreflightPassed
        && HasPendingRemoteResumeAdoption && ReferenceEquals(_remoteResumeAdoption, adoption)
        && (_remoteResumeInitializationCompleting && _remoteResumeInitializationPending
            || _remoteResumeExpectedLoader != null && _remoteResumeExpectedLoader.frame == 10 && !_remoteResumeExpectedLoader.enabled)
        && ReferenceEquals(GameMain.data, _remoteResumeCompletedWorld) && ReferenceEquals(GameMain.data, adoption.World)
        && ReferenceEquals(GameMain.mainPlayer, adoption.Player) && IsCurrentSessionOwned && _sessionId == adoption.SessionId
        && _remoteResumeJournalConfirmed && _pendingJournalResumeTicket is null && _resumeSourceLease is not null
        && GameMain.gameTick == adoption.Evidence.SourceGameTick
        && _flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence);

    internal bool ExactRemoteResumeInitializationHold(VoyagerRemoteResumeAdoption adoption) =>
        _remoteResumeInitializationPending && !_remoteResumeInitializationCompleting
        && _remoteResumeLoadCompleteObserved && !_remoteResumeLoadPreflightPassed && _resumeAdoptionError is null
        && HasPendingRemoteResumeAdoption && ReferenceEquals(_remoteResumeAdoption, adoption)
        && ReferenceEquals(GameMain.data, _remoteResumeCompletedWorld) && ReferenceEquals(GameMain.data, adoption.World)
        && ReferenceEquals(GameMain.mainPlayer, adoption.Player) && IsCurrentSessionOwned && _sessionId == adoption.SessionId
        && _remoteResumeJournalConfirmed && _pendingJournalResumeTicket is null && _resumeSourceLease is not null
        && GameMain.gameTick == adoption.Evidence.SourceGameTick
        && _flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence);

    internal void PumpRemoteResumeInitializationOnMainThread()
    {
        if (!_remoteResumeInitializationPending || _resumeAdoptionError is not null) return;
        try
        {
            var blocker = CaptureRemoteResumeAdoption(out var adoption);
            if (blocker is null)
                blocker = _remoteResumeInitializationGuard is null ? "remote_resume_initialization_guard_unavailable"
                    : _remoteResumeInitializationGuard(adoption!);
            if (blocker == NativeResumeInitializationPolicy.PendingCode) return;
            if (blocker is not null) { FailRemoteResumeAdmission(blocker); return; }
            if (adoption is null || !ExactRemoteResumeInitializationHold(adoption) || !GameMain.isPaused)
            { FailRemoteResumeAdmission("remote_resume_initialization_resume_binding_unproved"); return; }
            // The unique observed loader completion is retained as a receipt;
            // its destroyed Unity object is never searched for or dereferenced.
            // The fresh source/actor/owner proof authorizes just this Resume.
            _remoteResumeInitializationCompleting = true;
            _remoteResumeLoadPreflightRunning = true;
            try
            {
                GameMain.Resume();
                blocker = GameMain.isPaused ? "remote_resume_initialization_native_resume_unproved"
                    : _remoteResumeLoadCompleteGuard is null ? "remote_resume_native_loader_guard_unavailable"
                    : _remoteResumeLoadCompleteGuard();
            }
            finally
            {
                _remoteResumeLoadPreflightRunning = false;
                _remoteResumeInitializationCompleting = false;
            }
            if (blocker is not null) { FailRemoteResumeAdmission(blocker); return; }
            _remoteResumeInitializationPending = false;
            _remoteResumeLoadPreflightPassed = true;
        }
        catch (Exception exception)
        { FailRemoteResumeAdmission("remote_resume_initialization_callback_failed:" + exception.GetType().Name); }
    }

    internal void RecordRemoteResumeGroundObservation(VoyagerRemoteResumeAdoption adoption,
        NativePlayerGroundContactSnapshot? observation, bool afterSync)
    {
        if (observation is null || !ReferenceEquals(_remoteResumeAdoption, adoption)) return;
        _remoteResumeGroundDiagnosticAdoption = adoption;
        if (afterSync) adoption.GroundContactPostSync ??= observation.Copy();
        else adoption.GroundContactPreSync ??= observation.Copy();
    }

    internal void CopyRemoteResumeGroundDiagnostics(Player player, NativePlayerPhysicsSnapshot result)
    {
        var a = _remoteResumeGroundDiagnosticAdoption;
        if (a is null || !IsCurrentSessionOwned || _sessionId != a.SessionId
            || !ReferenceEquals(GameMain.data, a.World) || !ReferenceEquals(player, a.Player))
        { _remoteResumeGroundDiagnosticAdoption = null; return; }
        result.ResumeGroundContactPreSync = a.GroundContactPreSync?.Copy();
        result.ResumeGroundContactPostSync = a.GroundContactPostSync?.Copy();
        result.ResumeTerrainPreFirstFrame = a.TerrainPreFirstFrame?.Copy();
        result.ResumeTerrainPostUniverse = a.TerrainPostUniverse?.Copy();
        result.ResumeGroundContactPostUniverse = a.GroundContactPostUniverse?.Copy();
        result.ResumeNearSupportPreFirstFrame = a.NearSupportPreFirstFrame?.Copy();
        result.ResumeNearSupportPostUniverse = a.NearSupportPostUniverse?.Copy();
        result.ResumeNearSupportLatest = a.NearSupportLatest?.Copy();
        result.ResumeFirstFailure = a.FirstFailure?.Copy();
    }

    internal void RecordRemoteResumeTerrain(VoyagerRemoteResumeAdoption adoption,
        NativePlanetTerrainPhysicsSnapshot terrain, bool postUniverse, NativePlayerGroundContactSnapshot? ground = null)
    {
        if (!ReferenceEquals(_remoteResumeAdoption, adoption)) return;
        _remoteResumeGroundDiagnosticAdoption = adoption;
        if (postUniverse)
        { adoption.TerrainPostUniverse ??= terrain.Copy(); adoption.GroundContactPostUniverse ??= ground?.Copy(); }
        else adoption.TerrainPreFirstFrame ??= terrain.Copy();
    }

    internal bool ObserveRemoteResumeUniverseOnMainThread()
    {
        if (_remoteResumeAdoption is not null && (_resumeAdoptionError is not null || _remoteResumeAdoption.FirstFrame.Failure is not null))
            return FailRemoteResumeAdmission("remote_resume_failed_adoption_is_terminal");
        if (!HasPendingRemoteResumeAdoption || _remoteResumeAdoption?.FirstFrame is not { Armed: true } frame) return true;
        if (frame.PhysicsObserved && GameMain.gameTick > frame.SourceGameTick + 1 && !_remoteResumeAdoption.NativeContactPending) return true;
        var blocker = CaptureRemoteResumeAdoption(out var adoption);
        if (blocker is null)
            blocker = _remoteResumeUniverseGuard is null ? "remote_resume_universe_guard_unavailable" : _remoteResumeUniverseGuard(adoption!);
        return blocker is null || FailRemoteResumeAdmission(blocker);
    }

    internal void StopPendingRemoteFirstFrameOnMainThread()
    {
        if (HasPendingRemoteResumeAdoption
            && (_remoteResumeInitializationPending || _remoteResumeAdoption?.FirstFrame.Armed == true))
            FailRemoteResumeAdmission("remote_resume_first_frame_native_observer_lost");
    }

    private string? CaptureRemoteResumeAdoption(out VoyagerRemoteResumeAdoption? adoption)
    {
        adoption = null;
        if (_resumeAdoptionError is not null || _remoteResumeAdoption?.FirstFrame.Failure is not null)
            return "remote_resume_failed_adoption_is_terminal";
        var evidence = _pendingRemoteResumeEvidence;
        if (evidence is null || !_remoteResumeLoadCompleteObserved || !IsCurrentSessionOwned
            || GameMain.mainPlayer is null || !ReferenceEquals(GameMain.data, _remoteResumeCompletedWorld))
            return "remote_resume_native_loader_ownership_unproved";
        if (!_remoteResumeJournalConfirmed || _pendingJournalResumeTicket is not null)
            return "remote_resume_original_journal_not_confirmed";
        if (!_flightCheckpoints.RemoteResumeEvidenceStillMatches(evidence)
            || GameMain.gameTick < evidence.SourceGameTick || string.IsNullOrEmpty(_sessionId)
            || !ReferenceEquals(GameMain.data, _ownedData))
            return "remote_resume_original_runtime_proof_changed";
        _remoteResumeAdoption ??= new VoyagerRemoteResumeAdoption(_pendingRemoteResumeActionId!,
            _sessionId!, _ownedData!, GameMain.mainPlayer, evidence, _pendingRecoveryAuthorization);
        adoption = _remoteResumeAdoption;
        return null;
    }

    internal bool TryAdmitRemoteResumeOnMainThread()
    {
        if (_remoteResumeAdoption is not null && (_resumeAdoptionError is not null || _remoteResumeAdoption.FirstFrame.Failure is not null))
            return FailRemoteResumeAdmission("remote_resume_failed_adoption_is_terminal");
        if (_pendingRemoteResumeEvidence is null || _remoteResumeAdmitted) return true;
        if (!_remoteResumeLoadCompleteObserved || !_remoteResumeLoadPreflightPassed)
            return FailRemoteResumeAdmission("remote_resume_before_first_player_preflight_unproved");
        if (!IsCurrentSessionOwned || GameMain.mainPlayer is null)
        {
            // No authority over a replacement/unadopted world. Its ordinary
            // provenance checks must finish before any idle owner is published.
            return false;
        }
        var evidence = _pendingRemoteResumeEvidence;
        var admissionBlocker = CaptureRemoteResumeAdoption(out var adoption);
        if (admissionBlocker is not null) return FailRemoteResumeAdmission(admissionBlocker);
        if (adoption!.FirstFrame is { Armed: true } first
            && (!first.PhysicsObserved || GameMain.gameTick <= first.SourceGameTick + 1))
        {
            adoption.CurrentDecisionEvidence = new NativeResumeDecisionEvidence("native_physics_post501",
                GameMain.gameTick, DateTimeOffset.UtcNow, true);
            adoption.CurrentDecisionEvidence.Observe("native_paused", GameMain.isPaused);
            adoption.CurrentDecisionEvidence.Number("first_frame_source_tick", first.SourceGameTick);
            var phaseBlocker = first.ObservePhysics(GameMain.gameTick, !GameMain.isPaused
                && ReferenceEquals(GameMain.data, adoption.World) && _sessionId == adoption.SessionId);
            if (phaseBlocker is not null)
            {
                adoption.RetainFailure(adoption.CurrentDecisionEvidence.Failure(phaseBlocker, first.FailureDetail));
                return FailRemoteResumeAdmission(phaseBlocker);
            }
        }
        string? blocker;
        try { blocker = _remoteResumeAdmissionGuard is null ? "remote_resume_idle_guard_unavailable" : _remoteResumeAdmissionGuard(adoption!); }
        catch (Exception exception) { blocker = "remote_resume_idle_guard_capture_failed:" + exception.GetType().Name; }
        return CompleteRemoteResumeAdmission(blocker, evidence);
    }

    private bool RemoteResumeAllowsPrimaryResave()
    {
        if (_remoteResumeAdoption is not null && (_resumeAdoptionError is not null || _remoteResumeAdoption.FirstFrame.Failure is not null))
            return false;
        if (HasPendingRemoteResumeAdoption) return false;
        if (_remoteResumeAdoption is null || _ownedSaveState != OwnedSaveStates.WaitingToSave) return true;
        if (!_remoteResumeAdmitted || !IsCurrentSessionOwned || !ReferenceEquals(GameMain.data, _remoteResumeAdoption.World)
            || _sessionId != _remoteResumeAdoption.SessionId)
            return FailRemoteResumeAdmission("remote_resume_primary_resave_binding_unproved");
        string? blocker;
        try { blocker = _remoteResumeAdmissionGuard is null ? "remote_resume_idle_guard_unavailable" : _remoteResumeAdmissionGuard(_remoteResumeAdoption); }
        catch (Exception exception) { blocker = "remote_resume_resave_capture_failed:" + exception.GetType().Name; }
        return blocker is null || FailRemoteResumeAdmission(blocker);
    }

    private bool FailRemoteResumeAdmission(string code)
    {
        _remoteResumeInitializationPending = false;
        if (_remoteResumeAdoption is { } failureAdoption)
            failureAdoption.RetainFailure(NativeResumeDecisionEvidence.UnobservedFailure(code,
                "session_admission_without_native_sample", GameMain.gameTick, DateTimeOffset.UtcNow,
                failureAdoption.FirstFrame.FailureDetail ?? code));
        if (_remoteResumeAdoption?.FirstFrame.Armed == true) _remoteResumeAdoption.FirstFrame.Fail(code);
        var reason = "The exact remote LastExit continuation could not enter its strict idle guard. [" + code + "]";
        if (_remoteResumeAdoption?.FirstFrame.FailureDetail is { } detail)
            reason += " First failed premise: " + detail + ".";
        var exactLoadedCandidate = _expectedResumeTicket is { } expected && _resumeSourceLease is not null
            && ReferenceEquals(GameMain.data, _remoteResumeCompletedWorld) && GameMain.data is { } loaded
            && TryValidateResumeCandidate(loaded, expected, _resumeSourceLease.Prefix.GameTick, out _, out _);
        if (GameMain.isRunning && (IsCurrentSessionOwned && ReferenceEquals(GameMain.data, _ownedData) || exactLoadedCandidate))
        {
            try
            {
                GameMain.Pause();
                reason += GameMain.isPaused ? " Native Pause verified; this does not prove physical idle or authorize automatic Resume."
                    : " Native Pause could not be verified.";
            }
            catch (Exception exception) { reason += " Native Pause failed (" + exception.GetType().Name + ")."; }
            if (IsCurrentSessionOwned)
                QuarantineWritesOnMainThread(_pendingRemoteResumeActionId ?? _remoteResumeAdoption?.ResumeActionId ?? string.Empty, reason);
        }
        _resumeAdoptionError ??= reason;
        return false;
    }

    private void CancelRemoteResumeAdmission()
    {
        if (_pendingRemoteResumeEvidence is not null && IsCurrentSessionOwned)
            FailRemoteResumeAdmission("remote_resume_adoption_interrupted");
        _pendingRemoteResumeEvidence?.Dispose();
        _pendingRemoteResumeEvidence = null;
        _pendingRemoteResumeActionId = null;
        _remoteResumeAdoption = null;
        _remoteResumeGroundDiagnosticAdoption = null; // Session/Host cancellation ends diagnostic ownership too.
        _remoteResumeAdmitted = false;
        _remoteResumeJournalConfirmed = false;
        _remoteResumeLoadSubscription?.Dispose();
        _remoteResumeLoadSubscription = null;
        _remoteResumeExpectedLoader = null;
        _remoteResumeLoadersBeforeDispatch = null;
        _remoteResumeCompletedWorld = null;
        _remoteResumeLoadCompleteObserved = false;
        _remoteResumeLoadPreflightPassed = false;
        _remoteResumeLoadPreflightRunning = false;
        _remoteResumeInitializationPending = false;
        _remoteResumeInitializationCompleting = false;
        _pendingRecoveryAuthorization = null;
    }
}

// Published once, after real ownership and the original Journal checkpoint.
// This is a new resume action's runtime proof, never a fabricated old action.
internal sealed class VoyagerRemoteResumeAdoption
{
    public VoyagerRemoteResumeAdoption(string resumeActionId, string sessionId, GameData world,
        Player player, VoyagerRemoteResumeEvidence evidence, VoyagerRecoveryAuthorization? authorization)
    {
        ResumeActionId = resumeActionId; SessionId = sessionId; World = world; Player = player; Evidence = evidence; Authorization = authorization;
    }
    public string ResumeActionId { get; }
    public OwnedWorldResumeSourceKind SourceKind => OwnedWorldResumeSourceKind.LastExit;
    public string SessionId { get; }
    public GameData World { get; }
    public Player Player { get; }
    public VoyagerRemoteResumeEvidence Evidence { get; }
    public VoyagerRecoveryAuthorization? Authorization { get; }
    public long? ChargeStartedGameTick { get; set; }
    public DateTimeOffset? ChargeStartedUtc { get; set; }
    public string? ChargeCostEnvelope { get; set; }
    public double? ChargePerTickUpper { get; set; }
    public double? ChargeGenerationLowerBoundWatts { get; set; }
    public NativeLoadedTransformSyncAttempt GroundSync { get; } = new();
    public NativePlayerGroundContactSnapshot? GroundContactPreSync { get; set; }
    public NativePlayerGroundContactSnapshot? GroundContactPostSync { get; set; }
    public NativeFirstFrameAdmission FirstFrame { get; } = new();
    public NativePlanetTerrainPhysicsSnapshot? TerrainPreFirstFrame { get; set; }
    public NativePlanetTerrainPhysicsSnapshot? TerrainPostUniverse { get; set; }
    public NativePlayerGroundContactSnapshot? GroundContactPostUniverse { get; set; }
    public NativeNearGroundSupportSnapshot? NearSupportPreFirstFrame { get; set; }
    public NativeNearGroundSupportSnapshot? NearSupportPostUniverse { get; set; }
    public NativeNearGroundSupportSnapshot? NearSupportLatest { get; set; }
    public bool NativeContactPending { get; set; }
    public NativeResumeDecisionEvidence? CurrentDecisionEvidence { get; set; }
    public NativeResumeFailureSnapshot? FirstFailure { get; private set; }
    public void RetainFailure(NativeResumeFailureSnapshot? evidence)
    {
        if (FirstFailure is null && evidence is not null) FirstFailure = evidence.Copy();
    }
}
