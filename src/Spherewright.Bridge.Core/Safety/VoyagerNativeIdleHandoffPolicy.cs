namespace Spherewright.Bridge.Core.Safety;

// The original Move retains its already-funded stop while the controller
// settles. A terminal Move and the immediately following idle owner must use
// the same physical-motion predicate; otherwise a transient rigid-body tail
// can make a completed Move quarantine the same frame.
public static class VoyagerNativeIdleHandoffPolicy
{
    // Tiny Native physics noise is not an RTS order. At this bound even the
    // full13-tick local stop spans less than three micrometres. Real commands,
    // ground contact, original budgets and the later anchor checks remain
    // independent requirements.
    public const double MaximumResidualSpeed = .00001d;
    private const double MaximumResidualSquared = MaximumResidualSpeed * MaximumResidualSpeed;
    private static bool TinySquared(double value) => value >= 0d && value <= MaximumResidualSquared;

    public static bool PhysicallyAtRest(bool actualWalk, bool frameWalk, double speed,
        double controllerVelocitySquared, double rigidVelocitySquared, double angularVelocitySquared,
        double verticalSpeed, double walkRtsVelocitySquared, double driftRtsVelocitySquared) =>
        actualWalk && frameWalk && speed >= 0d && speed <= (double).1f
        && TinySquared(controllerVelocitySquared) && TinySquared(rigidVelocitySquared) && TinySquared(angularVelocitySquared)
        && Math.Abs(verticalSpeed) <= MaximumResidualSpeed && walkRtsVelocitySquared == 0d && driftRtsVelocitySquared == 0d;
}
