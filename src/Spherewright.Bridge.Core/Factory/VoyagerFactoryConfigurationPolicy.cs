using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Factory;

public static class VoyagerFactoryConfigurationPolicy
{
    // Normal prepare/commit still validates exact local entity, Native method,
    // inventory preservation and one fresh finite journey budget. In particular
    // the generic drone toggle cannot start unbudgeted remote construction.
    public static bool IsImmediateMode(string mode) => mode == BuildingConfigurationModes.LogisticsStationStorage
        || mode == BuildingConfigurationModes.LogisticsStationBelt || mode == BuildingConfigurationModes.LogisticsStationCharge
        || mode == BuildingConfigurationModes.Production || mode == BuildingConfigurationModes.SorterFilter
        || mode == BuildingConfigurationModes.StorageCapacity || mode == BuildingConfigurationModes.EjectorOrbit
        || mode == BuildingConfigurationModes.MinimalDysonReceivingNode || mode == BuildingConfigurationModes.DysonStructure;
}
