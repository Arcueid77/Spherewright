using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class BeltRoutePreviewPolicyTests
{
    private const string GeometryHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void AllowsTwelveContinuousBoundedSegmentsAndTheFullPolylineLimit()
    {
        var request = Request();
        request.BeltRoutePreview!.Segments.Clear();
        for (var i = 0; i < 12; i++)
        {
            request.BeltRoutePreview.Segments.Add(new BeltRouteSegment
            {
                RoutingMode = BeltPathModes.NativeGrid,
                Start = Point(i * 20, 200, 0),
                End = Point((i + 1) * 20, 200, 0),
                StartAltitudeLevel = 0,
                EndAltitudeLevel = 0,
            });
        }

        Assert.Null(BeltRoutePreviewPolicy.ValidateRequest(request));
    }

    [Fact]
    public void AllowsTheInclusivePerSegmentLengthBoundaries()
    {
        var request = Request();
        request.BeltRoutePreview!.Segments = new() { Segment(2, 3.5) };
        Assert.Null(BeltRoutePreviewPolicy.ValidateRequest(request));
        request.BeltRoutePreview.Segments = new() { Segment(2, 32) };
        Assert.Null(BeltRoutePreviewPolicy.ValidateRequest(request));
    }

    [Fact]
    public void AcceptsContinuousExplicitElevatedLayers()
    {
        var request = Request();
        var segments = request.BeltRoutePreview!.Segments;
        segments[0].RoutingMode = BeltPathModes.NativeElevatedGrid;
        segments[0].StartAltitudeLevel = 0;
        segments[0].EndAltitudeLevel = 1;
        segments[1].RoutingMode = BeltPathModes.NativeElevatedGrid;
        segments[1].StartAltitudeLevel = 1;
        segments[1].EndAltitudeLevel = 0;

        Assert.Null(BeltRoutePreviewPolicy.ValidateRequest(request));
    }

    [Theory]
    [InlineData("too_many_segments")]
    [InlineData("over_total_length")]
    [InlineData("span_under_minimum")]
    [InlineData("span_over_maximum")]
    [InlineData("disconnected_join")]
    [InlineData("join_level_mismatch")]
    [InlineData("legacy_endpoint_preview")]
    [InlineData("legacy_preferred_position")]
    [InlineData("legacy_path_end")]
    [InlineData("legacy_preferred_yaw")]
    [InlineData("legacy_preferred_distance")]
    [InlineData("legacy_path_length")]
    [InlineData("legacy_source")]
    [InlineData("legacy_destination")]
    [InlineData("legacy_resource")]
    [InlineData("legacy_source_hash")]
    [InlineData("legacy_destination_hash")]
    [InlineData("legacy_resource_hash")]
    [InlineData("legacy_altitude")]
    [InlineData("legacy_initial_filter")]
    [InlineData("wrong_mode")]
    [InlineData("wrong_building")]
    [InlineData("wrong_state_hash_version")]
    [InlineData("missing_player_hash")]
    public void RejectsOutOfBoundsOrMixedLegacyRouteRequests(string fault)
    {
        var request = Request();
        switch (fault)
        {
            case "too_many_segments":
                request.BeltRoutePreview!.Segments = Enumerable.Range(0, 13).Select(i => Segment(i, i + 1)).ToList();
                break;
            case "over_total_length":
                request.BeltRoutePreview!.Segments = Enumerable.Range(0, 9).Select(i => Segment(i * 30, (i + 1) * 30)).ToList();
                break;
            case "span_under_minimum": request.BeltRoutePreview!.Segments[0] = Segment(2, 3.49); break;
            case "span_over_maximum": request.BeltRoutePreview!.Segments[0] = Segment(2, 32.01); break;
            case "disconnected_join": request.BeltRoutePreview!.Segments[1].Start = Point(9, 200, 0); break;
            case "join_level_mismatch":
                request.BeltRoutePreview!.Segments[0].RoutingMode = BeltPathModes.NativeElevatedGrid;
                request.BeltRoutePreview.Segments[0].StartAltitudeLevel = 0;
                request.BeltRoutePreview.Segments[0].EndAltitudeLevel = 1;
                request.BeltRoutePreview.Segments[1].RoutingMode = BeltPathModes.NativeElevatedGrid;
                request.BeltRoutePreview.Segments[1].StartAltitudeLevel = 0;
                request.BeltRoutePreview.Segments[1].EndAltitudeLevel = 1;
                break;
            case "legacy_endpoint_preview": request.BeltEndpointPreview = new BeltEndpointPreviewRequest(); break;
            case "legacy_preferred_position": request.PreferredPosition = Point(1, 200, 0); break;
            case "legacy_path_end": request.PathEnd = Point(10, 200, 0); break;
            case "legacy_preferred_yaw": request.PreferredYaw = 0f; break;
            case "legacy_preferred_distance": request.PreferredDistance = 8f; break;
            case "legacy_path_length": request.PathLength = 8f; break;
            case "legacy_source": request.SourceObjectId = 41; break;
            case "legacy_destination": request.DestinationObjectId = 42; break;
            case "legacy_resource": request.ResourceNodeId = 77; break;
            case "legacy_source_hash": request.ExpectedSourceStateHash = "source"; break;
            case "legacy_destination_hash": request.ExpectedDestinationStateHash = "destination"; break;
            case "legacy_resource_hash": request.ExpectedResourceStateHash = "resource"; break;
            case "legacy_altitude": request.BeltStartAltitudeLevel = 0; break;
            case "legacy_initial_filter": request.InitialSorterFilterItemId = 1001; break;
            case "wrong_mode": request.BeltPathMode = BeltPathModes.NativeGrid; break;
            case "wrong_building": request.BuildingItemId = 2011; break;
            case "wrong_state_hash_version": request.StateHashVersion = 2; break;
            case "missing_player_hash": request.ExpectedPlayerStateHash = " "; break;
        }

        Assert.NotNull(BeltRoutePreviewPolicy.ValidateRequest(request));
    }

    [Theory]
    [InlineData("bad_geometry_hash")]
    [InlineData("bad_filter")]
    [InlineData("bad_source_sorter")]
    [InlineData("bad_destination_sorter")]
    [InlineData("same_endpoint")]
    [InlineData("elevated_missing_level")]
    [InlineData("unknown_routing_mode")]
    public void RejectsInvalidBindingsOrSegmentDefinitions(string fault)
    {
        var request = Request();
        switch (fault)
        {
            case "bad_geometry_hash": request.BeltRoutePreview!.ExpectedFactoryGeometryHash = "short"; break;
            case "bad_filter": request.BeltRoutePreview!.FilterItemId = 0; break;
            case "bad_source_sorter": request.BeltRoutePreview!.SourceSorterItemId = 2013; break;
            case "bad_destination_sorter": request.BeltRoutePreview!.DestinationSorterItemId = 0; break;
            case "same_endpoint": request.BeltRoutePreview!.Destination.ExistingObjectId = request.BeltRoutePreview.Source.ExistingObjectId; break;
            case "elevated_missing_level": request.BeltRoutePreview!.Segments[0].RoutingMode = BeltPathModes.NativeElevatedGrid; break;
            case "unknown_routing_mode": request.BeltRoutePreview!.Segments[0].RoutingMode = "automatic"; break;
        }

        Assert.NotNull(BeltRoutePreviewPolicy.ValidateRequest(request));
    }

    [Fact]
    public void ConfirmsExactReadOnlyRouteEchoWithBothSorterKindsAndDeduplicatedBudget()
    {
        var request = Request();
        var result = SuccessfulResult(request);

        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
        Assert.False(result.Prepared);
        Assert.False(result.CommitAllowedNow);
        Assert.Empty(result.PlanToken);
        Assert.False(result.BeltRoutePreview!.Executable);
        Assert.Equal(new[] { 2001, 2011, 2012 }, result.ItemBudget.Select(row => row.ItemId).OrderBy(id => id));
        Assert.Equal(5, result.ItemBudget.Single(row => row.ItemId == 2001).Count);
        Assert.Equal(1, result.ItemBudget.Single(row => row.ItemId == 2011).Count);
        Assert.Equal(1, result.ItemBudget.Single(row => row.ItemId == 2012).Count);
        Assert.All(result.ItemBudget, row => Assert.Equal("preview-construction-consumption", row.Direction));
    }

    [Fact]
    public void RejectsOldV1RoutePreviewEcho()
    {
        var request = Request();
        var result = SuccessfulResult(request);
        result.BeltRoutePreview!.Phase = "read_only_bounded_belt_route_preview_v1";

        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
    }

    [Fact]
    public void DeduplicatesBudgetWhenBothEndpointsUseTheSameSorterItem()
    {
        var request = Request();
        request.BeltRoutePreview!.DestinationSorterItemId = request.BeltRoutePreview.SourceSorterItemId;
        var result = SuccessfulResult(request);

        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
        Assert.Equal(2, result.ItemBudget.Count);
        Assert.Equal(2, result.ItemBudget.Single(row => row.ItemId == 2011).Count);
        Assert.Equal(5, result.ItemBudget.Single(row => row.ItemId == 2001).Count);
    }

    [Fact]
    public void EchoesEveryRequestedSegmentEndpointAtItsNativePointRange()
    {
        var request = Request();
        var result = SuccessfulResult(request);
        result.BeltRoutePreview!.Points[2].Position = Point(8.5f, 200, 0);
        result.PlannedPath[2] = Point(8.5f, 200, 0);

        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
    }

    [Fact]
    public void AcceptsNativeRejectedAdjustedPositionWhilePreservingPlannedAndObservedPoints()
    {
        var request = Request();
        var result = SuccessfulResult(request);
        var route = result.BeltRoutePreview!;
        route.NativePathCheckPassed = false;
        route.JointNativeCheckPerformed = false;
        route.JointNativeCheckPassed = false;
        route.FailedBeltIndex = 2;
        route.FailureStage = "native_full_path";
        route.FailureCondition = "Collision";
        route.Points[2].NativeCondition = "Collision";
        route.Points[2].Position = Point(8.25f, 200, 0);
        route.Endpoints.NativeCheckPerformed = false;
        route.Endpoints.NativeCheckPassed = false;
        route.Endpoints.Attachments.Clear();
        route.Endpoints.Blockers.Add("native_route_collision");

        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
        Assert.Equal((8f, 200f, 0f), Coordinates(route.Points[2].PlannedPosition));
        Assert.Equal((8.25f, 200f, 0f), Coordinates(route.Points[2].Position));
        Assert.Equal((8f, 200f, 0f), Coordinates(result.PlannedPath[2]));
        Assert.Equal("native_full_path", route.FailureStage);
        Assert.Equal(2, route.FailedBeltIndex);
        Assert.Empty(route.Endpoints.Attachments);
        Assert.Empty(result.PlanToken);
        Assert.False(result.Prepared);
        Assert.False(result.CommitAllowedNow);
    }

    [Fact]
    public void AcceptsSegmentEndpointsCopiedFromPriorGeometryGridSnapReadback()
    {
        var request = Request();
        var priorGroundGridSnaps = new[]
        {
            Point(2, 200, 0), Point(8, 200, 0), Point(14, 200, 0),
        };
        request.BeltRoutePreview!.Segments = new()
        {
            Segment(priorGroundGridSnaps[0], priorGroundGridSnaps[1]),
            Segment(priorGroundGridSnaps[1], priorGroundGridSnaps[2]),
        };
        var result = SuccessfulResult(request);

        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
        Assert.Equal(Coordinates(priorGroundGridSnaps[0]), Coordinates(result.BeltRoutePreview!.Points[0].Position));
        Assert.Equal(Coordinates(priorGroundGridSnaps[1]), Coordinates(result.BeltRoutePreview.Points[2].Position));
        Assert.Equal(Coordinates(priorGroundGridSnaps[2]), Coordinates(result.BeltRoutePreview.Points[4].Position));
    }

    [Theory]
    [InlineData("point_index")]
    [InlineData("planned_path")]
    [InlineData("segment_mode")]
    [InlineData("segment_range")]
    [InlineData("source_binding")]
    [InlineData("destination_binding")]
    [InlineData("geometry_hash")]
    [InlineData("budget_count")]
    [InlineData("extra_budget")]
    [InlineData("point_nan_rotation")]
    [InlineData("point_non_unit_rotation")]
    [InlineData("point_nan_tilt")]
    [InlineData("source_endpoint_nonfinite")]
    [InlineData("destination_endpoint_nonfinite")]
    [InlineData("endpoint_native_check_flag_mismatch")]
    [InlineData("positive_silently_adjusted_position")]
    public void RejectsAnyChangedPointSegmentBindingOrBudgetEcho(string fault)
    {
        var request = Request();
        var result = SuccessfulResult(request);
        switch (fault)
        {
            case "point_index": result.BeltRoutePreview!.Points[1].Index = 4; break;
            case "planned_path": result.PlannedPath[1] = Point(5.5f, 200, 0); break;
            case "segment_mode": result.BeltRoutePreview!.Segments[1].RoutingMode = BeltPathModes.NativeGeodesic; break;
            case "segment_range": result.BeltRoutePreview!.Segments[1].FirstBeltIndex = 1; break;
            case "source_binding": result.BeltRoutePreview!.Endpoints.Attachments[0].EndpointStateHash = "stale"; break;
            case "destination_binding": result.BeltRoutePreview!.Endpoints.Attachments[1].SorterItemId = 2011; break;
            case "geometry_hash": result.BeltRoutePreview!.FactoryGeometryHash = new string('f', 64); break;
            case "budget_count": result.ItemBudget.Single(row => row.ItemId == 2001).Count++; break;
            case "extra_budget": result.ItemBudget.Add(new ActionItemBudget { ItemId = 1001, Count = 1, Direction = "preview-construction-consumption" }); break;
            case "point_nan_rotation": result.BeltRoutePreview!.Points[1].Rotation.X = float.NaN; break;
            case "point_non_unit_rotation": result.BeltRoutePreview!.Points[1].Rotation.W = 2; break;
            case "point_nan_tilt": result.BeltRoutePreview!.Points[1].Tilt = float.NaN; break;
            case "source_endpoint_nonfinite":
                result.BeltRoutePreview!.Endpoints.Attachments.Single(row => row.Role == "source").Attachment.SourcePosition.X = float.NaN;
                break;
            case "destination_endpoint_nonfinite":
                result.BeltRoutePreview!.Endpoints.Attachments.Single(row => row.Role == "destination").Attachment.DestinationPosition.Z = float.PositiveInfinity;
                break;
            case "endpoint_native_check_flag_mismatch": result.BeltRoutePreview!.Endpoints.NativeCheckPerformed = false; break;
            case "positive_silently_adjusted_position": result.BeltRoutePreview!.Points[2].Position.X += .25f; break;
        }

        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
    }

    [Fact]
    public void AcceptsTheExactMaximumNativePointEchoAcrossTwelveOverlappingSegments()
    {
        var request = Request();
        var wanted = request.BeltRoutePreview!;
        wanted.Segments = Enumerable.Range(0, 12)
            .Select(i => Segment(2 + i * 20, 22 + i * 20)).ToList();
        var result = SuccessfulResult(request);
        var route = result.BeltRoutePreview!;
        var points = Enumerable.Range(0, BeltRoutePreviewPolicy.MaximumPoints)
            .Select(index => Point(index <= 176 ? 2 + index * 1.25f : 222 + (index - 176) * (20f / 15), 200, 0)).ToList();
        route.Points = points.Select((point, index) => new NativeBeltRoutePoint
        {
            Index = index,
            Position = ClonePoint(point),
            PlannedPosition = ClonePoint(point),
            Rotation = IdentityRotation(),
            Tilt = 0,
            NativeCondition = "Ok",
        }).ToList();
        result.PlannedPath.Clear();
        result.PlannedPath.AddRange(points);
        result.PlannedBeltPath!.NewObjectCount = points.Count;
        var destination = route.Endpoints.Attachments.Single(row => row.Role == "destination");
        destination.PlannedBeltIndex = points.Count - 1;
        destination.Attachment.SourcePosition = points[^1];
        result.ItemBudget.Single(row => row.ItemId == 2001).Count = points.Count;

        var segmentEchoes = new List<NativeBeltRouteSegment>();
        var first = 0;
        for (var i = 0; i < 12; i++)
        {
            var count = i == 11 ? 16 : 17;
            var last = first + count - 1;
            segmentEchoes.Add(new NativeBeltRouteSegment
            {
                Index = i,
                RoutingMode = BeltPathModes.NativeGrid,
                FirstBeltIndex = first,
                LastBeltIndex = last,
                NativePointCount = count,
            });
            first = last;
        }
        route.Segments = segmentEchoes;

        Assert.Equal(BeltRoutePreviewPolicy.MaximumPoints, route.Points.Count);
        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
    }

    [Fact]
    public void ConfirmsExplicitTokenlessNativeFailureWithoutTreatingItAsRouteSuccess()
    {
        var request = Request();
        var result = SuccessfulResult(request);
        var route = result.BeltRoutePreview!;
        route.NativePathCheckPerformed = true;
        route.NativePathCheckPassed = false;
        route.JointNativeCheckPerformed = false;
        route.JointNativeCheckPassed = false;
        route.FailedBeltIndex = 2;
        route.FailureStage = "native_full_path";
        route.FailureCondition = "Collision";
        route.Points[2].NativeCondition = "Collision";
        route.Endpoints.NativeCheckPerformed = false;
        route.Endpoints.NativeCheckPassed = false;
        route.Endpoints.Attachments.Clear();
        route.Endpoints.Blockers.Add("native_route_collision");

        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
        Assert.False(route.Executable);
        Assert.False(route.NativePathCheckPassed);
        Assert.False(route.JointNativeCheckPassed);
        Assert.Equal(5, route.Points.Count);
        Assert.Equal(2, route.Segments.Count);
        Assert.Empty(route.Endpoints.Attachments);
        Assert.Empty(result.PlanToken);
        Assert.False(result.Prepared);
        Assert.False(result.CommitAllowedNow);
    }

    [Fact]
    public void ConfirmsPreNativePathGuardFailureWithoutClaimingANativeBeltIndex()
    {
        var request = Request();
        var result = SuccessfulResult(request);
        var route = result.BeltRoutePreview!;
        route.NativePathCheckPerformed = false;
        route.NativePathCheckPassed = false;
        route.JointNativeCheckPerformed = false;
        route.JointNativeCheckPassed = false;
        route.FailedBeltIndex = null;
        route.FailureStage = "pre_native_path_guard";
        route.FailureCondition = "pre_native_geometry_guard";
        route.Endpoints.NativeCheckPerformed = false;
        route.Endpoints.NativeCheckPassed = false;
        route.Endpoints.Attachments.Clear();
        route.Endpoints.Blockers.Add("pre_native_geometry_guard");

        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
        Assert.Equal("pre_native_path_guard", route.FailureStage);
        Assert.Null(route.FailedBeltIndex);
        Assert.False(route.NativePathCheckPerformed);
        Assert.False(route.JointNativeCheckPerformed);
        Assert.Empty(route.Endpoints.Attachments);
        Assert.False(result.Prepared);
        Assert.False(result.CommitAllowedNow);
        Assert.Empty(result.PlanToken);
    }

    [Fact]
    public void ConfirmsPreNativeEndpointGuardOnlyAfterTheFullPathCheckPassed()
    {
        var request = Request();
        var result = SuccessfulResult(request);
        var route = result.BeltRoutePreview!;
        route.NativePathCheckPerformed = true;
        route.NativePathCheckPassed = true;
        route.JointNativeCheckPerformed = false;
        route.JointNativeCheckPassed = false;
        route.FailedBeltIndex = null;
        route.FailureStage = "pre_native_endpoint_guard";
        route.FailureCondition = "pre_native_endpoint_guard";
        route.Endpoints.NativeCheckPerformed = false;
        route.Endpoints.NativeCheckPassed = false;
        route.Endpoints.Attachments.Clear();
        route.Endpoints.Blockers.Add("pre_native_endpoint_guard");

        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
        Assert.Equal("pre_native_endpoint_guard", route.FailureStage);
        Assert.Null(route.FailedBeltIndex);
        Assert.True(route.NativePathCheckPassed);
        Assert.False(route.JointNativeCheckPerformed);
        Assert.Empty(route.Endpoints.Attachments);
    }

    [Fact]
    public void ConfirmsJointNativeFailureAsDistinctFromPreNativeAndPathFailures()
    {
        var request = Request();
        var result = SuccessfulResult(request);
        var route = result.BeltRoutePreview!;
        route.JointNativeCheckPassed = false;
        route.FailureStage = "joint_native";
        route.FailureCondition = "joint_native_attachment_failed";
        route.Endpoints.NativeCheckPassed = false;
        route.Endpoints.Attachments[0].NativeCondition = "Collision";
        route.Endpoints.Blockers.Add("joint_native_attachment_failed");

        Assert.True(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, result, "owned-session"));
        Assert.Equal("joint_native", route.FailureStage);
        Assert.True(route.NativePathCheckPassed);
        Assert.True(route.JointNativeCheckPerformed);
        Assert.False(route.JointNativeCheckPassed);
        Assert.Null(route.FailedBeltIndex);
    }

    [Fact]
    public void RejectsFailureStagesThatDoNotMatchChecksActuallyPerformed()
    {
        var request = Request();

        var claimedNativePathFailure = SuccessfulResult(request);
        var pathFailure = claimedNativePathFailure.BeltRoutePreview!;
        pathFailure.NativePathCheckPerformed = false;
        pathFailure.NativePathCheckPassed = false;
        pathFailure.JointNativeCheckPerformed = false;
        pathFailure.JointNativeCheckPassed = false;
        pathFailure.FailedBeltIndex = null;
        pathFailure.FailureStage = "native_full_path";
        pathFailure.FailureCondition = "native_path_failure_not_observed";
        pathFailure.Endpoints.NativeCheckPerformed = false;
        pathFailure.Endpoints.NativeCheckPassed = false;
        pathFailure.Endpoints.Attachments.Clear();
        pathFailure.Endpoints.Blockers.Add("native_path_failure_not_observed");
        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, claimedNativePathFailure, "owned-session"));

        var indexWithoutFailedPoint = SuccessfulResult(request);
        var indexedFailure = indexWithoutFailedPoint.BeltRoutePreview!;
        indexedFailure.NativePathCheckPerformed = true;
        indexedFailure.NativePathCheckPassed = false;
        indexedFailure.JointNativeCheckPerformed = false;
        indexedFailure.JointNativeCheckPassed = false;
        indexedFailure.FailedBeltIndex = 2;
        indexedFailure.FailureStage = "native_full_path";
        indexedFailure.FailureCondition = "native_collision";
        indexedFailure.Points[2].NativeCondition = "Ok";
        indexedFailure.Endpoints.NativeCheckPerformed = false;
        indexedFailure.Endpoints.NativeCheckPassed = false;
        indexedFailure.Endpoints.Attachments.Clear();
        indexedFailure.Endpoints.Blockers.Add("native_collision");
        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, indexWithoutFailedPoint, "owned-session"));

        var endpointBeforePathPass = SuccessfulResult(request);
        var endpointFailure = endpointBeforePathPass.BeltRoutePreview!;
        endpointFailure.NativePathCheckPerformed = true;
        endpointFailure.NativePathCheckPassed = false;
        endpointFailure.JointNativeCheckPerformed = false;
        endpointFailure.JointNativeCheckPassed = false;
        endpointFailure.FailedBeltIndex = null;
        endpointFailure.FailureStage = "pre_native_endpoint_guard";
        endpointFailure.FailureCondition = "endpoint_guard_ran_before_path_pass";
        endpointFailure.Endpoints.NativeCheckPerformed = false;
        endpointFailure.Endpoints.NativeCheckPassed = false;
        endpointFailure.Endpoints.Attachments.Clear();
        endpointFailure.Endpoints.Blockers.Add("endpoint_guard_ran_before_path_pass");
        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, endpointBeforePathPass, "owned-session"));

        var jointNotRun = SuccessfulResult(request);
        var jointFailure = jointNotRun.BeltRoutePreview!;
        jointFailure.JointNativeCheckPerformed = false;
        jointFailure.JointNativeCheckPassed = false;
        jointFailure.FailureStage = "joint_native";
        jointFailure.FailureCondition = "joint_check_not_observed";
        jointFailure.Endpoints.NativeCheckPerformed = false;
        jointFailure.Endpoints.NativeCheckPassed = false;
        jointFailure.Endpoints.Blockers.Add("joint_check_not_observed");
        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, jointNotRun, "owned-session"));
    }

    [Fact]
    public void RejectsAmbiguousOrIncompleteFailureAndOldPluginResponses()
    {
        var request = Request();
        var missing = new PreparedNormalAction { ExpectedStateHash = "player-hash" };
        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, missing, "owned-session"));

        var ambiguous = SuccessfulResult(request);
        ambiguous.BeltRoutePreview!.NativePathCheckPerformed = false;
        ambiguous.BeltRoutePreview.NativePathCheckPassed = false;
        ambiguous.BeltRoutePreview.JointNativeCheckPerformed = false;
        ambiguous.BeltRoutePreview.JointNativeCheckPassed = false;
        ambiguous.BeltRoutePreview.FailedBeltIndex = null;
        ambiguous.BeltRoutePreview.FailureCondition = null;
        ambiguous.BeltRoutePreview.Endpoints.NativeCheckPassed = false;
        ambiguous.BeltRoutePreview.Endpoints.Blockers.Add("unknown");
        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, ambiguous, "owned-session"));

        var uncheckedFailure = SuccessfulResult(request);
        uncheckedFailure.BeltRoutePreview!.NativePathCheckPerformed = false;
        uncheckedFailure.BeltRoutePreview.NativePathCheckPassed = false;
        uncheckedFailure.BeltRoutePreview.JointNativeCheckPerformed = false;
        uncheckedFailure.BeltRoutePreview.JointNativeCheckPassed = false;
        uncheckedFailure.BeltRoutePreview.FailedBeltIndex = 2;
        uncheckedFailure.BeltRoutePreview.FailureStage = "pre_native_path_guard";
        uncheckedFailure.BeltRoutePreview.FailureCondition = "native_collision";
        uncheckedFailure.BeltRoutePreview.Endpoints.NativeCheckPassed = false;
        uncheckedFailure.BeltRoutePreview.Endpoints.Blockers.Add("native_route_collision");
        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, uncheckedFailure, "owned-session"));

        var truncated = SuccessfulResult(request);
        truncated.BeltRoutePreview!.Points.RemoveAt(2);
        Assert.False(BeltRoutePreviewPolicy.ConfirmsReadOnlyEcho(request, truncated, "owned-session"));
    }

    [Fact]
    public void RequiresExplicitRoutePreviewModeWhenRequestIsAbsent()
    {
        var request = new PrepareBuildRequest { BeltPathMode = BeltPathModes.NativeRoutePreview };
        Assert.Equal("belt_route_preview_request_required", BeltRoutePreviewPolicy.ValidateRequest(request));
    }

    private static PrepareBuildRequest Request()
    {
        var points = RoutePoints();
        return new PrepareBuildRequest
        {
            PlanetId = 104,
            BuildingItemId = 2001,
            BeltPathMode = BeltPathModes.NativeRoutePreview,
            StateHashVersion = 1,
            ExpectedPlayerStateHash = "player-state-hash",
            BeltRoutePreview = new BeltRoutePreviewRequest
            {
                ExpectedFactoryGeometryHash = GeometryHash,
                FilterItemId = 1001,
                SourceSorterItemId = 2011,
                DestinationSorterItemId = 2012,
                Source = Binding(41, 3, 0, 2),
                Destination = Binding(43, 4, 0, 1),
                Segments = new()
                {
                    new BeltRouteSegment { RoutingMode = BeltPathModes.NativeGrid, Start = points[0], End = points[2] },
                    new BeltRouteSegment { RoutingMode = BeltPathModes.NativeGrid, Start = points[2], End = points[4] },
                },
            },
        };
    }

    private static PreparedNormalAction SuccessfulResult(PrepareBuildRequest request)
    {
        var wanted = request.BeltRoutePreview!;
        var points = RoutePoints();
        var budgetCounts = new Dictionary<int, int> { [2001] = points.Count };
        foreach (var sorterId in new[] { wanted.SourceSorterItemId, wanted.DestinationSorterItemId })
        {
            budgetCounts.TryGetValue(sorterId, out var current);
            budgetCounts[sorterId] = current + 1;
        }
        var attachments = new List<PlannedBeltEndpointAttachment>
        {
            Attachment(wanted.Source, "source", wanted.SourceSorterItemId, points[0], 0),
            Attachment(wanted.Destination, "destination", wanted.DestinationSorterItemId, points[^1], 4),
        };
        var route = new BeltRoutePreviewSnapshot
        {
            Phase = BeltRoutePreviewPolicy.Phase,
            FactoryGeometryHash = wanted.ExpectedFactoryGeometryHash,
            NativePathCheckPerformed = true,
            NativePathCheckPassed = true,
            JointNativeCheckPerformed = true,
            JointNativeCheckPassed = true,
            Points = points.Select((point, index) => new NativeBeltRoutePoint
            {
                Index = index,
                Position = ClonePoint(point),
                PlannedPosition = ClonePoint(point),
                Rotation = IdentityRotation(),
                Tilt = 0,
                NativeCondition = "Ok",
            }).ToList(),
            Segments = new()
            {
                new NativeBeltRouteSegment
                {
                    Index = 0, RoutingMode = BeltPathModes.NativeGrid, FirstBeltIndex = 0,
                    LastBeltIndex = 2, NativePointCount = 3,
                },
                new NativeBeltRouteSegment
                {
                    Index = 1, RoutingMode = BeltPathModes.NativeGrid, FirstBeltIndex = 2,
                    LastBeltIndex = 4, NativePointCount = 3,
                },
            },
            Endpoints = new BeltEndpointPreviewSnapshot
            {
                Phase = "read_only_belt_endpoint_preview",
                SessionId = "owned-session",
                PlanetId = 104,
                Revision = 7,
                CapturedAtGameTick = 120,
                NativeCheckPerformed = true,
                NativeCheckPassed = true,
                AssessmentHash = "assessment-hash",
                Attachments = attachments,
            },
        };
        var result = new PreparedNormalAction
        {
            Prepared = false,
            ActionKind = NormalActionKinds.Build,
            PlanToken = string.Empty,
            ExpectedStateHash = request.ExpectedPlayerStateHash,
            BuildKind = "belt_route_preview",
            BeltRoutePreview = route,
            PlannedBeltPath = new BeltPathPlanSnapshot
            {
                NewObjectCount = points.Count,
                RoutingMode = BeltPathModes.NativeRoutePreview,
                NativeValidationMode = "full_path_stage1_and_joint_all_objects",
                SourceBindingMode = "none",
                DestinationBindingMode = "none",
            },
            ItemBudget = budgetCounts.Select(pair => new ActionItemBudget
            {
                ItemId = pair.Key,
                Count = pair.Value,
                Direction = "preview-construction-consumption",
            }).ToList(),
        };
        result.PlannedPath.AddRange(points);
        return result;
    }

    private static PlannedBeltEndpointAttachment Attachment(PlannedBeltEndpointBinding binding, string role,
        int sorterItemId, Vector3Snapshot pathPoint, int beltIndex)
    {
        var source = role == "source";
        return new PlannedBeltEndpointAttachment
        {
            Role = role,
            ExistingObjectId = binding.ExistingObjectId,
            EndpointStateHash = binding.ExpectedEndpointStateHash,
            ExistingItemId = 2101,
            ObservedRecipeId = 0,
            PlannedBeltIndex = beltIndex,
            SorterItemId = sorterItemId,
            FilterItemId = 1001,
            ExistingBeltQuarterTurns = binding.ExistingBeltQuarterTurns,
            PlannedBeltQuarterTurns = binding.PlannedBeltQuarterTurns,
            NativeCondition = "Ok",
            NativeSpan = 2,
            Attachment = new InserterAttachmentPlanSnapshot
            {
                Mode = "read_only_planned_belt_exact_slots",
                SourceSlot = source ? binding.ExistingSlot : -1,
                DestinationSlot = source ? -1 : binding.ExistingSlot,
                SourcePosition = source ? Point(1, 200, 0) : pathPoint,
                DestinationPosition = source ? pathPoint : Point(20, 200, 0),
            },
        };
    }

    private static List<Vector3Snapshot> RoutePoints() => new()
    {
        Point(2, 200, 0), Point(5, 200, 0), Point(8, 200, 0), Point(11, 200, 0), Point(14, 200, 0),
    };

    private static BeltRouteSegment Segment(double startX, double endX) => new()
    {
        RoutingMode = BeltPathModes.NativeGrid,
        Start = Point((float)startX, 200, 0),
        End = Point((float)endX, 200, 0),
    };

    private static BeltRouteSegment Segment(Vector3Snapshot start, Vector3Snapshot end) => new()
    {
        RoutingMode = BeltPathModes.NativeGrid,
        Start = start,
        End = end,
    };

    private static PlannedBeltEndpointBinding Binding(int id, int slot, int existingTurns, int plannedTurns) => new()
    {
        ExistingObjectId = id,
        ExpectedEndpointStateHash = $"endpoint-{id}",
        ExistingSlot = slot,
        ExistingBeltQuarterTurns = existingTurns,
        PlannedBeltQuarterTurns = plannedTurns,
    };

    private static QuaternionSnapshot IdentityRotation() => new() { W = 1 };

    private static Vector3Snapshot ClonePoint(Vector3Snapshot point) => new() { X = point.X, Y = point.Y, Z = point.Z };

    private static Vector3Snapshot Point(float x, float y, float z) => new() { X = x, Y = y, Z = z };

    private static (float X, float Y, float Z) Coordinates(Vector3Snapshot point) => (point.X, point.Y, point.Z);
}
