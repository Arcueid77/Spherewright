using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Spherewright.Contracts.Actions;
using BepInEx.Logging;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void FreshNoLossAuthorityUsesOnlyTheExactCurrentNormalExitWhenPrimaryIsOlder()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var request = s.PrimaryRecoveryRequest(18);
        request.AcceptUnstoredProgressLoss = false;
        request.MinimumRecoveryGameTick = request.ExpectedRecoveryGameTick = null;
        var originals = s.ProtectedEvidencePaths.ToDictionary(path => path, File.ReadAllBytes);
        var prepared = s.PrepareResult(request);
        Assert.True(prepared.Success, prepared.Error?.Message);
        var plan = prepared.Value!;
        Assert.Equal(12528, plan.CandidateGameTick);
        Assert.Equal(0, plan.DiscardedUnstoredProgressGameTicks);
        Assert.True(VoyagerRemotePrimaryRecoveryPolicy.HasMatchingEcho(request, plan));
        var accepted = s.Commit(plan);
        Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        foreach (var pair in originals) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }

    [Theory]
    [InlineData(12527)] [InlineData(12529)]
    public void NoLossAuthorityCannotSelectALastExitFromAnotherProgressBoundary(long otherTick)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var request = s.PrimaryRecoveryRequest(18);
        request.AcceptUnstoredProgressLoss = false;
        request.MinimumRecoveryGameTick = request.ExpectedRecoveryGameTick = null;
        s.WriteSave(GameSave.LastExit, otherTick);
        var refused = s.PrepareResult(request);
        Assert.False(refused.Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData("source")] [InlineData("primary")] [InlineData("journal")]
    public void CurrentExitAuthorizationRechecksBothCompleteSavesAndJournalAtCommit(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var request = s.PrimaryRecoveryRequest(18);
        request.AcceptUnstoredProgressLoss = false;
        request.MinimumRecoveryGameTick = request.ExpectedRecoveryGameTick = null;
        var plan = s.Prepare(request);
        if (fault == "source") s.WriteSave(GameSave.LastExit, 12529);
        else if (fault == "primary") s.WriteSave(Scenario.Identity, 12511);
        else File.AppendAllText(s.JournalPath, " ");
        Assert.False(s.Commit(plan).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    // The same test compiles against the exact prior product source: the new
    // mode is a wire string, so a RED is a production refusal, not a build error.
    [Fact]
    public void ExpiredRemoteAuthorityCanPrepareFreshPrimaryAdmissionWithoutRewritingHistory()
    {
        using var s = new Scenario(expiredJourney: true);
        s.WriteSave(Scenario.Identity, 12510);
        var originals = s.ProtectedEvidencePaths.ToDictionary(p => p, File.ReadAllBytes);
        var request = WindowRequest();
        request.RecoveryMode = "authorize_remote_primary_recovery";
        var prepared = s.PrepareResult(request);
        Assert.True(prepared.Success, prepared.Error?.Message);
        Assert.Equal(12510, prepared.Value!.CandidateGameTick);
        Assert.Equal(s.OriginalDeadline, prepared.Value.OriginalJourneyDeadlineUtc);
        Assert.Equal(request.RecoveryDeadlineUtc, prepared.Value.EffectiveRecoveryDeadlineUtc);
        Assert.True(prepared.Value.UserConfirmationRequired);
        Assert.False(prepared.Value.CommitAllowedNow);
        Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var pair in originals) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }

    [Fact]
    public void CompletedRecoveryPreservationAndExpiredWindowCanPrepareAFreshPrimaryWithoutClaimingFailure()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var inherited = s.SeedCompletedAdmission(out var completedId);
        s.ChangePrior(t => t.RecoveryAuthorization = new(t.RecoveryAuthorization!.ResumeActionId,
            t.RecoveryAuthorization.EvidenceFingerprint, t.RecoveryAuthorization.ConfirmationDigest,
            DateTimeOffset.UtcNow.AddMinutes(-40), new(DateTimeOffset.UtcNow.AddMinutes(-20),
                VoyagerRecoveryWindowProposal.TaskScope, 9000, 240)));
        Assert.False(s.PrepareResult(inherited).Success);
        var checkpoint = s.Tickets.TryGetSavedWorldOpenProvenance(s.Token, out var old, out _, out _)
            ? old!.GameplayJournalCheckpoint! : throw new Xunit.Sdk.XunitException("Current protected provenance missing.");
        using (var shell = new PausedSaveShell(s, completedId))
        {
            s.Sessions.QuarantineRemoteIdleOnMainThread(completedId, "remote_idle_return_wall_reserve_insufficient");
            GameMain.gameTick = 12540; shell.Observe(10); shell.Observe(11);
            Assert.True(s.Sessions.TryBindPausedOwnedSave(out var binding, out var reason), reason);
            GameSave.FlightStartSave = name => { s.WriteSave(name, GameMain.gameTick); return true; };
            Assert.True(s.Sessions.TrySaveOwnedWorldNowOnMainThread(out var error, binding), error);
        }
        // Native shutdown/Unity are outside this fixture. Its LastExit write
        // models a normal close after the production paused-save verification.
        s.WriteSave(GameSave.LastExit, 12540);
        s.Sessions.Snapshot.GameLoaded = false; s.Sessions.Snapshot.OwnedBySpherewright = false;
        var originals = s.ProtectedEvidencePaths.Concat(s.PriorConsumptionPaths).ToDictionary(p => p, File.ReadAllBytes);
        var r = WindowRequest(); r.RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery;
        var p = s.Prepare(r);
        Assert.True(VoyagerRemotePrimaryRecoveryPolicy.HasMatchingEcho(r, p));
        Assert.Equal(12540, p.CandidateGameTick); Assert.Equal(0, p.DiscardedUnstoredProgressGameTicks);
        Assert.False(p.PriorActionTerminalFailureAttested); Assert.False(p.PriorActionTerminalCompletionAttested);
        Assert.Equal(checkpoint.JournalId, old!.GameplayJournalCheckpoint!.JournalId);
        var accepted = s.Commit(p); Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(Scenario.Identity, DSPGame.LoadedName);
        Assert.True(s.Sessions.RemoteEvidence!.RemotePrimaryRecovery);
        Assert.Null(s.Sessions.RecoveryAuthorization!.InheritedWindowConfirmedAtUtc);
        Assert.True(s.Result(completedId, out var original)); Assert.True(original!.Succeeded);
        Assert.Equal(NormalActionStates.Completed, original.State);
        foreach (var row in originals) Assert.Equal(row.Value, File.ReadAllBytes(row.Key));
    }

    [Theory]
    [InlineData("no_window")] [InlineData("prepare_confirmation")] [InlineData("prior_failed")]
    [InlineData("prior_completed")] [InlineData("wrong_owner")] [InlineData("missing_primary")]
    [InlineData("wrong_native")] [InlineData("stale_journal")] [InlineData("missing_replica")]
    [InlineData("unapproved_loss")] [InlineData("wrong_floor")] [InlineData("wrong_tick")]
    [InlineData("refunded_time")] [InlineData("expired_window")] [InlineData("source_before_landing")]
    public void FreshPrimaryRecoveryFailsClosedWithoutAnyOldActionAttestationOrBudgetRefund(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var r = s.PrimaryRecoveryRequest(18);
        switch (fault)
        {
            case "no_window": r.RecoveryDeadlineUtc = null; break;
            case "prepare_confirmation": r.UserConfirmedInConversation = true; break;
            case "prior_failed": r.PriorActionKnownTerminalFailed = true; break;
            case "prior_completed": r.PriorActionKnownTerminalCompleted = true; break;
            case "wrong_owner":
                using (var wrong = OwnedSavePrefixReaderTests.Fixture(identity: "different-synthetic-world",
                    versionPatch: 35, versionBuild: 29151, patch: 23, gameDescVersion: 10))
                    File.WriteAllBytes(GameSave.SavePath(Scenario.Identity), wrong.ToArray());
                break;
            case "missing_primary": File.Delete(GameSave.SavePath(Scenario.Identity)); break;
            case "wrong_native":
                s.ReplaceCurrentTicket(t => t.GameVersion = "0.10.35.29145"); s.ReopenPrimaryCredentials(); break;
            case "stale_journal": File.AppendAllText(s.JournalPath, "broken-synthetic-journal"); break;
            case "missing_replica": File.Delete(s.HandoffTicket); break;
            case "unapproved_loss":
                r.AcceptUnstoredProgressLoss = false; r.MinimumRecoveryGameTick = null; r.ExpectedRecoveryGameTick = null;
                s.WriteSave(GameSave.LastExit, 12527); // Neither complete file covers the current12528 boundary.
                break;
            // A higher expressly accepted missing-progress floor adds debit.
            // Reject understating the complete Native boundary/refunding time.
            case "wrong_floor": r.MinimumRecoveryGameTick--; break;
            case "wrong_tick": r.ExpectedRecoveryGameTick--; break;
            case "refunded_time":
                s.WriteSave(GameSave.LastExit, 12510 + 60000);
                r.MinimumRecoveryGameTick = 12510 + 60000; break;
            case "expired_window": r.RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(-1); break;
            case "source_before_landing": s.WriteSave(Scenario.Identity, 12490); r.ExpectedRecoveryGameTick = 12490; break;
        }
        var before = DSPGame.LoadCalls;
        var refused = s.PrepareResult(r); Assert.False(refused.Success);
        Assert.Equal(before, DSPGame.LoadCalls);
        if (fault is "unapproved_loss" or "wrong_floor" or "wrong_tick" or "refunded_time" or "expired_window" or "source_before_landing")
        {
            var cut = refused.Error!.NativeFirstFailure!;
            Assert.Null(cut.CapturedAtGameTick); Assert.NotEmpty(cut.FirstFailedPredicate!);
            Assert.Equal(fault == "source_before_landing" ? 12490d : 12510d, cut.Values["source_tick"]);
            Assert.False(cut.SafetyCertified);
            cut.Predicates.Clear(); Assert.NotEmpty(s.Sessions.RemotePrimaryRecoveryFailure!.Predicates);
        }
    }

    [Theory]
    [InlineData("primary")] [InlineData("lastexit")] [InlineData("journal")]
    [InlineData("journey")] [InlineData("replica")] [InlineData("confirmation")]
    public void ChangedFreshPrimaryPlanCannotBorrowItsPreparedDisclosure(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var r = s.PrimaryRecoveryRequest(18);
        var p = s.Prepare(r);
        switch (fault)
        {
            case "primary": s.WriteSave(Scenario.Identity, 12511); break;
            case "lastexit": s.WriteSave(GameSave.LastExit, 12529); break;
            case "journal": File.AppendAllText(s.JournalPath, " "); break;
            case "journey":
                var changed = s.Checkpoints.VoyagerJourney!; changed.SpentGameTicks++;
                File.WriteAllText(Path.Combine(s.Handoff, "voyager-journey.json"), PluginJson.Serialize(changed)); break;
            case "replica": s.MutateCredential(s.HandoffTicket); break;
        }
        var result = s.Commit(new CommitOwnedWorldResumeRequest { PlanToken = p.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString(), UserConfirmedInConversation = true,
            ConfirmationDigest = fault == "confirmation" ? "different-synthetic-confirmation" : p.ConfirmationDigest });
        Assert.False(result.Success); Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void FreshPrimaryAuthorityIsDurablyConsumedBeforeDispatchAndCannotBeRekeyed()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var r = s.PrimaryRecoveryRequest(18);
        var p = s.Prepare(r);
        var token = s.Token;
        var originalFiles = s.ProtectedEvidencePaths.ToDictionary(path => path, File.ReadAllBytes);
        DSPGame.BeforeLoad = () =>
        {
            Assert.False(s.Tickets.TryGetSavedWorldOpenProvenance(token, out _, out _, out _));
            Assert.Throws<IOException>(() => File.OpenWrite(GameSave.SavePath(Scenario.Identity)).Dispose());
            Assert.Throws<IOException>(() => File.OpenWrite(s.JournalPath).Dispose());
            Assert.Throws<IOException>(() => File.OpenWrite(Path.Combine(s.Handoff, "voyager-journey.json")).Dispose());
            foreach (var directory in new[] { s.Runtime, s.Handoff })
            {
                var consumed = Assert.Single(Directory.GetFiles(directory, "owned-world-resume-consumed-*.json"));
                var record = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(consumed))!;
                Assert.NotNull(record.RecoveryAuthorization);
                Assert.Equal(p.ConfirmationDigest, record.RecoveryAuthorization!.ConfirmationDigest);
                Assert.Equal(r.RecoveryDeadlineUtc, record.RecoveryAuthorization.Proposal.DeadlineUtc);
                Assert.Null(record.RecoveryAuthorization.PriorConsumedResumeTokenHash);
                var retained = Assert.Single(Directory.GetFiles(directory, "owned-world-reauthorization-attempt-*.json"));
                var attempt = PluginJson.Deserialize<OwnedWorldReauthorizationAttempt>(File.ReadAllText(retained))!;
                Assert.Equal(12528, attempt.Ticket.MinimumGameTick);
                Assert.True(attempt.Ticket.ExpiresAtUtc < DateTimeOffset.UtcNow);
            }
        };
        var commit = new CommitOwnedWorldResumeRequest { PlanToken = p.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString(), UserConfirmedInConversation = true, ConfirmationDigest = p.ConfirmationDigest };
        var accepted = s.Commit(commit); Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(Scenario.Identity, DSPGame.LoadedName);
        var replay = s.Commit(commit); Assert.True(replay.Value!.IdempotentReplay);
        Assert.Equal(accepted.Value!.ActionId, replay.Value.ActionId); Assert.Equal(1, DSPGame.LoadCalls);
        Assert.False(s.Commit(p).Success); Assert.False(s.PrepareResult(r).Success); Assert.Equal(1, DSPGame.LoadCalls);
        foreach (var row in originalFiles) Assert.Equal(row.Value, File.ReadAllBytes(row.Key));
    }

    [Fact]
    public void PartialFreshAuthorityPersistenceNeverLoadsOrLeavesSourceLeasesBehind()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var r = s.PrimaryRecoveryRequest(18); var p = s.Prepare(r);
        File.WriteAllText(Path.Combine(s.Handoff, ".test-only-fail-secure-write"), "synthetic-io-fault");
        var refused = s.Commit(p); Assert.False(refused.Success); Assert.Equal(0, DSPGame.LoadCalls);
        Assert.False(s.PrepareResult(r).Success);
        foreach (var path in s.ProtectedEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void ProgressDebitPersistenceFailureCannotPublishHealthOrReplaceItsFirstFailure()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        Assert.True(s.Commit(s.Prepare(s.PrimaryRecoveryRequest(18))).Success);
        var originalSpent = s.Checkpoints.VoyagerJourney!.SpentGameTicks;
        var path = Path.Combine(s.Handoff, "voyager-journey.json");
        var original = File.ReadAllBytes(path);
        // Real file sharing prevents atomic replacement after the production
        // completion releases its own leases. No Unity/ACL result is simulated.
        using var denyReplace = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.False(s.Sessions.CompleteContactForTest(null));
        Assert.False(s.Sessions.ContactAdmittedForTest);
        Assert.Equal("remote_primary_progress_debit_persistence_unproved", s.Sessions.ResumeAdoptionError);
        var failure = s.Sessions.PrimaryDebitFailure!;
        Assert.Equal("progress_loss_debit_persisted", failure.FirstFailedPredicate);
        Assert.Equal("not_observed", failure.FactsState);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(originalSpent, s.Checkpoints.VoyagerJourney.SpentGameTicks);
        Assert.False(s.Sessions.CompleteContactForTest("later_cancel"));
        Assert.Equal(failure.Code, s.Sessions.PrimaryDebitFailure!.Code);
    }

    [Theory]
    [InlineData(100, 100, 10, 28, 118)]
    [InlineData(118, 100, 10, 28, 118)]
    [InlineData(130, 100, 10, 28, 130)]
    [InlineData(100, 100, 28, 10, 100)]
    public void ApprovedProgressLossNeverRefundsOrDoubleChargesTheActiveAdmission(long spent, long original,
        long source, long known, long conserved) =>
        Assert.Equal(conserved, VoyagerRemotePrimaryRecoveryPolicy.ConservedSpent(spent, original, source, known));

    [Theory]
    [InlineData(-1, 0, 10, 28)]
    [InlineData(10, 11, 10, 28)]
    [InlineData(10, 10, -1, 28)]
    [InlineData(10, 10, 10, -1)]
    [InlineData(long.MaxValue, long.MaxValue, 0, 1)]
    public void UnprovedOrOverflowingConservativeSpentIsRejected(long spent, long original, long source, long known) =>
        Assert.Null(VoyagerRemotePrimaryRecoveryPolicy.ConservedSpent(spent, original, source, known));

    private sealed partial class Scenario
    {
        internal void ReopenPrimaryCredentials()
        {
            Tickets = new OwnedWorldResumeTicketStore(Runtime, Handoff, "test-bridge", Version, new ManualLogSource());
            Coordinator = new OwnedWorldResumeCoordinator(true, 30, 30, 16, Sessions, Tickets);
        }

        internal PrepareOwnedWorldResumeRequest PrimaryRecoveryRequest(long gap, bool openedFirst = false)
        {
            WriteSave(Identity, 12510); WriteSave(GameSave.LastExit, 12510 + gap);
            if (gap > 0) { ReplaceCurrentTicket(t => t.MinimumGameTick = 12510 + gap); ReopenPrimaryCredentials(); }
            if (openedFirst)
            {
                Assert.True(Tickets.TryGetSavedWorldOpenProvenance(Token, out var old, out _, out _));
                var journal = File.ReadAllBytes(JournalPath);
                var request = new PrepareOwnedWorldResumeRequest { RecoveryMode = OwnedWorldResumeModes.OpenSavedPrimary,
                    AcceptUnstoredProgressLoss = gap > 0,
                    MinimumRecoveryGameTick = gap > 0 ? 12510 + gap : null,
                    ExpectedRecoveryGameTick = gap > 0 ? 12510 : null };
                var accepted = Commit(Prepare(request)); Assert.True(accepted.Success, accepted.Error?.Message);
                var guard = Sessions.SavedOpenGuard!; var world = new object();
                // Exact loader completion/Pause is a Native shell substitute.
                // The production guard, Store/Journal/leases and consumption run.
                var loader = new PrimaryLoaderSubstitute();
                using var observer = new NativeLoadCompletionSubscription<PrimaryLoaderSubstitute>(loader,
                    h => loader.Completed += h, h => loader.Completed -= h,
                    l => l.Frame == 10 && !l.Enabled,
                    (_, complete, error) => { loader.Paused = true;
                        Assert.True(guard.ObserveLoad(world, complete && error is null, loader.Paused, 12510,
                            Sessions.SourceLease!.Prefix.MatchesExpectedIdentity, old!.ExpectedPlanetId == 201)); });
                loader.Finish();
                Sessions.Dispose();
                Assert.True(guard.ConfirmJournal(world, 12510, journal.SequenceEqual(File.ReadAllBytes(JournalPath)), () =>
                    Tickets.ArmFromOpenedSavedWorld(Identity, "synthetic-opened-session", 201, 12510,
                        accepted.Value!.ActionId, old!.GameplayJournalCheckpoint!,
                        Sessions.SavedOpenPreviousTicket, Sessions.SavedOpenPreparedKnownProgressGameTick)));
                Token = Tickets.CurrentResumeToken!;
                Assert.True(Tickets.TryGetSavedWorldOpenProvenance(Token, out var scoped, out _, out _));
                Assert.True(scoped!.SavedWorldOpenOnly); Assert.False(Tickets.TryGetActiveTicket(Token, out _, out _));
                // Simulated normal close/menu, not an executed Unity shutdown.
                WriteSave(GameSave.LastExit, 12510);
                Sessions.Snapshot.GameLoaded = false; Sessions.Snapshot.OwnedBySpherewright = false;
            }
            ReplaceCurrentTicket(t => { t.IssuedAtUtc = DateTimeOffset.UtcNow.AddDays(-8); t.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-7); });
            ReopenPrimaryCredentials();
            var r = WindowRequest(); r.RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery;
            r.AcceptUnstoredProgressLoss = gap > 0 && !openedFirst;
            r.MinimumRecoveryGameTick = r.AcceptUnstoredProgressLoss ? 12510 + gap : null;
            r.ExpectedRecoveryGameTick = r.AcceptUnstoredProgressLoss ? 12510 : null;
            return r;
        }
    }

    private sealed class PrimaryLoaderSubstitute
    {
        internal event Action? Completed;
        internal int Frame;
        internal bool Enabled = true;
        internal bool Paused;
        internal void Finish() { Frame = 10; Enabled = false; Completed?.Invoke(); }
    }
}
