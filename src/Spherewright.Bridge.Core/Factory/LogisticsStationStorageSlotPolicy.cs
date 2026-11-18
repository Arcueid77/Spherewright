namespace Spherewright.Bridge.Core.Factory;

/// <summary>Distinguishes a configured item from inventory actually in the slot.</summary>
public static class LogisticsStationStorageSlotPolicy
{
    public static bool CanAssign(int currentItem, int currentCount, int currentInc,
        int localOrder, int remoteOrder, int requestedItem) =>
        currentItem >= 0 && currentCount >= 0 && currentInc >= 0 && requestedItem > 0
        && localOrder == 0 && remoteOrder == 0
        && (currentItem == requestedItem || currentCount == 0 && currentInc == 0);
}
