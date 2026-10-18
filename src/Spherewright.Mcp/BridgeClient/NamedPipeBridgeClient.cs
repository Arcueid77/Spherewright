using System.IO.Pipes;
using System.Security.Principal;
using Newtonsoft.Json;
using Spherewright.Bridge.Core.Framing;
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
using Spherewright.Contracts.Versioning;

namespace Spherewright.Mcp.BridgeClient;

internal sealed class NamedPipeBridgeClient : IBridgeClient
{
    private const string ClientVersion = SpherewrightProduct.CurrentVersion;
    private readonly BridgeClientOptions _options;
    private readonly BridgeDescriptorLocator _locator;
    private readonly FrameCodec _frameCodec = new FrameCodec(ProtocolConstants.DefaultMaxFrameBytes);

    public NamedPipeBridgeClient(BridgeClientOptions options, BridgeDescriptorLocator locator)
    {
        _options = options;
        _locator = locator;
    }

    public async Task<BridgeCallResult<BridgeStatus>> GetBridgeStatusAsync(CancellationToken cancellationToken)
    {
        return await CallAsync<EmptyPayload, BridgeStatus>(
            BridgeMethods.GetBridgeStatus,
            null,
            new EmptyPayload(),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<BridgeCallResult<BlueprintInspection>> InspectBlueprintAsync(
        string sessionId, InspectBlueprintRequest request, CancellationToken cancellationToken) =>
        CallAsync<InspectBlueprintRequest, BlueprintInspection>(BridgeMethods.InspectBlueprint, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<BlueprintBuildList>> GetBlueprintBuildsAsync(string sessionId, BlueprintBuildRequest request, CancellationToken cancellationToken) =>
        CallAsync<BlueprintBuildRequest, BlueprintBuildList>(BridgeMethods.GetBlueprintBuilds, sessionId, request, cancellationToken);
    public Task<BridgeCallResult<PreparedNormalAction>> PrepareBlueprintBuildAsync(string sessionId, PrepareBlueprintBuildRequest request, CancellationToken cancellationToken) =>
        CallAsync<PrepareBlueprintBuildRequest, PreparedNormalAction>(BridgeMethods.PrepareBlueprintBuild, sessionId, request, cancellationToken);
    public Task<BridgeCallResult<NormalActionCommitResult>> CommitBlueprintBuildAsync(string sessionId, CommitNormalActionRequest request, CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitBlueprintBuild, sessionId, request, cancellationToken);
    public Task<BridgeCallResult<PreparedNormalAction>> PrepareCancelBlueprintAsync(string sessionId, PrepareCancelBlueprintRequest request, CancellationToken cancellationToken) =>
        CallAsync<PrepareCancelBlueprintRequest, PreparedNormalAction>(BridgeMethods.PrepareCancelBlueprint, sessionId, request, cancellationToken);
    public Task<BridgeCallResult<NormalActionCommitResult>> CommitCancelBlueprintAsync(string sessionId, CommitNormalActionRequest request, CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitCancelBlueprint, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<BlueprintInspection>> ExportBlueprintAsync(
        string sessionId, ExportBlueprintRequest request, CancellationToken cancellationToken) =>
        CallAsync<ExportBlueprintRequest, BlueprintInspection>(BridgeMethods.ExportBlueprint, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<GovernorPlanSnapshot>> GetGovernorPlanAsync(string sessionId, GetGovernorPlanRequest request, CancellationToken cancellationToken) =>
        CallAsync<GetGovernorPlanRequest, GovernorPlanSnapshot>(BridgeMethods.GetGovernorPlan, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<FoundryPlanSnapshot>> GetFoundryPlanAsync(
        string sessionId, GetFoundryPlanRequest request, CancellationToken cancellationToken) =>
        CallAsync<GetFoundryPlanRequest, FoundryPlanSnapshot>(BridgeMethods.GetFoundryPlan, sessionId, request, cancellationToken);

    public async Task<BridgeCallResult<SessionState>> GetSessionStateAsync(CancellationToken cancellationToken)
    {
        return await CallAsync<EmptyPayload, SessionState>(
            BridgeMethods.GetSessionState,
            null,
            new EmptyPayload(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<PlayerStateSnapshot>> GetPlayerStateAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<LocalPlanetRequest, PlayerStateSnapshot>(
            BridgeMethods.GetPlayerState,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<ProgressionStateSnapshot>> GetProgressionStateAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<LocalPlanetRequest, ProgressionStateSnapshot>(
            BridgeMethods.GetProgressionState,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<RecipeCatalogSnapshot>> GetRecipeCatalogAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<LocalPlanetRequest, RecipeCatalogSnapshot>(
            BridgeMethods.GetRecipeCatalog,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<ListResourceNodesResult>> ListResourceNodesAsync(
        string sessionId,
        ListResourceNodesRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<ListResourceNodesRequest, ListResourceNodesResult>(
            BridgeMethods.ListResourceNodes,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<ResourceNodeSnapshot>> InspectResourceNodeAsync(
        string sessionId,
        InspectResourceNodeRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<InspectResourceNodeRequest, ResourceNodeSnapshot>(
            BridgeMethods.InspectResourceNode,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<ListFactoryEntitiesResult>> ListFactoryEntitiesAsync(
        string sessionId,
        ListFactoryEntitiesRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<ListFactoryEntitiesRequest, ListFactoryEntitiesResult>(
            BridgeMethods.ListFactoryEntities,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<FactoryEntitySnapshot>> InspectFactoryEntityAsync(
        string sessionId,
        InspectFactoryEntityRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<InspectFactoryEntityRequest, FactoryEntitySnapshot>(
            BridgeMethods.InspectFactoryEntity,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<PowerSummarySnapshot>> GetPowerSummaryAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<LocalPlanetRequest, PowerSummarySnapshot>(
            BridgeMethods.GetPowerSummary,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<ActionResultSnapshot>> GetActionResultAsync(
        GetActionResultRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<GetActionResultRequest, ActionResultSnapshot>(
            BridgeMethods.GetActionResult,
            null,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareMoveAsync(
        string sessionId,
        PrepareMoveRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareMoveRequest, PreparedNormalAction>(BridgeMethods.PrepareMove, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitMoveAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitMove, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareInterplanetaryFlightAsync(
        string sessionId,
        PrepareInterplanetaryFlightRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareInterplanetaryFlightRequest, PreparedNormalAction>(
            BridgeMethods.PrepareInterplanetaryFlight,
            sessionId,
            request,
            cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitInterplanetaryFlightAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(
            BridgeMethods.CommitInterplanetaryFlight,
            sessionId,
            request,
            cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareHarvestAsync(
        string sessionId,
        PrepareHarvestRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareHarvestRequest, PreparedNormalAction>(BridgeMethods.PrepareHarvest, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitHarvestAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitHarvest, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareHandcraftAsync(
        string sessionId,
        PrepareHandcraftRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareHandcraftRequest, PreparedNormalAction>(BridgeMethods.PrepareHandcraft, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitHandcraftAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitHandcraft, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareSelectResearchAsync(
        string sessionId,
        PrepareSelectResearchRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareSelectResearchRequest, PreparedNormalAction>(BridgeMethods.PrepareSelectResearch, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitSelectResearchAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitSelectResearch, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareBuildAsync(
        string sessionId,
        PrepareBuildRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareBuildRequest, PreparedNormalAction>(BridgeMethods.PrepareBuild, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitBuildAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitBuild, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareUpgradeAsync(
        string sessionId, PrepareUpgradeRequest request, CancellationToken cancellationToken) =>
        CallAsync<PrepareUpgradeRequest, PreparedNormalAction>(BridgeMethods.PrepareUpgrade, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitUpgradeAsync(
        string sessionId, CommitNormalActionRequest request, CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitUpgrade, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareDismantleAsync(
        string sessionId,
        PrepareDismantleRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareDismantleRequest, PreparedNormalAction>(BridgeMethods.PrepareDismantle, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitDismantleAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitDismantle, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareConfigureBuildingAsync(
        string sessionId,
        PrepareConfigureBuildingRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareConfigureBuildingRequest, PreparedNormalAction>(BridgeMethods.PrepareConfigureBuilding, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitConfigureBuildingAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitConfigureBuilding, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareTransferAsync(
        string sessionId,
        PrepareTransferRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareTransferRequest, PreparedNormalAction>(BridgeMethods.PrepareTransfer, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitTransferAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitTransfer, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareLogisticsStationFleetTransferAsync(
        string sessionId,
        PrepareLogisticsStationFleetTransferRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareLogisticsStationFleetTransferRequest, PreparedNormalAction>(
            BridgeMethods.PrepareLogisticsStationFleetTransfer,
            sessionId,
            request,
            cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitLogisticsStationFleetTransferAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(
            BridgeMethods.CommitLogisticsStationFleetTransfer,
            sessionId,
            request,
            cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareRefuelAsync(
        string sessionId,
        PrepareRefuelRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareRefuelRequest, PreparedNormalAction>(BridgeMethods.PrepareRefuel, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitRefuelAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitRefuel, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareSaveAsync(
        string sessionId,
        PrepareSaveRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareSaveRequest, PreparedNormalAction>(BridgeMethods.PrepareSave, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitSaveAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(BridgeMethods.CommitSave, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<PreparedNormalAction>> PrepareQuarantineReconciliationAsync(
        string sessionId,
        PrepareQuarantineReconciliationRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PrepareQuarantineReconciliationRequest, PreparedNormalAction>(
            BridgeMethods.PrepareQuarantineReconciliation,
            sessionId,
            request,
            cancellationToken);

    public Task<BridgeCallResult<NormalActionCommitResult>> CommitQuarantineReconciliationAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<CommitNormalActionRequest, NormalActionCommitResult>(
            BridgeMethods.CommitQuarantineReconciliation,
            sessionId,
            request,
            cancellationToken);

    public async Task<BridgeCallResult<ListAssemblersResult>> ListAssemblersAsync(
        string sessionId,
        ListAssemblersRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<ListAssemblersRequest, ListAssemblersResult>(
            BridgeMethods.ListAssemblers,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<AssemblerSnapshot>> InspectAssemblerAsync(
        string sessionId,
        InspectAssemblerRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<InspectAssemblerRequest, AssemblerSnapshot>(
            BridgeMethods.InspectAssembler,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<BuildCatalog>> GetBuildCatalogAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        return await CallAsync<EmptyPayload, BuildCatalog>(
            BridgeMethods.GetBuildCatalog,
            sessionId,
            new EmptyPayload(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<PreparedTestWorldPlan>> PrepareTestWorldAsync(
        PrepareTestWorldRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<PrepareTestWorldRequest, PreparedTestWorldPlan>(
            BridgeMethods.PrepareNewGame,
            null,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<TestWorldCreationResult>> CommitTestWorldAsync(
        CommitTestWorldRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<CommitTestWorldRequest, TestWorldCreationResult>(
            BridgeMethods.CommitNewGame,
            null,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<PreparedUserSaveImportPlan>> PrepareUserSaveImportAsync(
        string sessionId,
        PrepareUserSaveImportRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<PrepareUserSaveImportRequest, PreparedUserSaveImportPlan>(
            BridgeMethods.PrepareImportCurrentGame,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<UserSaveImportResult>> CommitUserSaveImportAsync(
        string sessionId,
        CommitUserSaveImportRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<CommitUserSaveImportRequest, UserSaveImportResult>(
            BridgeMethods.CommitImportCurrentGame,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<OverseerProductionSnapshot>> GetOverseerProductionAsync(
        string sessionId,
        GetOverseerProductionRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<GetOverseerProductionRequest, OverseerProductionSnapshot>(
            BridgeMethods.GetOverseerProduction,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<OverseerSummarySnapshot>> GetOverseerSummaryAsync(
        string sessionId,
        GetOverseerSummaryRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<GetOverseerSummaryRequest, OverseerSummarySnapshot>(
            BridgeMethods.GetOverseerSummary,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<OverseerDiagnosticBundleSnapshot>> GetOverseerDiagnosticBundleAsync(
        string sessionId,
        GetOverseerDiagnosticBundleRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<GetOverseerDiagnosticBundleRequest, OverseerDiagnosticBundleSnapshot>(
            BridgeMethods.GetOverseerDiagnosticBundle,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<GameplayJournalSnapshot>> GetGameplayJournalAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        return await CallAsync<EmptyPayload, GameplayJournalSnapshot>(
            BridgeMethods.GetGameplayJournal,
            sessionId,
            new EmptyPayload(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<LocalStarSystemSnapshot>> GetLocalStarSystemAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<LocalPlanetRequest, LocalStarSystemSnapshot>(
            BridgeMethods.GetLocalStarSystem,
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<BridgeCallResult<VoyagerStarCatalogSnapshot>> GetVoyagerStarCatalogAsync(
        string sessionId,
        GetVoyagerStarCatalogRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<GetVoyagerStarCatalogRequest, VoyagerStarCatalogSnapshot>(
            BridgeMethods.GetVoyagerStarCatalog, sessionId, request, cancellationToken);

    public Task<BridgeCallResult<VoyagerRouteBudgetSnapshot>> PreviewVoyagerRouteAsync(
        string sessionId,
        PreviewVoyagerRouteRequest request,
        CancellationToken cancellationToken) =>
        CallAsync<PreviewVoyagerRouteRequest, VoyagerRouteBudgetSnapshot>(
            BridgeMethods.PreviewVoyagerRoute, sessionId, request, cancellationToken);

    public async Task<BridgeCallResult<PreparedOwnedWorldResumePlan>> PrepareOwnedWorldResumeAsync(
        PrepareOwnedWorldResumeRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<PrepareOwnedWorldResumeRequest, PreparedOwnedWorldResumePlan>(
            BridgeMethods.PrepareResumeOwnedGame,
            null,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<OwnedWorldResumeResult>> CommitOwnedWorldResumeAsync(
        CommitOwnedWorldResumeRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<CommitOwnedWorldResumeRequest, OwnedWorldResumeResult>(
            BridgeMethods.CommitResumeOwnedGame,
            null,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<PreparedFlightCheckpointReloadPlan>> PrepareFlightCheckpointReloadAsync(
        PrepareFlightCheckpointReloadRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<PrepareFlightCheckpointReloadRequest, PreparedFlightCheckpointReloadPlan>(
            BridgeMethods.PrepareReloadFlightCheckpoint,
            null,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeCallResult<FlightCheckpointReloadResult>> CommitFlightCheckpointReloadAsync(
        CommitFlightCheckpointReloadRequest request,
        CancellationToken cancellationToken)
    {
        return await CallAsync<CommitFlightCheckpointReloadRequest, FlightCheckpointReloadResult>(
            BridgeMethods.CommitReloadFlightCheckpoint,
            null,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<BridgeCallResult<TResponse>> CallAsync<TRequest, TResponse>(
        string method,
        string? sessionId,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        var located = _locator.Locate();
        if (!located.Success)
        {
            return BridgeCallResult<TResponse>.Failed(located.Error!);
        }

        var descriptor = located.Value!.Descriptor;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                descriptor.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(_options.ConnectTimeoutMilliseconds, timeout.Token).ConfigureAwait(false);

            var handshake = new BridgeRequestEnvelope<HandshakeRequest>
            {
                MessageType = BridgeMessageTypes.Handshake,
                RequestId = Guid.NewGuid().ToString("D"),
                Payload = new HandshakeRequest
                {
                    BridgeInstanceId = descriptor.BridgeInstanceId,
                    AuthToken = descriptor.AuthToken,
                    ClientName = "Spherewright.Mcp",
                    ClientVersion = ClientVersion,
                },
            };
            var handshakeResponse = await SendAsync<HandshakeRequest, HandshakeResponse>(
                pipe,
                handshake,
                timeout.Token).ConfigureAwait(false);
            if (!handshakeResponse.Success || handshakeResponse.Result?.Accepted != true)
            {
                return BridgeCallResult<TResponse>.Failed(handshakeResponse.Error ?? BridgeError.Create(
                    BridgeErrorCodes.AuthFailed,
                    "The active Spherewright bridge rejected authentication.",
                    true,
                    "Rediscover the active bridge descriptor and retry."));
            }

            var request = new BridgeRequestEnvelope<TRequest>
            {
                RequestId = Guid.NewGuid().ToString("D"),
                SessionId = sessionId,
                Method = method,
                Payload = payload,
            };
            var response = await SendAsync<TRequest, TResponse>(pipe, request, timeout.Token).ConfigureAwait(false);
            return response.Success && response.Result is not null
                ? BridgeCallResult<TResponse>.Succeeded(response.Result)
                : BridgeCallResult<TResponse>.Failed(response.Error ?? Internal("Bridge response was incomplete."));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return BridgeCallResult<TResponse>.Failed(BridgeError.Create(
                BridgeErrorCodes.RequestTimeout,
                "The read-only bridge request timed out.",
                true,
                "Ensure DSP and the Spherewright Plugin are responsive, then retry."));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is FrameProtocolException
            || exception is JsonException)
        {
            return BridgeCallResult<TResponse>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The Spherewright bridge connection is unavailable or invalid.",
                true,
                "Confirm DSP is running with the Plugin loaded, then retry."));
        }
    }

    private async Task<BridgeResponseEnvelope<TResponse>> SendAsync<TRequest, TResponse>(
        Stream pipe,
        BridgeRequestEnvelope<TRequest> request,
        CancellationToken cancellationToken)
    {
        var bytes = FrameCodec.EncodeUtf8(McpBridgeJson.Serialize(request));
        await _frameCodec.WriteFrameAsync(pipe, bytes, cancellationToken).ConfigureAwait(false);
        var responseBytes = await _frameCodec.ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
        if (responseBytes is null)
        {
            throw new IOException("Bridge connection closed before a response was received.");
        }

        var response = McpBridgeJson.Deserialize<BridgeResponseEnvelope<TResponse>>(
            FrameCodec.DecodeUtf8(responseBytes));
        if (response is null || !string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
        {
            throw new FrameProtocolException("Bridge response did not match the request.");
        }

        return response;
    }

    private static BridgeError Internal(string message)
    {
        return BridgeError.Create(
            BridgeErrorCodes.InternalError,
            message,
            true,
            "Retry the request. If it repeats, inspect the local Spherewright logs.");
    }
}
