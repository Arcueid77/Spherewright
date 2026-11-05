using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private Func<bool>? _stoppedSpaceObserverProof;
    internal void SetStoppedSpaceObserverProof(Func<bool> proof)=>_stoppedSpaceObserverProof=proof;

    private GameCallResult<PreparedNormalAction> PrepareStoppedSpaceHomeReturn(string? sessionId,PrepareInterplanetaryFlightRequest request)
    {
        var s=_sessions.CaptureOnMainThread();
        if(request.StateHashVersion!=StateHashVersion || sessionId!=s.SessionId
            || !_sessions.TryGetPausedSavedSpacePermit(out var permit)
            || !_sessions.TryGetStoppedSpaceHomeAnchor(out var home,out var anchor,out var cap)
            || request.DestinationPlanetId!=home || request.ExpectedStarSystemStateHash!=anchor
            || _stoppedSpaceObserverProof?.Invoke()!=true)
            return InvalidPlan("Only the exact paused saved-space source and its fresh original-home anchor admit this return.");
        var observed=_reader.GetPlayerStateOnMainThread(sessionId,new LocalPlanetRequest {PlanetId=0});
        if(!observed.Success || observed.Value is null)return GameCallResult<PreparedNormalAction>.Failed(observed.Error!);
        if(observed.Value.StateHash!=request.ExpectedPlayerStateHash)return StalePlan("The paused space player changed.");
        var player=GameMain.mainPlayer;var target=GameMain.galaxy.PlanetById(home);var j=_flightCheckpoints.VoyagerJourney!;
        if(player?.mecha is null || player.controller?.actionSail is null || target is null
            || request.StoppedSpaceReturnMaximumGameTicks>cap || !VoyagerMechaWorkIdle(player) || !BuildUiIsIdle(player))
            return InvalidPlan("Original-home target, idle mecha, controller and original return reserve must be exact.");
        var distance=Math.Max(0,(target.uPosition-player.uPosition).magnitude-target.realRadius);
        var next=StoppedSpaceNextControlUpper(player,home,j.WithdrawalReserveJoules);
        var budget=StoppedSpaceReturnBudgetPolicy.Evaluate(new StoppedSpaceReturnBudgetFacts {
            SourceTick=GameMain.gameTick,JourneyStartedTick=j.StartedAtGameTick,RecordedSpentTicks=j.SpentGameTicks,
            JourneyMaximumTicks=j.MaximumGameTicks,OriginalReturnTicks=j.ReturnReserveGameTicks,
            RequestedTicks=request.StoppedSpaceReturnMaximumGameTicks,RequestedWallSeconds=request.StoppedSpaceReturnMaximumWallSeconds,
            Distance=distance,SailSpeed=Math.Min(1000d,player.mecha.maxSailSpeed),Core=player.mecha.coreEnergy,
            CoreCapacity=player.mecha.coreEnergyCap,MinimumCoreRatio=request.MinimumCoreEnergyRatio,
            OriginalReturnCoreMinimum=j.ReturnCoreEnergyMinimumJoules,LoadedEnergy=CalculateAvailableFlightEnergy(player.mecha),
            WithdrawalFloor=j.WithdrawalReserveJoules,NextTickEnergyUpper=next });
        if(!budget.Proved)return InvalidPlan(budget.Blocker!);
        var hash=CanonicalStateHash.Combine("stopped-space-home-return-v1",observed.Value.StateHash,anchor,
            request.StoppedSpaceReturnMaximumGameTicks,request.StoppedSpaceReturnMaximumWallSeconds,
            request.MinimumCoreEnergyRatio,budget.RequiredFlightEnergy,next);
        var payload=NormalActionPlanPayload.InterplanetaryFlight(sessionId!,0,home,hash,observed.Value.StateHash,
            anchor,distance,request.MinimumCoreEnergyRatio,budget.RequiredFlightEnergy);
        payload.StoppedSpaceHomeReturn=new StoppedSpaceHomeBinding {
            World=GameMain.data,Player=player,Controller=player.controller,Permit=permit!,Home=home,Star=j.HomeStarId,
            OriginalLedger=PluginJson.Serialize(j),OriginalFailedAction=j.LastAccountedFlightActionId,
            MaximumTicks=request.StoppedSpaceReturnMaximumGameTicks,
            MaximumWall=request.StoppedSpaceReturnMaximumWallSeconds,PriorSpent=budget.ConservedPriorSpent,
            WithdrawalFloor=j.WithdrawalReserveJoules,NextTickUpper=next,
            MinimumStartCore=Math.Max(j.ReturnCoreEnergyMinimumJoules,Math.Max(player.mecha.coreEnergyCap*request.MinimumCoreEnergyRatio,2*next)),
            StartingWarpers=CountVoyagerWarpers(player.mecha.warpStorage) };
        return AddPreparedPlan(payload,s,request.StoppedSpaceReturnMaximumGameTicks,
            "One same-star native Sail return to the original home only. No warp, checkpoint load, old action replay or reserve renewal. Strict landing and a complete normal save precede healthy handoff.");
    }

    private BridgeError? RevalidateStoppedSpaceHomeReturn(NormalActionPlanPayload plan)
    {
        var b=plan.StoppedSpaceHomeReturn;
        if(b is null || !StoppedSpaceBindingMatches(b,plan.SessionId) || !_sessions.TryGetPausedSavedSpacePermit(out var permit)
            || permit!.OpenActionId!=b.Permit.OpenActionId || !b.Permit.Matches(_sessions.CaptureOnMainThread())
            || !_sessions.TryGetStoppedSpaceHomeAnchor(out var home,out var anchor,out _)
            || home!=b.Home || anchor!=plan.StarSystemStateHash || _stoppedSpaceObserverProof?.Invoke()!=true
            || PluginJson.Serialize(_flightCheckpoints.VoyagerJourney)!=b.OriginalLedger)
            return Stale("Paused source, exact original journey, native observer or original-home anchor changed.");
        var p=_reader.GetPlayerStateOnMainThread(plan.SessionId,new LocalPlanetRequest {PlanetId=0});
        if(!p.Success || p.Value is null || p.Value.StateHash!=plan.PlayerStateHash
            || b.Player.mecha.coreEnergy<b.MinimumStartCore
            || CalculateAvailableFlightEnergy(b.Player.mecha)<plan.RequiredFlightEnergy+b.WithdrawalFloor+2*b.NextTickUpper)
            return Stale("Fresh space source or closed funded return envelope changed.");
        return null;
    }

    private static double StoppedSpaceNextControlUpper(Player p,int home,double floor)
    {
        // Share the existing researched Native UseSailEnergy factor3 envelope,
        // including moving-body relative speed, steering, braking and background
        // work. A maxSailSpeed-only estimate misses orbital velocity on approach.
        var astro=GameMain.galaxy.astrosData[home];
        var speed=Math.Max(p.mecha.maxSailSpeed,p.uVelocity.magnitude+((astro.uPosNext-astro.uPos)*60d).magnitude);
        var extra=(Math.Max(0d,p.mecha.replicatePower)+Math.Max(0d,p.mecha.researchPower)
            +Math.Max(0d,p.mecha.miningPower)+Math.Max(0d,p.mecha.walkPower))*Math.Max(1d,p.mecha.reactorPowerConsRatio);
        var decision=VoyagerControlReservePolicy.Evaluate(new VoyagerControlReserveFacts {
            AvailableLoadedEnergyJoules=CalculateAvailableFlightEnergy(p.mecha),ProtectedReturnFloorJoules=floor,
            ThrustPowerPerAcceleration=p.mecha.thrustPowerPerAcc,EnergyConsumptionCoefficient=p.mecha.reactorPowerConsRatio,
            NativeEnergyScale=p.controller.actionSail.energy_scale,OrdinarySpeedUpperBound=speed,
            WarpStartPowerPerSpeed=p.mecha.warpStartPowerPerSpeed,WarpKeepingPowerPerSpeed=p.mecha.warpKeepingPowerPerSpeed,
            MaxWarpSpeed=p.mecha.maxWarpSpeed,HasNativeWarpTail=false,ExtraMechaPowerUpperBoundWatts=extra });
        return decision.RequiredMarginJoules??double.NaN;
    }
    private bool StoppedSpaceBindingMatches(StoppedSpaceHomeBinding b,string session)=>_sessions.IsCurrentSessionOwned
        && _sessions.SessionId==session && ReferenceEquals(GameMain.data,b.World) && ReferenceEquals(GameMain.mainPlayer,b.Player)
        && ReferenceEquals(b.Player.controller,b.Controller) && b.Player.isAlive;

    private void StartStoppedSpaceHomeReturn(ActionRecord action)
    {
        if(RevalidateStoppedSpaceHomeReturn(action.Plan) is {} error)throw new InvalidOperationException(error.Message);
        var b=action.Plan.StoppedSpaceHomeReturn!;
        b.Record=new StoppedSpaceReturnRecord {ActionId=action.ActionId,SessionId=action.SessionId,
            SourceFailedActionId=b.OriginalFailedAction,OriginalLedger=b.OriginalLedger,SourceTick=b.Permit.SavedTick,
            PriorSpent=b.PriorSpent,MaximumTicks=b.MaximumTicks,DeadlineUtc=DateTimeOffset.UtcNow.AddSeconds(b.MaximumWall)};
        _flightCheckpoints.RecordStoppedSpaceReturnStart(b.Record);
        b.Lease=DateTimeOffset.UtcNow.AddSeconds(30);action.FlightBestDistance=action.Plan.EstimatedDistance;
        action.FlightBestDistanceAtGameTick=GameMain.gameTick;action.FlightLastControlGameTick=-1;
        action.FlightDestinationContactAtGameTick=-1;action.FlightStableLandingAtGameTick=-1;
        action.State=NormalActionStates.WaitingForGame;
        action.Message="The exact saved space source is admitted only to a bounded original-home native Sail return; no warp entry is owned.";
        try {GameMain.Resume();}catch {GameMain.Pause();throw;}
    }

    internal void UpdateStoppedSpaceReturnPrePlayerTickOnMainThread()
    {
        foreach(var action in _actions.ActiveValues.Where(a=>!a.Terminal && a.Plan.StoppedSpaceHomeReturn is not null).ToArray())
        {
            if(!GuardStoppedSpaceHomeReturn(action))continue;
            UpdateInterplanetaryFlight(action);
        }
    }
    private bool GuardStoppedSpaceHomeReturn(ActionRecord a)
    {
        var b=a.Plan.StoppedSpaceHomeReturn!;var s=_sessions.CaptureOnMainThread();
        if(!StoppedSpaceBindingMatches(b,a.SessionId) || b.Player.mecha is null)
        {PauseStoppedSpaceReturn(a,"The original owned world/player binding ended.");return false;}
        var valid=StoppedSpaceReturnRuntimePolicy.AllowsNextNativeTick(new StoppedSpaceReturnRuntimeFacts {
            ExactNativeBinding=b.Record is not null && StoppedSpaceBindingMatches(b,a.SessionId),
            ExactSourceQuarantine=s.WriteHealth==WriteHealthStates.Quarantined && s.WriteQuarantineActionId==b.Permit.OpenActionId
                && s.WriteBlockers.Count==1 && s.WriteBlockers[0].Code==BridgeErrorCodes.WriteSubsystemQuarantined
                && s.WriteBlockers[0].Message==b.Permit.QuarantineReason,
            ExactOriginalLedger=PluginJson.Serialize(_flightCheckpoints.VoyagerJourney)==b.OriginalLedger,
            NativeObserverBound=_stoppedSpaceObserverProof?.Invoke()==true,
            NativeControlAvailable=!b.World.disableController && !b.World.guideRunning
                && LocalRawMovementInputIdle() && BuildUiIsIdle(b.Player),
            PlayerOrderAbsentOrOwned=b.Player.currentOrder is null || ReferenceEquals(b.Player.currentOrder,a.PlayerOrder),
            MovementState=b.Player.movementState.ToString(),CurrentStar=GameMain.localStar?.id,HomeStar=b.Star,
            SourceTick=b.Permit.SavedTick,CurrentTick=GameMain.gameTick,MaximumTicks=b.MaximumTicks,
            Now=DateTimeOffset.UtcNow,Deadline=b.Record?.DeadlineUtc,ObservationLease=b.Lease,
            WarpCommand=b.Player.warpCommand,Warping=b.Player.warping,WarpState=b.Player.warpState,
            StoredWarpers=CountVoyagerWarpers(b.Player.mecha.warpStorage),StartingWarpers=b.StartingWarpers,
            Core=b.Player.mecha.coreEnergy,LoadedEnergy=CalculateAvailableFlightEnergy(b.Player.mecha),
            NextTickUpper=Math.Max(b.NextTickUpper,StoppedSpaceNextControlUpper(b.Player,b.Home,b.WithdrawalFloor)),
            WithdrawalFloor=b.WithdrawalFloor });
        if(valid)return true;
        PauseStoppedSpaceReturn(a,"The exact source binding, original-home scope, observer, lease or funded return envelope ended.");return false;
    }
    private void PauseStoppedSpaceReturn(ActionRecord a,string reason)
    {
        var b=a.Plan.StoppedSpaceHomeReturn!;
        var same=StoppedSpaceBindingMatches(b,a.SessionId);
        if(same)GameMain.Pause();
        a.State=same?NormalActionStates.RecoveryRequired:NormalActionStates.OutcomeUnknown;a.Terminal=true;a.Succeeded=false;
        a.RecoveryRequired=true;a.CompletedAtGameTick=GameMain.gameTick;a.Message=reason+" Current native state is paused; never replay or load an older checkpoint.";
        if(same)a.AfterInventory=CaptureInventory(b.Player);
    }
    internal void PauseStoppedSpaceReturnsOnObserverLoss()
    {
        foreach(var a in _actions.ActiveValues.Where(a=>!a.Terminal&&a.Plan.StoppedSpaceHomeReturn is not null).ToArray())
            PauseStoppedSpaceReturn(a,"The original native pre-player observer was lost.");
    }
    private void UpdateStoppedSpaceReturnWatchdog()
    {
        foreach(var a in _actions.ActiveValues.Where(a=>!a.Terminal&&a.Plan.StoppedSpaceHomeReturn is not null).ToArray())
        {
            if(a.Plan.StoppedSpaceHomeReturn!.LandingPending)FinalizeStoppedSpaceHomeReturn(a);
            else GuardStoppedSpaceHomeReturn(a);
        }
    }
    private void FinalizeStoppedSpaceHomeReturn(ActionRecord a)
    {
        var b=a.Plan.StoppedSpaceHomeReturn!;var p=b.Player;var factory=GameMain.localPlanet?.factory;
        if(!GuardStoppedSpaceHomeReturn(a))return;
        if(!GameMain.isPaused || GameMain.localPlanet?.id!=b.Home || !StrictStoppedSpaceHomeIdle(p,allowNativePause:true) || factory is null
            || !TryObserveOwnedConstructionDrones(factory,p.mecha.constructionModule,out var drones,out _) || !drones)
        {PauseStoppedSpaceReturn(a,"Strict original-home native landing/idle/drones proof was not established.");return;}
        if(!_sessions.CompleteStoppedSpaceHomeAdmission(a.SessionId,b.World,b.Permit.OpenActionId,b.Permit.QuarantineReason,b.Home))
        {PauseStoppedSpaceReturn(a,"The loaded-source quarantine ownership changed before home handoff.");return;}
        if(!_sessions.TrySaveOwnedWorldNowOnMainThread(out var saveError)
            || !_sessions.StoppedSpaceHomeSaveCredentialMatches(a.SessionId,b.Home,GameMain.gameTick)
            || !_flightCheckpoints.CompleteStoppedSpaceHomeReturn(b.Record!,GameMain.gameTick,
                GameSave.SavePath(_sessions.CaptureOnMainThread().SaveName!),out saveError))
        {_sessions.QuarantineWritesOnMainThread(a.ActionId,saveError??"Space-return normal save failed.");PauseStoppedSpaceReturn(a,saveError??"Space-return save failed.");return;}
        b.Finalized=true;Complete(a,"The same owned current space source returned normally to its original home, proved strict native landing and was saved without checkpoint rollback; the failed outbound remains failed.");
        GameMain.Resume();
    }

    private static bool StrictStoppedSpaceHomeIdle(Player p,bool allowNativePause=false)=>ConstructionPlayerIdle(p,allowNativePause)
        && p.controller.rigid!=null && p.controller.rigid.velocity.sqrMagnitude==0f
        && p.controller.velocity.sqrMagnitude==0f && p.controller.actionWalk.rtsVelocity.sqrMagnitude==0f
        && p.controller.actionDrift.rtsVelocity.sqrMagnitude==0f
        && p.controller.actionWalk.moveVelocity.sqrMagnitude==0f && p.controller.actionDrift.moveVelocity.sqrMagnitude==0f;

    private sealed class StoppedSpaceHomeBinding
    {
        public GameData World=null!;public Player Player=null!;public PlayerController Controller=null!;
        public PausedSavedSpacePermit Permit=null!;public int Home;public int Star;public string OriginalLedger=string.Empty;
        public string OriginalFailedAction=string.Empty;public long MaximumTicks;public double MaximumWall;public long PriorSpent;
        public double WithdrawalFloor;public double NextTickUpper;public double MinimumStartCore;public int StartingWarpers;
        public DateTimeOffset Lease;public StoppedSpaceReturnRecord? Record;public bool LandingPending;public bool Finalized;
    }
    private sealed partial class NormalActionPlanPayload
    {public StoppedSpaceHomeBinding? StoppedSpaceHomeReturn {get;set;}}
}
