using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    [Fact]
    public void FailedContinuationPreservesItsSameContactFailureAndCanSaveTheTailWithoutGrantingResume()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var first = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint(); Assert.True(f.ReadAction(first.ActionId).Succeeded);
        var tick = f.Ticket.SavedGameTick + 39; Fixture.WriteSave(GameSave.LastExit, tick);
        var credentials = ReturnContinuationCredentials(f, tick); var oldToken = credentials.CurrentResumeToken!;
        var coordinator = new OwnedWorldResumeCoordinator(true, 60, 30, 32, f.Sessions, credentials);
        var p = coordinator.PrepareOnMainThread(new()
        {
            ResumeToken = oldToken,
            RecoveryMode = OwnedWorldResumeModes.ResumeReturnOnlyTask,
            RecoveryScope = VoyagerReturnOnlyContinuationPolicy.Scope,
            ExpectedRecoveryGameTick = tick,
            RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(20)
        });
        Assert.True(p.Success, p.Error?.Message);
        var result = coordinator.CommitOnMainThread(new()
        {
            PlanToken = p.Value!.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D"),
            UserConfirmedInConversation = true,
            ConfirmationDigest = p.Value.ConfirmationDigest
        }); Assert.True(result.Success, result.Error?.Message);
        Assert.True(f.Sessions.AdoptReturnContinuationNativeShellForTest(f.Store, credentials));
        var player = new Player(); player.mecha.coreEnergy = 1e8; player.mecha.LoadedEnergy = 1e10;
        var state = new NormalGameActionCoordinator.ContactStateProbe(player, tick);
        GameMain.gameTick++;
        var support = new Spherewright.Contracts.Players.NativeNearGroundSupportSnapshot
        {
            State = "observed",
            CapturedAtGameTick = GameMain.gameTick,
            CurrentPoolReference = true,
            LiveObject = true,
            ColliderChain = true,
            FootOverlapComplete = null,
            ActualGroundSphereHit = false,
            FootOverlapCount = 0,
            AllFootHitsCurrentSupportOrTerrain = true
        };
        var facts = state.Facts(player, ReturnContinuationContactFacts(f, player, support), support);
        var code = state.Policy.ObserveUniverse(facts); Assert.NotNull(code);
        var cut = state.Policy.FailureEvidence!; Assert.Equal("contact_foot_query_incomplete", cut.FirstFailedPredicate);
        Assert.Equal("unknown", cut.Predicates[cut.FirstFailedPredicate]);
        f.Sessions.RetainReturnContactFailureForTest(cut); Assert.False(f.Sessions.CompleteContactForTest(code));
        Assert.True(GameMain.isPaused); var firstCut = PluginJson.Serialize(cut);
        Assert.Equal(firstCut, PluginJson.Serialize(f.Store.VoyagerJourney!.ReturnOnlyTask!.CurrentContinuation!.FirstFailure));
        Assert.True(coordinator.TryGetActionResultOnMainThread(result.Value!.ActionId, out var failed));
        Assert.True(failed!.Terminal); Assert.False(failed.Succeeded); Assert.Equal(firstCut, PluginJson.Serialize(failed.NativeFirstFailure));
        // Already entered601 is paid and can finish after Pause. This is a
        // declared Native contract substitute, not an extra allowed work tick.
        var perTick = f.NormalActions.ContractMovementCostForTest().PerNativeTickEnergyJoules!.Value;
        player.mecha.coreEnergy -= perTick; player.mecha.LoadedEnergy -= perTick; GameMain.gameTick++;
        UnityEngine.Time.frameCount++; f.Sessions.ObservePausedOwnedSaveOnMainThread();
        UnityEngine.Time.frameCount++; f.Sessions.ObservePausedOwnedSaveOnMainThread();
        Assert.True(f.Sessions.TryBindPausedOwnedSave(out var binding, out var rejection), rejection);
        var previous = GameSave.FlightStartSave; GameSave.FlightStartSave = name => { Fixture.WriteSave(name, GameMain.gameTick); return true; };
        try { Assert.True(f.Sessions.TrySaveOwnedWorldNowOnMainThread(out var error, binding), error); }
        finally { GameSave.FlightStartSave = previous; }
        Assert.True(GameMain.isPaused); Assert.Equal(WriteHealthStates.Quarantined, f.Sessions.PreservationWriteHealth);
        Assert.Null(credentials.CurrentResumeToken); // consumed; preservation cannot issue a capability
        Assert.Equal(GameMain.gameTick, f.Store.VoyagerJourney!.ReturnOnlyTask!.PreservationReceipt!.GameTick);
        Assert.Equal(firstCut, PluginJson.Serialize(f.Store.VoyagerJourney.ReturnOnlyTask.CurrentContinuation!.FirstFailure));
        Assert.True(coordinator.TryGetActionResultOnMainThread(result.Value.ActionId, out failed));
        Assert.Equal(firstCut, PluginJson.Serialize(failed!.NativeFirstFailure)); Assert.False(failed.Succeeded);
        Assert.Equal(2, DSPGame.DispatchCalls);
    }

    [Fact]
    public void ReturnContinuationTraversesRealStoresStrictIdleSaveReturnAndHomeSaveWithoutTickRefund()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var first = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(first.ActionId).Succeeded);
        var tick = f.Ticket.SavedGameTick + 39; Fixture.WriteSave(GameSave.LastExit, tick);
        var credentials = ReturnContinuationCredentials(f, tick);
        var coordinator = new OwnedWorldResumeCoordinator(true, 60, 30, 32, f.Sessions, credentials);
        var proposal = coordinator.PrepareOnMainThread(new()
        {
            ResumeToken = credentials.CurrentResumeToken!,
            RecoveryMode = OwnedWorldResumeModes.ResumeReturnOnlyTask,
            RecoveryScope = VoyagerReturnOnlyContinuationPolicy.Scope,
            ExpectedRecoveryGameTick = tick,
            RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(20)
        });
        Assert.True(proposal.Success, proposal.Error?.Message);
        var committed = coordinator.CommitOnMainThread(new()
        {
            PlanToken = proposal.Value!.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D"),
            UserConfirmedInConversation = true,
            ConfirmationDigest = proposal.Value.ConfirmationDigest
        });
        Assert.True(committed.Success, committed.Error?.Message);
        Assert.True(f.Sessions.AdoptReturnContinuationNativeShellForTest(f.Store, credentials),
            f.Sessions.ReturnOnlySessionBindingFailure?.FirstFailedPredicate);
        f.Sessions.BindReturnContinuationResaveGuardForTest(f.NormalActions);
        f.NormalActions.ResetIdleNativeShellForTest();
        Assert.Equal(53961, f.Store.VoyagerJourney!.RemainingReturnOnlyTicks(f.Sessions.SessionId, tick));
        TraverseReturnContinuationNativeContract(f);
        var idle = f.NormalActions.ObserveRestoredReturnIdleForTest(); Assert.True(idle.Proved, idle.BlockerCode);
        Assert.True(f.Sessions.CompleteContactForTest(idle.Proved ? null : idle.BlockerCode));
        Assert.Equal("completed_health", f.Store.VoyagerJourney!.ReturnOnlyTask!.CurrentContinuation!.State);
        var save = GameSave.FlightStartSave;
        GameSave.FlightStartSave = name => { Fixture.WriteSave(name, GameMain.gameTick); return true; };
        try { Assert.True(f.Sessions.TrySaveOwnedWorldNowOnMainThread(out var error), error); }
        finally { GameSave.FlightStartSave = save; }
        Assert.True(coordinator.TryGetActionResultOnMainThread(committed.Value!.ActionId, out var completed));
        Assert.True(completed!.Terminal); Assert.True(completed.Succeeded, completed.Message);
        Assert.Equal(53958, f.Store.VoyagerJourney!.RemainingReturnOnlyTicks(f.Sessions.SessionId, GameMain.gameTick));
        Assert.Equal(first.ActionId, f.Store.VoyagerJourney.ReturnOnlyTask!.TaskId);
        Assert.True(f.ReadAction(first.ActionId).Succeeded);

        // The same controller now owns successive Native501 observations.
        // Flight/landing and Data.Export remain declared Native substitutes.
        for (var i = 0; i < 3; i++) { GameMain.gameTick++; f.NormalActions.RunNativeIdleGuardForTest(); }
        Assert.False(GameMain.isPaused);
        var original = f.Store.VoyagerJourney!; var task = PluginJson.Serialize(original.ReturnOnlyTask);
        var session = f.Sessions.SessionId!;
        var flight = new NormalGameActionCoordinator.ActionRecord
        {
            SessionId = session,
            PlanetId = original.RemotePlanetId!.Value,
            Plan = new()
            {
                VoyagerReturnLeg = true,
                VoyagerJourneyId = original.JourneyId,
                DestinationPlanetId = original.HomePlanetId,
                VoyagerBudget = new() { OriginStarId = original.DestinationStarId, DestinationStarId = original.HomeStarId }
            },
        };
        f.Sessions.Snapshot.Revision = 1;
        f.Sessions.FlightStartLoadedFromCheckpoint = false;
        GameSave.FlightStartSave = name => { Fixture.WriteSave(name, GameMain.gameTick); return true; };
        try
        {
            f.NormalActions.StartVoyagerForTest(flight);
            Assert.False(flight.Terminal, flight.Message); Assert.Equal(1, f.NormalActions.NativeFlightEntries);
            f.NormalActions.RunNativeIdleGuardForTest(); Assert.False(GameMain.isPaused);
            Assert.Equal("return", f.Store.VoyagerJourney!.Phase);
            Assert.Equal(task, PluginJson.Serialize(f.Store.VoyagerJourney.ReturnOnlyTask));
            Assert.True(flight.VoyagerDeadlineGameTick + VoyagerReturnOnlyRecoveryPolicy.StopAndTailGameTicks
                <= original.ReturnOnlyTask!.StartedAtGameTick + original.ReturnOnlyTask.MaximumGameTicks);
            GameMain.gameTick += 1100;
            var home = f.Store.VoyagerJourney!;
            Assert.True(f.Store.TryMarkFlightSucceeded(home.ActiveCheckpointId, flight.ActionId, GameMain.gameTick, out var error), error);
            home.Phase = "home_awaiting_save"; home.LastLandedGameTick = GameMain.gameTick; home.SpentGameTicks += 1100;
            Assert.True(f.Store.TryPersistVoyagerJourney(home, out error), error);
            GameMain.localPlanet!.id = GameMain.mainPlayer.planetId = original.HomePlanetId;
            f.Sessions.Snapshot.LocalPlanetId = original.HomePlanetId;
            Assert.True(f.Sessions.TrySaveOwnedWorldNowOnMainThread(out error), error);
            Assert.True(f.Store.TryCompleteVoyagerAfterOwnedSave(original.JourneyId, Fixture.NormalSaveActionId,
                session, original.HomePlanetId, GameMain.gameTick, f.PrimaryFingerprint(), out error), error);
            var closed = f.ReopenStore().VoyagerJourney!;
            Assert.Equal("completed", closed.Phase); Assert.False(closed.ReturnOnlyScopeActive);
            Assert.Equal(original.DeadlineUtc, closed.DeadlineUtc); Assert.Equal(original.MaximumGameTicks, closed.MaximumGameTicks);
            Assert.Equal(original.ReturnEnergyFloorJoules, closed.ReturnEnergyFloorJoules);
            Assert.Equal(task, PluginJson.Serialize(closed.ReturnOnlyTask));
            Assert.True(coordinator.TryGetActionResultOnMainThread(committed.Value.ActionId, out var stillCompleted));
            Assert.True(stillCompleted!.Succeeded); Assert.Equal(2, DSPGame.DispatchCalls);
        }
        finally { GameSave.FlightStartSave = save; }
    }
}
