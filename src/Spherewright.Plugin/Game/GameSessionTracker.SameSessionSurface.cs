using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private Func<OwnedWorldGameplayJournalCheckpoint>? _quiescentSurfaceJournalProvider;

    internal void SetQuiescentSurfaceJournalProvider(Func<OwnedWorldGameplayJournalCheckpoint> provider)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        if (_quiescentSurfaceJournalProvider is not null) throw new InvalidOperationException("surface_journal_provider_already_set");
        _quiescentSurfaceJournalProvider = provider;
    }

    // Read-only preparation for a possible same-session surface task. The
    // provider never scans or writes the Journal; the lease grants no resume.
    internal SameSessionSurfaceJournalEvidence OpenQuiescentSurfaceJournalOnMainThread()
    {
        if (!IsCurrentSessionOwned || !GameMain.isRunning || !GameMain.isPaused || string.IsNullOrWhiteSpace(_sessionId)
            || string.IsNullOrWhiteSpace(_ownedSaveName) || string.IsNullOrWhiteSpace(_gameVersion)
            || HasPendingRemoteResumeAdoption || _pendingJournalResumeTicket is not null
            || _resumeSourceLease is not null || _resumePrimaryLease is not null || _reauthorizationJournalLease is not null
            || _flightCheckpointSourceLease is not null || _flightCheckpoints.HasUnreconciledReload
            || _quiescentSurfaceJournalProvider is null)
            throw new InvalidOperationException("same_session_surface_journal_context_unproved");
        var checkpoint = _quiescentSurfaceJournalProvider();
        return _resumeTickets.OpenLiveSurfaceJournalEvidence(_ownedSaveName!, _gameVersion, checkpoint);
    }
}
