using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private const int VoyagerControlLeaseSeconds = 30;
    private const long VoyagerLocalActionMaximumTicks = 7200;
    internal bool VoyagerNativeTickAvailable { get; set; }
    internal DateTimeOffset VoyagerLastNativeTickObservedAtUtc { get; set; }
    private bool VoyagerNativeTickFresh => VoyagerNativeTickAvailable
        && DateTimeOffset.UtcNow - VoyagerLastNativeTickObservedAtUtc <= TimeSpan.FromSeconds(2)
        && DateTimeOffset.UtcNow >= VoyagerLastNativeTickObservedAtUtc;

    public GameCallResult<PreparedNormalAction> PrepareVoyagerFlightOnMainThread(
        string? requestedSessionId, PrepareVoyagerFlightRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if (!VoyagerNativeTickFresh) return NotReadyPlan("Fresh advancing native ticks are required for the exact Voyager control hook.");
        var observed = _reader.GetPlayerStateOnMainThread(requestedSessionId, new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!observed.Success || observed.Value is null) return GameCallResult<PreparedNormalAction>.Failed(observed.Error!);
        if (request.ExpectedPlayerStateHash != observed.Value.StateHash
            || !_reader.TryGetVoyagerRouteAnchor(requestedSessionId!, request.PlanetId, request.ExpectedRouteStateHash,
                out var inspectedRequest, out _))
            return StalePlan("Inspect the player and a sufficient Voyager route preview within 30 seconds before preparing.");
        var refreshed = _reader.RefreshVoyagerBudgetOnMainThread(requestedSessionId!, inspectedRequest!);
        if (!refreshed.Success || refreshed.Value is null) return GameCallResult<PreparedNormalAction>.Failed(refreshed.Error!);
        var budget = refreshed.Value;
        if (!budget.BudgetSufficient) return InvalidPlan(budget.Blockers.FirstOrDefault()?.Message ?? "The fresh route budget is insufficient.");
        var player = GameMain.mainPlayer;
        if (!VoyagerMechaWorkIdle(player)) return InvalidPlan("Finish the native mecha replicator/research queues before reserving a finite flight.");
        var journey = _flightCheckpoints.VoyagerJourney;
        var returning = !string.IsNullOrWhiteSpace(request.JourneyId);
        if (returning != budget.IsReturnLeg || request.JourneyId != inspectedRequest!.JourneyId)
            return StalePlan("The prepared leg must match the exact inspected outbound or original-journey return preview.");
        if (returning)
        {
            if (journey is null || journey.JourneyId != request.JourneyId || journey.Phase != "remote_landed"
                || journey.OwnedSaveName != common.Session!.SaveName || journey.GameVersion != common.Session.GameVersion
                || journey.RemotePlanetId != request.PlanetId || budget.DestinationPlanetId != journey.HomePlanetId
                || budget.DestinationStarId != journey.HomeStarId)
                return InvalidPlan("A return must name the exact active landed journey and its original home planet.");
            var rejection = ValidateVoyagerReturnReserve(journey, player);
            if (rejection is not null) return GameCallResult<PreparedNormalAction>.Failed(rejection);
        }
        else if (journey is not null && !IsVoyagerJourneyClosed(journey))
            return InvalidPlan("An existing bounded journey still owns the return reserve; reconcile it or prepare its exact return.");
        var journeyId = returning ? journey!.JourneyId : Guid.NewGuid().ToString("D");
        var playerHash = CanonicalStateHash.PlayerAction(observed.Value);
        var payload = NormalActionPlanPayload.VoyagerFlight(requestedSessionId!, request.PlanetId, journeyId,
            returning, budget, GameStateReader.CopyVoyagerRequest(inspectedRequest!), playerHash,
            VoyagerNativePrerequisiteHash(player), new VoyagerNativeBinding(GameMain.data, player));
        return AddPreparedPlan(payload, common.Session!, returning ? journey!.ReturnReserveGameTicks : budget.OutboundMaximumGameTicks,
            "The exact bounded native flight consumes one real mecha warper, exits warp, approaches the legally visible solid landing target and verifies 600 grounded ticks; the original home/return reserve survives outbound landing.");
    }

    private BridgeError? RevalidateVoyagerFlightOnMainThread(NormalActionPlanPayload plan)
    {
        if (!VoyagerNativeTickFresh || plan.VoyagerBudget is null || plan.VoyagerRequest is null
            || plan.VoyagerBinding is null || !VoyagerBindingMatches(plan))
            return Stale("The original owned world/player/controller or native-tick control hook changed before flight commit.");
        var player = GameMain.mainPlayer;
        var observed = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (!observed.Success || observed.Value is null) return observed.Error;
        if (CanonicalStateHash.PlayerAction(observed.Value) != plan.PlayerStateHash
            || VoyagerNativePrerequisiteHash(player) != plan.VoyagerNativePrerequisiteHash || !VoyagerMechaWorkIdle(player))
            return Stale("The inspected player, native fuel/warper balances, technology or idle control conditions changed before commit.");
        var fresh = _reader.RefreshVoyagerBudgetOnMainThread(plan.SessionId, plan.VoyagerRequest);
        if (!fresh.Success || fresh.Value is null) return fresh.Error;
        if (!fresh.Value.BudgetSufficient) return Stale(fresh.Value.Blockers.First().Message);
        var journey = _flightCheckpoints.VoyagerJourney;
        if (plan.VoyagerReturnLeg)
        {
            if (journey is null || journey.JourneyId != plan.VoyagerJourneyId || journey.Phase != "remote_landed"
                || journey.RemotePlanetId != plan.PlanetId || journey.HomePlanetId != plan.DestinationPlanetId)
                return Stale("The exact landed journey or original home changed before return commit.");
            return ValidateVoyagerReturnReserve(journey, player);
        }
        return journey is not null && !IsVoyagerJourneyClosed(journey)
            ? Stale("Another active journey already owns the finite return reserve.") : null;
    }

    private void InitializeVoyagerFlightNavigationOnMainThread(ActionRecord action)
    {
        var plan = action.Plan;
        var budget = plan.VoyagerBudget!;
        action.VoyagerControlLeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(VoyagerControlLeaseSeconds);
        action.VoyagerSelectedDestinationPlanetId = plan.DestinationPlanetId;
        action.VoyagerLastNativeTick = GameMain.gameTick;
        var targetStar = GameMain.galaxy.StarById(budget.DestinationStarId);
        action.VoyagerWarpNavigationPoint = budget.DestinationIsStarOnly
            ? targetStar.uPosition + (GameMain.mainPlayer.uPosition - targetStar.uPosition).normalized
                * Math.Max(200000d, targetStar.viewRadius + 50000d)
            : GameMain.galaxy.PlanetById(budget.DestinationPlanetId).uPosition;
        action.VoyagerPhase = "checkpoint";
    }

    // Called only by the audited GameLogic.onFactoryFrameBegin after native Player.GameTick.
    internal void UpdateVoyagerNativeTickOnMainThread()
    {
        foreach (var action in _actions.ActiveValues.Where(a => a.Plan.VoyagerBudget is not null).ToArray())
        {
            try { UpdateVoyagerFlightOnMainThread(action); }
            catch (Exception exception)
            {
                action.VoyagerOutcomeUnknown = true;
                RequestVoyagerStop(action, "The original Voyager control outcome is unknown after " + exception.GetType().Name + "; reconcile only this action.");
                if (!VoyagerBindingMatches(action.Plan)) MarkVoyagerOutcomeUnknown(action, action.VoyagerStopReason!);
                else if (action.Plan.VoyagerBinding!.Player.warpCommand && action.VoyagerWarpControl.EntryAttempted)
                    action.Plan.VoyagerBinding.Player.warpCommand = false;
            }
        }
        _actions.RefreshTerminals();
    }

    private void UpdateVoyagerFlightOnMainThread(ActionRecord action)
    {
        if (action.Terminal) return;
        if (!VoyagerBindingMatches(action.Plan))
        {
            MarkVoyagerOutcomeUnknown(action, "The original owned world/player/controller ended; do not touch another world's controls.");
            return;
        }
        var player = action.Plan.VoyagerBinding!.Player;
        var budget = action.Plan.VoyagerBudget!;
        var tick = GameMain.gameTick;
        if (tick == action.VoyagerLastNativeTick) return;
        var clock = EvaluateVoyagerClock(action);
        var delta = tick - action.VoyagerLastNativeTick;
        action.VoyagerLastNativeTick = tick;
        if (clock.ShouldRequestStop) RequestVoyagerStop(action, clock.StopReasonCode ?? "The exact bounded control clock expired.");
        if (player.warpState > 0f || player.warpCommand) action.VoyagerWarpTicks += Math.Max(0L, delta);
        if (!player.isAlive || GameMain.data.disableController || GameMain.data.guideRunning)
            RequestVoyagerStop(action, "Native live/control/guide conditions no longer permit the bounded flight.");
        if (!VoyagerMechaWorkIdle(player) || player.currentOrder is not null && !ReferenceEquals(player.currentOrder, action.PlayerOrder))
            RequestVoyagerStop(action, "Unexpected native mecha work or another player order appeared during the reserved flight.");
        if (!HasVoyagerNextControlReserve(action, player))
            RequestVoyagerStop(action, "The next native control and finite exit/braking allowance would reach the protected return floor.");
        if (tick >= action.VoyagerDeadlineGameTick || DateTimeOffset.UtcNow >= _sessions.EffectiveVoyagerDeadline(action.VoyagerJourney!)
            || DateTimeOffset.UtcNow >= action.VoyagerControlLeaseExpiresAtUtc)
            RequestVoyagerStop(action, "The finite flight deadline or original caller's 30-second observation lease expired.");
        if (action.VoyagerStopReason is not null)
        {
            if (action.VoyagerLandingCharge is not null)
            {
                FailVoyagerLandingCharge(action, action.VoyagerStopReason);
                return;
            }
            UpdateVoyagerStopOnMainThread(action, player);
            CaptureVoyagerReadback(action);
            return;
        }
        var destination = GetEffectiveFlightDestination(action);
        if (player.movementState < EMovementState.Sail)
        {
            action.VoyagerPhase = destination is not null && player.planetId == destination.id ? "landing" : "launch";
            if (destination is not null) UpdateInterplanetaryFlight(action);
            else UpdateNativeFlightLaunch(action, player, null, VoyagerNavigationTarget(action, player));
            CaptureVoyagerReadback(action);
            return;
        }
        ReleaseNativeAscentInput(action);
        var target = VoyagerNavigationTarget(action, player);
        var targetDistance = (target - player.uPosition).magnitude - (destination?.realRadius ?? 0d);
        action.RemainingDistance = Math.Max(0d, targetDistance);
        var exit = VoyagerWarpControlPolicy.CalculateExitDistanceUpperBound(1f, player.mecha.maxWarpSpeed,
            Math.Max(player.mecha.maxSailSpeed, player.uVelocity.magnitude));
        if (!exit.Computed) { RequestVoyagerStop(action, "No finite native warp exit bound is available."); return; }
        var approachDistance = exit.DistanceUpperBoundMeters + Math.Max(12000d, player.mecha.maxSailSpeed * 8d);
        if (action.VoyagerWarpControl.EntryAttempted)
        {
            var shouldExit = clock.ShouldExitWarp || action.VoyagerWarpTicks >= budget.MaximumWarpGameTicksPerLeg - 17L
                || targetDistance <= approachDistance || VoyagerExitEnvelopeNeedsClearance(player, exit.DistanceUpperBoundMeters);
            if (shouldExit && !action.VoyagerExitRequested)
            {
                action.VoyagerExitRequested = true;
                if (player.warpCommand) player.warpCommand = false;
            }
            if (!player.warpCommand && player.warpState == 0f)
            {
                action.VoyagerExitObserved = true;
                if (!action.VoyagerExitRequested && targetDistance > approachDistance)
                {
                    RequestVoyagerStop(action, "Native warp exited before the planned approach; no second entry is allowed for this leg.");
                    UpdateVoyagerStopOnMainThread(action, player);
                }
                else if (GameMain.localStar?.id != budget.DestinationStarId)
                {
                    RequestVoyagerStop(action, "The one finite warp entry ended before native target-system arrival.");
                    UpdateVoyagerStopOnMainThread(action, player);
                }
                else
                {
                    if (!SelectVoyagerLandingAfterExit(action, player))
                    {
                        UpdateVoyagerStopOnMainThread(action, player);
                    }
                    else
                    {
                        action.VoyagerPhase = "approach";
                        UpdateInterplanetaryFlight(action);
                    }
                }
            }
            else
            {
                action.VoyagerPhase = action.VoyagerExitRequested ? "exiting_warp" : "warp_cruise";
                if (!action.VoyagerExitRequested) SteerVoyagerPaidVelocity(player, target, 200d);
            }
        }
        else
        {
            var origin = GameMain.galaxy.PlanetById(action.PlanetId);
            if (origin is not null && ControlNativeSailDeparture(player, origin, target, out _, out _))
                action.VoyagerPhase = "origin_clearance";
            else if (TrySelectVoyagerVisibleDetour(player, target, destination?.id ?? 0, out var detour))
            {
                action.VoyagerPhase = "visible_body_clearance";
                ControlNativeSailTowardDetour(player, detour, out _, out _);
            }
            else
            {
                action.VoyagerPhase = "aligning";
                SteerVoyagerPaidVelocity(player, target, Math.Min(1000d, player.mecha.maxSailSpeed));
                if (VoyagerExitEnvelopeNeedsClearance(player, exit.DistanceUpperBoundMeters))
                {
                    action.VoyagerPhase = "entry_clearance";
                    CaptureVoyagerReadback(action);
                    return;
                }
                var direction = (target - player.uPosition).normalized;
                var heading = VectorLF3.AngleDEG((VectorLF3)(player.uRotation * Vector3.forward), direction);
                var decision = VoyagerWarpControlPolicy.EvaluateEntry(action.VoyagerWarpControl, new VoyagerWarpEntryFacts
                {
                    OwnedBindingVerified = true, ExclusiveBindingVerified = !_actions.ActiveValues.Any(a => a.ActionId != action.ActionId && IsPlayerOrderAction(a.ActionKind)),
                    FactsFresh = true, IsAlive = player.isAlive, IsSailing = player.movementState == EMovementState.Sail,
                    ControllerEnabled = !GameMain.data.disableController, NativeGuideRunning = GameMain.data.guideRunning,
                    LocalPlanetId = GameMain.localPlanet?.id ?? 0, ThrusterLevel = player.mecha.thrusterLevel,
                    WarpCommand = player.warpCommand, WarpState = player.warpState, CoreEnergyJoules = player.mecha.coreEnergy,
                    NativeStartThresholdJoules = budget.WarpStartEnergyThresholdJoules,
                    NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage), CurrentLegRemainingEntries = 1,
                    ReturnRemainingEntries = action.Plan.VoyagerReturnLeg ? 0 : 1,
                    ExtraWarperReserve = action.VoyagerJourney!.ExtraWarperReserve, RemainingBudgetLocked = true,
                    AvailableEnergyJoules = CalculateAvailableFlightEnergy(player.mecha),
                    LockedRemainingLegEnergyJoules = action.Plan.VoyagerReturnLeg ? budget.OutboundEnergyBudgetJoules : budget.OutboundEnergyBudgetJoules,
                    ReturnEnergyFloorJoules = action.Plan.VoyagerReturnLeg ? action.VoyagerJourney.WithdrawalReserveJoules : action.VoyagerJourney.ReturnEnergyFloorJoules,
                    HeadingErrorDegrees = heading, TargetNativelyVisible = destination is not null || budget.DestinationIsStarOnly,
                    // This is the fresh entry/exit envelope of legally visible bodies,
                    // not a resource survey or a promise about hidden destination planets.
                    PathEnvelopeLegallyObservable = true, PathEnvelope = VoyagerWarpPathEnvelope.Clear,
                    ClearPathDistanceMeters = Math.Max(0d, targetDistance - 12000d),
                    MaxWarpSpeed = player.mecha.maxWarpSpeed,
                    OrdinarySpeedUpperBound = Math.Max(player.mecha.maxSailSpeed, player.uVelocity.magnitude),
                });
                if (decision.Allowed)
                {
                    action.VoyagerWarpControl = decision.NextState; // write-ahead one-entry latch
                    var before = CountVoyagerWarpers(player.mecha.warpStorage);
                    var beforePackage = CountVoyagerWarpers(player.package);
                    var used = player.mecha.UseWarper();
                    var after = CountVoyagerWarpers(player.mecha.warpStorage);
                    if (!used || before - after != 1 || beforePackage != CountVoyagerWarpers(player.package))
                    {
                        action.VoyagerOutcomeUnknown = true;
                        RequestVoyagerStop(action, "The sole native UseWarper call did not prove exact chamber consumption; no re-entry or refund is permitted.");
                    }
                    else
                    {
                        action.VoyagerWarpersConsumed = 1;
                        player.warpCommand = true;
                        action.VoyagerPhase = "entering_warp";
                    }
                }
                else if (decision.Blockers.Any(b => b.Code != "warp_heading_not_aligned"))
                {
                    // A local-planet capture is expected during origin clearance;
                    // no warper was consumed and native flight may finish leaving it.
                    if (decision.Blockers.Any(b => b.Code != "warp_heading_not_aligned" && b.Code != "native_sailing_entry_conditions_required"))
                        RequestVoyagerStop(action, decision.Blockers.First(b => b.Code != "warp_heading_not_aligned").Message);
                }
            }
        }
        CaptureVoyagerReadback(action);
    }

    private VectorLF3 VoyagerNavigationTarget(ActionRecord action, Player player)
    {
        if (!action.VoyagerExitObserved) return action.VoyagerWarpNavigationPoint;
        var destination = GetEffectiveFlightDestination(action);
        return destination is not null ? destination.uPosition : action.VoyagerWarpNavigationPoint;
    }

    private static void SteerVoyagerPaidVelocity(Player player, VectorLF3 target, double speed)
    {
        var direction = (target - player.uPosition).normalized;
        var velocity = player.uVelocity;
        if (velocity.magnitude > 0.01d)
        {
            var turned = (VectorLF3)Vector3.RotateTowards((Vector3)velocity,
                (Vector3)(direction * Math.Max(speed, velocity.magnitude)), 0.04f, 0f);
            var delta = turned - velocity;
            player.controller.actionSail.UseSailEnergy(ref delta, 0.36d);
            player.uVelocity += delta;
        }
        var acceleration = Math.Min(7d, Math.Max(0d, speed - player.uVelocity.magnitude));
        if (acceleration > 0d)
            player.uVelocity += direction * (acceleration * player.controller.actionSail.UseSailEnergy(acceleration));
    }

    private bool SelectVoyagerLandingAfterExit(ActionRecord action, Player player)
    {
        if (action.VoyagerSelectedDestinationPlanetId > 0) return true;
        var star = GameMain.localStar;
        var budget = action.Plan.VoyagerBudget!;
        if (star?.id != budget.DestinationStarId || player.warpState != 0f || player.warpCommand)
        { RequestVoyagerStop(action, "Native target-system arrival and warp exit must precede landing-target selection."); return false; }
        var ordinaryDistanceLimit = Math.Max(0d, action.VoyagerDeadlineGameTick - GameMain.gameTick - 3600L)
            / 60d * player.mecha.maxSailSpeed * 0.5d;
        var selected = (star.planets ?? Array.Empty<PlanetData>())
            .Where(p => p is not null && p.type != EPlanetType.Gas && p.realRadius > 0f
                && (p.uPosition - star.uPosition).magnitude <= budget.MaximumArrivalOffsetMeters
                && (p.uPosition - player.uPosition).magnitude <= ordinaryDistanceLimit)
            .OrderBy(p => (p.uPosition - player.uPosition).sqrMagnitude).ThenBy(p => p.id).FirstOrDefault();
        if (selected is null)
        { RequestVoyagerStop(action, "Native arrival exposes no solid landing target inside the declared offset and remaining ordinary-flight window."); return false; }
        action.VoyagerSelectedDestinationPlanetId = selected.id;
        action.TargetObjectId = selected.id;
        return true;
    }

    private static bool VoyagerExitEnvelopeNeedsClearance(Player player, double exitDistance)
    {
        // The ball bounds a turning exit arc, not just an instantaneous velocity
        // ray. Only the natively current star's legally visible bodies are read.
        var star = GameMain.localStar;
        if (star is null) return false;
        if ((star.uPosition - player.uPosition).magnitude <= star.viewRadius + exitDistance + 1000d) return true;
        return (star.planets ?? Array.Empty<PlanetData>()).Any(p => p is not null
            && (p.uPosition - player.uPosition).magnitude <= p.realRadius + exitDistance + 1000d);
    }

    private static bool TrySelectVoyagerVisibleDetour(Player player, VectorLF3 target, int targetPlanetId, out FlightPathDetour detour)
    {
        detour = default;
        var star = GameMain.localStar;
        if (star is null) return false;
        FlightPathDetour? selected = null;
        var current = new FlightPathPoint(player.uPosition.x, player.uPosition.y, player.uPosition.z);
        var end = new FlightPathPoint(target.x, target.y, target.z);
        foreach (var body in star.planets ?? Array.Empty<PlanetData>())
        {
            if (body is null || body.id == targetPlanetId) continue;
            if (InterplanetaryFlightPathAvoidance.TryCreateDetour(current, end, body.id,
                new FlightPathPoint(body.uPosition.x, body.uPosition.y, body.uPosition.z), body.realRadius, out var candidate)
                && InterplanetaryFlightPathAvoidance.IsPreferred(candidate, selected)) selected = candidate;
        }
        if (InterplanetaryFlightPathAvoidance.TryCreateDetour(current, end, star.id * 100,
            new FlightPathPoint(star.uPosition.x, star.uPosition.y, star.uPosition.z), star.viewRadius, out var stellar)
            && InterplanetaryFlightPathAvoidance.IsPreferred(stellar, selected)) selected = stellar;
        if (!selected.HasValue) return false;
        detour = selected.Value;
        return true;
    }

    private void RequestVoyagerStop(ActionRecord action, string reason)
    {
        action.VoyagerStopReason ??= reason;
        action.VoyagerStopDeadlineGameTick ??= GameMain.gameTick + 1800L;
        action.VoyagerStopDeadlineUtc ??= DateTimeOffset.UtcNow.AddSeconds(30);
        action.VoyagerPhase = "stopping";
        action.State = NormalActionStates.WaitingForGame;
        action.Message = action.VoyagerStopReason + " Native exit and normal braking are still being reconciled under the original action.";
    }

    private void FinishVoyagerBeforeLaunchFailure(ActionRecord action, string message)
    {
        if (action.VoyagerJourney is not null
            && !_flightCheckpoints.TryCloseVoyagerBeforeNativeLaunch(action.Plan.VoyagerJourneyId!, action.ActionId, out var rejection))
        {
            MarkVoyagerOutcomeUnknown(action, message + " " + rejection);
            return;
        }
        action.State = NormalActionStates.ActionFailed;
        action.Terminal = true;
        action.Succeeded = false;
        action.RecoveryRequired = false;
        action.CompletedAtGameTick = VoyagerBindingMatches(action.Plan) ? GameMain.gameTick : action.VoyagerLastNativeTick;
        action.VoyagerPhase = "prelaunch_failed";
        action.Message = message + " No native launch or warp entry was dispatched.";
        if (VoyagerBindingMatches(action.Plan)) action.AfterInventory = CaptureInventory(action.Plan.VoyagerBinding!.Player);
        CaptureVoyagerReadback(action);
    }

    private void UpdateVoyagerStopOnMainThread(ActionRecord action, Player player)
    {
        ReleaseNativeAscentInput(action);
        AbortPlayerOrderIfOwned(action);
        var stop = VoyagerWarpControlPolicy.EvaluateStop(action.VoyagerWarpControl, true, true,
            player.warpCommand, player.warpState, player.uVelocity.magnitude);
        action.VoyagerWarpControl = stop.NextState;
        if (stop.ClearOwnedWarpCommand) player.warpCommand = false;
        if (player.movementState == EMovementState.Sail && stop.NormalBrakingRequired)
        {
            var braking = player.uVelocity * 0.05d;
            var exactUpperCost = 3d * player.mecha.thrustPowerPerAcc * player.mecha.reactorPowerConsRatio
                * player.controller.actionSail.energy_scale * braking.magnitude * 1.5d;
            if (!VoyagerCanSpendBeforeFloor(action, player, exactUpperCost))
            {
                TryAccountVoyagerLeg(action, "outcome_unknown", out _);
                MarkVoyagerOutcomeUnknown(action, "The protected return floor forbids further paid braking; native exit remains under the original action.");
                return;
            }
            player.controller.actionSail.UseSailEnergy(ref braking, 1.5d);
            player.uVelocity -= braking;
        }
        else if (player.movementState == EMovementState.Fly)
        {
            player.controller.actionFly.targetAltitude = 1f;
            player.controller.actionFly.moveVelocity = Vector3.zero;
            player.controller.actionFly.rtsVelocity = Vector3.zero;
        }
        var grounded = player.movementState == EMovementState.Walk && player.speed <= 0.1f;
        if (stop.StopSettled || (grounded && player.warpState == 0f && !player.warpCommand))
        {
            action.VoyagerStopSettled = true;
            action.VoyagerExitObserved = player.warpState == 0f && !player.warpCommand;
            if (!TryAccountVoyagerLeg(action, action.VoyagerOutcomeUnknown ? "outcome_unknown" : "recovery_required", out var rejection))
                MarkVoyagerOutcomeUnknown(action, rejection);
            else if (action.VoyagerOutcomeUnknown) MarkVoyagerOutcomeUnknown(action, action.VoyagerStopReason!);
            else Fail(action, action.VoyagerStopReason!);
        }
        else if (GameMain.gameTick >= action.VoyagerStopDeadlineGameTick || DateTimeOffset.UtcNow >= action.VoyagerStopDeadlineUtc)
        {
            TryAccountVoyagerLeg(action, "outcome_unknown", out _);
            MarkVoyagerOutcomeUnknown(action, "The bounded native stop did not prove both exit and braking; preserve the original action/checkpoint.");
        }
    }

    private VoyagerControlDeadlineDecision EvaluateVoyagerClock(ActionRecord action) =>
        VoyagerControlDeadlinePolicy.Evaluate(new VoyagerControlDeadlineFacts
        {
            CurrentGameTick = GameMain.gameTick, LastControlledTick = action.VoyagerLastNativeTick,
            FlightDeadlineTick = action.VoyagerDeadlineGameTick,
            JourneyDeadlineUtc = action.VoyagerJourney is { } stopJourney ? _sessions.EffectiveVoyagerDeadline(stopJourney) : null,
            ControlLeaseExpiresAtUtc = action.VoyagerControlLeaseExpiresAtUtc,
            StopRequested = action.VoyagerStopReason is not null, StopDeadlineGameTick = action.VoyagerStopDeadlineGameTick,
            StopDeadlineUtc = action.VoyagerStopDeadlineUtc, NativeStopSettled = action.VoyagerStopSettled,
            WarpGameTicks = action.VoyagerWarpTicks, MaximumWarpGameTicks = action.Plan.VoyagerBudget?.MaximumWarpGameTicksPerLeg,
            NowUtc = DateTimeOffset.UtcNow,
        });

    private void CheckVoyagerControlWatchdogOnMainThread()
    {
        foreach (var action in _actions.ActiveValues.Where(a => a.Plan.VoyagerBudget is not null).ToArray())
        {
            if (!VoyagerBindingMatches(action.Plan))
            {
                MarkVoyagerOutcomeUnknown(action, "The original owned Voyager binding ended; retain its last proved tick and original checkpoint.");
                continue;
            }
            var clock = EvaluateVoyagerClock(action);
            if (action.VoyagerLandingCharge is not null && (!VoyagerLandingChargeGuardFresh()
                || clock.ShouldRequestStop || action.VoyagerStopReason is not null))
            {
                FailVoyagerLandingCharge(action, action.VoyagerStopReason ?? clock.StopReasonCode
                    ?? "The original landing recharge lost its fresh pre-player observer.");
                continue;
            }
            if (clock.ShouldRequestStop)
            {
                RequestVoyagerStop(action, clock.StopReasonCode ?? "The original bounded control lease ended.");
                var player = action.Plan.VoyagerBinding!.Player;
                if (action.VoyagerWarpControl.EntryAttempted && player.warpCommand) player.warpCommand = false;
            }
            if (clock.StopOutcomeUnknown)
            {
                TryAccountVoyagerLeg(action, "outcome_unknown", out _);
                MarkVoyagerOutcomeUnknown(action, "The locked stop deadline elapsed without native exit/braking proof; do not rekey the flight.");
                CaptureVoyagerReadback(action);
            }
        }
    }

    private static double VoyagerProtectedFloor(ActionRecord action) => action.Plan.VoyagerReturnLeg
        ? action.VoyagerJourney!.WithdrawalReserveJoules : action.VoyagerJourney!.ReturnEnergyFloorJoules;

    private static double VoyagerExitEnergyUpper(Player player)
    {
        if (!player.warpCommand && player.warpState == 0f) return 0d;
        return (player.mecha.warpStartPowerPerSpeed + player.mecha.warpKeepingPowerPerSpeed)
            * player.mecha.maxWarpSpeed * player.mecha.reactorPowerConsRatio
            * player.controller.actionSail.energy_scale * 17d / 60d;
    }

    private static bool VoyagerCanSpendBeforeFloor(ActionRecord action, Player player, double cost)
    {
        return VoyagerControlReservePolicy.CanSpendBeforeFloor(CalculateAvailableFlightEnergy(player.mecha),
            VoyagerProtectedFloor(action), cost, VoyagerExitEnergyUpper(player));
    }

    private static bool HasVoyagerNextControlReserve(ActionRecord action, Player player)
    {
        if (action.VoyagerJourney is null) return !action.VoyagerNativeLaunchStarted;
        var speed = Math.Max(player.mecha.maxSailSpeed, player.uVelocity.magnitude);
        // Both local approach helpers can turn relative to an observed moving body.
        foreach (var id in new[] { action.PlanetId, action.VoyagerSelectedDestinationPlanetId })
            if (id > 0)
            {
                var astro = GameMain.galaxy.astrosData[id];
                speed = Math.Max(speed, player.uVelocity.magnitude + ((astro.uPosNext - astro.uPos) * 60d).magnitude);
            }
        var unexpectedMechaPower = (Math.Max(0d, player.mecha.replicatePower) + Math.Max(0d, player.mecha.researchPower)
            + Math.Max(0d, player.mecha.miningPower) + Math.Max(0d, player.mecha.walkPower)) * Math.Max(1d, player.mecha.reactorPowerConsRatio);
        return VoyagerControlReservePolicy.Evaluate(new VoyagerControlReserveFacts
        {
            AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(player.mecha),
            ProtectedReturnFloorJoules = VoyagerProtectedFloor(action),
            ThrustPowerPerAcceleration = player.mecha.thrustPowerPerAcc,
            EnergyConsumptionCoefficient = player.mecha.reactorPowerConsRatio,
            NativeEnergyScale = player.controller.actionSail.energy_scale, OrdinarySpeedUpperBound = speed,
            WarpStartPowerPerSpeed = player.mecha.warpStartPowerPerSpeed,
            WarpKeepingPowerPerSpeed = player.mecha.warpKeepingPowerPerSpeed,
            MaxWarpSpeed = player.mecha.maxWarpSpeed, HasNativeWarpTail = player.warpCommand || player.warpState > 0f,
            ExtraMechaPowerUpperBoundWatts = unexpectedMechaPower,
        }).Proved;
    }

    private void MarkVoyagerOutcomeUnknown(ActionRecord action, string message)
    {
        if (action.VoyagerLandingCharge is not null)
        {
            message += FreezeNativeLocalFailureEnergy(VoyagerBindingMatches(action.Plan));
            action.VoyagerLandingCharge = null;
        }
        // A rejected return launch may leave durable phase=return before any
        // Native controller took over. The remote idle owner cannot guard that
        // anomalous phase; freeze before further checkpoint/accounting I/O.
        if (action.Plan.VoyagerReturnLeg && !action.VoyagerNativeLaunchStarted)
            message += FreezeNativeLocalFailureEnergy(VoyagerBindingMatches(action.Plan));
        action.VoyagerOutcomeUnknown = true;
        action.State = NormalActionStates.OutcomeUnknown;
        action.Terminal = true;
        action.Succeeded = false;
        action.RecoveryRequired = !string.IsNullOrEmpty(action.FlightCheckpointId);
        action.Message = message;
        action.CompletedAtGameTick = VoyagerBindingMatches(action.Plan) ? GameMain.gameTick : action.VoyagerLastNativeTick;
        action.OriginalOutcomeMessage ??= message;
        if (!action.VoyagerLandingPendingPersistence && !string.IsNullOrEmpty(action.FlightCheckpointId))
        {
            var observedTick = VoyagerBindingMatches(action.Plan) ? GameMain.gameTick : action.VoyagerLastNativeTick;
            _flightCheckpoints.TryMarkRecoveryRequired(action.FlightCheckpointId!, action.ActionId, observedTick, out _);
            if (action.VoyagerJourney is not null) TryAccountVoyagerLeg(action, "outcome_unknown", out _);
        }
        if (action.VoyagerFlightReadback is not null && !VoyagerBindingMatches(action.Plan))
            action.VoyagerFlightReadback.BindingVerified = false;
        if (VoyagerBindingMatches(action.Plan))
        {
            action.AfterInventory = CaptureInventory(action.Plan.VoyagerBinding!.Player);
            _sessions.QuarantineWritesOnMainThread(action.ActionId, message);
        }
    }

    internal void StopVoyagerForHostShutdownOnMainThread()
    {
        VoyagerNativeTickAvailable = false;
        foreach (var action in _actions.ActiveValues.Where(a => a.Plan.VoyagerBudget is not null).ToArray())
        {
            if (action.VoyagerLandingCharge is not null)
            {
                FailVoyagerLandingCharge(action, "The Host stopped before the original landing recharge could hand off to its strict idle guard.");
                continue;
            }
            if (VoyagerBindingMatches(action.Plan))
            {
                var player = action.Plan.VoyagerBinding!.Player;
                if (action.VoyagerWarpControl.EntryAttempted && player.warpCommand) player.warpCommand = false;
                ReleaseNativeAscentInput(action);
                AbortPlayerOrderIfOwned(action);
            }
            RequestVoyagerStop(action, "The Host stopped before native exit/landing reconciliation; preserve the original flight and checkpoint.");
            TryAccountVoyagerLeg(action, "outcome_unknown", out _);
            MarkVoyagerOutcomeUnknown(action, action.VoyagerStopReason!);
            CaptureVoyagerReadback(action);
        }
    }

    public GameCallResult<PreparedNormalAction> PrepareCancelVoyagerFlightOnMainThread(
        string? requestedSessionId, PrepareCancelVoyagerFlightRequest request)
    {
        var session = _sessions.CaptureOnMainThread();
        if (request.StateHashVersion == 1 && session.SessionId == requestedSessionId
            && _sessions.CanStopIdleReturnOnlyTask(request.ActionId, requestedSessionId, request.PlanetId)
            && !_actions.ActiveValues.Any(a => !a.Terminal))
        {
            var stoppedTask = NormalActionPlanPayload.CancelVoyagerFlight(requestedSessionId!, request.PlanetId, request.ActionId);
            stoppedTask.ReturnOnlyTaskCancellation = true;
            return AddPreparedPlan(stoppedTask, session, 1,
                "Normal Native Pause stops this exact idle return-only task; its budget is not renewed and Save still requires all other health gates.");
        }
        if (request.StateHashVersion != 1 || !session.GameLoaded || !session.OwnedBySpherewright
            || session.SessionId != requestedSessionId || !_actions.TryGetValue(request.ActionId, out var flight)
            || flight.Terminal || flight.Plan.VoyagerBudget is null || flight.PlanetId != request.PlanetId
            || flight.SessionId != requestedSessionId || !VoyagerBindingMatches(flight.Plan))
            return InvalidPlan("Cancellation must bind the exact active original Voyager action, owned session and origin planet.");
        var payload = NormalActionPlanPayload.CancelVoyagerFlight(requestedSessionId!, request.PlanetId, request.ActionId);
        return AddPreparedPlan(payload, session, 600,
            "The original flight receives a sticky stop request. Only its result can prove native warp exit, normal braking and checkpoint recovery eligibility.");
    }

    private BridgeError? RevalidateCancelVoyagerFlightOnMainThread(NormalActionPlanPayload plan) =>
        plan.ReturnOnlyTaskCancellation ? _sessions.CanStopIdleReturnOnlyTask(plan.CancelVoyagerActionId!, plan.SessionId, plan.PlanetId)
            ? null : Stale("The exact idle return-only task binding changed before cancellation.")
        : !_actions.TryGetValue(plan.CancelVoyagerActionId!, out var flight) || flight.Terminal
        || flight.Plan.VoyagerBudget is null || flight.SessionId != plan.SessionId || !VoyagerBindingMatches(flight.Plan)
            ? Stale("The original Voyager flight is no longer active in the bound owned world.") : null;

    private void ExecuteCancelVoyagerFlightOnMainThread(ActionRecord cancellation)
    {
        if (cancellation.Plan.ReturnOnlyTaskCancellation)
        {
            if (!_sessions.StopIdleReturnOnlyTask(cancellation.Plan.CancelVoyagerActionId!, cancellation.SessionId, cancellation.PlanetId))
                throw new InvalidOperationException("Normal Pause of the exact idle return-only task was not proved.");
            Complete(cancellation, "The exact idle return-only task is normally paused and cannot resume automatically; original outcomes and budgets are preserved.");
            return;
        }
        if (!_actions.TryGetValue(cancellation.Plan.CancelVoyagerActionId!, out var flight))
            throw new InvalidOperationException("The exact original flight is missing.");
        RequestVoyagerStop(flight, "An explicitly prepared cancellation stopped the original finite voyage.");
        cancellation.TargetObjectId = flight.VoyagerSelectedDestinationPlanetId > 0 ? flight.VoyagerSelectedDestinationPlanetId : (int?)null;
        Complete(cancellation, "The sticky stop request was attached to the original flight; poll that action for actual exit/braking/checkpoint proof.");
    }

    private bool VoyagerBindingMatches(NormalActionPlanPayload plan) => plan.VoyagerBinding is not null
        && _sessions.IsCurrentSessionOwned && _sessions.SessionId == plan.SessionId
        && ReferenceEquals(GameMain.data, plan.VoyagerBinding.World)
        && ReferenceEquals(GameMain.mainPlayer, plan.VoyagerBinding.Player)
        && ReferenceEquals(GameMain.mainPlayer?.controller, plan.VoyagerBinding.Controller)
        && ReferenceEquals(GameMain.mainPlayer?.mecha, plan.VoyagerBinding.Mecha);

    private static bool VoyagerMechaWorkIdle(Player player) => player?.mecha?.forge is not null
        && player.mecha.forge.tasks.Count == 0 && GameMain.history is not null && !GameMain.history.techQueue.Any(id => id > 0);

    private static string VoyagerNativePrerequisiteHash(Player player) => CanonicalStateHash.Combine(
        "voyager-native-flight-inputs-v1", player.mecha.thrusterLevel, player.mecha.maxSailSpeed, player.mecha.maxWarpSpeed,
        player.mecha.coreEnergyCap, player.mecha.warpStartPowerPerSpeed, player.mecha.warpKeepingPowerPerSpeed,
        player.mecha.thrustPowerPerAcc, player.mecha.reactorPowerConsRatio, player.mecha.corePowerGen,
        player.mecha.reactorPowerGen, player.mecha.autoReplenishWarper, CountVoyagerWarpers(player.mecha.warpStorage),
        CapturePlayerPackageState(player), string.Join(";", player.mecha.reactorStorage.grids.Select(g => $"{g.itemId}:{g.count}:{g.inc}")));

    private static int CountVoyagerWarpers(StorageComponent storage) => storage.GetItemCount(1210);
    private static bool IsVoyagerJourneyClosed(VoyagerJourneyRecord journey) => journey.Phase == "completed"
        || journey.Phase == "recovered_closed" || journey.Phase == "prelaunch_failed";

    private BridgeError? ValidateVoyagerReturnReserve(VoyagerJourneyRecord journey, Player player)
    {
        var elapsedRemote = journey.LastLandedGameTick.HasValue ? Math.Max(0L, GameMain.gameTick - journey.LastLandedGameTick.Value) : 0L;
        var remaining = journey.ReturnOnlyTask is null ? _sessions.EffectiveVoyagerMaximumGameTicks(journey) - journey.SpentGameTicks - elapsedRemote
            : journey.RemainingReturnOnlyTicks(_sessions.SessionId, GameMain.gameTick) ?? -1;
        if (DateTimeOffset.UtcNow >= _sessions.EffectiveVoyagerDeadline(journey)
            || journey.ReturnOnlyTask is not null && _sessions.ReturnOnlyTaskStopped
            || (journey.ReturnOnlyTask is null ? remaining < journey.ReturnReserveGameTicks
                : VoyagerReturnOnlyRecoveryPolicy.FlightTicks(remaining, journey.ReturnReserveGameTicks) < 3600)
            || CalculateAvailableFlightEnergy(player.mecha) < journey.ReturnEnergyFloorJoules
            || player.mecha.coreEnergy < journey.ReturnCoreEnergyMinimumJoules
            || CountVoyagerWarpers(player.mecha.warpStorage) < 1 + journey.ExtraWarperReserve)
            return Stale("The original journey requires a fresh charged core, loaded return/withdrawal fuel, native warpers and its unchanged finite return window.");
        return null;
    }

    private BridgeError? ValidateVoyagerJourneyLocalCommit(NormalActionPlanPayload plan)
    {
        var returnOnly = _flightCheckpoints.VoyagerJourney;
        if (returnOnly?.ReturnOnlyScopeActive == true && plan.ActionKind != NormalActionKinds.Save
            && plan.ActionKind != NormalActionKinds.CancelVoyagerFlight
            && !(plan.VoyagerReturnLeg && plan.VoyagerBudget is not null))
            return Stale(VoyagerReturnOnlyRecoveryPolicy.ScopeMessage);
        if (IsStructuredConstruction(plan))
            return plan.ActionKind == NormalActionKinds.CancelBlueprintBuild
                ? ValidateStructuredConstructionCancellation(plan) : ValidateStructuredConstructionBudget(plan);
        var journey = _flightCheckpoints.VoyagerJourney;
        if (journey is null || IsVoyagerJourneyClosed(journey)) return null;
        if (!VoyagerOwnedJourneyMatchesCurrentSession(journey, plan.SessionId))
            return Stale("The active journey belongs to another exact owned save or Native version; its local-action budget cannot be borrowed.");
        if (plan.VoyagerBudget is not null || plan.ActionKind == NormalActionKinds.CancelVoyagerFlight) return null;
        if (journey.ReturnOnlyScopeActive && plan.ActionKind == NormalActionKinds.Save) return null;
        if (journey.Phase == "home_awaiting_save" && plan.ActionKind == NormalActionKinds.Save) return null;
        if (journey.Phase != "remote_landed" || journey.RemotePlanetId != plan.PlanetId)
            return Stale("The active Voyager action/checkpoint must be reconciled before unrelated writes.");
        if (!VoyagerRemoteIdleBindingMatches())
            return Stale("Remote local work requires this live landed-controller idle binding; a remote Host/save reload is not silently recertified.");
        if (plan.ActionKind == NormalActionKinds.Refuel || plan.ActionKind == NormalActionKinds.Save) return null;
        var immediate = plan.ActionKind == NormalActionKinds.Transfer || plan.ActionKind == NormalActionKinds.LogisticsStationFleetTransfer
            || plan.ActionKind == NormalActionKinds.ConfigureBuilding
                && (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationStorage
                    || plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt
                    || plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge);
        if (!immediate && plan.ActionKind != NormalActionKinds.Move && plan.ActionKind != NormalActionKinds.Harvest)
            return Stale("The remote reserve admits finite move/harvest, exact station configuration, ordinary package/storage or fleet transfer, native refuel and save only; construction needs its separately proved native drone lease.");
        var player = GameMain.mainPlayer;
        var upperPower = (Math.Max(0d, player.mecha.walkPower) * Math.Max(2d, player.mecha.walkSpeed)
            + Math.Max(0d, player.mecha.miningPower) + Math.Max(0d, player.mecha.replicatePower) + Math.Max(0d, player.mecha.researchPower))
            * Math.Max(1d, player.mecha.reactorPowerConsRatio);
        var workTicks = immediate ? 1 : VoyagerLocalActionMaximumTicks;
        var tailTicks = immediate ? 0 : VoyagerLocalWalkReservePolicy.StopGameTicks;
        var tailEnergy = upperPower / 60d;
        if (!immediate)
        {
            plan.VoyagerLocalWalkScene = CaptureNativeLocalWalkScene();
            var walk = CaptureVoyagerLocalWalkReserve(player, player.currentOrder is null, plan.VoyagerLocalWalkScene);
            if (!VoyagerLocalPrePlayerTickAvailable
                || DateTimeOffset.UtcNow - VoyagerLocalPrePlayerObservedAtUtc > TimeSpan.FromSeconds(1)
                || plan.VoyagerLocalWalkScene is null || !walk.Proved || !VoyagerLocalWalkReservePolicy.CanFund(player.mecha.coreEnergy,
                    CalculateAvailableFlightEnergy(player.mecha), journey.ReturnEnergyFloorJoules,
                    walk.PerNativeTickEnergyJoules, VoyagerLocalWalkReservePolicy.StopGameTicks))
                return Stale("The exact Native pre-player hook and narrow ordinary RTS Walk/Mine stop reserve are required; Fly, Drift, jump and user input are unproved.");
            upperPower = walk.WorkPowerUpperWatts!.Value;
            tailEnergy = walk.StopEnergyJoules!.Value;
        }
        var nowUtc = DateTimeOffset.UtcNow;
        // Native movement energy is counted in ticks; dynamic UPS means its
        // cleanup wall allowance must be reserved independently, not as13/60s.
        var cleanupWallSlack = Math.Max(0d, VoyagerLocalStopWallSeconds - tailTicks / 60d);
        var localDeadlineUtc = VoyagerLocalActionBudgetPolicy.WorkDeadlineUtc(nowUtc,
            _sessions.EffectiveVoyagerDeadline(journey).AddSeconds(-cleanupWallSlack),
            workTicks, journey.ReturnReserveGameTicks, tailTicks);
        if (!localDeadlineUtc.HasValue) return Stale("The original return wall-time reserve leaves no finite local work window.");
        var budget = VoyagerLocalActionBudgetPolicy.Evaluate(new VoyagerLocalActionBudgetFacts
        {
            ExactRemoteBinding = _sessions.IsCurrentSessionOwned && _sessions.SessionId == plan.SessionId
                && player.planetId == plan.PlanetId && GameMain.localPlanet?.factory is not null,
            NativePlayerIdle = player.isAlive && player.movementState == EMovementState.Walk && player.speed <= .1f
                && player.currentOrder is null && !player.warpCommand && player.warpState == 0f && VoyagerMechaWorkIdle(player)
                && player.mecha.constructionModule.allDronesIdle,
            CurrentGameTick = GameMain.gameTick, LastLandedGameTick = journey.LastLandedGameTick,
            SpentGameTicks = journey.SpentGameTicks, MaximumGameTicks = _sessions.EffectiveVoyagerMaximumGameTicks(journey),
            ReturnReserveGameTicks = journey.ReturnReserveGameTicks, WorkGameTicks = workTicks, TailGameTicks = tailTicks,
            NowUtc = nowUtc, JourneyDeadlineUtc = _sessions.EffectiveVoyagerDeadline(journey),
            AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(player.mecha),
            LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules, WorkPowerUpperWatts = upperPower,
            TailAndNextTickEnergyJoules = tailEnergy,
            NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage), OriginalExtraWarperReserve = journey.ExtraWarperReserve,
        });
        if (!budget.Proved) return Stale("The fresh remote action does not preserve the original loaded return floor, native warpers and finite time (" + budget.BlockerCode + ").");
        plan.VoyagerLocalDeadlineGameTick = GameMain.gameTick + workTicks;
        plan.VoyagerLocalStartedGameTick = GameMain.gameTick;
        plan.VoyagerLocalLastObservedGameTick = GameMain.gameTick;
        plan.VoyagerLocalStartedAtUtc = nowUtc;
        plan.VoyagerLocalDeadlineUtc = localDeadlineUtc;
        plan.VoyagerLocalOwnedSaveName = journey.OwnedSaveName;
        plan.VoyagerLocalGameVersion = journey.GameVersion;
        plan.VoyagerLocalJourneyId = journey.JourneyId;
        plan.VoyagerLocalBinding = new VoyagerNativeBinding(GameMain.data, player);
        plan.VoyagerLocalFactory = GameMain.localPlanet!.factory;
        return null;
    }

    private bool GuardVoyagerLocalAction(ActionRecord action)
    {
        if (!action.Plan.VoyagerLocalDeadlineGameTick.HasValue) return true;
        if (!VoyagerLocalReferencesMatch(action))
        {
            // Retain the original observation. Do not abort a different world's
            // order, capture its inventory, or quarantine its current session.
            action.State = NormalActionStates.OutcomeUnknown;
            action.Terminal = true;
            action.Succeeded = false;
            action.CompletedAtGameTick = action.Plan.VoyagerLocalLastObservedGameTick;
            action.FailureKind = "voyager_local_binding_lost";
            action.Message = "The exact original owned-world/Native/player/controller/mecha/factory binding ended. The original local action is unknown; retain its action and prior evidence without replay.";
            return false;
        }
        action.Plan.VoyagerLocalLastObservedGameTick = GameMain.gameTick;
        if (IsVoyagerLocalOrder(action) && action.VoyagerLocalStopDeadlineTick.HasValue) return false;
        var journey = _flightCheckpoints.VoyagerJourney;
        var localPlayer = action.Plan.VoyagerLocalBinding!.Player;
        if (journey is null || journey.JourneyId != action.Plan.VoyagerLocalJourneyId || journey.Phase != "remote_landed"
            || !VoyagerOwnedJourneyMatchesCurrentSession(journey, action.SessionId)
            || IsVoyagerLocalOrder(action) && DateTimeOffset.UtcNow >= action.VoyagerLocalObservationLeaseExpiresAtUtc
            || !VoyagerLocalActionBudgetPolicy.LocalWindowStillOpen(action.Plan.VoyagerLocalStartedGameTick,
                GameMain.gameTick, action.Plan.VoyagerLocalDeadlineGameTick.Value, action.Plan.VoyagerLocalStartedAtUtc,
                DateTimeOffset.UtcNow, action.Plan.VoyagerLocalDeadlineUtc)
            || CalculateAvailableFlightEnergy(localPlayer.mecha) <= journey.ReturnEnergyFloorJoules)
        {
            if (IsVoyagerLocalOrder(action))
            {
                RequestVoyagerLocalOrderStop(action, false, "The original finite remote local-action allowance ended; reassess the unchanged journey before another action.");
                return false;
            }
            AbortPlayerOrderIfOwned(action);
            Fail(action, "The original finite remote local-action allowance ended. Its owned order stopped; reassess the unchanged journey and return reserves before another action.");
            return false;
        }
        return true;
    }

    private bool VoyagerLocalReferencesMatch(ActionRecord action)
    {
        var p = action.Plan;
        if (p.VoyagerLocalBinding is not { } b || !_sessions.IsCurrentSessionOwned || _sessions.SessionId != action.SessionId
            || !ReferenceEquals(GameMain.data, b.World) || !ReferenceEquals(GameMain.mainPlayer, b.Player)
            || !ReferenceEquals(b.Player.controller, b.Controller) || !ReferenceEquals(b.Player.mecha, b.Mecha)
            || !ReferenceEquals(GameMain.localPlanet?.factory, p.VoyagerLocalFactory)
            || !ReferenceEquals(GameMain.data.localLoadedPlanetFactory, p.VoyagerLocalFactory)
            || b.Player.planetId != action.PlanetId || p.VoyagerLocalFactory?.planetId != action.PlanetId) return false;
        var session = _sessions.CaptureOnMainThread();
        return session.SessionId == action.SessionId && VoyagerLocalActionBudgetPolicy.MatchesOwnedJourneyIdentity(
            p.VoyagerLocalOwnedSaveName, p.VoyagerLocalGameVersion, session.OwnedBySpherewright,
            _sessions.OwnedSaveName, session.SaveName, session.GameVersion);
    }

    private bool TryAccountVoyagerLeg(ActionRecord action, string phase, out string rejection)
    {
        var journey = action.VoyagerJourney ?? _flightCheckpoints.VoyagerJourney;
        if (journey is null) { rejection = "The original protected Voyager reserve is missing."; return false; }
        if (!action.VoyagerLegAccounted)
        {
            var observedTick = VoyagerBindingMatches(action.Plan) ? GameMain.gameTick : action.VoyagerLastNativeTick;
            var spent = VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(journey.SpentGameTicks,
                action.StartedAtGameTick, observedTick, legAlreadyAccounted: false);
            if (!spent.HasValue) { rejection = "The original Voyager leg accounting is outside its finite range."; return false; }
            journey.SpentGameTicks = spent.Value;
            journey.LastAccountedGameTick = observedTick;
            journey.LastAccountedFlightActionId = action.ActionId;
            action.VoyagerLegAccounted = true;
        }
        journey.Phase = phase;
        journey.RemotePlanetId = action.Plan.VoyagerReturnLeg ? journey.RemotePlanetId : action.VoyagerSelectedDestinationPlanetId > 0 ? action.VoyagerSelectedDestinationPlanetId : (int?)null;
        if (phase == "remote_landed" || phase == "home_awaiting_save")
        {
            action.VoyagerLandedAtGameTick ??= GameMain.gameTick;
            journey.LastLandedGameTick = action.VoyagerLandedAtGameTick.Value;
        }
        action.VoyagerJourney = journey;
        return _flightCheckpoints.TryPersistVoyagerJourney(journey, out rejection);
    }

    private bool TrySealVoyagerLanding(ActionRecord action, out string rejection)
    {
        var player = GameMain.mainPlayer;
        if (!VoyagerBindingMatches(action.Plan) || player.warpCommand || player.warpState != 0f
            || player.planetId != action.VoyagerSelectedDestinationPlanetId || player.movementState != EMovementState.Walk)
        { rejection = "Actual alive/grounded target landing and native warp exit were not jointly proved."; return false; }
        action.VoyagerPhase = action.Plan.VoyagerReturnLeg ? "home_landed_awaiting_save" : "remote_landed";
        return TryAccountVoyagerLeg(action, action.Plan.VoyagerReturnLeg ? "home_awaiting_save" : "remote_landed", out rejection);
    }

    private void CompleteVoyagerLanding(ActionRecord action, string message)
    {
        if (action.Terminal && action.Succeeded) return; // Never rewrite a completed original flight.
        if (!VoyagerBindingMatches(action.Plan))
        {
            MarkVoyagerOutcomeUnknown(action, "The exact original landing binding ended before its metadata could be reconciled.");
            return;
        }
        var player = action.Plan.VoyagerBinding!.Player;
        if (!player.isAlive || player.movementState != EMovementState.Walk || player.speed > 0.1f
            || player.planetId != action.VoyagerSelectedDestinationPlanetId || player.warpCommand || player.warpState != 0f)
        {
            if (action.VoyagerLandingCharge is not null && !action.VoyagerLandingPendingPersistence)
            {
                FailVoyagerLandingCharge(action, "The original landing recharge no longer proves its stable living target landing. [remote_idle_landing_stability_unproved]");
                return;
            }
            if (action.VoyagerLandingPendingPersistence)
            {
                var pause = FreezeNativeLocalFailureEnergy(VoyagerBindingMatches(action.Plan));
                var reason = "The original landing metadata remains unresolved; current Native state no longer proves the same stable landing."
                    + " [remote_idle_landing_stability_unproved]";
                action.VoyagerStopReason ??= reason;
                MarkVoyagerOutcomeUnknown(action, reason + pause);
                CaptureVoyagerReadback(action);
                return;
            }
            if (action.Terminal)
            {
                action.State = NormalActionStates.OutcomeUnknown;
                action.Message = "The original landing metadata remains unresolved; current native state does not prove the same stable landing.";
                return;
            }
            RequestVoyagerStop(action, "Native warp exit and a stable living target landing are required before success can be sealed.");
            return;
        }
        NativeLocalWalkSceneBinding? landedIdleScene = null;
        var idleOwner = !action.Plan.VoyagerReturnLeg ? new VoyagerRemoteIdleOwner(action.Plan.VoyagerBinding!,
            action.SessionId, action.Plan.VoyagerJourneyId!, action.ActionId, action.ActionId) : null;
        if (!action.Plan.VoyagerReturnLeg)
        {
            var firstTerrain = action.VoyagerLandingHandoff is null
                ? GameStateReader.CaptureNativeTerrain(player, "voyager_landing_handoff") : null;
            VoyagerRemoteIdleAdmissionFacts? firstFacts = null;
            Spherewright.Contracts.Players.NativePlayerGroundContactSnapshot? firstGround = null;
            var journey = action.VoyagerJourney ?? _flightCheckpoints.VoyagerJourney;
            var spent = journey is null ? null : VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(
                journey.SpentGameTicks, action.StartedAtGameTick, GameMain.gameTick, action.VoyagerLegAccounted);
            var admission = journey is null || !spent.HasValue
                ? VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_landing_accounting_unproved")
                : CaptureVoyagerRemoteIdleAdmission(idleOwner!, journey, scene: action.VoyagerLandingCharge?.Scene, spent.Value,
                    action.VoyagerLandedAtGameTick ?? GameMain.gameTick, out landedIdleScene,
                    prospectiveRemotePlanetId: action.VoyagerSelectedDestinationPlanetId,
                    captureFacts: facts => firstFacts = facts, captureGround: ground => firstGround = ground,
                    groundStage: "voyager_landing_handoff");
            action.VoyagerLandingHandoff ??= new VoyagerLandingHandoffReadback
            {
                CapturedAtGameTick = GameMain.gameTick, Proved = admission.Proved,
                BlockerCode = admission.BlockerCode, CaptureStage = admission.CaptureStage,
                CaptureExceptionType = admission.CaptureExceptionType, Terrain = firstTerrain, GroundContact = firstGround?.Copy(),
                CoreEnergyJoules = VoyagerFiniteObservation(player.mecha.coreEnergy),
                LoadedEnergyJoules = VoyagerFiniteObservation(CalculateAvailableFlightEnergy(player.mecha)),
                PerNativeTickEnergyJoules = firstFacts?.MovementReserve?.PerNativeTickEnergyJoules,
            };
            if (!admission.Proved)
            {
                // Do not mark the checkpoint successful and then discover that
                // no idle controller can accept it. The original flight keeps
                // its finite stop/recovery path and the exact failed predicate.
                var reason = "The outbound landing cannot hand control to its strict remote idle guard."
                    + VoyagerRemoteIdleFailureDetail(admission);
                if (action.VoyagerLandingPendingPersistence)
                {
                    // A previous attempt may already have marked the checkpoint
                    // flight_succeeded. Never convert that retry into a stop or
                    // a recoverable failed flight; preserve its original poll.
                    action.VoyagerStopReason ??= reason;
                    MarkVoyagerOutcomeUnknown(action, reason + FreezeNativeLocalFailureEnergy(VoyagerBindingMatches(action.Plan)));
                    CaptureVoyagerReadback(action);
                }
                else if (!TryWaitForVoyagerLandingCore(action, admission, firstFacts, landedIdleScene))
                    RequestVoyagerStop(action, reason);
                return;
            }
            if (action.VoyagerLandingCharge is not null)
            {
                var blocker = VoyagerLandingChargeBlocker(action, firstFacts, continuing: true, postPlayer: true);
                if (blocker is not null)
                {
                    FailVoyagerLandingCharge(action, "The original landing recharge could not seal its strict handoff. [" + blocker + "]");
                    return;
                }
            }
        }
        action.VoyagerLandingPendingPersistence = true;
        action.VoyagerLandingCompletionMessage ??= message;
        var rejection = "The original checkpoint identity is missing.";
        if (string.IsNullOrEmpty(action.FlightCheckpointId)
            || !_flightCheckpoints.TryMarkFlightSucceeded(action.FlightCheckpointId!, action.ActionId, GameMain.gameTick, out rejection))
        {
            MarkVoyagerOutcomeUnknown(action, rejection);
            return;
        }
        _sessions.ForgetCurrentFlightCheckpoint(action.FlightCheckpointId!);
        if (!TrySealVoyagerLanding(action, out rejection))
        {
            MarkVoyagerOutcomeUnknown(action, rejection);
            return;
        }
        if (action.VoyagerOutcomeUnknown)
        {
            if (!_sessions.TryClearQuarantineOnMainThread(action.ActionId, action.OriginalOutcomeMessage!, out _)) return;
            action.ReconciledFromOutcomeUnknown = true;
            action.ReconciledAtGameTick = GameMain.gameTick;
        }
        if (!action.Plan.VoyagerReturnLeg)
        {
            _voyagerRemoteIdleOwner = idleOwner;
            _voyagerRemoteIdleScene = landedIdleScene;
        }
        else { _voyagerRemoteIdleOwner = null; _voyagerRemoteIdleScene = null; }
        action.VoyagerLandingCharge = null;
        action.VoyagerLandingPendingPersistence = false;
        action.State = NormalActionStates.Completed;
        action.Terminal = true;
        action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.Message = action.VoyagerLandingCompletionMessage;
        action.AfterInventory = CaptureInventory(player);
        action.AfterStateHash = _reader.GetPlayerStateOnMainThread(action.SessionId,
            new LocalPlanetRequest { PlanetId = player.planetId }).Value?.StateHash;
        CaptureVoyagerReadback(action);
        _sessions.IncrementRevisionOnMainThread();
    }

    private void CloseVoyagerAfterNormalSave(ActionRecord action, SessionState session)
    {
        var journey = _flightCheckpoints.VoyagerJourney;
        if (journey?.Phase != "home_awaiting_save") return;
        if (!session.OwnedBySpherewright || session.SessionId != action.SessionId || session.SaveName != journey.OwnedSaveName
            || session.LocalPlanetId != journey.HomePlanetId || !session.LastOwnedSaveGameTick.HasValue)
            throw new InvalidOperationException("The normally saved session does not prove this journey's home return.");
        using (var lease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(journey.OwnedSaveName), journey.OwnedSaveName))
        {
            if (!lease.Prefix.Peaceful || lease.Prefix.GameVersion != journey.GameVersion
                || lease.Prefix.GameTick != session.LastOwnedSaveGameTick.Value)
                throw new InvalidOperationException("The original native owned Save did not match its full-file provenance.");
            action.VoyagerCoverSaveProof = new VoyagerCoverSaveProof { JourneyId = journey.JourneyId,
                HomePlanetId = journey.HomePlanetId, SavedGameTick = session.LastOwnedSaveGameTick.Value, Fingerprint = lease.Fingerprint };
        }
        var proof = action.VoyagerCoverSaveProof;
        if (!_flightCheckpoints.TryCompleteVoyagerAfterOwnedSave(proof.JourneyId, action.ActionId, action.SessionId,
            proof.HomePlanetId, proof.SavedGameTick, proof.Fingerprint, out var rejection))
            throw new InvalidOperationException(rejection);
    }

    private void ReconcileVoyagerCoverSave(ActionRecord action)
    {
        var proof = action.VoyagerCoverSaveProof!;
        var session = _sessions.CaptureOnMainThread();
        if (!session.OwnedBySpherewright || session.SessionId != action.SessionId || session.LocalPlanetId != proof.HomePlanetId
            || !_flightCheckpoints.TryCompleteVoyagerAfterOwnedSave(proof.JourneyId, action.ActionId, action.SessionId,
                proof.HomePlanetId, proof.SavedGameTick, proof.Fingerprint, out _)) return;
        if (!_sessions.TryClearQuarantineOnMainThread(action.ActionId, action.OriginalOutcomeMessage!, out _)) return;
        action.State = NormalActionStates.Completed;
        action.Terminal = action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.ReconciledFromOutcomeUnknown = true;
        action.ReconciledAtGameTick = GameMain.gameTick;
        action.Message = "The original native Save's full-file proof now durably closes the returned journey; no Save was replayed.";
    }

    private sealed class VoyagerCoverSaveProof
    {
        public string JourneyId = string.Empty;
        public int HomePlanetId;
        public long SavedGameTick;
        public string Fingerprint = string.Empty;
    }

    private static PlanetData? GetEffectiveFlightDestination(ActionRecord action) =>
        GameMain.galaxy?.PlanetById(action.Plan.VoyagerBudget is not null ? action.VoyagerSelectedDestinationPlanetId : action.Plan.DestinationPlanetId);

    private void CaptureVoyagerReadback(ActionRecord action)
    {
        if (!VoyagerBindingMatches(action.Plan)) return; // preserve the last proved observation
        var player = action.Plan.VoyagerBinding!.Player;
        var budget = action.Plan.VoyagerBudget!;
        action.VoyagerFlightReadback = new VoyagerFlightReadback
        {
            JourneyId = action.Plan.VoyagerJourneyId!, Phase = action.VoyagerPhase,
            IsReturnLeg = action.Plan.VoyagerReturnLeg, OriginStarId = budget.OriginStarId,
            DestinationStarId = budget.DestinationStarId,
            SelectedDestinationPlanetId = action.VoyagerSelectedDestinationPlanetId > 0 ? action.VoyagerSelectedDestinationPlanetId : (int?)null,
            ActualStarId = GameMain.localStar?.id, ActualPlanetId = GameMain.localPlanet?.id,
            CapturedAtGameTick = GameMain.gameTick, FlightDeadlineGameTick = action.VoyagerDeadlineGameTick,
            JourneyDeadlineUtc = action.VoyagerJourney?.DeadlineUtc ?? default,
            EffectiveJourneyDeadlineUtc = action.VoyagerJourney is { } originalJourney ? _sessions.EffectiveVoyagerDeadline(originalJourney) : (DateTimeOffset?)null,
            WarpGameTicks = action.VoyagerWarpTicks, MaximumWarpGameTicks = budget.MaximumWarpGameTicksPerLeg,
            CoreEnergyJoules = VoyagerFiniteObservation(player.mecha.coreEnergy),
            LoadedEnergyJoules = VoyagerFiniteObservation(CalculateAvailableFlightEnergy(player.mecha)),
            ReturnEnergyFloorJoules = action.Plan.VoyagerReturnLeg ? action.VoyagerJourney?.WithdrawalReserveJoules ?? 0d : action.VoyagerJourney?.ReturnEnergyFloorJoules ?? 0d,
            NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage), PackageWarperCount = CountVoyagerWarpers(player.package),
            NativeWarpersConsumed = action.VoyagerWarpersConsumed,
            NativeWarpState = float.IsNaN(player.warpState) || float.IsInfinity(player.warpState) ? (float?)null : player.warpState,
            NativeWarpCommand = player.warpCommand, EntryAttempted = action.VoyagerWarpControl.EntryAttempted,
            StopRequested = action.VoyagerStopReason is not null, NativeExitObserved = action.VoyagerExitObserved,
            BindingVerified = true, RemainingDistanceMeters = action.RemainingDistance, FailureReason = action.VoyagerStopReason,
            ControlLeaseExpiresAtUtc = action.VoyagerControlLeaseExpiresAtUtc,
            StopDeadlineGameTick = action.VoyagerStopDeadlineGameTick, StopDeadlineUtc = action.VoyagerStopDeadlineUtc,
            FirstLandingHandoff = action.VoyagerLandingHandoff?.Copy(),
        };
    }

    private static double? VoyagerFiniteObservation(double value) => double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;

    private sealed class VoyagerNativeBinding
    {
        public VoyagerNativeBinding(GameData world, Player player) { World = world; Player = player; Controller = player.controller; Mecha = player.mecha; }
        public GameData World { get; }
        public Player Player { get; }
        public PlayerController Controller { get; }
        public Mecha Mecha { get; }
    }

    private sealed partial class ActionRecord
    {
        public VoyagerCoverSaveProof? VoyagerCoverSaveProof { get; set; }
        public VoyagerFlightReadback? VoyagerFlightReadback { get; set; }
        public VoyagerLandingHandoffReadback? VoyagerLandingHandoff { get; set; }
        public VoyagerLandingChargeState? VoyagerLandingCharge { get; set; }
        public bool VoyagerLandingChargeStarted { get; set; }
        public VoyagerJourneyRecord? VoyagerJourney { get; set; }
        public VoyagerWarpControlState VoyagerWarpControl { get; set; } = VoyagerWarpControlState.Ready;
        public string VoyagerPhase { get; set; } = "prepared";
        public int VoyagerSelectedDestinationPlanetId { get; set; }
        public VectorLF3 VoyagerWarpNavigationPoint { get; set; }
        public long VoyagerDeadlineGameTick { get; set; }
        public long VoyagerLastNativeTick { get; set; }
        public long VoyagerWarpTicks { get; set; }
        public int VoyagerWarpersConsumed { get; set; }
        public bool VoyagerExitRequested { get; set; }
        public bool VoyagerExitObserved { get; set; }
        public bool VoyagerStopSettled { get; set; }
        public bool VoyagerOutcomeUnknown { get; set; }
        public bool VoyagerLegAccounted { get; set; }
        public long? VoyagerLandedAtGameTick { get; set; }
        public bool VoyagerNativeLaunchStarted { get; set; }
        public bool VoyagerLandingPendingPersistence { get; set; }
        public string? VoyagerLandingCompletionMessage { get; set; }
        public string? VoyagerStopReason { get; set; }
        public DateTimeOffset VoyagerControlLeaseExpiresAtUtc { get; set; }
        public long? VoyagerStopDeadlineGameTick { get; set; }
        public DateTimeOffset? VoyagerStopDeadlineUtc { get; set; }
    }

    private sealed partial class NormalActionPlanPayload
    {
        public VoyagerRouteBudgetSnapshot? VoyagerBudget { get; private set; }
        public PreviewVoyagerRouteRequest? VoyagerRequest { get; private set; }
        public VoyagerNativeBinding? VoyagerBinding { get; private set; }
        public string? VoyagerJourneyId { get; private set; }
        public string? VoyagerNativePrerequisiteHash { get; private set; }
        public bool VoyagerReturnLeg { get; private set; }
        public bool ReturnOnlyTaskCancellation { get; set; }
        public string? CancelVoyagerActionId { get; private set; }
        public long? VoyagerLocalDeadlineGameTick { get; set; }
        public long VoyagerLocalStartedGameTick { get; set; }
        public long VoyagerLocalLastObservedGameTick { get; set; }
        public DateTimeOffset VoyagerLocalStartedAtUtc { get; set; }
        public DateTimeOffset? VoyagerLocalDeadlineUtc { get; set; }
        public string? VoyagerLocalOwnedSaveName { get; set; }
        public string? VoyagerLocalGameVersion { get; set; }
        public string? VoyagerLocalJourneyId { get; set; }
        public VoyagerNativeBinding? VoyagerLocalBinding { get; set; }
        public PlanetFactory? VoyagerLocalFactory { get; set; }
        public static NormalActionPlanPayload VoyagerFlight(string sessionId, int originPlanetId, string journeyId,
            bool returning, VoyagerRouteBudgetSnapshot budget, PreviewVoyagerRouteRequest request, string playerHash,
            string nativeHash, VoyagerNativeBinding binding) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.InterplanetaryFlight, SessionId = sessionId, PlanetId = originPlanetId,
                DestinationPlanetId = budget.DestinationPlanetId, VoyagerJourneyId = journeyId, VoyagerReturnLeg = returning,
                VoyagerBudget = budget, VoyagerRequest = request, VoyagerBinding = binding, PlayerStateHash = playerHash,
                VoyagerNativePrerequisiteHash = nativeHash, StarSystemStateHash = budget.CatalogStateHash,
                MinimumCoreEnergyRatio = budget.MinimumCoreEnergyRatio, RequiredFlightEnergy = budget.RequiredEnergyJoules,
                EstimatedDistance = budget.DistanceMeters,
                ExpectedStateHash = CanonicalStateHash.Combine("voyager-native-flight-v1", sessionId, journeyId, returning, budget.RouteStateHash, playerHash, nativeHash),
            };
        public static NormalActionPlanPayload CancelVoyagerFlight(string sessionId, int planetId, string actionId) =>
            new NormalActionPlanPayload { ActionKind = NormalActionKinds.CancelVoyagerFlight, SessionId = sessionId,
                PlanetId = planetId, CancelVoyagerActionId = actionId,
                ExpectedStateHash = CanonicalStateHash.Combine(NormalActionKinds.CancelVoyagerFlight, sessionId, planetId, actionId) };
    }
}
