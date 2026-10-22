using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    // Real Store + reload coordinator + flight-start methods + scope + expiry.
    // Native Load/adoption, PhysX, flight/landing, Pause and file export remain
    // explicit substitutes, never claimed to prove Unity or an actual return.
    [Fact]
    public void ColdReturnCanUseOneNewTaskWithoutRefundingTheOldLedgerOrChangingResourceFloors()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var source = f.Store.VoyagerJourney!;
        Assert.True(source.DeadlineUtc < DateTimeOffset.UtcNow);
        var proposal = NewReturnOnlyProposal(f);
        var accepted = DispatchReturnOnly(f, proposal);
        Assert.Null(f.Store.VoyagerJourney!.ReturnOnlyTask);
        f.AdoptExactCheckpoint();
        var result = f.ReadAction(accepted.ActionId);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, DSPGame.DispatchCalls);
        var after = f.ReopenStore().VoyagerJourney!;
        Assert.Equal(source.MaximumGameTicks, after.SpentGameTicks);
        Assert.True(after.SpentGameTicksIsConservative);
        Assert.Equal(source.MaximumGameTicks, after.MaximumGameTicks);
        Assert.Equal(source.DeadlineUtc, after.DeadlineUtc);
        Assert.Equal(source.ReturnReserveGameTicks, after.ReturnReserveGameTicks);
        Assert.Equal(source.ReturnEnergyFloorJoules, after.ReturnEnergyFloorJoules);
        Assert.Equal(source.WithdrawalReserveJoules, after.WithdrawalReserveJoules);
        Assert.Equal(source.ReturnCoreEnergyMinimumJoules, after.ReturnCoreEnergyMinimumJoules);
        Assert.Equal(source.ExtraWarperReserve, after.ExtraWarperReserve);
        var task = Assert.IsType<VoyagerReturnOnlyTaskRecord>(after.ReturnOnlyTask);
        Assert.Equal(PluginJson.Serialize(source), task.OriginalJourneyJson);
        Assert.Equal(accepted.ActionId, task.TaskId);
        Assert.Equal(source.ActiveActionId, task.OriginalFlightActionId);
        Assert.Equal(proposal.DeadlineUtc, task.DeadlineUtc);
        Assert.Equal(53999L, after.RemainingReturnOnlyTicks(task.SessionId, f.Ticket.SavedGameTick + 1));
        Assert.Equal(53000L, after.RemainingReturnOnlyTicks(task.SessionId, f.Ticket.SavedGameTick + 1000));
        Assert.Equal(0L, after.RemainingReturnOnlyTicks(task.SessionId, f.Ticket.SavedGameTick + 54001));
        Assert.Null(after.RemainingReturnOnlyTicks(task.SessionId, null));
        Assert.Null(after.RemainingReturnOnlyTicks(task.SessionId, f.Ticket.SavedGameTick - 1));
        Assert.Null(after.RemainingReturnOnlyTicks("another-session", f.Ticket.SavedGameTick + 1));
        var sourceArchive = Assert.Single(Directory.GetFiles(f.Handoff, "voyager-return-source-*.json"));
        Assert.Equal(PluginJson.Serialize(source), File.ReadAllText(sourceArchive));

        // Re-observation never dispatches Native again or changes the grant.
        var before = File.ReadAllBytes(f.JourneyPath);
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        Assert.Equal(before, File.ReadAllBytes(f.JourneyPath));

        f.Sessions.BindReturnOnlyGuardForTest(f.Store);
        var previousSave = GameSave.FlightStartSave;
        GameSave.FlightStartSave = name => { Fixture.WriteSave(name, GameMain.gameTick); return true; };
        try
        {
        // Real ordinary-save implementation, fixture Data.Export/prefix only.
        // Optional healthy Journal credential generation is not modeled here.
        Assert.True(f.Sessions.TrySaveOwnedWorldNowOnMainThread(out var saveError), saveError);
        Assert.False(f.Store.TryValidateCheckpointFile(f.Ticket, out _)); // Fresh newer-primary check retires a recovered ticket.
        Assert.Equal(FlightCheckpointLifecycleStates.Retired,
            PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(f.TicketPath))!.LifecycleState);

            f.Sessions.Snapshot.Revision = 1;
            f.Sessions.FlightStartCheckpointId = f.Ticket.CheckpointId;
            f.Sessions.FlightStartLoadedFromCheckpoint = true;
            GameMain.gameTick = f.Ticket.SavedGameTick + 1000;
            GameMain.mainPlayer = new();
            GameMain.localPlanet = new() { id = f.Ticket.OriginPlanetId };
            var flight = new NormalGameActionCoordinator.ActionRecord
            {
                SessionId = task.SessionId,
                PlanetId = f.Ticket.OriginPlanetId,
                Plan = new()
                {
                    VoyagerReturnLeg = true,
                    VoyagerJourneyId = source.JourneyId,
                    DestinationPlanetId = source.HomePlanetId,
                    VoyagerBudget = new() { OriginStarId = source.DestinationStarId, DestinationStarId = source.HomeStarId },
                },
            };
            var controller = f.NormalActions;
            controller.StartVoyagerForTest(flight);
            Assert.False(flight.Terminal, flight.Message);
            Assert.Equal(1, controller.NativeFlightEntries);
            controller.RunNativeIdleGuardForTest();
            Assert.False(GameMain.isPaused); // the real started return owns the next tick
            Assert.True(flight.VoyagerDeadlineGameTick + VoyagerReturnOnlyRecoveryPolicy.StopAndTailGameTicks
                <= f.Ticket.SavedGameTick + proposal.MaximumGameTicks);
            Assert.Equal(source.MaximumGameTicks + 1000, f.Store.VoyagerJourney!.SpentGameTicks);
            Assert.Equal(PluginJson.Serialize(task), PluginJson.Serialize(f.Store.VoyagerJourney.ReturnOnlyTask));
            Assert.Equal("return", f.Store.VoyagerJourney.Phase);
            Assert.NotEqual(f.Ticket.CheckpointId, f.Store.CurrentTicket!.CheckpointId);
            GameMain.gameTick += 1100;
            var returned = f.Store.VoyagerJourney!;
            Assert.True(f.Store.TryMarkFlightSucceeded(returned.ActiveCheckpointId, flight.ActionId, GameMain.gameTick, out var rejection), rejection);
            returned.Phase = "home_awaiting_save"; returned.LastLandedGameTick = GameMain.gameTick;
            returned.SpentGameTicks += 1100;
            Assert.True(f.Store.TryPersistVoyagerJourney(returned, out rejection), rejection);
            GameMain.localPlanet.id = source.HomePlanetId;
            f.Sessions.Snapshot.LocalPlanetId = source.HomePlanetId;
            Assert.True(f.Sessions.TrySaveOwnedWorldNowOnMainThread(out saveError), saveError);
            var savedFingerprint = f.PrimaryFingerprint();
            Assert.True(f.Store.TryCompleteVoyagerAfterOwnedSave(source.JourneyId, Fixture.NormalSaveActionId, task.SessionId,
                source.HomePlanetId, GameMain.gameTick, savedFingerprint, out rejection), rejection);
            var closed = f.ReopenStore().VoyagerJourney!;
            Assert.Equal("completed", closed.Phase);
            Assert.False(closed.ReturnOnlyScopeActive);
            Assert.Equal(task.OriginalJourneyJson, closed.ReturnOnlyTask!.OriginalJourneyJson);
            Assert.Equal(PluginJson.Serialize(source), File.ReadAllText(sourceArchive));
            Assert.Equal(1, DSPGame.DispatchCalls);
        }
        finally { GameSave.FlightStartSave = previousSave; }
    }

    [Theory]
    [InlineData("loss_not_accepted", "explicit_progress_loss_acceptance")]
    [InlineData("excess_ticks", "new_tick_cap_maximum")]
    [InlineData("excess_wall", "new_window_finite")]
    [InlineData("expired", "new_window_not_expired")]
    [InlineData("wrong_checkpoint", "exact_approved_checkpoint")]
    [InlineData("wrong_tick", "exact_approved_checkpoint")]
    [InlineData("wrong_file", "exact_approved_checkpoint")]
    [InlineData("wrong_original_action", "exact_approved_checkpoint")]
    [InlineData("not_main_menu", "idle_main_menu")]
    [InlineData("not_armed", "native_launch_was_armed")]
    public void NewTaskRejectsInvalidAuthorityOrSourceBeforeAnyNativeLoad(string changed, string predicate)
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active");
        var p = NewReturnOnlyProposal(f);
        switch (changed) {
            case "loss_not_accepted": p.AcceptUnstoredProgressLoss = false; break;
            case "excess_ticks": p.MaximumGameTicks++; break;
            case "excess_wall": p.DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(1201); break;
            case "expired": p.DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(-1); break;
            case "wrong_checkpoint": p.ExpectedCheckpointId += "other"; break;
            case "wrong_tick": p.ExpectedCheckpointGameTick++; break;
            case "wrong_file": p.ExpectedCheckpointFileFingerprint += "other"; break;
            case "wrong_original_action": p.ExpectedFlightActionId += "other"; break;
            case "not_main_menu": f.Sessions.Snapshot.GameLoaded = true;
                Assert.True(f.Store.TryMarkRecoveryRequired(f.Ticket.CheckpointId, f.Ticket.LastAttemptActionId!, 120, out _)); break;
            case "not_armed": f.Ticket.NativeLaunchArmed = false; break;
        }
        var before = File.ReadAllBytes(f.JourneyPath);
        var prepared = f.Coordinator.PrepareOnMainThread(new() { ReloadToken = f.Ticket.ReloadToken, ReturnOnlyTask = p });
        Assert.False(prepared.Success);
        Assert.Equal(predicate, prepared.Error!.NativeFirstFailure!.FirstFailedPredicate);
        Assert.Equal("not_reached", prepared.Error.NativeFirstFailure.Predicates["native_return"]);
        Assert.Equal(before, File.ReadAllBytes(f.JourneyPath));
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Fact]
    public void MissingConfirmationOrChangedOriginalLedgerNeverDispatchesTheNewRecovery()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active");
        var p = NewReturnOnlyProposal(f);
        var first = f.Coordinator.PrepareOnMainThread(new() { ReloadToken = f.Ticket.ReloadToken, ReturnOnlyTask = p });
        Assert.True(first.Success, first.Error?.Message);
        Assert.False(f.Coordinator.CommitOnMainThread(new() { PlanToken = first.Value!.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D") }).Success);
        Assert.Equal(0, DSPGame.DispatchCalls);
        var second = f.Coordinator.PrepareOnMainThread(new() { ReloadToken = f.Ticket.ReloadToken, ReturnOnlyTask = p });
        Assert.True(second.Success, second.Error?.Message);
        var changed = f.Store.VoyagerJourney!; changed.SpentGameTicks++;
        Assert.True(f.Store.TryPersistVoyagerJourney(changed, out _));
        var commit = f.Coordinator.CommitOnMainThread(new() { PlanToken = second.Value!.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D"), UserConfirmedInConversation = true });
        Assert.False(commit.Success);
        Assert.Equal(BridgeErrorCodes.StaleState, commit.Error!.Code);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData("tick_cap")] [InlineData("deadline")] [InlineData("source_tick")]
    [InlineData("session")] [InlineData("drop_task")] [InlineData("original_ledger")]
    public void DurableNewTaskCannotBeEditedOrRenewedAfterItWasGranted(string changed)
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active");
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f));
        f.AdoptExactCheckpoint(); Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        var original = File.ReadAllBytes(f.JourneyPath);
        var r = f.Store.VoyagerJourney!;
        switch (changed) {
            case "tick_cap": r.ReturnOnlyTask!.MaximumGameTicks--; break;
            case "deadline": r.ReturnOnlyTask!.DeadlineUtc = r.ReturnOnlyTask.DeadlineUtc.AddSeconds(1); break;
            case "source_tick": r.ReturnOnlyTask!.StartedAtGameTick--; break;
            case "session": r.ReturnOnlyTask!.SessionId = Guid.NewGuid().ToString("D"); break;
            case "drop_task": r.ReturnOnlyTask = null; break;
            case "original_ledger": r.ReturnOnlyTask!.OriginalJourneyJson += " "; break;
        }
        Assert.False(f.Store.TryPersistVoyagerJourney(r, out _));
        Assert.Equal(original, File.ReadAllBytes(f.JourneyPath));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GameGuardPausesWithoutAClientAndSaveRemainsAllowedWhileOtherWritesAreDenied(bool wallExpired)
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active");
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f));
        f.AdoptExactCheckpoint(); Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        f.Sessions.BindReturnOnlyGuardForTest(f.Store);
        var j = f.Store.VoyagerJourney!; var t = j.ReturnOnlyTask!;
        var before = File.ReadAllBytes(f.JourneyPath);
        f.Sessions.GuardReturnOnlyTaskOnMainThread(t.IssuedAtUtc);
        Assert.False(GameMain.isPaused);
        if (!wallExpired) GameMain.gameTick = t.StartedAtGameTick + t.MaximumGameTicks - 1;
        f.Sessions.GuardReturnOnlyTaskOnMainThread(wallExpired ? t.DeadlineUtc : t.IssuedAtUtc);
        Assert.True(GameMain.isPaused);
        f.Sessions.RefreshReturnOnlyScopeForTest();
        var state = f.Sessions.Snapshot;
        Assert.False(state.WritesAllowed);
        Assert.Equal(wallExpired ? "wall_budget_remaining" : "tick_budget_remaining",
            state.ReturnOnlyRecovery!.FirstFailure!.FirstFailedPredicate);
        Assert.Equal("native_pause_verified", state.ReturnOnlyRecovery.PauseOutcome);
        Assert.Null(NormalGameActionCoordinator.ReturnOnlyCommitAccessForTest(state, NormalActionKinds.Save, false));
        foreach (var kind in new[] { NormalActionKinds.Build, NormalActionKinds.BlueprintBuild, NormalActionKinds.Move,
            NormalActionKinds.SelectResearch, NormalActionKinds.ConfigureBuilding, NormalActionKinds.Refuel,
            NormalActionKinds.InterplanetaryFlight })
            Assert.Equal(VoyagerReturnOnlyRecoveryPolicy.ScopeBlocker,
                NormalGameActionCoordinator.ReturnOnlyCommitAccessForTest(state, kind, false)!.Code);
        state.WriteBlockers.Add(new() { Code = BridgeErrorCodes.WriteSubsystemQuarantined, Message = "other real blocker" });
        Assert.Equal(BridgeErrorCodes.WriteSubsystemQuarantined,
            NormalGameActionCoordinator.ReturnOnlyCommitAccessForTest(state, NormalActionKinds.Save, false)!.Code);
        var first = PluginJson.Serialize(state.ReturnOnlyRecovery.FirstFailure);
        GameMain.gameTick++; // Legitimate entered-frame tail, never a tick write in production.
        f.Sessions.GuardReturnOnlyTaskOnMainThread(t.DeadlineUtc.AddSeconds(1));
        f.Sessions.RefreshReturnOnlyScopeForTest();
        Assert.Equal(first, PluginJson.Serialize(state.ReturnOnlyRecovery!.FirstFailure));
        Assert.Equal(before, File.ReadAllBytes(f.JourneyPath));
    }

    [Fact]
    public void ANewerPrimaryStillPreventsAnExplicitReturnOnlyCheckpointRestore()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active");
        var p = NewReturnOnlyProposal(f);
        f.WriteSyntheticPrimaryProof(f.Ticket.SavedGameTick + 1);
        Assert.False(f.Coordinator.PrepareOnMainThread(new() { ReloadToken = f.Ticket.ReloadToken, ReturnOnlyTask = p }).Success);
        Assert.Equal(0, DSPGame.DispatchCalls);
        Assert.Null(f.Store.VoyagerJourney!.ReturnOnlyTask);
    }

    [Fact]
    public void SourceBudgetExhaustedDuringLoadRetainsItsSameDecisionFailureAndPausesWithoutASecondLoad()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active");
        var p = NewReturnOnlyProposal(f);
        var accepted = DispatchReturnOnly(f, p);
        f.AdoptExactCheckpoint();
        GameMain.isPaused = false;
        GameMain.gameTick = f.Ticket.SavedGameTick + p.MaximumGameTicks;
        f.Sessions.Snapshot.GameTick = GameMain.gameTick;
        var failed = f.ReadAction(accepted.ActionId);
        Assert.False(failed.Succeeded);
        Assert.Equal(NormalActionStates.OutcomeUnknown, failed.State);
        var cut = Assert.IsType<Spherewright.Contracts.Players.NativeResumeFailureSnapshot>(failed.NativeFirstFailure);
        Assert.Equal("minimum_return_ticks", cut.FirstFailedPredicate);
        Assert.Equal(GameMain.gameTick, cut.CapturedAtGameTick);
        Assert.Equal(0d, cut.Values["new_remaining_ticks"]);
        Assert.Equal("not_reached", cut.Predicates["native_return"]);
        Assert.True(GameMain.isPaused);
        Assert.Null(f.Store.VoyagerJourney!.ReturnOnlyTask);
        var first = PluginJson.Serialize(cut);
        GameMain.gameTick++; f.Sessions.Snapshot.GameTick = GameMain.gameTick;
        Assert.Equal(first, PluginJson.Serialize(f.ReadAction(accepted.ActionId).NativeFirstFailure));
        Assert.Equal(1, DSPGame.DispatchCalls);
        Assert.False(f.Coordinator.PrepareOnMainThread(new() { ReloadToken = f.Ticket.ReloadToken, ReturnOnlyTask = p }).Success);
    }

    [Fact]
    public void PreparedNewTaskIsDetachedAndPendingCommitCannotBeRekeyedOrDuplicated()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active");
        var p = NewReturnOnlyProposal(f);
        var first = f.Coordinator.PrepareOnMainThread(new() { ReloadToken = f.Ticket.ReloadToken, ReturnOnlyTask = p });
        var second = f.Coordinator.PrepareOnMainThread(new() { ReloadToken = f.Ticket.ReloadToken, ReturnOnlyTask = p });
        Assert.True(first.Success); Assert.True(second.Success);
        var originalDeadline = p.DeadlineUtc;
        p.DeadlineUtc = p.DeadlineUtc.AddHours(1);
        first.Value!.ReturnOnlyTask!.MaximumGameTicks = 1;
        var request = new CommitFlightCheckpointReloadRequest { PlanToken = first.Value.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D"), UserConfirmedInConversation = true };
        var accepted = f.Coordinator.CommitOnMainThread(request);
        Assert.True(accepted.Success);
        Assert.True(f.Coordinator.CommitOnMainThread(request).Value!.IdempotentReplay);
        Assert.False(f.Coordinator.CommitOnMainThread(new() { PlanToken = second.Value!.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D"), UserConfirmedInConversation = true }).Success);
        f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.Value!.ActionId).Succeeded);
        Assert.Equal(originalDeadline, f.Store.VoyagerJourney!.ReturnOnlyTask!.DeadlineUtc);
        Assert.Equal(54000, f.Store.VoyagerJourney.ReturnOnlyTask.MaximumGameTicks);
        Assert.Equal(1, DSPGame.DispatchCalls);
        Assert.Single(Directory.GetFiles(f.Handoff, "voyager-return-source-*.json"));
    }

    [Fact]
    public void OnlyTheBoundIdleTaskCanBeCancelledAndItsFirstFailureAndOriginalLedgerStayFixed()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active");
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f));
        f.AdoptExactCheckpoint(); Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        f.Sessions.BindReturnOnlyGuardForTest(f.Store);
        var t = f.Store.VoyagerJourney!.ReturnOnlyTask!;
        var before = File.ReadAllBytes(f.JourneyPath);
        Assert.False(f.Sessions.StopIdleReturnOnlyTask("other-task", t.SessionId, f.Ticket.OriginPlanetId));
        Assert.False(f.Sessions.StopIdleReturnOnlyTask(t.TaskId, "other-session", f.Ticket.OriginPlanetId));
        Assert.False(GameMain.isPaused);
        Assert.True(f.Sessions.StopIdleReturnOnlyTask(t.TaskId, t.SessionId, f.Ticket.OriginPlanetId));
        Assert.True(GameMain.isPaused);
        Assert.True(f.Sessions.ReturnOnlyTaskStopped);
        var cut = f.Sessions.ReturnOnlyFailureForTask(t.TaskId)!;
        Assert.Equal("task_continuation_authorized", cut.FirstFailedPredicate);
        var first = PluginJson.Serialize(cut);
        f.Sessions.GuardReturnOnlyTaskOnMainThread(t.DeadlineUtc.AddSeconds(1));
        Assert.Equal(first, PluginJson.Serialize(f.Sessions.ReturnOnlyFailureForTask(t.TaskId)));
        Assert.Equal(before, File.ReadAllBytes(f.JourneyPath));
    }

    private static VoyagerReturnOnlyRecoveryRequest NewReturnOnlyProposal(Fixture f)
    {
        Assert.True(f.Store.TryArmVoyagerNativeLaunch(f.Ticket.CheckpointId, f.Ticket.LastAttemptActionId!, out var rejection), rejection);
        f.Sessions.Snapshot.GameLoaded = false; f.Sessions.Snapshot.GameTick = null;
        return new() { ExpectedJourneyId = f.Record.JourneyId, ExpectedFlightActionId = f.Ticket.LastAttemptActionId!,
            ExpectedCheckpointId = f.Ticket.CheckpointId, ExpectedCheckpointGameTick = f.Ticket.SavedGameTick,
            ExpectedCheckpointFileFingerprint = f.Ticket.CheckpointFileFingerprint, MaximumGameTicks = 54000,
            DeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(19), AcceptUnstoredProgressLoss = true };
    }

    private static FlightCheckpointReloadResult DispatchReturnOnly(Fixture f, VoyagerReturnOnlyRecoveryRequest p)
    {
        var prepared = f.Coordinator.PrepareOnMainThread(new() { ReloadToken = f.Ticket.ReloadToken, ReturnOnlyTask = p });
        Assert.True(prepared.Success, prepared.Error?.Message);
        var commit = f.Coordinator.CommitOnMainThread(new() { PlanToken = prepared.Value!.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D"), UserConfirmedInConversation = true });
        Assert.True(commit.Success, commit.Error?.Message); Assert.True(commit.Value!.Accepted);
        return commit.Value;
    }
}
