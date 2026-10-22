using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    [Fact]
    public void UnrelatedWriteBlockersCannotBeWaivedByAHealthyPhysicalSample()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out var pending)); Assert.False(pending!.Terminal);
        GameMain.data!.gameDesc.isPeaceMode = false;
        f.NormalActions.RunNativeIdleGuardForTest();
        var failed = f.ReadAction(accepted.ActionId);
        Assert.False(failed.Succeeded); Assert.True(GameMain.isPaused);
        Assert.Equal("remaining_write_blockers", failed.NativeFirstFailure!.FirstFailedPredicate);
        Assert.Equal("return_only_checkpoint_write_blocked", failed.NativeFirstFailure.Code);
    }

    [Fact]
    public void ARenderPumpBeforeTheFirst501WaitsWithoutManufacturingNativeHealth()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        f.NormalActions.VoyagerLocalPrePlayerTickAvailable = false;
        f.NormalActions.PumpCheckpointReturnRecoveryOnMainThread();
        var pending = f.Sessions.CaptureOnMainThread();
        Assert.Equal("pending", pending.ReturnOnlyRecovery!.NativeHandoffState);
        Assert.NotNull(NormalGameActionCoordinator.SaveAccessForTest(pending, null));
        Assert.False(GameMain.isPaused);
        f.NormalActions.RunNativeIdleGuardForTest();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void AGapAfterAdmissionFailsClosedWithoutChangingTheCompletedRecovery()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        GameMain.gameTick += 2; f.NormalActions.RunNativeIdleGuardForTest();
        var stopped = f.Sessions.CaptureOnMainThread();
        Assert.True(GameMain.isPaused); Assert.Equal("failed", stopped.ReturnOnlyRecovery!.NativeHandoffState);
        Assert.Equal("native501_sequence", stopped.ReturnOnlyRecovery.FirstFailure!.FirstFailedPredicate);
        Assert.Equal(accepted.ActionId, stopped.WriteQuarantineActionId);
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded); // original success is immutable
    }

    [Fact]
    public void FirstNative501CanAdoptAndOwnCheckpointWithoutAnActionPoll()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        // Host reaches Native501 before a client asks for the load result.
        f.NormalActions.RunNativeIdleGuardForTest();
        Assert.False(GameMain.isPaused);
        Assert.Equal("proved", f.Sessions.CaptureOnMainThread().ReturnOnlyRecovery?.NativeHandoffState);
        var result = f.ReadAction(accepted.ActionId);
        Assert.True(result.Terminal); Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void AReplacementWorldCannotInheritCheckpointNativeHealth()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        GameMain.data = new() { gameName = f.Sessions.Snapshot.SaveName! };
        f.NormalActions.RunNativeIdleGuardForTest();
        Assert.False(GameMain.isPaused); // no authority to pause another world
        Assert.Equal("not_observed", f.Sessions.CaptureOnMainThread().ReturnOnlyRecovery!.NativeHandoffState);
    }

    [Fact]
    public void CheckpointIdentityAloneCannotPermitNormalSaveBeforeNative501()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out var pending));
        Assert.False(pending!.Terminal); Assert.False(pending.Succeeded);
        var s = f.Sessions.CaptureOnMainThread();
        Assert.Equal("pending", s.ReturnOnlyRecovery!.NativeHandoffState);
        Assert.Contains(s.WriteBlockers, b => b.Code == BridgeErrorCodes.BridgeNotReady);
        Assert.NotNull(NormalGameActionCoordinator.SaveAccessForTest(s, null));
        var durable = File.ReadAllBytes(f.JourneyPath);
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out pending));
        Assert.False(pending!.Terminal); Assert.Equal(durable, File.ReadAllBytes(f.JourneyPath));
        GameMain.gameTick++; f.NormalActions.RunNativeIdleGuardForTest();
        var ready = f.ReadAction(accepted.ActionId); Assert.True(ready.Succeeded, ready.Message);
        Assert.Equal("proved", f.Sessions.CaptureOnMainThread().ReturnOnlyRecovery!.NativeHandoffState);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData("dirty", "remote_idle_scene_unproved")]
    [InlineData("ground_unknown", "remote_idle_ground_not_observed")]
    [InlineData("ground_miss", "remote_idle_ground_contact_unproved")]
    [InlineData("speed", "remote_idle_actual_speed_not_at_rest")]
    [InlineData("core_unknown", "remote_idle_energy_unknown")]
    [InlineData("warper_unknown", "return_only_idle_native_reserve_unproved")]
    public void FailedNativeHandoffLatchesSameDecisionAndPreservesOnlyThePausedWorld(string change, string code)
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out var pending)); Assert.False(pending!.Terminal);
        var n = f.NormalActions.NativeIdleObservation;
        switch (change)
        {
            case "dirty": n.Dirty = true; break;
            case "ground_unknown": n.GroundSphereHit = null; break;
            case "ground_miss": n.GroundSphereHit = false; break;
            case "speed": n.Speed = 1; break;
            case "core_unknown": n.Core = null; break;
            case "warper_unknown": n.Warpers = null; break;
        }
        GameMain.gameTick++; f.NormalActions.RunNativeIdleGuardForTest();
        var failed = f.ReadAction(accepted.ActionId);
        Assert.True(failed.Terminal); Assert.False(failed.Succeeded); Assert.Equal(code, failed.NativeFirstFailure!.Code);
        Assert.Equal(GameMain.gameTick, failed.NativeFirstFailure.CapturedAtGameTick);
        Assert.True(GameMain.isPaused); Assert.Equal(WriteHealthStates.Quarantined, f.Sessions.CaptureOnMainThread().WriteHealth);
        var first = Spherewright.Plugin.Transport.PluginJson.Serialize(failed.NativeFirstFailure);
        f.NormalActions.NativeIdleObservation = new(); GameMain.gameTick++;
        f.NormalActions.RunNativeIdleGuardForTest();
        Assert.Equal(first, Spherewright.Plugin.Transport.PluginJson.Serialize(f.ReadAction(accepted.ActionId).NativeFirstFailure));
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void RuntimeReferenceLossAfterPreflightCannotUseTheOldFlightOrAnotherPlayer()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out var pending)); Assert.False(pending!.Terminal);
        GameMain.mainPlayer.controller = new(); GameMain.gameTick++;
        f.NormalActions.RunNativeIdleGuardForTest();
        var failed = f.ReadAction(accepted.ActionId);
        Assert.False(failed.Succeeded); Assert.Equal("same_native_controller", failed.NativeFirstFailure!.FirstFailedPredicate);
        Assert.Equal(accepted.ActionId, f.Sessions.CaptureOnMainThread().WriteQuarantineActionId);
        Assert.NotEqual(f.Record.ActiveActionId, f.Sessions.CaptureOnMainThread().WriteQuarantineActionId);
    }

    [Fact]
    public void FailedCheckpointHealthCanPreserveProgressWithoutInventingAnAdmissionOrResumeCredential()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out var pending)); Assert.False(pending!.Terminal);
        f.NormalActions.NativeIdleObservation.GroundSphereHit = null;
        GameMain.gameTick++; f.NormalActions.RunNativeIdleGuardForTest();
        var failed = f.ReadAction(accepted.ActionId); Assert.False(failed.Succeeded);
        var ledger = File.ReadAllBytes(f.JourneyPath); var checkpoint = File.ReadAllBytes(f.TicketPath);
        var before = GameMain.gameTick;
        UnityEngine.Time.frameCount++; f.Sessions.ObservePausedOwnedSaveOnMainThread();
        UnityEngine.Time.frameCount++; f.Sessions.ObservePausedOwnedSaveOnMainThread();
        Assert.True(f.Sessions.TryBindPausedOwnedSave(out var binding, out var rejection), rejection);
        var s = f.Sessions.CaptureOnMainThread();
        Assert.Null(NormalGameActionCoordinator.SaveAccessForTest(s, binding));
        Assert.NotNull(NormalGameActionCoordinator.ReturnOnlyCommitAccessForTest(s, Spherewright.Contracts.Actions.NormalActionKinds.Move, false));
        var originalSave = GameSave.FlightStartSave;
        GameSave.FlightStartSave = name => { Fixture.WriteSave(name, GameMain.gameTick); return true; };
        try
        {
            Assert.True(f.Sessions.TrySaveOwnedWorldNowOnMainThread(out var error, binding), error);
            Assert.Equal(before, GameMain.gameTick); Assert.True(GameMain.isPaused);
            Assert.Equal(WriteHealthStates.Quarantined, f.Sessions.CaptureOnMainThread().WriteHealth);
            Assert.False(f.Sessions.Snapshot.RestartResumeAvailable);
            Assert.Null(f.Sessions.Snapshot.RestartResumeToken);
            var after=f.Store.VoyagerJourney!;
            var original=Spherewright.Plugin.Transport.PluginJson.Deserialize<Spherewright.Plugin.RuntimeDescriptor.VoyagerJourneyRecord>(System.Text.Encoding.UTF8.GetString(ledger))!;
            Assert.NotNull(after.ReturnOnlyTask!.PreservationReceipt);
            Assert.Equal(before,after.ReturnOnlyTask.PreservationReceipt!.GameTick);
            after.ReturnOnlyTask.PreservationReceipt=null;
            Assert.Equal(Spherewright.Plugin.Transport.PluginJson.Serialize(original),Spherewright.Plugin.Transport.PluginJson.Serialize(after));
            Assert.Equal(checkpoint, File.ReadAllBytes(f.TicketPath));
            Assert.False(f.ReadAction(accepted.ActionId).Succeeded); Assert.Equal(1, DSPGame.DispatchCalls);
        }
        finally { GameSave.FlightStartSave = originalSave; }
    }

    [Fact]
    public void PersistedJourneyCanBeReopenedWithoutReusingOrChangingNativeReferences()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out var pending)); Assert.False(pending!.Terminal);
        var reopened = f.ReopenStore().VoyagerJourney!;
        Assert.NotSame(f.Store.VoyagerJourney, reopened);
        Assert.True(f.Store.TryPersistVoyagerJourney(reopened, out var error), error);
        GameMain.gameTick++; f.NormalActions.RunNativeIdleGuardForTest();
        Assert.True(f.ReadAction(accepted.ActionId).Succeeded);
        for (var i = 0; i < 3; i++) { GameMain.gameTick++; f.NormalActions.RunNativeIdleGuardForTest(); }
        Assert.False(GameMain.isPaused); Assert.Equal(WriteHealthStates.Healthy, f.Sessions.CaptureOnMainThread().WriteHealth);
    }

    [Fact]
    public void Entered601EnergyTailIsFundedAndCannotReplaceTheFirstFailure()
    {
        using var f = new Fixture(returnLeg: true, recoveryPhase: "active", originalWindowExpired: true);
        var accepted = DispatchReturnOnly(f, NewReturnOnlyProposal(f)); f.AdoptExactCheckpoint();
        Assert.True(f.Coordinator.TryGetActionResultOnMainThread(accepted.ActionId, out var pending)); Assert.False(pending!.Terminal);
        var n = f.NormalActions.NativeIdleObservation;
        var cost = f.NormalActions.ContractMovementCostForTest(); Assert.True(cost.Proved);
        Assert.True(VoyagerLocalWalkReservePolicy.CanFund(n.Core, n.Loaded, f.Record.ReturnEnergyFloorJoules,
            cost.PerNativeTickEnergyJoules, VoyagerLocalWalkReservePolicy.StopGameTicks));
        var core = n.Core!.Value; var loaded = n.Loaded!.Value;
        n.GroundSphereHit = null; GameMain.gameTick++; f.NormalActions.RunNativeIdleGuardForTest();
        var failed = f.ReadAction(accepted.ActionId); var first = Spherewright.Plugin.Transport.PluginJson.Serialize(failed.NativeFirstFailure);
        // DSP29151 Pause can leave this entered601. This is a declared energy
        // contract substitute; it does not execute Unity's real601 or generation.
        n.Core -= cost.PerNativeTickEnergyJoules; n.Loaded -= cost.PerNativeTickEnergyJoules;
        Assert.True(n.Core > 0); Assert.True(n.Loaded > f.Record.ReturnEnergyFloorJoules);
        Assert.Equal(core - cost.PerNativeTickEnergyJoules, n.Core); Assert.Equal(loaded - cost.PerNativeTickEnergyJoules, n.Loaded);
        failed.NativeFirstFailure!.Predicates["external_mutation"] = "true";
        var next = f.ReadAction(accepted.ActionId); Assert.Equal(first, Spherewright.Plugin.Transport.PluginJson.Serialize(next.NativeFirstFailure));
        Assert.True(GameMain.isPaused); Assert.Equal(f.Record.MaximumGameTicks, f.Store.VoyagerJourney!.SpentGameTicks);
    }
}
