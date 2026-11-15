using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.Tools;
using Spherewright.Mcp.BridgeClient;
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

    [Theory]
    [InlineData("valid")] [InlineData("missing_role")] [InlineData("both_roles")]
    [InlineData("discarded_progress")] [InlineData("false_failed_flight")] [InlineData("wrong_target")]
    [InlineData("missing_expected_echo")] [InlineData("wrong_expected_echo")] [InlineData("missing_expected_request")]
    public async Task SavedSuccessfulOutboundSurfaceRecoveryNeedsAnExclusiveNoLossEcho(string fault)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(55);
        var p = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, PlanToken = "synthetic-successful-surface-token",
            SurfaceRecoveryTarget = new Vector3Snapshot { X = 200, Y = 1, Z = 2 },
            SurfaceRecoveryMaximumWorkGameTicks = 600, SurfaceRecoveryMaximumWallSeconds = 20,
            RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery,
            RecoveryEvidenceVersion = VoyagerRemotePrimaryRecoveryPolicy.EvidenceVersion,
            RemotePrimaryRecoveryAuthorizationUsed = true, SuccessfulOutboundSurfaceSourceUsed = true,
            SurfaceRecoveryExpectedSourceGameTick = 1200,
            SourceGameVersion = "0.10.35.29151", TargetGameVersion = "0.10.35.29151",
            OriginalJourneyDeadlineUtc = DateTimeOffset.UtcNow.AddDays(-1), EffectiveRecoveryDeadlineUtc = deadline,
            RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope, MaximumChargeGameTicks = 3600, MaximumChargeWallSeconds = 120,
            NativeChargeFeasibilityRequiresFreshCapture = true, CandidateGameTick = 1200, MinimumGameTick = 1200,
            DiscardedUnstoredProgressGameTicks = 0, ConservedObservedProgressDebitGameTicks = 0,
            ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true,
            ConfirmationDigest = CanonicalStateHash.Combine("synthetic-successful-surface-digest"),
        };
        switch (fault)
        {
            case "missing_role": p.SuccessfulOutboundSurfaceSourceUsed = false; break;
            case "both_roles": p.CompletedSavedLandingAdmissionUsed = true; p.OriginalFailedFlightActionId = Guid.NewGuid().ToString(); break;
            case "discarded_progress": p.DiscardedUnstoredProgressGameTicks = 1; break;
            case "false_failed_flight": p.OriginalFailedFlightActionId = Guid.NewGuid().ToString(); break;
            case "wrong_target": p.SurfaceRecoveryTarget!.Z = 3; break;
            case "missing_expected_echo": p.SurfaceRecoveryExpectedSourceGameTick = null; break;
            case "wrong_expected_echo": p.SurfaceRecoveryExpectedSourceGameTick = 1201; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-saved-boundary-credential",
            recoveryMode: OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery, minimumRecoveryGameTick: 1200, expectedRecoveryGameTick: 1200,
            recoveryDeadlineUtc: deadline, recoveryScope: VoyagerRecoveryWindowProposal.TaskScope,
            maximumChargeGameTicks: 3600, maximumChargeWallSeconds: 120,
            surfaceRecoveryTargetX: 200, surfaceRecoveryTargetY: 1, surfaceRecoveryTargetZ: 2,
            surfaceRecoveryExpectedSourceGameTick: fault == "missing_expected_request" ? null : 1200);
        Assert.Equal(fault != "valid", result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-successful-surface-token", result.StructuredContent!.Value.GetRawText());
    }

    [Fact]
    public async Task SdkForwardsTheExactSavedSurfaceBoundaryWithoutLoading()
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(55);
        var p = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, PlanToken = "synthetic-surface-token", SurfaceRecoveryTarget = new() { X = 200, Y = 1, Z = 2 },
            SurfaceRecoveryExpectedSourceGameTick = 1200, SurfaceRecoveryMaximumWorkGameTicks = 600, SurfaceRecoveryMaximumWallSeconds = 20,
            RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery, RecoveryEvidenceVersion = VoyagerRemotePrimaryRecoveryPolicy.EvidenceVersion,
            RemotePrimaryRecoveryAuthorizationUsed = true, SuccessfulOutboundSurfaceSourceUsed = true,
            SourceGameVersion = "synthetic-native", TargetGameVersion = "synthetic-native", OriginalJourneyDeadlineUtc = deadline.AddDays(-1),
            EffectiveRecoveryDeadlineUtc = deadline, RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope,
            MaximumChargeGameTicks = 3600, MaximumChargeWallSeconds = 120, NativeChargeFeasibilityRequiresFreshCapture = true,
            CandidateGameTick = 1200, MinimumGameTick = 1200, DiscardedUnstoredProgressGameTicks = 0,
            ConservedObservedProgressDebitGameTicks = 0, ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true,
            ConfirmationDigest = CanonicalStateHash.Combine("synthetic-surface-digest"),
        };
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var services = new ServiceCollection(); services.AddSingleton<IBridgeClient>(bridge);
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(), t => t.ProtocolTool.Name == "spherewright_prepare_resume_owned_game");
        Assert.True(tool.ProtocolTool.InputSchema.GetProperty("properties").TryGetProperty("surfaceRecoveryExpectedSourceGameTick", out _));
        await using var server = McpServer.Create(new StreamServerTransport(Stream.Null, Stream.Null), new McpServerOptions(), null, provider);
        var parameters = new CallToolRequestParams { Name = tool.ProtocolTool.Name, Arguments = new Dictionary<string, JsonElement>
        {
            ["resumeToken"] = JsonSerializer.SerializeToElement("synthetic-credential"),
            ["recoveryMode"] = JsonSerializer.SerializeToElement(OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery),
            ["recoveryDeadlineUtc"] = JsonSerializer.SerializeToElement(deadline),
            ["recoveryScope"] = JsonSerializer.SerializeToElement(VoyagerRecoveryWindowProposal.TaskScope),
            ["maximumChargeGameTicks"] = JsonSerializer.SerializeToElement(3600), ["maximumChargeWallSeconds"] = JsonSerializer.SerializeToElement(120),
            ["surfaceRecoveryTargetX"] = JsonSerializer.SerializeToElement(200), ["surfaceRecoveryTargetY"] = JsonSerializer.SerializeToElement(1),
            ["surfaceRecoveryTargetZ"] = JsonSerializer.SerializeToElement(2), ["surfaceRecoveryExpectedSourceGameTick"] = JsonSerializer.SerializeToElement(1200),
        }};
        var context = new RequestContext<CallToolRequestParams>(server, new JsonRpcRequest { Id = new RequestId(1), Method = "tools/call" }, parameters)
        { Services = provider };
        var result = await tool.InvokeAsync(context);
        Assert.False(result.IsError, result.StructuredContent?.GetRawText());
        Assert.Equal(1200, bridge.LastResumePrepareRequest!.SurfaceRecoveryExpectedSourceGameTick);
        Assert.Null(bridge.LastResumeCommitRequest);
    }

    [Fact]
    public async Task ExpectedSurfaceBoundaryWithoutATargetIsRefusedBeforeBridgeAccess()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-credential",
            surfaceRecoveryExpectedSourceGameTick: 1200);
        Assert.True(result.IsError); Assert.Null(bridge.LastResumePrepareRequest);
    }
}
