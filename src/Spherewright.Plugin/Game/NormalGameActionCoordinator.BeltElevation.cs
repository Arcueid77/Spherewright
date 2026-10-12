using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private BuildPreparation TryPrepareElevatedBeltBuild(
        PlanetFactory factory, Player player, ItemProto item, PrepareBuildRequest request)
    {
        var error = BeltElevationPolicy.ValidateRequest(request, item.prefabDesc.isBelt);
        if (error is not null)
            return BuildPreparation.Failed(BridgeErrorCodes.InvalidRequest, error, BeltElevationPolicy.Recovery);
        if (factory.planet.aux?.customGrids is null || factory.planet.aux.activeGrid is null)
            return BuildPreparation.Failed(BridgeErrorCodes.BuildLocationInvalid,
                "belt_elevation_native_grid_unavailable: a geodesic fallback cannot satisfy an explicit grid request.",
                BeltElevationPolicy.Recovery);
        if (!TryResolveElevatedBeltEndpoint(factory, request.PreferredPosition!, request.BeltStartAltitudeLevel!.Value, out var start)
            || !TryResolveElevatedBeltEndpoint(factory, request.PathEnd!, request.BeltEndAltitudeLevel!.Value, out var end))
            return BuildPreparation.Failed(BridgeErrorCodes.BuildLocationInvalid,
                "belt_elevation_native_layer_mismatch: native Snap did not retain the exact requested altitude level.",
                BeltElevationPolicy.Recovery);
        FactoryEntitySnapshot? boundSource = null;
        if (request.SourceObjectId is { } sourceId)
        {
            if (!TryReadBuildEndpoint(request, sourceId, request.ExpectedSourceStateHash!, out boundSource, out var sourceError)
                || boundSource!.ItemId != 2001 || Vector3.Distance(ToVector(boundSource.Position), start.Pose.position) > .01f)
                return BuildPreparation.Failed(BridgeErrorCodes.StaleState,
                    sourceError ?? "The exact raised source belt does not match the requested native start layer/position.");
            start = new EndpointPoint(sourceId, 0,
                new Pose(ToVector(boundSource.Position), Maths.SphericalRotation(ToVector(boundSource.Position), 0f)));
        }
        if (!TryCreateBeltSteps(factory, item, start, end, request.BeltPathMode, out var candidate, out var rejection))
            return BuildPreparation.Failed(BridgeErrorCodes.BuildLocationInvalid, rejection, BeltElevationPolicy.Recovery);
        if (!TryValidateBeltBuild(factory, player, item, candidate, out var accepted, out rejection, out var rejectionError))
            return BuildPreparation.Failed(rejectionError?.Code ?? BridgeErrorCodes.BuildLocationInvalid,
                rejection, rejectionError?.Recovery ?? BeltElevationPolicy.Recovery);
        var preparation = BuildPreparation.Succeeded(NormalBuildKinds.Belt, accepted);
        if (boundSource is not null)
        {
            preparation.SourceObjectId = boundSource.ObjectId;
            preparation.SourceEndpointHash = BuildEndpointHash(boundSource);
        }
        return preparation;
    }

    private static bool TryResolveElevatedBeltEndpoint(PlanetFactory factory, Vector3Snapshot requested,
        int level, out EndpointPoint endpoint)
    {
        // PlanetAuxData.Snap(false) floors altitude in native 1.3333333 m layers
        // relative to planet.radius and adds .2 m. Supply that native layer's
        // centre, then prove the returned layer rather than trusting float floor.
        var groundRadius = factory.planet.radius + .2f;
        var position = factory.planet.aux.Snap(ToVector(requested).normalized
            * (groundRadius + level * BeltElevationPolicy.NativeLayerHeight), onTerrain: false);
        endpoint = new EndpointPoint(0, -1, new Pose(position, Maths.SphericalRotation(position, 0f)));
        return BeltElevationPolicy.TryGetLevel(Snapshot(position), groundRadius, out var actual) && actual == level;
    }

    private static bool CompletePreparedElevatedBeltPath(IReadOnlyList<BuildStepPlan> steps, float radius,
        IReadOnlyList<BuildStepPlan>? original = null)
    {
        var anchor = steps.Count > 0 ? steps[0].SourceBeltAnchor : null;
        var covered = anchor is not null;
        if (steps.Count < (covered ? 3 : 4) || steps.Count > (covered ? BeltElevationPolicy.MaximumPoints - 1 : BeltElevationPolicy.MaximumPoints)
            || original is not null && original.Count != steps.Count) return false;
        if (covered && (anchor!.ElevatedEmptyPath is null || anchor.ElevatedEmptyPath.AnchorId != anchor.EntityId
            || anchor.ElevatedEmptyPath.EntityIds.Length == 0
            || anchor.ElevatedEmptyPath.EntityIds[anchor.ElevatedEmptyPath.EntityIds.Length - 1] != anchor.EntityId
            || !BeltElevationPolicy.TryGetLevel(Snapshot(anchor.Position), radius, out var level) || level <= 0)) return false;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.ItemId != 2001 || step.BeltPathMode != BeltPathModes.NativeElevatedGrid || step.Tilt != 0
                || (i == 0 ? !ReferenceEquals(step.SourceBeltAnchor, anchor) : step.SourceBeltAnchor is not null)
                || step.DestinationBeltAnchor is not null
                || step.InputObjectId != (i == 0 ? anchor?.EntityId ?? 0 : 0) || step.OutputObjectId != 0
                || covered && i == 0 && (step.InputFromSlot != 0 || step.InputToSlot != 1)
                || step.InputStepIndex != (i > 0 ? i - 1 : -1)
                || step.OutputStepIndex != (i + 1 < steps.Count ? i + 1 : -1)
                || !(Vector3.Distance(step.Position, step.Position2) <= .01f)
                || original is not null && !(Vector3.Distance(step.Position, original[i].Position) <= .01f))
                return false;
        }
        var points = (covered ? new[] { Snapshot(anchor!.Position) } : Array.Empty<Vector3Snapshot>())
            .Concat(steps.Select(step => Snapshot(step.Position))).ToArray();
        return BeltElevationPolicy.CompleteNativePath(points, BeltBuildOccupancyPolicy.MaximumPathPoints,
            points[0], points[points.Length - 1], radius);
    }

    private static bool ProvesCreatedElevatedPath(PlanetFactory factory, IReadOnlyList<BuildStepPlan> steps,
        IReadOnlyList<BuildPreview> previews)
    {
        if (steps.Count != previews.Count || previews.Select(p => p.objId).Distinct().Count() != steps.Count
            || factory.prebuildPool is null || factory.prebuildConnPool is null) return false;
        var ids = previews.Select(p => p.objId).ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (id >= 0 || id == int.MinValue || -id >= factory.prebuildCursor || -id >= factory.prebuildPool.Length
                || (long)-id * 16 + 16 > factory.prebuildConnPool.Length) return false;
            var prebuild = factory.prebuildPool[-id];
            // Native CreatePrebuilds normalises both prebuild rotations to the
            // spherical basis; the completed renderer later derives pitch from
            // CargoPath. Do not confuse the two native pose lifecycles.
            var rotation = Maths.SphericalRotation(steps[i].Position, 0f);
            if (prebuild.id != -id || prebuild.protoId != 2001 || prebuild.isDestroyed || prebuild.tilt != 0
                || !(Vector3.Distance(prebuild.pos, steps[i].Position) <= .01f)
                || !(Vector3.Distance(prebuild.pos2, steps[i].Position) <= .01f)
                || !(Quaternion.Angle(prebuild.rot, rotation) <= .1f) || !(Quaternion.Angle(prebuild.rot2, rotation) <= .1f))
                return false;
        }
        return ProvesFreeElevatedConnections(factory, ids, steps[0].SourceBeltAnchor?.EntityId ?? 0);
    }

    private static bool ProvesCompletedElevatedPath(PlanetFactory factory, NormalActionPlanPayload plan,
        IReadOnlyList<int> ids)
    {
        var anchor = plan.BuildSteps.Count > 0 ? plan.BuildSteps[0].SourceBeltAnchor : null;
        if (ids.Count != plan.BuildSteps.Count || ids.Count < (anchor is null ? 4 : 3)
            || ids.Count > (anchor is null ? BeltElevationPolicy.MaximumPoints : BeltElevationPolicy.MaximumPoints - 1)
            || ids.Distinct().Count() != ids.Count) return false;
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            if (id <= 0 || id >= factory.entityCursor || id >= factory.entityPool.Length) return false;
            var entity = factory.entityPool[id];
            if (entity.id != id || entity.protoId != 2001 || entity.tilt != 0 || entity.beltId <= 0
                || !(Vector3.Distance(entity.pos, plan.BuildSteps[i].Position) <= .01f)
                || !ProvesNativeBeltRotation(factory, id, requireColliderExtent: true)) return false;
        }
        // This helper proves directed membership, native rotations, component
        // reciprocity and an empty independent open path. Its cover-specific
        // allowance for external consumers/changing slots is NOT allowed here.
        if (anchor is null)
            return TryCaptureEmptyBeltPath(factory, ids[0], 1, out var path, out _)
                && path!.EntityIds.SequenceEqual(ids) && ProvesFreeElevatedConnections(factory, ids);
        var previous = anchor.ElevatedEmptyPath;
        if (previous is null) return false;
        var expected = previous.EntityIds.Concat(ids).ToArray();
        return expected.Length <= BeltUpgradePathPolicy.MaximumBelts
            && TryCaptureEmptyBeltPath(factory, expected[expected.Length - 1], 0, out var joined, out _)
            && joined!.EntityIds.SequenceEqual(expected)
            && TryEmptyBeltMembersHash(factory, previous.EntityIds, previous.AnchorId, 0, true, out var oldMembersHash)
            && BeltSourceReusePolicy.SameEvidence(previous.PreservationHash, oldMembersHash)
            && ProvesFreeElevatedConnections(factory, ids, anchor.EntityId);
    }

    private static bool ProvesFreeElevatedConnections(PlanetFactory factory, IReadOnlyList<int> ids, int sourceId = 0)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            for (var slot = 0; slot < 16; slot++)
            {
                var expected = slot == 0 ? (i + 1 < ids.Count ? ids[i + 1] : 0)
                    : slot == 1 ? (i > 0 ? ids[i - 1] : sourceId) : 0;
                factory.ReadObjectConn(ids[i], slot, out var output, out var other, out var otherSlot);
                if (other != expected || other != 0 && (output != (slot == 0) || otherSlot != (slot == 0 ? 1 : 0)))
                    return false;
            }
        }
        return true;
    }
}
