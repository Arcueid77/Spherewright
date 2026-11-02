using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private bool FailedLandingResumeBindingMatches(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord j)
    {
        var e = adoption.Evidence;
        if (e is null) return false;
        var source = e.FailedLandingSource;
        var run = j.FailedLandingAdmissions.LastOrDefault();
        return source is { IsDisposed: false } && !e.IsDisposed && j.Phase == "recovery_required" && !j.ReturnLeg
            && !j.LastLandedGameTick.HasValue && run is { State: "adopted" }
            && run.ActionId == adoption.ResumeActionId && run.SessionId == adoption.SessionId
            && run.ContextFingerprint == source.Fingerprint && run.SourceFingerprint == e.SourceFingerprint
            && run.SourceGameTick == e.SourceGameTick && run.SourceGameTick == source.SavedAccountingAnchorGameTick
            && j.LastAccountedGameTick == run.SourceGameTick && j.SpentGameTicks == run.ConservedSpentGameTicks
            && _flightCheckpoints.RemoteResumeEvidenceStillMatches(e)
            && _sessions.IsCurrentSessionOwned && _sessions.SessionId == adoption.SessionId
            && VoyagerLocalActionBudgetPolicy.MatchesOwnedJourneyIdentity(j.OwnedSaveName, j.GameVersion,
                _sessions.IsCurrentSessionOwned, _sessions.OwnedSaveName, adoption.World?.gameName, _sessions.CurrentNativeGameVersion)
            && ReferenceEquals(GameMain.data, adoption.World) && ReferenceEquals(GameMain.mainPlayer, adoption.Player)
            && GameMain.localPlanet?.id == j.RemotePlanetId && adoption.Player.planetId == j.RemotePlanetId;
    }

    private long? RemoteResumeAccountingAnchor(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord j)
    {
        if (adoption.Evidence?.FailedLandingRecovery != true) return j.LastLandedGameTick;
        if (FailedLandingResumeBindingMatches(adoption, j)) return adoption.Evidence.FailedLandingAccountingAnchorGameTick;
        var run = j.FailedLandingAdmissions.LastOrDefault();
        // A fresh strict resave may follow the NEW persisted healthy admission.
        // It uses that landing, never the failed outbound's absent witness.
        return j.Phase == "remote_landed" && run is { State: "completed_health" }
            && run.ActionId == adoption.ResumeActionId && run.SessionId == adoption.SessionId
            && run.SourceFingerprint == adoption.Evidence.SourceFingerprint && run.TerminalAtGameTick == j.LastLandedGameTick
            && _sessions.IsCurrentSessionOwned && _sessions.SessionId == adoption.SessionId
            && ReferenceEquals(GameMain.data, adoption.World) && ReferenceEquals(GameMain.mainPlayer, adoption.Player)
                ? j.LastLandedGameTick : null;
    }
}
