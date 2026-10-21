using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeResumeFailureEvidenceTests
{
    [Fact]
    public void UnknownIsNotFalseAndUnevaluatedChecksAreNotPassed()
    {
        var e = new NativeResumeDecisionEvidence("native_physics_post501", 101, null, true);
        e.NotReached("support", "ground", "idle");
        Assert.False(e.Require("support", null));
        e.Observe("ground_hit", false); e.Observe("foot_owner", null); e.Number("core_j", double.NaN);
        var cut = e.Failure("stable_code");
        Assert.Equal("support", cut.FirstFailedPredicate);
        Assert.Equal("unknown", cut.Predicates["support"]);
        Assert.Equal("not_reached", cut.Predicates["idle"]);
        Assert.Equal("false", cut.Observations["ground_hit"]);
        Assert.Equal("unknown", cut.Observations["foot_owner"]);
        Assert.Null(cut.Values["core_j"]); Assert.False(cut.SafetyCertified);
        e.Require("support", true); e.Number("core_j", 50);
        Assert.Equal("unknown", cut.Predicates["support"]); Assert.Null(cut.Values["core_j"]);
        var copy = cut.Copy(); copy.Predicates["idle"] = "true";
        Assert.Equal("not_reached", cut.Predicates["idle"]);
    }

    [Fact]
    public void MissingFactsAndBoundedRecorderDoNotInventAnObservation()
    {
        var policy = new NativeResumeContactPolicy(100, DateTimeOffset.UtcNow);
        Assert.NotNull(policy.ObserveUniverse(null!));
        Assert.Equal("not_observed", policy.FailureEvidence!.FactsState);
        Assert.Null(policy.FailureEvidence.CapturedAtGameTick);
        Assert.Null(policy.FailureEvidence.CapturedAtUtc);
        var e = new NativeResumeDecisionEvidence("test", null, null, false);
        for (var i = 0; i < 96; i++) e.Number(i.ToString(), null);
        Assert.Throws<InvalidOperationException>(() => e.Number("overflow", 0));
    }

    [Fact]
    public void ChargeFailureNamesTheActualExhaustedBoundAndFreezesItsOriginalBudget()
    {
        var now = DateTimeOffset.UtcNow;
        var e = new NativeResumeDecisionEvidence("native_physics_post501", 101, now, true);
        var code = VoyagerRecoveryWindowPolicy.ChargeBlocker(100, 1000, 500, 200, 2, 100,
            10, 51, 10000, 3600, 8, 8, .2, 2, now, now.AddMinutes(20), e);
        Assert.Equal("remote_resume_charge_finite_budget_exhausted", code);
        var cut = e.Failure(code!);
        Assert.Equal("charge_ticks_remaining", cut.FirstFailedPredicate);
        Assert.Equal(8, cut.Values["charge_ticks_used"]); Assert.Equal(8, cut.Values["charge_tick_cap"]);
        Assert.Equal(10, cut.Values["spent_ticks"]); Assert.Equal(51, cut.Values["elapsed_ticks"]);
        Assert.Equal("not_reached", cut.Predicates["charge_original_tick_reserve"]);
        e.Number("charge_ticks_used", 0); e.Number("core_j", 1000);
        Assert.Equal(8, cut.Values["charge_ticks_used"]); Assert.Equal(100, cut.Values["core_j"]);
    }
}
