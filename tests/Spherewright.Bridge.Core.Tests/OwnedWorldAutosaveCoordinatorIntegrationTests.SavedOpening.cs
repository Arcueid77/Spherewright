using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class OwnedWorldAutosaveCoordinatorIntegrationTests
{
    // Native substitutes below implement the observed loader event/Pause contract.
    // Store, prefix/full-file leases, coordinator, journal verification, durable
    // consumption and load/adoption guard are production code. This is not Unity.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DaysOldPrimaryOpensPausedWithoutContinuingAnyOldAutomation(bool quarantined)
    {
        using var f = new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        if (quarantined) SetOpenTicket(f, t => t.QuarantineActionId = Guid.NewGuid().ToString());
        var oldToken = f.Store.CurrentResumeToken!;
        var primary = File.ReadAllBytes(f.PrimaryPath);
        var journal = File.ReadAllBytes(f.JournalPath);
        Assert.False(f.Store.TryGetActiveTicket(oldToken, out _, out _));
        Assert.True(f.Store.TryGetSavedWorldOpenProvenance(oldToken, out var original, out _, out _));
        var coordinator = f.NewCoordinator();
        var prepared = coordinator.PrepareOnMainThread(OpenRequest(f));
        Assert.True(prepared.Success, prepared.Error?.Message);
        var plan = prepared.Value!;
        Assert.True(OwnedSavedWorldOpenPolicy.HasMatchingEcho(plan));
        Assert.Null(plan.EffectiveRecoveryDeadlineUtc);
        Assert.Equal(0, plan.MaximumChargeGameTicks);
        Assert.Equal(0, plan.DiscardedUnstoredProgressGameTicks);
        Assert.Equal(primary, File.ReadAllBytes(f.PrimaryPath));
        DSPGame.BeforeLoad = () =>
        {
            Assert.False(f.Store.TryGetSavedWorldOpenProvenance(oldToken, out _, out _, out _));
            Assert.Throws<IOException>(() => File.OpenWrite(f.PrimaryPath).Dispose());
            Assert.Throws<IOException>(() => File.OpenWrite(f.JournalPath).Dispose());
        };
        var commit = f.Confirmed(plan);
        var accepted = coordinator.CommitOnMainThread(commit);
        Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(Identity, DSPGame.LoadedName);
        Assert.True(f.Sessions.SavedOpenObserverPrepared);
        Assert.True(f.Sessions.SavedOpenObserverBound);
        Assert.False(f.Sessions.RemoteLoaderObserverPrepared);
        Assert.Null(f.Sessions.RemoteEvidence);
        Assert.Null(f.Sessions.RecoveryAuthorization);
        Assert.True(coordinator.CommitOnMainThread(commit).Value!.IdempotentReplay);
        CompleteOpenSubstitute(f, accepted.Value!.ActionId, original!.GameplayJournalCheckpoint!);
        f.Sessions.ResumeAdoptionError = "later event before first poll";
        Assert.True(coordinator.TryGetActionResultOnMainThread(accepted.Value.ActionId, out var result));
        Assert.True(result!.Terminal && result.Succeeded);
        Assert.Equal(NormalActionStates.Completed, result.State);
        Assert.Contains("automation remains stopped", result.Message);
        var newToken = f.Store.CurrentResumeToken!;
        Assert.NotEqual(oldToken, newToken);
        Assert.True(f.Store.TryGetSavedWorldOpenProvenance(newToken, out var opened, out _, out _));
        Assert.True(opened!.SavedWorldOpenOnly);
        Assert.False(f.Store.TryGetActiveTicket(newToken, out _, out _));
        Assert.False(f.Store.TryGetFixedAutosaveProvenance(newToken, out _, out _, out _));
        Assert.False(f.Store.TryGetExpiredPrimaryProvenance(newToken, out _, out _, out _));
        Assert.Equal(primary, File.ReadAllBytes(f.PrimaryPath));
        Assert.Equal(journal, File.ReadAllBytes(f.JournalPath));
        f.Sessions.ResumeAdoptionError = "a later unrelated event";
        Assert.True(coordinator.TryGetActionResultOnMainThread(accepted.Value.ActionId, out var repeated));
        Assert.True(repeated!.Succeeded);
        Assert.Equal(1, DSPGame.LoadCalls);
    }

    [Fact]
    public void ACompletedOpeningRemainsOpenableWeeksLaterButNeverBecomesAnAutomationPermit()
    {
        using var f = new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        Assert.True(f.Store.TryGetSavedWorldOpenProvenance(f.Token, out var original, out _, out _));
        var c = f.NewCoordinator();
        var p = c.PrepareOnMainThread(OpenRequest(f)).Value!;
        var committed = c.CommitOnMainThread(f.Confirmed(p));
        Assert.True(committed.Success, committed.Error?.Message);
        CompleteOpenSubstitute(f, committed.Value!.ActionId, original!.GameplayJournalCheckpoint!);
        SetOpenTicket(f, t =>
        {
            t.SourceProcessId = int.MaxValue;
            t.IssuedAtUtc = DateTimeOffset.UtcNow.AddDays(-30);
            t.ExpiresAtUtc = t.IssuedAtUtc.AddDays(1);
        });
        var token = f.Store.CurrentResumeToken!;
        Assert.False(f.Store.TryGetActiveTicket(token, out _, out _));
        Assert.True(f.Store.TryGetSavedWorldOpenProvenance(token, out _, out _, out _));
        var second = f.NewCoordinator().PrepareOnMainThread(OpenRequest(f));
        Assert.True(second.Success, second.Error?.Message);
        Assert.True(second.Value!.PreviousAutomationRemainsStopped);
        Assert.False(f.NewCoordinator().PrepareOnMainThread(new() { ResumeToken = token }).Success);
    }

    [Fact]
    public void UnstoredProgressLossRequiresTheExactSeparateOwnerGrantAndRetainsOriginalFloor()
    {
        using var f = new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        const long gap = 37;
        SetOpenTicket(f, t => { t.MinimumGameTick = SavedTick + gap; t.QuarantineActionId = Guid.NewGuid().ToString(); });
        var c = f.NewCoordinator();
        Assert.False(c.PrepareOnMainThread(OpenRequest(f)).Success);
        var request = OpenRequest(f);
        request.AcceptUnstoredProgressLoss = true;
        request.MinimumRecoveryGameTick = SavedTick + gap;
        request.ExpectedRecoveryGameTick = SavedTick;
        var before = File.ReadAllBytes(f.TicketReplicaPath(false));
        var plan = c.PrepareOnMainThread(request);
        Assert.True(plan.Success, plan.Error?.Message);
        Assert.Equal(gap, plan.Value!.DiscardedUnstoredProgressGameTicks);
        Assert.Equal(before, File.ReadAllBytes(f.TicketReplicaPath(false)));
        var granted = c.CommitOnMainThread(f.Confirmed(plan.Value));
        Assert.True(granted.Success, granted.Error?.Message);
        var attempts = Directory.GetFiles(Path.GetDirectoryName(f.TicketReplicaPath(false))!, "owned-world-reauthorization-attempt-*.json");
        var retained = Spherewright.Plugin.Transport.PluginJson.Deserialize<OwnedWorldReauthorizationAttempt>(File.ReadAllText(Assert.Single(attempts)))!;
        Assert.Equal(SavedTick + gap, retained.Ticket.MinimumGameTick);
        Assert.Equal(1, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData("primary")]
    [InlineData("journal")]
    [InlineData("ticket")]
    [InlineData("revision")]
    [InlineData("confirmation")]
    public void SavedOpeningRechecksAllEvidenceBeforeConsumingOrLoading(string change)
    {
        using var f = new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        var c = f.NewCoordinator();
        var p = c.PrepareOnMainThread(OpenRequest(f)).Value!;
        var request = f.Confirmed(p);
        switch (change)
        {
            case "primary": var data = File.ReadAllBytes(f.PrimaryPath); data[^1]++; File.WriteAllBytes(f.PrimaryPath, data); break;
            case "journal": f.WriteJournal("changed-content"); break;
            case "ticket": SetOpenTicket(f, t => t.MinimumGameTick++); break;
            case "revision": f.Sessions.Revision++; break;
            case "confirmation": request.UserConfirmedInConversation = false; break;
        }
        Assert.False(c.CommitOnMainThread(request).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(f.HasTicketReplicas());
    }

    [Theory]
    [InlineData("foreign-identity")]
    [InlineData("native-version")]
    [InlineData("missing-primary")]
    [InlineData("replica-mismatch")]
    [InlineData("journal-missing")]
    public void SavedOpeningNeverFallsBackOrWeakensIdentityAndJournalChecks(string change)
    {
        using var f = new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        switch (change)
        {
            case "foreign-identity": Fixture.WriteSave(f.PrimaryPath, "synthetic-foreign-owned-identity", SavedTick); break;
            case "native-version": Fixture.WriteSave(f.PrimaryPath, Identity, SavedTick, 29104); break;
            case "missing-primary": File.Delete(f.PrimaryPath); break;
            case "replica-mismatch": f.ChangeTicketReplica(true, t => t.ExpiresAtUtc = t.ExpiresAtUtc.AddDays(1)); break;
            case "journal-missing": File.Delete(f.JournalPath); break;
        }
        Assert.False(f.NewCoordinator().PrepareOnMainThread(OpenRequest(f)).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void AmbiguousSavedLoadIsConsumedAndNeverReplayedWithAnotherKey()
    {
        using var f = new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        var c = f.NewCoordinator(); var p = c.PrepareOnMainThread(OpenRequest(f)).Value!;
        var request = f.Confirmed(p); var dispatches = 0;
        DSPGame.BeforeLoad = () => { dispatches++; throw new IOException("synthetic partial Native dispatch"); };
        var accepted = c.CommitOnMainThread(request);
        Assert.True(accepted.Success);
        Assert.Equal(NormalActionStates.OutcomeUnknown, accepted.Value!.State);
        Assert.True(c.CommitOnMainThread(request).Value!.IdempotentReplay);
        Assert.False(c.PrepareOnMainThread(OpenRequest(f)).Success);
        Assert.Equal(1, dispatches);
        Assert.False(f.HasTicketReplicas());
    }

    [Fact]
    public void NewerCompletePrimaryCanOpenWithoutInventingAnEqualTicketMinimum()
    {
        using var f = new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        SetOpenTicket(f, t => t.QuarantineActionId = Guid.NewGuid().ToString());
        Fixture.WriteSave(f.PrimaryPath, Identity, SavedTick + 40);
        var ticket = File.ReadAllBytes(f.TicketReplicaPath(false));
        var plan = f.NewCoordinator().PrepareOnMainThread(OpenRequest(f));
        Assert.True(plan.Success, plan.Error?.Message);
        Assert.Equal(SavedTick + 40, plan.Value!.CandidateGameTick);
        Assert.Equal(0, plan.Value.DiscardedUnstoredProgressGameTicks);
        Assert.Equal(ticket, File.ReadAllBytes(f.TicketReplicaPath(false)));
    }

    [Fact]
    public void NewerOwnedLastExitIsBoundAsKnownProgressAndCannotBeSilentlyRolledBack()
    {
        using var f = new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        var path = GameSave.SavePath(GameSave.LastExit);
        Fixture.WriteSave(path, Identity, SavedTick + 40);
        var c = f.NewCoordinator();
        Assert.False(c.PrepareOnMainThread(OpenRequest(f)).Success);
        var request = OpenRequest(f);
        request.AcceptUnstoredProgressLoss = true; request.MinimumRecoveryGameTick = SavedTick + 40;
        request.ExpectedRecoveryGameTick = SavedTick;
        var plan = c.PrepareOnMainThread(request);
        Assert.True(plan.Success, plan.Error?.Message);
        Assert.Equal(40, plan.Value!.DiscardedUnstoredProgressGameTicks);
        var bytes = File.ReadAllBytes(path); bytes[^1]++; File.WriteAllBytes(path, bytes);
        Assert.False(c.CommitOnMainThread(f.Confirmed(plan.Value)).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    private static PrepareOwnedWorldResumeRequest OpenRequest(Fixture f) => new()
    { ResumeToken = f.Store.CurrentResumeToken ?? f.Token, RecoveryMode = OwnedWorldResumeModes.OpenSavedPrimary };

    private static void SetOpenTicket(Fixture f, Action<OwnedWorldResumeTicket> change)
    {
        var ticket = Spherewright.Plugin.Transport.PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(f.TicketReplicaPath(false)))!;
        change(ticket);
        var json = Spherewright.Plugin.Transport.PluginJson.Serialize(ticket);
        File.WriteAllText(f.TicketReplicaPath(false), json); File.WriteAllText(f.TicketReplicaPath(true), json);
        f.ReopenStore();
    }

    private static void CompleteOpenSubstitute(Fixture f, string actionId, OwnedWorldGameplayJournalCheckpoint checkpoint)
    {
        var guard = f.Sessions.SavedOpenGuard!;
        var world = new object();
        var loader = new SavedOpenLoaderSubstitute();
        using var sub = new NativeLoadCompletionSubscription<SavedOpenLoaderSubstitute>(loader,
            h => loader.Complete += h, h => loader.Complete -= h,
            l => l.Frame == 10 && !l.Enabled,
            (_, completed, error) =>
            {
                loader.NormalPause();
                Assert.True(guard.ObserveLoad(world, completed && error is null, loader.Paused, SavedTick, true, true));
            });
        loader.Finish();
        Assert.Equal(1, loader.PauseCalls);
        Assert.False(guard.Completed);
        f.Sessions.Dispose(); // Source/Journal leases released after continuity confirmation.
        Assert.True(guard.ConfirmJournal(world, SavedTick, true, () =>
            f.Store.ArmFromOpenedSavedWorld(Identity, "synthetic-opened-session", 104, SavedTick, actionId, checkpoint)));
        f.Sessions.Snapshot.OwnedBySpherewright = true;
        f.Sessions.Snapshot.SessionId = "synthetic-opened-session";
        f.Sessions.Snapshot.LocalPlanetId = 104;
        f.Sessions.Snapshot.GameTick = SavedTick;
    }

    private sealed class SavedOpenLoaderSubstitute
    {
        internal Action? Complete;
        internal int Frame;
        internal bool Enabled = true;
        internal bool Paused;
        internal int PauseCalls;
        internal void NormalPause() { PauseCalls++; Paused = true; }
        internal void Finish() { Frame = 10; Enabled = false; Complete?.Invoke(); }
    }
}
