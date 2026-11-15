using System.Globalization;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Celestial;

// A new ordinary swarm orbit is a design change, not a sail, launch or power receipt.
public static class DysonSwarmOrbitPolicy
{
    public const int MaximumOrdinaryOrbitId = 20;
    public const int ReservedNativeOrbitId = 30;

    public static string? ValidateRequest(PrepareConfigureBuildingRequest request)
    {
        if (request.Mode != BuildingConfigurationModes.DysonSwarmOrbit || request.EntityId != 0
            || request.RecipeId != 0 || request.TechId != 0 || request.FilterItemId != 0
            || request.DysonStructure is not null || request.EjectorOrbitId != -1
            || !string.IsNullOrEmpty(request.StorageOperation) || request.StorageBannedGridCount != -1
            || request.StationStorageIndex != -1 || request.StationBeltSlotIndex != -1 || request.StationBeltStorageIndex != -1
            || request.StationItemId != 0 || request.StationMaximumCount != 0 || request.StationMaximumChargePowerWatts != 0
            || request.StationVesselDeliveryMinimumPercent != -1 || request.StationLocalLogic != LogisticsStorageLogics.None
            || request.StationRemoteLogic != LogisticsStorageLogics.None || !string.IsNullOrEmpty(request.ExpectedStationConfigurationStateHash))
            return "dyson_swarm_orbit_exclusive_parameters_required";
        if (request.PlanetId <= 0 || request.StateHashVersion != CanonicalStateHash.Version
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(request.ExpectedFactoryStateHash))
            return "dyson_swarm_orbit_current_hash_required";
        var o = request.DysonSwarmOrbit;
        return o is not null && Finite(o.Radius) && o.Radius >= 1f && o.Radius <= 1_000_000_000f
            && Finite(o.InclinationDegrees) && o.InclinationDegrees >= 0f && o.InclinationDegrees <= 180f
            && Finite(o.LongitudeDegrees) && o.LongitudeDegrees >= 0f && o.LongitudeDegrees < 360f
            ? null : "dyson_swarm_orbit_finite_radius_and_angles_required";
    }

    public static bool Complete(string sessionId, int planetId, int starId, DysonSwarmOrbitState? state)
    {
        if (state is null) return false;
        state.StateHash = string.Empty;
        if (string.IsNullOrWhiteSpace(sessionId) || planetId <= 0 || starId <= 0 || !state.NativeBindingVerified
            || !state.CoverageComplete || state.Cursor < 1 || state.Cursor > MaximumOrdinaryOrbitId + 1
            || state.Capacity < 32 || state.Capacity > DysonObservationPolicy.MaximumPoolSlots || state.Capacity < state.Cursor
            || !Finite(state.MinimumRadius) || state.MinimumRadius <= 0 || !Finite(state.MaximumRadius)
            || state.MaximumRadius < state.MinimumRadius || !Finite(state.AvoidRadius) || state.AvoidRadius < 0
            || state.ActiveSailCount < 0 || state.InFlightSailCount < 0 || state.Orbits is null
            || state.Orbits.Count > MaximumOrdinaryOrbitId || state.ReservedOrbit is not { } reserved
            || reserved.OrbitId != ReservedNativeOrbitId || !ValidOrbit(reserved)
            || state.Orbits.Any(o => o is null || o.OrbitId < 1 || o.OrbitId >= state.Cursor || !ValidOrbit(o))
            || state.Orbits.Select(o => o.OrbitId).Distinct().Count() != state.Orbits.Count
            || (long)reserved.SailCount + state.Orbits.Sum(o => (long)o.SailCount) > state.ActiveSailCount)
        { state.CoverageComplete = false; return false; }
        var fields = new List<object?> { sessionId, planetId, starId, state.Cursor, state.Capacity,
            F(state.MinimumRadius), F(state.MaximumRadius), F(state.AvoidRadius), state.ActiveSailCount, state.InFlightSailCount };
        AppendOrbit(fields, reserved);
        foreach (var orbit in state.Orbits.OrderBy(o => o.OrbitId)) AppendOrbit(fields, orbit);
        state.StateHash = CanonicalStateHash.Combine("native-swarm-orbit-state-v1", fields.ToArray());
        return true;
    }

