using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
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
        long? lastLandedGameTick, out NativeLocalWalkSceneBinding? observedScene, int? prospectiveRemotePlanetId = null)
    {
        observedScene = scene;
        var stage = "binding";
        try
        {
            if (!VoyagerRemoteIdleBindingMatches(owner, journey, prospectiveRemotePlanetId))
                return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_binding_unproved");
            var player = owner.Binding.Player;
            stage = "current_order";
            if (player.currentOrder is not null) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_current_order_active");
            stage = "scene_capture";
            observedScene ??= CaptureNativeLocalWalkScene();
            if (observedScene is null) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_scene_unproved");
            stage = "native_mode";
            var nativeBlocker = NativeWalkModeBlocker(player, stationary: false, observedScene,
                finiteWaterCostWithNativePause: true, allowDrift: true);
            if (nativeBlocker is not null) return VoyagerRemoteIdleAdmissionPolicy.Blocked(nativeBlocker);
            stage = "order_queue";
            if (player.orders.orderCount != 0) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_order_queue_active");
            stage = "build_ui";
            if (!BuildUiIsIdle(player)) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_build_ui_active");
            stage = "mecha_work";
            if (!VoyagerMechaWorkIdle(player)) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_mecha_work_active");
            stage = "construction_drones";
            if (!TryObserveOwnedConstructionDrones(player.factory, player.mecha.constructionModule, out var dronesIdle, out _))
                return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_construction_drones_unobserved");
            if (!dronesIdle) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_construction_drones_active");
            stage = "movement_cost";
            var reserve = EvaluateNativeLocalMovementCost(player, modeTransitionsExcluded: true);
            if (!reserve.Proved) return VoyagerRemoteIdleAdmissionPolicy.Blocked(reserve.BlockerCode ?? "remote_idle_movement_cost_unproved");
            stage = "walk_at_rest";
            if (player.movementState != EMovementState.Walk) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_actual_walk_required");
            if (!(player.speed <= .1f)) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_actual_speed_not_at_rest");
            stage = "energy_and_journey";
            var now = DateTimeOffset.UtcNow;
            return VoyagerRemoteIdleAdmissionPolicy.Evaluate(new VoyagerRemoteIdleAdmissionFacts
            {
                MovementReserve = reserve, CoreEnergyJoules = player.mecha.coreEnergy,
                JourneyBudget = new VoyagerLocalActionBudgetFacts
                {
                    ExactRemoteBinding = true, NativePlayerIdle = true, CurrentGameTick = GameMain.gameTick,
                    LastLandedGameTick = lastLandedGameTick, SpentGameTicks = spentGameTicks,
                    MaximumGameTicks = journey.MaximumGameTicks, ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
                    WorkGameTicks = 1, TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks,
                    NowUtc = now, JourneyDeadlineUtc = journey.DeadlineUtc,
                    AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(player.mecha), LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules,
                    WorkPowerUpperWatts = reserve.WorkPowerUpperWatts, TailAndNextTickEnergyJoules = reserve.StopEnergyJoules ?? double.NaN,
                    NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage), OriginalExtraWarperReserve = journey.ExtraWarperReserve,
                },
            });
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
        if (!sameOwner && !_flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence))
            return "remote_resume_original_durable_proof_changed";
        var owner = sameOwner ? existing! : new VoyagerRemoteIdleOwner(new VoyagerNativeBinding(adoption.World, adoption.Player),
            adoption.SessionId, journey.JourneyId, adoption.ResumeActionId, journey.ActiveActionId, adoption);
        var decision = CaptureVoyagerRemoteIdleAdmission(owner, journey, sameOwner ? _voyagerRemoteIdleScene : null,
            journey.SpentGameTicks, journey.LastLandedGameTick, out var scene);
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
                finiteWaterCostWithNativePause: true, allowDrift: true)
            && player.orders.orderCount == 0 && BuildUiIsIdle(player) && VoyagerMechaWorkIdle(player)
            && TryObserveOwnedConstructionDrones(player.factory, m.constructionModule, out var dronesIdle, out _) && dronesIdle;
        return EvaluateNativeLocalMovementCost(player, subset);
    }

    private static VoyagerLocalWalkReserve EvaluateNativeLocalMovementCost(Player player, bool modeTransitionsExcluded)
    {
        var c = player.controller; var w = c.actionWalk; var m = player.mecha;
        return VoyagerLocalWalkReservePolicy.Evaluate(new VoyagerLocalWalkFacts
        {
            ModeTransitionsExcluded = modeTransitionsExcluded, WalkSpeed = m.walkSpeed, WalkPowerWatts = m.walkPower,
            MiningPowerWatts = m.miningPower, OtherWorkPowerWatts = m.replicatePower + m.researchPower,
            ReactorConsumptionRatio = m.reactorPowerConsRatio, RtsSpeed = w.rtsVelocity.magnitude,
            ManualSpeed = w.moveVelocity.magnitude, SoftLandingRecover = c.softLandingRecover,
            IncludePotentialWaterDrift = true, NativeThrustPowerPerAcceleration = m.thrustPowerPerAcc,
            NativeGravityUpperBound = NativeLocalGravityUpperBound(player),
            IndependentMechaCosts = CaptureNativeMechaBackgroundCosts(player),
        });
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

    private static bool NativeWalkModeIsBounded(Player player, bool stationary, NativeLocalWalkSceneBinding? scene = null,
        bool finiteWaterCostWithNativePause = false, bool allowDrift = false) =>
        NativeWalkModeBlocker(player, stationary, scene, finiteWaterCostWithNativePause, allowDrift) is null;

    private static string? NativeWalkModeBlocker(Player player, bool stationary, NativeLocalWalkSceneBinding? scene,
        bool finiteWaterCostWithNativePause, bool allowDrift)
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
        if (!NativeLocalWalkSceneProved(player, scene, finiteWaterCostWithNativePause)) return "remote_idle_scene_unproved";
        if (!NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(CaptureNativeMechaBackgroundCosts(player))) return "remote_idle_independent_mecha_cost_unproved";
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
        if (!(allowDrift && player.movementState == EMovementState.Drift)
            && !Physics.CheckSphere(player.position + player.position.normalized * .15f, .35f, 15873)) return "remote_idle_ground_contact_unproved";
        return null;
    }

    public void UpdateVoyagerLocalPrePlayerTickOnMainThread()
    {
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
                action.VoyagerLocalLastPrePlayerTick = tick;
                action.Plan.VoyagerLocalLastObservedGameTick = tick;
                if (action.VoyagerLocalStopDeadlineTick.HasValue)
                { ObserveVoyagerLocalOrderDrain(action); continue; }
                if (!GuardVoyagerLocalAction(action)) continue;
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
        action.VoyagerLocalStopDeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(VoyagerLocalStopWallSeconds);
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
    }
}
