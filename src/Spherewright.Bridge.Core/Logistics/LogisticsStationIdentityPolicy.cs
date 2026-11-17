namespace Spherewright.Bridge.Core.Logistics;

public static class LogisticsStationIdentityPolicy
{
    // A paid construction completion must still identify the exact native
    // station component. Planetary stations use the native local-planet
    // sentinel and have no galactic station ID.
    public static bool MatchesPaidConstruction(
        int buildingItemId, int expectedStationId, int expectedEntityId, int factoryPlanetId,
        int stationId, int stationEntityId, int stationPlanetId, int galacticStationId,
        bool isInterstellar, bool isCollector, bool isVeinCollector)
    {
        if (expectedStationId <= 0 || expectedEntityId <= 0 || stationId != expectedStationId
            || stationEntityId != expectedEntityId || !MatchesLocalPlanet(isInterstellar, stationPlanetId, factoryPlanetId))
            return false;

        return buildingItemId switch
        {
            2103 => !isInterstellar && !isCollector && !isVeinCollector && galacticStationId == 0,
            2104 => isInterstellar && galacticStationId > 0,
            _ => false,
        };
    }

    public static bool MatchesLocalPlanet(
        bool isInterstellar,
        int stationPlanetId,
        int factoryPlanetId)
    {
        if (factoryPlanetId <= 0)
        {
            return false;
        }

        if (isInterstellar)
        {
            return stationPlanetId == factoryPlanetId;
        }

        return stationPlanetId == 0 || stationPlanetId == factoryPlanetId;
    }
}
