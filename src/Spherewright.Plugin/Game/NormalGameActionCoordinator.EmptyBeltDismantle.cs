using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private EmptyBeltDismantleBoundary CaptureEmptyBeltDismantleBoundary(
        FactoryEntitySnapshot target, bool retainSurvivors)
    {
        var factory = GameMain.data?.localLoadedPlanetFactory;
        if (!EmptyBeltDismantlePolicy.SupportsSnapshot(target)
            || factory is null || !ReferenceEquals(factory, GameMain.localPlanet?.factory)
            || factory.entityPool is null || factory.prebuildPool is null
            || factory.entityConnPool is null || factory.prebuildConnPool is null
            || factory.factorySystem?.inserterPool is null || factory.factorySystem.minerPool is null
            || factory.cargoTraffic?.beltPool is null || factory.cargoTraffic.pathPool is null
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.entityCursor, factory.entityPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.prebuildCursor, factory.prebuildPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.factorySystem.inserterCursor, factory.factorySystem.inserterPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.factorySystem.minerCursor, factory.factorySystem.minerPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.cargoTraffic.beltCursor, factory.cargoTraffic.beltPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.cargoTraffic.pathCursor, factory.cargoTraffic.pathPool.Length)
            || factory.entityConnPool.Length < factory.entityCursor * 16
            || factory.prebuildConnPool.Length < factory.prebuildCursor * 16
            || target.ObjectId >= factory.entityCursor)
            throw new InvalidOperationException("Empty belt recovery requires complete bounded current native factory evidence.");

        var traffic = factory.cargoTraffic;
        var beltId = factory.entityPool[target.ObjectId].beltId;
        if (beltId <= 0 || beltId >= traffic.beltCursor || traffic.beltPool[beltId].id != beltId)
            throw new InvalidOperationException("The empty belt component identity is unavailable.");
        var pathId = traffic.beltPool[beltId].segPathId;
        var nativePath = pathId > 0 && pathId < traffic.pathCursor ? traffic.GetCargoPath(pathId) : null;
        // Bound full-path allocation BEFORE calling the existing native decoder.
        if (nativePath?.belts is null || nativePath.belts.Count < 1
            || nativePath.belts.Count > EmptyBeltDismantlePolicy.MaximumBelts
            || nativePath.pathLength <= 0 || nativePath.pathLength > EmptyBeltDismantlePolicy.MaximumPathCells
            || !TryCaptureEmptyBeltPath(factory, target.ObjectId, 1, out var path, out var reason))
            throw new InvalidOperationException("Only the head of an empty open independent basic-belt path of at most16 belts/512 cells is recoverable.");

        var observed = _reader.InspectFactoryEntityOnMainThread(target.SessionId,
            new InspectFactoryEntityRequest { PlanetId = target.PlanetId, ObjectId = target.ObjectId,
                MaterialInventoryObjectIds = path!.EntityIds.ToList() });
        var cut = observed.Value?.MaterialInventoryCut;
        if (!observed.Success || !EmptyBeltDismantlePolicy.TryQualify(target, path.EntityIds, cut, out reason))
            throw new InvalidOperationException("Empty belt recovery did not prove the complete same-tick isolated, reciprocal, stock-free chain.");
        var members = new HashSet<int>(path.EntityIds);
        var componentIds = new HashSet<int>();
        var identity = new List<object?>();
        foreach (var id in path.EntityIds)
        {
            var entity = factory.entityPool[id];
            if (!componentIds.Add(entity.beltId) || !PureDismantleBelt(entity))
                throw new InvalidOperationException("Empty belt recovery cannot remove add-ons, other components or named/marked belts.");
            identity.Add(entity.modelIndex); identity.Add(entity.modelId); identity.Add(entity.colliderId);
            identity.Add(entity.mmblockId); identity.Add(entity.hashAddress); identity.Add(entity.audioId);
            identity.Add(entity.warningId);
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(id, slot, out _, out var endpoint, out _);
                if (endpoint == 0 && factory.entityConnPool[id * 16 + slot] != 0)
                    throw new InvalidOperationException("Empty belt recovery rejects malformed empty native connection slots.");
            }
        }
        for (var id = 1; id < factory.entityCursor; id++)
        {
            var entity = factory.entityPool[id];
            if (entity.id == 0) continue;
            if (entity.id != id || !members.Contains(id) && componentIds.Contains(entity.beltId))
                throw new InvalidOperationException("Belt entity/component identity is ambiguous.");
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(id, slot, out _, out var endpoint, out _);
                if (!EmptyBeltDismantlePolicy.ReferenceIsSafe(members, id, endpoint))
                    throw new InvalidOperationException("An external entity references the empty belt chain, including a one-sided reference.");
            }
        }
        for (var id = 1; id < factory.prebuildCursor; id++)
        {
            if (factory.prebuildPool[id].id == 0) continue;
            if (factory.prebuildPool[id].id != id) throw new InvalidOperationException("Prebuild identity is malformed.");
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(-id, slot, out _, out var endpoint, out _);
                if (!EmptyBeltDismantlePolicy.ReferenceIsSafe(members, -id, endpoint))
                    throw new InvalidOperationException("A prebuild still references the empty belt chain.");
            }
        }
        foreach (var inserter in factory.factorySystem.inserterPool.Take(factory.factorySystem.inserterCursor).Skip(1))
            if (inserter.id != 0 && (members.Contains(inserter.pickTarget) || members.Contains(inserter.insertTarget)))
                throw new InvalidOperationException("A cached sorter target still references the empty belt chain.");
        foreach (var miner in factory.factorySystem.minerPool.Take(factory.factorySystem.minerCursor).Skip(1))
            if (miner.id != 0 && members.Contains(miner.insertTarget))
                throw new InvalidOperationException("A cached miner outlet still references the empty belt chain.");
        for (var id = 1; id < traffic.beltCursor; id++)
        {
            var belt = traffic.beltPool[id];
            if (belt.id == 0) continue;
            if (belt.id != id || belt.entityId <= 0 || belt.entityId >= factory.entityCursor
                || factory.entityPool[belt.entityId].id != belt.entityId || factory.entityPool[belt.entityId].beltId != id)
                throw new InvalidOperationException("A native belt pool identity is ambiguous.");
            if (!componentIds.Contains(belt.id)
                && (belt.segPathId == path.PathId || componentIds.Contains(belt.outputId)
                    || componentIds.Contains(belt.mainInputId) || componentIds.Contains(belt.backInputId)
                    || componentIds.Contains(belt.leftInputId) || componentIds.Contains(belt.rightInputId)))
                throw new InvalidOperationException("An external native belt component references the empty chain.");
        }
        foreach (var other in traffic.pathPool.Take(traffic.pathCursor).Skip(1))
            if (other is not null && other.id != 0 && other.id != path.PathId && other.outputPath?.id == path.PathId)
                throw new InvalidOperationException("Another native cargo path still feeds this empty chain.");

        return new EmptyBeltDismantleBoundary
        {
            NativeHash = EmptyBeltDismantlePolicy.Fingerprint(target, path.EntityIds, cut!,
                CanonicalStateHash.Combine(path.BindingHash, identity.ToArray())),
            BeltId = beltId, PathId = path.PathId, EntityIds = path.EntityIds,
            Entities = retainSurvivors ? factory.entityPool.Take(factory.entityCursor).ToArray() : null,
            Prebuilds = retainSurvivors ? factory.prebuildPool.Take(factory.prebuildCursor).ToArray() : null,
            Belts = retainSurvivors ? traffic.beltPool.Take(traffic.beltCursor).ToArray() : null,
            EntityConnections = retainSurvivors ? factory.entityConnPool.Take(factory.entityCursor * 16).ToArray() : null,
            PrebuildConnections = retainSurvivors ? factory.prebuildConnPool.Take(factory.prebuildCursor * 16).ToArray() : null,
        };
    }

    private static bool PureDismantleBelt(EntityData entity) => entity.protoId == EmptyBeltDismantlePolicy.ItemId
        && entity.beltId > 0 && entity.tilt == 0 && entity.inserterId == 0 && entity.storageId == 0
        && entity.assemblerId == 0 && entity.minerId == 0 && entity.labId == 0 && entity.tankId == 0
        && entity.stationId == 0 && entity.dispenserId == 0 && entity.splitterId == 0 && entity.fractionatorId == 0
        && entity.ejectorId == 0 && entity.siloId == 0 && entity.powerConId == 0 && entity.powerNodeId == 0
        && entity.powerGenId == 0 && entity.powerAccId == 0 && entity.powerExcId == 0 && entity.monitorId == 0
        && entity.speakerId == 0 && entity.spraycoaterId == 0 && entity.pilerId == 0 && entity.turretId == 0
        && entity.beaconId == 0 && entity.fieldGenId == 0 && entity.battleBaseId == 0
        && entity.constructionModuleId == 0 && entity.combatModuleId == 0 && entity.combatStatId == 0
        && entity.constructStatId == 0 && entity.extraInfoId == 0 && entity.markerId == 0;

    private void VerifyEmptyBeltDismantleBoundary(int removedId, EmptyBeltDismantleBoundary before)
    {
        var factory = GameMain.data.localLoadedPlanetFactory;
        if (factory is null || before.Entities is null || before.Prebuilds is null || before.Belts is null
            || before.EntityConnections is null || before.PrebuildConnections is null
            || factory.entityCursor != before.Entities.Length || factory.prebuildCursor != before.Prebuilds.Length
            || factory.cargoTraffic.beltCursor != before.Belts.Length
            || factory.entityPool[removedId].id != 0 || factory.cargoTraffic.beltPool[before.BeltId].id != 0
            || factory.cargoTraffic.beltPool[before.BeltId].entityId != 0)
            throw new InvalidOperationException("Empty belt recovery did not prove exact entity/component disappearance.");
        var remainingIds = before.EntityIds.Skip(1).ToArray();
        if (!EmptyStorageDismantlePolicy.BoundedPool(factory.cargoTraffic.pathCursor, factory.cargoTraffic.pathPool.Length)
            || remainingIds.Length == 0 && factory.cargoTraffic.GetCargoPath(before.PathId)?.id > 0)
            throw new InvalidOperationException("The final belt's native path was not retired, or cargo path coverage is unavailable.");
        foreach (var path in factory.cargoTraffic.pathPool.Take(factory.cargoTraffic.pathCursor).Skip(1))
            if (path is not null && path.id != 0 && (path.belts is null || path.belts.Contains(before.BeltId)))
                throw new InvalidOperationException("A surviving native path still references the removed belt component.");
        if (remainingIds.Length > 0)
        {
            var fresh = _reader.InspectFactoryEntityOnMainThread(_sessions.SessionId,
                new InspectFactoryEntityRequest { PlanetId = factory.planetId, ObjectId = remainingIds[0] });
            if (!fresh.Success || fresh.Value is null
                || !CaptureEmptyBeltDismantleBoundary(fresh.Value, false).EntityIds.SequenceEqual(remainingIds))
                throw new InvalidOperationException("Native removal did not preserve the exact empty remaining chain.");
        }
        var members = new HashSet<int>(before.EntityIds);
        for (var id = 1; id < before.Entities.Length; id++)
        {
            if (id == removedId) continue;
            var expected = before.Entities[id]; // Copy only; never write a native pool.
            // Unity's quaternion operators use an angular tolerance. The full
            // entity comparison below is exact, so even a small component
            // change must first pass the native renderer/collider proof.
            if (remainingIds.Take(2).Contains(id) && !expected.rot.Equals(factory.entityPool[id].rot))
            {
                if (!ProvesNativeBeltRotation(factory, id, requireColliderExtent: true))
                    throw new InvalidOperationException("A touched surviving belt rotation lacks exact native renderer/collider proof.");
                expected.rot = factory.entityPool[id].rot;
            }
            if (!expected.Equals(factory.entityPool[id]))
                throw new InvalidOperationException("Empty belt removal changed an unrelated native entity field.");
        }
        for (var id = 1; id < before.Belts.Length; id++)
        {
            if (id == before.BeltId) continue;
            var expected = before.Belts[id];
            var actual = factory.cargoTraffic.beltPool[id];
            if (!members.Contains(expected.entityId) ? !expected.Equals(actual)
                : expected.id != actual.id || expected.entityId != actual.entityId || expected.speed != actual.speed)
                throw new InvalidOperationException("Native removal changed an unrelated belt component or surviving grade/speed.");
        }
        for (var id = 1; id < before.Prebuilds.Length; id++)
            if (!before.Prebuilds[id].Equals(factory.prebuildPool[id]))
                throw new InvalidOperationException("Empty belt removal changed a prebuild.");
        // Native ClearObjectConn removes precisely the two ends of the former
        // head edge. These are managed expected copies, never connection writes.
        Array.Clear(before.EntityConnections, removedId * 16, 16);
        if (remainingIds.Length > 0) before.EntityConnections[remainingIds[0] * 16 + 1] = 0;
        if (!before.EntityConnections.SequenceEqual(factory.entityConnPool.Take(before.EntityConnections.Length))
            || !before.PrebuildConnections.SequenceEqual(factory.prebuildConnPool.Take(before.PrebuildConnections.Length)))
            throw new InvalidOperationException("Empty belt removal changed an unapproved entity or prebuild connection.");
    }

    private sealed class EmptyBeltDismantleBoundary
    {
        public string NativeHash { get; set; } = string.Empty;
        public int BeltId { get; set; }
        public int PathId { get; set; }
        public int[] EntityIds { get; set; } = Array.Empty<int>();
        public EntityData[]? Entities { get; set; }
        public PrebuildData[]? Prebuilds { get; set; }
        public BeltComponent[]? Belts { get; set; }
        public int[]? EntityConnections { get; set; }
        public int[]? PrebuildConnections { get; set; }
    }
}
