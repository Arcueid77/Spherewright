using BepInEx.Logging;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Linked production admission/store/topology with the existing Native geometry
// and pool substitutes. These tests do not prove Unity loading or construction.
[Collection("Owned resume native substitutes")]
public sealed class SavedFactorySiteTests
{
    [Fact]
    public void PaidPendingSourceCanGainLaterNativeCompletionWithoutChangingItsUnknownControlOrDebit()
    {
        using var f=new Fixture(true,unknownTail:true,workFailure:true);
        Assert.True(f.Store().TryRead(out var rows));var b=rows.Single(x=>x.Phase=="outcome_unknown");
        var original=Newtonsoft.Json.JsonConvert.SerializeObject(b.ConstructionControl);
        Assert.True(NormalGameActionCoordinator.SavedPaidWorkReceiptForTest(b,f.Journey,120));
        Assert.NotNull(f.SavedLandingGuard());
        // Substitute only Native's already-paid prebuild->entity observation.
        b.ConfirmCompletion(0,3,121);b.LastEvidenceGameTick=123;b.Generation++;
        f.Factory.prebuildPool[1]=new();f.Factory.prebuildCount=0;f.Factory.entityPool[3]=new(){id=3,protoId=2011,rot=new(0,0,0,1)};
        f.Factory.entityCount=3;f.Factory.entityCursor=4;
        f.Factory.TestPorts.Clear();f.Connect(1,0,3,0);f.Connect(3,1,2,0);
        b.SavedTailCompletion=new(){OriginalActionId=b.ActionId!,OriginalControlHash=b.ConstructionControl!.EvidenceHash(),BuildPlanHash=b.PlanHash,
            SourceGameTick=120,NativeFactoryPassGameTick=122,PhysicalIdleGameTick=123,SourceFingerprint="sha256:"+new string('c',64)};
        GameMain.gameTick=130;Assert.True(f.Store().TryPut(b));Assert.Null(f.SavedLandingGuard());
        Assert.True(f.Store().TryRead(out var reopened));var saved=reopened.Single(x=>x.BuildId==b.BuildId);
        Assert.Equal("outcome_unknown",saved.Phase);Assert.Equal(original,Newtonsoft.Json.JsonConvert.SerializeObject(saved.ConstructionControl));
        Assert.Equal(1,saved.Objects[0].InventoryBefore);Assert.Equal(0,saved.Objects[0].InventoryAfter);
    }
    [Fact]
    public void LaterNativeIdleCertificateAdmitsPaidObjectsWhileOriginalUnknownControlRemainsByteIdentical()
    {
        using var f=new Fixture(false,unknownTail:true);
        Assert.NotNull(f.SavedLandingGuard());
        Assert.True(f.Store().TryRead(out var builds));var b=builds.Single(x=>x.Phase=="outcome_unknown");
        var original=Newtonsoft.Json.JsonConvert.SerializeObject(b.ConstructionControl);var action=b.ActionId;
        b.SavedTailCompletion=new() {OriginalActionId=action!,OriginalControlHash=b.ConstructionControl!.EvidenceHash(),
            BuildPlanHash=b.PlanHash,SourceGameTick=120,NativeFactoryPassGameTick=121,PhysicalIdleGameTick=122,
            SourceFingerprint="sha256:"+new string('b',64)};
        b.LastEvidenceGameTick=122;b.Generation++;GameMain.gameTick=130;
        Assert.True(f.Store().TryPut(b));Assert.Null(f.SavedLandingGuard());
        Assert.True(f.Store().TryRead(out var reopened));var saved=reopened.Single(x=>x.BuildId==b.BuildId);
        Assert.Equal("outcome_unknown",saved.Phase);Assert.Equal(action,saved.ActionId);
        Assert.Equal(original,Newtonsoft.Json.JsonConvert.SerializeObject(saved.ConstructionControl));
        f.Factory.TestPorts.Remove((1,0));Assert.NotNull(f.SavedLandingGuard());
    }
    [Fact]
    public void NewSavedLandingWitnessCanRecoverAnEntirePaidMiningSiteWithoutRelaxingDefaultRecovery()
    {
        using var f = new Fixture(false, miningOnly: true);
        var before = File.ReadAllBytes(f.StorePath);
        Assert.NotNull(f.Guard());
        Assert.Null(f.SavedLandingGuard());
        Assert.Equal(before, File.ReadAllBytes(f.StorePath));
        f.MakeUnknown();
        Assert.NotNull(f.SavedLandingGuard());
    }

