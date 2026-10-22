using Spherewright.Contracts.Actions;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private void StartVoyagerFlightOnMainThread(ActionRecord action)
    {
        var plan = action.Plan;
        var budget = plan.VoyagerBudget!;
        InitializeVoyagerFlightNavigationOnMainThread(action);
        if (!EnsureFlightCheckpointOnMainThread(action)) return;
        var journey = plan.VoyagerReturnLeg ? _flightCheckpoints.VoyagerJourney! : new VoyagerJourneyRecord
        {
            JourneyId = plan.VoyagerJourneyId!, OwnedSaveName = _sessions.CaptureOnMainThread().SaveName!,
            GameVersion = _sessions.CaptureOnMainThread().GameVersion, HomePlanetId = plan.PlanetId,
            HomeStarId = budget.OriginStarId, DestinationStarId = budget.DestinationStarId,
            StartedAtGameTick = GameMain.gameTick, MaximumGameTicks = budget.MaximumFlightGameTicks,
            ReturnReserveGameTicks = budget.ReturnReserveGameTicks,
            ReturnEnergyFloorJoules = budget.ReturnEnergyBudgetJoules + budget.WithdrawalReserveJoules,
            WithdrawalReserveJoules = budget.WithdrawalReserveJoules,
            ReturnCoreEnergyMinimumJoules = budget.ReturnCoreEnergyMinimumJoules,
            ExtraWarperReserve = plan.VoyagerRequest!.ExtraWarperReserve,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(budget.MaximumFlightGameTicks / 60d * 2d + 120d),
        };
        if (plan.VoyagerReturnLeg && journey.LastLandedGameTick.HasValue)
            journey.SpentGameTicks += Math.Max(0L, GameMain.gameTick - journey.LastLandedGameTick.Value);
        journey.ReturnLeg = plan.VoyagerReturnLeg;
        journey.Phase = plan.VoyagerReturnLeg ? "return" : "outbound";
        journey.ActiveActionId = action.ActionId;
        journey.ActiveCheckpointId = action.FlightCheckpointId!;
        action.VoyagerJourney = journey;
        var legTicks = plan.VoyagerReturnLeg ? journey.ReturnReserveGameTicks
            : Math.Min(budget.OutboundMaximumGameTicks, journey.MaximumGameTicks - journey.SpentGameTicks - journey.ReturnReserveGameTicks);
        if (plan.VoyagerReturnLeg && journey.ReturnOnlyTask is not null)
            legTicks = VoyagerReturnOnlyRecoveryPolicy.FlightTicks(
                journey.RemainingReturnOnlyTicks(action.SessionId, GameMain.gameTick), legTicks);
        var rejection = string.Empty;
        if (legTicks < 3600 || !_flightCheckpoints.TryPersistVoyagerJourney(journey, out rejection))
        {
            RequestVoyagerStop(action, legTicks < 3600 ? "The whole journey no longer reserves a finite return leg." : rejection);
            return;
        }
        action.VoyagerDeadlineGameTick = GameMain.gameTick + legTicks;
        action.VoyagerPhase = "launch";
        LaunchInterplanetaryFlightFromBoundCheckpointOnMainThread(action);
        ReleaseCheckpointIdleToStartedReturn(action);
        CaptureVoyagerReadback(action);
    }

    private void StartInterplanetaryFlightOnMainThread(ActionRecord action)
    {
        if (!EnsureFlightCheckpointOnMainThread(action)) return;
        LaunchInterplanetaryFlightFromBoundCheckpointOnMainThread(action);
    }

    // Both flight modes enter here only after one completed checkpoint ensure.
    // A fresh ticket is bound before commit increments the session revision;
    // recovered/reused tickets retain the existing tracker reuse rules.
    private void LaunchInterplanetaryFlightFromBoundCheckpointOnMainThread(ActionRecord action)
    {
        var ticket = _flightCheckpoints.CurrentTicket;
        var session = _sessions.CaptureOnMainThread();
        var freshCheckpoint = ticket is not null
            && ticket.SourceSessionId == session.SessionId && ticket.SourceRevision == session.Revision;
        var checkpointRejection = "The exact action-bound checkpoint or owned session changed before native launch.";
        if (ticket is null || ticket.CheckpointId != action.FlightCheckpointId
            || ticket.ReloadToken != action.FlightCheckpointReloadToken || ticket.SavedGameTick != action.FlightCheckpointGameTick
            || ticket.OriginPlanetId != action.PlanetId || ticket.DestinationPlanetId != action.Plan.DestinationPlanetId
            || ticket.VoyagerJourneyId != action.Plan.VoyagerJourneyId
            || !session.OwnedBySpherewright || session.SessionId != action.SessionId
            || session.SaveName != ticket.OwnedSaveName || session.GameVersion != ticket.GameVersion
            || session.LocalPlanetId != action.PlanetId
            || (!freshCheckpoint && !_sessions.CanReuseFlightCheckpointForCurrentSession(ticket))
            || !_flightCheckpoints.TryValidateCheckpointFile(ticket, out checkpointRejection))
        {
            Fail(action, "Native launch was not started: " + checkpointRejection);
            return;
        }
        if (action.Plan.VoyagerBudget is not null)
        {
            var journey = _flightCheckpoints.VoyagerJourney;
            if (journey is null || journey.JourneyId != action.Plan.VoyagerJourneyId
                || journey.ActiveCheckpointId != ticket.CheckpointId || journey.ActiveActionId != action.ActionId)
            {
                Fail(action, "Native launch was not started because the durable Voyager journey does not bind this exact checkpoint/action.");
                return;
            }
        }

        if (!_flightCheckpoints.TryMarkAttemptStarted(
                action.FlightCheckpointId!,
                action.ActionId,
                GameMain.gameTick,
                out var lifecycleRejection))
        {
            Fail(action, $"Native launch was not started because its checkpoint lifecycle could not be armed: {lifecycleRejection}");
            return;
        }

        var player = GameMain.mainPlayer;
        action.FlightBestDistance = action.Plan.EstimatedDistance;
        action.FlightBestDistanceAtGameTick = GameMain.gameTick;
        action.FlightLastControlGameTick = -1L;
        action.FlightDestinationContactAtGameTick = -1L;
        action.FlightStableLandingAtGameTick = -1L;
        // PlayerMove_Fly caps targetAltitude below its own sail threshold while
        // blueprint mode is retained. This is the same public setter the
        // current-version native sail-entry branch uses before switching modes.
        player.controller.actionBuild.blueprintMode = EBlueprintMode.None;
        if (action.Plan.VoyagerBudget is not null)
        {
            if (!_flightCheckpoints.TryArmVoyagerNativeLaunch(action.FlightCheckpointId!, action.ActionId, out var armRejection))
            {
                Fail(action, armRejection);
                return;
            }
            action.VoyagerNativeLaunchStarted = true;
        }
        EnterNativeFlight(player);
        action.State = NormalActionStates.WaitingForGame;
        action.Message = $"A separate pre-flight checkpoint was confirmed at tick {action.FlightCheckpointGameTick}; DSP then accepted native launch toward planet {action.Plan.DestinationPlanetId}.";
    }

    private bool EnsureFlightCheckpointOnMainThread(ActionRecord action)
    {
        var existing = _flightCheckpoints.CurrentTicket;
        if (existing is not null
            && existing.OriginPlanetId == action.PlanetId
            && existing.DestinationPlanetId == action.Plan.DestinationPlanetId
            && existing.VoyagerJourneyId == action.Plan.VoyagerJourneyId
            && _sessions.CanReuseFlightCheckpointForCurrentSession(existing)
            && _flightCheckpoints.TryValidateCheckpointFile(existing, out _))
        {
            BindFlightCheckpoint(action, existing);
            return true;
        }

        var session = _sessions.CaptureOnMainThread();
        if (!session.OwnedBySpherewright
            || string.IsNullOrWhiteSpace(session.SessionId)
            || string.IsNullOrWhiteSpace(session.SaveName)
            || session.LocalPlanetId != action.PlanetId)
        {
            Fail(action, "Native launch was not started because the exact primary owned-save identity was unavailable for a pre-flight checkpoint.");
            return false;
        }

        try
        {
            var ticket = _flightCheckpoints.CreateDraft(
                session.SaveName!,
                session.SessionId!,
                session.Revision,
                action.PlanetId,
                action.Plan.DestinationPlanetId,
                action.Plan.PlayerStateHash,
                action.Plan.StarSystemStateHash,
                action.Plan.VoyagerBudget?.OriginStarId ?? 0,
                action.Plan.VoyagerBudget?.DestinationStarId ?? 0,
                action.Plan.VoyagerJourneyId);
            GameMain.gameName = session.SaveName;
            if (!GameSave.SaveCurrentGame(ticket.CheckpointSaveName))
            {
                Fail(action, "Native launch was not started because DSP did not confirm the separate pre-flight save.");
                return false;
            }

            var savedGameTick = GameMain.gameTick;
            GameSave.ReadHeader(ticket.CheckpointSaveName, false, out var header);
            if (header is null || header.gameTick != savedGameTick)
            {
                Fail(action, "Native launch was not started because the pre-flight save header did not prove the exact saved game tick.");
                return false;
            }

            _flightCheckpoints.PersistCompletedCheckpoint(ticket, savedGameTick);
            _sessions.MarkCurrentSessionFlightCheckpoint(ticket);
            BindFlightCheckpoint(action, ticket);
            return true;
        }
        catch (Exception exception)
        {
            Fail(action, $"Native launch was not started because the protected pre-flight checkpoint could not be completed ({exception.GetType().Name}).");
            return false;
        }
    }

    private static void BindFlightCheckpoint(ActionRecord action, FlightCheckpointTicket ticket)
    {
        action.FlightCheckpointId = ticket.CheckpointId;
        action.FlightCheckpointReloadToken = ticket.ReloadToken;
        action.FlightCheckpointGameTick = ticket.SavedGameTick;
    }

}
