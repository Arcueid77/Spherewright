using Newtonsoft.Json;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using System.Reflection;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerSurfaceRecoveryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NativeWalkTransitionCanFinishItsFundedFallWithoutPretendingToBeGrounded()
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now); var f = Good();
        f.NativeModeBlocker = "remote_idle_ground_contact_unproved";
        f.StrictGroundedWalkWithZeroTail = true; f.OriginalOrderAbsent = true; f.DistanceToTargetMeters = 1;
        Assert.Null(p.ObserveUniverse(101, Now, true));
        Assert.Equal(VoyagerSurfaceRecoveryStep.Continue, p.ObservePhysics(f));
        Assert.False(p.Completed); Assert.False(f.OriginalJourney!.NativePlayerIdle);
        f.GameTick = f.OriginalJourney.CurrentGameTick = 102; f.NativeModeBlocker = null;
        Assert.Null(p.ObserveUniverse(102, Now, true));
        Assert.Equal(VoyagerSurfaceRecoveryStep.Complete, p.ObservePhysics(f));
    }

    [Theory]
    [InlineData("support")][InlineData("funding")][InlineData("mode")][InlineData("independent_guard")]
    public void GroundMissCannotHideAnyOtherMissingNativeProof(string fault)
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now); var f = Good();
        f.NativeModeBlocker = "remote_idle_ground_contact_unproved";
        if (fault == "support") f.CurrentTerrainSupportProved = false;
        if (fault == "funding") f.CoreEnergyJoules = 139;
        if (fault == "mode") f.NativeModeBlocker = "remote_idle_movement_mode_unproved";
        if (fault == "independent_guard") f.NativeModesCostsAndOrderProved = false;
        Assert.Null(p.ObserveUniverse(101, Now, true));
        Assert.Equal(VoyagerSurfaceRecoveryStep.Rejected, p.ObservePhysics(f));
        Assert.False(p.Completed);
    }

    [Fact]
    public void NewTaskNeedsReal151BeforeEach501AndOnlyStrictGroundedIdleCompletes()
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now);
        var f = Good();
        for (var tick = 101L; tick <= 150; tick++)
        {
            f.GameTick = f.OriginalJourney!.CurrentGameTick = tick;
            Assert.Null(p.ObserveUniverse(tick, f.NowUtc, true));
            Assert.Equal(VoyagerSurfaceRecoveryStep.Continue, p.ObservePhysics(f));
            Assert.False(f.OriginalJourney.NativePlayerIdle);
        }
        f.GameTick = f.OriginalJourney!.CurrentGameTick = 151;
        f.StrictGroundedWalkWithZeroTail = true; f.OriginalOrderAbsent = true;
        f.DistanceToTargetMeters = 1;
        Assert.Null(p.ObserveUniverse(151, f.NowUtc, true));
        Assert.Equal(VoyagerSurfaceRecoveryStep.Complete, p.ObservePhysics(f));
        Assert.True(p.Completed);
        Assert.False(f.OriginalJourney.NativePlayerIdle);
        Assert.Equal(13, VoyagerLocalWalkReservePolicy.StopGameTicks);
    }

    [Theory]
    [InlineData("151")][InlineData("source")][InlineData("native")][InlineData("support")][InlineData("landing")]
    [InlineData("core")][InlineData("loaded")][InlineData("time")][InlineData("ticks")][InlineData("warpers")]
    [InlineData("idle_surrogate")][InlineData("displacement")][InlineData("nonfinite")][InlineData("journal_tick")]
    public void MissingProofStopsPermanentlyWithoutBorrowingAnotherAction(string fault)
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now); var f = Good();
        if (fault != "151") Assert.Null(p.ObserveUniverse(101, Now, true));
        switch (fault)
        {
            case "source": f.ExactSourceWorldAndScene = false; break;
            case "native": f.NativeModesCostsAndOrderProved = false; break;
            case "support": f.CurrentTerrainSupportProved = false; break;
            case "landing": f.DryLandingPatchProved = false; break;
            case "core": f.CoreEnergyJoules = 139; break;
            case "loaded": f.OriginalJourney!.AvailableLoadedEnergyJoules = 1139; break;
            case "time": f.OriginalJourney!.JourneyDeadlineUtc = Now.AddSeconds(61); break;
            case "ticks": f.OriginalJourney!.MaximumGameTicks = 3600; break;
            case "warpers": f.OriginalJourney!.NativeWarperCount = 0; break;
            case "idle_surrogate": f.OriginalJourney!.NativePlayerIdle = true; break;
            case "displacement": f.DistanceFromSourceMeters = 24.01; break;
            case "nonfinite": f.DistanceToTargetMeters = double.NaN; break;
            case "journal_tick": f.OriginalJourney!.CurrentGameTick = 102; break;
        }
        Assert.Equal(VoyagerSurfaceRecoveryStep.Rejected, p.ObservePhysics(f));
        Assert.NotNull(p.Failure);
        Assert.Equal(VoyagerSurfaceRecoveryStep.Rejected, p.ObservePhysics(Good()));
        Assert.False(p.Completed);
    }

    [Fact]
    public void DuplicatePhysicsIsNotAnotherNativeTickOrANewLease()
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now); var f = Good();
        Assert.Null(p.ObserveUniverse(101, Now, true));
        Assert.Equal(VoyagerSurfaceRecoveryStep.Continue, p.ObservePhysics(f));
        Assert.Equal(VoyagerSurfaceRecoveryStep.Rejected, p.ObservePhysics(f));
        Assert.Equal(101, p.LastPhysicsTick);
    }

    [Fact]
    public void InitialDriftBindingExpiresBeforeEitherFirst151OrFirst501()
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now);
        Assert.NotNull(p.ObserveUniverse(101, Now.AddSeconds(2), true));
        p = new VoyagerSurfaceRecoveryPolicy(100, Now);
        Assert.Null(p.ObserveUniverse(101, Now.AddSeconds(1.9), true));
        var f = Good(); f.NowUtc = f.OriginalJourney!.NowUtc = Now.AddSeconds(2);
        Assert.Equal(VoyagerSurfaceRecoveryStep.Rejected, p.ObservePhysics(f));
    }

    [Fact]
    public void Duplicate151CannotRenewInitialBindingEvenWithAForwardClock()
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now);
        Assert.Null(p.ObserveUniverse(101, Now.AddSeconds(.1), true));
        Assert.NotNull(p.ObserveUniverse(101, Now.AddSeconds(.2), true));
        Assert.Equal(100L, p.LastPhysicsTick);
    }

    [Fact]
    public void WallDeadlineAndClockReversalCannotBeRenewedBy151()
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now);
        Assert.NotNull(p.ObserveUniverse(101, Now.AddSeconds(30), true));
        p = new VoyagerSurfaceRecoveryPolicy(100, Now);
        Assert.Null(p.ObserveUniverse(101, Now.AddSeconds(.1), true));
        Assert.NotNull(p.ObserveUniverse(101, Now, true));
    }

    [Fact]
    public void WorkCapIncludesTheFailure601AndDoesNotResetOnSuccessfulObservations()
    {
        var p = new VoyagerSurfaceRecoveryPolicy(100, Now); var f = Good();
        for (var tick = 101L; tick < 700; tick++)
        {
            f.GameTick = f.OriginalJourney!.CurrentGameTick = tick;
            Assert.Null(p.ObserveUniverse(tick, Now, true));
            Assert.Equal(VoyagerSurfaceRecoveryStep.Continue, p.ObservePhysics(f));
        }
        f.GameTick = f.OriginalJourney!.CurrentGameTick = 700;
        Assert.Null(p.ObserveUniverse(700, Now, true));
        Assert.Equal(VoyagerSurfaceRecoveryStep.Rejected, p.ObservePhysics(f));
        Assert.Equal("surface_recovery_work_exhausted", p.Failure);
    }

    [Fact]
    public void OptionalTargetIsDetachedAndAbsentFromHistoricalProposalByteShape()
    {
        var target = new Vector3Snapshot { X = 200 };
        var proposal = new VoyagerRecoveryWindowProposal(Now, "test", 1, 1, surfaceRecoveryTarget: target);
        target.X = 300;
        Assert.Equal(200f, proposal.SurfaceRecoveryTarget!.X);
        proposal.SurfaceRecoveryTarget.X = 400;
        Assert.Equal(200f, proposal.SurfaceRecoveryTarget.X);
        Assert.DoesNotContain("SurfaceRecoveryTarget", JsonConvert.SerializeObject(new VoyagerRecoveryWindowProposal(Now, "test", 1, 1)));
        Assert.Contains("SurfaceRecoveryTarget", JsonConvert.SerializeObject(proposal));
    }

    [Fact]
    public void SurfaceRecoveryRequiresExplicitFiniteAuthorizationAndCannotDefaultOrGrantTicksItself()
    {
        var r = new PrepareOwnedWorldResumeRequest { SurfaceRecoveryTarget = new Vector3Snapshot { X = 200 } };
        Assert.NotNull(VoyagerSurfaceRecoveryPolicy.ValidateRequest(r));
        r.RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery;
        r.MaximumChargeGameTicks = 600; r.MaximumChargeWallSeconds = 30;
        Assert.Null(VoyagerSurfaceRecoveryPolicy.ValidateRequest(r));
        r.AdditionalVoyagerGameTicks = 1;
        Assert.Null(VoyagerSurfaceRecoveryPolicy.ValidateRequest(r));
        r.AdditionalVoyagerGameTicks = VoyagerRecoveryWindowPolicy.MaximumAdditionalVoyagerGameTicks + 1;
        Assert.NotNull(VoyagerSurfaceRecoveryPolicy.ValidateRequest(r));
        r.AdditionalVoyagerGameTicks = 0; r.SurfaceRecoveryTarget.X = float.NaN;
        Assert.NotNull(VoyagerSurfaceRecoveryPolicy.ValidateRequest(r));
    }

    [Fact]
    public void RecoveryReadbackCopiesEveryPositionAndCannotGrantTicksOrRewriteAnOutcome()
    {
        var live = new NativeSurfaceRecoveryMoveSnapshot
        {
            ResumeActionId = "new-resume", SourcePosition = new Vector3Snapshot { X = 1 },
            TargetPosition = new Vector3Snapshot { Y = 2 }, CurrentPosition = new Vector3Snapshot { Z = 3 },
        };
        var copy = live.Copy();
        live.SourcePosition.X = 4; live.TargetPosition.Y = 5; live.CurrentPosition.Z = 6;
        Assert.Equal(1f, copy.SourcePosition.X); Assert.Equal(2f, copy.TargetPosition.Y); Assert.Equal(3f, copy.CurrentPosition.Z);
        Assert.False(copy.NewGameTicksGranted); Assert.False(copy.OriginalUnknownOutcomeChanged);
        Assert.False(copy.StrictPhysicalIdleProved);
    }

    [Fact]
    public void ProductionResumePayloadBindsTheTargetAndDetachesCallerMutation()
    {
        // Exercise the actual production plan constructor, not a surrogate
        // fingerprint function. This proves disclosure binding, not loading.
        var type = typeof(OwnedWorldResumeCoordinator).GetNestedType("OwnedWorldResumePlanPayload", BindingFlags.NonPublic)!;
        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single();
        object Plan(PrepareOwnedWorldResumeRequest r) => constructor.Invoke(new object?[]
        {
            new OwnedWorldResumeTicket(), OwnedWorldResumeSourceKind.LastExit, r, null, "synthetic", 1L,
            "", null, null, "", "", null, null,
        });
        string Fingerprint(object p) => (string)type.GetProperty("Fingerprint")!.GetValue(p)!;
        var request = new PrepareOwnedWorldResumeRequest
        {
            RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery,
            SurfaceRecoveryTarget = new Vector3Snapshot { X = 200, Y = 1, Z = 2 },
        };
        var first = Plan(request); var hash = Fingerprint(first);
        request.SurfaceRecoveryTarget.Z = 3;
        Assert.NotEqual(hash, Fingerprint(Plan(request)));
        var stored = (PrepareOwnedWorldResumeRequest)type.GetProperty("Request")!.GetValue(first)!;
        Assert.Equal(2f, stored.SurfaceRecoveryTarget!.Z);
        Assert.Equal(hash, Fingerprint(first));
        request.SurfaceRecoveryTarget = null;
        Assert.NotEqual(hash, Fingerprint(Plan(request)));
    }

    private static VoyagerSurfaceRecoveryFacts Good() => new()
    {
        GameTick = 101, NowUtc = Now, ExactSourceWorldAndScene = true,
        NativeModesCostsAndOrderProved = true, CurrentTerrainSupportProved = true, DryLandingPatchProved = true,
        DistanceFromSourceMeters = 1, DistanceToTargetMeters = 6,
        CoreEnergyJoules = 10000, PerNativeTickEnergyJoules = 10,
        OriginalJourney = new VoyagerLocalActionBudgetFacts
        {
            ExactRemoteBinding = true, NativePlayerIdle = false, LastLandedGameTick = 100, CurrentGameTick = 101,
            SpentGameTicks = 100, MaximumGameTicks = 10000, ReturnReserveGameTicks = 3600,
            WorkGameTicks = 1, TailGameTicks = 13, NowUtc = Now, JourneyDeadlineUtc = Now.AddMinutes(10),
            AvailableLoadedEnergyJoules = 10000, LockedReturnFloorJoules = 1000, NativeWarperCount = 2,
        },
    };
}
