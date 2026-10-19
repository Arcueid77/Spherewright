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
        player.NativePhysics.State = "unavailable";
        player.NativePhysics.FixedDeltaTimeSeconds = null;
        Assert.Equal(complete, CanonicalStateHash.Player(player));
        Assert.Equal(action, CanonicalStateHash.PlayerAction(player));

        player.Inventory[0].Count++;
        Assert.NotEqual(complete, CanonicalStateHash.Player(player));
        Assert.NotEqual(action, CanonicalStateHash.PlayerAction(player));
    }
}
