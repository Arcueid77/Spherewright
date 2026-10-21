using UnityEngine;

namespace Spherewright.Plugin.Game;

// Main-thread observation only. Native ApplyGravity rotates the actor each601;
// lossyScale is a decomposition of that rotation and is not bitwise invariant.
// Freeze the actual scale inputs and the complete parent chain instead. Uniform
// ancestor scales exclude rotation-induced shear; no tolerance hides a resize.
internal sealed class NativeContactTransformBinding
{
    private const int MaximumDepth = 32;
    private readonly Transform[] _chain;
    private readonly Vector3[] _scales;

    private NativeContactTransformBinding(Transform[] chain, Vector3[] scales)
    { _chain = chain; _scales = scales; }

    public static NativeContactTransformBinding? Capture(Transform transform)
    {
        var chain = new List<Transform>();
        var scales = new List<Vector3>();
        for (var node = transform; node != null; node = node.parent)
        {
            if (chain.Count == MaximumDepth || chain.Any(t => ReferenceEquals(t, node))) return null;
            var scale = node.localScale;
            if (!PoseFinite(node) || !PositiveFinite(scale.x) || !PositiveFinite(scale.y) || !PositiveFinite(scale.z)
                || chain.Count > 0 && (scale.x != scale.y || scale.y != scale.z)) return null;
            chain.Add(node); scales.Add(scale);
        }
        return chain.Count == 0 ? null : new NativeContactTransformBinding(chain.ToArray(), scales.ToArray());
    }

    public bool Matches(Transform transform)
    {
        if (!ReferenceEquals(transform, _chain[0])) return false;
        for (var i = 0; i < _chain.Length; i++)
        {
            var node = _chain[i];
            if (node == null || !PoseFinite(node) || !node.localScale.Equals(_scales[i])
                || !ReferenceEquals(node.parent, i + 1 < _chain.Length ? _chain[i + 1] : null)) return false;
        }
        return true;
    }

    private static bool PositiveFinite(float value) => value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
    private static bool Finite(float value) => !float.IsInfinity(value) && !float.IsNaN(value);
    private static bool PoseFinite(Transform node)
    {
        var p = node.localPosition; var q = node.localRotation;
        return Finite(p.x) && Finite(p.y) && Finite(p.z)
            && Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w)
            && q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w is > .99f and < 1.01f;
    }
}
