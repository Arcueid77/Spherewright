using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class OwnedLastExitOpenPolicyTests
{
    [Fact]
    public void ExactNewestSourceOpensDataOnlyWithoutRenewingTasks()
    {
        var request=Request();Assert.Null(OwnedWorldRecoveryPolicy.ValidateRequest(request));
        using var stream=OwnedSavePrefixReaderTests.Fixture(identity:"synthetic-exact-owned-world");
        var source=OwnedSavePrefixReader.Read(stream,"synthetic-exact-owned-world");
        request.MinimumRecoveryGameTick=request.ExpectedRecoveryGameTick=source.GameTick;
        Assert.Null(OwnedLastExitOpenPolicy.ValidateCandidate(request,source.GameTick-10,source.GameVersion,source));
        Assert.NotNull(OwnedLastExitOpenPolicy.ValidateCandidate(request,source.GameTick+1,source.GameVersion,source));
        request.ExpectedRecoveryGameTick--;Assert.NotNull(OwnedLastExitOpenPolicy.ValidateCandidate(request,source.GameTick,source.GameVersion,source));
    }

    [Theory]
    [InlineData("loss")][InlineData("different-tick")][InlineData("charging")][InlineData("deadline")]
    [InlineData("outcome")][InlineData("continuation")][InlineData("confirmation")]
    public void OpeningCannotConcealRollbackOrExecutionAuthority(string fault)
    {
        var request=Request();
        switch(fault)
        {
            case "loss":request.AcceptUnstoredProgressLoss=true;break;
            case "different-tick":request.ExpectedRecoveryGameTick=1000;break;
            case "charging":request.MaximumChargeGameTicks=1;break;
            case "deadline":request.RecoveryDeadlineUtc=DateTimeOffset.UtcNow.AddHours(1);break;
            case "outcome":request.PriorActionKnownTerminalFailed=true;break;
            case "continuation":request.RecoveryScope="continue-original";break;
            case "confirmation":request.UserConfirmedInConversation=true;break;
        }
        Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(request));
    }

    [Fact]
    public void OldPluginOrMissingExactLastExitEchoCannotExposeAToken()
    {
        var plan=new PreparedOwnedWorldResumePlan {Prepared=true,RecoveryMode=OwnedWorldResumeModes.OpenSavedLastExit,
            RecoveryEvidenceVersion=1,OpensSavedWorldOnly=true,PreviousAutomationRemainsStopped=true,
            NewChargingProhibited=true,ExactEmbeddedIdentityVerified=true,UserConfirmationRequired=true,
            CandidateGameTick=1100,MinimumGameTick=1100,DiscardedUnstoredProgressGameTicks=0,
            ConfirmationDigest="synthetic-disclosure"};
        Assert.False(OwnedLastExitOpenPolicy.ConfirmsEcho(plan));plan.OpensExactLastExitOnly=true;
        Assert.True(OwnedLastExitOpenPolicy.ConfirmsEcho(plan));plan.CommitAllowedNow=true;
        Assert.False(OwnedLastExitOpenPolicy.ConfirmsEcho(plan));
    }
    private static PrepareOwnedWorldResumeRequest Request()=>new() {RecoveryMode=OwnedWorldResumeModes.OpenSavedLastExit,
        MinimumRecoveryGameTick=1100,ExpectedRecoveryGameTick=1100};
}
