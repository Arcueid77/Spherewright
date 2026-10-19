using Spherewright.Bridge.Core.Logistics;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    private BridgeError? ValidateOwnedResidentStationReadOnMainThread(
        string? requestedSessionId,
        InspectFactoryEntityRequest request,
        bool directReadOnlyRoute,
        out PlanetFactory? factory)
    {
        factory = null;
        var ownedError = ValidateOwnedGameDataOnMainThread(requestedSessionId, out var gameData);
        if (ownedError is not null) return ownedError;
        var requestError = LogisticsActiveVesselObservationPolicy.ValidateOwnedRemoteStationRequest(
            directReadOnlyRoute, request.PlanetId, request.ObjectId,
            request.Geometry is not null, request.MaterialInventoryObjectIds?.Count > 0);
        if (requestError is not null)
            return InvalidRequest(requestError, "Use the direct read-only inspect route and one positive existing station entity, without geometry or material selections.");

        // This pool is already simulated by the owned game. Reading it never
        // requests a planet display, factory load/create or exploration scan.
        var poolError = TryGetOwnedFactories(gameData!, out var factories);
        if (poolError is not null) return poolError;
        var matches = factories.Where(value => value.planetId == request.PlanetId).ToList();
        if (matches.Count != 1)
            return NotReady("The exact requested station factory is not available in the already-owned simulation pool.");
        var target = matches[0];
        if (request.ObjectId >= target.entityCursor || target.entityPool is null || request.ObjectId >= target.entityPool.Length)
            return InvalidFactoryEntity("The requested positive owned station entity is unavailable.").Error;
        ref var entity = ref target.entityPool[request.ObjectId];
        if (entity.id != request.ObjectId || entity.stationId <= 0 || target.transport?.stationPool is null
            || entity.stationId >= target.transport.stationCursor || entity.stationId >= target.transport.stationPool.Length)
            return NotReady("The selected owned object is not an identity-verified existing logistics station.");
        var station = target.transport.stationPool[entity.stationId];
        if (station is null || station.id != entity.stationId || station.entityId != entity.id
            || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(station.isStellar, station.planetId, target.planetId))
            return NotReady("The requested owned station does not match its native factory/entity pool identity.");
        factory = target;
        return null;
    }

    private static LogisticsActiveVesselObservation CaptureActiveLogisticsVessels(
        PlanetFactory factory, int entityId)
    {
        var tick = GameMain.gameTick;
        var unavailable = new LogisticsActiveVesselObservation
        {
            CapturedAtGameTick = tick, ReasonCode = "active_vessel_station_unavailable",
        };
        if (entityId <= 0 || factory.entityPool is null || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length)
            return unavailable;
        ref var entity = ref factory.entityPool[entityId];
        var pool = factory.transport?.stationPool;
        if (entity.id != entityId || entity.stationId <= 0 || pool is null
            || entity.stationId >= factory.transport!.stationCursor || entity.stationId >= pool.Length)
            return unavailable;
        var station = pool[entity.stationId];
        if (station is null || station.id != entity.stationId || station.entityId != entityId
            || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(station.isStellar, station.planetId, factory.planetId))
            return unavailable;
        var data = station.workShipDatas;
        var count = station.workShipCount;
        var prefix = new List<LogisticsActiveVesselFacts>();
        // Never iterate an unbounded Native counter or read the cleared tail.
        if (count > 0 && data is not null && count <= data.Length)
        {
            for (var index = 0; index < Math.Min(count, LogisticsActiveVesselObservationPolicy.MaximumRecords); index++)
            {
                var ship = data[index];
                prefix.Add(new LogisticsActiveVesselFacts
                {
                    ShipIndex = ship.shipIndex, Gene = ship.gene, OtherGId = ship.otherGId,
                    ItemId = ship.itemId, ItemCount = ship.itemCount, Inc = ship.inc,
                    Stage = ship.stage, Direction = ship.direction, T = ship.t,
                    USpeed = ship.uSpeed, WarpState = ship.warpState, WarperCount = ship.warperCnt,
                });
            }
        }
        return LogisticsActiveVesselObservationPolicy.Capture(
            station.isStellar, station.gid, count, data?.Length ?? -1, prefix, tick);
    }
}
