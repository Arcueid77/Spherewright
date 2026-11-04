using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class MechaWarperAutoSupplyPolicyTests
{
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(20)]
    public void CheckboxCanEnableWithoutTouchingEmptyPartialOrFullChamber(int chamber)
    {
        var (request, player, before) = Fixture(chamber);
        Assert.Null(MechaWarperAutoSupplyPolicy.Validate(request, player, before));
        var after = Facts(chamber); after.AutoReplenishWarper = true;
        Assert.True(MechaWarperAutoSupplyPolicy.VerifyReadback(before, after, true));
        // This is a setting path, not an extension of the empty-chamber supply.
        if (chamber != 0) Assert.False(MechaWarperSupplyPolicy.Evaluate(before, 20).Allowed);
    }

    [Theory]
    [InlineData("already-enabled")][InlineData("stale-player")][InlineData("foreign-world")]
    [InlineData("wrong-filter")][InlineData("banned")][InlineData("overfull")]
    [InlineData("moving")][InlineData("drone-working")][InlineData("mixed-parameters")]
    public void InvalidOrStaleTransitionsAreRejected(string fault)
    {
        var (request, player, facts) = Fixture(2);
        switch (fault)
        {
            case "already-enabled": facts.AutoReplenishWarper=true; break;
            case "stale-player": player.InventorySlotCount++; break;
            case "foreign-world": facts.OwnedBindingVerified=false; break;
            case "wrong-filter": facts.ChamberFilterItemId=1802; break;
            case "banned": facts.ChamberBannedGridCount=1; break;
            case "overfull": facts.ChamberCount=21; break;
            case "moving": facts.IsIdle=false; break;
            case "drone-working": player.ConstructionDrones.Working=1; break;
            case "mixed-parameters": request.StationVesselDeliveryMinimumPercent=1; break;
        }
        Assert.NotNull(MechaWarperAutoSupplyPolicy.Validate(request, player, facts));
    }

    [Theory]
    [InlineData("setting-not-changed")][InlineData("package-count")][InlineData("chamber-count")]
    [InlineData("package-inc")][InlineData("chamber-inc")][InlineData("other-package-item")]
    [InlineData("binding")]
    public void ImmediateReadbackRequiresExactConservationAndSettingChange(string fault)
    {
        var before=Facts(2); var after=Facts(2); after.AutoReplenishWarper=true; var package=true;
        switch (fault)
        {
            case "setting-not-changed": after.AutoReplenishWarper=false; break;
            case "package-count": after.PackageCount--; break;
            case "chamber-count": after.ChamberCount++; break;
            case "package-inc": after.PackageInc++; break;
            case "chamber-inc": after.ChamberInc++; break;
            case "other-package-item": package=false; break;
            case "binding": after.CurrentPlanetVerified=false; break;
        }
        Assert.False(MechaWarperAutoSupplyPolicy.VerifyReadback(before,after,package));
    }

    [Fact]
    public void MissingOrFalseCohortEchoDoesNotAdmitAnOldPlugin()
    {
        var (request, _, _) = Fixture(2);
        var plan=new PreparedNormalAction {Prepared=true,CommitAllowedNow=true,
            ActionKind=NormalActionKinds.ConfigureBuilding,PlanToken="synthetic"};
        Assert.False(MechaWarperAutoSupplyPolicy.ConfirmsEcho(request,plan));
        plan.PlannedMechaWarperAutoSupplyEnabled=false;
        Assert.False(MechaWarperAutoSupplyPolicy.ConfirmsEcho(request,plan));
        plan.PlannedMechaWarperAutoSupplyEnabled=true;
        Assert.True(MechaWarperAutoSupplyPolicy.ConfirmsEcho(request,plan));
        plan.ActionKind=NormalActionKinds.SupplyMechaWarpers;
        Assert.False(MechaWarperAutoSupplyPolicy.ConfirmsEcho(request,plan));
        var readback=new MechaWarperAutoSupplyReadback();
        Assert.Null(readback.EnabledAfter); Assert.Null(readback.ChamberCountAfter);
        Assert.False(readback.ReadbackComplete); Assert.False(readback.ConservationVerified);
    }

    private static (PrepareConfigureBuildingRequest,PlayerStateSnapshot,MechaWarperSupplyFacts) Fixture(int chamber)
    {
        var player=new PlayerStateSnapshot {SessionId="synthetic-owned-session",PlanetId=101,
            IsAlive=true,IsOnPlanet=true,MovementState="Walk",CoreEnergy=1000000,InventorySlotCount=40};
        player.StateHash=CanonicalStateHash.PlayerAction(player);
        return(new PrepareConfigureBuildingRequest {PlanetId=101,EntityId=0,RecipeId=0,
            Mode=BuildingConfigurationModes.EnableMechaWarperAutoSupply,ExpectedFactoryStateHash=player.StateHash},player,Facts(chamber));
    }

    private static MechaWarperSupplyFacts Facts(int chamber) => new()
    {
        OwnedBindingVerified=true,CurrentPlanetVerified=true,PlayerStateHashFresh=true,
        IsAlive=true,IsGrounded=true,ControllerEnabled=true,IsIdle=true,
        ChamberIsFiltered=true,ChamberSlotCount=1,ChamberGridCount=1,ChamberItemId=1210,
        ChamberFilterItemId=1210,ChamberStackSize=20,ChamberCount=chamber,PackageCount=40,
    };
}
