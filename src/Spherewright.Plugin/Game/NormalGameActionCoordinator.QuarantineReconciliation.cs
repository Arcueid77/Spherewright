using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    public GameCallResult<PreparedNormalAction> PrepareQuarantineReconciliationOnMainThread(
        string? requestedSessionId,
        PrepareQuarantineReconciliationRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        var session = common.Session!;
        if (session.Revision != request.ExpectedRevision)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleRevision,
                "The session revision changed before quarantine reconciliation was prepared.",
                true,
                "Read session state and the quarantined action again, then prepare against that exact revision."));
        }

        if (!string.Equals(session.WriteHealth, WriteHealthStates.Quarantined, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(session.WriteQuarantineActionId)
            || !string.Equals(session.WriteQuarantineActionId, request.ActionId, StringComparison.Ordinal))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.WriteSubsystemQuarantined,
                "The requested action is not the exact action currently responsible for write quarantine.",
                false,
                "Use writeQuarantineActionId from the current owned-session state."));
        }

        var pausedIdle = IsPausedVoyagerIdleReconciliation(request.ActionId);
        ActionRecord? action = null;
        if (!pausedIdle && (!_actions.TryGetValue(request.ActionId, out action)
            || action is null
            || !string.Equals(action.SessionId, session.SessionId, StringComparison.Ordinal)
            || action.PlanetId != request.PlanetId
            || !action.Terminal
            || !string.Equals(action.State, NormalActionStates.OutcomeUnknown, StringComparison.Ordinal)
            || !string.Equals(action.ActionKind, NormalActionKinds.Build, StringComparison.Ordinal)))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionOutcomeUnknown,
                "Only an exact retained unknown build or a proved same-session paused Voyager idle owner can be reconciled.",
                false,
                "Keep the session running and inspect the writeQuarantineActionId action; restart-resume is required if its proof record is unavailable."));
        }

        IReadOnlyList<int> resolvedEntityIds = Array.Empty<int>();
        string proofHash, rejection;
        var proved = pausedIdle
            ? TryProvePausedVoyagerIdle(session, request.ActionId, out proofHash, out rejection)
            : TryProveQuarantinedBuild(action!, out resolvedEntityIds, out proofHash, out rejection);
        if (!proved)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                pausedIdle ? BridgeErrorCodes.ActionRejected : BridgeErrorCodes.ActionOutcomeUnknown,
                "The quarantined action still cannot be reconciled: " + rejection,
                true,
                "Leave writes quarantined, inspect the exact entities and topology, and retry only after the world itself provides unambiguous proof."));
        }

        var reason = _sessions.WriteQuarantineReason ?? action?.OriginalOutcomeMessage ?? action?.Message ?? string.Empty;
        var payload = NormalActionPlanPayload.QuarantineReconciliation(
            session.SessionId!,
            request.PlanetId,
            proofHash,
            request.ActionId,
            reason,
            session.Revision,
            resolvedEntityIds);
        payload.ReconcilePausedVoyagerIdle = pausedIdle;
        PreparedPlan<NormalActionPlanPayload> plan;
        try
        {
            plan = _plans.Add(proofHash, payload);
        }
        catch (InvalidOperationException)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ServerBusy,
                "Too many normal-game plans are active.",
                true,
                "Wait for old plans to expire, then prepare this reconciliation again."));
        }

        var blockers = session.WriteBlockers
            .Where(blocker => !string.Equals(blocker.Code, BridgeErrorCodes.WriteSubsystemQuarantined, StringComparison.Ordinal))
            .Select(CloneBlocker)
            .ToList();
        return GameCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction
        {
            Prepared = true,
            ActionKind = NormalActionKinds.ReconcileQuarantine,
            PlanToken = plan.Token,
            ExpiresAtUtc = plan.ExpiresAtUtc,
            ExpectedStateHash = proofHash,
            StateHashVersion = StateHashVersion,
            CommitAllowedNow = blockers.Count == 0,
            CommitBlockers = blockers,
            CompletionCondition = pausedIdle
                ? "The same paused world, controller, original journey and quarantine are freshly rechecked for strict idle and unchanged remaining reserves. Commit resumes this world and clears only this quarantine; no Load, Save, order replay or budget renewal occurs."
                : "The exact retained outcome-unknown build still has the same item-cost, entity identities, components, and directed topology; only then is its quarantine cleared.",
            ReconcilesActionId = request.ActionId,
            ProvedObjectIds = resolvedEntityIds.ToList(),
        });
    }

    public GameCallResult<NormalActionCommitResult> CommitQuarantineReconciliationOnMainThread(
        string? requestedSessionId,
        CommitNormalActionRequest request)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "A UUID idempotency key is required.",
                false,
                "Generate one UUID and reuse it for retries of this exact reconciliation commit."));
        }

        if (!string.Equals(requestedSessionId, request.SessionId, StringComparison.Ordinal))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "Envelope and commit payload session IDs do not match.",
                false,
                "Use the exact current owned session ID in both locations."));
        }

        var fingerprint = CanonicalStateHash.Combine(
            "commit-" + NormalActionKinds.ReconcileQuarantine,
            request.SessionId,
            request.PlanetId,
            request.PlanToken);
        if (_idempotency.TryGet(
            request.SessionId,
            request.IdempotencyKey,
            fingerprint,
            out var replay,
            out var conflict))
        {
            return GameCallResult<NormalActionCommitResult>.Succeeded(CloneCommitResult(replay!, true));
        }

        if (conflict)
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyConflict,
                "The idempotency key is already bound to a different normal-game commit.",
                false,
                "Reuse it only for the original commit or generate a new UUID for a newly prepared reconciliation."));
        }

        if (!_idempotency.HasCapacity(request.SessionId))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.IdempotencyCapacityExceeded,
                "The Plugin idempotency cache has no capacity for another reconciliation result.",
                false,
                "Restart and resume the exact owned world before attempting another reconciliation; quarantine was not changed."));
        }

        if (!_plans.TryGet(request.PlanToken, out var prepared, out var expired) || prepared is null)
        {
            return MissingPlan<NormalActionCommitResult>(expired);
        }

        var plan = prepared.Payload;
        if (!string.Equals(plan.ActionKind, NormalActionKinds.ReconcileQuarantine, StringComparison.Ordinal))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "The plan token does not belong to quarantine reconciliation.",
                false,
                "Commit the plan through its matching reconciliation method."));
        }

        var session = _sessions.CaptureOnMainThread();
        var accessError = ValidateQuarantineReconciliationCommit(session, plan, request);
        if (accessError is not null)
        {
            return GameCallResult<NormalActionCommitResult>.Failed(accessError);
        }

        if (plan.ReconcilePausedVoyagerIdle)
        {
            _plans.Remove(request.PlanToken);
            if (!TryResumeProvedVoyagerIdle(session, plan.ReconcileActionId, plan.ExpectedStateHash, out var idleRejection))
                return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(BridgeErrorCodes.StaleState,
                    idleRejection, false, "Keep the original pause and action outcomes; inspect the current failed predicate before preparing again."));
            var resumed = new NormalActionCommitResult
            {
                ActionId = plan.ReconcileActionId,
                ActionKind = NormalActionKinds.ReconcileQuarantine,
                IdempotencyKey = request.IdempotencyKey,
                State = NormalActionStates.Completed,
                Accepted = true,
            };
            if (!_idempotency.TryAdd(request.SessionId, request.IdempotencyKey, fingerprint, resumed))
            {
                GameMain.Pause();
                _sessions.QuarantineWritesOnMainThread(plan.ReconcileActionId,
                    "Paused Voyager reconciliation completed but its idempotent receipt could not be retained.");
                return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(BridgeErrorCodes.ActionOutcomeUnknown,
                    "The reconciliation receipt is unproved; Native Pause was restored.", false, "Inspect the same session; do not replay any previous action."));
            }
            return GameCallResult<NormalActionCommitResult>.Succeeded(resumed); // Original flight/Move results are untouched.
        }

        var rejection = "The retained quarantine action is unavailable.";
        IReadOnlyList<int> resolvedEntityIds = Array.Empty<int>();
        var proofHash = string.Empty;
        if (!_actions.TryGetValue(plan.ReconcileActionId, out var action)
            || !TryProveQuarantinedBuild(action, out resolvedEntityIds, out proofHash, out rejection)
            || !string.Equals(proofHash, plan.ExpectedStateHash, StringComparison.Ordinal)
            || !resolvedEntityIds.SequenceEqual(plan.ReconcileEntityIds))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "The exact quarantine proof changed after prepare: " + rejection,
                true,
                "Read the quarantined action and prepare a fresh reconciliation proof."));
        }

        var accepted = new NormalActionCommitResult
        {
            ActionId = action.ActionId,
            ActionKind = NormalActionKinds.ReconcileQuarantine,
            IdempotencyKey = request.IdempotencyKey,
            State = NormalActionStates.Completed,
            Accepted = true,
            IdempotentReplay = false,
        };
        _plans.Remove(request.PlanToken);
        if (!_sessions.TryClearQuarantineOnMainThread(plan.ReconcileActionId, plan.ReconcileReason, out var clearRejection))
        {
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState,
                clearRejection ?? "The exact quarantine identity changed before it could be cleared.",
                false,
                "Read current session state; do not retry the old action with a new idempotency key."));
        }

        if (!_idempotency.TryAdd(request.SessionId, request.IdempotencyKey, fingerprint, accepted))
        {
            _sessions.QuarantineWritesOnMainThread(
                action.ActionId,
                "Quarantine reconciliation changed write health but its idempotent result could not be retained.");
            return GameCallResult<NormalActionCommitResult>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionOutcomeUnknown,
                "Quarantine reconciliation completed its state transition but its idempotent result could not be retained.",
                false,
                "Do not retry with a new key; inspect session write health and the exact action result."));
        }

        action.State = NormalActionStates.Completed;
        action.Succeeded = true;
        action.TargetObjectIds = resolvedEntityIds.ToList();
        action.TargetObjectId = resolvedEntityIds.Count == 1 ? resolvedEntityIds[0] : (int?)null;
        action.ReconciledFromOutcomeUnknown = true;
        action.ReconciledAtGameTick = GameMain.gameTick;
        action.Message = $"The prior outcome-unknown build was reconciled from exact cost, entity, component, and directed-topology proof. Original quarantine: {action.OriginalOutcomeMessage}";
        action.AfterStateHash = CaptureStructuredAfterStateHash(action);
        return GameCallResult<NormalActionCommitResult>.Succeeded(accepted);
    }

    private BridgeError? ValidateQuarantineReconciliationCommit(
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
                "The reconciliation plan does not belong to the current owned session.",
                false,
                "Inspect the current owned session and its quarantine action again.");
        }

        if (request.PlanetId != plan.PlanetId || session.LocalPlanetId != plan.PlanetId)
        {
            return Stale("Commit planet, planned planet, and current local planet do not match.");
        }

        if (session.Revision != plan.ReconcileExpectedRevision
            || !string.Equals(session.WriteHealth, WriteHealthStates.Quarantined, StringComparison.Ordinal)
            || !string.Equals(session.WriteQuarantineActionId, plan.ReconcileActionId, StringComparison.Ordinal)
            || !string.Equals(_sessions.WriteQuarantineReason, plan.ReconcileReason, StringComparison.Ordinal))
        {
            return Stale("The exact quarantined action, reason, or session revision changed after prepare.");
        }

        var otherBlocker = session.WriteBlockers.FirstOrDefault(blocker =>
            !string.Equals(blocker.Code, BridgeErrorCodes.WriteSubsystemQuarantined, StringComparison.Ordinal));
        return otherBlocker is null
            ? null
            : BridgeError.Create(
                otherBlocker.Code,
                otherBlocker.Message,
                false,
                "Resolve every non-quarantine write blocker before reconciling this action.");
    }

    private bool TryProveQuarantinedBuild(
        ActionRecord action,
        out IReadOnlyList<int> resolvedEntityIds,
        out string proofHash,
        out string rejection)
    {
        resolvedEntityIds = Array.Empty<int>();
        proofHash = string.Empty;
        rejection = string.Empty;
        if (!string.Equals(action.ActionKind, NormalActionKinds.Build, StringComparison.Ordinal)
            || action.AfterInventory is null
            || action.Plan.BuildSteps.Count == 0
            || action.ExpectedBuildEntities.Count != action.Plan.BuildSteps.Count)
        {
            rejection = "The retained action lacks a complete build plan or quarantine-time inventory snapshot.";
            return false;
        }

        var expectedItemDelta = -action.Plan.BuildSteps.Count;
        var actualItemDelta = GetCount(action.AfterInventory, action.Plan.BuildingItemId)
            - GetCount(action.BeforeInventory, action.Plan.BuildingItemId);
        if (actualItemDelta != expectedItemDelta)
        {
            rejection = $"The retained building-item delta is {actualItemDelta}, not {expectedItemDelta}.";
            return false;
        }

        var factory = GameMain.localPlanet?.factory;
        if (factory is null)
        {
            rejection = "The current local factory is unavailable.";
            return false;
        }

        var anyPrebuildAlive = action.PrebuildIds.Any(prebuildId =>
            prebuildId > 0
            && prebuildId < factory.prebuildCursor
            && prebuildId < factory.prebuildPool.Length
            && factory.prebuildPool[prebuildId].id == prebuildId
            && !factory.prebuildPool[prebuildId].isDestroyed);
        if (anyPrebuildAlive)
        {
            rejection = "At least one accepted ordinary prebuild is still alive.";
            return false;
        }

        List<int> resolved;
        if (string.Equals(action.Plan.BuildKind, NormalBuildKinds.Belt, StringComparison.Ordinal))
        {
            if (!TryResolveUniqueBeltPath(factory, action, out resolved))
            {
                rejection = "No unique directed belt path matches every retained step and endpoint.";
                return false;
            }
        }
        else
        {
            resolved = new List<int>();
            foreach (var expected in action.ExpectedBuildEntities)
            {
                var candidates = FindTopologyMatchingCandidates(factory, action, expected, resolved);
                if (candidates.Count != 1)
                {
                    rejection = $"Build step at the retained pose has {candidates.Count} topology-matching candidates, not exactly one.";
                    return false;
                }

                resolved.Add(candidates[0]);
            }
        }

        if (!VerifyBuiltTopology(factory, action.Plan, resolved, out rejection))
        {
            return false;
        }

        var fields = new List<object?>
        {
            action.SessionId,
            action.PlanetId,
            action.ActionId,
            action.BeforeStateHash,
            action.OriginalOutcomeMessage ?? action.Message,
            action.Plan.BuildKind,
            action.Plan.BuildingItemId,
            action.Plan.BuildSteps.Count,
            actualItemDelta,
        };
        fields.AddRange(resolved.Cast<object?>());
        proofHash = CanonicalStateHash.Combine("quarantine-build-reconciliation", fields.ToArray());
        resolvedEntityIds = resolved;
        return true;
    }

    private static bool TryResolveUniqueBeltPath(
        PlanetFactory factory,
        ActionRecord action,
        out List<int> resolved)
    {
        var candidatesByStep = new List<IReadOnlyList<DirectedBuildEntityCandidate>>();
        foreach (var expected in action.ExpectedBuildEntities)
        {
            var candidates = new List<DirectedBuildEntityCandidate>();
            var limit = Math.Min(factory.entityCursor, factory.entityPool.Length);
            for (var entityId = 1; entityId < limit; entityId++)
            {
                ref var entity = ref factory.entityPool[entityId];
                var distance = (entity.pos - expected.Position).sqrMagnitude;
                if (entity.id != entityId || entity.protoId != expected.ItemId || distance >= 0.09f)
                {
                    continue;
                }

                factory.ReadObjectConn(entityId, 1, out var inputIsOutput, out var inputObjectId, out _);
                factory.ReadObjectConn(entityId, 0, out var outputIsOutput, out var outputObjectId, out _);
                candidates.Add(new DirectedBuildEntityCandidate(
                    entityId,
                    inputIsOutput ? 0 : inputObjectId,
                    outputIsOutput ? outputObjectId : 0,
                    distance));
            }

            candidatesByStep.Add(candidates);
        }

        var proved = BuildEntityAttribution.TrySelectUniqueDirectedPath(
            candidatesByStep,
            action.PreexistingBuildEntityIds,
            action.Plan.SourceObjectId,
            action.Plan.DestinationObjectId,
            0.09f,
            out var selected);
        resolved = proved ? selected.ToList() : new List<int>();
        return proved;
    }

    private static List<int> FindTopologyMatchingCandidates(
        PlanetFactory factory,
        ActionRecord action,
        BuildExpectedEntity expected,
        IReadOnlyCollection<int> alreadyResolved)
    {
        var result = new List<int>();
        var limit = Math.Min(factory.entityCursor, factory.entityPool.Length);
        for (var entityId = 1; entityId < limit; entityId++)
        {
            ref var entity = ref factory.entityPool[entityId];
            if (entity.id != entityId
                || entity.protoId != expected.ItemId
                || (entity.pos - expected.Position).sqrMagnitude >= 0.09f
                || action.PreexistingBuildEntityIds.Contains(entityId)
                || alreadyResolved.Contains(entityId))
            {
                continue;
            }

            if (string.Equals(action.Plan.BuildKind, NormalBuildKinds.Inserter, StringComparison.Ordinal))
            {
                if (entity.inserterId <= 0
                    || entity.inserterId >= factory.factorySystem.inserterCursor
                    || entity.inserterId >= factory.factorySystem.inserterPool.Length)
                {
                    continue;
                }

                ref var inserter = ref factory.factorySystem.inserterPool[entity.inserterId];
                if (inserter.id != entity.inserterId
                    || inserter.entityId != entityId
                    || inserter.pickTarget != action.Plan.SourceObjectId
                    || inserter.insertTarget != action.Plan.DestinationObjectId)
                {
                    continue;
                }
            }

            result.Add(entityId);
        }

        return result;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public string ReconcileActionId { get; private set; } = string.Empty;

        public string ReconcileReason { get; private set; } = string.Empty;

        public long ReconcileExpectedRevision { get; private set; }

        public bool ReconcilePausedVoyagerIdle { get; set; }

        public List<int> ReconcileEntityIds { get; } = new List<int>();

        public static NormalActionPlanPayload QuarantineReconciliation(
            string sessionId,
            int planetId,
            string proofHash,
            string actionId,
            string reason,
            long expectedRevision,
            IReadOnlyList<int> entityIds)
        {
            var result = new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.ReconcileQuarantine,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = proofHash,
                ReconcileActionId = actionId,
                ReconcileReason = reason,
                ReconcileExpectedRevision = expectedRevision,
                EstimatedTicks = 1,
            };
            result.ReconcileEntityIds.AddRange(entityIds);
            return result;
        }
    }
}
