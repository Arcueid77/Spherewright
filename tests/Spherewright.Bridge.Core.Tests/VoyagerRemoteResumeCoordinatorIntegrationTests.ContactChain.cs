using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;
using UnityEngine;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    // Real coordinator consumption/Store/leases, production retained-player and
    // contact facts/transition/completion. The shell supplies Native observations
    // and the audited151->501->601 ordering. No PhysX, loader, real Save, terrain
    // registration, Native energy generator or whole GameSessionTracker is run.
    [Fact]
    public void StoreBoundPausedDirtyProjectionContactAndStrictTakeoverComposeWithoutAReadyFlag()
    {
        using var s = new Scenario();
        Assert.True(s.Commit(s.Prepare()).Success);
        var evidence = s.Sessions.RemoteEvidence!;
        var journey = s.Checkpoints.VoyagerJourney!;
        Assert.NotSame(journey, s.Checkpoints.VoyagerJourney);
        Assert.True(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(journey, evidence));
        const long source = 12510;
        GameMain.gameTick = source; GameMain.isPaused = false;
        var player = new Player { position = new(.12345679f, 200, 0),
            uPosition = new(1000.1234567890123, 2200, 3000) };
        player.mecha.coreEnergy = 1e8; player.mecha.LoadedEnergy = journey.ReturnEnergyFloorJoules + 1e8;
        var relative = new VectorLF3(1000, 2000, 3000); var rotation = new Quaternion(0, 0, 0, 1);
        var retained = new NormalGameActionCoordinator.GroundStateProbe(player, relative, rotation);
        GameMain.isPaused = true;
        var init = new NativeResumeInitializationPolicy(source, DateTimeOffset.UtcNow);
        NativeResumeInitializationFacts Initialization(int count, bool dirty) => new()
        {
            CurrentGameTick = GameMain.gameTick, NowUtc = DateTimeOffset.UtcNow, NativePaused = GameMain.isPaused,
            ExactSourceWorldBinding = s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(journey, evidence),
            NativeStateUnchanged = retained.DuringPause(player), RegisteredCount = count, ColliderRefreshPending = dirty,
        };
        Assert.Equal(NativeResumeInitializationStep.Pending, init.Observe(Initialization(0, true)));
        Assert.Equal(NativeResumeInitializationStep.Pending, init.Observe(Initialization(1, true)));
        var ready = Initialization(1, false);
        Assert.Equal(NativeResumeInitializationStep.Ready, init.Observe(ready));
        ready.CurrentSupportOrGroundProof = FixtureSupport(source).FootOverlapComplete == true;
        Assert.True(init.TryAuthorizeResume(ready));
        Assert.Equal(source, GameMain.gameTick); // Paused host pumps spent zero Native ticks.
        GameMain.isPaused = false;
        var contact = new NormalGameActionCoordinator.ContactStateProbe(player, source);
        var support = FixtureSupport(source);
        var idle = ChainIdleFacts(s, player, support);
        Assert.Null(contact.Policy.PreflightBlocker(contact.Facts(player, idle, support)));
        var adoption = new VoyagerRemoteResumeAdoption();
        Assert.Null(adoption.FirstFrame.ArmNearSupport(source, true, true, true, true,
            () => retained.Physics(player, source + 1)));
        var projected = relative + Maths.QRotateLF(rotation, player.position);
        Assert.NotEqual(player.uPosition, projected); player.uPosition = projected;
        GameMain.gameTick = source + 1;
        Assert.Null(retained.Universe(player, source + 1, out var observed));
        Assert.Null(adoption.FirstFrame.ObserveUniverse(GameMain.gameTick, true, true, true));
        Assert.True(retained.Pin(observed));
        support.CapturedAtGameTick = GameMain.gameTick; idle = ChainIdleFacts(s, player, support);
        var f = contact.Facts(player, idle, support);
        Assert.Null(NormalGameActionCoordinator.AdvanceContactForTest(adoption, contact.Policy, f, false));
        Assert.False(s.Sessions.CompleteContactForTest(NativeResumeContactPolicy.GroundingPending));
        Assert.False(evidence.IsDisposed);
        Assert.Null(adoption.FirstFrame.ObservePhysics(GameMain.gameTick, true));
        Assert.Equal(NativeResumeContactPolicy.GroundingPending,
            NormalGameActionCoordinator.AdvanceContactForTest(adoption, contact.Policy, f, true));
        // A conservative cost debit and bounded Native motion are substitutes,
        // not a claim that Unity will land within this path or eight ticks.
        var perTick = idle.MovementReserve!.PerNativeTickEnergyJoules!.Value;
        player.mecha.coreEnergy -= perTick; player.mecha.LoadedEnergy -= perTick;
        player.transform.localRotation = new(0, .6f, 0, .8f);
        player.transform.lossyScale = new(1.0000002f, 1.0000001f, 1.0000001f);
        player.position = player.position - new Vector3(0, .006f, 0);
        player.speed = .45f; player.controller.velocity = new(0, -.45f, 0);
        player.controller.rigid.velocity = player.controller.velocity; player.controller.vertSpeed = -.45f;
        GameMain.gameTick++; support.CapturedAtGameTick = GameMain.gameTick;
        idle = ChainIdleFacts(s, player, support); f = contact.Facts(player, idle, support);
        Assert.False(f.StrictPhysicalIdleProved); Assert.True(f.ExactRecoveryBinding);
        Assert.Null(NormalGameActionCoordinator.AdvanceContactForTest(adoption, contact.Policy, f, false));
        Assert.Equal(NativeResumeContactPolicy.GroundingPending,
            NormalGameActionCoordinator.AdvanceContactForTest(adoption, contact.Policy, f, true));
        player.mecha.coreEnergy -= perTick; player.mecha.LoadedEnergy -= perTick;
        // A hypothetical legal collision result, deliberately distinct from the
        // sanitized preflight observation and from the unknown historical cause.
        GameMain.gameTick++; support.CapturedAtGameTick = GameMain.gameTick;
        support.ActualGroundSphereHit = true; support.FootOverlapCount = 1;
        player.speed = 0; player.controller.vertSpeed = 0;
        player.controller.velocity = default; player.controller.rigid.velocity = default;
        idle = ChainIdleFacts(s, player, support); f = contact.Facts(player, idle, support);
        Assert.Null(NormalGameActionCoordinator.AdvanceContactForTest(adoption, contact.Policy, f, false));
        Assert.True(adoption.NativeContactPending); Assert.False(evidence.IsDisposed);
        Assert.Null(NormalGameActionCoordinator.AdvanceContactForTest(adoption, contact.Policy, f, true));
        Assert.False(adoption.NativeContactPending);
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(idle).Proved);
        Assert.True(s.Sessions.CompleteContactForTest(null));
        Assert.True(s.Sessions.ContactAdmittedForTest); Assert.True(evidence.IsDisposed);
        Assert.False(s.Sessions.CompleteContactForTest(null));
        foreach (var path in s.ProtectedEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Theory]
    [InlineData("owner")] [InlineData("unknown_foot")] [InlineData("duplicate151")]
    [InlineData("physics_first")] [InlineData("budget")] [InlineData("cancel")]
    public void FailedContactKeepsRealEvidenceLeasesAndTheFirstCutThroughTheFailure601(string fault)
    {
        using var s = new Scenario(); Assert.True(s.Commit(s.Prepare()).Success);
        const long source = 12510;
        GameMain.gameTick = source; GameMain.isPaused = false;
        var player = new Player(); player.mecha.coreEnergy = 1e8;
        player.mecha.LoadedEnergy = s.Checkpoints.VoyagerJourney!.ReturnEnergyFloorJoules + 1e8;
        var state = new NormalGameActionCoordinator.ContactStateProbe(player, source);
        var adoption = new VoyagerRemoteResumeAdoption(); var support = FixtureSupport(source);
        GameMain.gameTick++;
        support.CapturedAtGameTick = GameMain.gameTick;
        var idle = ChainIdleFacts(s, player, support);
        if (fault == "owner") support.CurrentPoolReference = false;
        if (fault == "unknown_foot") support.FootOverlapComplete = null;
        if (fault == "budget") idle.JourneyBudget!.MaximumGameTicks = 1;
        var f = state.Facts(player, idle, support);
        string? code;
        if (fault == "cancel") { adoption.FirstFrame.Fail("original_cancel"); code = "original_cancel"; }
        else if (fault == "physics_first") code = NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, f, true);
        else
        {
            code = NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, f, false);
            if (fault == "duplicate151") code = NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, f, false);
        }
        Assert.NotNull(code); Assert.False(s.Sessions.CompleteContactForTest(code));
        Assert.False(s.Sessions.ContactAdmittedForTest); Assert.False(s.Sessions.RemoteEvidence!.IsDisposed);
        Assert.ThrowsAny<IOException>(() => { using var write = new FileStream(s.JournalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
        var cut = adoption.FirstFailure?.Copy();
        var before = player.mecha.coreEnergy; var p = idle.MovementReserve!.PerNativeTickEnergyJoules!.Value;
        GameMain.isPaused = true; // Native Pause does not abort an already-entered LogicFrame.
        player.mecha.coreEnergy -= p; player.mecha.LoadedEnergy -= p; // Failure frame601 remains paid.
        Assert.True(before >= NativeResumeContactPolicy.FundedTicks * p);
        Assert.True(player.mecha.coreEnergy >= VoyagerLocalWalkReservePolicy.StopGameTicks * p);
        support.FootOverlapComplete = true; support.CurrentPoolReference = true;
        var later = state.Facts(player, idle, support);
        Assert.Equal(code, NormalGameActionCoordinator.AdvanceContactForTest(adoption, state.Policy, later, true));
        Assert.False(s.Sessions.CompleteContactForTest(null));
        if (cut is not null)
        {
            Assert.Equal(before, cut.Values["core_j"]);
            Assert.Equal(GameMain.gameTick, cut.CapturedAtGameTick);
            Assert.Equal(cut.Code, adoption.FirstFailure!.Code);
            Assert.Equal(cut.FirstFailedPredicate, adoption.FirstFailure.FirstFailedPredicate);
            Assert.Equal(cut.Values["core_j"], adoption.FirstFailure.Values["core_j"]);
            if (fault == "unknown_foot") Assert.Equal("unknown", cut.Predicates["contact_foot_query_incomplete"]);
        }
    }

    private sealed class ContactFixture { public NativeNearGroundSupportSnapshot Preflight { get; set; } = new(); }
    private static NativeNearGroundSupportSnapshot FixtureSupport(long tick)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-resume-contact.json");
        var f = PluginJson.Deserialize<ContactFixture>(File.ReadAllText(path))!.Preflight;
        f.CapturedAtGameTick = tick;
        // Reference ownership is supplied by the Native shell, not present in
        // the sanitized scalar fixture; production fact composition reads it.
        f.CurrentPoolReference = true; f.LiveObject = true; f.ColliderChain = true;
        return f;
    }

    private static VoyagerRemoteIdleAdmissionFacts ChainIdleFacts(Scenario s, Player p, NativeNearGroundSupportSnapshot support)
    {
        var j = s.Checkpoints.VoyagerJourney!;
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(new VoyagerLocalWalkFacts
        {
            ModeTransitionsExcluded = true, IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
            WalkSpeed = 12, WalkPowerWatts = 1000, MiningPowerWatts = 200, OtherWorkPowerWatts = 100,
            ReactorConsumptionRatio = 2, RtsSpeed = 0, ManualSpeed = 0, SoftLandingRecover = 1,
        });
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            MovementReserve = reserve, CoreEnergyJoules = p.mecha.coreEnergy,
            JourneyBudget = new()
            {
                ExactRemoteBinding = s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(j, s.Sessions.RemoteEvidence!),
                NativePlayerIdle = support.ActualGroundSphereHit == true && p.speed <= .1f,
                CurrentGameTick = GameMain.gameTick, LastLandedGameTick = j.LastLandedGameTick, SpentGameTicks = j.SpentGameTicks,
                MaximumGameTicks = j.MaximumGameTicks, ReturnReserveGameTicks = j.ReturnReserveGameTicks,
                WorkGameTicks = 1, TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks,
                NowUtc = now, JourneyDeadlineUtc = j.DeadlineUtc,
                AvailableLoadedEnergyJoules = p.mecha.LoadedEnergy, LockedReturnFloorJoules = j.ReturnEnergyFloorJoules,
                WorkPowerUpperWatts = reserve.WorkPowerUpperWatts, TailAndNextTickEnergyJoules = reserve.StopEnergyJoules!.Value,
                NativeWarperCount = 1 + j.ExtraWarperReserve, OriginalExtraWarperReserve = j.ExtraWarperReserve,
            },
        };
    }
}
