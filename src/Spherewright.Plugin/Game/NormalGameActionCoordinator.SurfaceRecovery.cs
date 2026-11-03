using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private SurfaceRecoveryState? _surfaceRecovery;

    private sealed class SurfaceRecoveryState
    {
        public SurfaceRecoveryState(VoyagerRemoteResumeAdoption adoption, NativeLocalWalkSceneBinding scene, Vector3 target)
        {
            Adoption = adoption; Scene = new FirstFrameSurfaceState(scene, null); Target = target;
            Origin = adoption.Player.position; Binding = new VoyagerNativeBinding(adoption.World, adoption.Player);
            Body = adoption.Player.controller.rigid; ActorTransform = adoption.Player.transform; Parent = ActorTransform.parent;
            Scale = ActorTransform.lossyScale; AutoSync = Physics.autoSyncTransforms; QueriesHitTriggers = Physics.queriesHitTriggers;
            Kinematic = Body.isKinematic; UseGravity = Body.useGravity; Constraints = Body.constraints;
            Source = LoadedGroundSyncState.FromNativeFrame(adoption.Player, GameMain.data.relativePos, GameMain.data.relativeRot);
            Policy = new VoyagerSurfaceRecoveryPolicy(adoption.Evidence.SourceGameTick, DateTimeOffset.UtcNow);
        }
        public VoyagerRemoteResumeAdoption Adoption { get; }
        public FirstFrameSurfaceState Scene { get; }
        public LoadedGroundSyncState Source { get; }
        public VoyagerSurfaceRecoveryPolicy Policy { get; }
        public VoyagerNativeBinding Binding { get; }
        public Rigidbody Body { get; }
        public Transform ActorTransform { get; }
        public Transform? Parent { get; }
        public Vector3 Scale { get; }
        public bool AutoSync { get; }
        public bool QueriesHitTriggers { get; }
        public bool Kinematic { get; }
        public bool UseGravity { get; }
        public RigidbodyConstraints Constraints { get; }
        public Vector3 Origin { get; }
        public Vector3 Target { get; }
        public OrderNode? Order { get; set; }
        public bool NativeOrderAccepted { get; set; }
        public bool Draining { get; set; }
    }

    private string? HandleSurfaceRecovery(VoyagerRemoteResumeAdoption a, VoyagerJourneyRecord j,
        bool loadBoundary, bool atPhysics, out bool completed)
    {
        completed = false;
        if (_surfaceRecovery is not { } s || !ReferenceEquals(s.Adoption, a))
        {
            if (!loadBoundary || !_sessions.ExactRemoteResumeLoadPreflight(a) || !a.Evidence.RemotePrimaryRecovery
                || !a.Evidence.CompletedSavedLandingAdmissionUsed || a.Authorization?.Proposal.SurfaceRecoveryTarget is not { } proposed)
                return SurfaceFailure(a, "surface_recovery_exact_new_source_unproved");
            if (_surfaceRecovery is { Policy.Completed: false }) return SurfaceFailure(a, "surface_recovery_previous_task_unsettled");
            var p = a.Player; var target = ToVector(proposed); var scene = CaptureNativeLocalWalkScene();
            if (scene is null || !scene.MatchesCurrent() || GameMain.gameTick != a.Evidence.SourceGameTick
                || p.movementState != EMovementState.Drift || p.speed > .15f || p.currentOrder is not null
                || p.orders.orderCount != 0 || !VoyagerMechaWorkIdle(p) || !BuildUiIsIdle(p)
                || NativeWalkModeBlocker(p, true, scene, true, true, true) is not null
                || p.controller.velocity.sqrMagnitude != 0f || p.controller.rigid.velocity.sqrMagnitude != 0f
                || p.controller.rigid.isKinematic || !p.controller.rigid.detectCollisions
                || p.controller.actionWalk.rtsVelocity.sqrMagnitude != 0f || p.controller.actionDrift.rtsVelocity.sqrMagnitude != 0f
                || !TryObserveOwnedConstructionDrones(p.factory, p.mecha.constructionModule, out var idle, out _) || !idle
                || RemoteResumeChargeEnvironmentBlocker(p, j.JourneyId, a.CurrentDecisionEvidence, provedSavedLandingAdmission: true) is not null
                || !IsFinite(target.x) || !IsFinite(target.y) || !IsFinite(target.z)
                || Math.Abs(target.magnitude - p.position.magnitude) > 4f)
                return SurfaceFailure(a, "surface_recovery_source_native_premises_unproved");
            target = target.normalized * p.position.magnitude;
            var distance = Vector3.Distance(p.position, target);
            if (distance < 1f || distance > VoyagerSurfaceRecoveryPolicy.MaximumTargetDistanceMeters)
                return SurfaceFailure(a, "surface_recovery_new_target_distance_unproved");
            var sourceCost = EvaluateNativeLocalMovementCost(p, modeTransitionsExcluded: true);
            var armedUtc = DateTimeOffset.UtcNow;
            if (!sourceCost.Proved || !VoyagerSurfaceRecoveryPolicy.OriginalReserveAllows(new VoyagerSurfaceRecoveryFacts
            {
                GameTick = GameMain.gameTick, NowUtc = armedUtc, CoreEnergyJoules = p.mecha.coreEnergy,
                PerNativeTickEnergyJoules = sourceCost.PerNativeTickEnergyJoules,
                StopWallPatienceSeconds = VoyagerActiveMoveGroundGapPolicy.StopWallPatience(Time.fixedDeltaTime) ?? double.NaN,
                OriginalJourney = SurfaceRecoveryJourneyBudget(a, j, armedUtc),
            })) return SurfaceFailure(a, "surface_recovery_source14_reserve_unproved");
            s = _surfaceRecovery = new SurfaceRecoveryState(a, scene, target);
            if (!s.Source.Finite || !s.Source.Matches(p)) return SurfaceFailure(a, "surface_recovery_source_state_unproved");
            a.ChargeStartedGameTick = a.Evidence.SourceGameTick;
            a.ChargeStartedUtc = s.Policy.StartedUtc;
            a.SurfaceRecoveryPending = true;
            RecordSurfaceRecovery(s, "armed");
            _sessions.RecordRemoteResumeGroundObservation(a, GameStateReader.CaptureNativeGroundContact(p, "surface_recovery_source"), false);
            // The exact loader receipt authorizes only the first151/501 probe.
            // No order, movement, velocity, lifecycle or Save is invoked here.
            return null;
        }
        if (s.Policy.Failure is { } failure) return SurfaceFailure(a, failure);
        if (s.Policy.Completed) { completed = true; return null; }
        if (!atPhysics) return null;
        var player = a.Player;
        var firstTick = s.Policy.LastPhysicsTick == s.Policy.SourceGameTick;
        var exact = SurfaceRecoveryBinding(s) && (!firstTick
            || s.Source.CurrentBlockerAtNativePhysicsTick(player, GameMain.gameTick) is null);
        var order = s.Order is null ? firstTick && player.currentOrder is null
            : s.Draining ? player.currentOrder is null
            : ReferenceEquals(player.currentOrder, s.Order) || player.currentOrder is null && s.Order.targetReached;
        var native = order && player.orders.orderCount == 0 && BuildUiIsIdle(player) && VoyagerMechaWorkIdle(player)
            && NativeWalkModeBlocker(player, false, s.Scene.Scene, true, true, true) is null
            && TryObserveOwnedConstructionDrones(player.factory, player.mecha.constructionModule, out var dronesIdle, out _) && dronesIdle
            && RemoteResumeChargeEnvironmentBlocker(player, j.JourneyId, a.CurrentDecisionEvidence,
                provedSavedLandingAdmission: true, allowBoundedSurfaceRts: true) is null;
        var reserve = EvaluateNativeLocalMovementCost(player, native);
        var now = DateTimeOffset.UtcNow;
        var grounded = Physics.CheckSphere(player.position + player.position.normalized * .15f, .35f, 15873);
        var c = player.controller;
        var strict = grounded && player.movementState == EMovementState.Walk && c.movementStateInFrame == EMovementState.Walk
            && player.speed <= .1f && c.actionWalk.rtsVelocity.sqrMagnitude == 0f && c.actionWalk.moveVelocity.sqrMagnitude == 0f
            && c.actionDrift.rtsVelocity.sqrMagnitude == 0f && c.actionDrift.moveVelocity.sqrMagnitude == 0f;
        var f = new VoyagerSurfaceRecoveryFacts
        {
            GameTick = GameMain.gameTick, NowUtc = now, ExactSourceWorldAndScene = exact,
            NativeModesCostsAndOrderProved = native && reserve.Proved,
            CurrentTerrainSupportProved = SurfaceRecoveryTerrain(s, grounded), DryLandingPatchProved = SurfaceRecoveryLandingPatch(s),
            StrictGroundedWalkWithZeroTail = strict, OriginalOrderAbsent = s.Order is not null && player.currentOrder is null,
            DistanceFromSourceMeters = Vector3.Distance(player.position, s.Origin), DistanceToTargetMeters = Vector3.Distance(player.position, s.Target),
            CoreEnergyJoules = player.mecha.coreEnergy, PerNativeTickEnergyJoules = reserve.PerNativeTickEnergyJoules,
            StopWallPatienceSeconds = VoyagerActiveMoveGroundGapPolicy.StopWallPatience(Time.fixedDeltaTime) ?? double.NaN,
            OriginalJourney = SurfaceRecoveryJourneyBudget(a, j, now),
        };
        var e = a.CurrentDecisionEvidence!;
        e.Stage("surface_recovery_native501"); e.Observe("surface_exact_source_scene", f.ExactSourceWorldAndScene);
        e.Observe("surface_native_modes_costs_order", f.NativeModesCostsAndOrderProved);
        e.Observe("surface_current_owned_support", f.CurrentTerrainSupportProved);
        e.Observe("surface_dry_landing_patch", f.DryLandingPatchProved);
        e.Observe("surface_strict_grounded_walk_zero_tail", strict);
        e.Number("surface_source_tick", s.Policy.SourceGameTick); e.Number("surface_core_j", f.CoreEnergyJoules);
        e.Number("surface_native_tick_upper_j", f.PerNativeTickEnergyJoules);
        e.Number("surface_distance_from_source_m", f.DistanceFromSourceMeters); e.Number("surface_target_distance_m", f.DistanceToTargetMeters);
        var step = s.Policy.ObservePhysics(f);
        RecordSurfaceRecovery(s, step == VoyagerSurfaceRecoveryStep.Complete ? "completed_idle"
            : step == VoyagerSurfaceRecoveryStep.Rejected ? "failed" : "native_work", step == VoyagerSurfaceRecoveryStep.Complete);
        if (step == VoyagerSurfaceRecoveryStep.Rejected) return SurfaceFailure(a, s.Policy.Failure!);
        if (step == VoyagerSurfaceRecoveryStep.Complete)
        { a.SurfaceRecoveryPending = false; completed = true; return null; }
        if (s.Order is null)
        {
            s.Order = OrderNode.MoveTo(s.Target);
            player.Order(s.Order, false);
            if (!ReferenceEquals(player.currentOrder, s.Order)) return SurfaceFailure(a, "surface_recovery_native_order_not_accepted");
            s.NativeOrderAccepted = true;
            RecordSurfaceRecovery(s, "normal_native_order_accepted");
        }
        else if (!s.Draining && (s.Order.targetReached || f.DistanceToTargetMeters <= 1.5d))
        {
            if (ReferenceEquals(player.currentOrder, s.Order)) player.AbortOrder();
            if (player.currentOrder is not null) return SurfaceFailure(a, "surface_recovery_original_order_stop_unproved");
            s.Draining = true;
            RecordSurfaceRecovery(s, "native_settlement_pending");
        }
        // Existing pending admission retains its exclusive source/Journal
        // leases. This is not strict idle or permission for another writer.
        return VoyagerRecoveryWindowPolicy.ChargingPending;
    }

    private VoyagerLocalActionBudgetFacts SurfaceRecoveryJourneyBudget(VoyagerRemoteResumeAdoption a, VoyagerJourneyRecord j, DateTimeOffset now) => new()
    {
        ExactRemoteBinding = _sessions.IsCurrentSessionOwned && _sessions.SessionId == a.SessionId
            && ReferenceEquals(GameMain.data, a.World) && ReferenceEquals(GameMain.mainPlayer, a.Player)
            && _flightCheckpoints.RemoteResumeEvidenceStillMatches(a.Evidence),
        NativePlayerIdle = false, CurrentGameTick = GameMain.gameTick,
        LastLandedGameTick = RemoteResumeAccountingAnchor(a, j), SpentGameTicks = j.SpentGameTicks,
        MaximumGameTicks = _sessions.EffectiveVoyagerMaximumGameTicks(j), ReturnReserveGameTicks = j.ReturnReserveGameTicks,
        OriginalExtraWarperReserve = j.ExtraWarperReserve, NativeWarperCount = CountVoyagerWarpers(a.Player.mecha.warpStorage),
        WorkGameTicks = 1, TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks, NowUtc = now,
        JourneyDeadlineUtc = _sessions.EffectiveVoyagerDeadline(j), AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(a.Player.mecha),
        LockedReturnFloorJoules = j.ReturnEnergyFloorJoules,
    };

    private bool SurfaceRecoveryBinding(SurfaceRecoveryState s) => _sessions.IsCurrentSessionOwned
        && _sessions.SessionId == s.Adoption.SessionId && ReferenceEquals(GameMain.data, s.Adoption.World)
        && ReferenceEquals(GameMain.mainPlayer, s.Adoption.Player) && !GameMain.isPaused
        && ReferenceEquals(s.Binding.Player.controller, s.Binding.Controller) && ReferenceEquals(s.Binding.Player.mecha, s.Binding.Mecha)
        && ReferenceEquals(s.Binding.Controller.rigid, s.Body) && ReferenceEquals(s.Binding.Player.transform, s.ActorTransform)
        && ReferenceEquals(s.ActorTransform.parent, s.Parent) && s.ActorTransform.lossyScale.Equals(s.Scale)
        && s.Body.isKinematic == s.Kinematic && s.Body.useGravity == s.UseGravity && s.Body.constraints == s.Constraints
        && !s.Body.isKinematic && s.Body.detectCollisions
        && Physics.simulationMode == SimulationMode.FixedUpdate && Physics.autoSyncTransforms == s.AutoSync && Physics.queriesHitTriggers == s.QueriesHitTriggers
        && _flightCheckpoints.RemoteResumeEvidenceStillMatches(s.Adoption.Evidence) && s.Scene.Matches();

    private string? ObserveSurfaceRecoveryUniverse(VoyagerRemoteResumeAdoption a)
    {
        if (_surfaceRecovery is not { } s || !ReferenceEquals(s.Adoption, a)) return SurfaceFailure(a, "surface_recovery_controller_missing");
        var exact = SurfaceRecoveryBinding(s);
        var first = s.Policy.LastPhysicsTick == s.Policy.SourceGameTick;
        var observed = default(VectorLF3);
        if (first) exact &= s.Source.CurrentBlockerAtNativeUniverseTick(a.Player, GameMain.gameTick, out observed) is null;
        var terrain = GameStateReader.CaptureNativeTerrain(a.Player, "surface_recovery_native151");
        exact &= NativeTerrainObservationPolicy.Blocker(terrain, 9) is null;
        var failure = s.Policy.ObserveUniverse(GameMain.gameTick, DateTimeOffset.UtcNow, exact);
        if (failure is not null) return SurfaceFailure(a, failure);
        if (first && !s.Source.TryPinNativeUniversePosition(observed)) return SurfaceFailure(a, "surface_recovery_first151_position_unproved");
        return null;
    }

    private static bool SurfaceRecoveryTerrain(SurfaceRecoveryState s, bool grounded)
    {
        var p = s.Adoption.Player; var up = p.position.normalized;
        var hits = new Collider[257]; var count = Physics.OverlapSphereNonAlloc(p.position + up * .15f, .35f, hits, 15873);
        if (count >= hits.Length || Enumerable.Range(0, count).Any(i => !s.Scene.Surface.Any(c => ReferenceEquals(c, hits[i])))) return false;
        if (grounded) return count > 0;
        return count == 0 && Physics.SphereCast(new Ray(p.position + up * .35f, -up), .35f, out var hit, 2f, 15873)
            && s.Scene.Surface.Any(c => ReferenceEquals(c, hit.collider)) && Vector3.Dot(hit.normal, up) >= .5f;
    }

    private static bool SurfaceRecoveryLandingPatch(SurfaceRecoveryState s)
    {
        var planet = s.Scene.Scene.Planet; var up = s.Target.normalized;
        var east = Vector3.Cross(up, Math.Abs(up.y) < .9f ? Vector3.up : Vector3.right).normalized;
        var north = Vector3.Cross(up, east).normalized;
        for (var i = 0; i < 9; i++)
        {
            var angle = (i - 1) * Math.PI / 4d;
            var normal = i == 0 ? up : (s.Target + 3.5f * ((float)Math.Cos(angle) * east + (float)Math.Sin(angle) * north)).normalized;
            var ray = new Ray(normal * (planet.realRadius + 10f), -normal);
            if (!Physics.Raycast(ray, out var ground, 30f, 8704, QueryTriggerInteraction.Collide)
                || !s.Scene.Surface.Any(c => ReferenceEquals(c, ground.collider)) || ground.point.magnitude < planet.realRadius - .8f) return false;
            if (planet.waterItemId > 0 && (!Physics.Raycast(ray, out var water, 30f, 16, QueryTriggerInteraction.Collide)
                || ground.point.magnitude < water.point.magnitude + .1f)) return false;
        }
        return true;
    }

    private static string SurfaceFailure(VoyagerRemoteResumeAdoption a, string code)
    {
        if (a.SurfaceRecoveryMove is { } readback) { readback.State = "failed"; readback.Failure ??= code; }
        a.RetainFailure(a.CurrentDecisionEvidence?.Failure(code)
            ?? NativeResumeDecisionEvidence.UnobservedFailure(code, "surface_recovery_native_controller", GameMain.gameTick, DateTimeOffset.UtcNow));
        return code; // The existing admission owner performs the exact Pause/quarantine.
    }

    private static void RecordSurfaceRecovery(SurfaceRecoveryState s, string state, bool idle = false)
        => s.Adoption.SurfaceRecoveryMove = new NativeSurfaceRecoveryMoveSnapshot
        {
            ResumeActionId = s.Adoption.ResumeActionId, State = state, SourceGameTick = s.Policy.SourceGameTick,
            CapturedAtGameTick = GameMain.gameTick, MaximumWorkGameTicks = VoyagerSurfaceRecoveryPolicy.MaximumWorkGameTicks,
            MaximumWallSeconds = VoyagerSurfaceRecoveryPolicy.MaximumWallSeconds, NativeOrderAccepted = s.NativeOrderAccepted,
            StrictPhysicalIdleProved = idle, Failure = s.Policy.Failure, SourcePosition = Snapshot(s.Origin),
            TargetPosition = Snapshot(s.Target), CurrentPosition = Snapshot(s.Adoption.Player.position),
        };
}
