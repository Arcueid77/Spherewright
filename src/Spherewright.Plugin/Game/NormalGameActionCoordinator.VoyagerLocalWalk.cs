using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    public bool VoyagerLocalPrePlayerTickAvailable { get; set; }
    public DateTimeOffset VoyagerLocalPrePlayerObservedAtUtc { get; set; }
    private VoyagerRemoteIdleOwner? _voyagerRemoteIdleOwner;
    private NativeLocalWalkSceneBinding? _voyagerRemoteIdleScene;
    // A separate patience bound, not a 13/60s prediction of physics motion.
    private const double VoyagerLocalStopWallSeconds = VoyagerRemoteIdleAdmissionPolicy.StopWallSeconds;

    private bool VoyagerRemoteIdleBindingMatches()
    {
        return VoyagerRemoteIdleBindingMatches(_voyagerRemoteIdleOwner, _flightCheckpoints.VoyagerJourney);
    }

    private bool VoyagerRemoteIdleBindingMatches(VoyagerRemoteIdleOwner? owner, VoyagerJourneyRecord? journey,
        int? prospectiveRemotePlanetId = null)
    {
        var b = owner?.Binding;
        return owner is not null && b is not null && journey is not null
            && (prospectiveRemotePlanetId.HasValue || journey.Phase == "remote_landed")
            && journey.JourneyId == owner.JourneyId && VoyagerRemoteIdleRuntimeMatches(owner)
            && VoyagerLocalActionBudgetPolicy.MatchesOwnedJourneyIdentity(journey.OwnedSaveName, journey.GameVersion,
                _sessions.IsCurrentSessionOwned, _sessions.OwnedSaveName, b.World.gameName, _sessions.CurrentNativeGameVersion)
            && ReferenceEquals(GameMain.data, b.World) && ReferenceEquals(GameMain.mainPlayer, b.Player)
            && ReferenceEquals(b.Player.controller, b.Controller) && ReferenceEquals(b.Player.mecha, b.Mecha)
            && GameMain.localPlanet is { } planet && planet.id == (prospectiveRemotePlanetId ?? journey.RemotePlanetId)
            && ReferenceEquals(b.Player.planetData, planet) && ReferenceEquals(b.Player.factory, planet.factory);
    }

    private bool VoyagerRemoteIdleRuntimeMatches(VoyagerRemoteIdleOwner owner) =>
        _sessions.IsCurrentSessionOwned && _sessions.SessionId == owner.SessionId
        && ReferenceEquals(GameMain.data, owner.Binding.World) && ReferenceEquals(GameMain.mainPlayer, owner.Binding.Player)
        && ReferenceEquals(owner.Binding.Player.controller, owner.Binding.Controller)
        && ReferenceEquals(owner.Binding.Player.mecha, owner.Binding.Mecha);

    private VoyagerRemoteIdleAdmissionDecision CaptureVoyagerRemoteIdleAdmission(VoyagerRemoteIdleOwner owner,
        VoyagerJourneyRecord journey, NativeLocalWalkSceneBinding? scene, long spentGameTicks,
        long? lastLandedGameTick, out NativeLocalWalkSceneBinding? observedScene, int? prospectiveRemotePlanetId = null,
        Action<VoyagerRemoteIdleAdmissionFacts>? captureFacts = null, bool collectAfterGroundMiss = false,
        Action<NativePlayerGroundContactSnapshot>? captureGround = null, string groundStage = "native_guard")
    {
        observedScene = scene;
        var stage = "binding";
        VoyagerRemoteIdleAdmissionDecision Blocked(string code) => VoyagerRemoteIdleAdmissionPolicy.Blocked(code, stage);
        try
        {
            if (!VoyagerRemoteIdleBindingMatches(owner, journey, prospectiveRemotePlanetId))
                return Blocked("remote_idle_binding_unproved");
            var player = owner.Binding.Player;
            stage = "current_order";
            if (player.currentOrder is not null) return Blocked("remote_idle_current_order_active");
            stage = "scene_capture";
            observedScene ??= CaptureNativeLocalWalkScene();
            if (observedScene is null) return Blocked("remote_idle_scene_unproved");
            stage = "native_mode";
            var nativeBlocker = NativeWalkModeBlocker(player, stationary: false, observedScene,
                finiteWaterCostWithNativePause: true, allowDrift: true, allowBudgetedRecharge: true, captureGround, groundStage);
            var groundMiss = collectAfterGroundMiss && nativeBlocker == "remote_idle_ground_contact_unproved";
            if (nativeBlocker is not null && !groundMiss) return Blocked(nativeBlocker);
            stage = "order_queue";
            if (player.orders.orderCount != 0) return Blocked("remote_idle_order_queue_active");
            stage = "build_ui";
            if (!BuildUiIsIdle(player)) return Blocked("remote_idle_build_ui_active");
            stage = "mecha_work";
            if (!VoyagerMechaWorkIdle(player)) return Blocked("remote_idle_mecha_work_active");
            stage = "construction_drones";
            if (!TryObserveOwnedConstructionDrones(player.factory, player.mecha.constructionModule, out var dronesIdle, out _))
                return Blocked("remote_idle_construction_drones_unobserved");
            if (!dronesIdle) return Blocked("remote_idle_construction_drones_active");
            stage = "movement_cost";
            var reserve = EvaluateNativeLocalMovementCost(player, modeTransitionsExcluded: true);
            if (!reserve.Proved) return Blocked(reserve.BlockerCode ?? "remote_idle_movement_cost_unproved");
            stage = "walk_at_rest";
            if (player.movementState != EMovementState.Walk) return Blocked("remote_idle_actual_walk_required");
            if (!(player.speed <= .1f)) return Blocked("remote_idle_actual_speed_not_at_rest");
            stage = "energy_and_journey";
            var now = DateTimeOffset.UtcNow;
            var facts = new VoyagerRemoteIdleAdmissionFacts
            {
                MovementReserve = reserve, CoreEnergyJoules = player.mecha.coreEnergy,
                JourneyBudget = new VoyagerLocalActionBudgetFacts
                {
                    ExactRemoteBinding = true, NativePlayerIdle = !groundMiss, CurrentGameTick = GameMain.gameTick,
                    LastLandedGameTick = lastLandedGameTick, SpentGameTicks = spentGameTicks,
                    MaximumGameTicks = journey.MaximumGameTicks, ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
                    WorkGameTicks = 1, TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks,
                    NowUtc = now, JourneyDeadlineUtc = _sessions.EffectiveVoyagerDeadline(journey),
                    AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(player.mecha), LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules,
                    WorkPowerUpperWatts = reserve.WorkPowerUpperWatts, TailAndNextTickEnergyJoules = reserve.StopEnergyJoules ?? double.NaN,
                    NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage), OriginalExtraWarperReserve = journey.ExtraWarperReserve,
                },
            };
            captureFacts?.Invoke(facts);
            if (groundMiss) return Blocked("remote_idle_ground_contact_unproved");
            var decision = VoyagerRemoteIdleAdmissionPolicy.Evaluate(facts);
            return decision.Proved ? decision : Blocked(decision.BlockerCode ?? "remote_idle_journey_unproved");
        }
        catch (Exception exception)
        {
            // Only stable stage/type metadata is exposed; exception messages may
            // contain private paths. Keep the actual capture cause, not false.
            return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_capture_failed", stage, exception.GetType().Name);
        }
    }

    private static string VoyagerRemoteIdleFailureDetail(VoyagerRemoteIdleAdmissionDecision decision) =>
        " [" + decision.BlockerCode
        + (decision.CaptureStage is null ? string.Empty : "; capture=" + decision.CaptureStage)
        + (decision.CaptureExceptionType is null ? string.Empty : "; exception=" + decision.CaptureExceptionType) + "]";

    private void GuardVoyagerRemoteIdleOnMainThread(bool stoppingHost = false)
    {
        // The successful outbound remains successful. Its existing journey
        // continues owning idle energy until a budgeted controller takes over.
        // A terminal local order alone cannot guarantee the next tick stays dry.
        if (GameMain.isPaused) return;
        var bindingMatches = false;
        var bindingFailure = VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_binding_unproved");
        try { bindingMatches = VoyagerRemoteIdleBindingMatches(); }
        catch (Exception exception)
        { bindingFailure = VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_capture_failed", "binding", exception.GetType().Name); }
        if (!bindingMatches)
        {
            if (_voyagerRemoteIdleOwner is { } originalOwner && _flightCheckpoints.VoyagerJourney is { Phase: "remote_landed" } retainedOwner
                && retainedOwner.JourneyId == originalOwner.JourneyId && VoyagerRemoteIdleRuntimeMatches(originalOwner))
            {
                var reason = "The completed outbound's remote idle binding is no longer proved. Its successful flight is retained."
                    + VoyagerRemoteIdleFailureDetail(bindingFailure) + FreezeNativeLocalFailureEnergy(true);
                _sessions.QuarantineWritesOnMainThread(originalOwner.ActionId, reason);
                return;
            }
            // A remote primary/Host reload has no in-memory landed-controller
            // handoff. Do not silently recertify it or borrow an old action.
            var retained = _flightCheckpoints.VoyagerJourney;
            if (retained?.Phase == "remote_landed" && _sessions.SessionId is { } sessionId
                && VoyagerOwnedJourneyMatchesCurrentSession(retained, sessionId)
                && GameMain.mainPlayer?.planetId == retained.RemotePlanetId
                && !string.IsNullOrEmpty(retained.ActiveActionId))
            {
                var reason = "The original remote journey has no current landed-controller idle binding; remote local work is not recertified after reload."
                    + VoyagerRemoteIdleFailureDetail(bindingFailure)
                    + FreezeNativeLocalFailureEnergy(true);
                _sessions.QuarantineWritesOnMainThread(retained.ActiveActionId, reason);
            }
            return;
        }
        var owner = _voyagerRemoteIdleOwner!;
        var journey = _flightCheckpoints.VoyagerJourney!;
        if (!stoppingHost && _actions.ActiveValues.Any(a => !a.Terminal
            && (IsVoyagerLocalOrder(a) && a.Plan.VoyagerLocalJourneyId == journey.JourneyId
                || IsStructuredConstruction(a.Plan) && a.Plan.BlueprintState?.StructuredConstruction?.JourneyId == journey.JourneyId)))
            return; // Only controllers with the full current-tick reserve.
        // Host teardown must attempt Pause before any cost capture can throw.
        var admission = stoppingHost ? VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_host_stopping")
            : CaptureVoyagerRemoteIdleAdmission(owner, journey, _voyagerRemoteIdleScene,
                journey.SpentGameTicks, journey.LastLandedGameTick, out _);
        if (admission.Proved) return;
        var message = "The completed outbound's exact remote idle/return reserve is no longer proved. Its successful flight is retained; the original journey requires intervention."
            + VoyagerRemoteIdleFailureDetail(admission)
            + FreezeNativeLocalFailureEnergy(bindingMatches);
        _sessions.QuarantineWritesOnMainThread(owner.ActionId, message);
    }

    internal string? AdmitVerifiedVoyagerRemoteResumeOnMainThread(VoyagerRemoteResumeAdoption adoption)
        => CheckVerifiedVoyagerRemoteResumeOnMainThread(adoption, admit: true);

    // Called only by the exact completed-loader callback after Host proves its
    // Native delegate binding. It never fabricates a501 observation or owner.
    internal string? PreflightVerifiedVoyagerRemoteResumeOnMainThread(VoyagerRemoteResumeAdoption adoption)
        => CheckVerifiedVoyagerRemoteResumeOnMainThread(adoption, admit: false);

    private string? CheckVerifiedVoyagerRemoteResumeOnMainThread(VoyagerRemoteResumeAdoption adoption, bool admit)
    {
        if (adoption.FirstFrame.Failure is not null || _sessions.ResumeAdoptionError is not null)
            return "remote_resume_failed_adoption_is_terminal";
        if (admit && (!VoyagerLocalPrePlayerTickAvailable || DateTimeOffset.UtcNow < VoyagerLocalPrePlayerObservedAtUtc
            || DateTimeOffset.UtcNow - VoyagerLocalPrePlayerObservedAtUtc > TimeSpan.FromSeconds(2)))
            return "remote_resume_native_pre_player_guard_unproved";
        if (adoption.SourceKind != OwnedWorldResumeSourceKind.LastExit || !Guid.TryParse(adoption.ResumeActionId, out _)
            || !_sessions.IsCurrentSessionOwned || _sessions.SessionId != adoption.SessionId
            || !ReferenceEquals(GameMain.data, adoption.World) || !ReferenceEquals(GameMain.mainPlayer, adoption.Player))
            return "remote_resume_adopted_runtime_binding_unproved";
        var journey = _flightCheckpoints.VoyagerJourney;
        if (journey is null || journey.Phase != "remote_landed") return "remote_resume_original_remote_journey_unproved";
        var existing = _voyagerRemoteIdleOwner;
        var sameOwner = existing is not null && ReferenceEquals(existing.ResumeAdoption, adoption);
        if (!sameOwner && adoption.Authorization is { } chargeAuthorization && adoption.ChargeStartedUtc is { } chargeStarted
            && ((DateTimeOffset.UtcNow - chargeStarted).TotalSeconds > chargeAuthorization.Proposal.MaximumChargeWallSeconds
                || GameMain.gameTick - adoption.ChargeStartedGameTick!.Value > chargeAuthorization.Proposal.MaximumChargeGameTicks))
            return "remote_resume_charge_finite_budget_exhausted";
        if (!sameOwner && !_flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence))
            return "remote_resume_original_durable_proof_changed";
        var owner = sameOwner ? existing! : new VoyagerRemoteIdleOwner(new VoyagerNativeBinding(adoption.World, adoption.Player),
            adoption.SessionId, journey.JourneyId, adoption.ResumeActionId, journey.ActiveActionId, adoption);
        VoyagerRemoteIdleAdmissionFacts? captured = null;
        NativePlayerGroundContactSnapshot? ground = null;
        var loadBoundary = !admit && !sameOwner && _sessions.ExactRemoteResumeLoadPreflight(adoption);
        var decision = CaptureVoyagerRemoteIdleAdmission(owner, journey, sameOwner ? _voyagerRemoteIdleScene : null,
            journey.SpentGameTicks, journey.LastLandedGameTick, out var scene, captureFacts: f => captured = f,
            collectAfterGroundMiss: loadBoundary, captureGround: g => ground = g, groundStage: loadBoundary ? "load_preflight_before_first_frame" : admit ? "native_physics_post501" : "native_universe_post151");
        if (!sameOwner && adoption.FirstFrame.Armed && captured?.MovementReserve is { Proved: true } frameReserve
            && (adoption.ChargePerTickUpper != frameReserve.PerNativeTickEnergyJoules
                || adoption.ChargeCostEnvelope != RemoteResumeChargeCostEnvelope(adoption.Player)))
            return "remote_resume_charge_cost_envelope_changed";
        if (loadBoundary && decision.BlockerCode == "remote_idle_ground_contact_unproved" && captured is not null)
            return ArmVerifiedRemoteFirstFrame(adoption, owner, journey, scene, captured, ground);
        if (!admit && adoption.FirstFrame.UniverseObserved && adoption.TerrainPostUniverse is { } terrainAfter)
            _sessions.RecordRemoteResumeTerrain(adoption, terrainAfter, true, ground);
        if (!sameOwner && adoption.Authorization is { } authorization && captured is not null && !decision.Proved)
        {
            var bootstrapBlocker = ValidateRemoteResumeCharge(adoption, journey, captured, authorization);
            return bootstrapBlocker ?? (admit ? VoyagerRecoveryWindowPolicy.ChargingPending : null);
        }
        if (!decision.Proved) return decision.BlockerCode
            + (decision.CaptureStage is null ? string.Empty : ":" + decision.CaptureStage)
            + (decision.CaptureExceptionType is null ? string.Empty : ":" + decision.CaptureExceptionType);
        if (admit)
        {
            _voyagerRemoteIdleOwner = owner;
            _voyagerRemoteIdleScene = scene;
        }
        return null;
    }

    private string? ValidateRemoteResumeCharge(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord journey,
        VoyagerRemoteIdleAdmissionFacts facts, VoyagerRecoveryAuthorization authorization)
    {
        var p = adoption.Player; var m = p.mecha;
        var now = DateTimeOffset.UtcNow;
        var effectiveDeadline = _sessions.EffectiveVoyagerDeadline(journey);
        var originalBudgetBlocker = VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(facts.JourneyBudget, now, effectiveDeadline);
        if (originalBudgetBlocker is not null) return originalBudgetBlocker;
        var environment = RemoteResumeChargeEnvironmentBlocker(p);
        if (environment is not null) return environment;
        var reserve = facts.MovementReserve!;
        var perTick = reserve.PerNativeTickEnergyJoules ?? double.NaN;
        var envelope = RemoteResumeChargeCostEnvelope(p);
        if (adoption.ChargeCostEnvelope is not null && (adoption.ChargeCostEnvelope != envelope || adoption.ChargePerTickUpper != perTick))
            return "remote_resume_charge_cost_envelope_changed";
        var startTick = adoption.ChargeStartedGameTick ?? GameMain.gameTick;
        var startUtc = adoption.ChargeStartedUtc ?? now;
        var blocker = VoyagerRecoveryWindowPolicy.ChargeBlocker(m.coreEnergy, m.coreEnergyCap, CalculateAvailableFlightEnergy(m),
            journey.ReturnEnergyFloorJoules, perTick, journey.ReturnCoreEnergyMinimumJoules, journey.SpentGameTicks,
            GameMain.gameTick - journey.LastLandedGameTick!.Value, journey.MaximumGameTicks, journey.ReturnReserveGameTicks,
            GameMain.gameTick - startTick, authorization.Proposal.MaximumChargeGameTicks, (now - startUtc).TotalSeconds,
            authorization.Proposal.MaximumChargeWallSeconds, now, effectiveDeadline);
        if (blocker is not null) return blocker;
        adoption.ChargeStartedGameTick ??= startTick; adoption.ChargeStartedUtc ??= startUtc;
        adoption.ChargeCostEnvelope ??= envelope; adoption.ChargePerTickUpper ??= perTick;
        // g is a gross lower bound, not a promise of positive net progress. The
        // two-tick funding above never spends future unproved generation.
        adoption.ChargeGenerationLowerBoundWatts = GameStateReader.CaptureNativeCoreGenerationLowerBoundWatts(m);
        return null;
    }

    private sealed class VoyagerRemoteIdleOwner
    {
        public VoyagerRemoteIdleOwner(VoyagerNativeBinding binding, string sessionId, string journeyId,
            string actionId, string sourceFlightActionId, VoyagerRemoteResumeAdoption? resumeAdoption = null)
        {
            Binding = binding; SessionId = sessionId; JourneyId = journeyId; ActionId = actionId;
            SourceFlightActionId = sourceFlightActionId; ResumeAdoption = resumeAdoption;
        }
        public VoyagerNativeBinding Binding { get; }
        public string SessionId { get; }
        public string JourneyId { get; }
        public string ActionId { get; }
        public string SourceFlightActionId { get; }
        public VoyagerRemoteResumeAdoption? ResumeAdoption { get; }
    }

    private static bool IsVoyagerLocalOrder(ActionRecord a) => a.Plan.VoyagerLocalDeadlineGameTick.HasValue
        && (a.ActionKind == NormalActionKinds.Move || a.ActionKind == NormalActionKinds.Harvest);

    private static bool LocalRawMovementInputIdle() => !(bool)VFInput._moveLeft && !(bool)VFInput._moveRight
        && !(bool)VFInput._moveForward && !(bool)VFInput._moveBackward && !(bool)VFInput._jump
        && !(bool)VFInput._rollLeft && !(bool)VFInput._rollRight && !(bool)VFInput._pullUp && !(bool)VFInput._pushDown
        && !VFInput.rtsStop.onDown && !VFInput._rtsMove.onDown && !VFInput._rtsMove.onUp
        && !VFInput._rtsMine.onDown && !VFInput._rtsMine.onUp;

    private static VoyagerLocalWalkReserve CaptureVoyagerLocalWalkReserve(Player player, bool exactOrder, NativeLocalWalkSceneBinding? scene = null)
    {
        var c = player.controller; var w = c.actionWalk; var m = player.mecha;
        // Exclude the actual Walk branches, rather than inferring safety from
        // the movement enum: navigation's DetermineLowVelocity can set Fly,
        // UpdateJump can schedule jump energy or set Fly, and a queued order
        // can start again after Abort. Raw input is checked before GetInput.
        var subset = exactOrder && NativeWalkModeIsBounded(player, stationary: false, scene,
                finiteWaterCostWithNativePause: true, allowDrift: true, allowBudgetedRecharge: true)
            && player.orders.orderCount == 0 && BuildUiIsIdle(player) && VoyagerMechaWorkIdle(player)
            && TryObserveOwnedConstructionDrones(player.factory, m.constructionModule, out var dronesIdle, out _) && dronesIdle;
        return EvaluateNativeLocalMovementCost(player, subset);
    }

    private static VoyagerLocalWalkReserve EvaluateNativeLocalMovementCost(Player player, bool modeTransitionsExcluded,
        NativeMechaBackgroundCostFacts? background = null, NativeMechaRechargeFacts? recharge = null)
    {
        var c = player.controller; var w = c.actionWalk; var m = player.mecha;
        background ??= CaptureNativeMechaBackgroundCosts(player);
        if (!NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(background))
            recharge ??= CaptureNativeMechaRechargeFacts(player, background);
        return VoyagerLocalWalkReservePolicy.Evaluate(new VoyagerLocalWalkFacts
        {
            ModeTransitionsExcluded = modeTransitionsExcluded, WalkSpeed = m.walkSpeed, WalkPowerWatts = m.walkPower,
            MiningPowerWatts = m.miningPower, OtherWorkPowerWatts = m.replicatePower + m.researchPower,
            ReactorConsumptionRatio = m.reactorPowerConsRatio, RtsSpeed = w.rtsVelocity.magnitude,
            ManualSpeed = w.moveVelocity.magnitude, SoftLandingRecover = c.softLandingRecover,
            IncludePotentialWaterDrift = true, NativeThrustPowerPerAcceleration = m.thrustPowerPerAcc,
            NativeGravityUpperBound = NativeLocalGravityUpperBound(player),
            IndependentMechaCosts = background, BoundedRechargeCosts = recharge,
        });
    }

    internal static NativeLocalControlEnergyObservation CaptureConditionalNativeLocalControlEnergy(Player player)
    {
        var observation = new NativeLocalControlEnergyObservation();
        try
        {
            observation.IndependentDebitsExcluded = NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(
                CaptureNativeMechaBackgroundCosts(player));
            // Only a conditional numeric envelope. The caller never receives an
            // execution certificate by supplying a paused snapshot.
            var reserve = EvaluateNativeLocalMovementCost(player, modeTransitionsExcluded: true);
            observation.CostInputsProved = reserve.Proved;
            observation.PerNativeTickEnergyUpperJoules = reserve.Proved ? reserve.PerNativeTickEnergyJoules : null;
            if (reserve.Proved && reserve.BudgetedRecharge is { Proved: true } budgeted)
            {
                observation.Basis = "conditional_native_walk_drift_tick_upper_v2";
                observation.IndependentDebitsBudgeted = true;
                observation.IndependentEnergyPerNativeTickUpperJoules = budgeted.PerNativeTickEnergyUpperJoules;
                observation.ShieldRechargeEnergyPerNativeTickUpperJoules = budgeted.ShieldRechargeEnergyPerNativeTickUpperJoules;
                observation.LaserRechargeEnergyPerNativeTickUpperJoules = budgeted.LaserRechargeEnergyPerNativeTickUpperJoules;
            }
            if (!reserve.Proved) observation.Blockers.Add(reserve.BlockerCode ?? "native_cost_unknown");
            observation.Blockers.Add(GameMain.isPaused ? "native_paused_not_execution_ready" : "conditional_cost_not_execution_authority");
        }
        catch (Exception exception) { observation.Blockers.Add("native_cost_capture_failed:" + exception.GetType().Name); }
        return observation;
    }

    private static NativeMechaBackgroundCostFacts CaptureNativeMechaBackgroundCosts(Player player)
    {
        var m = player.mecha;
        return new NativeMechaBackgroundCostFacts
        {
            EnergyShieldCapacity = m.energyShieldCapacity, EnergyShieldRechargeEnabled = m.energyShieldRechargeEnabled,
            LaserActive = m.laserActive, AmmoItemId = m.ammoItemId, AmmoSelectSlot = m.ammoSelectSlot,
            AmmoRateOfFire = m.ammoROF, AmmoEnergyCost = m.ammoEnergyCost,
            AmmoStorageEmptyAndUnfiltered = m.ammoStorage is { } ammo && ammo.size >= 1 && ammo.size <= 16
                && ammo.grids is not null && ammo.grids.Length >= ammo.size
                && m.ammoSelectSlot >= 0 && m.ammoSelectSlot <= ammo.size
                && ammo.grids.Take(ammo.size).All(g => g.itemId == 0 && g.count == 0 && g.inc == 0 && g.filter == 0),
            GroundFleetCount = m.groundCombatModule?.fleetCount, SpaceFleetCount = m.spaceCombatModule?.fleetCount,
        };
    }

    private static NativeMechaRechargeFacts CaptureNativeMechaRechargeFacts(Player player, NativeMechaBackgroundCostFacts background)
    {
        var result = new NativeMechaRechargeFacts { Background = background };
        try
        {
            var m = player.mecha; var c = player.controller; var history = GameMain.history;
            result.StableCostInputs = history is not null && history.currentTech == 0
                && history.techQueue is { Length: 32 } && history.techQueue.All(id => id == 0)
                && LocalRawMovementInputIdle()
                && c.input0.sqrMagnitude == 0f && c.input1.sqrMagnitude == 0f;
            result.CoreEnergyJoules = m.coreEnergy; result.CoreCapacityJoules = m.coreEnergyCap;
            result.ReactorPowerGenerationWatts = m.reactorPowerGen; result.ShieldRechargeSpeed = m.energyShieldRechargeSpeed;
            result.CurrentReactorItemId = m.reactorItemId; result.CurrentReactorItemInc = m.reactorItemInc;
            var itemSet = LDB.items;
            var catalog = itemSet?.dataArray;
            if (itemSet is null || catalog is null || catalog.Length < 1 || catalog.Length > NativeMechaRechargeCostPolicy.MaximumItemCount)
            { result.CaptureBlockerCode = "native_independent_item_catalog_unproved"; return result; }
            var inc = Cargo.incTableMilli; var acc = Cargo.accTableMilli;
            if (inc is null || acc is null || inc.Length < 11 || acc.Length < 11
                || inc.Length > NativeMechaRechargeCostPolicy.MaximumTableLength || acc.Length > NativeMechaRechargeCostPolicy.MaximumTableLength)
            { result.CaptureBlockerCode = "native_independent_fuel_tables_unproved"; return result; }
            // Copies only global item metadata, never factory/resource data.
            result.ItemCatalog = catalog.Select(item => item is null ? null : new NativeRechargeItemFacts
                { ItemId = item.ID, ReactorInc = item.ReactorInc, Productive = item.Productive }).ToArray();
            var selected = m.reactorItemId > 0 ? itemSet.Select(m.reactorItemId) : null;
            result.CurrentSelection = selected is null ? null : new NativeRechargeItemFacts
                { ItemId = selected.ID, ReactorInc = selected.ReactorInc, Productive = selected.Productive };
            result.CurrentSelectionObserved = true;
            result.IncTableMilli = inc.Take(11).Select(value => (double)value).ToArray();
            result.AccTableMilli = acc.Take(11).Select(value => (double)value).ToArray();
        }
        catch (Exception exception) { result.CaptureBlockerCode = "native_independent_cost_capture_failed:" + exception.GetType().Name; }
        return result;
    }

    private static bool NativeWalkModeIsBounded(Player player, bool stationary, NativeLocalWalkSceneBinding? scene = null,
        bool finiteWaterCostWithNativePause = false, bool allowDrift = false, bool allowBudgetedRecharge = false) =>
        NativeWalkModeBlocker(player, stationary, scene, finiteWaterCostWithNativePause, allowDrift, allowBudgetedRecharge) is null;

    private static string? NativeWalkModeBlocker(Player player, bool stationary, NativeLocalWalkSceneBinding? scene,
        bool finiteWaterCostWithNativePause, bool allowDrift, bool allowBudgetedRecharge = false,
        Action<NativePlayerGroundContactSnapshot>? captureGround = null, string groundStage = "native_guard",
        Action<bool>? captureSphere = null)
    {
        var c = player.controller; var w = c.actionWalk;
        var vertical = c.vertSpeed;
        if (!player.isAlive) return "remote_idle_player_not_alive";
        if (GameMain.isPaused) return "remote_idle_game_paused";
        if (GameMain.data.disableController) return "remote_idle_controller_disabled";
        if (GameMain.data.guideRunning) return "remote_idle_guide_running";
        if (!(player.movementState == EMovementState.Walk || allowDrift && player.movementState == EMovementState.Drift)) return "remote_idle_movement_mode_unproved";
        if (!(c.movementStateInFrame == EMovementState.Walk || allowDrift && c.movementStateInFrame == EMovementState.Drift)) return "remote_idle_frame_movement_mode_unproved";
        if (player.warpCommand || player.warpState != 0f) return "remote_idle_warp_exit_unproved";
        if (player.navigation.navigating || player.navigation.hasTask) return "remote_idle_navigation_active";
        if (c.actionSail.fastTravelling) return "remote_idle_fast_travel_active";
        var sceneBlocker = NativeLocalWalkSceneBlocker(player, scene, finiteWaterCostWithNativePause);
        if (sceneBlocker is not null) return sceneBlocker;
        var independent = CaptureNativeMechaBackgroundCosts(player);
        if (!NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(independent))
        {
            if (!allowBudgetedRecharge) return "remote_idle_independent_mecha_cost_unproved";
            var bounded = NativeMechaRechargeCostPolicy.Evaluate(CaptureNativeMechaRechargeFacts(player, independent));
            if (!bounded.Proved) return bounded.BlockerCode;
        }
        if (!LocalRawMovementInputIdle()) return "remote_idle_raw_input_active";
        if (c.input0.sqrMagnitude != 0f || c.input1.sqrMagnitude != 0f) return "remote_idle_controller_input_active";
        if (w.jumpDelayTick > 0 || w.flyUpChance != 0f) return "remote_idle_jump_or_fly_pending";
        if (w.moveVelocity.sqrMagnitude != 0f) return "remote_idle_walk_manual_velocity_nonzero";
        if (finiteWaterCostWithNativePause && (c.actionDrift.moveVelocity.sqrMagnitude != 0f
            || float.IsNaN(c.actionDrift.rtsVelocity.magnitude) || float.IsInfinity(c.actionDrift.rtsVelocity.magnitude)
            || !(c.actionDrift.rtsVelocity.magnitude <= player.mecha.walkSpeed + .01f))) return "remote_idle_drift_velocity_unproved";
        if (stationary && w.rtsVelocity.sqrMagnitude != 0f) return "remote_idle_walk_rts_velocity_nonzero";
        if (float.IsNaN(vertical) || float.IsInfinity(vertical) || !allowDrift && !(Math.Abs(vertical) <= .01f)) return "remote_idle_vertical_velocity_unproved";
        if (!(c.overridePlayerSpeed >= 0f && c.overridePlayerSpeed <= .01f)) return "remote_idle_override_speed_unproved";
        if (!(c.softLandingRecover >= 0f && c.softLandingRecover <= 1f)) return "remote_idle_soft_landing_unproved";
        if (!(allowDrift && player.movementState == EMovementState.Drift))
        {
            var sphereHit = Physics.CheckSphere(player.position + player.position.normalized * .15f, .35f, 15873);
            captureSphere?.Invoke(sphereHit);
            captureGround?.Invoke(GameStateReader.CaptureNativeGroundContact(player, groundStage, sphereHit));
            if (!sphereHit) return "remote_idle_ground_contact_unproved";
        }
        return null;
    }

    private void CaptureVoyagerMoveStrictStart(ActionRecord action)
    {
        if (!IsVoyagerLocalOrder(action) || action.ActionKind != NormalActionKinds.Move
            || !VoyagerLocalReferencesMatch(action)) return;
        var p = action.Plan.VoyagerLocalBinding!.Player;
        var reserve = CaptureVoyagerLocalWalkReserve(p, p.currentOrder is null, action.Plan.VoyagerLocalWalkScene);
        var journey = _flightCheckpoints.VoyagerJourney;
        if (reserve.Proved && journey is not null && journey.JourneyId == action.Plan.VoyagerLocalJourneyId
            && journey.Phase == "remote_landed" && journey.RemotePlanetId == action.PlanetId
            && VoyagerOwnedJourneyMatchesCurrentSession(journey, action.SessionId)
            && CountVoyagerWarpers(p.mecha.warpStorage) >= 1 + journey.ExtraWarperReserve
            && VoyagerLocalWalkReservePolicy.CanFund(p.mecha.coreEnergy, CalculateAvailableFlightEnergy(p.mecha),
                journey.ReturnEnergyFloorJoules, reserve.PerNativeTickEnergyJoules, VoyagerLocalWalkReservePolicy.StopGameTicks)
            && p.orders.orderCount == 0 && p.movementState == EMovementState.Walk
            && p.controller.movementStateInFrame == EMovementState.Walk && p.speed <= .1f
            && p.controller.actionWalk.rtsVelocity.sqrMagnitude == 0f
            && p.controller.actionDrift.rtsVelocity.sqrMagnitude == 0f)
            action.VoyagerMoveGroundGap = new VoyagerActiveMoveGroundGapPolicy(GameMain.gameTick);
    }

    private bool DeferVoyagerMoveGroundGapArrival(ActionRecord action)
    {
        if (action.VoyagerMoveGroundGap is not { } gap) return false;
        if (gap.Rejected || gap.StopLatched || gap.GapStartedGameTick.HasValue) return true;
        // Update is not a Native501 certificate and cannot clear/start a gap.
        var p = action.Plan.VoyagerLocalBinding!.Player;
        return !Physics.CheckSphere(p.position + p.position.normalized * .15f, .35f, 15873);
    }

    private VoyagerActiveMoveGroundGapFacts CaptureVoyagerMoveGroundGapFacts(ActionRecord action, bool stopping,
        out string? blocker)
    {
        var p = action.Plan.VoyagerLocalBinding!.Player;
        bool? ground = null;
        blocker = NativeWalkModeBlocker(p, false, action.Plan.VoyagerLocalWalkScene, true, true, true,
            captureSphere: hit => ground = hit);
        // Native Drift skips Walk's sphere. A mode switch is not positive
        // contact evidence and cannot reset the same original eight-tick gap.
        if (blocker is null && !ground.HasValue)
            ground = Physics.CheckSphere(p.position + p.position.normalized * .15f, .35f, 15873);
        var exactOrder = stopping ? p.currentOrder is null
            : ReferenceEquals(p.currentOrder, action.PlayerOrder)
                || p.currentOrder is null && action.PlayerOrder?.targetReached == true;
        var subset = exactOrder && (blocker is null || blocker == "remote_idle_ground_contact_unproved")
            && p.orders.orderCount == 0 && BuildUiIsIdle(p) && VoyagerMechaWorkIdle(p)
            && TryObserveOwnedConstructionDrones(p.factory, p.mecha.constructionModule, out var idle, out _) && idle;
        var reserve = EvaluateNativeLocalMovementCost(p, subset);
        blocker ??= reserve.BlockerCode;
        var journey = _flightCheckpoints.VoyagerJourney;
        var now = DateTimeOffset.UtcNow;
        var exactJourney = journey is not null && journey.JourneyId == action.Plan.VoyagerLocalJourneyId
            && journey.Phase == "remote_landed" && journey.RemotePlanetId == action.PlanetId
            && VoyagerOwnedJourneyMatchesCurrentSession(journey, action.SessionId);
        return new VoyagerActiveMoveGroundGapFacts
        {
            ExactActiveStrictMove = !action.Terminal && action.ActionKind == NormalActionKinds.Move
                && action.VoyagerMoveGroundGap is not null && exactJourney && VoyagerLocalReferencesMatch(action),
            NonGroundNativePremisesProved = subset && reserve.Proved,
            GroundSphereHit = ground,
            ControlLeaseOpen = now >= action.Plan.VoyagerLocalStartedAtUtc
                && now < action.VoyagerLocalObservationLeaseExpiresAtUtc && VoyagerLocalPrePlayerTickAvailable
                && now >= VoyagerLocalPrePlayerObservedAtUtc
                && now - VoyagerLocalPrePlayerObservedAtUtc <= TimeSpan.FromSeconds(1),
            OriginalWorkWindowOpen = action.Plan.VoyagerLocalDeadlineGameTick.HasValue
                && VoyagerLocalActionBudgetPolicy.LocalWindowStillOpen(action.Plan.VoyagerLocalStartedGameTick,
                    GameMain.gameTick, action.Plan.VoyagerLocalDeadlineGameTick.Value,
                    action.Plan.VoyagerLocalStartedAtUtc, now, action.Plan.VoyagerLocalDeadlineUtc),
            CoreEnergyJoules = p.mecha.coreEnergy, PerNativeTickEnergyJoules = reserve.PerNativeTickEnergyJoules,
            OriginalJourney = journey is null ? null : new VoyagerLocalActionBudgetFacts
            {
                ExactRemoteBinding = exactJourney, NativePlayerIdle = false,
                CurrentGameTick = GameMain.gameTick, LastLandedGameTick = journey.LastLandedGameTick,
                SpentGameTicks = journey.SpentGameTicks, MaximumGameTicks = journey.MaximumGameTicks,
                ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
                NowUtc = now, JourneyDeadlineUtc = _sessions.EffectiveVoyagerDeadline(journey),
                AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(p.mecha),
                LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules,
                NativeWarperCount = CountVoyagerWarpers(p.mecha.warpStorage), OriginalExtraWarperReserve = journey.ExtraWarperReserve,
            },
        };
    }

    // Return true only when this tick is handled by the bounded missing-contact
    // work/stop path. Ordinary grounded work retains its original strict guard.
    private bool ObserveVoyagerMoveGroundGap(ActionRecord action)
    {
        var gap = action.VoyagerMoveGroundGap!;
        var facts = CaptureVoyagerMoveGroundGapFacts(action, false, out var blocker);
        switch (gap.Observe(facts))
        {
            case VoyagerMoveGroundGapStep.Grounded: return false;
            case VoyagerMoveGroundGapStep.ContinueGap: return true;
            case VoyagerMoveGroundGapStep.Stop:
                ArmVoyagerMoveGroundGapStop(action, facts, "The original Move's finite ground-contact gap ended; stop its exact order.");
                return true;
            case VoyagerMoveGroundGapStep.Duplicate: return true;
            default:
                FinishVoyagerLocalOrderUnknown(action, "The active Move's non-ground Native envelope is unproved. ["
                    + (blocker ?? gap.BlockerCode) + "]");
                return true;
        }
    }

    private void ArmVoyagerMoveGroundGapStop(ActionRecord action, VoyagerActiveMoveGroundGapFacts facts, string message)
    {
        if (action.Terminal || action.VoyagerLocalStopDeadlineTick.HasValue) return;
        if (action.VoyagerMoveGroundGap!.Rejected || !facts.ExactActiveStrictMove || !facts.NonGroundNativePremisesProved
            || !VoyagerActiveMoveGroundGapPolicy.OriginalBudgetAllows(facts, 0, VoyagerLocalWalkReservePolicy.StopGameTicks,
                VoyagerLocalStopWallSeconds)
            || !VoyagerActiveMoveGroundGapPolicy.CanFund(facts, VoyagerLocalWalkReservePolicy.StopGameTicks))
        { FinishVoyagerLocalOrderUnknown(action, "The original Move cannot fund its unchanged finite contact-loss stop. " + message); return; }
        action.VoyagerMoveGroundGap!.LatchStop("voyager_move_ground_contact_gap_ended");
        AbortPlayerOrderIfOwned(action, forLocalNativeStop: true);
        action.VoyagerMoveGroundGapStopping = true;
        action.VoyagerMoveGroundStopArmedAtTick = GameMain.gameTick;
        action.VoyagerMoveGroundStopArmedAtUtc = DateTimeOffset.UtcNow;
        action.VoyagerLocalStopDeadlineTick = checked(GameMain.gameTick + VoyagerLocalWalkReservePolicy.StopGameTicks);
        action.VoyagerLocalStopDeadlineUtc = action.VoyagerMoveGroundStopArmedAtUtc.Value.AddSeconds(VoyagerLocalStopWallSeconds);
        action.VoyagerLocalStopSucceeded = false;
        action.VoyagerLocalStopMessage = message;
        action.FailureKind = "voyager_move_ground_contact_gap_ended";
        action.State = NormalActionStates.WaitingForGame;
        action.Message = "The exact original Move stopped; strict grounded Walk and physical zero-tail readback remain required.";
    }

    private void ObserveVoyagerMoveGroundGapDrain(ActionRecord action)
    {
        if (!VoyagerLocalReferencesMatch(action)) { GuardVoyagerLocalAction(action); return; }
        var gap = action.VoyagerMoveGroundGap!;
        var facts = CaptureVoyagerMoveGroundGapFacts(action, true, out var blocker);
        var tick = GameMain.gameTick;
        var now = facts.OriginalJourney?.NowUtc ?? DateTimeOffset.UtcNow;
        var remaining = (int)Math.Min(VoyagerLocalWalkReservePolicy.StopGameTicks,
            Math.Max(1m, (decimal)action.VoyagerLocalStopDeadlineTick!.Value - tick));
        var wall = Math.Max(0d, (action.VoyagerLocalStopDeadlineUtc!.Value - now).TotalSeconds);
        if (gap.Rejected || !action.VoyagerMoveGroundStopArmedAtTick.HasValue
            || !action.VoyagerMoveGroundStopArmedAtUtc.HasValue
            || !facts.ExactActiveStrictMove || !facts.NonGroundNativePremisesProved
            || !VoyagerActiveMoveGroundGapPolicy.OriginalBudgetAllows(facts, 0, remaining, wall)
            || !VoyagerActiveMoveGroundGapPolicy.CanFund(facts, remaining))
        { FinishVoyagerLocalOrderUnknown(action, "The original contact-loss stop envelope changed. [" + (blocker ?? gap.BlockerCode) + "]"); return; }
        if (facts.GroundSphereHit == false && !action.VoyagerMoveGroundGapStopping)
        {
            // The stop was already armed by ordinary arrival/watchdog logic.
            // Preserve that exact end tick/UTC, but never turn contact loss
            // into task success when the physical tail eventually settles.
            gap.LatchStop("voyager_move_ground_contact_lost_during_stop");
            action.VoyagerMoveGroundGapStopping = true;
            action.VoyagerLocalStopSucceeded = false;
            action.FailureKind = "voyager_move_ground_contact_gap_ended";
            action.VoyagerLocalStopMessage += " Ground contact was lost during the original finite stop.";
        }
        var p = action.Plan.VoyagerLocalBinding!.Player;
        if (facts.GroundSphereHit == true && p.movementState == EMovementState.Walk
            && p.controller.movementStateInFrame == EMovementState.Walk && p.speed <= .1f
            && p.controller.actionWalk.rtsVelocity.sqrMagnitude == 0f && p.controller.actionWalk.moveVelocity.sqrMagnitude == 0f
            && p.controller.actionDrift.rtsVelocity.sqrMagnitude == 0f && p.controller.actionDrift.moveVelocity.sqrMagnitude == 0f)
        {
            action.VoyagerLocalCompleting = true;
            if (action.VoyagerLocalStopSucceeded)
                Complete(action, action.VoyagerLocalStopMessage + " Native grounded Walk and zero RTS tail are physically verified.");
            else
                Fail(action, action.VoyagerLocalStopMessage + " Native grounded Walk and zero RTS tail are physically verified; retain the failed Move.");
            return;
        }
        if (VoyagerActiveMoveGroundGapPolicy.MustPauseBeforeCurrentTick(action.VoyagerMoveGroundStopArmedAtTick!.Value,
            tick, action.VoyagerMoveGroundStopArmedAtUtc!.Value, now, action.VoyagerLocalStopDeadlineUtc.Value))
            FinishVoyagerLocalOrderUnknown(action, "The unchanged contact-loss stop bound ended before strict physical settlement; its failure601 remains funded.");
    }

    public void UpdateVoyagerLocalPrePlayerTickOnMainThread()
    {
        GuardVoyagerLandingChargeOnMainThread();
        GuardVoyagerRemoteIdleOnMainThread();
        foreach (var action in _actions.ActiveValues.Where(a => !a.Terminal && IsVoyagerLocalOrder(a)).ToArray())
        {
            try
            {
                if (!VoyagerLocalReferencesMatch(action)) { GuardVoyagerLocalAction(action); continue; }
                var tick = GameMain.gameTick;
                var previousTick = action.VoyagerLocalLastPrePlayerTick < 0
                    ? action.Plan.VoyagerLocalStartedGameTick : action.VoyagerLocalLastPrePlayerTick;
                if (action.VoyagerLocalLastPrePlayerTick == tick) continue;
                if (tick < previousTick || tick - previousTick > 2)
                { FinishVoyagerLocalOrderUnknown(action, "The original pre-player tick sequence was interrupted or reversed."); continue; }
                if (action.VoyagerMoveGroundGap is not null && action.VoyagerLocalStopDeadlineTick.HasValue
                    && tick - previousTick != 1)
                { FinishVoyagerLocalOrderUnknown(action, "The original Move stop lost its per-tick Native observation."); continue; }
                action.VoyagerLocalLastPrePlayerTick = tick;
                action.Plan.VoyagerLocalLastObservedGameTick = tick;
                if (action.VoyagerLocalStopDeadlineTick.HasValue)
                {
                    if (action.VoyagerMoveGroundGap is not null) ObserveVoyagerMoveGroundGapDrain(action);
                    else ObserveVoyagerLocalOrderDrain(action);
                    continue;
                }
                if (!GuardVoyagerLocalAction(action)) continue;
                if (action.VoyagerMoveGroundGap is not null && ObserveVoyagerMoveGroundGap(action)) continue;
                var player = action.Plan.VoyagerLocalBinding!.Player;
                var reserve = CaptureVoyagerLocalWalkReserve(player, ReferenceEquals(player.currentOrder, action.PlayerOrder)
                    || player.currentOrder is null && action.PlayerOrder?.targetReached == true, action.Plan.VoyagerLocalWalkScene);
                var journey = _flightCheckpoints.VoyagerJourney;
                if (!reserve.Proved || journey is null || CountVoyagerWarpers(player.mecha.warpStorage) < 1 + journey.ExtraWarperReserve
                    || !VoyagerLocalWalkReservePolicy.CanFund(player.mecha.coreEnergy,
                    CalculateAvailableFlightEnergy(player.mecha), journey.ReturnEnergyFloorJoules,
                    reserve.PerNativeTickEnergyJoules, VoyagerLocalWalkReservePolicy.StopGameTicks))
                    FinishVoyagerLocalOrderUnknown(action, "The next complete Native Walk/Drift tick and finite owned-order stop allowance are unproved; Fly/jump/manual input remain excluded.");
            }
            catch (Exception)
            { FinishVoyagerLocalOrderUnknown(action, "The original pre-player local-action observation failed; preserve its original action and evidence."); }
        }
        // A just-completed order no longer owns the upcoming Player tick.
        // Revalidate the idle handoff now, before601 can enter paid Drift.
        GuardVoyagerRemoteIdleOnMainThread();
    }

    private void RequestVoyagerLocalOrderStop(ActionRecord action, bool succeeded, string message)
    {
        if (action.Terminal || action.VoyagerLocalStopDeadlineTick.HasValue) return;
        if (!VoyagerLocalReferencesMatch(action)) { GuardVoyagerLocalAction(action); return; }
        var player = action.Plan.VoyagerLocalBinding!.Player;
        if (action.VoyagerMoveGroundGap is { } gap && (gap.GapStartedGameTick.HasValue
            || !Physics.CheckSphere(player.position + player.position.normalized * .15f, .35f, 15873)))
        {
            var facts = CaptureVoyagerMoveGroundGapFacts(action, false, out _);
            ArmVoyagerMoveGroundGapStop(action, facts, message);
            return;
        }
        var reserve = CaptureVoyagerLocalWalkReserve(player, ReferenceEquals(player.currentOrder, action.PlayerOrder)
            || player.currentOrder is null && action.PlayerOrder?.targetReached == true, action.Plan.VoyagerLocalWalkScene);
        var journey = _flightCheckpoints.VoyagerJourney;
        if (!reserve.Proved || journey is null || CountVoyagerWarpers(player.mecha.warpStorage) < 1 + journey.ExtraWarperReserve
            || !VoyagerLocalWalkReservePolicy.CanFund(player.mecha.coreEnergy,
            CalculateAvailableFlightEnergy(player.mecha), journey.ReturnEnergyFloorJoules,
            reserve.PerNativeTickEnergyJoules, VoyagerLocalWalkReservePolicy.StopGameTicks))
        { FinishVoyagerLocalOrderUnknown(action, "The original order stopped without a proved remaining Native Walk tail. " + message); return; }
        AbortPlayerOrderIfOwned(action, forLocalNativeStop: true); // Empty queue proved; Native velocity is never written.
        action.VoyagerLocalStopDeadlineTick = GameMain.gameTick + VoyagerLocalWalkReservePolicy.StopGameTicks;
        var stopStartedUtc = DateTimeOffset.UtcNow;
        action.VoyagerLocalStopDeadlineUtc = stopStartedUtc.AddSeconds(VoyagerLocalStopWallSeconds);
        if (action.VoyagerMoveGroundGap is not null)
        {
            action.VoyagerMoveGroundStopArmedAtTick = GameMain.gameTick;
            action.VoyagerMoveGroundStopArmedAtUtc = stopStartedUtc;
        }
        action.VoyagerLocalStopSucceeded = succeeded;
        action.VoyagerLocalStopMessage = message;
        action.State = NormalActionStates.WaitingForGame;
        action.Message = "The exact owned order stopped; its original13-tick/UTC allowance still needs real grounded Walk and zero-velocity readback. Unproved completion freezes Native energy.";
    }

    private void ObserveVoyagerLocalOrderDrain(ActionRecord action)
    {
        if (!VoyagerLocalReferencesMatch(action)) { GuardVoyagerLocalAction(action); return; }
        var player = action.Plan.VoyagerLocalBinding!.Player;
        var reserve = CaptureVoyagerLocalWalkReserve(player, player.currentOrder is null, action.Plan.VoyagerLocalWalkScene);
        var journey = _flightCheckpoints.VoyagerJourney;
        if (!reserve.Proved || journey is null || journey.JourneyId != action.Plan.VoyagerLocalJourneyId
            || journey.Phase != "remote_landed" || journey.RemotePlanetId != action.PlanetId
            || !VoyagerOwnedJourneyMatchesCurrentSession(journey, action.SessionId)
            || !FiniteConstruction(player.mecha.coreEnergy) || player.mecha.coreEnergy <= 0d
            || !FiniteConstruction(CalculateAvailableFlightEnergy(player.mecha))
            || CalculateAvailableFlightEnergy(player.mecha) < journey.ReturnEnergyFloorJoules
            || CountVoyagerWarpers(player.mecha.warpStorage) < 1 + journey.ExtraWarperReserve)
        { FinishVoyagerLocalOrderUnknown(action, "The original Native tail left its exact ordinary Walk/owned-world envelope."); return; }
        if (player.movementState == EMovementState.Walk && player.controller.movementStateInFrame == EMovementState.Walk
            && player.controller.actionWalk.rtsVelocity.sqrMagnitude == 0f && player.controller.actionWalk.moveVelocity.sqrMagnitude == 0f
            && player.speed <= .1f)
        {
            action.VoyagerLocalCompleting = true;
            if (action.VoyagerLocalStopSucceeded) Complete(action, action.VoyagerLocalStopMessage + " Native RTS tail is physically zero.");
            else Fail(action, action.VoyagerLocalStopMessage + " Native RTS tail is physically zero; retain the original journey.");
            return;
        }
        var remaining = action.VoyagerLocalStopDeadlineTick!.Value - GameMain.gameTick;
        if (!action.VoyagerLocalStopDeadlineUtc.HasValue || remaining <= 0
            || DateTimeOffset.UtcNow >= action.VoyagerLocalStopDeadlineUtc.Value
            || !VoyagerLocalWalkReservePolicy.CanFund(player.mecha.coreEnergy, CalculateAvailableFlightEnergy(player.mecha),
                journey.ReturnEnergyFloorJoules, reserve.PerNativeTickEnergyJoules, (int)Math.Min(remaining, VoyagerLocalWalkReservePolicy.StopGameTicks)))
            FinishVoyagerLocalOrderUnknown(action, "The first finite Native RTS cleanup deadline or original return/core tail reserve ended without physical stop proof.");
    }

    private void FinishVoyagerLocalOrderUnknown(ActionRecord action, string message)
    {
        if (action.Terminal) return;
        action.VoyagerMoveGroundGap?.Reject("voyager_move_ground_gap_original_outcome_unknown");
        var exact = VoyagerLocalReferencesMatch(action);
        message += FreezeNativeLocalFailureEnergy(exact);
        if (exact)
        {
            // Do not pop a user's queued order or replace an external order.
            if (action.Plan.VoyagerLocalBinding!.Player.orders.orderCount == 0) AbortPlayerOrderIfOwned(action, forLocalNativeStop: true);
            action.AfterInventory ??= CaptureInventory(action.Plan.VoyagerLocalBinding.Player);
        }
        action.Terminal = true; action.Succeeded = false; action.State = NormalActionStates.OutcomeUnknown;
        action.CompletedAtGameTick = action.Plan.VoyagerLocalLastObservedGameTick;
        action.FailureKind = "voyager_local_native_tail_unproved"; action.Message = message;
        if (exact) _sessions.QuarantineWritesOnMainThread(action.ActionId, message);
    }

    private void CheckVoyagerLocalWalkWatchdogOnMainThread()
    {
        foreach (var action in _actions.ActiveValues.Where(a => !a.Terminal && IsVoyagerLocalOrder(a)).ToArray())
        {
            if (!VoyagerLocalReferencesMatch(action)) { GuardVoyagerLocalAction(action); continue; }
            if (GameMain.isPaused || !VoyagerLocalPrePlayerTickAvailable
                || DateTimeOffset.UtcNow - VoyagerLocalPrePlayerObservedAtUtc > TimeSpan.FromSeconds(1))
            { FinishVoyagerLocalOrderUnknown(action, "The Native pre-player hook stopped or the user paused; do not infer physical cleanup or override control."); continue; }
            if (action.VoyagerLocalStopDeadlineTick.HasValue)
            {
                if (!action.VoyagerLocalStopDeadlineUtc.HasValue || DateTimeOffset.UtcNow >= action.VoyagerLocalStopDeadlineUtc.Value)
                    FinishVoyagerLocalOrderUnknown(action, "The original finite RTS tail wall deadline expired without Native physical-stop proof.");
            }
        }
    }

    public void StopVoyagerLocalForHostShutdownOnMainThread()
    {
        VoyagerLocalPrePlayerTickAvailable = false;
        GuardVoyagerLandingChargeOnMainThread(stoppingHost: true);
        GuardVoyagerRemoteIdleOnMainThread(stoppingHost: true);
        foreach (var action in _actions.ActiveValues.Where(a => !a.Terminal && IsVoyagerLocalOrder(a)).ToArray())
            FinishVoyagerLocalOrderUnknown(action, "The original local action lost its host/pre-player hook. Preserve its exact action; physical Native tail is unproved.");
    }

    private sealed partial class ActionRecord
    {
        public long VoyagerLocalLastPrePlayerTick { get; set; } = -1;
        public DateTimeOffset VoyagerLocalObservationLeaseExpiresAtUtc { get; set; } = DateTimeOffset.UtcNow.AddSeconds(30);
        public long? VoyagerLocalStopDeadlineTick { get; set; }
        public DateTimeOffset? VoyagerLocalStopDeadlineUtc { get; set; }
        public bool VoyagerLocalStopSucceeded { get; set; }
        public string? VoyagerLocalStopMessage { get; set; }
        public bool VoyagerLocalCompleting { get; set; }
        public VoyagerActiveMoveGroundGapPolicy? VoyagerMoveGroundGap { get; set; }
        public bool VoyagerMoveGroundGapStopping { get; set; }
        public long? VoyagerMoveGroundStopArmedAtTick { get; set; }
        public DateTimeOffset? VoyagerMoveGroundStopArmedAtUtc { get; set; }
    }
}
