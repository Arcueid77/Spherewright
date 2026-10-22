using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private SavedWorldOpenGuard? _savedWorldOpen;
    private GameLoader[]? _savedOpenLoadersBefore;
    private GameLoader? _savedOpenLoader;
    private NativeLoadCompletionSubscription<GameLoader>? _savedOpenSubscription;
    private Spherewright.Contracts.Players.NativeResumeFailureSnapshot? _savedOpenFailure;
    internal string? SavedWorldOpenSessionId { get; private set; }
    internal Spherewright.Contracts.Players.NativeResumeFailureSnapshot? SavedWorldOpenFirstFailure =>
        (_savedOpenFailure ?? _savedWorldOpen?.FirstFailure)?.Copy();

    internal bool SavedWorldOpenCompleted(string actionId) =>
        _savedWorldOpen is { Completed: true } guard && guard.ActionId == actionId;

    internal void PrepareSavedWorldOpenNativeObserver()
    {
        if (_savedWorldOpen is null || _savedOpenLoadersBefore is not null || _savedOpenSubscription is not null)
            throw new InvalidOperationException("saved_world_open_observer_unavailable");
        _savedOpenLoadersBefore = UnityEngine.Object.FindObjectsOfType<GameLoader>();
    }

    internal void BindSavedWorldOpenNativeObserver()
    {
        var loader = NativeLoadCompletionSubscription<GameLoader>.SelectUniqueNew(
            _savedOpenLoadersBefore ?? throw new InvalidOperationException("saved_world_open_dispatch_boundary_missing"),
            UnityEngine.Object.FindObjectsOfType<GameLoader>(),
            candidate => candidate.frame == 0 && candidate.enabled && candidate.gameObject.activeInHierarchy);
        _savedOpenLoadersBefore = null;
        _savedOpenLoader = loader;
        _savedOpenSubscription = new NativeLoadCompletionSubscription<GameLoader>(loader,
            handler => loader.onLoadComplete += handler, handler => loader.onLoadComplete -= handler,
            source => ReferenceEquals(source, loader) && source.frame == 10 && !source.enabled,
            OnSavedWorldOpenLoadComplete);
    }

    private void OnSavedWorldOpenLoadComplete(GameLoader loader, bool completed, string? captureType)
    {
        if (_savedWorldOpen is null || !ReferenceEquals(loader, _savedOpenLoader)) return;
        try
        {
            // Supported Native Pause at the exact loader event. No player601,
            // contact/charge/idle claim, physics write or task Resume is needed.
            GameMain.Pause();
            var ticket = _expectedResumeTicket;
            if (!_savedWorldOpen.ObserveLoad(GameMain.data, completed && captureType is null, GameMain.isPaused,
                GameMain.gameTick, ticket is null || GameMain.data is null ? null
                    : OwnedWorldProvenancePolicy.MatchesProtectedSaveIdentity(ticket.OwnedSaveName, GameMain.data.gameName),
                ticket is null || GameMain.data?.localPlanet is null ? null : GameMain.data.localPlanet.id == ticket.ExpectedPlanetId))
                _resumeAdoptionError = _savedWorldOpen.FirstFailure?.Code ?? "saved_world_open_native_boundary_unproved";
        }
        catch (Exception exception)
        {
            _resumeAdoptionError ??= "saved_world_open_load_callback_failed:" + exception.GetType().Name;
            RecordSavedOpenUnobservedFailure(_resumeAdoptionError, "saved_world_open_load_complete", "native_pause_call_completed");
        }
    }

    internal void RecordSavedWorldOpenDispatchFailure(string exceptionType)
    {
        if (_savedOpenSubscription is null && _savedOpenLoadersBefore is not null)
            try { BindSavedWorldOpenNativeObserver(); } catch (Exception) { }
        _resumeAdoptionError ??= "saved_world_open_dispatch_outcome_unknown:" + exceptionType;
        RecordSavedOpenUnobservedFailure(_resumeAdoptionError, "saved_world_open_dispatch", "native_dispatch_outcome_known");
    }

    private void StopPreviousAutomationForSavedWorldOpen()
    {
        if (_savedWorldOpen is null) return;
        _writeHealth = WriteHealthStates.Quarantined;
        _writeQuarantineActionId = _savedWorldOpen.ActionId;
        _writeQuarantineReason = OwnedSavedWorldOpenPolicy.AutomationStoppedCode
            + ": The saved world is open. Previous automatic tasks remain stopped; fresh execution authority and Native checks are required.";
    }

    private void ConfirmSavedWorldOpenJournal(OwnedWorldResumeTicket ticket, OwnedWorldGameplayJournalCheckpoint checkpoint)
    {
        var guard = _savedWorldOpen!;
        _pendingJournalResumeTicket = null;
        ConfirmedPlannedResumeMinimumGameTick = null;
        ReleaseResumeSourceLease();
        if (!guard.ConfirmJournal(GameMain.data, GameMain.gameTick, true, () =>
        {
            _resumeTickets.ArmFromOpenedSavedWorld(ticket.OwnedSaveName, _sessionId!, ticket.ExpectedPlanetId,
                guard.SavedTick, guard.ActionId, checkpoint);
            if (!_resumeTickets.TryGetSavedWorldOpenProvenance(_resumeTickets.CurrentResumeToken!, out var opened, out _, out _)
                || opened?.SavedWorldOpenOnly != true || opened.MinimumGameTick != guard.SavedTick
                || opened.SourceSessionId != _sessionId)
                throw new InvalidOperationException("saved_world_open_access_credential_unproved");
        }))
        { _resumeAdoptionError ??= guard.FirstFailure?.Code ?? "saved_world_open_journal_adoption_unproved"; return; }
        _ownedSaveState = OwnedSaveStates.Saved;
        _lastOwnedSaveGameTick = guard.SavedTick;
        SavedWorldOpenSessionId = _sessionId;
        StopPreviousAutomationForSavedWorldOpen();
    }

    private void RecordSavedOpenUnobservedFailure(string code, string stage, string predicate)
    {
        _savedOpenFailure ??= _savedWorldOpen?.FirstFailure ?? new Spherewright.Contracts.Players.NativeResumeFailureSnapshot
        {
            Code = code, Stage = stage, FirstFailedPredicate = predicate, FactsState = "not_observed",
            CapturedAtUtc = DateTimeOffset.UtcNow, CapturedAtGameTick = null,
        };
    }

    private void ResetSavedWorldOpen(bool clearFailure = false)
    {
        if (clearFailure) _savedOpenFailure = null;
        else _savedOpenFailure ??= _savedWorldOpen?.FirstFailure;
        _savedOpenSubscription?.Dispose(); _savedOpenSubscription = null;
        _savedOpenLoader = null; _savedOpenLoadersBefore = null; _savedWorldOpen = null;
        SavedWorldOpenSessionId = null;
    }
}
