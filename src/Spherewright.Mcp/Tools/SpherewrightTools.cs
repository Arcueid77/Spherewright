using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Diagnostics;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Resources;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Testing;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Resources;

namespace Spherewright.Mcp.Tools;

[McpServerToolType]
public static partial class SpherewrightTools
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [McpServerTool(
        Name = "spherewright_get_status",
        Title = "Get Spherewright status",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns the authenticated local Spherewright bridge, plugin, protocol, game-version, and write-health status. Read the advertised opening and core-operation Agent playbook before the first gameplay action, including after new-world creation. This tool never reads save contents or changes the game.")]
    public static async Task<CallToolResult> GetStatusAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken)
    {
        var result = await bridgeClient.GetBridgeStatusAsync(cancellationToken).ConfigureAwait(false);
        var payload = new SpherewrightStatusToolResult
        {
            Success = result.Success,
            Status = result.Value,
            Error = result.Error,
            AgentPlaybookResourceUri = AgentPlaybookResources.OpeningMovementUri,
            RecommendedFirstStep = "Read the opening and core-operation Agent playbook MCP resource before the first gameplay action.",
        };
        var text = result.Success
            ? "Spherewright bridge is connected."
            : $"{result.Error!.Code}: {result.Error.Message} Recovery: {result.Error.Recovery}";

        return new CallToolResult
        {
            IsError = !result.Success,
            StructuredContent = JsonSerializer.SerializeToElement(payload, JsonOptions),
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = text },
            },
        };
    }

    [McpServerTool(
        Name = "spherewright_get_session_state",
        Title = "Get Spherewright session state",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns a privacy-gated game-session snapshot. Before the first gameplay action in a session, read MCP resource spherewright://agent/playbooks/opening-movement-v1. At the main menu, gameLoaded=false is expected before protected resume. restartResumeAvailable advertises a stored ticket, not unexpired authority or final native menu readiness: fresh prepare_resume_owned_game checks both. For an expired healthy primary use only the documented reauthorize_expired_primary disclosure and subsequent confirmation flow, never edit expiry. Do not wait for a loaded world before calling prepare. Save, planet, and factory metadata are returned only for an owned world.")]
    public static async Task<CallToolResult> GetSessionStateAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken)
    {
        var result = await bridgeClient.GetSessionStateAsync(cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Spherewright session state is available.");
    }

    [McpServerTool(
        Name = "spherewright_get_player_state",
        Title = "Get player state in the owned ordinary world",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns a main-thread snapshot of position, movement, mecha energy, inventory, handcraft queue, construction drones, autoManageResearchItems and mechaResearchItemBuffer (research points, whole items and remainder points). constructionDrones.working counts all alive non-idle drones, not unfinished buildings; a successful build terminal must not be replayed because this count is still positive. Use bounded read-only readiness checks before the next operation. Compare research buffers with retained inventory and fresh progression when reconciling native research material returns; a backpack increase alone is not a new transfer or production event. It refuses unowned sessions.")]
    public static async Task<CallToolResult> GetPlayerStateAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID returned by spherewright_get_session_state.")] int planetId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetPlayerStateAsync(
            sessionId,
            new LocalPlanetRequest { PlanetId = planetId },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Player snapshot captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_get_progression_state",
        Title = "Get technology progression in the owned ordinary world",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns current technology, research queue, hash progress, requirements, prerequisites and unlock states. completionItemRewards is bounded native completion metadata, not a delivery receipt: null/missing means unknown; an empty list means observed no rewards. Reconcile actual before/after inventory, capacity, unlockTick and action boundaries before attributing a gain. Never replay a completed action or count rewards as handcraft/production; read the Agent playbook for research inventory accounting.")]
    public static async Task<CallToolResult> GetProgressionStateAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID returned by spherewright_get_session_state.")] int planetId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetProgressionStateAsync(
            sessionId,
            new LocalPlanetRequest { PlanetId = planetId },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Technology progression captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_get_gameplay_journal",
        Title = "Get the per-save first-event gameplay journal",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns the protected journal for the current owned save: first manual and production-line output per item are tracked independently, as are first technology and upgrade selections, with wall-clock and in-save game times. For attached_existing_save with historicalCoverageComplete=false, an absent first-event entry is not proof that the item was never made: historical seeds suppress later repeats without backfilling timestamps. Verify durableThroughSequence and persistence health; prove a craft from its terminal and materials, not an expected Journal increment. Never replay a completed craft because its first-event entry is absent.")]
    public static async Task<CallToolResult> GetGameplayJournalAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetGameplayJournalAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Per-save first-event gameplay journal captured.");
    }

    [McpServerTool(
        Name = "spherewright_get_local_star_system",
        Title = "Get planets in the current star system",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns the current star's planets, live distances, non-generating theme-based potential resources, and stable planet identity. It does not generate or inspect unvisited factories.")]
    public static async Task<CallToolResult> GetLocalStarSystemAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetLocalStarSystemAsync(
            sessionId,
            new LocalPlanetRequest { PlanetId = planetId },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Current-star planet catalog captured without generating unvisited factories.");
    }

    [McpServerTool(
        Name = "spherewright_get_recipe_catalog",
        Title = "Get runtime items, recipes, and first-red-matrix dependencies",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns current LDB item and recipe identities, unlock state, inputs and outputs, plus a deterministic runtime dependency graph rooted at the first red matrix. Item fuelHeatValueJoules/fuelType and nullable acceptedAsMechaFuel use the same current native eligibility as normal refuel preparation: fuels are not limited to names containing fuel rod. Null is unknown. Item heat is not burn-rate, movement-range or automatic-supply proof; acquire a real stack and use fresh prepare_refuel/commit_refuel with the native exact transfer count.")]
    public static async Task<CallToolResult> GetRecipeCatalogAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID returned by spherewright_get_session_state.")] int planetId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetRecipeCatalogAsync(
            sessionId,
            new LocalPlanetRequest { PlanetId = planetId },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Runtime recipe catalog and red-matrix dependency graph captured.");
    }

    [McpServerTool(
        Name = "spherewright_list_resource_nodes",
        Title = "List resource nodes in the owned ordinary world",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Lists an immutable bounded snapshot of local veins and hand-harvestable vegetation. The opaque cursor is bound to session, planet, filters, page size, and expiry.")]
    public static async Task<CallToolResult> ListResourceNodesAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID returned by spherewright_get_session_state.")] int planetId,
        [Description("Optional kind: vein or vegetation.")] string kind = "",
        [Description("Optional runtime resource type such as Iron, Copper, Oil, Tree, or Stone.")] string resourceType = "",
        [Description("Optional yielded item ID; use zero for no item filter.")] int productItemId = 0,
        [Description("Page size from 1 through 100.")] int limit = 50,
        [Description("Opaque continuation cursor, or empty to create a new snapshot.")] string cursor = "",
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.ListResourceNodesAsync(
            sessionId,
            new ListResourceNodesRequest
            {
                PlanetId = planetId,
                Kind = string.IsNullOrWhiteSpace(kind) ? null : kind,
                ResourceType = string.IsNullOrWhiteSpace(resourceType) ? null : resourceType,
                ProductItemId = productItemId > 0 ? productItemId : null,
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Resource-node page captured from one immutable owned-world snapshot.");
    }

    [McpServerTool(
        Name = "spherewright_inspect_resource_node",
        Title = "Inspect a live resource node",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Re-reads one live vein or vegetation object on Unity's main thread; it does not rely on a prior list snapshot.")]
    public static async Task<CallToolResult> InspectResourceNodeAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID returned by spherewright_get_session_state.")] int planetId,
        [Description("Resource kind returned by spherewright_list_resource_nodes.")] string kind,
        [Description("Node ID returned by spherewright_list_resource_nodes.")] int nodeId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.InspectResourceNodeAsync(
            sessionId,
            new InspectResourceNodeRequest { PlanetId = planetId, Kind = kind, NodeId = nodeId },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Live resource node captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_list_factory_entities",
        Title = "List built entities and prebuilds",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Lists an immutable bounded snapshot of built factory entities and legal prebuilds, including component identity, position, recipe, buffers, connections, and power state where applicable. Use componentKind=station for detailed logistics-station state, and componentKind=lab for matrix production/research; assembler-only results do not include labs. Finish the same filtered snapshot's pages before claiming absence. Buffer units matter: power-generation-current-tick is joules_per_tick, never fuel inventory, even if an older Plugin labels it items; unitsPerItem=0 means no item conversion. For power-generator entities the current reader does not populate isWorking; isWorking=false is not authoritative evidence of a stopped generator. Optional tankFluidCount reports identity-verified total fluid items including zero; null/missing is unknown. Empty tank buffers alone are not zero; the scalar and positive tank-fluid buffer are the same stock, not additive. Belt cargo is not observed in buffers; an empty belt buffers list does not prove an empty belt. List snapshots omit beltCargo: use inspect_factory_entity for that bounded local observation. Trace actual directed/reciprocal endpoints and observed device buffers/Overseer rates instead.")]
    public static async Task<CallToolResult> ListFactoryEntitiesAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID returned by spherewright_get_session_state.")] int planetId,
        [Description("Optional object kind: entity or prebuild.")] string objectKind = "",
        [Description("Optional component kind such as miner, assembler, lab, inserter, belt, storage, station, or power-generator.")] string componentKind = "",
        [Description("Optional exact building item ID; use zero for no item filter.")] int itemId = 0,
        [Description("Page size from 1 through 100.")] int limit = 50,
        [Description("Opaque continuation cursor, or empty to create a new snapshot.")] string cursor = "",
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.ListFactoryEntitiesAsync(
            sessionId,
            new ListFactoryEntitiesRequest
            {
                PlanetId = planetId,
                ObjectKind = string.IsNullOrWhiteSpace(objectKind) ? null : objectKind,
                ComponentKind = string.IsNullOrWhiteSpace(componentKind) ? null : componentKind,
                ItemId = itemId > 0 ? itemId : null,
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Factory-object page captured from one immutable owned-world snapshot.");
    }

    [McpServerTool(
        Name = "spherewright_inspect_factory_entity",
        Title = "Inspect a live built entity or prebuild",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Re-reads one built entity (positive objectId) or prebuild (negative objectId) on Unity's main thread. planetId may name the current local planet or an already-created, identity-verified simulation-resident factory in the current owned star system, even when its display is unloaded. Without ownedRemoteLogisticsStation, other stars fail closed. The explicit ownedRemoteLogisticsStation=true exception selects only one already-known positive logistics-station entity in an exact existing owned resident factory, including another star; it rejects geometry/material cuts and grants no remote writes. Unavailable simulation pools and inconsistent factory identities remain unavailable. This read does not create or load factories. All normal-action prepare/commit paths remain restricted to the current local planet. Completed station entities include detailed logisticsStation state and independent live/configuration hashes. A power-generation-current-tick buffer is joules_per_tick, never fuel inventory; zero does not prove empty fuel. Optional detail-only fuelPowerState copies ordinary thermal/fusion fuel stock and residual loaded heat separately: bufferedFuelCount is items, loadedFuelEnergyJoules is energy, not extra fuel items. Buffered and loaded fuel IDs may differ. Null/unavailable is unknown; an observed zero buffer can still have loaded heat. Capacity and generation are J/t, not burn or sustained supply. Lists omit this metadata. Explicit material cuts include validated thermal/fusion fuelPowerState at the cut tick; buffered fuel items and residual loaded heat remain separate from generation in J/t. Action hashes remain unchanged. For power-generator entities the current reader does not populate isWorking; isWorking=false is not authoritative evidence of a stopped generator. Optional tankFluidCount reports identity-verified total fluid items including zero; null/missing is unknown. Empty tank buffers alone are not zero; the scalar and positive tank-fluid buffer are the same stock, not additive. Belt cargo is not observed in buffers: use the separate detail-only beltCargo object. state=observed reports unique stacks touching only this belt segment at one game tick; unavailable/null is unknown, never zero. The read is bounded to 512 segment cells, with explicit reasons for unobserved seams or invalid references. Adjacent observations can include the same stack: do not sum them, infer flow from a single sample, or use these counts as an upgrade-preservation proof. Optional beltCargo.rearPickup identifies only the aligned native pickup packet when this segment ends an open path. no_aligned_packet does not mean an empty belt; not_applicable/unavailable/null is no queue-head evidence. Aggregated items are not queue order. Compare fresh reciprocal station input and needs/capacity before inferring mixed-input blockage, never call a single snapshot proof of sustained deadlock or clear stock to manufacture throughput. Optional detail-only sorterEndpoints exposes at most16 native slot world positions/outward directions with occupancy, or4 belt virtual orientations. Virtual slot=-1 has unknown physical occupancy, not a free-slot promise. Plan from slot geometry, not building centers; an observed endpoint is not a placement approval. Missing/unavailable geometry is unknown. Lists omit this geometry; existing action/endpoint hashes do not bind either optional observation. Follow bounded directed/reciprocal connections to real consumers; an unfinished trace is not proof that no consumer exists.")]
    public static async Task<CallToolResult> InspectFactoryEntityAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID, or an already-created identity-verified owned resident factory in the current star. With ownedRemoteLogisticsStation=true only, an already-known exact owned station factory may be in another star; this grants no load/create or remote write scope.")] int planetId,
        [Description("Required nonzero positive built entity ID or negative prebuild ID in that exact factory. The Bridge field is objectId, not entityId; for any remote read, use an already-known native object ID. This read does not load or enumerate another factory, and a missing ID never proves absence.")] int objectId,
        [Description("Optional explicit material cut: at most256 unique positive built-object IDs in the requested factory. Omit for the ordinary single-object read. Captures selected item stock and each selected belt's ENTIRE native cargo path once at the same game tick, at most64 paths / 32768 total cells / 4096 path members. Station stock is logisticsStation.storageSlots.count, excluding orders. Ordinary thermal/fusion stock is fuelPowerState.bufferedFuelCount; loadedFuelEnergyJoules is separate residual heat. Do not add power-generation-current-tick buffers to item stock. No traversal or automatic selection. Require materialInventoryCut.state=observed; missing/unavailable is unknown, not zero. Inspect all returned path members: counts may include belts outside the selection; never prorate or sum again per belt. A stock cut is not production, flow, source allocation or sustained supply.")] int[]? materialInventoryObjectIds = null,
        [Description("Optional READ-ONLY local-planet geometry cut: at most256 explicitly selected built/prebuild IDs and64 terrain points. Returns actual native poses, world collision bounds, complete selected connections, terrain rays and ground/layer0..3 native snap positions at one tick, bound to a whole static factory/configuration/terrain hash. Optional nativeBeltSegments accepts at most16 explicit free-to-free native_grid or native_elevated_grid spans, each1.5..30m, at most64 complete points per span /1024 total. It requires expectedFactoryGeometryHash and previously observed exact snapped endpoints/layers. Returns the ordinary generator's complete points and maximum slope BEFORE occupancy or native build-condition checks; NativeConditionsChecked=false, executable=false, no token, action, prebuild or qualification. Use these points for offline batch screening, never a geodesic estimate as grid evidence. Missing geometry is unavailable, never clear space; non-box bounds are conservative enclosures. No automatic traversal, selection, route search or placement approval. Remote same-star geometry is unsupported. Require the matching geometryCut echo; mixed cohorts fail closed.")] FactoryGeometryReadRequest? geometry = null,
        [Description("Optional READ-ONLY exact owned simulation-resident logistics station, including another star. Requires an already-known positive station objectId and an existing identity-verified owned factory. No factory load/create, geometry or material cut, traversal, scan or remote write scope. Detail-only activeVessels copies at most32 Native active carriers with complete/truncated/unknown coverage. A compressed array index is not shipIndex; ownerGId/shipIndex/gene identify only finite-window dispatch observations. Direction is away from/back to owner; t is stage-local, not route progress or ETA. Carrier motion, one arrival, or cargo0 does not prove delivery or sustained cycles.")] bool ownedRemoteLogisticsStation = false,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        if (ownedRemoteLogisticsStation && (objectId <= 0 || geometry is not null || materialInventoryObjectIds?.Length > 0))
            return ToToolResult(BridgeCallResult<FactoryEntitySnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, "Owned remote station reads require one positive station entity without geometry/material selections.", false,
                "Use the already-known exact owned station identity; no traversal or remote selection is performed.")),
                "No owned remote station captured.");
        var geometryError = Spherewright.Bridge.Core.Factory.FactoryGeometryReadPolicy.ValidateRequest(geometry);
        if (geometryError is not null)
            return ToToolResult(BridgeCallResult<FactoryEntitySnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, geometryError, false,
                "Use bounded explicit local object IDs and finite terrain positions.")), "No geometry cut captured.");
        var result = await bridgeClient.InspectFactoryEntityAsync(
            sessionId,
            new InspectFactoryEntityRequest
            { PlanetId = planetId, ObjectId = objectId, MaterialInventoryObjectIds = materialInventoryObjectIds?.ToList() ?? new List<int>(), Geometry = geometry,
                OwnedRemoteLogisticsStation = ownedRemoteLogisticsStation },
            cancellationToken).ConfigureAwait(false);
        if (ownedRemoteLogisticsStation && result.Success && (result.Value?.PlanetId != planetId || result.Value.ObjectId != objectId
            || result.Value.ObjectKind != FactoryObjectKinds.Entity || result.Value.LogisticsStation?.ActiveVessels is null))
            result = BridgeCallResult<FactoryEntitySnapshot>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not confirm the exact read-only owned resident station observation.", false,
                "Use matching Plugin/MCP files; missing vessel coverage is unknown and grants no remote write scope."));
        if (geometry is not null && result.Success
            && !Spherewright.Bridge.Core.Factory.FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(geometry, result.Value?.GeometryCut, sessionId, planetId))
            result = BridgeCallResult<FactoryEntitySnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady, "The Plugin did not confirm the exact read-only geometry cut.", false,
                "Use matching Plugin/MCP files; missing or malformed geometry is unknown, never zero or clear space."));
        return ToToolResult(result, "Live factory object captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_get_power_summary",
        Title = "Get local planet power networks",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns all active local power networks with generation, capacity, demand, served energy, storage, and service ratios from the current runtime. Pure power-node entity network IDs can be null; use fresh consumer/generator membership and this summary. Native tower construction can merge networks: reconcile actual members/topology, never replay a successful build on a caller assertion. Positive generation is not sustained fuel consumption. Instantaneous full service does not prove headroom: compare full-base load plus all remaining planned demand with fresh capacity; do not wait for a favorable capacity spike. Follow the agent playbook's demand and measurement rules.")]
    public static async Task<CallToolResult> GetPowerSummaryAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current local planet ID returned by spherewright_get_session_state.")] int planetId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetPowerSummaryAsync(
            sessionId,
            new LocalPlanetRequest { PlanetId = planetId },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Power-network summary captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_get_overseer_production",
        Title = "Get multi-planet production windows",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns a cursor-bound page of all already-created owned factories for up to 64 exact item IDs. Actual rates come from DSP's save-persisted 600-game-tick automatic production and consumption window; this is not an interval ledger, so do not sum overlapping or gapped windows as full-interval conservation, and keep inventory intervals separate. A below-target conservative lower bound is inconclusive, not proof of insufficient actual inflow. Read the Agent playbook before declaring a supply failure. A stopped and fully powered non-extractor with known zero production and a full native output buffer may report output_blocked even when its cycle exceeds the ready window; other diagnoses retain the complete-cycle requirement. The output_buffer_capacity evidence is the next-batch admission threshold, not physical storage capacity: a two-output assembly can already block at 19. An absent finding is not proof of sustainable supply. Theoretical output capacity is independently recomputed from identity-bound current runtime components. Supported assemblers, matrix labs, and resource extractors report bounded findings for material shortage, full output buffers, insufficient power, and exhausted veins; material shortages recursively follow item-admitting physical cargo paths and can cross an exact demand/supply station route into a producer behind the supply station's input belt. Physically proven logistics routes use a protected per-save game-tick window: active or warming shipments do not become shortages, while a truly missing consumer input with a positive demand reservation, available source inventory, a nonempty fleet, and 600 continuous ticks without carrier/order/delivery progress produces only a suspected logistics stall. Refilled input resets the stagnant baseline.")]
    public static async Task<CallToolResult> GetOverseerProductionAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("One to 64 unique positive runtime item IDs. Resend the identical IDs with a cursor.")] int[] itemIds,
        [Description("Number of planets per page, from 1 to 16; zero uses the default of 8.")] int limit = 0,
        [Description("Opaque nextCursor from the preceding page, or empty for a fresh snapshot.")] string cursor = "",
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetOverseerProductionAsync(
            sessionId,
            new GetOverseerProductionRequest
            {
                ItemIds = (itemIds ?? Array.Empty<int>()).ToList(),
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Save-persisted multi-planet production window captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_get_overseer_summary",
        Title = "Get multi-planet operations summary",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns one cursor-bound snapshot page containing per-planet power networks and logistics aggregates for every already-created owned factory, plus one global current-research summary. It does not create or load remote factories.")]
    public static async Task<CallToolResult> GetOverseerSummaryAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Number of planets per page, from 1 to 16; zero uses the default of 8.")] int limit = 0,
        [Description("Opaque nextCursor from the preceding page, or empty for a fresh snapshot.")] string cursor = "",
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetOverseerSummaryAsync(
            sessionId,
            new GetOverseerSummaryRequest
            {
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Multi-planet power, logistics, and research summary captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_get_overseer_diagnostic_bundle",
        Title = "Get a same-tick Overseer diagnostic bundle",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns one cursor-bound, same-game-tick diagnostic bundle that joins bounded production rates and root-cause findings with per-planet power and logistics summaries plus global research. A stocked_logistics_boundary retains consumer shortage and aggregate source inventory without claiming dispatch failure or an upstream producer cause. The public allowlist schema excludes save identity, filesystem paths, auth credentials, and action plan credentials. It does not create or load remote factories.")]
    public static async Task<CallToolResult> GetOverseerDiagnosticBundleAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("One to 64 unique positive runtime item IDs. Resend the identical IDs with a cursor.")] int[] itemIds,
        [Description("Number of planets per page, from 1 to 16; zero uses the default of 8.")] int limit = 0,
        [Description("Opaque nextCursor from the preceding page, or empty for a fresh snapshot.")] string cursor = "",
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetOverseerDiagnosticBundleAsync(
            sessionId,
            new GetOverseerDiagnosticBundleRequest
            {
                ItemIds = (itemIds ?? Array.Empty<int>()).ToList(),
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Same-tick multi-planet Overseer diagnostic bundle captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_configure_building",
        Title = "Prepare a device configuration",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Re-reads one exact built device for a production recipe, matrix-research mode, sorter filter, storage-capacity, station storage slot/output selector/charge setting without changing it. Storage-capacity requires an unstacked2101 warehouse and fresh full stateHash: storageOperation is set-bans (storageBannedGridCount is DISABLED final grids, 0 restores all), lock-occupied, filter-empty-or-matching (filterItemId), or clear-filters. Non-ban operations use storageBannedGridCount=-1. These native UI operations preserve all inventory and do not clear occupied grids or prove throughput; inspect plannedStorageConfiguration. Public buffers omit empty grids, so their offsets are NOT grid indices. filter0 does not prove an empty grid; lock-occupied does not reserve empty grids. Live delivery can occupy space between operations; storage_configuration_unchanged is not proof of a reservation. Read the agent playbook for bounded input guarding, normal transfer and capacity restoration; never repeatedly drain stock or relax the full stateHash. Sorter mode uses configurationStateHash including carried cargo: cargo-free sorters and ordinary2011/2012 unidirectional sorters in Inserting are supported. Existing cargo is preserved and still goes to the SAME destination; a new filter only governs future pickups and cannot clear a jam. Sorter configuration uses a request-bound expectedStateHash; neither the sorter-build echo plannedSorterFilterItemId nor nullable targetObjectId is required here. Keep the exact approved request, commit its fresh token once, poll the same action and verify the sorter/endpoints/inventory; missing build echoes never justify replay. Station modes bind their separate configuration hash and never clear, replace, or fill station inventory.")]
    public static async Task<CallToolResult> PrepareConfigureBuildingAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        int entityId,
        int recipeId,
        [Description("For mode=sorter-filter use the inspected root configurationStateHash, NOT stateHash, despite this parameter's name. For mode=storage-capacity use the full stateHash. Follow the documented hash domain for each other mode.")]
        string expectedFactoryStateHash,
        string mode = BuildingConfigurationModes.Production,
        int techId = 0,
        int filterItemId = 0,
        int stationStorageIndex = -1,
        int stationBeltSlotIndex = -1,
        int stationBeltStorageIndex = -1,
        int stationItemId = 0,
        int stationMaximumCount = 0,
        string stationLocalLogic = LogisticsStorageLogics.None,
        string stationRemoteLogic = LogisticsStorageLogics.None,
        long stationMaximumChargePowerWatts = 0,
        string expectedStationConfigurationStateHash = "",
        int stateHashVersion = 1,
        string storageOperation = "",
        int storageBannedGridCount = -1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareConfigureBuildingAsync(
            sessionId,
            new PrepareConfigureBuildingRequest
            {
                PlanetId = planetId,
                EntityId = entityId,
                RecipeId = recipeId,
                Mode = mode,
                TechId = techId,
                FilterItemId = filterItemId,
                StorageOperation = storageOperation,
                StorageBannedGridCount = storageBannedGridCount,
                StationStorageIndex = stationStorageIndex,
                StationBeltSlotIndex = stationBeltSlotIndex,
                StationBeltStorageIndex = stationBeltStorageIndex,
                StationItemId = stationItemId,
                StationMaximumCount = stationMaximumCount,
                StationLocalLogic = stationLocalLogic,
                StationRemoteLogic = stationRemoteLogic,
                StationMaximumChargePowerWatts = stationMaximumChargePowerWatts,
                ExpectedStationConfigurationStateHash = expectedStationConfigurationStateHash,
                ExpectedFactoryStateHash = expectedFactoryStateHash,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        if (mode == BuildingConfigurationModes.StorageCapacity && result.Success
            && (result.Value?.PlannedStorageOperation != storageOperation
                || result.Value?.PlannedStorageConfiguration is null))
            result = BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not confirm the storage UI operation and expected configuration; no token is exposed.",
                false, "Install a matching Plugin/MCP cohort, then fresh-read and prepare again."));
        return ToToolResult(result, "Device configuration plan prepared; device and station inventory state are unchanged.");
    }

    [McpServerTool(
        Name = "spherewright_commit_configure_building",
        Title = "Commit a device configuration",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Applies the prepared recipe, matrix-research mode, cargo-preserving sorter filter, single-warehouse storage-capacity operation, station storage slot, station output-belt selector, or maximum charge setting once through the current-version UI/business path. Warehouse operations prove every ordered grid's item/count/inc, filters/bans, identity, topology and player inventory; no items are moved or deleted by this operation. Successful storageConfigurationReadback retains the same-tick native boundary before normal delivery resumes. Later fresh inventory may differ after unbanning releases held cargo: reconcile delivery, do not require cross-tick equality or replay the action. Sorter cargo, destination, timing and topology remain unchanged by the filter assignment. Station inventory is never directly written. Poll the returned action to terminal and fresh-read delivery separately.")]
    public static async Task<CallToolResult> CommitConfigureBuildingAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitConfigureBuildingAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Device configuration completed with live readback.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_build",
        Title = "Prepare one normal owned-item building",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
[Description("Uses DSP's normal click/path/inserter validators for an unlocked building already in inventory. For ordinary2011/2012 sorters, set initialSorterFilterItemId to an unlocked item BEFORE connecting a mixed source: native construction installs the filter before the first pickup, with plannedSorterFilterItemId and final sign/filter readback. Zero means unfiltered; later configuration cannot undo contamination. Exact slots are tried first; one explicitly selected belt segment and an ordinary storage/assembler/lab may use native_single_belt_segment attachment, bounded to64 path intervals without retargeting neighboring belts. Two explicitly selected belt segments may use native_two_belt_segments, each bounded to64 intervals and the whole fallback to64 candidate placement checks, following native destination-then-source projection. This two-belt subset binds both segment geometries; it does not enumerate neighboring belts or rank UI cursor candidates. Inspect plannedInserterAttachment for both slots, positions and signed input/output offsets. Native facing/tilt, span, collision and materials remain mandatory; TooSkew is not permission to relax angles or move existing buildings. For exact-slot failures, nativeChecks counts actual DSP placement checks; read lastNativeRejection before the lastPreNativeRejection of a different slot pair. Neither a passing angle nor an unrelated TooSkew proves the overall placement result. On rejection read the attachment stage, candidateChecks and bestFacingDegrees: geometry_unavailable is not an angle verdict, and candidate checks do not imply native placement checks ran. Do not repeat an unchanged pair; move closer only for explicit OutOfReach. Bound endpoints use endpointStateHash; private segment geometry and offsets are rechecked before commit, while cargo flow alone is not geometry change. Full path checks use an inactive tool-owned stage1 command container, never the player's current anchor-only stage or a temporary edit to player commands. All NEW belt points, including both ends, require more than 0.25 m centre separation from existing built/prebuild belts and other new points; source-only non-removing cover reuse supports a same-grade flat built belt with a free output, one incoming belt at most, and a free destination. A narrow 2001 open-head destination cover also supports an explicit free source (native_grid) or a distinct free belt tail (native_grid or native_geodesic); both complete old paths must be empty. The target remains unfed; only the source may retain one stock-free, unpowered2301 Ti miner with zero Native power service and a sole reciprocal outlet to the path head. Read unpoweredSourceMinerObjectId and unpoweredSourceMinerStateHash; identity, Ti nodes, power, stock and every port are rebound before creation and at completion. Target input slots1/2/3 must all be free. At least two NEW belts are required; both covers stay in full native validation/creation and cost zero. Read destinationBindingMode=non_removing_belt_cover, reusedDestinationObjectId and destinationPreservationMode=empty_open_path_native_geometry_v1. Whole-path evidence is rebound before creation; completion proves exact joined membership, empty cargo, old edges and native rotations. Occupied-input merging, tier replacement, tilted/raised routes and closed paths still reject. Read plannedBeltPath: full_path_stage1, sourceBindingMode, reusedSourceObjectId and newObjectCount; itemBudget/plannedPath count only NEW objects, never either retained cover. Source-cover plans also require sourcePreservationMode=whole_path_native_rotation_v1: native path geometry must prove any completion-time belt rotation change. MCP withholds tokens when this echo is absent or inconsistent. Do not omit endpoint IDs to bypass occupancy or treat topology alone as permission for co-located belts. Belt NotEnoughItem / INVENTORY_INSUFFICIENT requires normal handcraft or transfer, not another site or Move: do not retry with unchanged inventory. After restocking, fresh-read and revalidate the complete path with the same explicit endpoint bindings; shortage does not prove placement or later collision checks. Prepare creates no prebuild and consumes nothing. Within an active remote_landed Voyager journey, the separate durable structured subset admits only1..8 same-item new2301/2104/2201/2203/2205/2001/2011 objects with exact Native checks, original return reserves and a16m target/ejection envelope. It excludes elevated/cover paths and requires fresh bounded Native drone/scene/energy proof. Poll the original action within30seconds and retain its buildId/constructionControl; expired work cannot be refreshed and pending objects must never be recreated.")]
    public static async Task<CallToolResult> PrepareBuildAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        int buildingItemId,
        string expectedPlayerStateHash,
        float preferredDistance = 12f,
        float? preferredPositionX = null,
        float? preferredPositionY = null,
        float? preferredPositionZ = null,
        float? preferredYaw = null,
        int resourceNodeId = 0,
        string expectedResourceStateHash = "",
        int sourceObjectId = 0,
        string expectedSourceStateHash = "",
        int destinationObjectId = 0,
        string expectedDestinationStateHash = "",
        float? pathEndX = null,
        float? pathEndY = null,
        float? pathEndZ = null,
        float pathLength = 6f,
        int stateHashVersion = 1,
        int initialSorterFilterItemId = 0,
        [Description("native_grid (default), native_geodesic, or native_elevated_grid. Elevated grid defaults to a separate2001 free-to-free subset: explicit start/end coordinates and beltStartAltitudeLevel/beltEndAltitudeLevel0..3, at least one raised; one native span1.5–30m, flat ends, slope<=0.5,4..64 NEW points. Its sole bound subset is one exact empty independent raised2001 tail: explicit source ID/hash/start position,3..63 NEW points plus the retained zero-cost non-removing source cover. No destination/resource binding or automatic bridge. Require exact routingMode/startAltitudeLevel/endAltitudeLevel/full_path_stage1/NEW-budget echoes. Native_geodesic supports one explicit free-ground2001/2002/2003 route1.5–30m after native snapping. Its sole bound subset is2001 with both source/destination IDs and endpoint hashes, no coordinate/resource arguments, and both complete old paths proven empty and independent. The destination remains unfed; the source may retain only a separately bound stock-free, unpowered2301 Ti miner. Require its unpoweredSourceMinerObjectId and unpoweredSourceMinerStateHash echoes when present, with zero Native power service and the same sole reciprocal head feed at completion. It retains two non-removing covers and at least two NEW belts; no occupied-input merging or raised geodesic. Require both cover/preservation echoes. Geodesic belts may rotate their four sorter directions: check source, bridge and consumer facing AND native span, not endpoint coordinates alone. For grid-aligned attachments prefer native_grid with straight end segments; fresh-read actual ports and prepare the critical sorter before constructing the remaining route. Never relax native checks or replay an unchanged rejected pair.")]
        string beltPathMode = BeltPathModes.NativeGrid,
        [Description("Only with native_elevated_grid: explicit native start layer 0..3. Default2001 free-to-free endpoints; at least one endpoint must be above layer0. An explicitly bound empty2001 source tail requires its exact start position/hash and a start layer1..3; destination stays free. A layer is 1.3333333m; native snapping and full placement checks decide feasibility. Require the Plugin's matching startAltitudeLevel echo before commit.")]
        int? beltStartAltitudeLevel = null,
        [Description("Only with native_elevated_grid: explicit native end layer 0..3. One native span no longer than30m, gentle slope and flat ends; no automatic bridge, guessed seam or destination reuse. The narrow raised source-tail cover must preserve the complete empty old path, old edges and native geometry. Require matching endAltitudeLevel and exact NEW budget; each subsequent seam still fresh-prepares after the previous real IDs exist.")]
        int? beltEndAltitudeLevel = null,
        [Description("Optional READ-ONLY qualification: one explicit free-to-free NEW2001 native_grid/native_elevated_grid span and at most two exact existing entity/hash/slot bindings. Source feeds its first belt; last belt feeds Destination. Specify ordinary2011/2012 sorter and positive runtime filter, explicit existing/planned belt quarter-turns0..3; an existing belt uses virtual slot-1. Planned quarter-turns select the NEW belt outward slot pose: Source role faces back toward the existing source; Destination role faces toward the existing destination. The adapter already applies the destination-end180-degree transform; do not apply it twice or copy a completed belt's path-derived quarter-turn into a spherical prebuild. Native path, angles, collision, whole NEW budget and technology are checked. Returns beltEndpointPreview with executable=false, no token/action/prebuild; it does NOT extend blueprint external matching or authorize construction. Native positive is not ongoing supply/power/whole-chain proof. Actual later construction still fresh-prepares each ordinary action. Mixed-cohort normal token responses are withheld.")]
        BeltEndpointPreviewRequest? beltEndpointPreview = null,
        [Description("Optional READ-ONLY complete NEW2001 route qualification. Supply a fresh factory geometry hash, both exact existing endpoint/hash/slot bindings, positive runtime filter, separate ordinary2011/2012 source/destination grades and1..12 explicit native_grid/native_elevated_grid spans. Use previously observed native snap positions, identical shared endpoints/layers, each1.5..30m, total<=240m and<=192 distinct NEW points. No automatic search, cover, resource binding or legacy position/path arguments. Native validates the entire path, then ALL NEW belts and both exact sorters together. Returns tokenless beltRoutePreview/executable=false with every point and both bindings, or honest pre-guard/native failure stages; no action, prebuild or construction capability. Full native positive is not continuous production or power proof. Actual construction requires separate fresh ordinary plans. Mixed-cohort normal tokens are withheld.")]
        BeltRoutePreviewRequest? beltRoutePreview = null,
        CancellationToken cancellationToken = default)
    {
        var request = new PrepareBuildRequest
            {
                PlanetId = planetId,
                BuildingItemId = buildingItemId,
                InitialSorterFilterItemId = initialSorterFilterItemId,
                PreferredDistance = preferredDistance,
                PreferredPosition = CreateOptionalVector(preferredPositionX, preferredPositionY, preferredPositionZ),
                PreferredYaw = preferredYaw,
                ResourceNodeId = resourceNodeId > 0 || ((beltRoutePreview is not null || beltPathMode != BeltPathModes.NativeGrid) && resourceNodeId != 0) ? resourceNodeId : (int?)null,
                ExpectedResourceStateHash = string.IsNullOrWhiteSpace(expectedResourceStateHash) ? null : expectedResourceStateHash,
                SourceObjectId = sourceObjectId > 0 || ((beltRoutePreview is not null || beltPathMode != BeltPathModes.NativeGrid) && sourceObjectId != 0) ? sourceObjectId : (int?)null,
                ExpectedSourceStateHash = string.IsNullOrWhiteSpace(expectedSourceStateHash) ? null : expectedSourceStateHash,
                DestinationObjectId = destinationObjectId > 0 || ((beltRoutePreview is not null || beltPathMode != BeltPathModes.NativeGrid) && destinationObjectId != 0) ? destinationObjectId : (int?)null,
                ExpectedDestinationStateHash = string.IsNullOrWhiteSpace(expectedDestinationStateHash) ? null : expectedDestinationStateHash,
                PathEnd = CreateOptionalVector(pathEndX, pathEndY, pathEndZ),
                PathLength = pathLength,
                BeltPathMode = beltRoutePreview is not null && beltPathMode == BeltPathModes.NativeGrid ? BeltPathModes.NativeRoutePreview : beltPathMode,
                BeltStartAltitudeLevel = beltStartAltitudeLevel,
                BeltEndAltitudeLevel = beltEndAltitudeLevel,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                StateHashVersion = stateHashVersion,
                BeltEndpointPreview = beltEndpointPreview,
                BeltRoutePreview = beltRoutePreview,
            };
        var routePreviewError = Spherewright.Bridge.Core.Factory.BeltRoutePreviewPolicy.ValidateRequest(request);
        if (routePreviewError is not null)
            return ToToolResult(BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, routePreviewError, false,
                "Use one explicit bounded read-only route request with fresh layout/player/endpoint hashes.")),
                "No read-only complete-route qualification performed.");
        var previewError = Spherewright.Bridge.Core.Factory.BeltEndpointPreviewPolicy.ValidateRequest(request);
        if (previewError is not null)
            return ToToolResult(BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, previewError, false,
                "Use an explicit NEW2001 span and exact existing endpoint bindings; no covers or implicit matching.")),
                "No read-only endpoint qualification performed.");
        var routingError = beltRoutePreview is not null ? null : Spherewright.Bridge.Core.Factory.BeltPathRoutingPolicy.ValidateRequest(request,
            buildingItemId >= 2001 && buildingItemId <= 2003);
        if (routingError is not null)
            return ToToolResult(BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, routingError, false,
                beltPathMode == BeltPathModes.NativeElevatedGrid
                    ? Spherewright.Bridge.Core.Factory.BeltElevationPolicy.Recovery
                    : Spherewright.Bridge.Core.Factory.BeltPathRoutingPolicy.Recovery)), "No construction plan prepared.");
        var result = await bridgeClient.PrepareBuildAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        if (beltRoutePreview is not null)
        {
            if (result.Success && !Spherewright.Bridge.Core.Factory.BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result.Value, sessionId))
                result = BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                    "The Plugin did not confirm the complete read-only route; no construction token is exposed.",
                    false, "Use matching Plugin/MCP files; never commit a substituted, partial or legacy route response."));
            return ToToolResult(result, "Read-only complete route qualification; no token/action/prebuild. Inspect all point and endpoint native results and failure stages.");
        }
        if (beltEndpointPreview is not null)
        {
            if (result.Success && !Spherewright.Bridge.Core.Factory.BeltEndpointPreviewPolicy.ConfirmsReadOnlyEcho(request, result.Value, sessionId))
                result = BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                    "The Plugin did not confirm the exact read-only endpoint qualification; no construction token is exposed.",
                    false, "Use matching Plugin/MCP files; never commit a substituted regular plan."));
            return ToToolResult(result, "Read-only route endpoint qualification; no token, action or construction. Check native flags and blockers.");
        }
        // Mixed-cohort installs must not silently turn a requested filter into a
        // legacy unfiltered plan. Do not expose that plan's commit capability.
        if (initialSorterFilterItemId > 0 && result.Success
            && result.Value?.PlannedSorterFilterItemId != initialSorterFilterItemId)
            result = BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The installed Plugin did not confirm the requested initial sorter filter; no construction token is exposed.",
                false, "Install matching Plugin/MCP files after a normal save and shutdown, then fresh-read and prepare again. Do not commit the unconfirmed plan."));
        if (buildingItemId >= 2001 && buildingItemId <= 2003 && result.Success && beltPathMode != BeltPathModes.NativeElevatedGrid
            && !Spherewright.Bridge.Core.Factory.BeltSourceReusePolicy.ConfirmsPlanEcho(result.Value, buildingItemId,
                Math.Max(0, sourceObjectId), Math.Max(0, destinationObjectId)))
            result = BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The installed Plugin did not confirm full native belt validation and the exact NEW-object budget; no construction token is exposed.",
                false, "Install matching Plugin/MCP files after a normal save and shutdown, then fresh-read and prepare again. Do not commit a legacy or unconfirmed belt plan."));
        if (buildingItemId >= 2001 && buildingItemId <= 2003 && result.Success && beltPathMode != BeltPathModes.NativeElevatedGrid
            && !Spherewright.Bridge.Core.Factory.BeltPathRoutingPolicy.ConfirmsPlanEcho(beltPathMode, result.Value?.PlannedBeltPath))
            result = BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The installed Plugin did not confirm the requested belt routing mode; no construction token is exposed.",
                false, "Install matching Plugin/MCP files after a normal save and shutdown, then fresh-prepare. Never commit a silently substituted grid route."));
        if (beltPathMode == BeltPathModes.NativeElevatedGrid && result.Success
            && !Spherewright.Bridge.Core.Factory.BeltElevationPolicy.ConfirmsPreparedEcho(
                result.Value, buildingItemId, beltStartAltitudeLevel, beltEndAltitudeLevel, sourceObjectId))
            result = BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The installed Plugin did not confirm the exact requested belt altitude levels; no construction token is exposed.",
                false, "Install matching Plugin/MCP files after a normal save and shutdown, then fresh-prepare. Never commit a silently substituted ground or different-height route."));
        return ToToolResult(result, "One owned-item construction plan prepared through DSP's build validator; no prebuild exists yet.");
    }

    [McpServerTool(
        Name = "spherewright_commit_build",
        Title = "Create and wait on one normal prebuild",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Consumes the prepared owned-item budget through the corresponding native click/path/inserter CreatePrebuilds and returns a pollable action. A prepared initial sorter filter is checked on the prebuild and completed entity. Spherewright never calls BuildFinally; normal construction drones must finish every step. Poll actionId to terminal before continuing. For the bounded Voyager structured subset, retain buildId/constructionControl and the one shared whole-batch debit. Dispatch disabled is not physical idle: require the exact real Native factory pass, a cleared prelaunch cache and actual owned-drone idle before safe terminal; unknown retains the original action and never replays creation.")]
    public static async Task<CallToolResult> CommitBuildAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitBuildAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal prebuild accepted; poll its actionId while construction drones work.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_dismantle",
        Title = "Prepare one supported miner, sorter, empty storage or empty belt head",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Re-reads one exact completed resource miner, ordinary2011/2012 sorter, empty default2101 storage, or isolated empty2001 chain head and the settled local player; verifies endpoint identity, native build range, idle manual build UI and conservative package capacity. Storage requires all30 grids completely empty/default, no layers or add-on, and no outgoing/incoming entity/prebuild or cached references; native identity/content are bound and rechecked. Belt head requires a complete same-tick empty open independent path of at most16 basic2001 belts/512 cells, a simple reciprocal chain, no external entity/prebuild/cached references or add-ons; complete native geometry/cargo/topology are bound and rechecked. Middle/tail, cargo, joins and higher belt grades reject. Sorters require native recoverable cargo, bounded inbound-reference checks and reciprocity of every PRESENT edge. Missing sorter ends are allowed; mismatched present edges, advanced stacking, prebuild targets and native instant-dismantle are rejected. Prepare removes nothing and returns no item; no automatic rebuild.")]
    public static async Task<CallToolResult> PrepareDismantleAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        int objectId,
        string expectedEndpointStateHash,
        string expectedPlayerStateHash,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareDismantleAsync(
            sessionId,
            new PrepareDismantleRequest
            {
                PlanetId = planetId,
                ObjectId = objectId,
                ExpectedEndpointStateHash = expectedEndpointStateHash,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal supported-entity dismantle prepared with no side effect.");
    }

    [McpServerTool(
        Name = "spherewright_commit_dismantle",
        Title = "Recover one exact supported miner, sorter, empty storage or empty belt head",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Calls DSP's normal PlayerAction_Build.DoDismantleObject once for the approved miner, ordinary2011/2012 sorter, isolated empty default2101 storage, or isolated empty2001 chain head. Poll actionId to terminal; proves exact disappearance and building/cargo recovery without unexplained inventory deltas. Sorter removal verifies cargo inc and surviving endpoints. Empty storage requires no layers/add-on/entity/prebuild/cached references, returns exactly one2101, preserves inventory inc and all surviving native entities/prebuilds/connections. Empty belt head returns exactly one2001, preserves inventory inc, the complete remaining empty chain and all unrelated entities/prebuilds/connections; only the removed head edge clears and touched belt rotations require exact native renderer/collider proof. It never writes connections directly and has no automatic rebuild. Missing outcome evidence quarantines writes; never repeat the dismantle under a new key.")]
    public static async Task<CallToolResult> CommitDismantleAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitDismantleAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal supported-entity dismantle accepted; poll actionId for terminal disappearance and recovery evidence.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_transfer",
        Title = "Prepare an exact player-storage transfer",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Re-reads the player and one exact storage, verifies source count, destination capacity, range, and bilateral conservation without moving any item.")]
    public static async Task<CallToolResult> PrepareTransferAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string direction,
        int storageEntityId,
        int itemId,
        int count,
        string expectedPlayerStateHash,
        string expectedStorageStateHash,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareTransferAsync(
            sessionId,
            new PrepareTransferRequest
            {
                PlanetId = planetId,
                Direction = direction,
                StorageEntityId = storageEntityId,
                ItemId = itemId,
                Count = count,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                ExpectedStorageStateHash = expectedStorageStateHash,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Exact player-storage transfer plan prepared; both containers are unchanged.");
    }

    [McpServerTool(
        Name = "spherewright_commit_transfer",
        Title = "Commit an exact player-storage transfer",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Moves the prepared exact count through StorageComponent's normal UI business operations and proves equal-and-opposite container deltas. Poll the action to terminal. For a successful transfer, beforeTargetAmount/afterTargetAmount are the storage counts and itemDeltas are the player's changes at completedAtGameTick. Check these same-tick deltas, then inspect later logistics separately: an active storage may already have received or sent items, so do not require cross-tick equality. A later stock difference or reporting error does not undo the terminal result; never replay an accepted transfer. Missing proof or an unexplained flow still requires reconciliation, not assumed conservation.")]
    public static async Task<CallToolResult> CommitTransferAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitTransferAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Player-storage transfer completed with exact bilateral conservation readback.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_logistics_station_fleet_transfer",
        Title = "Prepare an exact logistics-station fleet transfer",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Re-reads the player package and one exact local station fleet, then verifies item type, idle/working counts, prefab capacity, range, empty player hand, proliferator safety, and exact destination capacity without moving a craft. Native station warpers1210 use the same bilateral transfer: logistics-vessel warp-drive technology, actual warper capacity, exact zero-inc package items and fresh warperCount are required. This does not grant a remote station write scope.")]
    public static async Task<CallToolResult> PrepareLogisticsStationFleetTransferAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        int stationEntityId,
        string direction,
        int itemId,
        int count,
        string expectedPlayerStateHash,
        string expectedStationFleetStateHash,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareLogisticsStationFleetTransferAsync(
            sessionId,
            new PrepareLogisticsStationFleetTransferRequest
            {
                PlanetId = planetId,
                StationEntityId = stationEntityId,
                Direction = direction,
                ItemId = itemId,
                Count = count,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                ExpectedStationFleetStateHash = expectedStationFleetStateHash,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Exact player-station fleet transfer prepared; the player package and station remain unchanged.");
    }

    [McpServerTool(
        Name = "spherewright_commit_logistics_station_fleet_transfer",
        Title = "Commit an exact logistics-station fleet transfer",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Moves the prepared logistics drones, vessels or native station warpers1210 between the player package and one exact local station using the current UI counter semantics, then proves equal-and-opposite counts and preservation of working craft, storage, energy, and unrelated inventory. Warpers require native warp-drive technology, actual capacity and zero-inc items. Unknown outcomes retain the original action/key and never justify another transfer.")]
    public static async Task<CallToolResult> CommitLogisticsStationFleetTransferAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitLogisticsStationFleetTransferAsync(
            sessionId,
            request,
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Player-station fleet transfer completed with exact conservation readback.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_refuel",
        Title = "Prepare a normal mecha refuel transfer",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Re-reads player inventory and the mecha fuel chamber, then binds the exact native one-stack transfer count and destination grid without moving fuel.")]
    public static async Task<CallToolResult> PrepareRefuelAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        int itemId,
        int count,
        string expectedPlayerStateHash,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareRefuelAsync(
            sessionId,
            new PrepareRefuelRequest
            {
                PlanetId = planetId,
                ItemId = itemId,
                Count = count,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Native mecha-refuel plan prepared; inventory and fuel chamber are unchanged.");
    }

    [McpServerTool(
        Name = "spherewright_commit_refuel",
        Title = "Commit a normal mecha refuel transfer",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Uses Mecha.AutoReplenishFuel for the prepared stack and proves exact equal-and-opposite player/fuel-chamber item deltas; it never injects energy or items.")]
    public static async Task<CallToolResult> CommitRefuelAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitRefuelAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Mecha refuel completed through DSP's native transfer with conservation readback.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_save",
        Title = "Prepare a save of the owned world",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Binds the current revision and exact owned save identity without writing a save. preserveQuarantinedProgress is an explicit serialization-only opt-in for a previously admitted remote world paused by its retained idle guard: requires stable post-tail Native Pause and no pending action/load/Journal. It preserves quarantine, original outcomes, tickets and budgets; it does not grant Resume or recovery authority.")]
    public static async Task<CallToolResult> PrepareSaveAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        long expectedRevision,
        int stateHashVersion = 1,
        bool preserveQuarantinedProgress = false,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareSaveAsync(
            sessionId,
            new PrepareSaveRequest
            {
                PlanetId = planetId,
                ExpectedRevision = expectedRevision,
                StateHashVersion = stateHashVersion,
                PreserveQuarantinedProgress = preserveQuarantinedProgress,
            },
            cancellationToken).ConfigureAwait(false);
        if (preserveQuarantinedProgress && result.Success && (result.Value?.PreservesQuarantine != true
            || !result.Value.PreservationGameTick.HasValue || result.Value.PreservationGameTick < 0))
            return ToToolResult(BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState, "The Plugin did not disclose the requested paused quarantine-preserving save.",
                false, "Keep the world paused and use a matching verified Plugin/MCP cohort.")), "Save preservation is unproved.");
        return ToToolResult(result, "Owned-world save plan prepared; no save file was written.");
    }

    [McpServerTool(
        Name = "spherewright_commit_save",
        Title = "Save the owned world normally",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Calls DSP's normal save API only for the exact current Spherewright-owned save name and records the confirmed game tick; it never enumerates or opens another save.")]
    public static async Task<CallToolResult> CommitSaveAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitSaveAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "DSP confirmed a normal save of the exact active Spherewright-owned world.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_quarantine_reconciliation",
        Title = "Prove the exact quarantined action outcome",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Re-reads only the exact outcome-unknown build named by writeQuarantineActionId and prepares a short-lived proof when its retained item cost, built entities, components, and directed topology are all unambiguous. This never clears quarantine or changes the game.")]
    public static async Task<CallToolResult> PrepareQuarantineReconciliationAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string actionId,
        long expectedRevision,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareQuarantineReconciliationAsync(
            sessionId,
            new PrepareQuarantineReconciliationRequest
            {
                PlanetId = planetId,
                ActionId = actionId,
                ExpectedRevision = expectedRevision,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Exact quarantine reconciliation proof prepared; quarantine remains active until its matching commit.");
    }

    [McpServerTool(
        Name = "spherewright_commit_quarantine_reconciliation",
        Title = "Commit an exact quarantine reconciliation",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Revalidates the prepared proof and clears write quarantine only when the same retained outcome-unknown action, reason, item cost, entities, components, and topology still match. It is not an unconditional administrative clear.")]
    public static async Task<CallToolResult> CommitQuarantineReconciliationAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitQuarantineReconciliationAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "The exact prior action was proved and its matching write quarantine was cleared.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_move",
        Title = "Prepare normal surface movement",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Re-reads the owned-world player state and prepares a short-lived normal ground-movement order. Additive surfacePreview samples at most64m of the requested shortest surface arc using at most65 samples and at most130 native downward rays. shoreRisk=detected warns of a possible water/shore crossing; partial/unavailable/null is not dry-ground proof. not_detected is not route clearance or guaranteed Walk: small intervening features, obstacles and the actual controller path remain unchecked. Read the agent playbook before movement: a fully observed short crossing distinguishes water in transit from an explicitly above-water terminal suffix; an aggregate warning alone does not identify the landing. This advisory evidence does not change existing hash/commit admission and never chooses or executes another target. For ordinary walking, revise risky/unknown proposals from fresh evidence before commit; after terminal arrival always fresh-read Walk, low speed and energy. Explicit allowPassiveDrift (default false) binds the latest external player inspection for an idle Drift only: speed at most0.15m/s, at most0.05m total drift within2 seconds, unchanged original non-position PlayerAction fields and exact capacity, no forge/construction/player order. Inspect moveStateBinding and its shortened expiry; do not issue another player read between prepare and commit. This does not relax terrain or post-arrival Walk proof. It never moves or teleports the player during prepare.")]
    public static async Task<CallToolResult> PrepareMoveAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        float targetX,
        float targetY,
        float targetZ,
        string expectedPlayerStateHash,
        float arrivalTolerance = 1.5f,
        int stateHashVersion = 1,
        bool allowPassiveDrift = false,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareMoveAsync(
            sessionId,
            new PrepareMoveRequest
            {
                PlanetId = planetId,
                Target = new Vector3Snapshot { X = targetX, Y = targetY, Z = targetZ },
                ArrivalTolerance = arrivalTolerance,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                StateHashVersion = stateHashVersion,
                AllowPassiveDrift = allowPassiveDrift,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal surface-movement plan prepared; no player order was issued.");
    }

    [McpServerTool(
        Name = "spherewright_commit_move",
        Title = "Commit prepared normal surface movement",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Starts the prepared movement through DSP's Player.Order path and returns a pollable action. Poll its actionId to terminal. On position_stalled or route_stalled, follow the opening-movement playbook and never retry the same target. Inspect nearby objects including belts and sorters/inserters; center distance alone is not walking clearance. It never writes player position.")]
    public static async Task<CallToolResult> CommitMoveAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitMoveAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal movement order accepted; poll its actionId for game-tick completion.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_interplanetary_flight",
        Title = "Prepare a normal same-star planet flight",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Binds a non-gas destination in the current star, Drive Engine level 2, a nearly full core, usable normal fuel, and stable player/star-system snapshots. Prepare never launches or changes movement.")]
    public static async Task<CallToolResult> PrepareInterplanetaryFlightAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        int destinationPlanetId,
        string expectedPlayerStateHash,
        string expectedStarSystemStateHash,
        double minimumCoreEnergyRatio = 0.95d,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareInterplanetaryFlightAsync(
            sessionId,
            new PrepareInterplanetaryFlightRequest
            {
                PlanetId = planetId,
                DestinationPlanetId = destinationPlanetId,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                ExpectedStarSystemStateHash = expectedStarSystemStateHash,
                MinimumCoreEnergyRatio = minimumCoreEnergyRatio,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Same-star flight plan prepared; the player remains grounded.");
    }

    [McpServerTool(
        Name = "spherewright_commit_interplanetary_flight",
        Title = "Launch and land a prepared same-star flight",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Uses DSP's native flight/sail transition and sail-energy functions to launch, steer, brake, and land on the bound planet. Poll to terminal: a transient Walk tick does not complete landing or cancel an unfinished exact shore order. Stable arrival still requires 600 consecutive grounded low-speed ticks. A terminal recovery_required requires its matching checkpoint flow, not a competing Move or relabeling success. It never teleports, grants fuel, or enables sandbox fast travel.")]
    public static async Task<CallToolResult> CommitInterplanetaryFlightAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitInterplanetaryFlightAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal same-star flight accepted; poll its actionId through landing.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_harvest",
        Title = "Prepare normal manual harvesting",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Binds an inspected vein or vegetation node plus current player state and budgets its runtime manual-mining yield without changing the world.")]
    public static async Task<CallToolResult> PrepareHarvestAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string resourceKind,
        [Description("Resource node ID returned by spherewright_list_resource_nodes or spherewright_inspect_resource_node; factory object IDs are a separate namespace and are invalid here.")] int nodeId,
        int requestedYieldCount,
        string expectedResourceStateHash,
        string expectedPlayerStateHash,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareHarvestAsync(
            sessionId,
            new PrepareHarvestRequest
            {
                PlanetId = planetId,
                ResourceKind = resourceKind,
                NodeId = nodeId,
                RequestedYieldCount = requestedYieldCount,
                ExpectedResourceStateHash = expectedResourceStateHash,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal manual-harvest plan prepared; resource and inventory are unchanged.");
    }

    [McpServerTool(
        Name = "spherewright_commit_harvest",
        Title = "Commit prepared normal manual harvesting",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Starts DSP's normal player mining order. Walking, mining time, energy use, node depletion, and item delivery remain game-driven.")]
    public static async Task<CallToolResult> CommitHarvestAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitHarvestAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal manual-harvest order accepted; poll its actionId for conservation readback.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_handcraft",
        Title = "Prepare a normal replicator task",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Validates an unlocked handcraft recipe against the live player inventory through MechaForge's test path and returns a no-side-effect plan.")]
    public static async Task<CallToolResult> PrepareHandcraftAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        int recipeId,
        int count,
        string expectedPlayerStateHash,
        int stateHashVersion = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareHandcraftAsync(
            sessionId,
            new PrepareHandcraftRequest
            {
                PlanetId = planetId,
                RecipeId = recipeId,
                Count = count,
                ExpectedPlayerStateHash = expectedPlayerStateHash,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal replicator plan prepared; no material or queue state changed.");
    }

    [McpServerTool(
        Name = "spherewright_commit_handcraft",
        Title = "Commit a prepared normal replicator task",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Queues the prepared recipe through MechaForge.AddTask so normal ingredients, energy, time, and output delivery apply.")]
    public static async Task<CallToolResult> CommitHandcraftAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitHandcraftAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal replicator task accepted; poll its actionId for product readback.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_select_research",
        Title = "Prepare normal technology selection",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Validates a runtime technology, prerequisites, level, and queue without mutation. Default enqueues normally and advertises researchQueueReadbackVersion=1 for its immediate queue/progress/material preservation receipt; missing/zero is legacy or unproved. itemBudget research-consumption is this level's native total requirement, not an enqueue debit or remaining cost; int.MaxValue may be a capped projection. Explicit prioritizeQueued=true only moves an already queued non-head technology with all native prerequisites completed to the front, preserving other queue order, progress and inventory. Fresh selection hash required; paused, duplicate-level or invalid queues reject. No research progress or unlocks are granted.")]
    public static async Task<CallToolResult> PrepareSelectResearchAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        int techId,
        string expectedSelectionStateHash,
        int stateHashVersion = 1,
        bool prioritizeQueued = false,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareSelectResearchAsync(
            sessionId,
            new PrepareSelectResearchRequest
            {
                PlanetId = planetId,
                TechId = techId,
                ExpectedSelectionStateHash = expectedSelectionStateHash,
                PrioritizeQueued = prioritizeQueued,
                StateHashVersion = stateHashVersion,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal technology-selection plan prepared; research state is unchanged.");
    }

    [McpServerTool(
        Name = "spherewright_commit_select_research",
        Title = "Commit prepared normal technology selection",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Applies the exact prepared native enqueue or explicit queued-tech priority change. Priority uses native SortTechQueue, preserves all other order, verifies research progress and inventory immediately, and returns researchQueueReadback. Poll to terminal then fresh progression. No hashes, matrices or unlock flags are added.")]
    public static async Task<CallToolResult> CommitSelectResearchAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey);
        var result = await bridgeClient.CommitSelectResearchAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Normal technology queue accepted the selection.");
    }

    [McpServerTool(
        Name = "spherewright_get_action_result",
        Title = "Get a retained Spherewright action result",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns the current or terminal state of an action accepted by this Plugin process. It does not repeat the action. Retain terminal results promptly: ordinary normal-action receipts may expire after their configured completion-retention window; active, uncertain/recovery and checkpoint-bound flight evidence is retained, with new normal commits refused at capacity. Missing old evidence never proves non-execution or authorizes replay. Transfer beforeTargetAmount/afterTargetAmount and itemDeltas describe the synchronous completedAtGameTick boundary, not a later warehouse inventory after normal logistics. A host reporting error does not authorize replaying an accepted action.")]
    public static async Task<CallToolResult> GetActionResultAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Action ID returned by a Spherewright commit.")] string actionId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetActionResultAsync(
            new GetActionResultRequest { ActionId = actionId },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Retained action state captured without repeating the action.");
    }

    [McpServerTool(
        Name = "spherewright_list_assemblers",
        Title = "List assemblers in the owned ordinary world",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Lists a bounded page of assembler snapshots from the active Spherewright-owned ordinary world. It refuses unowned game sessions. This is not a list of every production device: matrix recipes use labs, not assemblers. Use spherewright_list_factory_entities with componentKind=lab for matrix production/research. Choose component families from the runtime recipe catalog; no match in one family or partial page does not prove no producer/consumer exists.")]
    public static async Task<CallToolResult> ListAssemblersAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Maximum assemblers to return, from 1 through 100.")] int limit = 50,
        [Description("Opaque continuation cursor from a previous result, or an empty string to start.")] string cursor = "",
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.ListAssemblersAsync(
            sessionId,
            new ListAssemblersRequest { Limit = limit, Cursor = string.IsNullOrEmpty(cursor) ? null : cursor },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Assembler page captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_inspect_assembler",
        Title = "Inspect an assembler in the owned ordinary world",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns one validated assembler snapshot from the active Spherewright-owned ordinary world. It refuses stale entities and unowned game sessions.")]
    public static async Task<CallToolResult> InspectAssemblerAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Current assembler entity ID returned by spherewright_list_assemblers.")] int entityId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.InspectAssemblerAsync(
            sessionId,
            new InspectAssemblerRequest { EntityId = entityId },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Assembler snapshot captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_get_build_catalog",
        Title = "Get the current unlocked ordinary build catalog",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns building and recipe candidates with native unlock/availability flags from the owned world's runtime prototypes. Optional fuelPowerProfile reports ordinary thermal/fusion base generation and full-load fuel energy in joules per tick plus the native fuel type mask. Null/missing is unknown, not zero. These catalog ratings are not live generation, inventory or sustainable supply and do not grant Foundry planned-generation credit. It refuses unowned sessions and never changes game state.")]
    public static async Task<CallToolResult> GetBuildCatalogAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Current session ID returned by spherewright_get_session_state.")] string sessionId,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.GetBuildCatalogAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Live build catalog with native availability flags captured from the owned ordinary world.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_new_game",
        Title = "Prepare a standard peaceful new game",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a short-lived, single-use plan for a fresh peaceful 1x world with sandbox mode disabled. This prepare step does not start a game or write a save.")]
    public static async Task<CallToolResult> PrepareTestWorldAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Galaxy seed from 0 through 99999999.")] int galaxySeed = 13572468,
        [Description("Galaxy star count from 20 through 80.")] int starCount = 32,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareTestWorldAsync(
            new PrepareTestWorldRequest { GalaxySeed = galaxySeed, StarCount = starCount },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Standard peaceful new-world plan prepared; no game state changed.");
    }

    [McpServerTool(
        Name = "spherewright_commit_new_game",
        Title = "Create the prepared standard peaceful world",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Consumes one unexpired plan to start a fresh peaceful 1x non-sandbox world through DSP's official new-game flow. Requires writes enabled and a UUID idempotency key. After creation, read MCP resource spherewright://agent/playbooks/opening-movement-v1 before the first gameplay action.")]
    public static async Task<CallToolResult> CommitTestWorldAsync(
        [Description("Injected authenticated bridge client.")] IBridgeClient bridgeClient,
        [Description("Single-use plan token returned by spherewright_prepare_new_game.")] string planToken,
        [Description("UUID reused for retries of this exact commit.")] string idempotencyKey,
        [Description("Cancellation token supplied by the MCP host.")] CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.CommitTestWorldAsync(
            new CommitTestWorldRequest { PlanToken = planToken, IdempotencyKey = idempotencyKey },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "Standard peaceful Spherewright world creation accepted.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_save_import",
        Title = "Prepare confirmation-gated import of the loaded save",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a no-game-side-effect plan bound to the exact currently loaded unowned session and revision. After this tool succeeds, stop and explicitly ask the user the returned confirmationPrompt—even if import was requested earlier. Do not call spherewright_commit_save_import until a subsequent user message clearly confirms. The original save remains unchanged; journal history starts at import.")]
    public static async Task<CallToolResult> PrepareUserSaveImportAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.PrepareUserSaveImportAsync(
            sessionId,
            new PrepareUserSaveImportRequest
            {
                ExpectedRevision = expectedRevision,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "A single-use owned-copy import plan was prepared with no game or save side effect. Ask the user the returned confirmationPrompt now; do not commit before their subsequent explicit confirmation.");
    }

    [McpServerTool(
        Name = "spherewright_commit_save_import",
        Title = "Clone and adopt the user-confirmed loaded save",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Consumes one prepared import plan only after a subsequent explicit user confirmation in the current conversation. Set all three confirmation fields true only when that later user message clearly authorizes this exact prepared import. Uses DSP's normal save API to create and verify a new internally named owned copy; it never names, enumerates, loads, overwrites, renames, or deletes the original save.")]
    public static async Task<CallToolResult> CommitUserSaveImportAsync(
        IBridgeClient bridgeClient,
        string sessionId,
        string planToken,
        string idempotencyKey,
        [Description("True only after a subsequent user message explicitly confirms this exact prepared import in the current conversation.")] bool userConfirmedInConversation,
        [Description("Acknowledge that the original save will not be overwritten, renamed, or deleted.")] bool acknowledgeOriginalSaveRemainsUnchanged,
        [Description("Acknowledge that the journal starts at import and does not reconstruct earlier first-time events.")] bool acknowledgeJournalStartsAtImport,
        CancellationToken cancellationToken = default)
    {
        var result = await bridgeClient.CommitUserSaveImportAsync(
            sessionId,
            new CommitUserSaveImportRequest
            {
                PlanToken = planToken,
                IdempotencyKey = idempotencyKey,
                UserConfirmedInConversation = userConfirmedInConversation,
                AcknowledgeOriginalSaveRemainsUnchanged = acknowledgeOriginalSaveRemainsUnchanged,
                AcknowledgeJournalStartsAtImport = acknowledgeJournalStartsAtImport,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "The explicitly confirmed current-world import reached a terminal result; the original save was not addressed or modified.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_resume_owned_game",
        Title = "Prepare one-time resume of the exact owned world",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Ordinary opening defaults to open_saved_primary: verify the saved world independently of old automation expiry, load paused, and keep old tasks stopped. No automatic save, charging or task renewal. Explicit authorize_remote_primary_recovery instead requires a NEW separately owner-authorized finite window for the current remote primary, exact digest confirmation and unchanged original Journey reserves/outcomes. An existing return-only task can instead use resume_return_only_task with the exact current fixed LastExit, recover_and_return_only scope, unchanged remaining ticks and no charging; a proposed new finite wall window needs separate owner authority. Prepare from an idle main menu: gameLoaded=false is expected, not a load failure. Use the current restartResumeAvailable/restartResumeToken; prepare checks native preload/menu/no-loader readiness, protected ticket and durable Journal. Do not wait for gameLoaded=true before prepare. The explicit legacy default healthy restart mode selects the exact ticket-bound primary; real quarantine selects a qualifying fixed LastExit. Only after explicit user conversation confirmation may verified_newer_lastexit bind a newer fixed LastExit to an exact approved tick and known progress floor. It verifies embedded full identity, current version, peaceful mode and bounded full-file evidence; commit rechecks and adoption must cover the candidate tick. No automatic fallback, arbitrary names, autosave selection, enumeration, ticket consumption or load during prepare.")]
    public static async Task<CallToolResult> PrepareOwnedWorldResumeAsync(
        IBridgeClient bridgeClient,
        string resumeToken,
        CancellationToken cancellationToken = default,
[Description("open_saved_primary is the ordinary-opening default: expired automation does not prevent opening the saved world, and old tasks remain stopped. authorize_remote_primary_recovery is a separate NEW finite automation authorization for the exact remote primary, with full Native admission and conserved original tick/energy/warper budgets. It accepts no prior-action outcome attestation. resume_return_only_task continues only an existing granted return-only task from its exact current fixed LastExit and protected progress boundary. It requires expectedRecoveryGameTick and recover_and_return_only scope; it grants no ticks, resources or charging and accepts no prior-outcome claims. An optional new absolute wall deadline, at most3600 seconds, requires applicable explicit owner authority and digest confirmation; a matching standing grant needs no repeated conversation approval; omission retains the original effective window. Full Native control admission, normal Save and a fresh healthy credential still precede success. Explicit legacy execution/recovery modes: default, verified_newer_lastexit, reauthorize_expired_primary, reauthorize_expired_autosave0, reauthorize_fixed_autosave0, reauthorize_version_changed_primary, reauthorize_failed_remote_last_exit, or continue_completed_remote_last_exit. The completed mode requires an immediate known-completed attestation and current context, inherits the same window/origin without new budget and prohibits charging. The failed-remote mode requires the independently captured current context, active quarantine credential, immediate known-terminal-failed attestation and finite proposal; it is never a default fallback or an old commitment opening. The expired-primary mode previews the exact expired primary and original Journal, discloses sourceGameVersion/targetGameVersion and requires subsequent conversation confirmation. Fixed AutoSave0 modes require a known floor and exact authorized candidate tick; prepare never accepts confirmation. Expired AutoSave0 uses expired healthy provenance and evidence version3; fixed AutoSave0 uses an active healthy credential and evidence version4. Both require the same supported source/runtime/candidate version, exact identity and original durable Journal; no migration or fallback. For active fixed AutoSave0 an existing explicit conversation grant matching the disclosed candidate suffices at commit without asking again; generic continue is not authority for another candidate. Expired-primary supports only exact researched directional pairs: 0.10.34.28529 to 0.10.35.29057 or 0.10.35.29088, 0.10.35.29088 to 0.10.35.29104, 0.10.35.29104 to 0.10.35.29131, 0.10.35.29131 to 0.10.35.29145, 0.10.35.29145 to 0.10.35.29151, and proven direct 0.10.35.29088 to 0.10.35.29151. The final two pairs are expired-primary only; direct adoption records one real durable transition without fabricating intermediate adoptions, preserving the original Journal prefix and five-transition limit; active-primary evidence5/6 stays unchanged. Version-changed-primary uses unchanged active healthy provenance only for 29104 to29131 with evidence5 or 29131 to29145 with evidence6, the exact recorded primary tick and original Journal; subsequent explicit confirmation of its pair-specific prepared disclosure is required. Default resume still requires equal versions. Unknown pairs and downgrades reject. Never renew/edit ticket expiry.")] string recoveryMode = OwnedWorldResumeModes.OpenSavedPrimary,
        [Description("Caller attests to actual explicit user confirmation in the conversation; never infer it.")] bool userConfirmedInConversation = false,
        [Description("Known latest progress; required for verified recovery or exact explicitly approved primary progress loss.")] long? minimumRecoveryGameTick = null,
        [Description("Exact explicitly authorized candidate tick; required for resume_return_only_task, verified_newer_lastexit, fixed AutoSave0 modes and explicit primary progress loss. No slot selection.")] long? expectedRecoveryGameTick = null,
        [Description("Optional one-shot absolute UTC cap for a quarantined remote default continuation, or required for reauthorize_failed_remote_last_exit or authorize_remote_primary_recovery. Requires a fresh disclosed proposal, applicable explicit conversation authority and its exact confirmationDigest; matching existing authority does not require asking again. Never changes the original journey deadline or tick budget. For resume_return_only_task, omission retains the existing effective window; a new at-most3600-second window needs applicable explicit owner authority; a matching standing grant needs no repeated conversation approval.")] DateTimeOffset? recoveryDeadlineUtc = null,
        [Description("resume_return_only_task requires recover_and_return_only, including when its deadline is omitted. Other recovery-window modes require continue_original_finite_task; otherwise empty.")] string recoveryScope = "",
        [Description("Optional natural-charge tick cap, 1..18000, inside the original remaining game ticks and five-minute adoption limit. Must be0 for resume_return_only_task.")] long maximumChargeGameTicks = 0,
        [Description("Optional natural-charge wall cap, greater than0 and at most300 seconds; never extends the original five-minute adoption lease. Must be0 for resume_return_only_task.")] double maximumChargeWallSeconds = 0d,
        [Description("Optional already-consumed credential for exactly one prior failed remote resume; requires priorSourceFingerprint and priorActionKnownTerminalFailed together. Never replays that action or selects a file.")] string priorConsumedResumeToken = "",
        [Description("Exact sha256 source-fingerprint witness for the prior consumed authorization. The Plugin opens its original commitment against current route, checkpoint, Journal and primary; no path or JSON input.")] string priorSourceFingerprint = "",
        [Description("Explicit caller attestation, supported by the original action receipt, that the exact prior resume terminally failed with no unknown outcome. Consumption is not a Native durable terminal certificate. Unknown or unobserved outcomes must not be asserted as failed.")] bool? priorActionKnownTerminalFailed = null,
        [Description("Optional additional exact legacy source fingerprint for one explicitly formatted V2 predecessor. Requires all three prior-failure fields; binds exactly four fixed consumed-record replicas and two commitment openings. No arbitrary ancestry, path or JSON input.")] string priorLegacySourceFingerprint = "",
        [Description("Optional exact middle source fingerprint for one fixed V3 predecessor. Requires the newest priorSourceFingerprint, oldest priorLegacySourceFingerprint, consumed token and explicit known-terminal-failure attestation. Exactly six fixed replicas and three openings; no fourth generation or arbitrary ancestry.")] string priorIntermediateSourceFingerprint = "",
        [Description("Required only for recoveryMode=reauthorize_failed_remote_last_exit: independently captured exact current remote-context sha256 fingerprint, not a path or JSON. Requires the immediate consumed token and known-terminal-failed caller attestation, with no legacy source witnesses. Active quarantined fixed LastExit only, exact zero-progress landed+1 edge, complete Journal/current primary/original checkpoint binding and charge caps no greater than prior authorization. Fresh disclosure and applicable explicit authority are required; this is new current-context authorization, never an opened old commitment or durable Native terminal certificate.")] string expectedRemoteRecoveryContextFingerprint = "",
        [Description("Optional default-only legacy local-Move outcome_unknown carrier, at most64KiB immutable UTF-8 JSON; no path. Kind/failure/source revision are caller-attested protected public captures, not authenticated Native outcome proof. Exact documented eight-field schema, current quarantined ticket/full LastExit/primary/successful checkpoint/Journal binding and fresh strict adoption remain required. Cannot combine with prior-failure witnesses or another recovery mode.")] string legacyLocalMoveUnknownEvidenceJson = "",
        [Description("Lowercase64hex SHA256 of the exact UTF-8 legacyLocalMoveUnknownEvidenceJson, required together. A matching hash binds bytes, not their truth. MCP withholds a plan unless the Plugin echoes this exact hash and explicit caller-attestation disclosure; commit retains the original unknown result.")] string expectedLegacyLocalMoveUnknownEvidenceHash = "",
        [Description("Only for continue_completed_remote_last_exit: explicit caller attestation backed by the retained original Native receipt that the immediately prior resume terminally completed. It is never inferred from consumption or substituted for failed/unknown. This mode inherits the identical existing absolute window and original origin; it accepts no new deadline/caps and prohibits new charging.")] bool? priorActionKnownTerminalCompleted = null,
        [Description("Only with recoveryMode=open_saved_primary or authorize_remote_primary_recovery: the owner explicitly accepts losing the exact unstored progress between minimumRecoveryGameTick (the current known floor) and expectedRecoveryGameTick (the exact complete primary). No arbitrary older save, automatic fallback or task resumption. For open_saved_primary, days of absence or an expired automation credential/Journey never expire saved-world access; the exact world loads paused, old automation remains stopped and no automatic Save/charging occurs. Without this flag the primary must cover all known progress.")] bool acceptUnstoredProgressLoss = false)
    {
        var request = new PrepareOwnedWorldResumeRequest
        {
            ResumeToken = resumeToken, RecoveryMode = recoveryMode,
            UserConfirmedInConversation = userConfirmedInConversation,
            AcceptUnstoredProgressLoss = acceptUnstoredProgressLoss,
            MinimumRecoveryGameTick = minimumRecoveryGameTick, ExpectedRecoveryGameTick = expectedRecoveryGameTick,
            RecoveryDeadlineUtc = recoveryDeadlineUtc, RecoveryScope = recoveryScope,
            MaximumChargeGameTicks = maximumChargeGameTicks, MaximumChargeWallSeconds = maximumChargeWallSeconds,
            PriorConsumedResumeToken = priorConsumedResumeToken, PriorSourceFingerprint = priorSourceFingerprint,
            PriorActionKnownTerminalFailed = priorActionKnownTerminalFailed,
            PriorActionKnownTerminalCompleted = priorActionKnownTerminalCompleted,
            PriorLegacySourceFingerprint = priorLegacySourceFingerprint,
            PriorIntermediateSourceFingerprint = priorIntermediateSourceFingerprint,
            ExpectedRemoteRecoveryContextFingerprint = expectedRemoteRecoveryContextFingerprint,
            LegacyLocalMoveUnknownEvidenceJson = legacyLocalMoveUnknownEvidenceJson,
            ExpectedLegacyLocalMoveUnknownEvidenceHash = expectedLegacyLocalMoveUnknownEvidenceHash,
        };
        var invalid = OwnedWorldRecoveryPolicy.ValidateRequest(request);
        if (invalid is not null)
        {
            var error = BridgeError.Create(BridgeErrorCodes.InvalidRequest, invalid, false,
                "Do not load a different save or infer conversation confirmation.");
            if (recoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery)
                error.NativeFirstFailure = VoyagerRemotePrimaryRecoveryPolicy.RequestFailure(request, DateTimeOffset.UtcNow);
            return ToToolResult(BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(error), invalid);
        }
        var result = await bridgeClient.PrepareOwnedWorldResumeAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        if (recoveryMode == OwnedWorldResumeModes.OpenSavedPrimary && result.Success
            && (result.Value is null || !OwnedSavedWorldOpenPolicy.HasMatchingEcho(result.Value)
                || result.Value.DiscardedUnstoredProgressGameTicks != (acceptUnstoredProgressLoss
                    ? minimumRecoveryGameTick - expectedRecoveryGameTick : 0)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo saved-world-only opening, stopped automation and exact progress loss; no token is exposed.",
                false, "Use matching Plugin/MCP bytes; opening world data must not renew old task authority."));
        if (recoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery && result.Success
            && (result.Value is null || !VoyagerRemotePrimaryRecoveryPolicy.HasMatchingEcho(request, result.Value)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact newly authorized remote-primary recovery, finite window and approved progress loss; no token is exposed.",
                false, "Use matching Plugin/MCP bytes and confirm this fresh disclosure. Ordinary file access does not grant automatic execution."));
        if (recoveryMode == OwnedWorldResumeModes.ResumeReturnOnlyTask && result.Success
            && VoyagerReturnOnlyContinuationPolicy.EchoFailure(request, result.Value, DateTimeOffset.UtcNow) is { } returnEchoFailure)
        {
            var error = BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact existing return-only task and its no-charge window; no plan token is exposed.",
                false, "Keep the original task and inspect the same-call predicate. Applicable standing owner authority does not need another conversation approval.");
            error.NativeFirstFailure = returnEchoFailure;
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(error);
        }
        if (VoyagerLegacyLocalMoveUnknownPolicy.HasEvidence(request) && result.Success
            && (result.Value is null || !VoyagerLegacyLocalMoveUnknownPolicy.HasMatchingEcho(request, result.Value)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo this exact legacy local-Move-unknown carrier and its caller-attestation trust boundary; no plan token is exposed.", false,
                "Use matching Plugin/MCP bytes. A raw hash is not Native authentication, and the original Move remains unknown."));
        if (recoveryMode == OwnedWorldResumeModes.ContinueCompletedRemoteLastExit && result.Success
            && (result.Value is null || !VoyagerCompletedRemoteContinuationPolicy.HasMatchingEcho(request, result.Value)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not disclose the exact completed predecessor, unchanged inherited window and prohibition on new charging; no token is exposed.",
                false, "Keep the original completed result and use matching verified Plugin/MCP bytes. Never supply new budget to this mode."));
        if (recoveryMode == OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit && result.Success
            && (result.Value is null || !VoyagerFailedRemoteReauthorizationPolicy.HasMatchingEcho(request, result.Value)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact fresh failed-remote-LastExit mode and owner context; no plan token is exposed.", false,
                "Use matching Plugin/MCP bytes. Prior consumption is not a Native terminal certificate or an opened legacy commitment."));
        if (recoveryMode != OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit
            && recoveryMode != OwnedWorldResumeModes.ContinueCompletedRemoteLastExit
            && OwnedWorldRecoveryPolicy.HasPriorFailedResumeWitness(request) && result.Success
            && (result.Value is not { } prior || !prior.Prepared || prior.RecoveryMode != OwnedWorldResumeModes.Default
                || !prior.PriorFailedResumeProvenanceUsed || !prior.PriorActionTerminalFailureAttested
                || prior.CommitAllowedNow || !prior.UserConfirmationRequired || !prior.ExactEmbeddedIdentityVerified
                || prior.RecoveryEvidenceVersion < 1 || string.IsNullOrEmpty(prior.ConfirmationDigest)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact prior failed-resume provenance and caller attestation; no plan token is exposed.", false,
                "Use matching Plugin/MCP bytes. A consumed record alone does not prove a terminal outcome."));
        if (!string.IsNullOrEmpty(priorLegacySourceFingerprint) && result.Success
            && result.Value?.PriorFailedResumeV2ProvenanceUsed != true)
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact fixed V2 predecessor proof; no plan token is exposed.", false,
                "Use matching Plugin/MCP bytes. Do not commit a plan that ignored the extra witness."));
        if (!string.IsNullOrEmpty(priorIntermediateSourceFingerprint) && result.Success
            && result.Value?.PriorFailedResumeV3ProvenanceUsed != true)
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact fixed V3 predecessor proof; no plan token is exposed.", false,
                "Use matching Plugin/MCP bytes. Do not commit a plan that ignored the intermediate witness."));
        if (recoveryDeadlineUtc.HasValue && recoveryMode != OwnedWorldResumeModes.ResumeReturnOnlyTask
            && result.Success && (result.Value is not { } window
            || window.EffectiveRecoveryDeadlineUtc != recoveryDeadlineUtc || !window.OriginalJourneyDeadlineUtc.HasValue
            || window.RecoveryScope != recoveryScope || window.MaximumChargeGameTicks != maximumChargeGameTicks
            || window.MaximumChargeWallSeconds != maximumChargeWallSeconds || !window.NativeChargeFeasibilityRequiresFreshCapture
            || !window.Prepared || window.RecoveryMode != recoveryMode
            || (recoveryMode != OwnedWorldResumeModes.Default && recoveryMode != OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit
                && recoveryMode != OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery)
            || window.CommitAllowedNow
            || string.IsNullOrEmpty(window.ConfirmationDigest) || !window.UserConfirmationRequired
            || !window.ExactEmbeddedIdentityVerified || window.RecoveryEvidenceVersion < 1))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact one-shot remote recovery window; no plan token is exposed.", false,
                "Install matching Plugin/MCP bytes and prepare again. Do not commit a default plan that ignored these fields."));
        if (recoveryMode == OwnedWorldResumeModes.VerifiedNewerLastExit && result.Success
            && (result.Value is null || !OwnedWorldRecoveryPolicy.HasMatchingEcho(request, result.Value)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not confirm the exact verified recovery mode, identity and candidate tick; no token is exposed.",
                false, "Install a matching Plugin/MCP cohort and prepare again; never commit an old Plugin's default-primary plan."));
        if (recoveryMode == OwnedWorldResumeModes.ReauthorizeExpiredPrimary && result.Success
            && (result.Value is null || !OwnedWorldReauthorizationPolicy.HasMatchingEcho(result.Value)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact expired-primary disclosure and confirmation binding; no token is exposed.",
                false, "Use a matching cohort. Do not commit another mode or fabricate confirmation."));
        if (recoveryMode == OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary && result.Success
            && (result.Value is null || !OwnedWorldPrimaryMigrationPolicy.HasMatchingEcho(result.Value)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact active-primary migration, version5 disclosure and confirmation binding; no token is exposed.",
                false, "Use a matching cohort and confirm the prepared disclosure before commit."));
        if (OwnedWorldAutosaveRecoveryPolicy.IsFixedAutosaveMode(recoveryMode) && result.Success
            && (result.Value is null || !OwnedWorldAutosaveRecoveryPolicy.HasMatchingEcho(request, result.Value)))
            result = BridgeCallResult<PreparedOwnedWorldResumePlan>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact fixed-AutoSave0 mode/versioned disclosure, identity, candidate tick and same-version binding; no token is exposed.",
                false, "Use a matching cohort. Do not commit another mode or fabricate confirmation."));
        return ToToolResult(result, "One-time exact owned-world resume plan prepared; no save was loaded or enumerated.");
    }

    [McpServerTool(
        Name = "spherewright_commit_resume_owned_game",
        Title = "Resume the exact normally closed owned world",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("The default MCP preparation selects saved-world-only opening: commit opens the exact owned primary paused without resuming old automation or resaving. The explicit legacy default healthy planned restart mode loads only the exact ticket-bound primary through DSPGame.StartGame. Real quarantine may load a qualifying fixed LastExit. Verified-newer LastExit rechecks its authorized tick, identity and full-file evidence under a read-only lease; drift rejects without fallback. Fixed AutoSave0 uses evidence version3 with expired provenance or version4 with an active healthy credential. Both require explicit conversation authority, the exact prepared digest, newer tick, identity, peaceful state, original durable Journal and the same supported source/runtime/candidate version. An existing explicit grant matching the active-mode candidate needs no repeated question. Consumption is durable before loading; ticket expiry is never edited. Autosave modes never migrate or fall back. Version-changed-primary uses evidence5 for active healthy29104 to29131 or evidence6 for active healthy29131 to29145 exact-primary provenance. Both require subsequent explicit confirmation and the exact pair-specific digest before durable consumption/native loading and the corresponding third or fourth Journal transition. All paths revalidate native menu readiness; adoption must prove owned identity, planet and Journal continuity before primary resave. Sandbox/resource multiplier are evidence, not gates. Poll the unique action to terminal, then fresh-read owned/saved/healthy state, save tick and durable Journal; reconcile successful unsaved work, never replay it. Never replay an accepted resume or choose another save to satisfy a mistaken readiness wait.")]
    public static async Task<CallToolResult> CommitOwnedWorldResumeAsync(
        IBridgeClient bridgeClient,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default,
        [Description("Only for protected reauthorization modes. Expired and version-changed-primary modes require subsequent explicit confirmation of the prepared disclosure. Active fixed AutoSave0 accepts an existing explicit conversation grant only if it matches this exact disclosed candidate; never infer authority from generic development/continue requests.")] bool userConfirmedInConversation = false,
        [Description("Echo this exact plan's confirmationDigest under the required explicit conversation authority. This is an Agent protocol echo, not a code the user must type. Empty for non-reauthorization modes.")] string confirmationDigest = "")
    {
        var result = await bridgeClient.CommitOwnedWorldResumeAsync(
            new CommitOwnedWorldResumeRequest
            {
                PlanToken = planToken,
                IdempotencyKey = idempotencyKey,
                UserConfirmedInConversation = userConfirmedInConversation,
                ConfirmationDigest = confirmationDigest,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "DSP accepted the exact protected owned-world resume; poll actionId for provenance validation and high-entropy resave.");
    }

    [McpServerTool(
        Name = "spherewright_prepare_reload_flight_checkpoint",
        Title = "Prepare the exact reusable pre-flight checkpoint",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Validates only the protected checkpoint bound to the supplied high-entropy token; never accepts save names or loads during prepare. Optional returnOnlyTask is a separately owner-approved interrupted-return recovery, maximum1200wall seconds/54000game ticks, retaining all original accounting and resource floors; commit needs explicit conversation confirmation.")]
    public static async Task<CallToolResult> PrepareFlightCheckpointReloadAsync(
        IBridgeClient bridgeClient,
        string reloadToken,
        CancellationToken cancellationToken = default,
        VoyagerReturnOnlyRecoveryRequest? returnOnlyTask = null)
    {
        if (returnOnlyTask is not null && VoyagerReturnOnlyRecoveryPolicy.RequestFailure(returnOnlyTask, DateTimeOffset.UtcNow) is { } invalid)
        {
            var error = BridgeError.Create(invalid.Code, "The new return-only recovery request is invalid: " + invalid.FirstFailedPredicate,
                false, "Use the exact owner-approved checkpoint and finite task. No load was dispatched.");
            error.NativeFirstFailure = invalid;
            return ToToolResult(BridgeCallResult<PreparedFlightCheckpointReloadPlan>.Failed(error), error.Message);
        }
        var result = await bridgeClient.PrepareFlightCheckpointReloadAsync(
            new PrepareFlightCheckpointReloadRequest { ReloadToken = reloadToken, ReturnOnlyTask = returnOnlyTask },
            cancellationToken).ConfigureAwait(false);
        if (returnOnlyTask is not null && result.Success && (result.Value is null
            || !VoyagerReturnOnlyRecoveryPolicy.HasMatchingEcho(returnOnlyTask, result.Value)))
            result = BridgeCallResult<PreparedFlightCheckpointReloadPlan>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not echo the exact separate return-only task; no plan token is exposed.", false,
                "Use matching qualified Plugin/MCP bytes and preserve the original checkpoint."));
        return ToToolResult(result, "Exact reusable pre-flight checkpoint reload prepared; no save was loaded.");
    }

    [McpServerTool(
        Name = "spherewright_commit_reload_flight_checkpoint",
        Title = "Reload the exact reusable pre-flight checkpoint",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Interrupts only its bound flight attempt, then loads the internally named checkpoint through DSPGame.StartGame. Adoption requires its exact saved tick, embedded primary owned-save identity, origin planet, and peaceful state; sandbox state and resource multiplier are preserved and reported but do not gate adoption, and the ticket remains reusable for another failed attempt.")]
    public static async Task<CallToolResult> CommitFlightCheckpointReloadAsync(
        IBridgeClient bridgeClient,
        string planToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default,
        bool userConfirmedInConversation = false)
    {
        var result = await bridgeClient.CommitFlightCheckpointReloadAsync(
            new CommitFlightCheckpointReloadRequest
            {
                PlanToken = planToken,
                IdempotencyKey = idempotencyKey,
                UserConfirmedInConversation = userConfirmedInConversation,
            },
            cancellationToken).ConfigureAwait(false);
        return ToToolResult(result, "DSP accepted the exact reusable pre-flight checkpoint; poll actionId until provenance validation completes.");
    }

    private static CommitNormalActionRequest CreateCommitRequest(
        string sessionId,
        int planetId,
        string planToken,
        string idempotencyKey)
    {
        return new CommitNormalActionRequest
        {
            SessionId = sessionId,
            PlanetId = planetId,
            PlanToken = planToken,
            IdempotencyKey = idempotencyKey,
        };
    }

    private static Vector3Snapshot? CreateOptionalVector(float? x, float? y, float? z)
    {
        if (!x.HasValue && !y.HasValue && !z.HasValue)
        {
            return null;
        }

        if (!x.HasValue || !y.HasValue || !z.HasValue)
        {
            throw new ArgumentException("All three vector coordinates must be supplied together.");
        }

        return new Vector3Snapshot { X = x.Value, Y = y.Value, Z = z.Value };
    }

    private static CallToolResult ToToolResult<T>(BridgeCallResult<T> result, string successText)
    {
        var payload = new SpherewrightToolResult<T>
        {
            Success = result.Success,
            Result = result.Value,
            Error = result.Error,
        };
        var text = result.Success
            ? successText
            : $"{result.Error!.Code}: {result.Error.Message} Recovery: {result.Error.Recovery}";

        return new CallToolResult
        {
            IsError = !result.Success,
            StructuredContent = JsonSerializer.SerializeToElement(payload, JsonOptions),
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = text },
            },
        };
    }
}
