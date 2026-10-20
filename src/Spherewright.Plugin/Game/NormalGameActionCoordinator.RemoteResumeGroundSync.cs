using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private string? LoadedGroundSyncFunding(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord journey,
        VoyagerRemoteIdleAdmissionFacts facts, VoyagerRecoveryAuthorization authorization)
    {
        // This is only first-frame funding, not idle admission. The contact
        // query failed, so this projection must retain NativePlayerIdle=false.
        var f = facts.JourneyBudget; var p = adoption.Player; var now = DateTimeOffset.UtcNow;
        var deadline = _sessions.EffectiveVoyagerDeadline(journey);
        if (f is null || !f.ExactRemoteBinding || f.NativePlayerIdle || !f.LastLandedGameTick.HasValue
            || f.CurrentGameTick != adoption.Evidence.SourceGameTick || f.CurrentGameTick != GameMain.gameTick
            || f.CurrentGameTick < f.LastLandedGameTick || f.SpentGameTicks < 0
            || f.MaximumGameTicks < 1 || f.ReturnReserveGameTicks < 3600 || f.ReturnReserveGameTicks > 108000
            || f.WorkGameTicks != 1 || f.TailGameTicks != VoyagerLocalWalkReservePolicy.StopGameTicks
            || f.OriginalExtraWarperReserve < 0 || f.OriginalExtraWarperReserve > 100
            || f.NowUtc == default || now < f.NowUtc || now - f.NowUtc > TimeSpan.FromSeconds(2)
            || f.JourneyDeadlineUtc != deadline || adoption.ChargeStartedGameTick.HasValue || adoption.ChargeStartedUtc.HasValue
            || facts.MovementReserve is not { Proved: true } reserve)
            return "remote_resume_ground_sync_original_budget_unproved";
        if (!f.NativeWarperCount.HasValue || f.NativeWarperCount < 1 + f.OriginalExtraWarperReserve)
            return "remote_resume_charge_original_warper_reserve_insufficient";
        var environment = RemoteResumeChargeEnvironmentBlocker(p);
        if (environment is not null) return environment;
        // No charge-start/envelope mutation, no idle=true surrogate, no grant reset.
        return VoyagerRecoveryWindowPolicy.ChargeBlocker(p.mecha.coreEnergy, p.mecha.coreEnergyCap,
            CalculateAvailableFlightEnergy(p.mecha), journey.ReturnEnergyFloorJoules,
            reserve.PerNativeTickEnergyJoules ?? double.NaN, journey.ReturnCoreEnergyMinimumJoules,
            journey.SpentGameTicks, GameMain.gameTick - journey.LastLandedGameTick!.Value,
            journey.MaximumGameTicks, journey.ReturnReserveGameTicks, 0, authorization.Proposal.MaximumChargeGameTicks,
            0d, authorization.Proposal.MaximumChargeWallSeconds, now, deadline);
    }

    private static string? RemoteResumeChargeEnvironmentBlocker(Player p)
    {
        var h = GameMain.history; var factory = p.factory; var module = p.mecha.constructionModule;
        if (p.controller.actionWalk.rtsVelocity.sqrMagnitude != 0f || p.controller.actionWalk.moveVelocity.sqrMagnitude != 0f
            || p.controller.actionDrift.rtsVelocity.sqrMagnitude != 0f || p.controller.actionDrift.moveVelocity.sqrMagnitude != 0f)
            return "remote_resume_charge_rts_or_manual_velocity_nonzero";
        if (h is null || h.currentTech != 0 || h.techQueue is null || h.techQueue.Length != 32 || h.techQueue.Any(id => id != 0))
            return "remote_resume_charge_global_research_active";
        if (factory is null || factory.entityCount != 0 || factory.entityCursor > 1
            || factory.prebuildCount != 0 || factory.prebuildCursor > 1
            || module.buildTargetTotalCount != 0 || module.repairTargetTotalCount != 0 || module.autoReconstructTargetTotalCount != 0
            || module.buildTargets is { } bt && (bt.Length > 256 || bt.Any(t => t.objectId != 0))
            || module.repairTargets is { } rt && (rt.Length > 256 || rt.Any(t => t.objectId != 0))
            || module.autoReconstructTargets is { } at && (at.Length > 256 || at.Any(t => t.objectId != 0)))
            return "remote_resume_charge_native_construction_not_empty";
        return null;
    }

    private sealed class LoadedGroundSyncState
    {
        private readonly Player _player;
        private readonly PlayerController _controller;
        private readonly Rigidbody _body;
        private readonly Transform _transform;
        private readonly Transform? _parent;
        private readonly Vector3 _position, _scale, _bodyVelocity, _angularVelocity, _controllerVelocity;
        private readonly Quaternion _rotation, _uRotation;
        private readonly Vector4 _input0, _input1;
        private readonly VectorLF3 _uPosition;
        private readonly long _tick;
        private readonly double _core, _reactor, _loaded;
        private readonly float _speed, _vertical, _warp;
        private readonly bool _warpCommand, _paused, _autoSync, _triggers;
        private readonly EMovementState _mode, _frameMode;
        public bool Finite { get; }
        public LoadedGroundSyncState(Player player)
        {
            _player = player; _controller = player.controller; _body = _controller.rigid; _transform = player.transform;
            _parent = _transform.parent; _position = player.position; _rotation = _transform.localRotation; _scale = _transform.lossyScale;
            _uPosition = player.uPosition; _uRotation = player.uRotation;
            _bodyVelocity = _body.velocity; _angularVelocity = _body.angularVelocity; _controllerVelocity = _controller.velocity;
            _input0 = _controller.input0; _input1 = _controller.input1;
            _tick = GameMain.gameTick; _core = player.mecha.coreEnergy; _reactor = player.mecha.reactorEnergy;
            _loaded = CalculateAvailableFlightEnergy(player.mecha); _speed = player.speed; _vertical = _controller.vertSpeed;
            _mode = player.movementState; _frameMode = _controller.movementStateInFrame;
            _warp = player.warpState; _warpCommand = player.warpCommand; _paused = GameMain.isPaused;
            _autoSync = Physics.autoSyncTransforms; _triggers = Physics.queriesHitTriggers;
            Finite = FiniteVector(_position) && FiniteVector(_scale) && _scale.x > 0f && _scale.y > 0f && _scale.z > 0f
                && FiniteRotation(_rotation) && FiniteRotation(_uRotation)
                && FiniteVector(_bodyVelocity) && FiniteVector(_angularVelocity) && FiniteVector(_controllerVelocity)
                && FiniteScalar(_input0.x) && FiniteScalar(_input0.y) && FiniteScalar(_input0.z) && FiniteScalar(_input0.w)
                && FiniteScalar(_input1.x) && FiniteScalar(_input1.y) && FiniteScalar(_input1.z) && FiniteScalar(_input1.w)
                && FiniteUniversal(_uPosition.x) && FiniteUniversal(_uPosition.y) && FiniteUniversal(_uPosition.z)
                && FiniteUniversal(_core) && _core >= 0d && FiniteUniversal(_reactor) && _reactor >= 0d
                && FiniteUniversal(_loaded) && _loaded >= 0d && FiniteScalar(_speed) && _speed >= 0f
                && FiniteScalar(_vertical) && FiniteScalar(_warp);
        }
        private static bool FiniteScalar(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static bool FiniteVector(Vector3 p) => FiniteScalar(p.x) && FiniteScalar(p.y) && FiniteScalar(p.z);
        private static bool FiniteUniversal(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static bool FiniteRotation(Quaternion q) => FiniteScalar(q.x) && FiniteScalar(q.y) && FiniteScalar(q.z) && FiniteScalar(q.w)
            && q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w is > .99f and < 1.01f;
        public bool Matches(Player p) => MatchesAtTick(p, _tick);
        public bool MatchesAtTick(Player p, long expectedTick) => Finite && ReferenceEquals(p, _player) && ReferenceEquals(p.controller, _controller)
            && ReferenceEquals(p.transform, _transform) && ReferenceEquals(_transform.parent, _parent)
            && ReferenceEquals(_controller.rigid, _body) && GameMain.gameTick == expectedTick
            && p.position.Equals(_position) && _transform.localRotation.Equals(_rotation) && _transform.lossyScale.Equals(_scale)
            && p.uPosition.x == _uPosition.x && p.uPosition.y == _uPosition.y && p.uPosition.z == _uPosition.z
            && p.uRotation.Equals(_uRotation) && _body.velocity.Equals(_bodyVelocity) && _body.angularVelocity.Equals(_angularVelocity)
            && _controller.velocity.Equals(_controllerVelocity) && _controller.input0.Equals(_input0) && _controller.input1.Equals(_input1)
            && p.currentOrder is null && p.orders.orderCount == 0 && LocalRawMovementInputIdle()
            && p.mecha.coreEnergy == _core && p.mecha.reactorEnergy == _reactor && CalculateAvailableFlightEnergy(p.mecha) == _loaded
            && p.speed == _speed && _controller.vertSpeed == _vertical && p.movementState == _mode && _controller.movementStateInFrame == _frameMode
            && p.warpState == _warp && p.warpCommand == _warpCommand && GameMain.isPaused == _paused
            && Physics.autoSyncTransforms == _autoSync && Physics.queriesHitTriggers == _triggers;
    }
}
