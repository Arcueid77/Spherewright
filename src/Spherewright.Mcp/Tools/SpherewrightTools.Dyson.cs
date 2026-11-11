using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Celestial;
using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Errors;
using Spherewright.Mcp.BridgeClient;

namespace Spherewright.Mcp.Tools;

public static partial class SpherewrightTools
{
    [McpServerTool(Name = "spherewright_get_dyson_state", Title = "Inspect local-star Dyson structures and receivers",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("READ-ONLY, tokenless Dyson observation for the current owned local star. Returns bounded existing layers, node/frame/shell topology and delivered/ordered structure/cell progress, matching rocket flights, Native sail counters and identity-bound ray receivers in already-created resident factories. Incomplete coverage keeps aggregate totals unknown. Ray-receiver generateCurrentTick measures actual recent electricity generation, while capacity and photon buffers have separate fields. Sustained-power acceptance uses a declared multi-tick observation interval and stable receiver identity. The reader captures existing Native objects on the game thread. executable=false and commitAllowedNow=false.")]
    public static async Task<CallToolResult> GetDysonStateAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Exact owned session ID from spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID from the owned session.")] int planetId,
        [Description("Use 0 for all existing layers or one exact Native layer ID 1 through 10.")] int layerId = 0,
        [Description("State-hash format version. Currently 1.")] int stateHashVersion = 1,
        CancellationToken cancellationToken = default,
        [Description("Optional READ-ONLY design cost preview for one selected layer1..10. Bind expectedDesignStateHash to its fresh designStateHash and provide1..64 ordered steps. Each localId is unique and negative. A later frame or shell may reference prior proposed nodes by their negative localId, or existing Native nodes by positive ID. Each step carries a node/frame/shell structure request for the selected layer. Native geometry is calculated on detached copies with no NewDyson call or token. Read geometryComplete and technologyAllowsNow independently. Complete geometry provides exact NEW rocket/sail demand even when stress research remains required. Predicted object IDs are observations. Use actual creation readbacks and fresh individual prepares during execution.")]
        DysonDesignPreviewRequest? designPreview = null)
    {
        if (layerId < 0 || layerId > 10 || stateHashVersion != 1)
            return ToToolResult(BridgeCallResult<DysonStateSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, "Dyson observations require layerId 0 through 10 and state-hash version 1.",
                false, "Use the current bounded observation schema.")), "Dyson observation rejected.");
        if (designPreview is not null && DysonDesignPreviewPolicy.ValidateRequest(layerId, designPreview) is { } invalid)
            return ToToolResult(BridgeCallResult<DysonStateSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, invalid, false,
                "Use one selected layer, its fresh design hash and bounded prior-node references.")), "Dyson design preview rejected.");
        var result = await bridgeClient.GetDysonStateAsync(sessionId, new GetDysonStateRequest
        {
            PlanetId = planetId, LayerId = layerId, StateHashVersion = stateHashVersion,
            DesignPreview = designPreview?.Copy(),
        }, cancellationToken).ConfigureAwait(false);
        if (designPreview is not null && result.Success && result.Value?.DesignPreview is null)
            result = BridgeCallResult<DysonStateSnapshot>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin returned no requested Native design preview.", false,
                "Use a coherent Plugin/MCP cohort with the bounded design-preview capability."));
        return ToToolResult(result, "Existing Dyson structures and ray-receiver observations captured.");
    }
}
