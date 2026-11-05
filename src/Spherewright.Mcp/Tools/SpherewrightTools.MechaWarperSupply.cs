using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Mcp.BridgeClient;

namespace Spherewright.Mcp.Tools;

public static partial class SpherewrightTools
{
    [McpServerTool(Name = "spherewright_prepare_supply_mecha_warpers", Title = "Prepare native mecha warper supply",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Prepares bounded native supply of real item1210 without moving stock. Default: healthy owned grounded idle player, empty native Filtered chamber1210/stack20, count=min(20,package count). Explicit uiTopUp=true: partially filled chamber, exact count=min(20-chamber count,package count), zero inc, native UI transfer; also supported for planetId0 only at the exact paused saved-space source under its sole opening quarantine and frozenSavedPoseVerified=true. Nonzero saved inertial velocity is valid only while the complete saved position/momentum cut stays frozen; synchronous readback must prove equal frozenSpacePoseBefore/After and spaceMomentumPreserved=true. No general write admission or game Resume. Requires fresh complete player stateHash and plannedMechaWarperUiTopUp=true echo for UI mode. Binds bilateral count/inc and unchanged autoReplenishWarper. Commit once and retain the original action and any unknown result without replay.")]
    public static async Task<CallToolResult> PrepareSupplyMechaWarpersAsync(
        IBridgeClient bridgeClient,
        [Description("Current owned session ID from spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID; 0 only for the exact paused saved-space UI role.")] int planetId,
        [Description("Fresh complete player stateHash from spherewright_get_player_state, not the narrower player action hash.")] string expectedPlayerStateHash,
        [Description("Exact native transfer count: min(20-current chamber count, package count); default mode requires an empty chamber. No smaller arbitrary batch.")] int count = 20,
        [Description("Request state-hash version; currently 1.")] int stateHashVersion = 1,
        CancellationToken cancellationToken = default,
        [Description("Explicit native UI partial-chamber top-up; exact count=min(20-current chamber,package). Default retains the empty-chamber AutoReplenishWarper path.")] bool uiTopUp = false)
    {
        var result = await bridgeClient.PrepareSupplyMechaWarpersAsync(sessionId, new PrepareSupplyMechaWarpersRequest
        {
            PlanetId = planetId,
            Count = count,
            ExpectedPlayerStateHash = expectedPlayerStateHash,
            StateHashVersion = stateHashVersion,
            UiTopUp = uiTopUp,
        }, cancellationToken).ConfigureAwait(false);
        if(uiTopUp && result.Success && result.Value?.PlannedMechaWarperUiTopUp!=true)
            result=BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not confirm the native UI partial-chamber transfer; no token is exposed.",false,
                "Use a matching cohort and fresh player inspection; never call the blind empty-chamber path on partial stock."));
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
