using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// No Unity/native updates: exercise copied costs against independent concrete
// RTS-stop trajectories. The Plugin must separately prove the mode predicates.
public sealed class VoyagerLocalWalkReservePolicyTests
{
    [Fact]
    public void SeparatelyBoundNativeDroneDebitPaysEveryTickWithoutApplyingTheMechaConsumptionRatioAgain()
    {
        var f=Facts();f.ReactorConsumptionRatio=2;
        var before=VoyagerLocalWalkReservePolicy.Evaluate(f);
        f.AdditionalNativePowerUpperWatts=60000;
        var after=VoyagerLocalWalkReservePolicy.Evaluate(f);
        Assert.True(before.Proved && after.Proved);
        Assert.Equal(60000*VoyagerLocalWalkReservePolicy.NativeTickSeconds,
            after.PerNativeTickEnergyJoules!.Value-before.PerNativeTickEnergyJoules!.Value,5);
        Assert.Equal(13*(after.PerNativeTickEnergyJoules.Value-before.PerNativeTickEnergyJoules.Value),
            after.StopEnergyJoules!.Value-before.StopEnergyJoules!.Value,5);
        f.AdditionalNativePowerUpperWatts=double.NaN;
        Assert.False(VoyagerLocalWalkReservePolicy.Evaluate(f).Proved);
        f.AdditionalNativePowerUpperWatts=-1;
        Assert.False(VoyagerLocalWalkReservePolicy.Evaluate(f).Proved);
    }
    [Theory]
    [InlineData(1f)] [InlineData(6f)] [InlineData(12f)] [InlineData(60f)]
    public void PaidThirteenTickMarginCoversOneActiveTickTheEntireDecliningNativeRtsTailAndOneSpare(float speed)
    {
        var f = Facts(); f.WalkSpeed = speed; f.RtsSpeed = speed + .009f;
        var r = VoyagerLocalWalkReservePolicy.Evaluate(f); Assert.True(r.Proved);
        var remaining = speed + .009f; var ticks = 0;
        double paid = ActualCost(f, remaining); // Last active order tick.
        while (remaining > 0f)
        {
            remaining = Math.Max(0f, remaining - (1f / 60f) * 6f * speed);
            paid += ActualCost(f, remaining); ticks++;
        }
        Assert.InRange(ticks, 1, 11);
        paid += ActualCost(f, speed + .01f); // Deliberately charge another worst tick.
        Assert.True(paid <= r.StopEnergyJoules);
        Assert.Equal(0f, remaining); // An Abort flag alone was not our stop evidence.
    }

    [Theory]
    [InlineData("fly_or_jump")] [InlineData("manual_inertia")] [InlineData("rts_outside")]
    [InlineData("soft_landing_boost")] [InlineData("unknown_cost")] [InlineData("nan_cost")]
    [InlineData("negative_ratio")] [InlineData("overflow")]
    public void UnprovedNativeBranchCannotBorrowAPlainWalkCertificate(string fault)
    {
        var f = Facts();
        if (fault == "fly_or_jump") f.ModeTransitionsExcluded = false;
        if (fault == "manual_inertia") f.ManualSpeed = .001;
        if (fault == "rts_outside") f.RtsSpeed = 13;
        if (fault == "soft_landing_boost") f.SoftLandingRecover = 1.001;
        if (fault == "unknown_cost") f.ReactorConsumptionRatio = null;
        if (fault == "nan_cost") f.WalkPowerWatts = double.NaN;
        if (fault == "negative_ratio") f.ReactorConsumptionRatio = -1;
        if (fault == "overflow") f.ReactorConsumptionRatio = double.MaxValue;
        var r = VoyagerLocalWalkReservePolicy.Evaluate(f);
        Assert.False(r.Proved); Assert.Null(r.StopEnergyJoules);
    }

    [Fact]
    public void HighWalkSpeedAndReactorRatioIncludeBothNativeMultipliersAndFloatTickDuration()
    {
        var f = Facts(); f.WalkSpeed = 60; f.RtsSpeed = 60; f.ReactorConsumptionRatio = 4;
        var r = VoyagerLocalWalkReservePolicy.Evaluate(f);
        Assert.True(r.Proved);
        Assert.True(r.PerNativeTickEnergyJoules > (1000 * 10 + 200 + 100) * 4d / 60d);
        Assert.True(r.WorkPowerUpperWatts > (1000 * 10 + 200 + 100) * 4d);
        // Loaded fuel does not make core0 capable of paying this immediate tail.
        Assert.False(VoyagerLocalWalkReservePolicy.CanFund(0, 1e10, 1e9, r.PerNativeTickEnergyJoules, 13));
    }

