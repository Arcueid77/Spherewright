using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private VoyagerRemoteIdleOwner? _voyagerRemoteIdleOwner;
    private NativeLocalWalkSceneBinding? _voyagerRemoteIdleScene;
    private bool _observingVoyagerPostPhysics;
    // A separate patience bound, not a 13/60s prediction of physics motion.
    private const double VoyagerLocalStopWallSeconds = VoyagerRemoteIdleAdmissionPolicy.StopWallSeconds;

    private bool VoyagerRemoteIdleBindingMatches()
    {
        return VoyagerRemoteIdleBindingMatches(_voyagerRemoteIdleOwner, _flightCheckpoints.VoyagerJourney);
    }

    private bool VoyagerRemoteIdleBindingMatches(VoyagerRemoteIdleOwner? owner, VoyagerJourneyRecord? journey,
        int? prospectiveRemotePlanetId = null)
    {
        var b = owner?.Binding;
        return owner is not null && b is not null && journey is not null
            && (prospectiveRemotePlanetId.HasValue || journey.Phase == "remote_landed")
            && journey.JourneyId == owner.JourneyId && VoyagerRemoteIdleRuntimeMatches(owner)
            && VoyagerLocalActionBudgetPolicy.MatchesOwnedJourneyIdentity(journey.OwnedSaveName, journey.GameVersion,
                _sessions.IsCurrentSessionOwned, _sessions.OwnedSaveName, b.World.gameName, _sessions.CurrentNativeGameVersion)
            && ReferenceEquals(GameMain.data, b.World) && ReferenceEquals(GameMain.mainPlayer, b.Player)
            && ReferenceEquals(b.Player.controller, b.Controller) && ReferenceEquals(b.Player.mecha, b.Mecha)
            && GameMain.localPlanet is { } planet && planet.id == (prospectiveRemotePlanetId ?? journey.RemotePlanetId)
            && ReferenceEquals(b.Player.planetData, planet) && ReferenceEquals(b.Player.factory, planet.factory);
    }

    private bool VoyagerRemoteIdleRuntimeMatches(VoyagerRemoteIdleOwner owner) =>
        _sessions.IsCurrentSessionOwned && _sessions.SessionId == owner.SessionId
        && ReferenceEquals(GameMain.data, owner.Binding.World) && ReferenceEquals(GameMain.mainPlayer, owner.Binding.Player)
        && ReferenceEquals(owner.Binding.Player.controller, owner.Binding.Controller)
        && ReferenceEquals(owner.Binding.Player.mecha, owner.Binding.Mecha);

    private void GuardVoyagerRemoteIdleOnMainThread(bool stoppingHost = false)
    {
        if (stoppingHost) _voyagerIdlePause = null;
        // The successful outbound remains successful. Its existing journey
        // continues owning idle energy until a budgeted controller takes over.
        // A terminal local order alone cannot guarantee the next tick stays dry.
        if (GameMain.isPaused) return;
        var bindingMatches = false;
        var bindingFailure = VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_binding_unproved");
        try { bindingMatches = VoyagerRemoteIdleBindingMatches(); }
        catch (Exception exception)
        { bindingFailure = VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_capture_failed", "binding", exception.GetType().Name); }
        if (!bindingMatches)
        {
            if (_voyagerRemoteIdleOwner is { CheckpointHandoff: { } pending } checkpointOwner)
            {
                var sameWorld = ReferenceEquals(GameMain.data, checkpointOwner.Binding.World);
                pending.Reject(CaptureCheckpointBindingLoss(checkpointOwner, DateTimeOffset.UtcNow));
                _sessions.FinishCheckpointReturnHandoff(checkpointOwner.ActionId, pending.FirstFailure);
                if (sameWorld && _sessions.IsCurrentSessionOwned && _sessions.SessionId == checkpointOwner.SessionId)
                    _sessions.QuarantineRemoteIdleOnMainThread(checkpointOwner.ActionId,
                        "The recovered return lost its exact Native idle binding. [remote_idle_binding_unproved]"
                        + FreezeNativeLocalFailureEnergy(true));
                return; // Never touch a replacement world or borrow the old flight action.
            }
            if (_voyagerRemoteIdleOwner is { } originalOwner && _flightCheckpoints.VoyagerJourney is { Phase: "remote_landed" } retainedOwner
                && retainedOwner.JourneyId == originalOwner.JourneyId && VoyagerRemoteIdleRuntimeMatches(originalOwner))
            {
                var reason = "The completed outbound's remote idle binding is no longer proved. Its successful flight is retained."
                    + VoyagerRemoteIdleFailureDetail(bindingFailure) + FreezeNativeLocalFailureEnergy(true);
                _sessions.QuarantineRemoteIdleOnMainThread(originalOwner.ActionId, reason);
                return;
            }
            // A remote primary/Host reload has no in-memory landed-controller
            // handoff. Do not silently recertify it or borrow an old action.
            var retained = _flightCheckpoints.VoyagerJourney;
            if (retained?.Phase == "remote_landed" && _sessions.SessionId is { } sessionId
                && VoyagerOwnedJourneyMatchesCurrentSession(retained, sessionId)
                && GameMain.mainPlayer?.planetId == retained.RemotePlanetId
                && !string.IsNullOrEmpty(retained.ActiveActionId))
            {
                var reason = "The original remote journey has no current landed-controller idle binding; remote local work is not recertified after reload."
                    + VoyagerRemoteIdleFailureDetail(bindingFailure)
                    + FreezeNativeLocalFailureEnergy(true);
                _sessions.QuarantineRemoteIdleOnMainThread(retained.ActiveActionId, reason);
            }
            return;
        }
        var owner = _voyagerRemoteIdleOwner!;
        var journey = _flightCheckpoints.VoyagerJourney!;
        if (!stoppingHost && _actions.ActiveValues.Any(a => !a.Terminal
            && (IsVoyagerLocalOrder(a) && a.Plan.VoyagerLocalJourneyId == journey.JourneyId
                || IsStructuredConstruction(a.Plan) && a.Plan.BlueprintState?.StructuredConstruction?.JourneyId == journey.JourneyId)))
            return; // Only controllers with the full current-tick reserve.
        // Host teardown must attempt Pause before any cost capture can throw.
        var admission = stoppingHost ? VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_host_stopping")
            : owner.CheckpointHandoff is not null ? CaptureCheckpointReturnIdleAdmission(owner, journey)
            : CaptureVoyagerRemoteIdleAdmission(owner, journey, _voyagerRemoteIdleScene,
                journey.SpentGameTicks, journey.LastLandedGameTick, out _);
        if (stoppingHost && owner.CheckpointHandoff is { } stopped)
        {
            stopped.Reject(NativeResumeDecisionEvidence.UnobservedFailure("remote_idle_host_stopping",
                "checkpoint_idle_host_stop", GameMain.gameTick, DateTimeOffset.UtcNow, "native_observer_available"));
            _sessions.FinishCheckpointReturnHandoff(owner.ActionId, stopped.FirstFailure);
        }
        if (admission.Proved) return;
        var message = "The completed outbound's exact remote idle/return reserve is no longer proved. Its successful flight is retained; the original journey requires intervention."
            + VoyagerRemoteIdleFailureDetail(admission)
            + FreezeNativeLocalFailureEnergy(bindingMatches);
        _sessions.QuarantineRemoteIdleOnMainThread(owner.ActionId, message);
        RetainVoyagerIdlePause(owner, journey, message, stoppingHost);
    }

    private sealed class VoyagerRemoteIdleOwner
    {
        public VoyagerRemoteIdleOwner(VoyagerNativeBinding binding, string sessionId, string journeyId,
            string actionId, string sourceFlightActionId, VoyagerRemoteResumeAdoption? resumeAdoption = null,
            VoyagerCheckpointIdleHandoff? checkpointHandoff = null)
        {
            Binding = binding; SessionId = sessionId; JourneyId = journeyId; ActionId = actionId;
            SourceFlightActionId = sourceFlightActionId; ResumeAdoption = resumeAdoption; CheckpointHandoff = checkpointHandoff;
        }
        public VoyagerNativeBinding Binding { get; }
        public string SessionId { get; }
        public string JourneyId { get; }
        public string ActionId { get; }
        public string SourceFlightActionId { get; }
        public VoyagerRemoteResumeAdoption? ResumeAdoption { get; }
        public VoyagerCheckpointIdleHandoff? CheckpointHandoff { get; }
    }

    private bool VoyagerOwnedJourneyMatchesCurrentSession(VoyagerJourneyRecord journey, string expectedSessionId)
    {
        var session = _sessions.CaptureOnMainThread();
        return _sessions.IsCurrentSessionOwned && _sessions.SessionId == expectedSessionId && session.SessionId == expectedSessionId
            && VoyagerLocalActionBudgetPolicy.MatchesOwnedJourneyIdentity(journey.OwnedSaveName, journey.GameVersion,
                session.OwnedBySpherewright, _sessions.OwnedSaveName, session.SaveName, session.GameVersion);
    }

    private static string VoyagerRemoteIdleFailureDetail(VoyagerRemoteIdleAdmissionDecision decision) =>
        " [" + decision.BlockerCode
        + (decision.CaptureStage is null ? string.Empty : "; capture=" + decision.CaptureStage)
        + (decision.CaptureExceptionType is null ? string.Empty : "; exception=" + decision.CaptureExceptionType) + "]";

    private static string FreezeNativeLocalFailureEnergy(bool exactOwnedBinding)
    {
        // Pause is an ordinary DSP control, never a velocity write or a
        // physical-stop certificate. At task501 this LogicFrame may still
        // finish: admission reserves that whole Player/construction tick.
        // Subsequent FixedUpdate calls return before another LogicFrame.
        // Never pause a replacement world or automatically Resume it.
        if (!exactOwnedBinding || !GameMain.isRunning) return string.Empty;
        try
        {
            GameMain.Pause();
            return GameMain.isPaused
                ? " Native Pause is verified; this LogicFrame may finish, then further Native energy is frozen. Physical idle remains unproved; do not automatically Resume or replay."
                : " Native Pause could not be verified; preserve the original failure for intervention.";
        }
        catch (Exception)
        { return " Native Pause failed; preserve the original failure for intervention."; }
    }

    public void UpdateVoyagerLocalPrePlayerTickOnMainThread()
    {
        _observingVoyagerPostPhysics = true;
        try
        {
            // Reconcile the already dispatched load BEFORE the idle guard,
            // even when no external client has polled the action yet.
            PumpCheckpointReturnRecoveryOnMainThread();
            UpdateVoyagerLocalPrePlayerTickCoreOnMainThread();
            PumpCheckpointReturnRecoveryOnMainThread();
        }
        finally { _observingVoyagerPostPhysics = false; }
    }

}
