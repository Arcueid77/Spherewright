using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeMotionObservationHashTests
{
    [Fact]
    public void CopiedNativeMotionIsAnObservationRatherThanAnActionCredentialOrIdleProjection()
    {
        var p=new PlayerStateSnapshot();var before=CanonicalStateHash.PlayerAction(p);
        p.NativePhysics=new NativePlayerPhysicsSnapshot {FixedDeltaTimeSeconds=.125f,ControllerMovementStateInFrame="Drift",
            CurrentOrderPresent=false,CurrentOrderTargetReached=null,QueuedOrderCount=0,
            WalkRtsVelocity=new Vector3Snapshot {X=12},DriftRtsVelocity=new Vector3Snapshot {X=2.4f},
            WalkManualVelocity=new(),DriftManualVelocity=new(),ControllerVerticalSpeed=-2.27f};
        Assert.Equal(before,CanonicalStateHash.PlayerAction(p));
        Assert.False(p.NativePhysics.SafetyCertified);Assert.Equal("Drift",p.NativePhysics.ControllerMovementStateInFrame);
        Assert.Equal(2.4f,p.NativePhysics.DriftRtsVelocity.X);Assert.Null(p.NativePhysics.CurrentOrderTargetReached);
    }
}