    [Fact]
    public void RepeatedWorkStopsBeforeFloorAndOnlyTheOriginalDecliningTailMayUseItsReservedMargin()
    {
        var r = VoyagerLocalWalkReservePolicy.Evaluate(Facts()); var e = r.PerNativeTickEnergyJoules!.Value;
        const double floor = 5e9;
        var loaded = floor + e * 14;
        Assert.True(VoyagerLocalWalkReservePolicy.CanFund(e * 14, loaded, floor, e, 13));
        loaded -= e * 1.5; // Next full13-tick certificate no longer fits.
        Assert.False(VoyagerLocalWalkReservePolicy.CanFund(e * 14, loaded, floor, e, 13));
        Assert.True(VoyagerLocalWalkReservePolicy.CanFund(e * 14, loaded, floor, e, 12));
        Assert.False(VoyagerLocalWalkReservePolicy.CanFund(e * 14, loaded, floor, e, 14));
        Assert.False(VoyagerLocalWalkReservePolicy.CanFund(double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, 13));
    }

    [Fact]
    public void LocalWallWindowAlsoReservesTheSameStopTailBeforeReturnTime()
    {
        var now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = VoyagerLocalActionBudgetPolicy.WorkDeadlineUtc(now, now.AddSeconds(950), 7200, 54000, 13);
        Assert.Equal(now.AddTicks(50 * TimeSpan.TicksPerSecond - 2_166_667), end);
    }

    [Theory]
    [InlineData(120d, 110d)] [InlineData(120d, 140d)] [InlineData(200d, 201d)] [InlineData(6000d, 6020d)]
    public void WholeNativeWaterRayFitsTheQueriedBallIndependentlyOfSurfaceDirection(double radius, double playerRadius)
    {
        Assert.True(VoyagerLocalWalkReservePolicy.WaterRayFitsQueriedBall(radius, playerRadius));
        // Native ray is p+10u -> p-20u; every intermediate point is in this
        // convex ball if both ends are. No future displacement is invented.
        for (var step = 0; step <= 30; step++)
            Assert.True(Math.Abs(playerRadius + 10 - step) < radius + 40);
    }

    [Theory]
    [InlineData(120d, 160d)] [InlineData(200d, 189d)] [InlineData(100d, 100d)]
    [InlineData(200d, double.NaN)] [InlineData(double.PositiveInfinity, 200d)]
    public void AFootPointOrPlanetNameCannotCertifyUnboundedOrUnknownRayCoverage(double radius, double playerRadius) =>
        Assert.False(VoyagerLocalWalkReservePolicy.WaterRayFitsQueriedBall(radius, playerRadius));

    [Fact]
    public void PotentialDriftIsOptInAndDoesNotChangeTheOriginalWalkCost()
    {
        var original = VoyagerLocalWalkReservePolicy.Evaluate(Facts());
        var facts = Facts();
        facts.NativeGravityUpperBound = double.NaN;
        facts.NativeThrustPowerPerAcceleration = double.PositiveInfinity;
        var unchanged = VoyagerLocalWalkReservePolicy.Evaluate(facts);
        Assert.True(unchanged.Proved);
        Assert.Equal(original.PerNativeTickEnergyJoules, unchanged.PerNativeTickEnergyJoules);
        Assert.Equal(original.WorkPowerUpperWatts, unchanged.WorkPowerUpperWatts);
        Assert.Equal(original.StopEnergyJoules, unchanged.StopEnergyJoules);
    }

    [Theory]
    [InlineData(1f, 1d)] [InlineData(6f, 4d)] [InlineData(12f, 2d)] [InlineData(60f, 4d)]
    public void PotentialDriftIncludesFullHorizontalMultipliersThrustAndExistingWork(float speed, double ratio)
    {
        var facts = DriftFacts(); facts.WalkSpeed = speed; facts.RtsSpeed = speed; facts.ReactorConsumptionRatio = ratio;
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(facts);
        Assert.True(reserve.Proved);
        // Native UseDriftEnergy: ratio-to-walkSpeed and max(1, speed/6),
        // with the input/RTS clamp pessimistically charged as1. The deliberately
        // wider two-speed allowance covers the bounded one-tick transition.
        var velocity = speed * 2f;
        var horizontal = facts.WalkPowerWatts!.Value * (double)(1f / 60f)
            * (double)(velocity / speed) * (double)Math.Max(1f, velocity / 6f) * ratio;
        var force = 1000f + (float)facts.NativeGravityUpperBound!.Value;
        var thrust = facts.NativeThrustPowerPerAcceleration!.Value * ratio * (1d / 60d) * force;
        var other = (facts.MiningPowerWatts!.Value + facts.OtherWorkPowerWatts!.Value) * ratio * (1f / 60f);
        Assert.True(reserve.PerNativeTickEnergyJoules > horizontal + thrust + other);
        Assert.True(reserve.WorkPowerUpperWatts >= reserve.PerNativeTickEnergyJoules * 60d);
    }

