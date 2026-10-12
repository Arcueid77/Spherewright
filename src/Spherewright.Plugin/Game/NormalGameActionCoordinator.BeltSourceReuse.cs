using System.Threading;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // Copied main-thread evidence, never a live GameData/Unity component in a plan.
    private sealed class BeltSourceState
    {
        internal int EntityId;
        internal int ItemId;
        internal Vector3 Position;
        internal float Tilt;
        internal string TopologyHash = string.Empty;
        internal string CompletionTopologyHash = string.Empty;
        internal string BindingHash = string.Empty;
        internal string CargoHash = string.Empty;
        internal EmptyBeltPathState? ElevatedEmptyPath;
        internal List<FactoryConnectionSnapshot> Connections = new List<FactoryConnectionSnapshot>();
    }

    private static bool TryCaptureBeltSource(PlanetFactory factory, int entityId,
        out BeltSourceState? state, out string reason, bool requireEmptyElevatedPath = false)
    {
        state = null;
        reason = "belt_source_identity_unavailable";
        if (!TryBeltSourceTopology(factory, entityId, out var topology, out var connections)) return false;
        reason = "belt_source_native_rotation_unproven";
        if (!TryBeltSourceTopology(factory, entityId, out var completionTopology, out _, nativeCompletionRotation: true)) return false;
        var entity = factory.entityPool[entityId];
        var traffic = factory.cargoTraffic;
        reason = "belt_source_component_unavailable";
        if (traffic?.beltPool is null || entity.beltId <= 0 || entity.beltId >= traffic.beltCursor
            || entity.beltId >= traffic.beltPool.Length) return false;
        var belt = traffic.beltPool[entity.beltId];
        if (belt.id != entity.beltId || belt.entityId != entityId || traffic.pathPool is null
            || belt.segPathId <= 0 || belt.segPathId >= traffic.pathCursor || belt.segPathId >= traffic.pathPool.Length)
            return false;
        var path = traffic.GetCargoPath(belt.segPathId);
        reason = "belt_source_path_identity_unavailable_or_closed";
        if (path is null || path.id != belt.segPathId || path.closed || path.buffer is null
            || !ReferenceEquals(path.cargoContainer, factory.cargoContainer)) return false;
        var buffer = path.buffer;
        reason = "belt_source_path_buffer_busy";
        if (!Monitor.TryEnter(buffer)) return false;
        try
        {
            if (!NativeBeltPathCapture.TryRead(path, out var export, out var captureReason))
            { reason = "belt_source_" + captureReason; return false; }
            reason = "belt_source_not_open_path_tail";
            if (export!.Closed || export.OutputPathId != 0 || export.BeltIds.Last() != belt.id) return false;
            if (!BeltUpgradePathPolicy.TryLocateAllCargo(export, out var references, out var cargoReason))
            { reason = "belt_source_" + cargoReason; return false; }
            var fields = new List<object?> { topology, completionTopology, export.Id, export.Length, export.OutputPathId, export.OutputIndex,
                Convert.ToBase64String(export.Geometry), string.Join(",", export.Speeds),
                string.Join(",", export.BeltIds), string.Join(",", export.InputPathIds) };
            foreach (var id in export.BeltIds)
            {
                reason = "belt_source_path_member_invalid";
                if (id <= 0 || id >= traffic.beltCursor || id >= traffic.beltPool.Length) return false;
                var member = traffic.beltPool[id];
                if (member.id != id || member.segPathId != path.id || member.entityId <= 0
                    || member.entityId >= factory.entityCursor || member.entityId >= factory.entityPool.Length
                    || factory.entityPool[member.entityId].id != member.entityId
                    || factory.entityPool[member.entityId].beltId != id
                    || member.segIndex < 0 || member.segLength <= 0
                    || (long)member.segIndex + member.segLength > export.Length) return false;
                if (!AppendBeltSourceObject(factory, member.entityId, fields)) return false;
                fields.Add(id); fields.Add(member.entityId); fields.Add(member.segIndex);
                fields.Add(member.segLength); fields.Add(member.segPivotOffset);
            }
            var container = path.cargoContainer;
            reason = "belt_source_cargo_pool_unavailable";
            if (container?.cargoPool is null || container.cursor < 0 || container.cursor > container.cargoPool.Length) return false;
            var cargoFields = new List<object?> { Convert.ToBase64String(export.CargoBuffer), references.Count };
            foreach (var reference in references)
            {
                reason = "belt_source_cargo_readback_mismatch";
                if (reference.CargoId < 0 || reference.CargoId >= container.cursor || reference.CargoId >= container.cargoPool.Length
                    || !path.GetCargoAtIndex(reference.ObservedPathCell, out var cargo, out var id, out _)
                    || id != reference.CargoId || cargo.item <= 0 || LDB.items.Select(cargo.item) is null || cargo.stack < 1)
                    return false;
                cargoFields.Add(id); cargoFields.Add(cargo.item); cargoFields.Add(cargo.stack); cargoFields.Add(cargo.inc);
            }
            state = new BeltSourceState { EntityId = entityId, ItemId = entity.protoId, Position = entity.pos, Tilt = entity.tilt,
                Connections = connections, TopologyHash = topology, CompletionTopologyHash = completionTopology,
                BindingHash = CanonicalStateHash.Combine("belt-source-binding-v1", fields.ToArray()),
                CargoHash = CanonicalStateHash.Combine("belt-source-cargo-v1", cargoFields.ToArray()) };
            if (requireEmptyElevatedPath)
            {
                if (!TryCaptureEmptyBeltPath(factory, entityId, 0, out var empty, out reason))
                { state = null; return false; }
                state.ElevatedEmptyPath = empty;
                state.BindingHash = BeltSourceReusePolicy.BindEmptyElevatedPathEvidence(state.BindingHash, empty!.BindingHash);
            }
            reason = string.Empty;
            return true;
        }
        finally { Monitor.Exit(buffer); }
    }

    // The source's formerly empty slot0 is checked separately. Preserve all other
    // source slots and every existing reciprocal neighbour, including sorters.
    private static bool TryBeltSourceTopology(PlanetFactory factory, int sourceId,
        out string hash, out List<FactoryConnectionSnapshot> sourceConnections, bool nativeCompletionRotation = false)
    {
        hash = string.Empty;
        sourceConnections = new List<FactoryConnectionSnapshot>();
        var fields = new List<object?>();
        if (!AppendBeltSourceObject(factory, sourceId, fields, nativeCompletionRotation)) return false;
        for (var slot = 0; slot < 16; slot++)
        {
            factory.ReadObjectConn(sourceId, slot, out var output, out var other, out var otherSlot);
            sourceConnections.Add(new FactoryConnectionSnapshot { Slot = slot, IsOutput = output, OtherObjectId = other, OtherSlot = otherSlot });
            if (slot == 0) continue;
            fields.Add(slot); fields.Add(output); fields.Add(other); fields.Add(otherSlot);
            if (other == 0) continue;
            if (other == sourceId || otherSlot < 0 || otherSlot >= 16 || !AppendBeltSourceObject(factory, other, fields, nativeCompletionRotation)) return false;
            factory.ReadObjectConn(other, otherSlot, out var reverseOutput, out var reverseId, out var reverseSlot);
            if (reverseOutput == output || reverseId != sourceId || reverseSlot != slot) return false;
            for (var neighbourSlot = 0; neighbourSlot < 16; neighbourSlot++)
            {
                factory.ReadObjectConn(other, neighbourSlot, out var o, out var n, out var s);
                fields.Add(neighbourSlot); fields.Add(o); fields.Add(n); fields.Add(s);
            }
        }
        hash = CanonicalStateHash.Combine(nativeCompletionRotation ? "belt-source-completion-neighbourhood-v1" : "belt-source-neighbourhood-v1", fields.ToArray());
        return true;
    }

    private static bool AppendBeltSourceObject(PlanetFactory factory, int id, List<object?> fields, bool nativeCompletionRotation = false)
    {
        if (factory.entityPool is null || factory.entityConnPool is null || id <= 0
            || id > BeltBuildOccupancyPolicy.MaximumFactorySlots || id >= factory.entityCursor || id >= factory.entityPool.Length
            || (long)id * 16 + 16 > factory.entityConnPool.Length || factory.entityPool[id].id != id) return false;
        var e = factory.entityPool[id];
        if (e.protoId <= 0 || LDB.items.Select(e.protoId)?.prefabDesc is null
            || !IsFinite(e.pos.x) || !IsFinite(e.pos.y) || !IsFinite(e.pos.z) || e.pos.sqrMagnitude < 1
            || !IsFinite(e.rot.x) || !IsFinite(e.rot.y) || !IsFinite(e.rot.z) || !IsFinite(e.rot.w) || !IsFinite(e.tilt)) return false;
        fields.Add(id); fields.Add(e.protoId); fields.Add(e.beltId); fields.Add(e.inserterId);
        fields.Add(e.pos.x); fields.Add(e.pos.y); fields.Add(e.pos.z);
        fields.Add(e.tilt);
        if (nativeCompletionRotation && e.beltId > 0)
        {
            if (!ProvesNativeBeltRotation(factory, id)) return false;
            fields.Add(BeltSourceRotationPolicy.PreservationMode); fields.Add(e.colliderId);
        }
        else { fields.Add(e.rot.x); fields.Add(e.rot.y); fields.Add(e.rot.z); fields.Add(e.rot.w); }
        return true;
    }

    private static bool TryAttachSourceCover(PlanetFactory factory, ItemProto item, IReadOnlyList<BuildStepPlan> steps,
        List<BuildPreview> newPreviews, SpherewrightPathBuildTool tool, out BuildPreview? cover, out string reason)
    {
        cover = null;
        reason = "belt_source_binding_changed";
        var bound = steps[0].SourceBeltAnchor;
        if (bound is null)
        {
            // A belt endpoint without explicit cover evidence must never silently use the old NEW-anchor path.
            var source = steps[0].InputObjectId;
            if (source > 0 && (source >= factory.entityCursor || source >= factory.entityPool.Length
                || factory.entityPool[source].id != source || factory.entityPool[source].beltId > 0)) return false;
            reason = string.Empty;
            return true;
        }
        var destination = steps[steps.Count - 1].DestinationBeltAnchor;
        if (steps.Skip(1).Any(s => s.SourceBeltAnchor is not null) || steps.Count < 2
            || steps[0].InputObjectId != bound.EntityId || steps[0].InputFromSlot != 0 || steps[0].InputToSlot != 1
            || steps[0].InputStepIndex != -1 || steps[steps.Count - 1].OutputObjectId != (destination?.EntityId ?? 0)
            || (destination is not null && (destination.SourcePath?.AnchorId != bound.EntityId || !EmptyJoinBindingsMatch(factory, destination)))
            || !TryCaptureBeltSource(factory, bound.EntityId, out var fresh, out reason,
                requireEmptyElevatedPath: steps[0].BeltPathMode == Spherewright.Contracts.Actions.BeltPathModes.NativeElevatedGrid)
            || !BeltSourceReusePolicy.SameEvidence(bound.BindingHash, fresh!.BindingHash)
            || !BeltSourceReusePolicy.Supports(bound.EntityId, fresh.ItemId, item.ID, 0, 0, fresh.Tilt, false,
                fresh.Connections[0].OtherObjectId, fresh.Connections.Skip(1).Take(3).Count(c => c.OtherObjectId != 0))) return false;
        cover = new BuildPreview { item = item, desc = item.prefabDesc, lpos = fresh.Position, lpos2 = fresh.Position,
            lrot = Maths.SphericalRotation(fresh.Position, 0f), lrot2 = Maths.SphericalRotation(fresh.Position, 0f),
            tilt = fresh.Tilt, isConnNode = true, needModel = false, condition = EBuildCondition.Ok,
            coverObjId = fresh.EntityId, willRemoveCover = false, output = newPreviews[0], outputFromSlot = 0, outputToSlot = 1 };
        tool.buildPreviews.Add(cover); // Keep the source in FULL native geometry/junction validation and creation.
        reason = string.Empty;
        return true;
    }

    private static bool SourceCoverMatches(BuildPreview? cover, BeltSourceState? source) => source is null
        ? cover is null
        : cover is not null && BeltSourceReusePolicy.IsNonRemovingCover(source.EntityId, cover.coverObjId, cover.willRemoveCover)
            && cover.condition == EBuildCondition.Ok && Vector3.Distance(cover.lpos, source.Position) <= .01f
            && Vector3.Distance(cover.lpos2, source.Position) <= .01f && cover.tilt == source.Tilt
            && cover.inputObjId == 0 && cover.input is null && cover.outputObjId == 0
            && cover.outputFromSlot == 0 && cover.outputToSlot == 1;

    private static void CreateWithSourceCoverProof(PlanetFactory factory, SpherewrightPathBuildTool tool,
        BuildPreview? cover, List<BuildPreview> newPreviews, BeltSourceState? bound)
    {
        if (bound is null) { tool.CreatePrebuildsWithDetachedCommand(); return; }
        if (!SourceCoverMatches(cover, bound) || !ReferenceEquals(cover!.output, newPreviews[0])
            || !TryCaptureBeltSource(factory, bound.EntityId, out var before, out _)
            || before!.Connections[0].OtherObjectId != 0)
            throw new InvalidOperationException("The exact non-removing source cover changed before native construction.");

        // Keep the raw whole-path binding for the post-create preservation proof.
        // The prepared elevated binding also includes the independently captured
        // empty/open path. Re-capture that evidence before create, without mixing
        // its wrapped hash into the raw before/after comparison.
        string? currentEmptyPathBinding = null;
        if (bound.ElevatedEmptyPath is not null)
        {
            if (!TryCaptureEmptyBeltPath(factory, bound.EntityId, 0, out var empty, out _))
                throw new InvalidOperationException("The exact empty elevated source path changed before native construction.");
            currentEmptyPathBinding = empty!.BindingHash;
        }
        if (!BeltSourceReusePolicy.MatchesPreparedCreationBinding(bound.BindingHash, before.BindingHash,
            bound.ElevatedEmptyPath?.BindingHash, currentEmptyPathBinding))
            throw new InvalidOperationException("The exact non-removing source binding changed before native construction.");
        tool.CreatePrebuildsWithDetachedCommand();
        if (cover.objId != bound.EntityId || !SourceCoverMatches(cover, bound)
            || !TryCaptureBeltSource(factory, bound.EntityId, out var after, out _)
            || !BeltSourceReusePolicy.SameEvidence(before.BindingHash, after!.BindingHash)
            || !BeltSourceReusePolicy.SameEvidence(before.CargoHash, after.CargoHash)
            || !ProvesSourceOutput(factory, bound, after.Connections, newPreviews[0].objId, prebuild: true))
            throw new InvalidOperationException("Native source reuse cargo, identity or reciprocal connection proof failed; do not replay.");
    }

    private static bool ProvesSourceOutput(PlanetFactory factory, BeltSourceState bound,
        List<FactoryConnectionSnapshot> after, int firstNewId, bool prebuild)
    {
        if (!BeltSourceReusePolicy.ProvesOnlyOutputChanged(bound.Connections, after, firstNewId)) return false;
        if (prebuild)
        {
            if (firstNewId >= 0 || firstNewId == int.MinValue || factory.prebuildPool is null || factory.prebuildConnPool is null
                || -firstNewId >= factory.prebuildCursor || -firstNewId >= factory.prebuildPool.Length
                || (long)-firstNewId * 16 + 16 > factory.prebuildConnPool.Length
                || factory.prebuildPool[-firstNewId].id != -firstNewId) return false;
        }
        else if (firstNewId <= 0 || firstNewId >= factory.entityCursor || firstNewId >= factory.entityPool.Length
            || (long)firstNewId * 16 + 16 > factory.entityConnPool.Length || factory.entityPool[firstNewId].id != firstNewId) return false;
        factory.ReadObjectConn(firstNewId, 1, out var output, out var other, out var slot);
        return !output && other == bound.EntityId && slot == 0;
    }
}
