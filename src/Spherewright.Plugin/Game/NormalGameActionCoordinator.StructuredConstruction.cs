using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private const long StructuredConstructionWorkTicks = 3600;
    private const int StructuredConstructionLeaseSeconds = 30;

    private static bool IsStructuredConstruction(NormalActionPlanPayload plan) =>
        plan.BlueprintState?.StructuredConstruction is not null;

    // The existing native structured prepare is the sole source of poses,
    // miner parameters and connections. Generic blueprint syntax is unchanged.
    private BridgeError? BindRemoteStructuredConstruction(NormalActionPlanPayload plan, SessionState session)
    {
        var journey = _flightCheckpoints.VoyagerJourney;
        if (journey is null || IsVoyagerJourneyClosed(journey)) return null;
        if (!VoyagerOwnedJourneyMatchesCurrentSession(journey, plan.SessionId))
            return Stale("A structured batch cannot use another exact owned save's or Native version's journey.");
        if (journey.Phase != "remote_landed" || journey.RemotePlanetId != plan.PlanetId
            || !StructuredConstructionBinding.SupportsItem(plan.BuildingItemId)
            || plan.BuildSteps.Count < 1 || plan.BuildSteps.Count > 8
            || plan.BuildSteps.Any(s => s.SourceBeltAnchor is not null || s.DestinationBeltAnchor is not null
                || s.BeltPathMode == BeltPathModes.NativeElevatedGrid))
            return Stale("Remote construction admits only an original-journey native-validated batch of 1..8 supported new objects, without elevated paths or belt-cover reuse.");
        if (!_blueprints.TryRead(out var old) || old.Count >= 32
            || old.Any(b => b.StructuredConstruction is not null && b.Phase != "completed"))
            return BlueprintError("Read and fresh-resume the exact existing structured build; do not submit a replacement batch.");

        var binding = new StructuredConstructionBinding
        {
            Kind = plan.BuildKind, JourneyId = journey.JourneyId, NativePlanHash = plan.ExpectedStateHash,
            ResourceNodeId = plan.BuildResourceNodeId, ResourceStateHash = plan.BuildResourceStateHash,
            SourceObjectId = plan.SourceObjectId, DestinationObjectId = plan.DestinationObjectId,
            Steps = plan.BuildSteps.Select(s => new StructuredConstructionConnection
            {
                InputStepIndex = s.InputStepIndex, OutputStepIndex = s.OutputStepIndex,
                InputObjectId = s.InputObjectId, OutputObjectId = s.OutputObjectId,
                InputFromSlot = s.InputFromSlot, InputToSlot = s.InputToSlot,
                OutputFromSlot = s.OutputFromSlot, OutputToSlot = s.OutputToSlot,
                Yaw = s.Yaw, IsConnectionNode = s.IsConnectionNode,
            }).ToList(),
        };
        foreach (var id in new[] { plan.SourceObjectId, plan.DestinationObjectId }.Where(id => id > 0).Distinct())
        {
            var snapshot = _reader.InspectFactoryEntityOnMainThread(plan.SessionId,
                new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = id });
            if (!snapshot.Success || snapshot.Value is null) return snapshot.Error ?? BlueprintError("structured_endpoint_unavailable");
            var value = snapshot.Value;
            binding.Endpoints.Add(new StructuredConstructionEndpoint
            {
                ObjectId = id, ItemId = value.ItemId, Position = value.Position, Rotation = value.Rotation,
                ExistingConnections = value.Connections,
            });
        }
        var site = new BlueprintSiteSnapshot
        {
            SessionId = plan.SessionId, PlanetId = plan.PlanetId, Revision = session.Revision,
            CapturedAtGameTick = GameMain.gameTick, BlueprintHash = plan.ExpectedStateHash,
            AssessmentHash = plan.ExpectedStateHash, Position = ConstructionVector(GameMain.mainPlayer.position),
            NativeCheckPerformed = true, NativeCheckPassed = true, InventorySufficient = true, TechnologySatisfied = true,
            NativeBlueprintObjectLimit = 8,
            Objects = plan.BuildSteps.Select((s, index) => new BlueprintSiteObject
            {
                Index = index, ItemId = s.ItemId, FilterItemId = s.FilterItemId,
                Position = ConstructionVector(s.Position), Position2 = ConstructionVector(s.Position2),
                Rotation = ConstructionRotation(s.Rotation), Rotation2 = ConstructionRotation(s.Rotation2),
                Tilt = s.Tilt, InputOffset = s.InputOffset, OutputOffset = s.OutputOffset,
                Parameters = s.Parameters.ToArray(), NativeCondition = "Ok",
            }).ToList(),
            ConstructionItems = new List<FoundryInventoryBudget> { new()
                { ItemId = plan.BuildingItemId, RequiredCount = plan.BuildSteps.Count,
                    PackageCount = GameMain.mainPlayer.package.GetItemCount(plan.BuildingItemId) } },
        };
        plan.BlueprintState = BlueprintBuildState.Create(site, structuredConstruction: binding);
        return ValidateStructuredConstructionBudget(plan);
    }

    private BridgeError? ValidateStructuredConstructionBudget(NormalActionPlanPayload plan)
    {
        if (!VoyagerRemoteIdleBindingMatches())
            return Stale("Structured remote work requires this live landed-controller idle binding; a remote Host/save reload is not silently recertified.");
        var build = plan.BlueprintState!;
        var journey = _flightCheckpoints.VoyagerJourney;
        var player = GameMain.mainPlayer;
        var factory = GameMain.localPlanet?.factory;
        plan.VoyagerLocalWalkScene = CaptureNativeLocalWalkScene();
        if (journey is null || journey.JourneyId != build.StructuredConstruction!.JourneyId
            || !VoyagerOwnedJourneyMatchesCurrentSession(journey, plan.SessionId)
            || journey.Phase != "remote_landed" || journey.RemotePlanetId != plan.PlanetId
            || !_sessions.IsCurrentSessionOwned || _sessions.SessionId != plan.SessionId
            || factory is null || plan.VoyagerLocalWalkScene is null || !ReferenceEquals(factory, GameMain.data.localLoadedPlanetFactory)
            || factory.planetId != plan.PlanetId || player?.mecha?.constructionModule is null
            || _actions.ActiveValues.Any(a => !a.Terminal)
            || !VoyagerNativeTickAvailable || DateTimeOffset.UtcNow - VoyagerLastNativeTickObservedAtUtc > TimeSpan.FromSeconds(1)
            || !VoyagerLocalPrePlayerTickAvailable || DateTimeOffset.UtcNow - VoyagerLocalPrePlayerObservedAtUtc > TimeSpan.FromSeconds(1)
            || !ConstructionPlayerIdle(player) || !TryObserveOwnedConstructionDrones(factory, player.mecha.constructionModule, out var idle, out _)
            || !idle || !ConstructionFactoryHasOnlyOwnedPending(factory, build))
            return Stale("Structured construction requires the exact idle owned remote player/factory, original journey and a fresh native before-construction tick hook.");
        var module = player.mecha.constructionModule;
        if (build.ConstructionControl is { } retainedControl
            && !retainedControl.NativeDrainBarrierPassed(GameMain.gameTick, ConstructionOwnPrelaunchCacheClear(factory, build)))
            return Stale("The original disabled dispatch has not proved a later native factory pass and a drained owned prelaunch cache.");
        if (!module.droneEnabled && !(build.ConstructionControl?.DispatchDisabled == true
            && build.ConstructionControl.OriginalDroneEnabled))
            return Stale("The ordinary construction drones were disabled outside this build's retained stop receipt.");
        var reserve = CaptureConstructionReserve(build, player, out var costHash);
        var workTicks = build.ConstructionControl is null ? StructuredConstructionWorkTicks
            : build.ConstructionControl.RemainingWorkGameTicks(GameMain.gameTick, DateTimeOffset.UtcNow).GetValueOrDefault();
        if (workTicks < 1) return Stale("The original structured batch's absolute work window expired; fresh resume cannot renew it or recreate its pending objects.");
        if (!reserve.Proved || !NativeConstructionDroneReservePolicy.CanFundNativeTail(player.mecha.coreEnergy,
            CalculateAvailableFlightEnergy(player.mecha), journey.ReturnEnergyFloorJoules, reserve.TailAndNextTickEnergyJoules))
            return Stale("The ordinary drone tail lacks finite native geometry/core-energy proof (" + reserve.BlockerCode + ").");
        var budget = VoyagerLocalActionBudgetPolicy.Evaluate(new VoyagerLocalActionBudgetFacts
        {
            ExactRemoteBinding = true, NativePlayerIdle = true,
            CurrentGameTick = GameMain.gameTick, LastLandedGameTick = journey.LastLandedGameTick,
            SpentGameTicks = journey.SpentGameTicks, MaximumGameTicks = journey.MaximumGameTicks,
            ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
            WorkGameTicks = workTicks, TailGameTicks = reserve.TailGameTicks,
            NowUtc = DateTimeOffset.UtcNow, JourneyDeadlineUtc = _sessions.EffectiveVoyagerDeadline(journey),
            AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(player.mecha),
            LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules,
            WorkPowerUpperWatts = reserve.PowerUpperWatts,
            TailAndNextTickEnergyJoules = reserve.TailAndNextTickEnergyJoules,
            NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage), OriginalExtraWarperReserve = journey.ExtraWarperReserve,
        });
        if (!budget.Proved) return Stale("The original return time, loaded floor or native 1+extra warpers cannot cover work and its finite drone tail (" + budget.BlockerCode + ").");
        plan.ConstructionReserve = reserve;
        plan.ConstructionNativeCostHash = costHash;
        plan.VoyagerLocalJourneyId = journey.JourneyId;
        plan.VoyagerLocalBinding = new VoyagerNativeBinding(GameMain.data, player);
        plan.VoyagerLocalFactory = factory;
        return null;
    }

    private GameCallResult<PreparedNormalAction> PrepareStructuredConstructionResume(
        PrepareBlueprintBuildRequest request, SessionState session, BlueprintBuildState build, PlayerStateSnapshot player)
    {
        var generation = build.Generation;
        var blockers = ReconcileStructuredConstructionState(build);
        if (generation != build.Generation)
        {
            if (!_blueprints.TryPut(build)) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("structured_progress_persistence_failed"));
            return StalePlan("Native construction progress advanced. Read the same buildId and fresh-prepare its retained pending objects.");
        }
        if (blockers.Count != 0 || build.Phase == "outcome_unknown"
            || build.Objects.Any(o => o.State != BlueprintObjectStates.PendingConstruction && o.State != BlueprintObjectStates.Completed))
            return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("Only proven pending structured objects can resume; reconcile the original action. " + string.Join(";", blockers)));
        if (build.Objects.All(o => o.State == BlueprintObjectStates.Completed)) return InvalidPlan("This exact batch is already built; never create it again.");
        if (build.ProgressHash(session.SessionId!, session.Revision) != request.ExpectedStateHash)
            return StalePlan("The exact retained structured progress changed after inspection.");
        var plan = NormalActionPlanPayload.Blueprint(session.SessionId!, request.PlanetId, session.Revision,
            player.StateHash, build, request.ExpectedStateHash, build.Objects.Count, null, null, false);
        var error = ValidateStructuredConstructionBudget(plan);
        if (error is not null) return GameCallResult<PreparedNormalAction>.Failed(error);
        var prepared = AddPreparedPlan(plan, session, StructuredConstructionWorkTicks,
            "Resume only this exact persisted native structured build. No Native Create call is repeated. Fresh native drone control is finite and must drain to physical idle before terminal success or safe pause.");
        if (prepared.Value is not null) prepared.Value.BlueprintBuild = BlueprintSnapshot(build, session, new List<string>());
        return prepared;
    }

    private BridgeError? RevalidateStructuredConstructionResume(NormalActionPlanPayload plan)
    {
        if (!_blueprints.TryRead(out var builds)) return BlueprintError("structured_progress_store_unavailable");
        var build = builds.SingleOrDefault(b => b.BuildId == plan.BlueprintState!.BuildId);
        if (build is null || build.PlanHash != plan.BlueprintState!.PlanHash) return Stale("The original structured build identity changed.");
        var blockers = ReconcileStructuredConstructionState(build);
        if (blockers.Count != 0 || build.Phase == "outcome_unknown"
            || build.Objects.Any(o => o.State != BlueprintObjectStates.PendingConstruction && o.State != BlueprintObjectStates.Completed)
            || build.Objects.All(o => o.State == BlueprintObjectStates.Completed)
            || build.ProgressHash(plan.SessionId, _sessions.CaptureOnMainThread().Revision) != plan.BlueprintInputHash)
            return Stale("The original pending structured batch must be freshly inspected and cannot be recreated.");
        var player = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (!player.Success || player.Value?.StateHash != plan.PlayerStateHash) return player.Error ?? Stale("The resume player changed after preparation.");
        plan.BlueprintState = build;
        return ValidateStructuredConstructionBudget(plan);
    }

    private void StartStructuredConstructionOnMainThread(ActionRecord action)
    {
        var build = CloneBlueprintState(action.Plan.BlueprintState!);
        var player = GameMain.mainPlayer;
        var factory = GameMain.data.localLoadedPlanetFactory;
        if (!ConstructionPlayerIdle(player) || !NativeLocalWalkSceneProved(player, action.Plan.VoyagerLocalWalkScene,
                finiteWaterCostWithNativePause: true))
        { Fail(action, "The exact Native stationary/pending-jump condition changed before structured start; no create occurred."); return; }
        var reserve = action.Plan.ConstructionReserve!;
        var oldControl = build.ConstructionControl;
        var nowUtc = DateTimeOffset.UtcNow;
        var submit = build.Objects.All(o => o.State == BlueprintObjectStates.NotSubmitted);
        action.BlueprintBuild = build;
        action.ConstructionBinding = new StructuredNativeBinding(GameMain.data, player, factory,
            _sessions.OwnedSaveName!, _sessions.CaptureOnMainThread().GameVersion);
        action.ConstructionLastNativeTick = GameMain.gameTick;
        build.ConstructionControl = new StructuredConstructionControlState
        {
            Anchor = ConstructionVector(player.position), NativeCostHash = action.Plan.ConstructionNativeCostHash!,
            StartedAtGameTick = oldControl?.StartedAtGameTick ?? GameMain.gameTick,
            StartedAtUtc = oldControl?.StartedAtUtc ?? nowUtc,
            WorkDeadlineGameTick = oldControl?.WorkDeadlineGameTick ?? GameMain.gameTick + StructuredConstructionWorkTicks,
            WorkDeadlineUtc = oldControl?.WorkDeadlineUtc ?? nowUtc.AddSeconds(StructuredConstructionWorkTicks / 60d),
            ObservationLeaseExpiresAtUtc = nowUtc.AddSeconds(StructuredConstructionLeaseSeconds),
            TailGameTicks = reserve.TailGameTicks, TailAndNextTickEnergyJoules = reserve.TailAndNextTickEnergyJoules,
            NativePowerUpperWatts = reserve.PowerUpperWatts, OriginalDroneEnabled = oldControl?.OriginalDroneEnabled ?? player.mecha.constructionModule.droneEnabled,
            NativeCreateActionId = oldControl?.NativeCreateActionId,
            PreexistingEntityIds = oldControl?.PreexistingEntityIds.ToList() ?? CaptureConstructionExistingEntities(factory, build),
        };
        build.Begin(action.SessionId, action.ActionId, build.Objects.Count, GameMain.gameTick);
        if (!_blueprints.TryPut(build)) { Fail(action, "The finite structured lease could not persist; no native create or drone toggle occurred."); return; }
        action.PlayerOrder = OrderNode.MoveTo(player.position);
        player.Order(action.PlayerOrder, false);
        action.State = NormalActionStates.WaitingForGame;
        try
        {
            if (!player.mecha.constructionModule.droneEnabled)
                player.mecha.constructionModule.ToggleConstructionDrones(factory, player);
            build.ConstructionControl.Phase = "working";
            if (submit)
            {
                build.ConstructionControl.NativeCreateActionId = action.ActionId;
                var before = CaptureInventory(player);
                build.BeforeStructuredSubmission(GameMain.gameTick, GetCount(before, action.Plan.BuildingItemId), BlueprintInventoryHash(before));
                if (!_blueprints.TryPut(build)) throw new InvalidOperationException("structured_write_ahead_persistence_failed");
                try { CreatePreparedPrebuildsOnMainThread(action); }
                finally { TryRecordStructuredSubmission(action, before); }
                if (build.Objects.Any(o => o.State != BlueprintObjectStates.PendingConstruction))
                    throw new InvalidOperationException("structured_native_batch_outcome_unproved");
            }
            if (!_blueprints.TryPut(build)) throw new InvalidOperationException("structured_submission_persistence_failed");
            action.Message = submit
                ? "One ordinary native structured batch was submitted durably; its original finite drone lease owns construction and physical drain."
                : "Only the exact persisted pending batch resumed under a fresh finite drone lease; Native Create was not called again.";
            action.ConstructionReserveProofValid = true;
        }
        catch (Exception exception)
        {
            RequestStructuredConstructionStop(action, "structured_start_exception:" + exception.GetType().Name,
                unknown: build.Objects.Any(o => o.State == BlueprintObjectStates.Submitting), bindingLost: false);
        }
    }

    private void TryRecordStructuredSubmission(ActionRecord action, Dictionary<int, int> before)
    {
        var build = action.BlueprintBuild!;
        var factory = action.ConstructionBinding!.Factory;
        var after = CaptureInventory(action.ConstructionBinding.Player);
        action.AfterInventory = after;
        if (!ConstructionBatchDebit(before, after, action.Plan.BuildingItemId, build.Objects.Count)) return;
        var ordered = new List<int>();
        foreach (var spec in build.Site.Objects)
        {
            var matches = action.PrebuildIds.Where(id => BlueprintPrebuildMatches(factory, id, spec)).ToArray();
            if (matches.Length != 1 || ordered.Contains(matches[0])) return;
            ordered.Add(matches[0]);
        }
        build.ConfirmStructuredSubmission(ordered, GetCount(after, action.Plan.BuildingItemId), BlueprintInventoryHash(after));
        if (ReconcileStructuredConstructionState(build).Count != 0 || !_blueprints.TryPut(build))
            throw new InvalidOperationException("structured_exact_native_submission_proof_failed");
    }

    public void UpdateStructuredConstructionNativeTickOnMainThread()
    {
        foreach (var action in _actions.ActiveValues.Where(a => IsStructuredConstruction(a.Plan)
                     && a.Plan.ActionKind != NormalActionKinds.CancelBlueprintBuild).ToArray())
        {
            if (action.Terminal || action.ConstructionLastNativeTick == GameMain.gameTick) continue;
            UpdateStructuredConstructionAction(action);
        }
    }

    public void UpdateStructuredConstructionPrePlayerTickOnMainThread()
    {
        foreach (var action in _actions.ActiveValues.Where(a => !a.Terminal && IsStructuredConstruction(a.Plan)
                     && a.Plan.ActionKind != NormalActionKinds.CancelBlueprintBuild).ToArray())
        {
            if (ConstructionReferencesMatch(action) && NativeWalkModeIsBounded(action.ConstructionBinding!.Player, stationary: true, action.Plan.VoyagerLocalWalkScene,
                    finiteWaterCostWithNativePause: true, allowBudgetedRecharge: true)
                && ConstructionHoldOwned(action)) continue;
            // Catch an already scheduled jump before Player.GameTick can debit
            // it. Never clear user input/Native jump or claim that an external
            // takeover's movement costs still have this certificate.
            RequestStructuredConstructionStop(action, "construction_pre_player_stationary_binding_lost", true, true);
            FinishStructuredConstructionUnknown(action);
        }
    }

    // Called only by the appended1400/main-thread observer, after Native's
    // exact construction-system task returned. Tick growth alone is no proof
    // that a cached prelaunch nomination has had its chance to be consumed.
    public void ObserveStructuredConstructionNativeFactoryPassOnMainThread(GameData sourceWorld)
    {
        if (!ReferenceEquals(GameMain.data, sourceWorld) || sourceWorld.factories is null
            || sourceWorld.factoryCount < 1 || sourceWorld.factoryCount > 512 || sourceWorld.factoryCount > sourceWorld.factories.Length) return;
        foreach (var action in _actions.ActiveValues.Where(a => IsStructuredConstruction(a.Plan)
                     && a.Plan.ActionKind != NormalActionKinds.CancelBlueprintBuild
                     && (!a.Terminal || a.State == NormalActionStates.OutcomeUnknown)).ToArray())
        {
            if (!ConstructionReferencesMatch(action) || action.BlueprintBuild?.ConstructionControl is not { StopReason: not null } control
                || !control.DispatchDisabled || action.ConstructionBinding!.Module.droneEnabled
                || !sourceWorld.factories.Take(sourceWorld.factoryCount).Any(f => ReferenceEquals(f, action.ConstructionBinding.Factory))) continue;
            if (!control.RecordDisabledNativeFactoryPass(GameMain.gameTick)) continue;
            action.BlueprintBuild.LastEvidenceGameTick = Math.Max(action.BlueprintBuild.LastEvidenceGameTick, GameMain.gameTick);
            action.BlueprintBuild.Generation++;
            if (!_blueprints.TryPut(action.BlueprintBuild))
            { action.ConstructionStopUnknown = true; FinishStructuredConstructionUnknown(action); }
        }
    }

    private void UpdateStructuredConstructionAction(ActionRecord action)
    {
        var build = action.BlueprintBuild!;
        var control = build.ConstructionControl!;
        try
        {
            if (!ConstructionReferencesMatch(action))
            { RequestStructuredConstructionStop(action, "native_construction_binding_lost", true, true); FinishStructuredConstructionUnknown(action); return; }
            var player = action.ConstructionBinding!.Player;
            var factory = action.ConstructionBinding.Factory;
            var tick = GameMain.gameTick;
            if (tick < action.ConstructionLastNativeTick || tick - action.ConstructionLastNativeTick > 2)
            {
                RequestStructuredConstructionStop(action, "native_construction_tick_gap_or_rewind", true, true);
                FinishStructuredConstructionUnknown(action); return;
            }
            action.ConstructionLastNativeTick = tick;
            var generationBeforeReconcile = build.Generation;
            var blockers = ReconcileStructuredConstructionState(build);
            if (build.Generation != generationBeforeReconcile && !_blueprints.TryPut(build))
            { RequestStructuredConstructionStop(action, "construction_progress_persistence_failed", true, false); FinishStructuredConstructionUnknown(action); return; }
            if (blockers.Count != 0) RequestStructuredConstructionStop(action, string.Join(";", blockers), true, false);
            if (control.StopReason is null)
            {
                var reason = ValidateStructuredConstructionTick(action);
                if (reason is not null) RequestStructuredConstructionStop(action, reason, true,
                    reason == "construction_anchor_or_order_lost" || reason == "construction_native_cost_or_envelope_changed");
                else if (build.Objects.All(o => o.State == BlueprintObjectStates.Completed))
                    RequestStructuredConstructionStop(action, "construction_complete_draining", false, false);
            }
            if (action.Terminal) return;
            if (control.StopReason is not null)
            {
                VerifyStructuredConstructionDrain(action);
                if (action.Terminal) return;
                if (!TryObserveOwnedConstructionDrones(factory, player.mecha.constructionModule, out var idle, out _))
                { action.ConstructionStopUnknown = true; idle = false; }
                if (idle && !player.mecha.constructionModule.droneEnabled && blockers.Count == 0
                    && control.NativeDrainBarrierPassed(tick, ConstructionOwnPrelaunchCacheClear(factory, build)))
                { FinishStructuredConstructionIdle(action); return; }
                if (control.StopOutcomeUnknown(tick, DateTimeOffset.UtcNow))
                { FinishStructuredConstructionUnknown(action); return; }
                if (!control.BindingLost && player.currentOrder is null && ConstructionHoldOwned(action))
                {
                    action.PlayerOrder = OrderNode.MoveTo(ToVector(control.Anchor));
                    player.Order(action.PlayerOrder, false);
                }
            }
            else
            {
                // A normal at-anchor Move order is naturally achieved by DSP.
                // Renew it only after proving that no user order/input replaced it.
                if (player.currentOrder is null)
                {
                    action.PlayerOrder = OrderNode.MoveTo(ToVector(control.Anchor));
                    player.Order(action.PlayerOrder, false);
                }
            }
        }
        catch (Exception exception)
        {
            RequestStructuredConstructionStop(action, "structured_tick_exception:" + exception.GetType().Name, true, true);
            FinishStructuredConstructionUnknown(action);
        }
    }

    private string? ValidateStructuredConstructionTick(ActionRecord action)
    {
        var build = action.BlueprintBuild!; var control = build.ConstructionControl!;
        var player = action.ConstructionBinding!.Player; var factory = action.ConstructionBinding.Factory;
        var journey = _flightCheckpoints.VoyagerJourney;
        if (journey is null || journey.JourneyId != build.StructuredConstruction!.JourneyId || journey.Phase != "remote_landed"
            || !VoyagerOwnedJourneyMatchesCurrentSession(journey, action.SessionId)
            || journey.RemotePlanetId != action.PlanetId || DateTimeOffset.UtcNow >= _sessions.EffectiveVoyagerDeadline(journey)
            || GameMain.gameTick >= control.WorkDeadlineGameTick || DateTimeOffset.UtcNow >= control.WorkDeadlineUtc
            || DateTimeOffset.UtcNow >= control.ObservationLeaseExpiresAtUtc)
            return "construction_original_time_or_poll_lease_ended";
        if (!ConstructionHoldOwned(action) || !player.mecha.constructionModule.droneEnabled)
            return "construction_anchor_or_order_lost";
        var reserve = CaptureConstructionReserve(build, player, out var hash);
        if (!reserve.Proved || hash != control.NativeCostHash || !ConstructionFactoryHasOnlyOwnedPending(factory, build)
            || !ConstructionDronesStayInEnvelope(action)) return "construction_native_cost_or_envelope_changed";
        var nowBudget = VoyagerLocalActionBudgetPolicy.Evaluate(new VoyagerLocalActionBudgetFacts
        {
            ExactRemoteBinding = true, NativePlayerIdle = true, CurrentGameTick = GameMain.gameTick,
            LastLandedGameTick = journey.LastLandedGameTick, SpentGameTicks = journey.SpentGameTicks,
            MaximumGameTicks = journey.MaximumGameTicks, ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
            WorkGameTicks = 1, TailGameTicks = reserve.TailGameTicks,
            NowUtc = DateTimeOffset.UtcNow, JourneyDeadlineUtc = _sessions.EffectiveVoyagerDeadline(journey),
            AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(player.mecha), LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules,
            WorkPowerUpperWatts = reserve.PowerUpperWatts, TailAndNextTickEnergyJoules = reserve.TailAndNextTickEnergyJoules,
            NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage), OriginalExtraWarperReserve = journey.ExtraWarperReserve,
        });
        action.ConstructionReserveProofValid = nowBudget.Proved && NativeConstructionDroneReservePolicy.CanFundNativeTail(
            player.mecha.coreEnergy, CalculateAvailableFlightEnergy(player.mecha), journey.ReturnEnergyFloorJoules,
            reserve.TailAndNextTickEnergyJoules);
        return action.ConstructionReserveProofValid ? null : "construction_original_return_or_core_tail_reserve_ended";
    }

    private void RequestStructuredConstructionStop(ActionRecord action, string reason, bool unknown, bool bindingLost)
    {
        if (action.Terminal || action.BlueprintBuild?.ConstructionControl is not { } control) return;
        action.ConstructionStopUnknown |= unknown;
        control.BindingLost |= bindingLost;
        if (bindingLost) action.ConstructionReserveProofValid = false;
        // A lost anchor/mode cannot inherit the fixed-anchor drone tail.
        // Freeze first, before dispatch bookkeeping or persistence can fail.
        if (bindingLost) FreezeNativeLocalFailureEnergy(ConstructionReferencesMatch(action));
        var stopTick = ConstructionReferencesMatch(action) && GameMain.gameTick >= control.StartedAtGameTick
            ? GameMain.gameTick : action.ConstructionLastNativeTick;
        if (!control.RequestStop(reason, Math.Max(control.StartedAtGameTick, stopTick), DateTimeOffset.UtcNow, bindingLost))
        {
            if (bindingLost) FinishStructuredConstructionUnknown(action);
            return; // Original cleanup limits never renew.
        }
        if (ConstructionReferencesMatch(action))
        {
            var binding = action.ConstructionBinding!;
            if (binding.Module.droneEnabled) binding.Module.ToggleConstructionDrones(binding.Factory, binding.Player);
            control.DispatchDisabled = !binding.Module.droneEnabled;
            // Retain the native at-anchor order while a valid tail drains.
            // Lost/user-owned orders are never replaced or aborted here.
        }
        else { control.BindingLost = true; action.ConstructionReserveProofValid = false; }
        action.BlueprintBuild.Generation++;
        if (!_blueprints.TryPut(action.BlueprintBuild)) action.ConstructionStopUnknown = true;
        action.State = NormalActionStates.WaitingForGame;
        action.Message = "The original structured batch stopped new submissions/ordinary drone dispatch; physical idle and exact topology are still pending: " + reason;
        if (bindingLost) FinishStructuredConstructionUnknown(action);
    }

    private void FinishStructuredConstructionIdle(ActionRecord action)
    {
        var build = action.BlueprintBuild!; var control = build.ConstructionControl!;
        control.DispatchDisabled = true;
        if (!control.TryMarkPhysicalIdle(GameMain.gameTick,
            ConstructionOwnPrelaunchCacheClear(action.ConstructionBinding!.Factory, build), nativePoolAndModuleIdle: true))
            return;
        build.LastEvidenceGameTick = Math.Max(build.LastEvidenceGameTick, GameMain.gameTick);
        var complete = build.Objects.All(o => o.State == BlueprintObjectStates.Completed);
        control.Phase = complete ? "completed_idle" : "paused_idle";
        build.Stop(complete ? "completed" : "paused");
        if (!_blueprints.TryPut(build)) { FinishStructuredConstructionUnknown(action); return; }
        action.TargetObjectIds = build.Objects.Where(o => o.EntityId.HasValue).Select(o => o.EntityId!.Value).ToList();
        action.TargetObjectId = action.TargetObjectIds.Count == 1 ? action.TargetObjectIds[0] : (int?)null;
        var restoreOriginal = control.MayRestoreOriginalDroneSetting(complete, !action.ConstructionStopUnknown,
            ConstructionHoldOwned(action), GameMain.isPaused);
        AbortPlayerOrderIfOwned(action);
        if (restoreOriginal)
        {
            var binding = action.ConstructionBinding!;
            binding.Module.ToggleConstructionDrones(binding.Factory, binding.Player);
            control.DispatchDisabled = !binding.Module.droneEnabled;
            build.Generation++;
            if (!_blueprints.TryPut(build)) { FinishStructuredConstructionUnknown(action); return; }
        }
        action.AfterInventory ??= CaptureInventory(action.ConstructionBinding!.Player);
        if (complete && !action.ConstructionStopUnknown)
            Complete(action, "Every native structured object has exact durable whole-batch debit, prebuild/entity/component/topology proof, and all owned construction drones are physically idle. Production/logistics require separate observations.");
        else
        {
            action.FailureKind = "structured_stopped_idle";
            Fail(action, "The exact structured batch is safely stopped with physical idle proof. Created objects remain; fresh-resume only this buildId's known pending work. " + control.StopReason);
        }
    }

    private void FinishStructuredConstructionUnknown(ActionRecord action)
    {
        if (action.Terminal) return;
        var build = action.BlueprintBuild!; var control = build.ConstructionControl!;
        var exact = ConstructionReferencesMatch(action);
        var freeze = FreezeNativeLocalFailureEnergy(exact);
        if (control.Phase != "interrupted") control.Phase = "quarantined";
        action.ConstructionReserveProofValid = false;
        build.Stop("outcome_unknown"); _blueprints.TryPut(build);
        action.State = NormalActionStates.OutcomeUnknown; action.Terminal = true; action.Succeeded = false;
        action.CompletedAtGameTick = action.ConstructionLastNativeTick;
        action.Message = "The original structured batch has not proved finite physical drain/reserve/binding. Keep its exact build/action; no pending object may be recreated. " + control.StopReason + freeze;
        action.OriginalOutcomeMessage = action.Message;
        if (ConstructionReferencesMatch(action))
        {
            action.AfterInventory ??= CaptureInventory(action.ConstructionBinding!.Player);
            _sessions.QuarantineWritesOnMainThread(action.ActionId, action.Message);
        } // A replacement world/session cannot inherit the original quarantine.
    }

    // Only an exact original-action read can reconcile a physically stopped
    // batch. It never enables drones, submits work, replenishes a lease or
    // turns the earlier reserve/binding failure into successful execution.
    private void ReconcileStructuredConstructionOriginalAction(ActionRecord action)
    {
        if (action.State != NormalActionStates.OutcomeUnknown || !action.Terminal
            || !ConstructionReferencesMatch(action) || action.BlueprintBuild?.ConstructionControl is not { } control
            || !control.DispatchDisabled || action.ConstructionBinding!.Module.droneEnabled
            || !ConstructionPlayerIdle(action.ConstructionBinding.Player)
            || !TryObserveOwnedConstructionDrones(action.ConstructionBinding.Factory, action.ConstructionBinding.Module, out var idle, out _)
            || !idle || !ConstructionFactoryHasOnlyOwnedPending(action.ConstructionBinding.Factory, action.BlueprintBuild)
            || !control.NativeDrainBarrierPassed(GameMain.gameTick,
                ConstructionOwnPrelaunchCacheClear(action.ConstructionBinding.Factory, action.BlueprintBuild))
            || action.BlueprintBuild.Objects.Any(o => o.State != BlueprintObjectStates.PendingConstruction && o.State != BlueprintObjectStates.Completed)
            || ReconcileStructuredConstructionState(action.BlueprintBuild).Count != 0)
            return;
        var complete = action.BlueprintBuild.Objects.All(o => o.State == BlueprintObjectStates.Completed);
        if (!control.TryMarkPhysicalIdle(GameMain.gameTick,
            ConstructionOwnPrelaunchCacheClear(action.ConstructionBinding.Factory, action.BlueprintBuild), nativePoolAndModuleIdle: true)) return;
        action.BlueprintBuild.LastEvidenceGameTick = Math.Max(action.BlueprintBuild.LastEvidenceGameTick, GameMain.gameTick);
        control.Phase = complete ? "completed_idle" : "paused_idle";
        action.BlueprintBuild.Stop(complete ? "completed" : "paused");
        if (!_blueprints.TryPut(action.BlueprintBuild)
            || !_sessions.TryClearQuarantineOnMainThread(action.ActionId, action.OriginalOutcomeMessage!, out _)) return;
        action.State = NormalActionStates.ActionFailed; action.Succeeded = false;
        action.ReconciledFromOutcomeUnknown = true; action.ReconciledAtGameTick = GameMain.gameTick;
        action.FailureKind = "structured_stopped_idle"; action.ConstructionReserveProofValid = false;
        action.Message = "Original-action read proved the retained whole-batch cost, exact pending/built objects, topology and disabled physically idle drones. The earlier execution/reserve failure remains failed. Fresh-resume only the same buildId's known pending objects; do not recreate them.";
    }

    private void CheckStructuredConstructionWatchdogOnMainThread()
    {
        foreach (var action in _actions.ActiveValues.Where(a => !a.Terminal && IsStructuredConstruction(a.Plan)
                     && a.Plan.ActionKind != NormalActionKinds.CancelBlueprintBuild).ToArray())
        {
            if (action.Terminal || action.BlueprintBuild?.ConstructionControl is not { } control) continue;
            if (GameMain.isPaused)
                RequestStructuredConstructionStop(action, "construction_user_paused", true, true);
            else if (!ConstructionReferencesMatch(action) || DateTimeOffset.UtcNow >= control.WorkDeadlineUtc
                || DateTimeOffset.UtcNow >= control.ObservationLeaseExpiresAtUtc
                || !VoyagerLocalPrePlayerTickAvailable
                || DateTimeOffset.UtcNow - VoyagerLocalPrePlayerObservedAtUtc > TimeSpan.FromSeconds(1)
                || DateTimeOffset.UtcNow - VoyagerLastNativeTickObservedAtUtc > TimeSpan.FromSeconds(1))
                RequestStructuredConstructionStop(action, "construction_host_clock_or_native_hook_stopped", true, !ConstructionReferencesMatch(action));
            if (control.DrainStartedAtUtc.HasValue
                && DateTimeOffset.UtcNow >= control.DrainStartedAtUtc.Value.AddSeconds(control.TailGameTicks / 60d))
                FinishStructuredConstructionUnknown(action);
        }
    }

    public void StopStructuredConstructionForHostShutdownOnMainThread()
    {
        foreach (var action in _actions.ActiveValues.Where(a => !a.Terminal && IsStructuredConstruction(a.Plan)
                     && a.Plan.ActionKind != NormalActionKinds.CancelBlueprintBuild).ToArray())
        {
            RequestStructuredConstructionStop(action, "construction_host_shutdown_pending", true, true);
            if (action.BlueprintBuild?.ConstructionControl is { } control) control.Phase = "interrupted";
            FinishStructuredConstructionUnknown(action);
        }
    }

    private BridgeError? ValidateStructuredConstructionCancellation(NormalActionPlanPayload plan) =>
        _actions.ActiveValues.Any(a => !a.Terminal && a.ActionId == plan.BlueprintState!.ActionId
            && a.BlueprintBuild?.BuildId == plan.BlueprintState.BuildId && IsStructuredConstruction(a.Plan))
            ? null : Stale("Only the exact active structured build may receive this sticky dispatch-stop request.");

    private void CancelStructuredConstructionOnMainThread(ActionRecord cancellation)
    {
        var active = _actions.ActiveValues.Single(a => !a.Terminal && a.ActionId == cancellation.Plan.BlueprintState!.ActionId
            && a.BlueprintBuild?.BuildId == cancellation.Plan.BlueprintState.BuildId && IsStructuredConstruction(a.Plan));
        RequestStructuredConstructionStop(active, "explicit_structured_cancellation", false, false);
        cancellation.BlueprintBuild = active.BlueprintBuild;
        Complete(cancellation, "The original structured action received a sticky stop-dispatch request. Poll that exact action for real physical drone idle; this cancellation is not completion, refund or a second construction submission.");
    }

    private void VerifyStructuredConstructionDrain(ActionRecord action)
    {
        var control = action.BlueprintBuild!.ConstructionControl!;
        var player = action.ConstructionBinding!.Player;
        var journey = _flightCheckpoints.VoyagerJourney;
        var remaining = control.DrainStartedAtGameTick.HasValue
            ? Math.Max(0L, control.TailGameTicks - (GameMain.gameTick - control.DrainStartedAtGameTick.Value)) : 0L;
        var reserve = CaptureConstructionReserve(action.BlueprintBuild, player, out var hash);
        var remainingEnergy = reserve.PowerUpperWatts * (remaining + 2d) / 60d
            + player.mecha.constructionModule.droneCount * Configs.freeMode.droneEjectEnergy;
        var valid = ConstructionHoldOwned(action) && !player.mecha.constructionModule.droneEnabled
            && reserve.Proved && hash == control.NativeCostHash && ConstructionDronesStayInEnvelope(action)
            && journey is not null && journey.JourneyId == action.BlueprintBuild.StructuredConstruction!.JourneyId
            && VoyagerOwnedJourneyMatchesCurrentSession(journey, action.SessionId)
            && journey.Phase == "remote_landed" && DateTimeOffset.UtcNow < _sessions.EffectiveVoyagerDeadline(journey)
            && NativeConstructionDroneReservePolicy.CanFundNativeTail(player.mecha.coreEnergy,
                CalculateAvailableFlightEnergy(player.mecha), journey.ReturnEnergyFloorJoules, remainingEnergy)
            && CountVoyagerWarpers(player.mecha.warpStorage) >= 1 + journey.ExtraWarperReserve;
        if (!valid)
        {
            action.ConstructionStopUnknown = true; control.BindingLost = true;
            FinishStructuredConstructionUnknown(action);
        }
        action.ConstructionReserveProofValid &= valid;
    }

    private static bool ConstructionBatchDebit(Dictionary<int, int> before, Dictionary<int, int> after, int itemId, int count) =>
        before.Keys.Concat(after.Keys).Distinct().All(id => GetCount(after, id) == GetCount(before, id) - (id == itemId ? count : 0));
    private static Vector3Snapshot ConstructionVector(Vector3 p) => new() { X = p.x, Y = p.y, Z = p.z };
    private static QuaternionSnapshot ConstructionRotation(Quaternion q) => new() { X = q.x, Y = q.y, Z = q.z, W = q.w };
    private static bool FiniteConstruction(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;

    private sealed class StructuredNativeBinding
    {
        public StructuredNativeBinding(GameData world, Player player, PlanetFactory factory, string ownedSaveName, string gameVersion)
        { World = world; Player = player; Controller = player.controller; Mecha = player.mecha; Factory = factory; Module = player.mecha.constructionModule;
            System = factory.constructionSystem; OwnedSaveName = ownedSaveName; GameVersion = gameVersion; }
        public GameData World { get; }
        public Player Player { get; }
        public PlayerController Controller { get; }
        public Mecha Mecha { get; }
        public PlanetFactory Factory { get; }
        public ConstructionModuleComponent Module { get; }
        public ConstructionSystem System { get; }
        public string OwnedSaveName { get; }
        public string GameVersion { get; }
    }

    private sealed partial class ActionRecord
    {
        public StructuredNativeBinding? ConstructionBinding { get; set; }
        public long ConstructionLastNativeTick { get; set; }
        public bool ConstructionReserveProofValid { get; set; }
        public bool ConstructionStopUnknown { get; set; }
    }

    private sealed partial class NormalActionPlanPayload
    {
        public NativeConstructionDroneReserve? ConstructionReserve { get; set; }
        public string? ConstructionNativeCostHash { get; set; }
    }
}
