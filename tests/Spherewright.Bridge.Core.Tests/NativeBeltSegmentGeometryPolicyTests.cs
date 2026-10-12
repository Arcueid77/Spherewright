using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeBeltSegmentGeometryPolicyTests
{
    private const string Hash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void SegmentOnlyBatchBindsTheWholeLayoutAndReturnsCompleteGeneratedPointsWithoutQualification()
    {
        var request = Request();
        request.NativeBeltSegments = Enumerable.Range(0, 16).Select(i => Segment(i * 8)).ToList();
        var cut = Cut(request);
        Assert.Null(FactoryGeometryReadPolicy.ValidateRequest(request));
        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "session", 104));
        Assert.All(cut.NativeBeltSegments, row =>
        {
            Assert.False(row.Executable);
            Assert.False(row.NativeConditionsChecked);
            Assert.Equal(4, row.Points.Count);
        });
    }

    [Theory]
    [InlineData("missing_hash")]
    [InlineData("too_many_segments")]
    [InlineData("null_segment")]
    [InlineData("unknown_mode")]
    [InlineData("preview_mode")]
    [InlineData("grid_with_layer")]
    [InlineData("elevated_ground_only")]
    [InlineData("layer_overflow")]
    [InlineData("nonfinite_endpoint")]
    [InlineData("short_span")]
    [InlineData("long_span")]
    public void InvalidSegmentRequestsRefuseBeforeAnyNativeRead(string fault)
    {
        var request = Request(); var s = request.NativeBeltSegments![0];
        switch (fault)
        {
            case "missing_hash": request.ExpectedFactoryGeometryHash = null; break;
            case "too_many_segments": request.NativeBeltSegments = Enumerable.Range(0, 17).Select(i => Segment(i * 8)).ToList(); break;
            case "null_segment": request.NativeBeltSegments[0] = null!; break;
            case "unknown_mode": s.RoutingMode = "straight_estimate"; break;
            case "preview_mode": s.RoutingMode = BeltPathModes.NativeRoutePreview; break;
            case "grid_with_layer": s.EndAltitudeLevel = 1; break;
            case "elevated_ground_only": s.RoutingMode = BeltPathModes.NativeElevatedGrid; break;
            case "layer_overflow": s.RoutingMode = BeltPathModes.NativeElevatedGrid; s.EndAltitudeLevel = 4; break;
            case "nonfinite_endpoint": s.Start.X = float.NaN; break;
            case "short_span": s.End.X = 1; break;
            case "long_span": s.End.X = 31; break;
        }
        Assert.NotNull(FactoryGeometryReadPolicy.ValidateRequest(request));
    }

    [Theory]
    [InlineData("legacy_missing_rows")]
    [InlineData("reordered_rows")]
    [InlineData("moved_endpoint")]
    [InlineData("changed_layer")]
    [InlineData("checked_conditions")]
    [InlineData("wrong_phase")]
    [InlineData("null_points")]
    [InlineData("incomplete_endpoint")]
    [InlineData("oversized_points")]
    [InlineData("nonfinite_point")]
    [InlineData("missing_slope")]
    [InlineData("nonfinite_slope")]
    [InlineData("missing_ground_radius")]
    [InlineData("negative_with_partial_points")]
    public void MissingMalformedOrSubstitutedGenerationEchoFailsClosed(string fault)
    {
        var request = Request(); request.NativeBeltSegments!.Add(Segment(10));
        var cut = Cut(request); var row = cut.NativeBeltSegments[0];
        switch (fault)
        {
            case "legacy_missing_rows": cut.NativeBeltSegments.Clear(); break;
            case "reordered_rows": cut.NativeBeltSegments.Reverse(); break;
            case "moved_endpoint": row.Segment.Start.X += 1; break;
            case "changed_layer": row.Segment.EndAltitudeLevel = 1; break;
            case "checked_conditions": row.NativeConditionsChecked = true; break;
            case "wrong_phase": row.Phase = "native_build_passed"; break;
            case "null_points": row.Points = null!; break;
            case "incomplete_endpoint": row.Points.RemoveAt(row.Points.Count - 1); break;
            case "oversized_points": row.Points = Enumerable.Range(0, 65).Select(i => Point(i)).ToList(); break;
            case "nonfinite_point": row.Points[1].Z = float.PositiveInfinity; break;
            case "missing_slope": row.NativeMaximumSlope = null; break;
            case "nonfinite_slope": row.NativeMaximumSlope = float.NaN; break;
            case "missing_ground_radius": row.NativeGroundRadius = null; break;
            case "negative_with_partial_points": row.Observed = false; row.ReasonCode = "rejected"; cut.State = "unavailable"; cut.Blockers.Add("rejected"); break;
        }
        Assert.False(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "session", 104));
    }

    [Fact]
    public void GenerationRefusalIsUnavailableAndRetainsItsExactSegmentWithoutPartialPoints()
    {
        var request = Request(); var cut = Cut(request); var row = cut.NativeBeltSegments[0];
        row.Observed = false; row.Points.Clear(); row.ReasonCode = "native_belt_segment_requires_exact_snapped_endpoints";
        cut.State = "unavailable"; cut.Blockers.Add(row.ReasonCode);
        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "session", 104));
        Assert.False(row.NativeConditionsChecked);
    }

    [Fact]
    public void ElevatedEchoRequiresTheWholeFlatEndedNativePathAndMeasuredGentleSlope()
    {
        var request = Request(); var s = request.NativeBeltSegments![0];
        var radius = 200.2 + (double)BeltElevationPolicy.NativeLayerHeight;
        Vector3Snapshot At(float x) => new() { X = x, Y = (float)Math.Sqrt(radius * radius - x * x) };
        s.RoutingMode = BeltPathModes.NativeElevatedGrid; s.StartAltitudeLevel = 1; s.EndAltitudeLevel = 1;
        s.Start = At(0); s.End = At(4);
        var cut = Cut(request); var row = cut.NativeBeltSegments[0];
        row.Points = new() { At(0), At(4f / 3), At(8f / 3), At(4) };
        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "session", 104));
        row.NativeMaximumSlope = .51f;
        Assert.False(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "session", 104));
        row.NativeMaximumSlope = 0; row.Points.RemoveAt(1);
        Assert.False(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "session", 104));
    }

    private static FactoryGeometryReadRequest Request() => new()
    { ExpectedFactoryGeometryHash = Hash, NativeBeltSegments = new() { Segment(0) } };

    private static BeltRouteSegment Segment(float x) => new()
    { RoutingMode = BeltPathModes.NativeGrid, Start = Point(x), End = Point(x + 6) };

    private static Vector3Snapshot Point(float x) => new() { X = x, Y = 200 };

    private static FactoryGeometryCutSnapshot Cut(FactoryGeometryReadRequest request) => new()
    {
        State = "observed", SessionId = "session", PlanetId = 104, FactoryGeometryHash = Hash,
        NativeBeltSegments = request.NativeBeltSegments!.Select((s, i) => new FactoryNativeBeltSegmentSnapshot
        {
            Index = i, Observed = true, NativeGroundRadius = 200.2f, NativeMaximumSlope = 0,
            Segment = new BeltRouteSegment { RoutingMode = s.RoutingMode,
                Start = new() { X=s.Start.X,Y=s.Start.Y,Z=s.Start.Z }, End = new() { X=s.End.X,Y=s.End.Y,Z=s.End.Z }, StartAltitudeLevel = s.StartAltitudeLevel, EndAltitudeLevel = s.EndAltitudeLevel },
            Points = new() { Point(s.Start.X), Point(s.Start.X + 2), Point(s.Start.X + 4), Point(s.End.X) },
        }).ToList(),
    };
}
