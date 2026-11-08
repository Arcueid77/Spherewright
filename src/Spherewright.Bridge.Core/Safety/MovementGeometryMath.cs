using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Safety;

internal readonly struct MovementVector
{
    internal readonly double X;
    internal readonly double Y;
    internal readonly double Z;
    internal MovementVector(double x, double y, double z) { X = x; Y = y; Z = z; }
    internal MovementVector(Vector3Snapshot value) : this(value.X, value.Y, value.Z) { }
    internal double LengthSquared => X * X + Y * Y + Z * Z;
    internal double Length => Math.Sqrt(LengthSquared);
    internal MovementVector Unit => Length > 1e-9 ? this / Length : default;
    internal Vector3Snapshot Snapshot() => new() { X = (float)X, Y = (float)Y, Z = (float)Z };
    public static MovementVector operator +(MovementVector a, MovementVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static MovementVector operator -(MovementVector a, MovementVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static MovementVector operator *(MovementVector a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);
    public static MovementVector operator /(MovementVector a, double scale) => a * (1 / scale);
    internal static double Dot(MovementVector a, MovementVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    internal static MovementVector Cross(MovementVector a, MovementVector b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}

internal static class MovementGeometryMath
{
    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    internal static double Clamp(double value, double lower, double upper) => Math.Max(lower, Math.Min(upper, value));

    internal static bool ValidPoint(Vector3Snapshot? value) => value is not null
        && Finite(value.X) && Finite(value.Y) && Finite(value.Z)
        && new MovementVector(value).Length <= 12000;

    internal static bool ValidCollider(WalkingColliderSnapshot? value)
    {
        if (value is null || !ValidPoint(value.Centre) || !ValidPoint(value.Extents) || value.Rotation is null
            || !Finite(value.Radius) || value.Radius < 0 || value.Radius > 128) return false;
        if (value.Shape == "sphere" || value.Shape == "capsule") return value.Radius > 0;
        if (value.Shape != "box" || value.Extents.X <= 0 || value.Extents.Y <= 0 || value.Extents.Z <= 0) return false;
        var q = value.Rotation;
        var norm = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        return Finite(norm) && Math.Abs(norm - 1) < .001;
    }

    internal static double BodyClearance(MovementGeometrySnapshot geometry, MovementVector foot,
        WalkingColliderSnapshot collider)
    {
        var minimum = double.PositiveInfinity;
        foreach (var shape in geometry.Body)
        {
            var a = foot + foot.Unit * shape.LowerCentreHeight;
            var b = foot + foot.Unit * shape.UpperCentreHeight;
            var centre = new MovementVector(collider.Centre);
            double distance;
            if (collider.Shape == "sphere")
                distance = SegmentPointDistance(a, b, centre) - collider.Radius;
            else if (collider.Shape == "capsule")
            {
                var axis = new MovementVector(collider.Extents);
                distance = SegmentDistance(a, b, centre - axis, centre + axis) - collider.Radius;
            }
            else
            {
                var start = InverseRotate(a - centre, collider.Rotation);
                var end = InverseRotate(b - centre, collider.Rotation);
                distance = SegmentBoxDistance(start, end, new MovementVector(collider.Extents));
            }
            minimum = Math.Min(minimum, distance - shape.Radius - .0001);
        }
        return minimum;
    }

    internal static MovementVector Rotate(MovementVector value, QuaternionSnapshot rotation, bool inverse = false)
    {
        var norm = Math.Sqrt(rotation.X * rotation.X + rotation.Y * rotation.Y
            + rotation.Z * rotation.Z + rotation.W * rotation.W);
        var scale = inverse ? -1 / norm : 1 / norm;
        var axis = new MovementVector(rotation.X * scale, rotation.Y * scale, rotation.Z * scale);
        var cross = MovementVector.Cross(axis, value) * 2;
        return value + cross * (rotation.W / norm) + MovementVector.Cross(axis, cross);
    }

    private static MovementVector InverseRotate(MovementVector value, QuaternionSnapshot rotation) => Rotate(value, rotation, true);

    internal static double SegmentPointDistance(MovementVector a, MovementVector b, MovementVector point)
    {
        var direction = b - a;
        var t = direction.LengthSquared <= 1e-12 ? 0
            : Clamp(MovementVector.Dot(point - a, direction) / direction.LengthSquared, 0, 1);
        return (a + direction * t - point).Length;
    }

    private static double SegmentDistance(MovementVector p, MovementVector q, MovementVector r, MovementVector s)
    {
        var first = q - p;
        var second = s - r;
        var offset = p - r;
        var a = first.LengthSquared;
        var e = second.LengthSquared;
        var f = MovementVector.Dot(second, offset);
        double u;
        double v;
        if (a <= 1e-12)
        {
            u = 0;
            v = e <= 1e-12 ? 0 : Clamp(f / e, 0, 1);
        }
        else
        {
            var c = MovementVector.Dot(first, offset);
            if (e <= 1e-12) { v = 0; u = Clamp(-c / a, 0, 1); }
            else
            {
                var b = MovementVector.Dot(first, second);
                var denominator = a * e - b * b;
                u = denominator > 1e-12 ? Clamp((b * f - c * e) / denominator, 0, 1) : 0;
                v = (b * u + f) / e;
                if (v < 0) { v = 0; u = Clamp(-c / a, 0, 1); }
                else if (v > 1) { v = 1; u = Clamp((b - c) / a, 0, 1); }
            }
        }
        return (p + first * u - r - second * v).Length;
    }

    private static double SegmentBoxDistance(MovementVector start, MovementVector end, MovementVector half)
    {
        var p = new[] { start.X, start.Y, start.Z };
        var d = new[] { end.X - start.X, end.Y - start.Y, end.Z - start.Z };
        var h = new[] { half.X, half.Y, half.Z };
        var cuts = new List<double> { 0, 1 };
        for (var axis = 0; axis < 3; axis++)
        {
            if (Math.Abs(d[axis]) < 1e-12) continue;
            foreach (var boundary in new[] { -h[axis], h[axis] })
            {
                var t = (boundary - p[axis]) / d[axis];
                if (t > 0 && t < 1) cuts.Add(t);
            }
        }
        cuts.Sort();
        var best = cuts.Min(SquaredDistance);
        for (var index = 1; index < cuts.Count; index++)
        {
            var low = cuts[index - 1];
            var high = cuts[index];
            var mid = (low + high) / 2;
            var numerator = 0d;
            var denominator = 0d;
            for (var axis = 0; axis < 3; axis++)
            {
                var value = p[axis] + d[axis] * mid;
                if (Math.Abs(value) <= h[axis]) continue;
                var residual = p[axis] - Math.Sign(value) * h[axis];
                numerator += d[axis] * residual;
                denominator += d[axis] * d[axis];
            }
            if (denominator > 1e-12)
                best = Math.Min(best, SquaredDistance(Clamp(-numerator / denominator, low, high)));
        }
        return Math.Sqrt(best);

        double SquaredDistance(double t)
        {
            var sum = 0d;
            for (var axis = 0; axis < 3; axis++)
            {
                var outside = Math.Max(0, Math.Abs(p[axis] + d[axis] * t) - h[axis]);
                sum += outside * outside;
            }
            return sum;
        }
    }
}
