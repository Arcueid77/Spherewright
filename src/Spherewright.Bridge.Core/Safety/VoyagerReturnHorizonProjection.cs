namespace Spherewright.Bridge.Core.Safety;

// A confirmed task may outlive one flight. Allocate only the original finite
// return leg; retain the actual task remainder separately, without modifying
// its spending, immutable journey limit, authorization or reserve.
public static class VoyagerReturnHorizonProjection
{
    public static long? PlanTicks(long protectedRemaining,long originalReturnLeg)
        => protectedRemaining>=0 && originalReturnLeg>=VoyagerRouteBudgetPolicy.MinimumLegGameTicks
            && originalReturnLeg<=VoyagerRouteBudgetPolicy.MaximumFlightGameTicks
                ? Math.Min(protectedRemaining,originalReturnLeg):null;
}
