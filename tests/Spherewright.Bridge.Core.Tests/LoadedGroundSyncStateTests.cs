using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using UnityEngine;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

[Collection("Owned resume native substitutes")]
public sealed class LoadedGroundSyncStateTests
{
    [Theory]
    [InlineData("reference")] [InlineData("controller")] [InlineData("transform")]
    [InlineData("parent")] [InlineData("rigidbody")] [InlineData("tick")]
    [InlineData("local_position")] [InlineData("local_rotation")] [InlineData("scale")]
    [InlineData("universal_position")] [InlineData("universal_rotation")]
    [InlineData("body_velocity")] [InlineData("body_angular_velocity")] [InlineData("controller_velocity")]
    [InlineData("input0")] [InlineData("input1")] [InlineData("current_order")]
    [InlineData("order_queue")] [InlineData("raw_input")]
    [InlineData("core_energy")] [InlineData("reactor_energy")] [InlineData("loaded_energy")]
    [InlineData("speed")] [InlineData("vertical_speed")] [InlineData("movement_state")]
    [InlineData("frame_movement_state")] [InlineData("warp_state")] [InlineData("warp_command")]
    [InlineData("pause_state")] [InlineData("physics_auto_sync")] [InlineData("physics_trigger_query")]
    public void EveryActualRetainedPredicateRejectsItsChangedField(string field)
    {
        var p = Reset(); var state = new NormalGameActionCoordinator.GroundStateProbe(p);
        Assert.True(state.Finite); Assert.Null(state.Blocker(p, 100));
        switch (field)
        {
            case "reference": p = new(); break;
            case "controller": p.controller = new(); break;
            case "transform": p.transform = new(); break;
            case "parent": p.transform.parent = new(); break;
            case "rigidbody": p.controller.rigid = new(); break;
            case "tick": GameMain.gameTick++; break;
            case "local_position": p.position = new(1, 200, 0); break;
            case "local_rotation": p.transform.localRotation = new(0, 1, 0, 0); break;
            case "scale": p.transform.lossyScale = new(2, 2, 2); break;
            case "universal_position": p.uPosition = p.uPosition with { z = 3001 }; break;
            case "universal_rotation": p.uRotation = new(0, 1, 0, 0); break;
            case "body_velocity": p.controller.rigid.velocity = new(0, -.01f, 0); break;
            case "body_angular_velocity": p.controller.rigid.angularVelocity = new(1, 0, 0); break;
            case "controller_velocity": p.controller.velocity = new(1, 0, 0); break;
            case "input0": p.controller.input0 = new(1, 0, 0, 0); break;
            case "input1": p.controller.input1 = new(0, 0, 0, 1); break;
            case "current_order": p.currentOrder = new(); break;
            case "order_queue": p.orders.orderCount = 1; break;
            case "raw_input": NormalGameActionCoordinator.GroundTestRawInputIdle = false; break;
            case "core_energy": p.mecha.coreEnergy--; break;
            case "reactor_energy": p.mecha.reactorEnergy--; break;
            case "loaded_energy": p.mecha.LoadedEnergy--; break;
            case "speed": p.speed = 1; break;
            case "vertical_speed": p.controller.vertSpeed = 1; break;
            case "movement_state": p.movementState = EMovementState.Drift; break;
            case "frame_movement_state": p.controller.movementStateInFrame = EMovementState.Drift; break;
            case "warp_state": p.warpState = 1; break;
            case "warp_command": p.warpCommand = true; break;
            case "pause_state": GameMain.isPaused = true; break;
            case "physics_auto_sync": Physics.autoSyncTransforms = true; break;
            case "physics_trigger_query": Physics.queriesHitTriggers = true; break;
        }
        var expected = field switch
        {
            "reference" or "controller" or "transform" or "parent" or "rigidbody" or "tick" => "player_" + field + "_changed",
            "current_order" or "order_queue" => "player_" + field + "_present",
            "raw_input" => "player_raw_input_active",
            _ => "player_" + field + "_changed",
        };
        Assert.Equal(expected, state.Blocker(p, 100)); Assert.False(state.Matches(p));
    }

