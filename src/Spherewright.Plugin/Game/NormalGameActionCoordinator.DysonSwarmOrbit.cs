using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareDysonSwarmOrbitOnMainThread(
        string? sessionId, PrepareConfigureBuildingRequest request, SessionState session)
    {
        if (DysonSwarmOrbitPolicy.ValidateRequest(request) is { } invalid) return InvalidPlan(invalid);
        var captured = CaptureDysonSwarmOrbitPlan(sessionId!, request);
        if (!captured.Success || captured.Value is null) return GameCallResult<PreparedNormalAction>.Failed(captured.Error!);
        var binding = captured.Value;
        if (binding.Plan.Orbit.PreviewOnly)
            return GameCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction
            {
                Prepared = false, CommitAllowedNow = false, ActionKind = NormalActionKinds.ConfigureBuilding,
                ExpectedStateHash = binding.Plan.BindingHash, PlannedDysonSwarmOrbit = binding.Plan,
                CompletionCondition = "Read-only Native swarm-orbit allocation and radius preview; no orbit, sail or launch is created.",
            });
        var result = AddPreparedPlan(NormalActionPlanPayload.ForDysonSwarmOrbit(binding), session, 1,
            "One Native NewOrbit allocation with exact orbit, existing orbit/sail-count and package readback; no sail is launched.");
        if (result.Success && result.Value is not null) result.Value.PlannedDysonSwarmOrbit = binding.Plan;
        return result;
    }

    private GameCallResult<NativeDysonSwarmOrbitBinding> CaptureDysonSwarmOrbitPlan(string sessionId, PrepareConfigureBuildingRequest request)
    {
        var observed = _reader.GetDysonStateOnMainThread(sessionId, new GetDysonStateRequest { PlanetId = request.PlanetId, LayerId = 0 });
        if (!observed.Success || observed.Value is null) return GameCallResult<NativeDysonSwarmOrbitBinding>.Failed(observed.Error!);
        var state = observed.Value; var orbits = state.SwarmOrbits; var star = GameMain.localStar;
        var world = GameMain.data; var spheres = world?.dysonSpheres;
        var sphere = star is not null && spheres is not null && star.index >= 0 && star.index < spheres.Length ? spheres[star.index] : null;
        var swarm = sphere?.swarm;
        if (orbits is null || !orbits.CoverageComplete || !orbits.NativeBindingVerified || orbits.StateHash != request.ExpectedFactoryStateHash
            || sphere is null || swarm is null || world is null || !ReferenceEquals(sphere.gameData, world)
            || !ReferenceEquals(sphere.starData, star) || !ReferenceEquals(swarm.dysonSphere, sphere)
            || !ReferenceEquals(swarm.gameData, world) || !ReferenceEquals(swarm.starData, star) || swarm.orbits is null)
            return DysonSwarmOrbitFailure("dyson_swarm_orbit_native_state_or_hash_changed");
        var next = DysonSwarmOrbitPolicy.NextOrbitId(orbits);
        if (next == 0) return DysonSwarmOrbitFailure("dyson_swarm_orbit_native_allocator_full_or_unproved");
        var spec = request.DysonSwarmOrbit!.Copy();
        var radius = spec.Radius;
        sphere.QuerySwarmRadius(ref radius);
        var rotation = Quaternion.Euler(0f, -spec.LongitudeDegrees, -spec.InclinationDegrees);
        var up = rotation * Vector3.up;
        var q = new QuaternionSnapshot { X = rotation.x, Y = rotation.y, Z = rotation.z, W = rotation.w };
        if (!IsFinite(radius) || radius < orbits.MinimumRadius || radius > orbits.MaximumRadius
            || !DysonSwarmOrbitPolicy.ValidRotation(q))
            return DysonSwarmOrbitFailure("dyson_swarm_orbit_native_radius_or_rotation_unproved");
        var plan = new DysonSwarmOrbitPlan
        {
            EvidenceVersion = 1, SessionId = sessionId, PlanetId = request.PlanetId, StarId = state.StarId,
            Orbit = spec, BeforeStateHash = orbits.StateHash, PlannedOrbitId = next,
            NativeRadius = radius, NativeRotation = q, NativeUp = new Vector3Snapshot { X = up.x, Y = up.y, Z = up.z }, ExistingActiveSails = orbits.ActiveSailCount,
            ExistingInFlightSails = orbits.InFlightSailCount, NativeChecksComplete = true,
        };
        plan.BindingHash = DysonSwarmOrbitPolicy.BindingHash(plan);
        return GameCallResult<NativeDysonSwarmOrbitBinding>.Succeeded(new NativeDysonSwarmOrbitBinding
        { Plan = plan, Observation = orbits, World = world, Sphere = sphere, Swarm = swarm, OrbitPool = swarm.orbits, BulletPool = swarm.bulletPool });
    }

    private BridgeError? RevalidateDysonSwarmOrbitOnMainThread(NormalActionPlanPayload payload)
    {
        var expected = payload.DysonSwarmOrbitBinding;
        if (expected is null || expected.Plan.Orbit.PreviewOnly) return Stale("dyson_swarm_orbit_executable_binding_missing");
        var captured = CaptureDysonSwarmOrbitPlan(payload.SessionId, DysonSwarmOrbitConfigurationRequest(expected.Plan));
        return captured.Success && captured.Value is { } current && SameDysonSwarmOrbitBinding(expected, current)
            ? null : captured.Error ?? Stale("dyson_swarm_orbit_native_binding_changed");
    }

    private static PrepareConfigureBuildingRequest DysonSwarmOrbitConfigurationRequest(DysonSwarmOrbitPlan plan) => new()
    {
        PlanetId = plan.PlanetId, Mode = BuildingConfigurationModes.DysonSwarmOrbit,
        ExpectedFactoryStateHash = plan.BeforeStateHash, DysonSwarmOrbit = plan.Orbit.Copy(),
    };

    private static bool SameDysonSwarmOrbitBinding(NativeDysonSwarmOrbitBinding a, NativeDysonSwarmOrbitBinding b) =>
        a.Plan.BindingHash == b.Plan.BindingHash && ReferenceEquals(a.World, b.World) && ReferenceEquals(a.Sphere, b.Sphere)
        && ReferenceEquals(a.Swarm, b.Swarm) && ReferenceEquals(a.OrbitPool, b.OrbitPool) && ReferenceEquals(a.BulletPool, b.BulletPool);

    private void ExecuteDysonSwarmOrbitOnMainThread(ActionRecord action)
    {
        var expected = action.Plan.DysonSwarmOrbitBinding!;
        var captured = CaptureDysonSwarmOrbitPlan(action.SessionId, DysonSwarmOrbitConfigurationRequest(expected.Plan));
        if (!captured.Success || captured.Value is not { } binding || !SameDysonSwarmOrbitBinding(expected, binding))
            throw new InvalidOperationException("The exact Native swarm orbit state changed before execution.");
        var plan = binding.Plan; var before = binding.Observation;
        var inventory = CapturePlayerPackageState(GameMain.mainPlayer);
        var readback = action.DysonSwarmOrbitReadback = new DysonSwarmOrbitReadback
        { EvidenceVersion = 1, StarId = plan.StarId, PlannedOrbitId = plan.PlannedOrbitId,
            PlanBindingHash = plan.BindingHash, BeforeStateHash = before.StateHash };
        var created = DysonSwarmOrbitNativeExecution.Execute(binding.Sphere, binding.Swarm, binding.OrbitPool, binding.BulletPool,
            before, plan, readback);
        action.TargetObjectId = readback.OrbitId > 0 ? readback.OrbitId : null;
        readback.InventoryPreserved = CapturePlayerPackageState(GameMain.mainPlayer) == inventory;
        if (!created || !ReferenceEquals(GameMain.data, binding.World) || !readback.InventoryPreserved)
            throw new InvalidOperationException("The Native orbit allocation did not prove its exact result and preserved swarm/inventory.");
        Complete(action, "Native NewOrbit created one empty enabled orbit; existing orbit data, sail counts and inventory were unchanged. No launch was performed.");
        action.AfterStateHash = readback.AfterStateHash;
    }

    private static GameCallResult<NativeDysonSwarmOrbitBinding> DysonSwarmOrbitFailure(string reason) =>
        GameCallResult<NativeDysonSwarmOrbitBinding>.Failed(BridgeError.Create(BridgeErrorCodes.ActionRejected, reason, true,
            "Inspect the complete current Native swarm and choose one finite orbit design before a new prepare."));

    private sealed class NativeDysonSwarmOrbitBinding
    {
        public DysonSwarmOrbitPlan Plan { get; set; } = null!;
        public DysonSwarmOrbitState Observation { get; set; } = null!;
        public GameData World { get; set; } = null!;
        public DysonSphere Sphere { get; set; } = null!;
        public DysonSwarm Swarm { get; set; } = null!;
        public SailOrbit[] OrbitPool { get; set; } = null!;
        public SailBullet[] BulletPool { get; set; } = null!;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public NativeDysonSwarmOrbitBinding? DysonSwarmOrbitBinding { get; set; }
        public static NormalActionPlanPayload ForDysonSwarmOrbit(NativeDysonSwarmOrbitBinding binding) => new()
        {
            ActionKind = NormalActionKinds.ConfigureBuilding, ConfigureMode = BuildingConfigurationModes.DysonSwarmOrbit,
            SessionId = binding.Plan.SessionId, PlanetId = binding.Plan.PlanetId, ExpectedStateHash = binding.Plan.BindingHash,
            FactoryStateHash = binding.Plan.BeforeStateHash, DysonSwarmOrbitBinding = binding,
        };
    }
    private sealed partial class ActionRecord
    {
        public DysonSwarmOrbitReadback? DysonSwarmOrbitReadback { get; set; }
    }
}
