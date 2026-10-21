using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeFirstFrameAdmissionTests
{
    [Theory]
    [InlineData("support")] [InlineData("recheck")] [InlineData("funding")]
    public void NearSupportRequiresItsOwnRetainedProofAndNeverReplacesMissingTerrainFootEvidence(string fault)
    {
        var frame = new NativeFirstFrameAdmission();
        Assert.NotNull(frame.ArmNearSupport(100, true, true, fault != "support", fault != "funding",
            fault == "recheck" ? null! : () => null));
        Assert.False(frame.Armed);
        Assert.NotNull(frame.Arm(100, true, true, true, true));
        var original = new NativeFirstFrameAdmission();
        Assert.NotNull(original.Arm(100, true, true, false, true));
    }
    [Fact]
    public void NearSupport151NeverAdmitsIdleAndRetainedSupportMustBeRecheckedAfterTheActual501()
    {
        var calls = 0; var frame = new NativeFirstFrameAdmission();
        Assert.Null(frame.ArmNearSupport(100, true, true, true, true, () => { calls++; return null; }));
        Assert.Null(frame.ObserveUniverse(101, true, true, true));
        Assert.Equal(0, calls); Assert.False(frame.PhysicsObserved);
        Assert.Null(frame.ObservePhysics(101, true)); Assert.Equal(1, calls);
        Assert.NotNull(frame.ObservePhysics(101, true)); Assert.Equal(1, calls);
    }
    [Fact]
    public void OnlyActualSourcePlusOneUniverseThenPhysicsCanCompleteTheOneFrameLatch()
    {
        var f = new NativeFirstFrameAdmission();
        Assert.Null(f.Arm(100, true, true, true, true));
        Assert.False(f.UniverseObserved); Assert.False(f.PhysicsObserved);
        Assert.Null(f.ObserveUniverse(101, true, true, true));
        Assert.False(f.PhysicsObserved);
        Assert.Null(f.ObservePhysics(101, true)); Assert.True(f.PhysicsObserved);
        Assert.NotNull(f.Arm(101, true, true, true, true));
    }
    [Theory]
    [InlineData("loader")] [InlineData("guide")] [InlineData("foot")] [InlineData("funding")]
    public void UnknownInitialPremiseCannotAllowANativeFrame(string fault)
    {
        var f = new NativeFirstFrameAdmission();
        Assert.NotNull(f.Arm(100, fault != "loader", fault != "guide", fault != "foot", fault != "funding"));
        Assert.False(f.Armed); Assert.NotNull(f.Arm(100, true, true, true, true));
    }
    [Theory]
    [InlineData("wrong_tick")] [InlineData("world_session_manager_or_order")] [InlineData("pose")] [InlineData("terrain")]
    [InlineData("duplicate151")] [InlineData("501_before151")] [InlineData("duplicate501")] [InlineData("host_teardown")]
    public void LostOrRepeatedRealStageStaysFailedAndCannotCreateASecondFrame(string fault)
    {
        var f = new NativeFirstFrameAdmission(); Assert.Null(f.Arm(100, true, true, true, true));
        if (fault == "host_teardown") f.Fail("remote_resume_first_frame_native_observer_lost");
        else if (fault == "501_before151") Assert.NotNull(f.ObservePhysics(101, true));
        else
        {
            var blocker = f.ObserveUniverse(fault == "wrong_tick" ? 102 : 101,
                fault != "world_session_manager_or_order", fault != "pose", fault != "terrain");
            if (fault == "duplicate151") blocker = f.ObserveUniverse(101, true, true, true);
            if (fault == "duplicate501") { Assert.Null(blocker); Assert.Null(f.ObservePhysics(101, true)); blocker = f.ObservePhysics(101, true); }
            Assert.NotNull(blocker);
        }
        Assert.NotNull(f.ObservePhysics(101, true)); Assert.NotNull(f.Arm(101, true, true, true, true));
    }
    [Theory]
    [InlineData("disabled")] [InlineData("inactive")] [InlineData("other_layer")] [InlineData("mixed_layer")]
    [InlineData("foreign")] [InlineData("trigger")] [InlineData("scene")] [InlineData("mesh_missing")]
    [InlineData("truncated")] [InlineData("overflow")] [InlineData("no_hit")] [InlineData("unknown")]
    [InlineData("nonfinite")] [InlineData("zero_scale")] [InlineData("missing_guide")] [InlineData("no_ground_observation")]
    public void FootEvidenceMustBeCompleteEnabledOwnedNontriggerThirtyOneMeshGeometry(string fault)
    {
        var s = Terrain(); var c = s.SurfaceColliders![0];
        switch (fault)
        {
            case "disabled": c.Enabled = false; break;
            case "inactive": c.ActiveInHierarchy = false; break;
            case "other_layer": c.Layer = 9; break;
            case "mixed_layer": s.SurfaceColliders.Add(new() { Layer = 9 }); break;
            case "foreign": s.FootOverlapsOwnedSurfaceOnly = false; break;
            case "trigger": c.IsTrigger = true; break;
            case "scene": c.SameDefaultPhysicsScene = false; break;
            case "mesh_missing": c.SharedMeshPresent = null; break;
            case "truncated": s.Truncated = true; break;
            case "overflow": s.FootOverlapCount = 257; s.FootOverlapComplete = false; break;
            case "no_hit": s.FootOverlapCount = 0; break;
            case "unknown": s.State = "unavailable"; break;
            case "nonfinite": c.WorldPosition!.X = float.NaN; break;
            case "zero_scale": c.WorldLossyScale!.Y = 0; break;
            case "missing_guide": s.GuideComplete = null; break;
            case "no_ground_observation": s.NativeGroundSphereHit = null; break;
        }
        Assert.NotNull(NativeTerrainObservationPolicy.FootBlocker(s));
    }
    [Fact]
    public void CopiedDetailDoesNotEnterEitherExistingPlayerHashAndDoesNotShareMutableLists()
    {
        var p = new PlayerStateSnapshot(); var hash = CanonicalStateHash.Player(p); var actionHash = CanonicalStateHash.PlayerAction(p);
        var terrain = Terrain(); Assert.Null(NativeTerrainObservationPolicy.FootBlocker(terrain));
        var copy = terrain.Copy(); copy.SurfaceColliders![0].WorldPosition!.X = 19;
        Assert.Equal(0, terrain.SurfaceColliders![0].WorldPosition!.X);
        p.NativePhysics = new() { Terrain = terrain, ResumeTerrainPreFirstFrame = copy,
            ResumeTerrainPostUniverse = copy.Copy(), ResumeGroundContactPostUniverse = new() { CheckSphereHit = true } };
        Assert.Equal(hash, CanonicalStateHash.Player(p)); Assert.Equal(actionHash, CanonicalStateHash.PlayerAction(p));
        Assert.False(terrain.SafetyCertified);
    }
    [Theory]
    [InlineData("core")] [InlineData("loaded_return")] [InlineData("tick_remaining")] [InlineData("charge_ticks")]
    [InlineData("charge_wall")] [InlineData("utc")]
    public void OriginalTwoTickFundingAndFiniteCapsRemainRequiredBeforeFirstFrame(string fault)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.NotNull(VoyagerRecoveryWindowPolicy.ChargeBlocker(fault == "core" ? 19 : 20, 1000,
            fault == "loaded_return" ? 119 : 120, 100, 10, 80, fault == "tick_remaining" ? 60000 : 10,
            1, 108000, 54000, fault == "charge_ticks" ? 18001 : 0, 18000,
            fault == "charge_wall" ? 301 : 0, 300, now, fault == "utc" ? now : now.AddHours(1)));
    }
    [Fact]
    public void TwoTickFundingDoesNotTurnIntoThirteenTickIdleCoreOrAnEnergyDebit()
    {
        var now = DateTimeOffset.UtcNow; var core = 20d;
        Assert.Null(VoyagerRecoveryWindowPolicy.ChargeBlocker(core, 1000, 120, 100, 10, 80,
            10, 1, 108000, 54000, 0, 18000, 0, 300, now, now.AddHours(1)));
        Assert.True(core < 13 * 10); Assert.Equal(20d, core);
    }
    [Fact]
    public void FailedAfterFirstPhysicsCannotRecoverOnALaterTickOrRearm()
    {
        var f = new NativeFirstFrameAdmission(); Assert.Null(f.Arm(100, true, true, true, true));
        Assert.Null(f.ObserveUniverse(101, true, true, true)); Assert.Null(f.ObservePhysics(101, true));
        var failure = f.Fail("remote_resume_first_frame_native_observer_lost");
        Assert.Equal(failure, f.ObserveUniverse(102, true, true, true));
        Assert.Equal(failure, f.ObservePhysics(102, true)); Assert.Equal(failure, f.Arm(102, true, true, true, true));
        Assert.Equal(failure, f.Failure);
    }
    [Theory]
    [InlineData("wrong_tick")] [InlineData("missing151")] [InlineData("binding")]
    public void RetainedStateCallbackCannotRunBeforeTheExactReal501Boundary(string fault)
    {
        var calls = 0; var f = new NativeFirstFrameAdmission();
        Assert.Null(f.Arm(100, true, true, true, true, () => { calls++; return null; }));
        if (fault != "missing151") Assert.Null(f.ObserveUniverse(101, true, true, true));
        Assert.NotNull(f.ObservePhysics(fault == "wrong_tick" ? 102 : 101, fault != "binding"));
        Assert.Equal(0, calls); Assert.False(f.PhysicsObserved);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RecheckFailureOrExceptionIsStickyAndNeverPublishesPhysicsObservation(bool throws)
    {
        var calls = 0; var f = new NativeFirstFrameAdmission();
        Assert.Null(f.Arm(100, true, true, true, true, () =>
        { calls++; if (throws) throw new InvalidDataException(); return "synthetic_retained_binding_lost"; }));
        Assert.Null(f.ObserveUniverse(101, true, true, true));
        var failure = f.ObservePhysics(101, true);
        Assert.NotNull(failure); Assert.False(f.PhysicsObserved); Assert.Equal(1, calls);
        Assert.Equal(failure, f.ObservePhysics(102, true)); Assert.Equal(failure, f.ObserveUniverse(102, true, true, true));
        Assert.Equal(1, calls);
    }
    [Fact]
    public void SuccessfulCallbackRunsOnceAndCannotBeReplacedOrReviveAStoppedAdoption()
    {
        var calls = 0; var f = new NativeFirstFrameAdmission();
        Assert.Null(f.Arm(100, true, true, true, true, () => { calls++; return null; }));
        Assert.Null(f.ObserveUniverse(101, true, true, true)); Assert.Null(f.ObservePhysics(101, true));
        Assert.True(f.PhysicsObserved); Assert.Equal(1, calls);
        var failure = f.Fail("synthetic_observer_lost");
        Assert.Equal(failure, f.Arm(102, true, true, true, true, () => throw new Exception()));
        Assert.Equal(failure, f.ObservePhysics(102, true)); Assert.Equal(1, calls);
        var armed = new NativeFirstFrameAdmission();
        Assert.Null(armed.Arm(100, true, true, true, true, () => { calls++; return null; }));
        Assert.NotNull(armed.Arm(100, true, true, true, true, () => throw new Exception()));
        Assert.NotNull(armed.ObserveUniverse(101, true, true, true)); Assert.Equal(1, calls);
    }
    private static NativePlanetTerrainPhysicsSnapshot Terrain() => new()
    {
        State = "observed", Complete = true, GuideComplete = true, RootLayer = 31, BodyLayer = 31,
        RootWorldPosition = new(), RootWorldRotation = Identity(), RootWorldLossyScale = One(),
        BodyWorldPosition = new(), BodyWorldRotation = Identity(), BodyWorldLossyScale = One(),
        NativeGroundSphereHit = false, FootOverlapComplete = true, FootOverlapsOwnedSurfaceOnly = true, FootOverlapCount = 1,
        SurfaceColliders = new() { new() { Type = "mesh", Enabled = true, ActiveInHierarchy = true, Layer = 31,
            IsTrigger = false, SharedMeshPresent = true, SameDefaultPhysicsScene = true,
            WorldPosition = new(), WorldRotation = Identity(), WorldLossyScale = One() } },
    };
    private static Vector3Snapshot One() => new() { X = 1, Y = 1, Z = 1 };
    private static QuaternionSnapshot Identity() => new() { W = 1 };
}
