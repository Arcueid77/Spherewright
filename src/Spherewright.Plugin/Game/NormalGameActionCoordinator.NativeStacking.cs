using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private BuildPreparation TryPrepareNativeStackBuild(PlanetFactory factory, Player player,
        ItemProto item, PrepareBuildRequest request)
    {
        var before = _reader.CaptureNativeStack(factory, request.StackOnObjectId!.Value);
        if (!before.Complete || before.StateHash != request.ExpectedStackStateHash)
            return BuildPreparation.Failed(BridgeErrorCodes.StaleState,
                before.ReasonCode ?? "The selected stack changed after inspection.");
        var modelIndex = request.PreferredModelIndex ?? item.ModelIndex;
        var desc = NativeBuildModel(item, modelIndex);
        if (desc?.isDispenser == true && desc.addonType == EAddonType.Storage)
            return TryPrepareStorageAttachmentBuild(factory, player, item, request, before, desc);
        if (desc is null || !desc.multiLevel || string.IsNullOrEmpty(GameStateReader.NativeStackFamily(desc)))
            return BuildPreparation.Failed(BridgeErrorCodes.InvalidRequest,
                "The selected runtime model does not support ordinary vertical stacking.");
        var top = factory.entityPool[before.TopObjectId];
        var topDesc = LDB.models.Select(top.modelIndex)?.prefabDesc;
        if (topDesc is null || top.protoId != item.ID
            && !(desc.multiLevelAlternativeIds ?? Array.Empty<int>()).Contains(top.protoId))
            return BuildPreparation.Failed(BridgeErrorCodes.BuildLocationInvalid,
                "The native model does not permit this lower/upper building combination.");
        if (!desc.multiLevelAllowRotate && request.PreferredYaw.HasValue)
            return BuildPreparation.Failed(BridgeErrorCodes.InvalidRequest,
                "This native stack retains its base orientation; omit preferredYaw.");
        var alternatives = desc.multiLevelAlternativeIds ?? Array.Empty<int>();
        var transposes = desc.multiLevelAlternativeYawTransposes ?? Array.Empty<bool>();
        var alternative = Array.IndexOf(alternatives, top.protoId);
        var transpose = alternative >= 0 && alternative < transposes.Length && transposes[alternative];
        var toolKind = NativeStackingPolicy.PlacementToolKind(item.BuildMode, GameStateReader.NativeStackFamily(desc));
        if (toolKind is null || toolKind == "addon" && !topDesc.isSpraycoster)
            return BuildPreparation.Failed(BridgeErrorCodes.InvalidRequest, "The native stack uses an unsupported placement tool.");
        var addonStack = toolKind == "addon";
        // DSP29151's Addon.DeterminePreviews uses this explicit radial offset
        // for spraycoaters, rather than the model's zero lapJoint.
        var position = addonStack ? top.pos.normalized * (top.pos.magnitude + 2.666667f)
            : top.pos + top.rot * topDesc.lapJoint;
        var rotation = desc.multiLevelAllowRotate
            ? Maths.SphericalRotation(position, request.PreferredYaw ?? 0)
            : transpose ? top.rot * Quaternion.Euler(0, 90, 0) : top.rot;
        var candidate = BuildStepPlan.Core(item.ID, position, rotation, request.PreferredYaw ?? 0);
        candidate.ModelIndex = modelIndex;
        candidate.InputObjectId = before.TopObjectId;
        candidate.InputFromSlot = 15;
        candidate.InputToSlot = 14;
        candidate.StackConstruction = new NativeStackConstructionPlan
        {
            SelectedObjectId = request.StackOnObjectId.Value,
            RootObjectId = before.RootObjectId,
            AnchorObjectId = before.TopObjectId,
            BeforeStackStateHash = before.StateHash,
            BuildingItemId = item.ID,
            ModelIndex = modelIndex,
            ExistingLayerCount = before.Layers.Count,
            ExistingObjectIds = before.Layers.Select(layer => layer.ObjectId).ToList(),
            ExistingAttachmentObjectIds = before.Attachments.Select(attachment => attachment.Entity.ObjectId).ToList(),
            MaximumLayers = desc.isLab ? GameMain.history.labLevel
                : desc.isStorage || desc.isTank || desc.isSplitter ? GameMain.history.storageLevel : null,
            HeightLimitKind = desc.isLab || desc.isStorage || desc.isTank || desc.isSplitter ? "technology" : "native_reach",
            BuildToolKind = toolKind,
        };
        if (addonStack) candidate.Parameters.AddRange(new int[128]);
        if (!TryValidateClickBuild(factory, player, item, candidate, 0, out var accepted, out var reason))
            return BuildPreparation.Failed(BridgeErrorCodes.BuildLocationInvalid, reason);
        var endpoint = _reader.InspectFactoryEntityOnMainThread(_sessions.SessionId,
            new InspectFactoryEntityRequest { PlanetId = factory.planetId, ObjectId = before.TopObjectId });
        if (!endpoint.Success || endpoint.Value is null)
            return BuildPreparation.Failed(BridgeErrorCodes.StaleState, "The native stack anchor became unavailable.");
        var result = BuildPreparation.Succeeded(NormalBuildKinds.Core, new[] { accepted });
        result.SourceObjectId = before.TopObjectId;
        result.SourceEndpointHash = endpoint.Value.EndpointStateHash;
        return result;
    }

    private static PrefabDesc? NativeBuildModel(ItemProto item, int modelIndex)
    {
        if (modelIndex == 0) return item.prefabDesc;
        if (modelIndex < item.ModelIndex || modelIndex - item.ModelIndex >= Math.Max(1, item.ModelCount)) return null;
        return LDB.models.Select(modelIndex)?.prefabDesc;
    }

    private static PrefabDesc BuildStepDescription(BuildStepPlan step, ItemProto item) =>
        NativeBuildModel(item, step.ModelIndex)
        ?? throw new InvalidOperationException("The bound native building model is unavailable.");

    private static bool TryValidateNativeAddonBuild(PlanetFactory factory, Player player, ItemProto item,
        BuildStepPlan candidate, out BuildStepPlan accepted, out string rejection)
    {
        accepted = candidate;
        rejection = string.Empty;
        var plan = candidate.StackConstruction;
        var description = BuildStepDescription(candidate, item);
        if (!(description.isSpraycoster || description.isDispenser && plan is not null
                && NativeStackingPolicy.IsStorageAttachment(plan)) || !BuildUiIsIdle(player))
        {
            rejection = "The exact idle native addon stack is unavailable.";
            return false;
        }
        if (OverlapsExistingFactoryObject(factory, BuildStepDescription(candidate, item), candidate.Position,
            candidate.Rotation, out var occupied, plan?.AnchorObjectId ?? 0, item.ID))
        {
            rejection = "The addon stack overlaps another native object:" + occupied;
            return false;
        }
        var tool = new SpherewrightAddonBuildTool();
        tool._Init(GameMain.data);
        tool.SetFactoryReferences();
        using var ui = new NativeBuildPreviewUiScope();
        try
        {
            if (!ReferenceEquals(tool.factory, factory))
            {
                rejection = "The isolated native addon validator is not bound to the local factory.";
                return false;
            }
            tool.handItem = item;
            tool.handPrefabDesc = BuildStepDescription(candidate, item);
            tool.SnapshotPlayerInventory();
            var preview = CreatePreview(candidate, item);
            tool.buildPreviews.Add(preview);
            tool.BindPreparedPose(preview);
            if (!tool.CheckPreparedBuildConditions() || preview.condition != EBuildCondition.Ok
                || preview.coverObjId != 0 || preview.willRemoveCover)
            {
                rejection = "Native addon stack validation returned " + preview.condition;
                return false;
            }
            accepted = BuildStepPlan.FromPreview(candidate, preview);
            return true;
        }
        finally
        {
            tool.ReleaseSnapshot();
            tool.buildPreviews.Clear();
            tool._Free();
        }
    }

    private bool NativeStackAnchorMatches(PlanetFactory factory, NativeStackConstructionPlan? plan)
    {
        if (plan is null) return true;
        var current = _reader.CaptureNativeStack(factory, plan.SelectedObjectId);
        return current.Complete && current.StateHash == plan.BeforeStackStateHash
            && current.RootObjectId == plan.RootObjectId && current.TopObjectId == plan.AnchorObjectId;
    }

    private static bool NativeStackCompletionMatches(PlanetFactory factory, NativeStackConstructionPlan plan, int entityId)
    {
        if (entityId <= 0 || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length)
            return false;
        var entity = factory.entityPool[entityId];
        if (entity.id != entityId || entity.protoId != plan.BuildingItemId || entity.modelIndex != plan.ModelIndex)
            return false;
        factory.ReadObjectConn(entityId, plan.InputToSlot, out var inputOutput, out var parent, out var parentSlot);
        factory.ReadObjectConn(plan.AnchorObjectId, plan.InputFromSlot, out var output, out var child, out var childSlot);
        return !inputOutput && parent == plan.AnchorObjectId && parentSlot == plan.InputFromSlot
            && output && child == entityId && childSlot == plan.InputToSlot;
    }

    private bool CaptureCompletedNativeStack(ActionRecord action)
    {
        var plan = action.Plan.BuildSteps.FirstOrDefault()?.StackConstruction
            ?? action.BlueprintBuild?.StructuredConstruction?.StackConstruction;
        if (plan is null) return true;
        var factory = GameMain.localPlanet?.factory;
        if (factory is null) return false;
        var snapshot = _reader.CaptureNativeStack(factory, plan.SelectedObjectId);
        if (!NativeStackingPolicy.ConfirmsCompletedMembers(plan, snapshot, out var createdObjectId)
            || NativeStackingPolicy.IsStorageAttachment(plan) && !action.TargetObjectIds.Contains(createdObjectId)
            || !NativeStackCompletionMatches(factory, plan, createdObjectId))
            return false;
        action.StackConstructionReadback = snapshot;
        return true;
    }

    private BuildPreparation TryPrepareStorageAttachmentBuild(PlanetFactory factory, Player player, ItemProto item,
        PrepareBuildRequest request, NativeStackSnapshot before, PrefabDesc description)
    {
        if (item.ID != 2107 || item.BuildMode != 5 || request.PreferredYaw.HasValue
            || before.Attachments.Count != 0
            || _flightCheckpoints.VoyagerJourney is { } journey && !IsVoyagerJourneyClosed(journey))
            return BuildPreparation.Failed(BridgeErrorCodes.InvalidRequest,
                "Storage attachment requires a closed journey, an attachment-free stack and the native host orientation.");
        var host = factory.entityPool[before.TopObjectId];
        var hostDescription = LDB.models.Select(host.modelIndex)?.prefabDesc;
        if (hostDescription is null || !hostDescription.isStorage || hostDescription.isBattleBase
            || hostDescription.colliders is null || hostDescription.colliders.Length == 0)
            return BuildPreparation.Failed(BridgeErrorCodes.InvalidRequest,
                "The native distributor requires an ordinary built storage host.");
        // Exact BuildTool_Addon.DeterminePreviews storage-addon pose and SetDefaultParams.
        var height = hostDescription.colliders[0].pos.y + hostDescription.colliders[0].ext.y;
        if (!IsFinite(height) || height <= 0 || description.workEnergyPerTick < 0
            || description.workEnergyPerTick > int.MaxValue)
            return BuildPreparation.Failed(BridgeErrorCodes.InvalidRequest, "The native attachment metadata is unavailable.");
        var candidate = BuildStepPlan.Core(item.ID, host.pos.normalized * (host.pos.magnitude + height), host.rot, 0);
        candidate.ModelIndex = request.PreferredModelIndex ?? item.ModelIndex;
        candidate.InputObjectId = host.id;
        candidate.InputFromSlot = 13;
        candidate.InputToSlot = 0;
        candidate.Parameters.AddRange(new int[128]);
        candidate.Parameters[0] = 2;
        candidate.Parameters[2] = (int)description.workEnergyPerTick;
        candidate.StackConstruction = new NativeStackConstructionPlan
        {
            SelectedObjectId = request.StackOnObjectId!.Value, RootObjectId = before.RootObjectId,
            AnchorObjectId = host.id, BeforeStackStateHash = before.StateHash,
            BuildingItemId = item.ID, ModelIndex = candidate.ModelIndex,
            ExistingLayerCount = before.Layers.Count, ExistingObjectIds = before.Layers.Select(layer => layer.ObjectId).ToList(),
            MaximumLayers = null, HeightLimitKind = "native_reach", BuildToolKind = "addon",
            InputFromSlot = 13, InputToSlot = 0,
        };
        if (!TryValidateClickBuild(factory, player, item, candidate, 0, out var accepted, out var reason))
            return BuildPreparation.Failed(BridgeErrorCodes.BuildLocationInvalid, reason);
        var endpoint = _reader.InspectFactoryEntityOnMainThread(_sessions.SessionId,
            new InspectFactoryEntityRequest { PlanetId = factory.planetId, ObjectId = host.id });
        if (!endpoint.Success || endpoint.Value is null)
            return BuildPreparation.Failed(BridgeErrorCodes.StaleState, "The native storage host became unavailable.");
        var result = BuildPreparation.Succeeded(NormalBuildKinds.Core, new[] { accepted });
        result.SourceObjectId = host.id;
        result.SourceEndpointHash = endpoint.Value.EndpointStateHash;
        return result;
    }
}
