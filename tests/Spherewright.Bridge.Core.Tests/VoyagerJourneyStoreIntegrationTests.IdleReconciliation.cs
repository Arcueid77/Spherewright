using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    private static Fixture PausedOrdinaryIdle()
    {
        GameMain.IdleReconcileResumeCalls = 0; GameMain.IgnoreIdleResume = false;
        var f = new Fixture(recoveryPhase: "active");
        var j = f.Store.VoyagerJourney!;
        var s = f.Sessions.Snapshot;
        s.GameLoaded = s.OwnedBySpherewright = true; s.SessionId = Fixture.SourceSessionId;
        s.SaveName = Fixture.Identity; s.GameVersion = "0.10.35.29151"; s.LocalPlanetId = 201; s.GameTick = 130;
        s.WriteHealth = WriteHealthStates.Healthy; s.WritesAllowed = true; s.WriteBlockers.Clear();
        f.Sessions.BindCheckpointNativeWorldFromSnapshotForTest(f.Store);
        Assert.True(f.Store.TryMarkFlightSucceeded(f.Ticket.CheckpointId, j.ActiveActionId, 130, out var error), error);
        j.Phase = "remote_landed"; j.RemotePlanetId = 201; j.LastLandedGameTick = 130;
        j.SpentGameTicks = 30; j.LastAccountedGameTick = 130;
        Assert.True(f.Store.TryPersistVoyagerJourney(j, out error), error);
        f.NormalActions.BindSuccessfulIdleForReconciliationTest();
        f.NormalActions.NativeIdleObservation.Dirty = true;
        f.NormalActions.RunNativeIdleGuardForTest();
        Assert.True(GameMain.isPaused);
        Assert.Equal(WriteHealthStates.Quarantined, f.Sessions.CaptureOnMainThread().WriteHealth);
        f.NormalActions.NativeIdleObservation.Dirty = false;
        return f;
    }

    [Fact]
    public void RecoveredSameSessionIdleCanBeProvedWithoutLoadingOrChangingTheJourney()
    {
        using var f = PausedOrdinaryIdle();
        var ledger = File.ReadAllBytes(f.JourneyPath); var tick = GameMain.gameTick;
        Assert.True(f.NormalActions.ProvePausedIdleForTest(out var proof, out var error), error);
        Assert.StartsWith("sha256:", proof);
        Assert.True(GameMain.isPaused); // Prepare is observation only.
        Assert.Equal(tick, GameMain.gameTick);
        Assert.Equal(ledger, File.ReadAllBytes(f.JourneyPath));
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.Equal(0, GameMain.IdleReconcileResumeCalls);
    }

    [Fact]
    public void CommitResumesOnlyThisPauseAndLeavesFailedMovesAndOriginalLedgerUntouched()
    {
        using var f = PausedOrdinaryIdle();
        var failed = f.NormalActions.RetainLocalResultForIdleTest(NormalActionStates.ActionFailed, true);
        var ledger = File.ReadAllBytes(f.JourneyPath); var tick = GameMain.gameTick;
        Assert.True(f.NormalActions.ProvePausedIdleForTest(out var proof, out var error), error);
        Assert.True(f.NormalActions.ResumePausedIdleForTest(proof, out error), error);
        Assert.False(GameMain.isPaused);
        Assert.Equal(WriteHealthStates.Healthy, f.Sessions.CaptureOnMainThread().WriteHealth);
        Assert.True(f.Sessions.CaptureOnMainThread().WritesAllowed);
        Assert.Equal(1, GameMain.IdleReconcileResumeCalls);
        Assert.Equal(NormalActionStates.ActionFailed, failed.State); Assert.Equal("original result", failed.Message);
        Assert.Equal(tick, GameMain.gameTick); Assert.Equal(ledger, File.ReadAllBytes(f.JourneyPath));
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.False(f.NormalActions.ResumePausedIdleForTest(proof, out _));
        Assert.Equal(1, GameMain.IdleReconcileResumeCalls);
    }

    [Theory]
    [InlineData("unknown_action")]
    [InlineData("active_action")]
    [InlineData("scene_dirty")]
    [InlineData("ground_unknown")]
    [InlineData("ground_absent")]
    [InlineData("moving")]
    [InlineData("rts_tail")]
    [InlineData("manual_tail")]
    [InlineData("physics_tail")]
    [InlineData("physics_unknown")]
    [InlineData("core_empty")]
    [InlineData("warper_reserve")]
    [InlineData("expired_return_window")]
    [InlineData("replacement_world")]
    [InlineData("other_blocker")]
    [InlineData("host_stopped")]
    [InlineData("native_guard_unavailable")]
    public void UnresolvedFactsOrExpiredBudgetsKeepTheSameWorldPaused(string change)
    {
        using var f = PausedOrdinaryIdle(); var n = f.NormalActions.NativeIdleObservation;
        var ledger = File.ReadAllBytes(f.JourneyPath);
        switch (change)
        {
            case "unknown_action": f.NormalActions.RetainLocalResultForIdleTest(NormalActionStates.OutcomeUnknown, true); break;
            case "active_action": f.NormalActions.RetainLocalResultForIdleTest(NormalActionStates.Executing, false); break;
            case "scene_dirty": n.Dirty = true; break;
            case "ground_unknown": n.GroundSphereHit = null; break;
            case "ground_absent": n.GroundSphereHit = false; break;
            case "moving": n.Speed = 1; break;
            case "rts_tail": n.RtsSpeed = 1; break;
            case "manual_tail": n.ManualSpeed = 1; break;
            case "physics_tail": n.PhysicsSpeed = 1; break;
            case "physics_unknown": n.PhysicsSpeed = null; break;
            case "core_empty": n.Core = 0; break;
            case "warper_reserve": n.Warpers = 0; break;
            case "expired_return_window": n.NowOverride = f.Store.VoyagerJourney!.DeadlineUtc; break;
            case "replacement_world": GameMain.data = new(); break;
            case "other_blocker": f.Sessions.DisablePausedSaveWrites(); break;
            case "host_stopped": f.NormalActions.StopHostForIdleReconciliationTest(); break;
            case "native_guard_unavailable": f.NormalActions.VoyagerLocalPrePlayerTickAvailable = false; break;
        }
        Assert.False(f.NormalActions.ProvePausedIdleForTest(out _, out var error)); Assert.NotEmpty(error);
        Assert.True(GameMain.isPaused); Assert.Equal(0, GameMain.IdleReconcileResumeCalls);
        Assert.Equal(WriteHealthStates.Quarantined, f.Sessions.CaptureOnMainThread().WriteHealth);
        Assert.Equal(ledger, File.ReadAllBytes(f.JourneyPath)); Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData("core")]
    [InlineData("position")]
    [InlineData("tick")]
    public void ChangedPreparedObservationIsRejectedBeforeNativeResume(string change)
    {
        using var f = PausedOrdinaryIdle();
        Assert.True(f.NormalActions.ProvePausedIdleForTest(out var proof, out var error), error);
        if (change == "core") f.NormalActions.NativeIdleObservation.Core -= 1;
        if (change == "position") GameMain.mainPlayer.position.x += 1;
        if (change == "tick") GameMain.gameTick++;
        Assert.False(f.NormalActions.ResumePausedIdleForTest(proof, out error));
        Assert.Equal("paused_voyager_idle_proof_changed", error);
        Assert.True(GameMain.isPaused); Assert.Equal(0, GameMain.IdleReconcileResumeCalls);
    }

    [Fact]
    public void FailedNativeResumeDoesNotClearQuarantine()
    {
        using var f = PausedOrdinaryIdle();
        Assert.True(f.NormalActions.ProvePausedIdleForTest(out var proof, out var error), error);
        GameMain.IgnoreIdleResume = true;
        try
        {
            Assert.False(f.NormalActions.ResumePausedIdleForTest(proof, out error));
            Assert.True(GameMain.isPaused);
            Assert.Equal(WriteHealthStates.Quarantined, f.Sessions.CaptureOnMainThread().WriteHealth);
            Assert.Equal(0, f.Sessions.ClearQuarantineCalls);
        }
        finally { GameMain.IgnoreIdleResume = false; }
    }
}
