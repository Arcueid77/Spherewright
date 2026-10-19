using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Finite geometry and interpretation of copied downward-ray evidence, not a pathfinder.</summary>
public static class MovementSurfacePreviewPolicy
{
    public const double MaximumArcMetres = 64;
    public const int MaximumSamples = 65;
    public const float RayLength = 30;
    public const float RayOriginAltitude = 10;

    public static MovementSurfaceSamplingPlan? CreatePlan(Vector3Snapshot? start, Vector3Snapshot? target, double radius)
    {
        if (!Finite(radius) || radius < 10 || radius > 10000 || !Unit(start, out var a) || !Unit(target, out var b)) return null;
        var dot = Math.Max(-1d, Math.Min(1d, a![0] * b![0] + a[1] * b[1] + a[2] * b[2]));
        // Antipodal points have no unique shortest arc, including on a small valid radius.
        if (dot <= -1d + 1e-12) return null;
        var angle = Math.Acos(dot);
        var arc = angle * radius;
        if (!Finite(arc) || arc > MaximumArcMetres + .00001) return null;
        var segments = Math.Max(1, (int)Math.Ceiling(Math.Min(MaximumArcMetres, arc)));
        var points = new List<Vector3Snapshot>();
        for (var i = 0; i <= segments; i++)
        {
            var t = (double)i / segments;
            var wa = angle < .000001 ? 1 - t : Math.Sin((1 - t) * angle) / Math.Sin(angle);
            var wb = angle < .000001 ? t : Math.Sin(t * angle) / Math.Sin(angle);
            var x = wa * a[0] + wb * b[0]; var y = wa * a[1] + wb * b[1]; var z = wa * a[2] + wb * b[2];
            var length = Math.Sqrt(x * x + y * y + z * z);
            points.Add(new Vector3Snapshot { X = (float)(x / length * radius), Y = (float)(y / length * radius), Z = (float)(z / length * radius) });
        }
        return new MovementSurfaceSamplingPlan(arc, points);
    }

    public static MovementSurfacePreview Summarize(MovementSurfaceSamplingPlan plan,
        IReadOnlyList<MovementSurfaceRayEvidence> rays, int waterItemId, long gameTick)
    {
        if (plan.Points.Count < 2 || plan.Points.Count > MaximumSamples || rays.Count != plan.Points.Count || waterItemId < 0 || gameTick < 0)
            throw new ArgumentException("surface_preview_evidence_mismatch");
        var result = new MovementSurfacePreview
        {
            State = "observed",
            CapturedAtGameTick = gameTick,
            ArcLengthMetres = plan.ArcLengthMetres,
            WaterItemId = waterItemId,
            RaycastCount = 2 * rays.Count
        };
        for (var i = 0; i < rays.Count; i++)
        {
            var ray = rays[i]; var point = plan.Points[i];
            var ground = ray.GroundHit && Distance(ray.GroundDistance) && Finite(ray.GroundAltitude)
                && Math.Abs(ray.GroundAltitude - (RayOriginAltitude - ray.GroundDistance)) <= .01;
            var water = ray.WaterHit && Distance(ray.WaterDistance);
            var sample = new MovementSurfaceSample
            {
                Position = new Vector3Snapshot { X = point.X, Y = point.Y, Z = point.Z },
                GroundHit = ground,
                WaterHit = water,
                GroundAltitudeMetres = ground ? ray.GroundAltitude : (double?)null,
                GroundBelowWaterMetres = ground && water ? ray.GroundDistance - ray.WaterDistance : (double?)null
            };
            if (ground && (waterItemId == 0 || water))
                sample.ShoreRisk = waterItemId > 0 && (ray.GroundDistance - ray.WaterDistance > .4 || ray.GroundAltitude < -.8)
                    ? "detected" : "not_detected";
            if (sample.ShoreRisk == "unknown") result.UnknownSampleCount++;
            if (sample.ShoreRisk == "detected") result.ShoreRiskSampleCount++;
            result.Samples.Add(sample);
        }
        result.State = result.UnknownSampleCount > 0 ? "partial" : "observed";
        result.ShoreRisk = result.ShoreRiskSampleCount > 0 ? "detected" : result.UnknownSampleCount > 0 ? "unknown" : "not_detected";
        return result;
    }

    private static bool Distance(double value) => Finite(value) && value >= 0 && value <= RayLength;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Unit(Vector3Snapshot? value, out double[]? unit)
    {
        unit = null;
        if (value is null || !Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) return false;
        var length = Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y + (double)value.Z * value.Z);
        if (!Finite(length) || length < 1) return false;
        unit = new[] { value.X / length, value.Y / length, value.Z / length }; return true;
    }
}

public sealed class MovementSurfaceSamplingPlan
{
    internal MovementSurfaceSamplingPlan(double arcLengthMetres, IReadOnlyList<Vector3Snapshot> points)
    { ArcLengthMetres = arcLengthMetres; Points = points; }
    public double ArcLengthMetres { get; }
    public IReadOnlyList<Vector3Snapshot> Points { get; }
}

public sealed class MovementSurfaceRayEvidence
{
    public bool GroundHit { get; set; }
    public double GroundDistance { get; set; }
    public double GroundAltitude { get; set; }
    public bool WaterHit { get; set; }
    public double WaterDistance { get; set; }
}
