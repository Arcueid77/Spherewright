using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class DysonSwarmOrbitPolicyTests
{
    [Fact]
    public void CompleteObservationBindsAllSlotsAndReusesTheFirstHole()
    {
        var state = State();
        Assert.True(DysonSwarmOrbitPolicy.Complete("session", 7, 2, state));
        Assert.Equal(2, DysonSwarmOrbitPolicy.NextOrbitId(state));
        var original = state.StateHash;
        state.Orbits.Reverse();
        Assert.True(DysonSwarmOrbitPolicy.Complete("session", 7, 2, state));
        Assert.Equal(original, state.StateHash);
        state.Orbits[0].Radius = MathF.BitIncrement(state.Orbits[0].Radius);
        Assert.True(DysonSwarmOrbitPolicy.Complete("session", 7, 2, state));
        Assert.NotEqual(original, state.StateHash);
    }

    [Fact]
    public void NextOrbitHonorsTheNativeTwentySlotLimit()
    {
        var state = State(); state.Cursor = 21; state.Orbits = Enumerable.Range(1, 20).Select(id => Orbit(id)).ToList();
        state.ActiveSailCount = 0; state.ReservedOrbit!.SailCount = 0;
        Assert.True(DysonSwarmOrbitPolicy.Complete("session", 7, 2, state));
        Assert.Equal(0, DysonSwarmOrbitPolicy.NextOrbitId(state));
        state.Orbits.RemoveAt(12);
        Assert.True(DysonSwarmOrbitPolicy.Complete("session", 7, 2, state));
        Assert.Equal(13, DysonSwarmOrbitPolicy.NextOrbitId(state));
    }

    [Theory]
    [InlineData(0f, 0f)] [InlineData(30f, 40f)] [InlineData(90f, 270f)] [InlineData(180f, 359.9f)]
    public void NativeAngleEchoMustMatchTheRequestedRotationIncludingEquivalentQuaternionSign(float inclination, float longitude)
    {
        var request = new DysonSwarmOrbitRequest { Radius = 8000, InclinationDegrees = inclination, LongitudeDegrees = longitude };
        var plan = Plan(request); var q = plan.NativeRotation;
        Assert.True(DysonSwarmOrbitPolicy.RotationMatchesRequest(request, q));
        Assert.True(DysonSwarmOrbitPolicy.RotationMatchesUp(q, plan.NativeUp));
        var negated = new QuaternionSnapshot { X = -q.X, Y = -q.Y, Z = -q.Z, W = -q.W };
        Assert.True(DysonSwarmOrbitPolicy.RotationMatchesRequest(request, negated));
        if (inclination != 0 || longitude != 0)
            Assert.False(DysonSwarmOrbitPolicy.RotationMatchesRequest(request, new QuaternionSnapshot { W = 1 }));
    }

    [Fact]
    public void PreciseFloatPlanSurvivesTheBridgeJsonRoundTrip()
    {
        var request = new DysonSwarmOrbitRequest { Radius = MathF.BitIncrement(8000f),
            InclinationDegrees = 37.12501f, LongitudeDegrees = 217.37502f };
        var plan = Plan(request); var json = Newtonsoft.Json.JsonConvert.SerializeObject(plan);
        var received = Newtonsoft.Json.JsonConvert.DeserializeObject<DysonSwarmOrbitPlan>(json);
        Assert.NotNull(received); Assert.Equal(plan.BindingHash, DysonSwarmOrbitPolicy.BindingHash(received!));
        Assert.True(DysonSwarmOrbitPolicy.RotationMatchesRequest(received!.Orbit, received.NativeRotation));
    }

    [Fact]
    public void KnownThirtyByFortyDegreeOrientationIsNotAnIdentityOrReversedAnglePair()
    {
        // Z(-30 degrees) followed by Y(-40 degrees), with its actual rotated up vector.
        var q = new QuaternionSnapshot { X = .08852133f, Y = -.33036608f, Z = -.24321035f, W = .90767337f };
        var up = new Vector3Snapshot { X = .38302222f, Y = .8660254f, Z = .3213938f };
        Assert.True(DysonSwarmOrbitPolicy.RotationMatchesRequest(new() { InclinationDegrees = 30, LongitudeDegrees = 40 }, q));
        Assert.True(DysonSwarmOrbitPolicy.RotationMatchesUp(q, up));
        Assert.False(DysonSwarmOrbitPolicy.RotationMatchesRequest(new() { InclinationDegrees = 40, LongitudeDegrees = 30 }, q));
    }

    [Theory]
    [InlineData("binding")] [InlineData("incomplete")] [InlineData("cursor")] [InlineData("capacity")]
    [InlineData("radius")] [InlineData("invalid_rotation")] [InlineData("duplicate")] [InlineData("future_slot")]
    [InlineData("reserved")] [InlineData("negative_sails")] [InlineData("more_orbit_sails_than_total")] [InlineData("wrong_up")]
    public void IncompleteOrInconsistentPoolNeverProducesAHash(string fault)
    {
        var s = State();
        switch (fault)
        {
            case "binding": s.NativeBindingVerified = false; break;
            case "incomplete": s.CoverageComplete = false; break;
            case "cursor": s.Cursor = 22; break;
            case "capacity": s.Capacity = 20; break;
            case "radius": s.Orbits[0].Radius = float.NaN; break;
            case "invalid_rotation": s.Orbits[0].Rotation.W = 0; break;
            case "duplicate": s.Orbits.Add(Orbit(1)); break;
            case "future_slot": s.Orbits.Add(Orbit(s.Cursor)); break;
            case "reserved": s.ReservedOrbit!.OrbitId = 1; break;
            case "negative_sails": s.InFlightSailCount = -1; break;
            case "more_orbit_sails_than_total": s.Orbits[0].SailCount = 100; break;
            case "wrong_up": s.Orbits[0].Up = new() { X = 1 }; break;
        }
        Assert.False(DysonSwarmOrbitPolicy.Complete("session", 7, 2, s));
        Assert.False(s.CoverageComplete); Assert.Empty(s.StateHash); Assert.Equal(0, DysonSwarmOrbitPolicy.NextOrbitId(s));
    }

    [Theory]
    [InlineData("valid")] [InlineData("extra_dyson")] [InlineData("extra_ejector")] [InlineData("bad_mode")]
    [InlineData("bad_hash")] [InlineData("bad_radius")] [InlineData("bad_inclination")] [InlineData("bad_longitude")]
    public void OrbitRequestsAreFiniteAndExclusive(string fault)
    {
        var request = Request();
        switch (fault)
        {
            case "extra_dyson": request.DysonStructure = new(); break;
            case "extra_ejector": request.EjectorOrbitId = 1; break;
            case "bad_mode": request.Mode = BuildingConfigurationModes.Production; break;
            case "bad_hash": request.ExpectedFactoryStateHash = "sha256:bad"; break;
            case "bad_radius": request.DysonSwarmOrbit!.Radius = float.NaN; break;
            case "bad_inclination": request.DysonSwarmOrbit!.InclinationDegrees = 181; break;
            case "bad_longitude": request.DysonSwarmOrbit!.LongitudeDegrees = 360; break;
        }
        Assert.Equal(fault == "valid", DysonSwarmOrbitPolicy.ValidateRequest(request) is null);
    }

    [Theory]
    [InlineData("valid")] [InlineData("preview")] [InlineData("wrong_request")] [InlineData("wrong_planet")]
    [InlineData("wrong_hash")] [InlineData("wrong_rotation")] [InlineData("wrong_up")] [InlineData("wrong_orientation")]
    [InlineData("preview_token")] [InlineData("missing_token")]
    public void PreparedEchoBindsExactRequestAndPreviewCannotCommit(string fault)
    {
        var request = Request(); request.DysonSwarmOrbit!.PreviewOnly = fault is "preview" or "preview_token";
        var plan = Plan(request.DysonSwarmOrbit.Copy());
        var prepared = new PreparedNormalAction { Prepared = !plan.Orbit.PreviewOnly, CommitAllowedNow = !plan.Orbit.PreviewOnly,
            PlanToken = plan.Orbit.PreviewOnly ? "" : "token", ActionKind = NormalActionKinds.ConfigureBuilding,
            ExpectedStateHash = plan.BindingHash, PlannedDysonSwarmOrbit = plan };
        switch (fault)
        {
            case "wrong_request": request.DysonSwarmOrbit.Radius++; break;
            case "wrong_planet": plan.PlanetId++; break;
            case "wrong_hash": prepared.ExpectedStateHash = "wrong"; break;
            case "wrong_rotation": plan.NativeRotation.W = 0; break;
            case "wrong_up": plan.NativeUp = new() { X = 1 }; plan.BindingHash = DysonSwarmOrbitPolicy.BindingHash(plan); prepared.ExpectedStateHash = plan.BindingHash; break;
            case "wrong_orientation": plan.NativeRotation = new() { W = 1 }; plan.NativeUp = new() { Y = 1 };
                plan.BindingHash = DysonSwarmOrbitPolicy.BindingHash(plan); prepared.ExpectedStateHash = plan.BindingHash; break;
            case "preview_token": prepared.PlanToken = "token"; break;
            case "missing_token": prepared.PlanToken = ""; break;
        }
        Assert.Equal(fault is "valid" or "preview", DysonSwarmOrbitPolicy.ConfirmsPreparedEcho("session", request, prepared));
    }

    [Theory]
    [InlineData("valid")] [InlineData("wrong_id")] [InlineData("wrong_radius")] [InlineData("nonempty")]
    [InlineData("disabled")] [InlineData("old_changed")] [InlineData("reserved_changed")]
    [InlineData("sails_changed")] [InlineData("flights_changed")] [InlineData("cursor_changed")]
    public void ImmediateReadbackRequiresTheExactEmptyNewOrbitAndPreservedSwarm(string fault)
    {
        var before = State(); Assert.True(DysonSwarmOrbitPolicy.Complete("session", 7, 2, before));
        var after = State(); var added = Orbit(2); after.Orbits.Add(added);
        Assert.True(DysonSwarmOrbitPolicy.Complete("session", 7, 2, after));
        var plan = Plan(Request().DysonSwarmOrbit!); plan.PlannedOrbitId = 2; plan.NativeRadius = added.Radius;
        plan.NativeRotation = added.Rotation; plan.NativeUp = added.Up!;
        switch (fault)
        {
            case "wrong_id": added.OrbitId = 4; break;
            case "wrong_radius": added.Radius++; break;
            case "nonempty": added.SailCount++; break;
            case "disabled": added.Enabled = false; break;
            case "old_changed": after.Orbits[0].Radius++; break;
            case "reserved_changed": after.ReservedOrbit!.SailCount++; break;
            case "sails_changed": after.ActiveSailCount++; break;
            case "flights_changed": after.InFlightSailCount++; break;
            case "cursor_changed": after.Cursor++; break;
        }
        var proved = DysonSwarmOrbitPolicy.MatchesNewOrbit(added, plan)
            && DysonSwarmOrbitPolicy.ExistingOrbitsAndSailCountsPreserved(before, after, 2);
        Assert.Equal(fault == "valid", proved);
    }

    private static DysonSwarmOrbitState State() => new()
    {
        NativeBindingVerified = true, CoverageComplete = true, Cursor = 4, Capacity = 32,
        MinimumRadius = 1000, MaximumRadius = 20000, AvoidRadius = 5000,
        ActiveSailCount = 4, InFlightSailCount = 2, ReservedOrbit = Orbit(30),
        Orbits = new() { Orbit(1, 2), Orbit(3, 1) },
    };
    private static SwarmOrbitSnapshot Orbit(int id, int sails = 0) => new()
    { OrbitId = id, Enabled = true, Radius = id == 30 ? 1 : 8000, SailCount = sails, Rotation = new() { W = 1 }, Up = new() { Y = 1 } };
    private static PrepareConfigureBuildingRequest Request() => new()
    {
        PlanetId = 7, Mode = BuildingConfigurationModes.DysonSwarmOrbit,
        ExpectedFactoryStateHash = "sha256:" + new string('a', 64),
        DysonSwarmOrbit = new() { Radius = 8000, InclinationDegrees = 30, LongitudeDegrees = 40 },
    };
    private static DysonSwarmOrbitPlan Plan(DysonSwarmOrbitRequest request)
    {
        var yaw = -request.LongitudeDegrees * Math.PI / 360d; var roll = -request.InclinationDegrees * Math.PI / 360d;
        var sy = Math.Sin(yaw); var cy = Math.Cos(yaw); var sz = Math.Sin(roll); var cz = Math.Cos(roll);
        var q = new QuaternionSnapshot { X = (float)(sy * sz), Y = (float)(sy * cz), Z = (float)(cy * sz), W = (float)(cy * cz) };
        var up = new Vector3Snapshot { X = 2 * (q.X * q.Y - q.W * q.Z), Y = 1 - 2 * (q.X * q.X + q.Z * q.Z), Z = 2 * (q.Y * q.Z + q.W * q.X) };
        var plan = new DysonSwarmOrbitPlan { EvidenceVersion = 1, SessionId = "session", PlanetId = 7, StarId = 2,
            Orbit = request, BeforeStateHash = "sha256:" + new string('a', 64), PlannedOrbitId = 2, NativeRadius = 8000,
            NativeRotation = q, NativeUp = up, ExistingActiveSails = 4, ExistingInFlightSails = 2, NativeChecksComplete = true };
        plan.BindingHash = DysonSwarmOrbitPolicy.BindingHash(plan); return plan;
    }
}
