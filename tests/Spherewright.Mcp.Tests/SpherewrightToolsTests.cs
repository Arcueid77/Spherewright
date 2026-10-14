using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Diagnostics;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Power;
using Spherewright.Contracts.Progression;
using Spherewright.Contracts.Protocol;
using Spherewright.Contracts.Resources;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Testing;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Resources;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed class SpherewrightToolsTests
{
    [Fact]
    public void BlueprintTropicSnappingGuidanceRequiresReturnedPosesAndFreshApproval()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.InspectBlueprintAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var text in new[] { description, guide })
        {
            Assert.Contains("SnapTropic", text);
            Assert.Contains("returned per-object poses", text);
            Assert.Contains("fresh approval", text);
        }
        Assert.Contains("does not waive", guide);
        Assert.Contains("Do not replay", guide);
    }

    [Fact]
    public void NativeOutputAdmissionThresholdGuidanceIsDiscoverableWithoutTreatingItAsStorageCapacity()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.GetOverseerProductionAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var text in new[] { description, guide })
        {
            Assert.Contains("output_buffer_capacity", text);
            Assert.Contains("next-batch admission threshold", text);
            Assert.Contains("not physical storage capacity", text);
            Assert.Contains("19", text);
        }
        Assert.Contains("101-q", guide);
        Assert.Contains("9*q+1", guide);
        Assert.Contains("19*q+1", guide);
        Assert.Contains("Do not clear storage", guide);
    }

    [Fact]
    public void LongCycleOutputBlockageGuidanceIsDiscoverableWithoutClaimingSustainableSupply()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.GetOverseerProductionAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var text in new[] { description, guide })
        {
            Assert.Contains("stopped and fully powered", text);
            Assert.Contains("output_blocked", text);
            Assert.Contains("complete-cycle requirement", text);
            Assert.Contains("An absent finding is not proof of sustainable supply", text);
        }
        Assert.Contains("unknown rate/power/capacity", guide);
        Assert.Contains("do not replay successful construction", guide);
    }

    [Fact]
    public void AttachedJournalGuidanceDoesNotInferNeverMadeFromMissingFirst()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.GetGameplayJournalAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var text in new[] { description, guide })
        {
            Assert.Contains("attached_existing_save", text);
            Assert.Contains("historicalCoverageComplete=false", text);
            Assert.Contains("not proof that the item was never made", text);
            Assert.Contains("durableThroughSequence", text);
            Assert.Contains("Never replay a completed craft", text);
        }
        Assert.Contains("complete old entry prefix", guide);
        Assert.Contains("no pending persistence and no persistence error", guide);
        Assert.Contains("not a mandatory +1", guide);
        Assert.Contains("backfill pre-tracking history", guide);
    }

    [Fact]
    public void TechCompletionRewardsAreDiscoverableAsMetadataNotDeliveryOrProduction()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.GetProgressionStateAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        var resource = AgentPlaybookResources.GetOpeningMovementPlaybook();
        Assert.Equal(AgentPlaybookResources.OpeningMovementUri, resource.Uri);
        foreach (var text in new[] { description, resource.Text })
        {
            Assert.Contains("completionItemRewards", text);
            Assert.Contains("not a delivery receipt", text);
            Assert.Contains("null/missing means unknown", text);
            Assert.Contains("empty list means observed no rewards", text);
            Assert.Contains("unlockTick", text);
            Assert.Contains("Never replay a completed action", text);
        }
        Assert.Contains("not handcraft or production", resource.Text);
        Assert.Contains("capacity overflow", resource.Text);
        Assert.Contains("multiple possible sources", resource.Text);
        Assert.Contains("at most32 entries", resource.Text);
        Assert.Contains("must not create or backfill a first-production Journal event", resource.Text);
    }

    [Fact]
    public void StalledMoveGuidanceIncludesSmallAndCoincidentFactoryObjects()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.CommitMoveAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        Assert.Contains("including belts and sorters/inserters", guide);
        Assert.Contains("including belts and sorters/inserters", description);
        Assert.Contains("Retain every distinct object ID even when positions coincide", guide);
        Assert.Contains("current catalog/inspection for item identity", guide);
        Assert.Contains("center distance alone is not walking clearance", description);
        Assert.Contains("not horizontal walking footprints", guide);
    }

    [Fact]
    public void StalledMoveGuidanceKeepsObservationAndRecoveryBounds()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("incomplete or expired pagination cannot rule out obstacles", guide);
        Assert.Contains("stay outside the observed obstacle cluster", guide);
        Assert.Contains("crossing the same obstruction is not a new route", guide);
        Assert.Contains("Do not dismantle a working line", guide);
        Assert.Contains("do not submit the same target again", guide);
        Assert.Contains("four targets", guide);
        Assert.Contains("each direction **once**", guide);
        Assert.Contains("All recovery attempts use the existing", guide);
        Assert.Contains("movementState=Walk", guide);
    }

    [Fact]
    public void OpeningPlaybookDistinguishesUnfilteredSorterReadbackFromRequestZero()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("componentKind=inserter", guide);
        Assert.Contains("filterItemId=null", guide);
        Assert.Contains("filterItemId=0", guide);
        Assert.Contains("request and response representations differ", guide);
        Assert.Contains("missing property, failed inspection or non-inserter is still unknown", guide);
        Assert.Contains("preserve any accepted action and never replay it", guide);
        Assert.Contains("does not require repeating build-path or placement previews", guide);
        Assert.Contains("`targetObjectId` is optional", guide);
        Assert.Contains("not a request-hash echo", guide);
        Assert.Contains("Keep explicitly documented mode-specific checks", guide);
    }

    [Fact]
    public void BeltShortageRecoveryIsDiscoverableWithoutClaimingPlacementApproval()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareBuildAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var text in new[] { description, guide })
        {
            Assert.Contains("INVENTORY_INSUFFICIENT", text);
            Assert.Contains("do not retry with unchanged inventory", text);
            Assert.Contains("normal handcraft or transfer", text);
            Assert.Contains("revalidate the complete path", text);
            Assert.Contains("same explicit endpoint bindings", text);
            Assert.Contains("does not prove placement", text);
        }
        Assert.Contains("Older Plugin versions", guide);
        Assert.Contains("Mixed, unknown, occupancy and cover failures", guide);
    }

    [Fact]
    public async Task BeltShortageErrorReachesMcpWithoutAPlanOrConstructionToken()
    {
        var error = Spherewright.Bridge.Core.Factory.BeltBuildRejectionPolicy.DescribeInventoryShortage(
            new[] { "Ok", "NotEnoughItem" }, true, false)!;
        var bridge = new FakeBridgeClient(SuccessResult()) { BuildPrepareError = error };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player");
        Assert.True(result.IsError);
        var content = result.StructuredContent!.Value;
        Assert.Equal(JsonValueKind.Null, content.GetProperty("result").ValueKind);
        Assert.Equal(error.Code, content.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(error.Recovery, content.GetProperty("error").GetProperty("recovery").GetString());
        Assert.DoesNotContain("planToken", content.GetRawText());
    }

    [Fact]
    public async Task BeltEndpointPreviewForwardsExactBindingsAndReturnsNoCommitAuthority()
    {
        var preview = EndpointPreviewRequest();
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            BeltEndpointPreviewPlan = EndpointPreviewPlan(preview),
        };

        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "owned-session", 104, 2001, "player",
            preferredPositionX: 1, preferredPositionY: 199.9925f, preferredPositionZ: 2,
            pathEndX: 11, pathEndY: 199.686f, pathEndZ: 2,
            beltPathMode: BeltPathModes.NativeGrid, beltEndpointPreview: preview);

        Assert.False(result.IsError);
        var request = Assert.IsType<PrepareBuildRequest>(bridge.LastBuildRequest);
        Assert.Equal("owned-session", bridge.LastSessionId);
        Assert.Same(preview, request.BeltEndpointPreview);
        Assert.Equal(2011, request.BeltEndpointPreview!.SorterItemId);
        Assert.Equal(1109, request.BeltEndpointPreview.FilterItemId);
        Assert.Equal(441, request.BeltEndpointPreview.Source!.ExistingObjectId);
        Assert.Equal("endpoint-441", request.BeltEndpointPreview.Source.ExpectedEndpointStateHash);
        Assert.Equal(3, request.BeltEndpointPreview.Source.ExistingSlot);
        Assert.Equal(2, request.BeltEndpointPreview.Source.PlannedBeltQuarterTurns);
        Assert.Equal(884, request.BeltEndpointPreview.Destination!.ExistingObjectId);
        Assert.Equal(-1, request.BeltEndpointPreview.Destination.ExistingSlot);

        var returned = result.StructuredContent!.Value.GetProperty("result");
        Assert.False(returned.GetProperty("prepared").GetBoolean());
        Assert.False(returned.GetProperty("commitAllowedNow").GetBoolean());
        Assert.Equal(string.Empty, returned.GetProperty("planToken").GetString());
        Assert.Equal("player", returned.GetProperty("expectedStateHash").GetString());
        var echo = returned.GetProperty("beltEndpointPreview");
        Assert.False(echo.GetProperty("executable").GetBoolean());
        Assert.Equal("owned-session", echo.GetProperty("sessionId").GetString());
        Assert.True(echo.GetProperty("nativeCheckPerformed").GetBoolean());
        Assert.True(echo.GetProperty("nativeCheckPassed").GetBoolean());
        Assert.Equal(2101, echo.GetProperty("attachments").EnumerateArray()
            .Single(row => row.GetProperty("role").GetString() == "source")
            .GetProperty("existingItemId").GetInt32());
        Assert.Equal(2001, echo.GetProperty("attachments").EnumerateArray()
            .Single(row => row.GetProperty("role").GetString() == "destination")
            .GetProperty("existingItemId").GetInt32());
        Assert.Equal(0, bridge.BuildCommitCalls);
    }

    [Fact]
    public async Task MixedLegacyPluginNormalTokenIsWithheldForEndpointPreview()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "owned-session", 104, 2001, "player",
            preferredPositionX: 1, preferredPositionY: 199.9925f, preferredPositionZ: 2,
            pathEndX: 11, pathEndY: 199.686f, pathEndZ: 2,
            beltPathMode: BeltPathModes.NativeGrid, beltEndpointPreview: EndpointPreviewRequest());

        Assert.True(result.IsError);
        var content = result.StructuredContent!.Value;
        Assert.False(content.GetProperty("success").GetBoolean());
        Assert.Equal(BridgeErrorCodes.BridgeNotReady, content.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, content.GetProperty("result").ValueKind);
        Assert.DoesNotContain("planToken", content.GetRawText());
        Assert.NotNull(bridge.LastBuildRequest);
        Assert.Equal(0, bridge.BuildCommitCalls);
    }

    [Fact]
    public async Task FailedNativeEndpointPreviewIsExplicitAndStillReadOnly()
    {
        var preview = EndpointPreviewRequest();
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            BeltEndpointPreviewPlan = EndpointPreviewPlan(preview, nativePassed: false),
        };

        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "owned-session", 104, 2001, "player",
            preferredPositionX: 1, preferredPositionY: 199.9925f, preferredPositionZ: 2,
            pathEndX: 11, pathEndY: 199.686f, pathEndZ: 2,
            beltPathMode: BeltPathModes.NativeGrid, beltEndpointPreview: preview);

        Assert.False(result.IsError);
        var returned = result.StructuredContent!.Value.GetProperty("result");
        Assert.False(returned.GetProperty("prepared").GetBoolean());
        Assert.False(returned.GetProperty("commitAllowedNow").GetBoolean());
        Assert.Equal(string.Empty, returned.GetProperty("planToken").GetString());
        var echo = returned.GetProperty("beltEndpointPreview");
        Assert.False(echo.GetProperty("executable").GetBoolean());
        Assert.False(echo.GetProperty("nativeCheckPassed").GetBoolean());
        Assert.NotEmpty(echo.GetProperty("blockers").EnumerateArray());
        Assert.Equal(0, bridge.BuildCommitCalls);
    }

    [Theory]
    [InlineData("missing_binding")]
    [InlineData("bad_hash")]
    [InlineData("unsupported_mode")]
    public async Task MalformedEndpointPreviewNeverReachesBridge(string fault)
    {
        var preview = EndpointPreviewRequest();
        var mode = BeltPathModes.NativeGrid;
        switch (fault)
        {
            case "missing_binding": preview.Source = null; preview.Destination = null; break;
            case "bad_hash": preview.Source!.ExpectedEndpointStateHash = " "; break;
            case "unsupported_mode": mode = BeltPathModes.NativeGeodesic; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "owned-session", 104, 2001, "player",
            preferredPositionX: 1, preferredPositionY: 199.9925f, preferredPositionZ: 2,
            pathEndX: 11, pathEndY: 199.686f, pathEndZ: 2,
            beltPathMode: mode, beltEndpointPreview: preview);

        Assert.True(result.IsError);
        Assert.Null(bridge.LastBuildRequest);
        Assert.Equal(0, bridge.BuildCommitCalls);
    }

    [Fact]
    public void EndpointPreviewIsAnOptionalFieldOnTheExistingPrepareToolOnly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer()
            .WithToolsFromAssembly(typeof(SpherewrightTools).Assembly)
            .WithResourcesFromAssembly(typeof(AgentPlaybookResources).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().ToArray();
        var tool = Assert.Single(tools, value => value.ProtocolTool.Name == "spherewright_prepare_build").ProtocolTool;

        Assert.Equal(64, tools.Length);
        Assert.Single(provider.GetServices<McpServerResource>());
        var properties = tool.InputSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("beltEndpointPreview", out var previewSchema));
        Assert.Contains("READ-ONLY qualification", previewSchema.GetProperty("description").GetString());
    }

    [Fact]
    public async Task MainMenuResumeIsPreparedBeforeAnyOwnedWorldIsLoaded()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            SessionSnapshot = new SessionState
            {
                BridgeConnected = true, GameLoaded = false, RestartResumeAvailable = true, RestartResumeToken = "ticket",
            },
        };
        var state = await SpherewrightTools.GetSessionStateAsync(bridge, CancellationToken.None);
        Assert.False(state.StructuredContent!.Value.GetProperty("result").GetProperty("gameLoaded").GetBoolean());
        Assert.True(state.StructuredContent!.Value.GetProperty("result").GetProperty("restartResumeAvailable").GetBoolean());
        var prepare = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "ticket", CancellationToken.None);
        Assert.False(prepare.IsError);
        Assert.Equal("ticket", bridge.LastResumePrepareRequest!.ResumeToken);
    }

    [Fact]
    public void MainMenuGuideSeparatesPreResumeReadinessFromPostAdoptionProof()
    {
        foreach (var name in new[] { nameof(SpherewrightTools.GetSessionStateAsync), nameof(SpherewrightTools.PrepareOwnedWorldResumeAsync) })
        {
            var method = typeof(SpherewrightTools).GetMethod(name)!;
            var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
                typeof(System.ComponentModel.DescriptionAttribute))!).Description;
            Assert.Contains("gameLoaded=false", description);
            Assert.Contains("restartResumeAvailable", description);
        }
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("do not wait for `gameLoaded=true` before prepare", guide);
        Assert.Contains("native preload/menu/no-loader", guide);
        var commit = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.CommitOwnedWorldResumeAsync))!;
        var commitDescription = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(commit,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        Assert.Contains("Default healthy planned restarts load only the exact ticket-bound primary", commitDescription);
        Assert.Contains("Real quarantine", commitDescription);
        Assert.Contains("Verified-newer LastExit", commitDescription);
        Assert.Contains("Fixed AutoSave0 uses evidence version3 with expired provenance or version4 with an active healthy credential", commitDescription);
        Assert.Contains("reauthorize_expired_autosave0", guide);
        Assert.Contains("recoveryEvidenceVersion=3", guide);
        Assert.Contains("reauthorize_fixed_autosave0", guide);
        Assert.Contains("Require evidence version **4**", guide);
        var prepare = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareOwnedWorldResumeAsync))!;
        var recoveryMode = prepare.GetParameters().Single(parameter => parameter.Name == "recoveryMode");
        var recoveryModeDescription = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(
            recoveryMode, typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        Assert.Contains("reauthorize_fixed_autosave0", recoveryModeDescription);
        Assert.Contains("evidence version4", recoveryModeDescription);
        var expectedTick = prepare.GetParameters().Single(parameter => parameter.Name == "expectedRecoveryGameTick");
        var expectedTickDescription = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(
            expectedTick, typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        Assert.Contains("fixed AutoSave0", expectedTickDescription);
        var confirmation = commit.GetParameters().Single(parameter => parameter.Name == "userConfirmedInConversation");
        var confirmationDescription = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(
            confirmation, typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        Assert.Contains("Active fixed AutoSave0 accepts an existing explicit conversation grant", confirmationDescription);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task VerifiedRecoveryRequiresAnExactPluginEchoBeforeExposingToken(bool echo, bool blocked)
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ResumePlan = echo ? new PreparedOwnedWorldResumePlan
            {
                Prepared = true, PlanToken = "recovery-plan", RecoveryMode = OwnedWorldResumeModes.VerifiedNewerLastExit,
                RecoveryEvidenceVersion = 1, CandidateGameTick = 62776058, MinimumGameTick = 62776058,
                ExactEmbeddedIdentityVerified = true,
            } : null,
        };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "ticket",
            recoveryMode: OwnedWorldResumeModes.VerifiedNewerLastExit, userConfirmedInConversation: true,
            minimumRecoveryGameTick: 62675846, expectedRecoveryGameTick: 62776058);
        Assert.Equal(blocked, result.IsError);
        Assert.Equal(62776058, bridge.LastResumePrepareRequest!.ExpectedRecoveryGameTick);
        Assert.Equal(62675846, bridge.LastResumePrepareRequest.MinimumRecoveryGameTick);
        Assert.True(bridge.LastResumePrepareRequest.UserConfirmedInConversation);
        if (blocked) Assert.DoesNotContain("planToken", result.StructuredContent!.Value.GetRawText());
    }

    [Theory]
    [InlineData(false, 62675846, 62776058)]
    [InlineData(true, -1, 62776058)]
    [InlineData(true, 62776059, 62776058)]
    public async Task InvalidRecoveryNeverReachesBridge(bool confirmed, long minimum, long expected)
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "ticket",
            recoveryMode: OwnedWorldResumeModes.VerifiedNewerLastExit, userConfirmedInConversation: confirmed,
            minimumRecoveryGameTick: minimum, expectedRecoveryGameTick: expected);
        Assert.True(result.IsError);
        Assert.Null(bridge.LastResumePrepareRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredPrimaryRequiresNewPluginDisclosureBeforeExposingPlan(bool validEcho)
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ResumePlan = validEcho ? new PreparedOwnedWorldResumePlan
            {
                Prepared = true, PlanToken = "fresh-plan", RecoveryMode = OwnedWorldResumeModes.ReauthorizeExpiredPrimary,
                RecoveryEvidenceVersion = 2, CandidateGameTick = 12345, MinimumGameTick = 12345,
                ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true, CommitAllowedNow = false,
                ConfirmationPrompt = Spherewright.Bridge.Core.Safety.OwnedWorldReauthorizationPolicy.ConfirmationPrompt,
                ConfirmationDigest = "synthetic-disclosure-digest",
                SourceGameVersion = "0.10.34.28529", TargetGameVersion = "0.10.35.29057",
            } : null,
        };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "expired-provenance",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeExpiredPrimary);
        Assert.Equal(!validEcho, result.IsError);
        Assert.False(bridge.LastResumePrepareRequest!.UserConfirmedInConversation);
        if (!validEcho) Assert.DoesNotContain("planToken", result.StructuredContent!.Value.GetRawText());
    }

    [Fact]
    public async Task VersionChangedPrimaryRejectsConfirmationBeforeAnyBridgeCall()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "active-ticket",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary, userConfirmedInConversation: true);
        Assert.True(result.IsError);
        Assert.Null(bridge.LastResumePrepareRequest);
    }

    [Theory]
    [InlineData("valid", false)]
    [InlineData("old-evidence", true)]
    [InlineData("default-mode", true)]
    [InlineData("wrong-target", true)]
    [InlineData("old-prompt", true)]
    [InlineData("missing-digest", true)]
    public async Task VersionChangedPrimaryOnlyExposesTheExactV5Disclosure(string fault, bool error)
    {
        var plan = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, PlanToken = "synthetic-active-migration-plan",
            RecoveryMode = OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary, RecoveryEvidenceVersion = 5,
            CandidateGameTick = 12345, MinimumGameTick = 12345, ExactEmbeddedIdentityVerified = true,
            UserConfirmationRequired = true, CommitAllowedNow = false,
            SourceGameVersion = "0.10.35.29104", TargetGameVersion = "0.10.35.29131",
            ConfirmationPrompt = Spherewright.Bridge.Core.Safety.OwnedWorldPrimaryMigrationPolicy.ConfirmationPrompt,
            ConfirmationDigest = "synthetic-disclosure-digest",
        };
        switch (fault)
        {
            case "old-evidence": plan.RecoveryEvidenceVersion = 2; break;
            case "default-mode": plan.RecoveryMode = OwnedWorldResumeModes.Default; break;
            case "wrong-target": plan.TargetGameVersion = "0.10.35.29132"; break;
            case "old-prompt": plan.ConfirmationPrompt = Spherewright.Bridge.Core.Safety.OwnedWorldReauthorizationPolicy.ConfirmationPrompt; break;
            case "missing-digest": plan.ConfirmationDigest = ""; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = plan };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "active-ticket",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary);
        Assert.Equal(error, result.IsError);
        Assert.False(bridge.LastResumePrepareRequest!.UserConfirmedInConversation);
        if (error) Assert.DoesNotContain("planToken", result.StructuredContent!.Value.GetRawText());
    }

    [Theory]
    [InlineData("valid", false)]
    [InlineData("legacy-evidence", true)]
    [InlineData("legacy-prompt", true)]
    [InlineData("skipped-source", true)]
    [InlineData("unknown-target", true)]
    [InlineData("default-mode", true)]
    [InlineData("missing-identity", true)]
    [InlineData("different-tick", true)]
    [InlineData("premature-commit", true)]
    [InlineData("missing-digest", true)]
    public async Task VersionChangedPrimaryOnlyExposesTheExact29145V6Disclosure(string fault, bool error)
    {
        var plan = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, PlanToken = "synthetic-29145-migration-plan",
            RecoveryMode = OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary, RecoveryEvidenceVersion = 6,
            CandidateGameTick = 12345, MinimumGameTick = 12345, ExactEmbeddedIdentityVerified = true,
            UserConfirmationRequired = true, CommitAllowedNow = false,
            SourceGameVersion = "0.10.35.29131", TargetGameVersion = "0.10.35.29145",
            ConfirmationPrompt = Spherewright.Bridge.Core.Safety.OwnedWorldPrimaryMigrationPolicy.NextPatchConfirmationPrompt,
            ConfirmationDigest = "synthetic-disclosure-digest",
        };
        switch (fault)
        {
            case "legacy-evidence": plan.RecoveryEvidenceVersion = 5; break;
            case "legacy-prompt": plan.ConfirmationPrompt = Spherewright.Bridge.Core.Safety.OwnedWorldPrimaryMigrationPolicy.ConfirmationPrompt; break;
            case "skipped-source": plan.SourceGameVersion = "0.10.35.29104"; break;
            case "unknown-target": plan.TargetGameVersion = "0.10.35.29146"; break;
            case "default-mode": plan.RecoveryMode = OwnedWorldResumeModes.Default; break;
            case "missing-identity": plan.ExactEmbeddedIdentityVerified = false; break;
            case "different-tick": plan.CandidateGameTick++; break;
            case "premature-commit": plan.CommitAllowedNow = true; break;
            case "missing-digest": plan.ConfirmationDigest = ""; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ResumePlan = plan };
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "active-ticket",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary);
        Assert.Equal(error, result.IsError);
        Assert.False(bridge.LastResumePrepareRequest!.UserConfirmedInConversation);
        if (error) Assert.DoesNotContain("planToken", result.StructuredContent!.Value.GetRawText());
    }

    [Fact]
    public async Task ExpiredPrimaryDoesNotAcceptAVerifiedLastExitEcho()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ResumePlan = new PreparedOwnedWorldResumePlan
            {
                Prepared = true, PlanToken = "last-exit-plan", RecoveryMode = OwnedWorldResumeModes.VerifiedNewerLastExit,
                RecoveryEvidenceVersion = 1, CandidateGameTick = 12345, MinimumGameTick = 12345,
                ExactEmbeddedIdentityVerified = true,
            },
        };

        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "expired-provenance",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeExpiredPrimary);

        Assert.True(result.IsError);
        Assert.DoesNotContain("planToken", result.StructuredContent!.Value.GetRawText());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredAutoSave0RequiresTheExactV3PluginDisclosureBeforeExposingPlan(bool validEcho)
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ResumePlan = validEcho ? new PreparedOwnedWorldResumePlan
            {
                Prepared = true, PlanToken = "autosave0-plan",
                RecoveryMode = OwnedWorldResumeModes.ReauthorizeExpiredAutosave0,
                RecoveryEvidenceVersion = 3, CandidateGameTick = 12345, MinimumGameTick = 12345,
                ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true, CommitAllowedNow = false,
                ConfirmationPrompt = Spherewright.Bridge.Core.Safety.OwnedWorldAutosaveRecoveryPolicy.ConfirmationPrompt,
                ConfirmationDigest = "synthetic-autosave0-disclosure-digest",
                SourceGameVersion = "0.10.35.29088", TargetGameVersion = "0.10.35.29088",
            } : null,
        };

        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "expired-provenance",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeExpiredAutosave0,
            minimumRecoveryGameTick: 12000, expectedRecoveryGameTick: 12345);

        Assert.Equal(!validEcho, result.IsError);
        Assert.False(bridge.LastResumePrepareRequest!.UserConfirmedInConversation);
        Assert.Equal(12000, bridge.LastResumePrepareRequest.MinimumRecoveryGameTick);
        Assert.Equal(12345, bridge.LastResumePrepareRequest.ExpectedRecoveryGameTick);
        if (!validEcho) Assert.DoesNotContain("planToken", result.StructuredContent!.Value.GetRawText());
    }

    [Theory]
    [InlineData(4, OwnedWorldResumeModes.ReauthorizeFixedAutosave0, false)]
    [InlineData(3, OwnedWorldResumeModes.ReauthorizeFixedAutosave0, true)]
    [InlineData(3, OwnedWorldResumeModes.ReauthorizeExpiredAutosave0, true)]
    public async Task ActiveFixedAutoSave0RequiresExactV4PluginEchoAndRejectsExpiredV3Disclosure(
        int evidenceVersion, string echoedMode, bool blocked)
    {
        var prompt = echoedMode == OwnedWorldResumeModes.ReauthorizeFixedAutosave0
            ? Spherewright.Bridge.Core.Safety.OwnedWorldAutosaveRecoveryPolicy.ActiveConfirmationPrompt
            : Spherewright.Bridge.Core.Safety.OwnedWorldAutosaveRecoveryPolicy.ConfirmationPrompt;
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ResumePlan = new PreparedOwnedWorldResumePlan
            {
                Prepared = true, PlanToken = "fixed-autosave0-plan",
                RecoveryMode = echoedMode, RecoveryEvidenceVersion = evidenceVersion,
                CandidateGameTick = 80731193, MinimumGameTick = 80731193,
                ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true, CommitAllowedNow = false,
                ConfirmationPrompt = prompt, ConfirmationDigest = "synthetic-fixed-autosave0-digest",
                SourceGameVersion = "0.10.35.29104", TargetGameVersion = "0.10.35.29104",
            },
        };

        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "active-provenance",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeFixedAutosave0,
            minimumRecoveryGameTick: 80627903, expectedRecoveryGameTick: 80731193);

        Assert.Equal(blocked, result.IsError);
        Assert.Equal(OwnedWorldResumeModes.ReauthorizeFixedAutosave0, bridge.LastResumePrepareRequest!.RecoveryMode);
        Assert.Equal(80627903, bridge.LastResumePrepareRequest.MinimumRecoveryGameTick);
        Assert.Equal(80731193, bridge.LastResumePrepareRequest.ExpectedRecoveryGameTick);
        Assert.False(bridge.LastResumePrepareRequest.UserConfirmedInConversation);
        if (blocked) Assert.DoesNotContain("planToken", result.StructuredContent!.Value.GetRawText());
    }

    [Fact]
    public async Task ExpiredAutoSave0DoesNotAcceptAnOldPluginOrAnotherRecoveryModeEcho()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ResumePlan = new PreparedOwnedWorldResumePlan
            {
                Prepared = true, PlanToken = "old-plan",
                RecoveryMode = OwnedWorldResumeModes.ReauthorizeExpiredPrimary,
                RecoveryEvidenceVersion = 2, CandidateGameTick = 12345, MinimumGameTick = 12345,
                ExactEmbeddedIdentityVerified = true, UserConfirmationRequired = true, CommitAllowedNow = false,
                ConfirmationPrompt = Spherewright.Bridge.Core.Safety.OwnedWorldReauthorizationPolicy.ConfirmationPrompt,
                ConfirmationDigest = "synthetic-disclosure-digest",
                SourceGameVersion = "0.10.35.29088", TargetGameVersion = "0.10.35.29088",
            },
        };

        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "expired-provenance",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeExpiredAutosave0,
            minimumRecoveryGameTick: 12000, expectedRecoveryGameTick: 12345);

        Assert.True(result.IsError);
        Assert.DoesNotContain("planToken", result.StructuredContent!.Value.GetRawText());
    }

    [Theory]
    [InlineData(OwnedWorldResumeModes.ReauthorizeExpiredAutosave0, true, 12000, 12345)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeExpiredAutosave0, false, -1, 12345)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeExpiredAutosave0, false, 12346, 12345)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeFixedAutosave0, true, 80627903, 80731193)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeFixedAutosave0, false, -1, 80731193)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeFixedAutosave0, false, 80731194, 80731193)]
    public async Task InvalidAutoSave0PrepareNeverReachesBridge(string mode, bool confirmed, long minimum, long expected)
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "expired-provenance",
            recoveryMode: mode,
            userConfirmedInConversation: confirmed, minimumRecoveryGameTick: minimum, expectedRecoveryGameTick: expected);

        Assert.True(result.IsError);
        Assert.Null(bridge.LastResumePrepareRequest);
    }

    [Fact]
    public async Task ExpiredPrimaryCannotClaimConfirmationDuringPrepare()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareOwnedWorldResumeAsync(bridge, "expired-provenance",
            recoveryMode: OwnedWorldResumeModes.ReauthorizeExpiredPrimary, userConfirmedInConversation: true);
        Assert.True(result.IsError);
        Assert.Null(bridge.LastResumePrepareRequest);
    }

    [Fact]
    public async Task CommitCarriesSubsequentConsentAndExactDisclosureDigestWithoutChangingDefault()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        await SpherewrightTools.CommitOwnedWorldResumeAsync(bridge, "fresh-plan", "idempotency",
            userConfirmedInConversation: true, confirmationDigest: "synthetic-disclosure-digest");
        Assert.True(bridge.LastResumeCommitRequest!.UserConfirmedInConversation);
        Assert.Equal("synthetic-disclosure-digest", bridge.LastResumeCommitRequest.ConfirmationDigest);
        await SpherewrightTools.CommitOwnedWorldResumeAsync(bridge, "default-plan", "default-idempotency");
        Assert.False(bridge.LastResumeCommitRequest!.UserConfirmedInConversation);
        Assert.Empty(bridge.LastResumeCommitRequest.ConfirmationDigest);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(BeltPathModes.NativeGrid, true)]
    [InlineData(BeltPathModes.NativeGeodesic, false)]
    public async Task GeodesicRequestRequiresAnExactPluginEchoBeforeExposingTheToken(string? echo, bool blocked)
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { BuildBeltRoutingMode = echo };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            preferredPositionX: 0, preferredPositionY: 200.2f, preferredPositionZ: 0,
            pathEndX: 10, pathEndY: 199.95f, pathEndZ: 0, beltPathMode: BeltPathModes.NativeGeodesic);
        Assert.Equal(BeltPathModes.NativeGeodesic, bridge.LastBuildRequest!.BeltPathMode);
        Assert.Equal(blocked, result.IsError);
        var content = result.StructuredContent!.Value;
        if (blocked)
        {
            Assert.Equal(BridgeErrorCodes.BridgeNotReady, content.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal(JsonValueKind.Null, content.GetProperty("result").ValueKind);
        }
        else Assert.Equal(echo, content.GetProperty("result").GetProperty("plannedBeltPath").GetProperty("routingMode").GetString());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(754)]
    public async Task GeodesicDoesNotNormalizeAnExistingEndpointIntoFreeSpace(int source)
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            preferredPositionX: 0, preferredPositionY: 200.2f, preferredPositionZ: 0,
            pathEndX: 10, pathEndY: 199.95f, pathEndZ: 0, sourceObjectId: source, beltPathMode: BeltPathModes.NativeGeodesic);
        Assert.True(result.IsError);
        Assert.Null(bridge.LastBuildRequest);
        Assert.Equal(BridgeErrorCodes.InvalidRequest, result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ElevatedGridMapsFreeEndpointsAndExactLevelsBeforeExposingToken()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            BuildBeltRoutingMode = BeltPathModes.NativeElevatedGrid,
            BuildBeltNewObjectCount = 4,
            BuildBeltStartAltitudeLevel = 0,
            BuildBeltEndAltitudeLevel = 1,
        };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            preferredPositionX: 0, preferredPositionY: 200.2f, preferredPositionZ: 0,
            pathEndX: 10, pathEndY: 199.95f, pathEndZ: 0,
            beltPathMode: BeltPathModes.NativeElevatedGrid,
            beltStartAltitudeLevel: 0, beltEndAltitudeLevel: 1);

        Assert.False(result.IsError);
        var request = Assert.IsType<PrepareBuildRequest>(bridge.LastBuildRequest);
        Assert.Equal(BeltPathModes.NativeElevatedGrid, request.BeltPathMode);
        Assert.Equal(0, request.BeltStartAltitudeLevel);
        Assert.Equal(1, request.BeltEndAltitudeLevel);
        Assert.Null(request.SourceObjectId);
        Assert.Null(request.DestinationObjectId);
        Assert.Null(request.ResourceNodeId);

        var echo = result.StructuredContent!.Value.GetProperty("result").GetProperty("plannedBeltPath");
        Assert.Equal("full_path_stage1", echo.GetProperty("nativeValidationMode").GetString());
        Assert.Equal(BeltPathModes.NativeElevatedGrid, echo.GetProperty("routingMode").GetString());
        Assert.Equal(4, echo.GetProperty("newObjectCount").GetInt32());
        Assert.Equal("none", echo.GetProperty("sourceBindingMode").GetString());
        Assert.Equal("none", echo.GetProperty("destinationBindingMode").GetString());
        Assert.Equal(0, echo.GetProperty("startAltitudeLevel").GetInt32());
        Assert.Equal(1, echo.GetProperty("endAltitudeLevel").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RaisedTailCoverRequiresTheInstalledSourcePreservationEcho(bool confirmedCover)
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            BuildBeltRoutingMode = BeltPathModes.NativeElevatedGrid,
            BuildBeltNewObjectCount = 4,
            BuildBeltStartAltitudeLevel = 2,
            BuildBeltEndAltitudeLevel = 1,
            BuildBeltSourceCover = confirmedCover,
            BuildBeltPreservationMode = confirmedCover ? "whole_path_native_rotation_v1" : null,
        };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            preferredPositionX: 0, preferredPositionY: 202.86667f, preferredPositionZ: 0,
            pathEndX: 10, pathEndY: 201.53333f, pathEndZ: 0,
            sourceObjectId: 754, expectedSourceStateHash: "source",
            beltPathMode: BeltPathModes.NativeElevatedGrid,
            beltStartAltitudeLevel: 2, beltEndAltitudeLevel: 1);
        Assert.NotNull(bridge.LastBuildRequest);
        Assert.Equal(!confirmedCover, result.IsError);
        if (confirmedCover)
        {
            var request = Assert.IsType<PrepareBuildRequest>(bridge.LastBuildRequest);
            Assert.Equal(754, request.SourceObjectId);
            Assert.Equal("source", request.ExpectedSourceStateHash);
            Assert.Null(request.DestinationObjectId);
            var echo = result.StructuredContent!.Value.GetProperty("result").GetProperty("plannedBeltPath");
            Assert.Equal("non_removing_belt_cover", echo.GetProperty("sourceBindingMode").GetString());
            Assert.Equal(754, echo.GetProperty("reusedSourceObjectId").GetInt32());
            Assert.Equal(4, echo.GetProperty("newObjectCount").GetInt32());
        }
        else
        {
            Assert.Equal(BridgeErrorCodes.BridgeNotReady,
                result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("item", 2002)]
    [InlineData("source", 754)]
    [InlineData("source", -1)]
    [InlineData("destination", 755)]
    [InlineData("destination", -1)]
    [InlineData("resource", 23)]
    [InlineData("resource", -1)]
    public async Task ElevatedGridRejectsAnyBoundOrNon2001EndpointBeforeBridge(string binding, int value)
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104,
            binding == "item" ? value : 2001, "player",
            preferredPositionX: 0, preferredPositionY: 200.2f, preferredPositionZ: 0,
            pathEndX: 10, pathEndY: 199.95f, pathEndZ: 0,
            resourceNodeId: binding == "resource" ? value : 0,
            sourceObjectId: binding == "source" ? value : 0,
            destinationObjectId: binding == "destination" ? value : 0,
            beltPathMode: BeltPathModes.NativeElevatedGrid,
            beltStartAltitudeLevel: 0, beltEndAltitudeLevel: 1);

        Assert.True(result.IsError);
        Assert.Null(bridge.LastBuildRequest);
        Assert.Equal(BridgeErrorCodes.InvalidRequest,
            result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(-99, 1)]
    [InlineData(0, -99)]
    [InlineData(0, 0)]
    [InlineData(-1, 1)]
    [InlineData(0, 4)]
    public async Task ElevatedGridRejectsMissingOrUnsupportedLevelsBeforeBridge(int rawStart, int rawEnd)
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            preferredPositionX: 0, preferredPositionY: 200.2f, preferredPositionZ: 0,
            pathEndX: 10, pathEndY: 199.95f, pathEndZ: 0,
            beltPathMode: BeltPathModes.NativeElevatedGrid,
            beltStartAltitudeLevel: rawStart == -99 ? null : rawStart,
            beltEndAltitudeLevel: rawEnd == -99 ? null : rawEnd);

        Assert.True(result.IsError);
        Assert.Null(bridge.LastBuildRequest);
        Assert.Equal(BridgeErrorCodes.InvalidRequest,
            result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DefaultBeltModeRejectsAltitudeFieldsBeforeBridge()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            beltStartAltitudeLevel: 0, beltEndAltitudeLevel: 1);

        Assert.True(result.IsError);
        Assert.Null(bridge.LastBuildRequest);
        Assert.Equal(BridgeErrorCodes.InvalidRequest,
            result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("wrong-routing")]
    [InlineData("wrong-stage")]
    [InlineData("missing-start")]
    [InlineData("missing-end")]
    [InlineData("wrong-start")]
    [InlineData("wrong-end")]
    [InlineData("too-few-new")]
    [InlineData("too-many-new")]
    [InlineData("source-cover")]
    [InlineData("destination-cover")]
    public async Task ElevatedGridRejectsLegacyOrMismatchedEchoWithoutExposingToken(string echoCase)
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            BuildBeltRoutingMode = BeltPathModes.NativeElevatedGrid,
            BuildBeltNewObjectCount = 4,
            BuildBeltStartAltitudeLevel = 0,
            BuildBeltEndAltitudeLevel = 1,
        };
        switch (echoCase)
        {
            case "legacy":
                bridge.BuildBeltRoutingMode = null;
                bridge.BuildBeltStartAltitudeLevel = null;
                bridge.BuildBeltEndAltitudeLevel = null;
                break;
            case "wrong-routing": bridge.BuildBeltRoutingMode = BeltPathModes.NativeGrid; break;
            case "wrong-stage": bridge.BuildBeltMode = "endpoint_only"; break;
            case "missing-start": bridge.BuildBeltStartAltitudeLevel = null; break;
            case "missing-end": bridge.BuildBeltEndAltitudeLevel = null; break;
            case "wrong-start": bridge.BuildBeltStartAltitudeLevel = 1; break;
            case "wrong-end": bridge.BuildBeltEndAltitudeLevel = 0; break;
            case "too-few-new": bridge.BuildBeltNewObjectCount = 3; break;
            case "too-many-new": bridge.BuildBeltNewObjectCount = 65; break;
            case "source-cover": bridge.BuildBeltSourceCover = true; break;
            case "destination-cover": bridge.BuildBeltDestinationCover = true; break;
        }

        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            preferredPositionX: 0, preferredPositionY: 200.2f, preferredPositionZ: 0,
            pathEndX: 10, pathEndY: 199.95f, pathEndZ: 0,
            beltPathMode: BeltPathModes.NativeElevatedGrid,
            beltStartAltitudeLevel: 0, beltEndAltitudeLevel: 1);

        Assert.True(result.IsError);
        Assert.NotNull(bridge.LastBuildRequest);
        var content = result.StructuredContent!.Value;
        Assert.Equal(BridgeErrorCodes.BridgeNotReady, content.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, content.GetProperty("result").ValueKind);
        Assert.DoesNotContain("\"planToken\"", content.GetRawText());
    }

    [Fact]
    public void NativeGeodesicSubsetIsDiscoverableInSchemaAndEmbeddedGuide()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(),
            value => value.ProtocolTool.Name == "spherewright_prepare_build").ProtocolTool;
        Assert.Contains("native_geodesic", tool.InputSchema.GetProperty("properties").GetProperty("beltPathMode").GetProperty("description").GetString());
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("beltPathMode=native_geodesic", guide);
        Assert.Contains("plannedBeltPath.routingMode", guide);
        Assert.Contains("both sorter attachments", guide);
    }

    [Fact]
    public void NativeElevatedGridSubsetIsDiscoverableWithoutClaimingLivePlacement()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(),
            value => value.ProtocolTool.Name == "spherewright_prepare_build").ProtocolTool;
        var properties = tool.InputSchema.GetProperty("properties");
        Assert.Contains(BeltPathModes.NativeElevatedGrid,
            properties.GetProperty("beltPathMode").GetProperty("description").GetString());
        Assert.Contains(BeltPathModes.NativeElevatedGrid,
            properties.GetProperty("beltStartAltitudeLevel").GetProperty("description").GetString());
        Assert.Contains(BeltPathModes.NativeElevatedGrid,
            properties.GetProperty("beltEndAltitudeLevel").GetProperty("description").GetString());

        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("beltPathMode=native_elevated_grid", guide);
        Assert.Contains("beltStartAltitudeLevel", guide);
        Assert.Contains("beltEndAltitudeLevel", guide);
    }

    [Fact]
    public void BeltRoutingGuidanceRequiresWholeConnectionGeometryBeforeRemainingConstruction()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(),
            value => value.ProtocolTool.Name == "spherewright_prepare_build").ProtocolTool;
        var description = tool.InputSchema.GetProperty("properties").GetProperty("beltPathMode")
            .GetProperty("description").GetString()!;
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var text in new[] { description, guide })
        {
            Assert.Contains("source, bridge and consumer", text);
            Assert.Contains("straight end segments", text);
            Assert.Contains("prepare the critical sorter before constructing the remaining route", text);
        }
        Assert.Contains("5m maximum straight distance", guide);
        Assert.Contains("3.2 maximum local grid segments", guide);
        Assert.Contains("Predicted cardinal directions are not native approval", guide);
        Assert.Contains("fallback excludes thermal generators", guide);
        Assert.Contains("older versions also reject two-belt interpolation", guide);
        Assert.Contains("native_two_belt_segments", guide);
        Assert.Contains("do not replay the unchanged pair", guide);
    }

    [Fact]
    public void FullNativePathStageDoesNotDependOnThePlayersUiCommand()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareBuildAsync))!;
        var description = (System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!;
        Assert.Contains("tool-owned stage1 command container", description.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("full native path checks", guide);
        Assert.Contains("never edits the player's command", guide);
        Assert.Contains("does not enable existing-belt cover reuse", guide);
    }

    [Fact]
    public void NewBeltOccupancyBoundaryIsDiscoverableWithoutPromisingUnboundedReuse()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(),
            value => value.ProtocolTool.Name == "spherewright_prepare_build").ProtocolTool;
        Assert.Contains("including both ends", tool.Description);
        Assert.Contains("0.25 m", tool.Description);
        Assert.Contains("source-only non-removing cover reuse", tool.Description);
        Assert.Contains("plannedBeltPath", tool.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("belt_path_existing_overlap", guide);
        Assert.Contains("Do not omit endpoint IDs", guide);
        Assert.Contains("native cover reuse", guide);
        Assert.Contains("does not automatically repair", guide);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("anchor_only_stage0")]
    public async Task LegacyBeltValidationCannotExposeAConstructionCapability(string? mode)
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { OmitBuildBeltEcho = mode is null, BuildBeltMode = mode ?? string.Empty };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player");
        Assert.True(result.IsError);
        var content = result.StructuredContent!.Value;
        Assert.Equal(BridgeErrorCodes.BridgeNotReady, content.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, content.GetProperty("result").ValueKind);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("whole_path_native_rotation_v1", false)]
    public async Task SourceCoverRequiresTheNativeCompletionRotationProof(string? mode, bool blocked)
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { BuildBeltSourceCover = true, BuildBeltPreservationMode = mode };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player", sourceObjectId: 754, expectedSourceStateHash: "source");
        Assert.Equal(blocked, result.IsError);
        if (blocked) Assert.Equal(JsonValueKind.Null, result.StructuredContent!.Value.GetProperty("result").ValueKind);
        else Assert.Equal(mode, result.StructuredContent!.Value.GetProperty("result").GetProperty("plannedBeltPath").GetProperty("sourcePreservationMode").GetString());
    }

    [Fact]
    public async Task CurrentBeltPlanEchoIsReturnedWithOnlyTheNewObjectBudget()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player");
        Assert.False(result.IsError);
        var echo = result.StructuredContent!.Value.GetProperty("result").GetProperty("plannedBeltPath");
        Assert.Equal("full_path_stage1", echo.GetProperty("nativeValidationMode").GetString());
        Assert.Equal(2, echo.GetProperty("newObjectCount").GetInt32());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("endpoint_only", true)]
    [InlineData("empty_open_path_native_geometry_v1", false)]
    public async Task DestinationCoverRequiresWholeEmptyPathPreservationEcho(string? mode, bool blocked)
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { BuildBeltDestinationCover = true,
            BuildBeltDestinationPreservationMode = mode, BuildBeltRoutingMode = BeltPathModes.NativeGrid };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            preferredPositionX: 0, preferredPositionY: 200, preferredPositionZ: 0,
            destinationObjectId: 755, expectedDestinationStateHash: "destination");
        Assert.Equal(blocked, result.IsError);
        if (blocked) Assert.Equal(JsonValueKind.Null, result.StructuredContent!.Value.GetProperty("result").ValueKind);
        else
        {
            var plan = result.StructuredContent!.Value.GetProperty("result");
            Assert.Equal(755, plan.GetProperty("plannedBeltPath").GetProperty("reusedDestinationObjectId").GetInt32());
            Assert.Equal(2, plan.GetProperty("itemBudget")[0].GetProperty("count").GetInt32());
            Assert.Equal(2, plan.GetProperty("plannedPath").GetArrayLength());
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task BoundGeodesicCannotExposeLegacyOrHalfCoverCapability(bool currentEcho, bool blocked)
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { BuildBeltSourceCover = true,
            BuildBeltPreservationMode = "whole_path_native_rotation_v1", BuildBeltRoutingMode = BeltPathModes.NativeGeodesic,
            BuildBeltDestinationCover = currentEcho, BuildBeltDestinationPreservationMode = currentEcho ? "empty_open_path_native_geometry_v1" : null };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2001, "player",
            sourceObjectId: 754, expectedSourceStateHash: "source", destinationObjectId: 755,
            expectedDestinationStateHash: "destination", beltPathMode: BeltPathModes.NativeGeodesic);
        Assert.Equal(blocked, result.IsError);
        if (blocked) Assert.Equal(JsonValueKind.Null, result.StructuredContent!.Value.GetProperty("result").ValueKind);
    }

    [Fact]
    public void EmptyOpenHeadReuseIsDiscoverableWithoutPromisingMergesOrFreeMaterials()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareBuildAsync))!;
        var description = (System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!;
        foreach (var text in new[] { description.Description, AgentPlaybookResources.GetOpeningMovementPlaybook().Text })
        {
            Assert.Contains("empty_open_path_native_geometry_v1", text);
            Assert.Contains("destinationBindingMode=non_removing_belt_cover", text);
            Assert.Contains("both complete old paths", text);
            Assert.Contains("at least two NEW", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AttachmentFailureStagesAreDiscoverableAndDoNotEncourageBlindPairRetries()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareBuildAsync))!;
        var description = (System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!;
        Assert.Contains("geometry_unavailable is not an angle verdict", description.Description);
        Assert.Contains("bestFacingDegrees", description.Description);
        Assert.Contains("Do not repeat an unchanged pair", description.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("candidate_validation_rejected", guide);
        Assert.Contains("not a native placement-check count", guide);
        Assert.Contains("bestFacingDegrees=unknown", guide);
    }

    [Fact]
    public void ExactSlotFailuresExplainActualNativeChecksWithoutTreatingLastAngleAsOverallCause()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareBuildAsync))!;
        var description = (System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!;
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var text in new[] { description.Description, guide })
        {
            Assert.Contains("nativeChecks", text);
            Assert.Contains("lastNativeRejection", text);
            Assert.Contains("lastPreNativeRejection", text);
        }
        Assert.Contains("actual calls to DSP's placement validator", guide);
        Assert.Contains("Zero checks means the native placement result is unknown", guide);
        Assert.Contains("Do not replay the unchanged pair", guide);
        Assert.Contains("Older Plugins may report only the last exact-slot error", guide);
    }

    [Fact]
    public void EmptyStorageRecoveryIsDiscoverableWithoutAdvertisingGeneralDeletionOrAutomaticRebuild()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var name in new[] { nameof(SpherewrightTools.PrepareDismantleAsync), nameof(SpherewrightTools.CommitDismantleAsync) })
        {
            var method = typeof(SpherewrightTools).GetMethod(name)!;
            var description = (System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
                typeof(System.ComponentModel.DescriptionAttribute))!;
            foreach (var text in new[] { description.Description, guide })
            {
                Assert.Contains("empty default2101 storage", text);
                Assert.Contains("prebuild", text);
                Assert.Contains("cached", text);
                Assert.Contains("no automatic rebuild", text);
            }
        }
        Assert.Contains("all30 native grids", guide);
        Assert.Contains("TooClose", guide);
        Assert.Contains("minimum grid span", guide);
    }

    [Fact]
    public void BoundedBeltAttachmentsAreDiscoverableWithoutWeakeningNativePlacementRules()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(),
            value => value.ProtocolTool.Name == "spherewright_prepare_build").ProtocolTool;
        Assert.Contains("native_single_belt_segment", tool.Description);
        Assert.Contains("native_two_belt_segments", tool.Description);
        Assert.Contains("Two explicitly selected belt segments", tool.Description);
        Assert.Contains("plannedInserterAttachment", tool.Description);
        Assert.Contains("without retargeting", tool.Description);
        Assert.Contains("collision and materials remain mandatory", tool.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("at most64 native path intervals", guide);
        Assert.Contains("native_two_belt_segments", guide);
        Assert.Contains("both geometry bindings", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a promise that a given pair is connectable", guide);
        Assert.Contains("geometry requires fresh prepare", guide);
        Assert.Contains("does not prove its two sorter attachments", guide);
        Assert.Contains("folded <40-degree seed screen before either curve projection", guide);
        Assert.Contains("cannot rescue a seed that never enters that branch", guide);
        Assert.Contains("smallest approved interface-checking piece", guide);
        Assert.Contains("Closed paths", guide);
    }

    [Fact]
    public void ExactObjectReadParameterAndPlaybookDiscloseDirectBridgeSchema()
    {
        var parameter = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.InspectFactoryEntityAsync))!
            .GetParameters().Single(value => value.Name == "objectId");
        var description = (System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(parameter,
            typeof(System.ComponentModel.DescriptionAttribute))!;
        Assert.Contains("objectId, not entityId", description.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("objectId, not entityId", guide);
        Assert.Contains("first100", guide);
    }

    [Fact]
    public void SorterEndpointGeometryIsDiscoverableAsReadOnlyNotPlacementAuthority()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = provider.GetServices<McpServerTool>().Single(value => value.ProtocolTool.Name == "spherewright_inspect_factory_entity").ProtocolTool;
        Assert.True(tool.Annotations!.ReadOnlyHint);
        Assert.Contains("sorterEndpoints", tool.Description);
        Assert.Contains("not a placement approval", tool.Description);
        Assert.Contains("unknown physical occupancy", tool.Description);
        Assert.Contains("not building centers", tool.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("sorterEndpoints", guide);
        Assert.Contains("one water source does not make a shared belt pure", guide);
    }

    [Fact]
    public void ObservationToolsAndPlaybookDiscloseComponentAndCargoCoverage()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().Select(t => t.ProtocolTool).ToArray();
        var assemblers = tools.Single(t => t.Name == "spherewright_list_assemblers");
        var entities = tools.Single(t => t.Name == "spherewright_list_factory_entities");
        var inspect = tools.Single(t => t.Name == "spherewright_inspect_factory_entity");
        Assert.Contains("labs, not assemblers", assemblers.Description);
        Assert.Contains("componentKind=lab", entities.Description);
        Assert.Contains("same filtered snapshot", entities.Description);
        foreach (var tool in new[] { entities, inspect })
        {
            Assert.True(tool.Annotations!.ReadOnlyHint);
            Assert.Contains("Belt cargo is not observed", tool.Description);
        }
        Assert.Contains("unfinished trace", inspect.Description);
        Assert.Contains("separate detail-only beltCargo", inspect.Description);
        Assert.Contains("unavailable/null is unknown", inspect.Description);
        Assert.Contains("do not sum", inspect.Description);
        Assert.Contains("upgrade-preservation proof", inspect.Description);
        Assert.Contains("List snapshots omit beltCargo", entities.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("labs, not assemblers", guide);
        Assert.Contains("Belt cargo is not observed", guide);
        Assert.Contains("cargo unknown, not an empty belt", guide);
        Assert.Contains("never manufacture demand by clearing storage", guide);
        Assert.Contains("state=observed", guide);
        Assert.Contains("do not sum", guide);
        Assert.Contains("whole-path upgrade cargo-preservation", guide);
    }

    [Fact]
    public void RearPickupEvidenceIsDiscoverableButNotQueueOrRepairAuthority()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var inspect = provider.GetServices<McpServerTool>().Single(t => t.ProtocolTool.Name == "spherewright_inspect_factory_entity").ProtocolTool;
        Assert.True(inspect.Annotations!.ReadOnlyHint);
        Assert.Contains("beltCargo.rearPickup", inspect.Description);
        Assert.Contains("no_aligned_packet does not mean an empty belt", inspect.Description);
        Assert.Contains("not queue order", inspect.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("beltCargo.rearPickup", guide);
        Assert.Contains("aligned native pickup packet", guide);
        Assert.Contains("cannot skip", guide);
        Assert.Contains("last received slot, not an input filter", guide);
        Assert.Contains("never clear stock to manufacture throughput", guide);
    }

    [Fact]
    public void SettlingGuidanceDoesNotReplayMovesOrCountRevisionsAsActions()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("bounded read-only settling check", guide);
        Assert.Contains("not an extra or repeated Move", guide);
        Assert.Contains("revision is not an accepted-action counter", guide);
    }

    [Fact]
    public void PlayerReadDisclosesDroneReadinessIsNotBuildCompletion()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var read = provider.GetServices<McpServerTool>()
            .Single(t => t.ProtocolTool.Name == "spherewright_get_player_state").ProtocolTool;
        Assert.True(read.Annotations!.ReadOnlyHint);
        Assert.Contains("all alive non-idle drones, not unfinished buildings", read.Description);
        Assert.Contains("must not be replayed", read.Description);
        Assert.Contains("bounded read-only readiness checks", read.Description);
    }

    [Fact]
    public void PackagedBuildReadinessGuidancePreservesCompletedPartsAndFreshIdentity()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("successful build terminal and idle construction drones are different facts", guide);
        Assert.Contains("at most20 reads separated by500ms", guide);
        Assert.Contains("Continue only the unsubmitted part with fresh hashes", guide);
        Assert.Contains("never replay the completed route or whole module", guide);
        Assert.Contains("Native entity IDs can be reused after removal", guide);
        Assert.Contains("revalidate item, pose and endpoints", guide);
        Assert.Contains("Do not impose an all-drones-idle gate on every ordinary build or save", guide);
        Assert.Contains("zero pending build/repair targets", guide);
        Assert.Contains("native validation still decides whether it can commit", guide);
        Assert.Contains("initial idle boundary and independent stage-readiness audit", guide);
        Assert.Contains("does not authorize overlapping actions, unknown outcomes or skipping state hashes", guide);
    }

    [Fact]
    public void PackagedPowerNodeGuidanceDistinguishesRuntimeBuffersFromStaticEvidence()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("not a universal static-topology fingerprint", guide);
        Assert.Contains("power-generation-current-tick", guide);
        Assert.Contains("complete before/probe/after snapshots", guide);
        Assert.Contains("network, connections and all other static fields", guide);
        Assert.Contains("nonnegative integer count may vary", guide);
        Assert.Contains("preserve its role, fuel item/name, units and inc", guide);
        Assert.Contains("unexplained hashes still stop the plan", guide);
        Assert.Contains("Do not rewrite returned hashes", guide);
        Assert.Contains("each real action still requires fresh native prepare/commit", guide);
    }

    [Fact]
    public void FlightGuidanceRequiresNativeShoreCompletionAndBoundedStableArrival()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var flight = provider.GetServices<McpServerTool>()
            .Single(t => t.ProtocolTool.Name == "spherewright_commit_interplanetary_flight").ProtocolTool;
        Assert.Contains("transient Walk tick does not complete landing", flight.Description);
        Assert.Contains("unfinished exact shore order", flight.Description);
        Assert.Contains("600 consecutive grounded low-speed ticks", flight.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("must keep its progress/energy checks until it reaches its target", guide);
        Assert.Contains("Do not issue a competing Move", guide);
        Assert.Contains("cannot be relabeled successful", guide);
        Assert.Contains("Cold startup may retire even a failed-flight checkpoint", guide);
        Assert.Contains("Never edit a ticket or revive a retired token", guide);
        Assert.Contains("a new flight with a new checkpoint", guide);
        Assert.Contains("NO_LOCAL_PLANET", guide);
        Assert.Contains("keep polling the same actionId", guide);
    }

    [Fact]
    public void OpeningPlaybookSeparatesNativeGridSpanFromMetreDistance()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("metre distance and native grid span are independent limits", guide);
        Assert.Contains("5.5m and3.499 grid units", guide);
        Assert.Contains("`TooFar` is not `OutOfReach`", guide);
        Assert.Contains("existing interior belt segments", guide);
        Assert.Contains("free physical slots and every old reciprocal edge", guide);
        Assert.Contains("never native placement approval", guide);
        Assert.Contains("preserve the whole successful route", guide);
        Assert.Contains("prepare each new pair once, and stop on rejection", guide);
        Assert.Contains("No arbitrary neighbor search, automatic rerouting or replay", guide);
    }

    [Fact]
    public void OpeningPlaybookReconcilesNewSorterEdgesBeforeCompletingStep()
    {
        var resource = AgentPlaybookResources.GetOpeningMovementPlaybook();
        Assert.Equal(AgentPlaybookResources.OpeningMovementUri, resource.Uri);
        var guide = resource.Text;
        Assert.Contains("pending-readback record", guide);
        Assert.Contains("matching fresh successful terminal", guide);
        Assert.Contains("uniquely returned new object ID and prepared source/destination", guide);
        Assert.Contains("only that proven object's expected reciprocal edges on those two endpoints", guide);
        Assert.Contains("preserve every old edge and other configuration field", guide);
        Assert.Contains("both physical endpoint slots before marking the local step complete", guide);
        Assert.Contains("Do not broadly skip connections, admit an unproved ID", guide);
        Assert.Contains("A post-check exception never authorizes replay", guide);
        Assert.Contains("continue only the unsubmitted suffix after fresh reconciliation", guide);
    }

    [Fact]
    public void FactoryReadGuidanceDoesNotBudgetGenerationAsFuelStock()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var read = provider.GetServices<McpServerTool>()
            .Single(t => t.ProtocolTool.Name == "spherewright_list_factory_entities").ProtocolTool;
        Assert.Contains("joules_per_tick, never fuel inventory", read.Description);
        Assert.Contains("unitsPerItem=0 means no item conversion", read.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("Exclude this role even when an older Plugin incorrectly reports", guide);
        Assert.Contains("never divide by zero", guide);
        Assert.Contains("full-width power summary", guide);
    }

    [Fact]
    public void PowerGuidanceRequiresNativeMembershipReconciliationAndMeasuredFuelDemand()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var read = provider.GetServices<McpServerTool>()
            .Single(t => t.ProtocolTool.Name == "spherewright_get_power_summary").ProtocolTool;
        Assert.True(read.Annotations!.ReadOnlyHint);
        Assert.Contains("network IDs can be null", read.Description);
        Assert.Contains("construction can merge networks", read.Description);
        Assert.Contains("never replay a successful build", read.Description);
        Assert.Contains("Positive generation is not sustained fuel consumption", read.Description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("Null is not proof of disconnection", guide);
        Assert.Contains("actual affected consumer/generator memberships", guide);
        Assert.Contains("an unexplained change still requires stopping", guide);
        Assert.Contains("Never replay a successful build", guide);
        Assert.Contains("adding generators does not multiply fuel consumption", guide);
        Assert.Contains("Declare useful demand and independent measurement windows first", guide);
        Assert.Contains("extra finite storage does not prove sustained supply", guide);
        Assert.Contains("throttling below the declared demand", guide);
        Assert.Contains("Keep failed windows and sampling gaps", guide);
    }

    [Fact]
    public async Task GovernorExposesReadOnlyDeclaredTargetAndExplicitSource()
    {
        var client = new FakeBridgeClient(SuccessResult());
        var source = new BlueprintSelectedEntity { ObjectId = 715, ExpectedRecipeId = 60, ExpectedEndpointStateHash = "fresh" };
        var layout = new FoundryBlueprintRequest { BlueprintCode = "explicit-layout" };
        var result = await SpherewrightTools.GetGovernorPlanAsync(client, "session", 104, 1112, 60, new[] { source }, .1m, 36000,
            validationBaselineProposalHash: "retained-pre-execution-proposal", parallelExpansionBlueprint: layout);
        Assert.False(result.IsError); Assert.Equal("session", client.LastSessionId);
        Assert.Same(source, Assert.Single(client.LastGovernorRequest!.SourceEntities));
        Assert.Equal(36000, client.LastGovernorRequest.ValidationGameTicks);
        Assert.Equal(600, client.LastGovernorRequest.MeasurementGameTicks);
        Assert.Equal(.1m, client.LastGovernorRequest.ToleranceFraction);
        Assert.Equal(60, client.LastGovernorRequest.TargetRatePerMinute);
        Assert.Equal("retained-pre-execution-proposal", client.LastGovernorRequest.ValidationBaselineProposalHash);
        Assert.Same(layout, client.LastGovernorRequest.ParallelExpansionBlueprint);
        var services = new ServiceCollection(); services.AddSingleton<IBridgeClient>(client);
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = provider.GetServices<McpServerTool>().Single(t => t.ProtocolTool.Name == "spherewright_get_governor_plan").ProtocolTool;
        Assert.True(tool.Annotations!.ReadOnlyHint);
        Assert.Contains("baseline", tool.Description);
        Assert.Contains("consumption", tool.Description);
        Assert.Contains("sourceEntities", tool.InputSchema.ToString());
        Assert.Contains("validationBaselineProposalHash", tool.InputSchema.ToString());
        Assert.Contains("parallelExpansionBlueprint", tool.InputSchema.ToString());
        Assert.Contains("measurementGameTicks", tool.InputSchema.ToString());
        Assert.Contains("parallelExpansionBlueprint", AgentPlaybookResources.GetOpeningMovementPlaybook().Text);
        Assert.Contains("target minus measured baseline", AgentPlaybookResources.GetOpeningMovementPlaybook().Text);
        Assert.Contains("costScope", AgentPlaybookResources.GetOpeningMovementPlaybook().Text);
        Assert.Contains("validationBaselineProposalHash", AgentPlaybookResources.GetOpeningMovementPlaybook().Text);
        Assert.Contains("declarationDurable", tool.Description);
        Assert.Contains("covering protected planned resume", tool.Description);
        Assert.Contains("declarationDurable", tool.InputSchema.ToString());
        Assert.Contains("Restart always resets continuous observation to zero", AgentPlaybookResources.GetOpeningMovementPlaybook().Text);
        Assert.Contains("not permission to relock after expansion", AgentPlaybookResources.GetOpeningMovementPlaybook().Text);
    }

    [Fact]
    public void AssemblyRegistration_ExposesOnlySafeCurrentGateTools()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();

        var tools = provider.GetServices<McpServerTool>().ToArray();

        Assert.Equal(
            new[]
            {
                "spherewright_commit_blueprint_build",
                "spherewright_commit_build",
                "spherewright_commit_cancel_blueprint",
                "spherewright_commit_configure_building",
                "spherewright_commit_dismantle",
                "spherewright_commit_handcraft",
                "spherewright_commit_harvest",
                "spherewright_commit_interplanetary_flight",
                "spherewright_commit_logistics_station_fleet_transfer",
                "spherewright_commit_move",
                "spherewright_commit_new_game",
                "spherewright_commit_quarantine_reconciliation",
                "spherewright_commit_refuel",
                "spherewright_commit_reload_flight_checkpoint",
                "spherewright_commit_resume_owned_game",
                "spherewright_commit_save",
                "spherewright_commit_save_import",
                "spherewright_commit_select_research",
                "spherewright_commit_transfer",
                "spherewright_commit_upgrade",
                "spherewright_export_blueprint",
                "spherewright_get_action_result",
                "spherewright_get_blueprint_builds",
                "spherewright_get_build_catalog",
                "spherewright_get_foundry_plan",
                "spherewright_get_gameplay_journal",
                "spherewright_get_governor_plan",
                "spherewright_get_local_star_system",
                "spherewright_get_overseer_diagnostic_bundle",
                "spherewright_get_overseer_production",
                "spherewright_get_overseer_summary",
                "spherewright_get_player_state",
                "spherewright_get_power_summary",
                "spherewright_get_progression_state",
                "spherewright_get_recipe_catalog",
                "spherewright_get_session_state",
                "spherewright_get_status",
                "spherewright_inspect_assembler",
                "spherewright_inspect_blueprint",
                "spherewright_inspect_factory_entity",
                "spherewright_inspect_resource_node",
                "spherewright_list_assemblers",
                "spherewright_list_factory_entities",
                "spherewright_list_resource_nodes",
                "spherewright_prepare_blueprint_build",
                "spherewright_prepare_build",
                "spherewright_prepare_cancel_blueprint",
                "spherewright_prepare_configure_building",
                "spherewright_prepare_dismantle",
                "spherewright_prepare_handcraft",
                "spherewright_prepare_harvest",
                "spherewright_prepare_interplanetary_flight",
                "spherewright_prepare_logistics_station_fleet_transfer",
                "spherewright_prepare_move",
                "spherewright_prepare_new_game",
                "spherewright_prepare_quarantine_reconciliation",
                "spherewright_prepare_refuel",
                "spherewright_prepare_reload_flight_checkpoint",
                "spherewright_prepare_resume_owned_game",
                "spherewright_prepare_save",
                "spherewright_prepare_save_import",
                "spherewright_prepare_select_research",
                "spherewright_prepare_transfer",
                "spherewright_prepare_upgrade",
            },
            tools.Select(tool => tool.ProtocolTool.Name).OrderBy(name => name).ToArray());
        Assert.All(
            tools.Where(tool => tool.ProtocolTool.Name.StartsWith("spherewright_get_", StringComparison.Ordinal)
                || tool.ProtocolTool.Name.StartsWith("spherewright_list_", StringComparison.Ordinal)
                || tool.ProtocolTool.Name.StartsWith("spherewright_inspect_", StringComparison.Ordinal)),
            tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint));
        Assert.All(
            tools.Where(tool => tool.ProtocolTool.Name.StartsWith("spherewright_commit_", StringComparison.Ordinal)),
            tool => Assert.True(tool.ProtocolTool.Annotations?.DestructiveHint));
        Assert.DoesNotContain(tools, tool => tool.ProtocolTool.Name.Contains("basic_production_line", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FoundryPlan_MapsExplicitRecipeAndSupplyChoicesWithoutCommit()
    {
        var client = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.GetFoundryPlanAsync(client, "session", 104, 1203, 20.5m,
            new[] { 1101 }, new[] { new FoundryRecipeChoice { ItemId = 1203, RecipeId = 16, BuildingItemId = 2303 } });
        Assert.False(result.IsError);
        Assert.Equal("session", client.LastSessionId);
        var request = Assert.IsType<GetFoundryPlanRequest>(client.LastFoundryRequest);
        Assert.Equal(20.5m, request.TargetRatePerMinute);
        Assert.Equal(1101, Assert.Single(request.ExternalSupplyItemIds));
        Assert.Equal(2303, Assert.Single(request.RecipeChoices).BuildingItemId);
        Assert.Null(request.Site);
    }

    [Fact]
    public async Task FoundryBlueprintLayoutAndIntentAreDiscoverableWithoutAnotherExecutor()
    {
        var client = new FakeBridgeClient(SuccessResult());
        var blueprint = new FoundryBlueprintRequest
        {
            BlueprintCode = "explicit data only", Site = new() { ExpectedPlayerStateHash = "player", Position = new() { Y = 200 } },
            BoundaryPorts = new() { new() { ItemId = 1001, Direction = "input", ObjectIndex = 0, Slot = 1 } },
        };
        await SpherewrightTools.GetFoundryPlanAsync(client, "session", 104, 1203, 10, blueprint: blueprint);
        Assert.Same(blueprint, client.LastFoundryRequest!.Blueprint);
        var intent = new FoundryConstructionIntent { TargetItemId = 1203, TargetRatePerMinute = 10, BoundaryPorts = blueprint.BoundaryPorts };
        await SpherewrightTools.PrepareBlueprintBuildAsync(client, "session", 104, "native-assessment", "player",
            blueprintCode: blueprint.BlueprintCode, site: blueprint.Site,
            foundryIntent: intent, expectedFoundryPlanHash: "construction-hash");
        Assert.Same(intent, client.LastBlueprintBuildRequest!.FoundryIntent);
        Assert.Equal("construction-hash", client.LastBlueprintBuildRequest.ExpectedFoundryPlanHash);
        Assert.Equal("native-assessment", client.LastBlueprintBuildRequest.ExpectedStateHash);
        var services = new ServiceCollection();
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().Select(t => t.ProtocolTool).ToArray();
        var read = tools.Single(t => t.Name == "spherewright_get_foundry_plan");
        Assert.True(read.Annotations!.ReadOnlyHint);
        Assert.True(read.InputSchema.GetProperty("properties").TryGetProperty("blueprint", out _));
        Assert.Contains("transportBudget", read.Description, StringComparison.Ordinal);
        Assert.Contains("transportCapacityVerified stays false", read.Description, StringComparison.Ordinal);
        var prepare = tools.Single(t => t.Name == "spherewright_prepare_blueprint_build");
        Assert.True(prepare.InputSchema.GetProperty("properties").TryGetProperty("foundryIntent", out _));
        Assert.True(prepare.InputSchema.GetProperty("properties").TryGetProperty("expectedFoundryPlanHash", out _));
        Assert.Contains("original intent remains durable", prepare.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpgradeMapsFreshIdentityRecipeAndPlayerEvidence()
    {
        var client = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareUpgradeAsync(client, "session", 104, 2255, 2304, 16,
            "endpoint-hash", "player-hash");
        Assert.False(result.IsError);
        var request = Assert.IsType<PrepareUpgradeRequest>(client.LastUpgradeRequest);
        Assert.Equal(0, request.ExpectedFilterItemId);
        Assert.Equal(104, request.PlanetId);
        Assert.Equal(2255, request.ObjectId);
        Assert.Equal(2304, request.TargetItemId);
        Assert.Equal(16, request.ExpectedRecipeId);
        Assert.Equal("endpoint-hash", request.ExpectedEndpointStateHash);
        Assert.Equal("player-hash", request.ExpectedPlayerStateHash);
        Assert.Equal(1, request.StateHashVersion);
    }

    [Fact]
    public async Task SorterUpgradeMapsExplicitInspectedFilter()
    {
        var client = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareUpgradeAsync(client, "session", 104, 749, 2012, 0,
            "endpoint-hash", "player-hash", expectedFilterItemId: 1101);
        Assert.False(result.IsError);
        var request = Assert.IsType<PrepareUpgradeRequest>(client.LastUpgradeRequest);
        Assert.Equal(2012, request.TargetItemId);
        Assert.Equal(1101, request.ExpectedFilterItemId);
        Assert.Equal(0, request.ExpectedRecipeId);
    }

    [Fact]
    public async Task FoundryPlan_MapsOptionalBoundedSiteAndRemainsDiscoverableReadOnly()
    {
        var client = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.GetFoundryPlanAsync(client, "session", 104, 1203, 30,
            site: new FoundrySiteRequest { Origin = new Vector3Snapshot { Y = 200 }, YawDegrees = 90, Columns = 2 });
        Assert.False(result.IsError);
        var request = Assert.IsType<GetFoundryPlanRequest>(client.LastFoundryRequest);
        Assert.Equal(200, request.Site!.Origin.Y); Assert.Equal(90, request.Site.YawDegrees); Assert.Equal(2, request.Site.Columns);
        Assert.Equal(12, request.Site.RowSpacing);
        var services = new ServiceCollection();
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = provider.GetServices<McpServerTool>().Single(t => t.ProtocolTool.Name == "spherewright_get_foundry_plan").ProtocolTool;
        Assert.True(tool.Annotations!.ReadOnlyHint); Assert.False(tool.Annotations.DestructiveHint);
        Assert.True(tool.InputSchema.GetProperty("properties").TryGetProperty("site", out _));
        Assert.Contains("native grid snapping", tool.Description, StringComparison.Ordinal);
        Assert.Contains("not permission to build", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyRegistration_ExposesReadableOpeningMovementPlaybookResource()
    {
        var services = new ServiceCollection();
        services.AddMcpServer().WithResourcesFromAssembly(typeof(AgentPlaybookResources).Assembly);
        using var provider = services.BuildServiceProvider();

        var resource = Assert.Single(provider.GetServices<McpServerResource>());
        Assert.False(resource.IsTemplated);
        var descriptor = resource.ProtocolResource
            ?? throw new InvalidOperationException("Expected a direct MCP resource descriptor.");
        Assert.Equal(AgentPlaybookResources.OpeningMovementUri, descriptor.Uri);
        Assert.Equal("text/markdown", descriptor.MimeType);

        var contents = AgentPlaybookResources.GetOpeningMovementPlaybook();
        Assert.Equal(AgentPlaybookResources.OpeningMovementUri, contents.Uri);
        Assert.Contains("do not submit the same target again", contents.Text, StringComparison.Ordinal);
        Assert.Contains("about **5 m**", contents.Text, StringComparison.Ordinal);
        Assert.Contains("four targets", contents.Text, StringComparison.Ordinal);
        Assert.Contains("each direction **once**", contents.Text, StringComparison.Ordinal);
        Assert.Contains("movementState=Walk", contents.Text, StringComparison.Ordinal);
        Assert.Contains("A commit acceptance or host timeout is not completion", contents.Text, StringComparison.Ordinal);
        Assert.Contains("Retain `actionId` and its terminal result before formatting", contents.Text, StringComparison.Ordinal);
        Assert.Contains("spherewright_get_foundry_plan", contents.Text, StringComparison.Ordinal);
        Assert.Contains("spherewright_get_governor_plan", contents.Text, StringComparison.Ordinal);
        Assert.Contains("prioritizeQueued=true", contents.Text, StringComparison.Ordinal);
        Assert.Contains("researchQueueReadback", contents.Text, StringComparison.Ordinal);
        Assert.Contains("get_blueprint_builds", contents.Text, StringComparison.Ordinal);
        Assert.Contains("never replay the whole blueprint", contents.Text, StringComparison.Ordinal);
        Assert.Contains("material draft is not an approved site", contents.Text, StringComparison.Ordinal);
        Assert.Contains("machine_previews_clear", contents.Text, StringComparison.Ordinal);
        Assert.Contains("up to 32 machines", contents.Text, StringComparison.Ordinal);
        Assert.Contains("Do not use `assessmentHash` as a state hash or commit token", contents.Text, StringComparison.Ordinal);
        Assert.Contains("prepare_harvest", contents.Text, StringComparison.Ordinal);
        Assert.Contains("Do not declare a production line complete", contents.Text, StringComparison.Ordinal);
        Assert.Contains("recovery_required", contents.Text, StringComparison.Ordinal);
        Assert.Contains("shore recovery", contents.Text, StringComparison.Ordinal);
        Assert.Contains("site.power", contents.Text, StringComparison.Ordinal);
        Assert.Contains("targetChainFindings", contents.Text, StringComparison.Ordinal);
        Assert.Contains("separate inventory observation interval", contents.Text, StringComparison.Ordinal);
        Assert.Contains("initialSorterFilterItemId", contents.Text, StringComparison.Ordinal);
        Assert.Contains("Later configuration cannot undo contamination", contents.Text, StringComparison.Ordinal);
        Assert.Contains("head-of-line blocking", contents.Text, StringComparison.Ordinal);
        Assert.Contains("reuse the already built objects", contents.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentPlaybook_DistinguishesSnappedPowerAssessmentFromRequestedConsumerPosition()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("snapped `site.machines[].position`", guide, StringComparison.Ordinal);
        Assert.Contains("not the requested `site.origin`", guide, StringComparison.Ordinal);
        Assert.Contains("not an exact-position power probe", guide, StringComparison.Ordinal);
        Assert.Contains("either a positive or a negative coverage result", guide, StringComparison.Ordinal);
        Assert.Contains("actual native `plannedPosition`", guide, StringComparison.Ordinal);
        Assert.Contains("clear coverage margin", guide, StringComparison.Ordinal);
        Assert.Contains("real planned consumers rather than the proxy machine", guide, StringComparison.Ordinal);
        Assert.Contains("completed consumer's actual power network and supply", guide, StringComparison.Ordinal);
        Assert.Contains("Missing geometry is unknown, not permission to build", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void NewWorldCommit_AdvertisesDiscoverableOpeningPlaybook()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer()
            .WithToolsFromAssembly(typeof(SpherewrightTools).Assembly)
            .WithResourcesFromAssembly(typeof(AgentPlaybookResources).Assembly);
        using var provider = services.BuildServiceProvider();

        var commitNewGame = Assert.Single(
            provider.GetServices<McpServerTool>(),
            tool => tool.ProtocolTool.Name == "spherewright_commit_new_game");
        var playbook = Assert.Single(provider.GetServices<McpServerResource>());
        var playbookDescriptor = playbook.ProtocolResource
            ?? throw new InvalidOperationException("Expected a direct MCP resource descriptor.");

        Assert.Contains(AgentPlaybookResources.OpeningMovementUri, commitNewGame.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Equal(AgentPlaybookResources.OpeningMovementUri, playbookDescriptor.Uri);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task BuildMapsInitialSorterFilterAndPreservesEndpointAndPlayerBindings(int filter)
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "owned-session", 104, 2011, "fresh-player",
            sourceObjectId: 10, expectedSourceStateHash: "fresh-source", destinationObjectId: 20,
            expectedDestinationStateHash: "fresh-destination", initialSorterFilterItemId: filter);
        Assert.False(result.IsError);
        var request = Assert.IsType<PrepareBuildRequest>(bridge.LastBuildRequest);
        Assert.Equal(filter, request.InitialSorterFilterItemId);
        Assert.Equal("fresh-player", request.ExpectedPlayerStateHash);
        Assert.Equal("fresh-source", request.ExpectedSourceStateHash);
        Assert.Equal("fresh-destination", request.ExpectedDestinationStateHash);
        Assert.Equal(10, request.SourceObjectId);
        Assert.Equal(20, request.DestinationObjectId);
    }

    [Fact]
    public void InitialSorterFilterIsDiscoverableWithContaminationWarning()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(),
            value => value.ProtocolTool.Name == "spherewright_prepare_build").ProtocolTool;
        Assert.True(tool.InputSchema.GetProperty("properties").TryGetProperty("initialSorterFilterItemId", out _));
        Assert.Contains("before the first pickup", tool.Description);
        Assert.Contains("later configuration cannot undo contamination", tool.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1115)]
    public async Task OldOrMismatchedPluginCannotExposeAnUnfilteredConstructionToken(int? echo)
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { OmitBuildFilterEcho = !echo.HasValue, BuildFilterEcho = echo };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2011, "player",
            initialSorterFilterItemId: 1000);
        Assert.True(result.IsError);
        var content = result.StructuredContent!.Value;
        Assert.False(content.GetProperty("success").GetBoolean());
        Assert.Equal(BridgeErrorCodes.BridgeNotReady, content.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, content.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task LegacyUnfilteredBuildDoesNotRequireTheNewResponseField()
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { OmitBuildFilterEcho = true };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "session", 104, 2011, "player");
        Assert.False(result.IsError);
        Assert.Equal(0, bridge.LastBuildRequest!.InitialSorterFilterItemId);
    }

    [Fact]
    public async Task StatusTool_ReturnsStructuredSuccess()
    {
        var result = await SpherewrightTools.GetStatusAsync(
            new FakeBridgeClient(SuccessResult()),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.HasValue);
        var structured = result.StructuredContent.Value;
        Assert.True(structured.GetProperty("success").GetBoolean());
        Assert.True(structured.GetProperty("status").GetProperty("bridgeConnected").GetBoolean());
        Assert.Equal(
            AgentPlaybookResources.OpeningMovementUri,
            structured.GetProperty("agentPlaybookResourceUri").GetString());
        Assert.Contains("before the first gameplay action", structured.GetProperty("recommendedFirstStep").GetString(), StringComparison.Ordinal);
        Assert.False(structured.TryGetProperty("authToken", out _));
    }

    [Fact]
    public async Task ActionResultTool_ReturnsStructuredMovementRecovery()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ActionResult = new ActionResultSnapshot
            {
                ActionId = "move-stall",
                ActionKind = NormalActionKinds.Move,
                State = NormalActionStates.ActionFailed,
                Terminal = true,
                Succeeded = false,
                Stalled = true,
                FailureKind = MovementFailureKinds.PositionStalled,
                StalledGameTicks = 180,
                RemainingDistance = 12.5,
                DoNotRetrySameTarget = true,
                RecommendedRecovery = MovementFailureRecoveryAdvisor.RecoverySummary,
                RecommendedShortMoveDistanceMeters = 5,
                OrthogonalProbeDistanceMeters = 4,
                MaximumOrthogonalProbeAttempts = 4,
            },
        };

        var result = await SpherewrightTools.GetActionResultAsync(
            bridge,
            "move-stall",
            CancellationToken.None);

        var action = result.StructuredContent!.Value.GetProperty("result");
        Assert.Equal("position_stalled", action.GetProperty("failureKind").GetString());
        Assert.Equal(180, action.GetProperty("stalledGameTicks").GetInt64());
        Assert.Equal(12.5, action.GetProperty("remainingDistance").GetDouble());
        Assert.True(action.GetProperty("doNotRetrySameTarget").GetBoolean());
        Assert.Equal(5, action.GetProperty("recommendedShortMoveDistanceMeters").GetDouble());
        Assert.Equal(4, action.GetProperty("orthogonalProbeDistanceMeters").GetDouble());
        Assert.Equal(4, action.GetProperty("maximumOrthogonalProbeAttempts").GetInt32());
    }

    [Fact]
    public async Task StatusTool_ReturnsStructuredBridgeNotReadyError()
    {
        var error = BridgeError.Create(
            BridgeErrorCodes.BridgeNotReady,
            "Bridge unavailable.",
            true,
            "Start DSP and retry.");

        var result = await SpherewrightTools.GetStatusAsync(
            new FakeBridgeClient(BridgeCallResult<BridgeStatus>.Failed(error)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(
            BridgeErrorCodes.BridgeNotReady,
            result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ListAssemblersTool_MapsSessionLimitAndCursor()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ListResult = BridgeCallResult<ListAssemblersResult>.Succeeded(new ListAssemblersResult
            {
                Revision = 9,
            }),
        };

        var result = await SpherewrightTools.ListAssemblersAsync(
            bridge,
            "session-9",
            17,
            "cursor-9",
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-9", bridge.LastSessionId);
        Assert.Equal(17, bridge.LastListRequest?.Limit);
        Assert.Equal("cursor-9", bridge.LastListRequest?.Cursor);
        Assert.Equal(9, result.StructuredContent!.Value.GetProperty("result").GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task GameplayJournalTool_MapsOwnedSession()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.GetGameplayJournalAsync(
            bridge,
            "session-journal",
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-journal", bridge.LastSessionId);
        Assert.Equal(
            "journal",
            result.StructuredContent!.Value.GetProperty("result").GetProperty("journalId").GetString());
    }

    [Fact]
    public async Task OverseerProductionTool_MapsBoundedItemsPageAndCursor()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.GetOverseerProductionAsync(
            bridge,
            "session-overseer",
            new[] { 6003, 6001 },
            4,
            "cursor-overseer",
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-overseer", bridge.LastSessionId);
        Assert.Equal(new[] { 6003, 6001 }, bridge.LastOverseerProductionRequest?.ItemIds);
        Assert.Equal(4, bridge.LastOverseerProductionRequest?.Limit);
        Assert.Equal("cursor-overseer", bridge.LastOverseerProductionRequest?.Cursor);
    }

    [Fact]
    public async Task OverseerSummaryTool_MapsPlanetPageAndCursor()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.GetOverseerSummaryAsync(
            bridge,
            "session-overseer-summary",
            3,
            "cursor-summary",
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-overseer-summary", bridge.LastSessionId);
        Assert.Equal(3, bridge.LastOverseerSummaryRequest?.Limit);
        Assert.Equal("cursor-summary", bridge.LastOverseerSummaryRequest?.Cursor);
    }

    [Fact]
    public async Task OverseerDiagnosticBundleTool_MapsBoundedItemsPageAndCursor()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.GetOverseerDiagnosticBundleAsync(
            bridge,
            "session-overseer-bundle",
            new[] { 6003, 1106 },
            2,
            "cursor-bundle",
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-overseer-bundle", bridge.LastSessionId);
        Assert.Equal(new[] { 6003, 1106 }, bridge.LastOverseerDiagnosticBundleRequest?.ItemIds);
        Assert.Equal(2, bridge.LastOverseerDiagnosticBundleRequest?.Limit);
        Assert.Equal("cursor-bundle", bridge.LastOverseerDiagnosticBundleRequest?.Cursor);
    }

    [Fact]
    public async Task OverseerBundlePreservesStockedLogisticsBoundaryEvidence()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            DiagnosticBundleSnapshot = new OverseerDiagnosticBundleSnapshot
            {
                Planets = new List<OverseerDiagnosticBundlePlanetSnapshot>
                {
                    new()
                    {
                        PlanetId = 104,
                        Production = new List<ProductionRateSnapshot>
                        {
                            new()
                            {
                                ItemId = 1106,
                                Findings = new List<OverseerFindingSnapshot>
                                {
                                    new()
                                    {
                                        Kind = OverseerFindingKinds.MaterialShortage,
                                        Evidence = new List<OverseerEvidenceSnapshot>
                                        {
                                            new() { Metric = "source_inventory", NumericValue = 200 },
                                            new() { Metric = "source_inventory_scope", TextValue = "configured_route_supply_total" },
                                            new() { Metric = "logistics_dispatch_state", TextValue = "unproven" },
                                            new() { Metric = "upstream_trace_stop_reason", TextValue = "stocked_logistics_boundary" },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        var result = await SpherewrightTools.GetOverseerDiagnosticBundleAsync(bridge, "session-boundary", new[] { 1106 });

        Assert.False(result.IsError);
        var finding = result.StructuredContent!.Value.GetProperty("result").GetProperty("planets")[0]
            .GetProperty("production")[0].GetProperty("findings")[0];
        Assert.Equal(OverseerFindingKinds.MaterialShortage, finding.GetProperty("kind").GetString());
        var evidence = finding.GetProperty("evidence");
        Assert.Equal(200, evidence[0].GetProperty("numericValue").GetDouble());
        Assert.Equal("configured_route_supply_total", evidence[1].GetProperty("textValue").GetString());
        Assert.Equal("unproven", evidence[2].GetProperty("textValue").GetString());
        Assert.Equal("stocked_logistics_boundary", evidence[3].GetProperty("textValue").GetString());
    }

    [Fact]
    public void PackagedPlaybookDistinguishesStockFromDispatchAndCausalProof()
    {
        var playbook = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("stocked_logistics_boundary", playbook, StringComparison.Ordinal);
        Assert.Contains("configured_route_supply_total", playbook, StringComparison.Ordinal);
        Assert.Contains("logistics_dispatch_state=unproven", playbook, StringComparison.Ordinal);
        Assert.Contains("not available/unreserved stock", playbook, StringComparison.Ordinal);
        Assert.Contains("before changing logistics or expanding a mine", playbook, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PrepareSelectResearch_MapsDedicatedSelectionHash(bool prioritize)
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareSelectResearchAsync(
            bridge,
            "session-research",
            104,
            1604,
            "sha256:selection",
            1,
            prioritize,
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-research", bridge.LastSessionId);
        Assert.Equal(1604, bridge.LastSelectResearchRequest?.TechId);
        Assert.Equal("sha256:selection", bridge.LastSelectResearchRequest?.ExpectedSelectionStateHash);
        Assert.Equal(prioritize, bridge.LastSelectResearchRequest?.PrioritizeQueued);
    }

    [Fact]
    public async Task WarehouseOperationMapsNativeDisabledGridCountAndFreshHash()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(bridge, "session-storage", 104, 761, 0,
            "fresh-storage", mode: BuildingConfigurationModes.StorageCapacity,
            storageOperation: StorageConfigurationOperations.SetBans, storageBannedGridCount: 30);
        Assert.False(result.IsError);
        Assert.Equal(BuildingConfigurationModes.StorageCapacity, bridge.LastConfigureRequest!.Mode);
        Assert.Equal("fresh-storage", bridge.LastConfigureRequest.ExpectedFactoryStateHash);
        Assert.Equal(StorageConfigurationOperations.SetBans, bridge.LastConfigureRequest.StorageOperation);
        Assert.Equal(30, bridge.LastConfigureRequest.StorageBannedGridCount);
    }

    [Fact]
    public async Task WarehouseWithoutConfirmedEchoNeverExposesToken()
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { OmitStorageEcho = true };
        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(bridge, "session-storage", 104, 761, 0,
            "fresh-storage", mode: BuildingConfigurationModes.StorageCapacity,
            storageOperation: StorageConfigurationOperations.FilterEmptyOrMatching, filterItemId: 1000);
        Assert.True(result.IsError);
        var content = result.StructuredContent!.Value;
        Assert.Equal(BridgeErrorCodes.BridgeNotReady, content.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, content.GetProperty("result").ValueKind);
        Assert.Equal(-1, bridge.LastConfigureRequest!.StorageBannedGridCount);
        Assert.Equal(1000, bridge.LastConfigureRequest.FilterItemId);
    }

    [Fact]
    public void WarehouseDescriptionDisclosesNativeCapacityMeaningAndNonGridBufferOffsets()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareConfigureBuildingAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Single()).Description;
        Assert.Contains("DISABLED final grids", description); Assert.Contains("NOT grid indices", description);
        Assert.Contains("do not clear occupied grids", description); Assert.Contains("lock-occupied", description);
    }

    [Theory]
    [InlineData(3600, false)] [InlineData(600, true)]
    public async Task GovernorMinuteWindowRequiresMatchingInstalledPlugin(int echoed, bool rejected)
    {
        var client = new FakeBridgeClient(SuccessResult()) { GovernorResponseMeasurementGameTicks = echoed };
        var result = await SpherewrightTools.GetGovernorPlanAsync(client, "session", 104, 1109, 76,
            new[] { new BlueprintSelectedEntity { ObjectId = 113, ExpectedEndpointStateHash = "fresh", ExpectedRecipeId = 17 } },
            measurementGameTicks: 3600);
        Assert.Equal(3600, client.LastGovernorRequest!.MeasurementGameTicks);
        Assert.Equal(rejected, result.IsError);
    }

    [Theory]
    [InlineData(0)] [InlineData(36000)]
    public async Task GovernorRejectsUnsupportedMeasurementPeriodBeforeCallingBridge(int period)
    {
        var client = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.GetGovernorPlanAsync(client, "session", 104, 1109, 76,
            new[] { new BlueprintSelectedEntity { ObjectId = 113, ExpectedEndpointStateHash = "fresh" } },
            measurementGameTicks: period);
        Assert.True(result.IsError); Assert.Null(client.LastGovernorRequest);
    }

    [Fact]
    public void WarehouseReservationRaceRecoveryIsDiscoverableInMcpAndPackagedPlaybook()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareConfigureBuildingAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Single()).Description;
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var phrase in new[] { "filter0 does not prove an empty grid", "lock-occupied does not reserve empty grids", "storage_configuration_unchanged" })
        {
            Assert.Contains(phrase, description);
            Assert.Contains(phrase, guide);
        }
        Assert.Contains("TakeItem searches forward", guide);
        Assert.Contains("original bans to restore", guide);
        Assert.Contains("not a global inventory freeze", guide);
        Assert.Contains("Ordinary sorter withdrawals can continue even with all grids banned", guide);
        Assert.Contains("exactly one matching projected filter", guide);
        Assert.Contains("plan only the unfinished steps", guide);
        Assert.Contains("Never repeat stock removal", guide);
        Assert.Contains("never repeatedly drain stock or relax the full stateHash", description);
    }

    [Fact]
    public void ConfigureHashParameterExplicitlySeparatesSorterAndWarehouseDomains()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareConfigureBuildingAsync))!;
        var parameter = method.GetParameters().Single(p => p.Name == "expectedFactoryStateHash");
        var description = ((System.ComponentModel.DescriptionAttribute)parameter.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Single()).Description;
        Assert.Contains("configurationStateHash, NOT stateHash", description);
        Assert.Contains("storage-capacity use the full stateHash", description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("sorter-filter uses root `configurationStateHash`; storage-capacity uses full `stateHash`", guide);
        Assert.Contains("inspect the actual payload/hash source", guide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WarehouseInstantReadbackSurvivesMcpAndWarnsAgainstCrossTickEquality()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ActionResult = new ActionResultSnapshot
            {
                ActionId = "storage-config", Terminal = true, Succeeded = true,
                StorageConfigurationReadback = new StorageConfigurationReadback
                {
                    EntityId = 761, CapturedAtGameTick = 100, Operation = StorageConfigurationOperations.SetBans,
                    BuffersBefore = new List<FactoryBufferSnapshot> { new() { ItemId = 1120, Count = 1 } },
                    BuffersAfter = new List<FactoryBufferSnapshot> { new() { ItemId = 1120, Count = 1 } },
                },
            },
        };
        var result = await SpherewrightTools.GetActionResultAsync(bridge, "storage-config", CancellationToken.None);
        var proof = result.StructuredContent!.Value.GetProperty("result").GetProperty("storageConfigurationReadback");
        Assert.Equal(100, proof.GetProperty("capturedAtGameTick").GetInt64());
        Assert.Equal(1, proof.GetProperty("buffersAfter")[0].GetProperty("count").GetInt32());
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.CommitConfigureBuildingAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Single()).Description;
        Assert.Contains("storageConfigurationReadback", description);
        Assert.Contains("do not require cross-tick equality", description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("synchronous native configuration boundary", guide);
        Assert.Contains("do not require cross-tick stock equality", guide);
    }

    [Fact]
    public void SorterFilterDescriptionDisclosesRetainedCargoAndUnchangedDestination()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareConfigureBuildingAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Single()).Description;
        Assert.Contains("configurationStateHash", description);
        Assert.Contains("Inserting", description);
        Assert.Contains("SAME destination", description);
        Assert.Contains("cannot clear a jam", description);
    }

    [Theory]
    [InlineData(600, 580, 0, 20)]
    [InlineData(0, 20, 20, 0)]
    public async Task TransferTerminalPreservesSameTickBilateralEvidence(
        int storageBefore, int storageAfter, int playerBefore, int playerAfter)
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ActionResult = new ActionResultSnapshot
            {
                ActionId = "transfer-proof", ActionKind = NormalActionKinds.Transfer,
                State = NormalActionStates.Completed, Terminal = true, Succeeded = true,
                StartedAtGameTick = 100, CompletedAtGameTick = 100,
                TargetObjectId = 753, TargetItemId = 1000, RequestedCount = 20,
                BeforeTargetAmount = storageBefore, AfterTargetAmount = storageAfter,
                ItemDeltas = new List<ActionItemDelta>
                {
                    new() { ItemId = 1000, BeforeCount = playerBefore, AfterCount = playerAfter, Delta = playerAfter - playerBefore },
                },
            },
        };
        var result = await SpherewrightTools.GetActionResultAsync(bridge, "transfer-proof", CancellationToken.None);
        var proof = result.StructuredContent!.Value.GetProperty("result");
        Assert.True(proof.GetProperty("terminal").GetBoolean());
        Assert.True(proof.GetProperty("succeeded").GetBoolean());
        Assert.Equal(100, proof.GetProperty("completedAtGameTick").GetInt64());
        Assert.Equal(storageBefore, proof.GetProperty("beforeTargetAmount").GetInt32());
        Assert.Equal(storageAfter, proof.GetProperty("afterTargetAmount").GetInt32());
        var player = proof.GetProperty("itemDeltas")[0];
        Assert.Equal(playerBefore, player.GetProperty("beforeCount").GetInt32());
        Assert.Equal(playerAfter, player.GetProperty("afterCount").GetInt32());
        Assert.Equal(0, storageAfter - storageBefore + player.GetProperty("delta").GetInt32());
    }

    [Fact]
    public void TransferGuideSeparatesTerminalConservationFromLaterLogistics()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.CommitTransferAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        foreach (var field in new[] { "beforeTargetAmount", "afterTargetAmount", "itemDeltas", "completedAtGameTick" })
            Assert.Contains(field, description);
        Assert.Contains("do not require cross-tick equality", description);
        Assert.Contains("never replay", description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("transfer uses `storageEntityId`", guide);
        Assert.Contains("ordinary transfer's terminal result", guide);
        Assert.Contains("600 -> 580", guide);
        Assert.Contains("later581", guide);
        Assert.Contains("not a sustained-supply baseline", guide);
        Assert.Contains("original game-tick deadline", guide);
        Assert.Contains("later zero-production window does not prove no earlier production", guide);
    }

    [Fact]
    public void SorterConfigurationGuideUsesRequestBindingNotBuildEchoes()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareConfigureBuildingAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Single()).Description;
        Assert.Contains("request-bound expectedStateHash", description);
        Assert.Contains("sorter-build echo plannedSorterFilterItemId", description);
        Assert.Contains("missing build echoes never justify replay", description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("request-bound `expectedStateHash`", guide);
        Assert.Contains("not mandatory configuration fields", guide);
        Assert.Contains("configuration terminal `targetObjectIds` may be empty", guide);
        Assert.Contains("reuse the current Core canonical hash", guide);
        Assert.Contains("poll the same action to terminal", guide);
        Assert.Contains("replay an accepted action after a caller assertion fails", guide);
    }

    [Fact]
    public async Task ConfigureBuildingTool_MapsSorterFilterWithoutInventedResponseEchoes()
    {
        var bridge = new FakeBridgeClient(SuccessResult())
        {
            ConfigurePlan = new PreparedNormalAction
            {
                Prepared = true,
                ActionKind = NormalActionKinds.ConfigureBuilding,
                PlanToken = "plan",
                CommitAllowedNow = true,
                ExpectedStateHash = "sha256:server-plan-binding",
            },
        };

        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(
            bridgeClient: bridge,
            sessionId: "session-filter",
            planetId: 103,
            entityId: 12,
            recipeId: 0,
            expectedFactoryStateHash: "sha256:factory",
            mode: BuildingConfigurationModes.SorterFilter,
            filterItemId: 1120,
            stateHashVersion: 1,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-filter", bridge.LastSessionId);
        Assert.Equal(BuildingConfigurationModes.SorterFilter, bridge.LastConfigureRequest?.Mode);
        Assert.Equal(1120, bridge.LastConfigureRequest?.FilterItemId);
        Assert.Equal("sha256:factory", bridge.LastConfigureRequest?.ExpectedFactoryStateHash);
        var plan = result.StructuredContent!.Value.GetProperty("result");
        Assert.True(plan.GetProperty("prepared").GetBoolean());
        Assert.True(plan.GetProperty("commitAllowedNow").GetBoolean());
        Assert.Equal("sha256:server-plan-binding", plan.GetProperty("expectedStateHash").GetString());
        Assert.False(plan.TryGetProperty("targetObjectId", out var target) && target.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task ConfigureBuildingTool_MapsLogisticsStationStorageMode()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(
            bridgeClient: bridge,
            sessionId: "session-station",
            planetId: 104,
            entityId: 920,
            recipeId: 0,
            expectedFactoryStateHash: "sha256:factory",
            mode: BuildingConfigurationModes.LogisticsStationStorage,
            stationStorageIndex: 2,
            stationItemId: 1106,
            stationMaximumCount: 5_000,
            stationLocalLogic: LogisticsStorageLogics.Demand,
            stationRemoteLogic: LogisticsStorageLogics.Supply,
            expectedStationConfigurationStateHash: "sha256:station-config",
            stateHashVersion: 1,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-station", bridge.LastSessionId);
        Assert.Equal(BuildingConfigurationModes.LogisticsStationStorage, bridge.LastConfigureRequest?.Mode);
        Assert.Equal(2, bridge.LastConfigureRequest?.StationStorageIndex);
        Assert.Equal(1106, bridge.LastConfigureRequest?.StationItemId);
        Assert.Equal(5_000, bridge.LastConfigureRequest?.StationMaximumCount);
        Assert.Equal(LogisticsStorageLogics.Demand, bridge.LastConfigureRequest?.StationLocalLogic);
        Assert.Equal(LogisticsStorageLogics.Supply, bridge.LastConfigureRequest?.StationRemoteLogic);
        Assert.Equal("sha256:station-config", bridge.LastConfigureRequest?.ExpectedStationConfigurationStateHash);
    }

    [Fact]
    public async Task ConfigureBuildingTool_MapsLogisticsStationChargeMode()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(
            bridgeClient: bridge,
            sessionId: "session-station-charge",
            planetId: 104,
            entityId: 920,
            recipeId: 0,
            expectedFactoryStateHash: "sha256:factory",
            mode: BuildingConfigurationModes.LogisticsStationCharge,
            stationMaximumChargePowerWatts: 12_000_000,
            expectedStationConfigurationStateHash: "sha256:station-config",
            stateHashVersion: 1,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-station-charge", bridge.LastSessionId);
        Assert.Equal(BuildingConfigurationModes.LogisticsStationCharge, bridge.LastConfigureRequest?.Mode);
        Assert.Equal(12_000_000, bridge.LastConfigureRequest?.StationMaximumChargePowerWatts);
        Assert.Equal("sha256:station-config", bridge.LastConfigureRequest?.ExpectedStationConfigurationStateHash);
    }

    [Fact]
    public async Task ConfigureBuildingTool_MapsLogisticsStationBeltMode()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(
            bridgeClient: bridge,
            sessionId: "session-station-belt",
            planetId: 104,
            entityId: 920,
            recipeId: 0,
            expectedFactoryStateHash: "sha256:factory",
            mode: BuildingConfigurationModes.LogisticsStationBelt,
            stationBeltSlotIndex: 3,
            stationBeltStorageIndex: 0,
            expectedStationConfigurationStateHash: "sha256:station-config",
            stateHashVersion: 1,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-station-belt", bridge.LastSessionId);
        Assert.Equal(BuildingConfigurationModes.LogisticsStationBelt, bridge.LastConfigureRequest?.Mode);
        Assert.Equal(3, bridge.LastConfigureRequest?.StationBeltSlotIndex);
        Assert.Equal(0, bridge.LastConfigureRequest?.StationBeltStorageIndex);
        Assert.Equal("sha256:station-config", bridge.LastConfigureRequest?.ExpectedStationConfigurationStateHash);
    }

    [Fact]
    public async Task DismantleTool_MapsStableEndpointAndPlayerHashes()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareDismantleAsync(
            bridge,
            "session-dismantle",
            102,
            17,
            "sha256:endpoint",
            "sha256:player",
            1,
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-dismantle", bridge.LastSessionId);
        Assert.Equal(102, bridge.LastDismantleRequest?.PlanetId);
        Assert.Equal(17, bridge.LastDismantleRequest?.ObjectId);
        Assert.Equal("sha256:endpoint", bridge.LastDismantleRequest?.ExpectedEndpointStateHash);
        Assert.Equal("sha256:player", bridge.LastDismantleRequest?.ExpectedPlayerStateHash);
    }

    [Fact]
    public async Task PrepareStationFleetTransfer_MapsExactFleetHashAndDirection()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareLogisticsStationFleetTransferAsync(
            bridge,
            "session-fleet",
            104,
            920,
            LogisticsStationFleetTransferDirections.PlayerToStation,
            LogisticsFleetItemIds.Drone,
            10,
            "sha256:player",
            "sha256:fleet",
            1,
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-fleet", bridge.LastSessionId);
        Assert.Equal(920, bridge.LastFleetTransferRequest?.StationEntityId);
        Assert.Equal(LogisticsStationFleetTransferDirections.PlayerToStation, bridge.LastFleetTransferRequest?.Direction);
        Assert.Equal(LogisticsFleetItemIds.Drone, bridge.LastFleetTransferRequest?.ItemId);
        Assert.Equal("sha256:fleet", bridge.LastFleetTransferRequest?.ExpectedStationFleetStateHash);
    }

    [Fact]
    public async Task PrepareQuarantineReconciliation_MapsExactActionAndRevision()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareQuarantineReconciliationAsync(
            bridge,
            "session-quarantine",
            104,
            "action-quarantine",
            445,
            1,
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-quarantine", bridge.LastSessionId);
        Assert.Equal("action-quarantine", bridge.LastReconciliationRequest?.ActionId);
        Assert.Equal(445, bridge.LastReconciliationRequest?.ExpectedRevision);
    }

    [Fact]
    public async Task PrepareInterplanetaryFlight_MapsBoundDestinationAndProofs()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareInterplanetaryFlightAsync(
            bridge,
            "session-flight",
            104,
            103,
            "sha256:player",
            "sha256:star",
            0.97d,
            1,
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("session-flight", bridge.LastSessionId);
        Assert.Equal(104, bridge.LastFlightRequest?.PlanetId);
        Assert.Equal(103, bridge.LastFlightRequest?.DestinationPlanetId);
        Assert.Equal("sha256:player", bridge.LastFlightRequest?.ExpectedPlayerStateHash);
        Assert.Equal("sha256:star", bridge.LastFlightRequest?.ExpectedStarSystemStateHash);
        Assert.Equal(0.97d, bridge.LastFlightRequest?.MinimumCoreEnergyRatio);
    }

    [Fact]
    public async Task PrepareFlightCheckpointReload_MapsOnlyReusableToken()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var result = await SpherewrightTools.PrepareFlightCheckpointReloadAsync(
            bridge,
            "checkpoint-token",
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("checkpoint-token", bridge.LastFlightCheckpointReloadRequest?.ReloadToken);
    }

    [Fact]
    public async Task SaveImportTools_MapExplicitConversationConfirmation()
    {
        var bridge = new FakeBridgeClient(SuccessResult());

        var prepared = await SpherewrightTools.PrepareUserSaveImportAsync(
            bridge,
            "session-import",
            7,
            CancellationToken.None);
        var committed = await SpherewrightTools.CommitUserSaveImportAsync(
            bridge,
            "session-import",
            "import-plan",
            "f1078b10-c48b-430f-b0e0-4de18438762c",
            true,
            true,
            true,
            CancellationToken.None);

        Assert.False(prepared.IsError);
        Assert.True(prepared.StructuredContent!.Value
            .GetProperty("result")
            .GetProperty("userConfirmationRequired")
            .GetBoolean());
        Assert.False(committed.IsError);
        Assert.Equal("session-import", bridge.LastSessionId);
        Assert.Equal(7, bridge.LastImportPrepareRequest?.ExpectedRevision);
        Assert.Equal("import-plan", bridge.LastImportCommitRequest?.PlanToken);
        Assert.True(bridge.LastImportCommitRequest?.UserConfirmedInConversation);
        Assert.True(bridge.LastImportCommitRequest?.AcknowledgeOriginalSaveRemainsUnchanged);
        Assert.True(bridge.LastImportCommitRequest?.AcknowledgeJournalStartsAtImport);
    }

    private static BridgeCallResult<BridgeStatus> SuccessResult()
    {
        return BridgeCallResult<BridgeStatus>.Succeeded(new BridgeStatus
        {
            BridgeConnected = true,
            BridgeInstanceId = "instance",
            PluginVersion = Spherewright.Contracts.Versioning.SpherewrightProduct.CurrentVersion,
            ProtocolVersion = ProtocolConstants.CurrentVersion,
            GameVersion = "0.10.34.28529",
            GameLoaded = false,
            WritesConfigured = false,
            WriteHealth = WriteHealthStates.Healthy,
        });
    }

    [Fact]
    public async Task BlueprintsExposeOnlyReadOnlyDataAndExactSelection()
    {
        var client = new FakeBridgeClient(SuccessResult());
        const string untrusted = "BLUEPRINT:data-is-not-instructions";
        Assert.False((await SpherewrightTools.InspectBlueprintAsync(client, "session", 104, untrusted)).IsError);
        Assert.Equal(untrusted, client.LastBlueprintRequest?.BlueprintCode);
        var selected = new BlueprintSelectedEntity { ObjectId = 724, ExpectedRecipeId = 97, ExpectedEndpointStateHash = "endpoint" };
        Assert.False((await SpherewrightTools.ExportBlueprintAsync(client, "session", 104, new[] { selected })).IsError);
        Assert.Same(selected, Assert.Single(client.LastExportRequest!.Entities));
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(client);
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().Where(t => t.ProtocolTool.Name == "spherewright_inspect_blueprint"
            || t.ProtocolTool.Name == "spherewright_export_blueprint").ToArray();
        Assert.Equal(2, tools.Length);
        Assert.Contains("storage2101", tools.Single(t => t.ProtocolTool.Name == "spherewright_inspect_blueprint")
            .ProtocolTool.Description, StringComparison.Ordinal);
        var playbook = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("storageConfiguration", playbook, StringComparison.Ordinal);
        Assert.Contains("never its cargo", playbook, StringComparison.Ordinal);
        Assert.Contains("inputEndpointFacingDot/outputEndpointFacingDot", playbook, StringComparison.Ordinal);
        Assert.Contains("TooSkew", playbook, StringComparison.Ordinal);
        Assert.All(tools, t =>
        {
            Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.False(t.ProtocolTool.Annotations?.DestructiveHint);
            Assert.Contains("data", t.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task OptionalBlueprintSiteIsDiscoverableBoundedAndStillReadOnly()
    {
        var client = new FakeBridgeClient(SuccessResult());
        var site = new BlueprintSiteRequest { Position = new Vector3Snapshot { X = 100, Z = 170 },
            QuarterTurns = 1, ExpectedPlayerStateHash = "fresh-player" };
        var result = await SpherewrightTools.InspectBlueprintAsync(client, "session", 104, "explicit-code", site);
        Assert.False(result.IsError);
        Assert.Same(site, client.LastBlueprintRequest!.Site);
        var services = new ServiceCollection();
        services.AddSingleton<IBridgeClient>(client);
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = provider.GetServices<McpServerTool>().Single(t => t.ProtocolTool.Name == "spherewright_inspect_blueprint").ProtocolTool;
        Assert.True(tool.Annotations!.ReadOnlyHint);
        Assert.Contains("site", tool.InputSchema.ToString());
        Assert.Contains("at most32 NEW objects", tool.Description);
        Assert.Contains("executable=false", tool.Description);
        Assert.Contains("No covering", tool.Description);
    }

    [Fact]
    public async Task BlueprintExecutionAndCancellationExposeFreshFiniteAuthority()
    {
        var client = new FakeBridgeClient(SuccessResult());
        Assert.False((await SpherewrightTools.GetBlueprintBuildsAsync(client, "session", 104, "build")).IsError);
        Assert.Equal("build", client.LastBlueprintReadRequest!.BuildId);
        Assert.False((await SpherewrightTools.PrepareBlueprintBuildAsync(client, "session", 104, "progress", "player",
            resumeBuildId: "build", maximumObjectsToSubmit: 1)).IsError);
        Assert.Equal("build", client.LastBlueprintBuildRequest!.ResumeBuildId);
        Assert.Equal("progress", client.LastBlueprintBuildRequest.ExpectedStateHash);
        Assert.Equal(1, client.LastBlueprintBuildRequest.MaximumObjectsToSubmit);
        Assert.Null(client.LastBlueprintBuildRequest.BlueprintCode);
        Assert.Null(client.LastBlueprintBuildRequest.Site);
        Assert.Null(client.LastBlueprintBuildRequest.FoundryIntent);
        Assert.Null(client.LastBlueprintBuildRequest.ExpectedFoundryPlanHash);
        Assert.False((await SpherewrightTools.CommitBlueprintBuildAsync(client, "session", 104, "token", "key")).IsError);
        Assert.False((await SpherewrightTools.PrepareCancelBlueprintAsync(client, "session", 104, "build", "fresh")).IsError);
        Assert.Equal("fresh", client.LastCancelBlueprintRequest!.ExpectedStateHash);
        Assert.False((await SpherewrightTools.CommitCancelBlueprintAsync(client, "session", 104, "token", "key")).IsError);
        var services = new ServiceCollection(); services.AddSingleton<IBridgeClient>(client);
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool);
        Assert.Contains("resumeBuildId", tools["spherewright_prepare_blueprint_build"].InputSchema.ToString());
        Assert.Contains("one dependency-ready native prebuild", tools["spherewright_commit_blueprint_build"].Description);
        Assert.Contains("NOT replay", tools["spherewright_commit_blueprint_build"].Description);
        Assert.Contains("no automatic demolition", tools["spherewright_prepare_cancel_blueprint"].Description, StringComparison.OrdinalIgnoreCase);
        Assert.True(tools["spherewright_get_blueprint_builds"].Annotations!.ReadOnlyHint);
        Assert.False(tools["spherewright_commit_blueprint_build"].Annotations!.ReadOnlyHint);
    }

    [Fact]
    public async Task MoveSurfaceEvidenceIsPreservedWithoutChangingRequestOrCommitAuthority()
    {
        var bridge=new FakeBridgeClient(SuccessResult()) { MovePlan=new PreparedNormalAction
        { Prepared=true,CommitAllowedNow=true,PlanToken="move-plan",SurfacePreview=new MovementSurfacePreview
        { State="partial",ShoreRisk="detected",UnknownSampleCount=1,ShoreRiskSampleCount=1,CapturedAtGameTick=123 } } };
        var result=await SpherewrightTools.PrepareMoveAsync(bridge,"session",104,200,0,1,"fresh-player");
        var plan=result.StructuredContent!.Value.GetProperty("result");
        Assert.Equal("detected",plan.GetProperty("surfacePreview").GetProperty("shoreRisk").GetString());
        Assert.Equal("move-plan",plan.GetProperty("planToken").GetString());
        Assert.True(plan.GetProperty("commitAllowedNow").GetBoolean());
        Assert.Equal("fresh-player",bridge.LastMoveRequest!.ExpectedPlayerStateHash);
        Assert.False(bridge.LastMoveRequest.AllowPassiveDrift);
        Assert.Equal(200,bridge.LastMoveRequest.Target.X);
        Assert.Equal(1,bridge.LastMoveRequest.StateHashVersion);
    }

    [Fact]
    public void MoveSurfacePreviewIsDiscoverableAndNeverDescribedAsPathfinding()
    {
        var services=new ServiceCollection();services.AddSingleton<IBridgeClient>(new FakeBridgeClient(SuccessResult()));
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider=services.BuildServiceProvider();
        var description=provider.GetServices<McpServerTool>().Single(t=>t.ProtocolTool.Name=="spherewright_prepare_move").ProtocolTool.Description!;
        Assert.Contains("surfacePreview",description);
        Assert.Contains("at most66",description);
        Assert.Contains("not route clearance",description);
        Assert.Contains("does not change existing hash/commit admission",description);
        Assert.Contains("allowPassiveDrift (default false)", description);
        Assert.Contains("moveStateBinding", description);
        var guide=AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("prepare_move.surfacePreview",guide);
        Assert.Contains("absent evidence is unknown, never dry ground",guide);
        Assert.Contains("drop the recovery intention",guide);
    }

    [Fact]
    public async Task MovePassiveDriftIsExplicitAndPreservesDisclosedBinding()
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { MovePlan = new PreparedNormalAction
        {
            Prepared = true, MoveStateBinding = new() { InspectionStateHash = "server-inspected",
                MaximumDisplacementMetres = 0.05, MaximumAgeSeconds = 2 }
        } };
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "session", 104, 200, 0, 1,
            "server-inspected", allowPassiveDrift: true);
        Assert.True(bridge.LastMoveRequest!.AllowPassiveDrift);
        Assert.Equal("server-inspected", bridge.LastMoveRequest.ExpectedPlayerStateHash);
        var binding = result.StructuredContent!.Value.GetProperty("result").GetProperty("moveStateBinding");
        Assert.Equal("bounded_passive_drift", binding.GetProperty("mode").GetString());
        Assert.Equal(0.05, binding.GetProperty("maximumDisplacementMetres").GetDouble());
        Assert.Equal(2, binding.GetProperty("maximumAgeSeconds").GetDouble());
    }

    [Fact]
    public void PackagedPowerGuidanceSeparatesNativeSpacingAndDynamicCapacity()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("wind-to-wind separation is at least 10.5m", guide);
        Assert.Contains("Check all planned pairs", guide);
        Assert.Contains("does not authorize moving or rebuilding successful objects", guide);
        Assert.Contains("current-tick available generation, not immutable installed topology", guide);
        Assert.Contains("Retain and explain changes", guide);
        Assert.Contains("ignore shortages", guide);
    }

    [Fact]
    public void FuelPowerRatingsAreDiscoverableWithoutClaimingLiveSupplyOrNewWriteAuthority()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.GetBuildCatalogAsync))!;
        var description = ((System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(method,
            typeof(System.ComponentModel.DescriptionAttribute))!).Description;
        Assert.Contains("fuelPowerProfile", description);
        Assert.Contains("Null/missing is unknown, not zero", description);
        Assert.Contains("do not grant Foundry planned-generation credit", description);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("fuelEnergyUsePerTick * 3600 / fuelHeatValueJoules", guide);
        Assert.Contains("not live generation, fuel inventory or sustainable supply", guide);
        Assert.Contains("do not credit unbuilt/unfuelled generators", guide);
        Assert.Contains("never substitute a remembered generator rating", guide);
    }

    private static BeltEndpointPreviewRequest EndpointPreviewRequest() => new()
    {
        SorterItemId = 2011,
        FilterItemId = 1109,
        Source = new PlannedBeltEndpointBinding
        {
            ExistingObjectId = 441,
            ExpectedEndpointStateHash = "endpoint-441",
            ExistingSlot = 3,
            ExistingBeltQuarterTurns = 0,
            PlannedBeltQuarterTurns = 2,
        },
        Destination = new PlannedBeltEndpointBinding
        {
            ExistingObjectId = 884,
            ExpectedEndpointStateHash = "endpoint-884",
            ExistingSlot = -1,
            ExistingBeltQuarterTurns = 0,
            PlannedBeltQuarterTurns = 1,
        },
    };

    private static PreparedNormalAction EndpointPreviewPlan(BeltEndpointPreviewRequest request, bool nativePassed = true)
    {
        const int beltCount = 4;
        var attachments = new List<PlannedBeltEndpointAttachment>();
        Add(request.Source, "source", 0);
        Add(request.Destination, "destination", beltCount - 1);
        var result = new PreparedNormalAction
        {
            Prepared = false,
            ActionKind = NormalActionKinds.Build,
            PlanToken = string.Empty,
            ExpectedStateHash = "player",
            BuildKind = "belt_endpoint_preview",
            BeltEndpointPreview = new BeltEndpointPreviewSnapshot
            {
                Phase = "read_only_belt_endpoint_preview",
                SessionId = "owned-session",
                PlanetId = 104,
                Revision = 27,
                CapturedAtGameTick = 8123,
                NativeCheckPerformed = true,
                NativeCheckPassed = nativePassed,
                AssessmentHash = "assessment-hash",
                Attachments = attachments,
                Blockers = nativePassed ? new() : new() { "native_overlap" },
            },
            PlannedBeltPath = new BeltPathPlanSnapshot
            {
                NativeValidationMode = "full_path_stage1",
                SourceBindingMode = "none",
                DestinationBindingMode = "none",
                NewObjectCount = beltCount,
                RoutingMode = BeltPathModes.NativeGrid,
            },
        };
        result.PlannedPath.AddRange(Enumerable.Range(0, beltCount).Select(EndpointPathPoint));
        result.ItemBudget.Add(new ActionItemBudget
        {
            ItemId = 2001,
            Count = beltCount,
            Direction = "preview-construction-consumption",
        });
        result.ItemBudget.Add(new ActionItemBudget
        {
            ItemId = request.SorterItemId,
            Count = attachments.Count,
            Direction = "preview-construction-consumption",
        });
        return result;

        void Add(PlannedBeltEndpointBinding? binding, string role, int beltIndex)
        {
            if (binding is null) return;
            var source = role == "source";
            attachments.Add(new PlannedBeltEndpointAttachment
            {
                Role = role,
                ExistingObjectId = binding.ExistingObjectId,
                EndpointStateHash = binding.ExpectedEndpointStateHash,
                ExistingItemId = binding.ExistingSlot == -1 ? 2001 : 2101,
                ObservedRecipeId = 58,
                PlannedBeltIndex = beltIndex,
                SorterItemId = request.SorterItemId,
                FilterItemId = request.FilterItemId,
                ExistingBeltQuarterTurns = binding.ExistingBeltQuarterTurns,
                PlannedBeltQuarterTurns = binding.PlannedBeltQuarterTurns,
                NativeCondition = nativePassed ? "Ok" : "Collision",
                NativeSpan = nativePassed ? 2 : 0,
                Attachment = new InserterAttachmentPlanSnapshot
                {
                    Mode = "read_only_planned_belt_exact_slots",
                    SourceSlot = source ? binding.ExistingSlot : -1,
                    DestinationSlot = source ? -1 : binding.ExistingSlot,
                    SourcePosition = source ? EndpointSurfacePoint(2, 3) : EndpointPathPoint(beltIndex),
                    DestinationPosition = source ? EndpointPathPoint(beltIndex) : EndpointSurfacePoint(3, 4),
                },
            });
        }
    }

    private static Vector3Snapshot EndpointPathPoint(int index) => index switch
    {
        0 => EndpointSurfacePoint(1, 2),
        3 => EndpointSurfacePoint(11, 2),
        _ => EndpointSurfacePoint(1 + index * 3, 2 + index),
    };

    private static Vector3Snapshot EndpointSurfacePoint(float x, float z) => new()
    {
        X = x,
        Y = (float)Math.Sqrt(200 * 200 - x * x - z * z),
        Z = z,
    };

    private sealed class FakeBridgeClient : IBridgeClient
    {
        public PrepareBlueprintBuildRequest? LastBlueprintBuildRequest { get; private set; }
        public PrepareCancelBlueprintRequest? LastCancelBlueprintRequest { get; private set; }
        public BlueprintBuildRequest? LastBlueprintReadRequest { get; private set; }
        public Task<BridgeCallResult<BlueprintBuildList>> GetBlueprintBuildsAsync(string sessionId, BlueprintBuildRequest request, CancellationToken cancellationToken)
        {
            LastBlueprintReadRequest = request;
            return Task.FromResult(BridgeCallResult<BlueprintBuildList>.Succeeded(new BlueprintBuildList()));
        }
        public Task<BridgeCallResult<PreparedNormalAction>> PrepareBlueprintBuildAsync(string sessionId, PrepareBlueprintBuildRequest request, CancellationToken cancellationToken)
        { LastBlueprintBuildRequest = request; return Prepared(sessionId, NormalActionKinds.BlueprintBuild); }
        public Task<BridgeCallResult<NormalActionCommitResult>> CommitBlueprintBuildAsync(string sessionId, CommitNormalActionRequest request, CancellationToken cancellationToken) =>
            Committed(sessionId, request, NormalActionKinds.BlueprintBuild);
        public Task<BridgeCallResult<PreparedNormalAction>> PrepareCancelBlueprintAsync(string sessionId, PrepareCancelBlueprintRequest request, CancellationToken cancellationToken)
        { LastCancelBlueprintRequest = request; return Prepared(sessionId, NormalActionKinds.CancelBlueprintBuild); }
        public Task<BridgeCallResult<NormalActionCommitResult>> CommitCancelBlueprintAsync(string sessionId, CommitNormalActionRequest request, CancellationToken cancellationToken) =>
            Committed(sessionId, request, NormalActionKinds.CancelBlueprintBuild);
        public InspectBlueprintRequest? LastBlueprintRequest { get; private set; }
        public ExportBlueprintRequest? LastExportRequest { get; private set; }
        public Task<BridgeCallResult<BlueprintInspection>> InspectBlueprintAsync(string sessionId,
            InspectBlueprintRequest request, CancellationToken cancellationToken)
        {
            LastBlueprintRequest = request;
            return Task.FromResult(BridgeCallResult<BlueprintInspection>.Succeeded(new BlueprintInspection { SessionId = sessionId }));
        }
        public Task<BridgeCallResult<BlueprintInspection>> ExportBlueprintAsync(string sessionId,
            ExportBlueprintRequest request, CancellationToken cancellationToken)
        {
            LastExportRequest = request;
            return Task.FromResult(BridgeCallResult<BlueprintInspection>.Succeeded(new BlueprintInspection { SessionId = sessionId }));
        }
        public PrepareUpgradeRequest? LastUpgradeRequest { get; private set; }
        public Task<BridgeCallResult<PreparedNormalAction>> PrepareUpgradeAsync(string sessionId,
            PrepareUpgradeRequest request, CancellationToken cancellationToken)
        {
            LastUpgradeRequest = request;
            return Prepared(sessionId, NormalActionKinds.Upgrade);
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitUpgradeAsync(string sessionId,
            CommitNormalActionRequest request, CancellationToken cancellationToken) =>
            Committed(sessionId, request, NormalActionKinds.Upgrade);
        private readonly BridgeCallResult<BridgeStatus> _result;

        public FakeBridgeClient(BridgeCallResult<BridgeStatus> result)
        {
            _result = result;
        }

        public BridgeCallResult<ListAssemblersResult>? ListResult { get; set; }

        public ActionResultSnapshot? ActionResult { get; set; }
        public PreparedNormalAction? MovePlan { get; set; }
        public PrepareMoveRequest? LastMoveRequest { get; private set; }
        public SessionState? SessionSnapshot { get; set; }
        public PrepareOwnedWorldResumeRequest? LastResumePrepareRequest { get; private set; }
        public PreparedOwnedWorldResumePlan? ResumePlan { get; set; }
        public CommitOwnedWorldResumeRequest? LastResumeCommitRequest { get; private set; }

        public string? LastSessionId { get; private set; }

        public ListAssemblersRequest? LastListRequest { get; private set; }

        public ListResourceNodesRequest? LastResourceListRequest { get; private set; }

        public PrepareConfigureBuildingRequest? LastConfigureRequest { get; private set; }
        public PreparedNormalAction? ConfigurePlan { get; set; }
        public bool OmitStorageEcho { get; set; }

        public PrepareBuildRequest? LastBuildRequest { get; private set; }
        public BridgeError? BuildPrepareError { get; set; }
        public PreparedNormalAction? BeltEndpointPreviewPlan { get; set; }
        public int BuildCommitCalls { get; private set; }
        public bool OmitBuildFilterEcho { get; set; }
        public bool OmitBuildBeltEcho { get; set; }
        public string BuildBeltMode { get; set; } = "full_path_stage1";
        public bool BuildBeltSourceCover { get; set; }
        public bool BuildBeltDestinationCover { get; set; }
        public string? BuildBeltDestinationPreservationMode { get; set; }
        public string? BuildBeltPreservationMode { get; set; }
        public string? BuildBeltRoutingMode { get; set; }
        public int? BuildBeltNewObjectCount { get; set; }
        public int? BuildBeltStartAltitudeLevel { get; set; }
        public int? BuildBeltEndAltitudeLevel { get; set; }
        public int? BuildFilterEcho { get; set; }

        public PrepareDismantleRequest? LastDismantleRequest { get; private set; }

        public PrepareLogisticsStationFleetTransferRequest? LastFleetTransferRequest { get; private set; }

        public PrepareQuarantineReconciliationRequest? LastReconciliationRequest { get; private set; }

        public PrepareInterplanetaryFlightRequest? LastFlightRequest { get; private set; }

        public PrepareSelectResearchRequest? LastSelectResearchRequest { get; private set; }

        public PrepareFlightCheckpointReloadRequest? LastFlightCheckpointReloadRequest { get; private set; }

        public PrepareUserSaveImportRequest? LastImportPrepareRequest { get; private set; }

        public CommitUserSaveImportRequest? LastImportCommitRequest { get; private set; }

        public GetOverseerProductionRequest? LastOverseerProductionRequest { get; private set; }

        public GetOverseerSummaryRequest? LastOverseerSummaryRequest { get; private set; }

        public GetOverseerDiagnosticBundleRequest? LastOverseerDiagnosticBundleRequest { get; private set; }

        public OverseerDiagnosticBundleSnapshot? DiagnosticBundleSnapshot { get; set; }

        public Task<BridgeCallResult<BridgeStatus>> GetBridgeStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }

        public Task<BridgeCallResult<SessionState>> GetSessionStateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(BridgeCallResult<SessionState>.Succeeded(SessionSnapshot ?? new SessionState
            {
                BridgeConnected = true,
                GameVersion = "0.10.34.28529",
            }));
        }

        public Task<BridgeCallResult<PlayerStateSnapshot>> GetPlayerStateAsync(
            string sessionId,
            LocalPlanetRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<PlayerStateSnapshot>.Succeeded(new PlayerStateSnapshot
            {
                SessionId = sessionId,
                PlanetId = request.PlanetId,
            }));
        }

        public Task<BridgeCallResult<ProgressionStateSnapshot>> GetProgressionStateAsync(
            string sessionId,
            LocalPlanetRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<ProgressionStateSnapshot>.Succeeded(new ProgressionStateSnapshot
            {
                SessionId = sessionId,
                PlanetId = request.PlanetId,
            }));
        }

        public Task<BridgeCallResult<GameplayJournalSnapshot>> GetGameplayJournalAsync(
            string sessionId,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<GameplayJournalSnapshot>.Succeeded(new GameplayJournalSnapshot
            {
                SessionId = sessionId,
                JournalId = "journal",
            }));
        }

        public Task<BridgeCallResult<LocalStarSystemSnapshot>> GetLocalStarSystemAsync(
            string sessionId,
            LocalPlanetRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<LocalStarSystemSnapshot>.Succeeded(new LocalStarSystemSnapshot
            {
                SessionId = sessionId,
                LocalPlanetId = request.PlanetId,
            }));
        }

        public Task<BridgeCallResult<RecipeCatalogSnapshot>> GetRecipeCatalogAsync(
            string sessionId,
            LocalPlanetRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<RecipeCatalogSnapshot>.Succeeded(new RecipeCatalogSnapshot
            {
                SessionId = sessionId,
                PlanetId = request.PlanetId,
            }));
        }

        public Task<BridgeCallResult<ListResourceNodesResult>> ListResourceNodesAsync(
            string sessionId,
            ListResourceNodesRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastResourceListRequest = request;
            return Task.FromResult(BridgeCallResult<ListResourceNodesResult>.Succeeded(new ListResourceNodesResult
            {
                SessionId = sessionId,
                PlanetId = request.PlanetId,
            }));
        }

        public Task<BridgeCallResult<ResourceNodeSnapshot>> InspectResourceNodeAsync(
            string sessionId,
            InspectResourceNodeRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<ResourceNodeSnapshot>.Succeeded(new ResourceNodeSnapshot
            {
                SessionId = sessionId,
                PlanetId = request.PlanetId,
                Kind = request.Kind,
                NodeId = request.NodeId,
            }));
        }

        public Task<BridgeCallResult<ListFactoryEntitiesResult>> ListFactoryEntitiesAsync(
            string sessionId,
            ListFactoryEntitiesRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<ListFactoryEntitiesResult>.Succeeded(new ListFactoryEntitiesResult
            {
                SessionId = sessionId,
                PlanetId = request.PlanetId,
            }));
        }

        public Task<BridgeCallResult<FactoryEntitySnapshot>> InspectFactoryEntityAsync(
            string sessionId,
            InspectFactoryEntityRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<FactoryEntitySnapshot>.Succeeded(new FactoryEntitySnapshot
            {
                SessionId = sessionId,
                PlanetId = request.PlanetId,
                ObjectId = request.ObjectId,
            }));
        }

        public Task<BridgeCallResult<PowerSummarySnapshot>> GetPowerSummaryAsync(
            string sessionId,
            LocalPlanetRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<PowerSummarySnapshot>.Succeeded(new PowerSummarySnapshot
            {
                SessionId = sessionId,
                PlanetId = request.PlanetId,
            }));
        }

        public Task<BridgeCallResult<ActionResultSnapshot>> GetActionResultAsync(
            GetActionResultRequest request,
            CancellationToken cancellationToken)
        {
            var result = ActionResult ?? new ActionResultSnapshot
            {
                ActionId = request.ActionId,
                ActionKind = "new-game",
                State = "completed",
                Terminal = true,
                Succeeded = true,
            };
            return Task.FromResult(BridgeCallResult<ActionResultSnapshot>.Succeeded(result));
        }

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareMoveAsync(
            string sessionId,
            PrepareMoveRequest request,
            CancellationToken cancellationToken)
        {
            LastMoveRequest=request;
            return MovePlan is null ? Prepared(sessionId,NormalActionKinds.Move)
                : Task.FromResult(BridgeCallResult<PreparedNormalAction>.Succeeded(MovePlan));
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitMoveAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.Move);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareInterplanetaryFlightAsync(
            string sessionId,
            PrepareInterplanetaryFlightRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastFlightRequest = request;
            return Prepared(sessionId, NormalActionKinds.InterplanetaryFlight);
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitInterplanetaryFlightAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.InterplanetaryFlight);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareHarvestAsync(
            string sessionId,
            PrepareHarvestRequest request,
            CancellationToken cancellationToken) => Prepared(sessionId, NormalActionKinds.Harvest);

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitHarvestAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.Harvest);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareHandcraftAsync(
            string sessionId,
            PrepareHandcraftRequest request,
            CancellationToken cancellationToken) => Prepared(sessionId, NormalActionKinds.Handcraft);

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitHandcraftAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.Handcraft);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareSelectResearchAsync(
            string sessionId,
            PrepareSelectResearchRequest request,
            CancellationToken cancellationToken)
        {
            LastSelectResearchRequest = request;
            return Prepared(sessionId, NormalActionKinds.SelectResearch);
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitSelectResearchAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.SelectResearch);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareBuildAsync(
            string sessionId,
            PrepareBuildRequest request,
            CancellationToken cancellationToken)
        {
            LastBuildRequest = request;
            LastSessionId = sessionId;
            if (BuildPrepareError is not null)
                return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Failed(BuildPrepareError));
            if (request.BeltEndpointPreview is not null && BeltEndpointPreviewPlan is not null)
                return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Succeeded(BeltEndpointPreviewPlan));
            var prepared = new PreparedNormalAction
            {
                Prepared = true, ActionKind = NormalActionKinds.Build, PlanToken = "plan",
                PlannedSorterFilterItemId = OmitBuildFilterEcho ? null : BuildFilterEcho ?? request.InitialSorterFilterItemId,
            };
            if (request.BuildingItemId >= 2001 && request.BuildingItemId <= 2003)
            {
                prepared.BuildKind = "belt";
                prepared.SourceObjectId = request.SourceObjectId;
                prepared.DestinationObjectId = request.DestinationObjectId;
                var newObjectCount = BuildBeltNewObjectCount ?? 2;
                for (var i = 0; i < newObjectCount; i++)
                    prepared.PlannedPath.Add(new Vector3Snapshot { X = i, Y = 200 });
                prepared.ItemBudget.Add(new ActionItemBudget { ItemId = request.BuildingItemId, Count = newObjectCount, Direction = "construction-consumption" });
                if (!OmitBuildBeltEcho)
                    prepared.PlannedBeltPath = new BeltPathPlanSnapshot { NativeValidationMode = BuildBeltMode,
                        SourceBindingMode = BuildBeltSourceCover ? "non_removing_belt_cover" : request.SourceObjectId.HasValue ? "native_device_port" : "none",
                        ReusedSourceObjectId = BuildBeltSourceCover ? request.SourceObjectId : null,
                        SourcePreservationMode = BuildBeltPreservationMode,
                        DestinationBindingMode = BuildBeltDestinationCover ? "non_removing_belt_cover" : request.DestinationObjectId.HasValue ? "native_device_port" : "none",
                        ReusedDestinationObjectId = BuildBeltDestinationCover ? request.DestinationObjectId : null,
                        DestinationPreservationMode = BuildBeltDestinationPreservationMode,
                        NewObjectCount = newObjectCount, RoutingMode = BuildBeltRoutingMode,
                        StartAltitudeLevel = BuildBeltStartAltitudeLevel, EndAltitudeLevel = BuildBeltEndAltitudeLevel };
            }
            return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Succeeded(prepared));
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitBuildAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken)
        {
            BuildCommitCalls++;
            return Committed(sessionId, request, NormalActionKinds.Build);
        }

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareDismantleAsync(
            string sessionId,
            PrepareDismantleRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastDismantleRequest = request;
            return Prepared(sessionId, NormalActionKinds.Dismantle);
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitDismantleAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.Dismantle);

        public async Task<BridgeCallResult<PreparedNormalAction>> PrepareConfigureBuildingAsync(
            string sessionId,
            PrepareConfigureBuildingRequest request,
            CancellationToken cancellationToken)
        {
            LastConfigureRequest = request;
            var result = await Prepared(sessionId, NormalActionKinds.ConfigureBuilding);
            if (ConfigurePlan is not null)
                result = BridgeCallResult<PreparedNormalAction>.Succeeded(ConfigurePlan);
            if (request.Mode == BuildingConfigurationModes.StorageCapacity && !OmitStorageEcho && result.Value is not null)
            {
                result.Value.PlannedStorageOperation = request.StorageOperation;
                result.Value.PlannedStorageConfiguration = new StorageConfigurationSnapshot
                    { GridCount = 30, BannedGridCount = request.StorageBannedGridCount, Mode = "default", GridFilterItemIds = Enumerable.Repeat(0, 30).ToList() };
            }
            return result;
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitConfigureBuildingAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.ConfigureBuilding);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareTransferAsync(
            string sessionId,
            PrepareTransferRequest request,
            CancellationToken cancellationToken) => Prepared(sessionId, NormalActionKinds.Transfer);

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitTransferAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.Transfer);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareLogisticsStationFleetTransferAsync(
            string sessionId,
            PrepareLogisticsStationFleetTransferRequest request,
            CancellationToken cancellationToken)
        {
            LastFleetTransferRequest = request;
            return Prepared(sessionId, NormalActionKinds.LogisticsStationFleetTransfer);
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitLogisticsStationFleetTransferAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) =>
            Committed(sessionId, request, NormalActionKinds.LogisticsStationFleetTransfer);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareRefuelAsync(
            string sessionId,
            PrepareRefuelRequest request,
            CancellationToken cancellationToken) => Prepared(sessionId, NormalActionKinds.Refuel);

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitRefuelAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.Refuel);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareSaveAsync(
            string sessionId,
            PrepareSaveRequest request,
            CancellationToken cancellationToken) => Prepared(sessionId, NormalActionKinds.Save);

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitSaveAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.Save);

        public Task<BridgeCallResult<PreparedNormalAction>> PrepareQuarantineReconciliationAsync(
            string sessionId,
            PrepareQuarantineReconciliationRequest request,
            CancellationToken cancellationToken)
        {
            LastReconciliationRequest = request;
            return Prepared(sessionId, NormalActionKinds.ReconcileQuarantine);
        }

        public Task<BridgeCallResult<NormalActionCommitResult>> CommitQuarantineReconciliationAsync(
            string sessionId,
            CommitNormalActionRequest request,
            CancellationToken cancellationToken) => Committed(sessionId, request, NormalActionKinds.ReconcileQuarantine);

        private Task<BridgeCallResult<PreparedNormalAction>> Prepared(string sessionId, string actionKind)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction
            {
                Prepared = true,
                ActionKind = actionKind,
                PlanToken = "plan",
            }));
        }

        private Task<BridgeCallResult<NormalActionCommitResult>> Committed(
            string sessionId,
            CommitNormalActionRequest request,
            string actionKind)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<NormalActionCommitResult>.Succeeded(new NormalActionCommitResult
            {
                ActionId = "action",
                ActionKind = actionKind,
                IdempotencyKey = request.IdempotencyKey,
                Accepted = true,
            }));
        }

        public Task<BridgeCallResult<ListAssemblersResult>> ListAssemblersAsync(
            string sessionId,
            ListAssemblersRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastListRequest = request;
            return Task.FromResult(ListResult ?? BridgeCallResult<ListAssemblersResult>.Succeeded(new ListAssemblersResult()));
        }

        public Task<BridgeCallResult<AssemblerSnapshot>> InspectAssemblerAsync(
            string sessionId,
            InspectAssemblerRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(BridgeCallResult<AssemblerSnapshot>.Succeeded(new AssemblerSnapshot
            {
                EntityId = request.EntityId,
            }));
        }

        public Task<BridgeCallResult<FoundryPlanSnapshot>> GetFoundryPlanAsync(
            string sessionId, GetFoundryPlanRequest request, CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastFoundryRequest = request;
            return Task.FromResult(BridgeCallResult<FoundryPlanSnapshot>.Succeeded(new FoundryPlanSnapshot
            {
                SessionId = sessionId, PlanetId = request.PlanetId,
                TargetItemId = request.TargetItemId, TargetRatePerMinute = request.TargetRatePerMinute,
            }));
        }

        public GetFoundryPlanRequest? LastFoundryRequest { get; private set; }
        public GetGovernorPlanRequest? LastGovernorRequest { get; private set; }
        public int? GovernorResponseMeasurementGameTicks { get; set; }
        public Task<BridgeCallResult<GovernorPlanSnapshot>> GetGovernorPlanAsync(string sessionId, GetGovernorPlanRequest request, CancellationToken cancellationToken)
        {
            LastSessionId = sessionId; LastGovernorRequest = request;
            return Task.FromResult(BridgeCallResult<GovernorPlanSnapshot>.Succeeded(new GovernorPlanSnapshot
            { SessionId = sessionId, PlanetId = request.PlanetId, TargetItemId = request.TargetItemId, TargetRatePerMinute = request.TargetRatePerMinute,
                MeasurementGameTicks = GovernorResponseMeasurementGameTicks ?? request.MeasurementGameTicks }));
        }

        public Task<BridgeCallResult<BuildCatalog>> GetBuildCatalogAsync(
            string sessionId,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(BridgeCallResult<BuildCatalog>.Succeeded(new BuildCatalog
            {
                PlanetId = 1001,
            }));
        }

        public Task<BridgeCallResult<PreparedTestWorldPlan>> PrepareTestWorldAsync(
            PrepareTestWorldRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(BridgeCallResult<PreparedTestWorldPlan>.Succeeded(new PreparedTestWorldPlan
            {
                PlanToken = "plan",
                GalaxySeed = request.GalaxySeed,
                StarCount = request.StarCount,
            }));
        }

        public Task<BridgeCallResult<TestWorldCreationResult>> CommitTestWorldAsync(
            CommitTestWorldRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(BridgeCallResult<TestWorldCreationResult>.Succeeded(new TestWorldCreationResult
            {
                ActionId = "action",
                Accepted = true,
            }));
        }

        public Task<BridgeCallResult<PreparedUserSaveImportPlan>> PrepareUserSaveImportAsync(
            string sessionId,
            PrepareUserSaveImportRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastImportPrepareRequest = request;
            return Task.FromResult(BridgeCallResult<PreparedUserSaveImportPlan>.Succeeded(
                new PreparedUserSaveImportPlan
                {
                    Prepared = true,
                    PlanToken = "import-plan",
                    OriginalSavePreserved = true,
                    HistoricalCoverageComplete = false,
                    UserConfirmationRequired = true,
                    ConfirmationPrompt = "Confirm this import in the conversation.",
                }));
        }

        public Task<BridgeCallResult<UserSaveImportResult>> CommitUserSaveImportAsync(
            string sessionId,
            CommitUserSaveImportRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastImportCommitRequest = request;
            return Task.FromResult(BridgeCallResult<UserSaveImportResult>.Succeeded(
                new UserSaveImportResult
                {
                    ActionId = "import-action",
                    Accepted = true,
                    State = NormalActionStates.Completed,
                    SessionId = sessionId,
                    OriginalSavePreserved = true,
                }));
        }

        public Task<BridgeCallResult<OverseerProductionSnapshot>> GetOverseerProductionAsync(
            string sessionId,
            GetOverseerProductionRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastOverseerProductionRequest = request;
            return Task.FromResult(BridgeCallResult<OverseerProductionSnapshot>.Succeeded(
                new OverseerProductionSnapshot
                {
                    SessionId = sessionId,
                    RequestedItemIds = request.ItemIds.ToList(),
                }));
        }

        public Task<BridgeCallResult<OverseerSummarySnapshot>> GetOverseerSummaryAsync(
            string sessionId,
            GetOverseerSummaryRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastOverseerSummaryRequest = request;
            return Task.FromResult(BridgeCallResult<OverseerSummarySnapshot>.Succeeded(
                new OverseerSummarySnapshot
                {
                    SessionId = sessionId,
                }));
        }

        public Task<BridgeCallResult<OverseerDiagnosticBundleSnapshot>> GetOverseerDiagnosticBundleAsync(
            string sessionId,
            GetOverseerDiagnosticBundleRequest request,
            CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            LastOverseerDiagnosticBundleRequest = request;
            return Task.FromResult(BridgeCallResult<OverseerDiagnosticBundleSnapshot>.Succeeded(
                DiagnosticBundleSnapshot ?? new OverseerDiagnosticBundleSnapshot
                {
                    SessionId = sessionId,
                    RequestedItemIds = request.ItemIds.ToList(),
                }));
        }

        public Task<BridgeCallResult<PreparedOwnedWorldResumePlan>> PrepareOwnedWorldResumeAsync(
            PrepareOwnedWorldResumeRequest request,
            CancellationToken cancellationToken)
        {
            LastResumePrepareRequest = request;
            return Task.FromResult(BridgeCallResult<PreparedOwnedWorldResumePlan>.Succeeded(ResumePlan ?? new PreparedOwnedWorldResumePlan
            {
                Prepared = true,
                PlanToken = "resume-plan",
            }));
        }

        public Task<BridgeCallResult<OwnedWorldResumeResult>> CommitOwnedWorldResumeAsync(
            CommitOwnedWorldResumeRequest request,
            CancellationToken cancellationToken)
        {
            LastResumeCommitRequest = request;
            return Task.FromResult(BridgeCallResult<OwnedWorldResumeResult>.Succeeded(new OwnedWorldResumeResult
            {
                ActionId = "resume-action",
                Accepted = true,
                State = NormalActionStates.WaitingForGame,
            }));
        }

        public Task<BridgeCallResult<PreparedFlightCheckpointReloadPlan>> PrepareFlightCheckpointReloadAsync(
            PrepareFlightCheckpointReloadRequest request,
            CancellationToken cancellationToken)
        {
            LastFlightCheckpointReloadRequest = request;
            return Task.FromResult(BridgeCallResult<PreparedFlightCheckpointReloadPlan>.Succeeded(
                new PreparedFlightCheckpointReloadPlan
                {
                    Prepared = true,
                    PlanToken = "checkpoint-plan",
                    CheckpointId = "checkpoint-id",
                }));
        }

        public Task<BridgeCallResult<FlightCheckpointReloadResult>> CommitFlightCheckpointReloadAsync(
            CommitFlightCheckpointReloadRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(BridgeCallResult<FlightCheckpointReloadResult>.Succeeded(
                new FlightCheckpointReloadResult
                {
                    ActionId = "checkpoint-action",
                    CheckpointId = "checkpoint-id",
                    Accepted = true,
                    State = NormalActionStates.WaitingForGame,
                }));
        }
    }
}
