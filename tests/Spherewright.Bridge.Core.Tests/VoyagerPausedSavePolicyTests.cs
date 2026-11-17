using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerPausedSavePolicyTests
{
    [Fact]
    public void OnlyBoundPausedSerializationCanAvoidTheRemoteIdleController()
    {
        Assert.True(VoyagerPausedSavePolicy.UsesBoundPreservation(NormalActionKinds.Save, true));
        Assert.False(VoyagerPausedSavePolicy.UsesBoundPreservation(NormalActionKinds.Save, false));
        Assert.True(VoyagerPausedSavePolicy.UsesBoundPreservation(NormalActionKinds.ReturnToMainMenu, true));
        Assert.False(VoyagerPausedSavePolicy.UsesBoundPreservation(NormalActionKinds.ReturnToMainMenu, false));
        Assert.False(VoyagerPausedSavePolicy.UsesBoundPreservation(NormalActionKinds.Build, true));
        Assert.False(VoyagerPausedSavePolicy.UsesBoundPreservation(null, true));
    }
}
