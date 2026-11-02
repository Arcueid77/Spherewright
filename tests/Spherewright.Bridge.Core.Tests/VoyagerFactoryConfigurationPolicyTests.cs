using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerFactoryConfigurationPolicyTests
{
    [Theory]
    [InlineData(BuildingConfigurationModes.Production)]
    [InlineData(BuildingConfigurationModes.SorterFilter)]
    [InlineData(BuildingConfigurationModes.StorageCapacity)]
    [InlineData(BuildingConfigurationModes.EjectorOrbit)]
    [InlineData(BuildingConfigurationModes.LogisticsStationStorage)]
    [InlineData(BuildingConfigurationModes.LogisticsStationBelt)]
    [InlineData(BuildingConfigurationModes.LogisticsStationCharge)]
    public void NormalFactoryModesUseExistingFiniteImmediateJourneyAdmission(string mode) =>
        Assert.True(VoyagerFactoryConfigurationPolicy.IsImmediateMode(mode));

    [Theory]
    [InlineData(BuildingConfigurationModes.EnableMechaConstructionDrones)]
    [InlineData(BuildingConfigurationModes.Research)]
    [InlineData("Production")]
    [InlineData("")]
    [InlineData("future-mode")]
    public void UnbudgetedDroneDispatchAndUnknownModesCannotBorrowImmediateAdmission(string mode) =>
        Assert.False(VoyagerFactoryConfigurationPolicy.IsImmediateMode(mode));
}
