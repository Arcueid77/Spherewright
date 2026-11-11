using BepInEx.Logging;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Real production bookkeeping/store, substituted I/O security and session.
// No DSP invocation, Native Create, flight or construction acceptance.
public sealed class StructuredConstructionStoreIntegrationTests
{
    [Theory]
    [InlineData(2101, 51, 8, "technology", "click", false)]
    [InlineData(2101, 51, 8, "technology", "click", true)]
    [InlineData(2040, 257, null, "native_reach", "click", false)]
    [InlineData(2313, 120, null, "native_reach", "addon", false)]
    public void NativeStackPendingStoreRetainsModelAnchorAndSinglePaidSubmission(int itemId, int model, int? cap, string limit, string tool, bool attachment)
    {
        using var fixture = new Fixture();
        var site = Site(1, itemId);
        site.Objects[0].NativeModelIndex = model;
        var binding = Binding(1);
        binding.SourceObjectId = 7;
        binding.Endpoints.Add(new() { ObjectId = 7, ItemId = itemId });
        binding.Steps[0].NativeModelIndex = model;
        binding.Steps[0].InputObjectId = 7;
        binding.Steps[0].InputFromSlot = 15;
        binding.Steps[0].InputToSlot = 14;
        binding.StackConstruction = new Spherewright.Contracts.Factory.NativeStackConstructionPlan
        {
            SelectedObjectId = 7, RootObjectId = 7, AnchorObjectId = 7,
            BeforeStackStateHash = "sha256:synthetic-stack", BuildingItemId = itemId, ModelIndex = model,
            ExistingLayerCount = 1, ExistingObjectIds = new() { 7 }, MaximumLayers = cap,
            HeightLimitKind = limit, BuildToolKind = tool,
            ExistingAttachmentObjectIds = attachment ? new() { 8 } : new(),
        };
        var state = BlueprintBuildState.Create(site, structuredConstruction: binding);
        Start(state);
        state.BeforeStructuredSubmission(101, 3, "before");
        state.ConfirmStructuredSubmission(new[] { 20 }, 2, "after");
        Assert.True(fixture.Store().TryPut(state));
        var restored = Assert.Single(fixture.Read());
        Assert.Equal(model, restored.Site.Objects[0].NativeModelIndex);
        Assert.Equal(7, restored.StructuredConstruction!.StackConstruction!.AnchorObjectId);
        Assert.Equal(new[] { 7 }, restored.StructuredConstruction.StackConstruction.ExistingObjectIds);
        Assert.Equal(cap, restored.StructuredConstruction.StackConstruction.MaximumLayers);
        Assert.Equal(tool, restored.StructuredConstruction.StackConstruction.BuildToolKind);
        Assert.Equal(limit, restored.StructuredConstruction.StackConstruction.HeightLimitKind);
        Assert.Equal(attachment ? new[] { 8 } : Array.Empty<int>(), restored.StructuredConstruction.StackConstruction.ExistingAttachmentObjectIds);
        if (tool == "click" && limit == "technology")
        {
            Assert.DoesNotContain("buildToolKind", PluginJson.Serialize(restored.StructuredConstruction.StackConstruction));
            Assert.DoesNotContain("heightLimitKind", PluginJson.Serialize(restored.StructuredConstruction.StackConstruction));
        }
        if (!attachment)
            Assert.DoesNotContain("existingAttachmentObjectIds", PluginJson.Serialize(restored.StructuredConstruction.StackConstruction));
        Assert.Equal(20, restored.Objects[0].PrebuildId);
        Assert.Equal(3, restored.Objects[0].InventoryBefore);
        Assert.Equal(2, restored.Objects[0].InventoryAfter);
        Assert.Throws<InvalidOperationException>(() => restored.BeforeStructuredSubmission(102, 2, "replay"));
        restored.Site.Objects[0].NativeModelIndex = model + 1;
        Assert.False(fixture.Store().TryPut(restored));
    }

