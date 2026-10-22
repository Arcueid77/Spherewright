using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerCompletedRemoteContinuationPolicyTests
{
    [Theory]
    [InlineData("funded")] [InlineData("core_low")] [InlineData("core_unknown")]
    [InlineData("loaded_low")] [InlineData("loaded_unknown")]
    public void CompletedContinuationRequiresAlreadyFundedStrictReserveAndNeverSpendsFutureCharging(string fault)
    {
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(new VoyagerLocalWalkFacts
        {
            ModeTransitionsExcluded = true, IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
            WalkSpeed = 12, WalkPowerWatts = 1000, MiningPowerWatts = 200, OtherWorkPowerWatts = 100,
            ReactorConsumptionRatio = 2, RtsSpeed = 0, ManualSpeed = 0, SoftLandingRecover = 1,
        });
        Assert.True(reserve.Proved); var p = reserve.PerNativeTickEnergyJoules!.Value;
        var facts = new VoyagerRemoteIdleAdmissionFacts { MovementReserve = reserve, CoreEnergyJoules = 13 * p,
            JourneyBudget = new() { AvailableLoadedEnergyJoules = 1000 + 13 * p, LockedReturnFloorJoules = 1000 } };
        if (fault == "core_low") facts.CoreEnergyJoules = 12 * p;
        if (fault == "core_unknown") facts.CoreEnergyJoules = null;
        if (fault == "loaded_low") facts.JourneyBudget.AvailableLoadedEnergyJoules = 1000 + 12 * p;
        if (fault == "loaded_unknown") facts.JourneyBudget.AvailableLoadedEnergyJoules = null;
        var cut = new NativeResumeDecisionEvidence("same_native_sample", 12521, null, true);
        var blocker = VoyagerCompletedRemoteContinuationPolicy.StrictFundingBlocker(facts, cut);
        if (fault == "funded") Assert.Null(blocker);
        else
        {
            Assert.NotNull(blocker); var failure = cut.Failure(blocker!);
            Assert.Equal(fault.EndsWith("unknown") ? "unknown" : "false", failure.Predicates[failure.FirstFailedPredicate]);
            if (fault.StartsWith("core")) Assert.Equal("not_reached", failure.Predicates["completed_resume_strict_loaded_funded"]);
        }
    }

    [Fact]
    public void LegacyAuthorizationBytesOmitTheNewOptionalOriginWhileInheritedOriginNeverMovesForward()
    {
        var origin = DateTimeOffset.UtcNow.AddSeconds(-30);
        var p = new VoyagerRecoveryWindowProposal(DateTimeOffset.UtcNow.AddMinutes(30), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240);
        var legacy = new VoyagerRecoveryAuthorization(Guid.NewGuid().ToString(), CanonicalStateHash.Combine("e"), CanonicalStateHash.Combine("c"), origin, p);
        Assert.DoesNotContain("inheritedWindowConfirmedAtUtc", PluginJson.Serialize(legacy));
        var continued = new VoyagerRecoveryAuthorization(Guid.NewGuid().ToString(), CanonicalStateHash.Combine("new-e"),
            CanonicalStateHash.Combine("new-c"), DateTimeOffset.UtcNow, p, inheritedWindowConfirmedAtUtc: origin);
        var copy = PluginJson.Deserialize<VoyagerRecoveryAuthorization>(PluginJson.Serialize(continued))!;
        Assert.Equal(origin, copy.WindowOriginUtc()); Assert.Equal(p.DeadlineUtc, copy.Proposal.DeadlineUtc);
        Assert.True(copy.ConfirmedAtUtc > copy.WindowOriginUtc());
    }
}
