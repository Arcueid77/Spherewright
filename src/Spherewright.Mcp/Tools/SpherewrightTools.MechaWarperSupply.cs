using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Actions;
using Spherewright.Mcp.BridgeClient;

namespace Spherewright.Mcp.Tools;

public static partial class SpherewrightTools
{
    [McpServerTool(Name = "spherewright_prepare_supply_mecha_warpers", Title = "Prepare native mecha warper supply",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Prepares a bounded native supply of real item1210 from the player package into the empty mecha warper chamber, without moving stock. Requires the fresh complete player stateHash, a healthy owned grounded idle player and exactly one native Filtered slot with filter1210, stack20 and zero count/inc. count must equal min(20, the fresh package count of1210), above zero. Binds package/chamber count/inc and the current automatic replenishment setting; preparation never changes autoReplenishWarper or spends a warper. Commit once using commit_supply_mecha_warpers and retain the original action on any unknown result.")]
    public static async Task<CallToolResult> PrepareSupplyMechaWarpersAsync(
        IBridgeClient bridgeClient,
        [Description("Current owned session ID from spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID in that same owned session.")] int planetId,
        [Description("Fresh complete player stateHash from spherewright_get_player_state, not the narrower player action hash.")] string expectedPlayerStateHash,
        [Description("Exact native transfer count: min(20, the fresh player-package count of item1210), above zero. Does not choose a smaller arbitrary batch.")] int count = 20,
        [Description("Request state-hash version; currently 1.")] int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareSupplyMechaWarpersAsync(sessionId, new PrepareSupplyMechaWarpersRequest
        {
            PlanetId = planetId,
            Count = count,
            ExpectedPlayerStateHash = expectedPlayerStateHash,
            StateHashVersion = stateHashVersion,
        }, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Native empty-chamber warper-supply plan prepared; stock and automatic replenishment are unchanged.");
    }

    [McpServerTool(Name = "spherewright_commit_supply_mecha_warpers", Title = "Commit native mecha warper supply",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Commits the exact prepared native empty-chamber supply through the shared normal-action coordinator and one Mecha.AutoReplenishWarper call. Moves real item1210 with immediate bilateral count/inc readback; never edits autoReplenishWarper or calls UseWarper. Acceptance is not completion: poll spherewright_get_action_result for the original action and mechaWarperSupplyReadback. Nullable after fields, actualStored and nativeReturnedSuccess remain unknown until proved; readbackComplete and conservationVerified describe the synchronous native-call boundary. On timeout, disconnect or outcome_unknown retain the original action/idempotency key, never rekey or repeat the native call.")]
    public static async Task<CallToolResult> CommitSupplyMechaWarpersAsync(
        IBridgeClient bridgeClient,
        [Description("The same owned session bound by the supply preparation.")] string sessionId,
        [Description("The same current local planet bound by the supply preparation.")] int planetId,
        [Description("Exact fresh planToken returned by prepare_supply_mecha_warpers.")] string planToken,
        [Description("One UUID for this exact commit; retain it with the original action and never replace it after an unknown result.")] string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitSupplyMechaWarpersAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Native warper-supply commit submitted; poll the original action for completion and nullable conservation readback.");
    }
}
