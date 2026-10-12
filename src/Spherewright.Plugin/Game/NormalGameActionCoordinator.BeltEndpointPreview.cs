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
    private GameCallResult<PreparedNormalAction> PrepareBeltEndpointPreviewOnMainThread(
        PrepareBuildRequest request, BuildPreparation preparation, PlayerStateSnapshot playerBefore,
        BeltRoutePreviewSnapshot? route = null)
    {
        var factory = GameMain.localPlanet!.factory;
        var player = GameMain.mainPlayer;
        var parameters = request.BeltEndpointPreview ?? new BeltEndpointPreviewRequest{
            Source=request.BeltRoutePreview!.Source,Destination=request.BeltRoutePreview.Destination,
            FilterItemId=request.BeltRoutePreview.FilterItemId,SorterItemId=request.BeltRoutePreview.SourceSorterItemId};
        if (!BuildUiIsIdle(player) || preparation.Kind != NormalBuildKinds.Belt
            || preparation.Steps.Count < 2 || preparation.Steps.Count > (route is null?BeltEndpointPreviewPolicy.MaximumBeltObjects:BeltRoutePreviewPolicy.MaximumPoints)
            || preparation.Steps.Any(s => s.InputObjectId != 0 || s.OutputObjectId != 0
                || s.SourceBeltAnchor is not null || s.DestinationBeltAnchor is not null)
            || route is null && Vector3.Distance(preparation.Steps[0].Position, preparation.Steps[preparation.Steps.Count - 1].Position) > 30f
            || factory.entityCursor > 131072 || factory.prebuildCursor > 131072)
            return InvalidPlan("belt_endpoint_preview_span_or_factory_limit");
        var count = (parameters.Source is null ? 0 : 1) + (parameters.Destination is null ? 0 : 1);
        var grades=route is null?Enumerable.Repeat(parameters.SorterItemId,count):new[]{request.BeltRoutePreview!.SourceSorterItemId,request.BeltRoutePreview.DestinationSorterItemId};
        foreach(var group in grades.GroupBy(id=>id))
        {
            var prototype=LDB.items.Select(group.Key);
            if(prototype?.prefabDesc is null || !prototype.prefabDesc.isInserter) return InvalidPlan("belt_endpoint_preview_sorter_prototype_unavailable");
            var filterError=ValidateInitialSorterFilter(prototype,parameters.FilterItemId);
            if(filterError is not null) return GameCallResult<PreparedNormalAction>.Failed(filterError);
            if(!GameMain.history.ItemUnlocked(prototype.ID)) return InvalidPlan("belt_endpoint_preview_sorter_technology_locked");
            if(player.package.GetItemCount(prototype.ID)<group.Count()) return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(BridgeErrorCodes.InventoryInsufficient,
                "The NEW route endpoint preview lacks its complete sorter budget. No joint native placement pass is claimed.",
                true, "Supply the declared materials normally only after the whole stage is executable, then fresh-prepare."));
        }

        var snapshot = new BeltEndpointPreviewSnapshot
        {
            SessionId = _sessions.SessionId!, PlanetId = request.PlanetId, Revision = _sessions.Revision,
            CapturedAtGameTick = GameMain.gameTick,
        };
        var response = route is not null?CreateReadOnlyRouteResponse(request,route,preparation.Steps):new PreparedNormalAction
        {
            Prepared = false, CommitAllowedNow = false, PlanToken = string.Empty, ActionKind = NormalActionKinds.Build,
            ExpectedStateHash = playerBefore.StateHash,
            BuildKind = "belt_endpoint_preview", BeltEndpointPreview = snapshot,
            PlannedPath = preparation.Steps.Select(s => Snapshot(s.Position)).ToList(),
            PlannedBeltPath = new BeltPathPlanSnapshot
            {
                NativeValidationMode = "full_path_stage1", RoutingMode = request.BeltPathMode,
                SourceBindingMode = "none", DestinationBindingMode = "none", NewObjectCount = preparation.Steps.Count,
                StartAltitudeLevel = request.BeltPathMode == BeltPathModes.NativeElevatedGrid ? request.BeltStartAltitudeLevel : null,
                EndAltitudeLevel = request.BeltPathMode == BeltPathModes.NativeElevatedGrid ? request.BeltEndAltitudeLevel : null,
            },
            CompletionCondition = "Read-only native qualification only: no prebuild, action, token or whole-chain production proof.",
        };
        if(route is null)
        {
            response.ItemBudget.Add(new ActionItemBudget { ItemId = 2001, Count = preparation.Steps.Count,
                Name = LDB.items.Select(2001).name, Direction = "preview-construction-consumption" });
            response.ItemBudget.Add(new ActionItemBudget { ItemId = parameters.SorterItemId, Count = count, Name = LDB.items.Select(parameters.SorterItemId).name,
                Direction = "preview-construction-consumption" });
        }
        else route.Endpoints=snapshot;
        var belts = CreateLinkedPreviews(preparation.Steps, LDB.items.Select(2001));
        var sorters = new List<BuildPreview>();
        var mirrors = new List<BuildPreview>();
        var bindings = new List<(PlannedBeltEndpointBinding Binding, FactoryEntitySnapshot Existing)>();
        var toolOwnsSorters = false;
        var toolOwnsBelts = false;
        try
        {
            foreach (var role in new[] { "source", "destination" })
            {
                var binding = role == "source" ? parameters.Source : parameters.Destination;
                if (binding is null) continue;
                if (!TryReadBuildEndpoint(request, binding.ExistingObjectId, binding.ExpectedEndpointStateHash,
                        out var existing, out var error)
                    || existing!.EndpointStateHash != binding.ExpectedEndpointStateHash)
                    return StalePlan(error ?? "The exact existing endpoint hash changed.");
                bindings.Add((binding, existing));
                var points = GetInserterEndpointPoints(factory, existing);
                var isBelt = factory.entityPool[existing.ObjectId].beltId > 0;
                var selected = points.Where(p => p.Slot == binding.ExistingSlot).ToArray();
                if ((isBelt && (binding.ExistingSlot != -1 || selected.Length != 4))
                    || (!isBelt && (binding.ExistingSlot < 0 || selected.Length != 1)))
                    return InvalidPlan("The specified existing slot is occupied, unavailable or of the wrong endpoint kind.");
                var actual = selected[isBelt ? binding.ExistingBeltQuarterTurns : 0];
                var source = role == "source";
                var sorter=LDB.items.Select(route is null?parameters.SorterItemId:source?request.BeltRoutePreview!.SourceSorterItemId:request.BeltRoutePreview!.DestinationSorterItemId);
                var index = source ? 0 : belts.Count - 1;
                var belt = belts[index];
                var rotation = Quaternion.AngleAxis(belt.tilt, belt.lrot * Vector3.forward) * belt.lrot
                    * Quaternion.Euler(0, binding.PlannedBeltQuarterTurns * 90f, 0);
                var plannedPose = new Pose(belt.lpos, rotation);
                var step = source
                    ? BuildStepPlan.Inserter(sorter.ID, actual.Pose, plannedPose, existing.ObjectId, actual.Slot, 0, -1)
                    : BuildStepPlan.Inserter(sorter.ID, plannedPose, actual.Pose, 0, -1, existing.ObjectId, actual.Slot);
                step.FilterItemId = parameters.FilterItemId;
                var preview = CreatePreview(step, sorter);
                ref var entity = ref factory.entityPool[existing.ObjectId];
                // Exact deep-copied reference to a REAL owned entity, outside bpPool.
                // Zero cover flags are essential: no covering/reservation/write branch.
                // Both references prevent native MatchInserter from replacing our binding.
                var mirror = new BuildPreview
                {
                    objId = existing.ObjectId, item = LDB.items.Select(entity.protoId),
                    desc = LDB.items.Select(entity.protoId).prefabDesc, lpos = entity.pos, lpos2 = entity.pos,
                    lrot = entity.rot, lrot2 = entity.rot, tilt = entity.tilt, condition = EBuildCondition.Ok,
                    needModel = false, isConnNode = isBelt,
                };
                mirrors.Add(mirror);
                preview.input = source ? mirror : belt;
                preview.output = source ? belt : mirror;
                sorters.Add(preview);
                snapshot.Attachments.Add(new PlannedBeltEndpointAttachment
                {
                    Role = role, ExistingObjectId = existing.ObjectId, EndpointStateHash = existing.EndpointStateHash,
                    ExistingItemId = existing.ItemId, ObservedRecipeId = existing.RecipeId, PlannedBeltIndex = index,
                    SorterItemId = sorter.ID, FilterItemId = parameters.FilterItemId,
                    ExistingBeltQuarterTurns = binding.ExistingBeltQuarterTurns, PlannedBeltQuarterTurns = binding.PlannedBeltQuarterTurns,
                    Attachment = new InserterAttachmentPlanSnapshot
                    {
                        Mode = "read_only_planned_belt_exact_slots", SourceSlot = step.InputFromSlot,
                        DestinationSlot = step.OutputToSlot, SourcePosition = Snapshot(step.Position),
                        DestinationPosition = Snapshot(step.Position2),
                    },
                });
                if (!NativeInserterEndpointGeometry.AcceptsStraightPair(Snapshot(step.Position2 - step.Position),
                    Snapshot(step.Rotation * Vector3.forward), Snapshot(step.Rotation2 * Vector3.back), true))
                    snapshot.Blockers.Add("planned_endpoint_TooSkew:" + role);
            }

            var occupancyBlocker = snapshot.Blockers.Count == 0
                ? PreviewSorterOccupancyBlocker(factory, sorters, belts) : null;
            if (occupancyBlocker is not null)
            {
                snapshot.Blockers.Add("planned_endpoint_collision_or_prototype_unavailable");
                snapshot.Blockers.Add(occupancyBlocker);
            }
            if(route is not null && ReadOnlyRouteCoverBlocker(factory,belts) is { } coverBlocker)
                snapshot.Blockers.Add(coverBlocker);
            // These are PRE-call guards: detecting a covered reference only after
            // native checking would be too late to prevent connection reservations.
            if (sorters.Any(p => !p.desc.isInserter || p.input is null || p.output is null)
                || sorters.Concat(mirrors).Concat(belts).Any(p => p.coverObjId != 0 || p.coverbp is not null
                    || p.willRemoveCover || p.willReconstructCover))
                snapshot.Blockers.Add("native_preview_cover_or_implicit_matching_forbidden");
            if (snapshot.Blockers.Count == 0)
            {
                using var tool = new SpherewrightBlueprintBuildTool();
                tool._Init(GameMain.data!);
                tool.SetFactoryReferences();
                if (!ReferenceEquals(tool.factory, factory)) return NotReadyPlan("Native linked preview factory mismatch.");
                if(route is null) tool.SetReadOnlyLinkedSorters(sorters, belts.Count + sorters.Count);
                else {tool.SetReadOnlyLinkedObjects(belts.Concat(sorters).ToArray());toolOwnsBelts=true;route.JointNativeCheckPerformed=true;}
                toolOwnsSorters = true;
                var exact = sorters.Select(p => (Input: p.input, Output: p.output,
                    Rotation: p.lrot, Rotation2: p.lrot2, Tilt: p.tilt)).ToArray();
                snapshot.NativeCheckPerformed = true;
                var valid = tool.CheckNewObjects();
                for (var i = 0; i < sorters.Count; i++)
                {
                    var p = sorters[i]; var a = snapshot.Attachments[i];
                    a.NativeCondition = p.condition.ToString();
                    a.NativeSpan = p.paramCount == 1 && p.parameters is { Length: > 0 } ? p.parameters[0] : 0;
                    var source = a.Role == "source";
                    if (p.input is null || p.output is null
                        || !ReferenceEquals(p.input, exact[i].Input) || !ReferenceEquals(p.output, exact[i].Output)
                        || p.inputObjId != (source ? a.ExistingObjectId : 0)
                        || p.outputObjId != (source ? 0 : a.ExistingObjectId)
                        || p.inputFromSlot != a.Attachment.SourceSlot || p.outputToSlot != a.Attachment.DestinationSlot
                        || p.inputToSlot != 1 || p.outputFromSlot != 0
                        || Quaternion.Angle(p.lrot, exact[i].Rotation) > .1f
                        || Quaternion.Angle(p.lrot2, exact[i].Rotation2) > .1f
                        || Math.Abs(p.tilt - exact[i].Tilt) > .001f
                        || p.coverObjId != 0 || p.coverbp is not null || p.willRemoveCover || p.willReconstructCover
                        || p.input.coverObjId != 0 || p.output.coverObjId != 0 || p.filterId != a.FilterItemId
                        || p.inputOffset != 0 || p.outputOffset != 0
                        || Vector3.Distance(p.lpos, ToVector(a.Attachment.SourcePosition)) > .001f
                        || Vector3.Distance(p.lpos2, ToVector(a.Attachment.DestinationPosition)) > .001f
                        || a.NativeSpan < 1 || a.NativeSpan > 3)
                        snapshot.Blockers.Add("native_changed_exact_endpoint_binding:" + a.Role);
                }
                if (!valid || snapshot.Attachments.Any(a => a.NativeCondition != "Ok"))
                    snapshot.Blockers.Add("native_planned_endpoint_rejected");
                if (!PreviewsExactlyMatch(preparation.Steps, belts))
                    snapshot.Blockers.Add("native_changed_prevalidated_belt_span");
                for (var i = 0; i < mirrors.Count; i++)
                {
                    var p = mirrors[i];
                    ref var entity = ref factory.entityPool[bindings[i].Existing.ObjectId];
                    if (p.objId != entity.id || p.item.ID != entity.protoId
                        || p.coverObjId != 0 || p.coverbp is not null || p.willRemoveCover || p.willReconstructCover
                        || Vector3.Distance(p.lpos, entity.pos) > .001f || Vector3.Distance(p.lpos2, entity.pos) > .001f
                        || Quaternion.Angle(p.lrot, entity.rot) > .1f || Quaternion.Angle(p.lrot2, entity.rot) > .1f
                        || Math.Abs(p.tilt - entity.tilt) > .001f || p.condition != EBuildCondition.Ok)
                        snapshot.Blockers.Add("native_changed_existing_endpoint_reference:" + snapshot.Attachments[i].Role);
                }
                snapshot.NativeCheckPassed = valid && snapshot.Blockers.Count == 0;
                if (route is not null)
                {
                    // Capture the native results while the tool still owns live
                    // previews. Its disposal frees every object in the pool.
                    route.Points = belts.Select((p, i) => new NativeBeltRoutePoint
                    {
                        Index = i, PlannedPosition = route.Points[i].PlannedPosition,
                        Position = Snapshot(p.lpos), Rotation = NativeFactoryGeometry.Rotation(p.lrot),
                        Tilt = p.tilt, NativeCondition = p.condition.ToString(),
                    }).ToList();
                    var failed = belts.FindIndex(p => p.condition != EBuildCondition.Ok);
                    if (failed >= 0)
                    {
                        route.FailedBeltIndex = failed;
                        route.FailureCondition = belts[failed].condition.ToString();
                        snapshot.Blockers.Add("native_joint_belt_rejected:" + failed);
                    }
                    route.JointNativeCheckPassed = snapshot.NativeCheckPassed && failed < 0;
                    snapshot.NativeCheckPassed = route.JointNativeCheckPassed;
                }
            }
            if(route is not null)
            {
                if(!route.JointNativeCheckPassed)
                {
                    route.FailureStage=route.JointNativeCheckPerformed?"joint_native":"pre_native_endpoint_guard";
                    route.FailureCondition ??= snapshot.Blockers.FirstOrDefault()??"native_joint_check_rejected";
                    if(snapshot.Blockers.Count==0) snapshot.Blockers.Add(route.FailureCondition);
                }
            }
            foreach (var b in bindings)
                if (!TryReadBuildEndpoint(request, b.Binding.ExistingObjectId, b.Binding.ExpectedEndpointStateHash,
                    out var after, out _) || after!.EndpointStateHash != b.Existing.EndpointStateHash)
                    return StalePlan("The existing endpoint changed during read-only qualification; no native positive is returned.");
            var playerAfter = _reader.GetPlayerStateOnMainThread(_sessions.SessionId,
                new LocalPlanetRequest { PlanetId = request.PlanetId });
            if (!playerAfter.Success || playerAfter.Value!.StateHash != playerBefore.StateHash)
                return StalePlan("Player/inventory changed during read-only qualification; no native positive is returned.");
            snapshot.AssessmentHash = CanonicalStateHash.Combine(BeltEndpointPreviewPolicy.Phase,
                snapshot.SessionId, snapshot.PlanetId, snapshot.Revision, playerBefore.StateHash,
                BuildPlanFingerprint(preparation), parameters.SorterItemId, parameters.FilterItemId,
                string.Join(";", snapshot.Attachments.Select(a => a.Role + ":" + a.ExistingObjectId + ":" + a.EndpointStateHash
                    + ":" + a.Attachment.SourceSlot + ":" + a.Attachment.DestinationSlot + ":" + a.ExistingBeltQuarterTurns
                    + ":" + a.PlannedBeltQuarterTurns + ":" + a.SorterItemId + ":" + a.NativeCondition + ":" + a.NativeSpan)),
                snapshot.NativeCheckPerformed, snapshot.NativeCheckPassed, string.Join(";", snapshot.Blockers));
            return GameCallResult<PreparedNormalAction>.Succeeded(response);
        }
        finally
        {
            if (!toolOwnsSorters) foreach (var p in sorters) p.Free();
            foreach (var p in mirrors) p.Free();
            if(!toolOwnsBelts) foreach (var p in belts) p.Free();
        }
    }

    private static string? ReadOnlyRouteCoverBlocker(PlanetFactory factory,IReadOnlyList<BuildPreview> belts)
    {
        // The established blueprint no-cover guard is stricter than the belt
        // centre guard: eliminate possible native cover matching BEFORE checking.
        for(var id=1;id<factory.entityCursor;id++)
        {
            var e=factory.entityPool[id]; if(e.id!=id) continue;
            if(belts.Any(b=>(b.lpos-e.pos).sqrMagnitude<=1f)) return "read_only_route_existing_cover_risk:"+id;
        }
        for(var id=1;id<factory.prebuildCursor;id++)
        {
            var p=factory.prebuildPool[id]; if(p.id!=id) continue;
            if(belts.Any(b=>(b.lpos-p.pos).sqrMagnitude<=1f)) return "read_only_route_prebuild_cover_risk:"+id;
        }
        // Native blueprint belt-cover matching is also reachable from its
        // 0.23m sphere against a long existing belt collider. Centre distance
        // alone cannot exclude that branch. Read the SAME native physics query
        // and reject every existing-belt hit before CheckBuildConditions can
        // reserve (WriteObjectConn) or clear a covered object's connection.
        var physics = factory.planet.physics;
        if (physics is null) return "read_only_route_native_physics_unavailable";
        var colliders = new Collider[256];
        for (var index = 0; index < belts.Count; index++)
        {
            var position = belts[index].lpos;
            Array.Clear(colliders, 0, colliders.Length);
            var count = Physics.OverlapSphereNonAlloc(position + position.normalized * .2f, .23f,
                colliders, 395264, QueryTriggerInteraction.Collide);
            if (count >= colliders.Length) return "read_only_route_native_cover_query_saturated:" + index;
            for (var hit = 0; hit < count; hit++)
            {
                if (!physics.GetColliderData(colliders[hit], out var data))
                    return "read_only_route_native_collider_identity_unavailable:" + index;
                if (!data.isForBuild) continue;
                var id = data.objType == EObjectType.None ? data.objId : data.objType == EObjectType.Prebuild ? -data.objId : 0;
                if (id == 0) continue;
                var slot = Math.Abs(id);
                if (id > 0 ? slot >= factory.entityCursor || factory.entityPool[slot].id != slot
                    : slot >= factory.prebuildCursor || factory.prebuildPool[slot].id != slot)
                    return "read_only_route_native_collider_object_unavailable:" + index;
                var model = id > 0 ? factory.entityPool[slot].modelIndex : factory.prebuildPool[slot].modelIndex;
                var desc = LDB.models.Select(model)?.prefabDesc;
                if (desc is null) return "read_only_route_native_collider_prototype_unavailable:" + index;
                if (desc.isBelt) return "read_only_route_native_belt_cover_risk:" + index + ":" + id;
            }
        }
        return null;
    }

    private static string? PreviewSorterOccupancyBlocker(PlanetFactory factory,
        IReadOnlyList<BuildPreview> sorters, IReadOnlyList<BuildPreview> belts)
    {
        var volumes = sorters.Select(BlueprintPreviewColliders).ToArray();
        for (var i = 0; i < sorters.Count; i++)
        {
            for (var j = 0; j < i; j++)
                if (BlueprintColliderSetsOverlap(volumes[i], volumes[j]))
                    return $"planned_endpoint_sorter_overlap:sorter:{i}:sorter:{j}";
            for (var j = 0; j < belts.Count; j++)
                if (!BlueprintLinkedSorterPair(sorters[i], belts[j])
                    && BlueprintColliderSetsOverlap(volumes[i], BlueprintPreviewColliders(belts[j])))
                    return $"planned_endpoint_unlinked_belt_overlap:sorter:{i}:belt:{j}";
        }
        for (var id = 1; id < factory.entityCursor && id < factory.entityPool.Length; id++)
        {
            ref var entity = ref factory.entityPool[id];
            if (entity.id != id) continue;
            var blocker = ExistingBlocker(id, entity.protoId, entity.pos, entity.rot);
            if (blocker is not null) return blocker;
        }
        for (var id = 1; id < factory.prebuildCursor && id < factory.prebuildPool.Length; id++)
        {
            ref var p = ref factory.prebuildPool[id];
            if (p.id != id) continue;
            var blocker = ExistingBlocker(-id, p.protoId, p.pos, p.rot);
            if (blocker is not null) return blocker;
        }
        return null;

        string? ExistingBlocker(int id, int proto, Vector3 pos, Quaternion rot)
        {
            var desc = LDB.items.Select(proto)?.prefabDesc;
            if (desc is null)
                return $"planned_endpoint_prototype_unavailable:object:{id}:proto:{proto}";
            var existing = CreateWorldBuildColliders(desc, pos, rot);
            for (var i = 0; i < sorters.Count; i++)
                if (sorters[i].inputObjId != id && sorters[i].outputObjId != id
                    && BlueprintColliderSetsOverlap(volumes[i], existing))
                    return $"planned_endpoint_existing_overlap:sorter:{i}:object:{id}";
            return null;
        }
    }
}
