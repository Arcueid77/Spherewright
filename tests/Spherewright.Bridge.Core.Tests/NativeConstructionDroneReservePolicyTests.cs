using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Copied scalar Native29151 mathematics only. These tests never load Unity,
// invoke a drone update, or establish a live construction/idle acceptance.
public sealed class NativeConstructionDroneReservePolicyTests
{
    [Theory]
    [InlineData(6d)] [InlineData(12d)] [InlineData(60d)] [InlineData(120d)]
    public void TailCoversCachedLaunchFarClosureAndNearRecycleAtEverySupportedSpeed(double speed)
    {
        var facts = Facts(); facts.NativeSpeedMetersPerSecond = speed;
        var r = NativeConstructionDroneReservePolicy.Evaluate(facts);
        Assert.True(r.Proved);
        // Even a cached stage1 launch consumes less than64 ticks. Then the
        // conservative s/4 far closure and32 near ticks fit the same reserve.
        Assert.True(r.TailGameTicks >= 29 + Math.Ceiling(28 / (speed / 60 / 4)) + 32);
        var runningPower = 4 * (speed * 200 + 10 * 60) + 500;
        var minimum = runningPower * (r.TailGameTicks + 1) / 60 + 4 * 1000;
        Assert.True(r.TailAndNextTickEnergyJoules > minimum); // next Native tick plus a spare tick
        Assert.InRange(r.TailGameTicks, 1, 7200);
    }

    [Theory]
    [InlineData("missing")] [InlineData("zero_speed")] [InlineData("too_fast")]
    [InlineData("negative_rate")] [InlineData("nan_rate")] [InlineData("infinite_background")]
    [InlineData("too_many_drones")] [InlineData("tiny_world")] [InlineData("large_world")]
    [InlineData("target_outside")] [InlineData("overflow")] [InlineData("pending_jump")] [InlineData("unknown_jump")]
    public void MissingOrUnboundedNativeCostsNeverReceiveAnEnergyCertificate(string fault)
    {
        var f = Facts();
        switch (fault)
        {
            case "missing": f.NativeRepairEnergyPerTick = null; break;
            case "zero_speed": f.NativeSpeedMetersPerSecond = 0; break;
            case "too_fast": f.NativeSpeedMetersPerSecond = 121; break;
            case "negative_rate": f.NativeEnergyPerMeter = -1; break;
            case "nan_rate": f.NativeEnergyPerMeter = double.NaN; break;
            case "infinite_background": f.OtherMechaPowerUpperWatts = double.PositiveInfinity; break;
            case "too_many_drones": f.MaximumMechaDrones = 33; break;
            case "tiny_world": f.AnchorRadiusMeters = 100; break;
            case "large_world": f.AnchorRadiusMeters = 6001; break;
            case "target_outside": f.MaximumTargetDistanceMeters = 16.001; break;
            case "overflow": f.NativeEnergyPerMeter = double.MaxValue; break;
            case "pending_jump": f.NativePendingJumpTicks = 1; break;
            case "unknown_jump": f.NativePendingJumpTicks = null; break;
        }
        Assert.False(NativeConstructionDroneReservePolicy.Evaluate(f).Proved);
    }

    [Theory]
    [InlineData(120d, 6f)] [InlineData(200d, 12f)] [InlineData(6000d, 120f)]
    public void CopiedRadialReturnRetainsDoubleCoordinatesAndConvergesInsideItsCertifiedTail(double radius, float speed)
    {
        var f = Motion(radius, speed);
        f.Position = new UniversalPositionSnapshot { X = radius + 2.8 + 24.000123456 };
        var budget = Facts(); budget.AnchorRadiusMeters = radius; budget.NativeSpeedMetersPerSecond = speed;
        var tail = NativeConstructionDroneReservePolicy.Evaluate(budget);
        Assert.True(tail.Proved);
        var ticks = 0;
        while (!NativeConstructionDroneReservePolicy.CopiedStepWouldNativeRecycle(f) && ticks < tail.TailGameTicks)
        {
            var before = Distance(f.Position, f.Eject);
            var next = NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(f);
            Assert.NotNull(next);
            Assert.True(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(4, speed, 0,
                before, 0, Distance(next!, f.Eject)));
            Assert.True(Distance(next!, f.Eject) < before);
            f.Position = next!; ticks++;
        }
        Assert.True(NativeConstructionDroneReservePolicy.CopiedStepWouldNativeRecycle(f));
        Assert.True(ticks < tail.TailGameTicks - 64); // Cached launch remains separately reserved.
    }

