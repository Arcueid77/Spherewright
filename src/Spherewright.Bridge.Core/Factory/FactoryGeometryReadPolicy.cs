using System.Text.RegularExpressions;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

public static class FactoryGeometryReadPolicy
{
    public const int MaximumObjects = 256;
    public const int MaximumTerrainPoints = 64;
    public const int MaximumColliders = 1024;
    public const int MaximumFactorySlots = 65536;
    public const int MaximumNativeBeltSegments = 16;
    public const int MaximumNativePointsPerSegment = 64;
    public const int MaximumNativeSegmentPoints = 1024;

    public static string? ValidateRequest(FactoryGeometryReadRequest? request)
    {
        if (request is null) return null;
        if (request.ObjectIds is null || request.TerrainPoints is null
            || request.ObjectIds.Count > MaximumObjects || request.TerrainPoints.Count > MaximumTerrainPoints
            || request.ObjectIds.Count + request.TerrainPoints.Count + (request.NativeBeltSegments?.Count ?? 0) == 0
            || request.ObjectIds.Distinct().Count() != request.ObjectIds.Count
            || request.ObjectIds.Any(id => id == 0 || id == int.MinValue || Math.Abs(id) >= MaximumFactorySlots)
            || request.TerrainPoints.Any(p => !ValidPoint(p))) return "factory_geometry_request_bounds_invalid";
        if (request.ExpectedFactoryGeometryHash is not null && !ValidHash(request.ExpectedFactoryGeometryHash))
            return "factory_geometry_expected_hash_invalid";
        if (request.NativeBeltSegments is { Count: > 0 } segments)
        {
            if (!ValidHash(request.ExpectedFactoryGeometryHash)) return "factory_geometry_native_segments_require_layout_hash";
            if (segments.Count > MaximumNativeBeltSegments || segments.Any(s => !ValidNativeSegment(s)))
                return "factory_geometry_native_segment_bounds_invalid";
        }
        return null;
    }

