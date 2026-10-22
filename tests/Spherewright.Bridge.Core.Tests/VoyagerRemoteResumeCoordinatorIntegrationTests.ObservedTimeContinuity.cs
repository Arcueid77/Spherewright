using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void PreviousObservedTimeSurvivesOpenOnlyThenNormalExitBeforeFreshAdmission()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var originalSpent = s.Checkpoints.VoyagerJourney!.SpentGameTicks;
        var request = s.PrimaryRecoveryRequest(18, openedFirst: true);
        // Native normal shutdown saves the actually loaded tick. This file
        // fixture models that documented contract; it does not run Unity exit.
        s.WriteSave(GameSave.LastExit, 12510);
        request.AcceptUnstoredProgressLoss = false;
        request.MinimumRecoveryGameTick = null;
        request.ExpectedRecoveryGameTick = null;
        var plan = s.Prepare(request);
        Assert.Equal(0, plan.DiscardedUnstoredProgressGameTicks);
        Assert.Equal(18, plan.ConservedObservedProgressDebitGameTicks);
        Assert.True(s.Commit(plan).Success);
        Assert.Equal(12528, s.Sessions.RemoteEvidence!.KnownPrimaryProgressTick);
        // This checks the production durable-debit transition only. The
        // separate contact-chain fixture covers strict admission conditions.
        Assert.True(s.Sessions.CompleteContactForTest(null));
        Assert.Equal(originalSpent + 18, s.Checkpoints.VoyagerJourney.SpentGameTicks);
    }

    [Fact]
    public void RepeatedOpeningAndNormalExitRetainTheHighestObservedTime()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        s.PrimaryRecoveryRequest(18, openedFirst: true);
        s.PrimaryRecoveryRequest(0, openedFirst: true);
        var request = s.PrimaryRecoveryRequest(0, openedFirst: true);
        var plan = s.Prepare(request);
        Assert.Equal(0, plan.DiscardedUnstoredProgressGameTicks);
        Assert.Equal(18, plan.ConservedObservedProgressDebitGameTicks);
        Assert.True(s.Commit(plan).Success);
        Assert.Equal(12528, s.Sessions.RemoteEvidence!.KnownPrimaryProgressTick);
    }

    [Fact]
    public void AnOldOpenOnlyCredentialWithUnknownObservedTimeCannotAuthorizeAutomaticRecovery()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var request = s.PrimaryRecoveryRequest(18, openedFirst: true);
        s.ReplaceCurrentTicket(t => t.ObservedProgressGameTickFloor = null); s.ReopenPrimaryCredentials();
        var loads = DSPGame.LoadCalls;
        var result = s.PrepareResult(request);
        Assert.False(result.Success); Assert.Equal(loads, DSPGame.LoadCalls);
        var failure = result.Error!.NativeFirstFailure!;
        Assert.Equal("primary_prior_observed_time_known", failure.FirstFailedPredicate);
        Assert.Equal("unknown", failure.Predicates["primary_prior_observed_time_known"]);
        Assert.Null(failure.Values["prior_observed_progress_floor"]);
        Assert.Null(failure.CapturedAtGameTick);
        Assert.True(s.PrepareResult(new PrepareOwnedWorldResumeRequest
            { RecoveryMode = OwnedWorldResumeModes.OpenSavedPrimary }).Success);
    }

    [Fact]
    public void RetainedObservedTimeCannotSpendTheOriginalReturnReserve()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var request = s.PrimaryRecoveryRequest(18, openedFirst: true);
        s.ReplaceCurrentTicket(t => t.ObservedProgressGameTickFloor = 72510); s.ReopenPrimaryCredentials();
        var loads = DSPGame.LoadCalls; var result = s.PrepareResult(request);
        Assert.False(result.Success); Assert.Equal(loads, DSPGame.LoadCalls);
        var failure = result.Error!.NativeFirstFailure!;
        Assert.Equal("remaining_original_tick_reserve", failure.FirstFailedPredicate);
        Assert.Equal(72510d, failure.Values["prior_observed_progress_floor"]);
        Assert.Equal(60000d, failure.Values["progress_loss_debit_ticks"]);
    }

    [Fact]
    public void ChangedRetainedObservationCannotBorrowAnEarlierPreparedDigest()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var plan = s.Prepare(s.PrimaryRecoveryRequest(18, openedFirst: true));
        var loads = DSPGame.LoadCalls;
        s.ReplaceCurrentTicket(t => t.ObservedProgressGameTickFloor++);
        Assert.False(s.Commit(plan).Success); Assert.Equal(loads, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData("below_saved_tick")] [InlineData("negative")] [InlineData("wrong_scope")]
    public void AnInvalidTimeObservationNeverBecomesAnActiveExecutionCredential(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var request = s.PrimaryRecoveryRequest(18, openedFirst: true);
        s.ReplaceCurrentTicket(t =>
        {
            if (fault == "below_saved_tick") t.ObservedProgressGameTickFloor = 12509;
            if (fault == "negative") t.ObservedProgressGameTickFloor = -1;
            if (fault == "wrong_scope") t.SavedWorldOpenOnly = false;
        });
        s.ReopenPrimaryCredentials(); var loads = DSPGame.LoadCalls;
        Assert.False(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        Assert.False(s.PrepareResult(request).Success); Assert.Equal(loads, DSPGame.LoadCalls);
    }

    [Fact]
    public void AHealthySavedCredentialDoesNotDebitPreviouslyPersistedTimeAgain()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var originalSpent = s.Checkpoints.VoyagerJourney!.SpentGameTicks;
        Assert.True(s.Commit(s.Prepare(s.PrimaryRecoveryRequest(18, openedFirst: true))).Success);
        Assert.True(s.Sessions.CompleteContactForTest(null));
        Assert.Equal(originalSpent + 18, s.Checkpoints.VoyagerJourney.SpentGameTicks);
        Assert.False(s.Tickets.TryGetSavedWorldOpenProvenance(s.Token, out _, out _, out _));
        var journal = PluginJson.Deserialize<OwnedWorldGameplayJournalCheckpoint>(s.Sessions.RemoteEvidence!.JournalJson)!;
        s.ArmHealthySavedCredentialForContinuity(journal);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out var fresh, out _));
        Assert.Null(fresh!.ObservedProgressGameTickFloor);
        s.ReopenPrimaryCredentials();
        var request = WindowRequest(); request.RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery;
        var plan = s.Prepare(request);
        Assert.Equal(0, plan.ConservedObservedProgressDebitGameTicks);
        Assert.True(s.Commit(plan).Success); Assert.True(s.Sessions.CompleteContactForTest(null));
        Assert.Equal(originalSpent + 18, s.Checkpoints.VoyagerJourney.SpentGameTicks);
    }

    private sealed partial class Scenario
    {
        internal void ArmHealthySavedCredentialForContinuity(OwnedWorldGameplayJournalCheckpoint journal)
        {
            Tickets.ArmFromHealthySavedOwnedSession(Identity, "synthetic-after-healthy-save", 201, 12510, journal);
            Token = Tickets.CurrentResumeToken!;
        }
    }
}