    [Fact]
    public void ZeroRtsDoesNotMakePotentialDriftStopAfterThirteenTicks()
    {
        var facts = DriftFacts(); facts.WalkPowerWatts = facts.MiningPowerWatts = facts.OtherWorkPowerWatts = 0;
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(facts);
        var actualThrustPerTick = facts.NativeThrustPowerPerAcceleration!.Value * facts.ReactorConsumptionRatio!.Value
            * (1d / 60d) * (1000f + (float)facts.NativeGravityUpperBound!.Value);
        Assert.True(reserve.Proved); Assert.True(reserve.PerNativeTickEnergyJoules > 0);
        Assert.True(actualThrustPerTick * 13 <= reserve.StopEnergyJoules);
        Assert.True(actualThrustPerTick * 14 > reserve.StopEnergyJoules);
        // The consumer still needs dry Walk/idle or Native pause. This budget
        // is not a declaration that Drift's ongoing vertical work has stopped.
    }

    [Theory]
    [InlineData("missing_thrust")] [InlineData("missing_gravity")]
    [InlineData("negative_thrust")] [InlineData("negative_gravity")]
    [InlineData("nan_thrust")] [InlineData("infinite_gravity")]
    [InlineData("float_gravity_overflow")] [InlineData("cost_overflow")]
    public void PotentialDriftRequiresFiniteNativeFactsAndFiniteFloatForce(string fault)
    {
        var facts = DriftFacts();
        if (fault == "missing_thrust") facts.NativeThrustPowerPerAcceleration = null;
        if (fault == "missing_gravity") facts.NativeGravityUpperBound = null;
        if (fault == "negative_thrust") facts.NativeThrustPowerPerAcceleration = -1;
        if (fault == "negative_gravity") facts.NativeGravityUpperBound = -1;
        if (fault == "nan_thrust") facts.NativeThrustPowerPerAcceleration = double.NaN;
        if (fault == "infinite_gravity") facts.NativeGravityUpperBound = double.PositiveInfinity;
        if (fault == "float_gravity_overflow") facts.NativeGravityUpperBound = 1e100;
        if (fault == "cost_overflow") facts.NativeThrustPowerPerAcceleration = double.MaxValue;
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(facts);
        Assert.False(reserve.Proved); Assert.Equal("native_potential_drift_cost_unproved", reserve.BlockerCode);
        Assert.Null(reserve.PerNativeTickEnergyJoules);
    }

    [Fact]
    public void ForceUpperUsesTheActualNativeFloatRoundingRatherThanAnArbitraryMargin()
    {
        const double gravity = 16_777_219d; // Native float conversion rounds upward.
        var force = 1000f + (float)gravity;
        var power = VoyagerLocalWalkReservePolicy.CalculatePotentialDriftPowerUpperBound(1, gravity, 1);
        Assert.Equal((double)force, power);
        Assert.True(power > 1000d + gravity);
        Assert.Null(VoyagerLocalWalkReservePolicy.CalculatePotentialDriftPowerUpperBound(1, double.MaxValue, 1));
        Assert.Null(VoyagerLocalWalkReservePolicy.CalculatePotentialDriftPowerUpperBound(1, 10, 0));
        Assert.Null(VoyagerLocalWalkReservePolicy.CalculatePotentialDriftPowerUpperBound(null, 10, 1));
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(64)]
    public void GlobalGravityCoversNativePositiveWeightedAverageAtNearSurfaceCutoffAndFarDistances(int count)
    {
        var radii = Enumerable.Range(0, count).Select(i => i % 2 == 0 ? 120d : 6000d).ToArray();
        var upper = VoyagerLocalWalkReservePolicy.CalculateNativeGravityUpperBound(radii, 1, 1200);
        Assert.NotNull(upper);
        for (var distanceCase = 0; distanceCase < 4; distanceCase++)
        {
            var numerator = 0d; var denominator = 0d;
            foreach (var radius in radii)
            {
                var distance = distanceCase switch { 0 => 1.001d, 1 => radius, 2 => 5000d + radius - .01d, _ => 100_000d };
                var y = Math.Max((5000f + (float)radius - (float)distance) / 2500f, 0f);
                var weight = Math.Pow(11d, y) - 1d;
                var distanceRatio = distance / radius;
                var scaled = distanceRatio * 0.800000011920929;
                var factor = Math.Min(1d, distanceRatio < 1d ? distanceRatio : 1d / (scaled * scaled));
                // Aligned directions maximize the weighted-vector magnitude;
                // cancellation in the actual vector sum only reduces it.
                numerator += Math.Sqrt(radius) * 3.5d * factor * weight;
                denominator += weight;
            }
            var starDistance = distanceCase == 3 ? 1_000_000d : 2d;
            var starDistanceBound = Math.Max(starDistance, 1200d);
            numerator += 64000000000000d * 1.3538551990520382E-06 * 4d / (starDistanceBound * starDistanceBound);
            denominator += 1d;
            Assert.True(denominator < 2.6e6);
            Assert.True(numerator / denominator < upper);
        }
    }

