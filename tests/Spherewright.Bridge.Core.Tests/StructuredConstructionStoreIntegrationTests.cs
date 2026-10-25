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
