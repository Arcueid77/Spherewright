using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using UnityEngine;
using Xunit;
using Probe = Spherewright.Plugin.Game.NormalGameActionCoordinator.GroundStateProbe;

namespace Spherewright.Bridge.Core.Tests;

// These tests link the production matcher. The substitutes model the audited
// arithmetic and predicate decisions; they do not execute Unity's frame order.
[Collection("Owned resume native substitutes")]
public sealed class LoadedGroundSyncNativeFrameTests
{
    [Fact]
    public void FloatLocalRoundTripCanDifferWithoutLocalMotionAndReach151Then501()
    {
        var (p, relative, rotation, projected) = RoundTrip();
        var original = p.uPosition; var local = p.position;
        Assert.NotEqual(original, projected);
        var state = new Probe(p, relative, rotation);
        var frame = new NativeFirstFrameAdmission();
        Assert.Null(frame.Arm(100, true, true, true, true, () => state.Physics(p, 101)));
        p.uPosition = projected; GameMain.gameTick = 101;
        Assert.Equal(local, p.position);
        Assert.Equal("player_universal_position_changed", state.Blocker(p, 101));
        Assert.Null(state.Universe(p, 101, out var observed));
        Assert.Equal(projected, observed);
        Assert.Null(frame.ObserveUniverse(101, true, true, true));
        Assert.False(frame.PhysicsObserved);
        Assert.True(state.Pin(observed));
        Assert.Null(frame.ObservePhysics(101, true));
        Assert.True(frame.PhysicsObserved);
        // Pinning the first frame never changes the ordinary strict predicate.
        Assert.Equal("player_universal_position_changed", state.Blocker(p, 101));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PinChoosesOneValidatedEndpointAndPhysicsCannotSwitchToTheOther(bool useProjection)
    {
        var (p, relative, rotation, projected) = RoundTrip();
        var original = p.uPosition; var state = new Probe(p, relative, rotation);
        p.uPosition = useProjection ? projected : original; GameMain.gameTick = 101;
        Assert.Null(state.Universe(p, 101, out var observed));
        Assert.Equal(p.uPosition, observed);
        Assert.True(state.Pin(observed)); Assert.Null(state.Physics(p, 101));
        Assert.False(state.Pin(observed));
        p.uPosition = useProjection ? original : projected;
        Assert.Equal("player_universal_position_changed", state.Physics(p, 101));
    }

    [Fact]
    public void FrozenEndpointUsesNativeMixedFloatAndDoubleQuaternionArithmetic()
    {
        var (p, relative, _, _) = RoundTrip();
        var rotation = new Quaternion(0f, .70710677f, 0f, .70710677f);
        var projected = relative + Maths.QRotateLF(rotation, p.position);
        var state = new Probe(p, relative, rotation);
        p.uPosition = projected; GameMain.gameTick = 101;
        Assert.Null(state.Universe(p, 101, out var observed));
        Assert.Equal(projected, observed); Assert.True(state.Pin(observed));
        Assert.Null(state.Physics(p, 101));
    }

    [Fact]
    public void AnArbitraryThirdEndpointFailsAndCannotBePinned()
    {
        var (p, relative, rotation, _) = RoundTrip(); var state = new Probe(p, relative, rotation);
        p.uPosition = p.uPosition with { x = p.uPosition.x + 1d }; GameMain.gameTick = 101;
        Assert.NotNull(state.Universe(p, 101, out var observed));
        Assert.False(state.Pin(observed)); Assert.NotNull(state.Physics(p, 101));
    }

    [Theory]
    [InlineData("position_nan")] [InlineData("position_infinity")]
    [InlineData("rotation_nan")] [InlineData("rotation_infinity")] [InlineData("rotation_zero")]
    public void AnInvalidNativeFrameNeverProducesAnAdmissibleEndpoint(string field)
    {
        var (p, relative, rotation, _) = RoundTrip();
        if (field == "position_nan") relative = relative with { x = double.NaN };
        if (field == "position_infinity") relative = relative with { y = double.PositiveInfinity };
        if (field == "rotation_nan") rotation = rotation with { x = float.NaN };
        if (field == "rotation_infinity") rotation = rotation with { w = float.PositiveInfinity };
        if (field == "rotation_zero") rotation = default;
        var state = new Probe(p, relative, rotation); GameMain.gameTick = 101;
        Assert.NotNull(state.Universe(p, 101, out var observed)); Assert.False(state.Pin(observed));
    }

    [Theory]
    [InlineData(99L)] [InlineData(100L)] [InlineData(102L)]
    public void OnlySourcePlusOneCanValidateTheFirstUniverseEndpoint(long tick)
    {
        var (p, relative, rotation, projected) = RoundTrip(); var state = new Probe(p, relative, rotation);
        p.uPosition = projected; GameMain.gameTick = tick;
        Assert.NotNull(state.Universe(p, tick, out var observed)); Assert.False(state.Pin(observed));
    }

    [Fact]
    public void AnOrdinarySnapshotHasNoNativeFrameOrImplicitPhysicsPin()
    {
        var (p, _, _, _) = RoundTrip(); var state = new Probe(p); GameMain.gameTick = 101;
        Assert.NotNull(state.Universe(p, 101, out var observed));
        Assert.False(state.Pin(observed)); Assert.NotNull(state.Physics(p, 101));
    }

    [Fact]
    public void AValidatedUniverseCandidateIsNotItselfAPhysicsPin()
    {
        var (p, relative, rotation, projected) = RoundTrip(); var state = new Probe(p, relative, rotation);
        p.uPosition = projected; GameMain.gameTick = 101;
        Assert.False(state.Pin(projected));
        Assert.Null(state.Universe(p, 101, out var observed));
        Assert.NotNull(state.Physics(p, 101));
        Assert.False(state.Pin(observed with { x = observed.x + 1d }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PinMustStillBeAtTheSameUnpausedFirstTick(bool pause)
    {
        var (p, relative, rotation, projected) = RoundTrip(); var state = new Probe(p, relative, rotation);
        p.uPosition = projected; GameMain.gameTick = 101;
        Assert.Null(state.Universe(p, 101, out var observed));
        if (pause) GameMain.isPaused = true; else GameMain.gameTick = 102;
        Assert.False(state.Pin(observed)); Assert.NotNull(state.Physics(p, GameMain.gameTick));
    }

    [Theory]
    [InlineData("local_position")] [InlineData("body_velocity")]
    [InlineData("input0")] [InlineData("core_energy")] [InlineData("loaded_energy")]
    public void AProjectedEndpointDoesNotExcuseOtherChangedStateOrPermitPin(string field)
    {
        var (p, relative, rotation, projected) = RoundTrip(); var state = new Probe(p, relative, rotation);
        p.uPosition = projected; GameMain.gameTick = 101;
        if (field == "local_position") p.position = new(1f, 200f, 0f);
        if (field == "body_velocity") p.controller.rigid.velocity = new(0f, -.001f, 0f);
        if (field == "input0") p.controller.input0 = new(1f, 0f, 0f, 0f);
        if (field == "core_energy") p.mecha.coreEnergy--;
        if (field == "loaded_energy") p.mecha.LoadedEnergy--;
        Assert.Equal("player_" + field + "_changed", state.Universe(p, 101, out var observed));
        Assert.False(state.Pin(observed)); Assert.NotNull(state.Physics(p, 101));
    }

    [Fact]
    public void NativeFrameDoesNotRelaxTheOrdinaryStrictOrPausedHoldComparisons()
    {
        var (p, relative, rotation, projected) = RoundTrip(); var state = new Probe(p, relative, rotation);
        Assert.True(state.Matches(p));
        GameMain.isPaused = true; Assert.True(state.DuringPause(p));
        p.uPosition = projected;
        Assert.False(state.DuringPause(p));
        Assert.Equal("player_universal_position_changed", state.Blocker(p, 100));
        GameMain.isPaused = false; Assert.False(state.Matches(p));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void FailedCore151DecisionDoesNotAutomaticallyPinAPlayerCandidate(bool bindingFailure)
    {
        var (p, relative, rotation, projected) = RoundTrip(); var state = new Probe(p, relative, rotation);
        var physicsCalls = 0; var frame = new NativeFirstFrameAdmission();
        Assert.Null(frame.Arm(100, true, true, true, true, () => { physicsCalls++; return state.Physics(p, 101); }));
        p.uPosition = projected; GameMain.gameTick = 101;
        Assert.Null(state.Universe(p, 101, out _));
        Assert.NotNull(frame.ObserveUniverse(101, !bindingFailure, true, bindingFailure,
            bindingFailure ? "binding_changed" : null, null, bindingFailure ? null : "terrain_changed"));
        // This models the required call ordering, not the unlinked coordinator:
        // the failed Core decision never calls Pin and cannot grant Physics.
        Assert.NotNull(state.Physics(p, 101));
        Assert.NotNull(frame.ObservePhysics(101, true)); Assert.Equal(0, physicsCalls);
    }

    private static (Player Player, VectorLF3 Relative, Quaternion Rotation, VectorLF3 Projected) RoundTrip()
    {
        GameMain.gameTick = 100; GameMain.isPaused = false;
        Physics.autoSyncTransforms = false; Physics.queriesHitTriggers = false;
        NormalGameActionCoordinator.GroundTestRawInputIdle = true;
        var relative = new VectorLF3(1000d, 2000d, 3000d);
        var rotation = new Quaternion(0f, 0f, 0f, 1f);
        const double importedOffset = .1234567890123d;
        var p = new Player
        {
            position = new((float)importedOffset, 200f, 0f),
            uPosition = new(relative.x + importedOffset, relative.y + 200d, relative.z),
        };
        var projected = relative + Maths.QRotateLF(rotation, p.position);
        return (p, relative, rotation, projected);
    }
}
