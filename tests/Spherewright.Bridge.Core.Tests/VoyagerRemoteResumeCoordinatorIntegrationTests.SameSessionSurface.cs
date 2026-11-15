using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void ExactSuccessfulOutboundEvidenceIsReadOnlyAndNeverReactivatesCheckpointLoading()
    {
        using var s = new Scenario(currentNative: true, nativeArmed: true);
        var journey = s.Checkpoints.VoyagerJourney!;
        Assert.False(s.Checkpoints.HasCurrentTicket); // Flight succeeded; its old checkpoint is not load authority.
        Assert.True(s.Checkpoints.TryOpenSameSessionSurfaceEvidence("synthetic-session", Scenario.Identity,
            201, journey.JourneyId, 12520, Process.GetCurrentProcess().Id, out var evidence, out var rejection), rejection);
        Assert.NotNull(evidence);
        Assert.True(OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(evidence!.JourneySnapshotFingerprint));
        Assert.True(OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(evidence.RouteFingerprint));
        Assert.True(s.Checkpoints.SameSessionSurfaceEvidenceStillMatches(evidence));
        Assert.False(s.Checkpoints.HasCurrentTicket);
        Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var file in s.ProtectedEvidencePaths.Where(p => p.Contains("flight-checkpoint")
            || p.Contains("voyager-journey") || Path.GetFileName(p).StartsWith("Spherewright_PreFlight_")))
            Assert.ThrowsAny<IOException>(() => { using var write = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
        evidence.Dispose();
        Assert.False(s.Checkpoints.SameSessionSurfaceEvidenceStillMatches(evidence));
        Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var file in s.ProtectedEvidencePaths.Where(p => p.Contains("flight-checkpoint")
            || p.Contains("voyager-journey") || Path.GetFileName(p).StartsWith("Spherewright_PreFlight_")))
            using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Theory]
    [InlineData("wrong_session")] [InlineData("wrong_world")] [InlineData("wrong_planet")]
    [InlineData("wrong_journey")] [InlineData("wrong_process")] [InlineData("before_landing")]
    [InlineData("unarmed")] [InlineData("failed_flight")] [InlineData("changed_journey")]
    [InlineData("changed_checkpoint")] [InlineData("changed_save")]
    [InlineData("duplicate_journey_field")] [InlineData("duplicate_checkpoint_field")]
    public void ChangedOrUnprovedOriginalNeverProducesSameSessionEvidence(string fault)
    {
        using var s = new Scenario(currentNative: true, failedLanding: fault == "failed_flight", nativeArmed: fault != "unarmed");
        var journey = s.Checkpoints.VoyagerJourney!;
        var session = fault == "wrong_session" ? "other-session" : "synthetic-session";
        var identity = fault == "wrong_world" ? "other-owned-world" : Scenario.Identity;
        var planet = fault == "wrong_planet" ? 202 : 201;
        var id = fault == "wrong_journey" ? Guid.NewGuid().ToString() : journey.JourneyId;
        var process = Process.GetCurrentProcess().Id + (fault == "wrong_process" ? 1 : 0);
        var tick = fault == "before_landing" ? 12499 : 12520;
        if (fault == "changed_journey")
        {
            var path = Path.Combine(s.Handoff, "voyager-journey.json");
            var changed = PluginJson.Deserialize<VoyagerJourneyRecord>(File.ReadAllText(path))!;
            changed.SpentGameTicks++; File.WriteAllText(path, PluginJson.Serialize(changed));
        }
        if (fault == "changed_checkpoint")
        {
            var path = Path.Combine(s.Handoff, "flight-checkpoint.json");
            var changed = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(path))!;
            changed.SourceRevision++; File.WriteAllText(path, PluginJson.Serialize(changed));
        }
        if (fault == "duplicate_journey_field" || fault == "duplicate_checkpoint_field")
        {
            var path = Path.Combine(s.Handoff, fault == "duplicate_journey_field" ? "voyager-journey.json" : "flight-checkpoint.json");
            var original = File.ReadAllText(path);
            File.WriteAllText(path, "{\"version\":1," + original.Substring(1));
        }
        if (fault == "changed_save")
        {
            var path = s.ProtectedEvidencePaths.Single(p => Path.GetFileName(p).StartsWith("Spherewright_PreFlight_"));
            File.AppendAllText(path, "changed");
        }
        Assert.False(s.Checkpoints.TryOpenSameSessionSurfaceEvidence(session, identity, planet, id, tick, process,
            out var evidence, out var rejection));
        Assert.Null(evidence); Assert.NotEmpty(rejection); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var file in s.ProtectedEvidencePaths.Where(File.Exists))
            using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Theory]
    [InlineData("voyager-journey.json")] [InlineData("flight-checkpoint.json")]
    public void EquivalentRecordsBindTheirOriginalBytesWithoutAssumingSerializerFormatting(string record)
    {
        using var s = new Scenario(currentNative: true, nativeArmed: true);
        var journey = s.Checkpoints.VoyagerJourney!;
        bool Open(out SameSessionSurfaceJourneyEvidence? evidence) =>
            s.Checkpoints.TryOpenSameSessionSurfaceEvidence("synthetic-session", Scenario.Identity, 201,
                journey.JourneyId, 12520, Process.GetCurrentProcess().Id, out evidence, out _);
        Assert.True(Open(out var first)); var original = first!.RouteFingerprint; first.Dispose();
        File.AppendAllText(Path.Combine(s.Handoff, record), " ");
        Assert.True(Open(out var second));
        Assert.NotEqual(original, second!.RouteFingerprint);
        Assert.False(s.Checkpoints.HasCurrentTicket); Assert.Equal(0, DSPGame.LoadCalls);
        second.Dispose();
    }

    [Fact]
    public void EvidenceReleaseAttemptsEveryLeaseEvenIfOneDisposeFails()
    {
        using var s = new Scenario(currentNative: true, nativeArmed: true);
        var first = new TestLease(false); var failing = new TestLease(true); var last = new TestLease(false);
        var evidence = new SameSessionSurfaceJourneyEvidence(s.Checkpoints, "journey", "checkpoint", "file",
            "journey-bytes", "checkpoint-bytes", Guid.NewGuid().ToString(), 1, first, failing, last);
        Assert.Throws<IOException>(() => evidence.Dispose());
        Assert.True(first.Released); Assert.True(failing.Released); Assert.True(last.Released); Assert.True(evidence.IsDisposed);
        evidence.Dispose();
    }

    private sealed class TestLease : IDisposable
    {
        private readonly bool _fail;
        internal TestLease(bool fail) => _fail = fail;
        internal bool Released { get; private set; }
        public void Dispose() { Released = true; if (_fail) throw new IOException("synthetic-release-failure"); }
    }

    [Fact]
    public void CurrentDurableJournalIsBoundWithoutConsumingAResumeTicketOrLoading()
    {
        using var s = new Scenario(currentNative: true, nativeArmed: true);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out var ticket, out var error), error);
        var checkpoint = ticket!.GameplayJournalCheckpoint!;
        using (var evidence = s.Tickets.OpenLiveSurfaceJournalEvidence(Scenario.Identity, "0.10.35.29151", checkpoint))
        {
            Assert.True(OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(evidence.Fingerprint));
            Assert.False(evidence.IsDisposed);
            Assert.ThrowsAny<IOException>(() => { using var write = new FileStream(s.JournalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
            Assert.Equal(0, DSPGame.LoadCalls);
        }
        using (new FileStream(s.JournalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out error), error);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void SameSessionJournalUsesOnlyTheQuiescentProviderWithoutSavingLoadingOrConsuming()
    {
        using var s = new Scenario(currentNative: true, nativeArmed: true);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out var ticket, out _));
        using var shell = new PausedSaveShell(s, Guid.NewGuid().ToString(), completed: false);
        var original = File.ReadAllBytes(s.JournalPath); var calls = 0;
        s.Sessions.SetQuiescentSurfaceJournalProvider(() => { calls++; return ticket!.GameplayJournalCheckpoint!; });
        using (var evidence = s.Sessions.OpenQuiescentSurfaceJournalOnMainThread())
        {
            Assert.True(OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(evidence.Fingerprint));
            Assert.Equal(1, calls); Assert.Equal(0, DSPGame.LoadCalls); Assert.Equal(original, File.ReadAllBytes(s.JournalPath));
        }
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    [Theory]
    [InlineData("provider_missing")] [InlineData("unpaused")] [InlineData("unowned")]
    [InlineData("journal_pending")] [InlineData("load_pending")]
    public void SameSessionJournalNeverInvokesItsProviderWhenTheNativeContextIsUnproved(string fault)
    {
        using var s = new Scenario(currentNative: true, nativeArmed: true);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out var ticket, out _));
        using var shell = new PausedSaveShell(s, Guid.NewGuid().ToString(), completed: false);
        var calls = 0;
        if (fault != "provider_missing") s.Sessions.SetQuiescentSurfaceJournalProvider(() => { calls++; return ticket!.GameplayJournalCheckpoint!; });
        if (fault == "unpaused") GameMain.isPaused = false;
        if (fault is "unowned" or "journal_pending" or "load_pending") s.Sessions.BreakPausedSaveShell(fault);
        Assert.Throws<InvalidOperationException>(() => s.Sessions.OpenQuiescentSurfaceJournalOnMainThread());
        Assert.Equal(0, calls); Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData("identity")] [InlineData("version")] [InlineData("sequence")]
    [InlineData("missing")] [InlineData("duplicate_json")]
    public void LiveJournalEvidenceRejectsUnprovedIdentityOrUnstableContentWithoutHoldingALease(string fault)
    {
        using var s = new Scenario(currentNative: true, nativeArmed: true);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out var ticket, out _));
        var source = ticket!.GameplayJournalCheckpoint!;
        var checkpoint = new OwnedWorldGameplayJournalCheckpoint
        {
            Version = source.Version, JournalId = source.JournalId, TrackingMode = source.TrackingMode,
            HistoricalCoverageComplete = source.HistoricalCoverageComplete, TrackingStartedAtGameTick = source.TrackingStartedAtGameTick,
            MinimumDurableThroughSequence = source.MinimumDurableThroughSequence + (fault == "sequence" ? 1 : 0),
        };
        if (fault == "missing") File.Delete(s.JournalPath);
        if (fault == "duplicate_json")
        {
            var original = File.ReadAllText(s.JournalPath);
            File.WriteAllText(s.JournalPath, "{\"version\":1," + original.Substring(1));
        }
        var error = Record.Exception(() => s.Tickets.OpenLiveSurfaceJournalEvidence(
            fault == "identity" ? "other-synthetic-world" : Scenario.Identity,
            fault == "version" ? "different-native-version" : "0.10.35.29151", checkpoint));
        Assert.NotNull(error);
        if (fault is "identity" or "missing") Assert.IsAssignableFrom<IOException>(error);
        else if (fault == "duplicate_json") Assert.IsAssignableFrom<Newtonsoft.Json.JsonException>(error);
        else Assert.IsType<InvalidDataException>(error);
        Assert.Equal(0, DSPGame.LoadCalls);
        if (File.Exists(s.JournalPath)) using (new FileStream(s.JournalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
}
