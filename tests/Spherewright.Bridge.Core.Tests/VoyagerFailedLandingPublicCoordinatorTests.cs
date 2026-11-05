using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void CompletionSharingFailureRetainsItsStoreCauseAndNeverClaimsHealthyAdmission()
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        Assert.True(s.Commit(s.Prepare(FailedLandingRequest())).Success);
        Assert.True(s.Sessions.AdoptFailedLandingNativeShellForTest(s.Checkpoints));
        GameMain.gameTick = 12512;
        using (var reader = new FileStream(Path.Combine(s.Handoff, "voyager-journey.json"),
            FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(s.Sessions.CompleteContactForTest(null));
            Assert.False(s.Sessions.ContactAdmittedForTest);
            var failure = s.Sessions.CompletionFailureForTest!;
            Assert.Equal("completion_terminal_persisted", failure.FirstFailedPredicate);
            Assert.Contains("IOException", failure.Observations["store_rejection"]);
            Assert.Equal("adopted", s.Checkpoints.VoyagerJourney!.FailedLandingAdmissions.Single().State);
            Assert.Null(s.Checkpoints.VoyagerJourney.LastLandedGameTick);
        }
        s.Sessions.PreserveFailedLandingForTest("preservation-after-sharing-failure");
        Assert.Equal("failed_health", s.Checkpoints.VoyagerJourney!.FailedLandingAdmissions.Single().State);
        Assert.Equal("completion_terminal_persisted", s.Checkpoints.VoyagerJourney.FailedLandingAdmissions.Single().FirstFailure!.FirstFailedPredicate);
    }
    [Fact]
    public void FailedLandingPublicPrepareDisclosesIndependentBoundaryAndRetainedFailureWithoutLoading()
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        var request = FailedLandingRequest(); var p = s.Prepare(request);
        Assert.True(VoyagerFailedLandingRecoveryPolicy.HasMatchingEcho(request, p));
        Assert.Equal(201, p.ExpectedPlanetId); Assert.Equal(12510, p.CandidateGameTick);
        Assert.Contains("original outbound remains terminally failed", p.ConfirmationPrompt);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.False(s.Commit(p, confirmed: false).Success);
        Assert.Equal(0, DSPGame.LoadCalls); Assert.Empty(s.Checkpoints.VoyagerJourney!.FailedLandingAdmissions);
    }

    [Fact]
    public void FailedLandingPublicCommitRegistersExactlyOnceAndBindsTargetWithoutEditingMotherTicket()
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        var p = s.Prepare(FailedLandingRequest());
        var request = new CommitOwnedWorldResumeRequest { PlanToken = p.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            UserConfirmedInConversation = true, ConfirmationDigest = p.ConfirmationDigest };
        var accepted = s.Commit(request); Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(1, DSPGame.LoadCalls); Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        Assert.Equal(104, s.Sessions.RemoteEvidence!.FailedLandingSource!.Ticket.ExpectedPlanetId);
        Assert.Equal(201, s.Sessions.FailedLandingExpectedPlanetForTest());
        Assert.Single(s.Checkpoints.VoyagerJourney!.FailedLandingAdmissions);
        Assert.Null(s.Checkpoints.VoyagerJourney.LastLandedGameTick);
        var replay = s.Commit(request); Assert.True(replay.Success); Assert.True(replay.Value!.IdempotentReplay);
        Assert.Equal(accepted.Value!.ActionId, replay.Value.ActionId); Assert.Equal(1, DSPGame.LoadCalls);
        Assert.False(s.PrepareResult(FailedLandingRequest()).Success);
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public void FailedLandingPublicSourceFlowsThroughRealStoreCompletionOrPausedFailure(bool healthy)
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        var accepted = s.Commit(s.Prepare(FailedLandingRequest())); Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.True(s.Sessions.AdoptFailedLandingNativeShellForTest(s.Checkpoints));
        Assert.Equal("adopted", s.Checkpoints.VoyagerJourney!.FailedLandingAdmissions.Single().State);
        var proof = s.Sessions.RemoteEvidence!; GameMain.gameTick = 12512;
        if (healthy) Assert.True(s.Sessions.CompleteContactForTest(null));
        else s.Sessions.PreserveFailedLandingForTest("synthetic_contact_failed");
        Assert.True(proof.IsDisposed); Assert.Equal(167, s.Checkpoints.VoyagerJourney!.SpentGameTicks);
        Assert.Equal(healthy ? "remote_landed" : "recovery_required", s.Checkpoints.VoyagerJourney.Phase);
        Assert.Equal(healthy ? "completed_health" : "failed_health", s.Checkpoints.VoyagerJourney.FailedLandingAdmissions.Single().State);
        Assert.Equal(1, DSPGame.LoadCalls); Assert.Null(s.Checkpoints.CurrentTicket!.SuccessfulFlightAtGameTick);
        // Both outcomes release LastExit for Native normal exit preservation.
        using var writable = new FileStream(GameSave.SavePath(GameSave.LastExit), FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
    }
}
