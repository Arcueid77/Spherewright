using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private VoyagerRemoteResumeAdoption? _firstFrameAdoption;
    private LoadedGroundSyncState? _firstFramePlayerState;
    private FirstFrameSurfaceState? _firstFrameSurfaceState;
    private RemoteResumeContactState? _firstFrameContact;
    private VoyagerRemoteResumeAdoption? _terrainInitializationAdoption;

    private string? ArmVerifiedRemoteFirstFrame(VoyagerRemoteResumeAdoption adoption, VoyagerRemoteIdleOwner owner,
        VoyagerJourneyRecord journey, NativeLocalWalkSceneBinding? scene, VoyagerRemoteIdleAdmissionFacts facts,
        NativePlayerGroundContactSnapshot? ground)
    {
        if (adoption.FirstFrame.Armed || adoption.FirstFrame.Failure is not null || adoption.Authorization is not { } authorization
            || !_sessions.ExactRemoteResumeLoadPreflight(adoption) || !VoyagerRemoteIdleBindingMatches(owner, journey)
            || scene is null || !scene.MatchesCurrent() || GameMain.isPaused || !GameMain.data.guideComplete
            || ground is not { State: "observed", CheckSphereHit: false } || ground.CapturedAtGameTick != adoption.Evidence.SourceGameTick)
            return "remote_resume_first_frame_exact_source_unproved";
        var funding = LoadedGroundSyncFunding(adoption, journey, facts, authorization);
        if (funding is not null) return funding;
        var playerState = LoadedGroundSyncState.FromNativeFrame(adoption.Player,
            GameMain.data.relativePos, GameMain.data.relativeRot);
        if (!playerState.Finite) return "remote_resume_first_frame_nonfinite_state";
        var terrain = GameStateReader.CaptureNativeTerrain(adoption.Player, "load_preflight_before_first_frame");
        terrain.NativeGroundSphereHit = ground.CheckSphereHit;
        var blocker = GameStateReader.CaptureFirstFrameTerrainFootOverlap(adoption.Player, terrain, scene.Surface, out var registered);
        _sessions.RecordRemoteResumeTerrain(adoption, terrain, false);
        var terrainInitialization = ReferenceEquals(_terrainInitializationAdoption, adoption)
            && BoundedSavedTerrainInitialization(adoption, terrain);
        if (blocker is not null && terrain.FootOverlapComplete == true && terrain.FootOverlapCount == 0
            && terrain.GuideComplete == true && NativeTerrainObservationPolicy.Blocker(terrain, 31) is null && !terrainInitialization)
            return ArmVerifiedRemoteNaturalContact(adoption, journey, scene, facts, playerState, terrain);
        if (blocker is not null && !terrainInitialization) return blocker;
        if (!playerState.Matches(adoption.Player) || !scene.MatchesCurrent()
            || !_sessions.ExactRemoteResumeLoadPreflight(adoption)) return "remote_resume_first_frame_query_binding_changed";
        var surfaceState = new FirstFrameSurfaceState(scene, registered);
        if (!surfaceState.MatchesBeforeUniverse()) return "remote_resume_registered_terrain_binding_unproved";
        blocker = terrainInitialization
            ? adoption.FirstFrame.ArmTerrainInitialization(adoption.Evidence.SourceGameTick, true, terrain.GuideComplete == true,
                true, funding is null, () => RecheckTerrainInitializationGround(adoption))
            : adoption.FirstFrame.Arm(adoption.Evidence.SourceGameTick, true, terrain.GuideComplete == true,
                NativeRegisteredTerrainOwnerPolicy.PreflightBlocker(terrain) is null, funding is null,
                registered is null ? null : () => RecheckRegisteredTerrainAtPhysics(adoption));
        if (blocker is not null) return blocker;
        _firstFrameAdoption = adoption; _firstFramePlayerState = playerState;
        _firstFrameContact = null;
        _firstFrameSurfaceState = surfaceState;
        // Start the existing finite charge accounting at the original source,
        // before allowing its one normal frame. No energy is debited or granted.
        adoption.ChargeStartedGameTick = adoption.Evidence.SourceGameTick;
        adoption.ChargeStartedUtc = adoption.Authorization?.InheritedWindowConfirmedAtUtc ?? DateTimeOffset.UtcNow;
        adoption.ChargeCostEnvelope = RemoteResumeChargeCostEnvelope(adoption.Player);
        adoption.ChargePerTickUpper = facts.MovementReserve!.PerNativeTickEnergyJoules;
        return null;
    }

    internal string? ObserveVerifiedRemoteResumeUniverseOnMainThread(VoyagerRemoteResumeAdoption adoption)
    {
        var evidence = BeginRemoteResumeDecision(adoption, "native_universe_post151");
        var code = EvaluateVerifiedRemoteResumeUniverseOnMainThread(adoption);
        return RetainRemoteResumeDecision(adoption, evidence, code);
    }

    private string? EvaluateVerifiedRemoteResumeUniverseOnMainThread(VoyagerRemoteResumeAdoption adoption)
    {
        if (adoption.FirstFrame.Failure is not null || _sessions.ResumeAdoptionError is not null)
            return "remote_resume_failed_adoption_is_terminal";
        // Contact attempts keep a real151 check on every pending frame; the
        // original one-frame latch is neither repeated nor borrowed as proof.
        if (adoption.NativeContactPending && adoption.FirstFrame.PhysicsObserved
            && GameMain.gameTick > adoption.Evidence.SourceGameTick + 1)
            return CheckVerifiedVoyagerRemoteResumeOnMainThread(adoption, admit: false);
        var journey = _flightCheckpoints.VoyagerJourney;
        var terrain = GameStateReader.CaptureNativeTerrain(adoption.Player, "native_universe_post151");
        NativePlayerGroundContactSnapshot? ground = null;
        var terrainBlocker = _firstFrameSurfaceState is { } retainedSurface
            ? retainedSurface.CurrentBlocker() : "first_frame_surface_state_missing";
        terrainBlocker ??= NativeTerrainObservationPolicy.Blocker(terrain, 9);
        if (ReferenceEquals(_terrainInitializationAdoption, adoption))
        {
            ground = GameStateReader.CaptureNativeGroundContact(adoption.Player, "native_universe_post151");
            terrain.NativeGroundSphereHit = ground.CheckSphereHit;
            terrainBlocker ??= ground.State == "observed" && ground.CheckSphereHit == true
                ? null : "remote_resume_terrain_initialization_actual_ground_unproved";
        }
        if (_firstFrameSurfaceState?.HasRegisteredOwners == true)
        {
            ground = GameStateReader.CaptureNativeGroundContact(adoption.Player, "native_universe_post151");
            terrain.NativeGroundSphereHit = ground.CheckSphereHit;
            var foot = GameStateReader.CapturePostUniverseTerrainFootOverlap(adoption.Player, terrain, _firstFrameSurfaceState.Surface);
            var registeredObserved = _firstFrameSurfaceState.ObserveAfterUniverse(terrain);
            if (!registeredObserved) terrainBlocker ??= "first_frame_registered_surface_unproved";
            terrainBlocker ??= foot;
        }
        if (ReferenceEquals(_firstFrameAdoption, adoption) && _firstFrameContact is { } contact && _firstFrameSurfaceState is { } surface)
        {
            var supportBlocker = contact.Support.Recheck(adoption.Player, "native_universe_post151", surface.Surface, out var support);
            adoption.NearSupportPostUniverse = support.Copy(); adoption.NearSupportLatest = support.Copy();
            terrainBlocker ??= supportBlocker;
        }
        _sessions.RecordRemoteResumeTerrain(adoption, terrain, true, ground);
        var bindingBlocker = RemoteFirstFrameBindingBlocker(adoption, journey);
        var retainedPlayer = _firstFramePlayerState;
        VectorLF3 observedUniversalPosition = default;
        var playerBlocker = retainedPlayer is not null
            ? retainedPlayer.CurrentBlockerAtNativeUniverseTick(adoption.Player, adoption.Evidence.SourceGameTick + 1,
                out observedUniversalPosition)
            : "first_frame_player_state_missing";
        var blocker = adoption.FirstFrame.ObserveUniverse(GameMain.gameTick, bindingBlocker is null,
            playerBlocker is null, terrainBlocker is null, bindingBlocker, playerBlocker, terrainBlocker);
        if (blocker is not null) return blocker;
        if (retainedPlayer is null || !retainedPlayer.TryPinNativeUniversePosition(observedUniversalPosition))
            return adoption.FirstFrame.Fail("remote_resume_first_frame_universe_unproved", "player_native_universe_pin_unproved");
        // Full contact/mode/cost/floor re-capture, but still no501 availability,
        // idle owner, lease release or Save. A failure still pays current601.
        return CheckVerifiedVoyagerRemoteResumeOnMainThread(adoption, admit: false);
    }

    private string? RemoteFirstFrameBindingBlocker(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord? journey)
    {
        if (!ReferenceEquals(_firstFrameAdoption, adoption)) return "first_frame_adoption_changed";
        if (!_sessions.IsCurrentSessionOwned) return "first_frame_session_not_owned";
        if (_sessions.SessionId != adoption.SessionId) return "first_frame_session_changed";
        if (!ReferenceEquals(GameMain.data, adoption.World)) return "first_frame_world_changed";
        if (!ReferenceEquals(GameMain.mainPlayer, adoption.Player)) return "first_frame_player_changed";
        if (GameMain.isPaused) return "first_frame_native_paused";
        if (!GameMain.data.guideComplete) return "first_frame_guide_incomplete";
        if (journey is null) return "first_frame_journey_missing";
        return _flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence)
            ? null : "first_frame_evidence_changed";
    }

    private bool BoundedSavedTerrainInitialization(VoyagerRemoteResumeAdoption adoption, NativePlanetTerrainPhysicsSnapshot terrain)
    {
        var p=adoption.Player; var planet=GameMain.localPlanet; var e=adoption.CurrentDecisionEvidence;
        if (_savedConstructionTail is not { } tail || !ReferenceEquals(tail.Adoption,adoption)
            || !SavedConstructionTailMatches(p,tail.Build.StructuredConstruction!.JourneyId)
            || !(_sessions.ExactRemoteResumeLoadPreflight(adoption) || _sessions.ExactRemoteResumeInitializationHold(adoption))
            || NativeTerrainObservationPolicy.Blocker(terrain,31) is not null || terrain.GuideComplete!=true
            || terrain.NativeGroundSphereHit!=false || terrain.FootOverlapComplete!=true || terrain.FootOverlapCount!=0
            || planet?.data?.heightData is null || planet.data.modData is null || !ReferenceEquals(p.planetData,planet)
            || !(planet.scale>0f) || !(p.position.magnitude>=120f && p.position.magnitude<=6000f)) return false;
        var radius=planet.data.QueryModifiedHeight(p.position.normalized)*planet.scale;
        var gap=p.position.magnitude-radius;
        e?.Number("conditional_native_surface_radius_m",radius);e?.Number("conditional_player_surface_gap_m",gap);
        e?.Observe("conditional_terrain_is_physical_ground",false);
        return NativeFirstFrameAdmission.TerrainInitializationHeightIsBounded(p.position.magnitude,radius);
    }

    private string? RecheckTerrainInitializationGround(VoyagerRemoteResumeAdoption adoption)
    {
        var journey=_flightCheckpoints.VoyagerJourney;
        if (!ReferenceEquals(_terrainInitializationAdoption,adoption) || journey is null
            || RemoteFirstFrameBindingBlocker(adoption,journey) is not null || !VoyagerLocalPrePlayerTickAvailable
            || GameMain.gameTick!=adoption.Evidence.SourceGameTick+1 || _firstFrameSurfaceState is not { } surface
            || surface.CurrentBlocker() is not null) return "remote_resume_terrain_initialization_binding_unproved";
        var ground=GameStateReader.CaptureNativeGroundContact(adoption.Player,"native_physics_post501");
        var terrain=GameStateReader.CaptureNativeTerrain(adoption.Player,"native_physics_post501");
        terrain.NativeGroundSphereHit=ground.CheckSphereHit;
        var foot=GameStateReader.CapturePostUniverseTerrainFootOverlap(adoption.Player,terrain,surface.Surface);
        if (ground.State!="observed" || ground.CapturedAtGameTick!=GameMain.gameTick || ground.CheckSphereHit!=true)
            return "remote_resume_terrain_initialization_actual_ground_unproved";
        return foot;
    }

    private string? RecheckRegisteredTerrainAtPhysics(VoyagerRemoteResumeAdoption adoption)
    {
        string? blocker; string? detail = null;
        try
        {
            detail = RegisteredTerrainPhysicsBindingBlocker(adoption);
            if (detail is not null)
                blocker = "remote_resume_registered_terrain_physics_binding_unproved";
            else
            {
                var surface = _firstFrameSurfaceState!;
                var terrain = GameStateReader.CaptureNativeTerrain(adoption.Player, "native_physics_post501");
                var ground = GameStateReader.CaptureNativeGroundContact(adoption.Player, "native_physics_post501");
                terrain.NativeGroundSphereHit = ground.CheckSphereHit;
                blocker = GameStateReader.CapturePostUniverseTerrainFootOverlap(adoption.Player, terrain, surface.Surface);
                var stateBlocker = surface.CurrentBlocker();
                if (stateBlocker is null && !surface.ObserveAfterUniverse(terrain)) stateBlocker = "first_frame_registered_surface_unproved";
                if (stateBlocker is null) stateBlocker = _firstFramePlayerState is { } player
                    ? player.CurrentBlockerAtNativePhysicsTick(adoption.Player, adoption.Evidence.SourceGameTick + 1) : "first_frame_player_state_missing";
                // The earlier foot-query failure retains priority, even though
                // the original state/registered callbacks must still run.
                if (blocker is null && stateBlocker is not null)
                { blocker = "remote_resume_registered_terrain_physics_state_unproved"; detail = stateBlocker; }
            }
        }
        catch (Exception exception) { blocker = "remote_resume_registered_terrain_physics_capture_unavailable:" + exception.GetType().Name; }
        if (adoption.TerrainPostUniverse is { } observed)
        {
            observed.RegisteredPostPhysicsCapturedAtGameTick = GameMain.gameTick;
            observed.RegisteredPostPhysicsVerified = blocker is null;
            observed.RegisteredPostPhysicsReasonCode = blocker;
        }
        if (blocker is not null) adoption.FirstFrame.Fail(blocker, detail);
        return blocker;
    }

    private string? RegisteredTerrainPhysicsBindingBlocker(VoyagerRemoteResumeAdoption adoption)
    {
        if (adoption.FirstFrame.Failure is not null) return "first_frame_already_failed";
        if (_sessions.ResumeAdoptionError is not null) return "first_frame_adoption_already_failed";
        if (!ReferenceEquals(_firstFrameAdoption, adoption)) return "first_frame_adoption_changed";
        if (_firstFrameSurfaceState?.HasRegisteredOwners != true) return "first_frame_registered_state_missing";
        if (!_sessions.IsCurrentSessionOwned) return "first_frame_session_not_owned";
        if (_sessions.SessionId != adoption.SessionId) return "first_frame_session_changed";
        if (!ReferenceEquals(GameMain.data, adoption.World)) return "first_frame_world_changed";
        if (!ReferenceEquals(GameMain.mainPlayer, adoption.Player)) return "first_frame_player_changed";
        if (GameMain.isPaused) return "first_frame_native_paused";
        if (GameMain.gameTick != adoption.Evidence.SourceGameTick + 1) return "first_frame_physics_tick_changed";
        if (!VoyagerLocalPrePlayerTickAvailable) return "first_frame_native_501_unavailable";
        return _flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence)
            ? null : "first_frame_evidence_changed";
    }

    private static string RemoteResumeChargeCostEnvelope(Player p)
    {
        var m = p.mecha;
        return CanonicalStateHash.Combine("remote-charge-fixed-cost-envelope-v1", m.walkSpeed, m.walkPower,
            m.miningPower, m.replicatePower, m.researchPower, m.reactorPowerConsRatio, m.thrustPowerPerAcc, NativeLocalGravityUpperBound(p));
    }

    private sealed class FirstFrameSurfaceState
    {
        private readonly NativeLocalWalkSceneBinding _scene;
        private readonly GameStateReader.RegisteredTerrainCapture? _registered;
        private readonly Mesh[] _meshes;
        private readonly Vector3[] _positions, _scales;
        private readonly Quaternion[] _rotations;
        public Collider[] Surface => _scene.Surface;
        public NativeLocalWalkSceneBinding Scene => _scene;
        public bool HasRegisteredOwners => _registered is not null;
        public FirstFrameSurfaceState(NativeLocalWalkSceneBinding scene, GameStateReader.RegisteredTerrainCapture? registered)
        {
            _scene = scene; _registered = registered; _meshes = scene.Surface.Select(c => ((MeshCollider)c).sharedMesh).ToArray();
            _positions = scene.Surface.Select(c => c.transform.position).ToArray();
            _rotations = scene.Surface.Select(c => c.transform.rotation).ToArray();
            _scales = scene.Surface.Select(c => c.transform.lossyScale).ToArray();
        }
        public bool Matches() => CurrentBlocker() is null;
        public string? CurrentBlocker()
        {
            var blocker = _scene.CurrentBlocker();
            if (blocker is not null) return blocker;
            for (var i = 0; i < _scene.Surface.Length; i++)
            {
                var c = _scene.Surface[i];
                if (c is not MeshCollider mesh) return "first_frame_surface_not_mesh";
                if (!ReferenceEquals(mesh.sharedMesh, _meshes[i])) return "first_frame_shared_mesh_changed";
                if (!c.transform.position.Equals(_positions[i])) return "first_frame_surface_position_changed";
                if (!c.transform.rotation.Equals(_rotations[i])) return "first_frame_surface_rotation_changed";
                if (!c.transform.lossyScale.Equals(_scales[i])) return "first_frame_surface_scale_changed";
            }
            return null;
        }
        public bool MatchesBeforeUniverse() => Matches() && (_registered is null || _registered.MatchesBeforeUniverse());
        public bool ObserveAfterUniverse(NativePlanetTerrainPhysicsSnapshot terrain) => _registered is not null
            && Matches() && _registered.ObserveAfterUniverse(terrain);
    }
}
