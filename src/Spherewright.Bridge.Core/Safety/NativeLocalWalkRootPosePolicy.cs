using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// This is the narrow residual that Native UpdateUniversalPosition may retain
// when Vector3's approximate equality suppresses assigning Vector3.one. It is
// not a tolerance on arbitrary transforms or a player/target distance bound.
public static class NativeLocalWalkRootPosePolicy
{
    public static float? UniformContractionScale(NativePlanetTerrainPhysicsSnapshot pose)
    {
        if (pose is null) throw new ArgumentNullException(nameof(pose));
        var local = pose.RootLocalScale;
        var world = pose.RootWorldLossyScale;
        var matrix = pose.RootLocalToWorldMatrix;
        if (pose.RootHasParent != false || local is null || world is null
            || pose.RootLocalPosition is not { X: 0f, Y: 0f, Z: 0f }
            || pose.RootWorldPosition is not { X: 0f, Y: 0f, Z: 0f }
            || pose.RootLocalRotation is not { X: 0f, Y: 0f, Z: 0f, W: 1f }
            || pose.RootWorldRotation is not { X: 0f, Y: 0f, Z: 0f, W: 1f }
            || !(local.X > 0f && local.X < 1f) || local.Y != local.X || local.Z != local.X
            || world.X != local.X || world.Y != local.X || world.Z != local.X
            || matrix is null || matrix.Count != 16) return null;
        var delta = local.X - 1f;
        var square = delta * delta;
        // Exact float expression/strict threshold from legal Native Vector3.
        if (!(square + square + square < 9.9999994E-11f)) return null;
        for (var i = 0; i < 16; i++)
        {
            var expected = i == 15 ? 1f : i is 0 or 5 or 10 ? local.X : 0f;
            if (matrix[i] != expected) return null; // Includes NaN/infinity/shear/translation.
        }
        return local.X;
    }
}