    public static bool ValidHash(string? value) => value is not null && Regex.IsMatch(value, "\\A[0-9A-Fa-f]{64}\\z");
    public static bool ValidPoint(Vector3Snapshot? p) => p is not null
        && new[] {p.X,p.Y,p.Z}.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 10000)
        && (double)p.X*p.X + (double)p.Y*p.Y + (double)p.Z*p.Z >= 1;

    public static bool ConfirmsReadOnlyEcho(FactoryGeometryReadRequest request,
        FactoryGeometryCutSnapshot? cut, string sessionId, int planetId)
    {
        if (ValidateRequest(request) is not null || cut is null || cut.Executable
            || cut.Phase != "read_only_factory_geometry_cut_v2" || cut.SessionId != sessionId
            || cut.PlanetId != planetId || cut.Revision < 0 || cut.CapturedAtGameTick < 0
            || !ValidHash(cut.FactoryGeometryHash)
            || request.ExpectedFactoryGeometryHash is { } expected && cut.FactoryGeometryHash != expected
            || cut.Objects is null || cut.Terrain is null || cut.Blockers is null || cut.NativeBeltSegments is null
            || cut.Objects.Count != request.ObjectIds.Count || cut.Terrain.Count != request.TerrainPoints.Count
            || cut.NativeBeltSegments.Count != (request.NativeBeltSegments?.Count ?? 0)
            || cut.Objects.Any(o => o is null || o.Colliders is null || o.Connections is null
                || o.Connections.Count > 16 || o.Connections.Any(c => c is null || c.Slot < 0 || c.Slot > 15)
                || o.Connections.Select(c => c.Slot).Distinct().Count() != o.Connections.Count
                || o.Colliders.Any(c => !ValidCollider(c))
                || o.Observed && (!ValidPoint(o.Position) || !ValidPoint(o.Position2)
                    || !ValidRotation(o.Rotation) || !ValidRotation(o.Rotation2) || !Finite(o.Tilt)
                    || o.GeometrySource is not ("native_build_collider_world_bounds" or "native_runtime_belt_collider_world_bounds")
                    || o.ObjectId > 0 && (o.ItemId is 2001 or 2002 or 2003) && o.GeometrySource != "native_runtime_belt_collider_world_bounds"
                    || o.GeometrySource == "native_runtime_belt_collider_world_bounds" && (o.ObjectId <= 0 || o.Colliders.Count != 1 || o.Colliders[0].NativeShape != "Box")
                    || o.ItemId<=0 || o.NativeHasBuildCollider && o.Colliders.Count==0)
                || !o.Observed && string.IsNullOrWhiteSpace(o.ReasonCode))
            || cut.Objects.Sum(o => o.Colliders.Count) > MaximumColliders
            || !cut.Objects.Select(o => o.ObjectId).SequenceEqual(request.ObjectIds)
            || cut.BuiltObjectCount < 0 || cut.PrebuildObjectCount < 0) return false;
        for (var i=0; i<cut.Terrain.Count; i++)
            if (cut.Terrain[i] is not { } p || p.Index != i || !SamePoint(p.RequestedPosition,request.TerrainPoints[i])
                || p.Observed && (!p.GroundRayHit || !ValidRadius(p.GroundRadius) || !ValidRadius(p.NativeModifiedSurfaceRadius)
                    || p.MaximumNeighbourGradient is null || double.IsNaN(p.MaximumNeighbourGradient.Value)
                    || double.IsInfinity(p.MaximumNeighbourGradient.Value) || p.MaximumNeighbourGradient<0
                    || p.WaterRayHit && !ValidRadius(p.WaterRadius)
                    || !ValidPoint(p.GroundGridSnapPosition) || p.AltitudeGridSnapPositions is null
                    || p.AltitudeGridSnapPositions.Count!=4 || p.AltitudeGridSnapPositions.Any(v=>!ValidPoint(v)))
                || !p.Observed && string.IsNullOrWhiteSpace(p.ReasonCode)) return false;
        if (cut.NativeBeltSegments.Sum(s => s?.Points?.Count ?? 0) > MaximumNativeSegmentPoints) return false;
        for (var i = 0; i < cut.NativeBeltSegments.Count; i++)
        {
            var p = cut.NativeBeltSegments[i]; var wanted = request.NativeBeltSegments![i];
            if (p is null || p.Index != i || p.Phase != "read_only_native_belt_segment_points_v1"
                || p.Executable || p.NativeConditionsChecked || !SameSegment(p.Segment, wanted) || p.Points is null
                || p.Points.Count > MaximumNativePointsPerSegment || p.Points.Any(v => !ValidPoint(v))
                || p.NativeMaximumSlope is { } slope && (!Finite(slope) || slope < 0)) return false;
            if (p.Observed)
            {
                if (p.ReasonCode is not null || !ValidRadius(p.NativeGroundRadius) || !p.NativeMaximumSlope.HasValue
                    || p.Points.Count < 2 || !SamePoint(p.Points[0], wanted.Start)
                    || !SamePoint(p.Points[p.Points.Count - 1], wanted.End)) return false;
                if (wanted.RoutingMode == BeltPathModes.NativeElevatedGrid
                    && (!BeltElevationPolicy.ValidNativeSlope(p.NativeMaximumSlope.Value)
                        || !BeltElevationPolicy.CompleteNativePath(p.Points, BeltBuildOccupancyPolicy.MaximumPathPoints,
                            wanted.Start, wanted.End, p.NativeGroundRadius!.Value))) return false;
            }
            else if (string.IsNullOrWhiteSpace(p.ReasonCode) || p.Points.Count != 0) return false;
        }
        var complete = cut.Objects.All(o => o.Observed) && cut.Terrain.All(t => t.Observed)
            && cut.NativeBeltSegments.All(s => s.Observed);
        return complete ? cut.State == "observed" && cut.Blockers.Count == 0
            : cut.State == "unavailable" && cut.Blockers.Count > 0;
    }

    public static bool SamePoint(Vector3Snapshot? a, Vector3Snapshot? b) => a is not null && b is not null
        && Math.Abs(a.X-b.X) <= .001f && Math.Abs(a.Y-b.Y) <= .001f && Math.Abs(a.Z-b.Z) <= .001f;

    private static bool ValidNativeSegment(BeltRouteSegment? s) => s is not null && ValidPoint(s.Start) && ValidPoint(s.End)
        && (s.RoutingMode == BeltPathModes.NativeGrid && s.StartAltitudeLevel == 0 && s.EndAltitudeLevel == 0
            || s.RoutingMode == BeltPathModes.NativeElevatedGrid && BeltElevationPolicy.ValidLevels(s.StartAltitudeLevel, s.EndAltitudeLevel))
        && DistanceSquared(s.Start, s.End) >= 1.5 * 1.5 && DistanceSquared(s.Start, s.End) <= 30 * 30;

    private static bool SameSegment(BeltRouteSegment? a, BeltRouteSegment b) => a is not null
        && a.RoutingMode == b.RoutingMode && a.StartAltitudeLevel == b.StartAltitudeLevel && a.EndAltitudeLevel == b.EndAltitudeLevel
        && SamePoint(a.Start, b.Start) && SamePoint(a.End, b.End);

    private static double DistanceSquared(Vector3Snapshot a, Vector3Snapshot b) =>
        Math.Pow((double)a.X-b.X, 2) + Math.Pow((double)a.Y-b.Y, 2) + Math.Pow((double)a.Z-b.Z, 2);

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool ValidRotation(QuaternionSnapshot? q) => q is not null
        && new[] { q.X, q.Y, q.Z, q.W }.All(Finite)
        && Math.Abs((double)q.X*q.X + (double)q.Y*q.Y + (double)q.Z*q.Z + (double)q.W*q.W - 1) <= .002;

    private static bool ValidRadius(float? radius) => radius.HasValue && !float.IsNaN(radius.Value)
        && !float.IsInfinity(radius.Value) && radius>1 && radius<=10000;
    private static bool ValidVector(Vector3Snapshot? v) => v is not null
        && new[]{v.X,v.Y,v.Z}.All(n=>!float.IsNaN(n) && !float.IsInfinity(n) && Math.Abs(n)<=10000);
    private static bool Unit(Vector3Snapshot? v) => ValidVector(v)
        && Math.Abs((double)v!.X*v.X+(double)v.Y*v.Y+(double)v.Z*v.Z-1)<=.002;
    private static bool ValidCollider(FactoryBuildColliderSnapshot? c) => c is not null
        && c.NativeShape is "Box" or "Sphere" or "Capsule" && (c.NativeShape=="Box" || c.ConservativeEnclosure)
        && ValidPoint(c.Centre) && Unit(c.AxisX) && Unit(c.AxisY) && Unit(c.AxisZ)
        && ValidVector(c.HalfExtents) && c.HalfExtents.X>0 && c.HalfExtents.Y>0 && c.HalfExtents.Z>0
        && !float.IsNaN(c.PaddingMetres) && c.PaddingMetres>=0 && c.PaddingMetres<=.011f;
}
