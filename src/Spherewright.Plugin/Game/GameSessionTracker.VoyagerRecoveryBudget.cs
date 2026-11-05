using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    internal DateTimeOffset EffectiveVoyagerDeadline(VoyagerJourneyRecord journey)
    {
        if (journey.ReturnOnlyTask is { } task)
            return IsCurrentSessionOwned && task.CurrentSessionId == _sessionId && journey.OwnedSaveName == _ownedSaveName
                && journey.GameVersion == _gameVersion ? task.CurrentDeadlineUtc.AddSeconds(-VoyagerControlDeadlinePolicy.StopCleanupSeconds)
                    : journey.DeadlineUtc;
        return MatchingVoyagerRecoveryAuthorization(journey)?.Proposal.DeadlineUtc ?? journey.DeadlineUtc;
    }

    internal long EffectiveVoyagerMaximumGameTicks(VoyagerJourneyRecord journey) =>
        VoyagerRecoveryWindowPolicy.EffectiveMaximumGameTicks(journey.MaximumGameTicks,
            MatchingVoyagerRecoveryAuthorization(journey)?.Proposal) ?? -1;

    private VoyagerRecoveryAuthorization? MatchingVoyagerRecoveryAuthorization(VoyagerJourneyRecord journey)
    {
        if (journey.ReturnOnlyTask is not null) return null;
        return MatchingVoyagerRecoveryScopeAuthorization(journey);
    }

    // Identity/accounting scope survives a return-only transition; its new
    // task clock takes precedence without erasing a prior progress debit.
    private VoyagerRecoveryAuthorization? MatchingVoyagerRecoveryScopeAuthorization(VoyagerJourneyRecord journey)
    {
        var adoption = _remoteResumeAdoption;
        var evidence = adoption?.Evidence;
        var authorization = adoption?.Authorization;
        if (authorization is null || evidence is null || !IsCurrentSessionOwned || !ReferenceEquals(GameMain.data, adoption!.World)
            || _sessionId != adoption.SessionId || authorization.ResumeActionId != adoption.ResumeActionId
            || authorization.EvidenceFingerprint != evidence.Fingerprint
            || journey.OwnedSaveName != _ownedSaveName || journey.GameVersion != _gameVersion) return null;
        var original = evidence.Journey;
        // Phase/action/accounting may advance normally; immutable limits may not.
        return journey.Version == original.Version && journey.JourneyId == original.JourneyId && journey.OwnedSaveName == original.OwnedSaveName
            && journey.GameVersion == original.GameVersion && journey.IssuedAtUtc == original.IssuedAtUtc
            && journey.HomePlanetId == original.HomePlanetId && journey.HomeStarId == original.HomeStarId
            && journey.DestinationStarId == original.DestinationStarId && journey.RemotePlanetId == original.RemotePlanetId
            && journey.StartedAtGameTick == original.StartedAtGameTick && journey.SpentGameTicks >= original.SpentGameTicks
            && journey.LastAccountedGameTick >= original.LastAccountedGameTick
            && journey.DeadlineUtc == original.DeadlineUtc && journey.MaximumGameTicks == original.MaximumGameTicks
            && journey.MaximumRoundTripWallSeconds == original.MaximumRoundTripWallSeconds
            && journey.ReturnReserveGameTicks == original.ReturnReserveGameTicks
            && journey.ReturnEnergyFloorJoules == original.ReturnEnergyFloorJoules
            && journey.WithdrawalReserveJoules == original.WithdrawalReserveJoules
            && journey.ReturnCoreEnergyMinimumJoules == original.ReturnCoreEnergyMinimumJoules
            && journey.ExtraWarperReserve == original.ExtraWarperReserve
            ? authorization : null;
    }

    internal long? EffectiveVoyagerSpentGameTicks(VoyagerJourneyRecord journey, long? projectedSpent = null)
    {
        if (!IsCurrentSessionOwned || journey.OwnedSaveName != _ownedSaveName || journey.GameVersion != _gameVersion) return null;
        var spent = projectedSpent ?? journey.SpentGameTicks;
        var adoption = _remoteResumeAdoption;
        var evidence = adoption?.Evidence;
        var original = evidence?.Journey;
        return VoyagerRecoveryAccountingScopePolicy.ConservedSpent(journey.JourneyId, original?.JourneyId,
            evidence?.RemotePrimaryRecovery == true,
            MatchingVoyagerRecoveryScopeAuthorization(journey) is not null, spent,
            original?.SpentGameTicks ?? -1, evidence?.SourceGameTick ?? -1,
            evidence?.KnownPrimaryProgressTick);
    }

}
