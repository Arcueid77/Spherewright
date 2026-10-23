using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    // This tests the MCP boundary, not Unity health. The real Store/coordinator
    // tests separately check that production plans satisfy this same policy.
    [Theory]
    [InlineData("valid_new_window")] [InlineData("valid_existing_window")]
    [InlineData("old_plugin")] [InlineData("missing_preview")]
    [InlineData("wrong_source")] [InlineData("unknown_source")]
    [InlineData("window_changed")] [InlineData("preview_window_changed")]
    [InlineData("missing_original_window")] [InlineData("missing_authority_marker")]
    [InlineData("charging")] [InlineData("charge_ticks")] [InlineData("charge_seconds")]
    [InlineData("charge_feasibility")] [InlineData("scope")]
    [InlineData("unknown_remaining")] [InlineData("excess_remaining")]
    [InlineData("confirmation")] [InlineData("already_committable")]
    [InlineData("prior_outcome_claim")] [InlineData("saved_open_only")]
    public async Task ReturnOnlyContinuationRequiresItsOwnExactEcho(string fault)
    {
        var newWindow = fault != "valid_existing_window";
        var deadline = DateTimeOffset.UtcNow.AddHours(1);
        var p = ReturnContinuationPlan(deadline, newWindow);
        switch (fault)
        {
            case "old_plugin": p.RecoveryMode = OwnedWorldResumeModes.Default; break;
            case "missing_preview": p.ReturnOnlyContinuation = null; break;
            case "wrong_source": p.ReturnOnlyContinuation!.SourceGameTick++; break;
            case "unknown_source": p.CandidateGameTick = null; break;
            case "window_changed": p.EffectiveRecoveryDeadlineUtc = deadline.AddSeconds(1); break;
            case "preview_window_changed": p.ReturnOnlyContinuation!.EffectiveDeadlineUtc = deadline.AddSeconds(1); break;
            case "missing_original_window": p.OriginalJourneyDeadlineUtc = null; break;
            case "missing_authority_marker": p.ReturnOnlyContinuation!.NewWallAuthorityRequired = false; break;
            case "charging": p.NewChargingProhibited = false; break;
            case "charge_ticks": p.MaximumChargeGameTicks = 1; break;
            case "charge_seconds": p.MaximumChargeWallSeconds = 1; break;
            case "charge_feasibility": p.NativeChargeFeasibilityRequiresFreshCapture = true; break;
            case "scope": p.RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope; break;
            case "unknown_remaining": p.ReturnOnlyContinuation!.RemainingGameTicks = 0; break;
            case "excess_remaining": p.ReturnOnlyContinuation!.RemainingGameTicks = 54001; break;
            case "confirmation": p.ConfirmationDigest = ""; break;
            case "already_committable": p.CommitAllowedNow = true; break;
            case "prior_outcome_claim": p.PriorActionTerminalCompletionAttested = true; break;
            case "saved_open_only": p.OpensSavedWorldOnly = true; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-current-credential",
            recoveryMode: OwnedWorldResumeModes.ResumeReturnOnlyTask,
            expectedRecoveryGameTick: 4242,
            recoveryDeadlineUtc: newWindow ? deadline : null,
            recoveryScope: VoyagerReturnOnlyContinuationPolicy.Scope);
        var valid = fault.StartsWith("valid_", StringComparison.Ordinal);
        Assert.Equal(!valid, result.IsError);
        Assert.Equal(newWindow ? deadline : (DateTimeOffset?)null, bridge.LastResumePrepareRequest!.RecoveryDeadlineUtc);
        if (!valid)
        {
            var body = result.StructuredContent!.Value;
            Assert.DoesNotContain(p.PlanToken, body.GetRawText());
            Assert.Equal(BridgeErrorCodes.BridgeNotReady, body.GetProperty("error").GetProperty("code").GetString());
            var cut = body.GetProperty("error").GetProperty("nativeFirstFailure");
            Assert.Equal("return_only_continuation_echo", cut.GetProperty("stage").GetString());
            Assert.Equal("not_reached", cut.GetProperty("predicates").GetProperty("native_load").GetString());
            Assert.Equal(JsonValueKind.Null, cut.GetProperty("capturedAtGameTick").ValueKind);
        }
    }

    [Fact]
    public async Task ReturnOnlyContinuationWithoutNewDeadlineStillWithholdsAnUnmatchedOldPluginPlan()
    {
        var p = ReturnContinuationPlan(DateTimeOffset.UtcNow.AddMinutes(20), false);
        p.ReturnOnlyContinuation = null;
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = p };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "synthetic-current-credential",
            recoveryMode: OwnedWorldResumeModes.ResumeReturnOnlyTask, expectedRecoveryGameTick: 4242,
            recoveryScope: VoyagerReturnOnlyContinuationPolicy.Scope);
        Assert.True(result.IsError);
        Assert.DoesNotContain(p.PlanToken, result.StructuredContent!.Value.GetRawText());
        var cut = result.StructuredContent.Value.GetProperty("error").GetProperty("nativeFirstFailure");
        Assert.Equal("typed_return_preview_observed", cut.GetProperty("firstFailedPredicate").GetString());
        Assert.Equal("not_reached", cut.GetProperty("predicates").GetProperty("native_load").GetString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReturnOnlyContinuationForwardsRealJsonArgumentsWithoutAddingChargeAuthority(bool newWindow)
    {
        var deadline = DateTimeOffset.UtcNow.AddHours(1);
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = ReturnContinuationPlan(deadline, newWindow) };
        var services = new ServiceCollection(); services.AddSingleton<IBridgeClient>(bridge);
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(),
            t => t.ProtocolTool.Name == "spherewright_prepare_resume_owned_game");
        await using var server = McpServer.Create(new StreamServerTransport(Stream.Null, Stream.Null), new McpServerOptions(), null, provider);
        var parameters = new CallToolRequestParams { Name = tool.ProtocolTool.Name, Arguments = new Dictionary<string, JsonElement>
        {
            ["resumeToken"] = JsonSerializer.SerializeToElement("synthetic-current-credential"),
            ["recoveryMode"] = JsonSerializer.SerializeToElement(OwnedWorldResumeModes.ResumeReturnOnlyTask),
            ["expectedRecoveryGameTick"] = JsonSerializer.SerializeToElement(4242),
            ["recoveryScope"] = JsonSerializer.SerializeToElement(VoyagerReturnOnlyContinuationPolicy.Scope),
        } };
        if (newWindow) parameters.Arguments["recoveryDeadlineUtc"] = JsonSerializer.SerializeToElement(deadline);
        var context = new RequestContext<CallToolRequestParams>(server,
            new JsonRpcRequest { Id = new RequestId(1), Method = "tools/call" }, parameters) { Services = provider };
        var result = await tool.InvokeAsync(context);
        Assert.False(result.IsError, result.StructuredContent?.GetRawText());
        var forwarded = bridge.LastResumePrepareRequest!;
        Assert.Equal(OwnedWorldResumeModes.ResumeReturnOnlyTask, forwarded.RecoveryMode);
        Assert.Equal(0, forwarded.MaximumChargeGameTicks); Assert.Equal(0, forwarded.MaximumChargeWallSeconds);
        Assert.False(forwarded.UserConfirmedInConversation);
        var echo = result.StructuredContent!.Value.GetProperty("result");
        Assert.True(echo.GetProperty("newChargingProhibited").GetBoolean());
        Assert.False(echo.GetProperty("returnOnlyContinuation").GetProperty("newTicksGranted").GetBoolean());
    }

    private static PreparedOwnedWorldResumePlan ReturnContinuationPlan(DateTimeOffset deadline, bool newWindow) => new()
    {
        Prepared = true, PlanToken = "synthetic-return-continuation-plan",
        RecoveryMode = OwnedWorldResumeModes.ResumeReturnOnlyTask,
        RecoveryEvidenceVersion = VoyagerRemoteResumePolicy.EvidenceVersion,
        OriginalJourneyDeadlineUtc = deadline.AddDays(-2), EffectiveRecoveryDeadlineUtc = deadline,
        RecoveryScope = VoyagerReturnOnlyContinuationPolicy.Scope,
        SourceGameVersion = "0.10.35.29151", TargetGameVersion = "0.10.35.29151",
        MinimumGameTick = 4242, CandidateGameTick = 4242, NewChargingProhibited = true,
        ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true,
        ConfirmationDigest = CanonicalStateHash.Combine("synthetic-return-continuation"),
        ReturnOnlyContinuation = new() {
            OriginalTaskId = "synthetic-original-task", SourceGameTick = 4242,
            OriginalMaximumGameTicks = 54000, RemainingGameTicks = 35000,
            OriginalDeadlineUtc = newWindow ? deadline.AddDays(-1) : deadline,
            EffectiveDeadlineUtc = deadline, NewWallAuthorityRequired = newWindow,
        },
        CommitBlockers = new() { new() { Code = BridgeErrorCodes.UserConfirmationRequired } },
    };
}
