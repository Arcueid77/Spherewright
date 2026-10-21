using Spherewright.Bridge.Core.Progression;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Progression;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class ResearchEnqueueReadbackTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("active")]
    [InlineData("paused")]
    [InlineData("repeated-level")]
    public void NativeAppendPreservesOrderAndProducesIndependentImmediateProof(string mode)
    {
        var before = Progress(); var after = Progress();
        if (mode != "empty")
        {
            before.TechQueue = mode == "repeated-level" ? new() { 3, 3 } : new() { 3, 5 };
            before.CurrentTechId = mode == "paused" ? 0 : 3;
        }
        after.TechQueue = before.TechQueue.Concat(new[] { 1001 }).ToList();
        after.CurrentTechId = mode == "empty" ? 1001 : before.CurrentTechId;
        var proof = ResearchQueuePolicy.ProveEnqueue(before, after, Player(), Player(), 1001);
        Assert.True(proof.InventoryPreserved); Assert.True(proof.ResearchProgressPreserved);
        Assert.Equal(42, proof.CapturedAtGameTick);
        Assert.Equal(before.TechQueue, proof.BeforeQueue); Assert.Equal(after.TechQueue, proof.AfterQueue);
        after.TechQueue.Clear(); before.TechQueue.Clear();
        Assert.Equal(1001, proof.AfterQueue.Last());
    }

    [Theory]
    [InlineData("wrong-target")] [InlineData("missing-append")] [InlineData("extra-append")]
    [InlineData("wrong-head")] [InlineData("context")] [InlineData("planet")]
    [InlineData("different-tick")] [InlineData("player-context")] [InlineData("player-tick")]
    [InlineData("progress")] [InlineData("unlock")] [InlineData("inventory")]
    [InlineData("inc")] [InlineData("slots")] [InlineData("fuel")]
    [InlineData("hand")] [InlineData("research-buffer")] [InlineData("auto-manage")]
    public void UnprovedQueueOrSideEffectNeverProducesPreservationFlags(string fault)
    {
        var before = Progress(); var after = Progress(); var a = Player(); var b = Player();
        after.TechQueue.Add(1001); after.CurrentTechId = 1001;
        switch (fault)
        {
            case "wrong-target": after.TechQueue[0] = 1002; break;
            case "missing-append": after.TechQueue.Clear(); break;
            case "extra-append": after.TechQueue.Add(1002); break;
            case "wrong-head": after.CurrentTechId = 1002; break;
            case "context": after.SessionId = "another-session"; break;
            case "planet": after.PlanetId++; break;
            case "different-tick": after.CapturedAtGameTick++; break;
            case "player-context": b.SessionId = "another-session"; break;
            case "player-tick": b.CapturedAtGameTick++; break;
            case "progress": after.Technologies[0].HashUploaded++; break;
            case "unlock": after.Technologies[0].Unlocked = true; break;
            case "inventory": b.Inventory[0].Count++; break;
            case "inc": b.Inventory[0].Inc++; break;
            case "slots": b.InventoryOccupiedSlotCount++; break;
            case "fuel": b.FuelStorage[0].Count++; break;
            case "hand": b.InHandItem = new() { ItemId = 1001, Count = 1 }; break;
            case "research-buffer": b.MechaResearchItemBuffer[0].PointCount++; break;
            case "auto-manage": b.AutoManageResearchItems = !b.AutoManageResearchItems; break;
        }
        Assert.Throws<InvalidOperationException>(() => ResearchQueuePolicy.ProveEnqueue(before, after, a, b, 1001));
    }

    [Fact]
    public void ReorderingExistingQueueIsNotAnAppend()
    {
        var before = Progress(); before.TechQueue = new() { 3, 5 }; before.CurrentTechId = 3;
        var after = Progress(); after.TechQueue = new() { 5, 3, 1001 }; after.CurrentTechId = 5;
        Assert.Throws<InvalidOperationException>(() => ResearchQueuePolicy.ProveEnqueue(before, after, Player(), Player(), 1001));
    }

    private static ProgressionStateSnapshot Progress() => new()
    {
        SessionId = "synthetic-session", PlanetId = 104, CapturedAtGameTick = 42,
        Technologies = new() { new() { TechId = 1001, HashRequired = 1200, HashUploaded = 0 } },
    };

    private static PlayerStateSnapshot Player() => new()
    {
        SessionId = "synthetic-session", PlanetId = 104, CapturedAtGameTick = 42,
        InventorySlotCount = 10, InventoryOccupiedSlotCount = 1,
        Inventory = new() { new() { ItemId = 1202, Count = 10, Inc = 0, SlotCount = 1 } },
        FuelStorage = new() { new() { ItemId = 1006, Count = 1 } },
        MechaResearchItemBuffer = new() { new() { ItemId = 1202, PointCount = 36, WholeItemCount = 0, RemainderPoints = 36 } },
    };
}
