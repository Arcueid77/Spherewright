using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private const long FlightLaunchTimeoutTicks = 3600;
    private const long MinimumFlightTimeoutTicks = 216000;
    private const long MinimumFlightProgressStallTicks = 18000;
    private const long FlightLandingStableTicks = 600;
    private const long FlightLandingTimeoutTicks = 7200;
    private const long FlightShoreTransitionGraceTicks = 120;
    private const int FlightShoreMaximumOrders = 3;
    private const float FlightShoreSearchMinimumDistance = 1f;
    private const float FlightShoreSearchMaximumDistance = 120f;
    private const float FlightShoreMinimumTerrainClearance = 0.2f;
    private const float FlightShoreNeighborhoodMinimumClearance = -0.05f;
    private const float FlightShoreNeighborhoodProbeDistance = 2f;
    private const float NativeSailEntryTargetAltitude = 100f;
    private const double FlightDetourMaximumRelativeSpeed = 200d;

    public GameCallResult<PreparedNormalAction> PrepareInterplanetaryFlightOnMainThread(
        string? requestedSessionId,
        PrepareInterplanetaryFlightRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.MinimumCoreEnergyRatio < 0.8d || request.MinimumCoreEnergyRatio > 1d)
        {
            return InvalidPlan("Minimum core-energy ratio must be from 0.8 through 1.0 for an interplanetary flight.");
        }

        var playerResult = _reader.GetPlayerStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(playerResult.Error!);
        }

        var starResult = _reader.GetLocalStarSystemOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!starResult.Success || starResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(starResult.Error!);
        }

        var playerSnapshot = playerResult.Value;
        var starSnapshot = starResult.Value;
        if (!string.Equals(request.ExpectedPlayerStateHash, playerSnapshot.StateHash, StringComparison.Ordinal)
            || !string.Equals(request.ExpectedStarSystemStateHash, starSnapshot.StateHash, StringComparison.Ordinal))
        {
            return StalePlan("Player or local-star identity changed after inspection; inspect both and prepare again.");
        }

        var player = GameMain.mainPlayer;
        var localPlanet = GameMain.localPlanet;
        var destination = GameMain.galaxy?.PlanetById(request.DestinationPlanetId);
        if (player?.mecha is null || player.controller is null || localPlanet is null || destination is null)
        {
            return NotReadyPlan("The player, local planet, or destination planet is not ready.");
        }

        if (!player.isAlive || player.movementState != EMovementState.Walk || player.planetId != request.PlanetId)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                "Interplanetary flight must start alive and grounded on the inspected origin planet.",
                true,
                "Land and stop on the current owned planet, then inspect the player and prepare again."));
        }

        if (player.currentOrder is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ServerBusy,
                "A DSP player order is still active, so launch would replace or race it.",
                true,
                "Wait for the exact move or harvest order to end, then inspect and prepare again."));
        }

        if (!BuildUiIsIdle(player))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ServerBusy,
                "The normal build UI still owns an active preview, so native flight controls are not isolated.",
                true,
                "Finish or cancel the current build preview, then inspect and prepare the flight again."));
        }

        if (destination.id == localPlanet.id
            || destination.star?.id != localPlanet.star?.id)
        {
            return InvalidPlan("Destination must be a different planet in the current star system.");
        }

        if (destination.type == EPlanetType.Gas)
        {
            return InvalidPlan("The bounded first interplanetary-flight action does not land on gas giants.");
        }

        if (player.mecha.thrusterLevel < 2)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                "Drive Engine level 2 is required before normal sail mode can begin.",
                true,
                "Complete the runtime technology that raises mecha thrusterLevel to at least 2, then inspect and prepare again."));
        }

        var coreRatio = player.mecha.coreEnergyCap > 0d
            ? player.mecha.coreEnergy / player.mecha.coreEnergyCap
            : 0d;
        if (coreRatio + 1e-9d < request.MinimumCoreEnergyRatio)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                $"Core energy ratio {coreRatio:F3} is below the prepared minimum {request.MinimumCoreEnergyRatio:F3}.",
                true,
                "Recharge at a verified wireless tower, then inspect and prepare again."));
        }

        var distance = Math.Max(0d, (destination.uPosition - player.uPosition).magnitude - destination.realRadius);
        var requiredEnergy = Math.Max(player.mecha.coreEnergyCap * 1.5d, distance * 1000d);
        var availableEnergy = CalculateAvailableFlightEnergy(player.mecha);
        if (availableEnergy + 1d < requiredEnergy
            || (player.mecha.reactorEnergy <= 0.5d && player.mecha.reactorItemId <= 0
                && !HasUsableFuel(player.mecha.reactorStorage)))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                $"Normal core and fuel energy reserve {availableEnergy:F0} J is below the conservative flight budget {requiredEnergy:F0} J, or no usable fuel remains after the core.",
                true,
                "Transfer ordinary fuel into the player inventory, refuel through the normal refuel action, recharge the core, then inspect and prepare again."));
        }

        var playerActionHash = CanonicalStateHash.PlayerAction(playerSnapshot);
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.InterplanetaryFlight,
            _sessions.SessionId,
            request.PlanetId,
            request.DestinationPlanetId,
            playerActionHash,
            starSnapshot.StateHash,
            request.MinimumCoreEnergyRatio,
            requiredEnergy);
        var estimatedTicks = Math.Max(
            7200L,
            (long)Math.Ceiling(distance / Math.Max(300d, player.mecha.maxSailSpeed) * 60d) + 7200L);
        var payload = NormalActionPlanPayload.InterplanetaryFlight(
            _sessions.SessionId!,
            request.PlanetId,
            request.DestinationPlanetId,
            expectedHash,
            playerActionHash,
            starSnapshot.StateHash,
            distance,
            request.MinimumCoreEnergyRatio,
            requiredEnergy);
        return AddPreparedPlan(
            payload,
            common.Session!,
            estimatedTicks,
            $"DSP enters native sail mode, approaches planet {destination.id}, and returns the living player to Walk state on that planet without fast travel or teleportation.");
    }

    private BridgeError? RevalidateInterplanetaryFlightPlanOnMainThread(NormalActionPlanPayload plan)
    {
        var playerResult = _reader.GetPlayerStateOnMainThread(
            plan.SessionId,
            new LocalPlanetRequest { PlanetId = plan.PlanetId });
        var starResult = _reader.GetLocalStarSystemOnMainThread(
            plan.SessionId,
            new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return playerResult.Error;
        }

        if (!starResult.Success || starResult.Value is null)
        {
            return starResult.Error;
        }

        if (!string.Equals(CanonicalStateHash.PlayerAction(playerResult.Value), plan.PlayerStateHash, StringComparison.Ordinal)
            || !string.Equals(starResult.Value.StateHash, plan.StarSystemStateHash, StringComparison.Ordinal))
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "Player or local-star state changed after the flight plan was prepared.",
                true,
                "Inspect the current player and star system, then prepare a new flight plan.");
        }

        var player = GameMain.mainPlayer;
        var destination = GameMain.galaxy?.PlanetById(plan.DestinationPlanetId);
        var coreRatio = player?.mecha?.coreEnergyCap > 0d
            ? player.mecha.coreEnergy / player.mecha.coreEnergyCap
            : 0d;
        if (player?.mecha is null || destination is null || player.movementState != EMovementState.Walk
            || player.currentOrder is not null || player.mecha.thrusterLevel < 2
            || !BuildUiIsIdle(player)
            || coreRatio + 1e-9d < plan.MinimumCoreEnergyRatio
            || CalculateAvailableFlightEnergy(player.mecha) + 1d < plan.RequiredFlightEnergy)
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "Flight prerequisites changed before commit.",
                true,
                "Land, recharge, refuel, inspect the current states, and prepare again.");
        }

        return null;
    }

    private void StartInterplanetaryFlightOnMainThread(ActionRecord action)
    {
        if (!EnsureFlightCheckpointOnMainThread(action))
        {
            return;
        }

        if (!_flightCheckpoints.TryMarkAttemptStarted(
                action.FlightCheckpointId!,
                action.ActionId,
                GameMain.gameTick,
                out var lifecycleRejection))
        {
            Fail(action, $"Native launch was not started because its checkpoint lifecycle could not be armed: {lifecycleRejection}");
            return;
        }

        var player = GameMain.mainPlayer;
        action.FlightBestDistance = action.Plan.EstimatedDistance;
        action.FlightBestDistanceAtGameTick = GameMain.gameTick;
        action.FlightLastControlGameTick = -1L;
        action.FlightDestinationContactAtGameTick = -1L;
        action.FlightStableLandingAtGameTick = -1L;
        // PlayerMove_Fly caps targetAltitude below its own sail threshold while
        // blueprint mode is retained. This is the same public setter the
        // current-version native sail-entry branch uses before switching modes.
        player.controller.actionBuild.blueprintMode = EBlueprintMode.None;
        if (action.Plan.VoyagerBudget is not null)
        {
            if (!_flightCheckpoints.TryArmVoyagerNativeLaunch(action.FlightCheckpointId!, action.ActionId, out var armRejection))
            {
                Fail(action, armRejection);
                return;
            }
            action.VoyagerNativeLaunchStarted = true;
        }
        EnterNativeFlight(player);
        action.State = NormalActionStates.WaitingForGame;
        action.Message = $"A separate pre-flight checkpoint was confirmed at tick {action.FlightCheckpointGameTick}; DSP then accepted native launch toward planet {action.Plan.DestinationPlanetId}.";
    }

    private bool EnsureFlightCheckpointOnMainThread(ActionRecord action)
    {
        var existing = _flightCheckpoints.CurrentTicket;
        if (existing is not null
            && existing.OriginPlanetId == action.PlanetId
            && existing.DestinationPlanetId == action.Plan.DestinationPlanetId
            && existing.VoyagerJourneyId == action.Plan.VoyagerJourneyId
            && _sessions.CanReuseFlightCheckpointForCurrentSession(existing)
            && _flightCheckpoints.TryValidateCheckpointFile(existing, out _))
        {
            BindFlightCheckpoint(action, existing);
            return true;
        }

        var session = _sessions.CaptureOnMainThread();
        if (!session.OwnedBySpherewright
            || string.IsNullOrWhiteSpace(session.SessionId)
            || string.IsNullOrWhiteSpace(session.SaveName)
            || session.LocalPlanetId != action.PlanetId)
        {
            Fail(action, "Native launch was not started because the exact primary owned-save identity was unavailable for a pre-flight checkpoint.");
            return false;
        }

        try
        {
            var ticket = _flightCheckpoints.CreateDraft(
                session.SaveName!,
                session.SessionId!,
                session.Revision,
                action.PlanetId,
                action.Plan.DestinationPlanetId,
                action.Plan.PlayerStateHash,
                action.Plan.StarSystemStateHash,
                action.Plan.VoyagerBudget?.OriginStarId ?? 0,
                action.Plan.VoyagerBudget?.DestinationStarId ?? 0,
                action.Plan.VoyagerJourneyId);
            GameMain.gameName = session.SaveName;
            if (!GameSave.SaveCurrentGame(ticket.CheckpointSaveName))
            {
                Fail(action, "Native launch was not started because DSP did not confirm the separate pre-flight save.");
                return false;
            }

            var savedGameTick = GameMain.gameTick;
            GameSave.ReadHeader(ticket.CheckpointSaveName, false, out var header);
            if (header is null || header.gameTick != savedGameTick)
            {
                Fail(action, "Native launch was not started because the pre-flight save header did not prove the exact saved game tick.");
                return false;
            }

            _flightCheckpoints.PersistCompletedCheckpoint(ticket, savedGameTick);
            _sessions.MarkCurrentSessionFlightCheckpoint(ticket);
            BindFlightCheckpoint(action, ticket);
            return true;
        }
        catch (Exception exception)
        {
            Fail(action, $"Native launch was not started because the protected pre-flight checkpoint could not be completed ({exception.GetType().Name}).");
            return false;
        }
    }

    private static void BindFlightCheckpoint(ActionRecord action, FlightCheckpointTicket ticket)
    {
        action.FlightCheckpointId = ticket.CheckpointId;
        action.FlightCheckpointReloadToken = ticket.ReloadToken;
        action.FlightCheckpointGameTick = ticket.SavedGameTick;
    }

    private void UpdateInterplanetaryFlight(ActionRecord action)
    {
        var player = GameMain.mainPlayer;
        var destination = GetEffectiveFlightDestination(action);
        if (player?.mecha is null || player.controller is null || destination is null || !player.isAlive)
        {
            Fail(action, "The player or bound destination became unavailable during interplanetary flight.");
            return;
        }

        if (action.FlightLastControlGameTick == GameMain.gameTick)
        {
            return;
        }

        action.FlightLastControlGameTick = GameMain.gameTick;
        var localPlanet = GameMain.localPlanet;
        var atDestination = localPlanet?.id == destination.id || player.planetId == destination.id;
        if (atDestination)
        {
            ReleaseNativeAscentInput(action);
            if (action.FlightDestinationContactAtGameTick < 0L)
            {
                action.FlightDestinationContactAtGameTick = GameMain.gameTick;
            }

            if (GameMain.gameTick > action.FlightDestinationContactAtGameTick + FlightLandingTimeoutTicks)
            {
                Fail(action, $"Native landing on planet {destination.id} did not remain grounded within the bounded settling window; reload the bound pre-flight checkpoint before retrying.");
                return;
            }

            if (player.movementState == EMovementState.Walk)
            {
                // Native AbortOrder does not set targetReached. A transient Walk tick must
                // continue the exact shore Move rather than cancel it before dry terrain.
                // Missing/replaced orders go through the same fail-closed reconciliation;
                // no stable ticks accrue while an unfinished shore order remains.
                if (!LandingShoreOrderPolicy.MayVerifyStableWalk(
                        action.PlayerOrder is not null,
                        player.currentOrder is not null,
                        action.PlayerOrder is not null && ReferenceEquals(player.currentOrder, action.PlayerOrder),
                        action.PlayerOrder?.targetReached ?? false))
                {
                    action.FlightStableLandingAtGameTick = -1L;
                    UpdateNativeShoreLanding(action, player, destination);
                    return;
                }

                AbortPlayerOrderIfOwned(action);
                if (player.speed <= 0.1f)
                {
                    if (action.FlightStableLandingAtGameTick < 0L)
                    {
                        action.FlightStableLandingAtGameTick = GameMain.gameTick;
                    }

                    var stableTicks = GameMain.gameTick - action.FlightStableLandingAtGameTick;
                    action.Message = $"Native landing on planet {destination.id} is grounded at {player.speed:F2} m/s for {stableTicks}/{FlightLandingStableTicks} verification ticks.";
                    if (stableTicks >= FlightLandingStableTicks)
                    {
                        var after = _reader.GetPlayerStateOnMainThread(
                            action.SessionId,
                            new LocalPlanetRequest { PlanetId = destination.id });
                        if (after.Success && after.Value is not null)
                        {
                            action.AfterStateHash = after.Value.StateHash;
                            Complete(action, $"Normal flight remained alive, grounded, and in Walk state on planet {destination.id} for {FlightLandingStableTicks} verification ticks.");
                        }

                        return;
                    }
                }
                else
                {
                    action.FlightStableLandingAtGameTick = -1L;
                    action.Message = $"Native landing touched planet {destination.id} in Walk state but is still settling at {player.speed:F2} m/s.";
                }

                return;
            }

            action.FlightStableLandingAtGameTick = -1L;
            if (player.movementState == EMovementState.Drift)
            {
                UpdateNativeShoreLanding(action, player, destination);
                return;
            }
        }
        else
        {
            action.FlightStableLandingAtGameTick = -1L;
        }

        if (player.movementState == EMovementState.Walk || player.movementState == EMovementState.Fly)
        {
            UpdateNativeFlightLaunch(action, player, destination, destination.uPosition);
            return;
        }

        if (player.movementState < EMovementState.Sail)
        {
            if (atDestination && GameMain.gameTick % 120L == 0L)
            {
                action.Message = $"Native landing has contacted planet {destination.id} in {player.movementState} state and is waiting for a stable grounded Walk state.";
            }

            return;
        }

        ReleaseNativeAscentInput(action);
        var origin = GameMain.galaxy?.PlanetById(action.PlanetId);
        var navigationTarget = destination.uPosition;
        var detour = default(FlightPathDetour);
        var detourSelected = action.Plan.VoyagerBudget is not null
            ? TrySelectVoyagerVisibleDetour(player, destination.uPosition, destination.id, out detour)
            : origin is not null && TrySelectNativeSailDetour(player, origin, destination, out detour);
        if (detourSelected)
        {
            navigationTarget = ToVectorLf3(detour.AimPoint);
        }

        if (origin is not null
            && ControlNativeSailDeparture(
                player,
                origin,
                navigationTarget,
                out var departureSurfaceDistance,
                out var departureSpeed))
        {
            action.Message = detourSelected
                ? $"Native sail is clearing the origin planet at {departureSurfaceDistance:F0} m above its surface and {departureSpeed:F1} m/s relative speed toward a verified detour around body {detour.ObstacleBodyId}."
                : $"Native sail is clearing the origin planet at {departureSurfaceDistance:F0} m above its surface and {departureSpeed:F1} m/s relative speed before turning toward planet {destination.id}.";
            return;
        }

        var surfaceDistance = Math.Max(0d, (destination.uPosition - player.uPosition).magnitude - destination.realRadius);
        double relativeSpeed;
        double waypointDistance = 0d;
        if (detourSelected)
        {
            ControlNativeSailTowardDetour(player, detour, out waypointDistance, out relativeSpeed,
                action.Plan.VoyagerBudget is not null ? player.mecha.maxSailSpeed : FlightDetourMaximumRelativeSpeed);
        }
        else
        {
            ControlNativeSailTowardPlanet(player, destination, out surfaceDistance, out relativeSpeed);
        }

        if (surfaceDistance + 1d < action.FlightBestDistance)
        {
            action.FlightBestDistance = surfaceDistance;
            action.FlightBestDistanceAtGameTick = GameMain.gameTick;
        }

        if (GameMain.gameTick % 300L == 0L)
        {
            action.Message = detourSelected
                ? $"Native sail is taking a {waypointDistance:F0} m waypoint around body {detour.ObstacleBodyId}; its direct route clearance was {detour.DirectClearance:F0}/{detour.RequiredClearance:F0} m, destination surface distance is {surfaceDistance:F0} m, and relative speed is {relativeSpeed:F1} m/s."
                : $"Native sail is {surfaceDistance:F0} m from planet {destination.id}'s surface at {relativeSpeed:F1} m/s; core energy {player.mecha.coreEnergy:F0}/{player.mecha.coreEnergyCap:F0} J.";
        }

        if (action.Plan.VoyagerBudget is not null) return;

        var timeout = Math.Max(MinimumFlightTimeoutTicks, action.Plan.EstimatedTicks * 6L);
        if (GameMain.gameTick > action.StartedAtGameTick + timeout)
        {
            action.Stalled = true;
            Fail(action, $"Native sail exceeded its bounded timeout at {surfaceDistance:F0} m from planet {destination.id}; the bound checkpoint must be reloaded before another attempt.");
            return;
        }

        var progressStallWindow = Math.Max(MinimumFlightProgressStallTicks, action.Plan.EstimatedTicks * 2L);
        if (surfaceDistance > destination.realRadius
            && GameMain.gameTick > action.FlightBestDistanceAtGameTick + progressStallWindow)
        {
            action.Stalled = true;
            Fail(action, $"Native sail made no new best-distance progress for {progressStallWindow} game ticks while {surfaceDistance:F0} m from planet {destination.id}; the bound checkpoint must be reloaded before another attempt.");
        }
    }

    private void UpdateNativeFlightLaunch(ActionRecord action, Player player, PlanetData? destination, VectorLF3 navigationTarget)
    {
        var localPlanet = GameMain.localPlanet;
        if (player.movementState == EMovementState.Walk)
        {
            if (localPlanet is not null
                && localPlanet.id != action.PlanetId
                && localPlanet.id != destination?.id)
            {
                Fail(action, $"Native flight landed on unexpected planet {localPlanet.id}; reload the bound pre-flight checkpoint before retrying.");
                return;
            }

            if (GameMain.gameTick > action.StartedAtGameTick + FlightLaunchTimeoutTicks)
            {
                Fail(action, "DSP did not enter native flight mode within the bounded launch window; the bound pre-flight checkpoint remains reusable.");
                return;
            }

            EnterNativeFlight(player);
            return;
        }

        if (player.movementState == EMovementState.Fly)
        {
            if (destination is not null && (localPlanet?.id == destination.id || player.planetId == destination.id))
            {
                ReleaseNativeAscentInput(action);
                player.controller.actionFly.targetAltitude = 1f;
                player.controller.actionFly.moveVelocity = Vector3.zero;
                player.controller.actionFly.rtsVelocity = Vector3.zero;
            }
            else
            {
                if (GameMain.gameTick > action.StartedAtGameTick + FlightLaunchTimeoutTicks)
                {
                    Fail(action, "DSP did not enter native sail mode within the bounded launch window; the bound pre-flight checkpoint remains reusable.");
                    return;
                }

                ApplyNativeAscentInput(action, player);
                // PlayerMove_Fly.GameTick lowers an unattended targetAltitude by
                // 0.1 before checking its >= 50 sail-entry threshold. Reasserting
                // exactly 50 after each tick therefore converges to a permanent
                // 49.9/50 oscillation. A modest margin lets DSP's own GameTick
                // satisfy altitude, horizontal-speed, and thruster checks and run
                // its native ResetSailState/camera/scenario transition.
                player.controller.actionFly.targetAltitude = NativeSailEntryTargetAltitude;
                var tangent = player.forward;
                tangent -= Vector3.Dot(tangent, player.position.normalized) * player.position.normalized;
                if (tangent.sqrMagnitude < 0.01f)
                {
                    tangent = Vector3.Cross(player.position.normalized, Vector3.up);
                }

                player.controller.actionFly.moveVelocity = tangent.normalized * Math.Max(13f, player.mecha.walkSpeed * 2.5f);
                action.Message = $"Native fly launch is at {player.controller.actionFly.currentAltitude:F1}/{player.controller.actionFly.targetAltitude:F1} m with {player.controller.horzSpeed:F1} m/s horizontal and {player.controller.vertSpeed:F1} m/s vertical speed; blueprint={player.controller.actionBuild.blueprintMode}, thruster={player.mecha.thrusterLevel}, frame-state={player.controller.movementStateInFrame}.";
                if (TryEnterCurrentVersionNativeSail(player))
                {
                    ReleaseNativeAscentInput(action);
                    var originPlanet = GameMain.galaxy?.PlanetById(action.PlanetId);
                    var initialNavigationTarget = navigationTarget;
                    var initialDetour = default(FlightPathDetour);
                    var initialDetourSelected = originPlanet is not null && destination is not null
                                                && TrySelectNativeSailDetour(
                                                    player,
                                                    originPlanet,
                                                    destination,
                                                    out initialDetour);
                    if (initialDetourSelected)
                    {
                        initialNavigationTarget = ToVectorLf3(initialDetour.AimPoint);
                    }

                    if (originPlanet is not null
                        && ControlNativeSailDeparture(
                            player,
                            originPlanet,
                            initialNavigationTarget,
                            out var initialSurfaceDistance,
                            out var initialRelativeSpeed))
                    {
                        action.Message = initialDetourSelected
                            ? $"DSP's current-version native Fly-to-Sail branch accepted; immediate origin clearance began at {initialSurfaceDistance:F0} m and {initialRelativeSpeed:F1} m/s toward a verified detour around body {initialDetour.ObstacleBodyId}."
                            : $"DSP's current-version native Fly-to-Sail branch accepted; immediate origin clearance began at {initialSurfaceDistance:F0} m and {initialRelativeSpeed:F1} m/s.";
                    }
                    else
                    {
                        action.Message = "DSP's current-version native Fly-to-Sail branch accepted the verified altitude, horizontal-speed, and thruster conditions.";
                    }
                }
            }

            return;
        }

    }

    private void UpdateNativeShoreLanding(ActionRecord action, Player player, PlanetData destination)
    {
        if (action.PlayerOrder is not null)
        {
            if (ReferenceEquals(player.currentOrder, action.PlayerOrder))
            {
                var wasPowerStarved = action.PowerStarvedAtGameTick.HasValue;
                if (FailPowerStarvedPlayerOrder(action, "shore-landing movement"))
                {
                    return;
                }

                if (action.PowerStarvedAtGameTick.HasValue)
                {
                    return;
                }

                var remainingDistance = SurfaceDistance(
                    player.position,
                    action.PlayerOrder.target,
                    destination.realRadius);
                action.MovementProgress ??= new MovementProgressWatchdog(
                    GameMain.gameTick,
                    player.position.x,
                    player.position.y,
                    player.position.z,
                    remainingDistance);
                if (wasPowerStarved)
                {
                    action.MovementProgress.ResetWindow(
                        GameMain.gameTick,
                        player.position.x,
                        player.position.y,
                        player.position.z,
                        remainingDistance);
                }

                var progress = action.MovementProgress.Observe(
                    GameMain.gameTick,
                    player.position.x,
                    player.position.y,
                    player.position.z,
                    remainingDistance);
                if (progress.Status != MovementProgressStatus.Progressing)
                {
                    AbortPlayerOrderIfOwned(action);
                    Fail(action,
                        $"Native Drift shore recovery stopped because its exact owned movement order made no safe physical progress for {progress.StalledGameTicks} game ticks; reload the bound pre-flight checkpoint before retrying.");
                    return;
                }

                if (GameMain.gameTick % 120L == 0L)
                {
                    action.Message = $"Native shore recovery is following its exact movement order toward dry terrain on planet {destination.id}; {remainingDistance:F1} m remains on bounded order {action.FlightLandingOrderCount}/{FlightShoreMaximumOrders}.";
                }

                return;
            }

            if (player.currentOrder is not null)
            {
                Fail(action, "A different player order replaced the exact owned Drift shore-recovery order; the bound pre-flight checkpoint must be reloaded before retrying.");
                return;
            }

            if (!action.PlayerOrder.targetReached)
            {
                Fail(action, "DSP cleared the exact owned Drift shore-recovery order before it reached the selected terrain; the bound pre-flight checkpoint must be reloaded before retrying.");
                return;
            }

            action.FlightLandingOrderReachedAtGameTick ??= GameMain.gameTick;
            if (GameMain.gameTick <= action.FlightLandingOrderReachedAtGameTick.Value + FlightShoreTransitionGraceTicks)
            {
                if (GameMain.gameTick % 30L == 0L)
                {
                    action.Message = $"Native Drift shore recovery reached its dry-terrain order and is waiting for DSP's ordinary Drift-to-Walk transition on planet {destination.id}.";
                }

                return;
            }

            action.PlayerOrder = null;
            action.MovementProgress = null;
            action.FlightLandingOrderReachedAtGameTick = null;
        }

        if (action.FlightLandingOrderCount >= FlightShoreMaximumOrders)
        {
            Fail(action, $"DSP remained in Drift after {FlightShoreMaximumOrders} bounded dry-terrain movement orders; reload the bound pre-flight checkpoint before retrying.");
            return;
        }

        if (player.currentOrder is not null)
        {
            Fail(action, "A player order appeared before Drift shore recovery could claim the native movement channel; the bound pre-flight checkpoint must be reloaded before retrying.");
            return;
        }

        Vector3 target;
        float targetDistance;
        float terrainClearance;
        try
        {
            if (!TryFindNearestDryLandingTarget(
                    destination,
                    player.position,
                    out target,
                    out targetDistance,
                    out terrainClearance))
            {
                Fail(action, $"No terrain with a verified dry neighborhood was found within {FlightShoreSearchMaximumDistance:F0} m of the ocean contact point on planet {destination.id}; reload the bound pre-flight checkpoint before retrying.");
                return;
            }
        }
        catch (Exception exception)
        {
            Fail(action, $"The current-version terrain query failed safely with {exception.GetType().Name} before a Drift shore-recovery order was issued; reload the bound pre-flight checkpoint before retrying.");
            return;
        }

        action.PlayerOrder = OrderNode.MoveTo(target);
        action.FlightLandingOrderCount++;
        action.FlightLandingOrderReachedAtGameTick = null;
        action.MovementProgress = new MovementProgressWatchdog(
            GameMain.gameTick,
            player.position.x,
            player.position.y,
            player.position.z,
            targetDistance);
        player.Order(action.PlayerOrder, false);
        action.Message = $"Native landing contacted ocean on planet {destination.id}; DSP accepted bounded Drift movement order {action.FlightLandingOrderCount}/{FlightShoreMaximumOrders} toward the nearest verified dry neighborhood {targetDistance:F1} m away with {terrainClearance:F2} m terrain clearance.";
    }

    private static bool TryFindNearestDryLandingTarget(
        PlanetData planet,
        Vector3 currentPosition,
        out Vector3 target,
        out float surfaceDistance,
        out float terrainClearance)
    {
        target = Vector3.zero;
        surfaceDistance = 0f;
        terrainClearance = 0f;
        var rawData = planet.data;
        var vertices = rawData?.vertices;
        if (rawData is null
            || vertices is null
            || vertices.Length == 0
            || rawData.heightData is null
            || rawData.indexMap is null
            || rawData.modData is null
            || planet.realRadius <= 0f
            || planet.scale <= 0f)
        {
            return false;
        }

        var currentNormal = currentPosition.normalized;
        if (currentNormal.sqrMagnitude < 0.99f)
        {
            return false;
        }

        LandingShoreCandidateScore? selected = null;
        Vector3 selectedDirection = Vector3.zero;
        var selectedTerrainRadius = 0f;
        var minimumDot = Math.Cos(FlightShoreSearchMaximumDistance / planet.realRadius);
        var maximumDot = Math.Cos(FlightShoreSearchMinimumDistance / planet.realRadius);
        for (var index = 0; index < vertices.Length; index++)
        {
            var direction = vertices[index];
            if (direction.sqrMagnitude < 0.99f)
            {
                continue;
            }

            direction.Normalize();
            var dot = Math.Max(-1d, Math.Min(1d, Vector3.Dot(currentNormal, direction)));
            if (dot < minimumDot || dot > maximumDot)
            {
                continue;
            }

            var distance = Math.Acos(dot) * planet.realRadius;
            var candidateTerrainRadius = rawData.QueryModifiedHeight(direction) * planet.scale;
            var candidate = new LandingShoreCandidateScore
            {
                Index = index,
                SurfaceDistance = distance,
                TerrainClearance = candidateTerrainRadius - planet.realRadius,
            };
            if (!LandingShoreSelection.IsEligible(
                    candidate,
                    FlightShoreSearchMinimumDistance,
                    FlightShoreSearchMaximumDistance,
                    FlightShoreMinimumTerrainClearance)
                || !LandingShoreSelection.IsPreferred(candidate, selected)
                || !HasDryLandingNeighborhood(planet, direction))
            {
                continue;
            }

            selected = candidate;
            selectedDirection = direction;
            selectedTerrainRadius = candidateTerrainRadius;
        }

        if (selected is null)
        {
            return false;
        }

        target = selectedDirection * (selectedTerrainRadius + 0.2f);
        surfaceDistance = (float)selected.SurfaceDistance;
        terrainClearance = (float)selected.TerrainClearance;
        return true;
    }

    private static bool HasDryLandingNeighborhood(PlanetData planet, Vector3 normal)
    {
        var rawData = planet.data;
        if (rawData is null || planet.realRadius <= 0f)
        {
            return false;
        }

        var tangentX = Vector3.Cross(normal, Math.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        var tangentY = Vector3.Cross(normal, tangentX).normalized;
        var angularDistance = FlightShoreNeighborhoodProbeDistance / planet.realRadius;
        var radialWeight = Mathf.Cos(angularDistance);
        var tangentWeight = Mathf.Sin(angularDistance);
        for (var sample = 0; sample < 8; sample++)
        {
            var heading = sample * Mathf.PI * 0.25f;
            var tangent = tangentX * Mathf.Cos(heading) + tangentY * Mathf.Sin(heading);
            var probe = (normal * radialWeight + tangent * tangentWeight).normalized;
            var clearance = rawData.QueryModifiedHeight(probe) * planet.scale - planet.realRadius;
            if (float.IsNaN(clearance)
                || float.IsInfinity(clearance)
                || clearance < FlightShoreNeighborhoodMinimumClearance)
            {
                return false;
            }
        }

        return true;
    }

    private static float SurfaceDistance(Vector3 first, Vector3 second, float radius)
    {
        var firstNormal = first.normalized;
        var secondNormal = second.normalized;
        var dot = Math.Max(-1d, Math.Min(1d, Vector3.Dot(firstNormal, secondNormal)));
        return (float)(Math.Acos(dot) * radius);
    }

    private static bool TrySelectNativeSailDetour(
        Player player,
        PlanetData origin,
        PlanetData destination,
        out FlightPathDetour detour)
    {
        detour = default;
        var planets = (destination.star?.id == GameMain.localStar?.id ? destination.star : origin.star)?.planets;
        if (planets is null)
        {
            return false;
        }

        var currentPoint = new FlightPathPoint(
            player.uPosition.x,
            player.uPosition.y,
            player.uPosition.z);
        var destinationPoint = new FlightPathPoint(
            destination.uPosition.x,
            destination.uPosition.y,
            destination.uPosition.z);
        FlightPathDetour? selected = null;
        foreach (var candidate in planets)
        {
            if (candidate is null
                || candidate.id == origin.id
                || candidate.id == destination.id)
            {
                continue;
            }

            var candidateCenter = new FlightPathPoint(
                candidate.uPosition.x,
                candidate.uPosition.y,
                candidate.uPosition.z);
            if (InterplanetaryFlightPathAvoidance.TryCreateDetour(
                    currentPoint,
                    destinationPoint,
                    candidate.id,
                    candidateCenter,
                    candidate.realRadius,
                    out var candidateDetour)
                && InterplanetaryFlightPathAvoidance.IsPreferred(candidateDetour, selected))
            {
                selected = candidateDetour;
            }
        }

        if (!selected.HasValue)
        {
            return false;
        }

        detour = selected.Value;
        return true;
    }

    private static VectorLF3 ToVectorLf3(FlightPathPoint point) =>
        new VectorLF3(point.X, point.Y, point.Z);

    private static void ControlNativeSailTowardDetour(
        Player player,
        FlightPathDetour detour,
        out double waypointDistance,
        out double relativeSpeed,
        double maximumRelativeSpeed = FlightDetourMaximumRelativeSpeed)
    {
        var sail = player.controller.actionSail;
        var aimPoint = ToVectorLf3(detour.AimPoint);
        var toWaypoint = aimPoint - player.uPosition;
        waypointDistance = toWaypoint.magnitude;
        var direction = waypointDistance > 1e-6d
            ? toWaypoint / waypointDistance
            : player.uVelocity.normalized;

        var astro = GameMain.galaxy.astrosData[detour.ObstacleBodyId];
        var obstacleVelocity = (astro.uPosNext - astro.uPos) * 60d;
        var relativeVelocity = player.uVelocity - obstacleVelocity;
        relativeSpeed = relativeVelocity.magnitude;
        if (relativeSpeed > 1e-6d)
        {
            var angle = VectorLF3.AngleDEG(relativeVelocity, direction);
            var turn = (float)(1.6d / Math.Max(10d, angle));
            var desiredRelative = direction * relativeSpeed;
            var steered = (VectorLF3)Vector3.Slerp((Vector3)relativeVelocity, (Vector3)desiredRelative, turn);
            var steeringDelta = steered - relativeVelocity;
            sail.UseSailEnergy(ref steeringDelta, 0.36d);
            player.uVelocity += steeringDelta;
            relativeVelocity = player.uVelocity - obstacleVelocity;
            relativeSpeed = relativeVelocity.magnitude;
        }

        var speedLimit = waypointDistance > Math.Max(1500d, relativeSpeed * 6d)
            ? Math.Max(FlightDetourMaximumRelativeSpeed, maximumRelativeSpeed) : FlightDetourMaximumRelativeSpeed;
        if (relativeSpeed > speedLimit * 1.05d)
        {
            var brakingDelta = relativeVelocity * 0.008d;
            sail.UseSailEnergy(ref brakingDelta, 1.5d);
            player.uVelocity -= brakingDelta;
            relativeSpeed = (player.uVelocity - obstacleVelocity).magnitude;
            return;
        }

        if (relativeSpeed < speedLimit)
        {
            var acceleration = Math.Min(
                7d,
                speedLimit - relativeSpeed);
            if (acceleration > 0d)
            {
                var ratio = sail.UseSailEnergy(acceleration);
                player.uVelocity += direction * (acceleration * ratio);
                relativeSpeed = (player.uVelocity - obstacleVelocity).magnitude;
            }
        }
    }

    private static void ControlNativeSailTowardPlanet(
        Player player,
        PlanetData destination,
        out double surfaceDistance,
        out double relativeSpeed)
    {
        var sail = player.controller.actionSail;
        var toDestination = destination.uPosition - player.uPosition;
        var centerDistance = toDestination.magnitude;
        surfaceDistance = Math.Max(0d, centerDistance - destination.realRadius);
        var direction = centerDistance > 1e-6d ? toDestination / centerDistance : player.uVelocity.normalized;

        var astro = GameMain.galaxy.astrosData[destination.id];
        var destinationVelocity = (astro.uPosNext - astro.uPos) * 60d;
        var relativeVelocity = player.uVelocity - destinationVelocity;
        relativeSpeed = relativeVelocity.magnitude;

        if (relativeSpeed > 1e-6d)
        {
            var angle = VectorLF3.AngleDEG(relativeVelocity, direction);
            var turn = (float)(1.6d / Math.Max(10d, angle));
            var desiredRelative = direction * relativeSpeed;
            var steered = (VectorLF3)Vector3.Slerp((Vector3)relativeVelocity, (Vector3)desiredRelative, turn);
            var steeringDelta = steered - relativeVelocity;
            sail.UseSailEnergy(ref steeringDelta, 0.36d);
            player.uVelocity += steeringDelta;
            relativeVelocity = player.uVelocity - destinationVelocity;
            relativeSpeed = relativeVelocity.magnitude;
        }

        var brakingDistance = Math.Max(1500d, relativeSpeed * 6d);
        var approachSpeed = Math.Max(25d, Math.Min(200d, surfaceDistance * 0.15d));
        if (surfaceDistance <= brakingDistance && relativeSpeed > approachSpeed)
        {
            var brakingDelta = relativeVelocity * 0.008d;
            sail.UseSailEnergy(ref brakingDelta, 1.5d);
            player.uVelocity -= brakingDelta;
            return;
        }

        if (surfaceDistance > brakingDistance && relativeSpeed < sail.maxSailSpeed)
        {
            var acceleration = Math.Max(7d, Math.Min(sail.max_acc, Math.Max(1d, relativeSpeed) * 0.02d));
            acceleration = Math.Min(acceleration, sail.maxSailSpeed - relativeSpeed);
            if (acceleration > 0d)
            {
                var ratio = sail.UseSailEnergy(acceleration);
                player.uVelocity += direction * (acceleration * ratio);
            }
        }
    }

    private static bool ControlNativeSailDeparture(
        Player player,
        PlanetData origin,
        VectorLF3 navigationTarget,
        out double surfaceDistance,
        out double relativeSpeed)
    {
        var fromOrigin = player.uPosition - origin.uPosition;
        var centerDistance = fromOrigin.magnitude;
        surfaceDistance = Math.Max(0d, centerDistance - origin.realRadius);
        if (centerDistance <= 1e-6d)
        {
            relativeSpeed = 0d;
            return false;
        }

        var outward = (Vector3)(fromOrigin / centerDistance);
        var toNavigationTarget = navigationTarget - player.uPosition;
        var destinationDirection = toNavigationTarget.magnitude > 1e-6d
            ? (Vector3)(toNavigationTarget / toNavigationTarget.magnitude)
            : outward;
        var inwardProjection = Vector3.Dot(destinationDirection, outward);
        var closestApproach = inwardProjection < 0f
            ? centerDistance * Math.Sqrt(Math.Max(0d, 1d - inwardProjection * inwardProjection))
            : double.MaxValue;
        var lineOfSightBlocked = inwardProjection < 0f
                                 && closestApproach < origin.realRadius + 100d;
        if (surfaceDistance >= 500d && !lineOfSightBlocked)
        {
            relativeSpeed = 0d;
            return false;
        }

        var tangent = destinationDirection - outward * inwardProjection;
        if (tangent.sqrMagnitude < 0.01f)
        {
            tangent = Vector3.Cross(outward, player.forward);
            if (tangent.sqrMagnitude < 0.01f)
            {
                tangent = Vector3.Cross(outward, Vector3.up);
            }
        }

        var outwardWeight = surfaceDistance < 500d ? 1f : 0.25f;
        var departureDirection = (outward * outwardWeight + tangent.normalized).normalized;
        var astro = GameMain.galaxy.astrosData[origin.id];
        var originVelocity = (astro.uPosNext - astro.uPos) * 60d;
        var relativeVelocity = player.uVelocity - originVelocity;
        relativeSpeed = relativeVelocity.magnitude;
        if (relativeSpeed > 1e-6d)
        {
            var angle = VectorLF3.AngleDEG(relativeVelocity, (VectorLF3)departureDirection);
            var turn = (float)(1.6d / Math.Max(10d, angle));
            var desiredRelative = (VectorLF3)departureDirection * relativeSpeed;
            var steered = (VectorLF3)Vector3.Slerp((Vector3)relativeVelocity, (Vector3)desiredRelative, turn);
            var steeringDelta = steered - relativeVelocity;
            player.controller.actionSail.UseSailEnergy(ref steeringDelta, 0.36d);
            player.uVelocity += steeringDelta;
            relativeVelocity = player.uVelocity - originVelocity;
            relativeSpeed = relativeVelocity.magnitude;
        }

        var departureSpeed = Math.Min(200d, player.controller.actionSail.maxSailSpeed);
        if (relativeSpeed < departureSpeed)
        {
            var acceleration = Math.Min(7d, departureSpeed - relativeSpeed);
            var ratio = player.controller.actionSail.UseSailEnergy(acceleration);
            player.uVelocity += (VectorLF3)departureDirection * (acceleration * ratio);
        }

        return true;
    }

    private static void EnterNativeFlight(Player player)
    {
        var previous = player.movementState;
        player.controller.actionWalk.SwitchToFly();
        var next = player.controller.movementStateInFrame;
        player.movementState = next;
        if (next != previous)
        {
            player.controller.NotifyMovementStateChange(previous, next);
        }
    }

    private static bool TryEnterCurrentVersionNativeSail(Player player)
    {
        var controller = player.controller;
        var fly = controller.actionFly;
        if (player.movementState != EMovementState.Fly
            || fly.targetAltitude < 50f
            || fly.currentAltitude <= 49f
            || controller.horzSpeed <= 12.5f
            || player.mecha.thrusterLevel < 2
            || GameCamera.instance is null
            || GameMain.gameScenario is null)
        {
            return false;
        }

        // Assembly-CSharp exposes no SwitchToSail method. These are the exact
        // side effects in PlayerMove_Fly.GameTick after its four guards above;
        // velocity, position, energy, gravity, and collision remain untouched.
        if (controller.cmd.type == ECommand.Build)
        {
            controller.cmd.SetNoneCommand();
            controller.actionBuild.blueprintMode = EBlueprintMode.None;
        }

        var previous = player.movementState;
        controller.movementStateInFrame = EMovementState.Sail;
        controller.actionSail.ResetSailState();
        GameCamera.instance.SyncForSailMode();
        GameMain.gameScenario.NotifyOnSailModeEnter();
        player.movementState = controller.movementStateInFrame;
        if (player.movementState != previous)
        {
            controller.NotifyMovementStateChange(previous, player.movementState);
        }

        return true;
    }

    private static void ApplyNativeAscentInput(ActionRecord action, Player player)
    {
        if (!action.FlightAscentInputOwned)
        {
            action.FlightOriginalVerticalInput = player.controller.input1.y;
            action.FlightOriginalForwardInput = player.controller.input0.y;
            action.FlightAscentInputOwned = true;
        }

        // This is the same vertical-control channel PlayerMove_Fly.GameTick
        // reads from ordinary player input. Keeping it positive across native
        // ticks is required when rendering and simulation advance at different
        // rates; targetAltitude alone decays back toward 15 between frames.
        player.controller.input1.y = 1f;
        // Sail entry also requires PlayerController.horzSpeed > 12.5. The
        // actionFly.moveVelocity state decays toward zero when input0 is idle,
        // so keep the ordinary forward-control channel asserted until DSP's
        // native GameTick performs the Fly -> Sail transition.
        player.controller.input0.y = 1f;
    }

    private static void ReleaseNativeAscentInput(ActionRecord action)
    {
        if (!action.FlightAscentInputOwned)
        {
            return;
        }

        var controller = GameMain.mainPlayer?.controller;
        if (controller is not null && Math.Abs(controller.input1.y - 1f) < 0.0001f)
        {
            controller.input1.y = action.FlightOriginalVerticalInput;
        }

        if (controller is not null && Math.Abs(controller.input0.y - 1f) < 0.0001f)
        {
            controller.input0.y = action.FlightOriginalForwardInput;
        }

        action.FlightAscentInputOwned = false;
    }

    private static double CalculateAvailableFlightEnergy(Mecha mecha)
    {
        var available = Math.Max(0d, mecha.coreEnergy) + Math.Max(0d, mecha.reactorEnergy);
        var storage = mecha.reactorStorage;
        var grids = storage?.grids ?? Array.Empty<StorageComponent.GRID>();
        var limit = Math.Min(storage?.size ?? 0, grids.Length);
        for (var index = 0; index < limit; index++)
        {
            var grid = grids[index];
            var item = grid.itemId > 0 && grid.count > 0 ? LDB.items.Select(grid.itemId) : null;
            if (item is not null && item.HeatValue > 0L && item.FuelType > 0)
            {
                available += item.HeatValue * (double)grid.count;
            }
        }

        return available;
    }
}
