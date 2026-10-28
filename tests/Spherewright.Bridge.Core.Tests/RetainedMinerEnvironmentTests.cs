using BepInEx.Logging;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class RetainedMinerEnvironmentTests
{
    private const string Blocker = "remote_resume_charge_native_construction_not_empty";

    [Theory]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public void SealedUnpoweredMinerSurvivesRestartWithRetiredPrebuildAndUnchangedReceipt(bool historicalDispatchDisabled, int prebuildCursor)
    {
        using var f = new Fixture(historicalDispatchDisabled, prebuildCursor); var before = File.ReadAllBytes(f.Path);
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
    [InlineData("live_dispatch_changed")]
    [InlineData("retired_slot_live_beyond_reset_cursor")]
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
            case "damaged_pool": f.Factory.entityCursor = f.Factory.entityPool.Length + 1; break;
            case "working_drone": f.Player.mecha.constructionModule.TestPhysicalIdle = false; break;
            case "build_target": f.Player.mecha.constructionModule.buildTargetTotalCount = 1; break;
            case "repair_target": f.Player.mecha.constructionModule.repairTargets = [new() { objectId = 1 }]; break;
            case "reconstruct_target": f.Player.mecha.constructionModule.autoReconstructTargetTotalCount = 1; break;
            case "wrong_component": f.Factory.factorySystem.minerPool[1].entityId = 2; break;
            case "wrong_world_reference": GameMain.data!.localLoadedPlanetFactory = new object(); break;
            case "live_dispatch_changed": f.Player.mecha.constructionModule.droneEnabled = false; break;
            case "retired_slot_live_beyond_reset_cursor": f.Factory.prebuildCursor = 1; f.Factory.prebuildPool[1].id = 1; break;
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

    [Fact]
    public void FailedRecoveryRetainsTheFirstSameSampleFactoryPredicateAndActualCursor()
    {
        using var f = new Fixture(true, 1);
        GameMain.data!.localLoadedPlanetFactory = new object();
        var evidence = new NativeResumeDecisionEvidence("load_preflight_before_first_frame", 112,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), true);
        Assert.Equal(Blocker, f.Guard(evidence));
        var failure = evidence.Failure(Blocker);
        Assert.Equal("retained_miner_loaded_factory_reference", failure.FirstFailedPredicate);
        Assert.Equal(1, failure.Values["retained_miner_prebuild_cursor"]);
        Assert.Equal("true", failure.Predicates["retained_miner_owned_journey"]);
        Assert.Equal("false", failure.Predicates["retained_miner_loaded_factory_reference"]);
        Assert.False(failure.SafetyCertified);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void CompletedPaidPolesPermitOnlyAZeroSourcePowerGraphWithoutChangingReceipts(int count)
    {
        using var f = new Fixture();
        for (var i = 0; i < count; i++) f.AddPole();
        var before = File.ReadAllBytes(f.Path);
        Assert.Null(f.Guard());
        Assert.Equal(before, File.ReadAllBytes(f.Path));
    }

    [Theory]
    [InlineData("generator")] [InlineData("stored_energy")] [InlineData("served_energy")]
    [InlineData("capacity")] [InlineData("export")] [InlineData("accumulator")]
    [InlineData("exchanger")] [InlineData("foreign_node")] [InlineData("foreign_connection")]
    [InlineData("missing_node")] [InlineData("missing_network")] [InlineData("wrong_component")]
    [InlineData("lost_receipt")] [InlineData("foreign_journey")] [InlineData("live_prebuild")]
    public void PoleRecoveryRejectsPoweredUnboundOrUnpaidState(string fault)
    {
        using var f = new Fixture(); f.AddPole();
        var power = f.Factory.powerSystem; var net = power.netPool[1]!;
        switch (fault)
        {
            case "generator": power.genCursor = 2; break;
            case "stored_energy": net.energyStored = 1; break;
            case "served_energy": net.energyServed = 1; break;
            case "capacity": net.energyCapacity = 1; break;
            case "export": net.energyExport = 1; break;
            case "accumulator": net.accumulators.Add(1); break;
            case "exchanger": net.exchangers.Add(1); break;
            case "foreign_node": net.nodes.Add(new() { id = 9 }); break;
            case "foreign_connection": net.nodes[0].conns.Add(new() { id = 9 }); break;
            case "missing_node": net.nodes.Clear(); break;
            case "missing_network": power.netPool[1] = null; break;
            case "wrong_component": power.nodePool[1].entityId = 3; break;
            case "lost_receipt": File.Delete(f.Path); break;
            case "foreign_journey": f.Journey = Guid.NewGuid().ToString(); break;
            case "live_prebuild": f.Factory.prebuildPool[2].id = 2; break;
        }
        Assert.Equal(Blocker, f.Guard());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ExactPaidUnpoweredBeltChainAndEmptySupplySlotSurviveRecoveryWithoutStoreWrites(bool originalEnabled)
    {
        using var f = new Fixture(); f.AddPole(originalEnabled); f.AddStationAndBelt(12, originalEnabled);
        var before = File.ReadAllBytes(f.Path);
        Assert.Null(f.Guard()); Assert.Null(f.Guard());
        Assert.Equal(before, File.ReadAllBytes(f.Path));
    }

    [Theory]
    [InlineData("cargo")] [InlineData("missing_member")] [InlineData("foreign_member")]
    [InlineData("output_port")] [InlineData("stock")] [InlineData("order")]
    [InlineData("power")] [InlineData("drone")] [InlineData("wrong_journey")]
    [InlineData("new_entity")] [InlineData("connection")]
    public void SavedBeltChainStillRejectsStockTopologyPowerAndOriginalProofChanges(string fault)
    {
        using var f = new Fixture(); f.AddPole(); f.AddStationAndBelt(12);
        var station = f.Factory.transport.stationPool[1]!;
        var path = f.Factory.cargoTraffic.pathPool[1]!;
        switch (fault)
        {
            case "cargo": path.Snapshot.CargoBuffer[0] = 128; break;
            case "missing_member": path.Snapshot.BeltIds.RemoveAt(0); break;
            case "foreign_member": path.Snapshot.BeltIds[0] = 31; break;
            case "output_port": station.slots[8].dir = IODir.Output; break;
            case "stock": station.storage[0].count = 1; break;
            case "order": station.storage[0].remoteOrder = 1; break;
            case "power": station.energy = 1; break;
            case "drone": f.Player.mecha.constructionModule.TestPhysicalIdle = false; break;
            case "wrong_journey": f.Journey = Guid.NewGuid().ToString(); break;
            case "new_entity": f.Factory.entityCount++; break;
            case "connection": f.Factory.TestConnectionsMatch = false; break;
        }
        Assert.Equal(Blocker, f.Guard());
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
        internal string? Guard(NativeResumeDecisionEvidence? evidence = null)
            => new NormalGameActionCoordinator(Store(), Sessions).MinerEnvironmentForTest(Player, Journey, evidence);
        internal void AddPole(bool originalEnabled = true)
        {
            var id = Factory.entityCursor++; Factory.entityCount++;
            var nodeId = Factory.powerSystem.nodeCursor++;
            Factory.entityPool[id] = new() { id = id, protoId = 2201, powerNodeId = nodeId };
            Factory.powerSystem.nodePool[nodeId] = new() { id = nodeId, entityId = id };
            Factory.powerSystem.netCursor = 2;
            Factory.powerSystem.netPool[1] ??= new();
            Factory.powerSystem.netPool[1]!.nodes.Add(new() { id = nodeId });
            Factory.powerSystem.consumerPool[1].networkId = 1;
            var site = new BlueprintSiteSnapshot { SessionId = "synthetic-session", PlanetId = 201, CapturedAtGameTick = 100,
                BlueprintHash = "synthetic-approved-pole", NativeCheckPerformed = true, NativeCheckPassed = true,
                InventorySufficient = true, TechnologySatisfied = true,
                Objects = [new() { Index = 0, ItemId = 2201, NativeCondition = "Ok" }] };
            var binding = new StructuredConstructionBinding { Kind = "core", JourneyId = Journey!,
                NativePlanHash = "sha256:" + new string('b', 64), Steps = [new()] };
            var state = BlueprintBuildState.Create(site, structuredConstruction: binding);
            var c = state.ConstructionControl = StructuredConstructionControlTests.Control();
            c.OriginalDroneEnabled = originalEnabled;
            Player.mecha.constructionModule.droneEnabled = originalEnabled;
            var action = Guid.NewGuid().ToString(); c.NativeCreateActionId = action;
            state.Begin("synthetic-session", action, 1, 100); state.BeforeStructuredSubmission(101, 11, "before-pole");
            state.ConfirmStructuredSubmission([id], 10, "after-pole"); state.ConfirmCompletion(0, id, 110);
            c.RequestStop("construction_complete_draining", 110, c.StartedAtUtc.AddSeconds(1), false);
            c.DispatchDisabled = true; Assert.True(c.RecordDisabledNativeFactoryPass(110)); Assert.True(c.TryMarkPhysicalIdle(112, true, true));
            c.Phase = "completed_idle"; state.LastEvidenceGameTick = 112; state.Stop("completed");
            Assert.True(Store().TryPut(state));
        }
        internal void AddStationAndBelt(int count, bool originalEnabled = true)
        {
            var stationId = Factory.entityCursor++; Factory.entityCount++;
            Factory.entityPool[stationId] = new() { id = stationId, protoId = 2104, stationId = 1, powerConId = 2 };
            Factory.powerSystem.consumerCursor = 3;
            Factory.powerSystem.consumerPool[2] = new() { id = 2, entityId = stationId };
            var station = Factory.transport.stationPool[1] = new() { id = 1, entityId = stationId, planetId = 201, gid = 2 };
            station.storage[0] = new() { itemId = 1013, max = 100, remoteLogic = ELogisticStorage.Supply };
            Seal(2104, "core", [stationId], [new()], originalEnabled: true,
                 stopReason: "construction_pre_player_stationary_binding_lost");
            // The real attribution capture records only already-existing
            // objects of this batch's item kind, not every older building.
            int[] prior = [];
            var ids = Enumerable.Range(Factory.entityCursor, count).ToArray();
            Factory.entityCursor += count; Factory.entityCount += count;
            Factory.cargoTraffic.beltCursor = count + 1; Factory.cargoTraffic.pathCursor = 2;
            for (var i = 0; i < count; i++)
            {
                Factory.entityPool[ids[i]] = new() { id = ids[i], protoId = 2001, beltId = i + 1 };
                Factory.cargoTraffic.beltPool[i + 1] = new() { id = i + 1, entityId = ids[i], segPathId = 1, segIndex = i * 10, segLength = 10 };
            }
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var length = count * 10;
                writer.Write(1); writer.Write(1); writer.Write(length); writer.Write(length);
                writer.Write(1); writer.Write(1); writer.Write(length); writer.Write(false);
                writer.Write(0); writer.Write(-1); writer.Write(count); writer.Write(0); writer.Write(new byte[length]);
                writer.Write(0); writer.Write(length); writer.Write(1);
                for (var i = 0; i < length * 7; i++) writer.Write(0f);
                for (var i = 1; i <= count; i++) writer.Write(i);
            }
            Assert.True(BeltUpgradePathPolicy.TryReadExport(stream.ToArray(), 1, count * 10, out var captured, out _));
            var snapshot = captured!;
            Factory.cargoTraffic.pathPool[1] = new() { id = 1, buffer = snapshot.CargoBuffer, Snapshot = snapshot, cargoContainer = Factory.cargoContainer };
            station.slots[8] = new() { dir = IODir.Input, beltId = count };
            var connections = Enumerable.Range(0, count).Select(i => new StructuredConstructionConnection
            {
                InputStepIndex = i == 0 ? -1 : i - 1, InputObjectId = i == 0 ? 1 : 0,
                OutputStepIndex = i + 1 == count ? -1 : i + 1, OutputObjectId = i + 1 == count ? stationId : 0,
                OutputToSlot = i + 1 == count ? 8 : 1,
            }).ToList();
            Seal(2001, "belt", ids, connections, originalEnabled: originalEnabled, prior: prior, sourceId: 1, destinationId: stationId);
        }
        private void Seal(int item, string kind, int[] ids, List<StructuredConstructionConnection> connections,
            bool originalEnabled, string stopReason = "construction_complete_draining", int[]? prior = null,
            int sourceId = 0, int destinationId = 0)
        {
            var offset = item == 2001 ? 100 : 0;
            var site = new BlueprintSiteSnapshot { SessionId = "synthetic-session", PlanetId = 201, CapturedAtGameTick = 100 + offset,
                BlueprintHash = "synthetic-native-paid", NativeCheckPerformed = true, NativeCheckPassed = true,
                InventorySufficient = true, TechnologySatisfied = true, NativeBlueprintObjectLimit = item == 2001 ? 12 : 8,
                Objects = ids.Select((_, i) => new BlueprintSiteObject { Index = i, ItemId = item, NativeCondition = "Ok" }).ToList() };
            var binding = new StructuredConstructionBinding { Kind = kind, JourneyId = Journey!, NativePlanHash = "sha256:" + new string('d', 64),
                Steps = connections, SourceObjectId = sourceId, DestinationObjectId = destinationId };
            foreach (var id in new[] { sourceId, destinationId }.Where(id => id > 0))
                binding.Endpoints.Add(new() { ObjectId = id, ItemId = Factory.entityPool[id].protoId });
            var state = BlueprintBuildState.Create(site, structuredConstruction: binding);
            var c = state.ConstructionControl = StructuredConstructionControlTests.Control();
            c.OriginalDroneEnabled = originalEnabled; c.PreexistingEntityIds = prior?.ToList() ?? [];
            c.StartedAtGameTick += offset; c.WorkDeadlineGameTick += offset;
            c.BindingLost = item == 2104;
            var action = Guid.NewGuid().ToString(); c.NativeCreateActionId = action;
            state.Begin("synthetic-session", action, ids.Length, 100 + offset); state.BeforeStructuredSubmission(101 + offset, ids.Length + 1, "before-paid");
            state.ConfirmStructuredSubmission(ids, 1, "after-paid");
            for (var i = 0; i < ids.Length; i++) state.ConfirmCompletion(i, ids[i], 110 + offset);
            c.RequestStop(stopReason, 110 + offset, c.StartedAtUtc.AddSeconds(1), false); c.DispatchDisabled = true;
            Assert.True(c.RecordDisabledNativeFactoryPass(110 + offset)); Assert.True(c.TryMarkPhysicalIdle(112 + offset, true, true));
            c.Phase = "completed_idle"; state.LastEvidenceGameTick = 112 + offset; state.Stop("completed");
            GameMain.gameTick = 112 + offset;
            Assert.True(Store().TryPut(state));
        }
        internal Fixture(bool historicalDispatchDisabled = false, int prebuildCursor = 2)
        {
            GameMain.history = new(); GameMain.gameTick = 112;
            Player.factory = Factory; GameMain.data = new() { localLoadedPlanetFactory = Factory };
            GameMain.localPlanet = new() { id = 201, factory = Factory };
            Factory.entityCount = 1; Factory.entityCursor = 2; Factory.prebuildCursor = prebuildCursor;
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
            c.Phase = "completed_idle"; c.DispatchDisabled = historicalDispatchDisabled; state.LastEvidenceGameTick = 112; state.Stop("completed");
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
