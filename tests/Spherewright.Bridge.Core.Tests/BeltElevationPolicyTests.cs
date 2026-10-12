using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class BeltElevationPolicyTests
{
    private const float GroundRadius = 200.2f;

    [Fact]
    public void RoutingEntryValidatesElevationInsteadOfFallingThroughToLegacyGrid()
    {
        var request = Request();
        Assert.Null(BeltPathRoutingPolicy.ValidateRequest(request, true));
        request.BeltStartAltitudeLevel = null;
        Assert.NotNull(BeltPathRoutingPolicy.ValidateRequest(request, true));
        request = Request();
        request.BeltPathMode = BeltPathModes.NativeGeodesic;
        Assert.NotNull(BeltPathRoutingPolicy.ValidateRequest(request, true));
    }

    [Fact]
    public void ExplicitElevationBindsModeAndExactGeometryWithoutChangingDefaultHash()
    {
        const string geometry = "native-points-and-levels";
        Assert.Equal(geometry, BeltPathRoutingPolicy.BindGeometry(BeltPathModes.NativeGrid, geometry));
        var elevated = BeltPathRoutingPolicy.BindGeometry(BeltPathModes.NativeElevatedGrid, geometry);
        Assert.NotEqual(geometry, elevated);
        Assert.NotEqual(BeltPathRoutingPolicy.BindGeometry(BeltPathModes.NativeGeodesic, geometry), elevated);
        Assert.NotEqual(BeltPathRoutingPolicy.BindGeometry(BeltPathModes.NativeElevatedGrid, "different-height-points"), elevated);
        Assert.True(BeltPathRoutingPolicy.ConfirmsPlanEcho(BeltPathModes.NativeElevatedGrid, Echo()));
        Assert.False(BeltPathRoutingPolicy.ConfirmsPlanEcho(BeltPathModes.NativeGrid, Echo()));
        Assert.False(BeltPathRoutingPolicy.ConfirmsPlanEcho(BeltPathModes.NativeGeodesic, Echo()));
    }

    [Fact]
    public void ElevatedFree2001RequestRequiresTheExplicitModeAndExactlyTwoLevels()
    {
        var request = Request();
        Assert.Null(BeltElevationPolicy.ValidateRequest(request, nativeBelt: true));

        request.BeltPathMode = BeltPathModes.NativeGrid;
        Assert.Equal("belt_elevation_requires_explicit_mode", BeltElevationPolicy.ValidateRequest(request, nativeBelt: true));

        request.BeltPathMode = BeltPathModes.NativeElevatedGrid;
        request.BeltEndAltitudeLevel = null;
        Assert.Equal("belt_elevation_levels_unsupported", BeltElevationPolicy.ValidateRequest(request, nativeBelt: true));
    }

    [Theory]
    [InlineData("source_positive")]
    [InlineData("source_zero")]
    [InlineData("source_negative")]
    [InlineData("destination_zero")]
    [InlineData("resource")]
    [InlineData("source_hash")]
    [InlineData("missing_start")]
    [InlineData("missing_end")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("wrong_item")]
    [InlineData("non_belt")]
    public void ElevatedRequestsRejectBoundAmbiguousOrUnsupportedInputs(string fault)
    {
        var request = Request();
        var nativeBelt = true;
        switch (fault)
        {
            case "source_positive": request.SourceObjectId = 92; break;
            case "source_zero": request.SourceObjectId = 0; break;
            case "source_negative": request.SourceObjectId = -92; break;
            case "destination_zero": request.DestinationObjectId = 0; break;
            case "resource": request.ResourceNodeId = 1; break;
            case "source_hash": request.ExpectedSourceStateHash = "fresh"; break;
            case "missing_start": request.PreferredPosition = null; break;
            case "missing_end": request.PathEnd = null; break;
            case "nan": request.PreferredPosition!.X = float.NaN; break;
            case "infinity": request.PathEnd!.Z = float.PositiveInfinity; break;
            case "wrong_item": request.BuildingItemId = 2002; break;
            case "non_belt": nativeBelt = false; break;
        }
        Assert.Equal("belt_elevation_requires_explicit_free_endpoints", BeltElevationPolicy.ValidateRequest(request, nativeBelt));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void LevelsZeroThroughThreeAreAllowedOnlyWhenOneEndIsRaised(int start, int end) =>
        Assert.True(BeltElevationPolicy.ValidLevels(start, end));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(4, 1)]
    [InlineData(1, 4)]
    public void MissingFlatOrOutOfRangeLevelsAreRejected(int? start, int? end) =>
        Assert.False(BeltElevationPolicy.ValidLevels(start, end));

    [Fact]
    public void MissingLevelsAreRejected() =>
        Assert.False(BeltElevationPolicy.ValidLevels(null, 1));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LevelDetectionAcceptsExactNativeLayerRadii(int expectedLevel)
    {
        Assert.True(BeltElevationPolicy.TryGetLevel(Point(expectedLevel, 2.4f), GroundRadius, out var actual));
        Assert.Equal(expectedLevel, actual);
    }

    [Fact]
    public void LevelDetectionRejectsNonfiniteIntermediateAndOutOfRangeRadii()
    {
        var nan = Point(1, 0); nan.X = float.NaN;
        var infinity = Point(1, 0); infinity.Z = float.PositiveInfinity;
        Assert.False(BeltElevationPolicy.TryGetLevel(nan, GroundRadius, out _));
        Assert.False(BeltElevationPolicy.TryGetLevel(infinity, GroundRadius, out _));
        Assert.False(BeltElevationPolicy.TryGetLevel(Point(-.25f, 0), GroundRadius, out _));
        Assert.False(BeltElevationPolicy.TryGetLevel(Point(4, 0), GroundRadius, out _));
        Assert.False(BeltElevationPolicy.TryGetLevel(Point(.25f, 0), GroundRadius, out _));
    }

    [Fact]
    public void EndpointsRequireRaisedBoundedAndSeparatedNativeLayerPoints()
    {
        Assert.True(BeltElevationPolicy.ValidEndpoints(Point(0, 0), Point(1, 4.8f), GroundRadius));
        Assert.False(BeltElevationPolicy.ValidEndpoints(Point(0, 0), Point(0, 4.8f), GroundRadius));
        Assert.False(BeltElevationPolicy.ValidEndpoints(Point(0, 0), Point(1, .01f), GroundRadius));
        Assert.False(BeltElevationPolicy.ValidEndpoints(Point(0, 0), Point(1, 40f), GroundRadius));
    }

    [Theory]
    [InlineData(0f, true)]
    [InlineData(.5f, true)]
    [InlineData(.50001f, false)]
    [InlineData(-.001f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(float.PositiveInfinity, false)]
    public void NativeSlopeIsFiniteNonnegativeAndBounded(float slope, bool expected) =>
        Assert.Equal(expected, BeltElevationPolicy.ValidNativeSlope(slope));

    [Fact]
    public void CompletePathAcceptsOnlySyntheticUpDownAndHighFlatShapes()
    {
        Assert.True(BeltElevationPolicy.CompleteNativePath(Uphill(), 256, Point(0, 0), Point(1, 4.8f), GroundRadius));
        Assert.True(BeltElevationPolicy.CompleteNativePath(Downhill(), 256, Point(1, 0), Point(0, 4.8f), GroundRadius));
        Assert.True(BeltElevationPolicy.CompleteNativePath(HighFlat(), 256, Point(2, 0), Point(2, 3.6f), GroundRadius));
        // These are synthetic policy inputs, not native placement permission.
    }

    [Fact]
    public void CompleteNativePathBoundsEndpointDistanceNotSummedGridRouteLength()
    {
        // A synthetic high, flat L-shaped grid route: its chord is under30m,
        // while its polyline is over30m. This is policy evidence, not a site pass.
        var radius = GroundRadius + 3 * BeltElevationPolicy.NativeLayerHeight;
        var path = new List<Vector3Snapshot>();
        for (var i = 0; i <= 14; i++)
        {
            var x = -20f + i * (20f / 14);
            path.Add(new Vector3Snapshot { X = x, Y = 0, Z = MathF.Sqrt(radius * radius - x * x) });
        }
        for (var i = 1; i <= 14; i++)
        {
            var y = i * (20f / 14);
            path.Add(new Vector3Snapshot { X = 0, Y = y, Z = MathF.Sqrt(radius * radius - y * y) });
        }

        double polylineLength = 0;
        for (var i = 1; i < path.Count; i++)
        {
            var dx = (double)path[i].X - path[i - 1].X;
            var dy = (double)path[i].Y - path[i - 1].Y;
            var dz = (double)path[i].Z - path[i - 1].Z;
            polylineLength += Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        Assert.Equal(29, path.Count);
        Assert.True(polylineLength > 30);
        Assert.True(BeltElevationPolicy.ValidEndpoints(path[0], path[^1], GroundRadius));
        Assert.True(BeltElevationPolicy.CompleteNativePath(path, 256, path[0], path[^1], GroundRadius));
    }

    [Theory]
    [InlineData(.51f, false)]
    [InlineData(.53f, true)]
    [InlineData(.6f, true)]
    public void FourPointPathsUseNativePointTwoEightNotTheTwoPointCleanupThreshold(float interval, bool expected)
    {
        var path = new[] { Point(2, 0), Point(2, interval), Point(2, interval + 1.2f), Point(2, interval + 2.4f) };
        Assert.Equal(expected, BeltElevationPolicy.CompleteNativePath(path, 256, path[0], path[^1], GroundRadius));
    }

    [Theory]
    [InlineData("saturated")]
    [InlineData("reserved_buffer")]
    [InlineData("missing_start")]
    [InlineData("endpoint_offset")]
    [InlineData("short_segment")]
    [InlineData("large_jump")]
    [InlineData("reverse_undulation")]
    [InlineData("height_out_of_bounds")]
    [InlineData("nonflat_start_end")]
    public void CompletePathFailsClosedInsteadOfRepairingNativeOutput(string fault)
    {
        var path = Uphill();
        var capacity = 256;
        switch (fault)
        {
            case "saturated": path = Enumerable.Repeat(Point(0, 0), 246).ToArray(); break;
            case "reserved_buffer": capacity = BeltPathRoutingPolicy.NativeReservedPoints + path.Length; break;
            case "missing_start": path[0] = Point(0, .02f); break;
            case "endpoint_offset": path[^1] = Point(1, 4.82f); break;
            case "short_segment": path[2] = Copy(path[1]); break;
            case "large_jump": path[2] = Point(.5f, 10f); break;
            case "reverse_undulation": path[3] = Point(.25f, 3.6f); break;
            case "height_out_of_bounds": path[2] = Point(4, 2.4f); break;
            case "nonflat_start_end":
                path[1] = Point(.5f, 1.2f);
                path[2] = Point(.5f, 2.4f);
                break;
        }
        Assert.False(BeltElevationPolicy.CompleteNativePath(path, capacity, Point(0, 0), Point(1, 4.8f), GroundRadius));
    }

    [Fact]
    public void CompletePathRejectsAnAbsentOrInvalidEndpointEvidence()
    {
        Assert.False(BeltElevationPolicy.CompleteNativePath(null, 256, Point(0, 0), Point(1, 4.8f), GroundRadius));
        Assert.False(BeltElevationPolicy.CompleteNativePath(Uphill(), 256, null!, Point(1, 4.8f), GroundRadius));
        Assert.False(BeltElevationPolicy.CompleteNativePath(Uphill(), 256, Point(0, 0), null!, GroundRadius));
    }

    [Fact]
    public void ExactElevatedEchoRequiresFreshModeLevelsFreeEndpointsAndBoundedNewCount()
    {
        var echo = Echo();
        Assert.True(BeltElevationPolicy.ConfirmsPlanEcho(0, 1, echo));

        echo.RoutingMode = BeltPathModes.NativeGrid;
        Assert.False(BeltElevationPolicy.ConfirmsPlanEcho(0, 1, echo));
        echo.RoutingMode = BeltPathModes.NativeElevatedGrid;
        echo.EndAltitudeLevel = 2;
        Assert.False(BeltElevationPolicy.ConfirmsPlanEcho(0, 1, echo));
        echo.EndAltitudeLevel = 1;
        echo.SourceBindingMode = "non_removing_belt_cover";
        echo.ReusedSourceObjectId = 92;
        Assert.False(BeltElevationPolicy.ConfirmsPlanEcho(0, 1, echo));
    }

    [Theory]
    [InlineData("legacy_missing")]
    [InlineData("wrong_stage")]
    [InlineData("destination_cover")]
    [InlineData("too_few")]
    [InlineData("too_many")]
    [InlineData("flat_request")]
    public void EchoRejectsOldPluginCoverOrCountAmbiguity(string fault)
    {
        BeltPathPlanSnapshot? echo = Echo();
        var requestedStart = 0;
        var requestedEnd = 1;
        switch (fault)
        {
            case "legacy_missing": echo = new BeltPathPlanSnapshot { NativeValidationMode = "full_path_stage1", SourceBindingMode = "none" }; break;
            case "wrong_stage": echo.NativeValidationMode = "native_grid"; break;
            case "destination_cover":
                echo.DestinationBindingMode = "non_removing_belt_cover";
                echo.ReusedDestinationObjectId = 93;
                echo.DestinationPreservationMode = "cover";
                break;
            case "too_few": echo.NewObjectCount = 3; break;
            case "too_many": echo.NewObjectCount = BeltElevationPolicy.MaximumPoints + 1; break;
            case "flat_request": requestedEnd = 0; echo.EndAltitudeLevel = 0; break;
        }
        Assert.False(BeltElevationPolicy.ConfirmsPlanEcho(requestedStart, requestedEnd, echo));
    }

    [Fact]
    public void RaisedSourceRequestRequiresExactIdHashPositionAndFreeDestination()
    {
        var request = Request();
        request.SourceObjectId = 92;
        request.ExpectedSourceStateHash = "fresh-endpoint";
        request.BeltStartAltitudeLevel = 2;
        request.BeltEndAltitudeLevel = 1;
        Assert.Null(BeltElevationPolicy.ValidateRequest(request, true));
        Assert.Null(BeltPathRoutingPolicy.ValidateRequest(request, true));
        request.ExpectedSourceStateHash = " ";
        Assert.NotNull(BeltElevationPolicy.ValidateRequest(request, true));
        request.ExpectedSourceStateHash = "fresh-endpoint";
        request.BeltStartAltitudeLevel = 0;
        Assert.Equal("belt_elevation_source_cover_requires_raised_start", BeltElevationPolicy.ValidateRequest(request, true));
        request.BeltStartAltitudeLevel = 2;
        request.DestinationObjectId = 93;
        Assert.NotNull(BeltElevationPolicy.ValidateRequest(request, true));
    }

    [Fact]
    public void RaisedCoverRetainsEveryNewPointAcrossAnActualLayerChange()
    {
        var full = new[] { Point(2, 0), Point(2, 1.2f), Point(1.5f, 2.4f), Point(1, 3.6f), Point(1, 4.8f) };
        Assert.True(BeltElevationPolicy.TrySeparateRaisedSourceCover(full, full[0], GroundRadius, out var points));
        Assert.Equal(full.Length - 1, points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            Assert.Equal(full[i + 1].X, points[i].X);
            Assert.Equal(full[i + 1].Y, points[i].Y);
            Assert.Equal(full[i + 1].Z, points[i].Z);
            Assert.NotSame(full[i + 1], points[i]);
        }
        Assert.False(BeltSourceReusePolicy.TrySeparateNewPoints(full, full[0], out _));
    }

    [Theory]
    [InlineData("wrong_source")]
    [InlineData("ground_source")]
    [InlineData("clipped_end")]
    [InlineData("duplicate")]
    [InlineData("null_first")]
    [InlineData("nonfinite")]
    public void RaisedCoverCannotHideIncompleteOrChangedNativePoints(string fault)
    {
        var full = HighFlat();
        var source = Copy(full[0]);
        switch (fault)
        {
            case "wrong_source": source = Point(2, 1.2f); break;
            case "ground_source": source = Point(0, 0); break;
            case "clipped_end": full[3] = Point(1.5f, 3.6f); break;
            case "duplicate": full[1] = Copy(full[0]); break;
            case "null_first": full[0] = null!; break;
            case "nonfinite": full[2].X = float.NaN; break;
        }
        Assert.False(BeltElevationPolicy.TrySeparateRaisedSourceCover(full, source, GroundRadius, out var points));
        Assert.Empty(points);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("wrong_source")]
    [InlineData("no_preservation")]
    [InlineData("destination")]
    [InlineData("wrong_budget")]
    [InlineData("truncated")]
    [InlineData("sixty_four_new")]
    public void RaisedPreparedEchoRequiresBoundSourceAndExactNewObjectBill(string fault)
    {
        var plan = RaisedPrepared();
        Assert.True(BeltElevationPolicy.ConfirmsPreparedEcho(plan, 2001, 2, 1, 92));
        switch (fault)
        {
            case "none": plan.PlannedBeltPath!.SourceBindingMode = "none"; break;
            case "wrong_source": plan.PlannedBeltPath!.ReusedSourceObjectId = 93; break;
            case "no_preservation": plan.PlannedBeltPath!.SourcePreservationMode = null; break;
            case "destination": plan.DestinationObjectId = 93; break;
            case "wrong_budget": plan.ItemBudget[0].Count = 2; break;
            case "truncated": plan.PlannedPath.RemoveAt(2); break;
            case "sixty_four_new": plan.PlannedBeltPath!.NewObjectCount = 64; break;
        }
        Assert.False(BeltElevationPolicy.ConfirmsPreparedEcho(plan, 2001, 2, 1, 92));
    }

    private static PreparedNormalAction RaisedPrepared() => new()
    {
        Prepared = true,
        BuildKind = "belt",
        SourceObjectId = 92,
        PlannedBeltPath = new BeltPathPlanSnapshot
        {
            RoutingMode = BeltPathModes.NativeElevatedGrid,
            NativeValidationMode = "full_path_stage1",
            StartAltitudeLevel = 2,
            EndAltitudeLevel = 1,
            NewObjectCount = 4,
            SourceBindingMode = "non_removing_belt_cover",
            ReusedSourceObjectId = 92,
            SourcePreservationMode = BeltSourceRotationPolicy.PreservationMode,
            DestinationBindingMode = "none",
        },
        PlannedPath = new() { Point(2, 1.2f), Point(1.5f, 2.4f), Point(1, 3.6f), Point(1, 4.8f) },
        ItemBudget = new() { new() { ItemId = 2001, Count = 4, Direction = "construction-consumption" } },
    };

    private static PrepareBuildRequest Request() => new()
    {
        BuildingItemId = 2001,
        BeltPathMode = BeltPathModes.NativeElevatedGrid,
        PreferredPosition = Point(0, 0),
        PathEnd = Point(1, 4.8f),
        BeltStartAltitudeLevel = 0,
        BeltEndAltitudeLevel = 1,
    };

    private static BeltPathPlanSnapshot Echo() => new()
    {
        RoutingMode = BeltPathModes.NativeElevatedGrid,
        NativeValidationMode = "full_path_stage1",
        StartAltitudeLevel = 0,
        EndAltitudeLevel = 1,
        NewObjectCount = 4,
        SourceBindingMode = "none",
        DestinationBindingMode = "none",
    };

    private static Vector3Snapshot[] Uphill() => new[]
    {
        Point(0, 0), Point(0, 1.2f), Point(.5f, 2.4f), Point(1, 3.6f), Point(1, 4.8f),
    };

    private static Vector3Snapshot[] Downhill() => new[]
    {
        Point(1, 0), Point(1, 1.2f), Point(.5f, 2.4f), Point(0, 3.6f), Point(0, 4.8f),
    };

    private static Vector3Snapshot[] HighFlat() => new[]
    {
        Point(2, 0), Point(2, 1.2f), Point(2, 2.4f), Point(2, 3.6f),
    };

    private static Vector3Snapshot Point(float level, float arcMeters)
    {
        var radius = GroundRadius + level * BeltElevationPolicy.NativeLayerHeight;
        var radians = arcMeters / radius;
        return new Vector3Snapshot
        {
            X = (float)(radius * Math.Sin(radians)),
            Y = 0,
            Z = (float)(radius * Math.Cos(radians)),
        };
    }

    private static Vector3Snapshot Copy(Vector3Snapshot point) => new() { X = point.X, Y = point.Y, Z = point.Z };
}
