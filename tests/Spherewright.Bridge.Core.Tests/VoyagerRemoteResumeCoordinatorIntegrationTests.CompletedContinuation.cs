using System.Security.Cryptography;
using System.Text;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void CompletedAdmissionCanContinueUnderItsUnchangedWindowWithoutBecomingAFailedPredecessor()
    {
        using var s = new Scenario(expiredJourney: true);
        var request = s.SeedCompletedAdmission(out var originalAction);
        var originals = s.ProtectedEvidencePaths.Concat(s.PriorConsumptionPaths).ToDictionary(p => p, File.ReadAllBytes);
        var prepared = s.Prepare(request);
        Assert.True(prepared.CompletedRemoteLastExitContinuationUsed);
        Assert.True(prepared.PriorActionTerminalCompletionAttested); Assert.False(prepared.PriorActionTerminalFailureAttested);
        Assert.False(prepared.FailedRemoteLastExitReauthorizationUsed); Assert.False(prepared.PriorFailedResumeProvenanceUsed);
        Assert.True(prepared.NewChargingProhibited); Assert.False(prepared.NativeOriginalOutcomeProved);
        Assert.Equal(s.PriorAuthorization!.Proposal.DeadlineUtc, prepared.EffectiveRecoveryDeadlineUtc);
        Assert.Equal(s.PriorAuthorization.WindowOriginUtc(), prepared.InheritedWindowConfirmedAtUtc);
        Assert.Equal(s.OriginalDeadline, prepared.OriginalJourneyDeadlineUtc);
        Assert.False(prepared.CommitAllowedNow); Assert.True(prepared.UserConfirmationRequired);
        Assert.True(VoyagerCompletedRemoteContinuationPolicy.HasMatchingEcho(request, prepared));
        var denied = s.Commit(prepared, confirmed: false); Assert.False(denied.Success);
        Assert.Equal(1, DSPGame.LoadCalls);
        prepared = s.Prepare(request);
        var commit = new CommitOwnedWorldResumeRequest { PlanToken = prepared.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString(), UserConfirmedInConversation = true, ConfirmationDigest = prepared.ConfirmationDigest };
        var accepted = s.Commit(commit); Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(2, DSPGame.LoadCalls); Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        var a = s.Sessions.RecoveryAuthorization!;
        Assert.Equal(s.PriorAuthorization.Proposal.DeadlineUtc, a.Proposal.DeadlineUtc);
        Assert.Equal(s.PriorAuthorization.WindowOriginUtc(), a.WindowOriginUtc());
        Assert.NotEqual(a.ConfirmedAtUtc, a.InheritedWindowConfirmedAtUtc); // Actual new consent time is not backdated.
        Assert.True(s.Sessions.RemoteEvidence!.CompletedRemoteContinuation);
        Assert.Null(s.Sessions.RemoteEvidence.PriorConsumedResumeTokenHash);
        foreach (var pair in originals) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.True(s.Result(originalAction, out var stillCompleted)); Assert.True(stillCompleted!.Succeeded);
        Assert.Equal(NormalActionStates.Completed, stillCompleted.State);
        var replay = s.Commit(commit); Assert.True(replay.Success); Assert.True(replay.Value!.IdempotentReplay);
        Assert.Equal(accepted.Value!.ActionId, replay.Value.ActionId); Assert.Equal(2, DSPGame.LoadCalls);
        Assert.False(s.PrepareResult(request).Success); // Consumed credential cannot be re-keyed.
    }

    [Theory]
    [InlineData("completion_unknown")] [InlineData("completion_false")] [InlineData("failed_claim")]
    [InlineData("fresh_deadline")] [InlineData("fresh_caps")] [InlineData("context")]
    [InlineData("prior_pair_changed")] [InlineData("missing_pair")] [InlineData("newer_primary")]
    [InlineData("expired_ticket")] [InlineData("source_beyond_minimum")]
    [InlineData("inherited_wall_exhausted")] [InlineData("inherited_deadline_exhausted")]
    [InlineData("wrong_owner")] [InlineData("journal_changed")] [InlineData("journey_changed")]
    public void CompletedContinuationRejectsUnknownRenewedStaleOrExhaustedEvidenceBeforeLoad(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedCompletedAdmission(out _);
        switch (fault)
        {
            case "completion_unknown": r.PriorActionKnownTerminalCompleted = null; break;
            case "completion_false": r.PriorActionKnownTerminalCompleted = false; break;
            case "failed_claim": r.PriorActionKnownTerminalFailed = true; break;
            case "fresh_deadline": r.RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(30); break;
            case "fresh_caps": r.MaximumChargeGameTicks = 1; break;
            case "context": r.ExpectedRemoteRecoveryContextFingerprint = CanonicalStateHash.Combine("different-synthetic-context"); break;
            case "prior_pair_changed": File.AppendAllText(s.PriorConsumptionPaths[0], " "); break;
            case "missing_pair": File.Delete(s.PriorConsumptionPaths[1]); break;
            case "newer_primary": s.WriteSave(Scenario.Identity, 12522); r.ExpectedRemoteRecoveryContextFingerprint = s.CompletedOwnerContext(); break;
            case "expired_ticket": s.ReplaceCurrentTicket(t => t.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)); break;
            case "source_beyond_minimum": s.WriteSave(GameSave.LastExit, 12522); r.ExpectedRemoteRecoveryContextFingerprint = s.CompletedOwnerContext(); break;
            case "inherited_wall_exhausted":
                s.ChangePrior(t => t.RecoveryAuthorization = new(t.RecoveryAuthorization!.ResumeActionId,
                    t.RecoveryAuthorization.EvidenceFingerprint, t.RecoveryAuthorization.ConfirmationDigest,
                    t.RecoveryAuthorization.ConfirmedAtUtc, t.RecoveryAuthorization.Proposal, inheritedWindowConfirmedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-10)));
                break;
            case "inherited_deadline_exhausted":
                s.ChangePrior(t => t.RecoveryAuthorization = new(t.RecoveryAuthorization!.ResumeActionId,
                    t.RecoveryAuthorization.EvidenceFingerprint, t.RecoveryAuthorization.ConfirmationDigest,
                    DateTimeOffset.UtcNow.AddMinutes(-20), new(DateTimeOffset.UtcNow.AddMinutes(-1), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240)));
                break;
            case "wrong_owner": s.RearmQuarantine(Guid.NewGuid().ToString(), minimum: 12521); break;
            case "journal_changed": File.AppendAllText(s.JournalPath, " "); break;
            case "journey_changed":
                var j = s.Checkpoints.VoyagerJourney!; j.SpentGameTicks = j.MaximumGameTicks;
                Assert.True(s.Checkpoints.TryPersistVoyagerJourney(j, out _)); r.ExpectedRemoteRecoveryContextFingerprint = s.CompletedOwnerContext(); break;
        }
        var result = s.PrepareResult(r); Assert.False(result.Success); Assert.Equal(1, DSPGame.LoadCalls);
        if (fault.StartsWith("inherited_"))
        {
            Assert.Contains("completed_remote_original_window_exhausted", result.Error!.Message);
            var cut = result.Error.NativeFirstFailure!;
            Assert.Equal("completed_original_wall_remaining", cut.FirstFailedPredicate);
            Assert.Null(cut.CapturedAtGameTick); // File/authorization sample at menu, never a live Native tick.
            Assert.Equal("false", cut.Predicates[cut.FirstFailedPredicate]);
            Assert.Equal("not_reached", cut.Predicates["completed_original_deadline_remaining"]);
            Assert.True(cut.Values["original_window_wall_used_s"] >= cut.Values["original_window_wall_cap_s"]);
            Assert.False(cut.SafetyCertified);
        }
    }

    [Fact]
    public void AnAlreadyReportedCompletedDefaultResumeCannotBeRewrittenByLaterIdleQuarantine()
    {
        using var s = new Scenario(expiredJourney: true); s.SeedCompletedAdmission(out var action);
        s.Sessions.ResumeAdoptionError = "later-idle-guard-stop";
        Assert.True(s.Result(action, out var result)); Assert.True(result!.Terminal); Assert.True(result.Succeeded);
        Assert.Equal(NormalActionStates.Completed, result.State);
    }

    [Fact]
    public void APreparedCompletedContinuationCannotBorrowARenewedOrExhaustedOriginAtCommit()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedCompletedAdmission(out _); var prepared = s.Prepare(r);
        s.ChangePrior(t => t.RecoveryAuthorization = new(t.RecoveryAuthorization!.ResumeActionId,
            t.RecoveryAuthorization.EvidenceFingerprint, t.RecoveryAuthorization.ConfirmationDigest,
            t.RecoveryAuthorization.ConfirmedAtUtc, t.RecoveryAuthorization.Proposal,
            inheritedWindowConfirmedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-10)));
        var result = s.Commit(prepared); Assert.False(result.Success); Assert.Equal(1, DSPGame.LoadCalls);
        var cut = result.Error!.NativeFirstFailure!;
        Assert.Equal("completed_original_wall_remaining", cut.FirstFailedPredicate);
        Assert.Null(cut.CapturedAtGameTick); Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        cut.Values.Clear(); Assert.NotEmpty(s.Sessions.CompletedContinuationFailure!.Values);
    }

    private sealed partial class Scenario
    {
        internal PrepareOwnedWorldResumeRequest SeedCompletedAdmission(out string actionId)
        {
            PriorToken = Token;
            var accepted = Commit(Prepare(WindowRequest())); Assert.True(accepted.Success, accepted.Error?.Message);
            actionId = accepted.Value!.ActionId; PriorAuthorization = Sessions.RecoveryAuthorization!;
            using (var native = new PausedSaveShell(this, actionId))
            {
                GameSave.FlightStartSave = name => { WriteSave(name, GameMain.gameTick); return true; };
                Assert.True(Sessions.TrySaveOwnedWorldNowOnMainThread(out var error), error);
                var state = Sessions.Snapshot; state.GameTick = GameMain.gameTick;
                state.OwnedSaveState = OwnedSaveStates.Saved; state.LastOwnedSaveGameTick = Sessions.LastPreservedTick;
                Assert.True(Result(actionId, out var completed)); Assert.True(completed!.Succeeded);
            }
            WriteSave(GameSave.LastExit, 12521); RearmQuarantine(actionId, minimum: 12521);
            // Normal close/menu is a shell transition only; no Unity shutdown is simulated.
            Sessions.Snapshot.GameLoaded = false; Sessions.Snapshot.OwnedBySpherewright = false;
            return new() { RecoveryMode = OwnedWorldResumeModes.ContinueCompletedRemoteLastExit,
                PriorConsumedResumeToken = PriorToken, PriorActionKnownTerminalCompleted = true,
                ExpectedRemoteRecoveryContextFingerprint = CompletedOwnerContext() };
        }
        internal string CompletedOwnerContext()
        {
            var t = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(RuntimeTicket))!;
            using var source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), Identity);
            using var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(Identity), Identity);
            using var cp = OwnedSaveRecoveryLease.Open(GameSave.SavePath(CheckpointSaveName), Identity);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(JournalPath)))).ToLowerInvariant();
            return VoyagerCompletedRemoteContinuationPolicy.ContextFingerprint(PluginJson.Serialize(Checkpoints.VoyagerJourney),
                PluginJson.Serialize(PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(Path.Combine(Handoff, "flight-checkpoint.json")))),
                cp.Fingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick, PluginJson.Serialize(t.GameplayJournalCheckpoint), hash);
        }
    }
}
