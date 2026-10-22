using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;
using UnityEngine;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    // Uses the retained sanitized observation and production contact facts,
    // projection/sequence policies and real Store. Unity collision, registration,
    // energy generation and the load callback remain explicit substitutes.
    private static void TraverseReturnContinuationNativeContract(Fixture f)
    {
        var source = GameMain.gameTick; var j = f.Store.VoyagerJourney!;
        var p = new Player { position = new(.12345679f, 200, 0), uPosition = new(1000.1234567890123, 2200, 3000) };
        p.mecha.coreEnergy = f.NormalActions.NativeIdleObservation.Core!.Value;
        p.mecha.LoadedEnergy = f.NormalActions.NativeIdleObservation.Loaded!.Value;
        var relative = new VectorLF3(1000, 2000, 3000); var rotation = new Quaternion(0, 0, 0, 1);
        var retained = new NormalGameActionCoordinator.GroundStateProbe(p, relative, rotation);
        var support = PluginJson.Deserialize<ReturnContactFixture>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-resume-contact.json")))!.Preflight;
        support.CapturedAtGameTick = source;
        // These reference facts belong to the Native shell, not the raw fixture.
        support.CurrentPoolReference = support.LiveObject = support.ColliderChain = true;
        GameMain.isPaused = true;
        var init = new NativeResumeInitializationPolicy(source, DateTimeOffset.UtcNow);
        NativeResumeInitializationFacts Initialization(int count, bool dirty) => new()
        {
            CurrentGameTick = GameMain.gameTick,
            NowUtc = DateTimeOffset.UtcNow,
            NativePaused = GameMain.isPaused,
            ExactSourceWorldBinding = f.Store.RemoteResumeRetainedJourneyStillMatches(j, f.Sessions.RemoteEvidence!),
            NativeStateUnchanged = retained.DuringPause(p),
            RegisteredCount = count,
            ColliderRefreshPending = dirty
        };
        Assert.Equal(NativeResumeInitializationStep.Pending, init.Observe(Initialization(0, true)));
        Assert.Equal(NativeResumeInitializationStep.Pending, init.Observe(Initialization(1, true)));
        var initialized = Initialization(1, false); Assert.Equal(NativeResumeInitializationStep.Ready, init.Observe(initialized));
        initialized.CurrentSupportOrGroundProof = support.FootOverlapComplete == true;
        Assert.True(init.TryAuthorizeResume(initialized)); Assert.Equal(source, GameMain.gameTick);
        GameMain.isPaused = false;
        var state = new NormalGameActionCoordinator.ContactStateProbe(p, source);
        var idle = ReturnContinuationContactFacts(f, p, support);
        Assert.Equal(j.MaximumGameTicks, idle.JourneyBudget!.SpentGameTicks);
        Assert.Null(state.Policy.PreflightBlocker(state.Facts(p, idle, support)));
        var adoption = new VoyagerRemoteResumeAdoption();
        Assert.Null(adoption.FirstFrame.ArmNearSupport(source, true, true, true, true, () => retained.Physics(p, source + 1)));
        var projected = relative + Maths.QRotateLF(rotation, p.position);
        Assert.NotEqual(p.uPosition, projected); p.uPosition = projected;
        GameMain.gameTick++; support.CapturedAtGameTick = GameMain.gameTick;
        Assert.Null(retained.Universe(p, GameMain.gameTick, out var observed)); Assert.True(retained.Pin(observed));
        Assert.Null(adoption.FirstFrame.ObserveUniverse(GameMain.gameTick, true, true, true));
        idle = ReturnContinuationContactFacts(f, p, support);
        var facts = state.Facts(p, idle, support);
        Assert.False(facts.StrictPhysicalIdleProved);
        Assert.Null(NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, facts, false));
        Assert.Null(adoption.FirstFrame.ObservePhysics(GameMain.gameTick, true));
        Assert.Equal(NativeResumeContactPolicy.GroundingPending,
            NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, facts, true));
        Assert.False(f.Sessions.CompleteContactForTest(NativeResumeContactPolicy.GroundingPending));
        var cost = idle.MovementReserve!.PerNativeTickEnergyJoules!.Value;
        p.mecha.coreEnergy -= cost; p.mecha.LoadedEnergy -= cost;
        p.position -= new Vector3(0, .006f, 0); p.speed = .45f;
        p.controller.velocity = p.controller.rigid.velocity = new(0, -.45f, 0); p.controller.vertSpeed = -.45f;
        GameMain.gameTick++; support.CapturedAtGameTick = GameMain.gameTick;
        idle = ReturnContinuationContactFacts(f, p, support); facts = state.Facts(p, idle, support);
        Assert.False(facts.StrictPhysicalIdleProved);
        Assert.Null(NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, facts, false));
        Assert.Equal(NativeResumeContactPolicy.GroundingPending,
            NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, facts, true));
        p.mecha.coreEnergy -= cost; p.mecha.LoadedEnergy -= cost;
        p.speed = p.controller.vertSpeed = 0; p.controller.velocity = p.controller.rigid.velocity = default;
        GameMain.gameTick++; support.CapturedAtGameTick = GameMain.gameTick;
        support.ActualGroundSphereHit = true; support.FootOverlapCount = 1;
        idle = ReturnContinuationContactFacts(f, p, support); facts = state.Facts(p, idle, support);
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(idle).Proved);
        Assert.Null(NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, facts, false));
        Assert.Null(NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, facts, true));
        Assert.True(state.Policy.Retired); Assert.False(adoption.NativeContactPending);
        Assert.False(f.Sessions.RemoteEvidence!.IsDisposed);
        f.NormalActions.NativeIdleObservation.Core = p.mecha.coreEnergy;
        f.NormalActions.NativeIdleObservation.Loaded = p.mecha.LoadedEnergy;
        Assert.Equal(j.SpentGameTicks, f.Store.VoyagerJourney!.SpentGameTicks);
        Assert.Equal(j.DeadlineUtc, f.Store.VoyagerJourney.DeadlineUtc);
    }

    private static VoyagerRemoteIdleAdmissionFacts ReturnContinuationContactFacts(Fixture f, Player p, NativeNearGroundSupportSnapshot support)
    {
        var j = f.Store.VoyagerJourney!; var t = j.ReturnOnlyTask!; var cost = f.NormalActions.ContractMovementCostForTest();
        return new()
        {
            MovementReserve = cost,
            CoreEnergyJoules = p.mecha.coreEnergy,
            JourneyBudget = new()
            {
                ExactRemoteBinding = f.Store.RemoteResumeRetainedJourneyStillMatches(j, f.Sessions.RemoteEvidence!),
                NativePlayerIdle = support.ActualGroundSphereHit == true && p.speed <= .1f,
                CurrentGameTick = GameMain.gameTick,
                LastLandedGameTick = j.LastLandedGameTick,
                SpentGameTicks = j.SpentGameTicks,
                MaximumGameTicks = j.MaximumGameTicks,
                ReturnReserveGameTicks = j.ReturnReserveGameTicks,
                WorkGameTicks = 1,
                TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks,
                NowUtc = DateTimeOffset.UtcNow,
                JourneyDeadlineUtc = t.CurrentDeadlineUtc.AddSeconds(-VoyagerControlDeadlinePolicy.StopCleanupSeconds),
                AvailableLoadedEnergyJoules = p.mecha.LoadedEnergy,
                LockedReturnFloorJoules = j.ReturnEnergyFloorJoules,
                WorkPowerUpperWatts = cost.WorkPowerUpperWatts,
                TailAndNextTickEnergyJoules = cost.StopEnergyJoules!.Value,
                NativeWarperCount = f.NormalActions.NativeIdleObservation.Warpers,
                OriginalExtraWarperReserve = j.ExtraWarperReserve
            },
            ReturnOnlyBudget = new()
            {
                ExactTaskBinding = t.CurrentSessionId == f.Sessions.SessionId,
                StartedAtGameTick = t.StartedAtGameTick,
                MaximumGameTicks = t.MaximumGameTicks,
                DeadlineUtc = t.CurrentDeadlineUtc
            }
        };
    }

    private sealed class ReturnContactFixture { public NativeNearGroundSupportSnapshot Preflight { get; set; } = new(); }
}
