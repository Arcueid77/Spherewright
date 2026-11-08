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
        var position = top.pos + top.rot * topDesc.lapJoint;
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
            MaximumLayers = desc.isLab ? GameMain.history.labLevel : GameMain.history.storageLevel,
        };
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
        if (!snapshot.Complete || snapshot.RootObjectId != plan.RootObjectId
            || snapshot.Layers.Count != plan.ExistingLayerCount + 1
            || !snapshot.Layers.Take(plan.ExistingLayerCount).Select(layer => layer.ObjectId).SequenceEqual(plan.ExistingObjectIds)
            || !NativeStackCompletionMatches(factory, plan, snapshot.TopObjectId))
            return false;
        action.StackConstructionReadback = snapshot;
        return true;
    }
}
