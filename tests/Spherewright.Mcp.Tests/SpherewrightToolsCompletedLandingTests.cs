using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing_role")][InlineData("missing_failure_action")][InlineData("successful_flight_claim")]
    public async Task RemotePrimaryCompletedLandingEchoKeepsTheOriginalFlightFailed(string fault)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(55);
        var p = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, PlanToken = "synthetic-completed-landing-plan",
            RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery,
            RecoveryEvidenceVersion = VoyagerRemotePrimaryRecoveryPolicy.EvidenceVersion, RemotePrimaryRecoveryAuthorizationUsed = true,
            CompletedSavedLandingAdmissionUsed = true, OriginalFailedFlightActionId = Guid.NewGuid().ToString(),
            SourceGameVersion = "0.10.35.29151", TargetGameVersion = "0.10.35.29151",
            OriginalJourneyDeadlineUtc = DateTimeOffset.UtcNow.AddDays(-1), EffectiveRecoveryDeadlineUtc = deadline,
            RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope, MaximumChargeGameTicks = 3600, MaximumChargeWallSeconds = 120,
            NativeChargeFeasibilityRequiresFreshCapture = true, CandidateGameTick = 1200, MinimumGameTick = 1200,
            DiscardedUnstoredProgressGameTicks = 0, ConservedObservedProgressDebitGameTicks = 0,
            ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true,
            ConfirmationDigest = CanonicalStateHash.Combine("synthetic-completed-landing-digest"),
        };
        switch (fault)
        {
            case "missing_role": p.CompletedSavedLandingAdmissionUsed = false; break;
            case "missing_failure_action": p.OriginalFailedFlightActionId = ""; break;
            case "successful_flight_claim": p.SuccessfulFlightWitnessRetiredAtGameTick = 1100; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-saved-boundary-credential",
            recoveryMode: OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery, minimumRecoveryGameTick: 1200, expectedRecoveryGameTick: 1200,
            recoveryDeadlineUtc: deadline, recoveryScope: VoyagerRecoveryWindowProposal.TaskScope,
            maximumChargeGameTicks: 3600, maximumChargeWallSeconds: 120);
        if (fault == "valid") Assert.False(result.IsError, result.StructuredContent?.GetRawText());
        else Assert.True(result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-completed-landing-plan", result.StructuredContent!.Value.GetRawText());
    }
}