    public static int NextOrbitId(DysonSwarmOrbitState? state)
    {
        if (state is not { CoverageComplete: true, NativeBindingVerified: true } || state.Orbits is null
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(state.StateHash) || state.Cursor < 1
            || state.Cursor > MaximumOrdinaryOrbitId + 1 || state.Orbits.Any(o => o is null || o.OrbitId < 1 || o.OrbitId >= state.Cursor)
            || state.Orbits.Select(o => o.OrbitId).Distinct().Count() != state.Orbits.Count) return 0;
        var ids = new HashSet<int>(state.Orbits.Select(o => o.OrbitId));
        for (var id = 1; id < state.Cursor; id++) if (!ids.Contains(id)) return id;
        return state.Cursor <= MaximumOrdinaryOrbitId ? state.Cursor : 0;
    }

    public static string BindingHash(DysonSwarmOrbitPlan plan, DysonSwarmOrbitRequest? requested = null)
    {
        var o = requested ?? plan.Orbit; var q = plan.NativeRotation; var up = plan.NativeUp;
        return CanonicalStateHash.Combine("native-swarm-orbit-create-v1", plan.EvidenceVersion, plan.SessionId,
            plan.PlanetId, plan.StarId, plan.BeforeStateHash, plan.PlannedOrbitId, F(plan.NativeRadius),
            F(q.X), F(q.Y), F(q.Z), F(q.W), F(up.X), F(up.Y), F(up.Z), plan.ExistingActiveSails, plan.ExistingInFlightSails,
            plan.NativeChecksComplete, F(o.Radius), F(o.InclinationDegrees), F(o.LongitudeDegrees), o.PreviewOnly);
    }

    public static bool ConfirmsPreparedEcho(string sessionId, PrepareConfigureBuildingRequest request, PreparedNormalAction? prepared)
    {
        if (ValidateRequest(request) is not null || prepared?.PlannedDysonSwarmOrbit is not { } plan
            || plan.Orbit is null || plan.NativeRotation is null || plan.EvidenceVersion != 1 || plan.SessionId != sessionId
            || plan.PlanetId != request.PlanetId || plan.StarId <= 0 || plan.PlannedOrbitId < 1
            || plan.PlannedOrbitId > MaximumOrdinaryOrbitId || !Finite(plan.NativeRadius) || plan.NativeRadius <= 0
            || !RotationMatchesRequest(plan.Orbit, plan.NativeRotation) || !RotationMatchesUp(plan.NativeRotation, plan.NativeUp)
            || plan.ExistingActiveSails < 0 || plan.ExistingInFlightSails < 0
            || !plan.NativeChecksComplete || plan.BeforeStateHash != request.ExpectedFactoryStateHash
            || plan.BindingHash != BindingHash(plan) || plan.BindingHash != BindingHash(plan, request.DysonSwarmOrbit)
            || prepared.ExpectedStateHash != plan.BindingHash || prepared.ActionKind != NormalActionKinds.ConfigureBuilding
            || prepared.CommitBlockers is null || prepared.CommitBlockers.Count != 0) return false;
        return plan.Orbit.PreviewOnly
            ? !prepared.Prepared && !prepared.CommitAllowedNow && string.IsNullOrEmpty(prepared.PlanToken)
            : prepared.Prepared && prepared.CommitAllowedNow && !string.IsNullOrEmpty(prepared.PlanToken);
    }

    public static bool SameOrbit(SwarmOrbitSnapshot a, SwarmOrbitSnapshot b) =>
        a is not null && b is not null && a.OrbitId == b.OrbitId && a.Enabled == b.Enabled && a.SailCount == b.SailCount
        && a.Radius.Equals(b.Radius) && a.Rotation is { } x && b.Rotation is { } y
        && x.X.Equals(y.X) && x.Y.Equals(y.Y) && x.Z.Equals(y.Z) && x.W.Equals(y.W)
        && a.Up is { } au && b.Up is { } bu && au.X.Equals(bu.X) && au.Y.Equals(bu.Y) && au.Z.Equals(bu.Z);

    public static bool MatchesNewOrbit(SwarmOrbitSnapshot? created, DysonSwarmOrbitPlan plan)
    {
        var q = plan.NativeRotation;
        return created is not null && q is not null && created.OrbitId == plan.PlannedOrbitId && created.Enabled
            && created.SailCount == 0 && created.Radius.Equals(plan.NativeRadius) && created.Rotation is { } r
            && r.X.Equals(q.X) && r.Y.Equals(q.Y) && r.Z.Equals(q.Z) && r.W.Equals(q.W)
            && created.Up is { } up && plan.NativeUp is { } expected
            && up.X.Equals(expected.X) && up.Y.Equals(expected.Y) && up.Z.Equals(expected.Z);
    }

