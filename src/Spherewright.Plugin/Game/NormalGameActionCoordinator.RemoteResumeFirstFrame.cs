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
        var playerState = new LoadedGroundSyncState(adoption.Player);
        if (!playerState.Finite) return "remote_resume_first_frame_nonfinite_state";
        var terrain = GameStateReader.CaptureNativeTerrain(adoption.Player, "load_preflight_before_first_frame");
        terrain.NativeGroundSphereHit = ground.CheckSphereHit;
        var blocker = GameStateReader.CaptureFirstFrameTerrainFootOverlap(adoption.Player, terrain, scene.Surface, out var registered);
        _sessions.RecordRemoteResumeTerrain(adoption, terrain, false);
        if (blocker is not null) return blocker;
        if (!playerState.Matches(adoption.Player) || !scene.MatchesCurrent()
            || !_sessions.ExactRemoteResumeLoadPreflight(adoption)) return "remote_resume_first_frame_query_binding_changed";
        var surfaceState = new FirstFrameSurfaceState(scene, registered);
        if (!surfaceState.MatchesBeforeUniverse()) return "remote_resume_registered_terrain_binding_unproved";
        blocker = adoption.FirstFrame.Arm(adoption.Evidence.SourceGameTick, true, terrain.GuideComplete == true,
            NativeRegisteredTerrainOwnerPolicy.PreflightBlocker(terrain) is null, funding is null,
            registered is null ? null : () => RecheckRegisteredTerrainAtPhysics(adoption));
        if (blocker is not null) return blocker;
        _firstFrameAdoption = adoption; _firstFramePlayerState = playerState;
        _firstFrameSurfaceState = surfaceState;
        // Start the existing finite charge accounting at the original source,
        // before allowing its one normal frame. No energy is debited or granted.
        adoption.ChargeStartedGameTick = adoption.Evidence.SourceGameTick;
        adoption.ChargeStartedUtc = DateTimeOffset.UtcNow;
        adoption.ChargeCostEnvelope = RemoteResumeChargeCostEnvelope(adoption.Player);
        adoption.ChargePerTickUpper = facts.MovementReserve!.PerNativeTickEnergyJoules;
        return null;
    }

    internal string? ObserveVerifiedRemoteResumeUniverseOnMainThread(VoyagerRemoteResumeAdoption adoption)
    {
        if (adoption.FirstFrame.Failure is not null || _sessions.ResumeAdoptionError is not null)
            return "remote_resume_failed_adoption_is_terminal";
        var journey = _flightCheckpoints.VoyagerJourney;
        var terrain = GameStateReader.CaptureNativeTerrain(adoption.Player, "native_universe_post151");
        NativePlayerGroundContactSnapshot? ground = null;
        var retainedTerrain = _firstFrameSurfaceState?.Matches() == true && NativeTerrainObservationPolicy.Blocker(terrain, 9) is null;
        if (_firstFrameSurfaceState?.HasRegisteredOwners == true)
        {
            ground = GameStateReader.CaptureNativeGroundContact(adoption.Player, "native_universe_post151");
            terrain.NativeGroundSphereHit = ground.CheckSphereHit;
            var foot = GameStateReader.CapturePostUniverseTerrainFootOverlap(adoption.Player, terrain, _firstFrameSurfaceState.Surface);
            retainedTerrain = _firstFrameSurfaceState.ObserveAfterUniverse(terrain) && retainedTerrain && foot is null;
        }
        _sessions.RecordRemoteResumeTerrain(adoption, terrain, true, ground);
        var bound = ReferenceEquals(_firstFrameAdoption, adoption) && _sessions.IsCurrentSessionOwned
            && _sessions.SessionId == adoption.SessionId && ReferenceEquals(GameMain.data, adoption.World)
            && ReferenceEquals(GameMain.mainPlayer, adoption.Player) && !GameMain.isPaused && GameMain.data.guideComplete
            && journey is not null && _flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence);
        var blocker = adoption.FirstFrame.ObserveUniverse(GameMain.gameTick, bound,
            _firstFramePlayerState?.MatchesAtTick(adoption.Player, adoption.Evidence.SourceGameTick + 1) == true,
            retainedTerrain);
        if (blocker is not null) return blocker;
        // Full contact/mode/cost/floor re-capture, but still no501 availability,
        // idle owner, lease release or Save. A failure still pays current601.
        return CheckVerifiedVoyagerRemoteResumeOnMainThread(adoption, admit: false);
    }

    private string? RecheckRegisteredTerrainAtPhysics(VoyagerRemoteResumeAdoption adoption)
    {
        string? blocker;
        try
        {
            if (adoption.FirstFrame.Failure is not null || _sessions.ResumeAdoptionError is not null
                || !ReferenceEquals(_firstFrameAdoption, adoption) || _firstFrameSurfaceState?.HasRegisteredOwners != true
                || !_sessions.IsCurrentSessionOwned || _sessions.SessionId != adoption.SessionId
                || !ReferenceEquals(GameMain.data, adoption.World) || !ReferenceEquals(GameMain.mainPlayer, adoption.Player)
                || GameMain.isPaused || GameMain.gameTick != adoption.Evidence.SourceGameTick + 1
                || !VoyagerLocalPrePlayerTickAvailable || !_flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence))
                blocker = "remote_resume_registered_terrain_physics_binding_unproved";
            else
            {
                var terrain = GameStateReader.CaptureNativeTerrain(adoption.Player, "native_physics_post501");
                var ground = GameStateReader.CaptureNativeGroundContact(adoption.Player, "native_physics_post501");
                terrain.NativeGroundSphereHit = ground.CheckSphereHit;
                blocker = GameStateReader.CapturePostUniverseTerrainFootOverlap(adoption.Player, terrain, _firstFrameSurfaceState.Surface);
                if (!_firstFrameSurfaceState.Matches() || !_firstFrameSurfaceState.ObserveAfterUniverse(terrain)
                    || _firstFramePlayerState?.MatchesAtTick(adoption.Player, adoption.Evidence.SourceGameTick + 1) != true)
                    blocker ??= "remote_resume_registered_terrain_physics_state_unproved";
            }
        }
        catch (Exception exception) { blocker = "remote_resume_registered_terrain_physics_capture_unavailable:" + exception.GetType().Name; }
        if (adoption.TerrainPostUniverse is { } observed)
        {
            observed.RegisteredPostPhysicsCapturedAtGameTick = GameMain.gameTick;
            observed.RegisteredPostPhysicsVerified = blocker is null;
            observed.RegisteredPostPhysicsReasonCode = blocker;
        }
        return blocker;
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
        public bool HasRegisteredOwners => _registered is not null;
        public FirstFrameSurfaceState(NativeLocalWalkSceneBinding scene, GameStateReader.RegisteredTerrainCapture? registered)
        {
            _scene = scene; _registered = registered; _meshes = scene.Surface.Select(c => ((MeshCollider)c).sharedMesh).ToArray();
            _positions = scene.Surface.Select(c => c.transform.position).ToArray();
            _rotations = scene.Surface.Select(c => c.transform.rotation).ToArray();
            _scales = scene.Surface.Select(c => c.transform.lossyScale).ToArray();
        }
        public bool Matches() => _scene.MatchesCurrent() && _scene.Surface.Select((c, i) => c is MeshCollider mesh
            && ReferenceEquals(mesh.sharedMesh, _meshes[i]) && c.transform.position.Equals(_positions[i])
            && c.transform.rotation.Equals(_rotations[i]) && c.transform.lossyScale.Equals(_scales[i])).All(v => v);
        public bool MatchesBeforeUniverse() => Matches() && (_registered is null || _registered.MatchesBeforeUniverse());
        public bool ObserveAfterUniverse(NativePlanetTerrainPhysicsSnapshot terrain) => _registered is not null
            && Matches() && _registered.ObserveAfterUniverse(terrain);
    }
}