    [Fact]
    public void NewSavedLandingWitnessPreservesTheExistingPaidPendingObjectRecoveryPath()
    {
        using var f = new Fixture(true);
        Assert.Null(f.SavedLandingGuard());
        Assert.Equal("landing_charge_completed_paid_site_required", f.LandingGuard());
    }
    [Fact]
    public void CompletedPaidMiningSiteCanSupportAnotherOwnedLandingWithoutChangingOldReceipts()
    {
        using var f = new Fixture(false, miningOnly: true);
        var before = File.ReadAllBytes(f.StorePath);
        f.Journey = Guid.NewGuid().ToString();
        Assert.Null(f.LandingGuard());
        Assert.Equal(before, File.ReadAllBytes(f.StorePath));
    }

    [Fact]
    public void LandingAdmissionDoesNotBorrowTheRecoveryPendingObjectException()
    {
        using var f = new Fixture(true);
        Assert.Null(f.Guard());
        Assert.Equal("landing_charge_completed_paid_site_required", f.LandingGuard());
    }

    [Theory]
    [InlineData("missing_journey")]
    [InlineData("foreign_entity")]
    [InlineData("broken_edge")]
    [InlineData("unknown_receipt")]
    [InlineData("busy_drone")]
    public void CompletedMiningLandingStillRequiresEveryOwnedObjectEdgeAndIdleReceipt(string fault)
    {
        using var f = new Fixture(false, miningOnly: true);
        switch (fault)
        {
            case "missing_journey": f.Journey = null!; break;
            case "foreign_entity": f.Factory.entityPool[4] = new() { id = 4, protoId = 2203 }; f.Factory.entityCount++; f.Factory.entityCursor++; break;
            case "broken_edge": f.Factory.TestPorts.Remove((1, 0)); break;
            case "unknown_receipt": f.MakeUnknown(); break;
            case "busy_drone": f.Player.mecha.constructionModule.TestPhysicalIdle = false; break;
        }
        Assert.NotNull(f.LandingGuard());
    }

    [Fact]
    public void PassiveStationKeepsMultipleNormalSupplyAndDemandSlotsOnRecovery()
    {
        using var f = new Fixture(false); f.AddStation();
        Assert.Null(f.Guard());
        var station = f.Factory.transport.stationPool[1]!;
        Assert.Equal(1013, station.storage[0].itemId);
        Assert.Equal(1301, station.storage[1].itemId);
        Assert.Equal(12, station.storage[1].count);
    }

