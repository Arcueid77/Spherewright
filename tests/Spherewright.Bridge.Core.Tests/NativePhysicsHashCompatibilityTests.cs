using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativePhysicsHashCompatibilityTests
{
    [Fact]
    public void PhysicsObservationsDoNotChangeExistingPlayerHashDomains()
    {
        var player = new PlayerStateSnapshot
        {
            SessionId = "synthetic-session", PlanetId = 104,
            IsAlive = true, IsOnPlanet = true, MovementState = "Walk",
            CoreEnergy = 100_000, CoreEnergyCapacity = 200_000,
            Inventory = new List<PlayerInventoryItem>
            {
                new() { ItemId = 1001, Count = 10, SlotCount = 1 },
            },
        };
        var complete = CanonicalStateHash.Player(player);
        var action = CanonicalStateHash.PlayerAction(player);
        player.NativePhysics = new NativePlayerPhysicsSnapshot
        {
            State = "observed", UnityVersion = "synthetic-engine",
            FixedDeltaTimeSeconds = 1f / 60f, MaxDepenetrationVelocity = 10f,
        };
        Assert.Equal(complete, CanonicalStateHash.Player(player));
        Assert.Equal(action, CanonicalStateHash.PlayerAction(player));
        player.NativeLocalControlEnergy = new NativeLocalControlEnergyObservation
        {
            PerNativeTickEnergyUpperJoules = 12345, CostInputsProved = true, IndependentDebitsExcluded = false,
            IndependentDebitsBudgeted = true, IndependentEnergyPerNativeTickUpperJoules = 500,
            ShieldRechargeEnergyPerNativeTickUpperJoules = 300, LaserRechargeEnergyPerNativeTickUpperJoules = 200,
            Blockers = new() { "native_paused_not_execution_ready" },
        };
        Assert.False(player.NativeLocalControlEnergy.ExecutionPremisesProved);
        Assert.Equal(complete, CanonicalStateHash.Player(player));
        Assert.Equal(action, CanonicalStateHash.PlayerAction(player));
        player.NativePhysics.State = "unavailable";
        player.NativePhysics.FixedDeltaTimeSeconds = null;
        Assert.Equal(complete, CanonicalStateHash.Player(player));
        Assert.Equal(action, CanonicalStateHash.PlayerAction(player));

        player.ConstructionDrones.NativeSpeedMetersPerSecond = 20;
        player.ConstructionDrones.NativeEnergyPerMeterJoules = 15000;
        player.ConstructionDrones.NativeEjectEnergyJoules = 100000;
        player.ConstructionDrones.NativeRepairEnergyPerTickJoules = 8000;
        player.ConstructionDrones.NativePendingJumpTicks = 0;
        Assert.Equal(complete, CanonicalStateHash.Player(player));
        Assert.Equal(action, CanonicalStateHash.PlayerAction(player));
        player.ConstructionDrones.NativeSpeedMetersPerSecond = null;
        player.ConstructionDrones.NativeEnergyPerMeterJoules = null;
        player.ConstructionDrones.NativeEjectEnergyJoules = null;
        player.ConstructionDrones.NativeRepairEnergyPerTickJoules = null;
        player.ConstructionDrones.NativePendingJumpTicks = 12;
        Assert.Equal(complete, CanonicalStateHash.Player(player));
        Assert.Equal(action, CanonicalStateHash.PlayerAction(player));

        player.Inventory[0].Count++;
        Assert.NotEqual(complete, CanonicalStateHash.Player(player));
        Assert.NotEqual(action, CanonicalStateHash.PlayerAction(player));
    }
}
