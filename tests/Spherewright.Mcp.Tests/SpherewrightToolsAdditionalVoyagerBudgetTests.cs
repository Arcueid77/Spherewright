using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")] [InlineData("wrong_addition")] [InlineData("missing_original")]
    [InlineData("wrong_effective")] [InlineData("invalid_original")]
    public async Task AdditionalVoyagerBudgetRequiresExactPreparedEcho(string fault)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(30);
        var p = new PreparedOwnedWorldResumePlan {
            Prepared = true, PlanToken = "synthetic-additional-budget-plan",
            RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery,
            RecoveryEvidenceVersion = VoyagerRemotePrimaryRecoveryPolicy.EvidenceVersion, RemotePrimaryRecoveryAuthorizationUsed = true,
            SourceGameVersion = "0.10.35.29151", TargetGameVersion = "0.10.35.29151",
            OriginalJourneyDeadlineUtc = deadline.AddDays(-1), EffectiveRecoveryDeadlineUtc = deadline,
            RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope, MaximumChargeGameTicks = 18000, MaximumChargeWallSeconds = 300,
            AdditionalVoyagerGameTicks = 54000, OriginalJourneyMaximumGameTicks = 108000, EffectiveJourneyMaximumGameTicks = 162000,
            NativeChargeFeasibilityRequiresFreshCapture = true, CandidateGameTick = 77, MinimumGameTick = 77,
            DiscardedUnstoredProgressGameTicks = 0, ConservedObservedProgressDebitGameTicks = 0,
            ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true,
            ConfirmationDigest = CanonicalStateHash.Combine("synthetic-additional-budget"),
        };
        switch (fault) {
            case "wrong_addition": p.AdditionalVoyagerGameTicks--; break;
            case "missing_original": p.OriginalJourneyMaximumGameTicks = null; break;
            case "wrong_effective": p.EffectiveJourneyMaximumGameTicks--; break;
            case "invalid_original": p.OriginalJourneyMaximumGameTicks = 108001; p.EffectiveJourneyMaximumGameTicks = 162001; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-current-primary-token",
            recoveryMode: OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery, recoveryDeadlineUtc: deadline,
            recoveryScope: VoyagerRecoveryWindowProposal.TaskScope, maximumChargeGameTicks: 18000, maximumChargeWallSeconds: 300,
            additionalVoyagerGameTicks: 54000);
        Assert.Equal(fault != "valid", result.IsError);
        Assert.Equal(54000, bridge.LastResumePrepareRequest!.AdditionalVoyagerGameTicks);
        if (fault != "valid") Assert.DoesNotContain("synthetic-additional-budget-plan", result.StructuredContent!.Value.GetRawText());
    }
}
