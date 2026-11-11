using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Celestial;
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
        CancellationToken cancellationToken = default)
    {
        if (layerId < 0 || layerId > 10 || stateHashVersion != 1)
            return ToToolResult(BridgeCallResult<DysonStateSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, "Dyson observations require layerId 0 through 10 and state-hash version 1.",
                false, "Use the current bounded observation schema.")), "Dyson observation rejected.");
        var result = await bridgeClient.GetDysonStateAsync(sessionId, new GetDysonStateRequest
        {
            PlanetId = planetId, LayerId = layerId, StateHashVersion = stateHashVersion,
        }, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Existing Dyson structures and ray-receiver observations captured.");
    }
}
