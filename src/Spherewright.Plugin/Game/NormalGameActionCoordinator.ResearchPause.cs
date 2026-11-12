using Spherewright.Bridge.Core.Progression;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static bool CanPrepareResearchPause(int techId, bool paused)
    {
        var history = GameMain.history;
        var tech = LDB.techs.Select(techId);
        if (history is null || tech is null || history.TechUnlocked(techId)
            || !ResearchQueuePolicy.CanSetPaused(history.techQueue, history.currentTech, techId, paused))
            return false;
        return (tech.PreTechs ?? Array.Empty<int>()).All(id => history.TechUnlocked(id, tech.PreTechsMax))
            && (tech.PreTechsImplicit ?? Array.Empty<int>()).All(id => history.TechUnlocked(id, tech.PreTechsMax));
    }

    private void ExecuteResearchPauseOnMainThread(ActionRecord action)
    {
        var paused = action.Plan.ResearchPaused
            ?? throw new InvalidOperationException("Prepared research pause direction is missing.");
        if (!CanPrepareResearchPause(action.Plan.TechId, paused))
            throw new InvalidOperationException("Prepared research pause state changed before execution.");
        var request = new LocalPlanetRequest { PlanetId = action.PlanetId };
        var before = _reader.GetProgressionStateOnMainThread(action.SessionId, request).Value
            ?? throw new InvalidOperationException("Research pause pre-execution readback unavailable.");
        var playerBefore = _reader.GetPlayerStateOnMainThread(action.SessionId, request).Value
            ?? throw new InvalidOperationException("Research pause material readback unavailable.");

        // UIResearchQueue uses these same Native business methods.
        if (paused) GameMain.history.PauseTechQueue();
        else GameMain.history.ResumeTechQueue();

        var after = _reader.GetProgressionStateOnMainThread(action.SessionId, request).Value
            ?? throw new InvalidOperationException("Research pause readback unavailable; retain the original action.");
        var playerAfter = _reader.GetPlayerStateOnMainThread(action.SessionId, request).Value
            ?? throw new InvalidOperationException("Research pause material readback unavailable; retain the original action.");
        action.ResearchQueueReadback = ResearchQueuePolicy.ProvePausedState(
            before, after, playerBefore, playerAfter, action.Plan.TechId, paused);
        Complete(action, paused
            ? "DSP normally paused research. The queue, progress, inventory and research buffers are preserved at this tick."
            : "DSP normally resumed research. The queue, progress, inventory and research buffers are preserved at this tick.");
    }
}
