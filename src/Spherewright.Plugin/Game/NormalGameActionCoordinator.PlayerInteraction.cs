using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private bool MatchesPlayerInteraction(string expectedHash, PlayerStateSnapshot current,
        out PassiveDriftMoveInspection? inspection)
    {
        inspection = null;
        var observed = _reader.LastPlayerInteractionInspection;
        if (GameMain.mainPlayer.currentOrder is not null || GameMain.mainPlayer.navigation.hasTask
            || GameMain.mainPlayer.navigation.navigating || observed is null
            || !observed.Allows(expectedHash, current, GameStateReader.PassiveDriftClockSeconds()))
            return expectedHash == current.StateHash;
        inspection = observed;
        return true;
    }

    private bool PlayerInteractionMatchesPlan(NormalActionPlanPayload plan, PlayerStateSnapshot current)
        => plan.PlayerInteractionInspection is { } inspection
            ? ReferenceEquals(inspection, _reader.LastPlayerInteractionInspection)
                && GameMain.mainPlayer.currentOrder is null && !GameMain.mainPlayer.navigation.hasTask
                && !GameMain.mainPlayer.navigation.navigating
                && inspection.Allows(inspection.StateHash, current, GameStateReader.PassiveDriftClockSeconds())
            : CanonicalStateHash.PlayerAction(current) == plan.PlayerStateHash;

    private sealed partial class NormalActionPlanPayload
    {
        public PassiveDriftMoveInspection? PlayerInteractionInspection { get; set; }
    }
}
