using Spherewright.Bridge.Core.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private bool HasCurrentSavedPaidStop(BlueprintBuildState build)
    {
        var proof = SavedPaidConstructionStop.Current(build, GameMain.gameTick);
        return proof is not null && proof.NativeSessionId == _sessions.SessionId && proof.StoppedActionId == build.ActionId
            && _savedConstructionTail is { GenericPaidSite: true, Sealed: true } tail && tail.Build.BuildId == build.BuildId
            && tail.Adoption.SessionId == _sessions.SessionId && _sessions.IsCurrentSessionOwned
            && ReferenceEquals(tail.Adoption.World, GameMain.data) && ReferenceEquals(tail.Adoption.Player, GameMain.mainPlayer)
            && ReferenceEquals(tail.Factory, GameMain.localPlanet?.factory);
    }
}
