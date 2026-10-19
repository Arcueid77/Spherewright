using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    public bool VoyagerLocalPrePlayerTickAvailable { get; set; }
    public DateTimeOffset VoyagerLocalPrePlayerObservedAtUtc { get; set; }
    private ActionRecord? _voyagerRemoteIdleOwner;
    private NativeLocalWalkSceneBinding? _voyagerRemoteIdleScene;
    // A separate patience bound, not a 13/60s prediction of physics motion.
    private const double VoyagerLocalStopWallSeconds = 1d;

    private bool VoyagerRemoteIdleBindingMatches()
    {
        var owner = _voyagerRemoteIdleOwner;
        var journey = _flightCheckpoints.VoyagerJourney;
        var b = owner?.Plan.VoyagerBinding;
        return owner is not null && b is not null && journey?.Phase == "remote_landed"
            && journey.JourneyId == owner.Plan.VoyagerJourneyId
            && VoyagerOwnedJourneyMatchesCurrentSession(journey, owner.SessionId)
            && ReferenceEquals(GameMain.data, b.World) && ReferenceEquals(GameMain.mainPlayer, b.Player)
            && ReferenceEquals(b.Player.controller, b.Controller) && ReferenceEquals(b.Player.mecha, b.Mecha)
            && GameMain.localPlanet is { } planet && planet.id == journey.RemotePlanetId
            && ReferenceEquals(b.Player.planetData, planet) && ReferenceEquals(b.Player.factory, planet.factory);
    }

    private void GuardVoyagerRemoteIdleOnMainThread(bool stoppingHost = false)
    {
        // The successful outbound remains successful. Its existing journey
        // continues owning idle energy until a budgeted controller takes over.
        // A terminal local order alone cannot guarantee the next tick stays dry.
        if (GameMain.isPaused) return;
        if (!VoyagerRemoteIdleBindingMatches())
        {
            // A remote primary/Host reload has no in-memory landed-controller
            // handoff. Do not silently recertify it or borrow an old action.
            var retained = _flightCheckpoints.VoyagerJourney;
            if (retained?.Phase == "remote_landed" && _sessions.SessionId is { } sessionId
                && VoyagerOwnedJourneyMatchesCurrentSession(retained, sessionId)
                && GameMain.mainPlayer?.planetId == retained.RemotePlanetId
                && !string.IsNullOrEmpty(retained.ActiveActionId))
            {
                var reason = "The original remote journey has no current landed-controller idle binding; remote local work is not recertified after reload."
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
        var ready = false;
        // Host teardown must attempt Pause before any cost capture can throw.
        if (!stoppingHost) try
        {
        var player = owner.Plan.VoyagerBinding!.Player;
        var reserve = CaptureVoyagerLocalWalkReserve(player, player.currentOrder is null, _voyagerRemoteIdleScene);
        ready = reserve.Proved && player.currentOrder is null
            && player.movementState == EMovementState.Walk && player.speed <= .1f
            && (journey.DeadlineUtc - DateTimeOffset.UtcNow).TotalSeconds
                > journey.ReturnReserveGameTicks / 60d + VoyagerLocalStopWallSeconds + 1d / 60d
            && VoyagerLocalWalkReservePolicy.CanFund(player.mecha.coreEnergy, CalculateAvailableFlightEnergy(player.mecha),
                journey.ReturnEnergyFloorJoules, reserve.PerNativeTickEnergyJoules, VoyagerLocalWalkReservePolicy.StopGameTicks)
            && VoyagerLocalActionBudgetPolicy.Evaluate(new VoyagerLocalActionBudgetFacts
            {
                ExactRemoteBinding = true, NativePlayerIdle = true, CurrentGameTick = GameMain.gameTick,
                LastLandedGameTick = journey.LastLandedGameTick, SpentGameTicks = journey.SpentGameTicks,
                MaximumGameTicks = journey.MaximumGameTicks, ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
                WorkGameTicks = 1, TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks,
                NowUtc = DateTimeOffset.UtcNow, JourneyDeadlineUtc = journey.DeadlineUtc,
                AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(player.mecha), LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules,
                WorkPowerUpperWatts = reserve.WorkPowerUpperWatts, TailAndNextTickEnergyJoules = reserve.StopEnergyJoules ?? double.NaN,
                NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage), OriginalExtraWarperReserve = journey.ExtraWarperReserve,
            }).Proved;
        }
        catch (Exception) { /* An unavailable cost observation cannot skip the energy freeze. */ }
        if (ready) return;
        var message = "The completed outbound's exact remote idle/return reserve is no longer proved. Its successful flight is retained; the original journey requires intervention."
            + FreezeNativeLocalFailureEnergy(VoyagerRemoteIdleBindingMatches());
        _sessions.QuarantineWritesOnMainThread(owner.ActionId, message);
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
        bool finiteWaterCostWithNativePause = false, bool allowDrift = false)
    {
        var c = player.controller; var w = c.actionWalk;
        var vertical = c.vertSpeed;
        return player.isAlive && !GameMain.isPaused && !GameMain.data.disableController && !GameMain.data.guideRunning
            && (player.movementState == EMovementState.Walk || allowDrift && player.movementState == EMovementState.Drift)
            && (c.movementStateInFrame == EMovementState.Walk || allowDrift && c.movementStateInFrame == EMovementState.Drift)
            && !player.warpCommand && player.warpState == 0f && !player.navigation.navigating && !player.navigation.hasTask
            && !c.actionSail.fastTravelling && NativeLocalWalkSceneProved(player, scene, finiteWaterCostWithNativePause)
            && NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(CaptureNativeMechaBackgroundCosts(player))
            && LocalRawMovementInputIdle()
            && c.input0.sqrMagnitude == 0f && c.input1.sqrMagnitude == 0f
            && w.jumpDelayTick <= 0 && w.flyUpChance == 0f && w.moveVelocity.sqrMagnitude == 0f
            && (!finiteWaterCostWithNativePause || c.actionDrift.moveVelocity.sqrMagnitude == 0f
                && !float.IsNaN(c.actionDrift.rtsVelocity.magnitude) && !float.IsInfinity(c.actionDrift.rtsVelocity.magnitude)
                && c.actionDrift.rtsVelocity.magnitude <= player.mecha.walkSpeed + .01f)
            && (!stationary || w.rtsVelocity.sqrMagnitude == 0f)
            && !float.IsNaN(vertical) && !float.IsInfinity(vertical) && (allowDrift || Math.Abs(vertical) <= .01f)
            && c.overridePlayerSpeed >= 0f && c.overridePlayerSpeed <= .01f
            && c.softLandingRecover >= 0f && c.softLandingRecover <= 1f
            && (allowDrift && player.movementState == EMovementState.Drift
                || Physics.CheckSphere(player.position + player.position.normalized * .15f, .35f, 15873));
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
