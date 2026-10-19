using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
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
    private Func<string?>? _remoteResumeLoadCompleteGuard;
    private GameLoader[]? _remoteResumeLoadersBeforeDispatch;
    private NativeLoadCompletionSubscription<GameLoader>? _remoteResumeLoadSubscription;
    private GameLoader? _remoteResumeExpectedLoader;
    private GameData? _remoteResumeCompletedWorld;
    private bool _remoteResumeLoadCompleteObserved;
    private bool _remoteResumeLoadPreflightPassed;

    internal bool RequiresRemoteResumeEvidence => _flightCheckpoints.VoyagerJourney?.Phase == "remote_landed"
        || _flightCheckpoints.VoyagerJourneyLoadError is not null;
    internal bool HasPendingRemoteResumeAdoption => _pendingRemoteResumeEvidence is not null && !_remoteResumeAdmitted;
    internal string CurrentNativeGameVersion => _gameVersion;

    internal bool TryOpenRemoteResumeEvidence(OwnedWorldResumeTicket ticket, OwnedSaveRecoveryLease source,
        OwnedSaveRecoveryLease primary, out VoyagerRemoteResumeEvidence? evidence, out string rejection,
        VoyagerRecoveryWindowProposal? proposal = null) =>
        _flightCheckpoints.TryOpenRemoteResumeEvidence(ticket, source, primary, out evidence, out rejection, proposal);

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
        Func<VoyagerRemoteResumeAdoption, string?> preflight)
    {
        if (_remoteResumeAdmissionGuard is not null) throw new InvalidOperationException("The remote resume guard is already configured.");
        _remoteResumeAdmissionGuard = guard ?? throw new ArgumentNullException(nameof(guard));
        _remoteResumePreflightGuard = preflight ?? throw new ArgumentNullException(nameof(preflight));
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
            var blocker = _remoteResumeLoadCompleteGuard is null ? "remote_resume_native_loader_guard_unavailable"
                : _remoteResumeLoadCompleteGuard();
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

    private string? CaptureRemoteResumeAdoption(out VoyagerRemoteResumeAdoption? adoption)
    {
        adoption = null;
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
        string? blocker;
        try { blocker = _remoteResumeAdmissionGuard is null ? "remote_resume_idle_guard_unavailable" : _remoteResumeAdmissionGuard(adoption!); }
        catch (Exception exception) { blocker = "remote_resume_idle_guard_capture_failed:" + exception.GetType().Name; }
        if (blocker == VoyagerRecoveryWindowPolicy.ChargingPending) return false;
        if (blocker is not null) return FailRemoteResumeAdmission(blocker);
        _remoteResumeAdmitted = true;
        // The journal has been confirmed and the exact first idle owner now
        // guards601. Only now may source/primary/metadata leases be released.
        ReleaseResumeSourceLease();
        evidence.Dispose();
        _pendingRemoteResumeEvidence = null;
        return true;
    }

    private bool RemoteResumeAllowsPrimaryResave()
    {
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
        var reason = "The exact remote LastExit continuation could not enter its strict idle guard. [" + code + "]";
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
        _remoteResumeAdmitted = false;
        _remoteResumeJournalConfirmed = false;
        _remoteResumeLoadSubscription?.Dispose();
        _remoteResumeLoadSubscription = null;
        _remoteResumeExpectedLoader = null;
        _remoteResumeLoadersBeforeDispatch = null;
        _remoteResumeCompletedWorld = null;
        _remoteResumeLoadCompleteObserved = false;
        _remoteResumeLoadPreflightPassed = false;
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
}