    [Theory]
    [InlineData("position")] [InlineData("rotation")] [InlineData("energy")] [InlineData("scale")]
    public void InvalidInitialSnapshotCannotBeRepairedByLaterValues(string field)
    {
        var p = Reset();
        if (field == "position") p.uPosition = new(double.NaN, 0, 0);
        if (field == "rotation") p.uRotation = default;
        if (field == "energy") p.mecha.LoadedEnergy = double.PositiveInfinity;
        if (field == "scale") p.transform.lossyScale = new(1, 0, 1);
        var state = new NormalGameActionCoordinator.GroundStateProbe(p);
        Assert.False(state.Finite);
        p.uPosition = new(1000, 2000, 3000); p.uRotation = new(0, 0, 0, 1);
        p.mecha.LoadedEnergy = 500; p.transform.lossyScale = new(1, 1, 1);
        Assert.Equal("player_initial_state_nonfinite", state.Blocker(p, 100));
    }

    [Fact]
    public void FirstFailureShortCircuitsLaterEnergyReadsAndDoesNotRebind()
    {
        var p = Reset(); var state = new NormalGameActionCoordinator.GroundStateProbe(p);
        p.position = new(1, 200, 0); p.mecha.coreEnergy--;
        p.mecha.LoadedReads = 0;
        Assert.Equal("player_local_position_changed", state.Blocker(p, 100));
        Assert.Equal(0, p.mecha.LoadedReads);
        p.position = new(0, 200, 0);
        Assert.Equal("player_core_energy_changed", state.Blocker(p, 100));
        Assert.Equal(0, p.mecha.LoadedReads);
        p.mecha.coreEnergy++;
        Assert.Null(state.Blocker(p, 100)); Assert.Equal(1, p.mecha.LoadedReads);
    }

    [Fact]
    public void NativePauseHoldExpectsPauseWithoutAcceptingActorDrift()
    {
        var p = Reset(); var state = new NormalGameActionCoordinator.GroundStateProbe(p);
        Assert.False(state.DuringPause(p));
        GameMain.isPaused = true;
        Assert.True(state.DuringPause(p)); Assert.False(state.Matches(p));
        p.controller.rigid.velocity = new(0, -1, 0);
        Assert.False(state.DuringPause(p));
        var capturedWhilePaused = new NormalGameActionCoordinator.GroundStateProbe(p);
        Assert.False(capturedWhilePaused.DuringPause(p));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PossiblePre151NativeWritesRemainRejectedAndPreciselyAttributed(bool restoredBodyVelocity)
    {
        var p = Reset(); var state = new NormalGameActionCoordinator.GroundStateProbe(p);
        var frame = new NativeFirstFrameAdmission();
        Assert.Null(frame.Arm(100, true, true, true, true));
        // Model the audited possible assignment, not an observed Native cause.
        if (restoredBodyVelocity) p.controller.rigid.velocity = new(0, -.001f, 0);
        else p.uPosition = p.uPosition with { x = 1000.000001 };
        GameMain.gameTick = 101;
        var detail = state.Blocker(p, 101); var reads = p.mecha.LoadedReads;
        Assert.Equal("remote_resume_first_frame_universe_unproved",
            frame.ObserveUniverse(101, true, detail is null, true, null, detail, null));
        Assert.Equal(restoredBodyVelocity ? "player_body_velocity_changed" : "player_universal_position_changed", frame.FailureDetail);
        Assert.Equal(reads, p.mecha.LoadedReads);
        Assert.False(frame.UniverseObserved); Assert.False(frame.PhysicsObserved);
        frame.Fail("later_cancel", "later_detail");
        Assert.NotNull(frame.ObservePhysics(101, true));
        Assert.Equal(detail, frame.FailureDetail);
    }

    [Fact]
    public void SameProductionSnapshotCanReach151Then501OnlyWithBothExactComparisons()
    {
        var p = Reset(); var state = new NormalGameActionCoordinator.GroundStateProbe(p);
        var calls = 0; var frame = new NativeFirstFrameAdmission();
        Assert.Null(frame.Arm(100, true, true, true, true, () => { calls++; return state.Blocker(p, 101); }));
        GameMain.gameTick = 101;
        var detail = state.Blocker(p, 101);
        Assert.Null(frame.ObserveUniverse(101, true, detail is null, true, null, detail, null));
        Assert.False(frame.PhysicsObserved); Assert.Equal(0, calls);
        Assert.Null(frame.ObservePhysics(101, true)); Assert.Equal(1, calls);
        Assert.True(frame.PhysicsObserved); Assert.Null(frame.FailureDetail);
        // This latch alone still has no idle owner, save or ticket capability.
    }

    private static Player Reset()
    {
        GameMain.gameTick = 100; GameMain.isPaused = false;
        Physics.autoSyncTransforms = false; Physics.queriesHitTriggers = false;
        NormalGameActionCoordinator.GroundTestRawInputIdle = true;
        return new();
    }
}
