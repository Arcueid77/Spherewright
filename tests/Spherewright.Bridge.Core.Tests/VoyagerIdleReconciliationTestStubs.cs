using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class FlightStartTestPlayer
{
    internal UnityEngine.Vector3 position = new(0, 200, 0);
}

internal static partial class GameMain
{
    internal static int IdleReconcileResumeCalls;
    internal static bool IgnoreIdleResume;
    internal static void Resume()
    {
        IdleReconcileResumeCalls++;
        if (!IgnoreIdleResume) isPaused = false;
    }
}

internal sealed partial class GameSessionTracker
{
    internal string? WriteQuarantineReason => _writeQuarantineReason;
}

// Native observations are substitutes; the retained Store and idle guard are production.
internal sealed partial class NormalGameActionCoordinator
{
    internal void BindSuccessfulIdleForReconciliationTest()
    {
        var j = _flightCheckpoints.VoyagerJourney!;
        var owner = new VoyagerRemoteIdleOwner(new VoyagerNativeBinding(GameMain.data!, GameMain.mainPlayer),
            _sessions.SessionId!, j.JourneyId, j.ActiveActionId, j.ActiveActionId);
        var decision = CaptureVoyagerRemoteIdleAdmission(owner, j, null, j.SpentGameTicks, j.LastLandedGameTick, out var scene);
        if (!decision.Proved) throw new InvalidOperationException(decision.BlockerCode);
        _voyagerRemoteIdleOwner = owner; _voyagerRemoteIdleScene = scene;
    }

    internal bool ProvePausedIdleForTest(out string proof, out string rejection)
    {
        var s = _sessions.CaptureOnMainThread();
        return TryProvePausedVoyagerIdle(s, s.WriteQuarantineActionId!, out proof, out rejection);
    }

    internal bool ResumePausedIdleForTest(string proof, out string rejection)
    {
        var s = _sessions.CaptureOnMainThread();
        return TryResumeProvedVoyagerIdle(s, s.WriteQuarantineActionId!, proof, out rejection);
    }

    internal void StopHostForIdleReconciliationTest() => GuardVoyagerRemoteIdleOnMainThread(stoppingHost: true);

    internal ActionRecord RetainLocalResultForIdleTest(string state, bool terminal)
    {
        var result = new ActionRecord
        {
            SessionId = _sessions.SessionId!,
            PlanetId = GameMain.localPlanet.id,
            ActionKind = NormalActionKinds.Move,
            State = state,
            Terminal = terminal,
            Message = "original result"
        };
        _actions.ActiveValues.Add(result);
        return result;
    }
}
