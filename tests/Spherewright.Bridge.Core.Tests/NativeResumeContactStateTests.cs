using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.Game;
using UnityEngine;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Actual Plugin fact composition, linked into this test assembly. Substitutes
// supply observations only: these tests do not execute Unity collision/timing.
[Collection("Owned resume native substitutes")]
public sealed class NativeResumeContactStateTests
{
    [Fact]
    public void ActualFactsKeepContactPendingUntilGroundedStrictIdleThenUseTheOriginalChargeAndIdleGates()
    {
        var player = new Player(); GameMain.gameTick = 100;
        var state = new NormalGameActionCoordinator.ContactStateProbe(player, 100);
        var idle = IdleFacts(); var support = Support(false);
        Assert.Null(state.Policy.PreflightBlocker(state.Facts(player, idle, support)));
        GameMain.gameTick = 101; idle.JourneyBudget!.CurrentGameTick = 101; support.CapturedAtGameTick = 101;
        player.transform.localRotation = new(0f, .6f, 0f, .8f);
        player.transform.lossyScale = new(1.0000002f, 1.0000001f, 1.0000001f);
        var airborne = state.Facts(player, idle, support);
        Assert.True(airborne.ExactRecoveryBinding); Assert.False(airborne.StrictPhysicalIdleProved);
        Assert.Null(state.Policy.ObserveUniverse(airborne));
        Assert.Equal(NativeResumeContactStep.Pending, state.Policy.Observe(airborne));
        GameMain.gameTick = 102; idle.JourneyBudget.CurrentGameTick = 102;
        support = Support(true); idle.JourneyBudget.NativePlayerIdle = true;
        var landed = state.Facts(player, idle, support);
        Assert.Null(state.Policy.ObserveUniverse(landed));
        Assert.Equal(NativeResumeContactStep.Ready, state.Policy.Observe(landed));
        var j = idle.JourneyBudget; var now = DateTimeOffset.UtcNow;
        Assert.Null(VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(j, now, j.JourneyDeadlineUtc));
        Assert.Null(VoyagerRecoveryWindowPolicy.ChargeBlocker(idle.CoreEnergyJoules!.Value, 1e9,
            j.AvailableLoadedEnergyJoules!.Value, j.LockedReturnFloorJoules!.Value,
            idle.MovementReserve!.PerNativeTickEnergyJoules!.Value, 1e6, j.SpentGameTicks,
            j.CurrentGameTick - j.LastLandedGameTick!.Value, j.MaximumGameTicks, j.ReturnReserveGameTicks,
            2, 100, .1, 2, now, j.JourneyDeadlineUtc));
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(idle).Proved);
        j.NativePlayerIdle = false;
        Assert.NotNull(VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(j, now, j.JourneyDeadlineUtc));
        Assert.False(VoyagerRemoteIdleAdmissionPolicy.Evaluate(idle).Proved);
    }

    [Theory]
    [InlineData("scale")] [InlineData("parent")] [InlineData("body")] [InlineData("constraints")]
    [InlineData("gravity")] [InlineData("kinematic")] [InlineData("controller_motion")]
    [InlineData("body_motion")] [InlineData("angular_nan")] [InlineData("vertical_motion")]
    [InlineData("support_tick")] [InlineData("support_owner")] [InlineData("foot_foreign")]
    [InlineData("binding")]
    public void NativeChangesStillRejectTheActualComposedContactFacts(string fault)
    {
        var player = new Player(); GameMain.gameTick = 100;
        var state = new NormalGameActionCoordinator.ContactStateProbe(player, 100);
        var idle = IdleFacts(); var support = Support(false);
        switch (fault)
        {
            case "scale": player.transform.localScale = new(1.0000001f, 1, 1); break;
            case "parent": player.transform.parent = new Transform(); break;
            case "body": player.controller.rigid = new Rigidbody(); break;
            case "constraints": player.controller.rigid.constraints = RigidbodyConstraints.FreezeAll; break;
            case "gravity": player.controller.rigid.useGravity = true; break;
            case "kinematic": player.controller.rigid.isKinematic = true; break;
            case "controller_motion": player.controller.velocity = new(1.51f, 0, 0); break;
            case "body_motion": player.controller.rigid.velocity = new(1.51f, 0, 0); break;
            case "angular_nan": player.controller.rigid.angularVelocity = new(float.NaN, 0, 0); break;
            case "vertical_motion": player.controller.vertSpeed = 1.51f; break;
            case "support_tick": support.CapturedAtGameTick--; break;
            case "support_owner": support.CurrentPoolReference = false; break;
            case "foot_foreign": support.ActualGroundSphereHit = true; support.FootOverlapCount = 1; support.AllFootHitsCurrentSupportOrTerrain = false; break;
        }
        Assert.NotNull(state.Policy.PreflightBlocker(state.Facts(player, idle, support, fault != "binding")));
    }

    private static NativeNearGroundSupportSnapshot Support(bool grounded) => new()
    {
        State = "observed", CapturedAtGameTick = GameMain.gameTick, CurrentPoolReference = true,
        LiveObject = true, ColliderChain = true, FootOverlapComplete = true, FootOverlapCount = grounded ? 1 : 0,
        ActualGroundSphereHit = grounded, AllFootHitsCurrentSupportOrTerrain = true, CastDistance = .224f, NormalDotUp = .8f,
    };

    private static VoyagerRemoteIdleAdmissionFacts IdleFacts()
    {
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(new VoyagerLocalWalkFacts
        {
            ModeTransitionsExcluded = true, IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
            WalkSpeed = 12, WalkPowerWatts = 1000, MiningPowerWatts = 200, OtherWorkPowerWatts = 100,
            ReactorConsumptionRatio = 2, RtsSpeed = 0, ManualSpeed = 0, SoftLandingRecover = 1,
        });
        Assert.True(reserve.Proved);
        var now = DateTimeOffset.UtcNow;
        return new VoyagerRemoteIdleAdmissionFacts
        {
            MovementReserve = reserve, CoreEnergyJoules = 1e8,
            JourneyBudget = new VoyagerLocalActionBudgetFacts
            {
                ExactRemoteBinding = true, NativePlayerIdle = false, CurrentGameTick = GameMain.gameTick,
                LastLandedGameTick = 50, SpentGameTicks = 10, MaximumGameTicks = 10000, ReturnReserveGameTicks = 3600,
                WorkGameTicks = 1, TailGameTicks = 13, OriginalExtraWarperReserve = 1, NativeWarperCount = 2,
                NowUtc = now, JourneyDeadlineUtc = now.AddMinutes(20), AvailableLoadedEnergyJoules = 2e8,
                LockedReturnFloorJoules = 1e8, WorkPowerUpperWatts = reserve.WorkPowerUpperWatts,
                TailAndNextTickEnergyJoules = reserve.StopEnergyJoules!.Value,
            },
        };
    }
}
