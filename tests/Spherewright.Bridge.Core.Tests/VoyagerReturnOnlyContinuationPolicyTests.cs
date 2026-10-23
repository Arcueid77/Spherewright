using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerReturnOnlyContinuationPolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private static VoyagerReturnOnlyContinuationFacts Facts() => new()
    {
        ExactOwnedNative = true,
        ExactTaskCredentialSession = true,
        OriginalCheckpointWitness = true,
        JournalBound = true,
        NoPendingRecovery = true,
        RemoteReturnPhase = true,
        CompleteSourceFiles = true,
        SourceGameTick = 139,
        PrimaryGameTick = 80,
        CredentialMinimumGameTick = 139,
        TaskStartedAtGameTick = 100,
        OriginalMaximumGameTicks = 54000,
        OriginalReturnLimitGameTicks = 54000,
        OriginalDeadlineUtc = Now.AddDays(-1),
        ProposedWallDeadlineUtc = Now.AddMinutes(20),
        NowUtc = Now,
    };

    [Fact]
    public void SourceAdmissionPreservesTheOldTicksAndUtcAndGrantsNoNativeHealth()
    {
        var f = Facts(); var original = f.OriginalDeadlineUtc;
        var d = VoyagerReturnOnlyContinuationPolicy.Evaluate(f);
        Assert.True(d.SourceEligible); Assert.Equal(53961, d.RemainingGameTicks);
        Assert.Equal(original, f.OriginalDeadlineUtc); Assert.Equal(54000, f.OriginalMaximumGameTicks);
        Assert.True(d.RequiresNewWallAuthority); Assert.Equal(f.ProposedWallDeadlineUtc, d.EffectiveDeadlineUtc);
    }

    [Theory]
    [InlineData(100)] // original pre-flight checkpoint is older than current progress
    [InlineData(999)] // a larger tick from an earlier branch proves no continuity
    public void OnlyTheExactProtectedCurrentBoundaryCanSupplyTheSource(long tick)
    {
        var f = Facts(); f.SourceGameTick = tick;
        var d = VoyagerReturnOnlyContinuationPolicy.Evaluate(f);
        Assert.False(d.SourceEligible);
        Assert.Equal("source_equals_credential_minimum", d.FirstFailure!.FirstFailedPredicate);
        Assert.Equal("not_reached", d.FirstFailure.Predicates["native_load"]);
    }

    [Fact]
    public void NoDeadlineMeansTheOriginalWindowMustStillBeValid()
    {
        var f = Facts(); f.ProposedWallDeadlineUtc = null;
        var d = VoyagerReturnOnlyContinuationPolicy.Evaluate(f);
        Assert.False(d.SourceEligible);
        Assert.Equal("wall_return_and_stop_reserved", d.FirstFailure!.FirstFailedPredicate);
        f.OriginalDeadlineUtc = Now.AddMinutes(20);
        d = VoyagerReturnOnlyContinuationPolicy.Evaluate(f);
        Assert.True(d.SourceEligible); Assert.False(d.RequiresNewWallAuthority);
    }

    [Theory]
    [InlineData("session", "task_session")]
    [InlineData("journal", "original_journal")]
    [InlineData("pending", "no_pending_recovery")]
    [InlineData("witness", "original_checkpoint_witness")]
    [InlineData("primary", "primary_not_newer")]
    [InlineData("cap", "original_task_cap")]
    [InlineData("exhausted", "remaining_return_ticks")]
    [InlineData("window", "new_wall_window_finite")]
    public void IllegalAndUnknownFactsFailClosedAtTheirOwnFirstPredicate(string change, string predicate)
    {
        var f = Facts();
        switch (change)
        {
            case "session": f.ExactTaskCredentialSession = false; break;
            case "journal": f.JournalBound = null; break;
            case "pending": f.NoPendingRecovery = false; break;
            case "witness": f.OriginalCheckpointWitness = false; break;
            case "primary": f.PrimaryGameTick = 140; break;
            case "cap": f.OriginalMaximumGameTicks++; break;
            case "exhausted": f.SourceGameTick = f.CredentialMinimumGameTick = 54099; break;
            case "window": f.ProposedWallDeadlineUtc = Now.AddSeconds(VoyagerReturnOnlyContinuationPolicy.MaximumWallSeconds + 1); break;
        }
        var d = VoyagerReturnOnlyContinuationPolicy.Evaluate(f);
        Assert.False(d.SourceEligible); Assert.Equal(predicate, d.FirstFailure!.FirstFailedPredicate);
        if (change == "journal") Assert.Equal("unknown", d.FirstFailure.Predicates[predicate]);
        Assert.Equal("not_reached", d.FirstFailure.Predicates["native_load"]);
    }

    [Theory]
    [InlineData(1200, true)] [InlineData(3600, true)] [InlineData(3601, false)]
    public void ExplicitWallWindowRemainsFiniteAndNeverChangesTheTickAllowance(int seconds, bool eligible)
    {
        var f = Facts(); f.ProposedWallDeadlineUtc = Now.AddSeconds(seconds);
        var d = VoyagerReturnOnlyContinuationPolicy.Evaluate(f);
        Assert.Equal(eligible, d.SourceEligible);
        Assert.Equal(53961, d.RemainingGameTicks);
        Assert.Equal(54000, f.OriginalMaximumGameTicks);
        Assert.Equal(Now.AddDays(-1), f.OriginalDeadlineUtc);
        if (!eligible) Assert.Equal("new_wall_window_finite", d.FirstFailure!.FirstFailedPredicate);
    }

    [Theory]
    [InlineData("confirm", "prepare_unconfirmed")]
    [InlineData("rollback", "no_rollback_request")]
    [InlineData("charge", "no_charging_grant")]
    [InlineData("outcome", "no_prior_outcome_claim")]
    [InlineData("old_token", "no_predecessor_replay")]
    public void UnsupportedRequestsExplainTheirFirstRejectionWithoutReadingAnyFile(string fault, string predicate)
    {
        var r = new Spherewright.Contracts.Sessions.PrepareOwnedWorldResumeRequest
        {
            RecoveryMode = Spherewright.Contracts.Sessions.OwnedWorldResumeModes.ResumeReturnOnlyTask,
            ResumeToken = "synthetic-token",
            ExpectedRecoveryGameTick = 139,
            RecoveryScope = VoyagerReturnOnlyContinuationPolicy.Scope
        };
        switch (fault)
        {
            case "confirm": r.UserConfirmedInConversation = true; break;
            case "rollback": r.AcceptUnstoredProgressLoss = true; break;
            case "charge": r.MaximumChargeGameTicks = 1; break;
            case "outcome": r.PriorActionKnownTerminalCompleted = true; break;
            case "old_token": r.PriorConsumedResumeToken = "old"; break;
        }
        Assert.NotNull(VoyagerReturnOnlyContinuationPolicy.ValidateRequest(r));
        var failure = VoyagerReturnOnlyContinuationPolicy.RequestFailure(r)!;
        Assert.Equal(predicate, failure.FirstFailedPredicate); Assert.Equal("false", failure.Predicates[predicate]);
        Assert.Null(failure.CapturedAtGameTick); Assert.Equal("not_reached", failure.Predicates["native_load"]);
    }
}
