using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeResumeContactPolicyTests
{
    [Fact]
    public void InitializedTerrainRequiresAnActualCurrentLayerNineCastAndKeepsGroundFalse()
    {
        var s=LoadedTerrain();s.Layer=9;s.CastLayerMask=15873;s.CastDistance=.38f;
        Assert.True(NativeResumeContactPolicy.InitializedTerrainCastProved(s));
        Assert.False(NativeResumeContactPolicy.LoadedTerrainCastProved(s));Assert.False(s.ActualGroundSphereHit);
        s.CastHit=false;Assert.False(NativeResumeContactPolicy.InitializedTerrainCastProved(s));
    }
    [Fact]
    public void InitializedCurrentTerrainUsesRealCastAndFiniteNativeSettlingBeforeActualGround()
    {
        var p=new NativeResumeContactPolicy(100,Now,initializedTerrain:true);var f=Good();f.CastDistanceMeters=.38;
        Assert.Null(p.PreflightBlocker(f));Assert.Equal(32,p.WorkTickLimit);
        for(var i=1;i<=20;i++)
        {
            f=Good(100+i);f.NowUtc=f.OriginalJourney!.NowUtc=Now.AddMilliseconds(i*20);
            f.CastDistanceMeters=.38-i*.005;f.InwardDisplacementMeters=i*.005;f.SpeedMetersPerSecond=1.8;
            if(i==20){Ready(f);f.SpeedMetersPerSecond=0;}
            Assert.Equal(i==20?NativeResumeContactStep.Ready:NativeResumeContactStep.Pending,Frame(p,f));
        }
        Assert.True(p.Retired);Assert.Equal(120,p.LastObservedGameTick);
        var legacy=new NativeResumeContactPolicy(100,Now);f=Good();f.CastDistanceMeters=.38;
        Assert.NotNull(legacy.PreflightBlocker(f));Assert.Equal(8,legacy.WorkTickLimit);
    }
    [Fact]
    public void CurrentTerrainSettlingStillStopsAtItsOriginalFiniteAttemptWithoutGround()
    {
        var p=new NativeResumeContactPolicy(100,Now,initializedTerrain:true);var f=Good();f.CastDistanceMeters=.38;
        Assert.Null(p.PreflightBlocker(f));
        for(var i=1;i<=32;i++)
        {
            f=Good(100+i);f.NowUtc=f.OriginalJourney!.NowUtc=Now.AddMilliseconds(i*20);f.CastDistanceMeters=.38;
            Assert.Equal(i==32?NativeResumeContactStep.Rejected:NativeResumeContactStep.Pending,Frame(p,f));
        }
        Assert.False(p.Retired);Assert.Equal("remote_resume_contact_attempt_exhausted",p.Failure);
    }
    private static Spherewright.Contracts.Players.NativeNearGroundSupportSnapshot LoadedTerrain() => new()
    {
        State="observed",ActualGroundSphereHit=false,CastAttempted=true,CastHit=true,CastLayerMask=1 << 31,
        CastDistance=.224f,NormalDotUp=.9f,Layer=31,Shape="mesh",OwnerType="current_terrain",
        Enabled=true,ActiveInHierarchy=true,IsTrigger=false,SameDefaultPhysicsScene=true,
        CurrentPoolReference=true,LiveObject=true,ColliderChain=true,FootOverlapComplete=true,FootOverlapCount=0,
        AllFootHitsCurrentSupportOrTerrain=true,
    };
    [Fact]
    public void ExactLoadedTerrainCastFundsOnlyConditionalContactAndDoesNotInventNativeGroundOrIdle()
    {
        var s=LoadedTerrain();Assert.True(NativeResumeContactPolicy.LoadedTerrainCastProved(s));
        Assert.False(s.ActualGroundSphereHit);Assert.False(s.SafetyCertified);Assert.Null(s.NativeLiveFlag);
        var p=new NativeResumeContactPolicy(100,Now);Assert.Null(p.PreflightBlocker(Good()));
        Assert.False(p.Retired);Assert.Equal(100,p.LastObservedGameTick);
    }
    [Theory]
    [InlineData("mask")] [InlineData("layer")] [InlineData("normal")] [InlineData("gap")]
    [InlineData("owner")] [InlineData("reference")] [InlineData("mesh")] [InlineData("native_ground")]
    [InlineData("foot")] [InlineData("live_mark")] [InlineData("trigger")] [InlineData("pose")]
    public void LoadedTerrainCannotBorrowAnUnobservedOrForeignContactWitness(string fault)
    {
        var s=LoadedTerrain();
        switch(fault)
        {
            case "mask":s.CastLayerMask=15873;break;case "layer":s.Layer=9;break;
            case "normal":s.NormalDotUp=.49f;break;case "gap":s.CastDistance=.251f;break;
            case "owner":s.OwnerType="foreign_terrain";break;case "reference":s.CurrentPoolReference=null;break;
            case "mesh":s.Shape="box";break;case "native_ground":s.ActualGroundSphereHit=true;break;
            case "foot":s.FootOverlapCount=1;break;case "live_mark":s.NativeLiveFlag=true;break;
            case "trigger":s.IsTrigger=true;break;case "pose":s.ColliderChain=false;break;
        }
        Assert.False(NativeResumeContactPolicy.LoadedTerrainCastProved(s));
    }
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static NativeResumeContactFacts Good(long tick = 100) => new()
    {
        ExactRecoveryBinding = true, NonGroundNativePremisesProved = true, CurrentSupportReferenceProved = true,
        FootQueryComplete = true, NativeGroundSphereHit = false, CastDistanceMeters = .224, CastNormalDotUp = .8,
        FootOverlapCount = 0, AllFootHitsOwned = false, StrictPhysicalIdleProved = false,
        InwardDisplacementMeters = 0, TangentialDisplacementMeters = 0, SpeedMetersPerSecond = 0,
        CoreEnergyJoules = 100, PerNativeTickEnergyJoules = 2, NowUtc = Now,
        OriginalJourney = new() { ExactRemoteBinding = true, NativePlayerIdle = false, CurrentGameTick = tick,
            LastLandedGameTick = 50, SpentGameTicks = 10, MaximumGameTicks = 10000, ReturnReserveGameTicks = 3600,
            WorkGameTicks = 1, TailGameTicks = 13, OriginalExtraWarperReserve = 1, NativeWarperCount = 2,
            NowUtc = Now, JourneyDeadlineUtc = Now.AddMinutes(20), AvailableLoadedEnergyJoules = 300, LockedReturnFloorJoules = 200 },
    };
    private static void Ready(NativeResumeContactFacts f)
    { f.NativeGroundSphereHit = true; f.FootOverlapCount = 1; f.AllFootHitsOwned = true; f.StrictPhysicalIdleProved = true; f.OriginalJourney!.NativePlayerIdle = true; }
    private static NativeResumeContactStep Frame(NativeResumeContactPolicy p, NativeResumeContactFacts f)
        => p.ObserveUniverse(f) is null ? p.Observe(f) : NativeResumeContactStep.Rejected;

    [Fact]
    public void UniverseObservationKeepsTruthfulNoGroundFactsAndDoesNotConsumeAnyWorkTick()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(101);
        Assert.Null(p.ObserveUniverse(f)); Assert.False(f.OriginalJourney!.NativePlayerIdle);
        Assert.Equal(100, p.LastObservedGameTick); Assert.False(p.Retired);
        Assert.Equal(NativeResumeContactStep.Pending, p.Observe(f));
        Assert.Equal(101, p.LastObservedGameTick);
    }
    [Fact]
    public void IdleFlagCannotAdmitAStillMovingActor()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(101); Ready(f); f.SpeedMetersPerSecond = .101;
        Assert.Equal(NativeResumeContactStep.Rejected, Frame(p, f)); Assert.False(p.Retired);
    }

    [Fact]
    public void CastOnlyFundsAFiniteAttemptAndNeverClaimsGround()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good();
        Assert.Null(p.PreflightBlocker(f));
        for (var t = 101; t < 108; t++) { f.OriginalJourney!.CurrentGameTick = t; Assert.Equal(NativeResumeContactStep.Pending, Frame(p, f)); }
        f.OriginalJourney!.CurrentGameTick = 108;
        Assert.Equal(NativeResumeContactStep.Rejected, Frame(p, f));
        Ready(f); f.OriginalJourney.CurrentGameTick++;
        Assert.Equal(NativeResumeContactStep.Rejected, p.Observe(f)); Assert.False(p.Retired);
    }
    [Fact]
    public void EighthPhysicsBoundaryRequiresRealIdleOrPausesWithItsCurrent601StillFunded()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good();
        for (var t = 101; t < 108; t++) { f.OriginalJourney!.CurrentGameTick = t; Assert.Equal(NativeResumeContactStep.Pending, Frame(p, f)); }
        f.OriginalJourney!.CurrentGameTick = 108; Ready(f);
        Assert.Equal(NativeResumeContactStep.Ready, Frame(p, f));
        Assert.Equal(NativeResumeContactStep.AlreadyRetired, p.Observe(Good(110)));
    }
    [Fact]
    public void GroundQueryAloneDoesNotAdmitAnActorStillMoving()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(101);
        f.NativeGroundSphereHit = true; f.FootOverlapCount = 1; f.AllFootHitsOwned = true; f.SpeedMetersPerSecond = .4;
        Assert.Equal(NativeResumeContactStep.Pending, Frame(p, f)); Assert.False(p.Retired);
    }
    [Theory]
    [InlineData(100)] [InlineData(102)]
    public void MissingOrDuplicateOriginalTickIsStickyFailure(int tick)
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(tick);
        Assert.Equal(NativeResumeContactStep.Rejected, Frame(p, f));
        f = Good(101); Ready(f); Assert.Equal(NativeResumeContactStep.Rejected, Frame(p, f));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void UnprovedNativeFactsOrMotionNeverArm(int fault)
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good();
        switch (fault) {
            case 0: f.CurrentSupportReferenceProved = false; break;
            case 1: f.NonGroundNativePremisesProved = false; break;
            case 2: f.FootQueryComplete = false; break;
            case 3: f.NativeGroundSphereHit = null; break;
            case 4: f.CastDistanceMeters = .251; break;
            case 5: f.CastNormalDotUp = .49; break;
            case 6: f.InwardDisplacementMeters = -.011; break;
            case 7: f.InwardDisplacementMeters = .251; break;
            case 8: f.TangentialDisplacementMeters = .051; break;
            case 9: f.SpeedMetersPerSecond = 1.51; break;
            case 10: f.FootOverlapCount = 1; break;
            case 11: f.NativeGroundSphereHit = true; f.StrictPhysicalIdleProved = true; break;
        }
        Assert.NotNull(p.PreflightBlocker(f));
    }
    [Fact]
    public void WholeOriginalElapsedAndFourteenTickFundingRemainRequired()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good();
        f.CoreEnergyJoules = 28; f.OriginalJourney!.AvailableLoadedEnergyJoules = 228;
        f.OriginalJourney.MaximumGameTicks = 10 + 50 + 14 + 3600;
        Assert.Null(p.PreflightBlocker(f));
        f.CoreEnergyJoules = 27.99; Assert.NotNull(p.PreflightBlocker(f)); f.CoreEnergyJoules = 28;
        f.OriginalJourney.AvailableLoadedEnergyJoules = 227.99; Assert.NotNull(p.PreflightBlocker(f));
        f.OriginalJourney.AvailableLoadedEnergyJoules = 228; f.OriginalJourney.MaximumGameTicks--;
        Assert.NotNull(p.PreflightBlocker(f));
    }
    [Fact]
    public void StalledWallClockDoesNotRenewTheAttempt()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(101); f.NowUtc = Now.AddSeconds(2);
        Assert.Equal(NativeResumeContactStep.Rejected, Frame(p, f));
    }
    [Fact]
    public void PhysicsWithoutSameTickUniverseAndDuplicateUniverseAreStickyRejections()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(101);
        Assert.Equal(NativeResumeContactStep.Rejected, p.Observe(f));
        Assert.NotNull(p.ObserveUniverse(f));
        p = new NativeResumeContactPolicy(100, Now);
        Assert.Null(p.ObserveUniverse(f)); Assert.NotNull(p.ObserveUniverse(f));
        Assert.Equal(NativeResumeContactStep.Rejected, p.Observe(f));
    }
    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-1d)]
    public void UnknownOrInvalidFundingIsRejected(double bad)
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(); f.PerNativeTickEnergyJoules = bad;
        Assert.NotNull(p.PreflightBlocker(f));
    }

    [Fact]
    public void FirstActualGroupedPredicateIsRetainedWithoutChangingTheCompatibleFailureCode()
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(101);
        f.ExactRecoveryBinding = false; f.NonGroundNativePremisesProved = false;
        Assert.Equal("remote_resume_contact_native_support_unproved", p.ObserveUniverse(f));
        Assert.Equal("contact_exact_recovery_binding_unproved", p.FailureDetail);
        f.ExactRecoveryBinding = true;
        Assert.NotNull(p.ObserveUniverse(f));
        p.Reject("later_failure", "later_detail");
        Assert.Equal("remote_resume_contact_native_support_unproved", p.Failure);
        Assert.Equal("contact_exact_recovery_binding_unproved", p.FailureDetail);
    }

    [Theory]
    [InlineData(0, "contact_facts_missing")]
    [InlineData(1, "contact_non_ground_native_premises_unproved")]
    [InlineData(2, "contact_current_support_reference_unproved")]
    [InlineData(3, "contact_foot_query_incomplete")]
    [InlineData(4, "contact_native_ground_query_unavailable")]
    [InlineData(5, "contact_foot_count_unproved")]
    [InlineData(6, "contact_airborne_foot_overlap")]
    [InlineData(7, "contact_grounded_foot_owner_unproved")]
    [InlineData(8, "contact_idle_without_native_ground")]
    public void GroupDetailsComeFromTheOriginalDecisionFacts(int fault, string detail)
    {
        var p = new NativeResumeContactPolicy(100, Now); var f = Good(101);
        switch (fault)
        {
            case 0: f = null!; break;
            case 1: f.NonGroundNativePremisesProved = false; break;
            case 2: f.CurrentSupportReferenceProved = false; break;
            case 3: f.FootQueryComplete = false; break;
            case 4: f.NativeGroundSphereHit = null; break;
            case 5: f.FootOverlapCount = -1; break;
            case 6: f.FootOverlapCount = 1; break;
            case 7: f.NativeGroundSphereHit = true; f.FootOverlapCount = 1; break;
            case 8: f.StrictPhysicalIdleProved = true; break;
        }
        Assert.Equal("remote_resume_contact_native_support_unproved", p.ObserveUniverse(f));
        Assert.Equal(detail, p.FailureDetail);
    }
}
