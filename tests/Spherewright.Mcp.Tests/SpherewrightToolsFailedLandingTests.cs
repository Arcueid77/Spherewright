using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing_role")][InlineData("wrong_anchor")][InlineData("success_claim")]
    [InlineData("added_ticks")][InlineData("other_planet")][InlineData("wrong_version")][InlineData("renewed_budget")]
    [InlineData("missing_failure")][InlineData("no_fresh_capture")]
    public async Task FailedLandingRequiresItsOwnSavedBoundaryAndRetainedFailureEcho(string fault)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(55);
        var p = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, PlanToken = "synthetic-failed-landing-plan", ExpectedPlanetId = 201,
            RecoveryMode = OwnedWorldResumeModes.AuthorizeFailedLandingLastExit,
            RecoveryEvidenceVersion = 1, FailedLandingRecoveryAuthorizationUsed = true,
            SavedLandingAccountingAnchorGameTick = 1200, CandidateGameTick = 1200, MinimumGameTick = 1200,
            OriginalFailedFlightActionId = Guid.NewGuid().ToString(), PriorActionTerminalFailureAttested = true,
            SourceGameVersion = "0.10.35.29151", TargetGameVersion = "0.10.35.29151",
            OriginalJourneyDeadlineUtc = DateTimeOffset.UtcNow.AddDays(-1), EffectiveRecoveryDeadlineUtc = deadline,
            RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope, MaximumChargeGameTicks = 3600, MaximumChargeWallSeconds = 120,
            OriginalJourneyMaximumGameTicks = 108000, EffectiveJourneyMaximumGameTicks = 108000,
            NativeChargeFeasibilityRequiresFreshCapture = true, ExactEmbeddedIdentityVerified = true,
            UserConfirmationRequired = true, ConfirmationDigest = CanonicalStateHash.Combine("synthetic-failed-landing-consent"),
        };
        switch (fault)
        {
            case "missing_role": p.FailedLandingRecoveryAuthorizationUsed = false; break;
            case "wrong_anchor": p.SavedLandingAccountingAnchorGameTick++; break;
            case "success_claim": p.SuccessfulFlightWitnessRetiredAtGameTick = 1100; break;
            case "added_ticks": p.AdditionalVoyagerGameTicks = 1; break;
            case "other_planet": p.ExpectedPlanetId = 0; break;
            case "wrong_version": p.TargetGameVersion = "foreign"; break;
            case "renewed_budget": p.EffectiveJourneyMaximumGameTicks++; break;
            case "missing_failure": p.PriorActionTerminalFailureAttested = false; break;
            case "no_fresh_capture": p.NativeChargeFeasibilityRequiresFreshCapture = false; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-failed-landing-credential",
            recoveryMode: OwnedWorldResumeModes.AuthorizeFailedLandingLastExit,
            minimumRecoveryGameTick: 1200, expectedRecoveryGameTick: 1200,
            recoveryDeadlineUtc: deadline, recoveryScope: VoyagerRecoveryWindowProposal.TaskScope,
            maximumChargeGameTicks: 3600, maximumChargeWallSeconds: 120, priorActionKnownTerminalFailed: true);
        Assert.Equal(fault != "valid", result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-failed-landing-plan", result.StructuredContent!.Value.GetRawText());
        Assert.Equal(OwnedWorldResumeModes.AuthorizeFailedLandingLastExit, bridge.LastResumePrepareRequest!.RecoveryMode);
    }
}
