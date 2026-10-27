using BepInEx.Logging;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class RetainedMinerEnvironmentTests
{
    private const string Blocker = "remote_resume_charge_native_construction_not_empty";

    [Fact]
    public void SealedUnpoweredMinerSurvivesRestartWithRetiredPrebuildAndUnchangedReceipt()
    {
        using var f = new Fixture(); var before = File.ReadAllBytes(f.Path);
        Assert.Null(f.Guard());
        Assert.Null(f.Guard()); // New Store/coordinator each time, no cached grant.
        Assert.Equal(before, File.ReadAllBytes(f.Path));
    }

    [Theory]
    [InlineData("no_receipt")] [InlineData("foreign_identity")] [InlineData("foreign_journey")]
    [InlineData("missing_journey")] [InlineData("unowned")] [InlineData("other_planet")]
    [InlineData("older_world")] [InlineData("extra_entity")] [InlineData("wrong_entity")]
    [InlineData("geometry")] [InlineData("connection")] [InlineData("powered")]
    [InlineData("generator")] [InlineData("output")] [InlineData("prebuild")]
    [InlineData("damaged_pool")] [InlineData("working_drone")] [InlineData("build_target")]
    [InlineData("repair_target")] [InlineData("reconstruct_target")] [InlineData("wrong_component")]
    [InlineData("wrong_world_reference")]
    public void OnlyTheExactCompletedIdleUnpoweredSingleMinerCanEnter(string fault)
    {
        using var f = new Fixture();
        switch (fault)
        {
            case "no_receipt": File.Delete(f.Path); break;
            case "foreign_identity": f.Sessions.OwnedSaveName = "synthetic-other-world"; break;
            case "foreign_journey": f.Journey = Guid.NewGuid().ToString(); break;
            case "missing_journey": f.Journey = null; break;
            case "unowned": f.Sessions.IsCurrentSessionOwned = false; break;
            case "other_planet": f.Factory.planetId++; break;
            case "older_world": GameMain.gameTick = 111; break;
            case "extra_entity": f.Factory.entityCount = 2; break;
            case "wrong_entity": f.Factory.entityPool[1].protoId = 2201; break;
            case "geometry": f.Factory.TestGeometryMatches = false; break;
            case "connection": f.Factory.TestConnectionsMatch = false; break;
            case "powered": f.Factory.powerSystem.consumerPool[1].networkId = 1; break;
            case "generator": f.Factory.powerSystem.genCursor = 2; break;
            case "output": f.Factory.factorySystem.minerPool[1].productCount = 1; break;
            case "prebuild": f.Factory.prebuildPool[1].id = 1; break;
            case "damaged_pool": f.Factory.entityCursor = 10; break;
            case "working_drone": f.Player.mecha.constructionModule.TestPhysicalIdle = false; break;
            case "build_target": f.Player.mecha.constructionModule.buildTargetTotalCount = 1; break;
            case "repair_target": f.Player.mecha.constructionModule.repairTargets = [new() { objectId = 1 }]; break;
            case "reconstruct_target": f.Player.mecha.constructionModule.autoReconstructTargetTotalCount = 1; break;
            case "wrong_component": f.Factory.factorySystem.minerPool[1].entityId = 2; break;
            case "wrong_world_reference": GameMain.data!.localLoadedPlanetFactory = new object(); break;
        }
        Assert.Equal(Blocker, f.Guard());
    }

    [Theory]
    [InlineData("missing_idle")] [InlineData("paused")] [InlineData("missing_debit")]
    [InlineData("missing_create")] [InlineData("lost_binding")] [InlineData("missing_pass")]
    public void IncompleteOrDamagedOriginalProofCannotAuthorizeRecovery(string fault)
    {
        using var f = new Fixture(); var record = f.Record();
        switch (fault)
        {
            case "missing_idle": record.ConstructionControl!.PhysicalIdleVerified = false; break;
            case "paused": record.Phase = "paused"; break;
            case "missing_debit": record.Objects[0].InventoryAfter = 11; break;
            case "missing_create": record.ConstructionControl!.NativeCreateActionId = null; break;
            case "lost_binding": record.ConstructionControl!.BindingLost = true; break;
            case "missing_pass": record.ConstructionControl!.DisabledNativeFactoryPassGameTick = null; break;
        }
        // Some incomplete records are rejected by persistence itself; either way
        // no certificate may be acquired from the altered durable document.
        var json = File.ReadAllText(f.Path);
        var document = Newtonsoft.Json.Linq.JObject.Parse(json);
        document["builds"]![0] = Newtonsoft.Json.Linq.JToken.FromObject(record,
            Newtonsoft.Json.JsonSerializer.Create(new() { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }));
        File.WriteAllText(f.Path, document.ToString());
        Assert.Equal(Blocker, f.Guard());
    }

    [Fact]
    public void EmptyFactoryAndResearchAndVelocityGuardsRetainTheirOriginalBehavior()
    {
        using var f = new Fixture(); f.Factory.entityCount = 0; f.Factory.entityCursor = f.Factory.prebuildCursor = 1;
        Assert.Null(f.Guard());
        GameMain.history!.currentTech = 1;
        Assert.Equal("remote_resume_charge_global_research_active", f.Guard());
        f.Player.controller.actionWalk.rtsVelocity = new(1, 0, 0);
        Assert.Equal("remote_resume_charge_rts_or_manual_velocity_nonzero", f.Guard());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Spherewright", "retained-miner", Guid.NewGuid().ToString("N"));
        internal readonly GameSessionTracker Sessions = new() { IsCurrentSessionOwned = true, OwnedSaveName = "synthetic-miner-world", SessionId = "synthetic-session" };
        internal readonly PlanetFactory Factory = new();
        internal readonly Player Player = new();
        internal string? Journey = Guid.NewGuid().ToString();
        internal string Path => Directory.GetFiles(System.IO.Path.Combine(root, "foundry"), "blueprints-*.json").Single();
        internal BlueprintBuildStore Store() => new(root, "0.10.35.29151", Sessions, new ManualLogSource());
        internal BlueprintBuildState Record() { Assert.True(Store().TryRead(out var builds)); return Assert.Single(builds); }
        internal string? Guard() => new NormalGameActionCoordinator(Store(), Sessions).MinerEnvironmentForTest(Player, Journey);
        internal Fixture()
        {
            GameMain.history = new(); GameMain.gameTick = 112;
            Player.factory = Factory; GameMain.data = new() { localLoadedPlanetFactory = Factory };
            GameMain.localPlanet = new() { id = 201, factory = Factory };
            Factory.entityCount = 1; Factory.entityCursor = Factory.prebuildCursor = 2;
            Factory.entityPool[1] = new() { id = 1, protoId = 2301, minerId = 1, powerConId = 1 };
            Factory.factorySystem.minerPool[1] = new() { id = 1, entityId = 1, pcId = 1 };
            Factory.powerSystem.consumerPool[1] = new() { id = 1, entityId = 1 };
            var site = new BlueprintSiteSnapshot { SessionId = "synthetic-session", PlanetId = 201, CapturedAtGameTick = 100,
                BlueprintHash = "synthetic-approved-site", NativeCheckPerformed = true, NativeCheckPassed = true,
                InventorySufficient = true, TechnologySatisfied = true,
                Objects = [new() { Index = 0, ItemId = 2301, Parameters = [7], NativeCondition = "Ok" }] };
            var binding = new StructuredConstructionBinding { Kind = "resource", JourneyId = Journey!, NativePlanHash = "sha256:" + new string('a', 64),
                ResourceNodeId = 7, ResourceStateHash = "synthetic-resource", Steps = [new()] };
            var state = BlueprintBuildState.Create(site, structuredConstruction: binding);
            var c = state.ConstructionControl = StructuredConstructionControlTests.Control();
            var action = Guid.NewGuid().ToString(); c.NativeCreateActionId = action;
            state.Begin("synthetic-session", action, 1, 100); state.BeforeStructuredSubmission(101, 11, "before");
            state.ConfirmStructuredSubmission([1], 10, "after"); state.ConfirmCompletion(0, 1, 110);
            c.RequestStop("construction_complete_draining", 110, c.StartedAtUtc.AddSeconds(1), false);
            c.DispatchDisabled = true; Assert.True(c.RecordDisabledNativeFactoryPass(110)); Assert.True(c.TryMarkPhysicalIdle(112, true, true));
            c.Phase = "completed_idle"; c.DispatchDisabled = false; state.LastEvidenceGameTick = 112; state.Stop("completed");
            Assert.True(Store().TryPut(state));
        }
        public void Dispose()
        {
            Sessions.Dispose(); GameMain.history = new();
            var parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Spherewright", "retained-miner")) + System.IO.Path.DirectorySeparatorChar;
            if (!System.IO.Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Owned cleanup path escaped.");
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
