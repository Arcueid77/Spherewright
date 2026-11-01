using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class EjectorOrbitConfigurationPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BindsAnExistingFixedTargetOrDisablesItWithoutChangingStock(int orbitId)
    {
        var (request, entity) = Fixture();
        request.EjectorOrbitId = orbitId;
        var before = System.Text.Json.JsonSerializer.Serialize(entity);
        Assert.Null(EjectorOrbitConfigurationPolicy.Validate(request, entity, out var plan));
        Assert.Equal(orbitId, plan!.OrbitId);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(entity));
    }

    [Theory]
    [InlineData("missing_state")]
    [InlineData("wrong_entity")]
    [InlineData("wrong_planet")]
    [InlineData("prebuild")]
    [InlineData("wrong_item")]
    [InlineData("binding_unproved")]
    [InlineData("component_replaced")]
    [InlineData("wrong_sail")]
    [InlineData("configuration_changed")]
    [InlineData("automatic_selection")]
    [InlineData("swarm_unavailable")]
    [InlineData("orbits_incomplete")]
    [InlineData("orbit_missing")]
    [InlineData("orbit_duplicate")]
    [InlineData("orbit_disabled")]
    [InlineData("orbit_mutated")]
    [InlineData("orbit_nan")]
    [InlineData("orbit_rotation_zero")]
    [InlineData("unspecified_target")]
    [InlineData("other_recipe")]
    [InlineData("other_technology")]
    [InlineData("other_filter")]
    [InlineData("other_storage")]
    [InlineData("other_station")]
    public void RejectsUnprovedOrMixedConfiguration(string fault)
    {
        var (r, e) = Fixture();
        var state = e.Ejector!;
        switch (fault)
        {
            case "missing_state": e.Ejector = null; break;
            case "wrong_entity": r.EntityId++; break;
            case "wrong_planet": r.PlanetId++; break;
            case "prebuild": e.ObjectKind = FactoryObjectKinds.Prebuild; break;
            case "wrong_item": e.ItemId = 2312; break;
            case "binding_unproved": state.NativeBindingVerified = false; break;
            case "component_replaced": state.EjectorComponentId++; break;
            case "wrong_sail": state.SailItemId = 1503; break;
            case "configuration_changed": state.OrbitId = 2; break;
            case "automatic_selection": state.AutomaticOrbitSelection = true; Rehash(r, e); break;
            case "swarm_unavailable": state.SwarmAvailable = false; break;
            case "orbits_incomplete": state.OrbitCoverageComplete = false; break;
            case "orbit_missing": state.Orbits.Clear(); break;
            case "orbit_duplicate": state.Orbits.Add(state.Orbits[0]); break;
            case "orbit_disabled": state.Orbits[0].Enabled = false; break;
            case "orbit_mutated": state.Orbits[0].Radius++; break;
            case "orbit_nan": state.Orbits[0].Radius = float.NaN; break;
            case "orbit_rotation_zero": state.Orbits[0].Rotation.W = 0; break;
            case "unspecified_target": r.EjectorOrbitId = -1; break;
            case "other_recipe": r.RecipeId = 70; break;
            case "other_technology": r.TechId = 1503; break;
            case "other_filter": r.FilterItemId = 1501; break;
            case "other_storage": r.StorageBannedGridCount = 0; break;
            case "other_station": r.StationMaximumChargePowerWatts = 1000; break;
        }
        Assert.NotNull(EjectorOrbitConfigurationPolicy.Validate(r, e, out var plan));
        Assert.Null(plan);
    }

    [Fact]
    public void CanStopFixedTargetingWhenSwarmObservationIsUnavailable()
    {
        var (r, e) = Fixture();
        r.EjectorOrbitId = 0;
        e.Ejector!.SwarmAvailable = false;
        e.Ejector.OrbitCoverageComplete = false;
        e.Ejector.Orbits.Clear();
        Assert.Null(EjectorOrbitConfigurationPolicy.Validate(r, e, out _));
    }

    [Fact]
    public void NativeMotionAndSailCountsDoNotInvalidateTheStaticConfigurationDomain()
    {
        var (r, e) = Fixture();
        var legacy = CanonicalStateHash.Factory(e);
        var hash = e.Ejector!.ConfigurationStateHash;
        e.Ejector.SailCount = 1;
        e.Ejector.FiredAnimationActive = true;
        e.Ejector.CycleProgressRaw = 12345;
        e.Ejector.RuntimeOrbitId = 0;
        e.Ejector.ActiveSailCount = 30;
        e.Ejector.Orbits[0].SailCount = 30;
        Assert.Equal(hash, EjectorOrbitConfigurationPolicy.ConfigurationHash(e, e.Ejector));
        Assert.Equal(legacy, CanonicalStateHash.Factory(e));
        Assert.False(e.Ejector.FiredAnimationIsLaunchReceipt);
        Assert.Null(EjectorOrbitConfigurationPolicy.Validate(r, e, out _));
    }

    [Theory]
    [InlineData("missing_echo")]
    [InlineData("wrong_target")]
    [InlineData("wrong_configuration")]
    [InlineData("missing_orbit_hash")]
    [InlineData("no_token")]
    [InlineData("not_prepared")]
    [InlineData("commit_forbidden")]
    public void ClientWithholdsMixedOrUnboundPreparedEchoes(string fault)
    {
        var (r, e) = Fixture();
        Assert.Null(EjectorOrbitConfigurationPolicy.Validate(r, e, out var plan));
        var result = new PreparedNormalAction { Prepared = true, CommitAllowedNow = true,
            ActionKind = NormalActionKinds.ConfigureBuilding, PlanToken = "fixture-only-token",
            PlannedEjectorOrbit = plan };
        Assert.True(EjectorOrbitConfigurationPolicy.ConfirmsPreparedEcho(r, result));
        switch (fault)
        {
            case "missing_echo": result.PlannedEjectorOrbit = null; break;
            case "wrong_target": plan!.OrbitId++; break;
            case "wrong_configuration": plan!.ConfigurationStateHash = "different"; break;
            case "missing_orbit_hash": plan!.OrbitStateHash = ""; break;
            case "no_token": result.PlanToken = ""; break;
            case "not_prepared": result.Prepared = false; break;
            case "commit_forbidden": result.CommitAllowedNow = false; break;
        }
        Assert.False(EjectorOrbitConfigurationPolicy.ConfirmsPreparedEcho(r, result));
    }

    private static (PrepareConfigureBuildingRequest, FactoryEntitySnapshot) Fixture()
    {
        var entity = new FactoryEntitySnapshot { PlanetId = 101, ObjectId = 7, ItemId = 2311,
            ObjectKind = FactoryObjectKinds.Entity, ComponentKind = "ejector",
            Position = new Vector3Snapshot { Y = 200 }, Rotation = new QuaternionSnapshot { W = 1 },
            Ejector = new EjectorStateSnapshot { NativeBindingVerified = true, EjectorComponentId = 1, StarId = 1,
                SailItemId = 1501, SailCount = 2, SwarmAvailable = true, OrbitCoverageComplete = true } };
        var orbit = new SwarmOrbitSnapshot { OrbitId = 1, Enabled = true, Radius = 10000,
            Rotation = new QuaternionSnapshot { W = 1 } };
        orbit.StateHash = EjectorOrbitConfigurationPolicy.OrbitHash(1, orbit);
        entity.Ejector.Orbits.Add(orbit);
        var request = new PrepareConfigureBuildingRequest { PlanetId = 101, EntityId = 7,
            Mode = BuildingConfigurationModes.EjectorOrbit, EjectorOrbitId = 1 };
        Rehash(request, entity);
        return (request, entity);
    }

    private static void Rehash(PrepareConfigureBuildingRequest request, FactoryEntitySnapshot entity) =>
        request.ExpectedFactoryStateHash = entity.Ejector!.ConfigurationStateHash =
            EjectorOrbitConfigurationPolicy.ConfigurationHash(entity, entity.Ejector);
}
