using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// No Unity/native updates: exercise copied costs against independent concrete
// RTS-stop trajectories. The Plugin must separately prove the mode predicates.
public sealed class VoyagerLocalWalkReservePolicyTests
{
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
