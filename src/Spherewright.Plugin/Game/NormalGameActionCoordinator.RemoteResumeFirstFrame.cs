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
        var blocker = GameStateReader.CaptureOwnedTerrainFootOverlap(adoption.Player, terrain, scene.Surface);
        _sessions.RecordRemoteResumeTerrain(adoption, terrain, false);
        if (blocker is not null) return blocker;
        if (!playerState.Matches(adoption.Player) || !scene.MatchesCurrent()
            || !_sessions.ExactRemoteResumeLoadPreflight(adoption)) return "remote_resume_first_frame_query_binding_changed";
        blocker = adoption.FirstFrame.Arm(adoption.Evidence.SourceGameTick, true, terrain.GuideComplete == true,
            NativeTerrainObservationPolicy.FootBlocker(terrain) is null, funding is null);
        if (blocker is not null) return blocker;
        _firstFrameAdoption = adoption; _firstFramePlayerState = playerState;
        _firstFrameSurfaceState = new FirstFrameSurfaceState(scene);
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
        _sessions.RecordRemoteResumeTerrain(adoption, terrain, true);
        var bound = ReferenceEquals(_firstFrameAdoption, adoption) && _sessions.IsCurrentSessionOwned
            && _sessions.SessionId == adoption.SessionId && ReferenceEquals(GameMain.data, adoption.World)
            && ReferenceEquals(GameMain.mainPlayer, adoption.Player) && !GameMain.isPaused && GameMain.data.guideComplete
            && journey is not null && _flightCheckpoints.RemoteResumeEvidenceStillMatches(adoption.Evidence);
        var blocker = adoption.FirstFrame.ObserveUniverse(GameMain.gameTick, bound,
            _firstFramePlayerState?.MatchesAtTick(adoption.Player, adoption.Evidence.SourceGameTick + 1) == true,
            _firstFrameSurfaceState?.Matches() == true && NativeTerrainObservationPolicy.Blocker(terrain, 9) is null);
        if (blocker is not null) return blocker;
        // Full contact/mode/cost/floor re-capture, but still no501 availability,
        // idle owner, lease release or Save. A failure still pays current601.
        return CheckVerifiedVoyagerRemoteResumeOnMainThread(adoption, admit: false);
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
        private readonly Mesh[] _meshes;
        private readonly Vector3[] _positions, _scales;
        private readonly Quaternion[] _rotations;
        public FirstFrameSurfaceState(NativeLocalWalkSceneBinding scene)
        {
            _scene = scene; _meshes = scene.Surface.Select(c => ((MeshCollider)c).sharedMesh).ToArray();
            _positions = scene.Surface.Select(c => c.transform.position).ToArray();
            _rotations = scene.Surface.Select(c => c.transform.rotation).ToArray();
            _scales = scene.Surface.Select(c => c.transform.lossyScale).ToArray();
        }
        public bool Matches() => _scene.MatchesCurrent() && _scene.Surface.Select((c, i) => c is MeshCollider mesh
            && ReferenceEquals(mesh.sharedMesh, _meshes[i]) && c.transform.position.Equals(_positions[i])
            && c.transform.rotation.Equals(_rotations[i]) && c.transform.lossyScale.Equals(_scales[i])).All(v => v);
    }
}
