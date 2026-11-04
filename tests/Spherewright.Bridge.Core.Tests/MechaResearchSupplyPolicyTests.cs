using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class MechaResearchSupplyPolicyTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void EmptyBufferSwitchHasExactOppositeSettingEcho(bool desired)
    {
        var (r,p)=Fixture(desired);Assert.Null(MechaResearchSupplyPolicy.Validate(r,p));
        var q=new PreparedNormalAction {Prepared=true,CommitAllowedNow=true,PlanToken="synthetic-plan",
            ActionKind=NormalActionKinds.ConfigureBuilding,PlannedMechaResearchAutoSupplyEnabled=desired};
        Assert.True(MechaResearchSupplyPolicy.ConfirmsEcho(r,q));
        q.PlannedMechaResearchAutoSupplyEnabled=null;Assert.False(MechaResearchSupplyPolicy.ConfirmsEcho(r,q));
        q.PlannedMechaResearchAutoSupplyEnabled=!desired;Assert.False(MechaResearchSupplyPolicy.ConfirmsEcho(r,q));
        p.AutoManageResearchItems=desired;Assert.NotNull(MechaResearchSupplyPolicy.Validate(r,p));
    }
    [Theory]
    [InlineData(1)][InlineData(3599)][InlineData(3600)][InlineData(36030)]
    public void AnyBufferedPointsBlockNativeFloorAndClearTakeback(int points)
    {
        var (r,p)=Fixture(false);p.MechaResearchItemBuffer.Add(new MechaResearchItemSnapshot
            {ItemId=6002,PointCount=points,WholeItemCount=points/3600,RemainderPoints=points%3600});
        p.StateHash=CanonicalStateHash.PlayerAction(p);r.ExpectedFactoryStateHash=p.StateHash;
        Assert.Equal("mecha_research_supply_requires_empty_point_buffer",MechaResearchSupplyPolicy.Validate(r,p));
    }
    [Theory]
    [InlineData("entity")][InlineData("station")][InlineData("recipe")][InlineData("mode")][InlineData("inventory")]
    public void MixedParametersAndStaleInventoryAreRefused(string fault)
    {
        var (r,p)=Fixture(false);
        switch(fault)
        {
            case "entity":r.EntityId=1;break;case "station":r.StationVesselDeliveryMinimumPercent=1;break;
            case "recipe":r.RecipeId=9;break;case "mode":r.Mode="Disable-Mecha-Research-Auto-Supply";break;
            case "inventory":p.InventorySlotCount++;break;
        }
        Assert.NotNull(MechaResearchSupplyPolicy.Validate(r,p));
    }
    private static (PrepareConfigureBuildingRequest,PlayerStateSnapshot) Fixture(bool desired)
    {
        var p=new PlayerStateSnapshot {SessionId="synthetic-owned-session",PlanetId=101,IsAlive=true,
            IsOnPlanet=true,MovementState="Walk",CoreEnergy=1000000,CoreEnergyCapacity=1000000,
            InventorySlotCount=40,AutoManageResearchItems=!desired};p.StateHash=CanonicalStateHash.PlayerAction(p);
        return(new PrepareConfigureBuildingRequest {PlanetId=101,EntityId=0,RecipeId=0,
            Mode=desired?BuildingConfigurationModes.EnableMechaResearchAutoSupply:BuildingConfigurationModes.DisableMechaResearchAutoSupply,
            ExpectedFactoryStateHash=p.StateHash},p);
    }
}
