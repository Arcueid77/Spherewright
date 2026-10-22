using BepInEx.Logging;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Sessions;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    [Fact]
    public void PreservationReceiptClosesTheTailGapWithoutRenewingTheCredentialOrBorrowingANewerTick()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        f.NormalActions.NativeIdleObservation.GroundSphereHit = null; GameMain.gameTick++; f.NormalActions.RunNativeIdleGuardForTest();
        var stoppedTick = GameMain.gameTick; var credentials = ReturnContinuationCredentials(f, stoppedTick);
        var token = credentials.CurrentResumeToken!;
        // Declared Native29151 contract substitute: the already entered601
        // can finish after Pause. No actual game state is written by this test.
        GameMain.gameTick++;
        UnityEngine.Time.frameCount++; f.Sessions.ObservePausedOwnedSaveOnMainThread();
        UnityEngine.Time.frameCount++; f.Sessions.ObservePausedOwnedSaveOnMainThread();
        Assert.True(f.Sessions.TryBindPausedOwnedSave(out var binding, out var rejection), rejection);
        var previous = GameSave.FlightStartSave;
        GameSave.FlightStartSave = name => { Fixture.WriteSave(name, GameMain.gameTick); return true; };
        try { Assert.True(f.Sessions.TrySaveOwnedWorldNowOnMainThread(out var error, binding), error); }
        finally { GameSave.FlightStartSave = previous; }
        var receipt = f.Store.VoyagerJourney!.ReturnOnlyTask!.PreservationReceipt!;
        Assert.Equal(stoppedTick + 1, receipt.GameTick); Assert.Equal(token, credentials.CurrentResumeToken);
        File.Copy(f.PrimaryPath, GameSave.SavePath(GameSave.LastExit));
        Assert.True(f.Store.TryOpenReturnOnlyContinuationSource(credentials, token, DateTimeOffset.UtcNow.AddMinutes(20),
            out var evidence, out var failure), failure?.Code);
        evidence!.Dispose();
        // Only this isolated synthetic fixture: a second valid export at the
        // same tick has a different Native saved-at timestamp and full hash.
        using (var changed = new FileStream(f.PrimaryPath, FileMode.Open, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(changed))
        {
            changed.Position = 44;
            writer.Write(new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc).Ticks);
        }
        var changedAccepted = f.Store.TryOpenReturnOnlyContinuationSource(credentials, token, DateTimeOffset.UtcNow.AddMinutes(20),
            out var changedEvidence, out failure); changedEvidence?.Dispose();
        Assert.False(changedAccepted);
        Assert.Equal("preservation_receipt_bound", failure!.FirstFailedPredicate);
        Assert.Equal("not_reached", failure.Predicates["native_load"]);
    }

    [Fact]
    public void ConfirmedReturnContinuationMustRegisterOneOriginalLoadAndKeepItsExistingTickGrant()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        var tick = f.Ticket.SavedGameTick + 39; Fixture.WriteSave(GameSave.LastExit, tick);
        var credentials = ReturnContinuationCredentials(f, tick);
        var coordinator = new OwnedWorldResumeCoordinator(true, 60, 30, 32, f.Sessions, credentials);
        var p = coordinator.PrepareOnMainThread(new()
        {
            ResumeToken = credentials.CurrentResumeToken!,
            RecoveryMode = OwnedWorldResumeModes.ResumeReturnOnlyTask,
            RecoveryScope = VoyagerReturnOnlyContinuationPolicy.Scope,
            ExpectedRecoveryGameTick = tick,
            RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(20)
        });
        Assert.True(p.Success, p.Error?.Message);
        var request = new CommitOwnedWorldResumeRequest
        {
            PlanToken = p.Value!.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D"),
            UserConfirmedInConversation = true,
            ConfirmationDigest = p.Value.ConfirmationDigest
        };
        var dispatches = DSPGame.DispatchCalls;
        var result = coordinator.CommitOnMainThread(request);
        Assert.True(result.Success, result.Error?.Message); Assert.True(result.Value!.Accepted);
        Assert.Equal(dispatches + 1, DSPGame.DispatchCalls);
        var task = f.Store.VoyagerJourney!.ReturnOnlyTask!;
        Assert.Equal(accepted.ActionId, task.TaskId); Assert.Equal(54000, task.MaximumGameTicks);
        Assert.Equal(result.Value.ActionId, task.CurrentContinuation!.ActionId);
        Assert.Equal(tick, task.CurrentContinuation.SourceGameTick);
        var repeated = coordinator.CommitOnMainThread(request);
        Assert.True(repeated.Success); Assert.True(repeated.Value!.IdempotentReplay);
        Assert.Equal(result.Value.ActionId, repeated.Value.ActionId); Assert.Equal(dispatches + 1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void ExistingReturnTaskPrepareDisclosesItsRemainingBudgetWithoutAnyNativeLoad()
    {
        // Main-menu/Unity readiness remains the existing declared substitute.
        // This composition executes the production coordinator, stores, full
        // source reader, Journal validation, policy and disclosure generation.
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        var tick = f.Ticket.SavedGameTick + 39; Fixture.WriteSave(GameSave.LastExit, tick);
        var credentials = ReturnContinuationCredentials(f, tick);
        var coordinator = new OwnedWorldResumeCoordinator(true, 60, 30, 32, f.Sessions, credentials);
        var before = File.ReadAllBytes(f.JourneyPath); var dispatches = DSPGame.DispatchCalls;
        var result = coordinator.PrepareOnMainThread(new()
        {
            ResumeToken = credentials.CurrentResumeToken!,
            RecoveryMode = OwnedWorldResumeModes.ResumeReturnOnlyTask,
            RecoveryScope = VoyagerReturnOnlyContinuationPolicy.Scope,
            ExpectedRecoveryGameTick = tick,
            RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(20)
        });
        Assert.True(result.Success, result.Error?.Message);
        var plan = result.Value!; Assert.True(plan.UserConfirmationRequired); Assert.True(plan.NewChargingProhibited);
        var preview = Assert.IsType<ReturnOnlyContinuationPreview>(plan.ReturnOnlyContinuation);
        Assert.Equal(accepted.ActionId, preview.OriginalTaskId); Assert.Equal(53961, preview.RemainingGameTicks);
        Assert.False(preview.NewTicksGranted); Assert.True(preview.NewWallAuthorityRequired);
        Assert.Equal(dispatches, DSPGame.DispatchCalls); Assert.Equal(before, File.ReadAllBytes(f.JourneyPath));
        Assert.False(coordinator.CommitOnMainThread(new()
        {
            PlanToken = plan.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D")
        }).Success);
        Assert.Equal(dispatches, DSPGame.DispatchCalls);
    }

    [Fact]
    public void ContinuationRebindsANewSessionWithoutResettingTicksOrChangingTheOriginalGrant()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        var old = f.Store.VoyagerJourney!; var original = old.ReturnOnlyTask!;
        var tick = f.Ticket.SavedGameTick + 39; Fixture.WriteSave(GameSave.LastExit, tick);
        var credentials = ReturnContinuationCredentials(f, tick);
        Assert.True(f.Store.TryOpenReturnOnlyContinuationSource(credentials, credentials.CurrentResumeToken!,
            DateTimeOffset.UtcNow.AddMinutes(20), out var proof, out var failure), failure?.Code);
        using var source = Assert.IsType<VoyagerReturnOnlyContinuationSource>(proof);
        var action = Guid.NewGuid().ToString("D"); var before = File.ReadAllBytes(f.JourneyPath);
        Assert.False(f.Store.TryRegisterReturnOnlyContinuation(source, action, false, source.Fingerprint, DateTimeOffset.UtcNow, out _));
        Assert.False(f.Store.TryRegisterReturnOnlyContinuation(source, action, true, "changed", DateTimeOffset.UtcNow, out _));
        Assert.False(f.Store.TryRegisterReturnOnlyContinuation(source, original.TaskId, true, source.Fingerprint, DateTimeOffset.UtcNow, out _));
        Assert.False(f.Store.TryRegisterReturnOnlyContinuation(source, original.OriginalFlightActionId, true, source.Fingerprint, DateTimeOffset.UtcNow, out _));
        Assert.Equal(before, File.ReadAllBytes(f.JourneyPath));
        Assert.True(f.Store.TryRegisterReturnOnlyContinuation(source, action, true, source.Fingerprint, DateTimeOffset.UtcNow, out var rejection), rejection);
        Assert.Null(f.Store.VoyagerJourney!.RemainingReturnOnlyTicks(original.SessionId, tick));
        var session = Guid.NewGuid().ToString("D");
        Assert.False(f.Store.TryBindReturnOnlyContinuationSession(action, original.SessionId, tick + 2, out _));
        Assert.True(f.Store.TryBindReturnOnlyContinuationSession(action, session, tick + 2, out rejection), rejection);
        var reopened = f.ReopenStore().VoyagerJourney!; var task = reopened.ReturnOnlyTask!;
        Assert.Equal(53959, reopened.RemainingReturnOnlyTicks(session, tick + 2));
        Assert.Equal(original.TaskId, task.TaskId); Assert.Equal(original.SessionId, task.SessionId);
        Assert.Equal(original.StartedAtGameTick, task.StartedAtGameTick); Assert.Equal(original.MaximumGameTicks, task.MaximumGameTicks);
        Assert.Equal(original.IssuedAtUtc, task.IssuedAtUtc); Assert.Equal(original.DeadlineUtc, task.DeadlineUtc);
        Assert.Equal(old.SpentGameTicks, reopened.SpentGameTicks); Assert.Equal(old.DeadlineUtc, reopened.DeadlineUtc);
        Assert.False(f.Store.TryRegisterReturnOnlyContinuation(source, Guid.NewGuid().ToString("D"), true, source.Fingerprint, DateTimeOffset.UtcNow, out _));
        var changed = reopened; changed.ReturnOnlyTask!.CurrentContinuation!.DeadlineUtc = source.DeadlineUtc.AddSeconds(1);
        Assert.False(f.Store.TryPersistVoyagerJourney(changed, out _));
    }

    [Fact]
    public void UnknownPreloadConsumptionPreservesTheAttemptAndCannotBeRekeyedIntoAnotherLoad()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var first = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint(); Assert.True(f.ReadAction(first.ActionId).Succeeded);
        var tick = f.Ticket.SavedGameTick + 39; Fixture.WriteSave(GameSave.LastExit, tick);
        var credentials = ReturnContinuationCredentials(f, tick); var token = credentials.CurrentResumeToken!;
        Assert.True(f.Store.TryOpenReturnOnlyContinuationSource(credentials, token, DateTimeOffset.UtcNow.AddMinutes(20),
            out var source, out var failure), failure?.Code);
        var action = Guid.NewGuid().ToString("D");
        Assert.True(f.Store.TryRegisterReturnOnlyContinuation(source!, action, true, source!.Fingerprint, DateTimeOffset.UtcNow, out _));
        source.Dispose(); var dispatches = DSPGame.DispatchCalls;
        var cut = f.Sessions.RecordReturnOnlyPreloadConsumptionFailure(action);
        Assert.Equal("unknown", cut.Predicates["durable_credential_consumption"]);
        Assert.Equal("not_reached", cut.Predicates["native_load"]); Assert.Equal("not_observed", cut.FactsState);
        Assert.Equal("outcome_unknown", f.ReopenStore().VoyagerJourney!.ReturnOnlyTask!.CurrentContinuation!.State);
        Assert.False(f.Store.TryOpenReturnOnlyContinuationSource(credentials, token, DateTimeOffset.UtcNow.AddMinutes(20), out _, out failure));
        Assert.Equal("no_pending_recovery", failure!.FirstFailedPredicate); Assert.Equal(dispatches, DSPGame.DispatchCalls);
        Assert.Equal(first.ActionId, f.Store.VoyagerJourney!.ReturnOnlyTask!.TaskId);
        Assert.Equal(54000, f.Store.VoyagerJourney.ReturnOnlyTask.MaximumGameTicks);
    }

    [Fact]
    public void ADisposedSourceCannotActivateAndChangedDurableRecordsAreNotRecertified()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        var minimum = f.Ticket.SavedGameTick + 39; Fixture.WriteSave(GameSave.LastExit, minimum);
        var credentials = ReturnContinuationCredentials(f, minimum);
        Assert.True(f.Store.TryOpenReturnOnlyContinuationSource(credentials, credentials.CurrentResumeToken!,
            DateTimeOffset.UtcNow.AddMinutes(20), out var proof, out var failure), failure?.Code);
        var source = Assert.IsType<VoyagerReturnOnlyContinuationSource>(proof); source.Dispose();
        Assert.False(f.Store.TryRegisterReturnOnlyContinuation(source, Guid.NewGuid().ToString("D"), true,
            source.Fingerprint, DateTimeOffset.UtcNow, out _));
        var durable = File.ReadAllText(f.JourneyPath); File.WriteAllText(f.JourneyPath, "{unexamined");
        Assert.False(f.Store.TryOpenReturnOnlyContinuationSource(credentials, credentials.CurrentResumeToken!,
            DateTimeOffset.UtcNow.AddMinutes(20), out _, out failure));
        Assert.Equal("return_only_continuation_files_unproved", failure!.Code);
        Assert.Equal("not_reached", failure.Predicates["native_load"]);
        Assert.Equal("{unexamined", File.ReadAllText(f.JourneyPath));
        File.WriteAllText(f.JourneyPath, durable); // isolated synthetic fixture only
    }

    private static OwnedWorldResumeTicketStore ReturnContinuationCredentials(Fixture f, long minimum,
        string? sessionOverride = null)
    {
        var j = f.Store.VoyagerJourney!;
        var runtime = Path.Combine(f.Handoff, "continuation-runtime"); Directory.CreateDirectory(runtime);
        var id = GameplayJournalIdentity.HashOwnedSaveIdentity(Fixture.Identity);
        var checkpoint = new OwnedWorldGameplayJournalCheckpoint
        {
            Version = 1,
            JournalId = id,
            TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave,
            TrackingStartedAtGameTick = 0,
            HistoricalCoverageComplete = true,
            MinimumDurableThroughSequence = 1
        };
        var path = Path.Combine(runtime, "journals", "gameplay-" + id + ".json"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, PluginJson.Serialize(new GameplayJournalDocument
        {
            Version = 1,
            JournalId = id,
            OwnedSaveIdentityHash = id,
            GameVersion = j.GameVersion,
            TrackingMode = checkpoint.TrackingMode,
            TrackingStartedAtGameTick = 0,
            HistoricalCoverageComplete = true,
            Entries = new List<GameplayJournalEntry> { new() { Sequence = 1 } }
        }));
        var credentials = new OwnedWorldResumeTicketStore(runtime, f.Handoff, "synthetic-continuation", j.GameVersion, new ManualLogSource());
        credentials.ArmFromQuarantinedOwnedSession(Fixture.Identity, sessionOverride ?? j.ReturnOnlyTask!.SessionId,
            j.RemotePlanetId!.Value, minimum, j.ReturnOnlyTask!.OriginalFlightActionId, checkpoint);
        return credentials;
    }

    [Fact]
    public void ReturnSourceBindsRealCredentialJournalAndCompleteFilesWithoutLoadingOrChangingTheGrant()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        var sourceTick = f.Ticket.SavedGameTick + 39;
        Fixture.WriteSave(GameSave.LastExit, sourceTick);
        var credentials = ReturnContinuationCredentials(f, sourceTick);
        var record = File.ReadAllBytes(f.JourneyPath); var cp = File.ReadAllBytes(f.TicketPath);
        var token = credentials.CurrentResumeToken!; var dispatches = DSPGame.DispatchCalls;
        Assert.True(f.Store.TryOpenReturnOnlyContinuationSource(credentials, token, DateTimeOffset.UtcNow.AddMinutes(20),
            out var proof, out var failure), failure?.Code);
        using (var opened = Assert.IsType<VoyagerReturnOnlyContinuationSource>(proof))
        {
            Assert.Equal(53961, opened.RemainingGameTicks);
            Assert.True(opened.RequiresNewWallAuthority);
            Assert.Equal(sourceTick, opened.Source.Prefix.GameTick);
            Assert.Equal(accepted.ActionId, opened.Journey.ReturnOnlyTask!.TaskId);
            Assert.Equal(record, File.ReadAllBytes(f.JourneyPath)); Assert.Equal(cp, File.ReadAllBytes(f.TicketPath));
            Assert.Equal(token, credentials.CurrentResumeToken); Assert.Equal(dispatches, DSPGame.DispatchCalls);
        }
    }

    [Theory]
    [InlineData("older", "source_equals_credential_minimum")]
    [InlineData("older_branch_larger_tick", "source_equals_credential_minimum")]
    [InlineData("newer_primary", "primary_not_newer")]
    [InlineData("other_session", "task_session")]
    public void ReturnSourceRefusesRollbackAnotherBranchAndAnotherSessionBeforeLoad(string change, string predicate)
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        var minimum = f.Ticket.SavedGameTick + 39;
        Fixture.WriteSave(GameSave.LastExit, change == "older" ? minimum - 1 : change == "older_branch_larger_tick" ? minimum + 10000 : minimum);
        if (change == "newer_primary") Fixture.WriteSave(Fixture.Identity, minimum + 1);
        var credentials = ReturnContinuationCredentials(f, minimum, change == "other_session" ? Guid.NewGuid().ToString("D") : null);
        var before = File.ReadAllBytes(f.JourneyPath); var dispatches = DSPGame.DispatchCalls;
        Assert.False(f.Store.TryOpenReturnOnlyContinuationSource(credentials, credentials.CurrentResumeToken!,
            DateTimeOffset.UtcNow.AddMinutes(20), out var proof, out var failure));
        Assert.Null(proof); Assert.Equal(predicate, failure!.FirstFailedPredicate);
        Assert.Equal("not_reached", failure.Predicates["native_load"]);
        Assert.Equal(before, File.ReadAllBytes(f.JourneyPath)); Assert.Equal(dispatches, DSPGame.DispatchCalls);
    }
}