    public static bool ExistingOrbitsAndSailCountsPreserved(DysonSwarmOrbitState before, DysonSwarmOrbitState after, int newId)
    {
        if (!before.CoverageComplete || !after.CoverageComplete || !before.NativeBindingVerified || !after.NativeBindingVerified
            || before.Capacity != after.Capacity || newId != NextOrbitId(before)
            || after.Cursor != Math.Max(before.Cursor, newId + 1) || before.MinimumRadius != after.MinimumRadius
            || before.MaximumRadius != after.MaximumRadius || before.AvoidRadius != after.AvoidRadius
            || before.ActiveSailCount != after.ActiveSailCount || before.InFlightSailCount != after.InFlightSailCount
            || before.ReservedOrbit is null || after.ReservedOrbit is null
            || !SameOrbit(before.ReservedOrbit, after.ReservedOrbit) || before.Orbits is null || after.Orbits is null
            || after.Orbits.Count != before.Orbits.Count + 1
            || after.Orbits.Count(o => o.OrbitId == newId) != 1) return false;
        foreach (var old in before.Orbits)
        {
            var matches = after.Orbits.Where(o => o.OrbitId == old.OrbitId).ToArray();
            if (matches.Length != 1 || !SameOrbit(old, matches[0])) return false;
        }
        return true;
    }

    public static bool ValidRotation(QuaternionSnapshot? q)
    {
        if (q is null || !Finite(q.X) || !Finite(q.Y) || !Finite(q.Z) || !Finite(q.W)) return false;
        var squared = (double)q.X * q.X + (double)q.Y * q.Y + (double)q.Z * q.Z + (double)q.W * q.W;
        return Math.Abs(squared - 1d) <= .001d;
    }
    public static bool RotationMatchesUp(QuaternionSnapshot? q, Vector3Snapshot? up)
    {
        if (!ValidRotation(q) || !ValidUp(up)) return false;
        var x = (double)q!.X; var y = (double)q.Y; var z = (double)q.Z; var w = (double)q.W;
        return Math.Abs(up!.X - 2d * (x * y - w * z)) <= .0001d
            && Math.Abs(up.Y - (1d - 2d * (x * x + z * z))) <= .0001d
            && Math.Abs(up.Z - 2d * (y * z + w * x)) <= .0001d;
    }
    // Native editor uses Quaternion.Euler(0, -longitude, -inclination).
    // Unity applies Z then X then Y; X is zero, so this is qY * qZ.
    public static bool RotationMatchesRequest(DysonSwarmOrbitRequest? request, QuaternionSnapshot? q)
    {
        if (request is null || !Finite(request.InclinationDegrees) || !Finite(request.LongitudeDegrees)
            || request.InclinationDegrees < 0 || request.InclinationDegrees > 180
            || request.LongitudeDegrees < 0 || request.LongitudeDegrees >= 360 || !ValidRotation(q)) return false;
        var yaw = -request.LongitudeDegrees * (Math.PI / 360d);
        var roll = -request.InclinationDegrees * (Math.PI / 360d);
        var sy = Math.Sin(yaw); var cy = Math.Cos(yaw); var sz = Math.Sin(roll); var cz = Math.Cos(roll);
        var expected = new[] { sy * sz, sy * cz, cy * sz, cy * cz };
        var actual = new[] { (double)q!.X, q.Y, q.Z, q.W };
        // q and -q represent the same rotation. Account for Native float trig.
        return expected.Zip(actual, (a, b) => Math.Abs(a - b)).Max() <= .0001d
            || expected.Zip(actual, (a, b) => Math.Abs(a + b)).Max() <= .0001d;
    }
    private static bool ValidOrbit(SwarmOrbitSnapshot o) => o.SailCount >= 0 && Finite(o.Radius) && o.Radius > 0
        && RotationMatchesUp(o.Rotation, o.Up);
    private static bool ValidUp(Vector3Snapshot? p)
    {
        if (p is null || !Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) return false;
        var squared = (double)p.X * p.X + (double)p.Y * p.Y + (double)p.Z * p.Z;
        return Math.Abs(squared - 1d) <= .001d;
    }
    private static void AppendOrbit(List<object?> fields, SwarmOrbitSnapshot o) => fields.AddRange(new object?[]
    { o.OrbitId, o.Enabled, o.SailCount, F(o.Radius), F(o.Rotation.X), F(o.Rotation.Y), F(o.Rotation.Z), F(o.Rotation.W),
      F(o.Up!.X), F(o.Up.Y), F(o.Up.Z) });
    private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    // Bind IEEE bits instead of runtime-specific shortest float formatting.
    // Signed zero is physically identical and may be normalized by JSON.
    private static string F(float x) => (x == 0f ? 0 : BitConverter.ToInt32(BitConverter.GetBytes(x), 0))
        .ToString(CultureInfo.InvariantCulture);
}