    [Fact]
    public void LaterNativeStopAdmitsOnlyTheSamePaidPendingBatchAndAppendsTheUnknownControlUnchanged()
    {
        using var fixture = new Fixture(); var state = Pending(2); var original = state.ConstructionControl!;
        Assert.DoesNotContain("savedPaidStops", PluginJson.Serialize(state));
        original.RequestStop("construction_pre_player_stationary_binding_lost", 110, original.StartedAtUtc.AddSeconds(1), true);
        original.DispatchDisabled = true; original.Phase = "quarantined"; state.LastEvidenceGameTick = 112; state.Stop("outcome_unknown");
        Assert.True(fixture.Store().TryPut(state)); var oldBytes = PluginJson.Serialize(original); var oldHash = original.EvidenceHash();
        state.ConfirmCompletion(0, 100, 121);
        var newSession = Guid.NewGuid().ToString();
        state.SavedPaidStops.Add(new() { OriginalActionId = original.NativeCreateActionId!, StoppedActionId = state.ActionId!,
            OriginalControlHash = oldHash, BuildPlanHash = state.PlanHash, PaidSubmissionHash = SavedPaidConstructionStop.SubmissionHash(state),
            NativeSessionId = newSession, SourceFingerprint = CanonicalStateHash.Combine("synthetic-saved-source"),
            SourceGameTick = 120, NativeFactoryPassGameTick = 122, PhysicalIdleGameTick = 123,
            CompletedAtStop = new() { new() { Index = 0, EntityId = 100 } } });
        state.LastEvidenceGameTick = 123; state.Generation++;
        Assert.True(fixture.Store().TryPut(state)); Assert.False(PaidFactoryCompletionPolicy.HasCompletedIdleProof(state, 123));
        Assert.Throws<InvalidOperationException>(() => state.Begin(newSession, Guid.NewGuid().ToString(), 2, 3800));
        var next = StructuredConstructionControlTests.Control(); next.StartedAtGameTick = 3800; next.WorkDeadlineGameTick = 7400;
        next.StartedAtUtc = original.WorkDeadlineUtc.AddSeconds(1); next.WorkDeadlineUtc = next.StartedAtUtc.AddSeconds(60);
        next.ObservationLeaseExpiresAtUtc = next.StartedAtUtc.AddSeconds(30); next.NativeCreateActionId = original.NativeCreateActionId;
        state.PreviousConstructionWindows.Add(original); state.ConstructionControl = next;
        Assert.Throws<InvalidOperationException>(() => state.BeginSavedPaidReadmission(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), 2, 3800));
        state.BeginSavedPaidReadmission(newSession, Guid.NewGuid().ToString(), 2, 3800);
        Assert.True(fixture.Store().TryPut(state)); var restored = Assert.Single(fixture.Read());
        Assert.Equal(oldBytes, PluginJson.Serialize(Assert.Single(restored.PreviousConstructionWindows)));
        Assert.Equal(oldHash, restored.SavedPaidStops.Single().OriginalControlHash);
        Assert.Equal(100, restored.Objects[0].EntityId); Assert.Equal(21, restored.Objects[1].PrebuildId);
        Assert.All(restored.Objects, o => { Assert.Equal(12, o.InventoryBefore); Assert.Equal(10, o.InventoryAfter); });
        Assert.Throws<InvalidOperationException>(() => restored.BeforeStructuredSubmission(3801, 10, "replay"));
        restored.SavedPaidStops[0].SourceFingerprint = CanonicalStateHash.Combine("replacement-source");
        Assert.False(fixture.Store().TryPut(restored));
    }

    [Theory]
    [InlineData("no-pass")][InlineData("not-idle")][InlineData("foreign-session")][InlineData("unpaid")][InlineData("completed-changed")]
    public void SavedPaidStopNeverBorrowsAnUnknownSubmissionOrUnprovedNativeBoundary(string fault)
    {
        var state = Pending(2); var c = state.ConstructionControl!;
        c.RequestStop("construction_pre_player_stationary_binding_lost", 110, c.StartedAtUtc.AddSeconds(1), true);
        c.DispatchDisabled = true; c.Phase = "quarantined"; state.LastEvidenceGameTick = 112; state.Stop("outcome_unknown");
        state.ConfirmCompletion(0, 100, 121); state.LastEvidenceGameTick = 123;
        var p = new SavedPaidConstructionStop { OriginalActionId = c.NativeCreateActionId!, StoppedActionId = state.ActionId!,
            OriginalControlHash = c.EvidenceHash(), BuildPlanHash = state.PlanHash, PaidSubmissionHash = SavedPaidConstructionStop.SubmissionHash(state),
            NativeSessionId = Guid.NewGuid().ToString(), SourceFingerprint = CanonicalStateHash.Combine("source"),
            SourceGameTick = 120, NativeFactoryPassGameTick = 122, PhysicalIdleGameTick = 123,
            CompletedAtStop = new() { new() { Index = 0, EntityId = 100 } } };
        Assert.True(p.Matches(state, 123));
        switch (fault)
        {
            case "no-pass": p.NativeFactoryPassGameTick = 120; break;
            case "not-idle": p.PhysicalIdleGameTick = 121; break;
            case "foreign-session": p.NativeSessionId = "unproved"; break;
            case "unpaid": state.Objects[1].InventoryAfter = 11; break;
            case "completed-changed": state.Objects[0].EntityId = 101; break;
        }
        Assert.False(p.Matches(state, 123)); Assert.False(c.CanReadmitPaidPending(3800, c.WorkDeadlineUtc.AddSeconds(1)));
    }
    [Fact]
    public void EmptySourceExtensionPersistsOnlyTheNewDebitAndNeverRecreatesItsPaidSourceOrPendingBatch()
    {
        using var fixture = new Fixture();
        var (binding, _) = StructuredEmptyBeltSourceTests.Scenario();
        var state = BlueprintBuildState.Create(Site(2, 2001), structuredConstruction: binding);
        var original = state.StructuredConstruction!.ImmutableHash(); Start(state);
        state.BeforeStructuredSubmission(101, 12, "before-two-new");
        state.ConfirmStructuredSubmission(new[] { 20, 21 }, 10, "paid-two-new-only");
        Assert.True(fixture.Store().TryPut(state));
        var restored = Assert.Single(fixture.Read());
        Assert.Equal(original, restored.StructuredConstruction!.ImmutableHash());
        Assert.Equal(9, restored.StructuredConstruction.EmptyBeltSource!.ObjectId);
        Assert.Equal(new int?[] { 20, 21 }, restored.Objects.Select(o => o.PrebuildId));
        Assert.All(restored.Objects, o => { Assert.Equal(12, o.InventoryBefore); Assert.Equal(10, o.InventoryAfter); });
        Assert.Throws<InvalidOperationException>(() => restored.BeforeStructuredSubmission(102, 10, "replay"));
        restored.StructuredConstruction.EmptyBeltSource.CompletionTopologyHash = CanonicalStateHash.Combine("tampered-source");
        Assert.False(fixture.Store().TryPut(restored));
    }
    [Fact]
    public void SafelyStoppedPaidBatchGetsDistinctWindowWithoutRewritingOldWindowOrDebit()
    {
        using var fixture = new Fixture();
        var state = Pending(2);
        ProvePhysicalDrain(state, 110, 112); state.ConstructionControl!.Phase = "paused_idle"; state.Stop("paused");
        Assert.True(fixture.Store().TryPut(state));
        var old = state.ConstructionControl; var oldHash = old.EvidenceHash();
        var resumed = Clone(state);
        resumed.PreviousConstructionWindows.Add(resumed.ConstructionControl!);
        resumed.ConstructionControl = StructuredConstructionControlTests.Control();
        resumed.ConstructionControl.StartedAtGameTick = 3800;
        resumed.ConstructionControl.WorkDeadlineGameTick = 7400;
        resumed.ConstructionControl.StartedAtUtc = old.WorkDeadlineUtc.AddSeconds(1);
        resumed.ConstructionControl.WorkDeadlineUtc = old.WorkDeadlineUtc.AddSeconds(61);
        resumed.ConstructionControl.ObservationLeaseExpiresAtUtc = old.WorkDeadlineUtc.AddSeconds(31);
        resumed.ConstructionControl.NativeCreateActionId = old.NativeCreateActionId;
        resumed.ConstructionControl.OriginalDroneEnabled = old.OriginalDroneEnabled;
        resumed.ConstructionControl.PreexistingEntityIds = old.PreexistingEntityIds.ToList();
        resumed.Begin("synthetic-new-session", Guid.NewGuid().ToString(), 2, 3800);
        Assert.True(fixture.Store().TryPut(resumed));
        var retained = Assert.Single(fixture.Read());
        Assert.Equal(oldHash, Assert.Single(retained.PreviousConstructionWindows).EvidenceHash());
        Assert.Equal(3800, retained.ConstructionControl!.StartedAtGameTick);
        Assert.Equal(state.Objects.Select(o => o.PrebuildId), retained.Objects.Select(o => o.PrebuildId));
        Assert.Equal(state.Objects.Select(o => o.AfterInventoryHash), retained.Objects.Select(o => o.AfterInventoryHash));
        Assert.Throws<InvalidOperationException>(() => retained.BeforeStructuredSubmission(3801, 12, "replay"));
        retained.PreviousConstructionWindows[0].WorkDeadlineGameTick++;
        Assert.False(fixture.Store().TryPut(retained));
    }

    [Theory]
    [InlineData(2302)] [InlineData(2304)] [InlineData(2309)] [InlineData(2101)]
    [InlineData(2106)] [InlineData(2211)] [InlineData(2311)] [InlineData(2312)] [InlineData(2208)]
    public void PaidFactoryCoreKeepsItsOriginalPrebuildAndDebitAcrossStoreRestart(int itemId)
    {
        using var fixture = new Fixture();
        var state = BlueprintBuildState.Create(Site(1, itemId), structuredConstruction: Binding(1));
        Start(state); state.BeforeStructuredSubmission(101, 1, "before-one-device");
        state.ConfirmStructuredSubmission(new[] { 20 }, 0, "paid-once");
        Assert.True(fixture.Store().TryPut(state));
        var resumed = Assert.Single(fixture.Read());
        Assert.Equal(state.BuildId, resumed.BuildId);
        Assert.Equal(20, resumed.Objects[0].PrebuildId);
        Assert.Equal(1, resumed.Objects[0].InventoryBefore);
        Assert.Equal(0, resumed.Objects[0].InventoryAfter);
        Assert.Throws<InvalidOperationException>(() => resumed.BeforeStructuredSubmission(102, 1, "replay"));
        resumed.ConfirmCompletion(0, 100, 110); ProvePhysicalDrain(resumed, 110, 112);
        resumed.ConstructionControl!.Phase = "completed_idle"; resumed.Stop("completed");
        Assert.True(fixture.Store().TryPut(resumed));
        var completed = Assert.Single(fixture.Read());
        Assert.Equal(100, completed.Objects[0].EntityId);
        Assert.Equal("paid-once", completed.Objects[0].AfterInventoryHash);
        Assert.Throws<InvalidOperationException>(() => completed.BeforeStructuredSubmission(120, 1, "recreate"));
    }

    [Fact]
    public void FactoryReceiptCapacityNeverEvictsAnOlderPaidBuild()
    {
        using var fixture = new Fixture();
        var states = Enumerable.Range(0, BlueprintBuildStore.MaximumBuilds)
            .Select(_ => BlueprintBuildState.Create(Site(1, 2304), structuredConstruction: Binding(1))).ToArray();
        foreach (var state in states) Assert.True(fixture.Store().TryPut(state));
        Assert.False(fixture.Store().TryPut(BlueprintBuildState.Create(Site(1, 2304), structuredConstruction: Binding(1))));
        var retained = fixture.Read();
        Assert.Equal(BlueprintBuildStore.MaximumBuilds, retained.Count);
        Assert.Equal(states.Select(s => s.BuildId).OrderBy(s => s), retained.Select(s => s.BuildId).OrderBy(s => s));
    }

    [Fact]
    public void TwelveBeltBatchKeepsOneExactDebitTopologyAndPhysicalCompletionAcrossRestart()
    {
        using var fixture = new Fixture();
        var binding = Binding(12); binding.Kind = "belt";
        for (var i = 0; i < 12; i++)
        {
            binding.Steps[i].InputStepIndex = i == 0 ? -1 : i - 1;
            binding.Steps[i].OutputStepIndex = i == 11 ? -1 : i + 1;
        }
        var site = Site(12, 2001); site.NativeBlueprintObjectLimit = 12;
        var state = BlueprintBuildState.Create(site, structuredConstruction: binding); Start(state);
        state.BeforeStructuredSubmission(101, 13, "before-twelve-belts");
        Assert.True(fixture.Store().TryPut(state));
        var ids = Enumerable.Range(20, 12).ToArray();
        var underpaid = Clone(state);
        Assert.Throws<InvalidOperationException>(() => underpaid.ConfirmStructuredSubmission(ids, 2, "underpaid"));
        state.ConfirmStructuredSubmission(ids, 1, "exact-paid");
        Assert.True(fixture.Store().TryPut(state));
        var restored = Assert.Single(fixture.Read());
        Assert.Equal(binding.ImmutableHash(), restored.StructuredConstruction!.ImmutableHash());
        Assert.All(restored.Objects, o => { Assert.Equal(13, o.InventoryBefore); Assert.Equal(1, o.InventoryAfter); });
        for (var i = 0; i < 12; i++) restored.ConfirmCompletion(i, 100 + i, 110);
        ProvePhysicalDrain(restored, 110, 112); restored.ConstructionControl!.Phase = "completed_idle"; restored.Stop("completed");
        Assert.True(fixture.Store().TryPut(restored));
        Assert.Equal("completed", Assert.Single(fixture.Read()).Phase);
        Assert.Throws<InvalidOperationException>(() => restored.BeforeStructuredSubmission(120, 13, "replay"));
    }

    [Theory]
    [InlineData(13, 2001, "belt")]
    [InlineData(9, 2201, "core")]
    [InlineData(12, 2203, "core")]
    public void BeltCapacityDoesNotExpandOtherItemsOrPermitThirteenObjects(int count, int item, string kind)
    {
        var binding = Binding(count); binding.Kind = kind;
        Assert.Throws<InvalidDataException>(() => BlueprintBuildState.Create(Site(count, item), structuredConstruction: binding));
    }

    [Theory]
    [InlineData(1)] [InlineData(8)]
    public void OneWholeBatchDebitSurvivesRestartAndPendingResumeWithoutAnotherSubmission(int count)
    {
        using var fixture = new Fixture();
        var state = Create(count); Start(state);
        state.BeforeStructuredSubmission(101, count + 10, "synthetic-before");
        Assert.True(fixture.Store().TryPut(state));
        var ids = Enumerable.Range(20, count).ToArray();
        state.ConfirmStructuredSubmission(ids, 10, "synthetic-after");
        Assert.True(fixture.Store().TryPut(state));
        var restored = Assert.Single(fixture.Read());
        foreach (var receipt in restored.Objects)
        {
            Assert.Equal(count + 10, receipt.InventoryBefore);
            Assert.Equal(10, receipt.InventoryAfter); // Shared count, not count separate debits.
        }
        var originalCreate = restored.ConstructionControl!.NativeCreateActionId;
        ProvePhysicalDrain(restored, 110, 112);
        restored.ConstructionControl.Phase = "paused_idle";
        restored.Stop("paused");
        Assert.True(fixture.Store().TryPut(restored));
        restored.ConstructionControl = StructuredConstructionControlTests.Control();
        restored.ConstructionControl.NativeCreateActionId = originalCreate;
        restored.Begin("synthetic-session", Guid.NewGuid().ToString(), count, 120);
        Assert.Equal(originalCreate, restored.ConstructionControl.NativeCreateActionId);
        Assert.Null(restored.NextReadyIndex());
        Assert.Throws<InvalidOperationException>(() => restored.BeforeStructuredSubmission(120, 10, "second-debit"));
        Assert.True(fixture.Store().TryPut(restored));
        Assert.Equal(ids, Assert.Single(fixture.Read()).Objects.Select(o => o.PrebuildId!.Value));
    }

    [Theory]
    [InlineData("unknown_ids")] [InlineData("duplicate_ids")] [InlineData("partial_debit")]
    public void CrashAfterWriteAheadCannotInventNativeReceiptsOrRecreateTheBatch(string failure)
    {
        using var fixture = new Fixture();
        var state = Create(2); Start(state); state.BeforeStructuredSubmission(101, 12, "before");
        Assert.True(fixture.Store().TryPut(state));
        var bytes = File.ReadAllBytes(fixture.Path);
        var ids = failure == "unknown_ids" ? Array.Empty<int>() : failure == "duplicate_ids" ? new[] { 1, 1 } : new[] { 1, 2 };
        Assert.Throws<InvalidOperationException>(() => state.ConfirmStructuredSubmission(ids,
            failure == "partial_debit" ? 11 : 10, "after"));
        var restored = Assert.Single(fixture.Read());
        Assert.All(restored.Objects, o => Assert.Equal(BlueprintObjectStates.Submitting, o.State));
        Assert.Throws<InvalidOperationException>(() => restored.Begin("new-session", Guid.NewGuid().ToString(), 2, 200));
        Assert.Throws<InvalidOperationException>(() => restored.BeforeStructuredSubmission(200, 12, "second-attempt"));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
    }

    [Theory]
    [InlineData("tick_window")] [InlineData("utc_window")] [InlineData("start_tick")]
    [InlineData("start_utc")] [InlineData("create_action")] [InlineData("before_inventory")]
    [InlineData("before_hash")] [InlineData("submit_tick")] [InlineData("prebuild")]
    [InlineData("after_hash")] [InlineData("old_entities")]
    public void DurableOriginalWindowAndSubmissionCannotBeReplacedByResume(string mutation)
    {
        using var fixture = new Fixture();
        var state = Pending(2); Assert.True(fixture.Store().TryPut(state));
        var bytes = File.ReadAllBytes(fixture.Path);
        var changed = Clone(state); changed.Generation++;
        var c = changed.ConstructionControl!;
        switch (mutation)
        {
            case "tick_window": c.WorkDeadlineGameTick++; break;
            case "utc_window": c.WorkDeadlineUtc = c.WorkDeadlineUtc.AddTicks(1); break;
            case "start_tick": c.StartedAtGameTick++; break;
            case "start_utc": c.StartedAtUtc = c.StartedAtUtc.AddTicks(1); break;
            case "create_action": c.NativeCreateActionId = Guid.NewGuid().ToString(); break;
            case "before_inventory": changed.Objects.ForEach(o => { o.InventoryBefore++; o.InventoryAfter++; }); break;
            case "before_hash": changed.Objects[0].BeforeInventoryHash = "replacement"; break;
            case "submit_tick": changed.Objects[0].SubmittedAtGameTick = 100; break;
            case "prebuild": changed.Objects[0].PrebuildId = 99; break;
            case "after_hash": changed.Objects[0].AfterInventoryHash = "replacement"; break;
            case "old_entities": c.PreexistingEntityIds.Add(123); break;
        }
        Assert.False(fixture.Store().TryPut(changed));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
    }

    [Fact]
    public void BeforeOnlyWriteAheadFactsAreImmutableEvenBeforeNativeIdsAreKnown()
    {
        using var fixture = new Fixture();
        var state = Create(2); Start(state); state.BeforeStructuredSubmission(101, 12, "before");
        Assert.True(fixture.Store().TryPut(state));
        var bytes = File.ReadAllBytes(fixture.Path);
        state.Objects.ForEach(o => o.InventoryBefore = 14); state.Generation++;
        Assert.False(fixture.Store().TryPut(state));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
    }

    [Fact]
    public void ExactAllEntitiesWithoutPhysicalDroneIdleRemainIncompleteAcrossRestart()
    {
        using var fixture = new Fixture(); var state = Pending(2);
        state.ConfirmCompletion(0, 100, 110); state.ConfirmCompletion(1, 101, 111);
        Assert.True(fixture.Store().TryPut(state));
        state.Stop("completed");
        Assert.Throws<InvalidDataException>(state.Validate);
        ProvePhysicalDrain(state, 111, 112);
        state.ConstructionControl!.Phase = "completed_idle";
        Assert.True(fixture.Store().TryPut(state));
        var restored = Assert.Single(fixture.Read());
        Assert.Equal("completed", restored.Phase);
        Assert.Throws<InvalidOperationException>(() => restored.Begin("new-session", Guid.NewGuid().ToString(), 2, 200));
        Assert.Throws<InvalidOperationException>(() => restored.BeforeStructuredSubmission(200, 10, "replay"));
    }

    [Fact]
    public void CompletedOriginalDroneRestorationSurvivesDurableStoreAndRestart()
    {
        using var fixture = new Fixture(); var state = Pending(1);
        state.ConfirmCompletion(0, 100, 110);
        ProvePhysicalDrain(state, 110, 112);
        var control = state.ConstructionControl!; control.Phase = "completed_idle";
        state.Stop("completed");
        var store = fixture.Store(); Assert.True(store.TryPut(state));
        var before = Assert.Single(fixture.Read());
        Assert.True(control.MayRestoreOriginalDroneSetting(true, true, true, false));

        control.DispatchDisabled = false; state.Generation++;
        Assert.True(store.TryPut(state));

        var restored = Assert.Single(fixture.Read());
        var beforeControl = before.ConstructionControl!;
        var restoredControl = restored.ConstructionControl!;
        Assert.Equal("completed", restored.Phase);
        Assert.Equal(before.PlanHash, restored.PlanHash);
        Assert.Equal(before.ActionId, restored.ActionId);
        Assert.False(restoredControl.DispatchDisabled);
        Assert.True(restoredControl.RetainsOriginalWindowAndSubmission(beforeControl));
        Assert.Equal(beforeControl.StopReason, restoredControl.StopReason);
        Assert.Equal(beforeControl.NativeCreateActionId, restoredControl.NativeCreateActionId);
        Assert.Equal(beforeControl.WorkDeadlineGameTick, restoredControl.WorkDeadlineGameTick);
        Assert.Equal(beforeControl.WorkDeadlineUtc, restoredControl.WorkDeadlineUtc);
        Assert.Equal(110, restoredControl.DisabledNativeFactoryPassGameTick);
        Assert.Equal(112, restoredControl.PhysicalIdleVerifiedAtGameTick);
        Assert.Equal(before.Objects[0].PrebuildId, restored.Objects[0].PrebuildId);
        Assert.Equal(before.Objects[0].EntityId, restored.Objects[0].EntityId);
        Assert.Equal(before.Objects[0].InventoryBefore, restored.Objects[0].InventoryBefore);
        Assert.Equal(before.Objects[0].InventoryAfter, restored.Objects[0].InventoryAfter);
        Assert.Throws<InvalidOperationException>(() => restored.BeforeStructuredSubmission(200, 11, "replay"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompletedIdleControlCannotLabelAPausedBatchAsCompleted(bool completedObject)
    {
        var state = Pending(1);
        if (completedObject) state.ConfirmCompletion(0, 100, 110);
        ProvePhysicalDrain(state, 110, 112);
        state.ConstructionControl!.Phase = "completed_idle";
        state.Stop("paused");

        Assert.Throws<InvalidDataException>(state.Validate);
    }

    [Theory]
    [InlineData("working")] [InlineData("cancelled")] [InlineData("original_disabled")]
    [InlineData("binding_lost")] [InlineData("missing_idle")] [InlineData("missing_pass")]
    [InlineData("pass_after_idle")]
    public void ReenabledDispatchWithoutSealedOriginalCompletionCannotReplaceDurableEvidence(string fault)
    {
        using var fixture = new Fixture(); var state = Pending(1);
        state.ConfirmCompletion(0, 100, 110);
        ProvePhysicalDrain(state, 110, 112);
        state.ConstructionControl!.Phase = "completed_idle"; state.Stop("completed");
        var store = fixture.Store(); Assert.True(store.TryPut(state));
        var bytes = File.ReadAllBytes(fixture.Path); var changed = Clone(state); changed.Generation++;
        var control = changed.ConstructionControl!; control.DispatchDisabled = false;
        switch (fault)
        {
            case "working": control.Phase = "working"; break;
            case "cancelled": control.StopReason = "explicit_structured_cancellation"; break;
            case "original_disabled": control.OriginalDroneEnabled = false; break;
            case "binding_lost": control.BindingLost = true; break;
            case "missing_idle": control.PhysicalIdleVerified = false; control.PhysicalIdleVerifiedAtGameTick = null; break;
            case "missing_pass": control.DisabledNativeFactoryPassGameTick = null; break;
            case "pass_after_idle": control.DisabledNativeFactoryPassGameTick = 113; break;
        }

        Assert.False(store.TryPut(changed));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
    }

    [Theory]
    [InlineData(0)] [InlineData(9)]
    public void StructuredBatchCannotExpandBeyondItsEightObjectTask(int count) =>
        Assert.Throws<InvalidDataException>(() => Create(count));

    [Fact]
    public void ResourceBindingDoesNotExpandGenericBlueprintSyntaxOrPermitNodeSubstitution()
    {
        var site = Site(1, 2301); site.Objects[0].Parameters = new[] { 7 };
        var binding = Binding(1); binding.Kind = "resource"; binding.ResourceNodeId = 7; binding.ResourceStateHash = "native-resource-state";
        var state = BlueprintBuildState.Create(site, structuredConstruction: binding);
        Assert.False(BoundedBlueprintReader.SupportsItem(2301));
        state.StructuredConstruction!.ResourceNodeId = 8;
        Assert.Throws<InvalidDataException>(state.Validate);
    }

    [Fact]
    public void UnknownOutcomeStaysBoundToOriginalBatchAndCannotBeMadeFreshByAnActionRead()
    {
        var state = Pending(1); var id = state.BuildId; var plan = state.PlanHash;
        state.ConstructionControl!.RequestStop("lost_order", 120,
            state.ConstructionControl.StartedAtUtc.AddSeconds(2), true);
        state.ConstructionControl.Phase = "quarantined"; state.Stop("outcome_unknown");
        var restored = Clone(state);
        Assert.Equal(id, restored.BuildId); Assert.Equal(plan, restored.PlanHash);
        Assert.Null(restored.NextReadyIndex());
        Assert.Throws<InvalidOperationException>(() => restored.Begin("another-session", Guid.NewGuid().ToString(), 1, 200));
        Assert.False(restored.ConstructionControl!.RenewObservationLease(restored.ConstructionControl.StartedAtUtc.AddSeconds(3)));
    }

    [Fact]
    public void CachedNominationAndActiveTailStayDurableUntilAnExplicitNativePassAndPhysicalIdle()
    {
        using var fixture = new Fixture(); var state = Pending(1);
        state.ConfirmCompletion(0, 100, 110);
        var c = state.ConstructionControl!;
        c.RequestStop("construction_complete_draining", 110, c.StartedAtUtc.AddSeconds(1), false);
        c.DispatchDisabled = true; state.Generation++;
        Assert.True(fixture.Store().TryPut(state));
        var restored = Assert.Single(fixture.Read());
        c = restored.ConstructionControl!;
        Assert.False(c.TryMarkPhysicalIdle(110, false, true)); // Cached nomination, still idle counts.
        Assert.False(c.TryMarkPhysicalIdle(111, true, true)); // Tick advancement without a real pass is insufficient.
        Assert.True(c.RecordDisabledNativeFactoryPass(111));
        restored.LastEvidenceGameTick = 111; restored.Generation++;
        Assert.True(fixture.Store().TryPut(restored));
        restored = Assert.Single(fixture.Read()); c = restored.ConstructionControl!;
        Assert.False(c.TryMarkPhysicalIdle(112, true, false)); // Cache consumed into a Native stage-one tail.
        Assert.False(c.RenewObservationLease(c.StartedAtUtc.AddSeconds(2)));
        Assert.True(c.TryMarkPhysicalIdle(160, true, true));
        restored.LastEvidenceGameTick = 160; restored.Generation++;
        c.Phase = "completed_idle"; restored.Stop("completed");
        Assert.True(fixture.Store().TryPut(restored));
        var completed = Assert.Single(fixture.Read());
        Assert.Equal("completed", completed.Phase);
        Assert.Equal(110, completed.ConstructionControl!.DrainStartedAtGameTick);
        Assert.Equal(111, completed.ConstructionControl.DisabledNativeFactoryPassGameTick);
        Assert.Equal(160, completed.ConstructionControl.PhysicalIdleVerifiedAtGameTick);
    }

    [Theory]
    [InlineData("no_pass")] [InlineData("pass_after_watermark")] [InlineData("same_tick_idle")]
    [InlineData("idle_after_watermark")] [InlineData("replace_pass")] [InlineData("renew_stop")]
    public void DurableDrainCannotInventAFactoryPassOrRenewItsOriginalCleanupClock(string mutation)
    {
        using var fixture = new Fixture(); var state = Pending(1);
        ProvePhysicalDrain(state, 110, 112); state.ConstructionControl!.Phase = "paused_idle";
        state.Stop("paused"); Assert.True(fixture.Store().TryPut(state));
        var before = File.ReadAllBytes(fixture.Path); var changed = Clone(state); changed.Generation++;
        var c = changed.ConstructionControl!;
        switch (mutation)
        {
            case "no_pass": c.DisabledNativeFactoryPassGameTick = null; break;
            case "pass_after_watermark": c.DisabledNativeFactoryPassGameTick = 113; break;
            case "same_tick_idle": c.PhysicalIdleVerifiedAtGameTick = 110; break;
            case "idle_after_watermark": c.PhysicalIdleVerifiedAtGameTick = 113; break;
            case "replace_pass": c.DisabledNativeFactoryPassGameTick = 111; break;
            case "renew_stop": c.DrainStartedAtGameTick++; c.DrainStartedAtUtc = c.DrainStartedAtUtc!.Value.AddTicks(1); break;
        }
        Assert.False(fixture.Store().TryPut(changed));
        Assert.Equal(before, File.ReadAllBytes(fixture.Path));
    }

    private static void ProvePhysicalDrain(BlueprintBuildState state, long stopTick, long idleTick)
    {
        var c = state.ConstructionControl!;
        c.RequestStop("construction_complete_draining", stopTick, c.StartedAtUtc.AddSeconds(1), false);
        c.DispatchDisabled = true;
        Assert.True(c.RecordDisabledNativeFactoryPass(stopTick));
        Assert.True(c.TryMarkPhysicalIdle(idleTick, true, true));
        state.LastEvidenceGameTick = idleTick; state.Generation++;
    }

    private static BlueprintBuildState Pending(int count)
    {
        var s = Create(count); Start(s); s.BeforeStructuredSubmission(101, count + 10, "before");
        s.ConfirmStructuredSubmission(Enumerable.Range(20, count).ToArray(), 10, "after"); return s;
    }
    private static void Start(BlueprintBuildState state)
    {
        state.ConstructionControl = StructuredConstructionControlTests.Control();
        var action = Guid.NewGuid().ToString(); state.ConstructionControl.NativeCreateActionId = action;
        state.Begin("synthetic-session", action, state.Objects.Count, 100);
    }
    private static BlueprintBuildState Create(int count) => BlueprintBuildState.Create(Site(count), structuredConstruction: Binding(count));
    private static BlueprintBuildState Clone(BlueprintBuildState state) =>
        PluginJson.DeserializeReplacingCollections<BlueprintBuildState>(PluginJson.Serialize(state))!;
    private static StructuredConstructionBinding Binding(int count) => new()
    {
        Kind = "core", JourneyId = Guid.NewGuid().ToString(), NativePlanHash = "sha256:" + new string('a', 64),
        Steps = Enumerable.Range(0, count).Select(_ => new StructuredConstructionConnection()).ToList(),
    };
    private static BlueprintSiteSnapshot Site(int count, int item = 2201) => new()
    {
        SessionId = "synthetic-session", PlanetId = 104, CapturedAtGameTick = 100, BlueprintHash = "synthetic-native-approved",
        NativeCheckPerformed = true, NativeCheckPassed = true, InventorySufficient = true, TechnologySatisfied = true,
        Objects = Enumerable.Range(0, count).Select(i => new BlueprintSiteObject { Index = i, ItemId = item, NativeCondition = "Ok" }).ToList(),
    };

    private sealed class Fixture : IDisposable
    {
        private const string Identity = "synthetic-structured-owned-identity";
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Spherewright", "structured-store", Guid.NewGuid().ToString("N"));
        private readonly GameSessionTracker _sessions = new() { IsCurrentSessionOwned = true, OwnedSaveName = Identity, SessionId = "synthetic-session" };
        internal string Path => System.IO.Path.Combine(_root, "foundry", "blueprints-"
            + CanonicalStateHash.Combine("spherewright-blueprint-progress-owned-v1", Identity).Substring(7) + ".json");
        internal BlueprintBuildStore Store() => new(_root, "0.10.35.29151", _sessions, new ManualLogSource());
        internal IReadOnlyList<BlueprintBuildState> Read()
        { var s = Store(); Assert.True(s.TryRead(out var builds)); return builds; }
        public void Dispose()
        {
            _sessions.Dispose();
            var parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Spherewright", "structured-store"))
                + System.IO.Path.DirectorySeparatorChar;
            if (!System.IO.Path.GetFullPath(_root).StartsWith(parent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup target escaped its owned parent.");
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
