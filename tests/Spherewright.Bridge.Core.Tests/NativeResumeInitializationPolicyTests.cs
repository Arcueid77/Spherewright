using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeResumeInitializationPolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static NativeResumeInitializationFacts Facts(int? count = 0, int milliseconds = 0) => new()
    {
        CurrentGameTick = 100, NowUtc = Now.AddMilliseconds(milliseconds), ExactSourceWorldBinding = true,
        NativeStateUnchanged = true, NativePaused = true, RegisteredCount = count,
    };

    [Fact]
    public void EmptyBeforeOrdinaryUpdateIsPendingWithoutNativeProgressOrResume()
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Pending, p.Observe(Facts()));
        Assert.Equal(100, p.SourceGameTick); Assert.Equal(1, p.ObservationCount);
        Assert.False(p.ResumeAuthorized); Assert.Null(p.FirstFailure);
    }

    [Fact]
    public void PositiveCollectionOnlyReadiesInitializationAndNeedsFreshSupportProof()
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Ready, p.Observe(Facts(1)));
        Assert.False(p.ResumeAuthorized);
        Assert.False(p.TryAuthorizeResume(Facts(1, 1)));
        Assert.Equal("remote_resume_initialization_current_support_unproved", p.FirstFailure);
        var recovered = Facts(1, 2); recovered.CurrentSupportOrGroundProof = true;
        Assert.False(p.TryAuthorizeResume(recovered));
    }

    [Fact]
    public void EighthObservationCanReadyThenAuthorizeExactlyOnceWithoutNinthObservation()
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        for (var i = 0; i < 7; i++)
            Assert.Equal(NativeResumeInitializationStep.Pending, p.Observe(Facts(0, i)));
        Assert.Equal(NativeResumeInitializationStep.Ready, p.Observe(Facts(4096, 7)));
        var fresh = Facts(4096, 8); fresh.CurrentSupportOrGroundProof = true;
        Assert.True(p.TryAuthorizeResume(fresh));
        Assert.Equal(8, p.ObservationCount); Assert.True(p.ResumeAuthorized);
        Assert.False(p.TryAuthorizeResume(fresh));
        Assert.Equal(8, p.ObservationCount); Assert.True(p.ResumeAuthorized);
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(fresh));
    }

    [Fact]
    public void EighthEmptyObservationExhaustsTheAttemptAndCannotRecoverLater()
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        for (var i = 0; i < 7; i++)
            Assert.Equal(NativeResumeInitializationStep.Pending, p.Observe(Facts(0, i)));
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(Facts(0, 7)));
        var ready = Facts(1, 8); ready.CurrentSupportOrGroundProof = true;
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(ready));
        Assert.False(p.TryAuthorizeResume(ready)); Assert.Equal(8, p.ObservationCount);
        Assert.Equal("remote_resume_initialization_observations_exhausted", p.FirstFailure);
    }

    [Theory]
    [InlineData(null)] [InlineData(-1)] [InlineData(4097)]
    public void UnknownNegativeOrOverLimitCountIsNotAnInitializationWait(int? count)
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(Facts(count)));
        Assert.Equal("remote_resume_initialization_registered_count_unproved", p.FirstFailure);
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(Facts(1, 1)));
        Assert.False(p.ResumeAuthorized);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ChangedSourceTickWorldStateOrPauseRejectsFreshAuthorization(int drift)
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Ready, p.Observe(Facts(1)));
        var fresh = Facts(1, 1); fresh.CurrentSupportOrGroundProof = true;
        switch (drift)
        {
            case 0: fresh.CurrentGameTick++; break;
            case 1: fresh.ExactSourceWorldBinding = false; break;
            case 2: fresh.NativeStateUnchanged = false; break; // Includes energy/input/pose drift.
            case 3: fresh.NativePaused = false; break;
        }
        Assert.False(p.TryAuthorizeResume(fresh));
        Assert.False(p.ResumeAuthorized); Assert.Equal(1, p.ObservationCount);
        var first = p.FirstFailure; p.Cancel("later_host_loss");
        var restored = Facts(1, 2); restored.CurrentSupportOrGroundProof = true;
        Assert.False(p.TryAuthorizeResume(restored)); Assert.Equal(first, p.FirstFailure);
    }

    [Theory]
    [InlineData(99L)] [InlineData(101L)]
    public void AnyNativeTickChangeDuringTheHoldIsStickyFailure(long tick)
    {
        var p = new NativeResumeInitializationPolicy(100, Now); var f = Facts(); f.CurrentGameTick = tick;
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(f));
        Assert.Equal("remote_resume_initialization_tick_changed", p.FirstFailure);
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(Facts(1)));
    }

    [Fact]
    public void WallLimitIsOriginalAndCannotBeRenewedByLaterObservations()
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Pending, p.Observe(Facts(0, 1900)));
        Assert.Equal(NativeResumeInitializationStep.Ready, p.Observe(Facts(1, 1999)));
        var late = Facts(1, 2000); late.CurrentSupportOrGroundProof = true;
        Assert.False(p.TryAuthorizeResume(late));
        Assert.Equal("remote_resume_initialization_wall_exhausted", p.FirstFailure);
        Assert.Equal(2, p.ObservationCount); Assert.Equal(Now, p.StartedUtc);
    }

    [Fact]
    public void ObservationAtTheWallBoundaryIsRejectedEvenIfTheCollectionBecamePositive()
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(Facts(1, 2000)));
        Assert.False(p.ResumeAuthorized);
    }

    [Fact]
    public void ReversedOrDefaultObservationClockNeverAuthorizes()
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Ready, p.Observe(Facts(1, 10)));
        var old = Facts(1, 9); old.CurrentSupportOrGroundProof = true;
        Assert.False(p.TryAuthorizeResume(old));
        Assert.Equal("remote_resume_initialization_clock_unproved", p.FirstFailure);
        p = new NativeResumeInitializationPolicy(100, Now); old.NowUtc = default;
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(old));
    }

    [Fact]
    public void CancellationBeforeOrAfterReadyRetainsFirstFailure()
    {
        foreach (var beforeObservation in new[] { true, false })
        {
            var p = new NativeResumeInitializationPolicy(100, Now);
            if (!beforeObservation) Assert.Equal(NativeResumeInitializationStep.Ready, p.Observe(Facts(1)));
            p.Cancel("original_host_loss"); p.Cancel("later_cancel");
            var fresh = Facts(1, 1); fresh.CurrentSupportOrGroundProof = true;
            Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(fresh));
            Assert.False(p.TryAuthorizeResume(fresh)); Assert.Equal("original_host_loss", p.FirstFailure);
        }
    }

    [Fact]
    public void FreshProofCannotBypassTheOriginalObservationSequence()
    {
        var p = new NativeResumeInitializationPolicy(100, Now); var fresh = Facts(1);
        fresh.CurrentSupportOrGroundProof = true;
        Assert.False(p.TryAuthorizeResume(fresh)); Assert.Equal(0, p.ObservationCount);
        Assert.False(p.ResumeAuthorized);
    }

    [Theory]
    [InlineData(null)] [InlineData(-1)] [InlineData(4097)] [InlineData(0)]
    public void FreshAuthorizationCannotReuseTheEarlierPositiveCollection(int? count)
    {
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Ready, p.Observe(Facts(1)));
        var fresh = Facts(count, 1); fresh.CurrentSupportOrGroundProof = true;
        Assert.False(p.TryAuthorizeResume(fresh)); Assert.False(p.ResumeAuthorized);
    }

    [Fact]
    public void InvalidConstructionAndNullFactsFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeResumeInitializationPolicy(-1, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeResumeInitializationPolicy(100, default));
        var p = new NativeResumeInitializationPolicy(100, Now);
        Assert.Equal(NativeResumeInitializationStep.Rejected, p.Observe(null!));
        Assert.False(p.TryAuthorizeResume(null!));
    }
}
