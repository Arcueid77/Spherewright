using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Logistics;

public static class LogisticsStationFleetTransferPolicy
{
    public static bool TryValidate(
        bool isInterstellar,
        bool isCollector,
        bool isVeinCollector,
        string direction,
        int itemId,
        int count,
        int playerItemCount,
        int playerItemInc,
        int idleDroneCount,
        int workingDroneCount,
        int droneCapacity,
        int idleVesselCount,
        int workingVesselCount,
        int vesselCapacity,
        bool playerCanAcceptWithdrawal,
        out string rejection)
        => TryValidate(isInterstellar, isCollector, isVeinCollector, direction, itemId, count,
            playerItemCount, playerItemInc, idleDroneCount, workingDroneCount, droneCapacity,
            idleVesselCount, workingVesselCount, vesselCapacity, playerCanAcceptWithdrawal,
            false, 0, 0, out rejection);

    public static bool TryValidate(
        bool isInterstellar, bool isCollector, bool isVeinCollector,
        string direction, int itemId, int count, int playerItemCount, int playerItemInc,
        int idleDroneCount, int workingDroneCount, int droneCapacity,
        int idleVesselCount, int workingVesselCount, int vesselCapacity,
        bool playerCanAcceptWithdrawal, bool nativeWarpDriveUnlocked,
        int warperCount, int warperCapacity, out string rejection)
    {
        rejection = string.Empty;
        if (isCollector || isVeinCollector)
        {
            rejection = "Orbital and vein collectors do not expose an ordinary player fleet slot.";
            return false;
        }

        if (direction != LogisticsStationFleetTransferDirections.PlayerToStation
            && direction != LogisticsStationFleetTransferDirections.StationToPlayer)
        {
            rejection = "Fleet transfer direction must be player-to-station or station-to-player.";
            return false;
        }

        if (count <= 0 || count > 100)
        {
            rejection = "Fleet transfer count must be from 1 through 100.";
            return false;
        }

        var isDrone = itemId == LogisticsFleetItemIds.Drone;
        var isVessel = itemId == LogisticsFleetItemIds.Vessel;
        var isWarper = itemId == LogisticsFleetItemIds.Warper;
        if (!isDrone && !isVessel && !isWarper)
        {
            rejection = "Only logistics drones (5001), vessels (5002), or native station warpers (1210) are supported.";
            return false;
        }

        if ((isVessel || isWarper) && !isInterstellar)
        {
            rejection = "A planetary logistics station has no interstellar vessel/warper slot.";
            return false;
        }

        if (isWarper && !nativeWarpDriveUnlocked)
        {
            rejection = "Native logistics-vessel warp-drive technology must be unlocked before using the station warper counter.";
            return false;
        }

        var idle = isWarper ? warperCount : isDrone ? idleDroneCount : idleVesselCount;
        var working = isWarper ? 0 : isDrone ? workingDroneCount : workingVesselCount;
        var capacity = isWarper ? warperCapacity : isDrone ? droneCapacity : vesselCapacity;
        if (idle < 0 || working < 0 || capacity <= 0 || (long)idle + working > capacity)
        {
            rejection = "The station fleet counters or current-version prefab capacity are inconsistent.";
            return false;
        }

        if (direction == LogisticsStationFleetTransferDirections.PlayerToStation)
        {
            if (playerItemCount < count)
            {
                rejection = "The player package contains fewer than the requested fleet items.";
                return false;
            }

            if (playerItemInc != 0)
            {
                rejection = "Proliferated fleet items are rejected because the normal station UI discards their proliferator points.";
                return false;
            }

            if ((long)idle + working + count > capacity)
            {
                rejection = "The requested items exceed the station fleet capacity after working craft are included.";
                return false;
            }

            return true;
        }

        if (idle < count)
        {
            rejection = "Only idle station craft can be withdrawn, and fewer than the requested count are idle.";
            return false;
        }

        if (!playerCanAcceptWithdrawal)
        {
            rejection = "The player package cannot accept the exact requested withdrawal.";
            return false;
        }

        return true;
    }
}
