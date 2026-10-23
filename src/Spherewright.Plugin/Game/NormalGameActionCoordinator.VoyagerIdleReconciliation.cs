using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // Same-process provenance only. This is neither a saved credential nor a
    // new budget: the original guard, owner references and ledger must survive.
    private (VoyagerRemoteIdleOwner Owner, string Reason, long Tick, string Ledger)? _voyagerIdlePause;

    private void RetainVoyagerIdlePause(VoyagerRemoteIdleOwner owner, VoyagerJourneyRecord journey,
        string reason, bool stoppingHost)
    {
        if (stoppingHost || !GameMain.isPaused || owner.CheckpointHandoff is not null) return;
        var session = _sessions.CaptureOnMainThread();
        if (session.WriteHealth != WriteHealthStates.Quarantined
            || session.WriteQuarantineActionId != owner.ActionId || _sessions.WriteQuarantineReason != reason) return;
        if (_voyagerIdlePause is { } previous && ReferenceEquals(previous.Owner, owner)) return;
        _voyagerIdlePause = (owner, reason, GameMain.gameTick, PluginJson.Serialize(journey));
    }

    private bool IsPausedVoyagerIdleReconciliation(string actionId) =>
        _voyagerIdlePause is { } pause && pause.Owner.ActionId == actionId;

    private bool TryProvePausedVoyagerIdle(SessionState session, string actionId,
        out string proofHash, out string rejection)
    {
        proofHash = string.Empty;
        rejection = "paused_voyager_idle_identity_unproved";
        var journey = _flightCheckpoints.VoyagerJourney;
        if (_voyagerIdlePause is not { } pause || journey is null || !GameMain.isPaused || !GameMain.isRunning
            || !VoyagerLocalPrePlayerTickAvailable
            || !ReferenceEquals(_voyagerRemoteIdleOwner, pause.Owner)
            || pause.Owner.ActionId != actionId || pause.Owner.SessionId != session.SessionId
            || !session.GameLoaded || !session.OwnedBySpherewright
            || session.WriteHealth != WriteHealthStates.Quarantined || session.WriteQuarantineActionId != actionId
            || _sessions.WriteQuarantineReason != pause.Reason || GameMain.gameTick < pause.Tick
            || !VoyagerRemoteIdleBindingMatches(pause.Owner, journey)
            || _sessions.HasPendingRemoteResumeAdoption || _sessions.ResumeAdoptionError is not null
            || pause.Owner.CheckpointHandoff is not null || journey.ReturnOnlyScopeActive
            || PluginJson.Serialize(journey) != pause.Ledger) return false;
        if (_actions.ActiveValues.Any(a => a.SessionId == session.SessionId
            && (!a.Terminal || a.State == NormalActionStates.OutcomeUnknown)))
        { rejection = "paused_voyager_idle_unresolved_action"; return false; }
        if (session.WriteBlockers.Count != 1
            || session.WriteBlockers.Any(b => b.Code != Spherewright.Contracts.Errors.BridgeErrorCodes.WriteSubsystemQuarantined
                || b.Message != pause.Reason))
        { rejection = "paused_voyager_idle_other_write_blocker"; return false; }

        VoyagerRemoteIdleAdmissionFacts? captured = null;
        var decision = CaptureVoyagerRemoteIdleAdmission(pause.Owner, journey, _voyagerRemoteIdleScene,
            journey.SpentGameTicks, journey.LastLandedGameTick, out _, captureFacts: f => captured = f,
            groundStage: "paused_idle_reconciliation", reconcilePaused: true);
        if (!decision.Proved || captured?.JourneyBudget is not { } budget)
        { rejection = decision.BlockerCode ?? "paused_voyager_idle_capture_unproved"; return false; }
        var p = pause.Owner.Binding.Player.position;
        proofHash = CanonicalStateHash.Combine("paused-voyager-idle-reconciliation-v1", session.SessionId,
            session.LocalPlanetId, session.Revision, actionId, pause.Reason, pause.Tick, pause.Ledger,
            GameMain.gameTick, p.x, p.y, p.z, captured.CoreEnergyJoules, budget.AvailableLoadedEnergyJoules,
            budget.NativeWarperCount, captured.MovementReserve?.PerNativeTickEnergyJoules);
        rejection = string.Empty;
        return true;
    }

    private bool TryResumeProvedVoyagerIdle(SessionState session, string actionId, string expectedProof,
        out string rejection)
    {
        if (!TryProvePausedVoyagerIdle(session, actionId, out var actual, out rejection)) return false;
        if (actual != expectedProof) { rejection = "paused_voyager_idle_proof_changed"; return false; }
        var pause = _voyagerIdlePause!.Value;
        try
        {
            // Native Resume only changes the pause flag. This main-thread call
            // completes before another Native tick; no movement order is issued.
            GameMain.Resume();
            if (!GameMain.isPaused && _sessions.TryClearQuarantineOnMainThread(actionId, pause.Reason, out _))
            { _voyagerIdlePause = null; return true; }
            rejection = "paused_voyager_idle_resume_or_clear_failed";
        }
        catch (Exception exception)
        { rejection = "paused_voyager_idle_resume_failed:" + exception.GetType().Name; }
        if (VoyagerRemoteIdleRuntimeMatches(pause.Owner))
        {
            GameMain.Pause();
            _sessions.QuarantineWritesOnMainThread(actionId, pause.Reason);
        }
        return false;
    }
}
