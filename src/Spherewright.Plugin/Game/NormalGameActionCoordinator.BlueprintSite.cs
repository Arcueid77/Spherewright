using Spherewright.Contracts.Factory;
using Spherewright.Bridge.Core.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    internal static void InspectNewBlueprintPreviews(PlanetFactory factory, SpherewrightBlueprintBuildTool tool,
        BlueprintSiteSnapshot result)
    {
        if (!BuildUiIsIdle(GameMain.mainPlayer)) { result.Blockers.Add("manual_build_ui_active"); return; }
        var previews = tool.bpPool.Take(tool.bpCursor).ToArray();
        var volumes = previews.Select(BlueprintPreviewColliders).ToArray();
        for (var i = 0; i < previews.Length; i++)
        {
            var preview = previews[i];
            if (Vector3.Distance(GameMain.mainPlayer.position, preview.lpos) > GameMain.mainPlayer.mecha.buildArea
                || Vector3.Distance(GameMain.mainPlayer.position, preview.lpos2) > GameMain.mainPlayer.mecha.buildArea)
                result.Blockers.Add("blueprint_object_out_of_build_range:" + i);
            if (preview.condition != EBuildCondition.Ok) result.Blockers.Add("native_translation_condition:" + i + ":" + preview.condition);
            for (var j = 0; j < i; j++)
            {
                // A sorter's own source/destination is intentionally touched. Other collisions
                // are rejected, including overlapping copies; there is no deduplication/cover.
                if (BlueprintLinkedSorterPair(preview, previews[j])) continue;
                if (BlueprintColliderSetsOverlap(volumes[i], volumes[j]))
                    result.Blockers.Add("blueprint_internal_collision:" + j + ":" + i);
            }
        }
        // Native paste checking can temporarily reserve slots of COVERED belts. Prove there
        // can be no cover BEFORE calling it, not merely discover a cover in its result.
        for (var id = 1; id < factory.entityCursor && id < factory.entityPool.Length; id++)
        {
            ref var entity = ref factory.entityPool[id];
            if (entity.id == id) CheckExisting(id, entity.protoId, entity.pos, entity.rot);
        }
        for (var id = 1; id < factory.prebuildCursor && id < factory.prebuildPool.Length; id++)
        {
            ref var prebuild = ref factory.prebuildPool[id];
            // Destroyed prebuilds can be reconstructed by native paste: they are occupied too.
            if (prebuild.id == id) CheckExisting(-id, prebuild.protoId, prebuild.pos, prebuild.rot);
        }
        if (result.Blockers.Any(b => b != "whole_blueprint_inventory_insufficient")) return;
        result.NativeCheckPerformed = true;
        var valid = tool.CheckNewObjects();
        result.NativeCheckPassed = valid && previews.All(p => p.condition == EBuildCondition.Ok
            && p.coverObjId == 0 && p.coverbp is null && !p.willRemoveCover && !p.willReconstructCover
            && p.inputObjId == 0 && p.outputObjId == 0 && p.addonObjId == 0);
        if (!result.NativeCheckPassed) result.Blockers.Add("native_blueprint_conditions_rejected");

        void CheckExisting(int id, int itemId, Vector3 pos, Quaternion rot)
        {
            var desc = LDB.items.Select(itemId)?.prefabDesc;
            if (desc is null)
            {
                if (!result.Blockers.Contains("existing_object_prototype_unavailable"))
                    result.Blockers.Add("existing_object_prototype_unavailable");
                return;
            }
            // Native covering can use centres rather than full colliders (notably belts).
            var existing = CreateWorldBuildColliders(desc, pos, rot);
            for (var i = 0; i < previews.Length; i++)
            {
                if (result.Objects[i].OccupiedObjectId.HasValue) continue;
                if ((previews[i].lpos - pos).sqrMagnitude <= 1f || BlueprintColliderSetsOverlap(volumes[i], existing))
                {
                    result.Objects[i].OccupiedObjectId = id;
                    result.Blockers.Add("blueprint_site_occupied:" + i);
                }
            }
        }
    }

    private static bool BlueprintLinkedSorterPair(BuildPreview a, BuildPreview b) =>
        a.desc.isInserter && (ReferenceEquals(a.input, b) || ReferenceEquals(a.output, b))
        || b.desc.isInserter && (ReferenceEquals(b.input, a) || ReferenceEquals(b.output, a));

    private static bool BlueprintColliderSetsOverlap(IReadOnlyList<WorldBuildCollider> left, IReadOnlyList<WorldBuildCollider> right)
    {
        foreach (var a in left)
        foreach (var b in right)
        {
            var radius = a.Extents.magnitude + b.Extents.magnitude + 0.001f;
            if ((a.Center - b.Center).sqrMagnitude <= radius * radius && OrientedBoxesOverlap(a, b)) return true;
        }
        return false;
    }

    private static List<WorldBuildCollider> BlueprintPreviewColliders(BuildPreview preview)
    {
        if (!preview.desc.isInserter) return CreateWorldBuildColliders(preview.desc, preview.lpos, preview.lrot);
        var delta = preview.lpos2 - preview.lpos;
        var rotation = delta.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(delta, (preview.lrot * Vector3.up + preview.lrot2 * Vector3.up).normalized) : preview.lrot;
        var centre = (preview.lpos + preview.lpos2) * 0.5f;
        var result = new List<WorldBuildCollider>();
        foreach (var col in preview.desc.buildColliders ?? Array.Empty<ColliderData>())
        {
            var dimensions = NativeInserterColliderGeometry.Calculate(delta.magnitude, col.ext.z,
                IsBelt(preview.input, preview.inputObjId), IsBelt(preview.output, preview.outputObjId));
            var local = col.pos; local.z += dimensions.CentreOffsetZ;
            var orientation = rotation * col.q;
            result.Add(new WorldBuildCollider(centre + rotation * local,
                orientation * Vector3.right, orientation * Vector3.up, orientation * Vector3.forward,
                new Vector3(col.ext.x, col.ext.y, dimensions.HalfLength)));
        }
        return result;

        bool IsBelt(BuildPreview? linked, int objectId)
        {
            if (linked is not null) return linked.desc.isBelt;
            var factory = GameMain.data.localLoadedPlanetFactory;
            return objectId > 0 && objectId < factory.entityCursor && factory.entityPool[objectId].id == objectId
                && LDB.items.Select(factory.entityPool[objectId].protoId).prefabDesc.isBelt;
        }
    }

    internal static List<FactoryBuildColliderSnapshot> ReadOnlyWorldBuildColliders(BuildPreview preview)
    {
        var desc=preview.desc;
        var source=desc.buildColliders is {Length:>0}?desc.buildColliders:desc.hasBuildCollider?new[]{desc.buildCollider}:Array.Empty<ColliderData>();
        if(source.Length>64 || source.Any(c=>c.shape!=EColliderShape.Box && c.shape!=EColliderShape.Sphere && c.shape!=EColliderShape.Capsule)
            || desc.isInserter && source.Any(c=>c.shape!=EColliderShape.Box))
            throw new InvalidOperationException("factory_geometry_native_collider_shape_unavailable");
        // The established inserter expansion helper uses buildColliders, not its
        // fallback. Missing native inserter arrays must remain explicitly unknown.
        if(desc.isInserter && desc.hasBuildCollider && desc.buildColliders is not {Length:>0})
            throw new InvalidOperationException("factory_geometry_native_inserter_colliders_unavailable");
        var boxes=BlueprintPreviewColliders(preview);
        if(boxes.Count!=source.Length && desc.hasBuildCollider)
            throw new InvalidOperationException("factory_geometry_native_collider_count_mismatch");
        return boxes.Select((b,i)=>new FactoryBuildColliderSnapshot{NativeShape=source[i].shape.ToString(),
            ConservativeEnclosure=source[i].shape!=EColliderShape.Box,Centre=NativeFactoryGeometry.Vector(b.Center),
            AxisX=NativeFactoryGeometry.Vector(b.AxisX),AxisY=NativeFactoryGeometry.Vector(b.AxisY),AxisZ=NativeFactoryGeometry.Vector(b.AxisZ),
            HalfExtents=NativeFactoryGeometry.Vector(b.Extents),PaddingMetres=desc.isInserter?0:.01f}).ToList();
    }
}
