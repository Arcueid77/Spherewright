using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerSameSessionSurfaceSourceTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StableSameSessionUnknownMoveBindsWithoutResumingOrChangingItsOutcome()
    {
        var refs = new References();
        var first = Capture(refs, 10, Now);
        var second = Capture(refs, 11, Now.AddMilliseconds(50));
        Assert.True(VoyagerSameSessionSurfaceSourcePolicy.TryBind(first, second, out var binding, out var error), error);
        Assert.NotNull(binding);
        Assert.False(binding!.GrantsResume);
        Assert.False(binding.OriginalOutcomeChanged);
        Assert.Equal(second.OriginalMove!.ActionId, binding.OriginalActionId);
        Assert.Equal(second.Session!.GameTick, binding.SourceGameTick);
        var detached = binding.Target; detached.X = 1;
        Assert.Equal(second.Target!.X, binding.Target.X);
        Assert.True(binding.MatchesFresh(Capture(refs, 12, Now.AddMilliseconds(100)), out error), error);
        Assert.Equal(NormalActionStates.OutcomeUnknown, second.OriginalMove.State);
        Assert.False(second.OriginalMove.ReconciledFromOutcomeUnknown);
    }

    [Theory]
    [InlineData("missing_scene")] [InlineData("changed_transform")] [InlineData("durable_unproved")]
    [InlineData("not_paused")] [InlineData("other_active")]
    [InlineData("other_blocker")] [InlineData("healthy")] [InlineData("other_quarantine")]
    [InlineData("other_session")] [InlineData("other_planet")] [InlineData("known_failure")]
    [InlineData("wrong_kind")] [InlineData("wrong_failure")] [InlineData("already_reconciled")]
    [InlineData("missing_original")] [InlineData("missing_key")] [InlineData("action_from_future")]
    [InlineData("missing_failure")] [InlineData("changed_failure")] [InlineData("not_peaceful")]
    [InlineData("moving")] [InlineData("space_transit")] [InlineData("controller_motion")] [InlineData("rigid_motion")]
    [InlineData("queued_order")] [InlineData("no_collider_proof")] [InlineData("input")]
    [InlineData("unknown_terrain")] [InlineData("wet_target")] [InlineData("far_target")]
    [InlineData("invalid_target")] [InlineData("bad_player_hash")] [InlineData("active_drone")]
    [InlineData("missing_energy")] [InlineData("missing_independent_debits")] [InlineData("unfunded")] [InlineData("lost_warper")]
    [InlineData("mixed_saved_source")] [InlineData("original_before_landing")] [InlineData("unknown_hash_version")]
    [InlineData("journey_changed")] [InlineData("expired_wall")]
    public void UnprovedSourceNeverProducesARecoveryBinding(string fault)
    {
        var refs = new References(); var first = Capture(refs, 10, Now); var second = Capture(refs, 11, Now.AddMilliseconds(50));
        var p = second.PlayerState!; var s = second.Session!; var a = second.OriginalMove!; var j = second.Funding!.OriginalJourney!;
        switch (fault)
        {
            case "missing_scene": second.SceneAndColliderOwnershipProved = false; break;
            case "changed_transform": second.NativeTransformStillMatches = false; break;
            case "durable_unproved": second.DurableEvidenceVerified = false; break;
            case "not_paused": second.NativePaused = false; break;
            case "other_active": second.NoOtherActiveActions = false; break;
            case "other_blocker": s.WriteBlockers.Add(new() { Code = "another-blocker", Message = "different" }); break;
            case "healthy": s.WriteHealth = WriteHealthStates.Healthy; break;
            case "other_quarantine": s.WriteQuarantineActionId = Guid.NewGuid().ToString(); break;
            case "other_session": a.SessionId = "another-session"; break;
            case "other_planet": a.PlanetId++; break;
            case "known_failure": a.State = NormalActionStates.ActionFailed; break;
            case "wrong_kind": a.ActionKind = NormalActionKinds.Build; break;
            case "wrong_failure": a.FailureKind = "different-failure"; break;
            case "already_reconciled": a.ReconciledFromOutcomeUnknown = true; break;
            case "missing_original": second.OriginalActionRecord = null; break;
            case "missing_key": a.IdempotencyKey = ""; break;
            case "action_from_future": a.CompletedAtGameTick = s.GameTick + 1; break;
            case "missing_failure": a.NativeFirstFailure = null; break;
            case "changed_failure": a.NativeFirstFailure!.Observations["original_order_absent"] = "false"; break;
            case "not_peaceful": s.PeacefulMode = PeacefulModeStates.ConfirmedCombat; break;
            case "moving": p.Speed = 1; p.StateHash = CanonicalStateHash.PlayerAction(p); break;
            case "space_transit": p.SpaceTransit = new(); p.StateHash = CanonicalStateHash.PlayerAction(p); break;
            case "controller_motion": p.NativePhysics!.Velocity!.X = 1; break;
            case "rigid_motion": second.RigidbodyVelocity!.Y = 1; break;
            case "queued_order": p.NativePhysics!.QueuedOrderCount = 1; break;
            case "no_collider_proof": p.NativePhysics!.CollidersComplete = false; break;
            case "input": second.NativeInputIdle = false; break;
            case "unknown_terrain": second.CurrentTerrainSupportProved = false; break;
            case "wet_target": second.DryLandingPatchProved = false; break;
            case "far_target": second.Target!.Y = 17; break;
            case "invalid_target": second.Target!.X = float.NaN; break;
            case "bad_player_hash": p.StateHash = "sha256:changed"; break;
            case "active_drone": p.ConstructionDrones.Working = 1; p.StateHash = CanonicalStateHash.PlayerAction(p); break;
            case "missing_energy": p.NativeLocalControlEnergy!.CostInputsProved = false; break;
            case "missing_independent_debits": p.NativeLocalControlEnergy!.ShieldRechargeEnergyPerNativeTickUpperJoules = null; break;
            case "unfunded": j.AvailableLoadedEnergyJoules = 1; break;
            case "lost_warper": j.NativeWarperCount = 0; break;
            case "mixed_saved_source": j.SavedLandingSourceBindingProved = true; j.SavedLandingAccountingAnchorGameTick = 100; break;
            case "original_before_landing": j.LastLandedGameTick = a.StartedAtGameTick + 1; break;
            case "unknown_hash_version": p.StateHashVersion = 2; break;
            case "journey_changed": second.ExpectedJourneyId = "another-journey"; break;
            case "expired_wall": j.JourneyDeadlineUtc = second.CapturedAtUtc.AddSeconds(30); break;
        }
        Assert.False(VoyagerSameSessionSurfaceSourcePolicy.TryBind(first, second, out var binding, out var error));
        Assert.Null(binding); Assert.NotEmpty(error);
        if (fault == "expired_wall") Assert.Equal("same_session_surface_original_wall_reserve_unproved", error);
    }

    [Theory]
    [InlineData("same_frame")] [InlineData("reversed_time")] [InlineData("late_second")]
    [InlineData("replacement_world")] [InlineData("replacement_action")] [InlineData("different_target")]
    [InlineData("original_failure_evidence_changed")] [InlineData("different_energy_breakdown")]
    [InlineData("different_journey_snapshot")] [InlineData("different_durable_evidence")]
    [InlineData("different_position")] [InlineData("subcentimetre_position")] [InlineData("small_energy_change")]
    [InlineData("different_funding")]
    public void TwoFramesMustBindTheSameFrozenSourceAndOriginalBudget(string fault)
    {
        var refs = new References(); var first = Capture(refs, 10, Now); var second = Capture(refs, 11, Now.AddMilliseconds(50));
        switch (fault)
        {
            case "same_frame": second.RenderFrame = first.RenderFrame; break;
            case "reversed_time": second.CapturedAtUtc = Now.AddMilliseconds(-1); second.Funding!.NowUtc = second.Funding.OriginalJourney!.NowUtc = second.CapturedAtUtc; break;
            case "late_second": second.CapturedAtUtc = Now.AddSeconds(3); second.Funding!.NowUtc = second.Funding.OriginalJourney!.NowUtc = second.CapturedAtUtc; break;
            case "replacement_world": second.World = new object(); break;
            case "replacement_action": second.OriginalActionRecord = new object(); break;
            case "original_failure_evidence_changed": second.OriginalMove!.NativeFirstFailure!.Values["earlier_speed_mps"] = 1.5; break;
            case "different_energy_breakdown": second.PlayerState!.NativeLocalControlEnergy!.LaserRechargeEnergyPerNativeTickUpperJoules = 101; break;
            case "different_journey_snapshot": second.JourneySnapshotFingerprint = "sha256:" + new string('c', 64); break;
            case "different_durable_evidence": second.DurableEvidenceFingerprint = "sha256:" + new string('d', 64); break;
            case "different_target": second.Target!.Y = 11; break;
            case "different_position": second.PlayerState!.Position.X = 200.1f; second.PlayerState.StateHash = CanonicalStateHash.PlayerAction(second.PlayerState); break;
            case "subcentimetre_position": second.PlayerState!.Position.X = 200.001f; second.PlayerState.StateHash = CanonicalStateHash.PlayerAction(second.PlayerState);
                Assert.Equal(first.PlayerState!.StateHash, second.PlayerState.StateHash); break;
            case "small_energy_change": second.PlayerState!.CoreEnergy += .1; second.Funding!.CoreEnergyJoules = second.PlayerState.CoreEnergy;
                second.PlayerState.StateHash = CanonicalStateHash.PlayerAction(second.PlayerState); Assert.Equal(first.PlayerState!.StateHash, second.PlayerState.StateHash); break;
            case "different_funding": second.Funding!.OriginalJourney!.AvailableLoadedEnergyJoules += 1; break;
        }
        Assert.False(VoyagerSameSessionSurfaceSourcePolicy.TryBind(first, second, out var binding, out _));
        Assert.Null(binding);
    }

    [Fact]
    public void FreshCommitCheckRejectsExpiredOrChangedEvidenceAndNeverRevivesTheOriginal()
    {
        var refs = new References(); var first = Capture(refs, 10, Now); var second = Capture(refs, 11, Now.AddMilliseconds(50));
        Assert.True(VoyagerSameSessionSurfaceSourcePolicy.TryBind(first, second, out var binding, out _));
        var late = Capture(refs, 12, Now.AddSeconds(3));
        Assert.False(binding!.MatchesFresh(late, out var rejection));
        Assert.Equal("same_session_surface_capture_expired", rejection);
        var changed = Capture(refs, 12, Now.AddMilliseconds(100)); changed.OriginalActionRecord = new object();
        Assert.False(binding.MatchesFresh(changed, out rejection));
        Assert.Equal("same_session_surface_native_reference_changed", rejection);
        Assert.False(binding.GrantsResume); Assert.False(binding.OriginalOutcomeChanged);
    }

    [Fact]
    public void ObservationNeedsTwoDistinctFrozenFramesAndNeverGrantsResume()
    {
        var refs = new References(); var observer = new VoyagerSameSessionSurfaceObservation();
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.AwaitingSecondFrame, observer.Observe(Capture(refs, 10, Now)));
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.AwaitingSecondFrame, observer.Observe(Capture(refs, 10, Now.AddMilliseconds(50))));
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.Bound, observer.Observe(Capture(refs, 11, Now.AddMilliseconds(100))));
        Assert.NotNull(observer.Binding); Assert.False(observer.Binding!.GrantsResume); Assert.False(observer.Binding.OriginalOutcomeChanged);
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.Rejected, observer.Expire(Now.AddSeconds(3)));
        Assert.Null(observer.Binding); Assert.Equal("same_session_surface_observation_expired", observer.Rejection);
    }

    [Theory]
    [InlineData("source_changed")] [InlineData("same_frame_changed")] [InlineData("wrong_frame_order")]
    [InlineData("expired")] [InlineData("unpaused")]
    public void ObservationLatchesItsFirstRefusalAndCannotTurnItIntoARecoveryGrant(string fault)
    {
        var refs = new References(); var observer = new VoyagerSameSessionSurfaceObservation(); var first = Capture(refs, 10, Now);
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.AwaitingSecondFrame, observer.Observe(first));
        var second = Capture(refs, 11, Now.AddMilliseconds(100));
        switch (fault)
        {
            case "source_changed": first.Target!.Y = 11; break;
            case "same_frame_changed": second.RenderFrame = 10; second.Target!.Y = 11; break;
            case "wrong_frame_order": second.RenderFrame = 9; break;
            case "expired": second.CapturedAtUtc = Now.AddSeconds(3); second.Funding!.NowUtc = second.Funding.OriginalJourney!.NowUtc = second.CapturedAtUtc; break;
            case "unpaused": second.NativePaused = false; break;
        }
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.Rejected, observer.Observe(second));
        var firstRejection = observer.Rejection;
        Assert.NotNull(firstRejection); Assert.Null(observer.Binding);
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.Rejected, observer.Observe(Capture(refs, 12, Now.AddMilliseconds(200))));
        Assert.Equal(firstRejection, observer.Rejection);
    }

    [Fact]
    public void NativeObservationLossInvalidatesEvenAnAlreadyBoundPreview()
    {
        var refs = new References(); var observer = new VoyagerSameSessionSurfaceObservation();
        observer.Observe(Capture(refs, 10, Now)); observer.Observe(Capture(refs, 11, Now.AddMilliseconds(100)));
        Assert.NotNull(observer.Binding);
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.Rejected, observer.Invalidate("native_scene_lost"));
        Assert.Null(observer.Binding); Assert.Equal("native_scene_lost", observer.Rejection);
        Assert.Equal(VoyagerSameSessionSurfaceObservationState.Rejected, observer.Invalidate("different_failure"));
        Assert.Equal("native_scene_lost", observer.Rejection);
    }

    internal sealed class References
    {
        public object World { get; } = new(); public object Player { get; } = new();
        public object Controller { get; } = new(); public object Rigidbody { get; } = new();
        public object Factory { get; } = new(); public object Scene { get; } = new();
        public object Transform { get; } = new(); public object Journey { get; } = new();
        public object Original { get; } = new();
        public string ActionId { get; } = Guid.NewGuid().ToString(); public string Key { get; } = Guid.NewGuid().ToString();
        public string JourneyId { get; } = Guid.NewGuid().ToString();
        public DateTimeOffset DeadlineUtc { get; set; } = Now.AddHours(2);
    }

    internal static VoyagerSameSessionSurfaceSourceFacts Capture(References r, int frame, DateTimeOffset now)
    {
        const string sessionId = "synthetic-session", reason = "retained synthetic move failure";
        var session = new SessionState
        {
            SessionId = sessionId, SaveName = "synthetic-owned-world", GameVersion = "synthetic-native", LocalPlanetId = 7,
            GameLoaded = true, OwnedBySpherewright = true, WritesAllowed = false, WriteHealth = WriteHealthStates.Quarantined,
            PeacefulMode = PeacefulModeStates.ConfirmedPeaceful, SandboxMode = SandboxModeStates.ConfirmedDisabled, ResourceMultiplier = 1,
            WriteQuarantineActionId = r.ActionId, GameTick = 200, Revision = 4,
            WriteBlockers = new() { new() { Code = BridgeErrorCodes.WriteSubsystemQuarantined, Message = reason } },
        };
        var action = new ActionResultSnapshot
        {
            ActionId = r.ActionId, IdempotencyKey = r.Key, ActionKind = NormalActionKinds.Move,
            State = NormalActionStates.OutcomeUnknown, Terminal = true, Succeeded = false,
            SessionId = sessionId, PlanetId = 7, StartedAtGameTick = 180, CompletedAtGameTick = 199,
            BeforeStateHash = "sha256:" + new string('a', 64), FailureKind = "voyager_local_native_tail_unproved", Message = reason,
            NativeFirstFailure = new() { Code = "voyager_local_native_tail_unproved", Stage = "original_move_stop_pre_player",
                FactsState = "observed", FirstFailedPredicate = "strict_ground_sphere", CapturedAtGameTick = 199,
                CapturedAtUtc = Now, Predicates = new() { ["strict_ground_sphere"] = "false" },
                Observations = new() { ["original_order_absent"] = "true" }, Values = new() { ["earlier_speed_mps"] = 1.0 } },
        };
        var player = new PlayerStateSnapshot
        {
            SessionId = sessionId, PlanetId = 7, CapturedAtGameTick = 200, Position = new() { X = 200 },
            IsAlive = true, IsOnPlanet = true, MovementState = "Drift", Speed = 0, CoreEnergy = 1000000,
            ConstructionDrones = new() { Total = 3, Alive = 3, Idle = 3, ActiveObservationsComplete = true },
            NativeLocalControlEnergy = new() { CostInputsProved = true, IndependentDebitsBudgeted = true, PerNativeTickEnergyUpperJoules = 1000,
                IndependentEnergyPerNativeTickUpperJoules = 300, ShieldRechargeEnergyPerNativeTickUpperJoules = 200,
                LaserRechargeEnergyPerNativeTickUpperJoules = 100 },
            NativePhysics = new()
            {
                State = "observed", SimulationMode = "FixedUpdate", FixedDeltaTimeSeconds = 1f / 60f, AutoSimulation = true, AutoSyncTransforms = true,
                GroundContact = new() { State = "observed", CapturedAtGameTick = 200, CheckSphereHit = false, QueriesHitTriggers = false },
                UseGravity = false, Constraints = "None", CollisionDetectionMode = "Discrete",
                SameDefaultPhysicsScene = true, CollidersComplete = true, CollidersTruncated = false,
                IsKinematic = false, DetectCollisions = true, ControllerMovementStateInFrame = "Drift",
                CurrentOrderPresent = false, QueuedOrderCount = 0, ControllerVerticalSpeed = 0,
                Velocity = new(), WalkRtsVelocity = new(), DriftRtsVelocity = new(), WalkManualVelocity = new(), DriftManualVelocity = new(),
            },
        };
        player.StateHash = CanonicalStateHash.PlayerAction(player);
        var journey = new VoyagerLocalActionBudgetFacts
        {
            ExactRemoteBinding = true, NativePlayerIdle = false, LastLandedGameTick = 100, CurrentGameTick = 200,
            SpentGameTicks = 100, MaximumGameTicks = 10000, ReturnReserveGameTicks = 3600,
            WorkGameTicks = 1, TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks, NowUtc = now,
            JourneyDeadlineUtc = r.DeadlineUtc, AvailableLoadedEnergyJoules = 100000000,
            LockedReturnFloorJoules = 1000000, NativeWarperCount = 2,
        };
        return new()
        {
            World = r.World, Player = r.Player, Controller = r.Controller, Rigidbody = r.Rigidbody, Factory = r.Factory,
            NativeSceneBinding = r.Scene, NativeTransformBinding = r.Transform, JourneyStore = r.Journey, OriginalActionRecord = r.Original,
            JourneyId = r.JourneyId, ExpectedJourneyId = r.JourneyId,
            JourneySnapshotFingerprint = "sha256:" + new string('a', 64), DurableEvidenceFingerprint = "sha256:" + new string('b', 64),
            Session = session, OriginalMove = action,
            PlayerState = player, RigidbodyVelocity = new(), RigidbodyAngularVelocity = new(), Target = new() { X = 200, Y = 10 },
            Funding = new() { GameTick = 200, NowUtc = now, CoreEnergyJoules = player.CoreEnergy,
                PerNativeTickEnergyJoules = 1000, OriginalJourney = journey },
            NativePaused = true, ExactNativeReferencesProved = true, SceneAndColliderOwnershipProved = true, NativeTransformStillMatches = true, DurableEvidenceVerified = true,
            CurrentTerrainSupportProved = true, DryLandingPatchProved = true, NoOtherActiveActions = true,
            NativeInputIdle = true, RenderFrame = frame, CapturedAtUtc = now,
        };
    }
}
