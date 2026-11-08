using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static BuildPreview BlueprintStepPreview(BlueprintBuildState build, int index)
    {
        var step = build.Site.Objects[index];
        var item = LDB.items.Select(step.ItemId);
        var result = new BuildPreview
        {
            item = item, desc = NativeBuildModel(item, step.NativeModelIndex ?? 0)
                ?? throw new InvalidOperationException("The approved native model is unavailable."),
            lpos = ToVector(step.Position), lpos2 = ToVector(step.Position2),
            lrot = BlueprintRotation(step.Rotation), lrot2 = BlueprintRotation(step.Rotation2), tilt = step.Tilt,
            recipeId = step.RecipeId, filterId = step.FilterItemId, inputOffset = step.InputOffset,
            outputOffset = step.OutputOffset, parameters = step.Parameters.ToArray(), paramCount = step.Parameters.Length,
            condition = EBuildCondition.Ok, isConnNode = item.prefabDesc.isBelt, needModel = false,
        };
        // Only this object's own native fields are written. Belt-to-sorter pickup edges
        // belong to the sorter and must never overwrite the belt's downstream output.
        var output = BlueprintSitePolicy.CreationOutput(build.Site, index);
        if (output is not null)
        {
            result.outputObjId = build.Objects[output.ToIndex].EntityId.GetValueOrDefault();
            result.outputFromSlot = output.FromSlot; result.outputToSlot = output.ToSlot;
        }
        var input = BlueprintSitePolicy.CreationInput(build.Site, index);
        if (input is not null)
        {
            result.inputObjId = build.Objects[input.FromIndex].EntityId.GetValueOrDefault();
            result.inputFromSlot = input.FromSlot; result.inputToSlot = input.ToSlot;
        }
        return result;
    }

    private static Quaternion BlueprintRotation(QuaternionSnapshot q) => new Quaternion(q.X, q.Y, q.Z, q.W);

    private static string BlueprintPreviewHash(BuildPreview p) => CanonicalStateHash.Combine("native-blueprint-step-v1",
        p.item.ID, p.lpos.x, p.lpos.y, p.lpos.z, p.lpos2.x, p.lpos2.y, p.lpos2.z,
        p.lrot.x, p.lrot.y, p.lrot.z, p.lrot.w, p.lrot2.x, p.lrot2.y, p.lrot2.z, p.lrot2.w,
        p.recipeId, p.filterId, p.tilt, p.inputObjId, p.inputFromSlot, p.inputToSlot, p.inputOffset,
        p.outputObjId, p.outputFromSlot, p.outputToSlot, p.outputOffset,
        CanonicalStateHash.Combine("parameters", (p.parameters ?? Array.Empty<int>()).Take(p.paramCount).Cast<object>().ToArray()));

    private static bool BlueprintNewSiteClear(PlanetFactory factory, BuildPreview preview)
    {
        if (factory.entityCursor > 131072 || factory.prebuildCursor > 131072) return false;
        var volumes = BlueprintPreviewColliders(preview);
        for (var id = 1; id < factory.entityCursor && id < factory.entityPool.Length; id++)
        {
            ref var e = ref factory.entityPool[id];
            if (e.id != id || (preview.desc.isInserter && (id == preview.inputObjId || id == preview.outputObjId))) continue;
            if (Overlaps(e.protoId, e.pos, e.rot)) return false;
        }
        for (var id = 1; id < factory.prebuildCursor && id < factory.prebuildPool.Length; id++)
        {
            ref var p = ref factory.prebuildPool[id];
            if (p.id == id && Overlaps(p.protoId, p.pos, p.rot)) return false;
        }
        return true;
        bool Overlaps(int itemId, Vector3 position, Quaternion rotation)
        {
            var desc = LDB.items.Select(itemId)?.prefabDesc;
            return desc is null || (preview.lpos - position).sqrMagnitude <= 1f
                || BlueprintColliderSetsOverlap(volumes, CreateWorldBuildColliders(desc, position, rotation));
        }
    }

    // Isolated ordinary construction tools. Native blueprint placement supplies the poses,
    // then one of the already used Click/Path/Inserter business paths owns each real debit,
    // prebuild, connection and drone assignment. Never call AddPrebuild/BuildFinally here.
    private sealed class BlueprintNativeStep : IDisposable
    {
        private readonly BuildTool _tool;
        private readonly Func<bool> _check;
        private readonly Action _create;
        private readonly Action _releaseSnapshot;
        private readonly NativeBuildPreviewUiScope _ui;
        public BuildPreview Preview { get; }

        public BlueprintNativeStep(BuildPreview preview)
        {
            Preview = preview;
            _ui = new NativeBuildPreviewUiScope();
            if (preview.desc.isBelt)
            {
                var tool = new SpherewrightPathBuildTool(); _tool = tool;
                tool.handItem = preview.item; tool.handPrefabDesc = preview.desc;
                _check = tool.CheckBuildConditions; _create = tool.CreatePrebuilds; _releaseSnapshot = tool.ReleaseSnapshot;
                Initialize(() => { tool._Init(GameMain.data); tool.SetFactoryReferences(); tool.SnapshotPlayerInventory(); });
                tool.startObjectId = preview.inputObjId;
            }
            else if (preview.desc.isInserter)
            {
                var tool = new SpherewrightInserterBuildTool(); _tool = tool;
                tool.handItem = preview.item; tool.handPrefabDesc = preview.desc;
                _check = tool.CheckBuildConditions; _create = tool.CreatePrebuilds; _releaseSnapshot = tool.ReleaseSnapshot;
                Initialize(() => { tool._Init(GameMain.data); tool.SetFactoryReferences(); tool.SnapshotPlayerInventory(); });
                tool.startObjectId = preview.inputObjId; tool.castObjectId = preview.outputObjId;
            }
            else
            {
                var tool = new SpherewrightClickBuildTool(); _tool = tool;
                tool.handItem = preview.item; tool.handPrefabDesc = preview.desc;
                _check = tool.CheckBuildConditions; _create = tool.CreatePrebuilds; _releaseSnapshot = tool.ReleaseSnapshot;
                Initialize(() => { tool._Init(GameMain.data); tool.SetFactoryReferences(); tool.SnapshotPlayerInventory(); });
            }
            _tool.buildPreviews.Add(preview);
        }

        public bool Check()
        {
            var before = BlueprintPreviewHash(Preview);
            return ReferenceEquals(_tool.factory, GameMain.data.localLoadedPlanetFactory)
                && _check() && Preview.condition == EBuildCondition.Ok && Preview.coverObjId == 0
                && !Preview.willRemoveCover && !Preview.willReconstructCover && Preview.addonObjId == 0
                && Preview.coverbp is null && before == BlueprintPreviewHash(Preview);
        }
        public void Create() => _create();
        private void Initialize(Action initialize)
        {
            try { initialize(); }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            try
            {
                _tool.buildPreviews?.Clear();
                try { _releaseSnapshot(); }
                finally { _tool._Free(); }
            }
            finally { _ui.Dispose(); }
        }
    }

    private static bool BlueprintPrebuildMatches(PlanetFactory factory, int id, BlueprintSiteObject spec)
    {
        if (id < 1 || id >= factory.prebuildCursor || id >= factory.prebuildPool.Length) return false;
        ref var p = ref factory.prebuildPool[id];
        var belt = LDB.items.Select(spec.ItemId).prefabDesc.isBelt;
        var rotation = belt ? Maths.SphericalRotation(ToVector(spec.Position), 0) : BlueprintRotation(spec.Rotation);
        return p.id == id && !p.isDestroyed && p.protoId == spec.ItemId && p.itemRequired == 0
            && (!spec.NativeModelIndex.HasValue || p.modelIndex == spec.NativeModelIndex.Value)
            && (p.pos - ToVector(spec.Position)).sqrMagnitude < 0.00001f
            && (p.pos2 - ToVector(belt ? spec.Position : spec.Position2)).sqrMagnitude < 0.00001f
            && Quaternion.Angle(p.rot, rotation) < .1f
            && Quaternion.Angle(p.rot2, belt ? rotation : BlueprintRotation(spec.Rotation2)) < .1f
            && Math.Abs(p.tilt - spec.Tilt) < .001f && p.recipeId == spec.RecipeId && p.filterId == spec.FilterItemId
            && p.pickOffset == (belt ? 0 : spec.InputOffset) && p.insertOffset == (belt ? 0 : spec.OutputOffset)
            && (p.parameters ?? Array.Empty<int>()).Take(p.paramCount).SequenceEqual(spec.Parameters);
    }

    private static bool BlueprintEntityMatches(PlanetFactory factory, int id, BlueprintSiteObject spec)
    {
        if (id < 1 || id >= factory.entityCursor || id >= factory.entityPool.Length) return false;
        ref var e = ref factory.entityPool[id];
        if (e.id != id || e.protoId != spec.ItemId || (e.pos - ToVector(spec.Position)).sqrMagnitude > .00001f) return false;
        var desc = LDB.items.Select(spec.ItemId).prefabDesc;
        // Path prebuilds use SphericalRotation, but normal completion calls the native
        // cargo renderer, which derives the entity AND collider pose from path geometry.
        // Reuse the exact bounded read-only proof already required for source belts;
        // never ignore belt rotation or weaken the separate prebuild/debit/edge checks.
        if (desc.isBelt)
        {
            if (!ProvesNativeBeltRotation(factory, id)) return false;
        }
        else if (Quaternion.Angle(e.rot, BlueprintRotation(spec.Rotation)) > .1f) return false;
        if (desc.isAssembler)
        {
            if (e.assemblerId < 1 || e.assemblerId >= factory.factorySystem.assemblerCursor) return false;
            var a = factory.factorySystem.assemblerPool[e.assemblerId];
            return a.id == e.assemblerId && a.entityId == id && a.recipeId == spec.RecipeId
                && a.forceAccMode == (spec.Parameters.Length > 0 && spec.Parameters[0] != 0);
        }
        if (desc.isInserter)
        {
            if (e.inserterId < 1 || e.inserterId >= factory.factorySystem.inserterCursor) return false;
            var s = factory.factorySystem.inserterPool[e.inserterId];
            var pose = factory.factorySystem.inserterPosePool[e.inserterId];
            return s.id == e.inserterId && s.entityId == id && s.filter == spec.FilterItemId
                && spec.Parameters.Length == 1 && s.stt == Math.Max(10000, desc.inserterSTT * spec.Parameters[0])
                && s.pickOffset == spec.InputOffset && s.insertOffset == spec.OutputOffset
                && (pose.pos2 - ToVector(spec.Position2)).sqrMagnitude < .00001f
                && Quaternion.Angle(pose.rot2, BlueprintRotation(spec.Rotation2)) < .1f;
        }
        if (spec.ItemId == 2101 && desc.isStorage)
        {
            var pool = factory.factoryStorage.storagePool;
            if (e.storageId < 1 || e.storageId >= factory.factoryStorage.storageCursor || e.storageId >= pool.Length) return false;
            var storage = pool[e.storageId];
            return storage is not null && storage.id == e.storageId && storage.entityId == id
                && (long)desc.storageCol * desc.storageRow == storage.size
                && BlueprintStoragePolicy.MatchesConfiguration(spec.Parameters, GameStateReader.CaptureStorageConfiguration(storage));
        }
        return desc.isBelt ? e.beltId > 0 : e.powerNodeId > 0;
    }

    // Checks ALL actual slots, including expected-free belt ends and dynamically assigned
    // belt sorter slots4..11; a cached inserter target is not a substitute for these edges.
    private static bool BlueprintConnectionsMatch(PlanetFactory factory, BlueprintBuildState build)
    {
        var ids = build.Objects.Select(o => o.State == BlueprintObjectStates.Completed ? o.EntityId.GetValueOrDefault()
            : o.State == BlueprintObjectStates.PendingConstruction ? -o.PrebuildId.GetValueOrDefault() : 0).ToArray();
        for (var index = 0; index < ids.Length; index++)
        {
            if (ids[index] == 0) continue;
            var expected = build.Site.Connections.Where(c => ids[c.FromIndex] != 0 && ids[c.ToIndex] != 0
                && (c.FromIndex == index || c.ToIndex == index)).ToArray();
            var found = new HashSet<BlueprintPlanConnection>();
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(ids[index], slot, out var output, out var other, out var otherSlot);
                if (other == 0) continue;
                var edge = expected.SingleOrDefault(c =>
                    (output ? c.FromIndex == index && ids[c.ToIndex] == other : c.ToIndex == index && ids[c.FromIndex] == other)
                    && BlueprintSlotMatches(output ? c.FromSlot : c.ToSlot, slot)
                    && BlueprintSlotMatches(output ? c.ToSlot : c.FromSlot, otherSlot));
                if (edge is null || !found.Add(edge)) return false;
                factory.ReadObjectConn(other, otherSlot, out var reverse, out var back, out var backSlot);
                if (reverse == output || back != ids[index] || backSlot != slot) return false;
            }
            if (found.Count != expected.Length) return false;
            if (ids[index] > 0 && LDB.items.Select(build.Site.Objects[index].ItemId).prefabDesc.isInserter)
            {
                var inserter = factory.factorySystem.inserterPool[factory.entityPool[ids[index]].inserterId];
                var input = expected.Single(c => c.ToIndex == index);
                var output = expected.Single(c => c.FromIndex == index);
                if (inserter.pickTarget != ids[input.FromIndex] || inserter.insertTarget != ids[output.ToIndex]) return false;
            }
        }
        return true;
    }

    private static bool BlueprintSlotMatches(int expected, int actual) => expected == -1
        ? actual >= 4 && actual <= 11 : actual == expected;
}
