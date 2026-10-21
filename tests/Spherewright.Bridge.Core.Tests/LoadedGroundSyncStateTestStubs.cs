// TEST ONLY. The production retained-player predicate is linked unchanged.
// These value/reference substitutes do not execute Unity or prove Native timing.
namespace UnityEngine
{
    internal record struct Vector3(float x, float y, float z);
    internal record struct Vector4(float x, float y, float z, float w);
    internal record struct Quaternion(float x, float y, float z, float w);
    internal sealed class Transform
    {
        internal Transform? parent;
        internal Quaternion localRotation = new(0, 0, 0, 1);
        internal Vector3 lossyScale = new(1, 1, 1);
    }
    internal sealed class Rigidbody
    {
        internal Vector3 velocity, angularVelocity;
    }
    internal static class Physics
    {
        internal static bool autoSyncTransforms, queriesHitTriggers;
    }
}

namespace Spherewright.Plugin.Game
{
    internal static partial class GameMain { internal static bool isPaused; }
    internal record struct VectorLF3(double x, double y, double z)
    {
        public static VectorLF3 operator +(VectorLF3 a, VectorLF3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static implicit operator VectorLF3(UnityEngine.Vector3 v) => new(v.x, v.y, v.z);
    }
    internal static class Maths
    {
        // Copy the audited Native arithmetic, including the float q.w*q.w
        // product before conversion to double. This is not a Unity runtime.
        internal static VectorLF3 QRotateLF(UnityEngine.Quaternion q, VectorLF3 v)
        {
            v.x *= 2.0; v.y *= 2.0; v.z *= 2.0;
            double n = (double)(q.w * q.w) - 0.5;
            double d = (double)q.x * v.x + (double)q.y * v.y + (double)q.z * v.z;
            return new(
                v.x * n + ((double)q.y * v.z - (double)q.z * v.y) * (double)q.w + (double)q.x * d,
                v.y * n + ((double)q.z * v.x - (double)q.x * v.z) * (double)q.w + (double)q.y * d,
                v.z * n + ((double)q.x * v.y - (double)q.y * v.x) * (double)q.w + (double)q.z * d);
        }
    }
    internal enum EMovementState { Walk, Drift }
    internal sealed class Player
    {
        internal PlayerController controller = new();
        internal UnityEngine.Transform transform = new();
        internal UnityEngine.Vector3 position = new(0, 200, 0);
        internal VectorLF3 uPosition = new(1000, 2000, 3000);
        internal UnityEngine.Quaternion uRotation = new(0, 0, 0, 1);
        internal object? currentOrder;
        internal PlayerOrders orders = new();
        internal Mecha mecha = new();
        internal float speed, warpState;
        internal bool warpCommand;
        internal EMovementState movementState;
    }
    internal sealed class PlayerController
    {
        internal UnityEngine.Rigidbody rigid = new();
        internal UnityEngine.Vector3 velocity;
        internal UnityEngine.Vector4 input0, input1;
        internal float vertSpeed;
        internal EMovementState movementStateInFrame;
    }
    internal sealed class PlayerOrders { internal int orderCount; }
    internal sealed class Mecha
    {
        internal double coreEnergy = 100, reactorEnergy = 200, LoadedEnergy = 500;
        internal int LoadedReads;
    }
    internal sealed partial class NormalGameActionCoordinator
    {
        internal static bool GroundTestRawInputIdle = true;
        private static bool LocalRawMovementInputIdle() => GroundTestRawInputIdle;
        private static double CalculateAvailableFlightEnergy(Mecha mecha)
        { mecha.LoadedReads++; return mecha.LoadedEnergy; }

        internal sealed class GroundStateProbe
        {
            private readonly LoadedGroundSyncState _state;
            internal GroundStateProbe(Player player) { _state = new LoadedGroundSyncState(player); }
            internal GroundStateProbe(Player player, VectorLF3 relativePosition, UnityEngine.Quaternion relativeRotation)
            { _state = LoadedGroundSyncState.FromNativeFrame(player, relativePosition, relativeRotation); }
            internal bool Finite => _state.Finite;
            internal bool Matches(Player player) => _state.Matches(player);
            internal bool DuringPause(Player player) => _state.MatchesDuringNativeInitializationPause(player);
            internal string? Blocker(Player player, long tick) => _state.CurrentBlockerAtTick(player, tick);
            internal string? Universe(Player player, long tick, out VectorLF3 observed)
                => _state.CurrentBlockerAtNativeUniverseTick(player, tick, out observed);
            internal bool Pin(VectorLF3 observed) => _state.TryPinNativeUniversePosition(observed);
            internal string? Physics(Player player, long tick) => _state.CurrentBlockerAtNativePhysicsTick(player, tick);
        }
    }
}
