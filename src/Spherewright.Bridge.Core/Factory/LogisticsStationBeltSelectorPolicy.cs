namespace Spherewright.Bridge.Core.Factory;

/// <summary>
/// Native station output selectors are one-based, with zero meaning unselected.
/// Switching an already selected output is an ordinary configuration change.
/// </summary>
public static class LogisticsStationBeltSelectorPolicy
{
    public static bool CanChange(int currentSelector, int requestedStorageIndex, int storageSlotCount) =>
        storageSlotCount > 0
        && currentSelector >= 0 && currentSelector <= storageSlotCount
        && requestedStorageIndex >= 0 && requestedStorageIndex < storageSlotCount
        && currentSelector != requestedStorageIndex + 1;
}
