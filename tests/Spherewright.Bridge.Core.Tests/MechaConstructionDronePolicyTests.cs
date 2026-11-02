using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class MechaConstructionDronePolicyTests
{
    [Fact]
    public void DisabledMasterCanBeEnabledWithExactIdlePlayerBinding()
    {
        var (request, player) = Fixture();
        var before = System.Text.Json.JsonSerializer.Serialize(player);
        Assert.Null(MechaConstructionDronePolicy.ValidateEnable(request, player));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(player));
        Assert.NotNull(MechaConstructionDronePolicy.BuildReadiness(player));
        player.ConstructionDrones.Enabled = true;
        Assert.Null(MechaConstructionDronePolicy.BuildReadiness(player));
    }

    [Theory]
    [InlineData("already_enabled")]
    [InlineData("building_disabled")]
    [InlineData("no_drone")]
    [InlineData("working")]
    [InlineData("drift")]
    [InlineData("moving")]
    [InlineData("no_energy")]
    public void FreshHashCannotAuthorizeAnUnsafeOrOppositeToggle(string change)
    {
        var (request, player) = Fixture();
        switch (change)
        {
            case "already_enabled": player.ConstructionDrones.Enabled = true; break;
            case "building_disabled": player.ConstructionDrones.ConstructionEnabled = false; break;
            case "no_drone": player.ConstructionDrones.Alive = 0; break;
            case "working": player.ConstructionDrones.Working = 1; break;
            case "drift": player.MovementState = "Drift"; break;
            case "moving": player.Speed = 1; break;
            case "no_energy": player.CoreEnergy = 0; break;
        }
        player.StateHash = CanonicalStateHash.PlayerAction(player);
        request.ExpectedFactoryStateHash = player.StateHash;
        Assert.NotNull(MechaConstructionDronePolicy.ValidateEnable(request, player));
    }

    [Fact]
    public void ChangedInventoryOrDroneStateInvalidatesTheOriginalInspection()
    {
        var (request, player) = Fixture();
        player.ConstructionDrones.Idle--;
        Assert.NotNull(MechaConstructionDronePolicy.ValidateEnable(request, player));
    }

    [Theory]
    [InlineData("entity")]
    [InlineData("recipe")]
    [InlineData("orbit")]
    [InlineData("station")]
    [InlineData("storage")]
    public void MixedConfigurationParametersNeverExposeAnEnableToken(string change)
    {
        var (request, player) = Fixture();
        switch (change)
        {
            case "entity": request.EntityId = 12; break;
            case "recipe": request.RecipeId = 1; break;
            case "orbit": request.EjectorOrbitId = 0; break;
            case "station": request.StationStorageIndex = 0; break;
            case "storage": request.StorageOperation = "set-bans"; break;
        }
        Assert.NotNull(MechaConstructionDronePolicy.ValidateEnable(request, player));
        Assert.False(MechaConstructionDronePolicy.ConfirmsPreparedEcho(request, Prepared()));
    }

    [Fact]
    public void MissingOrOppositeCohortEchoCannotExposeAnEnableToken()
    {
        var (request, _) = Fixture();
        Assert.True(MechaConstructionDronePolicy.ConfirmsPreparedEcho(request, Prepared()));
        var plan = Prepared();
        plan.PlannedMechaConstructionDronesEnabled = null;
        Assert.False(MechaConstructionDronePolicy.ConfirmsPreparedEcho(request, plan));
        plan.PlannedMechaConstructionDronesEnabled = false;
        Assert.False(MechaConstructionDronePolicy.ConfirmsPreparedEcho(request, plan));
        Assert.False(MechaConstructionDronePolicy.ConfirmsPreparedEcho(request, null));
    }

    private static PreparedNormalAction Prepared() => new()
    {
        Prepared = true, CommitAllowedNow = true, PlanToken = "synthetic-plan",
        ActionKind = NormalActionKinds.ConfigureBuilding, PlannedMechaConstructionDronesEnabled = true,
    };

    private static (PrepareConfigureBuildingRequest, PlayerStateSnapshot) Fixture()
    {
        var player = new PlayerStateSnapshot
        {
            SessionId = "synthetic-owned-session", PlanetId = 101,
            IsAlive = true, IsOnPlanet = true, MovementState = "Walk", CoreEnergy = 1000000,
            CoreEnergyCapacity = 1000000, InventorySlotCount = 40,
        };
        player.ConstructionDrones.Enabled = false;
        player.ConstructionDrones.ConstructionEnabled = true;
        player.ConstructionDrones.Total = player.ConstructionDrones.Alive = player.ConstructionDrones.Idle = 3;
        player.StateHash = CanonicalStateHash.PlayerAction(player);
        return (new PrepareConfigureBuildingRequest
        {
            PlanetId = 101, EntityId = 0, RecipeId = 0,
            Mode = BuildingConfigurationModes.EnableMechaConstructionDrones,
            ExpectedFactoryStateHash = player.StateHash,
        }, player);
    }
}
