using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private sealed class RemoteResumeContactState
    {
        public NativeResumeContactPolicy Policy { get; }
        public GameStateReader.NativeNearGroundSupportBinding Support { get; }
        private readonly Player _player;
        private readonly PlayerController _controller;
        private readonly Mecha _mecha;
        private readonly Rigidbody _body;
        private readonly Transform _transform;
        private readonly Transform? _parent;
        private readonly Vector3 _position, _up;
        private readonly NativeContactTransformBinding? _transformScale;
        private readonly bool _autoSync, _triggers, _kinematic, _useGravity;
        private readonly RigidbodyConstraints _constraints;
        public RemoteResumeContactState(Player p, GameStateReader.NativeNearGroundSupportBinding support, long sourceTick)
        {
            _player = p; _controller = p.controller; _mecha = p.mecha; _body = p.controller.rigid;
            _transform = p.transform; _parent = p.transform.parent; _position = p.position; _up = p.position.normalized;
            _transformScale = NativeContactTransformBinding.Capture(_transform);
            _autoSync = Physics.autoSyncTransforms; _triggers = Physics.queriesHitTriggers;
            _kinematic = _body.isKinematic; _useGravity = _body.useGravity; _constraints = _body.constraints;
            Support = support; Policy = new NativeResumeContactPolicy(sourceTick, DateTimeOffset.UtcNow);
        }
        public NativeResumeContactFacts Facts(Player p, VoyagerRemoteIdleAdmissionFacts facts,
            NativeNearGroundSupportSnapshot support, bool exactBinding)
        {
            var displacement = p.position - _position;
            var inward = -Vector3.Dot(displacement, _up);
            var tangential = (displacement + _up * inward).magnitude;
            var references = ReferenceEquals(p, _player) && ReferenceEquals(p.controller, _controller) && ReferenceEquals(p.mecha, _mecha)
                && ReferenceEquals(p.transform, _transform) && ReferenceEquals(_transform.parent, _parent)
                && ReferenceEquals(_controller.rigid, _body) && _transformScale?.Matches(_transform) == true
                && Physics.autoSyncTransforms == _autoSync && Physics.queriesHitTriggers == _triggers
                && _body.isKinematic == _kinematic && _body.useGravity == _useGravity && _body.constraints == _constraints;
            var motion = Finite(p.position) && Finite(_controller.velocity) && Finite(_body.velocity) && Finite(_body.angularVelocity)
                && Finite(_controller.vertSpeed) && Math.Abs(_controller.vertSpeed) <= NativeResumeContactPolicy.MaximumSpeedMetersPerSecond
                && _controller.velocity.magnitude <= NativeResumeContactPolicy.MaximumSpeedMetersPerSecond
                && _body.velocity.magnitude <= NativeResumeContactPolicy.MaximumSpeedMetersPerSecond;
            return new NativeResumeContactFacts
            {
                CapturedAtGameTick = GameMain.gameTick,
                ExactRecoveryBinding = exactBinding && references,
                NonGroundNativePremisesProved = facts.MovementReserve is null ? null : facts.MovementReserve.Proved && motion,
                CurrentSupportReferenceProved = support.State != "observed" ? null :
                    (support.CapturedAtGameTick == GameMain.gameTick) & support.CurrentPoolReference & support.LiveObject & support.ColliderChain,
                FootQueryComplete = support.FootOverlapComplete, FootOverlapCount = support.FootOverlapCount,
                AllFootHitsOwned = support.AllFootHitsCurrentSupportOrTerrain,
                NativeGroundSphereHit = support.ActualGroundSphereHit,
                StrictPhysicalIdleProved = facts.JourneyBudget is null || support.ActualGroundSphereHit is null
                    ? null : facts.JourneyBudget.NativePlayerIdle && support.ActualGroundSphereHit == true,
                CastDistanceMeters = support.CastDistance, CastNormalDotUp = support.NormalDotUp,
                InwardDisplacementMeters = inward, TangentialDisplacementMeters = tangential, SpeedMetersPerSecond = p.speed,
                CoreEnergyJoules = facts.CoreEnergyJoules, PerNativeTickEnergyJoules = facts.MovementReserve?.PerNativeTickEnergyJoules,
                OriginalJourney = facts.JourneyBudget,ReturnOnlyBudget=facts.ReturnOnlyBudget, NowUtc = DateTimeOffset.UtcNow,
            };
        }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
