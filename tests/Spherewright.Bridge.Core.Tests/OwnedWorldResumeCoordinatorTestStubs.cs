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

internal static partial class GameSave
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

internal static partial class DSPGame
{
    internal static int LoadCalls;
    internal static string? LoadedName;
    internal static Action? BeforeLoad;
    internal static void StartGame(string name)
    {
        DispatchCalls++;
        BeforeLoad?.Invoke();
        LoadedName = name;
        LoadCalls++;
    }
}

internal sealed partial class GameSessionTracker : IDisposable
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
    internal bool RequiresRemoteResumeEvidence { get; set; }
    internal FlightCheckpointStore? RemoteResumeCheckpoints { get; set; }
    internal VoyagerRemoteResumeEvidence? RemoteEvidence { get; private set; }
    internal string? RemoteResumeActionId { get; private set; }
    internal OwnedWorldResumeSourceKind ResumeSource { get; private set; }
    internal VoyagerRecoveryAuthorization? RecoveryAuthorization { get; private set; }
    internal Action? BeforeRemoteLoaderBind { get; set; }
    internal bool RemoteLoaderObserverPrepared { get; private set; }
    internal bool RemoteLoaderObserverBound { get; private set; }
    internal void PrepareRemoteResumeNativeLoadObserver() => RemoteLoaderObserverPrepared = true;
    internal void BindRemoteResumeNativeLoaderAfterDispatch()
    {
        BeforeRemoteLoaderBind?.Invoke();
        RemoteLoaderObserverBound = true;
    }
    internal void RecordRemoteResumeNativeDispatchObservationFailure(string exceptionType) =>
        ResumeAdoptionError = "remote_resume_postdispatch_loader_observation_failed:" + exceptionType;
    internal bool TryOpenRemoteResumeEvidence(OwnedWorldResumeTicket ticket, OwnedSaveRecoveryLease source,
        OwnedSaveRecoveryLease primary, out VoyagerRemoteResumeEvidence? evidence, out string rejection,
        VoyagerRecoveryWindowProposal? proposal = null)
    {
        evidence = null; rejection = "test_remote_store_unavailable";
        return RemoteResumeCheckpoints is not null
            && RemoteResumeCheckpoints.TryOpenRemoteResumeEvidence(ticket, source, primary, out evidence, out rejection, proposal);
    }
    internal SessionState CaptureOnMainThread() => Snapshot;
    internal void ExpectNextSessionToBeResumed(OwnedWorldResumeTicket ticket,
        OwnedSaveRecoveryLease? sourceLease = null, bool reauthorizingExpiredPrimary = false,
        IDisposable? journalLease = null, bool reauthorizingFixedAutosave0 = false,
        OwnedSaveRecoveryLease? primaryLease = null, VoyagerRemoteResumeEvidence? remoteEvidence = null,
        string? remoteResumeActionId = null, OwnedWorldResumeSourceKind resumeSource = OwnedWorldResumeSourceKind.None,
        VoyagerRecoveryAuthorization? recoveryAuthorization = null)
    {
        SourceLease = sourceLease;
        JournalLease = journalLease;
        PrimaryLease = primaryLease;
        Reauthorizing = reauthorizingExpiredPrimary;
        FixedAutosave = reauthorizingFixedAutosave0;
        RemoteEvidence = remoteEvidence;
        RemoteResumeActionId = remoteResumeActionId;
        ResumeSource = resumeSource;
        RecoveryAuthorization = recoveryAuthorization;
    }
    internal void CancelExpectedResumedSession() => Dispose();
    public void Dispose()
    {
        DisposeFlightCheckpointExpectation();
        SourceLease?.Dispose();
        SourceLease = null;
        JournalLease?.Dispose();
        JournalLease = null;
        PrimaryLease?.Dispose();
        PrimaryLease = null;
        RemoteEvidence?.Dispose();
        RemoteEvidence = null;
    }
}
