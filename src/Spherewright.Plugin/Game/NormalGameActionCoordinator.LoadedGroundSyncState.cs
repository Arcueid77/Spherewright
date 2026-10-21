using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
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
        private readonly VectorLF3? _nativeFirstFrameUPosition;
        private VectorLF3? _validatedUniverseCandidate, _pinnedUniversePosition;
        private readonly long _tick;
        private readonly double _core, _reactor, _loaded;
        private readonly float _speed, _vertical, _warp;
        private readonly bool _warpCommand, _paused, _autoSync, _triggers;
        private readonly EMovementState _mode, _frameMode;
        public bool Finite { get; }
        public LoadedGroundSyncState(Player player) : this(player, null, true) { }
        private LoadedGroundSyncState(Player player, VectorLF3? nativeFirstFrameUPosition, bool nativeFrameFinite)
        {
            _nativeFirstFrameUPosition = nativeFirstFrameUPosition;
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
            Finite = nativeFrameFinite && FiniteVector(_position) && FiniteVector(_scale) && _scale.x > 0f && _scale.y > 0f && _scale.z > 0f
                && FiniteRotation(_rotation) && FiniteRotation(_uRotation)
                && FiniteVector(_bodyVelocity) && FiniteVector(_angularVelocity) && FiniteVector(_controllerVelocity)
                && FiniteScalar(_input0.x) && FiniteScalar(_input0.y) && FiniteScalar(_input0.z) && FiniteScalar(_input0.w)
                && FiniteScalar(_input1.x) && FiniteScalar(_input1.y) && FiniteScalar(_input1.z) && FiniteScalar(_input1.w)
                && FiniteUniversal(_uPosition.x) && FiniteUniversal(_uPosition.y) && FiniteUniversal(_uPosition.z)
                && FiniteUniversal(_core) && _core >= 0d && FiniteUniversal(_reactor) && _reactor >= 0d
                && FiniteUniversal(_loaded) && _loaded >= 0d && FiniteScalar(_speed) && _speed >= 0f
                && FiniteScalar(_vertical) && FiniteScalar(_warp);
        }
        public static LoadedGroundSyncState FromNativeFrame(Player player, VectorLF3 relativePosition, Quaternion relativeRotation)
        {
            // DSP Import projects double uPosition into float local position.
            // Its ordinary LateUpdate re-expresses that unchanged position by
            // this exact formula. Freeze the ARM frame: Universe151 later
            // updates relativePos/relativeRot, which are not this endpoint.
            var projected = relativePosition + Maths.QRotateLF(relativeRotation, player.position);
            var finite = FiniteUniversal(relativePosition.x) && FiniteUniversal(relativePosition.y)
                && FiniteUniversal(relativePosition.z) && FiniteRotation(relativeRotation)
                && FiniteUniversal(projected.x) && FiniteUniversal(projected.y) && FiniteUniversal(projected.z);
            return new LoadedGroundSyncState(player, projected, finite);
        }
        private static bool FiniteScalar(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static bool FiniteVector(Vector3 p) => FiniteScalar(p.x) && FiniteScalar(p.y) && FiniteScalar(p.z);
        private static bool FiniteUniversal(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static bool FiniteRotation(Quaternion q) => FiniteScalar(q.x) && FiniteScalar(q.y) && FiniteScalar(q.z) && FiniteScalar(q.w)
            && q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w is > .99f and < 1.01f;
        public bool Matches(Player p) => CurrentBlockerAtTick(p, _tick) is null;
        public bool MatchesDuringNativeInitializationPause(Player p) => !_paused && CurrentBlocker(p, _tick, true, out _) is null;
        public bool MatchesAtTick(Player p, long expectedTick) => CurrentBlockerAtTick(p, expectedTick) is null;
        public string? CurrentBlockerAtTick(Player p, long expectedTick) => CurrentBlocker(p, expectedTick, _paused, out _);
        private bool NativeFirstFrameEdge(long expectedTick) => !_paused && _tick < long.MaxValue && expectedTick == _tick + 1;
        private static bool SameUniversalPosition(VectorLF3 a, VectorLF3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public string? CurrentBlockerAtNativeUniverseTick(Player p, long expectedTick, out VectorLF3 observed)
        {
            observed = default;
            _validatedUniverseCandidate = null;
            if (!NativeFirstFrameEdge(expectedTick) || !_nativeFirstFrameUPosition.HasValue)
                return "player_native_first_frame_source_unproved";
            if (_pinnedUniversePosition.HasValue) return "player_native_universe_already_pinned";
            var blocker = CurrentBlocker(p, expectedTick, false, out observed, allowNativeProjection: true);
            if (blocker is null) _validatedUniverseCandidate = observed;
            return blocker;
        }
        public bool TryPinNativeUniversePosition(VectorLF3 observed)
        {
            // The caller commits this only after the SAME real151 latch,
            // binding, terrain and retained-player checks all succeeded.
            if (_pinnedUniversePosition.HasValue || !_validatedUniverseCandidate.HasValue
                || !NativeFirstFrameEdge(GameMain.gameTick) || GameMain.isPaused
                || !SameUniversalPosition(observed, _validatedUniverseCandidate.Value)) return false;
            _pinnedUniversePosition = observed;
            _validatedUniverseCandidate = null;
            return true;
        }
        public string? CurrentBlockerAtNativePhysicsTick(Player p, long expectedTick)
        {
            if (!NativeFirstFrameEdge(expectedTick) || !_pinnedUniversePosition.HasValue)
                return "player_native_universe_position_unpinned";
            // Native151->501 has no permitted endpoint switch or rebase.
            return CurrentBlocker(p, expectedTick, false, out _, pinned: _pinnedUniversePosition);
        }
        private string? CurrentBlocker(Player p, long expectedTick, bool expectedPaused, out VectorLF3 observed,
            bool allowNativeProjection = false, VectorLF3? pinned = null)
        {
            observed = default;
            // This is the admission predicate itself. Never re-run an alternate
            // comparison after failure to guess which Native value changed.
            if (!Finite) return "player_initial_state_nonfinite";
            if (!ReferenceEquals(p, _player)) return "player_reference_changed";
            if (!ReferenceEquals(p.controller, _controller)) return "player_controller_changed";
            if (!ReferenceEquals(p.transform, _transform)) return "player_transform_changed";
            if (!ReferenceEquals(_transform.parent, _parent)) return "player_parent_changed";
            if (!ReferenceEquals(_controller.rigid, _body)) return "player_rigidbody_changed";
            if (GameMain.gameTick != expectedTick) return "player_tick_changed";
            if (!p.position.Equals(_position)) return "player_local_position_changed";
            if (!_transform.localRotation.Equals(_rotation)) return "player_local_rotation_changed";
            if (!_transform.lossyScale.Equals(_scale)) return "player_scale_changed";
            observed = p.uPosition;
            if (!SameUniversalPosition(observed, pinned ?? _uPosition)
                && !(allowNativeProjection && _nativeFirstFrameUPosition.HasValue
                    && SameUniversalPosition(observed, _nativeFirstFrameUPosition.Value)))
                return "player_universal_position_changed";
            if (!p.uRotation.Equals(_uRotation)) return "player_universal_rotation_changed";
            if (!_body.velocity.Equals(_bodyVelocity)) return "player_body_velocity_changed";
            if (!_body.angularVelocity.Equals(_angularVelocity)) return "player_body_angular_velocity_changed";
            if (!_controller.velocity.Equals(_controllerVelocity)) return "player_controller_velocity_changed";
            if (!_controller.input0.Equals(_input0)) return "player_input0_changed";
            if (!_controller.input1.Equals(_input1)) return "player_input1_changed";
            if (p.currentOrder is not null) return "player_current_order_present";
            if (p.orders.orderCount != 0) return "player_order_queue_present";
            if (!LocalRawMovementInputIdle()) return "player_raw_input_active";
            if (p.mecha.coreEnergy != _core) return "player_core_energy_changed";
            if (p.mecha.reactorEnergy != _reactor) return "player_reactor_energy_changed";
            if (CalculateAvailableFlightEnergy(p.mecha) != _loaded) return "player_loaded_energy_changed";
            if (p.speed != _speed) return "player_speed_changed";
            if (_controller.vertSpeed != _vertical) return "player_vertical_speed_changed";
            if (p.movementState != _mode) return "player_movement_state_changed";
            if (_controller.movementStateInFrame != _frameMode) return "player_frame_movement_state_changed";
            if (p.warpState != _warp) return "player_warp_state_changed";
            if (p.warpCommand != _warpCommand) return "player_warp_command_changed";
            if (GameMain.isPaused != expectedPaused) return "player_pause_state_changed";
            if (Physics.autoSyncTransforms != _autoSync) return "player_physics_auto_sync_changed";
            if (Physics.queriesHitTriggers != _triggers) return "player_physics_trigger_query_changed";
            return null;
        }
    }
}
