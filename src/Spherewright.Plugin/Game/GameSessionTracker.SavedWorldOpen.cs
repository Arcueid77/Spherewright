using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private SavedWorldOpenGuard? _savedWorldOpen;
    private long? _savedOpenPreparedKnownProgressGameTick;
    private long? _savedOpenObservedProgressFloor;
    private GameLoader[]? _savedOpenLoadersBefore;
    private GameLoader? _savedOpenLoader;
    private NativeLoadCompletionSubscription<GameLoader>? _savedOpenSubscription;
    private bool _savedOpenAnyNativeLocation;
    private PausedSavedSpacePoseGuard? _savedSpacePose;
    private Spherewright.Contracts.Players.NativeResumeFailureSnapshot? _savedOpenFailure;
    internal string? SavedWorldOpenSessionId { get; private set; }
    internal Spherewright.Contracts.Players.NativeResumeFailureSnapshot? SavedWorldOpenFirstFailure =>
        (_savedOpenFailure ?? _savedWorldOpen?.FirstFailure)?.Copy();

    internal bool SavedWorldOpenCompleted(string actionId) =>
        _savedWorldOpen is { Completed: true } guard && guard.ActionId == actionId;

    internal bool TryGetPausedSavedSpacePermit(out PausedSavedSpacePermit? permit)
    {
        permit=null;var p=GameMain.mainPlayer;
        if(!_savedOpenAnyNativeLocation || _savedWorldOpen is not {Completed:true} open
            || !open.MatchesCompletedWorld(GameMain.data) || !GameMain.isPaused || !GameMain.isRunning
            || GameMain.gameTick!=open.SavedTick || GameMain.localPlanet is not null || p?.controller is null
            || p.planetId!=0 || p.movementState!=EMovementState.Sail || !p.isAlive || p.warping
            || p.warpState!=0 || p.warpCommand || p.currentOrder is not null || p.orders?.orderCount!=0
            || _savedOpenFailure is not null || _savedSpacePose?.Matches(GameMain.data,p,p.controller,GameMain.gameTick,
                p.movementState.ToString(),CaptureSavedSpacePose(p))!=true
            || _writeQuarantineActionId!=open.ActionId || _writeQuarantineReason is null || _sessionId is null)return false;
        var candidate=new PausedSavedSpacePermit(_sessionId,open.ActionId,_writeQuarantineReason,open.SavedTick);
        if(!candidate.Matches(CaptureOnMainThread()))return false;
        permit=candidate;return true;
    }

    internal string? FrozenSavedSpacePoseFingerprint=>TryGetPausedSavedSpacePermit(out _)?_savedSpacePose?.Fingerprint:null;
    internal bool StopPausedSavedSpaceExecution(string actionId,object world,PausedSavedSpacePermit permit)
    {
        if(_savedWorldOpen?.MatchesCompletedWorld(world)!=true || !ReferenceEquals(GameMain.data,world)
            || _sessionId!=permit.SessionId || _savedWorldOpen.ActionId!=permit.OpenActionId)return false;
        _savedSpacePose?.Invalidate();
        _savedOpenFailure??=NativeResumeDecisionEvidence.UnobservedFailure(
            Spherewright.Contracts.Errors.BridgeErrorCodes.ActionOutcomeUnknown,"paused_saved_space_inventory",
            GameMain.gameTick,DateTimeOffset.UtcNow,"original_native_inventory_outcome_reconciled");
        _savedOpenFailure.Observations["original_action_id"]=actionId;
        return true;
    }
    private static Spherewright.Contracts.Players.NativeSpaceTransitSnapshot CaptureSavedSpacePose(Player p)=>new() {
        StarId=GameMain.localStar?.id,NativePaused=GameMain.isPaused,
        UniverseX=p.uPosition.x,UniverseY=p.uPosition.y,UniverseZ=p.uPosition.z,
        VelocityX=p.uVelocity.x,VelocityY=p.uVelocity.y,VelocityZ=p.uVelocity.z,Speed=p.uVelocity.magnitude,
        WarpState=p.warpState,WarpCommand=p.warpCommand,Warping=p.warping };

    internal bool TryGetStoppedSpaceHomeAnchor(out int home,out string hash,out long cap)
    {
        home=0;hash=string.Empty;cap=0;
        var j=_flightCheckpoints.VoyagerJourney;
        if(!TryGetPausedSavedSpacePermit(out var permit) || j is null || j.Phase!="recovery_required"
            || j.HomeStarId!=GameMain.localStar?.id || j.OwnedSaveName!=_ownedSaveName || j.GameVersion!=_gameVersion
            || !Guid.TryParse(j.LastAccountedFlightActionId,out _) || j.ReturnReserveGameTicks<3600)return false;
        var target=GameMain.galaxy?.PlanetById(j.HomePlanetId);
        if(target is null || target.star.id!=j.HomeStarId || target.type==EPlanetType.Gas)return false;
        var spent=Math.Max(j.SpentGameTicks,GameMain.gameTick-j.StartedAtGameTick);
        cap=Math.Min(j.ReturnReserveGameTicks,j.MaximumGameTicks-spent);
        if(cap<3600)return false;
        home=j.HomePlanetId;
        hash=CanonicalStateHash.Combine("paused-space-home-anchor-v1",permit!.SessionId,permit.OpenActionId,
            permit.SavedTick,Spherewright.Plugin.Transport.PluginJson.Serialize(j),home,target.uPosition.x,target.uPosition.y,target.uPosition.z);
        return true;
    }

    internal bool CompleteStoppedSpaceHomeAdmission(string sourceSession,object world,string openAction,string reason,int home)
    {
        if(_savedWorldOpen is not {Completed:true} source || !source.MatchesCompletedWorld(world)
            || !ReferenceEquals(world,GameMain.data) || _sessionId!=sourceSession || source.ActionId!=openAction
            || _writeQuarantineActionId!=openAction || _writeHealth!=WriteHealthStates.Quarantined
            || _writeQuarantineReason!=reason || CaptureOnMainThread().WriteBlockers.Count!=1
            || GameMain.localPlanet?.id!=home || GameMain.mainPlayer?.planetId!=home)return false;
        _writeHealth=WriteHealthStates.Healthy;_writeQuarantineActionId=null;_writeQuarantineReason=null;
        return true;
    }

    internal bool StoppedSpaceHomeSaveCredentialMatches(string session,int home,long savedTick)=>
        _resumeTickets.CurrentResumeToken is {} token && _resumeTickets.TryGetActiveTicket(token,out var ticket,out _)
        && ticket is not null && ticket.SavedWorldOpenOnly!=true && ticket.OwnedSaveName==_ownedSaveName
        && ticket.SourceSessionId==session && ticket.ExpectedPlanetId==home && ticket.MinimumGameTick==savedTick;

    internal void PrepareSavedWorldOpenNativeObserver(bool exactLastExitOpen=false)
    {
        if (_savedWorldOpen is null || _savedOpenLoadersBefore is not null || _savedOpenSubscription is not null)
            throw new InvalidOperationException("saved_world_open_observer_unavailable");
        _savedOpenLoadersBefore = UnityEngine.Object.FindObjectsOfType<GameLoader>();
        _savedOpenAnyNativeLocation=exactLastExitOpen;
    }

    internal void BindSavedWorldOpenNativeObserver()
    {
        var loader = NativeLoadCompletionSubscription<GameLoader>.SelectUniqueNew(
            _savedOpenLoadersBefore ?? throw new InvalidOperationException("saved_world_open_dispatch_boundary_missing"),
            UnityEngine.Object.FindObjectsOfType<GameLoader>(),
            candidate => candidate.frame == 0 && candidate.enabled && candidate.gameObject.activeInHierarchy);
        _savedOpenLoadersBefore = null;
        _savedOpenLoader = loader;
        _savedOpenSubscription = new NativeLoadCompletionSubscription<GameLoader>(loader,
            handler => loader.onLoadComplete += handler, handler => loader.onLoadComplete -= handler,
            source => ReferenceEquals(source, loader) && source.frame == 10 && !source.enabled,
            OnSavedWorldOpenLoadComplete);
    }

    private void OnSavedWorldOpenLoadComplete(GameLoader loader, bool completed, string? captureType)
    {
        if (_savedWorldOpen is null || !ReferenceEquals(loader, _savedOpenLoader)) return;
        try
        {
            // Supported Native Pause at the exact loader event. No player601,
            // contact/charge/idle claim, physics write or task Resume is needed.
            GameMain.Pause();
            var ticket = _expectedResumeTicket;
            if (!_savedWorldOpen.ObserveLoad(GameMain.data, completed && captureType is null, GameMain.isPaused,
                GameMain.gameTick, ticket is null || GameMain.data is null ? null
                    : OwnedWorldProvenancePolicy.MatchesProtectedSaveIdentity(ticket.OwnedSaveName, GameMain.data.gameName),
                _savedOpenAnyNativeLocation ? ObserveConsistentSavedNativeLocation()
                    : ticket is null || GameMain.data?.localPlanet is null ? null : GameMain.data.localPlanet.id == ticket.ExpectedPlanetId))
                _resumeAdoptionError = _savedWorldOpen.FirstFailure?.Code ?? "saved_world_open_native_boundary_unproved";
            else if(_savedOpenAnyNativeLocation && GameMain.localPlanet is null && GameMain.mainPlayer is {} spacePlayer)
                _savedSpacePose=PausedSavedSpacePoseGuard.Capture(GameMain.data,spacePlayer,spacePlayer.controller,
                    _savedWorldOpen.SavedTick,GameMain.gameTick,spacePlayer.movementState.ToString(),CaptureSavedSpacePose(spacePlayer));
        }
        catch (Exception exception)
        {
            _resumeAdoptionError ??= "saved_world_open_load_callback_failed:" + exception.GetType().Name;
            RecordSavedOpenUnobservedFailure(_resumeAdoptionError, "saved_world_open_load_complete", "native_pause_call_completed");
        }
    }

    internal void RecordSavedWorldOpenDispatchFailure(string exceptionType)
    {
        if (_savedOpenSubscription is null && _savedOpenLoadersBefore is not null)
            try { BindSavedWorldOpenNativeObserver(); } catch (Exception) { }
        _resumeAdoptionError ??= "saved_world_open_dispatch_outcome_unknown:" + exceptionType;
        RecordSavedOpenUnobservedFailure(_resumeAdoptionError, "saved_world_open_dispatch", "native_dispatch_outcome_known");
    }

    private void StopPreviousAutomationForSavedWorldOpen()
    {
        if (_savedWorldOpen is null) return;
        _writeHealth = WriteHealthStates.Quarantined;
        _writeQuarantineActionId = _savedWorldOpen.ActionId;
        _writeQuarantineReason = OwnedSavedWorldOpenPolicy.AutomationStoppedCode
            + ": The saved world is open. Previous automatic tasks remain stopped; fresh execution authority and Native checks are required.";
    }

    private void ConfirmSavedWorldOpenJournal(OwnedWorldResumeTicket ticket, OwnedWorldGameplayJournalCheckpoint checkpoint)
    {
        var guard = _savedWorldOpen!;
        _pendingJournalResumeTicket = null;
        ConfirmedPlannedResumeMinimumGameTick = null;
        ReleaseResumeSourceLease();
        if (!guard.ConfirmJournal(GameMain.data, GameMain.gameTick, true, () =>
        {
            _resumeTickets.ArmFromOpenedSavedWorld(ticket.OwnedSaveName, _sessionId!, ticket.ExpectedPlanetId,
                guard.SavedTick, guard.ActionId, checkpoint, ticket, _savedOpenPreparedKnownProgressGameTick);
            if (!_resumeTickets.TryGetSavedWorldOpenProvenance(_resumeTickets.CurrentResumeToken!, out var opened, out _, out _)
                || opened?.SavedWorldOpenOnly != true || opened.MinimumGameTick != guard.SavedTick
                || opened.SourceSessionId != _sessionId)
                throw new InvalidOperationException("saved_world_open_access_credential_unproved");
        }))
        { _resumeAdoptionError ??= guard.FirstFailure?.Code ?? "saved_world_open_journal_adoption_unproved"; return; }
        _ownedSaveState = OwnedSaveStates.Saved;
        _lastOwnedSaveGameTick = guard.SavedTick;
        SavedWorldOpenSessionId = _sessionId;
        StopPreviousAutomationForSavedWorldOpen();
    }

    private void RecordSavedOpenUnobservedFailure(string code, string stage, string predicate)
    {
        _savedOpenFailure ??= _savedWorldOpen?.FirstFailure ?? new Spherewright.Contracts.Players.NativeResumeFailureSnapshot
        {
            Code = code, Stage = stage, FirstFailedPredicate = predicate, FactsState = "not_observed",
            CapturedAtUtc = DateTimeOffset.UtcNow, CapturedAtGameTick = null,
        };
    }

    private void ResetSavedWorldOpen(bool clearFailure = false)
    {
        if (clearFailure) _savedOpenFailure = null;
        else _savedOpenFailure ??= _savedWorldOpen?.FirstFailure;
        _savedOpenSubscription?.Dispose(); _savedOpenSubscription = null;
        _savedOpenLoader = null; _savedOpenLoadersBefore = null; _savedWorldOpen = null;
        SavedWorldOpenSessionId = null;
        _savedOpenPreparedKnownProgressGameTick = null; _savedOpenObservedProgressFloor = null;
        _savedOpenAnyNativeLocation=false;
        _savedSpacePose=null;
    }

    private static bool? ObserveConsistentSavedNativeLocation()
    {
        var world=GameMain.data;var player=GameMain.mainPlayer;
        if(world is null || player is null)return null;
        if(world.localPlanet is {} planet)return planet.id>0 && player.planetId==planet.id;
        return player.planetId==0 && player.movementState==EMovementState.Sail && !player.warping
            && player.warpState==0f && !player.warpCommand
            && !double.IsNaN(player.uPosition.x) && !double.IsInfinity(player.uPosition.x)
            && !double.IsNaN(player.uPosition.y) && !double.IsInfinity(player.uPosition.y)
            && !double.IsNaN(player.uPosition.z) && !double.IsInfinity(player.uPosition.z);
    }

    private long? UnaccountedObservedProgressFloorForTicket()
    {
        var floor = _savedOpenObservedProgressFloor;
        if (!_remoteResumeAdmitted && _remoteResumeAdoption?.Evidence.RemotePrimaryRecovery == true
            && _remoteResumeAdoption.Evidence.KnownPrimaryProgressTick is { } pending)
            floor = Math.Max(floor ?? pending, pending);
        return floor > GameMain.gameTick ? floor : null;
    }
}
