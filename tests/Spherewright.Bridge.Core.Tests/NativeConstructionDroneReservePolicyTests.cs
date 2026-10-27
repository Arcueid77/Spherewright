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
    [InlineData(24d)] [InlineData(26d)]
    public void TallNativeTargetInsideExistingOuterEnvelopeKeepsTheExactPaidReturnTail(double reach)
    {
        var f = Facts(); var original = NativeConstructionDroneReservePolicy.Evaluate(f);
        f.MaximumTargetDistanceMeters = reach;
        var tall = NativeConstructionDroneReservePolicy.Evaluate(f);
        Assert.True(tall.Proved);
        Assert.Equal(28d, NativeConstructionDroneReservePolicy.MaximumDroneDistanceMeters);
        Assert.Equal(original.TailGameTicks,tall.TailGameTicks);
        Assert.Equal(original.TailAndNextTickEnergyJoules,tall.TailAndNextTickEnergyJoules);
        Assert.True(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(2,10,0,reach,reach,reach));
        Assert.False(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(2,10,0,reach,reach,28.001));
    }

    [Theory]
    [InlineData(120d,6f)] [InlineData(200d,10f)] [InlineData(6000d,120f)]
    public void NativeLaunchAndTallStraightApproachStayInsideTheOriginalOuterSphere(double radius,float speed)
    {
        // EjectMechaDrone:4.5*n+1.5*(unit_target+inside_unit_sphere), hence<=7.5m.
        var f=Motion(radius,speed); var eject=f.Eject;
        f.Initial=new Vector3Snapshot { X=eject.X+7.5f,Y=eject.Y,Z=eject.Z };
        f.Target=new Vector3Snapshot { X=eject.X,Y=eject.Y+26f,Z=eject.Z };
        f.Stage=1;
        for(var i=0;i<64;i++)
        {
            f.Progress=i/64f;
            var copied=NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(f)!;
            Assert.InRange(Distance(copied,eject),0,7.501);
        }
        f.Stage=2;f.Position=new UniversalPositionSnapshot { X=f.Initial.X,Y=f.Initial.Y,Z=f.Initial.Z };
        for(var i=0;i<1000 && Distance(f.Position,f.Target)>.5;i++)
        {
            var next=NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(f)!;
            Assert.True(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(2,speed,0,
                Distance(f.Position,eject),Distance(new UniversalPositionSnapshot {X=f.Target.X,Y=f.Target.Y,Z=f.Target.Z},eject),Distance(next,eject)));
            Assert.InRange(Distance(next,eject),0,26.01);f.Position=next;
        }
        Assert.InRange(Distance(f.Position,f.Target),0,.5); // Native stage2 enters work within .5m.
    }

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
            case "target_outside": f.MaximumTargetDistanceMeters = 26.001; break;
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
    [InlineData(1, 26.001d, 18d, 0d)] [InlineData(2, 28.1d, 18d, 0d)]
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

    [Theory]
    [InlineData(10d, null)] [InlineData(12d, null)] [InlineData(9.999999d, 0d)]
    [InlineData(12.000001d, 0d)] [InlineData(10d, .010900001d)]
    [InlineData(10d, -1d)] [InlineData(10d, double.NaN)] [InlineData(10d, double.PositiveInfinity)]
    public void MissingOrOutsideTighterSubsetRetainsTheExactOriginalTail(double speed, double? ejectDisplacement)
    {
        var facts = Facts(); facts.NativeSpeedMetersPerSecond = speed;
        facts.FixedToLiveEjectDistanceMeters = ejectDisplacement;
        var reserve = NativeConstructionDroneReservePolicy.Evaluate(facts);
        Assert.True(reserve.Proved);
        Assert.Equal(.25d, NativeConstructionDroneReservePolicy.ReturnClosureFraction(speed, ejectDisplacement));
        Assert.Equal((long)(64 + Math.Ceiling(4 * 28 / speed * 60) + 32), reserve.TailGameTicks);
    }

    [Theory]
    [InlineData(10d, 0d, 402L)] [InlineData(10d, .0109d, 402L)]
    [InlineData(12d, .0109d, 351L)]
    public void MeasuredTighterSubsetStillPaysOriginalLaunchNearAndTwoSpareTicks(double speed, double drift, long ticks)
    {
        var facts = Facts(); facts.NativeSpeedMetersPerSecond = speed;
        facts.FixedToLiveEjectDistanceMeters = drift;
        var reserve = NativeConstructionDroneReservePolicy.Evaluate(facts);
        Assert.True(reserve.Proved);
        Assert.Equal(ticks, reserve.TailGameTicks);
        Assert.Equal(reserve.PowerUpperWatts * (ticks + 2) / 60 + 4 * 1000,
            reserve.TailAndNextTickEnergyJoules);
        facts.NativePendingJumpTicks = 1;
        Assert.False(NativeConstructionDroneReservePolicy.Evaluate(facts).Proved);
    }

    [Fact]
    public void TighterReserveNeverLetsLoadedFuelPayForMissingCoreOrReturnFuel()
    {
        var facts = Facts(); facts.MaximumMechaDrones = 3; facts.NativeSpeedMetersPerSecond = 10;
        facts.NativeEnergyPerMeter = 9000; facts.NativeRepairEnergyPerTick = 3600;
        facts.NativeEjectEnergyJoules = 60000; facts.OtherMechaPowerUpperWatts = 228_000_000;
        var original = NativeConstructionDroneReservePolicy.Evaluate(facts);
        facts.FixedToLiveEjectDistanceMeters = .0109;
        var measured = NativeConstructionDroneReservePolicy.Evaluate(facts);
        const double coreCapacity = 1_600_000_000, floor = 5_000_000_000;
        Assert.True(original.TailAndNextTickEnergyJoules > coreCapacity);
        Assert.True(measured.TailAndNextTickEnergyJoules < coreCapacity);
        Assert.True(NativeConstructionDroneReservePolicy.CanFundNativeTail(coreCapacity,
            floor + measured.TailAndNextTickEnergyJoules, floor, measured.TailAndNextTickEnergyJoules));
        Assert.False(NativeConstructionDroneReservePolicy.CanFundNativeTail(measured.TailAndNextTickEnergyJoules - 1,
            floor + original.TailAndNextTickEnergyJoules, floor, measured.TailAndNextTickEnergyJoules));
        Assert.False(NativeConstructionDroneReservePolicy.CanFundNativeTail(coreCapacity,
            floor + measured.TailAndNextTickEnergyJoules - 1, floor, measured.TailAndNextTickEnergyJoules));
    }

    [Fact]
    public void TighterPaidTailRequiresStrongerCopiedStepThanLegacy()
    {
        Assert.True(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(4, 10, 0, 10, 0, 9.94));
        Assert.False(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(4, 10, 0, 10, 0, 9.94,
            fixedToLiveEjectDistanceMeters: .0109));
        Assert.True(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(4, 10, 0, 10, 0, 9.89,
            fixedToLiveEjectDistanceMeters: .0109));
    }

    [Fact]
    public void ActualFloatThresholdRoundingFailsConservativelyWithoutAnUnobservedTolerance()
    {
        var roundedUp = (double).0109f;
        var roundedDown = (double)MathF.BitDecrement(.0109f);
        Assert.True(roundedUp > .0109d);
        Assert.True(roundedDown < .0109d);
        Assert.Equal(.25d, NativeConstructionDroneReservePolicy.ReturnClosureFraction(10, roundedUp));
        Assert.Equal(.55d, NativeConstructionDroneReservePolicy.ReturnClosureFraction(10, roundedDown));
    }

    [Theory]
    [InlineData(120d, 10f)] [InlineData(200d, 10f)] [InlineData(6000d, 10f)]
    [InlineData(120d, 12f)] [InlineData(200d, 12f)] [InlineData(6000d, 12f)]
    public void CopiedNativeReturnConvergesWithRoundedCoordinatesAndOpposingEjectOffsets(double radius, float speed)
    {
        foreach (var sign in new[] { -1d, 1d })
        foreach (var startDistance in new[] { .4995d, .5d, .51d, 7.5d, 16d, 27.99d })
        foreach (var radialWeight in new[] { -1d, 0d, 1d })
        {
            var unit = 1 / Math.Sqrt(3);
            var fixedEject = new Vector3Snapshot
            { X = (float)((radius + 2.8) * unit), Y = (float)((radius + 2.8) * unit), Z = (float)((radius + 2.8) * unit) };
            var liveEject = new Vector3Snapshot
            { X = (float)(fixedEject.X + sign * .0105), Y = fixedEject.Y, Z = fixedEject.Z };
            var drift = FloatDistance(new() { X = liveEject.X, Y = liveEject.Y, Z = liveEject.Z }, fixedEject);
            var direction = new[] { 1d + radialWeight, -1d + radialWeight, radialWeight };
            var magnitude = Math.Sqrt(direction.Sum(x => x * x));
            var motion = Motion(radius, speed); motion.Target = liveEject; motion.Eject = liveEject;
            motion.Position = new()
            {
                X = fixedEject.X + startDistance * direction[0] / magnitude,
                Y = fixedEject.Y + startDistance * direction[1] / magnitude,
                Z = fixedEject.Z + startDistance * direction[2] / magnitude,
            };
            var facts = Facts(); facts.AnchorRadiusMeters = radius; facts.NativeSpeedMetersPerSecond = speed;
            facts.FixedToLiveEjectDistanceMeters = drift;
            var tail = NativeConstructionDroneReservePolicy.Evaluate(facts);
            Assert.True(tail.Proved); Assert.Equal(.55, NativeConstructionDroneReservePolicy.ReturnClosureFraction(speed, drift));
            var ticks = 0;
            while (!NativeConstructionDroneReservePolicy.CopiedStepWouldNativeRecycle(motion) && ticks < tail.TailGameTicks - 64)
            {
                var current = FloatDistance(motion.Position, fixedEject);
                var next = NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(motion);
                Assert.NotNull(next);
                // Never exclude a next point that exits the envelope from the sample.
                Assert.True(NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(4, speed, 0,
                    current, 0, Distance(next!, fixedEject), fixedToLiveEjectDistanceMeters: drift));
                motion.Position = next!; ticks++;
            }
            Assert.True(NativeConstructionDroneReservePolicy.CopiedStepWouldNativeRecycle(motion));
            Assert.True(ticks < tail.TailGameTicks - 64);
        }
    }

    private static double FloatDistance(UniversalPositionSnapshot position, Vector3Snapshot target)
    {
        var x = (float)position.X - target.X; var y = (float)position.Y - target.Y; var z = (float)position.Z - target.Z;
        return (float)Math.Sqrt(x * x + y * y + z * z);
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
