using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Errors;
using Spherewright.Mcp.BridgeClient;

namespace Spherewright.Mcp.Tools;

public static partial class SpherewrightTools
{
    [McpServerTool(Name = "spherewright_get_voyager_star_catalog", Title = "Observe native-visible Voyager destinations",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("READ-ONLY 0.5 Voyager catalog bound to the current owned session and origin planet. Applies native exploration visibility separately to stars, planets and resources; unknown or null is not zero or absent. A visible star does not reveal hidden planets or veins, and theme potential is not surveyed resource evidence. May request ordinary native scanning only for permitted views, using the normal star-map path; this can populate legitimate visible scan caches, while pending scans stay unknown. Never invokes arbitrary generators, creates a remote factory, saves, creates checkpoints, launches or warps. Returns tokenless evidence with prepared=false, executable=false and commitAllowedNow=false; discovery grants no cross-star commit or recovery authority. Use the returned catalog state hash with a fresh player state hash for preview_voyager_route.")]
    public static async Task<CallToolResult> GetVoyagerStarCatalogAsync(
        IBridgeClient bridgeClient,
        [Description("Current owned session ID from spherewright_get_session_state.")] string sessionId,
        [Description("Current origin planet ID from the same session.")] int planetId,
        [Description("Maximum stars in this bounded observation, from1 through32; not a distance limit and never broadens native exploration visibility.")] int limit = 16,
        [Description("Catalog state-hash version; currently 1.")] int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetVoyagerStarCatalogAsync(sessionId, new GetVoyagerStarCatalogRequest
        {
            PlanetId = planetId,
            Limit = limit,
            StateHashVersion = stateHashVersion,
        }, cancellationToken).ConfigureAwait(false);
        if (result.Success && result.Value is { } snapshot && (snapshot.Prepared || snapshot.Executable || snapshot.CommitAllowedNow))
        {
            return UnsafeVoyagerRead<VoyagerStarCatalogSnapshot>();
        }
        return ToToolResult(result, "Native-visible Voyager catalog captured; unknown resources remain unknown and no execution was enabled.");
    }

    [McpServerTool(Name = "spherewright_preview_voyager_route", Title = "Preview a finite Voyager round-trip budget",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("READ-ONLY, tokenless 0.5 Voyager route budget using fresh player/catalog hashes, native-visible destination, current native distance/technology/core/fuel/warper conditions and protected outbound/return reserves. Outbound maximumFlightGameTicks is the whole round-trip window, with returnReserveGameTicks reserved within it; a nonempty journeyId validates only the remaining homebound leg against the original journey reserve. Unknown is not zero cost or spare capacity. Even sufficient budgets return prepared=false, executable=false and commitAllowedNow=false: no planToken, launch, warp, fuel/warper use, save, checkpoint creation or recovery. Preview cannot prove safe landing or enable a cross-star commit. Existing flight recovery requires its exact eligible interrupted action/checkpoint and must never roll back progress covered by a newer primary save.")]
    public static async Task<CallToolResult> PreviewVoyagerRouteAsync(
        IBridgeClient bridgeClient,
        [Description("Current owned session ID from spherewright_get_session_state.")] string sessionId,
        [Description("Current origin planet ID from the same session.")] int planetId,
        [Description("Explicit native-visible destination planet ID, or 0 for a native-visible star marker selected with destinationStarId; never guess a hidden planet.")] int destinationPlanetId,
        [Description("Fresh complete player stateHash from spherewright_get_player_state; not the narrower player action hash domain.")] string expectedPlayerStateHash,
        [Description("Fresh stateHash from spherewright_get_voyager_star_catalog; not another hash domain.")] string expectedCatalogStateHash,
        [Description("Shared request state-hash version; currently 1.")] int stateHashVersion = 1,
        [Description("Maximum whole round-trip duration in game ticks, from7200 through108000; includes the reserved return window.")] long maximumFlightGameTicks = 36000,
        [Description("Game ticks reserved for return within maximumFlightGameTicks; at least3600, with at least3600 left for outbound travel. Not extra outbound time.")] long returnReserveGameTicks = 18000,
        [Description("Finite minimum departure core-energy ratio from0.8 through1.0; defaults to0.8.")] double minimumCoreEnergyRatio = 0.8d,
        [Description("Extra legitimate warper reserve from0 through18: outbound requires two native entries plus reserve; a homebound journey requires its one remaining entry plus the original reserve.")] int extraWarperReserve = 1,
        [Description("Explicit native-visible destination star ID; required when destinationPlanetId=0. A star marker does not reveal hidden planets or resources.")] int destinationStarId = 0,
        [Description("Finite maximum warp game ticks per leg, default1800; separate from the whole round-trip and return windows.")] long maximumWarpGameTicksPerLeg = 1800,
        [Description("Finite maximum arrival offset in metres, default3000000; an arrival envelope is not proof of landing.")] double maximumArrivalOffsetMeters = 3000000d,
        [Description("Original protected journeyId for a homebound return preview; null starts an outbound round-trip budget. A return validates only the remaining homebound leg and original reserve.")] string? journeyId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PreviewVoyagerRouteAsync(sessionId, new PreviewVoyagerRouteRequest
        {
            PlanetId = planetId,
            DestinationPlanetId = destinationPlanetId,
            DestinationStarId = destinationStarId,
            ExpectedPlayerStateHash = expectedPlayerStateHash,
            ExpectedCatalogStateHash = expectedCatalogStateHash,
            StateHashVersion = stateHashVersion,
            MaximumFlightGameTicks = maximumFlightGameTicks,
            ReturnReserveGameTicks = returnReserveGameTicks,
            MinimumCoreEnergyRatio = minimumCoreEnergyRatio,
            ExtraWarperReserve = extraWarperReserve,
            MaximumWarpGameTicksPerLeg = maximumWarpGameTicksPerLeg,
            MaximumArrivalOffsetMeters = maximumArrivalOffsetMeters,
            JourneyId = journeyId,
        }, cancellationToken).ConfigureAwait(false);
        if (result.Success && result.Value is { } snapshot && (snapshot.Prepared || snapshot.Executable || snapshot.CommitAllowedNow))
        {
            return UnsafeVoyagerRead<VoyagerRouteBudgetSnapshot>();
        }
        return ToToolResult(result, "Finite Voyager budget preview captured; read unknowns, blockers and protected return reserves before any later execution design.");
    }

    private static CallToolResult UnsafeVoyagerRead<T>() => ToToolResult(BridgeCallResult<T>.Failed(BridgeError.Create(
        BridgeErrorCodes.InternalError,
        "A read-only Voyager response incorrectly advertised execution authority.",
        false,
        "Do not launch or recover from this response. Verify the matching Plugin/MCP source cohort and inspect again.")), string.Empty);
}
