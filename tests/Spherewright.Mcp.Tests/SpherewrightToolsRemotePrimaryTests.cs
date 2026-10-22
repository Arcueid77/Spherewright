using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")] [InlineData("marker_missing")] [InlineData("saved_open_only")]
    [InlineData("window_changed")] [InlineData("unknown_loss")] [InlineData("wrong_loss")]
    [InlineData("completed_claim")] [InlineData("version_changed")] [InlineData("inherited_origin")]
    [InlineData("unknown_conserved_debit")] [InlineData("refunded_conserved_debit")]
    public async Task FreshRemotePrimaryNeedsItsOwnCompleteAuthorityEcho(string fault)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(20);
        var p = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, PlanToken = "synthetic-primary-authority-plan",
            RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery,
            RecoveryEvidenceVersion = VoyagerRemotePrimaryRecoveryPolicy.EvidenceVersion, RemotePrimaryRecoveryAuthorizationUsed = true,
            SourceGameVersion = "0.10.35.29151", TargetGameVersion = "0.10.35.29151",
            OriginalJourneyDeadlineUtc = DateTimeOffset.UtcNow.AddDays(-1), EffectiveRecoveryDeadlineUtc = deadline,
            RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope, MaximumChargeGameTicks = 9000, MaximumChargeWallSeconds = 240,
            NativeChargeFeasibilityRequiresFreshCapture = true, CandidateGameTick = 77, MinimumGameTick = 77,
            DiscardedUnstoredProgressGameTicks = 11, ConservedObservedProgressDebitGameTicks = 11,
            ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true,
            ConfirmationDigest = CanonicalStateHash.Combine("synthetic-fresh-primary-consent"),
        };
        switch (fault)
        {
            case "marker_missing": p.RemotePrimaryRecoveryAuthorizationUsed = false; break;
            case "saved_open_only": p.OpensSavedWorldOnly = true; break;
            case "window_changed": p.EffectiveRecoveryDeadlineUtc = deadline.AddSeconds(1); break;
            case "unknown_loss": p.DiscardedUnstoredProgressGameTicks = null; break;
            case "wrong_loss": p.DiscardedUnstoredProgressGameTicks = 12; break;
            case "completed_claim": p.PriorActionTerminalCompletionAttested = true; break;
            case "version_changed": p.TargetGameVersion = "0.10.35.29145"; break;
            case "inherited_origin": p.InheritedWindowConfirmedAtUtc = DateTimeOffset.UtcNow; break;
            case "unknown_conserved_debit": p.ConservedObservedProgressDebitGameTicks = null; break;
            case "refunded_conserved_debit": p.ConservedObservedProgressDebitGameTicks = 10; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-owned-file-credential",
            recoveryMode: OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery,
            recoveryDeadlineUtc: deadline, recoveryScope: VoyagerRecoveryWindowProposal.TaskScope,
            maximumChargeGameTicks: 9000, maximumChargeWallSeconds: 240,
            minimumRecoveryGameTick: 88, expectedRecoveryGameTick: 77, acceptUnstoredProgressLoss: true);
        Assert.Equal(fault != "valid", result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-primary-authority-plan", result.StructuredContent!.Value.GetRawText());
        Assert.Equal(OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery, bridge.LastResumePrepareRequest!.RecoveryMode);
    }
}