    [Fact]
    public void NativeRecycleObservationAllowsLiveFixedEjectRoundingWithoutClaimingPhysicalIdle()
    {
        var f = Motion(200, 6);
        f.Position = new UniversalPositionSnapshot { X = 203.301 };
        f.Target = new Vector3Snapshot { X = 202.802f };
        Assert.True(NativeConstructionDroneReservePolicy.CopiedStepWouldNativeRecycle(f));
        var next = NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(f)!;
        Assert.Equal(f.Position.X, next.X); // Native may return recycle, the observer never recycles.
        Assert.True(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(4, 6, .002,
            .501, .002, .501, nativeWouldRecycle: true));
    }

    [Theory]
    [InlineData(1, 17d, 18d, 0d)] [InlineData(2, 28.1d, 18d, 0d)]
    [InlineData(4, 0d, 28.001d, 0d)] [InlineData(4, 0d, 20d, .011d)]
    public void TargetMotionOrAnchorLossCannotInheritTheStationaryEnvelope(int stage, double target, double next, double drift)
    {
        Assert.False(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(stage, 6, drift, 20, target, next));
    }

    [Theory]
    [InlineData(null, 150d, 100d, 50d)] [InlineData(0d, 150d, 100d, 50d)]
    [InlineData(49.999d, 150d, 100d, 50d)] [InlineData(50d, 149.999d, 100d, 50d)]
    [InlineData(50d, 150d, 100d, double.NaN)] [InlineData(50d, double.PositiveInfinity, 100d, 50d)]
    [InlineData(50d, 150d, -1d, 50d)] [InlineData(double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue)]
    public void LoadedFuelCannotSubstituteForCoreOrSpendTheProtectedReturnFloor(double? core, double? loaded, double? floor, double? tail)
        => Assert.False(NativeConstructionDroneReservePolicy.CanFundNativeTail(core, loaded, floor, tail));

    [Fact]
    public void TailCanBePaidExactlyAboveFloorAndAnotherUnreservedTickIsRejected()
    {
        var reserve = NativeConstructionDroneReservePolicy.Evaluate(Facts());
        const double floor = 5_000_000_000;
        Assert.True(NativeConstructionDroneReservePolicy.CanFundNativeTail(reserve.TailAndNextTickEnergyJoules,
            floor + reserve.TailAndNextTickEnergyJoules, floor, reserve.TailAndNextTickEnergyJoules));
        Assert.False(NativeConstructionDroneReservePolicy.CanFundNativeTail(reserve.TailAndNextTickEnergyJoules,
            floor + reserve.TailAndNextTickEnergyJoules - reserve.PowerUpperWatts / 60, floor, reserve.TailAndNextTickEnergyJoules));
    }

    private static NativeConstructionDroneFacts Facts() => new()
    {
        MaximumMechaDrones = 4, NativeSpeedMetersPerSecond = 12, NativeEnergyPerMeter = 200,
        NativeEjectEnergyJoules = 1000, NativeRepairEnergyPerTick = 10, OtherMechaPowerUpperWatts = 500,
        AnchorRadiusMeters = 200, MaximumTargetDistanceMeters = 16, NativePendingJumpTicks = -1,
        IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
    };
    private static NativeConstructionDroneMotionFacts Motion(double radius, float speed) => new()
    {
        Stage = 4, SpeedMetersPerSecond = speed, Position = new() { X = radius + 10 },
        Target = new() { X = (float)(radius + 2.8) }, Eject = new() { X = (float)(radius + 2.8) },
    };
    private static double Distance(UniversalPositionSnapshot p, Vector3Snapshot q) =>
        Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y) + (p.Z - q.Z) * (p.Z - q.Z));
}
