using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Transport;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    public GameCallResult<BlueprintBuildList> GetBlueprintBuildsOnMainThread(string? sessionId, BlueprintBuildRequest request)
    {
        var common = ValidatePrepareCommon(sessionId, request.PlanetId, 1);
        if (common.Error is not null) return GameCallResult<BlueprintBuildList>.Failed(common.Error);
        if (request.BuildId is not null && !Guid.TryParse(request.BuildId, out _))
            return GameCallResult<BlueprintBuildList>.Failed(BlueprintError("blueprint_build_id_invalid"));
        if (!_blueprints.TryRead(out var builds)) return GameCallResult<BlueprintBuildList>.Failed(BlueprintError("blueprint_progress_store_unavailable"));
        var result = new BlueprintBuildList();
        foreach (var stored in builds.Where(b => b.Site.PlanetId == request.PlanetId
                     && (request.BuildId is null || b.BuildId == request.BuildId)))
        {
            var build = _actions.ActiveValues.FirstOrDefault(a => !a.Terminal && a.ActionId == stored.ActionId)?.BlueprintBuild ?? stored;
            if (request.BuildId is null)
            {
                var summary = BlueprintSnapshot(build, common.Session!, new List<string> { "summary_only:read_exact_buildId_for_fresh_world_proof" });
                summary.Site = null; summary.FoundryPlan = null; summary.Objects.Clear(); summary.StateHash = string.Empty; summary.FreshWorldReconciled = false;
                result.Builds.Add(summary);
                continue;
            }
            var generation = build.Generation;
            var blockers = ReconcileBlueprintState(build);
            // Observed completion is local metadata, not a game mutation. Persist it before
            // exposing a new watermark; otherwise repeated reads invent a different first
            // completion tick and can make every subsequent fresh prepare look stale.
            if (generation != build.Generation && !_blueprints.TryPut(build))
                return GameCallResult<BlueprintBuildList>.Failed(BlueprintError("blueprint_completion_persistence_failed"));
            result.Builds.Add(BlueprintSnapshot(build, common.Session!, blockers));
        }
        return GameCallResult<BlueprintBuildList>.Succeeded(result);
    }

    public GameCallResult<PreparedNormalAction> PrepareBlueprintBuildOnMainThread(string? sessionId, PrepareBlueprintBuildRequest request)
    {
        var common = ValidatePrepareCommon(sessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if (request.MaximumObjectsToSubmit < 1 || request.MaximumObjectsToSubmit > 32
            || string.IsNullOrEmpty(request.ExpectedStateHash)
            || string.IsNullOrEmpty(request.ExpectedPlayerStateHash)) return InvalidPlan("A bounded submission count and fresh site/progress/player hashes are required.");
        if ((request.FoundryIntent is null) != (request.ExpectedFoundryPlanHash is null))
            return InvalidPlan("Foundry intent and expectedFoundryPlanHash must be supplied together for a new finite construction.");
        if (_actions.ActiveValues.Any(a => !a.Terminal)) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("another_action_active"));
        var player = _reader.GetPlayerStateOnMainThread(sessionId, new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!player.Success) return GameCallResult<PreparedNormalAction>.Failed(player.Error!);
        if (player.Value!.StateHash != request.ExpectedPlayerStateHash) return StalePlan("Player state changed before blueprint preparation.");
        var playerError = BlueprintPlayerReady(player.Value);
        BlueprintBuildState build;
        var resume = request.ResumeBuildId is not null;
        if (resume)
        {
            if (request.BlueprintCode is not null || request.Site is not null || request.FoundryIntent is not null
                || request.ExpectedFoundryPlanHash is not null || !Guid.TryParse(request.ResumeBuildId, out _))
                return InvalidPlan("Resume accepts an owned buildId only, never a replacement code/site or old token.");
            if (!_blueprints.TryRead(out var builds)) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("blueprint_progress_store_unavailable"));
            build = builds.SingleOrDefault(b => b.BuildId == request.ResumeBuildId && b.Site.PlanetId == request.PlanetId)!;
            if (build is null) return InvalidPlan("No such finite construction belongs to this owned planet.");
            if (build.StructuredConstruction is not null)
                return PrepareStructuredConstructionResume(request, common.Session!, build, player.Value);
            if (playerError is not null) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError(playerError));
            var generation = build.Generation;
            var blockers = ReconcileBlueprintState(build);
            if (generation != build.Generation)
            {
                if (!_blueprints.TryPut(build)) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("blueprint_completion_persistence_failed"));
                return StalePlan("Pending construction completed; its observation was persisted. Fresh read progress and prepare again.");
            }
            if (blockers.Count != 0) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError(string.Join(";", blockers)));
            if (build.ProgressHash(sessionId!, common.Session!.Revision) != request.ExpectedStateHash)
                return StalePlan("Finite progress changed; read it and prepare again.");
            if (build.Objects.All(o => o.State == BlueprintObjectStates.Completed)) return InvalidPlan("The finite plan is already completed; do not replay the module.");
            var resumeError = ValidateRemainingBlueprint(build, player.Value);
            if (resumeError is not null) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError(resumeError));
            var foundryPowerError = RevalidateRemainingFoundryPower(build);
            if (foundryPowerError is not null) return GameCallResult<PreparedNormalAction>.Failed(foundryPowerError);
        }
        else
        {
            if (playerError is not null) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError(playerError));
            if (request.BlueprintCode is null || request.Site is null
                || request.Site.ExpectedPlayerStateHash != request.ExpectedPlayerStateHash)
                return InvalidPlan("A new module requires explicit code, placement and the same fresh player hash.");
            if (!_blueprints.TryRead(out var builds) || builds.Count >= 32)
                return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("blueprint_progress_store_unavailable_or_full"));
            BlueprintSiteSnapshot site;
            FoundryConstructionPlan? foundryPlan = null;
            if (request.FoundryIntent is not null)
            {
                var compiled = ReadFoundryConstruction(sessionId!, request.PlanetId, request.BlueprintCode, request.Site, request.FoundryIntent);
                if (!compiled.Success || compiled.Value?.BlueprintSite is null || compiled.Value.Construction is null)
                    return GameCallResult<PreparedNormalAction>.Failed(compiled.Error!);
                site = compiled.Value.BlueprintSite; foundryPlan = compiled.Value.Construction;
                if (foundryPlan.PlanHash != request.ExpectedFoundryPlanHash) return StalePlan("Foundry intent, layout or whole construction graph changed after inspection.");
                if (!foundryPlan.CanPrepare) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("foundry_plan_blocked:" + string.Join(";", foundryPlan.Blockers)));
            }
            else
            {
                var inspection = _reader.InspectBlueprintOnMainThread(sessionId, new InspectBlueprintRequest
                { PlanetId = request.PlanetId, BlueprintCode = request.BlueprintCode, Site = request.Site });
                if (!inspection.Success || inspection.Value?.Site is null) return GameCallResult<PreparedNormalAction>.Failed(inspection.Error!);
                site = inspection.Value.Site;
            }
            if (site.AssessmentHash != request.ExpectedStateHash) return StalePlan("Blueprint site or whole budget changed after inspection.");
            if (!site.NativeCheckPassed || !site.InventorySufficient || !site.TechnologySatisfied || site.Blockers.Count != 0)
                return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("blueprint_site_blocked:" + string.Join(";", site.Blockers)));
            build = BlueprintBuildState.Create(site, foundryPlan, request.FoundryIntent);
        }
        var plan = NormalActionPlanPayload.Blueprint(sessionId!, request.PlanetId, common.Session!.Revision,
            player.Value.StateHash, build, request.ExpectedStateHash, request.MaximumObjectsToSubmit,
            resume ? null : request.BlueprintCode, request.Site, false);
        var prepared = AddPreparedPlan(plan, common.Session, Math.Max(600, build.Objects.Count * 120),
            "Finite approved module only: at most one native prebuild per game tick; each debit and completed object is durably recorded. Batch limit pauses without rollback. Poll action to terminal and read blueprint progress; restart requires fresh prepare of this buildId, never whole-module replay.");
        if (prepared.Value is not null)
        {
            prepared.Value.BlueprintBuild = BlueprintSnapshot(build, common.Session, new List<string>());
            foreach (var group in build.Objects.Where(o => o.State == BlueprintObjectStates.NotSubmitted || o.State == BlueprintObjectStates.Blocked)
                         .GroupBy(o => build.Site.Objects[o.Index].ItemId))
                prepared.Value.ItemBudget.Add(new ActionItemBudget { ItemId = group.Key, Count = group.Count(), Direction = "consume",
                    Name = LDB.items.Select(group.Key).name });
        }
        return prepared;
    }

    private BridgeError? RevalidateBlueprintBuildOnMainThread(NormalActionPlanPayload plan)
    {
        if (IsStructuredConstruction(plan)) return RevalidateStructuredConstructionResume(plan);
        if (_actions.ActiveValues.Any(a => !a.Terminal) || _sessions.CaptureOnMainThread().Revision != plan.BlueprintRevision)
            return Stale("Session revision or active action changed after finite-plan preparation.");
        var player = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (!player.Success || player.Value is null) return player.Error;
        if (player.Value.StateHash != plan.PlayerStateHash) return Stale("Player changed after finite-plan preparation.");
        var playerError = BlueprintPlayerReady(player.Value);
        if (playerError is not null) return BlueprintError(playerError);
        if (plan.BlueprintCode is not null)
        {
            if (plan.BlueprintState!.FoundryIntent is not null)
            {
                var compiled = ReadFoundryConstruction(plan.SessionId, plan.PlanetId, plan.BlueprintCode,
                    plan.BlueprintSiteRequest!, plan.BlueprintState.FoundryIntent);
                if (!compiled.Success || compiled.Value?.Construction is null || compiled.Value.BlueprintSite is null)
                    return compiled.Error ?? BlueprintError("foundry_plan_unavailable");
                return compiled.Value.Construction.CanPrepare
                    && compiled.Value.Construction.PlanHash == plan.BlueprintState.FoundryPlan!.PlanHash
                    && compiled.Value.BlueprintSite.AssessmentHash == plan.BlueprintInputHash ? null
                    : Stale("Foundry layout, power readiness, intent or whole budget changed before commit.");
            }
            var current = _reader.InspectBlueprintOnMainThread(plan.SessionId, new InspectBlueprintRequest
            { PlanetId = plan.PlanetId, BlueprintCode = plan.BlueprintCode, Site = plan.BlueprintSiteRequest });
            if (!current.Success || current.Value?.Site is null) return current.Error ?? BlueprintError("blueprint_site_unavailable");
            return current.Value.Site.AssessmentHash == plan.BlueprintInputHash && current.Value.Site.NativeCheckPassed
                && current.Value.Site.Blockers.Count == 0 ? null : Stale("Blueprint scene, material budget or native condition changed.");
        }
        if (!_blueprints.TryRead(out var builds)) return BlueprintError("blueprint_progress_store_unavailable");
        var build = builds.SingleOrDefault(b => b.BuildId == plan.BlueprintState!.BuildId);
        if (build is null) return Stale("Finite progress no longer exists.");
        var blockers = ReconcileBlueprintState(build);
        if (blockers.Count != 0) return BlueprintError(string.Join(";", blockers));
        if (build.ProgressHash(plan.SessionId, plan.BlueprintRevision) != plan.BlueprintInputHash)
            return Stale("Finite progress changed after preparation.");
        var error = ValidateRemainingBlueprint(build, player.Value);
        if (error is null)
        {
            var powerError = RevalidateRemainingFoundryPower(build);
            if (powerError is not null) return powerError;
        }
        if (error is null) plan.BlueprintState = build;
        return error is null ? null : BlueprintError(error);
    }

    private void StartBlueprintBuildOnMainThread(ActionRecord action)
    {
        if (IsStructuredConstruction(action.Plan)) { StartStructuredConstructionOnMainThread(action); return; }
        var build = CloneBlueprintState(action.Plan.BlueprintState!);
        action.BlueprintBuild = build;
        build.Begin(action.SessionId, action.ActionId, action.Plan.BlueprintSubmissionLimit, GameMain.gameTick);
        if (!_blueprints.TryPut(build))
        { Fail(action, "Finite plan could not be made durable; no native object was submitted."); return; }
        action.State = NormalActionStates.WaitingForGame;
        action.Message = "Finite plan accepted and durable; the bounded executor will submit only dependency-ready new objects.";
        // First object is deliberately not submitted inside commit; cancellation/aggregation
        // remains an ordinary action, not an apparent atomic blueprint stamp.
    }

    private void UpdateBlueprintBuildOnMainThread(ActionRecord action)
    {
        if (action.LastBlueprintPollTick == GameMain.gameTick) return;
        action.LastBlueprintPollTick = GameMain.gameTick;
        var build = action.BlueprintBuild!;
        int? submitting = null;
        try
        {
            var session = _sessions.CaptureOnMainThread();
            if (session.WriteBlockers.Count != 0)
            { StopBlueprintAction(action, "blocked", "Session writes are blocked; no further objects submitted."); return; }
            var generation = build.Generation;
            var blockers = ReconcileBlueprintState(build);
            if (blockers.Count > 0)
            { StopBlueprintAction(action, "outcome_unknown", string.Join(";", blockers)); return; }
            if (generation != build.Generation && !_blueprints.TryPut(build))
                throw new InvalidOperationException("Completion evidence could not be persisted.");
            if (build.Objects.Any(o => o.State == BlueprintObjectStates.PendingConstruction))
            {
                var pending = build.Objects.Single(o => o.State == BlueprintObjectStates.PendingConstruction);
                if (GameMain.gameTick - pending.SubmittedAtGameTick > 7200)
                    StopBlueprintAction(action, "paused", "Construction remains pending after 7200 game ticks; inspect energy/range/drones, then explicitly resume. No object is replayed.");
                return;
            }
            if (build.Objects.All(o => o.State == BlueprintObjectStates.Completed))
            { StopBlueprintAction(action, "completed", "Every finite-plan object has exact native cost, configuration and reciprocal connection proof. Supply/power/production need separate fresh validation."); return; }
            if (build.SubmittedThisAction >= build.MaximumObjectsToSubmit)
            { StopBlueprintAction(action, "paused", "The approved submission limit was reached. Completed objects remain; save/restart or fresh prepare this buildId to continue only unsubmitted objects."); return; }
            var player = _reader.GetPlayerStateOnMainThread(action.SessionId, new LocalPlanetRequest { PlanetId = action.PlanetId });
            var readyError = player.Success ? BlueprintPlayerReady(player.Value!) : "player_unavailable";
            if (readyError is not null) { StopBlueprintAction(action, "blocked", readyError); return; }
            var index = build.NextReadyIndex();
            if (!index.HasValue) { StopBlueprintAction(action, "blocked", "no_dependency_ready_object"); return; }
            var spec = build.Site.Objects[index.Value];
            var factory = GameMain.data.localLoadedPlanetFactory;
            var preview = BlueprintStepPreview(build, index.Value);
            var ready = GameMain.history.blueprintLimit >= build.Objects.Count && GameMain.history.ItemUnlocked(spec.ItemId)
                && (spec.RecipeId == 0 || GameMain.history.RecipeUnlocked(spec.RecipeId))
                && (spec.FilterItemId == 0 || GameMain.history.ItemUnlocked(spec.FilterItemId))
                && GameMain.mainPlayer.package.GetItemCount(spec.ItemId) >= 1
                && Vector3.Distance(GameMain.mainPlayer.position, preview.lpos) <= player.Value!.BuildArea
                && Vector3.Distance(GameMain.mainPlayer.position, preview.lpos2) <= player.Value.BuildArea
                && BlueprintNewSiteClear(factory, preview);
            if (!ready) { build.Stop("blocked", index, "fresh_site_technology_range_or_material_blocker"); StopBlueprintAction(action, "blocked", "Fresh per-object preflight rejected before any native submission."); return; }
            using (var native = new BlueprintNativeStep(preview))
            {
                if (!native.Check())
                { build.Stop("blocked", index, "native_condition:" + preview.condition); StopBlueprintAction(action, "blocked", "Native per-object preflight rejected; no item was taken for this object."); return; }
                var before = CaptureInventory(GameMain.mainPlayer);
                build.BeforeSubmit(index.Value, GameMain.gameTick, GetCount(before, spec.ItemId), BlueprintInventoryHash(before));
                if (!_blueprints.TryPut(build))
                { StopBlueprintAction(action, "outcome_unknown", "Write-ahead evidence could not persist; native CreatePrebuilds was not called."); return; }
                submitting = index;
                native.Create();
                var after = CaptureInventory(GameMain.mainPlayer);
                if (preview.objId >= 0 || !BlueprintPrebuildMatches(factory, -preview.objId, spec)
                    || !BlueprintInventoryDebit(before, after, spec.ItemId))
                    throw new InvalidOperationException("Native prebuild or exact inventory debit could not be proven.");
                build.ConfirmSubmission(index.Value, -preview.objId, GetCount(after, spec.ItemId), BlueprintInventoryHash(after));
                if (!BlueprintConnectionsMatch(factory, build) || !_blueprints.TryPut(build))
                    throw new InvalidOperationException("Native reciprocal links or durable submission could not be proven.");
                _sessions.IncrementRevisionOnMainThread();
                action.TargetObjectIds = build.Objects.Where(o => o.EntityId.HasValue).Select(o => o.EntityId!.Value).ToList();
                action.Message = "Submitted blueprint object " + index + "; waiting for ordinary construction drones. Do not replay the module.";
            }
        }
        catch (Exception exception)
        {
            build.Stop("outcome_unknown", submitting, "execution_exception:" + exception.GetType().Name);
            _blueprints.TryPut(build);
            QuarantineBuildOutcome(action, "Finite construction outcome is uncertain (" + exception.GetType().Name + "); inspect the durable per-object record. Never replay the whole blueprint.");
        }
    }

    private void StopBlueprintAction(ActionRecord action, string phase, string message)
    {
        action.BlueprintBuild!.Stop(phase);
        var durable = _blueprints.TryPut(action.BlueprintBuild);
        if (!durable || phase == "outcome_unknown")
        { QuarantineBuildOutcome(action, message + (durable ? "" : " Progress persistence failed.")); return; }
        if (phase == "completed" || phase == "paused") Complete(action, message);
        else { action.FailureKind = phase; Fail(action, message); }
    }

    private List<string> ReconcileBlueprintState(BlueprintBuildState build)
    {
        if (build.StructuredConstruction is not null) return ReconcileStructuredConstructionState(build);
        var blockers = new List<string>();
        if (GameMain.gameTick < build.LastEvidenceGameTick) { blockers.Add("world_precedes_durable_construction_evidence"); return blockers; }
        var factory = GameMain.data.localLoadedPlanetFactory;
        if (factory?.planetId != build.Site.PlanetId || factory.entityCursor > 131072 || factory.prebuildCursor > 131072)
        { blockers.Add("construction_factory_unavailable_or_limit"); return blockers; }
        foreach (var step in build.Objects)
        {
            var spec = build.Site.Objects[step.Index];
            if (step.State == BlueprintObjectStates.Submitting || step.State == BlueprintObjectStates.OutcomeUnknown)
            { blockers.Add("object_outcome_unknown:" + step.Index); continue; }
            if (step.State == BlueprintObjectStates.Completed)
            {
                if (!BlueprintEntityMatches(factory, step.EntityId.GetValueOrDefault(), spec)) blockers.Add("completed_object_changed_or_missing:" + step.Index);
            }
            else if (step.State == BlueprintObjectStates.PendingConstruction)
            {
                var prebuild = step.PrebuildId.GetValueOrDefault();
                if (prebuild > 0 && prebuild < factory.prebuildCursor && factory.prebuildPool[prebuild].id == prebuild)
                {
                    if (!BlueprintPrebuildMatches(factory, prebuild, spec)) blockers.Add("pending_object_changed:" + step.Index);
                    continue;
                }
                var found = 0;
                for (var id = 1; id < factory.entityCursor && id < factory.entityPool.Length; id++)
                    if (BlueprintEntityMatches(factory, id, spec)) { if (found != 0) { found = -1; break; } found = id; }
                if (found <= 0) blockers.Add("pending_result_missing_or_ambiguous:" + step.Index);
                else build.ConfirmCompletion(step.Index, found, GameMain.gameTick);
            }
        }
        if (blockers.Count == 0 && !BlueprintConnectionsMatch(factory, build)) blockers.Add("construction_topology_changed_or_unproven");
        return blockers;
    }

    private static string? ValidateRemainingBlueprint(BlueprintBuildState build, PlayerStateSnapshot player)
    {
        if (GameMain.history.blueprintLimit < build.Objects.Count) return "blueprint_technology_locked";
        foreach (var group in build.Objects.Where(o => o.State == BlueprintObjectStates.NotSubmitted || o.State == BlueprintObjectStates.Blocked)
                     .GroupBy(o => build.Site.Objects[o.Index].ItemId))
            if (!GameMain.history.ItemUnlocked(group.Key) || GameMain.mainPlayer.package.GetItemCount(group.Key) < group.Count())
                return "remaining_whole_material_budget_or_unlock_missing";
        var factory = GameMain.data.localLoadedPlanetFactory;
        foreach (var step in build.Objects.Where(o => o.State == BlueprintObjectStates.NotSubmitted || o.State == BlueprintObjectStates.Blocked))
        {
            var spec = build.Site.Objects[step.Index];
            var preview = BlueprintStepPreview(build, step.Index);
            if (spec.RecipeId > 0 && !GameMain.history.RecipeUnlocked(spec.RecipeId)) return "remaining_recipe_locked";
            if (spec.FilterItemId > 0 && !GameMain.history.ItemUnlocked(spec.FilterItemId)) return "remaining_filter_item_locked";
            if (spec.ItemId == 2101 && (!GameStateReader.BlueprintStorageParametersMatchNative(spec.ItemId, spec.Parameters)
                || !BlueprintStoragePolicy.FiltersUnlocked(spec.Parameters, GameMain.history.ItemUnlocked)))
                return "remaining_storage_configuration_invalid_or_filter_locked";
            if (Vector3.Distance(GameMain.mainPlayer.position, preview.lpos) > player.BuildArea
                || Vector3.Distance(GameMain.mainPlayer.position, preview.lpos2) > player.BuildArea) return "remaining_object_out_of_range";
            if (!BlueprintNewSiteClear(factory, preview)) return "remaining_site_occupied";
        }
        return null;
    }

    private static string? BlueprintPlayerReady(PlayerStateSnapshot player) =>
        !player.IsAlive || !player.IsOnPlanet || player.MovementState != "Walk" || player.Speed > .1f
        || !BuildUiIsIdle(GameMain.mainPlayer) || GameMain.mainPlayer.mecha.coreEnergy < GameMain.mainPlayer.mecha.coreEnergyCap * .1
        ? "blueprint_requires_settled_walk_energy_and_idle_build_ui" : null;

    private BlueprintBuildProgress BlueprintSnapshot(BlueprintBuildState build, SessionState session, List<string> blockers) => new BlueprintBuildProgress
    {
        ConstructionControl = CaptureConstructionControl(build,
            _actions.ActiveValues.FirstOrDefault(a => a.BlueprintBuild?.BuildId == build.BuildId)),
        BuildId = build.BuildId, BlueprintHash = build.Site.BlueprintHash, PlanHash = build.PlanHash,
        SessionId = session.SessionId!, PlanetId = build.Site.PlanetId, Revision = session.Revision,
        CapturedAtGameTick = GameMain.gameTick, StateHash = build.ProgressHash(session.SessionId!, session.Revision),
        Phase = build.Phase == "running" && !_actions.ActiveValues.Any(a => !a.Terminal && a.ActionId == build.ActionId)
            ? "interrupted_requires_fresh_prepare" : build.Phase,
        ActionId = build.ActionId, PersistenceHealthy = true,
        FreshWorldReconciled = true,
        RequiresFreshPrepare = !_actions.ActiveValues.Any(a => !a.Terminal && a.ActionId == build.ActionId),
        SubmittedCount = build.Objects.Count(o => o.PrebuildId.HasValue),
        CompletedCount = build.Objects.Count(o => o.State == BlueprintObjectStates.Completed),
        Objects = CloneBlueprintObjects(build.Objects), Blockers = blockers,
        Site = PluginJson.Deserialize<BlueprintSiteSnapshot>(PluginJson.Serialize(build.Site))!,
        FoundryPlanHash = build.FoundryPlan?.PlanHash,
        FoundryPlan = build.FoundryPlan is null ? null : PluginJson.Deserialize<FoundryConstructionPlan>(PluginJson.Serialize(build.FoundryPlan)),
    };

    private GameCallResult<FoundryPlanSnapshot> ReadFoundryConstruction(string sessionId, int planetId,
        string code, BlueprintSiteRequest site, FoundryConstructionIntent intent) =>
        _reader.GetFoundryPlanOnMainThread(sessionId, new GetFoundryPlanRequest
        {
            PlanetId = planetId, TargetItemId = intent.TargetItemId, TargetRatePerMinute = intent.TargetRatePerMinute,
            ExternalSupplyItemIds = intent.ExternalSupplyItemIds, RecipeChoices = intent.RecipeChoices,
            Blueprint = new FoundryBlueprintRequest { BlueprintCode = code, Site = site, BoundaryPorts = intent.BoundaryPorts },
        });

    private BridgeError? RevalidateRemainingFoundryPower(BlueprintBuildState build)
    {
        if (build.FoundryPlan is null) return null;
        var intent = build.FoundryIntent!;
        var material = _reader.GetFoundryPlanOnMainThread(_sessions.SessionId, new GetFoundryPlanRequest
        {
            PlanetId = build.Site.PlanetId, TargetItemId = intent.TargetItemId, TargetRatePerMinute = intent.TargetRatePerMinute,
            ExternalSupplyItemIds = intent.ExternalSupplyItemIds, RecipeChoices = intent.RecipeChoices,
        });
        if (!material.Success || material.Value is null) return material.Error ?? BlueprintError("foundry_material_evidence_unavailable");
        if (material.Value.PlanHash != build.FoundryPlan.MaterialPlanHash)
            return BlueprintError("foundry_material_rules_changed_replan_required");
        var pending = build.Objects.Where(o => o.State != BlueprintObjectStates.Completed).ToArray();
        if (pending.Length == 0) return null;
        var catalog = _reader.GetBuildCatalogOnMainThread(_sessions.SessionId);
        if (!catalog.Success || catalog.Value is null) return catalog.Error ?? BlueprintError("foundry_transport_catalog_unavailable");
        var transport = FoundryTransportPlanner.Assess(build.Site, catalog.Value.Buildings, build.FoundryPlan.Routes);
        if (!transport.Satisfied || FoundryTransportPlanner.Fingerprint(transport)
            != FoundryTransportPlanner.Fingerprint(build.FoundryPlan.TransportBudget))
            return BlueprintError("foundry_transport_rules_changed_replan_required");
        // Completed objects already exist in the native network snapshot. Budget
        // only the remainder, never double-charge all machines after a restart.
        var remainder = pending.Select((o, i) => new BlueprintSiteObject
        {
            Index = i, ItemId = build.Site.Objects[o.Index].ItemId, Position = build.Site.Objects[o.Index].Position,
        }).ToArray();
        var power = GameStateReader.AssessFoundryPowerOnMainThread(GameMain.data.localLoadedPlanetFactory, remainder, catalog.Value.Buildings);
        return power.AllPlannedConsumersCovered && power.FullBaseLoadBudgetSatisfied && !power.GeometryBoundaryUncertain
            && power.Blockers.Count == 0 ? null : BlueprintError("foundry_remaining_power_not_ready:" + string.Join(";", power.Blockers));
    }

    private static BlueprintBuildState CloneBlueprintState(BlueprintBuildState value) => PluginJson.Deserialize<BlueprintBuildState>(PluginJson.Serialize(value))!;
    private static List<BlueprintObjectProgress> CloneBlueprintObjects(List<BlueprintObjectProgress> value) => PluginJson.Deserialize<List<BlueprintObjectProgress>>(PluginJson.Serialize(value))!;
    private static string BlueprintInventoryHash(Dictionary<int, int> inventory) => CanonicalStateHash.Combine("blueprint-inventory-v1",
        inventory.OrderBy(i => i.Key).Select(i => (object)CanonicalStateHash.Combine("item", i.Key, i.Value)).ToArray());
    private static bool BlueprintInventoryDebit(Dictionary<int, int> before, Dictionary<int, int> after, int itemId) =>
        before.Keys.Concat(after.Keys).Distinct().All(id => GetCount(after, id) == GetCount(before, id) - (id == itemId ? 1 : 0));
    private static BridgeError BlueprintError(string message) => BridgeError.Create(BridgeErrorCodes.BuildConnectionInvalid,
        message, false, "Fresh read blueprint progress, player, native site and material evidence; never replay submitted objects or replace a failed token with a whole-module retry.");

    private sealed partial class NormalActionPlanPayload
    {
        public BlueprintBuildState? BlueprintState { get; set; }
        public string? BlueprintCode { get; private set; }
        public BlueprintSiteRequest? BlueprintSiteRequest { get; private set; }
        public string BlueprintInputHash { get; private set; } = string.Empty;
        public long BlueprintRevision { get; private set; }
        public int BlueprintSubmissionLimit { get; private set; }
        public static NormalActionPlanPayload Blueprint(string sessionId, int planetId, long revision, string playerHash,
            BlueprintBuildState build, string inputHash, int limit, string? code, BlueprintSiteRequest? site, bool cancel) => new NormalActionPlanPayload
        {
            ActionKind = cancel ? NormalActionKinds.CancelBlueprintBuild : NormalActionKinds.BlueprintBuild,
            SessionId = sessionId, PlanetId = planetId, PlayerStateHash = playerHash,
            BlueprintState = CloneBlueprintState(build), BlueprintCode = code, BlueprintSiteRequest = site,
            BlueprintInputHash = inputHash, BlueprintRevision = revision, BlueprintSubmissionLimit = limit,
            ExpectedStateHash = CanonicalStateHash.Combine("finite-blueprint-plan-v1", sessionId, planetId, revision,
                playerHash, build.PlanHash, build.BuildId, inputHash, limit, cancel),
        };
    }
}
