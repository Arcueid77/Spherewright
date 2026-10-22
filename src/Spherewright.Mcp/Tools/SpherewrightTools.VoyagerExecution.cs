using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Actions;
using Spherewright.Mcp.BridgeClient;

namespace Spherewright.Mcp.Tools;

public static partial class SpherewrightTools
{
    [McpServerTool(Name = "spherewright_prepare_voyager_flight", Title = "Prepare a bounded native Voyager flight",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Prepares one bounded Voyager leg from the exact cached preview routeStateHash and fresh complete player stateHash. Native-visible star markers with no visible destination planet remain valid selections; never invent hidden planets or resources. Revalidates native technology, energy, warpers, departure/arrival envelope and persistent return reserves. Empty journeyId starts one outbound journey; a return must name the original journeyId. This does not launch, enter warp or complete a round trip. Use commit_voyager_flight once; the ordinary same-star flight commit cannot consume a Voyager plan.")]
    public static async Task<CallToolResult> PrepareVoyagerFlightAsync(
        IBridgeClient bridgeClient,
        [Description("Current owned session ID.")] string sessionId,
        [Description("Current origin planet ID from that session.")] int planetId,
        [Description("Exact routeStateHash from the current inspected preview_voyager_route result.")] string expectedRouteStateHash,
        [Description("Fresh complete player stateHash from spherewright_get_player_state.")] string expectedPlayerStateHash,
        [Description("Request state-hash version; currently 1.")] int stateHashVersion = 1,
        [Description("Original protected journeyId for the return leg; null or empty starts the bounded outbound journey.")] string? journeyId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareVoyagerFlightAsync(sessionId, new PrepareVoyagerFlightRequest
        {
            PlanetId = planetId, ExpectedRouteStateHash = expectedRouteStateHash,
            ExpectedPlayerStateHash = expectedPlayerStateHash, StateHashVersion = stateHashVersion, JourneyId = journeyId,
        }, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Bounded Voyager leg prepared; inspect native blockers and the protected return reserve before its exact commit.");
    }

    [McpServerTool(Name = "spherewright_commit_voyager_flight", Title = "Commit a bounded native Voyager flight",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Commits only the prepared Voyager mode through the existing interplanetary-flight action coordinator. Native launch, cruise, warp entry/exit and approach use one flight writer, a finite deadline, one exact checkpoint and persistent return reserves. Acceptance is not arrival, landing or Voyager acceptance. Poll the original action and voyagerFlightReadback phase, actual star/planet IDs, energy/warpers, stop state and nativeExitObserved. Its action planetId stays the leg's origin. Unknown IDs or remaining distance remain nullable. On timeout/disconnect/outcome_unknown reconcile the original action/idempotency key, never rekey; checkpoint reload remains separately protected and may not roll back a newer primary.")]
    public static async Task<CallToolResult> CommitVoyagerFlightAsync(
        IBridgeClient bridgeClient, string sessionId, int planetId, string planToken, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.CommitVoyagerFlightAsync(sessionId,
            CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey), cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Voyager flight commit submitted; poll its original action and actual native phase readback.");
    }

    [McpServerTool(Name = "spherewright_prepare_cancel_voyager_flight", Title = "Prepare stopping the original Voyager flight",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Prepares a stop request bound to one original active Voyager flight action and its origin planetId, including during space flight without a local planet. This neither starts a second movement writer nor loads a checkpoint. The original flight controller must observe native warp exit and braking before any settled/recovery-complete claim. Alternatively, the disclosed returnOnlyRecovery.taskId can stop its idle remote/home task through normal Pause; this grants no resource, budget or automatic Resume.")]
    public static async Task<CallToolResult> PrepareCancelVoyagerFlightAsync(
        IBridgeClient bridgeClient,
        [Description("Owned session containing the original active Voyager flight.")] string sessionId,
        [Description("The original flight action's origin planetId, not an invented current space planet.")] int planetId,
        [Description("Exact original active Voyager actionId, or the disclosed taskId of the idle return-only task.")] string actionId,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareCancelVoyagerFlightAsync(sessionId,
            new PrepareCancelVoyagerFlightRequest { PlanetId = planetId, ActionId = actionId, StateHashVersion = stateHashVersion },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Stop request prepared for the original Voyager action; the flight is not yet settled.");
    }

    [McpServerTool(Name = "spherewright_commit_cancel_voyager_flight", Title = "Request stopping the original Voyager flight",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Commits the exact prepared cancel_voyager_flight through the shared normal-action coordinator. Latches stopRequested on the original flight; it creates no second movement writer and does not reload a save. Acknowledgement is not native exit, braking or recovery completion: continue polling the original flight's voyagerFlightReadback and terminal state. Reconcile unknown outcomes under their original action/idempotency key; never repeat the flight or switch to another checkpoint.")]
    public static async Task<CallToolResult> CommitCancelVoyagerFlightAsync(
        IBridgeClient bridgeClient, string sessionId, int planetId, string planToken, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.CommitCancelVoyagerFlightAsync(sessionId,
            CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey), cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Original Voyager flight stop requested; poll that flight until native exit, braking and its terminal result are proved.");
    }
}
