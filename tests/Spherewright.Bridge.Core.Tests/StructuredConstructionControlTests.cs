using Spherewright.Bridge.Core.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class StructuredConstructionControlTests
{
    private static readonly DateTimeOffset Start = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FreshResumeRetainsOriginalWindowRatherThanGrantingAnotherMinute()
    {
        var original = Control();
        Assert.Equal(600, original.RemainingWorkGameTicks(3100, Start.AddSeconds(50)));
        Assert.Null(original.RemainingWorkGameTicks(110, Start.AddSeconds(60))); // paused game, expired wall
        Assert.Null(original.RemainingWorkGameTicks(3700, Start.AddSeconds(1))); // fast game, expired tick
        var resumed = Control(); resumed.ObservationLeaseExpiresAtUtc = Start.AddSeconds(59);
        Assert.True(resumed.RetainsOriginalWindowAndSubmission(original));
        resumed.WorkDeadlineUtc = Start.AddSeconds(110);
        Assert.False(resumed.RetainsOriginalWindowAndSubmission(original));
    }

    [Fact]
    public void PollingWorkingActionRenewsOnlyItsLeaseAndNeverItsEvidenceFingerprintOrWorkDeadline()
    {
        var c = Control(); var hash = c.EvidenceHash();
        Assert.True(c.RenewObservationLease(Start.AddSeconds(45)));
        Assert.Equal(Start.AddSeconds(60), c.ObservationLeaseExpiresAtUtc);
        Assert.Equal(hash, c.EvidenceHash());
        Assert.Equal(3700, c.WorkDeadlineGameTick);
        Assert.False(c.RenewObservationLease(Start.AddSeconds(60)));
    }

    [Fact]
    public void RepeatedCancellationAndOriginalPollCannotRenewFiniteDrainOrHidePausedWallExpiry()
    {
        var c = Control();
        Assert.True(c.RequestStop("explicit_structured_cancellation", 200, Start.AddSeconds(2), false));
        var tick = c.DrainStartedAtGameTick; var utc = c.DrainStartedAtUtc;
        Assert.False(c.RenewObservationLease(Start.AddSeconds(3)));
        Assert.False(c.RequestStop("later_poll", 1000, Start.AddSeconds(9), false));
        Assert.Equal(tick, c.DrainStartedAtGameTick); Assert.Equal(utc, c.DrainStartedAtUtc);
        Assert.False(c.StopOutcomeUnknown(200, Start.AddSeconds(11.999)));
        Assert.True(c.StopOutcomeUnknown(200, Start.AddSeconds(12))); // same tick during pause
        Assert.True(c.StopOutcomeUnknown(800, Start.AddSeconds(2))); // game bound first
    }

    [Theory]
    [InlineData("tick_missing")] [InlineData("utc_missing")] [InlineData("tick_rewind")]
    [InlineData("utc_rewind")] [InlineData("drain_missing")]
    public void MissingOrReversedCleanupClocksAreUnknownEvenWhenDroneFlagIsDisabled(string fault)
    {
        var c = Control(); c.RequestStop("host_shutdown", 200, Start.AddSeconds(2), true); c.DispatchDisabled = true;
        long? tick = 201; DateTimeOffset? utc = Start.AddSeconds(3);
        if (fault == "tick_missing") tick = null;
        if (fault == "utc_missing") utc = null;
        if (fault == "tick_rewind") tick = 199;
        if (fault == "utc_rewind") utc = Start;
        if (fault == "drain_missing") c.DrainStartedAtUtc = null;
        Assert.True(c.StopOutcomeUnknown(tick, utc));
        Assert.False(c.PhysicalIdleVerified);
    }

    [Theory]
    [InlineData("original_disabled")] [InlineData("user_paused")] [InlineData("foreign_order")]
    [InlineData("binding_lost")] [InlineData("not_idle")] [InlineData("cancelled")]
    [InlineData("unknown")] [InlineData("not_complete")]
    public void PhysicalDrainNeverRestoresDisabledDronesOrOverridesUserControl(string reason)
    {
        var c = Control(); c.RequestStop("construction_complete_draining", 200, Start.AddSeconds(2), false);
        c.DispatchDisabled = true;
        Assert.True(c.RecordDisabledNativeFactoryPass(200));
        Assert.True(c.TryMarkPhysicalIdle(201, true, true));
        c.Phase = "completed_idle";
        var complete = true; var known = true; var hold = true; var paused = false;
        if (reason == "original_disabled") c.OriginalDroneEnabled = false;
        if (reason == "user_paused") paused = true;
        if (reason == "foreign_order") hold = false;
        if (reason == "binding_lost") c.BindingLost = true;
        if (reason == "not_idle") c.PhysicalIdleVerified = false;
        if (reason == "cancelled") c.StopReason = "explicit_structured_cancellation";
        if (reason == "unknown") known = false;
        if (reason == "not_complete") complete = false;
        Assert.False(c.MayRestoreOriginalDroneSetting(complete, known, hold, paused));
    }

    [Fact]
    public void OnlyKnownCompletedIdleOriginalHoldMayRestoreItsPriorEnabledFlag()
    {
        var c = Control(); c.RequestStop("construction_complete_draining", 200, Start.AddSeconds(2), false);
        c.DispatchDisabled = true;
        Assert.True(c.RecordDisabledNativeFactoryPass(200));
        Assert.True(c.TryMarkPhysicalIdle(201, true, true));
        c.Phase = "completed_idle";
        Assert.True(c.MayRestoreOriginalDroneSetting(true, true, true, false));
    }

    [Fact]
    public void CachedPrelaunchCannotBecomeIdleBeforeARealDisabledFactoryPassAndLaterPoolReadback()
    {
        var c = Control();
        // Copied Native sequence: Player nominated a prebuild but the pool still
        // looks idle; closing the toggle does not consume that nomination.
        c.RequestStop("construction_complete_draining", 200, Start.AddSeconds(2), false);
        c.DispatchDisabled = true;
        Assert.False(c.TryMarkPhysicalIdle(200, false, true));
        Assert.False(c.TryMarkPhysicalIdle(201, true, true)); // A clock alone cannot prove a Native pass.
        Assert.True(c.RecordDisabledNativeFactoryPass(201));
        Assert.False(c.TryMarkPhysicalIdle(201, false, true)); // Exact cache is still nominated.
        Assert.False(c.TryMarkPhysicalIdle(202, true, false)); // Native consumed it and launched a drone.
        Assert.False(c.RenewObservationLease(Start.AddSeconds(3)));
        Assert.False(c.RequestStop("repeat_poll", 202, Start.AddSeconds(3), false));
        Assert.True(c.TryMarkPhysicalIdle(260, true, true)); // Later pool/module readback is physically idle.
        Assert.Equal(200, c.DrainStartedAtGameTick);
        Assert.Equal(201, c.DisabledNativeFactoryPassGameTick);
        Assert.Equal(260, c.PhysicalIdleVerifiedAtGameTick);
        c.Phase = "completed_idle";
        Assert.True(c.MayRestoreOriginalDroneSetting(true, true, true, false));
    }

    [Theory]
    [InlineData("no_pass")] [InlineData("same_tick")] [InlineData("cache_present")]
    [InlineData("active_drone")] [InlineData("rewound_pass")] [InlineData("enabled")]
    public void DisabledFlagAndIdleCountAreNotAConstructionDrainCertificate(string fault)
    {
        var c = Control(); c.RequestStop("explicit_structured_cancellation", 200, Start.AddSeconds(2), false);
        c.DispatchDisabled = true;
        if (fault != "no_pass") c.RecordDisabledNativeFactoryPass(200);
        if (fault == "rewound_pass") c.DisabledNativeFactoryPassGameTick = 199;
        if (fault == "enabled") c.DispatchDisabled = false;
        Assert.False(c.TryMarkPhysicalIdle(fault == "same_tick" ? 200 : 201,
            fault != "cache_present", fault != "active_drone"));
        Assert.False(c.PhysicalIdleVerified);
        Assert.Null(c.PhysicalIdleVerifiedAtGameTick);
        Assert.False(c.MayRestoreOriginalDroneSetting(true, true, true, false));
    }

    internal static StructuredConstructionControlState Control() => new()
    {
        Phase = "working", Anchor = new() { X = 200 }, NativeCostHash = "synthetic-native-cost",
        StartedAtGameTick = 100, StartedAtUtc = Start, WorkDeadlineGameTick = 3700, WorkDeadlineUtc = Start.AddSeconds(60),
        ObservationLeaseExpiresAtUtc = Start.AddSeconds(30), TailGameTicks = 600,
        TailAndNextTickEnergyJoules = 100_000, NativePowerUpperWatts = 1000, OriginalDroneEnabled = true,
    };
}
