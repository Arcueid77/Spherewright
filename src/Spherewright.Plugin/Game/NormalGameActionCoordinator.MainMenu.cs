using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    public GameCallResult<PreparedNormalAction> PrepareReturnToMainMenuOnMainThread(
        string? sessionId, PrepareSaveRequest request)
    {
        var common = ValidatePrepareCommon(sessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        var blocker = MainMenuIdleBlocker();
        if (blocker is not null) return GameCallResult<PreparedNormalAction>.Failed(blocker);
        var result = PrepareSavePlanOnMainThread(sessionId, request, NormalActionKinds.ReturnToMainMenu);
        if (result.Value is { } prepared)
            prepared.CompletionCondition = "One normal owned Save and complete current-file proof precede DSP's native Esc-menu EndGame path; success requires the idle main menu. The process remains running. Native EndGame may also write LastExit. Original outcomes and quarantine remain retained; no Load is performed.";
        return result;
    }

    private BridgeError? MainMenuIdleBlocker(ActionRecord? executing = null)
    {
        if (_actions.ActiveValues.Any(a => !a.Terminal && !ReferenceEquals(a, executing)))
            return Stale("Wait for every accepted action to become terminal before returning to the main menu.");
        var player = GameMain.mainPlayer;
        var factory = GameMain.data?.localLoadedPlanetFactory;
        if (!GameMain.isRunning || DSPGame.IsMenuDemo || UIRoot.instance is null
            || UnityEngine.Object.FindObjectOfType<GameLoader>() != null
            || player?.controller?.actionWalk is null || !player.controller.actionWalk.isGrounded
            || player.currentOrder is not null || !(player.controller.velocity.sqrMagnitude <= 0.01f)
            || player.mecha?.constructionModule is null || factory is null
            || !TryObserveOwnedConstructionDrones(factory, player.mecha.constructionModule, out var idle, out _)
            || !idle)
            return Stale("Returning to the main menu requires the exact loaded grounded, order-free player, physically idle construction drones and no game loader.");
        return null;
    }

    private void ExecuteReturnToMainMenuOnMainThread(ActionRecord action)
    {
        // Freeze inventory while the original world still exists. No later
        // snapshot may borrow a menu-demo or reloaded world's inventory.
        action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
        var operation = action.MainMenuReturn = new ProtectedMainMenuReturn();
        operation.Begin(
            () =>
            {
                var blocker = MainMenuIdleBlocker(action) ?? SavePreservationBlocker(action.Plan, action);
                if (blocker is not null) throw new InvalidOperationException(blocker.Message);
                if (!_sessions.TrySaveOwnedWorldNowOnMainThread(out var error, action.Plan.SavePreservation))
                    throw new InvalidOperationException(error);
                var session = _sessions.CaptureOnMainThread();
                if (action.Plan.SavePreservation is null) CloseVoyagerAfterNormalSave(action, session);
                return session.LastOwnedSaveGameTick ?? throw new InvalidDataException("No saved tick was recorded.");
            },
            tick =>
            {
                using var file = OwnedSaveRecoveryLease.Open(GameSave.SavePath(action.Plan.SaveOwnedName), action.Plan.SaveOwnedName);
                return file.Prefix.MatchesExpectedIdentity && file.Prefix.GameTick == tick && tick == GameMain.gameTick
                    && file.Prefix.GameVersion == _sessions.CurrentNativeGameVersion && file.Prefix.Peaceful
                    && !file.Prefix.Sandbox && MainMenuIdleBlocker(action) is null;
            },
            () =>
            {
                // Exactly UIEscMenu.OnButton5Click, never ExitProgram or Quit.
                UIRoot.instance.backToMainMenu = true;
                DSPGame.EndGame();
            }, DateTimeOffset.UtcNow);
        UpdateReturnToMainMenuOnMainThread(action);
    }

    private void UpdateReturnToMainMenuOnMainThread(ActionRecord action)
    {
        var operation = action.MainMenuReturn!;
        operation.Observe(() => TestWorldCoordinator.ValidateMainMenuReady() is null, DateTimeOffset.UtcNow);
        action.State = operation.Terminal
            ? operation.Succeeded ? NormalActionStates.Completed : NormalActionStates.OutcomeUnknown
            : NormalActionStates.WaitingForGame;
        action.Terminal = operation.Terminal;
        action.Succeeded = operation.Succeeded;
        if (!operation.Terminal) return;
        action.CompletedAtGameTick = operation.Readback.SavedGameTick ?? action.StartedAtGameTick;
        action.AfterStateHash = CanonicalStateHash.Combine(NormalActionKinds.ReturnToMainMenu,
            action.SessionId, operation.Readback.SavedGameTick, operation.Readback.MainMenuVerified,
            operation.Readback.FailedPredicate);
        action.Message = operation.Succeeded
            ? "The complete current owned Save was verified and DSP returned to its idle main menu in the same process."
            : "Main-menu return stopped at " + operation.Readback.FailedPredicate + "; retain this original action and inspect saved/runtime state before any new action.";
        if (!operation.Succeeded && _sessions.IsCurrentSessionOwned)
            _sessions.QuarantineWritesOnMainThread(action.ActionId, action.Message);
    }

    private sealed partial class ActionRecord
    {
        public ProtectedMainMenuReturn? MainMenuReturn { get; set; }
    }
}
