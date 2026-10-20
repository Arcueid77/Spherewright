using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private sealed class VoyagerLandingChargeState
    {
        public VoyagerLandingChargeState(NativeLocalWalkSceneBinding scene, string envelope, double perTick, long tick)
        { Scene = scene; CostEnvelope = envelope; PerTick = perTick; ArmedAtGameTick = tick; }
        public NativeLocalWalkSceneBinding Scene { get; }
        public string CostEnvelope { get; }
        public double PerTick { get; }
        public long ArmedAtGameTick { get; }
        // Only a successful real501 callback may populate this observation.
        public long? LastPrePlayerTick { get; set; }
    }

    private bool VoyagerLandingChargeFlightMatches(ActionRecord action)
    {
        var journey = action.VoyagerJourney;
        var durable = _flightCheckpoints.VoyagerJourney;
        return _actions.TryGetValue(action.ActionId, out var original) && ReferenceEquals(original, action)
            && !action.Terminal && !action.VoyagerOutcomeUnknown && !action.Plan.VoyagerReturnLeg
            && !action.VoyagerLandingPendingPersistence && action.VoyagerStopReason is null
            && action.VoyagerNativeLaunchStarted && !action.VoyagerLegAccounted && VoyagerBindingMatches(action.Plan)
            && journey is not null && durable is not null && journey.Phase == "outbound" && !journey.ReturnLeg
            && journey.JourneyId == action.Plan.VoyagerJourneyId && journey.ActiveActionId == action.ActionId
            && journey.ActiveCheckpointId == action.FlightCheckpointId
            && PluginJson.Serialize(journey) == PluginJson.Serialize(durable)
            && !EvaluateVoyagerClock(action).ShouldRequestStop
            && HasVoyagerNextControlReserve(action, action.Plan.VoyagerBinding!.Player);
    }

    private bool VoyagerLandingChargeGuardFresh() => VoyagerLocalPrePlayerTickAvailable && !GameMain.isPaused
        && DateTimeOffset.UtcNow >= VoyagerLocalPrePlayerObservedAtUtc
        && DateTimeOffset.UtcNow - VoyagerLocalPrePlayerObservedAtUtc <= TimeSpan.FromSeconds(2);

    private string? VoyagerLandingChargeBlocker(ActionRecord action, VoyagerRemoteIdleAdmissionFacts? facts,
        bool continuing, bool postPlayer)
    {
        try { return CaptureVoyagerLandingChargeBlocker(action, facts, continuing, postPlayer); }
        catch (Exception exception)
        { return "voyager_landing_charge_capture_failed; exception=" + exception.GetType().Name; }
    }

    private string? CaptureVoyagerLandingChargeBlocker(ActionRecord action, VoyagerRemoteIdleAdmissionFacts? facts,
        bool continuing, bool postPlayer)
    {
        var player = action.Plan.VoyagerBinding!.Player;
        var wait = action.VoyagerLandingCharge;
        var currentCost = RemoteResumeChargeCostEnvelope(player);
        var perTick = facts?.MovementReserve?.PerNativeTickEnergyJoules;
        var sequence = wait is null || VoyagerLandingChargePolicy.NativeSequenceProved(
            wait.ArmedAtGameTick, wait.LastPrePlayerTick, GameMain.gameTick, postPlayer);
        return VoyagerLandingChargePolicy.Blocker(new VoyagerLandingChargeFacts
        {
            Admission = facts, ContinuingWait = continuing,
            OriginalFlightActive = VoyagerLandingChargeFlightMatches(action),
            NativeGuardFresh = VoyagerLandingChargeGuardFresh() && sequence,
            CostEnvelopeStable = perTick.HasValue && (wait is null
                || wait.CostEnvelope == currentCost && wait.PerTick == perTick.Value),
            EnvironmentBlocker = RemoteResumeChargeEnvironmentBlocker(player),
            ContactAtGameTick = action.FlightDestinationContactAtGameTick,
            StableAtGameTick = action.FlightStableLandingAtGameTick,
            FlightDeadlineGameTick = action.VoyagerDeadlineGameTick,
            NowUtc = DateTimeOffset.UtcNow, ControlLeaseExpiresAtUtc = action.VoyagerControlLeaseExpiresAtUtc,
            CoreCapacityJoules = player.mecha.coreEnergyCap,
            ReturnCoreMinimumJoules = action.VoyagerJourney?.ReturnCoreEnergyMinimumJoules ?? double.NaN,
        }, FlightLandingStableTicks, FlightLandingTimeoutTicks);
    }

    private bool TryWaitForVoyagerLandingCore(ActionRecord action, VoyagerRemoteIdleAdmissionDecision admission,
        VoyagerRemoteIdleAdmissionFacts? facts, NativeLocalWalkSceneBinding? scene)
    {
        var continuing = action.VoyagerLandingCharge is not null;
        if (!continuing && (admission.BlockerCode != VoyagerLandingChargePolicy.CoreBlocker
            || action.VoyagerLandingChargeStarted || scene is null)) return false;
        if (continuing && !admission.Proved && admission.BlockerCode != VoyagerLandingChargePolicy.CoreBlocker)
        {
            FailVoyagerLandingCharge(action, "The original landing recharge lost its strict Native conditions."
                + VoyagerRemoteIdleFailureDetail(admission));
            return true;
        }
        var blocker = VoyagerLandingChargeBlocker(action, facts, continuing, postPlayer: continuing);
        if (blocker is not null)
        {
            FailVoyagerLandingCharge(action, "The original landing cannot continue its bounded Native recharge. [" + blocker + "]");
            return true;
        }
        action.VoyagerLandingChargeStarted = true;
        action.VoyagerLandingCharge ??= new VoyagerLandingChargeState(scene!,
            RemoteResumeChargeCostEnvelope(action.Plan.VoyagerBinding!.Player),
            facts!.MovementReserve!.PerNativeTickEnergyJoules!.Value, GameMain.gameTick);
        action.State = NormalActionStates.WaitingForGame;
        action.Message = "The original stable landing is awaiting its unchanged 13-tick core reserve under the original contact, flight and observation deadlines. Native generation is not guaranteed.";
        return true;
    }

    private void GuardVoyagerLandingChargeOnMainThread(bool stoppingHost = false)
    {
        foreach (var action in _actions.ActiveValues.Where(a => !a.Terminal && a.VoyagerLandingCharge is not null).ToArray())
        {
            try
            {
                if (stoppingHost || !VoyagerBindingMatches(action.Plan))
                {
                    FailVoyagerLandingCharge(action, "The original landing recharge lost its exact pre-player owner or Host.");
                    continue;
                }
                var journey = action.VoyagerJourney!;
                var spent = VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(journey.SpentGameTicks,
                    action.StartedAtGameTick, GameMain.gameTick, action.VoyagerLegAccounted);
                VoyagerRemoteIdleAdmissionFacts? facts = null;
                var owner = new VoyagerRemoteIdleOwner(action.Plan.VoyagerBinding!, action.SessionId,
                    action.Plan.VoyagerJourneyId!, action.ActionId, action.ActionId);
                var admission = !spent.HasValue
                    ? VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_landing_accounting_unproved")
                    : CaptureVoyagerRemoteIdleAdmission(owner, journey, action.VoyagerLandingCharge!.Scene,
                        spent.Value, GameMain.gameTick, out _, action.VoyagerSelectedDestinationPlanetId,
                        captureFacts: captured => facts = captured, groundStage: "voyager_landing_charge_pre_player");
                var blocker = admission.Proved || admission.BlockerCode == VoyagerLandingChargePolicy.CoreBlocker
                    ? VoyagerLandingChargeBlocker(action, facts, continuing: true, postPlayer: false)
                    : admission.BlockerCode ?? "voyager_landing_charge_capture_unproved";
                if (blocker is not null) FailVoyagerLandingCharge(action,
                    "The original landing recharge guard refused the next Native tick. [" + blocker + "]");
                else action.VoyagerLandingCharge!.LastPrePlayerTick = GameMain.gameTick;
            }
            catch (Exception exception)
            {
                FailVoyagerLandingCharge(action, "The original landing recharge capture failed. [voyager_landing_charge_capture_failed; exception="
                    + exception.GetType().Name + "]");
            }
        }
    }

    private void FailVoyagerLandingCharge(ActionRecord action, string reason)
    {
        if (action.Terminal) return;
        // Pause before checkpoint/accounting I/O. At task501 the current601
        // remains funded; Pause proves neither physical zero nor arrival.
        var exact = VoyagerBindingMatches(action.Plan);
        reason += FreezeNativeLocalFailureEnergy(exact);
        action.VoyagerLandingCharge = null;
        action.VoyagerLandingChargeStarted = true; // A failed original wait can never re-arm.
        if (action.VoyagerLandingPendingPersistence)
        {
            // The checkpoint may already be flight_succeeded. Preserve that
            // original outcome and pending persistence; never make it loadable.
            action.VoyagerStopReason ??= reason;
            MarkVoyagerOutcomeUnknown(action, reason);
            CaptureVoyagerReadback(action);
            return;
        }
        RequestVoyagerStop(action, reason);
        if (exact)
        {
            var p = action.Plan.VoyagerBinding!.Player;
            if (GameMain.isPaused && p.movementState == EMovementState.Walk && p.speed <= .1f
                && !p.warpCommand && p.warpState == 0f)
            {
                UpdateVoyagerStopOnMainThread(action, p);
                CaptureVoyagerReadback(action);
                return;
            }
        }
        MarkVoyagerOutcomeUnknown(action, action.VoyagerStopReason!);
        CaptureVoyagerReadback(action);
    }
}
