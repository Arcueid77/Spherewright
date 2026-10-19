using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    public GameCallResult<PreparedNormalAction> PrepareCancelBlueprintOnMainThread(string? sessionId, PrepareCancelBlueprintRequest request)
    {
        var common = ValidatePrepareCommon(sessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if (!Guid.TryParse(request.BuildId, out _) || string.IsNullOrEmpty(request.ExpectedStateHash))
            return InvalidPlan("Cancellation requires an exact buildId and its fresh progress hash.");
        if (!_blueprints.TryRead(out var builds)) return GameCallResult<PreparedNormalAction>.Failed(BlueprintError("blueprint_progress_store_unavailable"));
        var build = builds.SingleOrDefault(b => b.BuildId == request.BuildId && b.Site.PlanetId == request.PlanetId);
        if (build is null) return InvalidPlan("No such finite construction belongs to this owned planet.");
        // No world reconciliation is required to STOP new submissions. Pending/unknown
        // results must remain visible and are never erased, rolled back or auto-dismantled.
        if (build.ProgressHash(sessionId!, common.Session!.Revision) != request.ExpectedStateHash)
            return StalePlan("Read current finite progress before preparing cancellation.");
        if (build.Phase == "completed" || build.Phase == "cancelled") return InvalidPlan("The finite plan is already stopped.");
        var plan = NormalActionPlanPayload.Blueprint(sessionId!, request.PlanetId, common.Session.Revision,
            string.Empty, build, request.ExpectedStateHash, 0, null, null, true);
        return AddPreparedPlan(plan, common.Session, 1,
            "Stop future submissions of this exact finite build/action. Already created prebuilds and entities stay; drones may finish pending work. No demolition, refund, rollback or silent whole-module retry.");
    }

    private BridgeError? RevalidateBlueprintCancelOnMainThread(NormalActionPlanPayload plan)
    {
        if (!_blueprints.TryRead(out var builds)) return BlueprintError("blueprint_progress_store_unavailable");
        var build = builds.SingleOrDefault(b => b.BuildId == plan.BlueprintState!.BuildId);
        // Natural progress may advance after cancellation prepare. It is safe to stop the
        // same action, but not a newly resumed action with a different authority/identity.
        if (build is null || build.PlanHash != plan.BlueprintState!.PlanHash || build.ActionId != plan.BlueprintState.ActionId)
            return Stale("The finite action identity changed after cancellation preparation.");
        plan.BlueprintState = build;
        return null;
    }

    private void CancelBlueprintBuildOnMainThread(ActionRecord cancellation)
    {
        if (IsStructuredConstruction(cancellation.Plan)) { CancelStructuredConstructionOnMainThread(cancellation); return; }
        var build = cancellation.Plan.BlueprintState!;
        var active = _actions.ActiveValues.SingleOrDefault(a => !a.Terminal && a.ActionKind == NormalActionKinds.BlueprintBuild
            && a.BlueprintBuild?.BuildId == build.BuildId && a.ActionId == build.ActionId);
        if (active is not null) build = active.BlueprintBuild!;
        build.Stop("cancelled");
        if (!_blueprints.TryPut(build)) throw new InvalidOperationException("Cancellation could not be persisted.");
        cancellation.BlueprintBuild = build;
        if (active is not null)
        {
            active.FailureKind = "cancelled";
            Fail(active, "Explicit cancellation stopped only unsubmitted work. Already created objects are retained; inspect pending objects and fresh-prepare the same buildId if resuming.");
        }
        Complete(cancellation, "Future finite-plan submissions cancelled durably. No object was dismantled, no material refunded, and pending drones may continue normally.");
    }
}
