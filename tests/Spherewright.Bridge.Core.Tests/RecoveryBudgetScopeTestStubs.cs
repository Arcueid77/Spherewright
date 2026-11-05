using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

// Only loaded-world/session object identity is substituted. The scope matcher,
// accounting, allowance selection and evidence factory are linked production.
internal sealed partial class GameSessionTracker
{
    internal void BindAccountingRecoveryForTest(VoyagerRemoteResumeEvidence evidence, VoyagerRecoveryAuthorization authority)
    {
        _sessionId = SessionId = "synthetic-accounting-session";
        _ownedSaveName = OwnedSaveName = evidence.Journey.OwnedSaveName;
        _gameVersion = evidence.Journey.GameVersion; IsCurrentSessionOwned = true;
        _ownedData = GameMain.data = new() { gameName = _ownedSaveName, localLoadedPlanetFactory = new object() };
        _remoteResumeAdoption = new() { World = _ownedData, SessionId = _sessionId,
            ResumeActionId = authority.ResumeActionId, Evidence = evidence, Authorization = authority };
    }

    internal void BreakAccountingRecoveryForTest(string fault)
    {
        switch (fault)
        {
            case "world": GameMain.data = new(); break;
            case "session": _sessionId = "different-session"; break;
            case "save": _ownedSaveName = "different-owned-save"; break;
            case "version": _gameVersion = "different-native-version"; break;
            case "action": _remoteResumeAdoption!.ResumeActionId = "different-original-resume"; break;
            case "owned": IsCurrentSessionOwned = false; break;
            default: throw new ArgumentException(nameof(fault));
        }
    }
}