    [Theory]
    [InlineData("own_fleet")]
    [InlineData("negative_stock")]
    [InlineData("invalid_inc")]
    [InlineData("unknown_logic")]
    [InlineData("wrong_station_identity")]
    public void FactoryAdmissionCannotTurnThePassiveStationIntoAnUnprovedFleet(string fault)
    {
        using var f = new Fixture(false); f.AddStation(); var s = f.Factory.transport.stationPool[1]!;
        switch (fault)
        {
            case "own_fleet": s.idleShipCount = 1; break;
            case "negative_stock": s.storage[1].count = -1; break;
            case "invalid_inc": s.storage[1].inc = 121; break;
            case "unknown_logic": s.storage[1].remoteLogic = (ELogisticStorage)99; break;
            case "wrong_station_identity": s.entityId = 1; break;
        }
        Assert.Equal("remote_resume_paid_factory_site_unproved", f.Guard());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LaterPaidConnectionAndRetainedPendingPrebuildSurviveFreshStoreRead(bool pending)
    {
        using var f = new Fixture(pending);
        var before = File.ReadAllBytes(f.StorePath);
        Assert.Null(f.Guard());
        Assert.Null(f.Guard());
        Assert.Equal(before, File.ReadAllBytes(f.StorePath));
        Assert.Equal(pending ? 1 : 0, f.Factory.prebuildCount);
    }

    [Theory]
    [InlineData("extra_entity")]
    [InlineData("one_sided")]
    [InlineData("unreceipted_edge")]
    [InlineData("missing_edge")]
    [InlineData("geometry")]
    [InlineData("unknown_batch")]
    [InlineData("drone_busy")]
    [InlineData("other_world")]
    [InlineData("other_player")]
    [InlineData("other_pending_journey")]
    [InlineData("dispatch_live_with_pending")]
    [InlineData("extra_prebuild")]
    public void IncompleteForeignOrUnknownSiteNeverBorrowsFactoryRecovery(string fault)
    {
        using var f = new Fixture(true);
        switch (fault)
        {
            case "extra_entity": f.Factory.entityPool[3] = new() { id = 3, protoId = 2304 }; f.Factory.entityCursor++; f.Factory.entityCount++; break;
            case "one_sided": f.Factory.TestPorts.Remove((-1, 0)); break;
            case "unreceipted_edge": f.Connect(1, 2, 2, 2); break;
            case "missing_edge": f.Factory.TestPorts.Remove((1, 0)); f.Factory.TestPorts.Remove((-1, 0)); break;
            case "geometry": f.Factory.TestGeometryMatches = false; break;
            case "unknown_batch": f.MakeUnknown(); break;
            case "drone_busy": f.Player.mecha.constructionModule.TestPhysicalIdle = false; break;
            case "other_world": GameMain.data!.localLoadedPlanetFactory = new object(); break;
            case "other_player": GameMain.SavedFactoryTestPlayer = new(); break;
            case "other_pending_journey": f.Journey = Guid.NewGuid().ToString(); break;
            case "dispatch_live_with_pending": f.Player.mecha.constructionModule.droneEnabled = true; break;
            case "extra_prebuild": f.Factory.prebuildPool[2] = new() { id = 2 }; f.Factory.prebuildCursor++; f.Factory.prebuildCount++; break;
        }
        Assert.Equal("remote_resume_paid_factory_site_unproved", f.Guard());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "Spherewright", "saved-factory", Guid.NewGuid().ToString("N"));
        private readonly GameSessionTracker sessions = new() { IsCurrentSessionOwned = true, OwnedSaveName = "synthetic-factory-world", SessionId = "synthetic-session" };
        internal readonly PlanetFactory Factory = new() { planetId = 402 };
        internal readonly Player Player = new() { planetId = 402 };
        internal string Journey = Guid.NewGuid().ToString();
        private readonly int sourceItem;
        private readonly int destinationItem;
        internal BlueprintBuildStore Store() => new(root, "0.10.35.29151", sessions, new ManualLogSource());
        internal string StorePath => Directory.GetFiles(Path.Combine(root, "foundry"), "blueprints-*.json").Single();
        internal string? Guard() => new NormalGameActionCoordinator(Store(), sessions).MinerEnvironmentForTest(Player, Journey);
        internal string? LandingGuard() => new NormalGameActionCoordinator(Store(), sessions).CompletedLandingEnvironmentForTest(Player, Journey);
        internal string? SavedLandingGuard() => new NormalGameActionCoordinator(Store(), sessions).SavedLandingRecoveryEnvironmentForTest(Player, Journey);
        internal Fixture(bool pending, bool miningOnly = false,bool unknownTail=false,bool workFailure=false)
        {
            sourceItem = miningOnly ? 2301 : 2304; destinationItem = miningOnly ? 2203 : 2101;
            GameMain.gameTick = 120; GameMain.history = new(); Player.factory = Factory; GameMain.SavedFactoryTestPlayer = Player;
            GameMain.data = new() { localLoadedPlanetFactory = Factory }; GameMain.localPlanet = new() { id = 402, factory = Factory };
            Factory.entityCount = pending ? 2 : 3; Factory.entityCursor = pending ? 3 : 4;
            Factory.prebuildCount = pending ? 1 : 0; Factory.prebuildCursor = 2;
            Factory.entityPool[1] = new() { id = 1, protoId = sourceItem, minerId = miningOnly ? 1 : 0, rot = new(0, 0, 0, 1) };
            Factory.entityPool[2] = new() { id = 2, protoId = destinationItem, powerNodeId = miningOnly ? 1 : 0, rot = new(0, 0, 0, 1) };
            if (pending) Factory.prebuildPool[1] = new() { id = 1 };
            else Factory.entityPool[3] = new() { id = 3, protoId = 2011, rot = new(0, 0, 0, 1) };
            Add(sourceItem, 1, false); Add(destinationItem, 2, false); Add(2011, 3, pending,unknownTail,workFailure);
            Player.mecha.constructionModule.droneEnabled = !pending;
            Connect(1, 0, pending ? -1 : 3, 0); Connect(pending ? -1 : 3, 1, 2, 0);
        }
        internal void Connect(int from, int fromSlot, int to, int toSlot)
        {
            Factory.TestPorts[(from, fromSlot)] = new() { ObjectId = from, Slot = fromSlot, IsOutput = true, OtherObjectId = to, OtherSlot = toSlot };
            Factory.TestPorts[(to, toSlot)] = new() { ObjectId = to, Slot = toSlot, IsOutput = false, OtherObjectId = from, OtherSlot = fromSlot };
        }
        internal void AddStation()
        {
            Factory.entityPool[4] = new() { id = 4, protoId = 2104, stationId = 1, rot = new(0, 0, 0, 1) };
            Factory.entityCursor = 5; Factory.entityCount++;
            var s = Factory.transport.stationPool[1] = new() { id = 1, entityId = 4, planetId = 402, gid = 1 };
            s.storage[0] = new() { itemId = 1013, count = 10, max = 100, remoteLogic = ELogisticStorage.Supply };
            s.storage[1] = new() { itemId = 1301, count = 12, max = 100, remoteOrder = -10, remoteLogic = ELogisticStorage.Demand };
            Add(2104, 4, false);
        }
        private void Add(int item, int entity, bool pending,bool unknownTail=false,bool workFailure=false)
        {
            var site = new BlueprintSiteSnapshot { SessionId = "synthetic-session", PlanetId = 402, CapturedAtGameTick = 100,
                BlueprintHash = "synthetic-approved-factory", NativeCheckPerformed = true, NativeCheckPassed = true,
                InventorySufficient = true, TechnologySatisfied = true, Objects = [new() { Index = 0, ItemId = item, Rotation = new() { W = 1 } }] };
            var step = new StructuredConstructionConnection();
            var binding = new StructuredConstructionBinding { Kind = item == 2011 ? "inserter" : "core", JourneyId = Journey,
                NativePlanHash = "sha256:" + new string('a', 64), Steps = [step] };
            if (item == 2301)
            {
                binding.Kind = "resource"; binding.ResourceNodeId = 7; binding.ResourceStateHash = "synthetic-resource";
                site.Objects[0].Parameters = [7];
            }
            if (item == 2011)
            {
                binding.SourceObjectId = step.InputObjectId = 1; binding.DestinationObjectId = step.OutputObjectId = 2;
                step.InputFromSlot = 0; step.InputToSlot = 0; step.OutputFromSlot = 1; step.OutputToSlot = 0;
                binding.Endpoints = [new() { ObjectId = 1, ItemId = sourceItem, Rotation = new() { W = 1 } },
                    new() { ObjectId = 2, ItemId = destinationItem, Rotation = new() { W = 1 } }];
            }
            var state = BlueprintBuildState.Create(site, structuredConstruction: binding);
            var c = state.ConstructionControl = StructuredConstructionControlTests.Control();
            var action = Guid.NewGuid().ToString(); c.NativeCreateActionId = action;
            state.Begin("synthetic-session", action, 1, 100); state.BeforeStructuredSubmission(101, 1, "before");
            state.ConfirmStructuredSubmission([1], 0, "after");
            if (!pending) state.ConfirmCompletion(0, entity, 110);
            c.RequestStop(workFailure?"construction_pre_player_stationary_binding_lost":unknownTail?"construction_complete_draining":"explicit_structured_cancellation", 110, c.StartedAtUtc.AddSeconds(1), workFailure); c.DispatchDisabled = true;
            if(unknownTail)
            {c.Phase="quarantined";state.LastEvidenceGameTick=112;state.Stop("outcome_unknown");Assert.True(Store().TryPut(state));return;}
            Assert.True(c.RecordDisabledNativeFactoryPass(111)); Assert.True(c.TryMarkPhysicalIdle(112, true, true));
            c.Phase = pending ? "paused_idle" : "completed_idle"; state.LastEvidenceGameTick = 112; state.Stop(pending ? "paused" : "completed");
            Assert.True(Store().TryPut(state));
        }
        internal void MakeUnknown()
        {
            Assert.True(Store().TryRead(out var states)); var state = states.Single(s => s.Site.Objects[0].ItemId == 2011);
            if (state.Phase == "completed")
            {
                // A real completed receipt cannot be rewritten through the
                // store. Corrupt input must not obtain a landing certificate.
                var document = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(StorePath));
                var row = document["builds"]!.Single(t => (string?)t!["buildId"] == state.BuildId);
                row!["phase"] = "outcome_unknown";
                File.WriteAllText(StorePath, document.ToString());
                return;
            }
            state.Stop("outcome_unknown"); Assert.True(Store().TryPut(state));
        }
        public void Dispose()
        {
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Spherewright", "saved-factory")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
