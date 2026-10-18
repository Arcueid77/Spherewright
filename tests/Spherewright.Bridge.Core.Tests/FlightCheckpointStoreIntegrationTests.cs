using BepInEx.Logging;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Executes the production store with synthetic native headers; it proves neither
// Unity loading nor Windows ACL behavior nor a real failed-flight recovery.
[Collection("Owned resume native substitutes")]
public sealed class FlightCheckpointStoreIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spherewright-flight-" + Guid.NewGuid().ToString("N"));
    private readonly string _previousRoot;
    private readonly FlightCheckpointStore _store;
    private readonly FlightCheckpointTicket _ticket;

    public FlightCheckpointStoreIntegrationTests()
    {
        Directory.CreateDirectory(_root);
        _previousRoot = GameSave.Root;
        GameSave.Root = _root;
        _store = new FlightCheckpointStore("synthetic-bridge", "0.10.35.29151", new ManualLogSource(), Path.Combine(_root, "handoff"));
        _ticket = _store.CreateDraft("synthetic-owned-primary", "synthetic-session", 1, 101, 201, "player-hash", "star-hash");
        WriteHeader(_ticket.OwnedSaveName, 90);
        WriteHeader(_ticket.CheckpointSaveName, 100);
        _store.PersistCompletedCheckpoint(_ticket, 100);
        Assert.True(_store.TryMarkAttemptStarted(_ticket.CheckpointId, "original-flight", 101, out _));
        Assert.True(_store.TryMarkRecoveryRequired(_ticket.CheckpointId, "original-flight", 120, out _));
    }

    [Fact]
    public void FreshPrimaryCoveragePreventsSameProcessRollbackWithoutReopeningStore()
    {
        Assert.True(_store.TryGetActiveTicket(_ticket.ReloadToken, out _, out _));
        WriteHeader(_ticket.OwnedSaveName, 121);
        Assert.False(_store.TryGetActiveTicket(_ticket.ReloadToken, out _, out var rejection));
        Assert.Contains("rollback is forbidden", rejection);
        Assert.Equal(FlightCheckpointLifecycleStates.Retired, _ticket.LifecycleState);
        Assert.Equal(121, _ticket.RetiredAtGameTick);
        Assert.Null(_store.CurrentTicket);
    }

    [Fact]
    public void PrimaryChangedBetweenPrepareAndCommitInvalidatesSameCheckpointObject()
    {
        Assert.True(_store.TryGetActiveTicket(_ticket.ReloadToken, out var preparedTicket, out _));
        WriteHeader(_ticket.OwnedSaveName, 122);
        Assert.False(_store.TryValidateCheckpointFile(preparedTicket!, out var rejection));
        Assert.Contains("rollback is forbidden", rejection);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(100)]
    public void EqualOrOlderPrimaryPreservesExactRecoveryAuthority(long primaryTick)
    {
        WriteHeader(_ticket.OwnedSaveName, primaryTick);
        Assert.True(_store.TryGetActiveTicket(_ticket.ReloadToken, out var ticket, out _));
        Assert.Same(_ticket, ticket);
        Assert.True(_store.TryValidateCheckpointFile(ticket!, out _));
    }

    [Fact]
    public void MissingPrimaryFailsClosedWithoutInventingCoverage()
    {
        File.Delete(GameSave.SavePath(_ticket.OwnedSaveName));
        Assert.False(_store.TryGetActiveTicket(_ticket.ReloadToken, out _, out var rejection));
        Assert.Contains("unknown", rejection);
        Assert.Equal(FlightCheckpointLifecycleStates.RecoveryRequired, _ticket.LifecycleState);
    }

    [Fact]
    public void OverwrittenCheckpointCannotUseItsOldExactTicket()
    {
        WriteHeader(_ticket.CheckpointSaveName, 101);
        Assert.False(_store.TryValidateCheckpointFile(_ticket, out var rejection));
        Assert.Contains("could not be verified", rejection);
    }

    [Fact]
    public void SameTickAndTimestampBodyReplacementCannotUseTheOriginalCheckpoint()
    {
        var path = GameSave.SavePath(_ticket.CheckpointSaveName);
        var written = File.GetLastWriteTimeUtc(path);
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Write))
        {
            file.Position = file.Length - 1;
            file.WriteByte(17);
        }
        File.SetLastWriteTimeUtc(path, written);
        Assert.False(_store.TryValidateCheckpointFile(_ticket, out _));
    }

    [Fact]
    public void VerifiedCheckpointLeaseAllowsNativeReadingAndBlocksReplacement()
    {
        using var lease = _store.OpenVerifiedCheckpointLease(_ticket);
        using var nativeRead = File.OpenRead(GameSave.SavePath(_ticket.CheckpointSaveName));
        Assert.True(nativeRead.CanRead);
        if (OperatingSystem.IsWindows())
            Assert.Throws<IOException>(() => File.Open(GameSave.SavePath(_ticket.CheckpointSaveName), FileMode.Open, FileAccess.Write));
    }

    [Fact]
    public void ExpiredCheckpointCannotPassDirectFileRevalidation()
    {
        _ticket.IssuedAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        _ticket.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        Assert.False(_store.TryGetActiveTicket(_ticket.ReloadToken, out _, out _));
        Assert.False(_store.TryValidateCheckpointFile(_ticket, out _));
    }

    [Fact]
    public void MissingCheckpointFailsClosedWithoutAnUnhandledNativeReadError()
    {
        File.Delete(GameSave.SavePath(_ticket.CheckpointSaveName));
        Assert.False(_store.TryValidateCheckpointFile(_ticket, out var rejection));
        Assert.Contains("could not be verified", rejection);
    }

    [Fact]
    public void StaleActionCannotChangeTheLifecycleOfAnotherAttempt()
    {
        Assert.False(_store.TryMarkRecoveryRequired(_ticket.CheckpointId, "different-action", 125, out _));
        Assert.False(_store.TryMarkFlightSucceeded(_ticket.CheckpointId, "different-action", 125, out _));
        Assert.Equal("original-flight", _ticket.LastAttemptActionId);
        Assert.Equal(FlightCheckpointLifecycleStates.RecoveryRequired, _ticket.LifecycleState);
    }

    [Fact]
    public void NewerPrimaryStillBlocksWhenRetirementCannotPersist()
    {
        File.WriteAllText(Path.Combine(_root, "handoff", ".test-only-fail-secure-write"), "synthetic failure");
        WriteHeader(_ticket.OwnedSaveName, 123);
        Assert.False(_store.TryGetActiveTicket(_ticket.ReloadToken, out _, out var rejection));
        Assert.Contains("rollback is forbidden", rejection);
        Assert.Null(_store.CurrentTicket);
    }

    [Fact]
    public void SuccessfulFlightImmediatelyLosesRollbackAuthority()
    {
        Assert.True(_store.TryMarkFlightSucceeded(_ticket.CheckpointId, "original-flight", 125, out _));
        Assert.False(_store.TryGetActiveTicket(_ticket.ReloadToken, out _, out _));
        Assert.False(_store.TryValidateCheckpointFile(_ticket, out _));
    }

    private static void WriteHeader(string name, long tick)
    {
        using var fixture = OwnedSavePrefixReaderTests.Fixture(identity: "synthetic-owned-primary", patch: 23,
            versionPatch: 35, versionBuild: 29151, gameDescVersion: 10);
        using var stream = File.Create(GameSave.SavePath(name));
        fixture.CopyTo(stream);
        stream.Position = 36;
        using var writer = new BinaryWriter(stream);
        writer.Write(tick);
    }

    public void Dispose()
    {
        GameSave.Root = _previousRoot;
        Directory.Delete(_root, true);
    }
}
