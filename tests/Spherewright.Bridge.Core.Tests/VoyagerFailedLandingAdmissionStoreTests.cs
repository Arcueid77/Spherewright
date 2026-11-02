using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    private static string LandingDigest => CanonicalStateHash.Combine("synthetic-failed-landing-confirmation");
    private static Spherewright.Contracts.Players.NativeResumeFailureSnapshot LandingFailure()
        => NativeResumeDecisionEvidence.UnobservedFailure("synthetic_landing_failure", "synthetic_native_stage", null,
            DateTimeOffset.UtcNow, "synthetic_native_predicate");

    [Fact]
    public void FailedLandingNewLandingAdmissionRecordsAreOmittedFromEveryUnchangedLegacyJourney()
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        Assert.DoesNotContain("failedLandingAdmissions", File.ReadAllText(Path.Combine(s.Handoff, "voyager-journey.json")));
        Assert.Empty(s.Checkpoints.VoyagerJourney!.FailedLandingAdmissions);
    }

    [Fact]
    public void FailedLandingRegistrationAndBindingLeaveOldFailureUntouchedUntilNewSuccessfulAdmissionDebitsAllElapsedTicks()
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        var cpPath = s.ProtectedEvidencePaths.ElementAt(4); var cpBefore = File.ReadAllBytes(cpPath);
        Assert.True(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, FailedLandingRequest(), out var source, out _));
        using var evidence = VoyagerRemoteResumeEvidence.CreateFailedLandingRecovery(source!);
        var action = Guid.NewGuid().ToString(); var session = Guid.NewGuid().ToString();
        Assert.True(s.Checkpoints.TryRegisterFailedLandingAdmission(source!, action, true, LandingDigest, DateTimeOffset.UtcNow, out var error), error);
        evidence.BindRegisteredFailedLandingJourney(PluginJson.Serialize(s.Checkpoints.VoyagerJourney));
        Assert.True(s.Checkpoints.RemoteResumeEvidenceStillMatches(evidence));
        Assert.False(s.Checkpoints.TryRegisterFailedLandingAdmission(source!, Guid.NewGuid().ToString(), true, LandingDigest, DateTimeOffset.UtcNow, out _));
        Assert.True(s.Checkpoints.TryBindFailedLandingAdmissionSession(action, session, 12510, out error), error);
        Assert.True(s.Checkpoints.RemoteResumeEvidenceStillMatches(evidence));
        var j = s.Checkpoints.VoyagerJourney!;
        Assert.Null(j.LastLandedGameTick); Assert.Equal(165, j.SpentGameTicks);
        Assert.Equal("recovery_required", j.Phase); Assert.Equal(cpBefore, File.ReadAllBytes(cpPath));
        // Ordinary journal writes cannot forge a new role or revise its grant.
        j.FailedLandingAdmissions.Last().DeadlineUtc = j.FailedLandingAdmissions.Last().DeadlineUtc.AddSeconds(1);
        Assert.False(s.Checkpoints.TryPersistVoyagerJourney(j, out _));
        evidence.Dispose();
        Assert.True(s.Checkpoints.TryFinishFailedLandingAdmission(action, true, null, 12512, out error), error);
        j = s.Checkpoints.VoyagerJourney!;
        Assert.Equal("remote_landed", j.Phase); Assert.Equal(12512, j.LastLandedGameTick);
        Assert.Equal(12512, j.LastAccountedGameTick); Assert.Equal(167, j.SpentGameTicks);
        Assert.Equal("completed_health", j.FailedLandingAdmissions.Last().State);
        Assert.Equal(cpBefore, File.ReadAllBytes(cpPath)); // Still no old flight success or checkpoint rewrite.
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void FailedLandingPausedKnownFailedAdmissionRetainsItsFailureAndConservesElapsedTicksForANewExactSource()
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        Assert.True(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, FailedLandingRequest(), out var source, out _));
        var action = Guid.NewGuid().ToString();
        Assert.True(s.Checkpoints.TryRegisterFailedLandingAdmission(source!, action, true, LandingDigest, DateTimeOffset.UtcNow, out var registerError), registerError);
        Assert.True(s.Checkpoints.TryBindFailedLandingAdmissionSession(action, Guid.NewGuid().ToString(), 12510, out _));
        source!.Dispose();
        Assert.True(s.Checkpoints.TryFinishFailedLandingAdmission(action, false, LandingFailure(), 12515, out var error), error);
        var j = s.Checkpoints.VoyagerJourney!;
        Assert.Equal("recovery_required", j.Phase); Assert.Null(j.LastLandedGameTick);
        Assert.Equal(170, j.SpentGameTicks); Assert.Equal(12515, j.LastAccountedGameTick);
        Assert.Equal("failed_health", j.FailedLandingAdmissions.Single().State);
        Assert.Equal("synthetic_landing_failure", j.FailedLandingAdmissions.Single().FirstFailure!.Code);
        s.WriteSave(GameSave.LastExit, 12515); // Synthetic Native preservation fixture only.
        var r = FailedLandingRequest(); r.MinimumRecoveryGameTick = r.ExpectedRecoveryGameTick = 12515;
        Assert.True(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, r, out var next, out var failure), failure?.Code);
        using (next) { Assert.Equal(170, next!.ConservedSpentGameTicks); }
    }

    [Theory]
    [InlineData("pending")][InlineData("adopted")][InlineData("unknown")]
    public void FailedLandingPendingOrUnknownNativeAttemptCannotBeRekeyedThroughAnotherFailedLandingSource(string state)
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        Assert.True(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, FailedLandingRequest(), out var source, out _));
        var action = Guid.NewGuid().ToString();
        Assert.True(s.Checkpoints.TryRegisterFailedLandingAdmission(source!, action, true, LandingDigest, DateTimeOffset.UtcNow, out var registerError), registerError);
        if (state == "adopted") Assert.True(s.Checkpoints.TryBindFailedLandingAdmissionSession(action, Guid.NewGuid().ToString(), 12510, out _));
        source!.Dispose();
        if (state == "unknown") Assert.True(s.Checkpoints.TryFinishFailedLandingAdmission(action, false, LandingFailure(), null, out _));
        Assert.False(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, FailedLandingRequest(), out var next, out var failure));
        Assert.Null(next); Assert.NotNull(failure);
        Assert.Equal(165, s.Checkpoints.VoyagerJourney!.SpentGameTicks);
        Assert.Null(s.Checkpoints.VoyagerJourney.LastLandedGameTick);
    }

    [Theory]
    [InlineData("unconfirmed")][InlineData("digest")][InlineData("closed_source")][InlineData("wrong_action")]
    public void FailedLandingLandingRegistrationNeedsFreshConfirmationAndTheHeldOriginalSource(string fault)
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        Assert.True(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, FailedLandingRequest(), out var source, out _));
        using (source)
        {
            if (fault == "closed_source") source!.Dispose();
            Assert.False(s.Checkpoints.TryRegisterFailedLandingAdmission(source!, fault == "wrong_action" ? "invalid" : Guid.NewGuid().ToString(),
                fault != "unconfirmed", fault == "digest" ? "invalid" : LandingDigest, DateTimeOffset.UtcNow, out _));
            Assert.Empty(s.Checkpoints.VoyagerJourney!.FailedLandingAdmissions);
        }
    }
}
