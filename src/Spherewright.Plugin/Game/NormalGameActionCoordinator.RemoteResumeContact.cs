using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private string? ArmVerifiedRemoteNaturalContact(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord journey,
        NativeLocalWalkSceneBinding scene, VoyagerRemoteIdleAdmissionFacts facts, LoadedGroundSyncState playerState,
        NativePlanetTerrainPhysicsSnapshot terrain)
    {
        // No terrain hit exists here. Establish the exact Native near-pool
        // owner instead; this never turns the cast into ground or idle.
        var blocker = GameStateReader.TryCaptureNativeNearSupport(adoption.Player, "load_preflight_before_first_frame",
            scene.Surface, out var support, out var binding);
        adoption.NearSupportPreFirstFrame = support.Copy(); adoption.NearSupportLatest = support.Copy();
        if ((blocker == "remote_resume_near_support_registered_collection_empty"
            || blocker == "remote_resume_near_support_collider_refresh_pending") && binding is not null)
            return HoldRemoteResumeForNativeInitialization(adoption, journey, new FirstFrameSurfaceState(scene, null),
                playerState, binding, facts);
        if (blocker is not null || binding is null) return blocker ?? "remote_resume_near_support_binding_unproved";
        var contact = new RemoteResumeContactState(adoption.Player, binding, adoption.Evidence.SourceGameTick);
        blocker = contact.Policy.PreflightBlocker(contact.Facts(adoption.Player, facts, support,
            ExactRemoteContactBinding(adoption, journey, scene)));
        if (blocker is not null) return blocker;
        if (!playerState.Matches(adoption.Player) || !scene.MatchesCurrent()
            || !_sessions.ExactRemoteResumeLoadPreflight(adoption)) return "remote_resume_near_support_query_binding_changed";
        var surface = new FirstFrameSurfaceState(scene, null);
        if (!surface.MatchesBeforeUniverse()) return "remote_resume_near_support_surface_changed";
        _firstFrameAdoption = adoption; _firstFramePlayerState = playerState;
        _firstFrameSurfaceState = surface; _firstFrameContact = contact;
        adoption.NativeContactPending = true;
        blocker = adoption.FirstFrame.ArmNearSupport(adoption.Evidence.SourceGameTick, true, terrain.GuideComplete == true,
            true, true, () => RecheckRemoteContactFirstPhysics(adoption));
        if (blocker is not null) return blocker;
        // Reuse the original charge accounting, including these contact ticks.
        // No actor, Rigidbody, energy, cached contact or save is written.
        adoption.ChargeStartedGameTick = adoption.Evidence.SourceGameTick;
        adoption.ChargeStartedUtc = contact.Policy.StartedUtc;
        adoption.ChargeCostEnvelope = RemoteResumeChargeCostEnvelope(adoption.Player);
        adoption.ChargePerTickUpper = facts.MovementReserve!.PerNativeTickEnergyJoules;
        return null;
    }

    private bool ExactRemoteContactBinding(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord journey,
        NativeLocalWalkSceneBinding scene) => RemoteContactBindingBlocker(adoption, journey, scene) is null;

    private string? RemoteContactBindingBlocker(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord journey,
        NativeLocalWalkSceneBinding scene)
    {
        if (!_sessions.IsCurrentSessionOwned) return "contact_session_not_owned";
        if (_sessions.SessionId != adoption.SessionId) return "contact_session_changed";
        if (!ReferenceEquals(GameMain.data, adoption.World)) return "contact_world_changed";
        if (!ReferenceEquals(GameMain.mainPlayer, adoption.Player)) return "contact_player_changed";
        if (GameMain.isPaused) return "contact_native_paused";
        if (!GameMain.data.guideComplete) return "contact_guide_incomplete";
        var blocker = scene.CurrentBlocker();
        if (blocker is not null) return blocker;
        if (journey.Phase != "remote_landed") return "contact_journey_phase_changed";
        return _flightCheckpoints.RemoteResumeRetainedJourneyStillMatches(journey, adoption.Evidence)
            ? null : "contact_journey_evidence_changed";
    }

    private string? RecheckRemoteContactFirstPhysics(VoyagerRemoteResumeAdoption adoption)
    {
        string? bindingBlocker;
        if (!ReferenceEquals(_firstFrameAdoption, adoption)) bindingBlocker = "contact_adoption_changed";
        else if (_firstFrameContact is null) bindingBlocker = "contact_state_missing";
        else if (_firstFrameSurfaceState is null) bindingBlocker = "contact_surface_state_missing";
        else if (_flightCheckpoints.VoyagerJourney is not { } journey) bindingBlocker = "contact_journey_missing";
        else
        {
            bindingBlocker = RemoteContactBindingBlocker(adoption, journey, _firstFrameSurfaceState.Scene);
            if (bindingBlocker is null && !VoyagerLocalPrePlayerTickAvailable) bindingBlocker = "contact_native_501_unavailable";
            if (bindingBlocker is null && GameMain.gameTick != adoption.Evidence.SourceGameTick + 1) bindingBlocker = "contact_first_physics_tick_changed";
            if (bindingBlocker is null) bindingBlocker = _firstFramePlayerState is { } player
                ? player.CurrentBlockerAtTick(adoption.Player, GameMain.gameTick) : "first_frame_player_state_missing";
            bindingBlocker ??= _firstFrameSurfaceState.CurrentBlocker();
        }
        if (bindingBlocker is not null)
            return adoption.FirstFrame.Fail("remote_resume_near_support_first_physics_binding_unproved", bindingBlocker);
        var surface = _firstFrameSurfaceState!; var contact = _firstFrameContact!;
        var terrain = GameStateReader.CaptureNativeTerrain(adoption.Player, "native_physics_post501");
        var blocker = NativeTerrainObservationPolicy.Blocker(terrain, 9);
        if (blocker is not null) return blocker;
        blocker = contact.Support.Recheck(adoption.Player, "native_physics_post501", surface.Surface, out var support);
        adoption.NearSupportLatest = support.Copy();
        return blocker;
    }

    private string? ObserveVerifiedRemoteNaturalContact(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord journey,
        VoyagerRemoteIdleAdmissionFacts? captured, NativePlayerGroundContactSnapshot? ground, bool atPhysics)
    {
        string? bindingBlocker;
        if (_firstFrameContact is null) bindingBlocker = "contact_state_missing";
        else if (_firstFrameSurfaceState is null) bindingBlocker = "contact_surface_state_missing";
        else if (captured is null) bindingBlocker = "contact_idle_facts_missing";
        else if (adoption.Authorization is null) bindingBlocker = "contact_authorization_missing";
        else if (!ReferenceEquals(_firstFrameAdoption, adoption)) bindingBlocker = "contact_adoption_changed";
        else bindingBlocker = RemoteContactBindingBlocker(adoption, journey, _firstFrameSurfaceState.Scene)
            ?? _firstFrameSurfaceState.CurrentBlocker();
        if (bindingBlocker is not null)
            return adoption.FirstFrame.Fail("remote_resume_contact_current_binding_unproved", bindingBlocker);
        var contact = _firstFrameContact!; var surface = _firstFrameSurfaceState!;
        var authorization = adoption.Authorization!; var currentFacts = captured!;
        var terrain = GameStateReader.CaptureNativeTerrain(adoption.Player, atPhysics ? "native_physics_post501" : "native_universe_post151");
        var blocker = NativeTerrainObservationPolicy.Blocker(terrain, 9);
        if (blocker is not null) return blocker;
        blocker = contact.Support.Recheck(adoption.Player, terrain.CaptureStage, surface.Surface, out var support);
        adoption.NearSupportLatest = support.Copy();
        if (blocker is not null) return blocker;
        if (ground is null || ground.CapturedAtGameTick != GameMain.gameTick || ground.CheckSphereHit != support.ActualGroundSphereHit)
            return "remote_resume_contact_ground_queries_changed";
        blocker = RemoteResumeChargeEnvironmentBlocker(adoption.Player);
        if (blocker is not null) return blocker;
        // Preserve the already-started Native charge cap and original floor;
        // this is a numeric check, never an idle=true projection.
        blocker = VoyagerRecoveryWindowPolicy.ChargeBlocker(adoption.Player.mecha.coreEnergy, adoption.Player.mecha.coreEnergyCap,
            CalculateAvailableFlightEnergy(adoption.Player.mecha), journey.ReturnEnergyFloorJoules,
            currentFacts.MovementReserve!.PerNativeTickEnergyJoules ?? double.NaN, journey.ReturnCoreEnergyMinimumJoules,
            journey.SpentGameTicks, GameMain.gameTick - journey.LastLandedGameTick!.Value, journey.MaximumGameTicks,
            journey.ReturnReserveGameTicks, GameMain.gameTick - adoption.ChargeStartedGameTick!.Value,
            authorization.Proposal.MaximumChargeGameTicks, (DateTimeOffset.UtcNow - adoption.ChargeStartedUtc!.Value).TotalSeconds,
            authorization.Proposal.MaximumChargeWallSeconds, DateTimeOffset.UtcNow, _sessions.EffectiveVoyagerDeadline(journey));
        if (blocker is not null) return blocker;
        var facts = contact.Facts(adoption.Player, currentFacts, support, true);
        if (!atPhysics) return contact.Policy.ObserveUniverse(facts);
        var step = contact.Policy.Observe(facts);
        if (step == NativeResumeContactStep.Ready) adoption.NativeContactPending = false;
        return step == NativeResumeContactStep.Ready ? null
            : step == NativeResumeContactStep.Pending ? NativeResumeContactPolicy.GroundingPending
            : contact.Policy.Failure ?? "remote_resume_contact_attempt_retired";
    }

    private sealed class RemoteResumeContactState
    {
        public NativeResumeContactPolicy Policy { get; }
        public GameStateReader.NativeNearGroundSupportBinding Support { get; }
        private readonly Player _player;
        private readonly PlayerController _controller;
        private readonly Mecha _mecha;
        private readonly Rigidbody _body;
        private readonly Transform _transform, _parent;
        private readonly Vector3 _position, _up, _scale;
        private readonly bool _autoSync, _triggers, _kinematic, _useGravity;
        private readonly RigidbodyConstraints _constraints;
        public RemoteResumeContactState(Player p, GameStateReader.NativeNearGroundSupportBinding support, long sourceTick)
        {
            _player = p; _controller = p.controller; _mecha = p.mecha; _body = p.controller.rigid;
            _transform = p.transform; _parent = p.transform.parent; _position = p.position; _up = p.position.normalized;
            _scale = p.transform.lossyScale; _autoSync = Physics.autoSyncTransforms; _triggers = Physics.queriesHitTriggers;
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
                && ReferenceEquals(_controller.rigid, _body) && _transform.lossyScale.Equals(_scale)
                && Physics.autoSyncTransforms == _autoSync && Physics.queriesHitTriggers == _triggers
                && _body.isKinematic == _kinematic && _body.useGravity == _useGravity && _body.constraints == _constraints;
            var motion = Finite(p.position) && Finite(_controller.velocity) && Finite(_body.velocity) && Finite(_body.angularVelocity)
                && Finite(_controller.vertSpeed) && Math.Abs(_controller.vertSpeed) <= NativeResumeContactPolicy.MaximumSpeedMetersPerSecond
                && _controller.velocity.magnitude <= NativeResumeContactPolicy.MaximumSpeedMetersPerSecond
                && _body.velocity.magnitude <= NativeResumeContactPolicy.MaximumSpeedMetersPerSecond;
            return new NativeResumeContactFacts
            {
                ExactRecoveryBinding = exactBinding && references,
                NonGroundNativePremisesProved = facts.MovementReserve is { Proved: true } && motion,
                CurrentSupportReferenceProved = support.State == "observed" && support.CapturedAtGameTick == GameMain.gameTick
                    && support.CurrentPoolReference == true && support.LiveObject == true && support.ColliderChain == true,
                FootQueryComplete = support.FootOverlapComplete == true, FootOverlapCount = support.FootOverlapCount ?? -1,
                AllFootHitsOwned = support.AllFootHitsCurrentSupportOrTerrain == true,
                NativeGroundSphereHit = support.ActualGroundSphereHit,
                StrictPhysicalIdleProved = facts.JourneyBudget?.NativePlayerIdle == true && support.ActualGroundSphereHit == true,
                CastDistanceMeters = support.CastDistance, CastNormalDotUp = support.NormalDotUp,
                InwardDisplacementMeters = inward, TangentialDisplacementMeters = tangential, SpeedMetersPerSecond = p.speed,
                CoreEnergyJoules = facts.CoreEnergyJoules, PerNativeTickEnergyJoules = facts.MovementReserve?.PerNativeTickEnergyJoules,
                OriginalJourney = facts.JourneyBudget, NowUtc = DateTimeOffset.UtcNow,
            };
        }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