    [Fact]
    public void GlobalGravityIncludesTheStarAtItsPhysicsRadiusAndAllSixtyFourPlanets()
    {
        var radii = Enumerable.Repeat(6000d, 64).ToArray();
        var star = 64000000000000d * 2d * 1.3538551990520382E-06 * 4d / (500d * 500d);
        var upper = VoyagerLocalWalkReservePolicy.CalculateNativeGravityUpperBound(radii, 2, 500);
        Assert.True(upper > star);
        Assert.True(upper > Math.Sqrt(6000d) * 3.5d);
        Assert.Equal(4d * (Math.Sqrt(6000d) * 3.5d + star + 1d), upper);
    }

    [Theory]
    [InlineData("null_planets")] [InlineData("empty_planets")] [InlineData("too_many_planets")]
    [InlineData("small_planet")] [InlineData("large_planet")] [InlineData("nan_planet")]
    [InlineData("infinite_planet")] [InlineData("missing_mass")] [InlineData("zero_mass")]
    [InlineData("negative_mass")] [InlineData("nan_mass")] [InlineData("infinite_mass")]
    [InlineData("missing_physics_radius")] [InlineData("zero_physics_radius")]
    [InlineData("negative_physics_radius")] [InlineData("nan_physics_radius")]
    [InlineData("infinite_physics_radius")] [InlineData("squared_radius_overflow")]
    [InlineData("squared_radius_underflow")] [InlineData("numerator_overflow")]
    [InlineData("float_gravity_overflow")]
    public void GlobalGravityRefusesUnknownUnsupportedAndOverflowingNativeInputs(string fault)
    {
        IReadOnlyList<double>? radii = new[] { 200d }; double? mass = 1; double? physicsRadius = 1200;
        if (fault == "null_planets") radii = null;
        if (fault == "empty_planets") radii = Array.Empty<double>();
        if (fault == "too_many_planets") radii = Enumerable.Repeat(200d, 65).ToArray();
        if (fault == "small_planet") radii = new[] { 119.9d };
        if (fault == "large_planet") radii = new[] { 6000.1d };
        if (fault == "nan_planet") radii = new[] { double.NaN };
        if (fault == "infinite_planet") radii = new[] { double.PositiveInfinity };
        if (fault == "missing_mass") mass = null;
        if (fault == "zero_mass") mass = 0;
        if (fault == "negative_mass") mass = -1;
        if (fault == "nan_mass") mass = double.NaN;
        if (fault == "infinite_mass") mass = double.PositiveInfinity;
        if (fault == "missing_physics_radius") physicsRadius = null;
        if (fault == "zero_physics_radius") physicsRadius = 0;
        if (fault == "negative_physics_radius") physicsRadius = -1;
        if (fault == "nan_physics_radius") physicsRadius = double.NaN;
        if (fault == "infinite_physics_radius") physicsRadius = double.PositiveInfinity;
        if (fault == "squared_radius_overflow") physicsRadius = 1e200;
        if (fault == "squared_radius_underflow") physicsRadius = 1e-200;
        if (fault == "numerator_overflow") mass = double.MaxValue;
        if (fault == "float_gravity_overflow") { mass = 1e33; physicsRadius = 1; }
        Assert.Null(VoyagerLocalWalkReservePolicy.CalculateNativeGravityUpperBound(radii, mass, physicsRadius));
    }

    private static VoyagerLocalWalkFacts DriftFacts()
    {
        var facts = Facts(); facts.IncludePotentialWaterDrift = true;
        facts.NativeThrustPowerPerAcceleration = 300; facts.NativeGravityUpperBound = 50;
        return facts;
    }

    private static double ActualCost(VoyagerLocalWalkFacts f, float velocity) =>
        (f.WalkPowerWatts!.Value * (velocity / f.WalkSpeed!.Value) * Math.Max(1d, velocity / 6d)
         + f.MiningPowerWatts!.Value + f.OtherWorkPowerWatts!.Value)
        * f.ReactorConsumptionRatio!.Value * VoyagerLocalWalkReservePolicy.NativeTickSeconds;

    private static VoyagerLocalWalkFacts Facts() => new()
    {
        ModeTransitionsExcluded = true, IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
        WalkSpeed = 12, WalkPowerWatts = 1000, MiningPowerWatts = 200,
        OtherWorkPowerWatts = 100, ReactorConsumptionRatio = 2, RtsSpeed = 0, ManualSpeed = 0, SoftLandingRecover = 1,
    };
}
