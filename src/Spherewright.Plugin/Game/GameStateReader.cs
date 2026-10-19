using System.Security.Cryptography;
using System.Text;
using Spherewright.Bridge.Core.Diagnostics;
using Spherewright.Bridge.Core.Logistics;
using Spherewright.Bridge.Core.Progression;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Snapshots;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Diagnostics;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Power;
using Spherewright.Contracts.Progression;
using Spherewright.Contracts.Resources;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;
    private const int DefaultOverseerPlanetLimit = 8;
    private const int MaximumOverseerPlanetLimit = 16;
    private const int MaximumOverseerItemCount = 64;
    private const int MaximumOverseerFactoryCount = 512;
    private const int MaximumOverseerPowerNetworkScanCount = 4096;
    private const int MaximumOverseerPowerGeneratorScanCount = 65536;
    private const int MaximumOverseerStationScanCount = 4096;
    private const int MaximumOverseerNetworkDetailsPerPlanet = 64;
    private const int MaximumOverseerStationStorageSlots = 64;
    private const int MaximumOverseerTechQueueCount = 64;
    private const int MaximumOverseerTechQueueScanCount = 4096;
    private const int MaximumOverseerTechnologyScanCount = 12000;
    private const int MaximumOverseerTheoreticalComponentScanCount = 131072;
    private const int MaximumOverseerTheoreticalSourceReferenceScanCount = 262144;
    private const int MaximumOverseerDirectDiagnosticComponentScanCount = 131072;
    private const int MaximumOverseerDirectDiagnosticSourceReferenceScanCount = 262144;
    private const int MaximumOverseerDirectFindingsPerItem = 16;
    private const int MaximumOverseerInfrastructureFindingsPerPlanet = 16;
    private const int OverseerSnapshotScopeId = int.MaxValue;
    private readonly GameSessionTracker _sessions;
    private readonly OverseerLogisticsProgressStore _overseerLogisticsProgressStore;
    private readonly GovernorDeclarationStore _governorDeclarationStore;
    internal PassiveDriftMoveInspection? LastPassiveDriftMoveInspection { get; private set; }

    internal static double PassiveDriftClockSeconds() =>
        System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
    private readonly SnapshotPageStore<ResourceNodeSnapshot> _resourceSnapshots =
        new SnapshotPageStore<ResourceNodeSnapshot>(TimeSpan.FromSeconds(60), 16);
    private readonly SnapshotPageStore<FactoryEntitySnapshot> _factorySnapshots =
        new SnapshotPageStore<FactoryEntitySnapshot>(TimeSpan.FromSeconds(60), 16);
    private readonly SnapshotPageStore<AssemblerSnapshot> _assemblerSnapshots =
        new SnapshotPageStore<AssemblerSnapshot>(TimeSpan.FromSeconds(60), 16);
    private readonly SnapshotPageStore<OverseerPlanetProductionSnapshot> _overseerProductionSnapshots =
        new SnapshotPageStore<OverseerPlanetProductionSnapshot>(TimeSpan.FromSeconds(60), 8);
    private readonly SnapshotPageStore<OverseerSummaryPageEntry> _overseerSummarySnapshots =
        new SnapshotPageStore<OverseerSummaryPageEntry>(TimeSpan.FromSeconds(60), 8);
    private readonly SnapshotPageStore<OverseerDiagnosticBundlePageEntry> _overseerDiagnosticBundleSnapshots =
        new SnapshotPageStore<OverseerDiagnosticBundlePageEntry>(TimeSpan.FromSeconds(60), 8);

    public GameStateReader(
        GameSessionTracker sessions,
        OverseerLogisticsProgressStore overseerLogisticsProgressStore,
        GovernorDeclarationStore governorDeclarationStore)
    {
        _sessions = sessions;
        _overseerLogisticsProgressStore = overseerLogisticsProgressStore;
        _governorDeclarationStore = governorDeclarationStore;
    }

    public GameCallResult<SessionState> GetSessionStateOnMainThread()
    {
        return GameCallResult<SessionState>.Succeeded(_sessions.CaptureOnMainThread());
    }

    public GameCallResult<PlayerStateSnapshot> GetPlayerStateOnMainThread(
        string? requestedSessionId,
        LocalPlanetRequest request,
        bool captureMoveInspection = false)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<PlayerStateSnapshot>.Failed(accessError);
        }

        var player = GameMain.mainPlayer;
        if (player?.package is null || player.mecha is null)
        {
            return GameCallResult<PlayerStateSnapshot>.Failed(NotReady(
                "The player inventory or mecha is not ready in the owned ordinary world."));
        }

        var result = new PlayerStateSnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory!.planetId,
            CapturedAtGameTick = GameMain.gameTick,
            Position = CaptureVector(player.position),
            MovementState = player.movementState.ToString(),
            IsAlive = player.isAlive,
            IsOnPlanet = player.planetId == factory.planetId,
            IsFlying = player.movementState == EMovementState.Fly,
            IsSailing = player.movementState >= EMovementState.Sail,
            Speed = player.speed,
            CoreEnergy = player.mecha.coreEnergy,
            CoreEnergyCapacity = player.mecha.coreEnergyCap,
            ReactorEnergy = player.mecha.reactorEnergy,
            ReactorItemId = player.mecha.reactorItemId,
            ReactorItemName = player.mecha.reactorItemId > 0 ? GetItemName(player.mecha.reactorItemId) : null,
            ReactorItemInc = player.mecha.reactorItemInc,
            AutoReplenishFuel = player.mecha.autoReplenishFuel,
            FuelStorageSlotCount = player.mecha.reactorStorage?.size ?? 0,
            BuildArea = player.mecha.buildArea,
            InventorySlotCount = player.package.size,
            AutoManageResearchItems = GameMain.history?.autoManageLabItems ?? false,
            MechaResearchPower = player.mecha.researchPower,
            NativePhysics = CaptureNativePlayerPhysicsOnMainThread(player),
            NativeLocalControlEnergy = NormalGameActionCoordinator.CaptureConditionalNativeLocalControlEnergy(player),
        };

        var inventory = new Dictionary<int, PlayerInventoryItem>();
        var packageGrids = player.package.grids ?? Array.Empty<StorageComponent.GRID>();
        var gridCount = Math.Min(player.package.size, packageGrids.Length);
        for (var index = 0; index < gridCount; index++)
        {
            var grid = packageGrids[index];
            if (grid.itemId <= 0 || grid.count <= 0)
            {
                continue;
            }

            result.InventoryOccupiedSlotCount++;
            if (!inventory.TryGetValue(grid.itemId, out var entry))
            {
                entry = new PlayerInventoryItem
                {
                    ItemId = grid.itemId,
                    Name = GetItemName(grid.itemId),
                };
                inventory.Add(grid.itemId, entry);
            }

            entry.Count += grid.count;
            entry.Inc += grid.inc;
            entry.SlotCount++;
        }

        result.Inventory = inventory.Values.OrderBy(item => item.ItemId).ToList();
        var fuelInventory = new Dictionary<int, PlayerInventoryItem>();
        var fuelStorage = player.mecha.reactorStorage;
        var fuelGrids = fuelStorage?.grids ?? Array.Empty<StorageComponent.GRID>();
        var fuelGridCount = Math.Min(fuelStorage?.size ?? 0, fuelGrids.Length);
        for (var index = 0; index < fuelGridCount; index++)
        {
            var grid = fuelGrids[index];
            if (grid.itemId <= 0 || grid.count <= 0)
            {
                continue;
            }

            result.FuelStorageOccupiedSlotCount++;
            if (!fuelInventory.TryGetValue(grid.itemId, out var entry))
            {
                entry = new PlayerInventoryItem
                {
                    ItemId = grid.itemId,
                    Name = GetItemName(grid.itemId),
                };
                fuelInventory.Add(grid.itemId, entry);
            }

            entry.Count += grid.count;
            entry.Inc += grid.inc;
            entry.SlotCount++;
        }

        result.FuelStorage = fuelInventory.Values.OrderBy(item => item.ItemId).ToList();
        if (player.inhandItemId > 0 && player.inhandItemCount > 0)
        {
            result.InHandItem = new PlayerInventoryItem
            {
                ItemId = player.inhandItemId,
                Name = GetItemName(player.inhandItemId),
                Count = player.inhandItemCount,
                Inc = player.inhandItemInc,
                SlotCount = 1,
            };
        }

        var researchItems = player.mecha.lab?.itemPoints?.items;
        if (researchItems is not null)
        {
            foreach (var item in researchItems.OrderBy(item => item.Key))
            {
                if (item.Key <= 0 || item.Value <= 0)
                {
                    continue;
                }

                result.MechaResearchItemBuffer.Add(new MechaResearchItemSnapshot
                {
                    ItemId = item.Key,
                    Name = GetItemName(item.Key),
                    PointCount = item.Value,
                    WholeItemCount = item.Value / 3600,
                    RemainderPoints = item.Value % 3600,
                });
            }
        }

        var forgeTasks = player.mecha.forge?.tasks;
        if (forgeTasks is not null)
        {
            for (var index = 0; index < forgeTasks.Count; index++)
            {
                var task = forgeTasks[index];
                if (task is null)
                {
                    continue;
                }

                var taskSnapshot = new HandcraftTaskSnapshot
                {
                    QueueIndex = index,
                    RecipeId = task.recipeId,
                    RecipeName = LDB.recipes.Select(task.recipeId)?.name ?? string.Empty,
                    RemainingCraftCount = task.count,
                    Progress = task.tick,
                    ProgressRequired = task.tickSpend,
                    ParentTaskIndex = task.parentTaskIndex,
                    IngredientsReserved = task.itemEnough,
                };
                AddPlayerItemAmounts(taskSnapshot.Inputs, task.itemIds, task.itemCounts, task.served);
                AddPlayerItemAmounts(taskSnapshot.Outputs, task.productIds, task.productCounts, task.produced);
                result.HandcraftQueue.Add(taskSnapshot);
            }
        }

        var construction = player.mecha.constructionModule;
        if (construction is not null)
        {
            result.ConstructionDrones = new ConstructionDroneSnapshot
            {
                Enabled = construction.droneEnabled,
                ConstructionEnabled = construction.droneConstructEnabled,
                Total = construction.droneCount,
                Alive = construction.droneAliveCount,
                Idle = construction.droneIdleCount,
                Working = Math.Max(0, construction.droneAliveCount - construction.droneIdleCount),
                PendingBuildTargets = construction.buildTargetTotalCount,
                PendingRepairTargets = construction.repairTargetTotalCount,
            };
        }

        // The public action fingerprint must remain stable while DSP performs
        // ordinary per-tick energy recharge. Exact energy is still returned in
        // the snapshot and captured separately by action readback, but it is
        // not a safe optimistic-concurrency field: including it makes every
        // prepare stale on the next Unity frame after the first energy use.
        result.StateHash = CanonicalStateHash.PlayerAction(result);
        result.StateHashVersion = CanonicalStateHash.Version;
        if (captureMoveInspection)
            LastPassiveDriftMoveInspection = player.currentOrder is null
                ? PassiveDriftMoveInspection.Capture(result, PassiveDriftClockSeconds(), DateTimeOffset.UtcNow)
                : null;
        return GameCallResult<PlayerStateSnapshot>.Succeeded(result);
    }

    public GameCallResult<ProgressionStateSnapshot> GetProgressionStateOnMainThread(
        string? requestedSessionId,
        LocalPlanetRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<ProgressionStateSnapshot>.Failed(accessError);
        }

        var history = GameMain.history;
        if (history?.techStates is null || history.techQueue is null)
        {
            return GameCallResult<ProgressionStateSnapshot>.Failed(NotReady(
                "The technology state is not ready in the owned ordinary world."));
        }

        var queue = history.techQueue.Where(techId => techId > 0).ToList();
        var result = new ProgressionStateSnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory!.planetId,
            CapturedAtGameTick = GameMain.gameTick,
            CurrentTechId = history.currentTech,
            CurrentTechName = LDB.techs.Select(history.currentTech)?.name,
            TechQueue = queue,
        };

        foreach (var tech in LDB.techs.dataArray.OrderBy(proto => proto.ID))
        {
            if (tech is null || !history.techStates.TryGetValue(tech.ID, out var state))
            {
                continue;
            }

            var snapshot = new TechStateSnapshot
            {
                TechId = tech.ID,
                Name = tech.name ?? string.Empty,
                Unlocked = state.unlocked,
                CurrentLevel = state.curLevel,
                MaximumLevel = state.maxLevel,
                HashUploaded = state.hashUploaded,
                HashRequired = state.hashNeeded,
                UnlockTick = state.unlockTick,
                IsLabTech = tech.IsLabTech,
                IsQueued = queue.Contains(tech.ID),
                PrerequisiteTechIds = (tech.PreTechs ?? Array.Empty<int>())
                    .Concat(tech.PreTechsImplicit ?? Array.Empty<int>())
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList(),
                UnlockRecipeIds = (tech.UnlockRecipes ?? Array.Empty<int>()).OrderBy(id => id).ToList(),
                CompletionItemRewards = TechCompletionRewardCatalog.Capture(
                    tech.AddItems, tech.AddItemCounts, itemId => LDB.items.Select(itemId)?.name),
            };

            var techItems = tech.Items ?? Array.Empty<int>();
            var techItemPoints = tech.ItemPoints ?? Array.Empty<int>();
            var itemCount = Math.Min(techItems.Length, techItemPoints.Length);
            for (var index = 0; index < itemCount; index++)
            {
                var itemId = techItems[index];
                var requiredItemCount = state.hashNeeded * techItemPoints[index] / TechProto.kPointPerItem;
                var isMatrix = TechProto.matrixIds.Contains(itemId);
                snapshot.ItemRequirements.Add(new TechItemRequirement
                {
                    ItemId = itemId,
                    Name = GetItemName(itemId),
                    PointsPerHash = techItemPoints[index],
                    RequiredItemCount = requiredItemCount,
                    IsMatrix = isMatrix,
                });
                if (isMatrix)
                {
                    snapshot.MatrixRequirements.Add(new TechMatrixRequirement
                    {
                        ItemId = itemId,
                        Name = GetItemName(itemId),
                        PointsPerHash = techItemPoints[index],
                        RequiredItemCount = requiredItemCount,
                    });
                }
            }

            result.Technologies.Add(snapshot);
        }

        result.StateHash = CanonicalStateHash.Progression(result);
        result.StateHashVersion = CanonicalStateHash.Version;
        result.SelectionStateHash = CanonicalStateHash.ProgressionSelection(result);
        result.SelectionStateHashVersion = CanonicalStateHash.Version;
        return GameCallResult<ProgressionStateSnapshot>.Succeeded(result);
    }

    public GameCallResult<RecipeCatalogSnapshot> GetRecipeCatalogOnMainThread(
        string? requestedSessionId,
        LocalPlanetRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<RecipeCatalogSnapshot>.Failed(accessError);
        }

        var history = GameMain.history;
        if (history is null)
        {
            return GameCallResult<RecipeCatalogSnapshot>.Failed(NotReady(
                "The runtime item and recipe catalog is not ready in the owned ordinary world."));
        }

        var result = new RecipeCatalogSnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory!.planetId,
            CapturedAtGameTick = GameMain.gameTick,
        };

        foreach (var item in LDB.items.dataArray.OrderBy(proto => proto.ID))
        {
            if (item is null)
            {
                continue;
            }

            result.Items.Add(new ItemCatalogEntry
            {
                ItemId = item.ID,
                Name = item.name ?? string.Empty,
                StackSize = item.StackSize,
                IsRaw = item.isRaw,
                CanBuild = item.CanBuild,
                Unlocked = history.ItemUnlocked(item.ID),
                HandcraftRecipeId = item.handcraft?.ID,
                FuelHeatValueJoules = item.HeatValue >= 0 ? (long?)item.HeatValue : null,
                FuelType = item.FuelType >= 0 ? (int?)item.FuelType : null,
                AcceptedAsMechaFuel = StorageComponent.itemIsFuel is not null && item.ID > 0 && item.ID < StorageComponent.itemIsFuel.Length
                    ? (bool?)OrdinaryMechaFuelPolicy.IsAccepted(item.HeatValue, item.FuelType, StorageComponent.itemIsFuel[item.ID]) : null,
            });
        }

        foreach (var recipe in LDB.recipes.dataArray.OrderBy(proto => proto.ID))
        {
            if (recipe is null)
            {
                continue;
            }

            var entry = new RecipeCatalogEntry
            {
                RecipeId = recipe.ID,
                Name = recipe.name ?? string.Empty,
                RecipeType = recipe.Type.ToString(),
                Handcraft = recipe.Handcraft,
                Unlocked = history.RecipeUnlocked(recipe.ID),
                TimeSpend = recipe.TimeSpend,
                PrerequisiteTechId = recipe.preTech?.ID,
                PrerequisiteTechName = recipe.preTech?.name,
            };
            AddCatalogAmounts(entry.Inputs, recipe.Items, recipe.ItemCounts);
            AddCatalogAmounts(entry.Outputs, recipe.Results, recipe.ResultCounts);
            result.Recipes.Add(entry);
        }

        var redMatrixItemId = TechProto.matrixIds.Length > 1 ? TechProto.matrixIds[1] : 0;
        result.FirstRedMatrixDependencies = RuntimeDependencyGraphBuilder.Build(
            redMatrixItemId,
            GetItemName(redMatrixItemId),
            result.Recipes);
        return GameCallResult<RecipeCatalogSnapshot>.Succeeded(result);
    }

    public GameCallResult<ListResourceNodesResult> ListResourceNodesOnMainThread(
        string? requestedSessionId,
        ListResourceNodesRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<ListResourceNodesResult>.Failed(accessError);
        }

        var limitError = ValidateListLimit(request.Limit, "Resource-node");
        if (limitError is not null)
        {
            return GameCallResult<ListResourceNodesResult>.Failed(limitError);
        }

        var normalizedKind = NormalizeOptional(request.Kind);
        if (normalizedKind is not null
            && normalizedKind != ResourceNodeKinds.Vein
            && normalizedKind != ResourceNodeKinds.Vegetation)
        {
            return GameCallResult<ListResourceNodesResult>.Failed(InvalidRequest(
                "Resource kind must be vein, vegetation, or empty.",
                "Use a resource kind returned by this tool."));
        }

        var normalizedType = NormalizeOptional(request.ResourceType);
        var filterHash = ComputeFilterHash(
            $"kind={normalizedKind ?? "*"}|type={normalizedType ?? "*"}|product={request.ProductItemId?.ToString() ?? "*"}|limit={request.Limit}");
        SnapshotPage<ResourceNodeSnapshot>? page;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var status = _resourceSnapshots.TryGetPage(
                request.Cursor,
                _sessions.SessionId!,
                factory!.planetId,
                filterHash,
                request.Limit,
                out page);
            if (status != SnapshotCursorStatus.Success || page is null)
            {
                return GameCallResult<ListResourceNodesResult>.Failed(StaleCursor(
                    "The resource cursor is unknown, expired, or bound to a different session, planet, filter, or page size."));
            }
        }
        else
        {
            var nodes = CaptureResourceNodes(factory!, normalizedKind, normalizedType, request.ProductItemId);
            if (!_resourceSnapshots.TryCreate(
                    _sessions.SessionId!,
                    factory!.planetId,
                    filterHash,
                    nodes,
                    request.Limit,
                    out page)
                || page is null)
            {
                return GameCallResult<ListResourceNodesResult>.Failed(SnapshotCapacityExceeded("resource-node"));
            }
        }

        return GameCallResult<ListResourceNodesResult>.Succeeded(new ListResourceNodesResult
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory!.planetId,
            CapturedAtGameTick = page.Items.Count > 0 ? page.Items[0].CapturedAtGameTick : GameMain.gameTick,
            SnapshotId = page.SnapshotId,
            SnapshotExpiresAtUtc = page.ExpiresAtUtc,
            Nodes = page.Items.ToList(),
            NextCursor = page.NextCursor,
        });
    }

    public GameCallResult<ResourceNodeSnapshot> InspectResourceNodeOnMainThread(
        string? requestedSessionId,
        InspectResourceNodeRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<ResourceNodeSnapshot>.Failed(accessError);
        }

        if (request.NodeId <= 0)
        {
            return InvalidResource("The requested resource node ID must be positive.");
        }

        ResourceNodeSnapshot? snapshot;
        if (string.Equals(request.Kind, ResourceNodeKinds.Vein, StringComparison.OrdinalIgnoreCase))
        {
            snapshot = request.NodeId < factory!.veinCursor
                ? TryCaptureVein(factory, request.NodeId)
                : null;
        }
        else if (string.Equals(request.Kind, ResourceNodeKinds.Vegetation, StringComparison.OrdinalIgnoreCase))
        {
            snapshot = request.NodeId < factory!.vegeCursor
                ? TryCaptureVegetation(factory, request.NodeId)
                : null;
        }
        else
        {
            return GameCallResult<ResourceNodeSnapshot>.Failed(InvalidRequest(
                "Resource kind must be vein or vegetation.",
                "Use the kind and nodeId returned by spherewright_list_resource_nodes."));
        }

        return snapshot is null
            ? InvalidResource("The requested resource node no longer exists in the local factory.")
            : GameCallResult<ResourceNodeSnapshot>.Succeeded(snapshot);
    }

    public GameCallResult<ListFactoryEntitiesResult> ListFactoryEntitiesOnMainThread(
        string? requestedSessionId,
        ListFactoryEntitiesRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<ListFactoryEntitiesResult>.Failed(accessError);
        }

        var limitError = ValidateListLimit(request.Limit, "Factory-entity");
        if (limitError is not null)
        {
            return GameCallResult<ListFactoryEntitiesResult>.Failed(limitError);
        }

        var normalizedObjectKind = NormalizeOptional(request.ObjectKind);
        if (normalizedObjectKind is not null
            && normalizedObjectKind != FactoryObjectKinds.Entity
            && normalizedObjectKind != FactoryObjectKinds.Prebuild)
        {
            return GameCallResult<ListFactoryEntitiesResult>.Failed(InvalidRequest(
                "Factory object kind must be entity, prebuild, or empty.",
                "Use an object kind returned by this tool."));
        }

        var normalizedComponentKind = NormalizeOptional(request.ComponentKind);
        var filterHash = ComputeFilterHash(
            $"object={normalizedObjectKind ?? "*"}|component={normalizedComponentKind ?? "*"}|item={request.ItemId?.ToString() ?? "*"}|limit={request.Limit}");
        SnapshotPage<FactoryEntitySnapshot>? page;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var status = _factorySnapshots.TryGetPage(
                request.Cursor,
                _sessions.SessionId!,
                factory!.planetId,
                filterHash,
                request.Limit,
                out page);
            if (status != SnapshotCursorStatus.Success || page is null)
            {
                return GameCallResult<ListFactoryEntitiesResult>.Failed(StaleCursor(
                    "The factory cursor is unknown, expired, or bound to a different session, planet, filter, or page size."));
            }
        }
        else
        {
            var entities = CaptureFactoryEntities(
                factory!,
                normalizedObjectKind,
                normalizedComponentKind,
                request.ItemId);
            if (!_factorySnapshots.TryCreate(
                    _sessions.SessionId!,
                    factory!.planetId,
                    filterHash,
                    entities,
                    request.Limit,
                    out page)
                || page is null)
            {
                return GameCallResult<ListFactoryEntitiesResult>.Failed(SnapshotCapacityExceeded("factory-entity"));
            }
        }

        return GameCallResult<ListFactoryEntitiesResult>.Succeeded(new ListFactoryEntitiesResult
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory!.planetId,
            CapturedAtGameTick = page.Items.Count > 0 ? page.Items[0].CapturedAtGameTick : GameMain.gameTick,
            SnapshotId = page.SnapshotId,
            SnapshotExpiresAtUtc = page.ExpiresAtUtc,
            Entities = page.Items.ToList(),
            NextCursor = page.NextCursor,
        });
    }

    public GameCallResult<FactoryEntitySnapshot> InspectFactoryEntityOnMainThread(
        string? requestedSessionId,
        InspectFactoryEntityRequest request) =>
        InspectFactoryEntityOnMainThread(requestedSessionId, request, allowCurrentStarRemoteReadOnly: false);

    internal GameCallResult<FactoryEntitySnapshot> InspectFactoryEntityOnMainThread(
        string? requestedSessionId,
        InspectFactoryEntityRequest request,
        bool allowCurrentStarRemoteReadOnly)
    {
        // Only the direct read-only Bridge inspection route opts into this scope.
        // Action-coordinator calls retain the two-argument, strictly local overload.
        var localPlanetId = GameMain.localPlanet?.id ?? 0;
        PlanetFactory? factory;
        var accessError = request.OwnedRemoteLogisticsStation
            ? ValidateOwnedResidentStationReadOnMainThread(requestedSessionId, request, allowCurrentStarRemoteReadOnly, out factory)
            : allowCurrentStarRemoteReadOnly && request.PlanetId != localPlanetId
                ? ValidateOwnedCurrentStarFactoryReadOnMainThread(requestedSessionId, request.PlanetId, out factory)
                : ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out factory);
        if (accessError is not null)
        {
            return GameCallResult<FactoryEntitySnapshot>.Failed(accessError);
        }

        // Keep the owned boundary first. A missing/incorrectly named ID is an
        // invalid request, not evidence that a previously observed building vanished.
        var idError = FactoryObjectReadPolicy.ValidateObjectId(request.ObjectId);
        if (idError is not null) return GameCallResult<FactoryEntitySnapshot>.Failed(idError);
        var selectionError = MaterialInventoryCutPolicy.ValidateSelection(request.MaterialInventoryObjectIds);
        if (selectionError is not null) return GameCallResult<FactoryEntitySnapshot>.Failed(selectionError);
        var geometryError = FactoryGeometryReadPolicy.ValidateRequest(request.Geometry);
        if (geometryError is not null) return GameCallResult<FactoryEntitySnapshot>.Failed(InvalidRequest(geometryError,"Use explicit bounded local geometry selections."));
        if (request.Geometry is not null && request.PlanetId != localPlanetId)
            return GameCallResult<FactoryEntitySnapshot>.Failed(InvalidRequest("factory_geometry_current_local_planet_only","Read geometry only in the currently loaded local factory."));

        FactoryEntitySnapshot? snapshot = null;
        if (request.ObjectId > 0 && request.ObjectId < factory!.entityCursor)
        {
            snapshot = TryCaptureFactoryEntity(factory, request.ObjectId);
            if (snapshot is not null)
                snapshot.SorterEndpoints = CaptureSorterEndpoints(factory, request.ObjectId);
            if (snapshot?.ComponentKind == "belt")
                snapshot.BeltCargo = CaptureBeltCargo(factory, request.ObjectId);
            if (snapshot?.ComponentKind == "power-generator")
                snapshot.FuelPowerState = CaptureFuelPowerState(factory, request.ObjectId);
            if (allowCurrentStarRemoteReadOnly && snapshot?.LogisticsStation is not null)
                snapshot.LogisticsStation.ActiveVessels = CaptureActiveLogisticsVessels(factory, request.ObjectId);
        }
        else if (request.ObjectId < 0 && -request.ObjectId < factory!.prebuildCursor)
        {
            snapshot = TryCapturePrebuild(factory, -request.ObjectId);
        }

        if (snapshot is not null && request.MaterialInventoryObjectIds?.Count > 0)
            snapshot.MaterialInventoryCut = CaptureMaterialInventoryCut(factory!, request.MaterialInventoryObjectIds);
        if (snapshot is not null && request.Geometry is not null)
        {
            try { snapshot.GeometryCut = CaptureFactoryGeometryCut(factory!,request.Geometry); }
            catch (InvalidOperationException error) {return GameCallResult<FactoryEntitySnapshot>.Failed(InvalidRequest(error.Message,"Refresh the explicit geometry cut; no route qualification or write was performed."));}
        }

        return snapshot is null
            ? InvalidFactoryEntity("The requested factory object no longer exists in the local factory.")
            : GameCallResult<FactoryEntitySnapshot>.Succeeded(snapshot);
    }

    public GameCallResult<PowerSummarySnapshot> GetPowerSummaryOnMainThread(
        string? requestedSessionId,
        LocalPlanetRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<PowerSummarySnapshot>.Failed(accessError);
        }

        var powerSystem = factory!.powerSystem;
        if (powerSystem?.netPool is null)
        {
            return GameCallResult<PowerSummarySnapshot>.Failed(NotReady(
                "The local planet power system is not ready."));
        }

        var result = new PowerSummarySnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory.planetId,
            CapturedAtGameTick = GameMain.gameTick,
        };
        if (powerSystem.netCursor < 1 || powerSystem.netCursor > powerSystem.netPool.Length)
        {
            return GameCallResult<PowerSummarySnapshot>.Failed(NotReady(
                "The local planet power-network pool is not ready."));
        }

        for (var networkId = 1; networkId < powerSystem.netCursor; networkId++)
        {
            var network = powerSystem.netPool[networkId];
            if (network is null || network.id == 0)
            {
                continue;
            }

            if (network.id != networkId)
            {
                return GameCallResult<PowerSummarySnapshot>.Failed(NotReady(
                    "An active local power network does not match its pool identity."));
            }

            var captureError = TryCapturePowerNetwork(powerSystem, networkId, network, out var snapshot);
            if (captureError is not null)
            {
                return GameCallResult<PowerSummarySnapshot>.Failed(captureError);
            }

            try
            {
                result.Networks.Add(snapshot!);
                checked
                {
                    result.TotalEnergyRequired += snapshot!.EnergyRequired;
                    result.TotalEnergyServed += snapshot.EnergyServed;
                    result.TotalEnergyCapacity += snapshot.EnergyCapacity;
                    result.TotalEnergyGenerated += snapshot.EnergyGenerated;
                    result.TotalEnergyExported += snapshot.EnergyExported;
                }
            }
            catch (OverflowException)
            {
                return GameCallResult<PowerSummarySnapshot>.Failed(NotReady(
                    "The local planet power summary exceeds safe numeric bounds."));
            }
        }

        return GameCallResult<PowerSummarySnapshot>.Succeeded(result);
    }

    public GameCallResult<OverseerProductionSnapshot> GetOverseerProductionOnMainThread(
        string? requestedSessionId,
        GetOverseerProductionRequest request)
    {
        var accessError = ValidateOwnedGameDataOnMainThread(requestedSessionId, out var gameData);
        if (accessError is not null)
        {
            return GameCallResult<OverseerProductionSnapshot>.Failed(accessError);
        }

        var limit = request.Limit == 0 ? DefaultOverseerPlanetLimit : request.Limit;
        if (limit < 1 || limit > MaximumOverseerPlanetLimit)
        {
            return GameCallResult<OverseerProductionSnapshot>.Failed(InvalidRequest(
                $"Overseer planet page limit must be between 1 and {MaximumOverseerPlanetLimit}.",
                "Use a bounded planet page limit and retry."));
        }

        var suppliedItemIds = request.ItemIds ?? new List<int>();
        if (suppliedItemIds.Count < 1 || suppliedItemIds.Count > MaximumOverseerItemCount)
        {
            return GameCallResult<OverseerProductionSnapshot>.Failed(InvalidRequest(
                $"Overseer production requires between 1 and {MaximumOverseerItemCount} item IDs.",
                "Provide a bounded set of exact item IDs from the recipe catalog."));
        }

        var itemIds = suppliedItemIds.OrderBy(itemId => itemId).ToArray();
        if (itemIds.Distinct().Count() != itemIds.Length
            || itemIds.Any(itemId => itemId <= 0 || LDB.items.Select(itemId) is null))
        {
            return GameCallResult<OverseerProductionSnapshot>.Failed(InvalidRequest(
                "Overseer production item IDs must be unique positive IDs present in the current runtime catalog.",
                "Refresh the recipe catalog and retry with unique current item IDs."));
        }

        var filterHash = ComputeFilterHash(
            $"overseer-production|items={string.Join(",", itemIds)}|limit={limit}");
        SnapshotPage<OverseerPlanetProductionSnapshot>? page;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var status = _overseerProductionSnapshots.TryGetPage(
                request.Cursor,
                _sessions.SessionId!,
                OverseerSnapshotScopeId,
                filterHash,
                limit,
                out page);
            if (status != SnapshotCursorStatus.Success || page is null)
            {
                return GameCallResult<OverseerProductionSnapshot>.Failed(StaleCursor(
                    "The Overseer cursor is unknown, expired, or bound to a different session, item filter, or page size."));
            }
        }
        else
        {
            var captureError = TryCaptureOverseerProduction(gameData!, itemIds, out var planets);
            if (captureError is not null)
            {
                return GameCallResult<OverseerProductionSnapshot>.Failed(captureError);
            }

            if (!_overseerProductionSnapshots.TryCreate(
                    _sessions.SessionId!,
                    OverseerSnapshotScopeId,
                    filterHash,
                    planets,
                    limit,
                    out page)
                || page is null)
            {
                return GameCallResult<OverseerProductionSnapshot>.Failed(
                    SnapshotCapacityExceeded("Overseer production"));
            }
        }

        var capturedAtGameTick = page.Items.Count > 0
            ? page.Items[0].CapturedAtGameTick
            : GameMain.gameTick;
        var nativeWindow = NativeProductionRateCalculator.Calculate(capturedAtGameTick, 0, 0).Window;
        return GameCallResult<OverseerProductionSnapshot>.Succeeded(new OverseerProductionSnapshot
        {
            SessionId = _sessions.SessionId!,
            CapturedAtGameTick = capturedAtGameTick,
            SnapshotId = page.SnapshotId,
            SnapshotExpiresAtUtc = page.ExpiresAtUtc,
            TotalFactoryCount = page.TotalItemCount,
            ReturnedFactoryCount = page.Items.Count,
            RequestedItemIds = itemIds.ToList(),
            RateSource = OverseerRateSources.NativeFactoryStatisticsLevel0,
            Window = nativeWindow,
            Planets = page.Items.ToList(),
            NextCursor = page.NextCursor,
        });
    }

    public GameCallResult<OverseerSummarySnapshot> GetOverseerSummaryOnMainThread(
        string? requestedSessionId,
        GetOverseerSummaryRequest request)
    {
        var accessError = ValidateOwnedGameDataOnMainThread(requestedSessionId, out var gameData);
        if (accessError is not null)
        {
            return GameCallResult<OverseerSummarySnapshot>.Failed(accessError);
        }

        var limit = request.Limit == 0 ? DefaultOverseerPlanetLimit : request.Limit;
        if (limit < 1 || limit > MaximumOverseerPlanetLimit)
        {
            return GameCallResult<OverseerSummarySnapshot>.Failed(InvalidRequest(
                $"Overseer planet page limit must be between 1 and {MaximumOverseerPlanetLimit}.",
                "Use a bounded planet page limit and retry."));
        }

        var filterHash = ComputeFilterHash($"overseer-summary|limit={limit}");
        SnapshotPage<OverseerSummaryPageEntry>? page;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var status = _overseerSummarySnapshots.TryGetPage(
                request.Cursor,
                _sessions.SessionId!,
                OverseerSnapshotScopeId,
                filterHash,
                limit,
                out page);
            if (status != SnapshotCursorStatus.Success || page is null)
            {
                return GameCallResult<OverseerSummarySnapshot>.Failed(StaleCursor(
                    "The Overseer summary cursor is unknown, expired, or bound to a different session or page size."));
            }
        }
        else
        {
            var captureError = TryCaptureOverseerSummary(gameData!, out var entries);
            if (captureError is not null)
            {
                return GameCallResult<OverseerSummarySnapshot>.Failed(captureError);
            }

            if (!_overseerSummarySnapshots.TryCreate(
                    _sessions.SessionId!,
                    OverseerSnapshotScopeId,
                    filterHash,
                    entries,
                    limit,
                    out page)
                || page is null)
            {
                return GameCallResult<OverseerSummarySnapshot>.Failed(
                    SnapshotCapacityExceeded("Overseer cross-domain"));
            }
        }

        if (page.Items.Count == 0)
        {
            return GameCallResult<OverseerSummarySnapshot>.Failed(NotReady(
                "The owned world did not yield an Overseer factory summary page."));
        }

        return GameCallResult<OverseerSummarySnapshot>.Succeeded(new OverseerSummarySnapshot
        {
            SessionId = _sessions.SessionId!,
            CapturedAtGameTick = page.Items[0].CapturedAtGameTick,
            SnapshotId = page.SnapshotId,
            SnapshotExpiresAtUtc = page.ExpiresAtUtc,
            TotalFactoryCount = page.TotalItemCount,
            ReturnedFactoryCount = page.Items.Count,
            Research = page.Items[0].Research,
            Planets = page.Items.Select(entry => entry.Planet).ToList(),
            NextCursor = page.NextCursor,
        });
    }

    public GameCallResult<OverseerDiagnosticBundleSnapshot> GetOverseerDiagnosticBundleOnMainThread(
        string? requestedSessionId,
        GetOverseerDiagnosticBundleRequest request)
    {
        var accessError = ValidateOwnedGameDataOnMainThread(requestedSessionId, out var gameData);
        if (accessError is not null)
        {
            return GameCallResult<OverseerDiagnosticBundleSnapshot>.Failed(accessError);
        }

        var limit = request.Limit == 0 ? DefaultOverseerPlanetLimit : request.Limit;
        if (limit < 1 || limit > MaximumOverseerPlanetLimit)
        {
            return GameCallResult<OverseerDiagnosticBundleSnapshot>.Failed(InvalidRequest(
                $"Overseer diagnostic-bundle planet page limit must be between 1 and {MaximumOverseerPlanetLimit}.",
                "Use a bounded planet page limit and retry."));
        }

        var suppliedItemIds = request.ItemIds ?? new List<int>();
        if (suppliedItemIds.Count < 1 || suppliedItemIds.Count > MaximumOverseerItemCount)
        {
            return GameCallResult<OverseerDiagnosticBundleSnapshot>.Failed(InvalidRequest(
                $"Overseer diagnostic bundle requires between 1 and {MaximumOverseerItemCount} item IDs.",
                "Provide a bounded set of exact item IDs from the recipe catalog."));
        }

        var itemIds = suppliedItemIds.OrderBy(itemId => itemId).ToArray();
        if (itemIds.Distinct().Count() != itemIds.Length
            || itemIds.Any(itemId => itemId <= 0 || LDB.items.Select(itemId) is null))
        {
            return GameCallResult<OverseerDiagnosticBundleSnapshot>.Failed(InvalidRequest(
                "Overseer diagnostic-bundle item IDs must be unique positive IDs present in the current runtime catalog.",
                "Refresh the recipe catalog and retry with unique current item IDs."));
        }

        var filterHash = ComputeFilterHash(
            $"overseer-diagnostic-bundle|items={string.Join(",", itemIds)}|limit={limit}");
        SnapshotPage<OverseerDiagnosticBundlePageEntry>? page;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var status = _overseerDiagnosticBundleSnapshots.TryGetPage(
                request.Cursor,
                _sessions.SessionId!,
                OverseerSnapshotScopeId,
                filterHash,
                limit,
                out page);
            if (status != SnapshotCursorStatus.Success || page is null)
            {
                return GameCallResult<OverseerDiagnosticBundleSnapshot>.Failed(StaleCursor(
                    "The Overseer diagnostic-bundle cursor is unknown, expired, or bound to a different session, item filter, or page size."));
            }
        }
        else
        {
            var captureError = TryCaptureOverseerDiagnosticBundle(gameData!, itemIds, out var entries);
            if (captureError is not null)
            {
                return GameCallResult<OverseerDiagnosticBundleSnapshot>.Failed(captureError);
            }

            if (!_overseerDiagnosticBundleSnapshots.TryCreate(
                    _sessions.SessionId!,
                    OverseerSnapshotScopeId,
                    filterHash,
                    entries,
                    limit,
                    out page)
                || page is null)
            {
                return GameCallResult<OverseerDiagnosticBundleSnapshot>.Failed(
                    SnapshotCapacityExceeded("Overseer diagnostic bundle"));
            }
        }

        if (page.Items.Count == 0)
        {
            return GameCallResult<OverseerDiagnosticBundleSnapshot>.Failed(NotReady(
                "The owned world did not yield an Overseer diagnostic-bundle page."));
        }

        var capturedAtGameTick = page.Items[0].CapturedAtGameTick;
        return GameCallResult<OverseerDiagnosticBundleSnapshot>.Succeeded(new OverseerDiagnosticBundleSnapshot
        {
            SchemaVersion = OverseerDiagnosticBundleProfiles.CurrentSchemaVersion,
            PrivacyProfile = OverseerDiagnosticBundleProfiles.PublicAllowlistV1,
            SessionId = _sessions.SessionId!,
            CapturedAtGameTick = capturedAtGameTick,
            SnapshotId = page.SnapshotId,
            SnapshotExpiresAtUtc = page.ExpiresAtUtc,
            TotalFactoryCount = page.TotalItemCount,
            ReturnedFactoryCount = page.Items.Count,
            RequestedItemIds = itemIds.ToList(),
            RateSource = OverseerRateSources.NativeFactoryStatisticsLevel0,
            Window = NativeProductionRateCalculator.Calculate(capturedAtGameTick, 0, 0).Window,
            Research = page.Items[0].Research,
            Planets = page.Items.Select(entry => entry.Planet).ToList(),
            NextCursor = page.NextCursor,
        });
    }

    private BridgeError? TryCaptureOverseerDiagnosticBundle(
        GameData gameData,
        IReadOnlyList<int> itemIds,
        out List<OverseerDiagnosticBundlePageEntry> entries)
    {
        entries = new List<OverseerDiagnosticBundlePageEntry>();
        var productionError = TryCaptureOverseerProduction(gameData, itemIds, out var productionPlanets);
        if (productionError is not null)
        {
            return productionError;
        }

        var summaryError = TryCaptureOverseerSummary(gameData, out var summaryEntries);
        if (summaryError is not null)
        {
            return summaryError;
        }

        if (productionPlanets.Count != summaryEntries.Count)
        {
            return NotReady("The same-tick Overseer domains returned different owned-factory counts.");
        }

        for (var index = 0; index < productionPlanets.Count; index++)
        {
            var production = productionPlanets[index];
            var summary = summaryEntries[index];
            if (production.CapturedAtGameTick != summary.CapturedAtGameTick)
            {
                entries.Clear();
                return NotReady("The same-tick Overseer domains do not share one exact owned-factory identity and capture tick.");
            }

            try
            {
                entries.Add(new OverseerDiagnosticBundlePageEntry
                {
                    CapturedAtGameTick = production.CapturedAtGameTick,
                    Research = summary.Research,
                    Planet = OverseerDiagnosticBundleComposer.ComposePlanet(production, summary.Planet),
                });
            }
            catch (ArgumentException)
            {
                entries.Clear();
                return NotReady("The same-tick Overseer domains do not share one exact owned-factory identity and capture tick.");
            }
        }

        return null;
    }

    private static BridgeError? TryCaptureOverseerSummary(
        GameData gameData,
        out List<OverseerSummaryPageEntry> entries)
    {
        entries = new List<OverseerSummaryPageEntry>();
        var factoryError = TryGetOwnedFactories(gameData, out var factories);
        if (factoryError is not null)
        {
            return factoryError;
        }

        long powerNetworkScanCount = 0;
        long powerGeneratorScanCount = 0;
        long stationScanCount = 0;
        foreach (var factory in factories)
        {
            var powerSystem = factory.powerSystem;
            var transport = factory.transport;
            if (powerSystem?.netPool is null
                || powerSystem.netCursor < 1
                || powerSystem.netCursor > powerSystem.netPool.Length
                || transport?.stationPool is null
                || transport.stationCursor < 1
                || transport.stationCursor > transport.stationPool.Length)
            {
                return NotReady("An owned factory's power or logistics index is not ready.");
            }

            powerNetworkScanCount += powerSystem.netCursor - 1L;
            stationScanCount += transport.stationCursor - 1L;
            if (powerNetworkScanCount > MaximumOverseerPowerNetworkScanCount
                || stationScanCount > MaximumOverseerStationScanCount)
            {
                return OverseerScopeExceeded();
            }

            for (var networkId = 1; networkId < powerSystem.netCursor; networkId++)
            {
                var network = powerSystem.netPool[networkId];
                if (network is not null && network.id != 0)
                {
                    powerGeneratorScanCount += network.generators?.Count ?? 0;
                }
            }

            if (powerGeneratorScanCount > MaximumOverseerPowerGeneratorScanCount)
            {
                return OverseerScopeExceeded();
            }
        }

        var capturedAtGameTick = GameMain.gameTick;
        var researchError = TryCaptureOverseerResearch(out var research);
        if (researchError is not null)
        {
            return researchError;
        }

        var localPlanetId = gameData.localPlanet?.id ?? 0;
        foreach (var factory in factories)
        {
            var powerError = TryCaptureOverseerPower(factory, out var power);
            if (powerError is not null)
            {
                entries.Clear();
                return powerError;
            }

            var logisticsError = TryCaptureOverseerLogistics(factory, out var logistics);
            if (logisticsError is not null)
            {
                entries.Clear();
                return logisticsError;
            }

            entries.Add(new OverseerSummaryPageEntry
            {
                CapturedAtGameTick = capturedAtGameTick,
                Research = research!,
                Planet = new OverseerPlanetSummarySnapshot
                {
                    FactoryIndex = factory.index,
                    PlanetId = factory.planetId,
                    PlanetName = factory.planet.displayName,
                    IsLocalPlanet = factory.planetId == localPlanetId,
                    FactoryDisplayLoaded = factory.planet.factoryLoaded,
                    CapturedAtGameTick = capturedAtGameTick,
                    Power = power!,
                    Logistics = logistics!,
                },
            });
        }

        return null;
    }

    private static BridgeError? TryCaptureOverseerPower(
        PlanetFactory factory,
        out OverseerPowerSummarySnapshot? summary)
    {
        summary = null;
        var powerSystem = factory.powerSystem;
        if (powerSystem?.netPool is null
            || powerSystem.netCursor < 1
            || powerSystem.netCursor > powerSystem.netPool.Length)
        {
            return NotReady("An owned factory's power-network pool is not ready.");
        }

        var networks = new List<PowerNetworkSnapshot>();
        for (var networkId = 1; networkId < powerSystem.netCursor; networkId++)
        {
            var network = powerSystem.netPool[networkId];
            if (network is null || network.id == 0)
            {
                continue;
            }

            if (network.id != networkId)
            {
                return NotReady("An active power network does not match its pool identity.");
            }

            var captureError = TryCapturePowerNetwork(powerSystem, networkId, network, out var snapshot);
            if (captureError is not null)
            {
                return captureError;
            }

            networks.Add(snapshot!);
        }

        try
        {
            summary = OverseerPowerSummaryCalculator.Calculate(
                networks,
                MaximumOverseerNetworkDetailsPerPlanet);
            return null;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is OverflowException)
        {
            return NotReady("An owned factory's power counters are incomplete, invalid, or exceed safe numeric bounds.");
        }
    }

    private static BridgeError? TryCapturePowerNetwork(
        PowerSystem powerSystem,
        int networkId,
        PowerNetwork network,
        out PowerNetworkSnapshot? snapshot)
    {
        snapshot = null;
        if (network.nodes is null
            || network.consumers is null
            || network.generators is null
            || network.accumulators is null
            || network.exchangers is null
            || network.energyRequired < 0
            || network.energyServed < 0
            || network.energyCapacity < 0
            || network.energyExport < 0
            || network.energyStored < 0
            || double.IsNaN(network.consumerRatio)
            || double.IsInfinity(network.consumerRatio)
            || network.consumerRatio < 0d
            || double.IsNaN(network.generaterRatio)
            || double.IsInfinity(network.generaterRatio)
            || network.generaterRatio < 0d
            || powerSystem.genPool is null
            || powerSystem.genCursor < 1
            || powerSystem.genCursor > powerSystem.genPool.Length)
        {
            return NotReady("An active power network has incomplete or invalid native counters.");
        }

        long generated = 0;
        var generatorIds = new HashSet<int>();
        try
        {
            foreach (var generatorId in network.generators)
            {
                if (generatorId <= 0
                    || !generatorIds.Add(generatorId)
                    || generatorId >= powerSystem.genCursor
                    || generatorId >= powerSystem.genPool.Length)
                {
                    return NotReady("An active power network references an invalid generator component.");
                }

                ref var generator = ref powerSystem.genPool[generatorId];
                if (generator.id != generatorId
                    || generator.networkId != networkId
                    || generator.generateCurrentTick < 0)
                {
                    return NotReady("An active power network does not match its generator component identity or counters.");
                }

                checked
                {
                    generated += generator.generateCurrentTick;
                }
            }
        }
        catch (OverflowException)
        {
            return NotReady("An active power network's generated energy exceeds safe numeric bounds.");
        }

        snapshot = new PowerNetworkSnapshot
        {
            NetworkId = networkId,
            NodeCount = network.nodes.Count,
            ConsumerCount = network.consumers.Count,
            GeneratorCount = network.generators.Count,
            AccumulatorCount = network.accumulators.Count,
            ExchangerCount = network.exchangers.Count,
            EnergyRequired = network.energyRequired,
            EnergyServed = network.energyServed,
            EnergyCapacity = network.energyCapacity,
            EnergyGenerated = generated,
            EnergyExported = network.energyExport,
            EnergyStored = network.energyStored,
            ConsumerRatio = network.consumerRatio,
            GeneratorRatio = network.generaterRatio,
        };
        return null;
    }

    private static BridgeError? TryCaptureOverseerLogistics(
        PlanetFactory factory,
        out OverseerLogisticsSummarySnapshot? summary)
    {
        summary = new OverseerLogisticsSummarySnapshot();
        var transport = factory.transport;
        if (transport?.stationPool is null
            || transport.stationCursor < 1
            || transport.stationCursor > transport.stationPool.Length
            || factory.entityPool is null)
        {
            summary = null;
            return NotReady("An owned factory's logistics-station pool is not ready.");
        }

        try
        {
            for (var stationId = 1; stationId < transport.stationCursor; stationId++)
            {
                var station = transport.stationPool[stationId];
                if (station is null || station.id == 0)
                {
                    continue;
                }

                if (station.id != stationId
                    || station.entityId <= 0
                    || station.entityId >= factory.entityCursor
                    || station.entityId >= factory.entityPool.Length)
                {
                    summary = null;
                    return NotReady("An active logistics station does not match its station or entity pool identity.");
                }

                ref var entity = ref factory.entityPool[station.entityId];
                if (entity.id != station.entityId
                    || entity.stationId != stationId
                    || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(
                        station.isStellar,
                        station.planetId,
                        factory.planetId)
                    || station.energy < 0
                    || station.energyMax < 0
                    || station.warperCount < 0
                    || station.idleDroneCount < 0
                    || station.workDroneCount < 0
                    || station.idleShipCount < 0
                    || station.workShipCount < 0)
                {
                    summary = null;
                    return NotReady("An active logistics station has inconsistent identity or counters.");
                }

                checked
                {
                    summary.StationCount++;
                    summary.StoredEnergy += station.energy;
                    summary.EnergyCapacity += station.energyMax;
                    summary.WarperCount += station.warperCount;
                    summary.IdleDroneCount += station.idleDroneCount;
                    summary.WorkingDroneCount += station.workDroneCount;
                    summary.IdleVesselCount += station.idleShipCount;
                    summary.WorkingVesselCount += station.workShipCount;
                    if (station.isCollector)
                    {
                        summary.CollectorCount++;
                    }
                    else if (station.isVeinCollector)
                    {
                        summary.VeinCollectorCount++;
                    }
                    else if (station.isStellar)
                    {
                        summary.InterstellarStationCount++;
                    }
                    else
                    {
                        summary.PlanetaryStationCount++;
                    }
                }

                if (!station.isCollector)
                {
                    if (TryGetStationPowerRatio(factory, ref entity, station, out var powerRatio)
                        && powerRatio >= ProductionFaultClassifier.DefaultMinimumPowerServeRatio)
                    {
                        checked
                        {
                            summary.PoweredStationCount++;
                        }
                    }
                    else
                    {
                        checked
                        {
                            summary.UnderpoweredStationCount++;
                        }
                    }
                }

                var stores = station.storage;
                if (stores is null)
                {
                    summary = null;
                    return NotReady("An active logistics station has no storage-slot array.");
                }

                if (stores.Length > MaximumOverseerStationStorageSlots)
                {
                    summary = null;
                    return OverseerScopeExceeded();
                }

                foreach (var store in stores)
                {
                    if (store.itemId < 0
                        || store.count < 0
                        || !IsKnownLogisticsMode(store.localLogic)
                        || !IsKnownLogisticsMode(store.remoteLogic)
                        || (store.itemId == 0
                            && (store.count != 0
                                || store.localOrder != 0
                                || store.remoteOrder != 0
                                || store.localLogic != ELogisticStorage.None
                                || store.remoteLogic != ELogisticStorage.None)))
                    {
                        summary = null;
                        return NotReady("A logistics station storage slot has inconsistent identity, inventory, order, or mode state.");
                    }

                    checked
                    {
                        if (store.itemId > 0)
                        {
                            summary.ConfiguredStorageSlotCount++;
                            summary.StoredItemCount += store.count;
                        }

                        if (store.localLogic == ELogisticStorage.Supply) summary.LocalSupplySlotCount++;
                        if (store.localLogic == ELogisticStorage.Demand) summary.LocalDemandSlotCount++;
                        if (store.remoteLogic == ELogisticStorage.Supply) summary.RemoteSupplySlotCount++;
                        if (store.remoteLogic == ELogisticStorage.Demand) summary.RemoteDemandSlotCount++;
                        if (store.localOrder != 0)
                        {
                            summary.OutstandingLocalOrderSlotCount++;
                            summary.OutstandingLocalOrderMagnitude += Math.Abs((long)store.localOrder);
                        }

                        if (store.remoteOrder != 0)
                        {
                            summary.OutstandingRemoteOrderSlotCount++;
                            summary.OutstandingRemoteOrderMagnitude += Math.Abs((long)store.remoteOrder);
                        }
                    }
                }
            }

            return null;
        }
        catch (OverflowException)
        {
            summary = null;
            return NotReady("The logistics summary exceeds safe numeric bounds.");
        }
    }

    private static bool IsKnownLogisticsMode(ELogisticStorage mode) =>
        mode == ELogisticStorage.None
        || mode == ELogisticStorage.Supply
        || mode == ELogisticStorage.Demand;

    private static bool TryGetStationPowerRatio(
        PlanetFactory factory,
        ref EntityData entity,
        StationComponent station,
        out double ratio)
    {
        ratio = 0d;
        var consumerId = station.pcId;
        var powerSystem = factory.powerSystem;
        if (consumerId <= 0
            || consumerId != entity.powerConId
            || powerSystem?.consumerPool is null
            || consumerId >= powerSystem.consumerCursor
            || consumerId >= powerSystem.consumerPool.Length)
        {
            return false;
        }

        ref var consumer = ref powerSystem.consumerPool[consumerId];
        if (consumer.id != consumerId || consumer.entityId != entity.id)
        {
            return false;
        }

        var value = GetPowerServeRatio(powerSystem, consumer.networkId);
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0d)
        {
            return false;
        }

        ratio = value.Value;
        return true;
    }

    private static BridgeError? TryCaptureOverseerResearch(
        out OverseerResearchSummarySnapshot? summary)
    {
        summary = null;
        var history = GameMain.history;
        if (history?.techStates is null || history.techQueue is null || LDB.techs?.dataArray is null)
        {
            return NotReady("The owned world's technology state is not ready.");
        }

        if (history.techQueue.Length > MaximumOverseerTechQueueScanCount
            || LDB.techs.dataArray.Length > MaximumOverseerTechnologyScanCount)
        {
            return OverseerScopeExceeded();
        }

        foreach (var techId in history.techQueue)
        {
            if (techId < 0
                || (techId > 0
                    && (LDB.techs.Select(techId) is null
                        || !history.techStates.ContainsKey(techId))))
            {
                return NotReady("The technology queue contains an invalid runtime technology identity.");
            }
        }

        var queue = history.techQueue.Where(techId => techId > 0).ToArray();
        var result = new OverseerResearchSummarySnapshot
        {
            CurrentTechId = history.currentTech,
            CurrentTechName = history.currentTech > 0 ? LDB.techs.Select(history.currentTech)?.name : null,
            QueuedTechCount = queue.Length,
            TechQueueTruncated = queue.Length > MaximumOverseerTechQueueCount,
            TechQueue = queue.Take(MaximumOverseerTechQueueCount).ToList(),
        };

        foreach (var tech in LDB.techs.dataArray)
        {
            if (tech is null || !history.techStates.TryGetValue(tech.ID, out var state))
            {
                continue;
            }

            result.RuntimeTechStateCount++;
            if (state.unlocked)
            {
                result.UnlockedTechCount++;
            }
        }

        if (history.currentTech <= 0)
        {
            summary = result;
            return null;
        }

        var currentTech = LDB.techs.Select(history.currentTech);
        if (currentTech is null
            || !history.techStates.TryGetValue(history.currentTech, out var currentState)
            || currentState.hashUploaded < 0
            || currentState.hashNeeded < 0)
        {
            return NotReady("The current technology identity or hash counters are inconsistent.");
        }

        result.CurrentHashUploaded = currentState.hashUploaded;
        result.CurrentHashRequired = currentState.hashNeeded;
        result.CurrentHashRemaining = Math.Max(0, currentState.hashNeeded - currentState.hashUploaded);
        var items = currentTech.Items ?? Array.Empty<int>();
        var itemPoints = currentTech.ItemPoints ?? Array.Empty<int>();
        if (items.Length != itemPoints.Length || items.Length > 16)
        {
            return NotReady("The current technology item requirements are inconsistent or exceed safe bounds.");
        }

        try
        {
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index] <= 0 || itemPoints[index] < 0 || LDB.items.Select(items[index]) is null)
                {
                    return NotReady("The current technology contains an invalid runtime item requirement.");
                }

                result.CurrentRequirements.Add(new OverseerResearchItemSnapshot
                {
                    ItemId = items[index],
                    ItemName = GetItemName(items[index]),
                    PointsPerHash = itemPoints[index],
                    RequiredItemCount = OverseerResearchMath.CalculateItemCount(
                        currentState.hashNeeded,
                        itemPoints[index]),
                    RemainingItemCount = OverseerResearchMath.CalculateItemCount(
                        result.CurrentHashRemaining,
                        itemPoints[index]),
                    IsMatrix = TechProto.matrixIds.Contains(items[index]),
                });
            }
        }
        catch (OverflowException)
        {
            return NotReady("The current technology item requirement exceeds safe numeric bounds.");
        }

        summary = result;
        return null;
    }

    private static BridgeError? TryGetOwnedFactories(
        GameData gameData,
        out List<PlanetFactory> factories)
    {
        factories = new List<PlanetFactory>();
        var factoryPool = gameData.factories;
        if (gameData.factoryCount > MaximumOverseerFactoryCount)
        {
            return OverseerScopeExceeded();
        }

        if (factoryPool is null
            || gameData.factoryCount < 1
            || gameData.factoryCount > factoryPool.Length)
        {
            return NotReady("The owned world's factory index is not ready.");
        }

        for (var factoryIndex = 0; factoryIndex < gameData.factoryCount; factoryIndex++)
        {
            var factory = factoryPool[factoryIndex];
            var planet = factory?.planet;
            if (factory is null
                || planet is null
                || factory.index != factoryIndex
                || planet.factoryIndex != factoryIndex
                || !ReferenceEquals(planet.factory, factory)
                || factory.planetId <= 0
                || factory.planetId != planet.id)
            {
                factories.Clear();
                return NotReady("An owned factory does not match its factory and planet pool identity.");
            }

            factories.Add(factory);
        }

        return null;
    }

    private BridgeError? TryCaptureOverseerProduction(
        GameData gameData,
        IReadOnlyList<int> itemIds,
        out List<OverseerPlanetProductionSnapshot> planets)
    {
        planets = new List<OverseerPlanetProductionSnapshot>();
        var factoryError = TryGetOwnedFactories(gameData, out var factories);
        if (factoryError is not null)
        {
            return factoryError;
        }

        var production = gameData.statistics?.production;
        var factoryStats = production?.factoryStatPool;
        if (factoryStats is null || gameData.factoryCount > factoryStats.Length)
        {
            return NotReady("The owned world's production-statistics index is not ready.");
        }

        var capturedAtGameTick = GameMain.gameTick;
        var localPlanetId = gameData.localPlanet?.id ?? 0;
        var settingsError = TryCaptureOverseerTheoreticalSettings(gameData, out var theoreticalSettings);
        if (settingsError is not null)
        {
            return settingsError;
        }

        long theoreticalComponentScanCount = 0;
        long theoreticalSourceReferenceScanCount = 0;
        long diagnosticComponentScanCount = 0;
        long diagnosticSourceReferenceScanCount = 0;
        var diagnosticWindow = NativeProductionRateCalculator.Calculate(capturedAtGameTick, 0, 0).Window;
        var requestedItemIds = new HashSet<int>(itemIds);
        var logisticsError = TryCaptureOverseerDiagnosticLogisticsRoutes(
            factories,
            _overseerLogisticsProgressStore,
            ref diagnosticComponentScanCount,
            ref diagnosticSourceReferenceScanCount,
            out var diagnosticLogistics);
        if (logisticsError is not null)
        {
            return logisticsError;
        }

        var directDiagnosticsByFactory = new Dictionary<int, OverseerDirectDiagnosticCapture>();
        foreach (var factory in factories)
        {
            var diagnosticError = TryCaptureOverseerDirectDiagnostics(
                factory,
                requestedItemIds,
                diagnosticWindow,
                diagnosticLogistics!,
                ref diagnosticComponentScanCount,
                ref diagnosticSourceReferenceScanCount,
                out var directDiagnostics);
            if (diagnosticError is not null)
            {
                return diagnosticError;
            }

            directDiagnosticsByFactory.Add(factory.index, directDiagnostics!);
        }

        var directDiagnosticsByPlanet = new Dictionary<int, OverseerDirectDiagnosticCapture>();
        foreach (var factory in factories)
        {
            if (directDiagnosticsByPlanet.ContainsKey(factory.planetId))
            {
                return NotReady("Owned production diagnostics contain a duplicate planet identity.");
            }

            directDiagnosticsByPlanet.Add(
                factory.planetId,
                directDiagnosticsByFactory[factory.index]);
        }
        foreach (var capture in directDiagnosticsByFactory.Values)
        {
            // Producers can only be resolved after every owned factory has
            // been captured, so no live game object crosses this boundary.
            capture.BindUpstreamProducers((planetId, itemId) =>
                directDiagnosticsByPlanet.TryGetValue(planetId, out var producerCapture)
                    ? producerCapture.GetProducers(itemId)
                    : Array.Empty<ProductionFaultInput>());
        }

        // Route samples are collected for every owned factory first, then
        // atomically persisted once. Only durable analyses are copied into the
        // public diagnostic DTOs below.
        diagnosticLogistics!.FinalizeProgressEvidence();

        foreach (var factory in factories)
        {
            var factoryIndex = factory.index;
            var factoryStat = factoryStats[factoryIndex];
            var planet = factory.planet;
            if (factoryStat is null
                || factoryStat.productIndices is null
                || factoryStat.productPool is null)
            {
                planets.Clear();
                return NotReady("An owned factory or its production-statistics identity is inconsistent.");
            }

            var theoreticalError = TryCaptureOverseerTheoreticalProduction(
                factory,
                itemIds,
                theoreticalSettings!,
                ref theoreticalComponentScanCount,
                ref theoreticalSourceReferenceScanCount,
                out var theoreticalRates);
            if (theoreticalError is not null)
            {
                planets.Clear();
                return theoreticalError;
            }

            var directDiagnostics = directDiagnosticsByFactory[factoryIndex];

            var snapshot = new OverseerPlanetProductionSnapshot
            {
                FactoryIndex = factoryIndex,
                PlanetId = factory.planetId,
                PlanetName = planet.displayName,
                IsLocalPlanet = factory.planetId == localPlanetId,
                FactoryDisplayLoaded = planet.factoryLoaded,
                CapturedAtGameTick = capturedAtGameTick,
                InfrastructureFindingCount = directDiagnostics.InfrastructureFindings.Count,
                InfrastructureFindingsTruncated = directDiagnostics.InfrastructureFindings.Count
                    > MaximumOverseerInfrastructureFindingsPerPlanet,
                InfrastructureFindings = directDiagnostics.InfrastructureFindings
                    .OrderBy(finding => finding.ObjectId)
                    .Take(MaximumOverseerInfrastructureFindingsPerPlanet)
                    .ToList(),
            };
            foreach (var itemId in itemIds)
            {
                if (itemId >= factoryStat.productIndices.Length)
                {
                    planets.Clear();
                    return NotReady("A requested runtime item is outside the production-statistics index.");
                }

                long producedCount = 0;
                long consumedCount = 0;
                var productIndex = factoryStat.productIndices[itemId];
                if (productIndex > 0)
                {
                    if (productIndex >= factoryStat.productCursor
                        || productIndex >= factoryStat.productPool.Length)
                    {
                        planets.Clear();
                        return NotReady("A production-statistics product index is outside its active pool.");
                    }

                    var product = factoryStat.productPool[productIndex];
                    if (product is null
                        || product.itemId != itemId
                        || product.total is null
                        || product.total.Length <= 7
                        || product.total[0] < 0
                        || product.total[7] < 0)
                    {
                        planets.Clear();
                        return NotReady("A production-statistics product record is incomplete or inconsistent.");
                    }

                    producedCount = product.total[0];
                    consumedCount = product.total[7];
                }

                var rate = NativeProductionRateCalculator.Calculate(
                    capturedAtGameTick,
                    producedCount,
                    consumedCount);
                var theoreticalProductionPerMinute = theoreticalRates![itemId];
                var productionRate = new ProductionRateSnapshot
                {
                    PlanetId = factory.planetId,
                    ItemId = itemId,
                    ItemName = GetItemName(itemId),
                    ProducedCount = producedCount,
                    ConsumedCount = consumedCount,
                    ActualProductionPerMinute = rate.ActualProductionPerMinute,
                    ActualConsumptionPerMinute = rate.ActualConsumptionPerMinute,
                    TheoreticalProductionPerMinute = theoreticalProductionPerMinute,
                    Utilization = OverseerTheoreticalProductionCalculator.CalculateUtilization(
                        rate.Window.State,
                        rate.ActualProductionPerMinute,
                        theoreticalProductionPerMinute),
                    TheoreticalRateSource = OverseerTheoreticalRateSources.CurrentRuntimeComponentFormulaV1,
                    RateSource = OverseerRateSources.NativeFactoryStatisticsLevel0,
                    TheoreticalCoverage = OverseerTheoreticalCoverageStates.Complete,
                };
                ApplyOverseerDirectDiagnostics(
                    directDiagnostics,
                    directDiagnosticsByPlanet,
                    rate.Window,
                    productionRate);
                snapshot.Production.Add(productionRate);
            }

            planets.Add(snapshot);
        }

        return null;
    }

    private static BridgeError? TryCaptureOverseerTheoreticalSettings(
        GameData gameData,
        out OverseerTheoreticalSettings? settings)
    {
        settings = null;
        var history = gameData.history;
        if (history is null
            || float.IsNaN(history.miningSpeedScale)
            || float.IsInfinity(history.miningSpeedScale)
            || history.miningSpeedScale < 0f
            || Cargo.incTableMilli is null
            || Cargo.accTableMilli is null)
        {
            return NotReady("The owned world's theoretical-production settings are not ready.");
        }

        var proliferatorAbility = 0;
        var proliferatorItemIds = LDB.items.Select(2313)?.prefabDesc?.incItemId;
        if (proliferatorItemIds is not null)
        {
            foreach (var itemId in proliferatorItemIds)
            {
                var item = itemId > 0 ? LDB.items.Select(itemId) : null;
                if (item is null || item.Ability < 0)
                {
                    return NotReady("The runtime proliferator catalog contains an invalid item or ability.");
                }

                if (history.ItemUnlocked(itemId) && item.Ability > proliferatorAbility)
                {
                    proliferatorAbility = item.Ability;
                }
            }
        }

        if (proliferatorAbility >= Cargo.incTableMilli.Length
            || proliferatorAbility >= Cargo.accTableMilli.Length)
        {
            return NotReady("The unlocked proliferator ability is outside the current runtime multiplier tables.");
        }

        try
        {
            settings = new OverseerTheoreticalSettings
            {
                ProductMultiplier = 1f + (float)Cargo.incTableMilli[proliferatorAbility],
                AccelerationMultiplier = 1f + (float)Cargo.accTableMilli[proliferatorAbility],
                MiningSpeedScale = history.miningSpeedScale,
                FractionatorStackMultiplier =
                    OverseerTheoreticalProductionCalculator.CalculateFractionatorStackMultiplier(
                        history.TechUnlocked(1607),
                        history.inserterStackOutput,
                        history.stationPilerLevel),
            };
            return null;
        }
        catch (ArgumentException)
        {
            return NotReady("The owned world's theoretical-production multipliers are invalid.");
        }
    }

    private static BridgeError? TryCaptureOverseerTheoreticalProduction(
        PlanetFactory factory,
        IReadOnlyList<int> itemIds,
        OverseerTheoreticalSettings settings,
        ref long componentScanCount,
        ref long sourceReferenceScanCount,
        out Dictionary<int, double>? rates)
    {
        rates = itemIds.ToDictionary(itemId => itemId, _ => 0d);
        var factorySystem = factory.factorySystem;
        var powerSystem = factory.powerSystem;
        var transport = factory.transport;
        if (factorySystem?.assemblerPool is null
            || factorySystem.labPool is null
            || factorySystem.minerPool is null
            || factorySystem.fractionatorPool is null
            || powerSystem?.consumerPool is null
            || powerSystem.genPool is null
            || powerSystem.netPool is null
            || transport?.stationPool is null
            || factory.entityPool is null
            || factory.veinPool is null
            || !IsValidPoolCursor(factorySystem.assemblerCursor, factorySystem.assemblerPool.Length)
            || !IsValidPoolCursor(factorySystem.labCursor, factorySystem.labPool.Length)
            || !IsValidPoolCursor(factorySystem.minerCursor, factorySystem.minerPool.Length)
            || !IsValidPoolCursor(factorySystem.fractionatorCursor, factorySystem.fractionatorPool.Length)
            || !IsValidPoolCursor(powerSystem.consumerCursor, powerSystem.consumerPool.Length)
            || !IsValidPoolCursor(powerSystem.genCursor, powerSystem.genPool.Length)
            || !IsValidPoolCursor(powerSystem.netCursor, powerSystem.netPool.Length)
            || !IsValidPoolCursor(transport.stationCursor, transport.stationPool.Length)
            || !IsValidPoolCursor(factory.entityCursor, factory.entityPool.Length)
            || !IsValidPoolCursor(factory.veinCursor, factory.veinPool.Length))
        {
            rates = null;
            return NotReady("An owned factory's theoretical-production pools are not ready.");
        }

        var additionalComponents =
            factorySystem.assemblerCursor - 1L
            + factorySystem.labCursor - 1L
            + factorySystem.minerCursor - 1L
            + factorySystem.fractionatorCursor - 1L
            + powerSystem.genCursor - 1L
            + transport.stationCursor - 1L;
        if (!TryConsumeBudget(
                ref componentScanCount,
                additionalComponents,
                MaximumOverseerTheoreticalComponentScanCount))
        {
            rates = null;
            return OverseerScopeExceeded();
        }

        try
        {
            var error = TryCaptureAssemblerTheoreticalRates(
                factory,
                settings,
                rates,
                ref sourceReferenceScanCount);
            if (error is not null) return error;

            error = TryCaptureLabTheoreticalRates(
                factory,
                settings,
                rates,
                ref sourceReferenceScanCount);
            if (error is not null) return error;

            error = TryCaptureMinerTheoreticalRates(
                factory,
                settings,
                rates,
                ref sourceReferenceScanCount);
            if (error is not null) return error;

            error = TryCaptureFractionatorTheoreticalRates(factory, settings, rates);
            if (error is not null) return error;

            error = TryCaptureGammaTheoreticalRates(factory, rates);
            if (error is not null) return error;

            error = TryCaptureCollectorTheoreticalRates(
                factory,
                settings,
                rates,
                ref sourceReferenceScanCount);
            if (error is not null) return error;

            return null;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is OverflowException)
        {
            rates = null;
            return NotReady("An owned factory's theoretical-production formula inputs are invalid or exceed finite bounds.");
        }
    }

    private static BridgeError? TryCaptureAssemblerTheoreticalRates(
        PlanetFactory factory,
        OverseerTheoreticalSettings settings,
        Dictionary<int, double> rates,
        ref long sourceReferenceScanCount)
    {
        var factorySystem = factory.factorySystem;
        for (var assemblerId = 1; assemblerId < factorySystem.assemblerCursor; assemblerId++)
        {
            ref var assembler = ref factorySystem.assemblerPool[assemblerId];
            if (assembler.id == 0) continue;
            if (assembler.id != assemblerId)
            {
                return NotReady("An active assembler does not match its component-pool identity.");
            }

            var connectionError = TryGetTheoreticalConsumerConnection(
                factory,
                TheoreticalProducerKind.Assembler,
                assemblerId,
                assembler.entityId,
                assembler.pcId,
                out var connected);
            if (connectionError is not null) return connectionError;
            if (assembler.recipeId <= 0) continue;

            if (LDB.recipes.Select(assembler.recipeId) is null
                || assembler.speed <= 0
                || assembler.recipeExecuteData is null)
            {
                return NotReady("An active configured assembler has an invalid runtime recipe or speed.");
            }

            var recipeError = TryValidateTheoreticalRecipe(
                assembler.recipeExecuteData,
                ref sourceReferenceScanCount);
            if (recipeError is not null) return recipeError;
            if (!connected) continue;

            for (var index = 0; index < assembler.recipeExecuteData.products.Length; index++)
            {
                AddTheoreticalRate(
                    rates,
                    assembler.recipeExecuteData.products[index],
                    OverseerTheoreticalProductionCalculator.CalculateRecipeOutputPerMinute(
                        assembler.speed,
                        assembler.recipeExecuteData.timeSpend,
                        assembler.incUsed,
                        assembler.recipeExecuteData.productive,
                        assembler.forceAccMode,
                        settings.ProductMultiplier,
                        settings.AccelerationMultiplier,
                        assembler.recipeExecuteData.productCounts[index]));
            }
        }

        return null;
    }

    private static BridgeError? TryCaptureLabTheoreticalRates(
        PlanetFactory factory,
        OverseerTheoreticalSettings settings,
        Dictionary<int, double> rates,
        ref long sourceReferenceScanCount)
    {
        var factorySystem = factory.factorySystem;
        for (var labId = 1; labId < factorySystem.labCursor; labId++)
        {
            ref var lab = ref factorySystem.labPool[labId];
            if (lab.id == 0) continue;
            if (lab.id != labId || (lab.matrixMode && lab.researchMode))
            {
                return NotReady("An active lab has inconsistent component identity or operating modes.");
            }

            var connectionError = TryGetTheoreticalConsumerConnection(
                factory,
                TheoreticalProducerKind.Lab,
                labId,
                lab.entityId,
                lab.pcId,
                out var connected);
            if (connectionError is not null) return connectionError;
            if (!lab.matrixMode) continue;

            if (lab.recipeId <= 0
                || LDB.recipes.Select(lab.recipeId) is null
                || lab.speed <= 0
                || lab.recipeExecuteData is null)
            {
                return NotReady("An active matrix lab has an invalid runtime recipe or speed.");
            }

            var recipeError = TryValidateTheoreticalRecipe(
                lab.recipeExecuteData,
                ref sourceReferenceScanCount);
            if (recipeError is not null) return recipeError;
            if (!connected) continue;

            for (var index = 0; index < lab.recipeExecuteData.products.Length; index++)
            {
                AddTheoreticalRate(
                    rates,
                    lab.recipeExecuteData.products[index],
                    OverseerTheoreticalProductionCalculator.CalculateRecipeOutputPerMinute(
                        lab.speed,
                        lab.recipeExecuteData.timeSpend,
                        lab.incUsed,
                        lab.recipeExecuteData.productive,
                        lab.forceAccMode,
                        settings.ProductMultiplier,
                        settings.AccelerationMultiplier,
                        lab.recipeExecuteData.productCounts[index]));
            }
        }

        return null;
    }

    private static BridgeError? TryCaptureMinerTheoreticalRates(
        PlanetFactory factory,
        OverseerTheoreticalSettings settings,
        Dictionary<int, double> rates,
        ref long sourceReferenceScanCount)
    {
        var factorySystem = factory.factorySystem;
        for (var minerId = 1; minerId < factorySystem.minerCursor; minerId++)
        {
            ref var miner = ref factorySystem.minerPool[minerId];
            if (miner.id == 0) continue;
            if (miner.id != minerId || miner.type == EMinerType.None || miner.period <= 0 || miner.speed <= 0)
            {
                return NotReady("An active miner has inconsistent identity, type, period, or speed.");
            }

            var connectionError = TryGetTheoreticalConsumerConnection(
                factory,
                TheoreticalProducerKind.Miner,
                minerId,
                miner.entityId,
                miner.pcId,
                out var connected);
            if (connectionError is not null) return connectionError;
            if (!connected) continue;

            int productId;
            double sourceMultiplier;
            switch (miner.type)
            {
                case EMinerType.Vein:
                    if (miner.veinCount < 0)
                    {
                        return NotReady("An active vein miner has an invalid source-node index.");
                    }

                    if (miner.veinCount == 0) continue;
                    if (miner.veins is null || miner.veinCount > miner.veins.Length)
                    {
                        return NotReady("An active vein miner has an invalid source-node index.");
                    }

                    if (miner.currentVeinIndex < 0 || miner.currentVeinIndex >= miner.veinCount)
                    {
                        return NotReady("An active vein miner's current source-node index is invalid.");
                    }

                    if (!TryConsumeBudget(
                            ref sourceReferenceScanCount,
                            miner.veinCount,
                            MaximumOverseerTheoreticalSourceReferenceScanCount))
                    {
                        return OverseerScopeExceeded();
                    }

                    productId = 0;
                    for (var index = 0; index < miner.veinCount; index++)
                    {
                        var veinError = TryGetTheoreticalVein(factory, miner.veins[index], out var vein);
                        if (veinError is not null) return veinError;
                        if (productId == 0) productId = vein.productId;
                        if (vein.productId != productId)
                        {
                            return NotReady("A vein miner references source nodes with different products.");
                        }
                    }

                    var currentVeinId = miner.veins[miner.currentVeinIndex];
                    if (factory.veinPool[currentVeinId].productId != productId)
                    {
                        return NotReady("A vein miner's current source node does not match its source set.");
                    }

                    sourceMultiplier = miner.veinCount;
                    break;

                case EMinerType.Oil:
                    if (miner.veins is null || miner.veins.Length == 0)
                    {
                        return NotReady("An active oil extractor has no source-node identity.");
                    }

                    if (!TryConsumeBudget(
                            ref sourceReferenceScanCount,
                            1,
                            MaximumOverseerTheoreticalSourceReferenceScanCount))
                    {
                        return OverseerScopeExceeded();
                    }

                    var oilError = TryGetTheoreticalVein(factory, miner.veins[0], out var oilVein);
                    if (oilError is not null) return oilError;
                    if (oilVein.amount < 0)
                    {
                        return NotReady("An oil source has a negative runtime amount.");
                    }

                    productId = oilVein.productId;
                    sourceMultiplier = oilVein.amount * (double)VeinData.oilSpeedMultiplier;
                    break;

                case EMinerType.Water:
                    productId = factory.planet.waterItemId;
                    sourceMultiplier = 1d;
                    break;

                default:
                    return NotReady("An active miner uses an unsupported runtime miner type.");
            }

            if (productId <= 0 || LDB.items.Select(productId) is null)
            {
                return NotReady("An active miner has an invalid runtime product identity.");
            }

            AddTheoreticalRate(
                rates,
                productId,
                OverseerTheoreticalProductionCalculator.CalculateMinerOutputPerMinute(
                    miner.period,
                    settings.MiningSpeedScale,
                    miner.speed,
                    sourceMultiplier));
        }

        return null;
    }

    private static BridgeError? TryCaptureFractionatorTheoreticalRates(
        PlanetFactory factory,
        OverseerTheoreticalSettings settings,
        Dictionary<int, double> rates)
    {
        var factorySystem = factory.factorySystem;
        for (var fractionatorId = 1; fractionatorId < factorySystem.fractionatorCursor; fractionatorId++)
        {
            ref var fractionator = ref factorySystem.fractionatorPool[fractionatorId];
            if (fractionator.id == 0) continue;
            if (fractionator.id != fractionatorId)
            {
                return NotReady("An active fractionator does not match its component-pool identity.");
            }

            var connectionError = TryGetTheoreticalConsumerConnection(
                factory,
                TheoreticalProducerKind.Fractionator,
                fractionatorId,
                fractionator.entityId,
                fractionator.pcId,
                out var connected);
            if (connectionError is not null) return connectionError;
            if (!connected || fractionator.productId <= 0) continue;
            if (LDB.items.Select(fractionator.productId) is null)
            {
                return NotReady("An active fractionator has an invalid runtime product identity.");
            }

            AddTheoreticalRate(
                rates,
                fractionator.productId,
                OverseerTheoreticalProductionCalculator.CalculateFractionatorOutputPerMinute(
                    fractionator.incUsed,
                    settings.AccelerationMultiplier,
                    fractionator.produceProb,
                    settings.FractionatorStackMultiplier));
        }

        return null;
    }

    private static BridgeError? TryCaptureGammaTheoreticalRates(
        PlanetFactory factory,
        Dictionary<int, double> rates)
    {
        var powerSystem = factory.powerSystem;
        for (var generatorId = 1; generatorId < powerSystem.genCursor; generatorId++)
        {
            ref var generator = ref powerSystem.genPool[generatorId];
            if (generator.id == 0) continue;
            if (generator.id != generatorId)
            {
                return NotReady("An active power generator does not match its component-pool identity.");
            }

            var connectionError = TryGetTheoreticalGeneratorConnection(
                factory,
                generatorId,
                ref generator,
                out var connected);
            if (connectionError is not null) return connectionError;
            if (!connected || !generator.gamma || generator.productId <= 0) continue;
            if (LDB.items.Select(generator.productId) is null)
            {
                return NotReady("An active gamma receiver has an invalid runtime product identity.");
            }

            AddTheoreticalRate(
                rates,
                generator.productId,
                OverseerTheoreticalProductionCalculator.CalculateGammaOutputPerMinute(
                    generator.capacityCurrentTick,
                    generator.productHeat));
        }

        return null;
    }

    private static BridgeError? TryCaptureCollectorTheoreticalRates(
        PlanetFactory factory,
        OverseerTheoreticalSettings settings,
        Dictionary<int, double> rates,
        ref long sourceReferenceScanCount)
    {
        var transport = factory.transport;
        var collectorFactor = OverseerTheoreticalProductionCalculator.CalculateCollectorSpeedFactor(
            settings.MiningSpeedScale,
            factory.planet.gasTotalHeat,
            transport.collectorsWorkCost);
        for (var stationId = 1; stationId < transport.stationCursor; stationId++)
        {
            var station = transport.stationPool[stationId];
            if (station is null || station.id == 0) continue;
            if (station.id != stationId
                || station.entityId <= 0
                || station.entityId >= factory.entityCursor
                || station.entityId >= factory.entityPool.Length)
            {
                return NotReady("An active collector station does not match its station or entity pool identity.");
            }

            ref var entity = ref factory.entityPool[station.entityId];
            if (entity.id != station.entityId
                || entity.stationId != stationId
                || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(
                    station.isStellar,
                    station.planetId,
                    factory.planetId))
            {
                return NotReady("An active collector station has inconsistent entity or planet identity.");
            }

            if (!station.isCollector) continue;
            if (station.collectionIds is null
                || station.collectionPerTick is null
                || station.collectionIds.Length != station.collectionPerTick.Length)
            {
                return NotReady("An orbital collector has inconsistent runtime collection arrays.");
            }

            if (!TryConsumeBudget(
                    ref sourceReferenceScanCount,
                    station.collectionIds.Length,
                    MaximumOverseerTheoreticalSourceReferenceScanCount))
            {
                return OverseerScopeExceeded();
            }

            for (var index = 0; index < station.collectionIds.Length; index++)
            {
                var itemId = station.collectionIds[index];
                if (itemId <= 0 || LDB.items.Select(itemId) is null)
                {
                    return NotReady("An orbital collector has an invalid runtime product identity.");
                }

                AddTheoreticalRate(
                    rates,
                    itemId,
                    OverseerTheoreticalProductionCalculator.CalculateCollectorOutputPerMinute(
                        station.collectionPerTick[index],
                        collectorFactor));
            }
        }

        return null;
    }

    private static BridgeError? TryValidateTheoreticalRecipe(
        RecipeExecuteData recipe,
        ref long sourceReferenceScanCount)
    {
        if (recipe.requires is null
            || recipe.requireCounts is null
            || recipe.products is null
            || recipe.productCounts is null
            || recipe.timeSpend <= 0
            || recipe.requires.Length != recipe.requireCounts.Length
            || recipe.products.Length != recipe.productCounts.Length
            || recipe.products.Length == 0)
        {
            return NotReady("A configured production recipe has inconsistent runtime arrays or cycle time.");
        }

        if (!TryConsumeBudget(
                ref sourceReferenceScanCount,
                recipe.requires.Length + (long)recipe.products.Length,
                MaximumOverseerTheoreticalSourceReferenceScanCount))
        {
            return OverseerScopeExceeded();
        }

        for (var index = 0; index < recipe.requires.Length; index++)
        {
            if (recipe.requires[index] <= 0
                || recipe.requireCounts[index] <= 0
                || LDB.items.Select(recipe.requires[index]) is null)
            {
                return NotReady("A configured production recipe has an invalid runtime input.");
            }
        }

        for (var index = 0; index < recipe.products.Length; index++)
        {
            if (recipe.products[index] <= 0
                || recipe.productCounts[index] <= 0
                || LDB.items.Select(recipe.products[index]) is null)
            {
                return NotReady("A configured production recipe has an invalid runtime output.");
            }
        }

        return null;
    }

    private static BridgeError? TryGetTheoreticalConsumerConnection(
        PlanetFactory factory,
        TheoreticalProducerKind producerKind,
        int componentId,
        int entityId,
        int consumerId,
        out bool connected)
    {
        connected = false;
        if (entityId <= 0
            || entityId >= factory.entityCursor
            || entityId >= factory.entityPool.Length
            || consumerId <= 0)
        {
            return NotReady("A theoretical-production component has an invalid entity or power-consumer identity.");
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId
            || entity.powerConId != consumerId
            || GetTheoreticalProducerComponentId(ref entity, producerKind) != componentId)
        {
            return NotReady("A theoretical-production component does not match its entity identity.");
        }

        var powerSystem = factory.powerSystem;
        if (consumerId >= powerSystem.consumerCursor
            || consumerId >= powerSystem.consumerPool.Length)
        {
            return NotReady("A theoretical-production component references an invalid power consumer.");
        }

        ref var consumer = ref powerSystem.consumerPool[consumerId];
        if (consumer.id != consumerId || consumer.entityId != entityId || consumer.networkId < 0)
        {
            return NotReady("A theoretical-production component does not match its power-consumer identity.");
        }

        if (consumer.networkId == 0) return null;
        var networkError = TryValidateTheoreticalNetwork(powerSystem, consumer.networkId);
        if (networkError is not null) return networkError;
        connected = true;
        return null;
    }

    private static BridgeError? TryGetTheoreticalGeneratorConnection(
        PlanetFactory factory,
        int generatorId,
        ref PowerGeneratorComponent generator,
        out bool connected)
    {
        connected = false;
        if (generator.entityId <= 0
            || generator.entityId >= factory.entityCursor
            || generator.entityId >= factory.entityPool.Length
            || generator.networkId < 0)
        {
            return NotReady("A power generator has an invalid entity or network identity.");
        }

        ref var entity = ref factory.entityPool[generator.entityId];
        if (entity.id != generator.entityId || entity.powerGenId != generatorId)
        {
            return NotReady("A power generator does not match its entity identity.");
        }

        if (generator.networkId == 0) return null;
        var networkError = TryValidateTheoreticalNetwork(factory.powerSystem, generator.networkId);
        if (networkError is not null) return networkError;
        connected = true;
        return null;
    }

    private static BridgeError? TryValidateTheoreticalNetwork(PowerSystem powerSystem, int networkId)
    {
        if (networkId <= 0
            || networkId >= powerSystem.netCursor
            || networkId >= powerSystem.netPool.Length)
        {
            return NotReady("A theoretical-production component references an invalid power network.");
        }

        var network = powerSystem.netPool[networkId];
        return network is null || network.id != networkId
            ? NotReady("A theoretical-production component's power network does not match its pool identity.")
            : null;
    }

    private static BridgeError? TryGetTheoreticalVein(
        PlanetFactory factory,
        int veinId,
        out VeinData vein)
    {
        vein = default;
        if (veinId <= 0 || veinId >= factory.veinCursor || veinId >= factory.veinPool.Length)
        {
            return NotReady("A miner references a source node outside the active vein pool.");
        }

        ref var candidate = ref factory.veinPool[veinId];
        if (candidate.id != veinId || candidate.productId <= 0 || LDB.items.Select(candidate.productId) is null)
        {
            return NotReady("A miner's source node has inconsistent identity or product state.");
        }

        vein = candidate;
        return null;
    }

    private static void AddTheoreticalRate(
        Dictionary<int, double> rates,
        int itemId,
        double contribution)
    {
        if (!rates.TryGetValue(itemId, out var current)) return;
        rates[itemId] = OverseerTheoreticalProductionCalculator.AddRates(current, contribution);
    }

    private static int GetTheoreticalProducerComponentId(
        ref EntityData entity,
        TheoreticalProducerKind producerKind)
    {
        return producerKind switch
        {
            TheoreticalProducerKind.Assembler => entity.assemblerId,
            TheoreticalProducerKind.Lab => entity.labId,
            TheoreticalProducerKind.Miner => entity.minerId,
            TheoreticalProducerKind.Fractionator => entity.fractionatorId,
            _ => 0,
        };
    }

    private static bool IsValidPoolCursor(int cursor, int poolLength) =>
        cursor >= 1 && cursor <= poolLength;

    private static bool TryConsumeBudget(ref long total, long additional, long maximum)
    {
        if (additional < 0 || additional > maximum || total > maximum - additional)
        {
            return false;
        }

        total += additional;
        return true;
    }

    public GameCallResult<ListAssemblersResult> ListAssemblersOnMainThread(
        string? requestedSessionId,
        ListAssemblersRequest request)
    {
        var accessError = ValidateOwnedSessionOnMainThread(requestedSessionId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<ListAssemblersResult>.Failed(accessError);
        }

        var limit = request.Limit == 0 ? DefaultLimit : request.Limit;
        if (limit < 1 || limit > MaximumLimit)
        {
            return GameCallResult<ListAssemblersResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                $"Assembler list limit must be between 1 and {MaximumLimit}.",
                false,
                "Use a bounded limit and retry."));
        }

        var filterHash = ComputeFilterHash($"assemblers|limit={limit}");
        SnapshotPage<AssemblerSnapshot>? page;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var status = _assemblerSnapshots.TryGetPage(
                request.Cursor,
                _sessions.SessionId!,
                factory!.planetId,
                filterHash,
                limit,
                out page);
            if (status != SnapshotCursorStatus.Success || page is null)
            {
                return GameCallResult<ListAssemblersResult>.Failed(StaleCursor(
                    "The assembler cursor is unknown, expired, or bound to a different session, planet, or page size."));
            }
        }
        else
        {
            var assemblers = new List<AssemblerSnapshot>();
            var pool = factory!.factorySystem.assemblerPool;
            var cursor = Math.Min(factory.factorySystem.assemblerCursor, pool.Length);
            for (var componentId = 1; componentId < cursor; componentId++)
            {
                ref var assembler = ref pool[componentId];
                if (assembler.id != componentId)
                {
                    continue;
                }

                var snapshot = TryCaptureAssembler(factory, componentId, ref assembler);
                if (snapshot is not null)
                {
                    assemblers.Add(snapshot);
                }
            }

            if (!_assemblerSnapshots.TryCreate(
                    _sessions.SessionId!,
                    factory.planetId,
                    filterHash,
                    assemblers,
                    limit,
                    out page)
                || page is null)
            {
                return GameCallResult<ListAssemblersResult>.Failed(SnapshotCapacityExceeded("assembler"));
            }
        }

        return GameCallResult<ListAssemblersResult>.Succeeded(new ListAssemblersResult
        {
            Revision = _sessions.Revision,
            Assemblers = page.Items.ToList(),
            NextCursor = page.NextCursor,
        });
    }

    public GameCallResult<AssemblerSnapshot> InspectAssemblerOnMainThread(
        string? requestedSessionId,
        InspectAssemblerRequest request)
    {
        var accessError = ValidateOwnedSessionOnMainThread(requestedSessionId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<AssemblerSnapshot>.Failed(accessError);
        }

        if (request.EntityId <= 0 || request.EntityId >= factory!.entityCursor)
        {
            return InvalidAssembler("The requested entity does not exist in the current factory.");
        }

        ref var entity = ref factory.entityPool[request.EntityId];
        if (entity.id != request.EntityId || entity.assemblerId <= 0)
        {
            return InvalidAssembler("The requested entity is missing or is not an assembler.");
        }

        var componentId = entity.assemblerId;
        if (componentId >= factory.factorySystem.assemblerCursor)
        {
            return InvalidAssembler("The assembler component is no longer valid.");
        }

        ref var assembler = ref factory.factorySystem.assemblerPool[componentId];
        var snapshot = assembler.id == componentId && assembler.entityId == request.EntityId
            ? TryCaptureAssembler(factory, componentId, ref assembler)
            : null;
        return snapshot is null
            ? InvalidAssembler("The assembler component is no longer valid.")
            : GameCallResult<AssemblerSnapshot>.Succeeded(snapshot);
    }

    public GameCallResult<BuildCatalog> GetBuildCatalogOnMainThread(string? requestedSessionId)
    {
        var accessError = ValidateOwnedSessionOnMainThread(requestedSessionId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<BuildCatalog>.Failed(accessError);
        }

        var player = GameMain.mainPlayer;
        var history = GameMain.history;
        if (player is null || history is null || player.controller?.actionBuild is null)
        {
            return GameCallResult<BuildCatalog>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The player build system is not ready in the owned ordinary world.",
                true,
                "Wait until the player and local factory finish loading, then retry."));
        }

        var result = new BuildCatalog
        {
            PlanetId = factory!.planetId,
            Revision = _sessions.Revision,
            PlayerPosition = new Vector3Snapshot
            {
                X = player.position.x,
                Y = player.position.y,
                Z = player.position.z,
            },
            PlayerBuildArea = player.mecha.buildArea,
            SandboxToolsEnabled = GameMain.sandboxToolsEnabled,
        };

        foreach (var item in LDB.items.dataArray)
        {
            if (item is null || !item.CanBuild || item.prefabDesc is null)
            {
                continue;
            }

            var role = GetBasicLineRole(item.prefabDesc);
            if (role is null)
            {
                continue;
            }

            result.Buildings.Add(new BuildCatalogItem
            {
                ItemId = item.ID,
                Name = item.name ?? string.Empty,
                Role = role,
                ModelIndex = item.ModelIndex,
                Grade = item.Grade,
                SupportedUpgradeTargetItemIds = GetSupportedUpgradeTargets(item),
                BuildMode = item.BuildMode,
                Unlocked = history.ItemUnlocked(item.ID),
                Available = history.ItemUnlocked(item.ID),
                RecipeType = item.prefabDesc.assemblerRecipeType.ToString(),
                SlotCount = item.prefabDesc.slotPoses?.Length ?? 0,
                RoughRadius = item.prefabDesc.roughRadius,
                PlacementRadius = CapturePlacementRadius(item.prefabDesc),
                PowerConnectDistance = item.prefabDesc.powerConnectDistance,
                PowerCoverRadius = item.prefabDesc.powerCoverRadius,
                ProductionSpeedRaw = item.prefabDesc.isAssembler ? item.prefabDesc.assemblerSpeed
                    : item.prefabDesc.isLab ? item.prefabDesc.labAssembleSpeed : (int?)null,
                BeltSpeedRaw = item.prefabDesc.isBelt ? item.prefabDesc.beltSpeed : (int?)null,
                InserterSttRaw = item.prefabDesc.isInserter ? item.prefabDesc.inserterSTT : (int?)null,
                InserterGrade = item.prefabDesc.isInserter ? item.prefabDesc.inserterGrade : (int?)null,
                WorkEnergyPerTick = item.prefabDesc.workEnergyPerTick,
                IdleEnergyPerTick = item.prefabDesc.idleEnergyPerTick,
                NativePowerProfileKnown = true,
                IsPowerConsumer = item.prefabDesc.isPowerConsumer,
                IsPowerNode = item.prefabDesc.isPowerNode,
                IsPowerCharger = item.prefabDesc.isPowerCharger,
                WindGenerationAtCurrentPlanetPerTick = CaptureFoundryWindGeneration(item.prefabDesc, factory!.planet.windStrength),
                FuelPowerProfile = CaptureFuelPowerProfile(item.ID, item.prefabDesc),
            });
        }

        foreach (var recipe in LDB.recipes.dataArray)
        {
            if (recipe is null
                || recipe.Items is null
                || recipe.Results is null
                || recipe.ItemCounts is null
                || recipe.ResultCounts is null)
            {
                continue;
            }

            var recipeSnapshot = new BuildCatalogRecipe
            {
                RecipeId = recipe.ID,
                Name = recipe.name ?? string.Empty,
                RecipeType = recipe.Type.ToString(),
                Unlocked = history.RecipeUnlocked(recipe.ID),
                TimeSpend = recipe.TimeSpend,
            };
            for (var index = 0; index < Math.Min(recipe.Items.Length, recipe.ItemCounts.Length); index++)
            {
                recipeSnapshot.Inputs.Add(CreateIngredient(recipe.Items[index], recipe.ItemCounts[index]));
            }

            for (var index = 0; index < Math.Min(recipe.Results.Length, recipe.ResultCounts.Length); index++)
            {
                recipeSnapshot.Outputs.Add(CreateIngredient(recipe.Results[index], recipe.ResultCounts[index]));
            }

            result.Recipes.Add(recipeSnapshot);
        }

        result.Buildings = result.Buildings
            .OrderBy(item => item.Role, StringComparer.Ordinal)
            .ThenByDescending(item => item.Unlocked)
            .ThenBy(item => item.Grade)
            .ThenBy(item => item.ItemId)
            .ToList();
        result.Recipes = result.Recipes
            .OrderByDescending(recipe => recipe.Unlocked)
            .ThenByDescending(recipe => recipe.Inputs.Any(input => input.RawMaterial))
            .ThenBy(recipe => recipe.RecipeId)
            .ToList();
        result.RecommendedBasicLine = CreateRecommendation(result);

        return GameCallResult<BuildCatalog>.Succeeded(result);
    }

    private List<ResourceNodeSnapshot> CaptureResourceNodes(
        PlanetFactory factory,
        string? kind,
        string? resourceType,
        int? productItemId)
    {
        var result = new List<ResourceNodeSnapshot>();
        if (kind is null || kind == ResourceNodeKinds.Vein)
        {
            var limit = Math.Min(factory.veinCursor, factory.veinPool?.Length ?? 0);
            for (var nodeId = 1; nodeId < limit; nodeId++)
            {
                var snapshot = TryCaptureVein(factory, nodeId);
                if (snapshot is not null && ResourceMatches(snapshot, resourceType, productItemId))
                {
                    result.Add(snapshot);
                }
            }
        }

        if (kind is null || kind == ResourceNodeKinds.Vegetation)
        {
            var limit = Math.Min(factory.vegeCursor, factory.vegePool?.Length ?? 0);
            for (var nodeId = 1; nodeId < limit; nodeId++)
            {
                var snapshot = TryCaptureVegetation(factory, nodeId);
                if (snapshot is not null && ResourceMatches(snapshot, resourceType, productItemId))
                {
                    result.Add(snapshot);
                }
            }
        }

        return result
            .OrderBy(node => node.Kind, StringComparer.Ordinal)
            .ThenBy(node => node.NodeId)
            .ToList();
    }

    private ResourceNodeSnapshot? TryCaptureVein(PlanetFactory factory, int nodeId)
    {
        if (nodeId <= 0 || nodeId >= factory.veinCursor || nodeId >= factory.veinPool.Length)
        {
            return null;
        }

        ref var vein = ref factory.veinPool[nodeId];
        if (vein.id != nodeId || vein.type == EVeinType.None)
        {
            return null;
        }

        var player = GameMain.mainPlayer;
        var distance = player is null ? -1f : (vein.pos - player.position).magnitude;
        var veinProto = LDB.veins.Select((int)vein.type);
        var snapshot = new ResourceNodeSnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory.planetId,
            Kind = ResourceNodeKinds.Vein,
            NodeId = nodeId,
            ResourceType = vein.type.ToString(),
            ProtoId = (int)vein.type,
            Name = veinProto?.name ?? vein.type.ToString(),
            RemainingAmount = vein.amount,
            GroupIndex = vein.groupIndex,
            MinerCount = vein.minerCount,
            Position = CaptureVector(vein.pos),
            DistanceFromPlayer = distance,
            SameLocalPlanet = player?.planetId == factory.planetId,
            WithinPlayerBuildArea = player?.mecha is not null && distance <= player.mecha.buildArea,
            CapturedAtGameTick = GameMain.gameTick,
        };
        if (vein.productId > 0)
        {
            snapshot.Yields.Add(new ResourceYieldSnapshot
            {
                ItemId = vein.productId,
                Name = GetItemName(vein.productId),
                Count = 1,
                Chance = 1f,
            });
        }

        snapshot.StateHash = CanonicalStateHash.Resource(snapshot);
        snapshot.StateHashVersion = CanonicalStateHash.Version;
        return snapshot;
    }

    private ResourceNodeSnapshot? TryCaptureVegetation(PlanetFactory factory, int nodeId)
    {
        if (nodeId <= 0 || nodeId >= factory.vegeCursor || nodeId >= factory.vegePool.Length)
        {
            return null;
        }

        ref var vegetation = ref factory.vegePool[nodeId];
        if (vegetation.id != nodeId || vegetation.protoId <= 0)
        {
            return null;
        }

        var proto = LDB.veges.Select(vegetation.protoId);
        if (proto is null)
        {
            return null;
        }

        var player = GameMain.mainPlayer;
        var distance = player is null ? -1f : (vegetation.pos - player.position).magnitude;
        var snapshot = new ResourceNodeSnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory.planetId,
            Kind = ResourceNodeKinds.Vegetation,
            NodeId = nodeId,
            ResourceType = proto.Type.ToString(),
            ProtoId = vegetation.protoId,
            Name = proto.name ?? string.Empty,
            RemainingAmount = 1,
            Position = CaptureVector(vegetation.pos),
            DistanceFromPlayer = distance,
            SameLocalPlanet = player?.planetId == factory.planetId,
            WithinPlayerBuildArea = player?.mecha is not null && distance <= player.mecha.buildArea,
            CapturedAtGameTick = GameMain.gameTick,
        };
        var miningItems = proto.MiningItem ?? Array.Empty<int>();
        var miningCounts = proto.MiningCount ?? Array.Empty<int>();
        var miningChances = proto.MiningChance ?? Array.Empty<float>();
        var yieldCount = Math.Min(
            miningItems.Length,
            Math.Min(miningCounts.Length, miningChances.Length));
        for (var index = 0; index < yieldCount; index++)
        {
            snapshot.Yields.Add(new ResourceYieldSnapshot
            {
                ItemId = miningItems[index],
                Name = GetItemName(miningItems[index]),
                Count = miningCounts[index],
                Chance = miningChances[index],
            });
        }

        snapshot.StateHash = CanonicalStateHash.Resource(snapshot);
        snapshot.StateHashVersion = CanonicalStateHash.Version;
        return snapshot;
    }

    private List<FactoryEntitySnapshot> CaptureFactoryEntities(
        PlanetFactory factory,
        string? objectKind,
        string? componentKind,
        int? itemId)
    {
        var result = new List<FactoryEntitySnapshot>();
        if (objectKind is null || objectKind == FactoryObjectKinds.Entity)
        {
            var entityLimit = Math.Min(factory.entityCursor, factory.entityPool?.Length ?? 0);
            for (var entityId = 1; entityId < entityLimit; entityId++)
            {
                var snapshot = TryCaptureFactoryEntity(factory, entityId);
                if (snapshot is not null && FactoryEntityMatches(snapshot, componentKind, itemId))
                {
                    result.Add(snapshot);
                }
            }
        }

        if (objectKind is null || objectKind == FactoryObjectKinds.Prebuild)
        {
            var prebuildLimit = Math.Min(factory.prebuildCursor, factory.prebuildPool?.Length ?? 0);
            for (var prebuildId = 1; prebuildId < prebuildLimit; prebuildId++)
            {
                var snapshot = TryCapturePrebuild(factory, prebuildId);
                if (snapshot is not null && FactoryEntityMatches(snapshot, componentKind, itemId))
                {
                    result.Add(snapshot);
                }
            }
        }

        return result
            .OrderBy(snapshot => snapshot.ObjectKind, StringComparer.Ordinal)
            .ThenBy(snapshot => Math.Abs(snapshot.ObjectId))
            .ToList();
    }

    private FactoryEntitySnapshot? TryCaptureFactoryEntity(PlanetFactory factory, int entityId)
    {
        if (entityId <= 0 || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length)
        {
            return null;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.protoId <= 0)
        {
            return null;
        }

        var item = LDB.items.Select(entity.protoId);
        var snapshot = new FactoryEntitySnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory.planetId,
            ObjectId = entityId,
            ObjectKind = FactoryObjectKinds.Entity,
            ItemId = entity.protoId,
            Name = item?.name ?? string.Empty,
            ComponentKind = GetComponentKind(ref entity),
            Position = CaptureVector(entity.pos),
            Rotation = CaptureQuaternion(entity.rot),
            CapturedAtGameTick = GameMain.gameTick,
        };

        CaptureConnections(factory, entityId, snapshot.Connections);
        CapturePower(factory, ref entity, snapshot);
        CaptureAssembler(factory, ref entity, snapshot);
        CaptureLab(factory, ref entity, snapshot);
        CaptureMiner(factory, ref entity, snapshot);
        CaptureStorage(factory, ref entity, snapshot);
        CaptureLogisticsStation(factory, ref entity, snapshot);
        CaptureTank(factory, ref entity, snapshot);
        CaptureInserter(factory, ref entity, snapshot);
        snapshot.StateHash = CanonicalStateHash.Factory(snapshot);
        snapshot.StateHashVersion = CanonicalStateHash.Version;
        snapshot.ConfigurationStateHash = CanonicalStateHash.FactoryConfiguration(snapshot);
        snapshot.ConfigurationStateHashVersion = CanonicalStateHash.Version;
        snapshot.EndpointStateHash = CanonicalStateHash.FactoryEndpoint(snapshot);
        snapshot.EndpointStateHashVersion = CanonicalStateHash.Version;
        return snapshot;
    }

    private FactoryEntitySnapshot? TryCapturePrebuild(PlanetFactory factory, int prebuildId)
    {
        if (prebuildId <= 0 || prebuildId >= factory.prebuildCursor || prebuildId >= factory.prebuildPool.Length)
        {
            return null;
        }

        ref var prebuild = ref factory.prebuildPool[prebuildId];
        if (prebuild.id != prebuildId || prebuild.protoId <= 0 || prebuild.isDestroyed)
        {
            return null;
        }

        var item = LDB.items.Select(prebuild.protoId);
        var snapshot = new FactoryEntitySnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory.planetId,
            ObjectId = -prebuildId,
            ObjectKind = FactoryObjectKinds.Prebuild,
            ItemId = prebuild.protoId,
            Name = item?.name ?? string.Empty,
            ComponentKind = GetPrefabComponentKind(item?.prefabDesc),
            Position = CaptureVector(prebuild.pos),
            Rotation = CaptureQuaternion(prebuild.rot),
            RecipeId = prebuild.recipeId,
            RecipeName = prebuild.recipeId > 0 ? LDB.recipes.Select(prebuild.recipeId)?.name : null,
            RequiredBuildItemCount = prebuild.itemRequired,
            ConstructionProgress = prebuild.builderValue,
            CapturedAtGameTick = GameMain.gameTick,
        };
        CaptureConnections(factory, -prebuildId, snapshot.Connections);
        snapshot.StateHash = CanonicalStateHash.Factory(snapshot);
        snapshot.StateHashVersion = CanonicalStateHash.Version;
        snapshot.ConfigurationStateHash = CanonicalStateHash.FactoryConfiguration(snapshot);
        snapshot.ConfigurationStateHashVersion = CanonicalStateHash.Version;
        snapshot.EndpointStateHash = CanonicalStateHash.FactoryEndpoint(snapshot);
        snapshot.EndpointStateHashVersion = CanonicalStateHash.Version;
        return snapshot;
    }

    private static void CaptureConnections(
        PlanetFactory factory,
        int objectId,
        ICollection<FactoryConnectionSnapshot> result)
    {
        for (var slot = 0; slot < 16; slot++)
        {
            factory.ReadObjectConn(objectId, slot, out var isOutput, out var otherObjectId, out var otherSlot);
            if (otherObjectId == 0)
            {
                continue;
            }

            result.Add(new FactoryConnectionSnapshot
            {
                Slot = slot,
                IsOutput = isOutput,
                OtherObjectId = otherObjectId,
                OtherSlot = otherSlot,
            });
        }
    }

    private static void CapturePower(
        PlanetFactory factory,
        ref EntityData entity,
        FactoryEntitySnapshot snapshot)
    {
        var consumerId = entity.powerConId;
        if (consumerId > 0
            && consumerId < factory.powerSystem.consumerCursor
            && consumerId < factory.powerSystem.consumerPool.Length)
        {
            ref var consumer = ref factory.powerSystem.consumerPool[consumerId];
            if (consumer.id == consumerId && consumer.entityId == entity.id)
            {
                snapshot.PowerNetworkId = consumer.networkId;
                snapshot.PowerDemandPerTick = consumer.requiredEnergy;
                snapshot.PowerServeRatio = GetPowerServeRatio(factory.powerSystem, consumer.networkId);
                return;
            }
        }

        var generatorId = entity.powerGenId;
        if (generatorId > 0
            && generatorId < factory.powerSystem.genCursor
            && generatorId < factory.powerSystem.genPool.Length)
        {
            ref var generator = ref factory.powerSystem.genPool[generatorId];
            if (generator.id == generatorId && generator.entityId == entity.id)
            {
                snapshot.PowerNetworkId = generator.networkId;
                snapshot.PowerServeRatio = GetPowerServeRatio(factory.powerSystem, generator.networkId);
                snapshot.Buffers.Add(FactoryBufferSemantics.PowerGeneration(
                    generator.generateCurrentTick, generator.curFuelId, GetItemName(generator.curFuelId)));
            }
        }
    }

    private static double? GetPowerServeRatio(PowerSystem powerSystem, int networkId)
    {
        if (networkId <= 0 || networkId >= powerSystem.netCursor || networkId >= powerSystem.netPool.Length)
        {
            return null;
        }

        var network = powerSystem.netPool[networkId];
        return network is not null && network.id == networkId ? network.consumerRatio : null;
    }

    private static void CaptureAssembler(
        PlanetFactory factory,
        ref EntityData entity,
        FactoryEntitySnapshot snapshot)
    {
        var assemblerId = entity.assemblerId;
        if (assemblerId <= 0
            || assemblerId >= factory.factorySystem.assemblerCursor
            || assemblerId >= factory.factorySystem.assemblerPool.Length)
        {
            return;
        }

        ref var assembler = ref factory.factorySystem.assemblerPool[assemblerId];
        if (assembler.id != assemblerId || assembler.entityId != entity.id)
        {
            return;
        }

        snapshot.RecipeId = assembler.recipeId;
        snapshot.ForceAccelerationMode = assembler.forceAccMode;
        snapshot.RecipeName = assembler.recipeId > 0 ? LDB.recipes.Select(assembler.recipeId)?.name : null;
        snapshot.IsWorking = assembler.replicating;
        snapshot.Progress = assembler.time;
        snapshot.ProgressRequired = assembler.recipeExecuteData?.timeSpend ?? 0;
        if (assembler.recipeExecuteData is not null)
        {
            AddFactoryBuffers(snapshot.Buffers, "input", assembler.recipeExecuteData.requires, assembler.served, assembler.incServed);
            AddFactoryBuffers(snapshot.Buffers, "output", assembler.recipeExecuteData.products, assembler.produced, null);
        }
    }

    private static void CaptureLab(
        PlanetFactory factory,
        ref EntityData entity,
        FactoryEntitySnapshot snapshot)
    {
        var labId = entity.labId;
        if (labId <= 0
            || labId >= factory.factorySystem.labCursor
            || labId >= factory.factorySystem.labPool.Length)
        {
            return;
        }

        ref var lab = ref factory.factorySystem.labPool[labId];
        if (lab.id != labId || lab.entityId != entity.id)
        {
            return;
        }

        snapshot.RecipeId = lab.recipeId;
        snapshot.RecipeName = lab.recipeId > 0 ? LDB.recipes.Select(lab.recipeId)?.name : null;
        snapshot.IsWorking = lab.replicating;
        snapshot.Progress = lab.researchMode ? lab.hashBytes : lab.time;
        snapshot.ProgressRequired = lab.researchMode
            ? (GameMain.history?.techStates.TryGetValue(lab.techId, out var techState) == true
                ? (int)Math.Min(int.MaxValue, techState.hashNeeded)
                : 0)
            : lab.recipeExecuteData?.timeSpend ?? 0;
        if (lab.researchMode)
        {
            AddFactoryBuffers(snapshot.Buffers, "research-matrix", LabComponent.matrixIds, lab.matrixServed, lab.matrixIncServed, 3600);
        }
        else if (lab.recipeExecuteData is not null)
        {
            AddFactoryBuffers(snapshot.Buffers, "input", lab.recipeExecuteData.requires, lab.served, lab.incServed);
            AddFactoryBuffers(snapshot.Buffers, "output", lab.recipeExecuteData.products, lab.produced, null);
        }
    }

    private static void CaptureMiner(
        PlanetFactory factory,
        ref EntityData entity,
        FactoryEntitySnapshot snapshot)
    {
        var minerId = entity.minerId;
        if (minerId <= 0
            || minerId >= factory.factorySystem.minerCursor
            || minerId >= factory.factorySystem.minerPool.Length)
        {
            return;
        }

        ref var miner = ref factory.factorySystem.minerPool[minerId];
        if (miner.id != minerId || miner.entityId != entity.id)
        {
            return;
        }

        snapshot.IsWorking = miner.workstate != EWorkState.Idle;
        snapshot.Progress = miner.time;
        snapshot.ProgressRequired = miner.period;
        snapshot.InsertTargetObjectId = miner.insertTarget == 0 ? null : miner.insertTarget;
        var minerVeins = miner.veins ?? Array.Empty<int>();
        var veinCount = Math.Min(miner.veinCount, minerVeins.Length);
        for (var index = 0; index < veinCount; index++)
        {
            if (minerVeins[index] > 0)
            {
                snapshot.ResourceNodeIds.Add(minerVeins[index]);
            }
        }

        if (miner.productId > 0 || miner.productCount > 0)
        {
            snapshot.Buffers.Add(new FactoryBufferSnapshot
            {
                Role = "mined-output",
                ItemId = miner.productId,
                Name = GetItemName(miner.productId),
                Count = miner.productCount,
            });
        }
    }

    private static void CaptureStorage(
        PlanetFactory factory,
        ref EntityData entity,
        FactoryEntitySnapshot snapshot)
    {
        var storageId = entity.storageId;
        if (storageId <= 0
            || storageId >= factory.factoryStorage.storageCursor
            || storageId >= factory.factoryStorage.storagePool.Length)
        {
            return;
        }

        var storage = factory.factoryStorage.storagePool[storageId];
        if (storage is null || storage.id != storageId || storage.entityId != entity.id)
        {
            return;
        }

        var storageGrids = storage.grids ?? Array.Empty<StorageComponent.GRID>();
        var gridCount = Math.Min(storage.size, storageGrids.Length);
        snapshot.StorageConfiguration = CaptureStorageConfiguration(storage);
        for (var index = 0; index < gridCount; index++)
        {
            var grid = storageGrids[index];
            if (grid.itemId <= 0 || grid.count <= 0)
            {
                continue;
            }

            snapshot.Buffers.Add(new FactoryBufferSnapshot
            {
                Role = "storage",
                ItemId = grid.itemId,
                Name = GetItemName(grid.itemId),
                Count = grid.count,
                Inc = grid.inc,
            });
        }
    }

    internal static StorageConfigurationSnapshot? CaptureStorageConfiguration(StorageComponent storage)
    {
        if (storage.size < 1 || storage.size > BlueprintStoragePolicy.MaximumGridCount
            || storage.grids is null || storage.grids.Length < storage.size
            || storage.bans < 0 || storage.bans > storage.size
            || (storage.type != EStorageType.Default && storage.type != EStorageType.Filtered)) return null;
        return new StorageConfigurationSnapshot
        {
            GridCount = storage.size, BannedGridCount = storage.bans,
            Mode = storage.type == EStorageType.Default ? "default" : "filtered",
            GridFilterItemIds = storage.grids.Take(storage.size).Select(grid => grid.filter).ToList(),
        };
    }

    private void CaptureLogisticsStation(
        PlanetFactory factory,
        ref EntityData entity,
        FactoryEntitySnapshot snapshot)
    {
        var stationId = entity.stationId;
        var transport = factory.transport;
        if (stationId <= 0
            || transport?.stationPool is null
            || stationId >= transport.stationCursor
            || stationId >= transport.stationPool.Length)
        {
            return;
        }

        var station = transport.stationPool[stationId];
        if (station is null
            || station.id != stationId
            || station.entityId != entity.id
            || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(
                station.isStellar,
                station.planetId,
                factory.planetId))
        {
            return;
        }

        var maximumChargeEnergyPerTick = 0L;
        var consumerId = station.pcId;
        if (consumerId > 0
            && consumerId == entity.powerConId
            && consumerId < factory.powerSystem.consumerCursor
            && consumerId < factory.powerSystem.consumerPool.Length)
        {
            ref var consumer = ref factory.powerSystem.consumerPool[consumerId];
            if (consumer.id == consumerId && consumer.entityId == entity.id)
            {
                maximumChargeEnergyPerTick = consumer.workEnergyPerTick;
            }
        }

        var result = new LogisticsStationSnapshot
        {
            SessionId = _sessions.SessionId!,
            PlanetId = factory.planetId,
            EntityId = entity.id,
            StationId = station.id,
            GalacticStationId = station.gid,
            BuildingItemId = entity.protoId,
            BuildingName = GetItemName(entity.protoId),
            Position = CaptureVector(entity.pos),
            IsInterstellar = station.isStellar,
            IsCollector = station.isCollector,
            IsVeinCollector = station.isVeinCollector,
            PowerNetworkId = snapshot.PowerNetworkId,
            PowerServeRatio = snapshot.PowerServeRatio,
            Energy = station.energy,
            EnergyCapacity = station.energyMax,
            RequestedChargeEnergyPerTick = station.energyPerTick,
            RequestedChargePowerWatts = station.energyPerTick * 60L,
            MaximumChargeEnergyPerTick = maximumChargeEnergyPerTick,
            MaximumChargePowerWatts = maximumChargeEnergyPerTick * 60L,
            WarperCount = station.warperCount,
            WarperCapacity = station.warperMaxCount,
            IdleDroneCount = station.idleDroneCount,
            DroneCapacity = LDB.items.Select(entity.protoId)?.prefabDesc?.stationMaxDroneCount ?? 0,
            WorkingDroneCount = station.workDroneCount,
            IdleVesselCount = station.idleShipCount,
            VesselCapacity = LDB.items.Select(entity.protoId)?.prefabDesc?.stationMaxShipCount ?? 0,
            WorkingVesselCount = station.workShipCount,
            DroneTripRangeRaw = station.tripRangeDrones,
            VesselTripRangeRaw = station.tripRangeShips,
            IncludeOrbitCollectors = station.includeOrbitCollector,
            WarpEnableDistanceRaw = station.warpEnableDist,
            WarpersRequired = station.warperNecessary,
            DroneDeliverySetting = station.deliveryDrones,
            VesselDeliverySetting = station.deliveryShips,
            PilerCount = station.pilerCount,
            DroneAutoReplenish = station.droneAutoReplenish,
            VesselAutoReplenish = station.shipAutoReplenish,
            RemoteGroupMask = station.remoteGroupMask,
            RemoteRoutePriority = station.routePriority.ToString(),
            CapturedAtGameTick = GameMain.gameTick,
        };

        foreach (var itemId in station.needs ?? Array.Empty<int>())
        {
            if (itemId > 0 && !result.NeededItemIds.Contains(itemId))
            {
                result.NeededItemIds.Add(itemId);
            }
        }

        var stores = station.storage ?? Array.Empty<StationStore>();
        for (var index = 0; index < stores.Length; index++)
        {
            var store = stores[index];
            result.StorageSlots.Add(new LogisticsStationStorageSlotSnapshot
            {
                Index = index,
                ItemId = store.itemId,
                ItemName = store.itemId > 0 ? GetItemName(store.itemId) : null,
                Count = store.count,
                Inc = store.inc,
                MaximumCount = store.max,
                LocalOrder = store.localOrder,
                RemoteOrder = store.remoteOrder,
                TotalOrdered = store.totalOrdered,
                LocalSupplyCount = store.localSupplyCount,
                LocalDemandCount = store.localDemandCount,
                RemoteSupplyCount = store.remoteSupplyCount,
                RemoteDemandCount = store.remoteDemandCount,
                LocalLogic = store.localLogic.ToString(),
                RemoteLogic = store.remoteLogic.ToString(),
                KeepMode = store.keepMode,
                KeepIncRatio = store.keepIncRatio,
            });
        }

        var beltSlots = station.slots ?? Array.Empty<SlotData>();
        for (var index = 0; index < beltSlots.Length; index++)
        {
            var slot = beltSlots[index];
            result.BeltSlots.Add(new LogisticsStationBeltSlotSnapshot
            {
                Index = index,
                Direction = slot.dir.ToString(),
                BeltComponentId = slot.beltId,
                BeltEntityId = ResolveBeltEntityId(factory, slot.beltId),
                StorageIndex = slot.storageIdx,
                Counter = slot.counter,
            });
        }

        result.NeededItemIds.Sort();
        result.StateHash = CanonicalStateHash.LogisticsStation(result);
        result.StateHashVersion = CanonicalStateHash.Version;
        result.ConfigurationStateHash = CanonicalStateHash.LogisticsStationConfiguration(result);
        result.ConfigurationStateHashVersion = CanonicalStateHash.Version;
        result.FleetStateHash = CanonicalStateHash.LogisticsStationFleet(result);
        result.FleetStateHashVersion = CanonicalStateHash.Version;
        snapshot.LogisticsStation = result;
    }

    private static int ResolveBeltEntityId(PlanetFactory factory, int beltId)
    {
        var traffic = factory.cargoTraffic;
        if (beltId <= 0
            || traffic?.beltPool is null
            || beltId >= traffic.beltCursor
            || beltId >= traffic.beltPool.Length)
        {
            return 0;
        }

        ref var belt = ref traffic.beltPool[beltId];
        return belt.id == beltId ? belt.entityId : 0;
    }

    public GameCallResult<LocalStarSystemSnapshot> GetLocalStarSystemOnMainThread(
        string? requestedSessionId,
        LocalPlanetRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out _);
        if (accessError is not null)
        {
            return GameCallResult<LocalStarSystemSnapshot>.Failed(accessError);
        }

        var player = GameMain.mainPlayer;
        var galaxy = GameMain.galaxy;
        var localPlanet = GameMain.localPlanet;
        var localStar = GameMain.localStar;
        if (player is null || galaxy is null || localPlanet is null || localStar?.planets is null)
        {
            return GameCallResult<LocalStarSystemSnapshot>.Failed(NotReady(
                "The local star system is not ready in the owned ordinary world."));
        }

        var planets = new List<PlanetSnapshot>();
        foreach (var planet in localStar.planets.Where(candidate => candidate is not null).OrderBy(candidate => candidate.id))
        {
            var theme = LDB.themes.Select(planet.theme);
            var visibility = VoyagerVisibilityPolicy.EvaluatePlanet(GameMain.history?.universeObserveLevel ?? -1,
                planet.id == localPlanet.id, true, planet.factory is not null,
                (planet.uPosition - player.uPosition).magnitude, false, false);
            var resources = visibility.ObservationAllowed ? CapturePotentialResourceTypes(theme) : new List<string>();
            var universalPosition = planet.uPosition;
            planets.Add(new PlanetSnapshot
            {
                PlanetId = planet.id,
                Name = planet.displayName,
                PlanetType = planet.type.ToString(),
                ThemeId = visibility.ObservationAllowed ? planet.theme : 0,
                ThemeName = visibility.ObservationAllowed ? theme?.displayName ?? string.Empty : string.Empty,
                IsCurrentPlanet = planet.id == localPlanet.id,
                IsBirthPlanet = planet.id == galaxy.birthPlanetId,
                IsGasGiant = planet.type == EPlanetType.Gas,
                FactoryLoaded = planet.factoryLoaded,
                RealRadius = planet.realRadius,
                OrbitRadius = planet.orbitRadius,
                DistanceFromPlayer = (planet.uPosition - player.uPosition).magnitude,
                UniversalPosition = new UniversalPositionSnapshot
                {
                    X = universalPosition.x,
                    Y = universalPosition.y,
                    Z = universalPosition.z,
                },
                PotentialResourceTypes = resources,
                PotentialResourceTypesVisible = visibility.ObservationAllowed,
                RequiredObserveLevel = visibility.RequiredObserveLevel,
            });
        }

        var result = new LocalStarSystemSnapshot
        {
            SessionId = _sessions.SessionId!,
            LocalPlanetId = localPlanet.id,
            StarId = localStar.id,
            StarName = localStar.displayName,
            CapturedAtGameTick = GameMain.gameTick,
            Planets = planets,
        };
        result.StateHash = CanonicalStateHash.Combine(
            "local-star-system",
            result.SessionId,
            result.StarId,
            string.Join("|", planets.Select(planet =>
                $"{planet.PlanetId}:{planet.Name}:{planet.PlanetType}:{planet.ThemeId}:{planet.IsGasGiant}:{planet.PotentialResourceTypesVisible}:{planet.RequiredObserveLevel}:{string.Join(",", planet.PotentialResourceTypes)}")));
        return GameCallResult<LocalStarSystemSnapshot>.Succeeded(result);
    }

    private static void CaptureTank(
        PlanetFactory factory,
        ref EntityData entity,
        FactoryEntitySnapshot snapshot)
    {
        var tankId = entity.tankId;
        if (tankId <= 0
            || tankId >= factory.factoryStorage.tankCursor
            || tankId >= factory.factoryStorage.tankPool.Length)
        {
            return;
        }

        ref var tank = ref factory.factoryStorage.tankPool[tankId];
        var observedCount = TankFluidObservationPolicy.ObserveCount(
            entity.id, tankId, tank.id, tank.entityId, tank.fluidId, tank.fluidCount);
        if (!observedCount.HasValue)
        {
            return;
        }

        snapshot.TankFluidCount = observedCount.Value;
        if (observedCount.Value == 0) return;

        snapshot.Buffers.Add(new FactoryBufferSnapshot
        {
            Role = "tank-fluid",
            ItemId = tank.fluidId,
            Name = GetItemName(tank.fluidId),
            Count = tank.fluidCount,
            Inc = tank.fluidInc,
        });
    }

    private static void CaptureInserter(
        PlanetFactory factory,
        ref EntityData entity,
        FactoryEntitySnapshot snapshot)
    {
        var inserterId = entity.inserterId;
        if (inserterId <= 0
            || inserterId >= factory.factorySystem.inserterCursor
            || inserterId >= factory.factorySystem.inserterPool.Length)
        {
            return;
        }

        ref var inserter = ref factory.factorySystem.inserterPool[inserterId];
        if (inserter.id != inserterId || inserter.entityId != entity.id)
        {
            return;
        }

        snapshot.PickTargetObjectId = inserter.pickTarget == 0 ? null : inserter.pickTarget;
        snapshot.InsertTargetObjectId = inserter.insertTarget == 0 ? null : inserter.insertTarget;
        snapshot.FilterItemId = inserter.filter == 0 ? null : inserter.filter;
        snapshot.FilterItemName = inserter.filter > 0 ? GetItemName(inserter.filter) : null;
        snapshot.InserterStage = inserter.stage.ToString();
        snapshot.InserterStackCount = inserter.stackCount;
        snapshot.IsWorking = inserter.pickTarget != 0
            && inserter.insertTarget != 0
            && (inserter.itemCount > 0 || inserter.time > 0);
        snapshot.Progress = inserter.time;
        snapshot.ProgressRequired = inserter.stt;
        if (inserter.itemId > 0 && inserter.itemCount > 0)
        {
            snapshot.Buffers.Add(new FactoryBufferSnapshot
            {
                Role = "inserter-held",
                ItemId = inserter.itemId,
                Name = GetItemName(inserter.itemId),
                Count = inserter.itemCount,
                Inc = inserter.itemInc,
            });
        }
    }

    private static void AddFactoryBuffers(
        ICollection<FactoryBufferSnapshot> target,
        string role,
        int[]? itemIds,
        int[]? counts,
        int[]? incs,
        int unitsPerItem = 1)
    {
        var length = Math.Min(itemIds?.Length ?? 0, counts?.Length ?? 0);
        for (var index = 0; index < length; index++)
        {
            target.Add(new FactoryBufferSnapshot
            {
                UnitsPerItem = unitsPerItem,
                CountUnit = unitsPerItem == 3600 ? "research_matrix_points" : "items",
                Role = role,
                ItemId = itemIds![index],
                Name = GetItemName(itemIds[index]),
                Count = counts![index],
                Inc = index < (incs?.Length ?? 0) ? incs![index] : 0,
            });
        }
    }

    private static string GetComponentKind(ref EntityData entity)
    {
        if (entity.minerId > 0) return "miner";
        if (entity.assemblerId > 0) return "assembler";
        if (entity.labId > 0) return "lab";
        if (entity.inserterId > 0) return "inserter";
        if (entity.beltId > 0) return "belt";
        if (entity.storageId > 0) return "storage";
        if (entity.tankId > 0) return "tank";
        if (entity.powerGenId > 0) return "power-generator";
        if (entity.powerNodeId > 0) return "power-node";
        if (entity.stationId > 0) return "station";
        if (entity.splitterId > 0) return "splitter";
        if (entity.fractionatorId > 0) return "fractionator";
        if (entity.spraycoaterId > 0) return "spray-coater";
        if (entity.pilerId > 0) return "piler";
        return "other";
    }

    private static string GetPrefabComponentKind(PrefabDesc? descriptor)
    {
        if (descriptor is null) return "other";
        if (descriptor.veinMiner || descriptor.oilMiner || descriptor.minerType != EMinerType.None) return "miner";
        if (descriptor.isLab) return "lab";
        if (descriptor.isAssembler) return "assembler";
        if (descriptor.isInserter) return "inserter";
        if (descriptor.isBelt) return "belt";
        if (descriptor.isStorage) return "storage";
        if (descriptor.isPowerGen) return "power-generator";
        if (descriptor.isPowerNode) return "power-node";
        if (descriptor.isStation) return "station";
        if (descriptor.isSplitter) return "splitter";
        if (descriptor.isFractionator) return "fractionator";
        return "other";
    }

    private BridgeError? ValidateOwnedPlanetOnMainThread(
        string? requestedSessionId,
        int requestedPlanetId,
        out PlanetFactory? factory)
    {
        var error = ValidateOwnedSessionOnMainThread(requestedSessionId, out factory);
        if (error is not null)
        {
            return error;
        }

        if (requestedPlanetId <= 0 || factory!.planetId != requestedPlanetId)
        {
            factory = null;
            return BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "The requested planet does not match the currently loaded local planet.",
                true,
                "Call get_session_state and retry with its current localPlanetId.");
        }

        return null;
    }

    private BridgeError? ValidateOwnedCurrentStarFactoryReadOnMainThread(
        string? requestedSessionId,
        int requestedPlanetId,
        out PlanetFactory? factory)
    {
        factory = null;
        var localPlanet = GameMain.localPlanet;
        var localStar = GameMain.localStar;
        if (localPlanet is null || localStar?.planets is null)
        {
            return NotReady("The current owned local star system is not ready for a same-star factory read.");
        }

        // Reuse the existing exact-owned-session and local-factory guard before
        // using the current star as the only remote-read scope.
        var localError = ValidateOwnedPlanetOnMainThread(requestedSessionId, localPlanet.id, out _);
        if (localError is not null) return localError;
        var gameData = GameMain.data;
        if (gameData is null) return NotReady("The exact owned game data is not ready.");

        var localPlanetMembership = localStar.planets.Count(planet =>
            planet is not null && planet.id == localPlanet.id && ReferenceEquals(planet, localPlanet));
        if (localPlanetMembership != 1)
        {
            return NotReady("The active local planet does not match the current star-system planet pool.");
        }

        var matches = localStar.planets.Where(planet => planet is not null && planet.id == requestedPlanetId).ToList();
        if (matches.Count > 1)
        {
            return NotReady("The current star has duplicate planet identities; the requested factory cannot be selected safely.");
        }

        var targetPlanet = matches.SingleOrDefault();
        var targetIsInCurrentStar = targetPlanet is not null;
        if (targetPlanet is null)
        {
            var outside = OwnedFactoryScopePolicy.EvaluateCurrentStarRead(
                requestedPlanetId, requestedPlanetId, false, false,
                -1, gameData.factoryCount, gameData.factories?.Length ?? 0,
                -1, -1, 0, false);
            return CreateOwnedFactoryReadScopeError(outside);
        }

        var resolvedTargetPlanet = targetPlanet!;

        var factoryPoolError = TryGetOwnedFactories(gameData, out var ownedFactories);
        if (factoryPoolError is not null) return factoryPoolError;

        var candidate = resolvedTargetPlanet.factoryIndex >= 0
            && resolvedTargetPlanet.factoryIndex < ownedFactories.Count
                ? ownedFactories[resolvedTargetPlanet.factoryIndex]
                : null;
        var status = OwnedFactoryScopePolicy.EvaluateCurrentStarRead(
            requestedPlanetId,
            resolvedTargetPlanet.id,
            targetIsInCurrentStar,
            resolvedTargetPlanet.factoryLoaded,
            resolvedTargetPlanet.factoryIndex,
            gameData.factoryCount,
            gameData.factories?.Length ?? 0,
            candidate?.index ?? -1,
            resolvedTargetPlanet.factoryIndex,
            candidate?.planetId ?? 0,
            candidate is not null && ReferenceEquals(candidate.planet, resolvedTargetPlanet),
            factorySimulationResident: candidate is not null
                && ReferenceEquals(resolvedTargetPlanet.factory, candidate));
        var scopeError = CreateOwnedFactoryReadScopeError(status);
        if (scopeError is not null) return scopeError;

        factory = candidate;
        return null;
    }

    private static BridgeError? CreateOwnedFactoryReadScopeError(OwnedFactoryReadStatus status) => status switch
    {
        OwnedFactoryReadStatus.Allowed => null,
        OwnedFactoryReadStatus.InvalidPlanetIdentity => InvalidRequest(
            "A same-star factory read requires a positive, identity-matched planetId.",
            "Use a planetId from the current owned star-system snapshot."),
        OwnedFactoryReadStatus.OutsideCurrentStar => BridgeError.Create(
            BridgeErrorCodes.StaleState,
            "The requested planet is not in the current local star system.",
            false,
            "Only inspect an already-created factory in the current owned star."),
        OwnedFactoryReadStatus.FactoryNotLoaded => NotReady(
            "The requested planet's factory is not loaded; no inventory was observed."),
        OwnedFactoryReadStatus.FactoryIndexUnavailable => NotReady(
            "The requested planet's factory index is not available in the owned factory pool."),
        OwnedFactoryReadStatus.FactoryIdentityMismatch => NotReady(
            "The requested planet and factory pool identities do not match; no inventory was observed."),
        _ => NotReady("The requested factory read scope could not be validated."),
    };

    private BridgeError? ValidateOwnedSessionOnMainThread(string? requestedSessionId, out PlanetFactory? factory)
    {
        factory = null;
        var error = ValidateOwnedGameDataOnMainThread(requestedSessionId, out var gameData);
        if (error is not null)
        {
            return error;
        }

        factory = gameData!.localLoadedPlanetFactory;
        if (factory is null)
        {
            return BridgeError.Create(
                BridgeErrorCodes.NoLocalPlanet,
                "The owned session does not currently have a loaded local factory.",
                true,
                "Wait for the local planet factory to load and retry.");
        }

        return null;
    }

    private BridgeError? ValidateOwnedGameDataOnMainThread(string? requestedSessionId, out GameData? gameData)
    {
        gameData = null;
        var state = _sessions.CaptureOnMainThread();
        if (!state.GameLoaded)
        {
            return BridgeError.Create(
                BridgeErrorCodes.GameNotLoaded,
                "No game session is currently loaded.",
                true,
                "Create and load a Spherewright-owned ordinary world, then retry.");
        }

        if (!state.OwnedBySpherewright)
        {
            return BridgeError.Create(
                BridgeErrorCodes.SessionNotOwned,
                "The loaded game session was not created by this Spherewright Plugin process, so its contents are restricted.",
                false,
                "Return to the main menu and create a Spherewright-owned ordinary world.");
        }

        if (string.IsNullOrWhiteSpace(requestedSessionId)
            || !string.Equals(requestedSessionId, state.SessionId, StringComparison.Ordinal))
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "The supplied session ID does not match the active owned session.",
                true,
                "Call get_session_state and retry with its sessionId.");
        }

        gameData = GameMain.data;
        if (gameData is null || !_sessions.IsCurrentSessionOwned)
        {
            return BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The exact owned game data is not ready.",
                true,
                "Wait for the owned ordinary world to finish loading and retry.");
        }

        return null;
    }

    private AssemblerSnapshot? TryCaptureAssembler(
        PlanetFactory factory,
        int componentId,
        ref AssemblerComponent assembler)
    {
        var entityId = assembler.entityId;
        if (entityId <= 0 || entityId >= factory.entityCursor)
        {
            return null;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.assemblerId != componentId)
        {
            return null;
        }

        var item = LDB.items.Select(entity.protoId);
        var recipe = assembler.recipeId > 0 ? LDB.recipes.Select(assembler.recipeId) : null;
        return new AssemblerSnapshot
        {
            PlanetId = factory.planetId,
            EntityId = entityId,
            ComponentId = componentId,
            BuildingItemId = entity.protoId,
            BuildingName = item?.name ?? string.Empty,
            RecipeId = assembler.recipeId,
            RecipeName = recipe?.name,
            TimeSpent = assembler.time,
            TimeRequired = assembler.recipeExecuteData?.timeSpend ?? 0,
            IsWorking = assembler.replicating,
            Position = new Vector3Snapshot
            {
                X = entity.pos.x,
                Y = entity.pos.y,
                Z = entity.pos.z,
            },
            Revision = _sessions.Revision,
        };
    }

    private static void AddPlayerItemAmounts(
        ICollection<PlayerItemAmount> target,
        int[]? itemIds,
        int[]? counts,
        int[]? bufferedCounts)
    {
        var length = Math.Min(itemIds?.Length ?? 0, counts?.Length ?? 0);
        for (var index = 0; index < length; index++)
        {
            target.Add(new PlayerItemAmount
            {
                ItemId = itemIds![index],
                Name = GetItemName(itemIds[index]),
                Count = counts![index],
                BufferedCount = index < (bufferedCounts?.Length ?? 0) ? bufferedCounts![index] : 0,
            });
        }
    }

    private static void AddCatalogAmounts(
        ICollection<CatalogItemAmount> target,
        int[]? itemIds,
        int[]? counts)
    {
        var length = Math.Min(itemIds?.Length ?? 0, counts?.Length ?? 0);
        for (var index = 0; index < length; index++)
        {
            target.Add(new CatalogItemAmount
            {
                ItemId = itemIds![index],
                Name = GetItemName(itemIds[index]),
                Count = counts![index],
            });
        }
    }

    private static bool ResourceMatches(
        ResourceNodeSnapshot snapshot,
        string? resourceType,
        int? productItemId)
    {
        return (resourceType is null
                || string.Equals(snapshot.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase))
            && (!productItemId.HasValue
                || snapshot.Yields.Any(item => item.ItemId == productItemId.Value));
    }

    private static bool FactoryEntityMatches(
        FactoryEntitySnapshot snapshot,
        string? componentKind,
        int? itemId)
    {
        return (componentKind is null
                || string.Equals(snapshot.ComponentKind, componentKind, StringComparison.OrdinalIgnoreCase))
            && (!itemId.HasValue || snapshot.ItemId == itemId.Value);
    }

    private static List<string> CapturePotentialResourceTypes(ThemeProto? theme)
    {
        if (theme is null)
        {
            return new List<string>();
        }

        var veinTypes = new SortedSet<int>();
        var regular = theme.VeinSpot ?? Array.Empty<int>();
        for (var index = 0; index < regular.Length && index + 1 < (int)EVeinType.Max; index++)
        {
            if (regular[index] > 0)
            {
                veinTypes.Add(index + 1);
            }
        }

        foreach (var veinType in theme.RareVeins ?? Array.Empty<int>())
        {
            if (veinType > 0 && veinType < (int)EVeinType.Max)
            {
                veinTypes.Add(veinType);
            }
        }

        return veinTypes.Select(value => ((EVeinType)value).ToString()).ToList();
    }

    private static Vector3Snapshot CaptureVector(UnityEngine.Vector3 value)
    {
        return new Vector3Snapshot
        {
            X = value.x,
            Y = value.y,
            Z = value.z,
        };
    }

    private static QuaternionSnapshot CaptureQuaternion(UnityEngine.Quaternion value)
    {
        return new QuaternionSnapshot
        {
            X = value.x,
            Y = value.y,
            Z = value.z,
            W = value.w,
        };
    }

    private static string GetItemName(int itemId)
    {
        return itemId > 0 ? LDB.items.Select(itemId)?.name ?? string.Empty : string.Empty;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value!.Trim().ToLowerInvariant();
    }

    private static string ComputeFilterHash(string canonicalFilter)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalFilter));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return "sha256:" + builder;
        }
    }

    private static BridgeError? ValidateListLimit(int limit, string label)
    {
        return limit >= 1 && limit <= MaximumLimit
            ? null
            : BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                $"{label} list limit must be between 1 and {MaximumLimit}.",
                false,
                "Use a bounded limit and retry.");
    }

    private static BridgeError InvalidRequest(string message, string recovery)
    {
        return BridgeError.Create(BridgeErrorCodes.InvalidRequest, message, false, recovery);
    }

    private static BridgeError NotReady(string message)
    {
        return BridgeError.Create(
            BridgeErrorCodes.BridgeNotReady,
            message,
            true,
            "Wait until the owned ordinary world finishes loading, then retry.");
    }

    private static BridgeError StaleCursor(string message)
    {
        return BridgeError.Create(
            BridgeErrorCodes.StaleCursor,
            message,
            true,
            "Discard the cursor and start a new listing with the current session, planet, and filters.");
    }

    private static BridgeError SnapshotCapacityExceeded(string label)
    {
        return BridgeError.Create(
            BridgeErrorCodes.ServerBusy,
            $"The bounded {label} snapshot capacity is temporarily full.",
            true,
            "Wait for an existing snapshot to expire, then start a new listing.");
    }

    private static BridgeError OverseerScopeExceeded()
    {
        return BridgeError.Create(
            BridgeErrorCodes.ServerBusy,
            "The owned world's current factory, production-component, power, logistics, or technology scope exceeds the bounded Overseer snapshot budget.",
            false,
            "Use the existing narrower observation tools while a future incremental Overseer snapshot implementation handles this scale.");
    }

    private static GameCallResult<ResourceNodeSnapshot> InvalidResource(string message)
    {
        return GameCallResult<ResourceNodeSnapshot>.Failed(BridgeError.Create(
            BridgeErrorCodes.InvalidEntity,
            message,
            false,
            "Refresh the resource-node list and use a current kind and node ID."));
    }

    private static GameCallResult<FactoryEntitySnapshot> InvalidFactoryEntity(string message)
    {
        return GameCallResult<FactoryEntitySnapshot>.Failed(BridgeError.Create(
            BridgeErrorCodes.InvalidEntity,
            message,
            false,
            "Refresh the factory-entity list and use a current object ID."));
    }

    private static string? GetBasicLineRole(PrefabDesc descriptor)
    {
        if (descriptor.minerType == EMinerType.Water)
        {
            return "water-pump";
        }

        if (descriptor.oilMiner || descriptor.minerType == EMinerType.Oil)
        {
            return "oil-extractor";
        }

        if (descriptor.veinMiner || descriptor.minerType == EMinerType.Vein)
        {
            return "vein-miner";
        }

        if (descriptor.isBelt)
        {
            return "belt";
        }

        if (descriptor.isStorage && !descriptor.isTank && !descriptor.isStation && !descriptor.isBattleBase)
        {
            return "storage";
        }

        if (descriptor.isTank)
        {
            return "tank";
        }

        if (descriptor.isAssembler && descriptor.assemblerRecipeType == ERecipeType.Smelt)
        {
            return "smelter";
        }

        if (descriptor.isAssembler && descriptor.assemblerRecipeType == ERecipeType.Refine)
        {
            return "refinery";
        }

        if (descriptor.isAssembler)
        {
            return "assembler";
        }

        if (descriptor.isLab)
        {
            return "matrix-lab";
        }

        if (descriptor.isInserter)
        {
            return "inserter";
        }

        if (descriptor.isPowerGen && descriptor.windForcedPower && descriptor.isPowerNode)
        {
            return "wind-power";
        }

        if (descriptor.isPowerGen)
        {
            return "power-generator";
        }

        if (descriptor.isPowerNode)
        {
            return "power-node";
        }

        return null;
    }

    private static BuildCatalogIngredient CreateIngredient(int itemId, int count)
    {
        var item = LDB.items.Select(itemId);
        return new BuildCatalogIngredient
        {
            ItemId = itemId,
            Name = item?.name ?? string.Empty,
            Count = count,
            RawMaterial = item?.isRaw ?? false,
        };
    }

    private static BasicLineRecommendation? CreateRecommendation(BuildCatalog catalog)
    {
        var storage = catalog.Buildings.FirstOrDefault(item => item.Available && item.Role == "storage");
        var smelter = catalog.Buildings.FirstOrDefault(item => item.Available && item.Role == "smelter");
        var inserter = catalog.Buildings.FirstOrDefault(item => item.Available && item.Role == "inserter");
        var power = catalog.Buildings.FirstOrDefault(item => item.Available && item.Role == "wind-power");
        var recipe = catalog.Recipes.FirstOrDefault(item => item.Unlocked && item.Inputs.Count > 0 && item.Inputs[0].RawMaterial)
            ?? catalog.Recipes.FirstOrDefault(item => item.Unlocked && item.Inputs.Count > 0 && item.Outputs.Count > 0);
        if (storage is null || smelter is null || inserter is null || power is null || recipe is null)
        {
            return null;
        }

        return new BasicLineRecommendation
        {
            StorageItemId = storage.ItemId,
            AssemblerItemId = smelter.ItemId,
            InserterItemId = inserter.ItemId,
            PowerGeneratorItemId = power.ItemId,
            RecipeId = recipe.RecipeId,
            InputItemId = recipe.Inputs[0].ItemId,
            OutputItemId = recipe.Outputs[0].ItemId,
        };
    }

    private static GameCallResult<AssemblerSnapshot> InvalidAssembler(string message)
    {
        return GameCallResult<AssemblerSnapshot>.Failed(BridgeError.Create(
            BridgeErrorCodes.InvalidEntity,
            message,
            false,
            "Refresh the assembler list and use a current assembler entity ID."));
    }

    private sealed class OverseerSummaryPageEntry
    {
        public long CapturedAtGameTick { get; set; }

        public OverseerResearchSummarySnapshot Research { get; set; } = new OverseerResearchSummarySnapshot();

        public OverseerPlanetSummarySnapshot Planet { get; set; } = new OverseerPlanetSummarySnapshot();
    }

    private sealed class OverseerDiagnosticBundlePageEntry
    {
        public long CapturedAtGameTick { get; set; }

        public OverseerResearchSummarySnapshot Research { get; set; } = new OverseerResearchSummarySnapshot();

        public OverseerDiagnosticBundlePlanetSnapshot Planet { get; set; } =
            new OverseerDiagnosticBundlePlanetSnapshot();
    }

    private sealed class OverseerTheoreticalSettings
    {
        public float ProductMultiplier { get; set; }

        public float AccelerationMultiplier { get; set; }

        public float MiningSpeedScale { get; set; }

        public int FractionatorStackMultiplier { get; set; }
    }

    private enum TheoreticalProducerKind
    {
        Assembler,
        Lab,
        Miner,
        Fractionator,
    }
}
