namespace Spherewright.Bridge.Core.Safety;

// The original Move retains its already-funded stop while the controller
// settles. A terminal Move and the immediately following idle owner must use
// the same physical-motion predicate; otherwise a transient rigid-body tail
// can make a completed Move quarantine the same frame.
public static class VoyagerNativeIdleHandoffPolicy
{
    public static bool PhysicallyAtRest(bool actualWalk, bool frameWalk, double speed,
        double controllerVelocitySquared, double rigidVelocitySquared, double angularVelocitySquared,
        double verticalSpeed, double walkRtsVelocitySquared, double driftRtsVelocitySquared) =>
        actualWalk && frameWalk && speed >= 0d && speed <= (double).1f
        && controllerVelocitySquared == 0d && rigidVelocitySquared == 0d && angularVelocitySquared == 0d
        && verticalSpeed == 0d && walkRtsVelocitySquared == 0d && driftRtsVelocitySquared == 0d;
}
