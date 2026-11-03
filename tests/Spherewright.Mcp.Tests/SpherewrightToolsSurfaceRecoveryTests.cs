using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing_target")][InlineData("changed_target")][InlineData("work_cap")][InlineData("wall_cap")][InlineData("unexpected_target")][InlineData("partial_request")][InlineData("narrow_cap")]
    public async Task NewSurfaceRecoveryNeedsTheExactTargetEchoAndFullOptIn(string fault)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(55);
        var p = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, PlanToken = "synthetic-surface-recovery-plan",
            SurfaceRecoveryTarget = new Vector3Snapshot { X = 200, Y = 1, Z = 2 },
            SurfaceRecoveryMaximumWorkGameTicks = 600, SurfaceRecoveryMaximumWallSeconds = 20,
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
            case "missing_target": p.SurfaceRecoveryTarget = null; break;
            case "changed_target": p.SurfaceRecoveryTarget!.Z = 3; break;
            case "work_cap": p.SurfaceRecoveryMaximumWorkGameTicks = 601; break;
            case "wall_cap": p.SurfaceRecoveryMaximumWallSeconds = 21; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-saved-boundary-credential",
            recoveryMode: OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery, minimumRecoveryGameTick: 1200, expectedRecoveryGameTick: 1200,
            recoveryDeadlineUtc: deadline, recoveryScope: VoyagerRecoveryWindowProposal.TaskScope,
            maximumChargeGameTicks: fault == "narrow_cap" ? 599 : 3600, maximumChargeWallSeconds: 120,
            surfaceRecoveryTargetX: fault == "unexpected_target" ? null : 200f,
            surfaceRecoveryTargetY: fault is "unexpected_target" or "partial_request" ? null : 1f,
            surfaceRecoveryTargetZ: fault == "unexpected_target" ? null : 2f);
        if (fault is "partial_request" or "narrow_cap") Assert.Null(bridge.LastResumePrepareRequest);
        if (fault == "valid") Assert.False(result.IsError, result.StructuredContent?.GetRawText());
        else Assert.True(result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-surface-recovery-plan", result.StructuredContent!.Value.GetRawText());
    }
}
