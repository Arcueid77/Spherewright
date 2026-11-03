using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerFailedLandingRecoveryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static PrepareOwnedWorldResumeRequest Request() => new()
    {
        RecoveryMode = VoyagerFailedLandingRecoveryPolicy.Mode, PriorActionKnownTerminalFailed = true,
        MinimumRecoveryGameTick = 1200, ExpectedRecoveryGameTick = 1200,
        RecoveryDeadlineUtc = Now.AddMinutes(55), RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope,
        MaximumChargeGameTicks = 3600, MaximumChargeWallSeconds = 120,
    };
    private static VoyagerFailedLandingSourceFacts Facts() => new()
    {
        FixedLastExit = true, CompleteSourceAndPrimary = true, ExactOwnedIdentityAndNativeVersion = true,
        Peaceful = true, ExactOriginalJourneyCheckpointAndJournal = true,
        OriginalCheckpointRecoveryRequired = true, OriginalOutboundFailureContext = true,
        ExactSelectedRemotePlanet = true, CallerAttestsTerminalFailure = true,
        SourceGameTick = 1200, PrimaryGameTick = 1000, TicketMinimumGameTick = 1000,
        CheckpointGameTick = 1000, LastAccountedGameTick = 1200, SpentGameTicks = 200,
        MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000, NowUtc = Now,
    };

    [Fact]
    public void CurrentFailedLandingBoundaryCreatesAccountingAnchorWithoutSuccessfulFlightWitness()
    {
        var f = Facts(); var result = VoyagerFailedLandingRecoveryPolicy.Evaluate(Request(), f);
        Assert.True(result.Proved);
        Assert.Equal(1200, result.SavedAccountingAnchorGameTick);
        Assert.Equal(200, result.ConservedSpentGameTicks);
        Assert.False(f.OriginalCheckpointFlightSucceeded);
        Assert.Equal(200, f.SpentGameTicks);
    }

    [Theory]
    [InlineData("source")][InlineData("identity")][InlineData("journal")][InlineData("checkpoint")]
    [InlineData("success")][InlineData("failure_context")][InlineData("planet")][InlineData("attestation")]
    public void UnknownForeignOrAlreadySuccessfulSourceCannotBorrowFailedLandingRole(string fault)
    {
        var f = Facts();
        switch (fault)
        {
            case "source": f.CompleteSourceAndPrimary = false; break;
            case "identity": f.ExactOwnedIdentityAndNativeVersion = false; break;
            case "journal": f.ExactOriginalJourneyCheckpointAndJournal = false; break;
            case "checkpoint": f.OriginalCheckpointRecoveryRequired = false; break;
            case "success": f.OriginalCheckpointFlightSucceeded = true; break;
            case "failure_context": f.OriginalOutboundFailureContext = false; break;
            case "planet": f.ExactSelectedRemotePlanet = false; break;
            case "attestation": f.CallerAttestsTerminalFailure = false; break;
        }
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(Request(), f).Proved);
    }

    [Theory]
    [InlineData("older_source")][InlineData("newer_primary")][InlineData("ticket_edge")][InlineData("future_accounting")]
    public void CompleteCurrentBoundaryNeverLoadsOlderPrimaryOrCheckpoint(string fault)
    {
        var f = Facts();
        switch (fault)
        {
            case "older_source": f.SourceGameTick--; break;
            case "newer_primary": f.PrimaryGameTick = 1201; break;
            case "ticket_edge": f.TicketMinimumGameTick = 1201; break;
            case "future_accounting": f.LastAccountedGameTick = 1201; break;
        }
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(Request(), f).Proved);
    }

    [Fact]
    public void UnaccountedBoundaryAndExhaustedOrOverflowedOriginalBudgetStayBlocked()
    {
        var undercounted = Facts(); undercounted.SpentGameTicks--;
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(Request(), undercounted).Proved);
        var f = Facts(); f.LastAccountedGameTick--;
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(Request(), f).Proved);
        f = Facts(); f.SpentGameTicks = f.MaximumGameTicks - f.ReturnReserveGameTicks;
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(Request(), f).Proved);
        f = Facts(); f.SpentGameTicks = long.MaxValue;
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(Request(), f).Proved);
    }

    [Fact]
    public void CallerCannotAuthorizeLossExtraTicksReplayOrAnOldDeadline()
    {
        var r = Request(); r.AcceptUnstoredProgressLoss = true;
        Assert.NotNull(VoyagerFailedLandingRecoveryPolicy.ValidateRequest(r));
        r = Request(); r.AdditionalVoyagerGameTicks = 1;
        Assert.NotNull(VoyagerFailedLandingRecoveryPolicy.ValidateRequest(r));
        r = Request(); r.PriorConsumedResumeToken = "old-token";
        Assert.NotNull(VoyagerFailedLandingRecoveryPolicy.ValidateRequest(r));
        r = Request(); r.RecoveryDeadlineUtc = Now.AddMinutes(-1);
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(r, Facts()).Proved);
        r = Request(); r.RecoveryDeadlineUtc = Now.Add(VoyagerRecoveryWindowPolicy.MaximumConfirmedWindow).AddSeconds(1);
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(r, Facts()).Proved);
    }

    [Fact]
    public void FreshWindowStillReservesTheEntireOriginalReturnWallInterval()
    {
        var r = Request(); r.RecoveryDeadlineUtc = Now.AddSeconds(901);
        Assert.False(VoyagerFailedLandingRecoveryPolicy.Evaluate(r, Facts()).Proved);
        r.RecoveryDeadlineUtc = Now.AddMinutes(55);
        Assert.True(VoyagerFailedLandingRecoveryPolicy.Evaluate(r, Facts()).Proved);
    }
    [Fact]
    public void ExplicitFiniteLongerWindowPreservesOriginalSpentTickCapAndReturnReserve()
    {
        var r=Request();r.RecoveryDeadlineUtc=Now.AddHours(12);var f=Facts();
        var d=VoyagerFailedLandingRecoveryPolicy.Evaluate(r,f);Assert.True(d.Proved);
        Assert.Equal(200,d.ConservedSpentGameTicks);Assert.Equal(108000,f.MaximumGameTicks);Assert.Equal(54000,f.ReturnReserveGameTicks);
        r.AdditionalVoyagerGameTicks=1;Assert.NotNull(VoyagerFailedLandingRecoveryPolicy.ValidateRequest(r));
    }
}
