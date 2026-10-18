using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Diagnostics;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Power;
using Spherewright.Contracts.Progression;
using Spherewright.Contracts.Resources;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Testing;

namespace Spherewright.Mcp.BridgeClient;

public interface IBridgeClient
{
    Task<BridgeCallResult<GovernorPlanSnapshot>> GetGovernorPlanAsync(string sessionId, GetGovernorPlanRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<BlueprintBuildList>> GetBlueprintBuildsAsync(string sessionId, BlueprintBuildRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<PreparedNormalAction>> PrepareBlueprintBuildAsync(string sessionId, PrepareBlueprintBuildRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<NormalActionCommitResult>> CommitBlueprintBuildAsync(string sessionId, CommitNormalActionRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<PreparedNormalAction>> PrepareCancelBlueprintAsync(string sessionId, PrepareCancelBlueprintRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<NormalActionCommitResult>> CommitCancelBlueprintAsync(string sessionId, CommitNormalActionRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<BlueprintInspection>> InspectBlueprintAsync(
        string sessionId, InspectBlueprintRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<BlueprintInspection>> ExportBlueprintAsync(
        string sessionId, ExportBlueprintRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<PreparedNormalAction>> PrepareUpgradeAsync(
        string sessionId, PrepareUpgradeRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<NormalActionCommitResult>> CommitUpgradeAsync(
        string sessionId, CommitNormalActionRequest request, CancellationToken cancellationToken);
    Task<BridgeCallResult<FoundryPlanSnapshot>> GetFoundryPlanAsync(
        string sessionId, GetFoundryPlanRequest request, CancellationToken cancellationToken);

    Task<BridgeCallResult<BridgeStatus>> GetBridgeStatusAsync(CancellationToken cancellationToken);

    Task<BridgeCallResult<SessionState>> GetSessionStateAsync(CancellationToken cancellationToken);

    Task<BridgeCallResult<PlayerStateSnapshot>> GetPlayerStateAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<ProgressionStateSnapshot>> GetProgressionStateAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<GameplayJournalSnapshot>> GetGameplayJournalAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<LocalStarSystemSnapshot>> GetLocalStarSystemAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<VoyagerStarCatalogSnapshot>> GetVoyagerStarCatalogAsync(
        string sessionId,
        GetVoyagerStarCatalogRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<VoyagerRouteBudgetSnapshot>> PreviewVoyagerRouteAsync(
        string sessionId,
        PreviewVoyagerRouteRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<RecipeCatalogSnapshot>> GetRecipeCatalogAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<ListResourceNodesResult>> ListResourceNodesAsync(
        string sessionId,
        ListResourceNodesRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<ResourceNodeSnapshot>> InspectResourceNodeAsync(
        string sessionId,
        InspectResourceNodeRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<ListFactoryEntitiesResult>> ListFactoryEntitiesAsync(
        string sessionId,
        ListFactoryEntitiesRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<FactoryEntitySnapshot>> InspectFactoryEntityAsync(
        string sessionId,
        InspectFactoryEntityRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PowerSummarySnapshot>> GetPowerSummaryAsync(
        string sessionId,
        LocalPlanetRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<OverseerProductionSnapshot>> GetOverseerProductionAsync(
        string sessionId,
        GetOverseerProductionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<OverseerSummarySnapshot>> GetOverseerSummaryAsync(
        string sessionId,
        GetOverseerSummaryRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<OverseerDiagnosticBundleSnapshot>> GetOverseerDiagnosticBundleAsync(
        string sessionId,
        GetOverseerDiagnosticBundleRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<ActionResultSnapshot>> GetActionResultAsync(
        GetActionResultRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareMoveAsync(
        string sessionId,
        PrepareMoveRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitMoveAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareInterplanetaryFlightAsync(
        string sessionId,
        PrepareInterplanetaryFlightRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitInterplanetaryFlightAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareHarvestAsync(
        string sessionId,
        PrepareHarvestRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitHarvestAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareHandcraftAsync(
        string sessionId,
        PrepareHandcraftRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitHandcraftAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareSelectResearchAsync(
        string sessionId,
        PrepareSelectResearchRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitSelectResearchAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareBuildAsync(
        string sessionId,
        PrepareBuildRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitBuildAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareDismantleAsync(
        string sessionId,
        PrepareDismantleRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitDismantleAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareConfigureBuildingAsync(
        string sessionId,
        PrepareConfigureBuildingRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitConfigureBuildingAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareTransferAsync(
        string sessionId,
        PrepareTransferRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitTransferAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareLogisticsStationFleetTransferAsync(
        string sessionId,
        PrepareLogisticsStationFleetTransferRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitLogisticsStationFleetTransferAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareRefuelAsync(
        string sessionId,
        PrepareRefuelRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitRefuelAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareSaveAsync(
        string sessionId,
        PrepareSaveRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitSaveAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedNormalAction>> PrepareQuarantineReconciliationAsync(
        string sessionId,
        PrepareQuarantineReconciliationRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<NormalActionCommitResult>> CommitQuarantineReconciliationAsync(
        string sessionId,
        CommitNormalActionRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<ListAssemblersResult>> ListAssemblersAsync(
        string sessionId,
        ListAssemblersRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<AssemblerSnapshot>> InspectAssemblerAsync(
        string sessionId,
        InspectAssemblerRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<BuildCatalog>> GetBuildCatalogAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedTestWorldPlan>> PrepareTestWorldAsync(
        PrepareTestWorldRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<TestWorldCreationResult>> CommitTestWorldAsync(
        CommitTestWorldRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedUserSaveImportPlan>> PrepareUserSaveImportAsync(
        string sessionId,
        PrepareUserSaveImportRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<UserSaveImportResult>> CommitUserSaveImportAsync(
        string sessionId,
        CommitUserSaveImportRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedOwnedWorldResumePlan>> PrepareOwnedWorldResumeAsync(
        PrepareOwnedWorldResumeRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<OwnedWorldResumeResult>> CommitOwnedWorldResumeAsync(
        CommitOwnedWorldResumeRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<PreparedFlightCheckpointReloadPlan>> PrepareFlightCheckpointReloadAsync(
        PrepareFlightCheckpointReloadRequest request,
        CancellationToken cancellationToken);

    Task<BridgeCallResult<FlightCheckpointReloadResult>> CommitFlightCheckpointReloadAsync(
        CommitFlightCheckpointReloadRequest request,
        CancellationToken cancellationToken);
}
