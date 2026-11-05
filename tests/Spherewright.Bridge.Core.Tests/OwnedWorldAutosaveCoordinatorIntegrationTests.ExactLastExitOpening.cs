using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;
public sealed partial class OwnedWorldAutosaveCoordinatorIntegrationTests
{
    [Fact]
    public void LatestLastExitOpensPausedWithoutSelectingOlderPrimaryOrResumingTasks()
    {
        using var f=new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        var last=Path.Combine(Path.GetDirectoryName(f.PrimaryPath)!,"_lastexit_.dsv");
        Fixture.WriteSave(last,Identity,CandidateTick+100);
        var primary=File.ReadAllBytes(f.PrimaryPath);var exit=File.ReadAllBytes(last);
        var coordinator=f.NewCoordinator();var prepared=coordinator.PrepareOnMainThread(new PrepareOwnedWorldResumeRequest
        {ResumeToken=f.Store.CurrentResumeToken!,RecoveryMode=OwnedWorldResumeModes.OpenSavedLastExit,
            MinimumRecoveryGameTick=CandidateTick+100,ExpectedRecoveryGameTick=CandidateTick+100});
        Assert.True(prepared.Success,prepared.Error?.Message);Assert.True(OwnedLastExitOpenPolicy.ConfirmsEcho(prepared.Value!));
        var accepted=coordinator.CommitOnMainThread(f.Confirmed(prepared.Value!));
        Assert.True(accepted.Success,accepted.Error?.Message);Assert.Equal("_lastexit_",DSPGame.LoadedName);
        Assert.True(f.Sessions.SavedOpenAnyNativeLocation);Assert.True(f.Sessions.SavedOpenObserverBound);
        Assert.Equal(primary,File.ReadAllBytes(f.PrimaryPath));Assert.Equal(exit,File.ReadAllBytes(last));
    }
}
