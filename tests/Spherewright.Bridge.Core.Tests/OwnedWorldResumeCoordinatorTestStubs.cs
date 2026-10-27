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
    internal Spherewright.Contracts.Players.NativeResumeFailureSnapshot? CompletedContinuationFailure => RemoteResumeCheckpoints?.CompletedContinuationFailure;
    internal Spherewright.Contracts.Players.NativeResumeFailureSnapshot? RemotePrimaryRecoveryFailure => RemoteResumeCheckpoints?.RemotePrimaryRecoveryFailure;
    internal void BeginRemotePrimaryDecision() => RemoteResumeCheckpoints?.BeginRemotePrimaryDecision();
    internal void RecordRemotePrimarySourceFailure(string code, string predicate, string? exceptionType = null) =>
        RemoteResumeCheckpoints?.RecordRemotePrimarySourceFailure(code, predicate, exceptionType);
    internal void BeginCompletedContinuationDecision() => RemoteResumeCheckpoints?.BeginCompletedContinuationDecision();
    internal FlightCheckpointStore? RemoteResumeCheckpoints { get; set; }
    internal VoyagerRemoteResumeEvidence? RemoteEvidence { get; private set; }
    internal string? RemoteResumeActionId { get; private set; }
    internal OwnedWorldResumeSourceKind ResumeSource { get; private set; }
    internal VoyagerRecoveryAuthorization? RecoveryAuthorization { get; private set; }
    private string? PersistRemoteResumeProgressDebit(VoyagerRemoteResumeEvidence evidence) =>
        RemoteResumeCheckpoints?.PersistRemotePrimaryProgressDebit(evidence);
    internal Spherewright.Contracts.Players.NativeResumeFailureSnapshot? PrimaryDebitFailure { get; private set; }
    private void RetainRemotePrimaryDebitFailure(string code, VoyagerRemoteResumeEvidence evidence)
    {
        PrimaryDebitFailure ??= NativeResumeDecisionEvidence.UnobservedFailure(code, "remote_primary_progress_debit",
            GameMain.gameTick, DateTimeOffset.UtcNow, "progress_loss_debit_persisted");
    }
    internal Action? BeforeRemoteLoaderBind { get; set; }
    internal bool RemoteLoaderObserverPrepared { get; private set; }
    internal bool RemoteLoaderObserverBound { get; private set; }
    internal SavedWorldOpenGuard? SavedOpenGuard { get; private set; }
    internal Spherewright.Contracts.Players.NativeResumeFailureSnapshot? SavedWorldOpenFirstFailure => SavedOpenGuard?.FirstFailure;
    internal bool SavedOpenObserverPrepared { get; private set; }
    internal string? SavedWorldOpenSessionId => Snapshot.SessionId;
    internal bool SavedOpenObserverBound { get; private set; }
    internal OwnedWorldResumeTicket? SavedOpenPreviousTicket { get; private set; }
    internal long? SavedOpenPreparedKnownProgressGameTick { get; private set; }
    internal void PrepareSavedWorldOpenNativeObserver() => SavedOpenObserverPrepared = true;
    internal void BindSavedWorldOpenNativeObserver() => SavedOpenObserverBound = true;
    internal void RecordSavedWorldOpenDispatchFailure(string error) => ResumeAdoptionError = "saved_world_open_dispatch_outcome_unknown:" + error;
    internal bool SavedWorldOpenCompleted(string actionId) => SavedOpenGuard is { Completed: true } guard && guard.ActionId == actionId;
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
        VoyagerRecoveryWindowProposal? proposal = null, ConsumedRemoteResumeProof? priorConsumedProof = null,
        string priorSourceFingerprint = "", bool priorActionKnownTerminalFailed = false, string priorLegacySourceFingerprint = "", string priorIntermediateSourceFingerprint = "",
        bool failedRemoteReauthorization = false, string expectedRemoteContext = "", string validatedJournalHash = "",
        string legacyLocalMoveEvidenceJson = "", string legacyLocalMoveEvidenceHash = "", string legacyCredentialRawFingerprint = "",
        bool completedRemoteContinuation = false, bool priorActionKnownTerminalCompleted = false,
        PrepareOwnedWorldResumeRequest? remotePrimaryRequest = null, OwnedSaveRecoveryLease? progressLastExit = null)
    {
        evidence = null; rejection = "test_remote_store_unavailable";
        return RemoteResumeCheckpoints is not null
            && RemoteResumeCheckpoints.TryOpenRemoteResumeEvidence(ticket, source, primary, out evidence, out rejection, proposal,
                priorConsumedProof, priorSourceFingerprint, priorActionKnownTerminalFailed, priorLegacySourceFingerprint, priorIntermediateSourceFingerprint,
                failedRemoteReauthorization, expectedRemoteContext, validatedJournalHash,
                legacyLocalMoveEvidenceJson, legacyLocalMoveEvidenceHash, legacyCredentialRawFingerprint,
                completedRemoteContinuation, priorActionKnownTerminalCompleted, remotePrimaryRequest, progressLastExit);
    }
    internal SessionState CaptureOnMainThread()
    { RefreshCheckpointNativeShellForTest(); return Snapshot; }
    internal bool FailBeforeRemoteLeaseHandoffForTest;
    internal void ExpectNextSessionToBeResumed(OwnedWorldResumeTicket ticket,
        OwnedSaveRecoveryLease? sourceLease = null, bool reauthorizingExpiredPrimary = false,
        IDisposable? journalLease = null, bool reauthorizingFixedAutosave0 = false,
        OwnedSaveRecoveryLease? primaryLease = null, VoyagerRemoteResumeEvidence? remoteEvidence = null,
        string? remoteResumeActionId = null, OwnedWorldResumeSourceKind resumeSource = OwnedWorldResumeSourceKind.None,
        VoyagerRecoveryAuthorization? recoveryAuthorization = null,
        bool savedWorldOpenOnly = false, string? savedWorldOpenActionId = null,
        long? savedWorldOpenKnownProgressGameTick = null)
    {
        if (FailBeforeRemoteLeaseHandoffForTest) throw new InvalidOperationException("synthetic_pre_dispatch_failure");
        if (remoteEvidence is not null && !RemoteResumeLeaseHandoffMatches(ticket, sourceLease, primaryLease,
            journalLease, remoteEvidence, remoteResumeActionId, resumeSource, reauthorizingExpiredPrimary, reauthorizingFixedAutosave0))
            throw new InvalidOperationException("remote_resume_full_lease_handoff_unproved");
        SourceLease = sourceLease;
        ResetContactForExpectedWorld();
        JournalLease = journalLease;
        PrimaryLease = primaryLease;
        Reauthorizing = reauthorizingExpiredPrimary;
        FixedAutosave = reauthorizingFixedAutosave0;
        RemoteEvidence = remoteEvidence;
        RemoteResumeActionId = remoteResumeActionId;
        _pendingRemoteResumeActionId=remoteResumeActionId;
        ResumeSource = resumeSource;
        RecoveryAuthorization = recoveryAuthorization;
        SavedOpenGuard = savedWorldOpenOnly ? new SavedWorldOpenGuard(savedWorldOpenActionId!, sourceLease!.Prefix.GameTick) : null;
        SavedOpenPreviousTicket = savedWorldOpenOnly ? ticket : null;
        SavedOpenPreparedKnownProgressGameTick = savedWorldOpenKnownProgressGameTick;
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
