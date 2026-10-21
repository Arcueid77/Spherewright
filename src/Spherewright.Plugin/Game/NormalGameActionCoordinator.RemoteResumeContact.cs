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
                ? player.CurrentBlockerAtNativePhysicsTick(adoption.Player, GameMain.gameTick) : "first_frame_player_state_missing";
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
        if (!atPhysics)
        {
            var failure = contact.Policy.ObserveUniverse(facts);
            return failure is null ? null : adoption.FirstFrame.Fail(failure, contact.Policy.FailureDetail);
        }
        var step = contact.Policy.Observe(facts);
        if (step == NativeResumeContactStep.Ready) adoption.NativeContactPending = false;
        return step == NativeResumeContactStep.Ready ? null
            : step == NativeResumeContactStep.Pending ? NativeResumeContactPolicy.GroundingPending
            : adoption.FirstFrame.Fail(contact.Policy.Failure ?? "remote_resume_contact_attempt_retired", contact.Policy.FailureDetail);
    }

}
