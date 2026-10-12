using Spherewright.Bridge.Core.Safety;
using Spherewright.Bridge.Core.Logistics;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Resources;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareStructuredBuildOnMainThread(
        string? requestedSessionId,
        PrepareBuildRequest request)
    {
        var routePreviewError = BeltRoutePreviewPolicy.ValidateRequest(request);
        if (routePreviewError is not null) return InvalidPlan(routePreviewError);
        var endpointPreviewError = BeltEndpointPreviewPolicy.ValidateRequest(request);
        if (endpointPreviewError is not null) return InvalidPlan(endpointPreviewError);
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.PreferredDistance < 5f || request.PreferredDistance > 30f)
        {
            return InvalidPlan("Preferred build distance must be from 5 through 30 metres.");
        }

        if (request.PathLength < 1.5f || request.PathLength > 30f)
        {
            return InvalidPlan("A belt path length must be from 1.5 through 30 metres.");
        }

        var item = LDB.items.Select(request.BuildingItemId);
        if (item?.prefabDesc is null || !item.CanBuild)
        {
            return InvalidPlan("The requested runtime item is not a placeable building.");
        }

        var filterError = ValidateInitialSorterFilter(item, request.InitialSorterFilterItemId);
        if (filterError is not null)
            return GameCallResult<PreparedNormalAction>.Failed(filterError);

        var routingError = request.BeltRoutePreview is null ? BeltPathRoutingPolicy.ValidateRequest(request, item.prefabDesc.isBelt) : null;
        if (routingError is not null)
            return InvalidPlan(routingError + ": " + (request.BeltPathMode == BeltPathModes.NativeElevatedGrid
                ? BeltElevationPolicy.Recovery : BeltPathRoutingPolicy.Recovery));

        var player = GameMain.mainPlayer;
        var factory = GameMain.localPlanet?.factory;
        if (player?.package is null || factory is null || player.controller?.actionBuild is null)
        {
            return NotReadyPlan("The local player build system or factory is not ready.");
        }

        if (!GameMain.history.ItemUnlocked(item.ID))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                "The requested building item is not unlocked in the current ordinary world.",
                false,
                "Complete the normal prerequisite technology before preparing construction."));
        }

        var playerResult = _reader.GetPlayerStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(playerResult.Error!);
        }

        if (!string.Equals(request.ExpectedPlayerStateHash, playerResult.Value.StateHash, StringComparison.Ordinal))
        {
            return StalePlan("Player inventory, construction queue, or position changed after inspection.");
        }
        if (request.BeltRoutePreview is not null)
        {
            try { return PrepareBoundedBeltRoutePreviewOnMainThread(request,playerResult.Value); }
            catch (InvalidOperationException error) { return NotReadyPlan(error.Message); }
        }

        BuildPreparation preparation;
        if (item.prefabDesc.isBelt)
        {
            preparation = TryPrepareBeltBuild(factory, player, item, request);
        }
        else if (item.prefabDesc.isInserter)
        {
            preparation = TryPrepareInserterBuild(factory, player, item, request);
        }
        else if (item.prefabDesc.veinMiner || item.prefabDesc.oilMiner
                 || item.prefabDesc.minerType == EMinerType.Vein
                 || item.prefabDesc.minerType == EMinerType.Oil)
        {
            preparation = TryPrepareResourceBuild(factory, player, item, request, requestedSessionId!);
        }
        else
        {
            preparation = TryPrepareCoreBuild(factory, player, item, request);
        }

        if (!preparation.Success)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                preparation.ErrorCode,
                preparation.Rejection,
                true,
                preparation.Recovery ?? "Inspect the bound state, move into construction range or choose another candidate, then prepare again."));
        }

        if (preparation.Steps.Count == 0)
        {
            return InvalidPlan("DSP did not produce any validated construction step.");
        }

        if (player.package.GetItemCount(item.ID) < preparation.Steps.Count)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                $"The validated construction requires {preparation.Steps.Count} {item.name}, but the player owns fewer.",
                true,
                "Handcraft the exact missing building count through normal gameplay and prepare again."));
        }

        // A qualification request must never fall through to AddPreparedPlan.
        // Its NEW span has passed the same native path validation as ordinary builds.
        if (request.BeltEndpointPreview is not null)
            return PrepareBeltEndpointPreviewOnMainThread(request, preparation, playerResult.Value);

        var playerActionHash = CanonicalStateHash.PlayerAction(playerResult.Value);
        var buildFingerprint = BuildPlanFingerprint(preparation);
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.Build,
            _sessions.SessionId,
            request.PlanetId,
            playerActionHash,
            item.ID,
            preparation.Kind,
            preparation.ResourceStateHash,
            preparation.SourceEndpointHash,
            preparation.DestinationEndpointHash,
            buildFingerprint);
        var payload = NormalActionPlanPayload.StructuredBuild(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            playerActionHash,
            item.ID,
            preparation);
        var prepared = AddPreparedPlan(
            payload,
            common.Session!,
            Math.Max(3600, preparation.Steps.Count * 900L),
            $"DSP creates {preparation.Steps.Count} ordinary {preparation.Kind} prebuild(s), consumes exactly that many owned items, and construction drones replace every prebuild with a reread matching entity.");
        if (prepared.Success && prepared.Value is not null)
        {
            prepared.Value.BuildKind = preparation.Kind;
            if (preparation.Kind == NormalBuildKinds.Belt)
            {
                var anchor = preparation.Steps[0].SourceBeltAnchor;
                var destinationAnchor = preparation.Steps[preparation.Steps.Count - 1].DestinationBeltAnchor;
                prepared.Value.PlannedBeltPath = new BeltPathPlanSnapshot
                {
                    NativeValidationMode = "full_path_stage1",
                    SourceBindingMode = anchor is not null ? "non_removing_belt_cover"
                        : preparation.SourceObjectId > 0 ? "native_device_port" : "none",
                    ReusedSourceObjectId = anchor?.EntityId,
                    SourcePreservationMode = anchor is null ? null : BeltSourceRotationPolicy.PreservationMode,
                    DestinationBindingMode = destinationAnchor is not null ? "non_removing_belt_cover"
                        : preparation.DestinationObjectId > 0 ? "native_device_port" : "none",
                    ReusedDestinationObjectId = destinationAnchor?.EntityId,
                    DestinationPreservationMode = destinationAnchor is null ? null : BeltDestinationReusePolicy.PreservationMode,
                    UnpoweredSourceMinerObjectId = destinationAnchor?.SourcePath?.UnpoweredMinerFeed?.ObjectId,
                    UnpoweredSourceMinerStateHash = destinationAnchor?.SourcePath?.UnpoweredMinerFeed?.BindingHash,
                    NewObjectCount = preparation.Steps.Count,
                    RoutingMode = preparation.Steps[0].BeltPathMode,
                    StartAltitudeLevel = request.BeltPathMode == BeltPathModes.NativeElevatedGrid ? request.BeltStartAltitudeLevel : null,
                    EndAltitudeLevel = request.BeltPathMode == BeltPathModes.NativeElevatedGrid ? request.BeltEndAltitudeLevel : null,
                };
            }
            prepared.Value.PlannedSorterFilterItemId = preparation.Kind == NormalBuildKinds.Inserter
                ? preparation.Steps[0].FilterItemId : (int?)null;
            if (preparation.Kind == NormalBuildKinds.Inserter)
            {
                var step = preparation.Steps[0];
                prepared.Value.PlannedInserterAttachment = new InserterAttachmentPlanSnapshot
                {
                    Mode = step.AttachmentDestinationGeometryHash is not null ? "native_two_belt_segments"
                        : step.AttachmentGeometryHash is null ? "exact_slots" : "native_single_belt_segment",
                    SourceSlot = step.InputFromSlot, DestinationSlot = step.OutputToSlot,
                    InputOffset = step.InputOffset, OutputOffset = step.OutputOffset,
                    SourcePosition = Snapshot(step.Position), DestinationPosition = Snapshot(step.Position2),
                };
            }
            prepared.Value.SourceObjectId = preparation.SourceObjectId > 0 ? preparation.SourceObjectId : (int?)null;
            prepared.Value.DestinationObjectId = preparation.DestinationObjectId > 0
                ? preparation.DestinationObjectId
                : (int?)null;
            prepared.Value.PlannedPosition = Snapshot(preparation.Steps[0].Position);
            prepared.Value.PlannedYaw = preparation.Steps[0].Yaw;
            prepared.Value.PlannedPath = preparation.Steps.Select(step => Snapshot(step.Position)).ToList();
            prepared.Value.PlannedResourceNodeIds = preparation.ResourceNodeIds.ToList();
            prepared.Value.ItemBudget.Add(new ActionItemBudget
            {
                ItemId = item.ID,
                Name = item.name ?? string.Empty,
                Count = preparation.Steps.Count,
                Direction = "construction-consumption",
            });
        }

        return prepared;
    }

    private BuildPreparation TryPrepareCoreBuild(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        PrepareBuildRequest request)
    {
        if (request.ResourceNodeId.HasValue || request.SourceObjectId.HasValue
            || request.DestinationObjectId.HasValue || request.PathEnd is not null)
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.InvalidRequest,
                "A core building request cannot bind resource, connection, or belt-path targets.");
        }

        var candidates = new List<BuildStepPlan>();
        if (request.PreferredPosition is not null)
        {
            var requested = ToVector(request.PreferredPosition);
            if (!IsFinite(requested.x) || !IsFinite(requested.y) || !IsFinite(requested.z)
                || requested.sqrMagnitude < 1f)
            {
                return BuildPreparation.Failed(BridgeErrorCodes.InvalidRequest, "Preferred building coordinates are invalid.");
            }

            var position = factory.planet.aux.Snap(requested, onTerrain: true);
            var yaw = Mathf.Repeat(request.PreferredYaw ?? 0f, 360f);
            candidates.Add(BuildStepPlan.Core(item.ID, position, Maths.SphericalRotation(position, yaw), yaw));
        }
        else
        {
            var distances = new[]
            {
                request.PreferredDistance,
                Math.Min(30f, request.PreferredDistance + 5f),
                Math.Max(5f, request.PreferredDistance - 4f),
            }.Distinct().ToArray();
            var lateralOffsets = new[] { 0f, 5f, -5f, 10f, -10f, 15f, -15f };
            for (var yaw = 0f; yaw < 360f; yaw += 30f)
            {
                var basis = Maths.SphericalRotation(player.position, yaw);
                foreach (var distance in distances)
                {
                    foreach (var lateral in lateralOffsets)
                    {
                        var position = factory.planet.aux.Snap(
                            player.position + basis * Vector3.forward * distance + basis * Vector3.right * lateral,
                            onTerrain: true);
                        candidates.Add(BuildStepPlan.Core(item.ID, position, Maths.SphericalRotation(position, yaw), yaw));
                    }
                }
            }
        }

        var last = "No core-building candidate was accepted.";
        foreach (var candidate in candidates)
        {
            if (TryValidateClickBuild(factory, player, item, candidate, 0, out var accepted, out last))
            {
                return BuildPreparation.Succeeded(NormalBuildKinds.Core, new[] { accepted });
            }
        }

        return BuildPreparation.Failed(BridgeErrorCodes.BuildLocationInvalid, last);
    }

    private BuildPreparation TryPrepareResourceBuild(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        PrepareBuildRequest request,
        string requestedSessionId)
    {
        if (!request.ResourceNodeId.HasValue || request.ResourceNodeId.Value <= 0
            || string.IsNullOrWhiteSpace(request.ExpectedResourceStateHash))
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.InvalidRequest,
                "A vein miner or oil extractor requires one inspected vein node and its exact state hash.");
        }

        if (request.SourceObjectId.HasValue || request.DestinationObjectId.HasValue || request.PathEnd is not null)
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.InvalidRequest,
                "A resource-building request cannot also be a connection or belt-path request.");
        }

        var resourceResult = _reader.InspectResourceNodeOnMainThread(
            requestedSessionId,
            new InspectResourceNodeRequest
            {
                PlanetId = request.PlanetId,
                Kind = ResourceNodeKinds.Vein,
                NodeId = request.ResourceNodeId.Value,
            });
        if (!resourceResult.Success || resourceResult.Value is null)
        {
            return BuildPreparation.Failed(
                resourceResult.Error?.Code ?? BridgeErrorCodes.InvalidResourceTarget,
                resourceResult.Error?.Message ?? "The bound vein no longer exists.");
        }

        var resource = resourceResult.Value;
        if (!string.Equals(resource.StateHash, request.ExpectedResourceStateHash, StringComparison.Ordinal))
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.StaleState,
                "The bound vein amount, identity, group, or miner count changed after inspection.");
        }

        var isOil = string.Equals(resource.ResourceType, EVeinType.Oil.ToString(), StringComparison.OrdinalIgnoreCase);
        if ((item.prefabDesc.oilMiner || item.prefabDesc.minerType == EMinerType.Oil) != isOil)
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.InvalidResourceTarget,
                isOil
                    ? "The selected building is not an oil extractor."
                    : "An oil extractor cannot target a solid mineral vein.");
        }

        var target = ToVector(resource.Position);
        var candidates = new List<BuildStepPlan>();
        if (request.PreferredPosition is not null)
        {
            var requested = factory.planet.aux.Snap(ToVector(request.PreferredPosition), onTerrain: true);
            var yaw = Mathf.Repeat(request.PreferredYaw ?? 0f, 360f);
            candidates.Add(BuildStepPlan.Core(item.ID, requested, Maths.SphericalRotation(requested, yaw), yaw));
        }
        else if (isOil)
        {
            var position = factory.planet.aux.Snap(target, onTerrain: true);
            for (var yaw = 0f; yaw < 360f; yaw += 30f)
            {
                candidates.Add(BuildStepPlan.Core(item.ID, position, Maths.SphericalRotation(position, yaw), yaw));
            }
        }
        else
        {
            var offsets = new[] { 3.5f, 4.5f, 5.5f, 6.5f, 7.25f };
            for (var yaw = 0f; yaw < 360f; yaw += 15f)
            {
                var basis = Maths.SphericalRotation(target, yaw);
                foreach (var offset in offsets)
                {
                    var position = factory.planet.aux.Snap(
                        target + basis * Vector3.forward * offset,
                        onTerrain: true);
                    candidates.Add(BuildStepPlan.Core(item.ID, position, Maths.SphericalRotation(position, yaw), yaw));
                }
            }
        }

        var last = "No resource-building candidate was accepted.";
        var acceptedCandidates = new List<BuildStepPlan>();
        foreach (var candidate in candidates)
        {
            if (TryValidateClickBuild(
                    factory,
                    player,
                    item,
                    candidate,
                    resource.NodeId,
                    out var accepted,
                    out last))
            {
                acceptedCandidates.Add(accepted);
            }
        }

        if (acceptedCandidates.Count > 0)
        {
            var scores = acceptedCandidates.Select((candidate, index) => new ResourceCoverageCandidateScore
            {
                Index = index,
                CoveredNodeCount = candidate.Parameters.Where(nodeId => nodeId > 0).Distinct().Count(),
                DistanceToBoundNode = Vector3.Distance(candidate.Position, target),
                Yaw = candidate.Yaw,
            }).ToArray();
            var selectedIndex = ResourceCoverageSelection.SelectBestIndex(scores);
            var selected = acceptedCandidates[selectedIndex];
            var result = BuildPreparation.Succeeded(NormalBuildKinds.Resource, new[] { selected });
            result.ResourceNodeId = resource.NodeId;
            result.ResourceStateHash = resource.StateHash;
            result.ResourceNodeIds.AddRange(selected.Parameters.Where(nodeId => nodeId > 0).Distinct().OrderBy(nodeId => nodeId));
            return result;
        }

        return BuildPreparation.Failed(BridgeErrorCodes.BuildLocationInvalid, last);
    }

    private BuildPreparation TryPrepareInserterBuild(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        PrepareBuildRequest request)
    {
        if (!request.SourceObjectId.HasValue || request.SourceObjectId.Value <= 0
            || !request.DestinationObjectId.HasValue || request.DestinationObjectId.Value <= 0
            || string.IsNullOrWhiteSpace(request.ExpectedSourceStateHash)
            || string.IsNullOrWhiteSpace(request.ExpectedDestinationStateHash))
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.InvalidRequest,
                "An inserter requires exact inspected source and destination entities plus both state hashes.");
        }

        if (request.SourceObjectId == request.DestinationObjectId)
        {
            return BuildPreparation.Failed(BridgeErrorCodes.BuildConnectionInvalid, "An inserter cannot connect an entity to itself.");
        }

        FactoryEntitySnapshot? source;
        FactoryEntitySnapshot? destination;
        string? sourceError = null;
        string? destinationError = null;
        if (!TryReadBuildEndpoint(request, request.SourceObjectId.Value, request.ExpectedSourceStateHash!, out source, out sourceError)
            || !TryReadBuildEndpoint(request, request.DestinationObjectId.Value, request.ExpectedDestinationStateHash!, out destination, out destinationError))
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.StaleState,
                sourceError ?? destinationError ?? "A bound inserter endpoint changed after inspection.");
        }

        var sourcePoints = GetInserterEndpointPoints(factory, source!);
        var destinationPoints = GetInserterEndpointPoints(factory, destination!);
        if (sourcePoints.Count == 0 || destinationPoints.Count == 0)
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.BuildConnectionInvalid,
                "One bound endpoint exposes no current-version inserter slot or belt attachment pose.");
        }

        var last = "DSP rejected every bounded inserter endpoint pair.";
        var exactRejections = new ExactInserterRejectionReport();
        foreach (var sourcePoint in sourcePoints)
        {
            foreach (var destinationPoint in destinationPoints)
            {
                var step = BuildStepPlan.Inserter(
                    item.ID,
                    sourcePoint.Pose,
                    destinationPoint.Pose,
                    sourcePoint.ObjectId,
                    sourcePoint.Slot,
                    destinationPoint.ObjectId,
                    destinationPoint.Slot);
                step.FilterItemId = request.InitialSorterFilterItemId;
                if (TryValidateInserterBuild(factory, player, item, step, out var accepted, out last, out var nativeCheckPerformed))
                {
                    var result = BuildPreparation.Succeeded(NormalBuildKinds.Inserter, new[] { accepted });
                    result.SourceObjectId = source!.ObjectId;
                    result.DestinationObjectId = destination!.ObjectId;
                    result.SourceEndpointHash = BuildEndpointHash(source);
                    result.DestinationEndpointHash = BuildEndpointHash(destination);
                    return result;
                }
                exactRejections.Record(last, nativeCheckPerformed);
            }
        }

        if (TryPrepareBeltAttachment(factory, player, item, sourcePoints, destinationPoints,
                request.InitialSorterFilterItemId, out var offsetStep, out var offsetRejection))
        {
            var result = BuildPreparation.Succeeded(NormalBuildKinds.Inserter, new[] { offsetStep! });
            result.SourceObjectId = source!.ObjectId;
            result.DestinationObjectId = destination!.ObjectId;
            result.SourceEndpointHash = BuildEndpointHash(source);
            result.DestinationEndpointHash = BuildEndpointHash(destination);
            return result;
        }
        return BuildPreparation.Failed(BridgeErrorCodes.BuildConnectionInvalid,
            "Exact-slot candidates rejected; " + exactRejections.DescribeFailure() + " " + offsetRejection,
            InserterAttachmentSearchReport.Recovery);
    }

    private BuildPreparation TryPrepareBeltBuild(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        PrepareBuildRequest request)
    {
        if (request.BeltPathMode == BeltPathModes.NativeElevatedGrid)
            return TryPrepareElevatedBeltBuild(factory, player, item, request);
        FactoryEntitySnapshot? source = null;
        FactoryEntitySnapshot? destination = null;
        if (request.SourceObjectId.HasValue)
        {
            string? sourceError = null;
            if (request.SourceObjectId.Value <= 0 || string.IsNullOrWhiteSpace(request.ExpectedSourceStateHash)
                || !TryReadBuildEndpoint(request, request.SourceObjectId.Value, request.ExpectedSourceStateHash!, out source, out sourceError))
            {
                return BuildPreparation.Failed(
                    BridgeErrorCodes.StaleState,
                    sourceError ?? "The bound belt source is invalid or changed after inspection.");
            }
        }

        if (request.DestinationObjectId.HasValue)
        {
            string? destinationError = null;
            if (request.DestinationObjectId.Value <= 0 || string.IsNullOrWhiteSpace(request.ExpectedDestinationStateHash)
                || !TryReadBuildEndpoint(request, request.DestinationObjectId.Value, request.ExpectedDestinationStateHash!, out destination, out destinationError))
            {
                return BuildPreparation.Failed(
                    BridgeErrorCodes.StaleState,
                    destinationError ?? "The bound belt destination is invalid or changed after inspection.");
            }
        }

        var sourcePorts = source is null
            ? new List<EndpointPoint> { new EndpointPoint(0, -1, ResolveFreeBeltStart(factory, player, request)) }
            : GetFreePortPoints(factory, source, requireOutput: true);
        var destinationPorts = destination is null
            ? new List<EndpointPoint>()
            : GetFreePortPoints(factory, destination, requireOutput: false);
        if (sourcePorts.Count == 0 || (destination is not null && destinationPorts.Count == 0))
        {
            return BuildPreparation.Failed(
                BridgeErrorCodes.BuildConnectionInvalid,
                "The bound source or destination exposes no free current-version belt port.", BeltSourceReusePolicy.Recovery);
        }

        var last = "DSP rejected every bounded belt path candidate.";
        BridgeError? lastError = null;
        foreach (var sourcePort in sourcePorts)
        {
            var endCandidates = new List<EndpointPoint>();
            if (destination is not null)
            {
                endCandidates.AddRange(destinationPorts);
            }
            else if (request.PathEnd is not null)
            {
                var end = factory.planet.aux.Snap(ToVector(request.PathEnd), onTerrain: true);
                endCandidates.Add(new EndpointPoint(0, -1, new Pose(end, Maths.SphericalRotation(end, 0f))));
            }
            else
            {
                var baseForward = ProjectTangent(sourcePort.Pose.rotation * Vector3.forward, sourcePort.Pose.position);
                var baseRight = ProjectTangent(sourcePort.Pose.rotation * Vector3.right, sourcePort.Pose.position);
                var directions = new[] { baseForward, -baseForward, baseRight, -baseRight };
                foreach (var direction in directions.Where(direction => direction.sqrMagnitude > 0.5f))
                {
                    var end = factory.planet.aux.Snap(
                        sourcePort.Pose.position + direction.normalized * request.PathLength,
                        onTerrain: true);
                    endCandidates.Add(new EndpointPoint(0, -1, new Pose(end, Maths.SphericalRotation(end, 0f))));
                }
            }

            foreach (var endPort in endCandidates)
            {
                lastError = null;
                if (!TryCreateBeltSteps(
                        factory,
                        item,
                        sourcePort,
                        endPort,
                        request.BeltPathMode,
                        out var candidate,
                        out last))
                {
                    continue;
                }

                if (TryValidateBeltBuild(factory, player, item, candidate, out var accepted, out last, out lastError))
                {
                    var result = BuildPreparation.Succeeded(NormalBuildKinds.Belt, accepted);
                    if (source is not null)
                    {
                        result.SourceObjectId = source.ObjectId;
                        result.SourceEndpointHash = BuildEndpointHash(source);
                    }

                    if (destination is not null)
                    {
                        result.DestinationObjectId = destination.ObjectId;
                        result.DestinationEndpointHash = BuildEndpointHash(destination);
                    }

                    return result;
                }
            }
        }

        return BuildPreparation.Failed(lastError?.Code ?? BridgeErrorCodes.BuildLocationInvalid,
            last, lastError?.Recovery ?? BeltSourceReusePolicy.Recovery);
    }

    private bool TryReadBuildEndpoint(
        PrepareBuildRequest request,
        int objectId,
        string expectedStateHash,
        out FactoryEntitySnapshot? snapshot,
        out string? error)
    {
        var result = _reader.InspectFactoryEntityOnMainThread(
            _sessions.SessionId,
            new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = objectId });
        snapshot = result.Value;
        error = result.Error?.Message;
        if (!result.Success || snapshot is null || snapshot.ObjectKind != FactoryObjectKinds.Entity)
        {
            return false;
        }

        if (!string.Equals(snapshot.EndpointStateHash, expectedStateHash, StringComparison.Ordinal)
            && !string.Equals(snapshot.StateHash, expectedStateHash, StringComparison.Ordinal))
        {
            error = $"Bound endpoint {objectId} identity, pose, or connections changed after inspection.";
            return false;
        }

        return true;
    }

    private static Pose ResolveFreeBeltStart(PlanetFactory factory, Player player, PrepareBuildRequest request)
    {
        if (request.PreferredPosition is not null)
        {
            var requested = factory.planet.aux.Snap(ToVector(request.PreferredPosition), onTerrain: true);
            return new Pose(requested, Maths.SphericalRotation(requested, request.PreferredYaw ?? 0f));
        }

        var yaw = request.PreferredYaw ?? 0f;
        var basis = Maths.SphericalRotation(player.position, yaw);
        var position = factory.planet.aux.Snap(
            player.position + basis * Vector3.forward * request.PreferredDistance,
            onTerrain: true);
        return new Pose(position, Maths.SphericalRotation(position, yaw));
    }

    private static bool TryCreateBeltSteps(
        PlanetFactory factory,
        ItemProto item,
        EndpointPoint source,
        EndpointPoint destination,
        string routingMode,
        out List<BuildStepPlan> steps,
        out string rejection,
        Action<float>? nativeSlopeReadback = null,
        Action<IReadOnlyList<Vector3>>? nativePointsReadback = null)
    {
        steps = new List<BuildStepPlan>();
        rejection = string.Empty;
        BeltSourceState? sourceAnchor = null;
        BeltDestinationState? destinationAnchor = null;
        var geodesic = routingMode == BeltPathModes.NativeGeodesic;
        var elevated = routingMode == BeltPathModes.NativeElevatedGrid;
        if (elevated && (item.ID != 2001 || source.ObjectId != 0 || destination.ObjectId != 0
            || !BeltElevationPolicy.ValidEndpoints(Snapshot(source.Pose.position), Snapshot(destination.Pose.position),
                factory.planet.radius + .2f)))
        {
            rejection = "belt_elevation_span_unsupported: requires one explicit free-to-free2001 span with verified native levels, 1.5–30m apart.";
            return false;
        }
        var sourceIsBelt = source.ObjectId > 0 && factory.entityPool[source.ObjectId].beltId > 0;
        var destinationIsBelt = destination.ObjectId > 0 && factory.entityPool[destination.ObjectId].beltId > 0;
        var dualCover = sourceIsBelt && destinationIsBelt && item.ID == 2001 && source.ObjectId != destination.ObjectId;
        if (geodesic && (((source.ObjectId != 0 || destination.ObjectId != 0) && !dualCover)
            || !BeltPathRoutingPolicy.ValidGroundEndpoints(Snapshot(source.Pose.position),
                Snapshot(destination.Pose.position), factory.planet.realRadius + .2f)))
        {
            rejection = "belt_geodesic_ground_span_unsupported: requires explicit free ground endpoints or proven dual2001 empty-path covers, 1.5–30m apart, with no raised/tilted route.";
            return false;
        }
        if (sourceIsBelt)
        {
            if (!TryCaptureBeltSource(factory, source.ObjectId, out sourceAnchor, out rejection)
                || !BeltSourceReusePolicy.Supports(source.ObjectId, sourceAnchor!.ItemId, item.ID, source.Slot,
                    destinationIsBelt ? 0 : destination.ObjectId, sourceAnchor.Tilt, false, sourceAnchor.Connections[0].OtherObjectId,
                    sourceAnchor.Connections.Skip(1).Take(3).Count(c => c.OtherObjectId != 0)))
            {
                rejection = "belt_source_cover_unsupported: requires one same-grade flat built belt with a free output, an open bounded path and a free destination. " + rejection;
                return false;
            }
        }
        if (destinationIsBelt && ((source.ObjectId != 0 && !sourceIsBelt)
            || !TryCaptureBeltDestination(factory, item, destination, sourceAnchor, out destinationAnchor, out rejection)))
        {
            rejection = "belt_destination_cover_unsupported: requires 2001, an empty unfed open head and a free source or distinct empty open tail. " + rejection;
            return false;
        }
        var points = new Vector3[BeltBuildOccupancyPolicy.MaximumPathPoints];
        var maxSlope = 0f;
        var count = factory.planet.aux.SnapLineNonAlloc(
            source.Pose.position,
            destination.Pose.position,
            1,
            geodesic: geodesic,
            begin_flat: source.ObjectId == 0 || sourceIsBelt,
            points,
            forceVertical: false,
            ref maxSlope,
            useOldPath: sourceIsBelt && destinationIsBelt);
        nativeSlopeReadback?.Invoke(maxSlope);
        if (count < 2 || count >= points.Length)
        {
            rejection = "DSP's terrain grid did not return a complete, unsaturated bounded belt path.";
            return false;
        }
        // Geometry observation copies the full native result before endpoint
        // canonicalisation. Ordinary action callers do not request this copy.
        nativePointsReadback?.Invoke(points.Take(count).ToArray());

        if (geodesic && !BeltPathRoutingPolicy.CompleteGroundPath(points.Take(count).Select(Snapshot).ToArray(),
            points.Length, Snapshot(source.Pose.position), Snapshot(destination.Pose.position), factory.planet.realRadius + .2f))
        {
            rejection = "belt_geodesic_path_incomplete: native points are saturated, incomplete or not entirely ground-level; no endpoint is replaced to mask truncation.";
            return false;
        }

        if (elevated && (!BeltElevationPolicy.ValidNativeSlope(maxSlope)
            || !BeltElevationPolicy.CompleteNativePath(points.Take(count).Select(Snapshot).ToArray(), points.Length,
                Snapshot(source.Pose.position), Snapshot(destination.Pose.position), factory.planet.radius + .2f)))
        {
            rejection = "belt_elevation_native_path_incomplete: requires unsaturated complete native points, gentle slope, flat ends and no short-segment cleanup; no endpoint may hide truncation.";
            return false;
        }

        if (destinationAnchor is not null && (count >= points.Length - BeltPathRoutingPolicy.NativeReservedPoints
            || Vector3.Distance(points[0], source.Pose.position) > .01f
            || Vector3.Distance(points[count - 1], destination.Pose.position) > .01f))
        {
            rejection = "belt_join_native_path_incomplete: no endpoint substitution may hide a truncated or mismatched native route.";
            return false;
        }

        points[0] = source.Pose.position;
        points[count - 1] = destination.Pose.position;
        if (elevated && !BeltElevationPolicy.CompleteNativePath(points.Take(count).Select(Snapshot).ToArray(), points.Length,
            Snapshot(source.Pose.position), Snapshot(destination.Pose.position), factory.planet.radius + .2f))
        {
            rejection = "belt_elevation_native_endpoint_adjustment_unsupported";
            return false;
        }
        if (destinationAnchor is not null)
        {
            if (!BeltDestinationReusePolicy.TrySeparateNewPoints(points.Take(count).Select(Snapshot).ToArray(),
                sourceAnchor is null ? null : Snapshot(sourceAnchor.Position), Snapshot(destinationAnchor.Position), points.Length,
                out var newPoints, out rejection)) return false;
            points = newPoints.Select(ToVector).ToArray();
            count = points.Length;
        }
        else if (sourceAnchor is not null)
        {
            // Only the exact zero-cost source is partitioned out of the NEW-object
            // plan. It is retained as a native non-removing cover in BOTH validation
            // and creation. No native point is dropped to evade a collision/angle.
            if (!BeltSourceReusePolicy.TrySeparateNewPoints(points.Take(count).Select(Snapshot).ToArray(),
                    Snapshot(sourceAnchor.Position), out var newPoints))
            {
                rejection = "belt_source_path_unsupported: retain an exact source cover plus at least two complete, flat NEW points.";
                return false;
            }
            points = newPoints.Select(ToVector).ToArray();
            count = points.Length;
        }
        for (var index = 0; index < count; index++)
        {
            var step = BuildStepPlan.Belt(item.ID, points[index]);
            step.BeltPathMode = routingMode;
            step.InputStepIndex = index > 0 ? index - 1 : -1;
            step.OutputStepIndex = index + 1 < count ? index + 1 : -1;
            if (index == 0 && source.ObjectId > 0)
            {
                step.SourceBeltAnchor = sourceAnchor;
                step.InputObjectId = source.ObjectId;
                step.InputFromSlot = source.Slot;
                step.InputToSlot = 1;
            }

            if (index == count - 1 && destination.ObjectId > 0)
            {
                step.DestinationBeltAnchor = destinationAnchor;
                step.OutputObjectId = destination.ObjectId;
                step.OutputFromSlot = 0;
                step.OutputToSlot = destination.Slot;
            }

            steps.Add(step);
        }

        return true;
    }

    private static bool TryValidateClickBuild(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        BuildStepPlan candidate,
        int requiredResourceNodeId,
        out BuildStepPlan accepted,
        out string rejection,
        FoundryNativePreviewEvidence? evidence = null)
    {
        accepted = candidate;
        rejection = string.Empty;
        if (!BuildUiIsIdle(player))
        {
            rejection = "The player's normal build UI owns preview state.";
            return false;
        }

        // BuildTool_Click normally relies on the live UI preview/collider
        // lifecycle.  An isolated validator can otherwise miss an already
        // built collider even though its remaining DSP checks pass.  Bind the
        // exact current-version build colliders to both poses and perform the
        // same conservative occupied-volume check before calling the native
        // validator.  This also covers prebuilds created by ordinary gameplay.
        if (OverlapsExistingFactoryObject(factory, item.prefabDesc, candidate.Position, candidate.Rotation,
                out var occupiedObjectId))
        {
            rejection = $"The exact build-collider volume overlaps existing factory object {occupiedObjectId}.";
            if (evidence is not null) evidence.OccupiedObjectId = occupiedObjectId;
            return false;
        }

        var tool = new SpherewrightClickBuildTool();
        tool._Init(GameMain.data!);
        tool.SetFactoryReferences();
        try
        {
            if (!ReferenceEquals(tool.factory, factory))
            {
                rejection = "The isolated click-build validator is not bound to the local factory.";
                return false;
            }

            tool.handItem = item;
            tool.handPrefabDesc = item.prefabDesc;
            tool.yaw = candidate.Yaw;
            tool.SnapshotPlayerInventory();
            var preview = CreatePreview(candidate, item);
            preview.parameters = null;
            preview.paramCount = 0;
            tool.buildPreviews.Add(preview);
            var valid = tool.CheckBuildConditions();
            if (evidence is not null)
            {
                evidence.NativeCheckPerformed = true;
                evidence.Condition = preview.condition.ToString();
            }
            if (!valid || preview.condition != EBuildCondition.Ok || preview.coverObjId != 0)
            {
                rejection = $"DSP click-build validation returned {preview.condition}.";
                return false;
            }

            if (requiredResourceNodeId > 0
                && (preview.parameters is null
                    || !preview.parameters.Take(preview.paramCount).Contains(requiredResourceNodeId)))
            {
                rejection = "DSP accepted the position but did not bind the requested exact resource node.";
                return false;
            }

            accepted = BuildStepPlan.FromPreview(candidate, preview);
            return true;
        }
        finally
        {
            tool.buildPreviews.Clear();
            tool.ReleaseSnapshot();
            tool._Free();
        }
    }

    private static bool OverlapsExistingFactoryObject(
        PlanetFactory factory,
        PrefabDesc candidateDescription,
        Vector3 candidatePosition,
        Quaternion candidateRotation,
        out int occupiedObjectId)
    {
        occupiedObjectId = 0;
        var candidateColliders = CreateWorldBuildColliders(
            candidateDescription,
            candidatePosition,
            candidateRotation);
        if (candidateColliders.Count == 0)
        {
            return false;
        }

        var entityLimit = Math.Min(factory.entityCursor, factory.entityPool.Length);
        for (var entityId = 1; entityId < entityLimit; entityId++)
        {
            ref var entity = ref factory.entityPool[entityId];
            if (entity.id != entityId || entity.protoId <= 0)
            {
                continue;
            }

            var description = LDB.items.Select(entity.protoId)?.prefabDesc;
            if (description is null)
            {
                continue;
            }

            if (BuildColliderSetsOverlap(
                    candidateColliders,
                    CreateWorldBuildColliders(description, entity.pos, entity.rot)))
            {
                occupiedObjectId = entityId;
                return true;
            }
        }

        var prebuildLimit = Math.Min(factory.prebuildCursor, factory.prebuildPool.Length);
        for (var prebuildId = 1; prebuildId < prebuildLimit; prebuildId++)
        {
            ref var prebuild = ref factory.prebuildPool[prebuildId];
            if (prebuild.id != prebuildId || prebuild.isDestroyed || prebuild.protoId <= 0)
            {
                continue;
            }

            var description = LDB.items.Select(prebuild.protoId)?.prefabDesc;
            if (description is null)
            {
                continue;
            }

            if (BuildColliderSetsOverlap(
                    candidateColliders,
                    CreateWorldBuildColliders(description, prebuild.pos, prebuild.rot)))
            {
                occupiedObjectId = -prebuildId;
                return true;
            }
        }

        return false;
    }

    private static List<WorldBuildCollider> CreateWorldBuildColliders(
        PrefabDesc description,
        Vector3 position,
        Quaternion rotation)
    {
        var result = new List<WorldBuildCollider>();
        var colliders = description.buildColliders;
        if (colliders is null || colliders.Length == 0)
        {
            if (!description.hasBuildCollider)
            {
                return result;
            }

            colliders = new[] { description.buildCollider };
        }

        foreach (var collider in colliders)
        {
            var localRotation = Quaternion.Dot(collider.q, collider.q) > 0.01f
                ? collider.q
                : Quaternion.identity;
            var worldRotation = rotation * localRotation;
            Vector3 extents;
            switch (collider.shape)
            {
                case EColliderShape.Box:
                    extents = new Vector3(
                        Math.Abs(collider.ext.x),
                        Math.Abs(collider.ext.y),
                        Math.Abs(collider.ext.z));
                    break;
                case EColliderShape.Sphere:
                    extents = Vector3.one * Math.Abs(collider.radius);
                    break;
                case EColliderShape.Capsule:
                    // A capsule is conservatively enclosed by an oriented box.
                    var capsuleRadius = Math.Abs(collider.radius);
                    extents = new Vector3(
                        Math.Abs(collider.ext.x) + capsuleRadius,
                        Math.Abs(collider.ext.y) + capsuleRadius,
                        Math.Abs(collider.ext.z) + capsuleRadius);
                    break;
                default:
                    continue;
            }

            if (extents.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            result.Add(new WorldBuildCollider(
                position + rotation * collider.pos,
                (worldRotation * Vector3.right).normalized,
                (worldRotation * Vector3.up).normalized,
                (worldRotation * Vector3.forward).normalized,
                extents + Vector3.one * 0.01f));
        }

        return result;
    }

    private static bool BuildColliderSetsOverlap(
        IReadOnlyList<WorldBuildCollider> left,
        IReadOnlyList<WorldBuildCollider> right)
    {
        for (var leftIndex = 0; leftIndex < left.Count; leftIndex++)
        {
            for (var rightIndex = 0; rightIndex < right.Count; rightIndex++)
            {
                if (OrientedBoxesOverlap(left[leftIndex], right[rightIndex]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool OrientedBoxesOverlap(WorldBuildCollider left, WorldBuildCollider right)
    {
        const float epsilon = 0.00001f;
        var leftAxes = new[] { left.AxisX, left.AxisY, left.AxisZ };
        var rightAxes = new[] { right.AxisX, right.AxisY, right.AxisZ };
        var leftExtents = new[] { left.Extents.x, left.Extents.y, left.Extents.z };
        var rightExtents = new[] { right.Extents.x, right.Extents.y, right.Extents.z };
        var rotation = new float[3, 3];
        var absoluteRotation = new float[3, 3];
        for (var leftAxis = 0; leftAxis < 3; leftAxis++)
        {
            for (var rightAxis = 0; rightAxis < 3; rightAxis++)
            {
                rotation[leftAxis, rightAxis] = Vector3.Dot(leftAxes[leftAxis], rightAxes[rightAxis]);
                absoluteRotation[leftAxis, rightAxis] = Math.Abs(rotation[leftAxis, rightAxis]) + epsilon;
            }
        }

        var delta = right.Center - left.Center;
        var translation = new[]
        {
            Vector3.Dot(delta, leftAxes[0]),
            Vector3.Dot(delta, leftAxes[1]),
            Vector3.Dot(delta, leftAxes[2]),
        };

        for (var axis = 0; axis < 3; axis++)
        {
            var rightRadius = rightExtents[0] * absoluteRotation[axis, 0]
                + rightExtents[1] * absoluteRotation[axis, 1]
                + rightExtents[2] * absoluteRotation[axis, 2];
            if (Math.Abs(translation[axis]) > leftExtents[axis] + rightRadius)
            {
                return false;
            }
        }

        for (var axis = 0; axis < 3; axis++)
        {
            var leftRadius = leftExtents[0] * absoluteRotation[0, axis]
                + leftExtents[1] * absoluteRotation[1, axis]
                + leftExtents[2] * absoluteRotation[2, axis];
            var projected = Math.Abs(translation[0] * rotation[0, axis]
                + translation[1] * rotation[1, axis]
                + translation[2] * rotation[2, axis]);
            if (projected > leftRadius + rightExtents[axis])
            {
                return false;
            }
        }

        for (var leftAxis = 0; leftAxis < 3; leftAxis++)
        {
            var leftNext = (leftAxis + 1) % 3;
            var leftLast = (leftAxis + 2) % 3;
            for (var rightAxis = 0; rightAxis < 3; rightAxis++)
            {
                var rightNext = (rightAxis + 1) % 3;
                var rightLast = (rightAxis + 2) % 3;
                var leftRadius = leftExtents[leftNext] * absoluteRotation[leftLast, rightAxis]
                    + leftExtents[leftLast] * absoluteRotation[leftNext, rightAxis];
                var rightRadius = rightExtents[rightNext] * absoluteRotation[leftAxis, rightLast]
                    + rightExtents[rightLast] * absoluteRotation[leftAxis, rightNext];
                var projected = Math.Abs(
                    translation[leftLast] * rotation[leftNext, rightAxis]
                    - translation[leftNext] * rotation[leftLast, rightAxis]);
                if (projected > leftRadius + rightRadius)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryValidateInserterBuild(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        BuildStepPlan candidate,
        out BuildStepPlan accepted,
        out string rejection,
        out bool nativeCheckPerformed)
    {
        accepted = candidate;
        rejection = string.Empty;
        nativeCheckPerformed = false;
        var filterError = ValidateInitialSorterFilter(item, candidate.FilterItemId);
        if (filterError is not null)
        {
            rejection = filterError.Message;
            return false;
        }
        // Vanilla DeterminePreviews rejects TooSkew BEFORE CheckBuildConditions.
        // Exact slots are tried first; the bounded segment fallback retains a private
        // geometry binding and still obeys the native angle/collision/material checks.
        if (candidate.InputObjectId <= 0 || candidate.InputObjectId >= factory.entityCursor
            || candidate.InputObjectId >= factory.entityPool.Length
            || candidate.OutputObjectId <= 0 || candidate.OutputObjectId >= factory.entityCursor
            || candidate.OutputObjectId >= factory.entityPool.Length
            || factory.entityPool[candidate.InputObjectId].id != candidate.InputObjectId
            || factory.entityPool[candidate.OutputObjectId].id != candidate.OutputObjectId)
        {
            rejection = "The bound inserter endpoints no longer identify completed local entities.";
            return false;
        }
        if (!RevalidateBeltAttachment(factory, candidate))
        {
            rejection = "The exact native belt attachment geometry or offset changed; fresh prepare is required.";
            return false;
        }
        if (!NativeInserterEndpointGeometry.AcceptsStraightPair(
                Snapshot(candidate.Position2 - candidate.Position), Snapshot(candidate.Rotation * Vector3.forward),
                Snapshot(candidate.Rotation2 * Vector3.back),
                factory.entityPool[candidate.InputObjectId].beltId > 0 || factory.entityPool[candidate.OutputObjectId].beltId > 0))
        {
            rejection = "Native inserter endpoint selection requires facing slots (TooSkew); the selected poses do not pass the native angle bound.";
            return false;
        }
        if (candidate.AttachmentGeometryHash is not null
            && !InserterBeltAttachmentPolicy.AcceptsFinalRotations(AttachmentRotation(candidate.Rotation), AttachmentRotation(candidate.Rotation2)))
        {
            rejection = "Native belt tilt correction leaves the inserter too skewed.";
            return false;
        }
        if (!BuildUiIsIdle(player))
        {
            rejection = "The player's normal build UI owns preview state.";
            return false;
        }

        var tool = new SpherewrightInserterBuildTool();
        tool._Init(GameMain.data!);
        tool.SetFactoryReferences();
        try
        {
            if (!ReferenceEquals(tool.factory, factory))
            {
                rejection = "The isolated inserter validator is not bound to the local factory.";
                return false;
            }

            tool.handItem = item;
            tool.handPrefabDesc = item.prefabDesc;
            tool.startObjectId = candidate.InputObjectId;
            tool.castObjectId = candidate.OutputObjectId;
            tool.SnapshotPlayerInventory();
            var preview = CreatePreview(candidate, item);
            tool.buildPreviews.Add(preview);
            nativeCheckPerformed = true;
            var valid = tool.CheckBuildConditions();
            if (!valid || preview.condition != EBuildCondition.Ok || preview.coverObjId != 0
                || preview.filterId != candidate.FilterItemId)
            {
                rejection = $"DSP inserter validation returned {preview.condition}.";
                return false;
            }

            accepted = BuildStepPlan.FromPreview(candidate, preview);
            return true;
        }
        finally
        {
            tool.buildPreviews.Clear();
            tool.ReleaseSnapshot();
            tool._Free();
        }
    }

    private static bool TryValidateBeltBuild(
        PlanetFactory factory,
        Player player,
        ItemProto item,
        IReadOnlyList<BuildStepPlan> candidates,
        out List<BuildStepPlan> accepted,
        out string rejection,
        out BridgeError? rejectionError,
        BeltRoutePreviewSnapshot? readOnlyRoute = null)
    {
        accepted = new List<BuildStepPlan>();
        rejection = string.Empty;
        rejectionError = null;
        if (candidates.Count < 2 || candidates.Any(step => step.BeltPathMode != candidates[0].BeltPathMode)
            || (candidates[0].BeltPathMode != BeltPathModes.NativeGrid && candidates[0].BeltPathMode != BeltPathModes.NativeGeodesic
                && candidates[0].BeltPathMode != BeltPathModes.NativeElevatedGrid
                && !(readOnlyRoute is not null && candidates[0].BeltPathMode == BeltPathModes.NativeRoutePreview)))
        {
            rejection = "belt_routing_mode_inconsistent";
            return false;
        }
        var geodesic = candidates[0].BeltPathMode == BeltPathModes.NativeGeodesic;
        var elevated = candidates[0].BeltPathMode == BeltPathModes.NativeElevatedGrid;
        if (elevated && !CompletePreparedElevatedBeltPath(candidates, factory.planet.radius + .2f))
        {
            rejection = "belt_elevation_prepared_path_invalid";
            return false;
        }
        if (geodesic && !CompletePreparedGroundBeltPath(candidates, factory.planet.realRadius + .2f))
        {
            rejection = "belt_geodesic_prepared_ground_path_invalid";
            return false;
        }
        if (!BuildUiIsIdle(player))
        {
            rejection = "The player's normal build UI owns preview state.";
            return false;
        }

        if (!TryValidateNewBeltOccupancy(factory, candidates, out rejection)) return false;

        var tool = new SpherewrightPathBuildTool();
        tool._Init(GameMain.data!);
        tool.SetFactoryReferences();
        var previews = CreateLinkedPreviews(candidates, item);
        try
        {
            if (!ReferenceEquals(tool.factory, factory))
            {
                rejection = "The isolated path-build validator is not bound to the local factory.";
                return false;
            }

            tool.handItem = item;
            tool.handPrefabDesc = item.prefabDesc;
            tool.startObjectId = candidates[0].InputObjectId;
            tool.castObjectId = candidates[candidates.Count - 1].OutputObjectId;
            tool.SnapshotPlayerInventory();
            if (!TryAttachSourceCover(factory, item, candidates, previews, tool, out var cover, out rejection)) return false;
            tool.buildPreviews.AddRange(previews);
            if (!TryAttachDestinationCover(factory, item, candidates, previews, tool, out var destinationCover, out rejection)) return false;
            var valid = tool.CheckFullPathConditions();
            if (readOnlyRoute is not null)
            {
                readOnlyRoute.NativePathCheckPerformed=true;
                readOnlyRoute.Points=previews.Select((p,i)=>new NativeBeltRoutePoint{Index=i,PlannedPosition=Snapshot(candidates[i].Position),Position=Snapshot(p.lpos),
                    Rotation=NativeFactoryGeometry.Rotation(p.lrot),Tilt=p.tilt,NativeCondition=p.condition.ToString()}).ToList();
                var failed=previews.FindIndex(p=>p.condition!=EBuildCondition.Ok);
                if(failed>=0) {readOnlyRoute.FailedBeltIndex=failed;readOnlyRoute.FailureCondition=previews[failed].condition.ToString();}
            }
            var rejected = previews.FirstOrDefault(preview => preview.condition != EBuildCondition.Ok || preview.coverObjId != 0 || preview.willRemoveCover)
                ?? (cover is not null && cover.condition != EBuildCondition.Ok ? cover : null)
                ?? (destinationCover is not null && destinationCover.condition != EBuildCondition.Ok ? destinationCover : null);
            if (!valid || rejected is not null || !SourceCoverMatches(cover, candidates[0].SourceBeltAnchor)
                || !DestinationCoverMatches(destinationCover, candidates[candidates.Count - 1].DestinationBeltAnchor, previews))
            {
                // Material checking precedes other native conditions. Describe a pure
                // shortage without asserting that unchecked geometry would be valid.
                var conditions = previews.Select(preview => preview.condition.ToString()).ToList();
                if (cover is not null) conditions.Add(cover.condition.ToString());
                if (destinationCover is not null) conditions.Add(destinationCover.condition.ToString());
                rejectionError = BeltBuildRejectionPolicy.DescribeInventoryShortage(conditions,
                    SourceCoverMatches(cover, candidates[0].SourceBeltAnchor)
                        && DestinationCoverMatches(destinationCover, candidates[candidates.Count - 1].DestinationBeltAnchor, previews),
                    previews.Any(preview => preview.coverObjId != 0 || preview.willRemoveCover));
                rejection = rejectionError?.Message
                    ?? $"DSP belt-path validation returned {rejected?.condition.ToString() ?? "rejected"}.";
                return false;
            }

            if (destinationCover is not null && !EmptyJoinPreviewGraphMatches(tool, cover, destinationCover, previews))
            {
                rejection = "belt_join_native_preview_links_changed";
                return false;
            }

            for (var index = 0; index < previews.Count; index++)
            {
                accepted.Add(BuildStepPlan.FromPreview(candidates[index], previews[index]));
            }

            var targetAnchor = candidates[candidates.Count - 1].DestinationBeltAnchor;
            if (targetAnchor is not null && accepted.Any(step => step.Tilt != 0
                || Math.Abs(step.Position.magnitude - targetAnchor.Position.magnitude) > .05f))
            {
                rejection = "belt_join_native_adjustment_not_horizontal";
                return false;
            }

            // Native checking can adjust heights/poses. Its output is also a NEW-object
            // path, so it must pass the complete occupancy guard after adjustment.
            if (elevated && !CompletePreparedElevatedBeltPath(accepted, factory.planet.radius + .2f, candidates))
            {
                rejection = "belt_elevation_native_adjustment_unsupported: the native validator changed the explicit free path or endpoint levels.";
                return false;
            }
            if (geodesic && !CompletePreparedGroundBeltPath(accepted, factory.planet.realRadius + .2f, candidates))
            {
                rejection = "belt_geodesic_native_adjustment_unsupported: native checking moved an endpoint or raised the free ground path.";
                return false;
            }
            return TryValidateNewBeltOccupancy(factory, accepted, out rejection);
        }
        finally
        {
            try { tool.ReleaseSnapshot(); }
            finally { tool.buildPreviews.Clear(); tool._Free(); }
        }
    }

    private BridgeError? RevalidateStructuredBuildOnMainThread(NormalActionPlanPayload plan)
    {
        var playerResult = _reader.GetPlayerStateOnMainThread(
            plan.SessionId,
            new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return playerResult.Error;
        }

        var item = LDB.items.Select(plan.BuildingItemId);
        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        if (item?.prefabDesc is null || factory is null || player?.package is null
            || !GameMain.history.ItemUnlocked(plan.BuildingItemId)
            || player.package.GetItemCount(plan.BuildingItemId) < plan.BuildSteps.Count
            || !string.Equals(CanonicalStateHash.PlayerAction(playerResult.Value), plan.PlayerStateHash, StringComparison.Ordinal))
        {
            return Stale("Building unlock, owned item count, player, or construction queue changed after prepare.");
        }

        if (plan.BuildResourceNodeId > 0)
        {
            var resource = _reader.InspectResourceNodeOnMainThread(
                plan.SessionId,
                new InspectResourceNodeRequest
                {
                    PlanetId = plan.PlanetId,
                    Kind = ResourceNodeKinds.Vein,
                    NodeId = plan.BuildResourceNodeId,
                });
            if (!resource.Success || resource.Value is null
                || !string.Equals(resource.Value.StateHash, plan.BuildResourceStateHash, StringComparison.Ordinal))
            {
                return Stale("The exact resource target changed after build preparation.");
            }
        }

        if (!RevalidateBuildEndpoint(plan, plan.SourceObjectId, plan.SourceFactoryStateHash)
            || !RevalidateBuildEndpoint(plan, plan.DestinationObjectId, plan.DestinationFactoryStateHash))
        {
            return Stale("A bound build endpoint identity or connection changed after preparation.");
        }

        bool valid;
        string rejection;
        if (plan.BuildKind == NormalBuildKinds.Belt)
        {
            valid = TryValidateBeltBuild(factory, player, item, plan.BuildSteps, out var accepted, out rejection, out _)
                && BuildStepsEqual(plan.BuildSteps, accepted);
        }
        else if (plan.BuildKind == NormalBuildKinds.Inserter)
        {
            valid = TryValidateInserterBuild(factory, player, item, plan.BuildSteps[0], out var accepted, out rejection, out _)
                && BuildStepsEqual(plan.BuildSteps, new[] { accepted });
        }
        else
        {
            valid = TryValidateClickBuild(
                    factory,
                    player,
                    item,
                    plan.BuildSteps[0],
                    plan.BuildResourceNodeId,
                    out var accepted,
                    out rejection)
                && BuildStepsEqual(plan.BuildSteps, new[] { accepted });
        }

        return valid ? null : Stale("DSP no longer accepts the exact prepared construction plan: " + rejection);
    }

    private bool RevalidateBuildEndpoint(NormalActionPlanPayload plan, int objectId, string endpointHash)
    {
        if (objectId <= 0)
        {
            return string.IsNullOrEmpty(endpointHash);
        }

        var snapshot = _reader.InspectFactoryEntityOnMainThread(
            plan.SessionId,
            new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = objectId });
        return snapshot.Success && snapshot.Value is not null
            && string.Equals(BuildEndpointHash(snapshot.Value), endpointHash, StringComparison.Ordinal);
    }

    private static void CreatePreparedPrebuildsOnMainThread(ActionRecord action)
    {
        var factory = GameMain.localPlanet?.factory
            ?? throw new InvalidOperationException("The local factory is unavailable.");
        var player = GameMain.mainPlayer
            ?? throw new InvalidOperationException("The player is unavailable.");
        var item = LDB.items.Select(action.Plan.BuildingItemId)
            ?? throw new InvalidOperationException("The planned building prototype disappeared.");
        var baseline = player.package.GetItemCount(item.ID);
        if (baseline < action.Plan.BuildSteps.Count)
        {
            throw new InvalidOperationException("The planned building items are no longer in inventory.");
        }

        if (!BuildUiIsIdle(player))
        {
            throw new InvalidOperationException("The normal build UI acquired preview state during commit.");
        }

        foreach (var step in action.Plan.BuildSteps)
            if (ValidateInitialSorterFilter(item, step.FilterItemId) is not null)
                throw new InvalidOperationException("The prepared initial sorter filter is no longer supported or unlocked.");

        List<BuildPreview> previews;
        Action create;
        Action cleanup;
        if (action.Plan.BuildKind == NormalBuildKinds.Belt)
        {
            if (!TryValidateNewBeltOccupancy(factory, action.Plan.BuildSteps, out var occupied))
                throw new InvalidOperationException("The prepared new belt site changed before construction: " + occupied);
            var tool = new SpherewrightPathBuildTool();
            tool._Init(GameMain.data!);
            tool.SetFactoryReferences();
            tool.handItem = item;
            tool.handPrefabDesc = item.prefabDesc;
            tool.startObjectId = action.Plan.BuildSteps[0].InputObjectId;
            tool.castObjectId = action.Plan.BuildSteps[action.Plan.BuildSteps.Count - 1].OutputObjectId;
            tool.SnapshotPlayerInventory();
            previews = CreateLinkedPreviews(action.Plan.BuildSteps, item);
            BuildPreview? sourceCover;
            BuildPreview? destinationCover;
            try
            {
                if (!TryAttachSourceCover(factory, item, action.Plan.BuildSteps, previews, tool, out sourceCover, out var sourceRejection))
                    throw new InvalidOperationException("The prepared source cover is no longer valid: " + sourceRejection);
                tool.buildPreviews.AddRange(previews);
                if (!TryAttachDestinationCover(factory, item, action.Plan.BuildSteps, previews, tool, out destinationCover, out var destinationRejection))
                    throw new InvalidOperationException("The prepared destination cover is no longer valid: " + destinationRejection);
                if (!tool.CheckFullPathConditions() || !PreviewsExactlyMatch(action.Plan.BuildSteps, previews))
                    throw new InvalidOperationException("DSP rejected or changed the exact prepared belt path at commit.");
                if (!SourceCoverMatches(sourceCover, action.Plan.BuildSteps[0].SourceBeltAnchor))
                    throw new InvalidOperationException("DSP changed the exact prepared non-removing source cover.");
                if (!DestinationCoverMatches(destinationCover, action.Plan.BuildSteps[action.Plan.BuildSteps.Count - 1].DestinationBeltAnchor, previews))
                    throw new InvalidOperationException("DSP changed the exact prepared non-removing destination cover.");
            }
            catch
            {
                try { tool.ReleaseSnapshot(); }
                finally { tool.buildPreviews.Clear(); tool._Free(); }
                throw;
            }

            create = () => CreateWithBeltCoverProof(factory, tool, sourceCover, destinationCover, previews,
                action.Plan.BuildSteps[0].SourceBeltAnchor, action.Plan.BuildSteps[action.Plan.BuildSteps.Count - 1].DestinationBeltAnchor);
            cleanup = () =>
            {
                try { tool.ReleaseSnapshot(); }
                finally { tool.buildPreviews.Clear(); tool._Free(); }
            };
        }
        else if (action.Plan.BuildKind == NormalBuildKinds.Inserter)
        {
            if (!RevalidateBeltAttachment(factory, action.Plan.BuildSteps[0]))
                throw new InvalidOperationException("The bound native belt attachment changed before construction; do not replay the accepted action.");
            var tool = new SpherewrightInserterBuildTool();
            tool._Init(GameMain.data!);
            tool.SetFactoryReferences();
            tool.handItem = item;
            tool.handPrefabDesc = item.prefabDesc;
            tool.startObjectId = action.Plan.SourceObjectId;
            tool.castObjectId = action.Plan.DestinationObjectId;
            tool.SnapshotPlayerInventory();
            previews = CreateLinkedPreviews(action.Plan.BuildSteps, item);
            tool.buildPreviews.AddRange(previews);
            if (!tool.CheckBuildConditions() || !PreviewsExactlyMatch(action.Plan.BuildSteps, previews))
            {
                tool.buildPreviews.Clear();
                tool.ReleaseSnapshot();
                tool._Free();
                throw new InvalidOperationException("DSP rejected or changed the exact prepared inserter at commit.");
            }

            create = tool.CreatePrebuilds;
            cleanup = () =>
            {
                tool.buildPreviews.Clear();
                tool.ReleaseSnapshot();
                tool._Free();
            };
        }
        else
        {
            var tool = new SpherewrightClickBuildTool();
            tool._Init(GameMain.data!);
            tool.SetFactoryReferences();
            tool.handItem = item;
            tool.handPrefabDesc = item.prefabDesc;
            tool.yaw = action.Plan.BuildSteps[0].Yaw;
            tool.SnapshotPlayerInventory();
            previews = CreateLinkedPreviews(action.Plan.BuildSteps, item);
            tool.buildPreviews.AddRange(previews);
            if (!tool.CheckBuildConditions() || !PreviewsExactlyMatch(action.Plan.BuildSteps, previews))
            {
                tool.buildPreviews.Clear();
                tool.ReleaseSnapshot();
                tool._Free();
                throw new InvalidOperationException("DSP rejected or changed the exact prepared building at commit.");
            }

            create = tool.CreatePrebuilds;
            cleanup = () =>
            {
                tool.buildPreviews.Clear();
                tool.ReleaseSnapshot();
                tool._Free();
            };
        }

        try
        {
            action.PreexistingBuildEntityIds.Clear();
            if (action.Plan.BuildKind == NormalBuildKinds.Inserter)
            {
                // DSP anchors every sorter that leaves a building at the same
                // source pose.  Capture older co-located sorters before the
                // prebuild exists so completion cannot attribute this action
                // to a different, already-built sorter.
                CaptureBuiltEntityIds(
                    factory,
                    item.ID,
                    previews[0].lpos,
                    action.PreexistingBuildEntityIds);
            }
            else if (action.Plan.BuildKind == NormalBuildKinds.Belt)
            {
                // Defence-in-depth attribution only, NOT permission for a co-located
                // source/new belt. The complete-path occupancy gate rejects such
                // previews before any native construction.
                foreach (var preview in previews)
                {
                    CaptureBuiltEntityIds(
                        factory,
                        item.ID,
                        preview.lpos,
                        action.PreexistingBuildEntityIds);
                }
            }

            // Retain the exact prepared geometry before native create/post-create
            // proofs. A proof exception after material consumption must not discard
            // the evidence required for same-action quarantine reconciliation.
            action.ExpectedBuildEntities.Clear();
            foreach (var step in action.Plan.BuildSteps)
                action.ExpectedBuildEntities.Add(new BuildExpectedEntity
                {
                    ItemId = item.ID,
                    Position = step.Position,
                    InputObjectId = step.InputObjectId,
                    OutputObjectId = step.OutputObjectId,
                    InputStepIndex = step.InputStepIndex,
                    OutputStepIndex = step.OutputStepIndex,
                });

            try { create(); }
            finally
            {
                // Record only actual surviving native prebuild identities. Partial
                // creation still remains unknown; cost, unique entity attribution
                // and full directed topology are required by reconciliation.
                foreach (var preview in previews)
                {
                    var id = preview.objId;
                    if (id >= 0 || id == int.MinValue || factory.prebuildPool is null
                        || -id >= factory.prebuildCursor || -id >= factory.prebuildPool.Length) continue;
                    var prebuild = factory.prebuildPool[-id];
                    if (prebuild.id == -id && prebuild.protoId == item.ID && !prebuild.isDestroyed
                        && Vector3.Distance(prebuild.pos, preview.lpos) <= .01f
                        && !action.PrebuildIds.Contains(-id)) action.PrebuildIds.Add(-id);
                }
            }
            if (action.Plan.BuildKind == NormalBuildKinds.Belt
                && action.Plan.BuildSteps[0].BeltPathMode == BeltPathModes.NativeElevatedGrid
                && !ProvesCreatedElevatedPath(factory, action.Plan.BuildSteps, previews))
                throw new InvalidOperationException("Native elevated prebuild identity, pose or complete free-path connection proof failed; do not replay construction.");
            foreach (var preview in previews)
            {
                if (preview.objId >= 0)
                {
                    throw new InvalidOperationException("DSP did not return an ordinary prebuild object ID for every step.");
                }

                if (action.Plan.BuildKind == NormalBuildKinds.Inserter)
                {
                    var prebuildId = -preview.objId;
                    if (prebuildId <= 0 || prebuildId >= factory.prebuildCursor || prebuildId >= factory.prebuildPool.Length
                        || factory.prebuildPool[prebuildId].id != prebuildId
                        || factory.prebuildPool[prebuildId].protoId != item.ID
                        || factory.prebuildPool[prebuildId].filterId != action.Plan.BuildSteps[0].FilterItemId
                        || factory.prebuildPool[prebuildId].pickOffset != action.Plan.BuildSteps[0].InputOffset
                        || factory.prebuildPool[prebuildId].insertOffset != action.Plan.BuildSteps[0].OutputOffset)
                        throw new InvalidOperationException("Native sorter prebuild identity or initial filter readback failed; do not replay construction.");
                    var step = action.Plan.BuildSteps[0];
                    var prebuild = factory.prebuildPool[prebuildId];
                    if (step.AttachmentGeometryHash is not null
                        && (Vector3.Distance(prebuild.pos, step.Position) > .01f
                            || Vector3.Distance(prebuild.pos2, step.Position2) > .01f
                            || Quaternion.Angle(prebuild.rot, step.Rotation) > .1f
                            || Quaternion.Angle(prebuild.rot2, step.Rotation2) > .1f))
                        throw new InvalidOperationException("Native sorter prebuild attachment poses changed; do not replay construction.");
                }

                if (!action.PrebuildIds.Contains(-preview.objId)) action.PrebuildIds.Add(-preview.objId);
            }

            if (player.package.GetItemCount(item.ID) != baseline - previews.Count)
            {
                throw new InvalidOperationException("The accepted prebuild set did not consume exactly the planned owned items.");
            }
        }
        finally
        {
            cleanup();
        }
    }

    private void UpdatePreparedBuildOnMainThread(ActionRecord action)
    {
        var factory = GameMain.localPlanet?.factory;
        if (factory is null)
        {
            return;
        }

        var anyAlive = action.PrebuildIds.Any(prebuildId =>
            prebuildId > 0
            && prebuildId < factory.prebuildCursor
            && prebuildId < factory.prebuildPool.Length
            && factory.prebuildPool[prebuildId].id == prebuildId
            && !factory.prebuildPool[prebuildId].isDestroyed);
        if (anyAlive)
        {
            if (GameMain.gameTick > action.StartedAtGameTick + Math.Max(72000, action.Plan.EstimatedTicks * 20))
            {
                QuarantineBuildOutcome(action, "Ordinary prebuilds remained unfinished beyond the bounded game-tick window.");
            }

            return;
        }

        var resolved = new List<int>();
        foreach (var expected in action.ExpectedBuildEntities)
        {
            var entityId = FindBuiltEntityExcluding(
                factory,
                expected.ItemId,
                expected.Position,
                action.PreexistingBuildEntityIds,
                resolved);
            if (entityId <= 0)
            {
                QuarantineBuildOutcome(action, "An accepted prebuild disappeared without a provable matching built entity.");
                return;
            }

            resolved.Add(entityId);
        }

        if (!VerifyBuiltTopology(factory, action.Plan, resolved, out var rejection))
        {
            QuarantineBuildOutcome(action, "Built-entity topology readback failed: " + rejection);
            return;
        }

        action.TargetObjectIds = resolved;
        action.TargetObjectId = resolved.Count == 1 ? resolved[0] : (int?)null;
        Complete(action, $"Construction drones completed all {resolved.Count} ordinary prebuild(s), and entity/component/connection readback matched the plan.");
    }

    private void QuarantineBuildOutcome(ActionRecord action, string message)
    {
        action.State = NormalActionStates.OutcomeUnknown;
        action.Terminal = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.Message = message;
        action.OriginalOutcomeMessage = message;
        action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
        _sessions.QuarantineWritesOnMainThread(action.ActionId, message);
    }

    private static bool VerifyBuiltTopology(
        PlanetFactory factory,
        NormalActionPlanPayload plan,
        IReadOnlyList<int> entityIds,
        out string rejection)
    {
        rejection = string.Empty;
        if (plan.BuildKind == NormalBuildKinds.Resource)
        {
            ref var entity = ref factory.entityPool[entityIds[0]];
            if (entity.minerId <= 0 || entity.minerId >= factory.factorySystem.minerCursor)
            {
                rejection = "The resource building has no valid miner component.";
                return false;
            }

            ref var miner = ref factory.factorySystem.minerPool[entity.minerId];
            var actualNodeIds = miner.veins is null
                ? Array.Empty<int>()
                : miner.veins.Take(Math.Min(miner.veinCount, miner.veins.Length))
                    .Where(nodeId => nodeId > 0)
                    .Distinct()
                    .OrderBy(nodeId => nodeId)
                    .ToArray();
            var expectedNodeIds = plan.BuildSteps[0].Parameters
                .Where(nodeId => nodeId > 0)
                .Distinct()
                .OrderBy(nodeId => nodeId)
                .ToArray();
            if (!actualNodeIds.Contains(plan.BuildResourceNodeId)
                || !actualNodeIds.SequenceEqual(expectedNodeIds))
            {
                rejection = "The built miner did not retain the exact prepared resource-node coverage.";
                return false;
            }
        }

        if (plan.BuildKind == NormalBuildKinds.Inserter)
        {
            ref var entity = ref factory.entityPool[entityIds[0]];
            if (entity.inserterId <= 0 || entity.inserterId >= factory.factorySystem.inserterCursor
                || entity.inserterId >= factory.factorySystem.inserterPool.Length
                || entity.inserterId >= factory.factorySystem.inserterPosePool.Length
                || factory.factorySystem.inserterPool[entity.inserterId].id != entity.inserterId
                || factory.factorySystem.inserterPool[entity.inserterId].entityId != entityIds[0])
            {
                rejection = "The built sorter has no valid inserter component.";
                return false;
            }

            ref var inserter = ref factory.factorySystem.inserterPool[entity.inserterId];
            if (inserter.pickTarget != plan.SourceObjectId || inserter.insertTarget != plan.DestinationObjectId)
            {
                rejection = $"Sorter readback was {inserter.pickTarget}->{inserter.insertTarget}, not {plan.SourceObjectId}->{plan.DestinationObjectId}.";
                return false;
            }

            var step = plan.BuildSteps[0];
            if (step.AttachmentGeometryHash is not null
                && (!RevalidateBeltAttachment(factory, step) || inserter.pickOffset != step.InputOffset
                    || inserter.insertOffset != step.OutputOffset
                    || Vector3.Distance(entity.pos, step.Position) > .01f
                    || Quaternion.Angle(entity.rot, step.Rotation) > .1f
                    || Vector3.Distance(factory.factorySystem.inserterPosePool[entity.inserterId].pos2, step.Position2) > .01f
                    || Quaternion.Angle(factory.factorySystem.inserterPosePool[entity.inserterId].rot2, step.Rotation2) > .1f))
            {
                rejection = "The built sorter did not retain its bound native segment, offsets and source pose.";
                return false;
            }
            if (!IsSorterFilterApplied(entityIds[0], step.FilterItemId))
            {
                rejection = "The built sorter did not retain its exact prepared initial filter and native sign.";
                return false;
            }
            if (!ObjectConnectionMatches(
                    factory,
                    plan.SourceObjectId,
                    step.InputFromSlot,
                    expectedIsOutput: true,
                    entityIds[0]))
            {
                rejection = $"Prepared source slot {step.InputFromSlot} does not point to the built sorter {entityIds[0]}.";
                return false;
            }

            if (!ObjectConnectionMatches(
                    factory,
                    plan.DestinationObjectId,
                    step.OutputToSlot,
                    expectedIsOutput: false,
                    entityIds[0]))
            {
                rejection = $"Prepared destination slot {step.OutputToSlot} does not point to the built sorter {entityIds[0]}.";
                return false;
            }
        }

        if (plan.BuildKind == NormalBuildKinds.Belt)
        {
            if (plan.BuildSteps[0].BeltPathMode == BeltPathModes.NativeElevatedGrid
                && !ProvesCompletedElevatedPath(factory, plan, entityIds))
            {
                rejection = "The elevated path failed exact position, native rotation/collider, empty directed membership or free-end proof.";
                return false;
            }
            var destinationAnchor = plan.BuildSteps[plan.BuildSteps.Count - 1].DestinationBeltAnchor;
            if (destinationAnchor is not null && !ProvesCompletedEmptyJoin(factory, destinationAnchor, entityIds))
            {
                rejection = "The reused destination or complete joined paths failed empty cargo, old identity/edges, native rotation or exact membership proof.";
                return false;
            }
            var sourceAnchor = plan.BuildSteps[0].SourceBeltAnchor;
            if (sourceAnchor is not null && (!TryBeltSourceTopology(factory, sourceAnchor.EntityId, out var topology, out var connections, nativeCompletionRotation: true)
                || !BeltSourceReusePolicy.SameEvidence(sourceAnchor.CompletionTopologyHash, topology)
                || !ProvesSourceOutput(factory, sourceAnchor, connections, entityIds[0], prebuild: false)))
            {
                rejection = "The reused source identity, original neighbours or new reciprocal output did not survive construction.";
                return false;
            }
            for (var index = 0; index < entityIds.Count; index++)
            {
                ref var entity = ref factory.entityPool[entityIds[index]];
                if (entity.beltId <= 0)
                {
                    rejection = $"Path entity {entityIds[index]} has no belt component.";
                    return false;
                }

                var expectedOutput = index + 1 < entityIds.Count
                    ? entityIds[index + 1]
                    : plan.DestinationObjectId;
                factory.ReadObjectConn(entityIds[index], 0, out var isOutput, out var otherObjectId, out _);
                if (!BeltConnectionProof.OutputMatches(expectedOutput, isOutput, otherObjectId))
                {
                    rejection = expectedOutput > 0
                        ? $"Belt segment {entityIds[index]} output is not connected to {expectedOutput}."
                        : $"Belt segment {entityIds[index]} unexpectedly has an output-side connection to object {otherObjectId}.";
                    return false;
                }
            }

            // Native belt slots 1..3 are inputs. An unplanned side input or a
            // non-free start is not completion of this exact prepared path.
            for (var inputSlot = 1; inputSlot <= 3; inputSlot++)
            {
                var expectedInput = inputSlot == 1 ? plan.SourceObjectId : 0;
                factory.ReadObjectConn(entityIds[0], inputSlot, out var isOutput, out var otherObjectId, out _);
                if (!BeltConnectionProof.InputMatches(expectedInput, isOutput, otherObjectId))
                {
                    rejection = $"The first belt segment input slot {inputSlot} does not match the prepared source or free-end proof.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ObjectConnectionMatches(
        PlanetFactory factory,
        int objectId,
        int slot,
        bool expectedIsOutput,
        int expectedOtherObjectId)
    {
        if (objectId <= 0)
        {
            return false;
        }

        foreach (var candidateSlot in BuildConnectionSlots.SelectVerificationCandidates(slot, 16))
        {
            factory.ReadObjectConn(objectId, candidateSlot, out var isOutput, out var otherObjectId, out _);
            if (isOutput == expectedIsOutput && otherObjectId == expectedOtherObjectId)
            {
                return true;
            }
        }

        return false;
    }

    private GameCallResult<PreparedNormalAction> PrepareStorageTransferOnMainThread(
        string? requestedSessionId,
        PrepareTransferRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.Direction != TransferDirections.PlayerToStorage
            && request.Direction != TransferDirections.StorageToPlayer)
        {
            return InvalidPlan("Transfer direction must be player-to-storage or storage-to-player.");
        }

        if (request.ItemId <= 0 || request.Count <= 0 || request.Count > 10000)
        {
            return InvalidPlan("Transfer item and count must be positive; count is bounded to 10000 per action.");
        }

        var playerResult = _reader.GetPlayerStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        var storageResult = _reader.InspectFactoryEntityOnMainThread(
            requestedSessionId,
            new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = request.StorageEntityId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(playerResult.Error!);
        }

        if (!storageResult.Success || storageResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(storageResult.Error!);
        }

        var storageSnapshot = storageResult.Value;
        if (!string.Equals(playerResult.Value.StateHash, request.ExpectedPlayerStateHash, StringComparison.Ordinal)
            || !string.Equals(storageSnapshot.StateHash, request.ExpectedStorageStateHash, StringComparison.Ordinal))
        {
            return StalePlan("Player inventory or exact storage contents changed after inspection.");
        }

        if (storageSnapshot.ObjectKind != FactoryObjectKinds.Entity
            || !string.Equals(storageSnapshot.ComponentKind, "storage", StringComparison.Ordinal))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidEntity,
                "The transfer target is not an exact built storage component.",
                false,
                "Inspect a built storage entity and use its object ID."));
        }

        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        if (factory is null || player?.package is null
            || !TryGetStorage(factory, request.StorageEntityId, out var storage))
        {
            return NotReadyPlan("The exact storage component is unavailable.");
        }

        var distance = Vector3.Distance(player.position, ToVector(storageSnapshot.Position));
        if (distance > player.mecha.buildArea)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.TargetOutOfRange,
                $"The storage is {distance:F2} metres away, outside the current normal interaction/build area.",
                true,
                "Move into range through spherewright_prepare_move, then inspect and prepare again."));
        }

        if (!CanTransferExactly(player.package, storage!, request.Direction, request.ItemId, request.Count, out var rejection))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                rejection.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0
                    ? BridgeErrorCodes.InventoryInsufficient
                    : BridgeErrorCodes.InventoryFull,
                rejection,
                true,
                "Adjust the count or free destination capacity, then inspect and prepare again."));
        }

        var playerActionHash = CanonicalStateHash.PlayerAction(playerResult.Value);
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.Transfer,
            _sessions.SessionId,
            request.PlanetId,
            playerActionHash,
            storageSnapshot.StateHash,
            request.StorageEntityId,
            request.Direction,
            request.ItemId,
            request.Count);
        var payload = NormalActionPlanPayload.Transfer(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            playerActionHash,
            storageSnapshot.StateHash,
            request.StorageEntityId,
            request.Direction,
            request.ItemId,
            request.Count);
        var prepared = AddPreparedPlan(
            payload,
            common.Session!,
            1,
            "The source decreases and destination increases by the exact requested count while their combined item count is conserved.");
        if (prepared.Success && prepared.Value is not null)
        {
            prepared.Value.SourceObjectId = request.Direction == TransferDirections.StorageToPlayer
                ? request.StorageEntityId
                : (int?)null;
            prepared.Value.DestinationObjectId = request.Direction == TransferDirections.PlayerToStorage
                ? request.StorageEntityId
                : (int?)null;
            prepared.Value.EstimatedDistance = distance;
            prepared.Value.ItemBudget.Add(new ActionItemBudget
            {
                ItemId = request.ItemId,
                Name = LDB.items.Select(request.ItemId)?.name ?? string.Empty,
                Count = request.Count,
                Direction = request.Direction,
            });
        }

        return prepared;
    }

    private BridgeError? RevalidateStorageTransferOnMainThread(NormalActionPlanPayload plan)
    {
        var playerResult = _reader.GetPlayerStateOnMainThread(
            plan.SessionId,
            new LocalPlanetRequest { PlanetId = plan.PlanetId });
        var storageResult = _reader.InspectFactoryEntityOnMainThread(
            plan.SessionId,
            new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.TransferStorageEntityId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return playerResult.Error;
        }

        if (!storageResult.Success || storageResult.Value is null)
        {
            return storageResult.Error;
        }

        if (!string.Equals(CanonicalStateHash.PlayerAction(playerResult.Value), plan.PlayerStateHash, StringComparison.Ordinal)
            || !string.Equals(storageResult.Value.StateHash, plan.TransferStorageStateHash, StringComparison.Ordinal))
        {
            return Stale("Player inventory or storage contents changed after transfer preparation.");
        }

        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        if (factory is null || player?.package is null
            || !TryGetStorage(factory, plan.TransferStorageEntityId, out var storage)
            || !CanTransferExactly(player.package, storage!, plan.TransferDirection, plan.TransferItemId, plan.Count, out _))
        {
            return Stale("Transfer source count, destination capacity, or exact storage identity changed.");
        }

        return null;
    }

    private static void ExecuteStorageTransferOnMainThread(ActionRecord action)
    {
        var factory = GameMain.localPlanet?.factory
            ?? throw new InvalidOperationException("The local factory is unavailable.");
        var player = GameMain.mainPlayer
            ?? throw new InvalidOperationException("The player is unavailable.");
        var plan = action.Plan;
        if (!TryGetStorage(factory, plan.TransferStorageEntityId, out var storage))
        {
            throw new InvalidOperationException("The exact storage component disappeared.");
        }

        var playerBefore = player.package.GetItemCount(plan.TransferItemId);
        var storageBefore = storage!.GetItemCount(plan.TransferItemId);
        var source = plan.TransferDirection == TransferDirections.PlayerToStorage ? player.package : storage;
        var destination = plan.TransferDirection == TransferDirections.PlayerToStorage ? storage : player.package;
        var removed = source.TakeItem(plan.TransferItemId, plan.Count, out var removedInc);
        if (removed != plan.Count)
        {
            throw new InvalidOperationException("The exact transfer source did not remove the prepared count.");
        }

        var added = destination.AddItemStacked(plan.TransferItemId, removed, removedInc, out var remainingInc);
        if (added != removed || remainingInc != 0)
        {
            throw new InvalidOperationException("The exact transfer destination did not accept the prepared count.");
        }

        if (ReferenceEquals(destination, player.package))
        {
            player.NotifyPackageAddItem(plan.TransferItemId, added, removedInc);
        }

        var playerAfter = player.package.GetItemCount(plan.TransferItemId);
        var storageAfter = storage.GetItemCount(plan.TransferItemId);
        var expectedPlayerDelta = plan.TransferDirection == TransferDirections.PlayerToStorage ? -plan.Count : plan.Count;
        if (playerAfter - playerBefore != expectedPlayerDelta
            || storageAfter - storageBefore != -expectedPlayerDelta
            || playerBefore + storageBefore != playerAfter + storageAfter)
        {
            throw new InvalidOperationException("Post-transfer readback did not prove exact bilateral conservation.");
        }

        action.TargetObjectId = plan.TransferStorageEntityId;
        action.TargetItemId = plan.TransferItemId;
        action.BeforeTargetAmount = storageBefore;
        action.AfterTargetAmount = storageAfter;
        action.Message = $"Normal storage transfer conserved item {plan.TransferItemId}: player {playerBefore}->{playerAfter}, storage {storageBefore}->{storageAfter}.";
        action.State = NormalActionStates.Completed;
        action.Terminal = true;
        action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.AfterInventory = CaptureInventory(player);
        action.AfterStateHash = CanonicalStateHash.Combine(
            NormalActionKinds.Transfer,
            playerAfter,
            storageAfter,
            plan.TransferItemId,
            plan.Count);
    }

    private GameCallResult<PreparedNormalAction> PrepareStructuredConfigurationOnMainThread(
        string? requestedSessionId,
        PrepareConfigureBuildingRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.Mode == BuildingConfigurationModes.StorageCapacity)
            return PrepareStorageConfigurationOnMainThread(requestedSessionId, request, common.Session!);
        if (!string.IsNullOrEmpty(request.StorageOperation) || request.StorageBannedGridCount != -1)
            return InvalidPlan("Storage operation parameters require storage-capacity mode.");

        if (request.Mode != BuildingConfigurationModes.Production
            && request.Mode != BuildingConfigurationModes.Research
            && request.Mode != BuildingConfigurationModes.SorterFilter
            && request.Mode != BuildingConfigurationModes.LogisticsStationStorage
            && request.Mode != BuildingConfigurationModes.LogisticsStationBelt
            && request.Mode != BuildingConfigurationModes.LogisticsStationCharge)
        {
            return InvalidPlan("Configuration mode must be production, research, sorter-filter, logistics-station-storage, logistics-station-belt, or logistics-station-charge.");
        }

        var stationLocalLogic = ELogisticStorage.None;
        var stationRemoteLogic = ELogisticStorage.None;
        if (request.Mode == BuildingConfigurationModes.LogisticsStationStorage
            && (!TryParseLogisticsStorageLogic(request.StationLocalLogic, out stationLocalLogic)
                || !TryParseLogisticsStorageLogic(request.StationRemoteLogic, out stationRemoteLogic)))
        {
            return InvalidPlan("Station local and remote logic must each be none, supply, or demand.");
        }

        var snapshotResult = _reader.InspectFactoryEntityOnMainThread(
            requestedSessionId,
            new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = request.EntityId });
        if (!snapshotResult.Success || snapshotResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(snapshotResult.Error!);
        }

        var snapshot = snapshotResult.Value;
        var sorterFilterMode = request.Mode == BuildingConfigurationModes.SorterFilter;
        if (sorterFilterMode)
        {
            var hashError = SorterFilterPolicy.ValidateInspectionHash(request.ExpectedFactoryStateHash,
                snapshot.StateHash, snapshot.ConfigurationStateHash);
            if (hashError is not null) return GameCallResult<PreparedNormalAction>.Failed(hashError);
        }
        var configurationStateHash = sorterFilterMode
            ? snapshot.ConfigurationStateHash
            : snapshot.StateHash;
        if (!string.Equals(request.ExpectedFactoryStateHash, configurationStateHash, StringComparison.Ordinal))
        {
            return StalePlan(sorterFilterMode
                ? "Sorter identity, topology, current filter, or carried cargo changed after inspection."
                : "Building mode, recipe, progress, buffers, or identity changed after inspection.");
        }

        var stationMode = request.Mode == BuildingConfigurationModes.LogisticsStationStorage
                          || request.Mode == BuildingConfigurationModes.LogisticsStationBelt
                          || request.Mode == BuildingConfigurationModes.LogisticsStationCharge;
        if (snapshot.ObjectKind != FactoryObjectKinds.Entity
            || (!stationMode && !sorterFilterMode && (snapshot.Progress != 0
                || snapshot.IsWorking
                || snapshot.Buffers.Any(buffer => buffer.Count != 0))))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                stationMode
                    ? "The logistics-station target is not a completed built entity."
                    : "Only a fully idle built device with empty input, output, and internal buffers can be configured.",
                true,
                stationMode
                    ? "Wait for normal construction to finish, then inspect the exact station entity again."
                    : "Wait for the exact device to become idle and empty, then inspect and prepare again."));
        }

        var factory = GameMain.localPlanet?.factory;
        if (factory is null)
        {
            return NotReadyPlan("The local factory is unavailable.");
        }

        RecipeProto? recipe = null;
        var stationBeltItemId = 0;
        if (request.Mode == BuildingConfigurationModes.Production)
        {
            recipe = LDB.recipes.Select(request.RecipeId);
            if (recipe is null || !GameMain.history.RecipeUnlocked(request.RecipeId))
            {
                return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                    BridgeErrorCodes.InvalidRecipe,
                    "The requested runtime recipe does not exist or is not unlocked.",
                    false,
                    "Complete the normal prerequisite technology and choose an unlocked recipe."));
            }

            if (!CanDeviceRunRecipe(factory, request.EntityId, recipe, out var reason))
            {
                return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                    BridgeErrorCodes.RecipeNotSupportedByBuilding,
                    reason,
                    false,
                    "Choose a recipe whose runtime type matches the exact built device."));
            }
        }
        else if (request.Mode == BuildingConfigurationModes.Research
                 && !CanLabEnterResearchMode(factory, request.EntityId, request.TechId, out var reason))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidTechnology,
                reason,
                false,
                "Select an active matrix technology through the normal research queue, then configure an empty matrix lab."));
        }
        else if (request.Mode == BuildingConfigurationModes.SorterFilter
                 && !CanSetSorterFilter(factory, request.EntityId, request.FilterItemId, out var sorterReason))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.ActionRejected,
                sorterReason,
                true,
                "Inspect the connected sorter again. Cargo-free sorters or ordinary2011/2012 unidirectional sorters in Inserting with valid existing cargo are supported; changing the filter never removes or redirects held cargo."));
        }
        else if (stationMode)
        {
            if (snapshot.LogisticsStation is null
                || !string.Equals(
                    request.ExpectedStationConfigurationStateHash,
                    snapshot.LogisticsStation.ConfigurationStateHash,
                    StringComparison.Ordinal))
            {
                return StalePlan("The logistics-station identity, storage-slot configuration, route settings, or belt topology changed after inspection.");
            }

            if (request.Mode == BuildingConfigurationModes.LogisticsStationStorage
                && !CanConfigureLogisticsStationStorage(
                    factory,
                    request.EntityId,
                    request.StationStorageIndex,
                    request.StationItemId,
                    request.StationMaximumCount,
                    stationLocalLogic,
                    stationRemoteLogic,
                    out var stationReason))
            {
                return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                    BridgeErrorCodes.ActionRejected,
                    stationReason,
                    true,
                    "Choose an unlocked item and an empty or same-item slot with no outstanding orders; use 100-item limit steps within the station's current researched capacity."));
            }

            if (request.Mode == BuildingConfigurationModes.LogisticsStationCharge
                && !CanConfigureLogisticsStationCharge(
                    factory,
                    request.EntityId,
                    request.StationMaximumChargePowerWatts,
                    out _,
                    out var chargeReason))
            {
                return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                    BridgeErrorCodes.ActionRejected,
                    chargeReason,
                    true,
                    "Choose a different 3 MW UI step within one-half through five times this exact station prefab's default charging energy."));
            }

            if (request.Mode == BuildingConfigurationModes.LogisticsStationBelt
                && !CanConfigureLogisticsStationBelt(
                    factory,
                    request.EntityId,
                    request.StationBeltSlotIndex,
                    request.StationBeltStorageIndex,
                    out stationBeltItemId,
                    out var beltReason))
            {
                return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                    BridgeErrorCodes.ActionRejected,
                    beltReason,
                    true,
                    "Choose an empty connected output port and a configured unlocked station storage slot; existing nonzero output selectors are never replaced."));
            }
        }

        var normalizedStationLocalLogic = ToContractLogisticsStorageLogic(stationLocalLogic);
        var normalizedStationRemoteLogic = ToContractLogisticsStorageLogic(stationRemoteLogic);

        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.ConfigureBuilding,
            _sessions.SessionId,
            request.PlanetId,
            configurationStateHash,
            request.EntityId,
            request.Mode,
            request.RecipeId,
            request.TechId,
            request.FilterItemId,
            request.ExpectedStationConfigurationStateHash,
            request.StationStorageIndex,
            request.StationBeltSlotIndex,
            request.StationBeltStorageIndex,
            stationBeltItemId,
            request.StationItemId,
            request.StationMaximumCount,
            normalizedStationLocalLogic,
            normalizedStationRemoteLogic,
            request.StationMaximumChargePowerWatts);
        var payload = NormalActionPlanPayload.Configure(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            configurationStateHash,
            request.EntityId,
            request.RecipeId,
            request.Mode,
            request.TechId,
            request.FilterItemId,
            request.ExpectedStationConfigurationStateHash,
            request.StationStorageIndex,
            request.StationBeltSlotIndex,
            request.StationBeltStorageIndex,
            stationBeltItemId,
            request.StationItemId,
            request.StationMaximumCount,
            normalizedStationLocalLogic,
            normalizedStationRemoteLogic,
            request.StationMaximumChargePowerWatts);
        var prepared = AddPreparedPlan(
            payload,
            common.Session!,
            1,
            request.Mode == BuildingConfigurationModes.Research
                ? "The exact empty matrix lab reports research mode and the active technology after the UI/business setting path is called once."
                : request.Mode == BuildingConfigurationModes.SorterFilter
                    ? "The exact connected sorter reports the target filter/sign; the complete native inserter state except filter, both endpoints, and player inventory are preserved immediately. Existing cargo still requires delivery to the same destination."
                    : request.Mode == BuildingConfigurationModes.LogisticsStationStorage
                        ? "The exact station slot reports the selected unlocked item, 100-step limit, and local/remote logic after PlanetTransport.SetStationStorage is called once; item count and proliferator points remain unchanged by the call."
                    : request.Mode == BuildingConfigurationModes.LogisticsStationBelt
                        ? "The exact connected station output port reports the selected storage slot after the current-version UI field path is applied once; station inventory, player inventory, port topology, and every unrelated selector remain unchanged."
                    : request.Mode == BuildingConfigurationModes.LogisticsStationCharge
                        ? "The exact station power consumer reports the requested UI-step maximum charging power after the current-version UI field path is applied once; station and player inventory remain unchanged."
                    : "The exact idle device reports the target recipe after the current-version UI/business setting path is called once.");
        if (prepared.Success && prepared.Value is not null && recipe is not null)
        {
            AddRecipeBudget(prepared.Value, recipe, 1);
        }

        return prepared;
    }

    private BridgeError? RevalidateStructuredConfigurationOnMainThread(NormalActionPlanPayload plan)
    {
        var factory = GameMain.localPlanet?.factory;
        if (factory is null)
        {
            return Stale("The local factory disappeared after configuration preparation.");
        }

        if (plan.ConfigureMode == BuildingConfigurationModes.Research)
        {
            return CanLabEnterResearchMode(factory, plan.EntityId, plan.ConfigureTechId, out _)
                ? null
                : Stale("The exact lab or active matrix technology changed after preparation.");
        }

        if (plan.ConfigureMode == BuildingConfigurationModes.SorterFilter)
        {
            var snapshotResult = _reader.InspectFactoryEntityOnMainThread(
                plan.SessionId,
                new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.EntityId });
            if (!snapshotResult.Success
                || snapshotResult.Value is null
                || !string.Equals(
                    snapshotResult.Value.ConfigurationStateHash,
                    plan.FactoryStateHash,
                    StringComparison.Ordinal))
            {
                return Stale("The exact sorter identity, topology, current filter, or carried cargo changed after preparation.");
            }

            return CanSetSorterFilter(factory, plan.EntityId, plan.ConfigureFilterItemId, out _)
                ? null
                : Stale("The exact sorter no longer satisfies the cargo-preserving filter assignment window, or the filter item changed after preparation.");
        }

        if (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationStorage
            || plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt
            || plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge)
        {
            var snapshotResult = _reader.InspectFactoryEntityOnMainThread(
                plan.SessionId,
                new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.EntityId });
            var stationSnapshot = snapshotResult.Value?.LogisticsStation;
            if (!snapshotResult.Success
                || stationSnapshot is null
                || !string.Equals(
                    stationSnapshot.ConfigurationStateHash,
                    plan.StationConfigurationStateHash,
                    StringComparison.Ordinal))
            {
                return Stale("The exact station identity or configuration changed after preparation.");
            }

            if (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge)
            {
                return CanConfigureLogisticsStationCharge(
                    factory,
                    plan.EntityId,
                    plan.ConfigureStationMaximumChargePowerWatts,
                    out _,
                    out _)
                    ? null
                    : Stale("The exact station prefab, power-consumer identity, charging bound, or current maximum changed after preparation.");
            }

            if (plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt)
            {
                return CanConfigureLogisticsStationBelt(
                           factory,
                           plan.EntityId,
                           plan.ConfigureStationBeltSlotIndex,
                           plan.ConfigureStationBeltStorageIndex,
                           out var stationBeltItemId,
                           out _)
                       && stationBeltItemId == plan.ConfigureStationBeltItemId
                    ? null
                    : Stale("The exact station output port, connected belt, selected storage item, or selector state changed after preparation.");
            }

            return TryParseLogisticsStorageLogic(plan.ConfigureStationLocalLogic, out var localLogic)
                   && TryParseLogisticsStorageLogic(plan.ConfigureStationRemoteLogic, out var remoteLogic)
                   && CanConfigureLogisticsStationStorage(
                       factory,
                       plan.EntityId,
                       plan.ConfigureStationStorageIndex,
                       plan.ConfigureStationItemId,
                       plan.ConfigureStationMaximumCount,
                       localLogic,
                       remoteLogic,
                       out _)
                ? null
                : Stale("The exact station slot, item unlock, capacity, current item, or outstanding orders changed after preparation.");
        }

        var recipe = LDB.recipes.Select(plan.ConfigureRecipeId);
        return recipe is not null
               && GameMain.history.RecipeUnlocked(plan.ConfigureRecipeId)
               && CanDeviceRunRecipe(factory, plan.EntityId, recipe, out _)
            ? null
            : Stale("The exact device no longer supports the prepared unlocked recipe.");
    }

    private static bool CanLabEnterResearchMode(
        PlanetFactory factory,
        int entityId,
        int techId,
        out string reason)
    {
        reason = "The exact entity is not a matrix lab or the requested matrix technology is not the current normal research target.";
        if (entityId <= 0 || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        var item = entity.id == entityId ? LDB.items.Select(entity.protoId) : null;
        var tech = LDB.techs.Select(techId);
        return item?.prefabDesc?.isLab == true
               && entity.labId > 0
               && tech is not null
               && tech.IsLabTech
               && GameMain.history.currentTech == techId
               && !GameMain.history.TechUnlocked(techId);
    }

    private static bool TryParseLogisticsStorageLogic(string? value, out ELogisticStorage logic)
    {
        if (string.Equals(value, LogisticsStorageLogics.None, StringComparison.OrdinalIgnoreCase))
        {
            logic = ELogisticStorage.None;
            return true;
        }

        if (string.Equals(value, LogisticsStorageLogics.Supply, StringComparison.OrdinalIgnoreCase))
        {
            logic = ELogisticStorage.Supply;
            return true;
        }

        if (string.Equals(value, LogisticsStorageLogics.Demand, StringComparison.OrdinalIgnoreCase))
        {
            logic = ELogisticStorage.Demand;
            return true;
        }

        logic = ELogisticStorage.None;
        return false;
    }

    private static string ToContractLogisticsStorageLogic(ELogisticStorage logic) => logic switch
    {
        ELogisticStorage.Supply => LogisticsStorageLogics.Supply,
        ELogisticStorage.Demand => LogisticsStorageLogics.Demand,
        _ => LogisticsStorageLogics.None,
    };

    private static bool CanConfigureLogisticsStationCharge(
        PlanetFactory factory,
        int entityId,
        long maximumChargePowerWatts,
        out long maximumChargeEnergyPerTick,
        out string reason)
    {
        maximumChargeEnergyPerTick = 0;
        reason = "The exact logistics-station power consumer is unavailable for this configuration.";
        if (entityId <= 0
            || entityId >= factory.entityCursor
            || entityId >= factory.entityPool.Length)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        var item = entity.id == entityId ? LDB.items.Select(entity.protoId) : null;
        if (item?.prefabDesc is null || entity.stationId <= 0)
        {
            return false;
        }

        var station = factory.transport?.GetStationComponent(entity.stationId);
        if (station is null
            || station.id != entity.stationId
            || station.entityId != entityId
            || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(
                station.isStellar,
                station.planetId,
                factory.planetId)
            || station.isCollector
            || station.isVeinCollector
            || station.pcId <= 0
            || station.pcId != entity.powerConId
            || station.pcId >= factory.powerSystem.consumerCursor
            || station.pcId >= factory.powerSystem.consumerPool.Length)
        {
            return false;
        }

        ref var consumer = ref factory.powerSystem.consumerPool[station.pcId];
        if (consumer.id != station.pcId || consumer.entityId != entityId)
        {
            return false;
        }

        if (!LogisticsStationChargePolicy.TryNormalizeUiPower(
                item.prefabDesc.workEnergyPerTick,
                maximumChargePowerWatts,
                out maximumChargeEnergyPerTick,
                out var minimumEnergyPerTick,
                out var maximumEnergyPerTick))
        {
            reason = $"The requested maximum charge power must be a 3 MW UI step from {minimumEnergyPerTick * 60L} through {maximumEnergyPerTick * 60L} watts for this station prefab.";
            return false;
        }

        if (consumer.workEnergyPerTick == maximumChargeEnergyPerTick)
        {
            reason = "The requested station maximum charge power is already applied.";
            return false;
        }

        return true;
    }

    private static bool CanConfigureLogisticsStationStorage(
        PlanetFactory factory,
        int entityId,
        int storageIndex,
        int itemId,
        int maximumCount,
        ELogisticStorage localLogic,
        ELogisticStorage remoteLogic,
        out string reason)
    {
        reason = "The exact logistics-station storage slot is unavailable for this configuration.";
        if (entityId <= 0
            || entityId >= factory.entityCursor
            || entityId >= factory.entityPool.Length
            || itemId <= 0
            || maximumCount <= 0
            || maximumCount % 100 != 0)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.stationId <= 0)
        {
            return false;
        }

        var station = factory.transport?.GetStationComponent(entity.stationId);
        if (station is null
            || station.id != entity.stationId
            || station.entityId != entityId
            || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(
                station.isStellar,
                station.planetId,
                factory.planetId)
            || station.isCollector
            || station.isVeinCollector
            || station.storage is null
            || storageIndex < 0
            || storageIndex >= station.storage.Length)
        {
            return false;
        }

        if (!station.isStellar && remoteLogic != ELogisticStorage.None)
        {
            reason = "A planetary logistics station requires remote logic none.";
            return false;
        }

        if (LDB.items.Select(itemId) is null || !GameMain.history.ItemUnlocked(itemId))
        {
            reason = "The requested station item does not exist or is not normally unlocked.";
            return false;
        }

        var model = LDB.models.Select(entity.modelIndex);
        var baseCapacity = model?.prefabDesc?.stationMaxItemCount ?? 0;
        var researchedExtraCapacity = station.isStellar
            ? GameMain.history.remoteStationExtraStorage
            : GameMain.history.localStationExtraStorage;
        var capacity = baseCapacity + researchedExtraCapacity;
        if (capacity <= 0 || maximumCount > capacity)
        {
            reason = $"The requested maximum {maximumCount} exceeds the station's current researched capacity {capacity}.";
            return false;
        }

        for (var index = 0; index < station.storage.Length; index++)
        {
            if (index != storageIndex && station.storage[index].itemId == itemId)
            {
                reason = "The requested item is already assigned to another slot in this station.";
                return false;
            }
        }

        var current = station.storage[storageIndex];
        if (current.itemId != 0 && current.itemId != itemId)
        {
            reason = "This action never replaces or clears an occupied station slot; choose an empty slot or keep the same item.";
            return false;
        }

        if (current.localOrder != 0 || current.remoteOrder != 0)
        {
            reason = "The station slot has outstanding logistics orders and must become idle before configuration.";
            return false;
        }

        if (current.itemId == 0 && (current.count != 0 || current.inc != 0))
        {
            reason = "The nominally empty station slot contains unexplained inventory state.";
            return false;
        }

        if (current.itemId == itemId
            && current.max == maximumCount
            && current.localLogic == localLogic
            && current.remoteLogic == remoteLogic)
        {
            reason = "The requested station storage configuration is already applied.";
            return false;
        }

        return true;
    }

    private static bool CanConfigureLogisticsStationBelt(
        PlanetFactory factory,
        int entityId,
        int beltSlotIndex,
        int storageIndex,
        out int selectedItemId,
        out string reason)
    {
        selectedItemId = 0;
        reason = "The exact logistics-station output port or storage slot is unavailable for this configuration.";
        if (entityId <= 0
            || entityId >= factory.entityCursor
            || entityId >= factory.entityPool.Length)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.stationId <= 0)
        {
            return false;
        }

        var station = factory.transport?.GetStationComponent(entity.stationId);
        if (station is null
            || station.id != entity.stationId
            || station.entityId != entityId
            || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(
                station.isStellar,
                station.planetId,
                factory.planetId)
            || station.isCollector
            || station.isVeinCollector
            || station.storage is null
            || station.slots is null
            || beltSlotIndex < 0
            || beltSlotIndex >= station.slots.Length
            || storageIndex < 0
            || storageIndex >= station.storage.Length)
        {
            return false;
        }

        ref var slot = ref station.slots[beltSlotIndex];
        var desiredStorageIndex = storageIndex + 1;
        if (slot.dir != IODir.Output || slot.beltId <= 0 || slot.counter != 0)
        {
            reason = "The requested station port must be a connected output with no pending port cargo.";
            return false;
        }

        var traffic = factory.cargoTraffic;
        if (traffic?.beltPool is null
            || slot.beltId >= traffic.beltCursor
            || slot.beltId >= traffic.beltPool.Length)
        {
            reason = "The output port's connected belt component is no longer current.";
            return false;
        }

        ref var belt = ref traffic.beltPool[slot.beltId];
        if (belt.id != slot.beltId
            || belt.entityId <= 0
            || belt.entityId >= factory.entityCursor
            || belt.entityId >= factory.entityPool.Length)
        {
            reason = "The output port's connected belt identity is invalid.";
            return false;
        }

        ref var beltEntity = ref factory.entityPool[belt.entityId];
        if (beltEntity.id != belt.entityId || beltEntity.beltId != slot.beltId)
        {
            reason = "The output port no longer resolves back to the same built belt entity.";
            return false;
        }

        selectedItemId = station.storage[storageIndex].itemId;
        if (selectedItemId <= 0
            || LDB.items.Select(selectedItemId) is null
            || !GameMain.history.ItemUnlocked(selectedItemId))
        {
            reason = "The requested station storage slot must contain a normally unlocked configured item.";
            return false;
        }

        if (slot.storageIdx == desiredStorageIndex)
        {
            reason = "The requested station output selector is already applied.";
            return false;
        }

        if (slot.storageIdx != 0)
        {
            reason = "This action never replaces an existing nonzero station output selector.";
            return false;
        }

        return true;
    }

    private static bool CanSetSorterFilter(
        PlanetFactory factory,
        int entityId,
        int filterItemId,
        out string reason)
    {
        reason = "The exact entity is not a connected cargo-free sorter or a supported ordinary Inserting sorter with valid retained cargo, or the requested filter item is unavailable.";
        if (entityId <= 0 || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length
            || entityId >= factory.entitySignPool.Length
            || filterItemId < 0)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.inserterId <= 0
            || entity.inserterId >= factory.factorySystem.inserterCursor
            || entity.inserterId >= factory.factorySystem.inserterPool.Length)
        {
            return false;
        }

        ref var inserter = ref factory.factorySystem.inserterPool[entity.inserterId];
        if (inserter.id != entity.inserterId || inserter.entityId != entityId
            || !SorterFilterPolicy.IsSafePreservingAssignmentWindow(
                entity.protoId,
                inserter.bidirectional,
                inserter.stage.ToString(),
                filterItemId,
                inserter.pickTarget,
                inserter.insertTarget,
                inserter.itemId,
                inserter.itemCount,
                inserter.stackCount,
                inserter.itemInc))
        {
            return false;
        }

        if (filterItemId == 0)
        {
            return true;
        }

        return LDB.items.Select(filterItemId) is not null
               && GameMain.history.ItemUnlocked(filterItemId);
    }

    private static bool IsLabInResearchMode(int entityId, int techId)
    {
        var factory = GameMain.localPlanet?.factory;
        if (factory is null || entityId <= 0 || entityId >= factory.entityCursor)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.labId <= 0 || entity.labId >= factory.factorySystem.labCursor)
        {
            return false;
        }

        ref var lab = ref factory.factorySystem.labPool[entity.labId];
        return lab.id == entity.labId && lab.researchMode && lab.techId == techId && lab.recipeId == 0;
    }

    private static bool IsSorterFilterApplied(int entityId, int filterItemId)
    {
        var factory = GameMain.localPlanet?.factory;
        if (factory is null || entityId <= 0 || entityId >= factory.entityCursor
            || entityId >= factory.entityPool.Length || entityId >= factory.entitySignPool.Length)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.inserterId <= 0
            || entity.inserterId >= factory.factorySystem.inserterCursor
            || entity.inserterId >= factory.factorySystem.inserterPool.Length)
        {
            return false;
        }

        ref var inserter = ref factory.factorySystem.inserterPool[entity.inserterId];
        ref var sign = ref factory.entitySignPool[entityId];
        return inserter.id == entity.inserterId
               && inserter.entityId == entityId
               && SorterBuildFilterPolicy.MatchesReadback(filterItemId, inserter.filter, sign.iconId0, sign.iconType);
    }

    private static BridgeError? ValidateInitialSorterFilter(ItemProto item, int filterItemId)
    {
        var existingInRange = filterItemId > 0 && filterItemId <= short.MaxValue && LDB.items.Select(filterItemId) is not null;
        var code = SorterBuildFilterPolicy.Validate(item.ID, item.prefabDesc.isInserter, filterItemId,
            existingInRange, existingInRange && GameMain.history.ItemUnlocked(filterItemId));
        return code is null ? null : BridgeError.Create(code,
            "A nonzero initialSorterFilterItemId requires an existing unlocked item and an ordinary2011/2012 sorter; zero means unfiltered.",
            false, "Inspect the runtime catalog and exact endpoints, then prepare with a supported initial filter before connecting a mixed source.");
    }

    private static bool IsLogisticsStationStorageConfigurationApplied(
        FactoryEntitySnapshot snapshot,
        NormalActionPlanPayload plan)
    {
        var slot = snapshot.LogisticsStation?.StorageSlots.FirstOrDefault(candidate =>
            candidate.Index == plan.ConfigureStationStorageIndex);
        return slot is not null
               && slot.ItemId == plan.ConfigureStationItemId
               && slot.MaximumCount == plan.ConfigureStationMaximumCount
               && string.Equals(slot.LocalLogic, plan.ConfigureStationLocalLogic, StringComparison.OrdinalIgnoreCase)
               && string.Equals(slot.RemoteLogic, plan.ConfigureStationRemoteLogic, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLogisticsStationChargeConfigurationApplied(
        FactoryEntitySnapshot snapshot,
        NormalActionPlanPayload plan) => snapshot.LogisticsStation is not null
            && snapshot.LogisticsStation.MaximumChargePowerWatts == plan.ConfigureStationMaximumChargePowerWatts;

    private static bool IsLogisticsStationBeltConfigurationApplied(
        FactoryEntitySnapshot snapshot,
        NormalActionPlanPayload plan)
    {
        var station = snapshot.LogisticsStation;
        var beltSlot = station?.BeltSlots.FirstOrDefault(candidate =>
            candidate.Index == plan.ConfigureStationBeltSlotIndex);
        var storageSlot = station?.StorageSlots.FirstOrDefault(candidate =>
            candidate.Index == plan.ConfigureStationBeltStorageIndex);
        return beltSlot is not null
               && storageSlot is not null
               && string.Equals(beltSlot.Direction, IODir.Output.ToString(), StringComparison.Ordinal)
               && beltSlot.BeltComponentId > 0
               && beltSlot.BeltEntityId > 0
               && beltSlot.StorageIndex == plan.ConfigureStationBeltStorageIndex + 1
               && storageSlot.ItemId == plan.ConfigureStationBeltItemId;
    }

    private static string CaptureLogisticsStationBeltSelectorInvariantState(
        StationComponent station,
        int targetBeltSlotIndex)
    {
        var values = new List<object?>
        {
            station.id,
            station.gid,
            station.entityId,
            station.planetId,
            station.isStellar,
            station.isCollector,
            station.isVeinCollector,
            station.energy,
            station.energyPerTick,
            station.energyMax,
            station.warperCount,
            station.idleDroneCount,
            station.workDroneCount,
            station.idleShipCount,
            station.workShipCount,
            station.tripRangeDrones,
            station.tripRangeShips,
            station.includeOrbitCollector,
            station.warpEnableDist,
            station.warperNecessary,
            station.deliveryDrones,
            station.deliveryShips,
            station.pilerCount,
            station.droneAutoReplenish,
            station.shipAutoReplenish,
            station.remoteGroupMask,
            station.routePriority,
        };
        var needs = station.needs ?? Array.Empty<int>();
        values.Add(needs.Length);
        foreach (var itemId in needs)
        {
            values.Add(itemId);
        }

        var slots = station.slots ?? Array.Empty<SlotData>();
        values.Add(slots.Length);
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            values.Add(index);
            values.Add(slot.dir);
            values.Add(slot.beltId);
            values.Add(index == targetBeltSlotIndex ? 0 : slot.storageIdx);
            values.Add(slot.counter);
        }

        return CanonicalStateHash.Combine("logistics-station-belt-selector-invariant-v1", values.ToArray());
    }

    private static string CaptureLogisticsStationStorageState(StationComponent station)
    {
        var stores = station.storage ?? Array.Empty<StationStore>();
        var values = new List<object?>
        {
            station.id,
            station.entityId,
            station.planetId,
            stores.Length,
        };
        for (var index = 0; index < stores.Length; index++)
        {
            var store = stores[index];
            values.Add(index);
            values.Add(store.itemId);
            values.Add(store.count);
            values.Add(store.inc);
            values.Add(store.max);
            values.Add(store.localOrder);
            values.Add(store.remoteOrder);
            values.Add(store.totalOrdered);
            values.Add(store.localSupplyCount);
            values.Add(store.localDemandCount);
            values.Add(store.remoteSupplyCount);
            values.Add(store.remoteDemandCount);
            values.Add(store.localLogic);
            values.Add(store.remoteLogic);
            values.Add(store.keepMode);
            values.Add(store.keepIncRatio);
        }

        return CanonicalStateHash.Combine("logistics-station-storage-runtime-v1", values.ToArray());
    }

    private string? CaptureStructuredAfterStateHash(ActionRecord action)
    {
        if (action.ActionKind == NormalActionKinds.ConfigureBuilding)
        {
            var snapshot = _reader.InspectFactoryEntityOnMainThread(
                action.SessionId,
                new InspectFactoryEntityRequest { PlanetId = action.PlanetId, ObjectId = action.Plan.EntityId });
            return action.Plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationStorage
                   || action.Plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationBelt
                   || action.Plan.ConfigureMode == BuildingConfigurationModes.LogisticsStationCharge
                ? snapshot.Value?.LogisticsStation?.ConfigurationStateHash
                : snapshot.Value?.StateHash;
        }

        if (action.ActionKind == NormalActionKinds.Build && action.TargetObjectIds.Count > 0)
        {
            var hashes = new List<object?> { action.Plan.BuildKind, action.TargetObjectIds.Count };
            foreach (var objectId in action.TargetObjectIds)
            {
                var snapshot = _reader.InspectFactoryEntityOnMainThread(
                    action.SessionId,
                    new InspectFactoryEntityRequest { PlanetId = action.PlanetId, ObjectId = objectId });
                hashes.Add(snapshot.Value?.StateHash);
            }

            return CanonicalStateHash.Combine(NormalActionKinds.Build, hashes.ToArray());
        }

        return null;
    }

    private static bool CanTransferExactly(
        StorageComponent playerPackage,
        StorageComponent storage,
        string direction,
        int itemId,
        int count,
        out string rejection)
    {
        rejection = string.Empty;
        var source = direction == TransferDirections.PlayerToStorage ? playerPackage : storage;
        var destination = direction == TransferDirections.PlayerToStorage ? storage : playerPackage;
        using var sourceCopy = new StorageCopy(source);
        using var destinationCopy = new StorageCopy(destination);
        var removed = sourceCopy.Value.TakeItem(itemId, count, out var inc);
        if (removed != count)
        {
            rejection = $"The exact transfer source contains fewer than {count} of item {itemId}.";
            return false;
        }

        var added = destinationCopy.Value.AddItemStacked(itemId, count, inc, out var remainingInc);
        if (added != count || remainingInc != 0)
        {
            rejection = $"The exact transfer destination cannot accept {count} of item {itemId}.";
            return false;
        }

        return true;
    }

    private static bool TryGetStorage(PlanetFactory factory, int entityId, out StorageComponent? storage)
    {
        storage = null;
        if (entityId <= 0 || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.storageId <= 0
            || entity.storageId >= factory.factoryStorage.storageCursor
            || entity.storageId >= factory.factoryStorage.storagePool.Length)
        {
            return false;
        }

        storage = factory.factoryStorage.storagePool[entity.storageId];
        return storage is not null && storage.id == entity.storageId && storage.entityId == entityId;
    }

    private static List<EndpointPoint> GetFreePortPoints(
        PlanetFactory factory,
        FactoryEntitySnapshot snapshot,
        bool requireOutput)
    {
        var result = new List<EndpointPoint>();
        if (snapshot.ObjectId <= 0 || snapshot.ObjectId >= factory.entityCursor
            || snapshot.ObjectId >= factory.entityPool.Length
            || factory.entityPool[snapshot.ObjectId].id != snapshot.ObjectId)
        {
            return result;
        }

        ref var entity = ref factory.entityPool[snapshot.ObjectId];
        var item = LDB.items.Select(entity.protoId);
        if (item?.prefabDesc?.isBelt == true)
        {
            var slot = requireOutput ? 0 : 1;
            factory.ReadObjectConn(snapshot.ObjectId, slot, out _, out var otherObjectId, out _);
            if (otherObjectId == 0)
            {
                var rotation = Quaternion.AngleAxis(entity.tilt, entity.rot * Vector3.forward) * entity.rot;
                if (!requireOutput)
                {
                    rotation *= Quaternion.Euler(0f, 180f, 0f);
                }

                result.Add(new EndpointPoint(snapshot.ObjectId, slot, new Pose(entity.pos, rotation)));
            }

            return result;
        }

        var ports = item?.prefabDesc?.portPoses ?? Array.Empty<Pose>();
        for (var index = 0; index < ports.Length; index++)
        {
            factory.ReadObjectConn(snapshot.ObjectId, index, out _, out var otherObjectId, out _);
            if (otherObjectId != 0)
            {
                continue;
            }

            var pose = ports[index].GetTransformedBy(new Pose(entity.pos, entity.rot));
            result.Add(new EndpointPoint(snapshot.ObjectId, index, pose));
        }

        return result;
    }

    private static List<EndpointPoint> GetInserterEndpointPoints(
        PlanetFactory factory,
        FactoryEntitySnapshot snapshot)
    {
        var result = new List<EndpointPoint>();
        if (snapshot.ObjectId <= 0 || snapshot.ObjectId >= factory.entityCursor
            || snapshot.ObjectId >= factory.entityPool.Length
            || factory.entityPool[snapshot.ObjectId].id != snapshot.ObjectId)
        {
            return result;
        }

        ref var entity = ref factory.entityPool[snapshot.ObjectId];
        var item = LDB.items.Select(entity.protoId);
        if (item?.prefabDesc is null)
        {
            return result;
        }

        if (item.prefabDesc.isBelt)
        {
            var baseRotation = Quaternion.AngleAxis(entity.tilt, entity.rot * Vector3.forward) * entity.rot;
            var beltSlots = new[]
            {
                Quaternion.identity,
                Quaternion.Euler(0f, 90f, 0f),
                Quaternion.Euler(0f, 180f, 0f),
                Quaternion.Euler(0f, -90f, 0f),
            };
            foreach (var slotRotation in beltSlots)
            {
                result.Add(new EndpointPoint(
                    snapshot.ObjectId,
                    -1,
                    new Pose(entity.pos, baseRotation * slotRotation)));
            }

            return result;
        }

        var slots = item.prefabDesc.slotPoses ?? Array.Empty<Pose>();
        if (slots.Length > 16 || factory.entityConnPool is null
            || (long)snapshot.ObjectId * 16 + slots.Length > factory.entityConnPool.Length) return result;
        var occupiedSlots = new List<int>();
        for (var index = 0; index < slots.Length; index++)
        {
            factory.ReadObjectConn(snapshot.ObjectId, index, out _, out var otherObjectId, out _);
            if (otherObjectId != 0)
            {
                occupiedSlots.Add(index);
            }
        }

        foreach (var index in BuildConnectionSlots.SelectAvailable(slots.Length, occupiedSlots))
        {
            var transformed = slots[index].GetTransformedBy(new Pose(entity.pos, entity.rot));
            result.Add(new EndpointPoint(snapshot.ObjectId, index, transformed));
        }

        return result;
    }

    private static string BuildEndpointHash(FactoryEntitySnapshot snapshot) =>
        CanonicalStateHash.FactoryEndpoint(snapshot);

    private static string BuildPlanFingerprint(BuildPreparation preparation)
    {
        var fields = new List<object?>
        {
            preparation.Kind,
            preparation.ResourceNodeId,
            preparation.SourceObjectId,
            preparation.DestinationObjectId,
            preparation.Steps.Count,
        };
        foreach (var step in preparation.Steps)
        {
            step.AppendFingerprint(fields);
        }

        return BeltPathRoutingPolicy.BindGeometry(preparation.Steps[0].BeltPathMode,
            CanonicalStateHash.Combine("build-steps", fields.ToArray()));
    }

    private static bool BuildStepsEqual(
        IReadOnlyList<BuildStepPlan> expected,
        IReadOnlyList<BuildStepPlan> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (!expected[index].EquivalentTo(actual[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PreviewsExactlyMatch(IReadOnlyList<BuildStepPlan> plan, IReadOnlyList<BuildPreview> previews)
    {
        if (plan.Count != previews.Count)
        {
            return false;
        }

        for (var index = 0; index < previews.Count; index++)
        {
            if (previews[index].condition != EBuildCondition.Ok || previews[index].coverObjId != 0 || previews[index].willRemoveCover
                || !plan[index].EquivalentTo(BuildStepPlan.FromPreview(plan[index], previews[index])))
            {
                return false;
            }
        }

        return true;
    }

    private static List<BuildPreview> CreateLinkedPreviews(IReadOnlyList<BuildStepPlan> steps, ItemProto item)
    {
        var previews = steps.Select(step => CreatePreview(step, item)).ToList();
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index].InputStepIndex >= 0)
            {
                previews[index].input = previews[steps[index].InputStepIndex];
            }

            if (steps[index].OutputStepIndex >= 0)
            {
                previews[index].output = previews[steps[index].OutputStepIndex];
            }
        }

        return previews;
    }

    private static BuildPreview CreatePreview(BuildStepPlan step, ItemProto item)
    {
        var preview = new BuildPreview
        {
            item = item,
            desc = item.prefabDesc,
            lpos = step.Position,
            lpos2 = step.Position2,
            lrot = step.Rotation,
            lrot2 = step.Rotation2,
            tilt = step.Tilt,
            filterId = step.FilterItemId,
            inputObjId = step.InputObjectId,
            outputObjId = step.OutputObjectId,
            inputFromSlot = step.InputFromSlot,
            inputToSlot = step.InputToSlot,
            outputFromSlot = step.OutputFromSlot,
            outputToSlot = step.OutputToSlot,
            inputOffset = step.InputOffset,
            outputOffset = step.OutputOffset,
            condition = EBuildCondition.Ok,
            isConnNode = step.IsConnectionNode,
            needModel = false,
        };
        if (step.Parameters.Count > 0)
        {
            preview.parameters = step.Parameters.ToArray();
            preview.paramCount = preview.parameters.Length;
        }

        return preview;
    }

    private static bool BuildUiIsIdle(Player player)
    {
        var build = player.controller?.actionBuild;
        return build is not null
               && !build.active
               && build.templatePreviews.Count == 0
               && build.clickTool.buildPreviews.Count == 0
               && build.pathTool.buildPreviews.Count == 0
               && build.inserterTool.buildPreviews.Count == 0;
    }

    private static int FindBuiltEntityExcluding(
        PlanetFactory factory,
        int itemId,
        Vector3 position,
        IReadOnlyCollection<int> preexistingEntityIds,
        IReadOnlyCollection<int> alreadySelectedEntityIds)
    {
        var candidates = new List<BuildEntityCandidate>();
        var limit = Math.Min(factory.entityCursor, factory.entityPool.Length);
        for (var entityId = 1; entityId < limit; entityId++)
        {
            ref var entity = ref factory.entityPool[entityId];
            if (entity.id != entityId || entity.protoId != itemId)
            {
                continue;
            }

            var distance = (entity.pos - position).sqrMagnitude;
            if (distance < 0.09f)
            {
                candidates.Add(new BuildEntityCandidate(entityId, distance));
            }
        }

        return BuildEntityAttribution.SelectNearestNewCandidate(
            candidates,
            preexistingEntityIds,
            alreadySelectedEntityIds,
            0.09f);
    }

    private static void CaptureBuiltEntityIds(
        PlanetFactory factory,
        int itemId,
        Vector3 position,
        ISet<int> destination)
    {
        var limit = Math.Min(factory.entityCursor, factory.entityPool.Length);
        for (var entityId = 1; entityId < limit; entityId++)
        {
            ref var entity = ref factory.entityPool[entityId];
            if (entity.id == entityId
                && entity.protoId == itemId
                && (entity.pos - position).sqrMagnitude < 0.09f)
            {
                destination.Add(entityId);
            }
        }
    }

    private static Vector3 ProjectTangent(Vector3 direction, Vector3 surfacePosition)
    {
        var normal = surfacePosition.normalized;
        return direction - normal * Vector3.Dot(direction, normal);
    }

    private static Vector3Snapshot Snapshot(Vector3 value) => new Vector3Snapshot
    {
        X = value.x,
        Y = value.y,
        Z = value.z,
    };

    private readonly struct WorldBuildCollider
    {
        public WorldBuildCollider(
            Vector3 center,
            Vector3 axisX,
            Vector3 axisY,
            Vector3 axisZ,
            Vector3 extents)
        {
            Center = center;
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
            Extents = extents;
        }

        public Vector3 Center { get; }

        public Vector3 AxisX { get; }

        public Vector3 AxisY { get; }

        public Vector3 AxisZ { get; }

        public Vector3 Extents { get; }
    }

    private sealed class StorageCopy : IDisposable
    {
        public StorageCopy(StorageComponent source)
        {
            Value = new StorageComponent(source.size);
            Array.Copy(source.grids, Value.grids, Math.Min(source.size, source.grids.Length));
            Value.type = source.type;
            Value.bans = source.bans;
            Value.isPlayerInventory = source.isPlayerInventory;
        }

        public StorageComponent Value { get; }

        public void Dispose() => Value.Free();
    }

    private sealed class BuildPreparation
    {
        public bool Success { get; private set; }
        public string ErrorCode { get; private set; } = BridgeErrorCodes.BuildLocationInvalid;
        public string Rejection { get; private set; } = string.Empty;
        public string? Recovery { get; private set; }
        public string Kind { get; private set; } = string.Empty;
        public List<BuildStepPlan> Steps { get; } = new List<BuildStepPlan>();
        public int ResourceNodeId { get; set; }
        public string ResourceStateHash { get; set; } = string.Empty;
        public List<int> ResourceNodeIds { get; } = new List<int>();
        public int SourceObjectId { get; set; }
        public string SourceEndpointHash { get; set; } = string.Empty;
        public int DestinationObjectId { get; set; }
        public string DestinationEndpointHash { get; set; } = string.Empty;

        public static BuildPreparation Succeeded(string kind, IEnumerable<BuildStepPlan> steps)
        {
            var result = new BuildPreparation { Success = true, Kind = kind };
            result.Steps.AddRange(steps);
            return result;
        }

        public static BuildPreparation Failed(string code, string rejection, string? recovery = null) => new BuildPreparation
        {
            ErrorCode = code,
            Rejection = rejection,
            Recovery = recovery,
        };
    }

    private sealed class BuildStepPlan
    {
        public int ItemId { get; private set; }
        public int FilterItemId { get; set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Vector3 Position2 { get; private set; }
        public Quaternion Rotation2 { get; private set; }
        public float Yaw { get; private set; }
        public float Tilt { get; private set; }
        public int InputStepIndex { get; set; } = -1;
        public int OutputStepIndex { get; set; } = -1;
        public int InputObjectId { get; set; }
        public int OutputObjectId { get; set; }
        public int InputFromSlot { get; set; }
        public int InputToSlot { get; set; }
        public int OutputFromSlot { get; set; }
        public int OutputToSlot { get; set; }
        public int InputOffset { get; set; }
        public int OutputOffset { get; set; }
        public int AttachmentBeltObjectId { get; set; }
        public string? AttachmentGeometryHash { get; set; }
        public string? AttachmentDestinationGeometryHash { get; set; }
        public BeltSourceState? SourceBeltAnchor { get; set; }
        public BeltDestinationState? DestinationBeltAnchor { get; set; }
        public string BeltPathMode { get; set; } = BeltPathModes.NativeGrid;
        public bool IsConnectionNode { get; private set; }
        public List<int> Parameters { get; } = new List<int>();

        public static BuildStepPlan Core(int itemId, Vector3 position, Quaternion rotation, float yaw) => new BuildStepPlan
        {
            ItemId = itemId,
            Position = position,
            Position2 = position,
            Rotation = rotation,
            Rotation2 = rotation,
            Yaw = yaw,
        };

        public static BuildStepPlan Belt(int itemId, Vector3 position) => new BuildStepPlan
        {
            ItemId = itemId,
            Position = position,
            Position2 = position,
            Rotation = Maths.SphericalRotation(position, 0f),
            Rotation2 = Maths.SphericalRotation(position, 0f),
            IsConnectionNode = true,
            InputToSlot = 1,
            OutputFromSlot = 0,
            OutputToSlot = 1,
        };

        public static BuildStepPlan Inserter(
            int itemId,
            Pose source,
            Pose destination,
            int sourceObjectId,
            int sourceSlot,
            int destinationObjectId,
            int destinationSlot) => new BuildStepPlan
            {
                ItemId = itemId,
                Position = source.position,
                Rotation = source.rotation,
                Position2 = destination.position,
                Rotation2 = destination.rotation * Quaternion.Euler(0f, 180f, 0f),
                InputObjectId = sourceObjectId,
                InputFromSlot = sourceSlot,
                InputToSlot = 1,
                OutputObjectId = destinationObjectId,
                OutputFromSlot = 0,
                OutputToSlot = destinationSlot,
            };

        public static BuildStepPlan FromPreview(BuildStepPlan template, BuildPreview preview)
        {
            var result = new BuildStepPlan
            {
                ItemId = template.ItemId,
                FilterItemId = preview.desc.isInserter ? preview.filterId : 0,
                Position = preview.lpos,
                Rotation = preview.lrot,
                Position2 = preview.lpos2,
                Rotation2 = preview.lrot2,
                Yaw = template.Yaw,
                Tilt = preview.tilt,
                InputStepIndex = template.InputStepIndex,
                OutputStepIndex = template.OutputStepIndex,
                InputObjectId = preview.inputObjId,
                OutputObjectId = template.DestinationBeltAnchor is null ? preview.outputObjId : template.OutputObjectId,
                InputFromSlot = preview.inputFromSlot,
                InputToSlot = preview.inputToSlot,
                OutputFromSlot = preview.outputFromSlot,
                OutputToSlot = preview.outputToSlot,
                InputOffset = preview.inputOffset,
                OutputOffset = preview.outputOffset,
                AttachmentBeltObjectId = template.AttachmentBeltObjectId,
                AttachmentGeometryHash = template.AttachmentGeometryHash,
                AttachmentDestinationGeometryHash = template.AttachmentDestinationGeometryHash,
                SourceBeltAnchor = template.SourceBeltAnchor,
                DestinationBeltAnchor = template.DestinationBeltAnchor,
                BeltPathMode = template.BeltPathMode,
                IsConnectionNode = preview.isConnNode,
            };
            if (preview.parameters is not null && preview.paramCount > 0)
            {
                result.Parameters.AddRange(preview.parameters.Take(Math.Min(preview.paramCount, preview.parameters.Length)));
            }

            return result;
        }

        public bool EquivalentTo(BuildStepPlan other)
        {
            return ItemId == other.ItemId
                   && FilterItemId == other.FilterItemId
                   && Vector3.Distance(Position, other.Position) <= 0.01f
                   && Vector3.Distance(Position2, other.Position2) <= 0.01f
                   && Quaternion.Angle(Rotation, other.Rotation) <= 0.1f
                   && Quaternion.Angle(Rotation2, other.Rotation2) <= 0.1f
                   && Math.Abs(Tilt - other.Tilt) <= 0.01f
                   && InputStepIndex == other.InputStepIndex
                   && OutputStepIndex == other.OutputStepIndex
                   && InputObjectId == other.InputObjectId
                   && OutputObjectId == other.OutputObjectId
                   && InputFromSlot == other.InputFromSlot
                   && InputToSlot == other.InputToSlot
                   && OutputFromSlot == other.OutputFromSlot
                   && OutputToSlot == other.OutputToSlot
                   && InputOffset == other.InputOffset
                   && OutputOffset == other.OutputOffset
                   && AttachmentBeltObjectId == other.AttachmentBeltObjectId
                   && string.Equals(AttachmentGeometryHash, other.AttachmentGeometryHash, StringComparison.Ordinal)
                   && string.Equals(AttachmentDestinationGeometryHash, other.AttachmentDestinationGeometryHash, StringComparison.Ordinal)
                   && string.Equals(SourceBeltAnchor?.BindingHash, other.SourceBeltAnchor?.BindingHash, StringComparison.Ordinal)
                   && string.Equals(DestinationBeltAnchor?.BindingHash, other.DestinationBeltAnchor?.BindingHash, StringComparison.Ordinal)
                   && string.Equals(BeltPathMode, other.BeltPathMode, StringComparison.Ordinal)
                   && IsConnectionNode == other.IsConnectionNode
                   && Parameters.SequenceEqual(other.Parameters);
        }

        public void AppendFingerprint(ICollection<object?> fields)
        {
            fields.Add(ItemId);
            fields.Add(FilterItemId);
            fields.Add(Position.x);
            fields.Add(Position.y);
            fields.Add(Position.z);
            fields.Add(Rotation.x);
            fields.Add(Rotation.y);
            fields.Add(Rotation.z);
            fields.Add(Rotation.w);
            fields.Add(Position2.x);
            fields.Add(Position2.y);
            fields.Add(Position2.z);
            fields.Add(Rotation2.x);
            fields.Add(Rotation2.y);
            fields.Add(Rotation2.z);
            fields.Add(Rotation2.w);
            fields.Add(Tilt);
            fields.Add(InputStepIndex);
            fields.Add(OutputStepIndex);
            fields.Add(InputObjectId);
            fields.Add(OutputObjectId);
            fields.Add(InputFromSlot);
            fields.Add(InputToSlot);
            fields.Add(OutputFromSlot);
            fields.Add(OutputToSlot);
            fields.Add(AttachmentBeltObjectId);
            fields.Add(SourceBeltAnchor?.BindingHash);
            if (DestinationBeltAnchor is not null)
            {
                fields.Add("belt-destination-binding-v1");
                fields.Add(DestinationBeltAnchor.BindingHash);
            }
            fields.Add(InserterBeltAttachmentPolicy.BindOffsets(InputOffset, OutputOffset,
                AttachmentDestinationGeometryHash is null ? AttachmentGeometryHash
                    : InserterBeltAttachmentPolicy.BindGeometryPair(AttachmentGeometryHash!, AttachmentDestinationGeometryHash)));
            fields.Add(IsConnectionNode);
            fields.Add(Parameters.Count);
            foreach (var parameter in Parameters)
            {
                fields.Add(parameter);
            }
        }

        public void ApplyNativeBeltTilt(PlanetFactory factory, bool sourceIsBelt)
        {
            var entity = factory.entityPool[sourceIsBelt ? InputObjectId : OutputObjectId];
            var forward = entity.rot * Vector3.forward;
            var nativeUp = Quaternion.AngleAxis(entity.tilt, forward) * (entity.rot * Vector3.up);
            var rotation = sourceIsBelt ? Rotation : Rotation2;
            var previewUp = rotation * Vector3.up;
            var angle = Vector3.Angle(previewUp, nativeUp);
            if (Vector3.Dot(Vector3.Cross(nativeUp, forward), previewUp) <= 0) angle = -angle;
            rotation = Quaternion.AngleAxis(angle, forward) * rotation;
            if (sourceIsBelt) Rotation = rotation;
            else Rotation2 = rotation;
        }
    }

    private sealed class BuildExpectedEntity
    {
        public int ItemId { get; set; }
        public Vector3 Position { get; set; }
        public int InputObjectId { get; set; }
        public int OutputObjectId { get; set; }
        public int InputStepIndex { get; set; }
        public int OutputStepIndex { get; set; }
    }

    private readonly struct EndpointPoint
    {
        public EndpointPoint(int objectId, int slot, Pose pose)
        {
            ObjectId = objectId;
            Slot = slot;
            Pose = pose;
        }

        public int ObjectId { get; }
        public int Slot { get; }
        public Pose Pose { get; }
    }

    private sealed partial class NormalActionPlanPayload
    {
        public static NormalActionPlanPayload StructuredBuild(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            int buildingItemId,
            BuildPreparation preparation)
        {
            var result = new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Build,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                BuildingItemId = buildingItemId,
                BuildKind = preparation.Kind,
                BuildResourceNodeId = preparation.ResourceNodeId,
                BuildResourceStateHash = preparation.ResourceStateHash,
                SourceObjectId = preparation.SourceObjectId,
                SourceFactoryStateHash = preparation.SourceEndpointHash,
                DestinationObjectId = preparation.DestinationObjectId,
                DestinationFactoryStateHash = preparation.DestinationEndpointHash,
                Count = preparation.Steps.Count,
                EstimatedTicks = Math.Max(3600, preparation.Steps.Count * 900L),
            };
            result.BuildSteps.AddRange(preparation.Steps);
            return result;
        }

        public static NormalActionPlanPayload Transfer(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            string storageStateHash,
            int storageEntityId,
            string direction,
            int itemId,
            int count) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Transfer,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                TransferStorageStateHash = storageStateHash,
                TransferStorageEntityId = storageEntityId,
                TransferDirection = direction,
                TransferItemId = itemId,
                Count = count,
                EstimatedTicks = 1,
            };
    }
}
