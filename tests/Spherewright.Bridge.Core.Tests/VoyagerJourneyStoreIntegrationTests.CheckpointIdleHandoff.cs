using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    [Fact]
    public void ReturnOnlyReloadMustNotCompleteBeforeTheProductionNativeIdleGuardCanOwnIt()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f));
        f.AdoptExactCheckpoint();
        f.Sessions.BindCheckpointNativeWorldFromSnapshotForTest(f.Store);
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out var result));
        // Before the repair the real reload coordinator reports success here,
        // then the linked real next-frame guard quarantines the same world.
        f.NormalActions.RunNativeIdleGuardForTest();
        Assert.NotEqual(WriteHealthStates.Quarantined, f.Sessions.Snapshot.WriteHealth);
        Assert.False(GameMain.isPaused);
        Assert.False(result!.Terminal); // first result awaits exact Native501 admission
        var completed = f.ReadAction(accepted.ActionId);
        Assert.True(completed.Terminal); Assert.True(completed.Succeeded);
    }
}
