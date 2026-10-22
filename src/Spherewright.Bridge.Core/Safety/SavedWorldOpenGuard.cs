using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Only observes a completed paused load. Never resumes simulation or grants task authority.</summary>
public sealed class SavedWorldOpenGuard
{
    public string ActionId { get; }
    public long SavedTick { get; }
    public bool LoadObserved { get; private set; }
    public bool Completed { get; private set; }
    public NativeResumeFailureSnapshot? FirstFailure => _failure?.Copy();
    private NativeResumeFailureSnapshot? _failure;
    private object? _world;

    public SavedWorldOpenGuard(string actionId, long savedTick)
    {
        if (!Guid.TryParse(actionId, out _) || savedTick < 0) throw new ArgumentException("Exact opening action/tick required.");
        ActionId = actionId; SavedTick = savedTick;
    }

    public bool ObserveLoad(object? world, bool? nativeCompleted, bool? nativePaused, long? tick,
        bool? identityMatches, bool? planetMatches)
    {
        if (_failure is not null || Completed) return false;
        var e = new NativeResumeDecisionEvidence("saved_world_open_load_complete", tick, DateTimeOffset.UtcNow, true);
        e.Number("saved_game_tick", SavedTick); e.Number("loaded_game_tick", tick);
        e.NotReached("single_load_observation", "native_load_completed", "native_paused", "exact_saved_tick", "owned_identity", "owned_planet", "world_observed");
        var passed = e.Require("single_load_observation", !LoadObserved)
            && e.Require("native_load_completed", nativeCompleted)
            && e.Require("native_paused", nativePaused)
            && e.Require("exact_saved_tick", tick.HasValue ? tick == SavedTick : null)
            && e.Require("owned_identity", identityMatches) && e.Require("owned_planet", planetMatches)
            && e.Require("world_observed", world is not null);
        LoadObserved = true;
        if (!passed) { _failure = e.Failure("saved_world_open_native_boundary_unproved", ""); return false; }
        _world = world;
        return true;
    }

    public bool ConfirmJournal(object? world, long tick, bool journalConfirmed, Action issueOpenOnlyCredential)
    {
        if (_failure is not null || Completed) return false;
        var e = new NativeResumeDecisionEvidence("saved_world_open_journal_adoption", tick, DateTimeOffset.UtcNow, true);
        e.Number("saved_game_tick", SavedTick); e.Number("loaded_game_tick", tick);
        e.NotReached("native_load_observed", "same_loaded_world", "exact_saved_tick", "journal_confirmed", "open_only_credential_persisted");
        var passed = e.Require("native_load_observed", LoadObserved && _world is not null)
            && e.Require("same_loaded_world", ReferenceEquals(world, _world))
            && e.Require("exact_saved_tick", tick == SavedTick) && e.Require("journal_confirmed", journalConfirmed);
        if (passed)
        {
            try { issueOpenOnlyCredential(); passed = e.Require("open_only_credential_persisted", true); }
            catch (Exception ex)
            {
                e.Require("open_only_credential_persisted", null);
                _failure = e.Failure("saved_world_open_journal_adoption_unproved");
                _failure.Observations["credential_exception"] = ex.GetType().Name;
                return false;
            }
        }
        if (!passed) { _failure = e.Failure("saved_world_open_journal_adoption_unproved", ""); return false; }
        Completed = true;
        return true;
    }
}
