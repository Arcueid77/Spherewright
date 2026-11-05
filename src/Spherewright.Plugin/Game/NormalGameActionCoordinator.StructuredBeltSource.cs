using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static bool TryBindStructuredEmptyBeltSource(NormalActionPlanPayload plan,
        IReadOnlyList<BlueprintBuildState> paid, out StructuredEmptyBeltSource? evidence, out string rejection)
    {
        evidence = null; rejection = "structured_empty_source_shape_unproved";
        var steps = plan.BuildSteps;
        var source = steps[0].SourceBeltAnchor;
        if (source is null) return !steps.Skip(1).Any(s => s.SourceBeltAnchor is not null);
        if (plan.BuildKind != NormalBuildKinds.Belt || plan.BuildingItemId != 2001 || source.ItemId != 2001 || steps.Count < 2
            || plan.SourceObjectId != source.EntityId || plan.DestinationObjectId != 0
            || steps[0].InputObjectId != source.EntityId || steps[0].InputStepIndex != -1
            || steps[0].InputFromSlot != 0 || steps[0].InputToSlot != 1
            || steps.Skip(1).Any(s => s.SourceBeltAnchor is not null)
            || steps.Any(s => s.DestinationBeltAnchor is not null || s.BeltPathMode != BeltPathModes.NativeGrid || s.Tilt != 0)) return false;
        rejection = "structured_empty_source_paid_identity_unproved";
        if (!paid.Any(b => b.Site.PlanetId == plan.PlanetId && b.StructuredConstruction is not null
            && PaidFactoryCompletionPolicy.HasCompletedIdleProof(b, GameMain.gameTick)
            && b.Objects.Count == b.Site.Objects.Count && b.Objects.Zip(b.Site.Objects, (o, spec) =>
                o.State == BlueprintObjectStates.Completed && o.EntityId == source.EntityId && spec.ItemId == 2001
                    && (source.Position - ToVector(spec.Position)).sqrMagnitude <= .003f * .003f).Any(x => x))) return false;
        var factory = GameMain.localPlanet?.factory;
        rejection = "structured_empty_source_current_path_unproved";
        var reason = "factory_unavailable";
        if (factory is null || !TryCaptureBeltSource(factory, source.EntityId, out var fresh, out reason))
        { rejection += ":" + reason; return false; }
        if (fresh!.ItemId != 2001 || fresh.Connections.Count == 0 || fresh.Connections[0].OtherObjectId != 0
            || fresh.CargoStackCount != 0 || !BeltSourceReusePolicy.SameEvidence(source.BindingHash, fresh.BindingHash)
            || !BeltSourceReusePolicy.SameEvidence(source.CargoHash, fresh.CargoHash)
            || !BeltSourceReusePolicy.SameEvidence(source.CompletionTopologyHash, fresh.CompletionTopologyHash)) return false;
        evidence = new StructuredEmptyBeltSource { ObjectId = source.EntityId, BindingHash = fresh.BindingHash,
            CargoHash = fresh.CargoHash, CompletionTopologyHash = fresh.CompletionTopologyHash, CargoStackCount = 0 };
        rejection = string.Empty; return true;
    }

    private static string? StructuredEmptySourcePrecreateBlocker(NormalActionPlanPayload plan)
    {
        var build = plan.BlueprintState;
        var source = build?.StructuredConstruction?.EmptyBeltSource;
        // Paid pending/completed continuation never calls Create again and may
        // legitimately have cargo. Only an entirely NEW batch uses this grant.
        if (source is null || build!.Objects.Any(o => o.State != BlueprintObjectStates.NotSubmitted)) return null;
        var factory = GameMain.localPlanet?.factory;
        if (factory is null || plan.BuildSteps.Count < 2 || plan.BuildSteps[0].SourceBeltAnchor is null
            || !TryCaptureBeltSource(factory, source.ObjectId, out var fresh, out _)
            || fresh!.ItemId != 2001 || fresh.Connections.Count == 0 || fresh.Connections[0].OtherObjectId != 0
            || !source.MatchesCurrent(fresh.EntityId, fresh.BindingHash, fresh.CargoHash, fresh.CompletionTopologyHash, fresh.CargoStackCount))
            return "structured_empty_source_changed_before_native_create";
        return null;
    }
}
