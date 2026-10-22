using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    // The blocked quarantine/ordinary-save combination comes from the local
    // sanitized observation. Tail/render sequences below are contract-based
    // counterfactuals, not claimed historic Unity samples or Data.Export.
    [Fact]
    public void CompletedRecoveryThenIdleQuarantineCanPreserveTheStableTailWithoutClearingAnything()
    {
        using var s = new Scenario();
        var accepted = s.Commit(s.Prepare()).Value!;
        using var shell = new PausedSaveShell(s, accepted.ActionId);
        var originals = s.ProtectedEvidencePaths.Where(p => p != GameSave.SavePath(Scenario.Identity))
            .ToDictionary(p => p, File.ReadAllBytes);
        var consumed = Directory.GetFiles(s.Runtime, "owned-world-resume-consumed-*.json").ToDictionary(p => p, File.ReadAllBytes);
        var beforeCalls = GameSave.FlightStartSaveCalls;

        s.Sessions.QuarantineRemoteIdleOnMainThread(accepted.ActionId, "remote_idle_return_wall_reserve_insufficient");
        Assert.Equal(BridgeErrorCodes.WriteSubsystemQuarantined,
            NormalGameActionCoordinator.SaveAccessForTest(s.Sessions.Snapshot, null)!.Code);
        shell.Observe(10);
        Assert.False(s.Sessions.TryBindPausedOwnedSave(out _, out var rejection));
        Assert.Equal("paused_save_tail_not_observed", rejection);
        GameMain.gameTick++; // Native Pause may leave the current601 tail.
        shell.Observe(11);
        Assert.False(s.Sessions.TryBindPausedOwnedSave(out _, out _));
        shell.Observe(12);
        Assert.True(s.Sessions.TryBindPausedOwnedSave(out var binding, out rejection), rejection);
        Assert.Null(NormalGameActionCoordinator.SaveAccessForTest(s.Sessions.Snapshot, binding));
        shell.Observe(13); // A prepared binding survives further identical paused render frames.
        Assert.True(s.Sessions.ValidatePausedOwnedSave(binding!, out rejection), rejection);
        Assert.Equal(beforeCalls, GameSave.FlightStartSaveCalls);

        GameSave.FlightStartSave = name => { s.WriteSave(name, GameMain.gameTick); return true; };
        Assert.True(s.Sessions.TrySaveOwnedWorldNowOnMainThread(out var error, binding), error);
        using var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(Scenario.Identity), Scenario.Identity);
        Assert.Equal(binding!.GameTick, primary.Prefix.GameTick);
        Assert.Equal(primary.Prefix.GameTick, s.Sessions.LastPreservedTick);
        Assert.Equal(WriteHealthStates.Quarantined, s.Sessions.PreservationWriteHealth);
        Assert.False(s.Sessions.Snapshot.WritesAllowed);
        Assert.Equal(accepted.ActionId, s.Sessions.Snapshot.WriteQuarantineActionId);
        Assert.True(GameMain.isPaused);
        Assert.Equal(beforeCalls + 1, GameSave.FlightStartSaveCalls);
        foreach (var pair in originals.Concat(consumed)) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.Equal(1, DSPGame.LoadCalls); // The only Load is the original recovery.
    }

    [Theory]
    [InlineData("adoption_pending")] [InlineData("adoption_failure")]
    [InlineData("journal_pending")] [InlineData("load_pending")]
    [InlineData("newer_saved")] [InlineData("unowned")]
    [InlineData("other_blocker")] [InlineData("same_code_other_reason")]
    [InlineData("other_quarantine_owner")] [InlineData("native_running")]
    public void UnprovedOrContradictoryPreservationNeverGetsAFileWrite(string fault)
    {
        using var s = new Scenario(); var action = s.Commit(s.Prepare()).Value!;
        using var shell = new PausedSaveShell(s, action.ActionId, fault != "adoption_pending");
        var before = File.ReadAllBytes(GameSave.SavePath(Scenario.Identity)); var calls = GameSave.FlightStartSaveCalls;
        if (fault == "other_quarantine_owner")
            s.Sessions.QuarantineWritesOnMainThread("different-original-action", "retained_unknown");
        else s.Sessions.QuarantineRemoteIdleOnMainThread(action.ActionId, "remote_idle_return_wall_reserve_insufficient");
        if (fault == "other_blocker") s.Sessions.DisablePausedSaveWrites();
        if (fault == "same_code_other_reason")
        {
            File.WriteAllText(Path.Combine(s.Handoff, "voyager-journey.json"), "invalid-synthetic-record");
            var broken = new FlightCheckpointStore("test-bridge", s.Checkpoints.VoyagerJourney!.GameVersion,
                new BepInEx.Logging.ManualLogSource(), s.Handoff);
            Assert.NotNull(broken.VoyagerJourneyLoadError);
            s.Sessions.SetPausedSaveCheckpoints(broken);
        }
        if (fault == "native_running") GameMain.isPaused = false;
        s.Sessions.BreakPausedSaveShell(fault);
        shell.Observe(10); shell.Observe(11);
        Assert.False(s.Sessions.TryBindPausedOwnedSave(out _, out var rejection));
        Assert.NotEmpty(rejection); Assert.Equal(calls, GameSave.FlightStartSaveCalls);
        var cut = s.Sessions.PausedSaveFirstFailure!;
        Assert.Equal(GameMain.gameTick, cut.CapturedAtGameTick); Assert.False(cut.SafetyCertified);
        Assert.Equal("not_reached", cut.Predicates["native_save"]);
        Assert.Equal(before, File.ReadAllBytes(GameSave.SavePath(Scenario.Identity)));
    }

    [Theory]
    [InlineData("false_return")] [InlineData("short_export")]
    [InlineData("older_export")] [InlineData("newer_export")]
    [InlineData("replacement_world")]
    public void AReturnedNativeSaveDoesNotCertifyIncompleteOlderOrChangedProgress(string fault)
    {
        using var s = new Scenario(); var action = s.Commit(s.Prepare()).Value!;
        using var shell = new PausedSaveShell(s, action.ActionId);
        s.Sessions.QuarantineRemoteIdleOnMainThread(action.ActionId, "remote_idle_return_wall_reserve_insufficient");
        shell.Observe(10); shell.Observe(11);
        Assert.True(s.Sessions.TryBindPausedOwnedSave(out var binding, out _));
        var previous = s.Sessions.LastPreservedTick;
        GameSave.FlightStartSave = name =>
        {
            if (fault == "false_return") return false;
            s.WriteSave(name, GameMain.gameTick + (fault == "older_export" ? -1 : fault == "newer_export" ? 1 : 0));
            if (fault == "short_export") using (var f = File.OpenWrite(GameSave.SavePath(name))) f.SetLength(52);
            if (fault == "replacement_world") GameMain.data = new();
            return true;
        };
        Assert.False(s.Sessions.TrySaveOwnedWorldNowOnMainThread(out var error, binding));
        Assert.NotNull(error); Assert.Equal(previous, s.Sessions.LastPreservedTick);
        Assert.Equal(WriteHealthStates.Quarantined, s.Sessions.PreservationWriteHealth);
        Assert.Equal(action.ActionId, s.Sessions.Snapshot.WriteQuarantineActionId);
        var failure = binding!.FirstFailure!;
        Assert.NotEmpty(failure.FirstFailedPredicate); Assert.False(failure.SafetyCertified);
        if (fault != "replacement_world")
        {
            Assert.Equal("observed", failure.FactsState);
            Assert.Null(failure.CapturedAtGameTick); // Never borrow the earlier binding as the failure instant.
            Assert.Equal(fault is "false_return" or "short_export" ? null : GameMain.gameTick
                + (fault == "older_export" ? -1 : 1), failure.Values["native_file_tick"]);
        }
    }

    private sealed class PausedSaveShell : IDisposable
    {
        private readonly Scenario _scenario;
        private readonly PausedSaveTestWorld? _oldWorld = GameMain.data;
        private readonly bool _oldPaused = GameMain.isPaused, _oldRunning = GameMain.isRunning;
        private readonly int _oldFrame = UnityEngine.Time.frameCount;
        private readonly long _oldTick = GameMain.gameTick;
        private readonly Func<string, bool>? _oldSave = GameSave.FlightStartSave;
        private readonly int _oldCalls = GameSave.FlightStartSaveCalls;
        private readonly FlightStartTestPlanet _oldPlanet = GameMain.localPlanet;
        internal PausedSaveShell(Scenario scenario, string actionId, bool completed = true)
        {
            _scenario = scenario; GameMain.gameTick = 12520; GameMain.isPaused = true; GameMain.isRunning = true;
            scenario.Sessions.BindPausedSaveShell("synthetic-adopted-session", Scenario.Identity,
                scenario.Checkpoints.VoyagerJourney!.GameVersion, actionId, scenario.Checkpoints, scenario.Tickets, completed);
        }
        internal void Observe(int frame)
        { UnityEngine.Time.frameCount = frame; _scenario.Sessions.ObservePausedOwnedSaveOnMainThread(); }
        public void Dispose()
        {
            GameMain.data = _oldWorld; GameMain.isPaused = _oldPaused; GameMain.isRunning = _oldRunning;
            GameMain.gameTick = _oldTick; GameMain.localPlanet = _oldPlanet; UnityEngine.Time.frameCount = _oldFrame;
            GameSave.FlightStartSave = _oldSave; GameSave.FlightStartSaveCalls = _oldCalls;
        }
    }
}
