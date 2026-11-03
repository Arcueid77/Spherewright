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
            TransformBinding = NativeContactTransformBinding.Capture(ActorTransform); AutoSync = Physics.autoSyncTransforms; QueriesHitTriggers = Physics.queriesHitTriggers;
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
        public NativeContactTransformBinding? TransformBinding { get; }
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
            var sourceModeBlocker = scene is null ? "surface_scene_missing" : NativeWalkModeBlocker(p, true, scene, true, true, true);
            var sourceModeProved = p.movementState == EMovementState.Drift && sourceModeBlocker is null
                || p.movementState == EMovementState.Walk && sourceModeBlocker == "remote_idle_ground_contact_unproved";
            if (scene is null || !scene.MatchesCurrent() || GameMain.gameTick != a.Evidence.SourceGameTick
                || !sourceModeProved || p.speed > .15f || p.currentOrder is not null
                || p.orders.orderCount != 0 || !VoyagerMechaWorkIdle(p) || !BuildUiIsIdle(p)
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
            if (s.TransformBinding is null || !s.Source.Finite || !s.Source.Matches(p)) return SurfaceFailure(a, "surface_recovery_source_state_unproved");
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
        // Preserve the FIRST comparison used by this decision. Do not run
        // another Native comparison after Pause to infer a different cause.
        var bindingBlocker = SurfaceRecoveryBindingBlocker(s);
        var sourceStateBlocker = bindingBlocker is null && firstTick
            ? s.Source.CurrentBlockerAtNativePhysicsTick(player, GameMain.gameTick) : null;
        var exact = bindingBlocker is null && sourceStateBlocker is null;
        var order = s.Order is null ? firstTick && player.currentOrder is null
            : s.Draining ? player.currentOrder is null
            : ReferenceEquals(player.currentOrder, s.Order) || player.currentOrder is null && s.Order.targetReached;
        var modeBlocker = NativeWalkModeBlocker(player, false, s.Scene.Scene, true, true, true);
        // Ground is the LAST predicate in that Native guard. Only this sole
        // miss can continue under the independently proved owned support,
        // per-tick14P and original finite controller; it is never idle.
        var modeProved = modeBlocker is null || modeBlocker == "remote_idle_ground_contact_unproved";
        var environmentBlocker = RemoteResumeChargeEnvironmentBlocker(player, j.JourneyId, a.CurrentDecisionEvidence,
            provedSavedLandingAdmission: true, allowBoundedSurfaceRts: true);
        var native = order && player.orders.orderCount == 0 && BuildUiIsIdle(player) && VoyagerMechaWorkIdle(player)
            && modeProved && environmentBlocker is null
            && TryObserveOwnedConstructionDrones(player.factory, player.mecha.constructionModule, out var dronesIdle, out _) && dronesIdle;
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
            NativeModesCostsAndOrderProved = native && reserve.Proved, NativeModeBlocker = modeBlocker,
            CurrentTerrainSupportProved = SurfaceRecoveryTerrain(s, grounded, a.CurrentDecisionEvidence!), DryLandingPatchProved = SurfaceRecoveryLandingPatch(s),
            StrictGroundedWalkWithZeroTail = strict, OriginalOrderAbsent = s.Order is not null && player.currentOrder is null,
            DistanceFromSourceMeters = Vector3.Distance(player.position, s.Origin), DistanceToTargetMeters = Vector3.Distance(player.position, s.Target),
            CoreEnergyJoules = player.mecha.coreEnergy, PerNativeTickEnergyJoules = reserve.PerNativeTickEnergyJoules,
            StopWallPatienceSeconds = VoyagerActiveMoveGroundGapPolicy.StopWallPatience(Time.fixedDeltaTime) ?? double.NaN,
            OriginalJourney = SurfaceRecoveryJourneyBudget(a, j, now),
        };
        var e = a.CurrentDecisionEvidence!;
        e.Stage("surface_recovery_native501"); e.Observe("surface_exact_source_scene", f.ExactSourceWorldAndScene);
        e.Require("surface_owned_order", order);
        e.Require(modeProved ? "surface_mode_bounded" : "surface_mode_" + modeBlocker, modeProved);
        e.Require(environmentBlocker is null ? "surface_environment" : "surface_environment_" + environmentBlocker, environmentBlocker is null);
        e.Require(reserve.Proved ? "surface_tick_cost" : "surface_tick_cost_" + reserve.BlockerCode, reserve.Proved);
        e.Require(bindingBlocker is null ? "surface_runtime_binding" : "surface_" + bindingBlocker, bindingBlocker is null);
        if (bindingBlocker is null)
            e.Require(sourceStateBlocker is null ? "surface_first_physics_player_state" : "surface_first_physics_" + sourceStateBlocker,
                sourceStateBlocker is null);
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

    private string? SurfaceRecoveryBindingBlocker(SurfaceRecoveryState s)
    {
        if (!_sessions.IsCurrentSessionOwned) return "session_not_owned";
        if (_sessions.SessionId != s.Adoption.SessionId) return "session_changed";
        if (!ReferenceEquals(GameMain.data, s.Adoption.World)) return "world_changed";
        if (!ReferenceEquals(GameMain.mainPlayer, s.Adoption.Player)) return "player_changed";
        if (GameMain.isPaused) return "native_paused";
        if (!ReferenceEquals(s.Binding.Player.controller, s.Binding.Controller)) return "controller_changed";
        if (!ReferenceEquals(s.Binding.Player.mecha, s.Binding.Mecha)) return "mecha_changed";
        if (!ReferenceEquals(s.Binding.Controller.rigid, s.Body)) return "rigidbody_changed";
        if (!ReferenceEquals(s.Binding.Player.transform, s.ActorTransform)) return "transform_changed";
        if (!ReferenceEquals(s.ActorTransform.parent, s.Parent)) return "parent_changed";
        if (s.TransformBinding?.Matches(s.ActorTransform) != true) return "scale_input_or_parent_chain_changed";
        if (s.Body.isKinematic != s.Kinematic) return "kinematic_changed";
        if (s.Body.useGravity != s.UseGravity) return "gravity_mode_changed";
        if (s.Body.constraints != s.Constraints) return "constraints_changed";
        if (s.Body.isKinematic) return "body_kinematic";
        if (!s.Body.detectCollisions) return "collisions_disabled";
        if (Physics.simulationMode != SimulationMode.FixedUpdate) return "simulation_mode_changed";
        if (Physics.autoSyncTransforms != s.AutoSync) return "auto_sync_changed";
        if (Physics.queriesHitTriggers != s.QueriesHitTriggers) return "trigger_query_changed";
        if (!_flightCheckpoints.RemoteResumeEvidenceStillMatches(s.Adoption.Evidence)) return "durable_evidence_changed";
        return s.Scene.CurrentBlocker();
    }

    private string? ObserveSurfaceRecoveryUniverse(VoyagerRemoteResumeAdoption a)
    {
        if (_surfaceRecovery is not { } s || !ReferenceEquals(s.Adoption, a)) return SurfaceFailure(a, "surface_recovery_controller_missing");
        var e = a.CurrentDecisionEvidence!; e.Stage("surface_recovery_native151");
        var runtimeBlocker = SurfaceRecoveryBindingBlocker(s);
        if (!e.Require(runtimeBlocker is null ? "surface151_runtime_binding" : "surface151_" + runtimeBlocker, runtimeBlocker is null))
            return SurfaceFailure(a, "surface_recovery_native151_binding_unproved");
        var first = s.Policy.LastPhysicsTick == s.Policy.SourceGameTick;
        var observed = default(VectorLF3);
        var sourceBlocker = first ? s.Source.CurrentBlockerAtNativeUniverseTick(a.Player, GameMain.gameTick, out observed) : null;
        if (!e.Require(sourceBlocker is null ? "surface151_player_state" : "surface151_" + sourceBlocker, sourceBlocker is null))
            return SurfaceFailure(a, "surface_recovery_native151_player_unproved");
        var terrain = GameStateReader.CaptureNativeTerrain(a.Player, "surface_recovery_native151");
        var terrainBlocker = NativeTerrainObservationPolicy.Blocker(terrain, 9);
        if (!e.Require(terrainBlocker is null ? "surface151_terrain" : "surface151_" + terrainBlocker, terrainBlocker is null))
            return SurfaceFailure(a, "surface_recovery_native151_terrain_unproved");
        var failure = s.Policy.ObserveUniverse(GameMain.gameTick, DateTimeOffset.UtcNow, true);
        if (failure is not null) return SurfaceFailure(a, failure);
        if (first && !s.Source.TryPinNativeUniversePosition(observed)) return SurfaceFailure(a, "surface_recovery_first151_position_unproved");
        return null;
    }

    private static bool SurfaceRecoveryTerrain(SurfaceRecoveryState s, bool grounded, NativeResumeDecisionEvidence evidence)
    {
        var p = s.Adoption.Player; var position = p.position; var up = position.normalized;
        var hits = new Collider[257];
        var count = Physics.OverlapSphereNonAlloc(position + up * .15f, .35f, hits, 15873, QueryTriggerInteraction.Ignore);
        var complete = count >= 0 && count < hits.Length;
        evidence.Number("surface_support_foot_count", count); evidence.Observe("surface_support_native_ground", grounded);
        if (!evidence.Require("surface_support_complete_foot_query", complete)) return false;
        var owned = true;
        for (var i = 0; i < count; i++)
        {
            var c = hits[i];
            var current = c is MeshCollider mesh && mesh.sharedMesh != null && c.enabled && c.gameObject.activeInHierarchy
                && !c.isTrigger && c.gameObject.layer == 9 && s.Scene.Surface.Count(t => ReferenceEquals(t, c)) == 1;
            if (!current)
            {
                owned = false; evidence.Number("surface_support_foreign_layer", c == null ? null : c.gameObject.layer);
                evidence.Observe("surface_support_foreign_is_trigger", c == null ? null : c.isTrigger);
                evidence.Observe("surface_support_foreign_is_current_ocean", c != null && ReferenceEquals(c, s.Scene.Scene.Ocean));
                evidence.Observe("surface_support_foreign_is_current_sphere", c != null && ReferenceEquals(c, s.Scene.Scene.Simulator.sphereCollider));
                break;
            }
        }
        if (!evidence.Require("surface_support_exact_current_foot_owners", owned)) return false;
        if (grounded)
        {
            evidence.Require("surface_support_grounded_foot_count", count > 0);
            return NativeSurfaceRecoverySupportPolicy.Proved(true, count, complete, owned, false);
        }
        RaycastHit hit;
        bool cast;
        if (count == 0)
            cast = Physics.SphereCast(new Ray(position + up * .35f, -up), .35f, out hit, 2f, 15873, QueryTriggerInteraction.Ignore);
        else
            // A sphere cast starting in an existing contact can legitimately
            // miss. The downward ray starts outside the complete foot cut.
            cast = Physics.Raycast(new Ray(position + up * 2f, -up), out hit, 4f, 15873, QueryTriggerInteraction.Ignore);
        evidence.Observe("surface_support_downward_cast_hit", cast);
        evidence.Number("surface_support_overlap_branch", count > 0 ? 1 : 0);
        var normal = cast ? Vector3.Dot(hit.normal, up) : (float?)null;
        var height = cast ? Vector3.Dot(hit.point - position, up) : (float?)null;
        evidence.Number("surface_support_normal_dot_up", normal); evidence.Number("surface_support_height_from_actor", height);
        var supporting = cast && s.Scene.Surface.Count(t => ReferenceEquals(t, hit.collider)) == 1
            && normal >= .5f && height >= -2f && height <= .2f;
        evidence.Require("surface_support_owned_downward_plane", supporting);
        return NativeSurfaceRecoverySupportPolicy.Proved(false, count, complete, owned, supporting);
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
