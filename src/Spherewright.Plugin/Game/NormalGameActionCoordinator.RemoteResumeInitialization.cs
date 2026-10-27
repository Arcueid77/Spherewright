using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private RemoteResumeInitializationHold? _remoteResumeInitializationHold;

    private string? HoldRemoteResumeForNativeInitialization(VoyagerRemoteResumeAdoption adoption,
        VoyagerJourneyRecord journey, FirstFrameSurfaceState surface, LoadedGroundSyncState state,
        GameStateReader.NativeNearGroundSupportBinding binding, VoyagerRemoteIdleAdmissionFacts facts)
    {
        if (_remoteResumeInitializationHold is { } previous && ReferenceEquals(previous.Adoption, adoption))
            return previous.Policy.FirstFailure ?? (previous.Policy.ResumeAuthorized
                ? "remote_resume_initialization_already_resumed" : NativeResumeInitializationPolicy.PendingCode);
        if (!_sessions.ExactRemoteResumeLoadPreflight(adoption) || !state.Matches(adoption.Player)
            || !surface.MatchesBeforeUniverse() || facts.MovementReserve?.PerNativeTickEnergyJoules is not { } perTick
            || !(perTick > 0d) || double.IsNaN(perTick) || double.IsInfinity(perTick))
            return "remote_resume_initialization_original_source_unproved";
        _remoteResumeInitializationHold?.Policy.Cancel("remote_resume_initialization_adoption_replaced");
        _remoteResumeInitializationHold = new RemoteResumeInitializationHold(adoption, journey, surface, state, binding, perTick);
        return NativeResumeInitializationPolicy.PendingCode;
    }

    internal string? ContinueVerifiedRemoteInitializationOnMainThread(VoyagerRemoteResumeAdoption adoption)
    {
        var evidence = BeginRemoteResumeDecision(adoption, "native_initialization_pause");
        var code = EvaluateVerifiedRemoteInitializationOnMainThread(adoption);
        return RetainRemoteResumeDecision(adoption, evidence, code);
    }

    private string? EvaluateVerifiedRemoteInitializationOnMainThread(VoyagerRemoteResumeAdoption adoption)
    {
        if (_remoteResumeInitializationHold is not { } hold || !ReferenceEquals(hold.Adoption, adoption))
            return "remote_resume_initialization_original_hold_unproved";
        var p = adoption.Player;
        var bound = _sessions.ExactRemoteResumeInitializationHold(adoption)
            && _flightCheckpoints.RemoteResumeRetainedJourneyStillMatches(hold.Journey, adoption.Evidence)
            && hold.Surface.MatchesBeforeUniverse();
        var collectionBound = hold.Support.TryObserveInitialization(p, hold.Surface.Surface, out var count,
            out var colliderRefreshPending);
        var facts = new NativeResumeInitializationFacts
        {
            CurrentGameTick = GameMain.gameTick, NowUtc = DateTimeOffset.UtcNow,
            ExactSourceWorldBinding = bound && collectionBound,
            NativePaused = GameMain.isPaused,
            NativeStateUnchanged = hold.State.MatchesDuringNativeInitializationPause(p) && hold.MaterialsMatch(p)
                && hold.CostEnvelope == RemoteResumeChargeCostEnvelope(p) && VoyagerMechaWorkIdle(p)
                && RemoteResumeChargeEnvironmentBlocker(p, hold.Journey.JourneyId, adoption.CurrentDecisionEvidence) is null && !GameMain.data.disableController
                && !GameMain.data.guideRunning && BuildUiIsIdle(p),
            RegisteredCount = count,
            ColliderRefreshPending = colliderRefreshPending,
        };
        if (facts.NativeStateUnchanged && InitializationFundingBlocker(hold) is not null)
            facts.NativeStateUnchanged = false;
        var evidence = adoption.CurrentDecisionEvidence!;
        evidence.Observe("initialization_binding", facts.ExactSourceWorldBinding);
        evidence.Observe("initialization_state_unchanged", facts.NativeStateUnchanged);
        evidence.Observe("initialization_native_pause", facts.NativePaused);
        evidence.Observe("collider_refresh_pending", facts.ColliderRefreshPending);
        evidence.Number("registered_colliders", facts.RegisteredCount);
        evidence.Number("host_observations_used_before_sample", hold.Policy.ObservationCount);
        evidence.Number("host_observation_limit", NativeResumeInitializationPolicy.MaximumHostPumpObservations);
        evidence.Number("initialization_wall_seconds", (facts.NowUtc - hold.Policy.StartedUtc).TotalSeconds);
        var step = hold.Policy.Observe(facts);
        var snapshot = new NativeNearGroundSupportSnapshot
        {
            CaptureStage = "native_initialization_pause", CapturedAtGameTick = GameMain.gameTick,
            RegisteredColliderCount = count, ReasonCode = hold.Policy.FirstFailure ?? NativeResumeInitializationPolicy.PendingCode,
            Identity = new NativeNearGroundSupportIdentitySnapshot { PlanetColliderDirty = colliderRefreshPending },
        };
        adoption.NearSupportLatest = snapshot;
        if (step == NativeResumeInitializationStep.Rejected) return hold.Policy.FirstFailure;
        if (step == NativeResumeInitializationStep.Pending) return NativeResumeInitializationPolicy.PendingCode;

        // Ordinary Native Update/LateUpdate have settled registration and its
        // deferred refresh while paused at the original tick. Only now make
        // the first exact pool/object/chain binding; this is never a rebind.
        var ground = GameStateReader.CaptureNativeGroundContact(p, "native_initialization_pause_pre_resume");
        var blocker = hold.Support.Capture(p, "native_initialization_pause_pre_resume", hold.Surface.Surface, true, out snapshot);
        adoption.NearSupportLatest = snapshot.Copy();
        facts.NowUtc = DateTimeOffset.UtcNow;
        facts.ExactSourceWorldBinding = bound && _sessions.ExactRemoteResumeInitializationHold(adoption)
            && hold.Support.TryObserveInitialization(p, hold.Surface.Surface, out count, out colliderRefreshPending);
        facts.RegisteredCount = count;
        facts.ColliderRefreshPending = colliderRefreshPending;
        facts.NativeStateUnchanged &= hold.State.MatchesDuringNativeInitializationPause(p) && hold.MaterialsMatch(p);
        facts.CurrentSupportOrGroundProof = blocker is null || (ground.State == "observed"
            && ground.CapturedAtGameTick == GameMain.gameTick && ground.CheckSphereHit == true
            && blocker == "remote_resume_near_support_initial_sphere_already_grounded");
        evidence.Observe("support_or_ground_proved", facts.CurrentSupportOrGroundProof);
        evidence.Observe("final_collider_refresh_pending", facts.ColliderRefreshPending);
        if (!facts.CurrentSupportOrGroundProof)
        {
            hold.Policy.Cancel(blocker ?? "remote_resume_initialization_current_support_unproved");
            return hold.Policy.FirstFailure;
        }
        // This consumes only the initialization Resume permission. The session
        // tracker immediately re-runs full preflight; no idle owner is published.
        return hold.Policy.TryAuthorizeResume(facts) ? null : hold.Policy.FirstFailure;
    }

    private string? InitializationFundingBlocker(RemoteResumeInitializationHold hold)
    {
        var adoption = hold.Adoption; var p = adoption.Player; var journey = hold.Journey;
        if (adoption.Authorization is not { } authorization || journey.LastLandedGameTick is null
            || CountVoyagerWarpers(p.mecha.warpStorage) < 1 + journey.ExtraWarperReserve)
            return "remote_resume_initialization_original_reserve_unproved";
        if(adoption.Evidence.ReturnOnlyContinuation)return ReturnOnlyNativeFundingBlocker(adoption,journey,hold.PerTickEnergy);
        return VoyagerRecoveryWindowPolicy.ChargeBlocker(p.mecha.coreEnergy, p.mecha.coreEnergyCap,
            CalculateAvailableFlightEnergy(p.mecha), journey.ReturnEnergyFloorJoules, hold.PerTickEnergy,
            journey.ReturnCoreEnergyMinimumJoules, _sessions.EffectiveVoyagerSpentGameTicks(journey) ?? -1,
            GameMain.gameTick - journey.LastLandedGameTick.Value,
            _sessions.EffectiveVoyagerMaximumGameTicks(journey), journey.ReturnReserveGameTicks, 0, authorization.Proposal.MaximumChargeGameTicks,
            0d, authorization.Proposal.MaximumChargeWallSeconds, DateTimeOffset.UtcNow, _sessions.EffectiveVoyagerDeadline(journey),
            adoption.CurrentDecisionEvidence);
    }

    private sealed class RemoteResumeInitializationHold
    {
        public VoyagerRemoteResumeAdoption Adoption { get; }
        public VoyagerJourneyRecord Journey { get; }
        public FirstFrameSurfaceState Surface { get; }
        public LoadedGroundSyncState State { get; }
        public GameStateReader.NativeNearGroundSupportBinding Support { get; }
        public NativeResumeInitializationPolicy Policy { get; }
        public string CostEnvelope { get; }
        public double PerTickEnergy { get; }
        private readonly StorageComponent[] _stores;
        private readonly StorageComponent.GRID[][] _grids;
        private readonly int[] _sizes;
        private readonly string _materials;

        public RemoteResumeInitializationHold(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord journey,
            FirstFrameSurfaceState surface, LoadedGroundSyncState state,
            GameStateReader.NativeNearGroundSupportBinding support, double perTickEnergy)
        {
            Adoption = adoption; Journey = journey; Surface = surface; State = state; Support = support;
            PerTickEnergy = perTickEnergy; CostEnvelope = RemoteResumeChargeCostEnvelope(adoption.Player);
            Policy = new NativeResumeInitializationPolicy(adoption.Evidence.SourceGameTick, DateTimeOffset.UtcNow);
            _stores = new[] { adoption.Player.package, adoption.Player.mecha.reactorStorage, adoption.Player.mecha.warpStorage };
            if (_stores.Any(s => s is null || s.grids is null || s.size < 1 || s.size > 1024 || s.grids.Length != s.size))
                throw new InvalidOperationException("remote_resume_initialization_material_bounds_unproved");
            _grids = _stores.Select(s => s.grids).ToArray(); _sizes = _stores.Select(s => s.size).ToArray();
            _materials = MaterialHash(adoption.Player);
        }

        public bool MaterialsMatch(Player p) => ReferenceEquals(p.package, _stores[0])
            && ReferenceEquals(p.mecha.reactorStorage, _stores[1]) && ReferenceEquals(p.mecha.warpStorage, _stores[2])
            && _stores.Select((s, i) => ReferenceEquals(s.grids, _grids[i]) && s.size == _sizes[i]).All(v => v)
            && MaterialHash(p) == _materials;

        private string MaterialHash(Player p) => CanonicalStateHash.Combine("native-resume-initialization-materials-v1",
            VoyagerNativePrerequisiteHash(p), string.Join(";", _stores.SelectMany(s => s.grids)
                .Select(g => $"{g.itemId}:{g.count}:{g.inc}")));
    }
}
