using Spherewright.Bridge.Core.Progression;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private void ExecuteResearchEnqueueOnMainThread(ActionRecord action)
    {
        var request = new LocalPlanetRequest { PlanetId = action.PlanetId };
        var before = _reader.GetProgressionStateOnMainThread(action.SessionId, request).Value
            ?? throw new InvalidOperationException("Research enqueue pre-execution readback unavailable.");
        var playerBefore = _reader.GetPlayerStateOnMainThread(action.SessionId, request).Value
            ?? throw new InvalidOperationException("Research enqueue material readback unavailable.");

        // The normal native queue path. No progress, unlock, inventory, buffer,
        // queue-array or player-input writes are made by the adapter.
        GameMain.history.EnqueueTech(action.Plan.TechId);

        var after = _reader.GetProgressionStateOnMainThread(action.SessionId, request).Value
            ?? throw new InvalidOperationException("Research enqueue readback unavailable; do not replay.");
        var playerAfter = _reader.GetPlayerStateOnMainThread(action.SessionId, request).Value
            ?? throw new InvalidOperationException("Research enqueue material readback unavailable; do not replay.");
        action.ResearchQueueReadback = ResearchQueuePolicy.ProveEnqueue(
            before, after, playerBefore, playerAfter, action.Plan.TechId);
        Complete(action, "DSP normally appended the requested technology; immediate queue, research progress, inventory and research buffers verified. Research still requires normal materials and time.");
    }
}
