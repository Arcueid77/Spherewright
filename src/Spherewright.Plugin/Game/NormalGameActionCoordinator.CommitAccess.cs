using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static BridgeError? ValidateCommitCommon(
        SessionState session,
        NormalActionPlanPayload plan,
        CommitNormalActionRequest request)
    {
        if (!session.OwnedBySpherewright
            || !string.Equals(session.SessionId, plan.SessionId, StringComparison.Ordinal)
            || !string.Equals(request.SessionId, plan.SessionId, StringComparison.Ordinal))
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "The prepared action does not belong to the current owned session.",
                false,
                "Inspect the current session and prepare a fresh action.");
        }

        if (request.PlanetId != plan.PlanetId || (plan.ActionKind != NormalActionKinds.CancelVoyagerFlight
            && session.LocalPlanetId != plan.PlanetId))
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "Commit planet, planned planet, and current local planet do not match.",
                false,
                "Return to the planned planet and prepare a fresh action.");
        }

        var commitBlockers = plan.ActionKind == NormalActionKinds.Save && plan.SavePreservation is not null
            ? plan.SavePreservation.RemainingBlockers(session.WriteBlockers) : session.WriteBlockers;
        if (commitBlockers.Count > 0)
        {
            var blocker = commitBlockers[0];
            return BridgeError.Create(
                blocker.Code,
                blocker.Message,
                false,
                "Resolve every current session write blocker, then prepare a fresh action.");
        }

        return null;
    }
}
