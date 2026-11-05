using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class PausedSavedSpaceReplayTests
{
    [Fact]
    public void RealPausedSailCutPassesFrozenPoseScopedCommitAndUiConservationWithoutGroundProof()
    {
        using var input=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","paused-saved-sail-momentum.json")));
        var options=new JsonSerializerOptions {PropertyNameCaseInsensitive=true};
        var s=input.RootElement.GetProperty("session").Deserialize<SessionState>(options)!;
        var p=input.RootElement.GetProperty("player").Deserialize<PlayerStateSnapshot>(options)!;
        Assert.Null(p.NativePhysics);Assert.Null(p.NativeLocalControlEnergy);Assert.False(p.IsOnPlanet);
        Assert.Equal("Sail",p.MovementState);Assert.True(p.SpaceTransit!.Speed>.5);
        Assert.False(p.SpaceTransit.FrozenSavedPoseVerified); // Old cohort supplied no such proof.
        var world=new object();var actor=new object();var controller=new object();
        var g=PausedSavedSpacePoseGuard.Capture(world,actor,controller,s.GameTick!.Value,p.CapturedAtGameTick,p.MovementState,p.SpaceTransit);
        Assert.NotNull(g);var authorized=g!.Matches(world,actor,controller,p.CapturedAtGameTick,p.MovementState,p.SpaceTransit);
        var permit=new PausedSavedSpacePermit(s.SessionId!,s.WriteQuarantineActionId!,s.WriteBlockers.Single().Message,s.GameTick.Value);
        Assert.True(permit.Matches(s));Assert.True(authorized);
        Assert.Null(NormalGameActionCoordinator.PausedSpaceInventoryCommitForTest(s,permit,NormalActionKinds.SupplyMechaWarpers,true));
        Assert.NotNull(NormalGameActionCoordinator.PausedSpaceInventoryCommitForTest(s,permit,NormalActionKinds.Move,true));
        // The slot shape below is the pinned Native contract, not a claim that
        // this read-only capture executed a UI transfer or observed its slots.
        MechaWarperSupplyFacts Stock(int chamber,int package)=>new() {
            OwnedBindingVerified=authorized,PlayerStateHashFresh=true,IsAlive=p.IsAlive,ControllerEnabled=true,IsIdle=true,
            CurrentPlanetVerified=p.IsOnPlanet,IsGrounded=p.MovementState=="Walk",PausedSpaceSourceAuthorized=authorized,
            PausedSpaceSpeed=p.SpaceTransit.Speed,PausedSpacePoseFingerprint=g.Fingerprint,
            ChamberIsFiltered=true,ChamberSlotCount=1,ChamberGridCount=1,ChamberItemId=1210,ChamberFilterItemId=1210,
            ChamberStackSize=20,ChamberCount=chamber,PackageCount=package };
        var before=Stock(2,20);var after=Stock(20,2);
        Assert.False(MechaWarperSupplyPolicy.Evaluate(before,18).Allowed);
        Assert.True(MechaWarperUiTopUpPolicy.Evaluate(before,18).Allowed);
        Assert.True(MechaWarperUiTopUpPolicy.Verify(before,after,18,true).Succeeded);
        after.PausedSpacePoseFingerprint="different-native-pose";
        Assert.False(MechaWarperUiTopUpPolicy.Verify(before,after,18,true).Succeeded);
        g.Invalidate();Assert.False(g.Matches(world,actor,controller,p.CapturedAtGameTick,p.MovementState,p.SpaceTransit));
        Assert.False(s.WritesAllowed); // UI authority never becomes general healthy execution.
    }
}
