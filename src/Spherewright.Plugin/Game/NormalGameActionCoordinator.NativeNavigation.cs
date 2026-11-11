using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareNativeNavigationOnMainThread(string? sessionId,
        PrepareMoveRequest request, SessionState session)
    {
        if (NativeSurfaceNavigationPolicy.ValidateRequest(request) is { } invalid) return InvalidPlan(invalid);
        if (_flightCheckpoints.VoyagerJourney is { } journey && !IsVoyagerJourneyClosed(journey))
            return InvalidPlan("native_navigation_open_journey_return_binding_unavailable");
        var captured = CaptureNativeNavigation(sessionId!, request);
        if (!captured.Success || captured.Value is null)
            return GameCallResult<PreparedNormalAction>.Failed(captured.Error!);
        var binding = captured.Value;
        var result = AddPreparedPlan(NormalActionPlanPayload.ForNativeNavigation(binding), session,
            NativeSurfaceNavigationPolicy.MaximumGameTicks,
            "Native planetary navigation observes Fly, native arrival and a grounded stationary stop on the same owned planet.");
        if (result.Value is not null) result.Value.NativeSurfaceNavigation = binding.Plan;
        return result;
    }

    private GameCallResult<NativeNavigationBinding> CaptureNativeNavigation(string sessionId,
        PrepareMoveRequest request, string? expectedActionHash = null)
    {
        var observed = _reader.GetPlayerStateOnMainThread(sessionId, new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!observed.Success || observed.Value is null) return GameCallResult<NativeNavigationBinding>.Failed(observed.Error!);
        var snapshot = observed.Value;
        var player = GameMain.mainPlayer;
        var planet = GameMain.localPlanet;
        var controller = player?.controller;
        var nav = player?.navigation;
        if (player?.mecha is null || controller is null || nav is null || planet is null || planet.id != request.PlanetId
            || planet.type == EPlanetType.Gas || !ReferenceEquals(player.planetData, planet)
            || player.mecha.thrusterLevel < 1 || !NativeNavigationGroundedStop(player)
            || nav.navigating || nav.hasTask || player.currentOrder is not null || GameMain.data.guideRunning
            || !NativeNavigationInputsIdle(player) || !player.mecha.constructionModule.allDronesIdle
            || snapshot.ConstructionDrones.PendingBuildTargets != 0 || snapshot.ConstructionDrones.PendingRepairTargets != 0)
            return NavigationFailed("native_navigation_grounded_idle_binding_required");
        var astros = GameMain.galaxy?.astrosData;
        if (astros is null || planet.id < 1 || planet.id >= astros.Length
            || !IsFinite(astros[planet.id].uRadius) || Math.Abs(astros[planet.id].uRadius - planet.realRadius) > .01)
            return NavigationFailed("native_navigation_current_astro_radius_unproved");
        var actionHash = CanonicalStateHash.PlayerAction(snapshot);
        if (expectedActionHash is null ? request.ExpectedPlayerStateHash != snapshot.StateHash : expectedActionHash != actionHash)
            return NavigationFailed("native_navigation_player_changed");
        var requested = ToVector(request.Target);
        if (!IsFinite(requested.x) || !IsFinite(requested.y) || !IsFinite(requested.z)
            || requested.magnitude < 1 || Mathf.Abs(requested.magnitude - player.position.magnitude) > 8)
            return NavigationFailed("native_navigation_surface_target_required");
        var target = requested.normalized * player.position.magnitude;
        var arc = PlayerNavigation.SphericalDistance(player.position, target, planet.realRadius, false);
        var energy = CaptureNavigationTickCost(player, out var costBlocker);
        if (!energy.HasValue) return NavigationFailed(costBlocker!);
        var loadedEnergy = CalculateAvailableFlightEnergy(player.mecha);
        if (!NativeSurfaceNavigationPolicy.CanFund(arc, nav.flyThreshold, nav.sailThreshold,
            energy.Value, player.mecha.coreEnergy, loadedEnergy, 0))
            return NavigationFailed(FormattableString.Invariant(
                $"native_navigation_threshold_or_complete_energy_budget_unproved (arc={arc:R}, fly={nav.flyThreshold:R}, sail={nav.sailThreshold:R}, joulesPerTick={energy.Value:R}, coreJoules={player.mecha.coreEnergy:R}, loadedJoules={loadedEnergy:R})"));
        var plan = new NativeSurfaceNavigationPlan
        {
            PlanetId = request.PlanetId, SessionId = sessionId, PlayerActionStateHash = actionHash,
            Target = Snapshot(target), ArcDistanceMetres = arc,
            NativeFlyThresholdMetres = nav.flyThreshold, NativeSailThresholdMetres = nav.sailThreshold,
            ArrivalToleranceMetres = request.ArrivalTolerance, MaximumGameTicks = NativeSurfaceNavigationPolicy.MaximumGameTicks,
            LandingReserveGameTicks = NativeSurfaceNavigationPolicy.LandingReserveGameTicks,
            EnergyUpperPerTickJoules = energy.Value,
            RequiredLoadedEnergyJoules = energy.Value * NativeSurfaceNavigationPolicy.MaximumGameTicks,
        };
        plan.BindingHash = NativeSurfaceNavigationPolicy.Hash(sessionId, actionHash, plan);
        return GameCallResult<NativeNavigationBinding>.Succeeded(new NativeNavigationBinding
        {
            Plan = plan, SessionId = sessionId, PlayerHash = actionHash, World = GameMain.data, Planet = planet,
            Player = player, Mecha = player.mecha, Controller = controller, Navigation = nav,
        });
    }

    private static bool NativeNavigationInputsIdle(Player player) => LocalRawMovementInputIdle()
        && !(bool)VFInput._warpKey && !(bool)VFInput._sailSpeedUp
        && player.controller.input0.sqrMagnitude == 0 && player.controller.input1.sqrMagnitude == 0
        && !player.warpCommand && player.warpState == 0
        && player.controller.actionWalk.jumpDelayTick <= 0;

    private static bool NativeNavigationGroundedStop(Player player) => player.isAlive
        && player.movementState == EMovementState.Walk && player.speed <= .1f
        && player.controller.velocity.sqrMagnitude <= .01f && player.controller.actionWalk.isGrounded
        && player.controller.softLandingRecover >= .99f;

    private static double? CaptureNavigationTickCost(Player player, out string? blocker)
    {
        blocker = null;
        var work = VoyagerMechaWorkQuiescencePolicy.Evaluate(CaptureVoyagerMechaWork(player),
            allowRecipeOnlyMechaResearch: true);
        if (!work.Proved)
        {
            blocker = work.BlockerCode;
            return null;
        }
        var background = CaptureNativeMechaBackgroundCosts(player);
        double independent = 0;
        if (!NativeMechaBackgroundCostPolicy.IndependentCombatCoreDebitsExcluded(background))
        {
            var recharge = NativeMechaRechargeCostPolicy.Evaluate(CaptureNativeMechaRechargeFacts(player, background,
                allowRecipeOnlyMechaResearch: true));
            if (!recharge.Proved)
            {
                blocker = recharge.BlockerCode;
                return null;
            }
            independent = recharge.PerNativeTickEnergyUpperJoules!.Value;
        }
        var controller = player.controller;
        var cost = NativeLandingTickEnergyPolicy.Evaluate(new()
        {
            OwnedOrderAndNoManualInput = NativeNavigationInputsIdle(player), JumpAndGuideExcluded = !GameMain.data.guideRunning,
            WalkSpeed = player.mecha.walkSpeed,
            ObservedRtsSpeed = Math.Max(controller.actionWalk.rtsVelocity.magnitude,
                Math.Max(controller.actionFly.moveVelocity.magnitude, controller.actionFly.rtsVelocity.magnitude)),
            WalkPower = player.mecha.walkPower,
            OtherWorkPower = player.mecha.miningPower + player.mecha.replicatePower + player.mecha.researchPower,
            ThrustPowerPerAcceleration = player.mecha.thrustPowerPerAcc, ConsumptionRatio = player.mecha.reactorPowerConsRatio,
            GravityUpper = NativeLocalGravityUpperBound(player, allowUnlandedSail: true),
            VerticalSpeed = Math.Max(60, Math.Abs(controller.vertSpeed)), IndependentEnergyPerTick = independent,
            // Native navigation switches Walk to Fly directly. The input and
            // pending-jump guards exclude PlayerMove_Walk.UseJumpEnergy.
            PotentialJumpEnergy = 0,
        });
        if (!cost.HasValue) blocker = "native_navigation_tick_cost_inputs_unproved";
        return cost;
    }

    private BridgeError? RevalidateNativeNavigationOnMainThread(NormalActionPlanPayload payload)
    {
        var expected = payload.NativeNavigation;
        if (expected is null) return Stale("native_navigation_binding_missing");
        if (_flightCheckpoints.VoyagerJourney is { } journey && !IsVoyagerJourneyClosed(journey))
            return Stale("native_navigation_open_journey_return_binding_unavailable");
        var captured = CaptureNativeNavigation(payload.SessionId, new PrepareMoveRequest
        {
            PlanetId = payload.PlanetId, UseNativeNavigation = true, Target = expected.Plan.Target,
            ArrivalTolerance = expected.Plan.ArrivalToleranceMetres,
        }, expected.PlayerHash);
        return captured.Success && captured.Value is { } actual && actual.Plan.BindingHash == expected.Plan.BindingHash
            && ReferenceEquals(actual.World, expected.World) && ReferenceEquals(actual.Player, expected.Player)
            && ReferenceEquals(actual.Controller, expected.Controller) && ReferenceEquals(actual.Navigation, expected.Navigation)
            && ReferenceEquals(actual.Planet, expected.Planet) && ReferenceEquals(actual.Mecha, expected.Mecha)
                ? null : captured.Error ?? Stale("native_navigation_identity_or_cost_changed");
    }

    private void StartNativeNavigation(ActionRecord action)
    {
        var binding = action.Plan.NativeNavigation!;
        action.NativeSurfaceNavigationReadback = new() { PlanBindingHash = binding.Plan.BindingHash };
        action.NavigationLastTick = GameMain.gameTick;
        action.NavigationDeadline = DateTimeOffset.UtcNow.AddSeconds(120);
        binding.Navigation.Navigate(action.PlanetId, ToVector(binding.Plan.Target));
        if (!OwnsNativeNavigation(binding)) throw new InvalidOperationException("Native planetary navigation did not bind its exact plan.");
        action.NativeSurfaceNavigationReadback.NativeNavigationStarted = true;
        action.State = NormalActionStates.WaitingForGame;
        action.Message = "DSP accepted Native same-planet navigation with its current flight thresholds.";
    }

    private static bool OwnsNativeNavigation(NativeNavigationBinding binding) => binding.Navigation.navigating
        && binding.Navigation.naviAstroId == binding.Plan.PlanetId
        && binding.Navigation.naviTarget.Equals((VectorLF3)ToVector(binding.Plan.Target))
        && binding.Navigation.useFly && !binding.Navigation.useSail && !binding.Navigation.useWarp;

    private void UpdateNativeNavigation(ActionRecord action)
    {
        var binding = action.Plan.NativeNavigation!;
        var player = binding.Player;
        if (!ReferenceEquals(GameMain.data, binding.World) || !ReferenceEquals(GameMain.mainPlayer, player)
            || !ReferenceEquals(GameMain.localPlanet, binding.Planet) || !_sessions.IsCurrentSessionOwned
            || _sessions.SessionId != action.SessionId || !ReferenceEquals(player.controller, binding.Controller)
            || !ReferenceEquals(player.navigation, binding.Navigation) || !ReferenceEquals(player.mecha, binding.Mecha)
            || player.planetId != action.PlanetId)
        {
            UnknownNativeNavigation(action, "Native navigation lost its original world/player/planet binding.");
            return;
        }
        action.NavigationLastTick = GameMain.gameTick;
        var readback = action.NativeSurfaceNavigationReadback!;
        readback.NativeGameTicks = GameMain.gameTick - action.StartedAtGameTick;
        var distance = Vector3.Distance(player.position, ToVector(binding.Plan.Target));
        readback.FinalDistanceMetres = distance;
        readback.NativeFlyObserved |= player.movementState == EMovementState.Fly;
        if (!NativeNavigationInputsIdle(player) || player.currentOrder is not null || player.movementState >= EMovementState.Sail)
        {
            UnknownNativeNavigation(action, "Native navigation encountered foreign input/order or an unexpected Sail transition.");
            return;
        }
        if (!action.NavigationLanding)
        {
            if (binding.Navigation.flyThreshold != binding.Plan.NativeFlyThresholdMetres
                || binding.Navigation.sailThreshold != binding.Plan.NativeSailThresholdMetres
                || (binding.Navigation.hasTask && !OwnsNativeNavigation(binding)))
            {
                UnknownNativeNavigation(action, "The owned navigation thresholds or task changed during execution.");
                return;
            }
            if (!OwnsNativeNavigation(binding))
            {
                var horizontal = Vector3.Distance(player.position.normalized * binding.Planet.realRadius,
                    ToVector(binding.Plan.Target).normalized * binding.Planet.realRadius);
                if (horizontal < 3.01f && readback.NativeFlyObserved)
                {
                    readback.NativeArrivalObserved = true;
                    action.NavigationLanding = true;
                }
                else BeginNativeNavigationLanding(action, "The original Native navigation task ended before owned arrival proof.");
            }
            else if (binding.Navigation.DetermineArrive())
            {
                binding.Navigation.Arrive();
                readback.NativeArrivalObserved = true;
                action.NavigationLanding = true;
            }
            else
            {
                var cost = CaptureNavigationTickCost(player, out _);
                if (!player.mecha.constructionModule.allDronesIdle
                    || !cost.HasValue || cost.Value > binding.Plan.EnergyUpperPerTickJoules * (1 + 1e-8)
                    || player.mecha.coreEnergy < binding.Plan.EnergyUpperPerTickJoules * 2
                    || CalculateAvailableFlightEnergy(player.mecha) < binding.Plan.EnergyUpperPerTickJoules * binding.Plan.LandingReserveGameTicks
                    || readback.NativeGameTicks >= binding.Plan.MaximumGameTicks - binding.Plan.LandingReserveGameTicks
                    || DateTimeOffset.UtcNow >= action.NavigationDeadline.AddSeconds(-30))
                    BeginNativeNavigationLanding(action, "The bounded navigation work or energy allowance ended.");
            }
        }
        if (!action.NavigationLanding) return;
        if (player.movementState == EMovementState.Fly) player.controller.actionFly.targetAltitude = 1f;
        readback.NativeNavigationReleased = !binding.Navigation.hasTask && !binding.Navigation.navigating;
        if (readback.NativeNavigationReleased && NativeNavigationGroundedStop(player))
        {
            readback.GroundedStopVerified = true;
            if (action.NavigationFailure is null && readback.NativeArrivalObserved && readback.NativeFlyObserved
                && distance <= action.Plan.ArrivalTolerance)
                Complete(action, "Native planetary navigation flew, arrived and settled on the original planet.");
            else
                Fail(action, action.NavigationFailure ?? "Native navigation settled outside the declared arrival tolerance.");
        }
        else if (readback.NativeGameTicks > binding.Plan.MaximumGameTicks || DateTimeOffset.UtcNow > action.NavigationDeadline)
            UnknownNativeNavigation(action, "The original Native navigation landing has no bounded grounded-stop proof.");
    }

    private static void BeginNativeNavigationLanding(ActionRecord action, string failure)
    {
        var binding = action.Plan.NativeNavigation!;
        action.NavigationFailure = failure;
        action.NavigationLanding = true;
        if (OwnsNativeNavigation(binding)) binding.Navigation.Abort();
    }

    private void UnknownNativeNavigation(ActionRecord action, string message)
    {
        var binding = action.Plan.NativeNavigation!;
        if (ReferenceEquals(GameMain.data, binding.World) && ReferenceEquals(GameMain.mainPlayer, binding.Player)
            && OwnsNativeNavigation(binding)) binding.Navigation.Abort();
        action.State = NormalActionStates.OutcomeUnknown;
        action.Terminal = true;
        action.Message = message;
        action.OriginalOutcomeMessage = message;
        action.CompletedAtGameTick = action.NavigationLastTick;
        if (ReferenceEquals(GameMain.data, binding.World) && ReferenceEquals(GameMain.mainPlayer, binding.Player))
            action.AfterInventory = CaptureInventory(binding.Player);
        if (_sessions.SessionId == action.SessionId) _sessions.QuarantineWritesOnMainThread(action.ActionId, message);
    }

    private static GameCallResult<NativeNavigationBinding> NavigationFailed(string reason)
        => GameCallResult<NativeNavigationBinding>.Failed(BridgeError.Create(BridgeErrorCodes.ActionRejected,
            reason, true, "Use the current owned grounded planet, current Native navigation thresholds and complete energy funding."));

    private sealed class NativeNavigationBinding
    {
        public NativeSurfaceNavigationPlan Plan { get; set; } = null!;
        public string SessionId { get; set; } = string.Empty;
        public string PlayerHash { get; set; } = string.Empty;
        public GameData World { get; set; } = null!;
        public PlanetData Planet { get; set; } = null!;
        public Player Player { get; set; } = null!;
        public Mecha Mecha { get; set; } = null!;
        public PlayerController Controller { get; set; } = null!;
        public PlayerNavigation Navigation { get; set; } = null!;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public NativeNavigationBinding? NativeNavigation { get; set; }
        public static NormalActionPlanPayload ForNativeNavigation(NativeNavigationBinding binding) => new()
        {
            ActionKind = NormalActionKinds.Move, SessionId = binding.SessionId,
            PlanetId = binding.Plan.PlanetId, ExpectedStateHash = binding.Plan.BindingHash, PlayerStateHash = binding.PlayerHash,
            TargetPosition = ToVector(binding.Plan.Target), ArrivalTolerance = binding.Plan.ArrivalToleranceMetres,
            EstimatedDistance = binding.Plan.ArcDistanceMetres, NativeNavigation = binding,
        };
    }

    private sealed partial class ActionRecord
    {
        public NativeSurfaceNavigationReadback? NativeSurfaceNavigationReadback { get; set; }
        public DateTimeOffset NavigationDeadline { get; set; }
        public long NavigationLastTick { get; set; }
        public bool NavigationLanding { get; set; }
        public string? NavigationFailure { get; set; }
    }
}
