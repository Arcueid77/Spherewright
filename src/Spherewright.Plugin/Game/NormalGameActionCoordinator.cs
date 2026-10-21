using Spherewright.Bridge.Core.Safety;
using Spherewright.Bridge.Core.Logistics;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Progression;
using Spherewright.Contracts.Resources;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private const int StateHashVersion = 1;
    private const long PowerStarvationGraceTicks = 600;
    private readonly GameSessionTracker _sessions;
    private readonly GameStateReader _reader;
    private readonly FlightCheckpointStore _flightCheckpoints;
    private readonly BlueprintBuildStore _blueprints;
    private readonly PreparedPlanStore<NormalActionPlanPayload> _plans;
    private readonly IdempotencyCache<NormalActionCommitResult> _idempotency;
    private readonly BoundedActionHistory<ActionRecord> _actions;

    public NormalGameActionCoordinator(
        int planLifetimeSeconds,
        int idempotencyRetentionMinutes,
        int idempotencyCapacity,
        GameSessionTracker sessions,
        GameStateReader reader,
        FlightCheckpointStore flightCheckpoints,
        BlueprintBuildStore blueprints)
    {
        _sessions = sessions;
        _reader = reader;
        _flightCheckpoints = flightCheckpoints;
        _blueprints = blueprints;
        _actions = new BoundedActionHistory<ActionRecord>(idempotencyCapacity,
            TimeSpan.FromMinutes(idempotencyRetentionMinutes), action => action.Terminal,
            action => action.State == NormalActionStates.OutcomeUnknown || action.RecoveryRequired
                || !string.IsNullOrEmpty(action.OriginalOutcomeMessage)
                || !string.IsNullOrEmpty(action.FlightCheckpointId)
                || (!action.Succeeded && (action.ActionKind == NormalActionKinds.Build
                    || action.ActionKind == NormalActionKinds.BlueprintBuild)));
        _plans = new PreparedPlanStore<NormalActionPlanPayload>(
            TimeSpan.FromSeconds(planLifetimeSeconds),
            128);
        _idempotency = new IdempotencyCache<NormalActionCommitResult>(
            idempotencyCapacity,
            TimeSpan.FromMinutes(idempotencyRetentionMinutes));
    }

    public GameCallResult<PreparedNormalAction> PrepareMoveOnMainThread(
        string? requestedSessionId,
        PrepareMoveRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (!IsFinite(request.Target.X) || !IsFinite(request.Target.Y) || !IsFinite(request.Target.Z)
            || request.ArrivalTolerance < 0.5f || request.ArrivalTolerance > 5f)
        {
            return InvalidPlan("Move target must be finite and arrival tolerance must be from 0.5 through 5 metres.");
        }

        var playerResult = _reader.GetPlayerStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(playerResult.Error!);
        }

        var player = playerResult.Value;
        var driftInspection = request.AllowPassiveDrift ? _reader.LastPassiveDriftMoveInspection : null;
        if (request.AllowPassiveDrift)
        {
            if (driftInspection is null || GameMain.mainPlayer.currentOrder is not null
                || _actions.ActiveValues.Any(action => !action.Terminal && IsPlayerOrderAction(action.ActionKind))
                || !driftInspection.Allows(request.ExpectedPlayerStateHash, player,
                    GameStateReader.PassiveDriftClockSeconds()))
                return StalePlan("Passive-drift Move requires the same recent server inspection, unchanged action state, low speed and at most 0.05 metres displacement within two seconds.");
        }
        else if (!string.Equals(request.ExpectedPlayerStateHash, player.StateHash, StringComparison.Ordinal))
        {
            return StalePlan("Player state changed after inspection; inspect the player and prepare again.");
        }

        var livePlayer = GameMain.mainPlayer;
        var target = ToVector(request.Target);
        var expectedRadius = livePlayer.position.magnitude;
        if (expectedRadius < 1f || Math.Abs(target.magnitude - expectedRadius) > 8f)
        {
            return InvalidPlan("Move target is not on the current planet surface.");
        }

        target = target.normalized * expectedRadius;
        var distance = Vector3.Distance(livePlayer.position, target);
        var playerActionHash = CanonicalStateHash.PlayerAction(player);
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.Move,
            _sessions.SessionId,
            request.PlanetId,
            playerActionHash,
            target.x,
            target.y,
            target.z,
            request.ArrivalTolerance);
        if (driftInspection is not null)
        {
            var binding = driftInspection.Describe();
            expectedHash = CanonicalStateHash.Combine(NormalActionKinds.Move, expectedHash,
                "bounded-passive-drift-v1", binding.InspectionStateHash,
                binding.OriginPosition.X, binding.OriginPosition.Y, binding.OriginPosition.Z,
                binding.InspectionCapturedAtGameTick, binding.ExpiresAtUtc);
        }
        var payload = NormalActionPlanPayload.Move(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            playerActionHash,
            target,
            request.ArrivalTolerance,
            distance,
            driftInspection);
        var surfacePreview = CaptureMoveSurfacePreview(player.Position, target);
        var prepared = AddPreparedPlan(payload, common.Session!,
            Math.Max(1L, (long)Math.Ceiling(distance / 6f * 60f)),
            "Player remains on the same planet and reaches the target within the requested tolerance.");
        if (prepared.Value is not null)
        {
            prepared.Value.SurfacePreview = surfacePreview;
            if (driftInspection is not null)
            {
                prepared.Value.MoveStateBinding = driftInspection.Describe();
                if (driftInspection.ExpiresAtUtc < prepared.Value.ExpiresAtUtc)
                    prepared.Value.ExpiresAtUtc = driftInspection.ExpiresAtUtc;
            }
        }
        return prepared;
    }

    public GameCallResult<PreparedNormalAction> PrepareHarvestOnMainThread(
        string? requestedSessionId,
        PrepareHarvestRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.RequestedYieldCount <= 0 || request.RequestedYieldCount > 1000)
        {
            return InvalidPlan("Requested harvest yield must be from 1 through 1000 items.");
        }

        var kind = request.ResourceKind?.Trim().ToLowerInvariant();
        if (kind != ResourceNodeKinds.Vein && kind != ResourceNodeKinds.Vegetation)
        {
            return InvalidPlan("Harvest resource kind must be vein or vegetation.");
        }

        var resourceResult = _reader.InspectResourceNodeOnMainThread(
            requestedSessionId,
            new InspectResourceNodeRequest { PlanetId = request.PlanetId, Kind = kind, NodeId = request.NodeId });
        if (!resourceResult.Success || resourceResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(resourceResult.Error!);
        }

        var playerResult = _reader.GetPlayerStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(playerResult.Error!);
        }

        var resource = resourceResult.Value;
        var player = playerResult.Value;
        if (!string.Equals(request.ExpectedResourceStateHash, resource.StateHash, StringComparison.Ordinal)
            || !string.Equals(request.ExpectedPlayerStateHash, player.StateHash, StringComparison.Ordinal))
        {
            return StalePlan("Player or resource state changed after inspection; inspect both and prepare again.");
        }

        if (kind == ResourceNodeKinds.Vein
            && string.Equals(resource.ResourceType, EVeinType.Oil.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return InvalidPlan("Crude-oil veins cannot be harvested by the player's manual mining action.");
        }

        if (!resource.WithinPlayerBuildArea)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.TargetOutOfRange,
                "The selected resource is outside the player's current normal interaction area.",
                true,
                "Move through bounded surface waypoints, then inspect a resource with withinPlayerBuildArea=true and prepare again."));
        }

        if (resource.Yields.Count == 0)
        {
            return InvalidPlan("The selected runtime resource has no normal manual-harvest yield.");
        }

        var playerActionHash = CanonicalStateHash.PlayerAction(player);
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.Harvest,
            _sessions.SessionId,
            request.PlanetId,
            resource.StateHash,
            playerActionHash,
            request.RequestedYieldCount);
        var payload = NormalActionPlanPayload.Harvest(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            playerActionHash,
            resource,
            request.RequestedYieldCount);
        var result = AddPreparedPlan(
            payload,
            common.Session!,
            EstimateHarvestTicks(resource, request.RequestedYieldCount),
            "The normal player mining order reduces the bound node and the corresponding inventory yield is reread.");
        if (result.Success && result.Value is not null)
        {
            result.Value.EstimatedDistance = resource.DistanceFromPlayer;
            foreach (var yield in resource.Yields)
            {
                result.Value.ItemBudget.Add(new ActionItemBudget
                {
                    ItemId = yield.ItemId,
                    Name = yield.Name,
                    Count = kind == ResourceNodeKinds.Vegetation ? yield.Count : request.RequestedYieldCount,
                    Direction = "output",
                });
            }
        }

        return result;
    }

    public GameCallResult<PreparedNormalAction> PrepareHandcraftOnMainThread(
        string? requestedSessionId,
        PrepareHandcraftRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.Count <= 0 || request.Count > 1000)
        {
            return InvalidPlan("Handcraft count must be from 1 through 1000 recipe executions.");
        }

        var recipe = LDB.recipes.Select(request.RecipeId);
        var history = GameMain.history;
        var livePlayer = GameMain.mainPlayer;
        if (recipe is null || !recipe.Handcraft)
        {
            return InvalidPlan("The requested runtime recipe does not support normal handcrafting.");
        }

        if (history is null || !history.RecipeUnlocked(request.RecipeId))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRecipe,
                "The requested handcraft recipe is not unlocked.",
                false,
                "Select an unlocked handcraft recipe or complete its prerequisite technology."));
        }

        if (livePlayer?.mecha?.forge is null || livePlayer.package is null)
        {
            return NotReadyPlan("The player's normal replicator is not ready.");
        }

        var playerResult = _reader.GetPlayerStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(playerResult.Error!);
        }

        var player = playerResult.Value;
        if (!string.Equals(request.ExpectedPlayerStateHash, player.StateHash, StringComparison.Ordinal))
        {
            return StalePlan("Player inventory or forge state changed after inspection; inspect and prepare again.");
        }

        if (!livePlayer.mecha.forge.TryAddTask(request.RecipeId, request.Count))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                "The normal replicator cannot reserve the requested recipe from the current inventory and unlocked dependency recipes.",
                true,
                "Harvest or produce the missing ingredients, ensure dependency recipes are unlocked, then inspect and prepare again."));
        }

        var playerActionHash = CanonicalStateHash.PlayerAction(player);
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.Handcraft,
            _sessions.SessionId,
            request.PlanetId,
            playerActionHash,
            request.RecipeId,
            request.Count);
        var payload = NormalActionPlanPayload.Handcraft(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            playerActionHash,
            request.RecipeId,
            request.Count);
        var prepared = AddPreparedPlan(
            payload,
            common.Session!,
            (long)recipe.TimeSpend * request.Count,
            "The accepted normal replicator task leaves the forge queue and its runtime recipe products are reread in inventory.");
        if (prepared.Success && prepared.Value is not null)
        {
            AddRecipeBudget(prepared.Value, recipe, request.Count);
        }

        return prepared;
    }

    public GameCallResult<PreparedNormalAction> PrepareSelectResearchOnMainThread(
        string? requestedSessionId,
        PrepareSelectResearchRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        var progressionResult = _reader.GetProgressionStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!progressionResult.Success || progressionResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(progressionResult.Error!);
        }

        var progression = progressionResult.Value;
        if (!string.Equals(request.ExpectedSelectionStateHash, progression.SelectionStateHash, StringComparison.Ordinal))
        {
            return StalePlan("Technology queue or state changed after inspection; inspect progression and prepare again.");
        }

        var tech = LDB.techs.Select(request.TechId);
        var history = GameMain.history;
        var techState = progression.Technologies.FirstOrDefault(candidate => candidate.TechId == request.TechId);
        if (tech is null || techState is null)
        {
            return InvalidPlan("The requested technology does not exist in the current runtime catalog.");
        }

        if (techState.Unlocked)
        {
            return InvalidPlan("The requested technology is already unlocked.");
        }

        int[]? researchOrder = null;
        if (request.PrioritizeQueued)
        {
            researchOrder = TryPrepareResearchPriority(request.TechId);
            if (researchOrder is null)
                return InvalidPlan("Priority requires an active queue, a selected non-head queued technology, and every native prerequisite already unlocked. No queue or progress was changed.");
        }
        else if (history is null || !history.CanEnqueueTech(request.TechId))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                "DSP cannot enqueue the requested technology with the current prerequisites and queue.",
                true,
                "Complete prerequisite technology or free a research queue slot, then inspect and prepare again."));
        }

        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.SelectResearch,
            _sessions.SessionId,
            request.PlanetId,
            progression.SelectionStateHash,
            request.TechId,
            request.PrioritizeQueued);
        var payload = NormalActionPlanPayload.Research(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            progression.SelectionStateHash,
            request.TechId,
            researchOrder);
        var prepared = AddPreparedPlan(
            payload,
            common.Session!,
            techState.HashRequired - techState.HashUploaded,
            request.PrioritizeQueued
                ? "Native queue priority only: requested technology becomes head; all other order, research progress and inventory remain unchanged. This does not complete research."
                : "DSP's normal technology queue contains the requested technology and currentTech reflects the queue head.");
        if (prepared.Success && prepared.Value is not null)
        {
            foreach (var requirement in techState.ItemRequirements)
            {
                prepared.Value.ItemBudget.Add(new ActionItemBudget
                {
                    ItemId = requirement.ItemId,
                    Name = requirement.Name,
                    Count = checked((int)Math.Min(int.MaxValue, requirement.RequiredItemCount)),
                    Direction = "research-consumption",
                });
            }
        }

        return prepared;
    }

    public GameCallResult<PreparedNormalAction> PrepareBuildOnMainThread(
        string? requestedSessionId,
        PrepareBuildRequest request)
        => PrepareStructuredBuildOnMainThread(requestedSessionId, request);

    public GameCallResult<PreparedNormalAction> PrepareDismantleOnMainThread(
        string? requestedSessionId,
        PrepareDismantleRequest request)
        => PrepareDismantlePlanOnMainThread(requestedSessionId, request);

    public GameCallResult<PreparedNormalAction> PrepareConfigureBuildingOnMainThread(
        string? requestedSessionId,
        PrepareConfigureBuildingRequest request)
        => PrepareStructuredConfigurationOnMainThread(requestedSessionId, request);

    public GameCallResult<PreparedNormalAction> PrepareTransferOnMainThread(
        string? requestedSessionId,
        PrepareTransferRequest request)
        => PrepareStorageTransferOnMainThread(requestedSessionId, request);

    public GameCallResult<PreparedNormalAction> PrepareLogisticsStationFleetTransferOnMainThread(
        string? requestedSessionId,
        PrepareLogisticsStationFleetTransferRequest request)
        => PrepareStationFleetTransferOnMainThread(requestedSessionId, request);

    public GameCallResult<PreparedNormalAction> PrepareRefuelOnMainThread(
        string? requestedSessionId,
        PrepareRefuelRequest request)
        => PrepareRefuelPlanOnMainThread(requestedSessionId, request);

    public GameCallResult<PreparedNormalAction> PrepareSaveOnMainThread(
        string? requestedSessionId,
        PrepareSaveRequest request)
        => PrepareSavePlanOnMainThread(requestedSessionId, request);

    public GameCallResult<NormalActionCommitResult> CommitOnMainThread(
        string expectedActionKind,
        string? requestedSessionId,
        CommitNormalActionRequest request,
        bool? expectedVoyagerMode = null)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "A UUID idempotency key is required.",
                false,
                "Generate one UUID and reuse it for retries of this exact commit."));
        }

        if (!string.Equals(requestedSessionId, request.SessionId, StringComparison.Ordinal))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "Envelope and commit payload session IDs do not match.",
                false,
                "Use the exact current owned session ID in both locations."));
        }

        var fingerprint = CanonicalStateHash.Combine(
            expectedActionKind == NormalActionKinds.InterplanetaryFlight && expectedVoyagerMode == true
                ? "commit-voyager-flight" : "commit-" + expectedActionKind,
            request.SessionId,
            request.PlanetId,
            request.PlanToken);
        if (_idempotency.TryGet(
            request.SessionId,
            request.IdempotencyKey,
            fingerprint,
            out var replay,
            out var conflict))
        {
            var current = replay!;
            if (_actions.TryGetValue(current.ActionId, out var record))
            {
                current = CreateCommitResult(record, true);
            }
            else
            {
                current = CloneCommitResult(current, true);
            }

            return GameCallResult<NormalActionCommitResult>.Succeeded(current);
        }

        if (conflict)
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyConflict,
                "The idempotency key is already bound to a different normal-game commit.",
                false,
                "Reuse it only for the original commit or generate a new UUID for a newly prepared action."));
        }

        if (!_plans.TryGet(request.PlanToken, out var prepared, out var expired) || prepared is null)
        {
            return MissingPlan<NormalActionCommitResult>(expired);
        }

        var plan = prepared.Payload;
        if (!string.Equals(plan.ActionKind, expectedActionKind, StringComparison.Ordinal))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "The plan token belongs to a different action method.",
                false,
                "Commit the plan through its matching action method."));
        }

        if (expectedActionKind == NormalActionKinds.InterplanetaryFlight
            && expectedVoyagerMode.HasValue && expectedVoyagerMode.Value != (plan.VoyagerBudget is not null))
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, "The flight token belongs to the other flight mode.", false,
                "Use the exact matching same-star or Voyager commit method."));

        var session = _sessions.CaptureOnMainThread();
        var accessError = ValidateCommitCommon(session, plan, request);
        if (accessError is not null)
        {
            return GameCallResult<NormalActionCommitResult>.Failed(accessError);
        }

        if (plan.ActionKind != NormalActionKinds.CancelBlueprintBuild
            && _actions.ActiveValues.Any(a => !a.Terminal && (a.ActionKind == NormalActionKinds.BlueprintBuild
                || IsStructuredConstruction(a.Plan))))
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(BridgeErrorCodes.ServerBusy,
                "A finite blueprint action owns construction; wait or explicitly prepare/commit cancellation first.",
                false, "Poll the active action and its per-object progress. Do not issue competing actions."));

        var staleError = RevalidatePlanOnMainThread(plan);
        if (staleError is not null)
        {
            return GameCallResult<NormalActionCommitResult>.Failed(staleError);
        }

        var journeyError = ValidateVoyagerJourneyLocalCommit(plan);
        if (journeyError is not null) return GameCallResult<NormalActionCommitResult>.Failed(journeyError);

        var activePlayerOrder = _actions.ActiveValues.FirstOrDefault(action =>
            !action.Terminal && IsPlayerOrderAction(action.ActionKind));
        if (IsPlayerOrderAction(plan.ActionKind) && activePlayerOrder is not null)
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.ServerBusy,
                $"Player movement action {activePlayerOrder.ActionId} is still active; a second move, harvest, or flight would replace or race DSP's single player controller.",
                true,
                "Wait for the active player-order action to become terminal, then inspect and prepare again."));
        }

        if (!_actions.HasCapacity())
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.ServerBusy,
                "Retained action evidence is at capacity; no action was accepted or evicted.",
                true,
                "Wait for completed-result retention to expire or reconcile protected outcomes. Do not replay uncertain actions; inspect and prepare again before a later commit."));
        }

        var action = new ActionRecord
        {
            ActionId = Guid.NewGuid().ToString("D"),
            ActionKind = plan.ActionKind,
            SessionId = plan.SessionId,
            PlanetId = plan.PlanetId,
            IdempotencyKey = request.IdempotencyKey,
            State = NormalActionStates.Executing,
            StartedAtGameTick = GameMain.gameTick,
            BeforeStateHash = plan.ExpectedStateHash,
            TargetObjectId = plan.DestinationPlanetId > 0
                ? plan.DestinationPlanetId
                : plan.ResourceNodeId > 0
                    ? plan.ResourceNodeId
                    : (int?)null,
            TargetItemId = plan.RecipeId > 0
                ? plan.RecipeId
                : plan.TechId > 0
                    ? plan.TechId
                    : plan.BuildingItemId > 0
                        ? plan.BuildingItemId
                        : plan.TransferItemId > 0
                            ? plan.TransferItemId
                            : plan.FleetTransferItemId > 0
                                ? plan.FleetTransferItemId
                                : plan.FuelItemId > 0
                                    ? plan.FuelItemId
                                    : plan.ConfigureStationItemId > 0
                                        ? plan.ConfigureStationItemId
                                        : plan.ConfigureRecipeId > 0
                                            ? plan.ConfigureRecipeId
                                            : plan.ConfigureFilterItemId > 0
                                                ? plan.ConfigureFilterItemId
                                                : plan.ConfigureTechId > 0
                                                    ? plan.ConfigureTechId
                                                    : (int?)null,
            RequestedCount = plan.Count > 0 ? plan.Count : (int?)null,
            BeforeTargetAmount = plan.ResourceRemaining > 0 ? plan.ResourceRemaining : (int?)null,
            BeforeInventory = CaptureInventory(GameMain.mainPlayer),
            ExpectedYieldItemIds = plan.YieldItemIds.ToArray(),
            Plan = plan,
        };
        var accepted = CreateCommitResult(action, false);
        if (!_idempotency.TryAdd(request.SessionId, request.IdempotencyKey, fingerprint, accepted))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyCapacityExceeded,
                "The Plugin idempotency cache has reached its configured capacity.",
                false,
                "Start a new Plugin process before attempting further writes."));
        }

        _plans.Remove(request.PlanToken);
        _actions.Add(action.ActionId, action);
        try
        {
            StartActionOnMainThread(action);
        }
        catch (Exception exception)
        {
            action.State = NormalActionStates.OutcomeUnknown;
            action.Terminal = true;
            action.Message = $"The action start outcome could not be proven after {exception.GetType().Name}.";
            action.OriginalOutcomeMessage = action.Message;
            action.CompletedAtGameTick = GameMain.gameTick;
            _sessions.QuarantineWritesOnMainThread(action.ActionId, action.Message);
            // Freeze the actual debit at the quarantine boundary. A later result
            // snapshot may observe inventory, but cannot replace retained proof.
            action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
        }

        return GameCallResult<NormalActionCommitResult>.Succeeded(CreateCommitResult(action, false));
    }

    public void UpdateOnMainThread()
    {
        CheckVoyagerControlWatchdogOnMainThread();
        CheckStructuredConstructionWatchdogOnMainThread();
        CheckVoyagerLocalWalkWatchdogOnMainThread();
        foreach (var action in _actions.ActiveValues.ToArray())
        {
            // Voyager control runs at the audited native tick hook, never at render cadence.
            if (action.Plan.VoyagerBudget is not null || IsStructuredConstruction(action.Plan)) continue;
            UpdateActionOnMainThread(action);
        }
        _actions.RefreshTerminals();
    }

    public bool TryGetActionResultOnMainThread(string actionId, out ActionResultSnapshot? result)
    {
        if (!_actions.TryGetValue(actionId, out var action))
        {
            result = null;
            return false;
        }

        if (!action.Terminal && action.Plan.VoyagerBudget is not null && VoyagerBindingMatches(action.Plan))
            action.VoyagerControlLeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(VoyagerControlLeaseSeconds);
        if (!action.Terminal && action.BlueprintBuild?.ConstructionControl is { StopReason: null } construction
            && ConstructionReferencesMatch(action))
            construction.RenewObservationLease(DateTimeOffset.UtcNow);
        if (!action.Terminal && IsVoyagerLocalOrder(action) && !action.VoyagerLocalStopDeadlineTick.HasValue
            && VoyagerLocalReferencesMatch(action) && DateTimeOffset.UtcNow < action.VoyagerLocalObservationLeaseExpiresAtUtc)
        {
            var nextLease = DateTimeOffset.UtcNow.AddSeconds(30);
            action.VoyagerLocalObservationLeaseExpiresAtUtc = action.Plan.VoyagerLocalDeadlineUtc.HasValue
                && nextLease > action.Plan.VoyagerLocalDeadlineUtc.Value ? action.Plan.VoyagerLocalDeadlineUtc.Value : nextLease;
        }
        if (action.VoyagerLandingPendingPersistence) CompleteVoyagerLanding(action, action.VoyagerLandingCompletionMessage!);
        if (action.VoyagerCoverSaveProof is not null && action.State == NormalActionStates.OutcomeUnknown)
            ReconcileVoyagerCoverSave(action);
        if (IsStructuredConstruction(action.Plan)) ReconcileStructuredConstructionOriginalAction(action);
        result = CreateActionSnapshot(action);
        return true;
    }

    public bool CanReloadFlightCheckpointOnMainThread(FlightCheckpointTicket ticket, out string rejection)
    {
        var active = _actions.ActiveValues.ToArray();
        if (active.Length == 0)
        {
            rejection = string.Empty;
            return true;
        }

        if (active.Length == 1
            && active[0].ActionKind == NormalActionKinds.InterplanetaryFlight
            && string.Equals(active[0].FlightCheckpointId, ticket.CheckpointId, StringComparison.Ordinal))
        {
            rejection = string.Empty;
            return true;
        }

        rejection = "A non-flight normal-game action is still active, so an exact checkpoint reload would interrupt an unproved outcome.";
        return false;
    }

    public bool HasRecoveryRequiredFlightOnMainThread(string checkpointId) =>
        !string.IsNullOrWhiteSpace(checkpointId)
        && _actions.Values.Any(action =>
            action.Terminal
            && action.RecoveryRequired
            && action.ActionKind == NormalActionKinds.InterplanetaryFlight
            && string.Equals(action.FlightCheckpointId, checkpointId, StringComparison.Ordinal));

    public void NotifyFlightCheckpointReloadStartingOnMainThread(FlightCheckpointTicket ticket)
    {
        foreach (var action in _actions.ActiveValues.Where(action =>
                     !action.Terminal
                     && action.ActionKind == NormalActionKinds.InterplanetaryFlight
                     && string.Equals(action.FlightCheckpointId, ticket.CheckpointId, StringComparison.Ordinal)))
        {
            Fail(action, "The exact bound pre-flight checkpoint reload was accepted; this flight attempt is superseded.");
        }
    }

    private void StartActionOnMainThread(ActionRecord action)
    {
        var player = GameMain.mainPlayer;
        var plan = action.Plan;
        switch (action.ActionKind)
        {
            case NormalActionKinds.Move:
                CaptureVoyagerMoveStrictStart(action);
                action.MovementProgress = new MovementProgressWatchdog(
                    GameMain.gameTick,
                    player.position.x,
                    player.position.y,
                    player.position.z,
                    Vector3.Distance(player.position, plan.TargetPosition));
                action.PlayerOrder = OrderNode.MoveTo(plan.TargetPosition);
                player.Order(action.PlayerOrder, false);
                action.State = NormalActionStates.WaitingForGame;
                action.Message = "DSP accepted a normal player movement order.";
                break;
            case NormalActionKinds.InterplanetaryFlight:
                if (plan.VoyagerBudget is not null) StartVoyagerFlightOnMainThread(action);
                else StartInterplanetaryFlightOnMainThread(action);
                break;
            case NormalActionKinds.CancelVoyagerFlight:
                ExecuteCancelVoyagerFlightOnMainThread(action);
                break;
            case NormalActionKinds.Harvest:
                var objectType = plan.ResourceKind == ResourceNodeKinds.Vein
                    ? EObjectType.Vein
                    : EObjectType.Vegetable;
                var approach = CalculateMiningApproach(player.position, plan.TargetPosition);
                action.HarvestApproach = approach;
                action.HarvestApproachProgress = new HarvestApproachProgressWatchdog(
                    GameMain.gameTick,
                    player.position.x,
                    player.position.y,
                    player.position.z,
                    approach.x,
                    approach.y,
                    approach.z);
                action.PlayerOrder = OrderNode.MineTarget(
                    approach,
                    objectType,
                    plan.ResourceNodeId,
                    plan.TargetPosition);
                player.Order(action.PlayerOrder, false);
                action.State = NormalActionStates.WaitingForGame;
                action.Message = "DSP accepted a normal player mining order; walking, energy use, and mining remain game-tick driven.";
                break;
            case NormalActionKinds.Handcraft:
                action.ForgeTask = player.mecha.forge.AddTask(plan.RecipeId, plan.Count);
                if (action.ForgeTask is null)
                {
                    Fail(action, "DSP's normal replicator rejected the task without changing the verified player state.");
                    break;
                }

                action.State = NormalActionStates.WaitingForGame;
                action.Message = "DSP accepted the recipe into the normal replicator queue.";
                break;
            case NormalActionKinds.SelectResearch:
                if (plan.ResearchQueueOrder is not null)
                {
                    ExecuteResearchPriorityOnMainThread(action);
                    break;
                }
                GameMain.history.EnqueueTech(plan.TechId);
                if (!GameMain.history.techQueue.Contains(plan.TechId))
                {
                    Fail(action, "DSP did not add the technology to its normal research queue.");
                    break;
                }

                Complete(action, "DSP's normal technology queue accepted the requested technology.");
                break;
            case NormalActionKinds.Build:
                if (IsStructuredConstruction(plan)) { StartStructuredConstructionOnMainThread(action); break; }
                CreatePreparedPrebuildsOnMainThread(action);
                action.TargetObjectId = action.PrebuildIds.Count == 1 ? -action.PrebuildIds[0] : (int?)null;
                action.TargetObjectIds = action.PrebuildIds.Select(id => -id).ToList();
                action.TargetItemId = plan.BuildingItemId;
                action.State = NormalActionStates.WaitingForGame;
                action.Message = $"DSP created {action.PrebuildIds.Count} ordinary prebuild(s) and consumed the owned building items; construction drones now own completion.";
                break;
            case NormalActionKinds.Dismantle:
                ExecuteDismantleOnMainThread(action);
                break;
            case NormalActionKinds.Upgrade:
                ExecuteUpgradeOnMainThread(action);
                break;
            case NormalActionKinds.BlueprintBuild:
                StartBlueprintBuildOnMainThread(action);
                break;
            case NormalActionKinds.CancelBlueprintBuild:
                CancelBlueprintBuildOnMainThread(action);
                break;
            case NormalActionKinds.Transfer:
                ExecuteStorageTransferOnMainThread(action);
                break;
            case NormalActionKinds.LogisticsStationFleetTransfer:
                ExecuteStationFleetTransferOnMainThread(action);
                break;
            case NormalActionKinds.Refuel:
                ExecuteRefuelOnMainThread(action);
                break;
            case NormalActionKinds.SupplyMechaWarpers:
                ExecuteSupplyMechaWarpersOnMainThread(action);
                break;
            case NormalActionKinds.Save:
                ExecuteSaveOnMainThread(action);
                break;
            case NormalActionKinds.ConfigureBuilding:
                if (plan.ConfigureMode == BuildingConfigurationModes.StorageCapacity)
                {
                    ExecuteStorageConfigurationOnMainThread(action);
                    break;
                }
                ApplyBuildingConfigurationOnMainThread(plan);
                action.TargetObjectId = plan.EntityId;
                action.TargetItemId = plan.ConfigureMode == BuildingConfigurationModes.SorterFilter
                    ? plan.ConfigureFilterItemId
                    : plan.ConfigureMode == BuildingConfigurationModes.Research
                        ? plan.ConfigureTechId
                        : plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationStorage
                            ? plan.ConfigureStationItemId
                            : plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt
                                ? plan.ConfigureStationBeltItemId
                            : plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge
                                ? (int?)null
                                : plan.ConfigureRecipeId;
                var configured = _reader.InspectFactoryEntityOnMainThread(
                    plan.SessionId,
                    new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.EntityId });
                if (!configured.Success || configured.Value is null
                    || (plan.ConfigureMode == BuildingConfigurationModes.Production
                        && configured.Value.RecipeId != plan.ConfigureRecipeId)
                    || (plan.ConfigureMode == BuildingConfigurationModes.Research
                        && !IsLabInResearchMode(plan.EntityId, plan.ConfigureTechId))
                    || (plan.ConfigureMode == BuildingConfigurationModes.SorterFilter
                        && ((configured.Value.FilterItemId ?? 0) != plan.ConfigureFilterItemId
                            || !IsSorterFilterApplied(plan.EntityId, plan.ConfigureFilterItemId)))
                    || (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationStorage
                        && !IsLogisticsStationStorageConfigurationApplied(configured.Value, plan))
                    || (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt
                        && !IsLogisticsStationBeltConfigurationApplied(configured.Value, plan))
                    || (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge
                        && !IsLogisticsStationChargeConfigurationApplied(configured.Value, plan)))
                {
                    throw new InvalidOperationException("The configured device mode could not be proven by immediate readback.");
                }

                Complete(action, plan.ConfigureMode == BuildingConfigurationModes.SorterFilter
                    ? "The current-version sorter UI setting path applied the item filter and component/sign readback matched."
                    : plan.ConfigureMode == BuildingConfigurationModes.Research
                        ? "The current-version lab setting path applied research mode and active-technology readback matched."
                        : plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationStorage
                            ? "PlanetTransport.SetStationStorage applied the unlocked item, capacity, and local/remote logic once; immediate readback matched and the call preserved slot inventory."
                        : plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt
                            ? "The current-version station UI field path selected the configured item for the exact output port; immediate topology and inventory readback matched."
                        : plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge
                            ? "The current-version station UI field path applied the maximum charge power once; immediate power-consumer readback matched and station/player inventory remained unchanged."
                        : "The current-version device configuration path applied the unlocked recipe and readback matched.");
                action.AfterStateHash = plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationStorage
                                        || plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt
                                        || plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge
                    ? configured.Value.LogisticsStation?.ConfigurationStateHash
                    : configured.Value.StateHash;
                break;
            default:
                throw new InvalidOperationException("Unsupported normal-game action kind.");
        }

        _sessions.IncrementRevisionOnMainThread();
    }

    private void UpdateActionOnMainThread(ActionRecord action)
    {
        // Local journey identity loss is unknown under its original evidence;
        // the generic branch below must not stamp a replacement world's tick.
        if (action.Plan.VoyagerLocalDeadlineGameTick.HasValue && !GuardVoyagerLocalAction(action)) return;
        var isFlight = action.ActionKind == NormalActionKinds.InterplanetaryFlight;
        if (!_sessions.IsCurrentSessionOwned
            || !string.Equals(_sessions.SessionId, action.SessionId, StringComparison.Ordinal)
            || (!isFlight && GameMain.localPlanet?.id != action.PlanetId))
        {
            if (isFlight)
            {
                Fail(action, "The owned session ended before the normal flight completed; its bound checkpoint requires recovery.");
                return;
            }

            action.State = NormalActionStates.ActionFailed;
            action.Terminal = true;
            action.CompletedAtGameTick = GameMain.gameTick;
            action.Message = "The owned session or local planet ended before the normal game action completed.";
            return;
        }

        switch (action.ActionKind)
        {
            case NormalActionKinds.Move:
                UpdateMove(action);
                break;
            case NormalActionKinds.InterplanetaryFlight:
                UpdateInterplanetaryFlight(action);
                break;
            case NormalActionKinds.Harvest:
                UpdateHarvest(action);
                break;
            case NormalActionKinds.Handcraft:
                UpdateHandcraft(action);
                break;
            case NormalActionKinds.Build:
                UpdateBuild(action);
                break;
            case NormalActionKinds.BlueprintBuild:
                UpdateBlueprintBuildOnMainThread(action);
                break;
        }
    }

    private void UpdateMove(ActionRecord action)
    {
        var player = GameMain.mainPlayer;
        var distance = Vector3.Distance(player.position, action.Plan.TargetPosition);
        if (distance <= action.Plan.ArrivalTolerance && !DeferVoyagerMoveGroundGapArrival(action))
        {
            AbortPlayerOrderIfOwned(action);
            Complete(action, $"Player reached the surface target within {distance:F2} metres through normal movement ticks.");
            return;
        }

        var wasPowerStarved = action.PowerStarvedAtGameTick.HasValue;
        if (FailPowerStarvedPlayerOrder(action, "movement"))
        {
            return;
        }

        if (action.PowerStarvedAtGameTick.HasValue)
        {
            return;
        }

        action.MovementProgress ??= new MovementProgressWatchdog(
            action.StartedAtGameTick,
            player.position.x,
            player.position.y,
            player.position.z,
            distance);
        if (wasPowerStarved)
        {
            action.MovementProgress.ResetWindow(
                GameMain.gameTick,
                player.position.x,
                player.position.y,
                player.position.z,
                distance);
        }

        var progress = action.MovementProgress.Observe(
            GameMain.gameTick,
            player.position.x,
            player.position.y,
            player.position.z,
            distance);
        if (progress.Status != MovementProgressStatus.Progressing)
        {
            AbortPlayerOrderIfOwned(action);
            action.Stalled = true;
            ApplyMovementFailureAdvice(action, MovementFailureRecoveryAdvisor.ForStall(progress));
            var condition = progress.Status == MovementProgressStatus.PositionStalled
                ? $"made less than {MovementProgressWatchdog.DefaultMinimumDisplacement:F2} metres of physical progress"
                : $"did not reduce its best remaining distance by {MovementProgressWatchdog.DefaultMinimumTargetProgress:F2} metres";
            Fail(action,
                $"The normal movement order {condition} for {progress.StalledGameTicks} game ticks "
                + $"while {progress.RemainingDistance:F2} metres remained; Spherewright stopped only its exact owned order before the global timeout or energy exhaustion.");
            return;
        }

        if (GameMain.gameTick > action.StartedAtGameTick + Math.Max(3600, action.Plan.EstimatedTicks * 8))
        {
            AbortPlayerOrderIfOwned(action);
            ApplyMovementFailureAdvice(
                action,
                MovementFailureRecoveryAdvisor.ForBoundedTimeout(
                    GameMain.gameTick - action.StartedAtGameTick,
                    distance));
            Fail(action, "The normal movement order did not reach its target within the bounded game-tick window.");
        }
    }

    private static void ApplyMovementFailureAdvice(
        ActionRecord action,
        MovementFailureRecoveryAdvice advice)
    {
        action.FailureKind = advice.FailureKind;
        action.StalledGameTicks = advice.StalledGameTicks;
        action.RemainingDistance = advice.RemainingDistance;
        action.DoNotRetrySameTarget = advice.DoNotRetrySameTarget;
        action.RecommendedRecovery = advice.RecommendedRecovery;
        action.RecommendedShortMoveDistanceMeters = advice.RecommendedShortMoveDistanceMeters;
        action.OrthogonalProbeDistanceMeters = advice.OrthogonalProbeDistanceMeters;
        action.MaximumOrthogonalProbeAttempts = advice.MaximumOrthogonalProbeAttempts;
    }

    private void UpdateHarvest(ActionRecord action)
    {
        var plan = action.Plan;
        var afterInventory = CaptureInventory(GameMain.mainPlayer);
        var yielded = plan.YieldItemIds.Sum(itemId =>
            GetCount(afterInventory, itemId) - GetCount(action.BeforeInventory, itemId));
        var inspect = _reader.InspectResourceNodeOnMainThread(
            plan.SessionId,
            new InspectResourceNodeRequest
            {
                PlanetId = plan.PlanetId,
                Kind = plan.ResourceKind,
                NodeId = plan.ResourceNodeId,
            });
        var nodeObserved = inspect.Success && inspect.Value is not null;
        var nodeRemoved = !inspect.Success && inspect.Error?.Code == BridgeErrorCodes.InvalidEntity;
        var remaining = nodeObserved ? inspect.Value!.RemainingAmount : 0;
        var targetReduced = plan.ResourceRemaining - remaining;
        var completed = plan.ResourceKind == ResourceNodeKinds.Vegetation
            ? nodeRemoved
            : yielded >= plan.Count || nodeRemoved;
        if (completed)
        {
            AbortPlayerOrderIfOwned(action);
            action.AfterTargetAmount = remaining;
            Complete(action,
                $"Normal manual harvesting completed: node reduction {targetReduced}, observed inventory yield {yielded}.");
            return;
        }

        var wasPowerStarved = action.PowerStarvedAtGameTick.HasValue;
        if (FailPowerStarvedPlayerOrder(action, "mining"))
        {
            return;
        }

        if (GameMain.gameTick > action.StartedAtGameTick + Math.Max(7200, plan.EstimatedTicks * 8))
        {
            AbortPlayerOrderIfOwned(action);
            Fail(action, "The normal mining order did not produce the requested observed yield within the bounded game-tick window.");
            return;
        }

        if (action.PowerStarvedAtGameTick.HasValue)
        {
            return;
        }

        if (!action.HarvestApproach.HasValue)
        {
            AbortPlayerOrderIfOwned(action);
            Fail(action, "The committed normal mining action did not retain its exact native approach point.");
            return;
        }

        var player = GameMain.mainPlayer;
        var approach = action.HarvestApproach.Value;
        action.HarvestApproachProgress ??= new HarvestApproachProgressWatchdog(
            action.StartedAtGameTick,
            player.position.x,
            player.position.y,
            player.position.z,
            approach.x,
            approach.y,
            approach.z);
        if (wasPowerStarved)
        {
            action.HarvestApproachProgress.ResetWindow(
                GameMain.gameTick,
                player.position.x,
                player.position.y,
                player.position.z);
        }

        var progress = action.HarvestApproachProgress.Observe(
            GameMain.gameTick,
            player.position.x,
            player.position.y,
            player.position.z,
            action.PlayerOrder?.targetReached ?? false,
            yielded > 0,
            nodeObserved && targetReduced > 0);
        if (!action.HarvestApproachProgress.IsApproachMonitoringComplete
            && progress.Status != MovementProgressStatus.Progressing)
        {
            AbortPlayerOrderIfOwned(action);
            action.Stalled = true;
            ApplyMovementFailureAdvice(action, MovementFailureRecoveryAdvisor.ForStall(progress));
            var condition = progress.Status == MovementProgressStatus.PositionStalled
                ? $"made less than {MovementProgressWatchdog.DefaultMinimumDisplacement:F2} metres of physical progress"
                : $"did not reduce its best remaining distance by {MovementProgressWatchdog.DefaultMinimumTargetProgress:F2} metres";
            Fail(action,
                $"The normal mining approach order {condition} for {progress.StalledGameTicks} game ticks "
                + $"while {progress.RemainingDistance:F2} metres remained; Spherewright stopped only its exact owned order before the global timeout or energy exhaustion.");
            return;
        }

    }

    private bool FailPowerStarvedPlayerOrder(ActionRecord action, string orderKind)
    {
        var mecha = GameMain.mainPlayer?.mecha;
        if (mecha is null || mecha.coreEnergy > 0.5d || mecha.reactorEnergy > 0.5d
            || mecha.reactorItemId > 0 || HasUsableFuel(mecha.reactorStorage))
        {
            action.PowerStarvedAtGameTick = null;
            return false;
        }

        action.PowerStarvedAtGameTick ??= GameMain.gameTick;
        if (GameMain.gameTick < action.PowerStarvedAtGameTick.Value + PowerStarvationGraceTicks)
        {
            return false;
        }

        AbortPlayerOrderIfOwned(action);
        Fail(action,
            $"The normal {orderKind} order stopped after the mecha had no core energy, reactor energy, current reactor item, or usable fuel for {PowerStarvationGraceTicks} game ticks.");
        return true;
    }

    private static bool HasUsableFuel(StorageComponent? storage)
    {
        var grids = storage?.grids;
        if (storage is null || grids is null)
        {
            return false;
        }

        var limit = Math.Min(storage.size, grids.Length);
        for (var index = 0; index < limit; index++)
        {
            var grid = grids[index];
            var item = grid.itemId > 0 && grid.count > 0 ? LDB.items.Select(grid.itemId) : null;
            if (item is not null && item.HeatValue > 0L && item.FuelType > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void AbortPlayerOrderIfOwned(ActionRecord action, bool forLocalNativeStop = false)
    {
        if (IsVoyagerLocalOrder(action) && !action.VoyagerLocalCompleting && !forLocalNativeStop)
            return; // The original local stop path first proves its paid Native tail.
        var player = GameMain.mainPlayer;
        if (player is null || action.PlayerOrder is null)
        {
            return;
        }

        if (ReferenceEquals(player.currentOrder, action.PlayerOrder))
        {
            player.AbortOrder();
        }
    }

    private static bool IsPlayerOrderAction(string actionKind) =>
        actionKind == NormalActionKinds.Move
        || actionKind == NormalActionKinds.Harvest
        || actionKind == NormalActionKinds.InterplanetaryFlight;

    private void UpdateHandcraft(ActionRecord action)
    {
        var player = GameMain.mainPlayer;
        var queueContainsTask = action.ForgeTask is not null && player.mecha.forge.tasks.Contains(action.ForgeTask);
        if (queueContainsTask)
        {
            return;
        }

        var after = CaptureInventory(player);
        var expectedProducts = GetRecipeProducts(action.Plan.RecipeId, action.Plan.Count);
        var productsObserved = expectedProducts.All(pair =>
            GetCount(after, pair.Key) - GetCount(action.BeforeInventory, pair.Key) >= pair.Value);
        if (productsObserved)
        {
            Complete(action, "The normal replicator task completed and all runtime recipe products were reread in player inventory.");
        }
        else
        {
            Fail(action, "The replicator task left the queue, but the expected products were not all present in player inventory.");
        }
    }

    private void UpdateBuild(ActionRecord action)
        => UpdatePreparedBuildOnMainThread(action);

    private BridgeError? RevalidatePlanOnMainThread(NormalActionPlanPayload plan)
    {
        switch (plan.ActionKind)
        {
            case NormalActionKinds.InterplanetaryFlight:
                return plan.VoyagerBudget is not null
                    ? RevalidateVoyagerFlightOnMainThread(plan)
                    : RevalidateInterplanetaryFlightPlanOnMainThread(plan);
            case NormalActionKinds.CancelVoyagerFlight:
                return RevalidateCancelVoyagerFlightOnMainThread(plan);
            case NormalActionKinds.Move:
            case NormalActionKinds.Handcraft:
                var player = _reader.GetPlayerStateOnMainThread(
                    plan.SessionId,
                    new LocalPlanetRequest { PlanetId = plan.PlanetId });
                if (!player.Success || player.Value is null)
                {
                    return player.Error;
                }

                var matchesPlayerState = plan.ActionKind == NormalActionKinds.Move
                    && plan.PassiveDriftInspection is not null
                    ? ReferenceEquals(plan.PassiveDriftInspection, _reader.LastPassiveDriftMoveInspection)
                        && GameMain.mainPlayer.currentOrder is null
                        && plan.PassiveDriftInspection.Allows(plan.PassiveDriftInspection.StateHash,
                            player.Value, GameStateReader.PassiveDriftClockSeconds())
                    : string.Equals(CanonicalStateHash.PlayerAction(player.Value), plan.PlayerStateHash, StringComparison.Ordinal);
                if (!matchesPlayerState)
                {
                    return Stale("Player state no longer matches the prepared action.");
                }

                if (plan.ActionKind == NormalActionKinds.Handcraft)
                {
                    var recipe = LDB.recipes.Select(plan.RecipeId);
                    if (recipe is null || !recipe.Handcraft || !GameMain.history.RecipeUnlocked(plan.RecipeId)
                        || !GameMain.mainPlayer.mecha.forge.TryAddTask(plan.RecipeId, plan.Count))
                    {
                        return Stale("Handcraft recipe, unlock, or material availability changed after prepare.");
                    }
                }

                return null;
            case NormalActionKinds.Harvest:
                var resource = _reader.InspectResourceNodeOnMainThread(
                    plan.SessionId,
                    new InspectResourceNodeRequest
                    {
                        PlanetId = plan.PlanetId,
                        Kind = plan.ResourceKind,
                        NodeId = plan.ResourceNodeId,
                    });
                var harvestPlayer = _reader.GetPlayerStateOnMainThread(
                    plan.SessionId,
                    new LocalPlanetRequest { PlanetId = plan.PlanetId });
                if (!resource.Success || resource.Value is null)
                {
                    return resource.Error;
                }

                if (!harvestPlayer.Success || harvestPlayer.Value is null)
                {
                    return harvestPlayer.Error;
                }

                return string.Equals(resource.Value.StateHash, plan.ResourceStateHash, StringComparison.Ordinal)
                    && string.Equals(CanonicalStateHash.PlayerAction(harvestPlayer.Value), plan.PlayerStateHash, StringComparison.Ordinal)
                    ? null
                    : Stale("Player or bound resource state changed after prepare.");
            case NormalActionKinds.SelectResearch:
                var progression = _reader.GetProgressionStateOnMainThread(
                    plan.SessionId,
                    new LocalPlanetRequest { PlanetId = plan.PlanetId });
                if (!progression.Success || progression.Value is null)
                {
                    return progression.Error;
                }

                return string.Equals(progression.Value.SelectionStateHash, plan.ProgressionSelectionStateHash, StringComparison.Ordinal)
                    && (plan.ResearchQueueOrder is null ? GameMain.history.CanEnqueueTech(plan.TechId)
                        : TryPrepareResearchPriority(plan.TechId)?.SequenceEqual(plan.ResearchQueueOrder) == true)
                    ? null
                    : Stale("Technology state, prerequisites, or queue changed after prepare.");
            case NormalActionKinds.Build:
                return RevalidateStructuredBuildOnMainThread(plan);
            case NormalActionKinds.Dismantle:
                return RevalidateDismantlePlanOnMainThread(plan);
            case NormalActionKinds.Upgrade:
                return RevalidateUpgradeOnMainThread(plan);
            case NormalActionKinds.BlueprintBuild:
                return RevalidateBlueprintBuildOnMainThread(plan);
            case NormalActionKinds.CancelBlueprintBuild:
                return RevalidateBlueprintCancelOnMainThread(plan);
            case NormalActionKinds.Transfer:
                return RevalidateStorageTransferOnMainThread(plan);
            case NormalActionKinds.LogisticsStationFleetTransfer:
                return RevalidateStationFleetTransferOnMainThread(plan);
            case NormalActionKinds.Refuel:
                return RevalidateRefuelOnMainThread(plan);
            case NormalActionKinds.SupplyMechaWarpers:
                return RevalidateSupplyMechaWarpersOnMainThread(plan);
            case NormalActionKinds.Save:
                return RevalidateSaveOnMainThread(plan);
            case NormalActionKinds.ConfigureBuilding:
                if (plan.ConfigureMode == BuildingConfigurationModes.StorageCapacity)
                    return RevalidateStorageConfigurationOnMainThread(plan);
                var configureSnapshot = _reader.InspectFactoryEntityOnMainThread(
                    plan.SessionId,
                    new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.EntityId });
                if (!configureSnapshot.Success || configureSnapshot.Value is null)
                {
                    return configureSnapshot.Error;
                }

                var sorterFilterMode = plan.ConfigureMode == BuildingConfigurationModes.SorterFilter;
                var currentConfigurationHash = sorterFilterMode
                    ? configureSnapshot.Value.ConfigurationStateHash
                    : configureSnapshot.Value.StateHash;
                if (!string.Equals(currentConfigurationHash, plan.FactoryStateHash, StringComparison.Ordinal)
                    || (!sorterFilterMode
                        && plan.ConfigureMode != BuildingConfigurationModes.LogisticsStationStorage
                        && plan.ConfigureMode != BuildingConfigurationModes.LogisticsStationBelt
                        && plan.ConfigureMode != BuildingConfigurationModes.LogisticsStationCharge
                        && (configureSnapshot.Value.Progress != 0
                            || configureSnapshot.Value.IsWorking
                            || configureSnapshot.Value.Buffers.Any(buffer => buffer.Count != 0))))
                {
                    return Stale("Device identity, buffers, progress, unlock, or recipe state changed after prepare.");
                }

                return RevalidateStructuredConfigurationOnMainThread(plan);
            default:
                return BridgeError.Create(
                    BridgeErrorCodes.InvalidRequest,
                    "Unsupported prepared action kind.",
                    false,
                    "Prepare one of the public normal-game action types.");
        }
    }

    private CommonPrepareResult ValidatePrepareCommon(string? requestedSessionId, int planetId, int stateHashVersion)
    {
        if (stateHashVersion != StateHashVersion)
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "Unsupported action state-hash version.",
                false,
                "Inspect current state and use the returned stateHashVersion."));
        }

        var session = _sessions.CaptureOnMainThread();
        if (!session.GameLoaded)
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.GameNotLoaded,
                "No game is loaded.",
                true,
                "Create and wait for a fresh Spherewright-owned ordinary world."));
        }

        if (!session.OwnedBySpherewright)
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.SessionNotOwned,
                "Normal-game actions are restricted to the exact world created by this Plugin process.",
                false,
                "Return to the main menu and create a fresh world through Spherewright."));
        }

        if (!string.Equals(requestedSessionId, session.SessionId, StringComparison.Ordinal))
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "The requested session is not the current owned session.",
                false,
                "Inspect current session state and retry with its exact session ID."));
        }

        if (!OwnedFactoryScopePolicy.IsCurrentLocalActionTarget(planetId, session.LocalPlanetId ?? 0))
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.NoLocalPlanet,
                "The requested planet is not the current local planet.",
                false,
                "Use the current localPlanetId returned by session state."));
        }

        return CommonPrepareResult.Succeeded(session);
    }

    private static BridgeError? ValidateCommitCommon(
        SessionState session,
        NormalActionPlanPayload plan,
        CommitNormalActionRequest request)
    {
        if (!session.OwnedBySpherewright
            || !string.Equals(session.SessionId, plan.SessionId, StringComparison.Ordinal)
            || !string.Equals(request.SessionId, plan.SessionId, StringComparison.Ordinal))
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "The prepared action does not belong to the current owned session.",
                false,
                "Inspect the current session and prepare a fresh action.");
        }

        if (request.PlanetId != plan.PlanetId || (plan.ActionKind != NormalActionKinds.CancelVoyagerFlight
            && session.LocalPlanetId != plan.PlanetId))
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "Commit planet, planned planet, and current local planet do not match.",
                false,
                "Return to the planned planet and prepare a fresh action.");
        }

        if (session.WriteBlockers.Count > 0)
        {
            var blocker = session.WriteBlockers[0];
            return BridgeError.Create(
                blocker.Code,
                blocker.Message,
                false,
                "Resolve every current session write blocker, then prepare a fresh action.");
        }

        return null;
    }

    private GameCallResult<PreparedNormalAction> AddPreparedPlan(
        NormalActionPlanPayload payload,
        SessionState session,
        long estimatedTicks,
        string completionCondition)
    {
        payload.EstimatedTicks = estimatedTicks;
        PreparedPlan<NormalActionPlanPayload> plan;
        try
        {
            plan = _plans.Add(payload.ExpectedStateHash, payload);
        }
        catch (InvalidOperationException)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ServerBusy,
                "Too many normal-game plans are active.",
                true,
                "Wait for old plans to expire, then inspect and prepare again."));
        }

        return GameCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction
        {
            Prepared = true,
            ActionKind = payload.ActionKind,
            PlanToken = plan.Token,
            ExpiresAtUtc = plan.ExpiresAtUtc,
            ExpectedStateHash = payload.ExpectedStateHash,
            StateHashVersion = StateHashVersion,
            CommitAllowedNow = session.WriteBlockers.Count == 0,
            EstimatedDistance = payload.EstimatedDistance,
            EstimatedGameTicks = estimatedTicks,
            CommitBlockers = session.WriteBlockers.Select(CloneBlocker).ToList(),
            CompletionCondition = completionCondition,
        });
    }

    private void Complete(ActionRecord action, string message)
    {
        if (IsVoyagerLocalOrder(action) && !action.VoyagerLocalCompleting)
        { RequestVoyagerLocalOrderStop(action, true, message); return; }
        if (action.Plan.VoyagerBudget is not null)
        {
            CompleteVoyagerLanding(action, message);
            return;
        }
        if (action.ActionKind == NormalActionKinds.InterplanetaryFlight)
        {
            AbortPlayerOrderIfOwned(action);
            ReleaseNativeAscentInput(action);
            var lifecycleRejection = "The bound checkpoint identity is missing.";
            if (string.IsNullOrWhiteSpace(action.FlightCheckpointId)
                || !_flightCheckpoints.TryMarkFlightSucceeded(
                    action.FlightCheckpointId!,
                    action.ActionId,
                    GameMain.gameTick,
                    out lifecycleRejection))
            {
                action.State = NormalActionStates.OutcomeUnknown;
                action.Terminal = true;
                action.Succeeded = false;
                action.CompletedAtGameTick = GameMain.gameTick;
                action.Message = $"The physical flight completed, but its rollback checkpoint could not be sealed before the primary save: {lifecycleRejection}";
                action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
                action.AfterStateHash ??= CaptureAfterStateHash(action);
                _sessions.QuarantineWritesOnMainThread(action.ActionId, action.Message);
                return;
            }

            _sessions.ForgetCurrentFlightCheckpoint(action.FlightCheckpointId!);
        }

        action.State = NormalActionStates.Completed;
        action.Terminal = true;
        action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.Message = message;
        action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
        action.AfterStateHash = CaptureAfterStateHash(action);
        _sessions.IncrementRevisionOnMainThread();
    }

    private void Fail(ActionRecord action, string message)
    {
        if (IsVoyagerLocalOrder(action) && !action.VoyagerLocalCompleting)
        { RequestVoyagerLocalOrderStop(action, false, message); return; }
        if (action.Plan.VoyagerBudget is not null && !action.VoyagerNativeLaunchStarted)
        {
            FinishVoyagerBeforeLaunchFailure(action, message);
            return;
        }
        if (action.Plan.VoyagerBudget is not null && !action.VoyagerStopSettled)
        {
            RequestVoyagerStop(action, message);
            return;
        }
        if (action.ActionKind == NormalActionKinds.InterplanetaryFlight)
        {
            AbortPlayerOrderIfOwned(action);
            ReleaseNativeAscentInput(action);
            if (!string.IsNullOrWhiteSpace(action.FlightCheckpointId))
            {
                action.RecoveryRequired = true;
                if (!_flightCheckpoints.TryMarkRecoveryRequired(
                        action.FlightCheckpointId!,
                        action.ActionId,
                        GameMain.gameTick,
                        out var lifecycleRejection))
                {
                    message += $" Checkpoint lifecycle persistence also failed: {lifecycleRejection}";
                }
            }
        }

        action.State = action.RecoveryRequired
            ? NormalActionStates.RecoveryRequired
            : NormalActionStates.ActionFailed;
        action.Terminal = true;
        action.Succeeded = false;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.Message = message;
        action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
        action.AfterStateHash = CaptureAfterStateHash(action);
    }

    private string? CaptureAfterStateHash(ActionRecord action)
    {
        var structured = CaptureStructuredAfterStateHash(action);
        if (structured is not null)
        {
            return structured;
        }

        if (action.ActionKind == NormalActionKinds.SelectResearch)
        {
            var progression = _reader.GetProgressionStateOnMainThread(
                action.SessionId,
                new LocalPlanetRequest { PlanetId = action.PlanetId });
            return progression.Value?.StateHash;
        }

        var player = _reader.GetPlayerStateOnMainThread(
            action.SessionId,
            new LocalPlanetRequest { PlanetId = action.PlanetId });
        return player.Value?.StateHash;
    }

    private ActionResultSnapshot CreateActionSnapshot(ActionRecord action)
    {
        var result = new ActionResultSnapshot
        {
            ActionId = action.ActionId,
            ActionKind = action.ActionKind,
            State = action.State,
            Terminal = action.Terminal,
            Succeeded = action.Succeeded,
            SessionId = action.SessionId,
            PlanetId = action.PlanetId,
            Message = action.Message,
            IdempotencyKey = action.IdempotencyKey,
            StartedAtGameTick = action.StartedAtGameTick,
            CompletedAtGameTick = action.CompletedAtGameTick,
            BeforeStateHash = action.BeforeStateHash,
            AfterStateHash = action.AfterStateHash,
            TargetObjectId = action.TargetObjectId,
            TargetObjectIds = action.TargetObjectIds.ToList(),
            TargetItemId = action.TargetItemId,
            RequestedCount = action.RequestedCount,
            BeforeTargetAmount = action.BeforeTargetAmount,
            AfterTargetAmount = action.AfterTargetAmount,
            UpgradeReadback = action.UpgradeReadback,
            ResearchQueueReadback = action.ResearchQueueReadback,
            StorageConfigurationReadback = action.StorageConfigurationReadback,
            MechaWarperSupplyReadback = action.MechaWarperSupplyReadback,
            VoyagerFlightReadback = action.VoyagerFlightReadback,
            BlueprintBuildId = action.BlueprintBuild?.BuildId,
            BlueprintBuildPhase = action.BlueprintBuild?.Phase,
            BlueprintObjects = action.BlueprintBuild is null ? null : CloneBlueprintObjects(action.BlueprintBuild.Objects),
            ConstructionControl = CaptureConstructionControl(action.BlueprintBuild, action),
            ReconciledFromOutcomeUnknown = action.ReconciledFromOutcomeUnknown,
            ReconciledAtGameTick = action.ReconciledAtGameTick,
            FlightCheckpointId = action.FlightCheckpointId,
            FlightCheckpointReloadToken = action.FlightCheckpointReloadToken,
            FlightCheckpointGameTick = action.FlightCheckpointGameTick,
            Stalled = action.Stalled,
            RecoveryRequired = action.RecoveryRequired,
            FailureKind = action.FailureKind,
            StalledGameTicks = action.StalledGameTicks,
            RemainingDistance = action.RemainingDistance,
            DoNotRetrySameTarget = action.DoNotRetrySameTarget,
            RecommendedRecovery = action.RecommendedRecovery,
            RecommendedShortMoveDistanceMeters = action.RecommendedShortMoveDistanceMeters,
            OrthogonalProbeDistanceMeters = action.OrthogonalProbeDistanceMeters,
            MaximumOrthogonalProbeAttempts = action.MaximumOrthogonalProbeAttempts,
        };
        if (action.Plan.VoyagerBudget is not null && !VoyagerBindingMatches(action.Plan) && action.AfterInventory is null)
            return result; // No current-world inventory is evidence for an ended original binding.
        if (IsStructuredConstruction(action.Plan) && !ConstructionReferencesMatch(action) && action.AfterInventory is null)
            return result;
        if (action.Plan.VoyagerLocalDeadlineGameTick.HasValue && !VoyagerLocalReferencesMatch(action) && action.AfterInventory is null)
            return result;
        var after = action.AfterInventory ?? CaptureInventory(GameMain.mainPlayer);
        foreach (var itemId in action.BeforeInventory.Keys.Concat(after.Keys).Distinct().OrderBy(id => id))
        {
            var beforeCount = GetCount(action.BeforeInventory, itemId);
            var afterCount = GetCount(after, itemId);
            if (beforeCount == afterCount)
            {
                continue;
            }

            result.ItemDeltas.Add(new ActionItemDelta
            {
                ItemId = itemId,
                Name = LDB.items.Select(itemId)?.name ?? string.Empty,
                BeforeCount = beforeCount,
                AfterCount = afterCount,
                Delta = afterCount - beforeCount,
            });
        }

        return result;
    }

    private static NormalActionCommitResult CreateCommitResult(ActionRecord action, bool replay)
    {
        return new NormalActionCommitResult
        {
            ActionId = action.ActionId,
            ActionKind = action.ActionKind,
            IdempotencyKey = action.IdempotencyKey,
            State = action.State,
            Accepted = true,
            IdempotentReplay = replay,
        };
    }

    private static NormalActionCommitResult CloneCommitResult(NormalActionCommitResult result, bool replay)
    {
        return new NormalActionCommitResult
        {
            ActionId = result.ActionId,
            ActionKind = result.ActionKind,
            IdempotencyKey = result.IdempotencyKey,
            State = result.State,
            Accepted = result.Accepted,
            IdempotentReplay = replay,
        };
    }

    private static Dictionary<int, int> CaptureInventory(Player player)
    {
        var result = new Dictionary<int, int>();
        if (player?.package?.grids is not null)
        {
            for (var index = 0; index < Math.Min(player.package.size, player.package.grids.Length); index++)
            {
                var grid = player.package.grids[index];
                if (grid.itemId > 0 && grid.count > 0)
                {
                    result[grid.itemId] = GetCount(result, grid.itemId) + grid.count;
                }
            }
        }

        if (player is not null && player.inhandItemId > 0 && player.inhandItemCount > 0)
        {
            result[player.inhandItemId] = GetCount(result, player.inhandItemId) + player.inhandItemCount;
        }

        return result;
    }

    private static string CapturePlayerPackageState(Player player)
    {
        var fields = new List<object?>();
        var grids = player?.package?.grids ?? Array.Empty<StorageComponent.GRID>();
        var size = player?.package?.size ?? 0;
        fields.Add(size);
        for (var index = 0; index < Math.Min(size, grids.Length); index++)
        {
            var grid = grids[index];
            fields.Add(index);
            fields.Add(grid.itemId);
            fields.Add(grid.count);
            fields.Add(grid.inc);
        }

        fields.Add(player?.inhandItemId ?? 0);
        fields.Add(player?.inhandItemCount ?? 0);
        fields.Add(player?.inhandItemInc ?? 0);
        return CanonicalStateHash.Combine("player-package-v1", fields.ToArray());
    }

    private static Dictionary<int, int> GetRecipeProducts(int recipeId, int count)
    {
        var result = new Dictionary<int, int>();
        var recipe = LDB.recipes.Select(recipeId);
        if (recipe is null)
        {
            return result;
        }

        for (var index = 0; index < Math.Min(recipe.Results.Length, recipe.ResultCounts.Length); index++)
        {
            result[recipe.Results[index]] = recipe.ResultCounts[index] * count;
        }

        return result;
    }

    private static bool TryFindCoreBuildCandidate(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        float preferredDistance,
        out Vector3 position,
        out Quaternion rotation,
        out float yaw,
        out string rejection)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        yaw = 0f;
        rejection = "No candidate was tested.";
        var distances = new[]
        {
            preferredDistance,
            Math.Min(30f, preferredDistance + 5f),
            Math.Max(5f, preferredDistance - 4f),
        }.Distinct().ToArray();
        var lateralOffsets = new[] { 0f, 5f, -5f, 10f, -10f, 15f, -15f };
        for (var candidateYaw = 0f; candidateYaw < 360f; candidateYaw += 30f)
        {
            var basis = Maths.SphericalRotation(player.position, candidateYaw);
            var forward = basis * Vector3.forward;
            var right = basis * Vector3.right;
            foreach (var distance in distances)
            {
                foreach (var lateral in lateralOffsets)
                {
                    var candidatePosition = factory.planet.aux.Snap(
                        player.position + forward * distance + right * lateral,
                        onTerrain: true);
                    var candidateRotation = Maths.SphericalRotation(candidatePosition, candidateYaw);
                    if (ValidateExactCoreBuildCandidate(
                            factory,
                            player,
                            item,
                            candidatePosition,
                            candidateRotation,
                            candidateYaw,
                            out rejection))
                    {
                        position = candidatePosition;
                        rotation = candidateRotation;
                        yaw = candidateYaw;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool ValidateExactCoreBuildCandidate(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        Vector3 position,
        Quaternion rotation,
        float yaw,
        out string rejection)
    {
        rejection = string.Empty;
        var build = player.controller.actionBuild;
        if (build.active || build.templatePreviews.Count != 0 || build.clickTool.buildPreviews.Count != 0)
        {
            rejection = "The player's normal build UI owns preview state.";
            return false;
        }

        build.SetFactoryReferences();
        var tool = new SpherewrightClickBuildTool();
        tool._Init(GameMain.data!);
        tool.SetFactoryReferences();
        try
        {
            if (!ReferenceEquals(tool.factory, factory))
            {
                rejection = "The isolated DSP click-build validator is not bound to the local factory.";
                return false;
            }

            tool.handItem = item;
            tool.handPrefabDesc = item.prefabDesc;
            tool.yaw = yaw;
            if (!tool.SnapshotPlayerInventory())
            {
                rejection = "The player inventory could not be copied for DSP build validation.";
                return false;
            }

            var preview = CreateCorePreview(item, position, rotation);
            tool.buildPreviews.Add(preview);
            var accepted = tool.CheckBuildConditions();
            rejection = accepted && preview.condition == EBuildCondition.Ok
                ? string.Empty
                : $"DSP returned {preview.condition}.";
            return accepted && preview.condition == EBuildCondition.Ok;
        }
        finally
        {
            tool.buildPreviews.Clear();
            tool.ReleaseSnapshot();
            tool._Free();
        }
    }

    private static int CreateCorePrebuildOnMainThread(ActionRecord action)
    {
        var factory = GameMain.localPlanet?.factory
            ?? throw new InvalidOperationException("The local factory is unavailable.");
        var player = GameMain.mainPlayer
            ?? throw new InvalidOperationException("The player is unavailable.");
        var item = LDB.items.Select(action.Plan.BuildingItemId)
            ?? throw new InvalidOperationException("The planned building prototype disappeared.");
        var baseline = player.package.GetItemCount(item.ID);
        if (baseline <= 0)
        {
            throw new InvalidOperationException("The planned building item is no longer in inventory.");
        }

        var build = player.controller.actionBuild;
        if (build.active || build.templatePreviews.Count != 0 || build.clickTool.buildPreviews.Count != 0)
        {
            throw new InvalidOperationException("The normal build UI acquired preview state during commit.");
        }

        build.SetFactoryReferences();
        var tool = new SpherewrightClickBuildTool();
        tool._Init(GameMain.data!);
        tool.SetFactoryReferences();
        try
        {
            if (!ReferenceEquals(tool.factory, factory))
            {
                throw new InvalidOperationException("The DSP click-build tool is no longer bound to the local factory.");
            }

            tool.handItem = item;
            tool.handPrefabDesc = item.prefabDesc;
            tool.yaw = action.Plan.BuildYaw;
            if (!tool.SnapshotPlayerInventory())
            {
                throw new InvalidOperationException("The player inventory could not be copied for commit validation.");
            }

            var preview = CreateCorePreview(item, action.Plan.BuildPosition, action.Plan.BuildRotation);
            tool.buildPreviews.Add(preview);
            if (!tool.CheckBuildConditions() || preview.condition != EBuildCondition.Ok)
            {
                throw new InvalidOperationException($"DSP rejected the prepared building with {preview.condition}.");
            }

            tool.CreatePrebuilds();
            if (preview.objId >= 0)
            {
                throw new InvalidOperationException("DSP did not return an ordinary prebuild object ID.");
            }

            if (player.package.GetItemCount(item.ID) != baseline - 1)
            {
                throw new InvalidOperationException("The accepted prebuild did not consume exactly one owned building item.");
            }

            return -preview.objId;
        }
        finally
        {
            tool.buildPreviews.Clear();
            tool.ReleaseSnapshot();
            tool._Free();
        }
    }

    private static BuildPreview CreateCorePreview(ItemProto item, Vector3 position, Quaternion rotation)
    {
        return new BuildPreview
        {
            item = item,
            desc = item.prefabDesc,
            lpos = position,
            lpos2 = position,
            lrot = rotation,
            lrot2 = rotation,
            condition = EBuildCondition.Ok,
            needModel = false,
        };
    }

    private static int FindBuiltEntity(PlanetFactory factory, int itemId, Vector3 position)
    {
        var limit = Math.Min(factory.entityCursor, factory.entityPool.Length);
        for (var entityId = 1; entityId < limit; entityId++)
        {
            ref var entity = ref factory.entityPool[entityId];
            if (entity.id == entityId && entity.protoId == itemId
                && Vector3.Distance(entity.pos, position) <= 0.25f)
            {
                return entityId;
            }
        }

        return 0;
    }

    private static bool CanDeviceRunRecipe(
        PlanetFactory factory,
        int entityId,
        RecipeProto recipe,
        out string reason)
    {
        reason = "The exact built device does not support the requested runtime recipe type.";
        if (entityId <= 0 || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.protoId <= 0)
        {
            return false;
        }

        var item = LDB.items.Select(entity.protoId);
        if (item?.prefabDesc is null)
        {
            return false;
        }

        if (entity.labId > 0 && item.prefabDesc.isLab)
        {
            return recipe.Type == ERecipeType.Research;
        }

        if (entity.assemblerId > 0 && item.prefabDesc.isAssembler)
        {
            return item.prefabDesc.assemblerRecipeType == recipe.Type;
        }

        return false;
    }

    private void ApplyBuildingConfigurationOnMainThread(NormalActionPlanPayload plan)
    {
        var factory = GameMain.localPlanet?.factory
            ?? throw new InvalidOperationException("The local factory is unavailable.");
        ref var entity = ref factory.entityPool[plan.EntityId];
        if (plan.ConfigureMode == BuildingConfigurationModes.Research)
        {
            if (!CanLabEnterResearchMode(factory, plan.EntityId, plan.ConfigureTechId, out var researchReason))
            {
                throw new InvalidOperationException(researchReason);
            }

            ref var lab = ref factory.factorySystem.labPool[entity.labId];
            lab.SetFunction(true, 0, plan.ConfigureTechId, factory.entitySignPool);
            factory.factorySystem.SyncLabFunctions(GameMain.mainPlayer, entity.labId);
            factory.factorySystem.SyncLabForceAccMode(GameMain.mainPlayer, entity.labId);
            return;
        }

        if (plan.ConfigureMode == BuildingConfigurationModes.SorterFilter)
        {
            if (!CanSetSorterFilter(factory, plan.EntityId, plan.ConfigureFilterItemId, out var sorterReason))
            {
                throw new InvalidOperationException(sorterReason);
            }

            ref var inserter = ref factory.factorySystem.inserterPool[entity.inserterId];
            // A full native value-type copy includes private idleTick and typed
            // targets as well as held count/inc, timing, stage, offsets and energy.
            var expectedInserter = inserter;
            expectedInserter.filter = plan.ConfigureFilterItemId;
            var expectedSign = factory.entitySignPool[entity.id];
            expectedSign.iconId0 = (uint)plan.ConfigureFilterItemId;
            expectedSign.iconType = plan.ConfigureFilterItemId > 0 ? 1u : 0u;
            var packageBefore = CapturePlayerPackageState(GameMain.mainPlayer);
            var endpointBefore = _reader.InspectFactoryEntityOnMainThread(plan.SessionId,
                new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.EntityId });
            if (!endpointBefore.Success || endpointBefore.Value is null)
                throw new InvalidOperationException("The sorter endpoint evidence could not be captured before changing its filter.");
            // Endpoint hashes also include the blueprint filter setting.
            endpointBefore.Value.FilterItemId = plan.ConfigureFilterItemId > 0 ? plan.ConfigureFilterItemId : (int?)null;
            var expectedEndpointHash = CanonicalStateHash.FactoryEndpoint(endpointBefore.Value);
            inserter.filter = plan.ConfigureFilterItemId;
            ref var sign = ref factory.entitySignPool[entity.id];
            sign.iconId0 = (uint)plan.ConfigureFilterItemId;
            sign.iconType = plan.ConfigureFilterItemId > 0 ? 1u : 0u;
            var endpointAfter = _reader.InspectFactoryEntityOnMainThread(plan.SessionId,
                new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.EntityId });
            if (!inserter.Equals(expectedInserter) || !sign.Equals(expectedSign)
                || packageBefore != CapturePlayerPackageState(GameMain.mainPlayer)
                || !endpointAfter.Success || endpointAfter.Value is null
                || expectedEndpointHash != endpointAfter.Value.EndpointStateHash)
                throw new InvalidOperationException("Sorter filter assignment did not preserve exact native cargo, stage, targets, topology, sign and player inventory.");
            return;
        }

        if (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge)
        {
            if (!CanConfigureLogisticsStationCharge(
                    factory,
                    plan.EntityId,
                    plan.ConfigureStationMaximumChargePowerWatts,
                    out var maximumChargeEnergyPerTick,
                    out var chargeReason))
            {
                throw new InvalidOperationException(chargeReason);
            }

            var station = factory.transport.GetStationComponent(entity.stationId)
                ?? throw new InvalidOperationException("The logistics station disappeared before charge configuration.");
            ref var consumer = ref factory.powerSystem.consumerPool[station.pcId];
            var beforePackageState = CapturePlayerPackageState(GameMain.mainPlayer);
            var beforeStationStorageState = CaptureLogisticsStationStorageState(station);
            var beforeStationEnergy = station.energy;
            var beforeStationRequestedEnergy = station.energyPerTick;
            var beforeConsumerRequiredEnergy = consumer.requiredEnergy;
            var beforeConsumerIdleEnergy = consumer.idleEnergyPerTick;
            var beforeConsumerNetworkId = consumer.networkId;

            consumer.workEnergyPerTick = maximumChargeEnergyPerTick;

            if (consumer.workEnergyPerTick != maximumChargeEnergyPerTick
                || consumer.id != station.pcId
                || consumer.entityId != plan.EntityId
                || consumer.networkId != beforeConsumerNetworkId
                || consumer.requiredEnergy != beforeConsumerRequiredEnergy
                || consumer.idleEnergyPerTick != beforeConsumerIdleEnergy
                || station.energy != beforeStationEnergy
                || station.energyPerTick != beforeStationRequestedEnergy
                || !string.Equals(beforeStationStorageState, CaptureLogisticsStationStorageState(station), StringComparison.Ordinal)
                || !string.Equals(beforePackageState, CapturePlayerPackageState(GameMain.mainPlayer), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The station maximum-charge assignment did not preserve and prove the exact power-consumer, station inventory, and player inventory state.");
            }

            return;
        }

        if (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationStorage)
        {
            if (!TryParseLogisticsStorageLogic(plan.ConfigureStationLocalLogic, out var localLogic)
                || !TryParseLogisticsStorageLogic(plan.ConfigureStationRemoteLogic, out var remoteLogic))
            {
                throw new InvalidOperationException("The logistics-station storage logic is invalid.");
            }

            if (!CanConfigureLogisticsStationStorage(
                    factory,
                    plan.EntityId,
                    plan.ConfigureStationStorageIndex,
                    plan.ConfigureStationItemId,
                    plan.ConfigureStationMaximumCount,
                    localLogic,
                    remoteLogic,
                    out var stationReason))
            {
                throw new InvalidOperationException(stationReason);
            }

            var station = factory.transport.GetStationComponent(entity.stationId)
                ?? throw new InvalidOperationException("The logistics station disappeared before configuration.");
            var beforeSlot = station.storage[plan.ConfigureStationStorageIndex];
            var beforeInventoryState = CapturePlayerPackageState(GameMain.mainPlayer);
            factory.transport.SetStationStorage(
                station.id,
                plan.ConfigureStationStorageIndex,
                plan.ConfigureStationItemId,
                plan.ConfigureStationMaximumCount,
                localLogic,
                remoteLogic,
                GameMain.mainPlayer);
            var afterSlot = station.storage[plan.ConfigureStationStorageIndex];
            var afterInventoryState = CapturePlayerPackageState(GameMain.mainPlayer);
            if (afterSlot.itemId != plan.ConfigureStationItemId
                || afterSlot.max != plan.ConfigureStationMaximumCount
                || afterSlot.localLogic != localLogic
                || afterSlot.remoteLogic != remoteLogic
                || afterSlot.count != beforeSlot.count
                || afterSlot.inc != beforeSlot.inc
                || !string.Equals(beforeInventoryState, afterInventoryState, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The station configuration call did not preserve and prove the exact slot and player inventory state.");
            }

            return;
        }

        if (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt)
        {
            if (!CanConfigureLogisticsStationBelt(
                    factory,
                    plan.EntityId,
                    plan.ConfigureStationBeltSlotIndex,
                    plan.ConfigureStationBeltStorageIndex,
                    out var stationBeltItemId,
                    out var stationBeltReason)
                || stationBeltItemId != plan.ConfigureStationBeltItemId)
            {
                throw new InvalidOperationException(stationBeltReason);
            }

            var station = factory.transport.GetStationComponent(entity.stationId)
                ?? throw new InvalidOperationException("The logistics station disappeared before output-selector configuration.");
            ref var beforeSlot = ref station.slots[plan.ConfigureStationBeltSlotIndex];
            var beforeDirection = beforeSlot.dir;
            var beforeBeltId = beforeSlot.beltId;
            var beforeCounter = beforeSlot.counter;
            var beforeStationStorageState = CaptureLogisticsStationStorageState(station);
            var beforeBeltInvariantState = CaptureLogisticsStationBeltSelectorInvariantState(
                station,
                plan.ConfigureStationBeltSlotIndex);
            var beforePackageState = CapturePlayerPackageState(GameMain.mainPlayer);

            station.slots[plan.ConfigureStationBeltSlotIndex].storageIdx =
                plan.ConfigureStationBeltStorageIndex + 1;

            ref var afterSlot = ref station.slots[plan.ConfigureStationBeltSlotIndex];
            if (afterSlot.dir != beforeDirection
                || afterSlot.dir != IODir.Output
                || afterSlot.beltId != beforeBeltId
                || afterSlot.counter != beforeCounter
                || afterSlot.storageIdx != plan.ConfigureStationBeltStorageIndex + 1
                || !string.Equals(beforeStationStorageState, CaptureLogisticsStationStorageState(station), StringComparison.Ordinal)
                || !string.Equals(
                    beforeBeltInvariantState,
                    CaptureLogisticsStationBeltSelectorInvariantState(station, plan.ConfigureStationBeltSlotIndex),
                    StringComparison.Ordinal)
                || !string.Equals(beforePackageState, CapturePlayerPackageState(GameMain.mainPlayer), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The station output-selector assignment did not preserve and prove port topology, station inventory, and player inventory.");
            }

            return;
        }

        var recipe = LDB.recipes.Select(plan.ConfigureRecipeId)
            ?? throw new InvalidOperationException("The configured recipe disappeared.");
        if (!CanDeviceRunRecipe(factory, plan.EntityId, recipe, out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        if (entity.labId > 0)
        {
            ref var lab = ref factory.factorySystem.labPool[entity.labId];
            lab.SetFunction(false, recipe.ID, 0, factory.entitySignPool);
            factory.factorySystem.SyncLabFunctions(GameMain.mainPlayer, entity.labId);
            factory.factorySystem.SyncLabForceAccMode(GameMain.mainPlayer, entity.labId);
            return;
        }

        ref var assembler = ref factory.factorySystem.assemblerPool[entity.assemblerId];
        assembler.SetRecipe(recipe.ID, factory.entitySignPool);
        var execute = assembler.recipeExecuteData;
        GameMain.gameScenario?.NotifyOnAssemblerRecipePick(
            factory.index,
            assembler.id,
            assembler.recipeId,
            execute?.requires,
            execute?.requireCounts,
            execute?.products,
            execute?.productCounts);
    }

    private static int GetCount(IReadOnlyDictionary<int, int> inventory, int itemId)
    {
        return inventory.TryGetValue(itemId, out var count) ? count : 0;
    }

    private static void AddRecipeBudget(PreparedNormalAction result, RecipeProto recipe, int count)
    {
        for (var index = 0; index < Math.Min(recipe.Items.Length, recipe.ItemCounts.Length); index++)
        {
            result.ItemBudget.Add(new ActionItemBudget
            {
                ItemId = recipe.Items[index],
                Name = LDB.items.Select(recipe.Items[index])?.name ?? string.Empty,
                Count = recipe.ItemCounts[index] * count,
                Direction = "input",
            });
        }

        for (var index = 0; index < Math.Min(recipe.Results.Length, recipe.ResultCounts.Length); index++)
        {
            result.ItemBudget.Add(new ActionItemBudget
            {
                ItemId = recipe.Results[index],
                Name = LDB.items.Select(recipe.Results[index])?.name ?? string.Empty,
                Count = recipe.ResultCounts[index] * count,
                Direction = "output",
            });
        }
    }

    private static Vector3 CalculateMiningApproach(Vector3 playerPosition, Vector3 objectPosition)
    {
        var offsetDirection = objectPosition - playerPosition;
        if (offsetDirection.sqrMagnitude < 0.01f)
        {
            return objectPosition;
        }

        var target = objectPosition - offsetDirection.normalized * 1.2f;
        return target.normalized * objectPosition.magnitude;
    }

    private static long EstimateHarvestTicks(ResourceNodeSnapshot resource, int count)
    {
        if (resource.Kind == ResourceNodeKinds.Vein)
        {
            var proto = LDB.veins.Select(resource.ProtoId);
            return proto is null ? 3600L : (long)proto.MiningTime * count;
        }

        var vegetation = LDB.veges.Select(resource.ProtoId);
        return vegetation is null ? 3600L : vegetation.MiningTime;
    }

    private static Vector3 ToVector(Vector3Snapshot snapshot) => new Vector3(snapshot.X, snapshot.Y, snapshot.Z);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static WriteBlocker CloneBlocker(WriteBlocker blocker) =>
        new WriteBlocker { Code = blocker.Code, Message = blocker.Message };

    private static BridgeError Stale(string message) => BridgeError.Create(
        BridgeErrorCodes.StaleState,
        message,
        true,
        "Inspect current state and prepare a fresh action.");

    private static GameCallResult<PreparedNormalAction> InvalidPlan(string message) =>
        GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
            BridgeErrorCodes.InvalidRequest,
            message,
            false,
            "Correct the request using current structured observations."));

    private static GameCallResult<PreparedNormalAction> StalePlan(string message) =>
        GameCallResult<PreparedNormalAction>.Failed(Stale(message));

    private static GameCallResult<PreparedNormalAction> NotReadyPlan(string message) =>
        GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
            BridgeErrorCodes.BridgeNotReady,
            message,
            true,
            "Wait for the owned world and player systems to finish loading, then retry."));

    private static GameCallResult<T> MissingPlan<T>(bool expired) =>
        GameCallResult<T>.Failed(BridgeError.Create(
            expired ? BridgeErrorCodes.PlanExpired : BridgeErrorCodes.PlanNotFound,
            expired ? "The normal-game plan expired." : "The normal-game plan was not found or was already accepted.",
            true,
            "Inspect current state, prepare a fresh plan, and commit it once."));

    private sealed class CommonPrepareResult
    {
        private CommonPrepareResult(SessionState? session, BridgeError? error)
        {
            Session = session;
            Error = error;
        }

        public SessionState? Session { get; }

        public BridgeError? Error { get; }

        public static CommonPrepareResult Succeeded(SessionState session) => new CommonPrepareResult(session, null);

        public static CommonPrepareResult Failed(BridgeError error) => new CommonPrepareResult(null, error);
    }

    private sealed partial class ActionRecord
    {
        public string ActionId { get; set; } = string.Empty;
        public string ActionKind { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public int PlanetId { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public bool Terminal { get; set; }
        public bool Succeeded { get; set; }
        public long StartedAtGameTick { get; set; }
        public long? CompletedAtGameTick { get; set; }
        public string BeforeStateHash { get; set; } = string.Empty;
        public string? AfterStateHash { get; set; }
        public int? TargetObjectId { get; set; }
        public List<int> TargetObjectIds { get; set; } = new List<int>();
        public int? TargetItemId { get; set; }
        public int? RequestedCount { get; set; }
        public int? BeforeTargetAmount { get; set; }
        public int? AfterTargetAmount { get; set; }
        public UpgradeReadback? UpgradeReadback { get; set; }
        public ResearchQueueReadback? ResearchQueueReadback { get; set; }
        public StorageConfigurationReadback? StorageConfigurationReadback { get; set; }
        public MechaWarperSupplyReadback? MechaWarperSupplyReadback { get; set; }
        public Spherewright.Bridge.Core.Factory.BlueprintBuildState? BlueprintBuild { get; set; }
        public long LastBlueprintPollTick { get; set; }
        public string? Message { get; set; }
        public string? OriginalOutcomeMessage { get; set; }
        public bool ReconciledFromOutcomeUnknown { get; set; }
        public long? ReconciledAtGameTick { get; set; }
        public Dictionary<int, int> BeforeInventory { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int>? AfterInventory { get; set; }
        public int[] ExpectedYieldItemIds { get; set; } = Array.Empty<int>();
        public ForgeTask? ForgeTask { get; set; }
        public OrderNode? PlayerOrder { get; set; }
        public long? PowerStarvedAtGameTick { get; set; }
        public MovementProgressWatchdog? MovementProgress { get; set; }
        public Vector3? HarvestApproach { get; set; }
        public HarvestApproachProgressWatchdog? HarvestApproachProgress { get; set; }
        public long FlightLastControlGameTick { get; set; }
        public double FlightBestDistance { get; set; }
        public long FlightBestDistanceAtGameTick { get; set; }
        public long FlightDestinationContactAtGameTick { get; set; }
        public long FlightStableLandingAtGameTick { get; set; }
        public int FlightLandingOrderCount { get; set; }
        public long? FlightLandingOrderReachedAtGameTick { get; set; }
        public bool FlightAscentInputOwned { get; set; }
        public float FlightOriginalVerticalInput { get; set; }
        public float FlightOriginalForwardInput { get; set; }
        public string? FlightCheckpointId { get; set; }
        public string? FlightCheckpointReloadToken { get; set; }
        public long? FlightCheckpointGameTick { get; set; }

        public bool Stalled { get; set; }

        public bool RecoveryRequired { get; set; }
        public string? FailureKind { get; set; }
        public long? StalledGameTicks { get; set; }
        public double? RemainingDistance { get; set; }
        public bool DoNotRetrySameTarget { get; set; }
        public string? RecommendedRecovery { get; set; }
        public double? RecommendedShortMoveDistanceMeters { get; set; }
        public double? OrthogonalProbeDistanceMeters { get; set; }
        public int? MaximumOrthogonalProbeAttempts { get; set; }
        public List<int> PrebuildIds { get; set; } = new List<int>();
        public List<BuildExpectedEntity> ExpectedBuildEntities { get; set; } = new List<BuildExpectedEntity>();
        public HashSet<int> PreexistingBuildEntityIds { get; } = new HashSet<int>();
        public NormalActionPlanPayload Plan { get; set; } = null!;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public string ActionKind { get; private set; } = string.Empty;
        public string SessionId { get; private set; } = string.Empty;
        public int PlanetId { get; private set; }
        public string ExpectedStateHash { get; private set; } = string.Empty;
        public string PlayerStateHash { get; private set; } = string.Empty;
        public PassiveDriftMoveInspection? PassiveDriftInspection { get; private set; }
        public string ResourceStateHash { get; private set; } = string.Empty;
        public string ProgressionSelectionStateHash { get; private set; } = string.Empty;
        public string StarSystemStateHash { get; private set; } = string.Empty;
        public Vector3 TargetPosition { get; private set; }
        public float ArrivalTolerance { get; private set; }
        public double EstimatedDistance { get; private set; }
        public int DestinationPlanetId { get; private set; }
        public double MinimumCoreEnergyRatio { get; private set; }
        public double RequiredFlightEnergy { get; private set; }
        public long EstimatedTicks { get; set; }
        public string ResourceKind { get; private set; } = string.Empty;
        public int ResourceNodeId { get; private set; }
        public int ResourceRemaining { get; private set; }
        public List<int> YieldItemIds { get; } = new List<int>();
        public int RecipeId { get; private set; }
        public int TechId { get; private set; }
        public int[]? ResearchQueueOrder { get; private set; }
        public int BuildingItemId { get; private set; }
        public string BuildKind { get; private set; } = string.Empty;
        public string BuildResourceStateHash { get; private set; } = string.Empty;
        public int BuildResourceNodeId { get; private set; }
        public string SourceFactoryStateHash { get; private set; } = string.Empty;
        public string DestinationFactoryStateHash { get; private set; } = string.Empty;
        public int SourceObjectId { get; private set; }
        public int DestinationObjectId { get; private set; }
        public int SourceSlot { get; private set; } = -1;
        public int DestinationSlot { get; private set; } = -1;
        public List<BuildStepPlan> BuildSteps { get; } = new List<BuildStepPlan>();
        public Vector3 BuildPosition { get; private set; }
        public Quaternion BuildRotation { get; private set; }
        public float BuildYaw { get; private set; }
        public string FactoryStateHash { get; private set; } = string.Empty;
        public int EntityId { get; private set; }
        public int ConfigureRecipeId { get; private set; }
        public string ConfigureMode { get; private set; } = BuildingConfigurationModes.Production;
        public int ConfigureTechId { get; private set; }
        public int ConfigureFilterItemId { get; private set; }
        public string StationConfigurationStateHash { get; private set; } = string.Empty;
        public int ConfigureStationStorageIndex { get; private set; } = -1;
        public int ConfigureStationBeltSlotIndex { get; private set; } = -1;
        public int ConfigureStationBeltStorageIndex { get; private set; } = -1;
        public int ConfigureStationBeltItemId { get; private set; }
        public int ConfigureStationItemId { get; private set; }
        public int ConfigureStationMaximumCount { get; private set; }
        public string ConfigureStationLocalLogic { get; private set; } = LogisticsStorageLogics.None;
        public string ConfigureStationRemoteLogic { get; private set; } = LogisticsStorageLogics.None;
        public long ConfigureStationMaximumChargePowerWatts { get; private set; }
        public string TransferDirection { get; private set; } = string.Empty;
        public int TransferStorageEntityId { get; private set; }
        public int TransferItemId { get; private set; }
        public string TransferStorageStateHash { get; private set; } = string.Empty;
        public string StationFleetStateHash { get; private set; } = string.Empty;
        public string FleetTransferDirection { get; private set; } = string.Empty;
        public int FleetTransferStationEntityId { get; private set; }
        public int FleetTransferItemId { get; private set; }
        public int Count { get; private set; }

        public static NormalActionPlanPayload Move(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            Vector3 target,
            float tolerance,
            double distance,
            PassiveDriftMoveInspection? passiveDriftInspection = null) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Move,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                PassiveDriftInspection = passiveDriftInspection,
                TargetPosition = target,
                ArrivalTolerance = tolerance,
                EstimatedDistance = distance,
            };

        public static NormalActionPlanPayload InterplanetaryFlight(
            string sessionId,
            int planetId,
            int destinationPlanetId,
            string expectedStateHash,
            string playerStateHash,
            string starSystemStateHash,
            double distance,
            double minimumCoreEnergyRatio,
            double requiredFlightEnergy) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.InterplanetaryFlight,
                SessionId = sessionId,
                PlanetId = planetId,
                DestinationPlanetId = destinationPlanetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                StarSystemStateHash = starSystemStateHash,
                EstimatedDistance = distance,
                MinimumCoreEnergyRatio = minimumCoreEnergyRatio,
                RequiredFlightEnergy = requiredFlightEnergy,
            };

        public static NormalActionPlanPayload Harvest(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            ResourceNodeSnapshot resource,
            int count)
        {
            var result = new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Harvest,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                ResourceStateHash = resource.StateHash,
                TargetPosition = ToVector(resource.Position),
                EstimatedDistance = resource.DistanceFromPlayer,
                ResourceKind = resource.Kind,
                ResourceNodeId = resource.NodeId,
                ResourceRemaining = resource.RemainingAmount,
                Count = count,
            };
            result.YieldItemIds.AddRange(resource.Yields.Select(yield => yield.ItemId));
            return result;
        }

        public static NormalActionPlanPayload Handcraft(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            int recipeId,
            int count) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Handcraft,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                RecipeId = recipeId,
                Count = count,
            };

        public static NormalActionPlanPayload Research(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string progressionSelectionStateHash,
            int techId,
            int[]? researchQueueOrder = null) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.SelectResearch,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                ProgressionSelectionStateHash = progressionSelectionStateHash,
                TechId = techId,
                ResearchQueueOrder = researchQueueOrder?.ToArray(),
            };

        public static NormalActionPlanPayload Build(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            int buildingItemId,
            Vector3 position,
            Quaternion rotation,
            float yaw) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Build,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                BuildingItemId = buildingItemId,
                BuildPosition = position,
                BuildRotation = rotation,
                BuildYaw = yaw,
            };

        public static NormalActionPlanPayload Configure(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string factoryStateHash,
            int entityId,
            int recipeId,
            string mode,
            int techId,
            int filterItemId,
            string stationConfigurationStateHash,
            int stationStorageIndex,
            int stationBeltSlotIndex,
            int stationBeltStorageIndex,
            int stationBeltItemId,
            int stationItemId,
            int stationMaximumCount,
            string stationLocalLogic,
            string stationRemoteLogic,
            long stationMaximumChargePowerWatts) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.ConfigureBuilding,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                FactoryStateHash = factoryStateHash,
                EntityId = entityId,
                ConfigureRecipeId = recipeId,
                ConfigureMode = mode,
                ConfigureTechId = techId,
                ConfigureFilterItemId = filterItemId,
                StationConfigurationStateHash = stationConfigurationStateHash,
                ConfigureStationStorageIndex = stationStorageIndex,
                ConfigureStationBeltSlotIndex = stationBeltSlotIndex,
                ConfigureStationBeltStorageIndex = stationBeltStorageIndex,
                ConfigureStationBeltItemId = stationBeltItemId,
                ConfigureStationItemId = stationItemId,
                ConfigureStationMaximumCount = stationMaximumCount,
                ConfigureStationLocalLogic = stationLocalLogic,
                ConfigureStationRemoteLogic = stationRemoteLogic,
                ConfigureStationMaximumChargePowerWatts = stationMaximumChargePowerWatts,
            };
    }
}
