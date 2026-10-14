using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

// TEST ONLY. The production coordinator and ticket store are linked, but these
// native/session substitutes do NOT prove Unity loading, adoption or ACLs.
internal static class TestWorldCoordinator
{
    internal static BridgeError? ReadinessError;
    internal static BridgeError? ValidateMainMenuReady() => ReadinessError;
}

internal static class GameSave
{
    internal const string LastExit = "_lastexit_";
    internal const string AutoSave0 = "_autosave_0";
    internal static string Root = string.Empty;
    internal static string SavePath(string name) => Path.Combine(Root, name + ".dsv");
    internal static void ReadHeader(string name, bool unused, out Header? header)
    {
        using var stream = File.OpenRead(SavePath(name));
        using var reader = new BinaryReader(stream);
        stream.Position = 36;
        header = new Header { gameTick = reader.ReadInt64() };
    }
    internal sealed class Header { internal long gameTick; }
}

internal static class DSPGame
{
    internal static int LoadCalls;
    internal static string? LoadedName;
    internal static Action? BeforeLoad;
    internal static void StartGame(string name)
    {
        BeforeLoad?.Invoke();
        LoadedName = name;
        LoadCalls++;
    }
}

internal sealed class GameSessionTracker : IDisposable
{
    internal bool IsCurrentSessionOwned { get; set; }
    internal string? OwnedSaveName { get; set; }
    internal string? SessionId { get; set; }
    internal long Revision { get; set; }
    internal string? ResumeAdoptionError { get; set; }
    internal SessionState Snapshot { get; } = new();
    internal OwnedSaveRecoveryLease? SourceLease { get; private set; }
    internal IDisposable? JournalLease { get; private set; }
    internal OwnedSaveRecoveryLease? PrimaryLease { get; private set; }
    internal bool Reauthorizing { get; private set; }
    internal bool FixedAutosave { get; private set; }
    internal SessionState CaptureOnMainThread() => Snapshot;
    internal void ExpectNextSessionToBeResumed(OwnedWorldResumeTicket ticket,
        OwnedSaveRecoveryLease? sourceLease = null, bool reauthorizingExpiredPrimary = false,
        IDisposable? journalLease = null, bool reauthorizingFixedAutosave0 = false,
        OwnedSaveRecoveryLease? primaryLease = null)
    {
        SourceLease = sourceLease;
        JournalLease = journalLease;
        PrimaryLease = primaryLease;
        Reauthorizing = reauthorizingExpiredPrimary;
        FixedAutosave = reauthorizingFixedAutosave0;
    }
    internal void CancelExpectedResumedSession() => Dispose();
    public void Dispose()
    {
        SourceLease?.Dispose();
        SourceLease = null;
        JournalLease?.Dispose();
        JournalLease = null;
        PrimaryLease?.Dispose();
        PrimaryLease = null;
    }
}
