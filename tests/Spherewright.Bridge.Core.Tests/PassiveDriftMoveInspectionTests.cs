using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class PassiveDriftMoveInspectionTests
{
    private static readonly DateTimeOffset Utc = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReleasedLowSpeedHoverCanBindAnInventoryInteraction()
    {
        var player = Hovering();
        var hash = player.StateHash;
        Assert.Null(PassiveDriftMoveInspection.Capture(player, 10, Utc));
        var inspection = PassiveDriftMoveInspection.Capture(player, 10, Utc, allowHover: true)!;
        player.Position.X += .011f;
        player.CoreEnergy -= 4000;
        Assert.True(inspection.Allows(hash, player, 10.4));
        Assert.Equal(Utc.AddSeconds(2), inspection.ExpiresAtUtc);
    }

    [Theory]
    [InlineData("speed")] [InlineData("space")] [InlineData("inventory")] [InlineData("mode")]
    [InlineData("expired")] [InlineData("distance")] [InlineData("drone")]
    public void HoverInteractionRetainsItsOriginalMotionIdentityAndInventoryBounds(string change)
    {
        var player = Hovering();
        var hash = player.StateHash;
        var inspection = PassiveDriftMoveInspection.Capture(player, 10, Utc, allowHover: true)!;
        var now = 10.4;
        switch (change)
        {
            case "speed": player.Speed = .151f; break;
            case "space": player.IsOnPlanet = false; break;
            case "inventory": player.Inventory[0].Count++; break;
            case "mode": player.MovementState = "Drift"; player.IsFlying = false; break;
            case "expired": now = 12.001; break;
            case "distance": player.Position.X += .051f; break;
            case "drone": player.ConstructionDrones.Working = 1; break;
        }
        Assert.False(inspection.Allows(hash, player, now));
    }

    private static PlayerStateSnapshot Hovering()
    {
        var player = Player();
        player.MovementState = "Fly";
        player.IsFlying = true;
        player.StateHash = CanonicalStateHash.PlayerAction(player);
        return player;
    }

    [Fact]
    public void CentimetreBucketCrossingKeepsDefaultHashStrictButAllowsBoundedIdleDrift()
    {
        var player = Player();
        var hash = player.StateHash;
        var inspection = PassiveDriftMoveInspection.Capture(player, 10, Utc)!;
        player.Position.X += 0.011f;
        player.CoreEnergy -= 91_151;
        Assert.NotEqual(hash, CanonicalStateHash.PlayerAction(player));
        Assert.True(inspection.Allows(hash, player, 10.4));
        // The same inspection is used at commit. Prepare never extends its age.
        Assert.True(inspection.Allows(hash, player, 11.9));
        Assert.False(inspection.Allows(hash, player, 12.001));
        Assert.Equal(Utc.AddSeconds(2), inspection.Describe().ExpiresAtUtc);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("planet")]
    [InlineData("movement")]
    [InlineData("dead")]
    [InlineData("off-planet")]
    [InlineData("flying")]
    [InlineData("inventory-count")]
    [InlineData("inventory-inc")]
    [InlineData("inventory-slot")]
    [InlineData("held")]
    [InlineData("fuel")]
    [InlineData("reactor-item")]
    [InlineData("zero-core")]
    [InlineData("reactor-zero-boundary")]
    [InlineData("capacity")]
    [InlineData("forge")]
    [InlineData("drone-working")]
    [InlineData("drone-build")]
    [InlineData("drone-repair")]
    [InlineData("speed")]
    [InlineData("displacement")]
    [InlineData("nan-position")]
    [InlineData("infinite-energy")]
    public void RejectsChangedStateAndUnsafeDrift(string change)
    {
        var player = Player();
        var inspection = PassiveDriftMoveInspection.Capture(player, 10, Utc)!;
        var originalHash = player.StateHash;
        switch (change)
        {
            case "session": player.SessionId = "other"; break;
            case "planet": player.PlanetId++; break;
            case "movement": player.MovementState = "Walk"; break;
            case "dead": player.IsAlive = false; break;
            case "off-planet": player.IsOnPlanet = false; break;
            case "flying": player.IsFlying = true; break;
            case "inventory-count": player.Inventory[0].Count++; break;
            case "inventory-inc": player.Inventory[0].Inc++; break;
            case "inventory-slot": player.Inventory[0].SlotCount++; break;
            case "held": player.InHandItem = new() { ItemId = 1005, Count = 1 }; break;
            case "fuel": player.FuelStorage[0].Count--; break;
            case "reactor-item": player.ReactorItemId++; break;
            case "zero-core": player.CoreEnergy = 0; break;
            case "reactor-zero-boundary": player.ReactorEnergy = 0; break;
            case "capacity": player.CoreEnergyCapacity++; break;
            case "forge": player.HandcraftQueue.Add(new() { Inputs = new() { new() { ItemId = 1005, Count = 1 } } }); break;
            case "drone-working": player.ConstructionDrones.Working = 1; break;
            case "drone-build": player.ConstructionDrones.PendingBuildTargets = 1; break;
            case "drone-repair": player.ConstructionDrones.PendingRepairTargets = 1; break;
            case "speed": player.Speed = 0.151f; break;
            case "displacement": player.Position.X += 0.051f; break;
            case "nan-position": player.Position.Y = float.NaN; break;
            case "infinite-energy": player.CoreEnergy = double.PositiveInfinity; break;
            default: throw new InvalidOperationException(change);
        }
        Assert.False(inspection.Allows(originalHash, player, 10.2));
    }

    [Fact]
    public void RejectsUnknownHashAndNonMonotonicOrNonFiniteAge()
    {
        var player = Player();
        var inspection = PassiveDriftMoveInspection.Capture(player, 10, Utc)!;
        Assert.False(inspection.Allows("fabricated", player, 10.2));
        Assert.False(inspection.Allows(player.StateHash, player, 9.99));
        Assert.False(inspection.Allows(player.StateHash, player, double.NaN));
        Assert.False(inspection.Allows(player.StateHash, player, double.PositiveInfinity));
        Assert.Null(PassiveDriftMoveInspection.Capture(player, -1, Utc));
        player.StateHash = "fabricated";
        Assert.Null(PassiveDriftMoveInspection.Capture(player, 10, Utc));
    }

    [Fact]
    public void BindingDoesNotBorrowMutableSnapshotOrDescriptionPosition()
    {
        var player = Player();
        var hash = player.StateHash;
        var inspection = PassiveDriftMoveInspection.Capture(player, 10, Utc)!;
        player.Position.X += 1;
        inspection.Describe().OriginPosition.X += 1;
        Assert.False(inspection.Allows(hash, player, 10.2));
        Assert.Equal(-105.79f, inspection.Describe().OriginPosition.X);
    }

    [Fact]
    public void NormalEnergyDrainRetainsExistingPositiveEnergySemantics()
    {
        var player = Player();
        var hash = player.StateHash;
        var inspection = PassiveDriftMoveInspection.Capture(player, 10, Utc)!;
        player.CoreEnergy = 1;
        player.ReactorEnergy = 1;
        Assert.True(inspection.Allows(hash, player, 10.2));
        player.CoreEnergy = -1;
        Assert.False(inspection.Allows(hash, player, 10.2));
    }

    private static PlayerStateSnapshot Player()
    {
        var player = new PlayerStateSnapshot
        {
            SessionId = "owned", PlanetId = 104, CapturedAtGameTick = 100,
            IsAlive = true, IsOnPlanet = true, MovementState = "Drift", Speed = 0.12f,
            Position = new Vector3Snapshot { X = -105.79f, Y = 111.07f, Z = -130 },
            CoreEnergy = 1_000_000_000, CoreEnergyCapacity = 2_000_000_000,
            ReactorEnergy = 100, ReactorItemId = 1802,
            InventorySlotCount = 40, InventoryOccupiedSlotCount = 1,
            Inventory = new() { new() { ItemId = 2001, Count = 147, SlotCount = 1 } },
            FuelStorageSlotCount = 4, FuelStorageOccupiedSlotCount = 1,
            FuelStorage = new() { new() { ItemId = 1802, Count = 3, SlotCount = 1 } },
            ConstructionDrones = new() { Enabled = true, ConstructionEnabled = true, Total = 3, Alive = 3, Idle = 3 },
        };
        player.StateHash = CanonicalStateHash.PlayerAction(player);
        return player;
    }
}
