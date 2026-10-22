using BepInEx.Logging;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Links the actual journey/checkpoint store and recovery coordinator. Synthetic save
// prefixes and session/native substitutes do not prove Unity, real flight, normal
// save/load acceptance, Windows ACLs, or a usable in-flight controller.
[Collection("Owned resume native substitutes")]
public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    [Fact]
    public void ReopenPreservesOriginalDeadlineSpentTicksAndAllReturnReservations()
    {
        using var fixture = new Fixture();
        var original = fixture.Store.VoyagerJourney!;
        var reopened = fixture.ReopenStore();
        Assert.Null(reopened.VoyagerJourneyLoadError);
        Assert.Equal(PluginJson.Serialize(original), PluginJson.Serialize(reopened.VoyagerJourney));
        var callerCopy = reopened.VoyagerJourney!;
        callerCopy.ReturnEnergyFloorJoules = 1d;
        callerCopy.DeadlineUtc = callerCopy.DeadlineUtc.AddHours(1);
        Assert.Equal(original.ReturnEnergyFloorJoules, reopened.VoyagerJourney!.ReturnEnergyFloorJoules);
        Assert.Equal(original.DeadlineUtc, reopened.VoyagerJourney!.DeadlineUtc);
        Assert.Equal(original.SpentGameTicks, reopened.VoyagerJourney!.SpentGameTicks);
    }

    [Fact]
    public void MonotonicProgressPreservesTheOriginalReserveAndDeadlineAcrossRestart()
    {
        using var fixture = new Fixture();
        var update = fixture.Store.VoyagerJourney!;
        var deadline = update.DeadlineUtc;
        var returnFloor = update.ReturnEnergyFloorJoules;
        update.SpentGameTicks += 600;
        var originalPhase = update.Phase;
        Assert.True(fixture.Store.TryPersistVoyagerJourney(update, out var rejection), rejection);
        var restored = fixture.ReopenStore().VoyagerJourney!;
        Assert.Equal(update.SpentGameTicks, restored.SpentGameTicks);
        Assert.Equal(originalPhase, restored.Phase);
        Assert.Equal(deadline, restored.DeadlineUtc);
        Assert.Equal(returnFloor, restored.ReturnEnergyFloorJoules);
    }

    [Theory]
    [InlineData("new_journey")]
    [InlineData("owned_save")]
    [InlineData("home_planet")]
    [InlineData("home_star")]
    [InlineData("destination_star")]
    [InlineData("started_tick")]
    [InlineData("issued")]
    [InlineData("deadline")]
    [InlineData("maximum_ticks")]
    [InlineData("return_ticks")]
    [InlineData("spent_ticks")]
    [InlineData("return_energy")]
    [InlineData("withdrawal_energy")]
    [InlineData("return_core")]
    [InlineData("extra_warpers")]
    public void ActiveJourneyCannotRefreshItsIdentityDeadlineOrReplenishSpentBudget(string changed)
    {
        using var fixture = new Fixture();
        var durableBefore = File.ReadAllBytes(fixture.JourneyPath);
        var original = fixture.Store.VoyagerJourney!;
        var update = fixture.Store.VoyagerJourney!;
        switch (changed)
        {
            case "new_journey": update.JourneyId = Guid.NewGuid().ToString("N"); update.Phase = "outbound";
                update.ReturnLeg = false; update.SpentGameTicks = 0; update.RemotePlanetId = null; update.LastLandedGameTick = null; break;
            case "owned_save": update.OwnedSaveName = "different-synthetic-owned-save"; break;
            case "home_planet": update.HomePlanetId++; break;
            case "home_star": update.HomeStarId = 3; break;
            case "destination_star": update.DestinationStarId = 3; break;
            case "started_tick": update.StartedAtGameTick++; break;
            case "issued": update.IssuedAtUtc = update.IssuedAtUtc.AddSeconds(1); break;
            case "deadline": update.DeadlineUtc = update.DeadlineUtc.AddSeconds(1); break;
            case "maximum_ticks": update.MaximumGameTicks++; break;
            case "return_ticks": update.ReturnReserveGameTicks--; break;
            case "spent_ticks": update.SpentGameTicks--; break;
            case "return_energy": update.ReturnEnergyFloorJoules--; break;
            case "withdrawal_energy": update.WithdrawalReserveJoules--; break;
            case "return_core": update.ReturnCoreEnergyMinimumJoules--; break;
            case "extra_warpers": update.ExtraWarperReserve--; break;
            default: throw new ArgumentException(nameof(changed));
        }
        Assert.False(fixture.Store.TryPersistVoyagerJourney(update, out var rejection));
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(durableBefore, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal(PluginJson.Serialize(original), PluginJson.Serialize(fixture.Store.VoyagerJourney));
        Assert.Equal(PluginJson.Serialize(original), PluginJson.Serialize(fixture.ReopenStore().VoyagerJourney));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("null")]
    [InlineData("oversized")]
    [InlineData("version")]
    [InlineData("nonfinite_floor")]
    public void CorruptOrUnknownJourneyStaysOnDiskAndCannotBeSilentlyReplaced(string corruption)
    {
        using var fixture = new Fixture();
        var original = fixture.Store.VoyagerJourney!;
        var content = corruption switch
        {
            "json" => "{broken",
            "null" => "null",
            "oversized" => new string('x', 65537),
            "version" => PluginJson.Serialize(Copy(original, record => record.Version = 99)),
            "nonfinite_floor" => PluginJson.Serialize(Copy(original, record => record.ReturnEnergyFloorJoules = double.NaN)),
            _ => throw new ArgumentException(nameof(corruption)),
        };
        File.WriteAllText(fixture.JourneyPath, content);
        var reopened = fixture.ReopenStore();
        Assert.Null(reopened.VoyagerJourney);
        Assert.False(string.IsNullOrWhiteSpace(reopened.VoyagerJourneyLoadError));
        Assert.False(reopened.TryPersistVoyagerJourney(original, out var rejection));
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(content, File.ReadAllText(fixture.JourneyPath));
        Assert.False(string.IsNullOrWhiteSpace(reopened.VoyagerJourneyLoadError));
    }

    [Fact]
    public void CorruptionDuringTheSameProcessCannotBeOverwrittenByItsCachedCopy()
    {
        using var fixture = new Fixture();
        var update = fixture.Store.VoyagerJourney!;
        update.SpentGameTicks++;
        File.WriteAllText(fixture.JourneyPath, "{truncated-original-journey");
        Assert.False(fixture.Store.TryPersistVoyagerJourney(update, out _));
        Assert.Equal("{truncated-original-journey", File.ReadAllText(fixture.JourneyPath));
        Assert.False(string.IsNullOrWhiteSpace(fixture.Store.VoyagerJourneyLoadError));
    }

    [Fact]
    public void DirectoryAtTheRecordPathIsUnreadableEvidenceRatherThanNoJourney()
    {
        using var fixture = new Fixture();
        var original = fixture.Store.VoyagerJourney!;
        File.Delete(fixture.JourneyPath);
        Directory.CreateDirectory(fixture.JourneyPath);
        var reopened = fixture.ReopenStore();
        Assert.Null(reopened.VoyagerJourney);
        Assert.False(string.IsNullOrWhiteSpace(reopened.VoyagerJourneyLoadError));
        Assert.False(reopened.TryPersistVoyagerJourney(original, out _));
        Assert.True(Directory.Exists(fixture.JourneyPath));
    }

    [Fact]
    public void FailedProgressPersistenceKeepsTheOriginalDurableReserve()
    {
        using var fixture = new Fixture();
        var original = fixture.Store.VoyagerJourney!;
        var durable = File.ReadAllBytes(fixture.JourneyPath);
        var update = fixture.Store.VoyagerJourney!;
        update.SpentGameTicks++;
        fixture.FailWrites();
        Assert.False(fixture.Store.TryPersistVoyagerJourney(update, out _));
        Assert.Equal(durable, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal(PluginJson.Serialize(original), PluginJson.Serialize(fixture.Store.VoyagerJourney));
        fixture.AllowWrites();
        Assert.Equal(PluginJson.Serialize(original), PluginJson.Serialize(fixture.ReopenStore().VoyagerJourney));
    }

    [Fact]
    public void StarOnlyCheckpointPersistsZeroPlanetAndExactStarJourneyBinding()
    {
        using var fixture = new Fixture();
        var reopened = fixture.ReopenStore();
        Assert.True(reopened.TryGetActiveTicket(fixture.Ticket.ReloadToken, out var ticket, out var rejection), rejection);
        Assert.Equal(0, ticket!.DestinationPlanetId);
        Assert.Equal(1, ticket.OriginStarId);
        Assert.Equal(2, ticket.DestinationStarId);
        Assert.Equal(fixture.Record.JourneyId, ticket.VoyagerJourneyId);
        Assert.True(reopened.TryValidateCheckpointFile(ticket, out _));
    }

    [Theory]
    [InlineData(0, 2, true)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    public void StarOnlyCheckpointRequiresCompleteDifferentStarsAndJourney(int originStar, int destinationStar, bool hasJourney)
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidOperationException>(() => fixture.Store.CreateDraft(Fixture.Identity,
            Fixture.SourceSessionId, 1, 101, 0, "synthetic-player", "synthetic-catalogue",
            originStar, destinationStar, hasJourney ? fixture.Record.JourneyId : null));
    }

    [Theory]
    [InlineData("origin_star")]
    [InlineData("same_star")]
    [InlineData("journey")]
    public void StarOnlyCheckpointCannotLoseItsProtectedDestinationMetadata(string changed)
    {
        using var fixture = new Fixture();
        var durable = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(fixture.TicketPath))!;
        if (changed == "origin_star") durable.OriginStarId = 0;
        else if (changed == "same_star") durable.DestinationStarId = durable.OriginStarId;
        else durable.VoyagerJourneyId = null;
        File.WriteAllText(fixture.TicketPath, PluginJson.Serialize(durable));
        var reopened = fixture.ReopenStore();
        Assert.False(reopened.TryGetActiveTicket(fixture.Ticket.ReloadToken, out _, out _));
        Assert.False(reopened.TryValidateCheckpointFile(durable, out _));
    }

    [Theory]
    [InlineData(false, "recovered_closed")]
    [InlineData(true, "remote_landed")]
    public void ExactNewSessionRecoveryPersistsTheAfterRecordWithoutResettingOriginalBudget(bool returnLeg, string phase)
    {
        using var fixture = new Fixture(returnLeg);
        var original = fixture.Store.VoyagerJourney!;
        var accepted = fixture.DispatchReload();
        fixture.AdoptExactCheckpoint();
        var result = fixture.ReadAction(accepted.ActionId);
        Assert.True(result.Succeeded);
        Assert.True(result.Terminal);
        Assert.Equal(NormalActionStates.Completed, result.State);
        Assert.Equal(1, DSPGame.DispatchCalls);
        var restored = fixture.ReopenStore().VoyagerJourney!;
        Assert.Equal(phase, restored.Phase);
        Assert.Equal(original.JourneyId, restored.JourneyId);
        Assert.Equal(original.IssuedAtUtc, restored.IssuedAtUtc);
        Assert.Equal(original.DeadlineUtc, restored.DeadlineUtc);
        Assert.Equal(original.SpentGameTicks, restored.SpentGameTicks);
        Assert.Equal(original.ReturnEnergyFloorJoules, restored.ReturnEnergyFloorJoules);
        Assert.Equal(original.WithdrawalReserveJoules, restored.WithdrawalReserveJoules);
        Assert.Equal(original.ReturnCoreEnergyMinimumJoules, restored.ReturnCoreEnergyMinimumJoules);
        if (returnLeg) Assert.Equal(fixture.Ticket.SavedGameTick, restored.LastLandedGameTick);
        var afterRecordBytes = File.ReadAllBytes(fixture.JourneyPath);
        fixture.ReadAction(accepted.ActionId);
        Assert.Equal(afterRecordBytes, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownReloadSourceAccountsOnlyNineteenTicksAfterAlreadyAccountedTick120(bool returnLeg)
    {
        using var fixture = new Fixture(returnLeg);
        var original = fixture.Store.VoyagerJourney!;
        Assert.Equal(120L, original.LastAccountedGameTick);
        Assert.Equal(original.ActiveActionId, original.LastAccountedFlightActionId);
        Assert.Equal(returnLeg ? 70L : 20L, original.SpentGameTicks);
        fixture.Sessions.Snapshot.GameTick = 139;
        var accepted = fixture.DispatchReload();
        fixture.AdoptExactCheckpoint();
        var recovered = fixture.ReadAction(accepted.ActionId);
        Assert.True(recovered.Succeeded);
        var restored = fixture.ReopenStore().VoyagerJourney!;
        Assert.Equal(returnLeg ? 89L : 39L, restored.SpentGameTicks);
        Assert.Equal(19L, restored.SpentGameTicks - original.SpentGameTicks);
        Assert.Equal(139L, restored.LastAccountedGameTick);
        Assert.Equal(original.ActiveActionId, restored.LastAccountedFlightActionId);
        Assert.False(restored.SpentGameTicksIsConservative);
        AssertOriginalReservations(original, restored);

        var afterRecord = File.ReadAllBytes(fixture.JourneyPath);
        for (var poll = 0; poll < 4; poll++)
        {
            var repeat = fixture.ReadAction(accepted.ActionId);
            Assert.Equal(accepted.ActionId, repeat.ActionId);
            Assert.True(repeat.Succeeded);
            Assert.Equal(afterRecord, File.ReadAllBytes(fixture.JourneyPath));
        }
        // The durable after-record is also idempotent after losing the in-memory
        // reload cache and its source tick: it must not add time or forfeit it again.
        var reopened = fixture.ReopenStore();
        Assert.True(reopened.TryReconcileVoyagerRecovery(reopened.CurrentTicket!, out var rejection), rejection);
        Assert.Equal(afterRecord, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal(restored.SpentGameTicks, reopened.VoyagerJourney!.SpentGameTicks);
        Assert.False(reopened.VoyagerJourney!.SpentGameTicksIsConservative);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData(false, "active")]
    [InlineData(true, "active")]
    [InlineData(false, "recovery_required")]
    [InlineData(true, "recovery_required")]
    [InlineData(false, "outcome_unknown")]
    [InlineData(true, "outcome_unknown")]
    public void RecoveryWithoutTheOldProcessSourceTickForfeitsEveryRemainingJourneyTick(bool returnLeg, string recoveryPhase)
    {
        using var fixture = new Fixture(returnLeg, recoveryPhase: recoveryPhase);
        var original = fixture.Store.VoyagerJourney!;
        Assert.Equal(recoveryPhase == "active" ? (returnLeg ? "return" : "outbound") : recoveryPhase, original.Phase);
        Assert.Equal(120L, original.LastAccountedGameTick);
        Assert.False(original.SpentGameTicksIsConservative);
        // Give the independent budget policy otherwise-sufficient synthetic
        // homebound facts, so a later refusal isolates the lost time allowance.
        Assert.True(PreviewOtherwiseReadyReturn(original).BudgetSufficient);
        fixture.MarkCheckpointRecovered();
        var reopened = fixture.ReopenStore();
        var exactRecoveredTicket = reopened.CurrentTicket!;
        Assert.Equal(FlightCheckpointLifecycleStates.Recovered, exactRecoveredTicket.LifecycleState);
        Assert.Equal(recoveryPhase == "active" ? (long?)null : 120L, exactRecoveredTicket.RecoveryRequiredAtGameTick);
        Assert.True(reopened.TryReconcileVoyagerRecovery(exactRecoveredTicket, out var rejection), rejection);

        var restored = fixture.ReopenStore().VoyagerJourney!;
        Assert.Equal(returnLeg ? "remote_landed" : "recovered_closed", restored.Phase);
        Assert.True(restored.SpentGameTicks >= original.MaximumGameTicks);
        Assert.True(restored.SpentGameTicks >= original.SpentGameTicks);
        Assert.True(restored.SpentGameTicksIsConservative);
        Assert.Equal(original.LastAccountedGameTick, restored.LastAccountedGameTick);
        Assert.Equal(original.LastAccountedFlightActionId, restored.LastAccountedFlightActionId);
        AssertOriginalReservations(original, restored);
        if (returnLeg) Assert.Equal(exactRecoveredTicket.SavedGameTick, restored.LastLandedGameTick);
        var returnBudget = PreviewOtherwiseReadyReturn(restored);
        Assert.False(returnBudget.BudgetComputed);
        Assert.False(returnBudget.BudgetSufficient);
        Assert.Contains(returnBudget.Blockers, blocker => blocker.Code == "return_journey_reserve_invalid");

        var afterRecord = File.ReadAllBytes(fixture.JourneyPath);
        var afterRestart = fixture.ReopenStore();
        Assert.True(afterRestart.TryReconcileVoyagerRecovery(afterRestart.CurrentTicket!, out rejection), rejection);
        Assert.Equal(afterRecord, File.ReadAllBytes(fixture.JourneyPath));
        Assert.True(afterRestart.VoyagerJourney!.SpentGameTicksIsConservative);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingSourceTickCannotReduceAJourneyThatAlreadyOverspentItsOriginalHorizon(bool returnLeg)
    {
        using var fixture = new Fixture(returnLeg, recoveryPhase: "outcome_unknown");
        var original = fixture.Store.VoyagerJourney!;
        original.SpentGameTicks = original.MaximumGameTicks + 17;
        Assert.True(fixture.Store.TryPersistVoyagerJourney(original, out var rejection), rejection);
        fixture.MarkCheckpointRecovered();
        var reopened = fixture.ReopenStore();
        Assert.True(reopened.TryReconcileVoyagerRecovery(reopened.CurrentTicket!, out rejection), rejection);
        var restored = fixture.ReopenStore().VoyagerJourney!;
        Assert.Equal(36017L, restored.SpentGameTicks);
        Assert.True(restored.SpentGameTicksIsConservative);
        Assert.Equal(120L, restored.LastAccountedGameTick);
        Assert.Equal(original.LastAccountedFlightActionId, restored.LastAccountedFlightActionId);
        AssertOriginalReservations(original, restored);
        Assert.False(PreviewOtherwiseReadyReturn(restored).BudgetSufficient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AfterRecordWriteFailureReconcilesOnlyTheOriginalLoadWithoutAnotherDispatch(bool nativeThrows)
    {
        using var fixture = new Fixture(returnLeg: true);
        var original = fixture.Store.VoyagerJourney!;
        if (nativeThrows) DSPGame.BeforeLoad = () => throw new IOException("Synthetic original load entered, outcome unknown.");
        var accepted = fixture.DispatchReload();
        fixture.AdoptExactCheckpoint();
        // Isolate the actual after-record boundary after the production ticket marker
        // was durably reconciled, before the coordinator can report flight recovery.
        Assert.True(fixture.Store.TryMarkReloadReconciled(fixture.Ticket.CheckpointId, accepted.ActionId, out _));
        fixture.FailWrites();
        var unknown = fixture.ReadAction(accepted.ActionId);
        Assert.Equal(NormalActionStates.OutcomeUnknown, unknown.State);
        Assert.False(unknown.Terminal);
        Assert.False(unknown.Succeeded);
        Assert.Equal(WriteHealthStates.Quarantined, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Equal(accepted.ActionId, fixture.Sessions.Snapshot.WriteQuarantineActionId);
        Assert.Equal(1, DSPGame.DispatchCalls);
        Assert.Equal(original.DeadlineUtc, fixture.Store.VoyagerJourney!.DeadlineUtc);
        fixture.AllowWrites();
        var completed = fixture.ReadAction(accepted.ActionId);
        Assert.True(completed.Succeeded);
        Assert.True(completed.ReconciledFromOutcomeUnknown);
        Assert.Equal(WriteHealthStates.Healthy, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Equal("remote_landed", fixture.ReopenStore().VoyagerJourney!.Phase);
        Assert.Equal(original.ReturnEnergyFloorJoules, fixture.Store.VoyagerJourney!.ReturnEnergyFloorJoules);
        Assert.Equal(original.SpentGameTicks, fixture.Store.VoyagerJourney!.SpentGameTicks);
        Assert.Equal(original.DeadlineUtc, fixture.Store.VoyagerJourney!.DeadlineUtc);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void MissingJourneyCannotBeDroppedWhenTheNativeCheckpointWasAdopted()
    {
        using var fixture = new Fixture();
        var accepted = fixture.DispatchReload();
        File.Delete(fixture.JourneyPath);
        fixture.AdoptExactCheckpoint();
        var unknown = fixture.ReadAction(accepted.ActionId);
        Assert.Equal(NormalActionStates.OutcomeUnknown, unknown.State);
        Assert.False(unknown.Terminal);
        Assert.False(unknown.Succeeded);
        Assert.False(File.Exists(fixture.JourneyPath));
        Assert.Equal(WriteHealthStates.Quarantined, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData("owned_save")]
    [InlineData("origin_planet")]
    [InlineData("origin_star")]
    [InlineData("destination_star")]
    [InlineData("destination_planet")]
    [InlineData("native_version")]
    [InlineData("fingerprint")]
    [InlineData("reload_action")]
    public void RecoveryCannotAcceptAStringIdMatchWithDifferentExactCheckpointFacts(string changed)
    {
        using var fixture = new Fixture(returnLeg: true);
        fixture.MarkCheckpointRecovered();
        var before = File.ReadAllBytes(fixture.JourneyPath);
        var forged = PluginJson.Deserialize<FlightCheckpointTicket>(PluginJson.Serialize(fixture.Ticket))!;
        switch (changed)
        {
            case "owned_save": forged.OwnedSaveName = "other-synthetic-owned-save"; break;
            case "origin_planet": forged.OriginPlanetId++; break;
            case "origin_star": forged.OriginStarId = 3; break;
            case "destination_star": forged.DestinationStarId = 3; break;
            case "destination_planet": forged.DestinationPlanetId++; break;
            case "native_version": forged.GameVersion = "different-native"; break;
            case "fingerprint": forged.CheckpointFileFingerprint = "different-file"; break;
            case "reload_action": forged.ReloadActionId = Guid.NewGuid().ToString("D"); break;
            default: throw new ArgumentException(nameof(changed));
        }
        Assert.False(fixture.Store.TryReconcileVoyagerRecovery(forged, out _));
        Assert.Equal(before, File.ReadAllBytes(fixture.JourneyPath));
    }

    [Fact]
    public void MissingOriginalJourneyWithAnAttemptedVoyagerTicketIsUnknownOnRestart()
    {
        using var fixture = new Fixture(persistJourney: false);
        var reopened = fixture.ReopenStore();
        Assert.Null(reopened.VoyagerJourney);
        Assert.False(string.IsNullOrWhiteSpace(reopened.VoyagerJourneyLoadError));
        Assert.False(reopened.TryPersistVoyagerJourney(fixture.Record, out _));
        Assert.False(File.Exists(fixture.JourneyPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnInterruptedJourneyCannotReopenOutboundToReuseItsOriginalBudget(bool returnLeg)
    {
        using var fixture = new Fixture(returnLeg);
        var original = File.ReadAllBytes(fixture.JourneyPath);
        var rewound = fixture.Store.VoyagerJourney!;
        rewound.Phase = "outbound";
        rewound.ReturnLeg = false;
        Assert.False(fixture.Store.TryPersistVoyagerJourney(rewound, out _));
        Assert.Equal(original, File.ReadAllBytes(fixture.JourneyPath));
    }

    [Fact]
    public void CoveringPrimaryHeaderDoesNotCloseJourneyUntilTheExactNormalSaveProofIsBound()
    {
        using var fixture = new Fixture(returnLeg: true, returnedHome: true);
        var original = fixture.Store.VoyagerJourney!;
        fixture.WriteSyntheticPrimaryProof(150);
        var fingerprint = fixture.PrimaryFingerprint();
        Assert.True(fixture.Store.TryRetireAfterPrimarySave(Fixture.Identity, 150, out var retired, out var retireRejection), retireRejection);
        Assert.True(retired);
        Assert.Equal("home_awaiting_save", fixture.Store.VoyagerJourney!.Phase);
        Assert.Null(fixture.Store.VoyagerJourney!.CompletedSaveActionId);
        Assert.Equal("home_awaiting_save", fixture.ReopenStore().VoyagerJourney!.Phase);

        Assert.True(fixture.Store.TryCompleteVoyagerAfterOwnedSave(original.JourneyId, Fixture.NormalSaveActionId,
            Fixture.SourceSessionId, 101, 150, fingerprint, out var rejection), rejection);
        var completed = fixture.ReopenStore().VoyagerJourney!;
        Assert.Equal("completed", completed.Phase);
        Assert.Equal(Fixture.NormalSaveActionId, completed.CompletedSaveActionId);
        Assert.Equal(Fixture.SourceSessionId, completed.CompletedSaveSessionId);
        Assert.Equal(150L, completed.CompletedSaveGameTick);
        Assert.Equal(fingerprint, completed.CompletedSaveFingerprint);
        Assert.Equal(original.DeadlineUtc, completed.DeadlineUtc);
        Assert.Equal(original.SpentGameTicks, completed.SpentGameTicks);
        Assert.Equal(original.ReturnEnergyFloorJoules, completed.ReturnEnergyFloorJoules);
        Assert.Equal(original.WithdrawalReserveJoules, completed.WithdrawalReserveJoules);
        Assert.Equal(original.ReturnCoreEnergyMinimumJoules, completed.ReturnCoreEnergyMinimumJoules);
        Assert.Equal(1, fixture.SyntheticPrimaryProofWrites);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Fact]
    public void ExactCompletedSaveProofIsIdempotentAcrossRestartEvenWhenNewMetadataWritesAreUnavailable()
    {
        using var fixture = new Fixture(returnLeg: true, returnedHome: true);
        fixture.WriteSyntheticPrimaryProof(150);
        var fingerprint = fixture.PrimaryFingerprint();
        Assert.True(fixture.CompleteNormalSave(fingerprint, out var rejection), rejection);
        var recordBytes = File.ReadAllBytes(fixture.JourneyPath);
        var saveBytes = File.ReadAllBytes(fixture.PrimaryPath);
        var saveWrittenAt = File.GetLastWriteTimeUtc(fixture.PrimaryPath);
        fixture.FailWrites();
        Assert.True(fixture.CompleteNormalSave(fingerprint, out rejection), rejection);
        var reopened = fixture.ReopenStore();
        Assert.True(reopened.TryCompleteVoyagerAfterOwnedSave(fixture.Record.JourneyId, Fixture.NormalSaveActionId,
            Fixture.SourceSessionId, 101, 150, fingerprint, out rejection), rejection);
        Assert.Equal(recordBytes, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal(saveBytes, File.ReadAllBytes(fixture.PrimaryPath));
        Assert.Equal(saveWrittenAt, File.GetLastWriteTimeUtc(fixture.PrimaryPath));
        Assert.Equal(1, fixture.SyntheticPrimaryProofWrites);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData("save_action")]
    [InlineData("session")]
    [InlineData("tick")]
    [InlineData("fingerprint")]
    [InlineData("home_planet")]
    [InlineData("journey")]
    [InlineData("newer_matching_save")]
    public void CompletedJourneyCannotBeReboundToAnotherSaveOrProof(string changed)
    {
        using var fixture = new Fixture(returnLeg: true, returnedHome: true);
        fixture.WriteSyntheticPrimaryProof(150);
        var fingerprint = fixture.PrimaryFingerprint();
        Assert.True(fixture.CompleteNormalSave(fingerprint, out var rejection), rejection);
        var original = File.ReadAllBytes(fixture.JourneyPath);
        var journeyId = fixture.Record.JourneyId;
        var actionId = Fixture.NormalSaveActionId;
        var sessionId = Fixture.SourceSessionId;
        var homePlanetId = 101;
        var tick = 150L;
        switch (changed)
        {
            case "save_action": actionId = Guid.NewGuid().ToString("D"); break;
            case "session": sessionId = Guid.NewGuid().ToString("D"); break;
            case "tick": tick++; break;
            case "fingerprint": fingerprint = "different-full-file-proof"; break;
            case "home_planet": homePlanetId = 201; break;
            case "journey": journeyId = Guid.NewGuid().ToString("N"); break;
            case "newer_matching_save":
                fixture.WriteSyntheticPrimaryProof(160);
                fingerprint = fixture.PrimaryFingerprint();
                tick = 160;
                actionId = Guid.NewGuid().ToString("D");
                break;
            default: throw new ArgumentException(nameof(changed));
        }
        var saveBefore = File.ReadAllBytes(fixture.PrimaryPath);
        Assert.False(fixture.Store.TryCompleteVoyagerAfterOwnedSave(journeyId, actionId, sessionId,
            homePlanetId, tick, fingerprint, out _));
        Assert.Equal(original, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal(saveBefore, File.ReadAllBytes(fixture.PrimaryPath));
        Assert.Equal(Fixture.NormalSaveActionId, fixture.ReopenStore().VoyagerJourney!.CompletedSaveActionId);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData("same_tick_body")]
    [InlineData("embedded_identity")]
    [InlineData("native_version")]
    [InlineData("older_tick")]
    [InlineData("foreign_home")]
    [InlineData("wrong_journey")]
    [InlineData("missing_file")]
    [InlineData("different_file_tick")]
    [InlineData("different_fingerprint")]
    [InlineData("invalid_save_action")]
    [InlineData("missing_session")]
    [InlineData("missing_fingerprint")]
    public void HomeSaveClosureRejectsIncompleteOrDifferentFullFileAndCallerFacts(string changed)
    {
        using var fixture = new Fixture(returnLeg: true, returnedHome: true);
        fixture.WriteSyntheticPrimaryProof(150);
        var fingerprint = fixture.PrimaryFingerprint();
        var original = File.ReadAllBytes(fixture.JourneyPath);
        var journeyId = fixture.Record.JourneyId;
        var actionId = Fixture.NormalSaveActionId;
        var sessionId = Fixture.SourceSessionId;
        var homePlanetId = 101;
        var tick = 150L;
        switch (changed)
        {
            case "same_tick_body":
                var writtenAt = File.GetLastWriteTimeUtc(fixture.PrimaryPath);
                var replaced = File.ReadAllBytes(fixture.PrimaryPath);
                replaced[replaced.Length - 1] ^= 1;
                File.WriteAllBytes(fixture.PrimaryPath, replaced);
                File.SetLastWriteTimeUtc(fixture.PrimaryPath, writtenAt);
                using (var stillMatchingPrefix = OwnedSaveRecoveryLease.Open(fixture.PrimaryPath, Fixture.Identity))
                {
                    Assert.Equal(150L, stillMatchingPrefix.Prefix.GameTick);
                    Assert.True(stillMatchingPrefix.Prefix.MatchesExpectedIdentity);
                    Assert.NotEqual(fingerprint, stillMatchingPrefix.Fingerprint);
                }
                break;
            case "embedded_identity":
                const string foreignIdentity = "synthetic-foreign-embedded-world";
                fixture.WriteSyntheticPrimaryProof(150, embeddedIdentity: foreignIdentity);
                fingerprint = fixture.PrimaryFingerprint(foreignIdentity);
                break;
            case "native_version":
                fixture.WriteSyntheticPrimaryProof(150, nativeBuild: 29145);
                fingerprint = fixture.PrimaryFingerprint();
                break;
            case "older_tick":
                tick = Fixture.HomeLandingGameTick - 1;
                fixture.WriteSyntheticPrimaryProof(tick);
                fingerprint = fixture.PrimaryFingerprint();
                break;
            case "foreign_home": homePlanetId = 201; break;
            case "wrong_journey": journeyId = Guid.NewGuid().ToString("N"); break;
            case "missing_file": File.Delete(fixture.PrimaryPath); break;
            case "different_file_tick": tick++; break;
            case "different_fingerprint": fingerprint = "different-full-file-proof"; break;
            case "invalid_save_action": actionId = "not-a-save-action-id"; break;
            case "missing_session": sessionId = string.Empty; break;
            case "missing_fingerprint": fingerprint = string.Empty; break;
            default: throw new ArgumentException(nameof(changed));
        }
        Assert.False(fixture.Store.TryCompleteVoyagerAfterOwnedSave(journeyId, actionId, sessionId,
            homePlanetId, tick, fingerprint, out var rejection));
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(original, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal("home_awaiting_save", fixture.Store.VoyagerJourney!.Phase);
        Assert.Null(fixture.Store.VoyagerJourney!.CompletedSaveActionId);
        Assert.Equal("home_awaiting_save", fixture.ReopenStore().VoyagerJourney!.Phase);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMatchingPrimaryCannotCloseAnInterruptedJourneyBeforeItsHomeLanding(bool returnLeg)
    {
        using var fixture = new Fixture(returnLeg);
        fixture.WriteSyntheticPrimaryProof(150);
        var original = File.ReadAllBytes(fixture.JourneyPath);
        Assert.False(fixture.CompleteNormalSave(fixture.PrimaryFingerprint(), out _));
        Assert.Equal(original, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal("recovery_required", fixture.Store.VoyagerJourney!.Phase);
    }

    [Fact]
    public void TemporaryClosureMetadataFailureRetriesTheSameOriginalProofWithoutAnotherSyntheticSave()
    {
        using var fixture = new Fixture(returnLeg: true, returnedHome: true);
        fixture.WriteSyntheticPrimaryProof(150);
        var fingerprint = fixture.PrimaryFingerprint();
        var saveBytes = File.ReadAllBytes(fixture.PrimaryPath);
        var saveWrittenAt = File.GetLastWriteTimeUtc(fixture.PrimaryPath);
        var recordBefore = File.ReadAllBytes(fixture.JourneyPath);
        fixture.FailWrites();
        Assert.False(fixture.CompleteNormalSave(fingerprint, out var rejection));
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(recordBefore, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal("home_awaiting_save", fixture.Store.VoyagerJourney!.Phase);
        Assert.Null(fixture.Store.VoyagerJourney!.CompletedSaveActionId);
        fixture.AllowWrites();
        Assert.True(fixture.CompleteNormalSave(fingerprint, out rejection), rejection);
        Assert.Equal("completed", fixture.ReopenStore().VoyagerJourney!.Phase);
        Assert.Equal(Fixture.NormalSaveActionId, fixture.Store.VoyagerJourney!.CompletedSaveActionId);
        Assert.Equal(fingerprint, fixture.Store.VoyagerJourney!.CompletedSaveFingerprint);
        Assert.Equal(saveBytes, File.ReadAllBytes(fixture.PrimaryPath));
        Assert.Equal(saveWrittenAt, File.GetLastWriteTimeUtc(fixture.PrimaryPath));
        // This counter labels fixture file creation only. No native GameSave.Save
        // substitute is invoked or claimed; the production store retries metadata.
        Assert.Equal(1, fixture.SyntheticPrimaryProofWrites);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Fact]
    public void PublicProgressPersistenceCannotInventCompletedWithoutNormalSaveProof()
    {
        using var fixture = new Fixture(returnLeg: true, returnedHome: true);
        fixture.WriteSyntheticPrimaryProof(150);
        var original = File.ReadAllBytes(fixture.JourneyPath);
        var completed = fixture.Store.VoyagerJourney!;
        completed.Phase = "completed";
        completed.CompletedSaveGameTick = 150;
        completed.CompletedSaveActionId = Fixture.NormalSaveActionId;
        completed.CompletedSaveSessionId = Fixture.SourceSessionId;
        completed.CompletedSaveFingerprint = fixture.PrimaryFingerprint();
        Assert.False(fixture.Store.TryPersistVoyagerJourney(completed, out _));
        Assert.Equal(original, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal("home_awaiting_save", fixture.Store.VoyagerJourney!.Phase);
    }

    [Theory]
    [InlineData("spent_ticks")]
    [InlineData("return_energy")]
    [InlineData("landed_tick")]
    [InlineData("save_action")]
    [InlineData("save_session")]
    [InlineData("save_fingerprint")]
    public void ClosedJourneyMetadataIsImmutableEvenWhenAChangeWouldIncreaseItsBudgetProtection(string changed)
    {
        using var fixture = new Fixture(returnLeg: true, returnedHome: true);
        fixture.WriteSyntheticPrimaryProof(150);
        Assert.True(fixture.CompleteNormalSave(fixture.PrimaryFingerprint(), out var rejection), rejection);
        var original = File.ReadAllBytes(fixture.JourneyPath);
        var closed = fixture.Store.VoyagerJourney!;
        switch (changed)
        {
            case "spent_ticks": closed.SpentGameTicks++; break;
            case "return_energy": closed.ReturnEnergyFloorJoules++; break;
            case "landed_tick": closed.LastLandedGameTick++; break;
            case "save_action": closed.CompletedSaveActionId = Guid.NewGuid().ToString("D"); break;
            case "save_session": closed.CompletedSaveSessionId = Guid.NewGuid().ToString("D"); break;
            case "save_fingerprint": closed.CompletedSaveFingerprint = "different-full-file-proof"; break;
            default: throw new ArgumentException(nameof(changed));
        }
        Assert.False(fixture.Store.TryPersistVoyagerJourney(closed, out _));
        Assert.Equal(original, File.ReadAllBytes(fixture.JourneyPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedSaveReconciliationCannotReportSuccessAfterItsDurableJourneyDisappearsOrChanges(bool replace)
    {
        using var fixture = new Fixture(returnLeg: true, returnedHome: true);
        fixture.WriteSyntheticPrimaryProof(150);
        var fingerprint = fixture.PrimaryFingerprint();
        Assert.True(fixture.CompleteNormalSave(fingerprint, out var rejection), rejection);
        if (replace)
        {
            var replaced = fixture.Store.VoyagerJourney!;
            replaced.CompletedSaveActionId = Guid.NewGuid().ToString("D");
            File.WriteAllText(fixture.JourneyPath, PluginJson.Serialize(replaced));
        }
        else File.Delete(fixture.JourneyPath);
        var replacedBytes = replace ? File.ReadAllBytes(fixture.JourneyPath) : null;
        Assert.False(fixture.CompleteNormalSave(fingerprint, out _));
        Assert.Equal(replace, File.Exists(fixture.JourneyPath));
        if (replace) Assert.Equal(replacedBytes, File.ReadAllBytes(fixture.JourneyPath));
        Assert.Equal(1, fixture.SyntheticPrimaryProofWrites);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    private static void AssertOriginalReservations(VoyagerJourneyRecord original, VoyagerJourneyRecord restored)
    {
        Assert.Equal(original.JourneyId, restored.JourneyId);
        Assert.Equal(original.OwnedSaveName, restored.OwnedSaveName);
        Assert.Equal(original.GameVersion, restored.GameVersion);
        Assert.Equal(original.ActiveActionId, restored.ActiveActionId);
        Assert.Equal(original.ActiveCheckpointId, restored.ActiveCheckpointId);
        Assert.Equal(original.StartedAtGameTick, restored.StartedAtGameTick);
        Assert.Equal(original.IssuedAtUtc, restored.IssuedAtUtc);
        Assert.Equal(original.DeadlineUtc, restored.DeadlineUtc);
        Assert.Equal(original.MaximumGameTicks, restored.MaximumGameTicks);
        Assert.Equal(original.ReturnReserveGameTicks, restored.ReturnReserveGameTicks);
        Assert.Equal(original.ReturnEnergyFloorJoules, restored.ReturnEnergyFloorJoules);
        Assert.Equal(original.WithdrawalReserveJoules, restored.WithdrawalReserveJoules);
        Assert.Equal(original.ReturnCoreEnergyMinimumJoules, restored.ReturnCoreEnergyMinimumJoules);
        Assert.Equal(original.ExtraWarperReserve, restored.ExtraWarperReserve);
    }

    // This is an optimistic copied-facts policy probe, not native reader or
    // phase authorization. Even assuming all other homebound gates are met,
    // exhausted original journey time cannot buy another return budget.
    private static VoyagerRouteBudgetSnapshot PreviewOtherwiseReadyReturn(VoyagerJourneyRecord journey) =>
        VoyagerRouteBudgetPolicy.Evaluate(new VoyagerRouteBudgetInput
        {
            SessionId = "synthetic-recovered-session", OriginPlanetId = 201, OriginStarId = journey.DestinationStarId,
            DestinationPlanetId = journey.HomePlanetId, DestinationStarId = journey.HomeStarId,
            PlayerStateHash = "synthetic-player", CatalogStateHash = "synthetic-catalogue",
            DestinationVisible = true, DistanceMeters = 2400000d, IsAlive = true, IsGrounded = true,
            ControllerEnabled = true, PlayerOrderIdle = true, BuildUiIdle = true, ThrusterLevel = 3,
            MaxSailSpeed = 1000d, MaxWarpSpeed = 100000d, CoreCapacityJoules = 1e9d, CoreEnergyJoules = 1e9d,
            ReactorEnergyJoules = 1e6d, FuelStorageEnergyJoules = 1e12d, CoreGenerationLowerBoundWatts = 1e7d,
            WarpKeepingPowerPerSpeed = 0.01d, WarpStartPowerPerSpeed = 1d,
            EnergyConsumptionCoefficient = 1d, ThrustPowerPerAcceleration = 20d, NativeWarperCount = 2,
            ReturnJourneyVerified = true, ReturnJourneyId = journey.JourneyId,
            ReturnLegMaximumGameTicks = journey.ReturnReserveGameTicks,
            RemainingJourneyGameTicks = journey.MaximumGameTicks - journey.SpentGameTicks,
            LockedReturnEnergyFloorJoules = journey.ReturnEnergyFloorJoules,
            LockedWithdrawalReserveJoules = journey.WithdrawalReserveJoules,
            LockedReturnCoreMinimumJoules = journey.ReturnCoreEnergyMinimumJoules,
            OriginalExtraWarperReserve = journey.ExtraWarperReserve,
        }, new PreviewVoyagerRouteRequest
        {
            JourneyId = journey.JourneyId, PlanetId = 201, DestinationPlanetId = journey.HomePlanetId,
            DestinationStarId = journey.HomeStarId, ExpectedPlayerStateHash = "synthetic-player",
            ExpectedCatalogStateHash = "synthetic-catalogue",
        });

    private static VoyagerJourneyRecord Copy(VoyagerJourneyRecord source, Action<VoyagerJourneyRecord> change)
    {
        var copy = PluginJson.Deserialize<VoyagerJourneyRecord>(PluginJson.Serialize(source))!;
        change(copy);
        return copy;
    }

    private sealed class Fixture : IDisposable
    {
        internal const string Identity = "synthetic-voyager-owned-primary";
        internal const string SourceSessionId = "22222222-2222-4222-8222-222222222222";
        internal const string NormalSaveActionId = "55555555-5555-4555-8555-555555555555";
        internal const long HomeLandingGameTick = 140;
        private const string OriginalFlightId = "11111111-1111-4111-8111-111111111111";
        private const string Version = "0.10.35.29151";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "spherewright-voyager-journey-" + Guid.NewGuid().ToString("N"));
        private readonly string _previousRoot = GameSave.Root;
        private readonly BridgeError? _previousReadiness = TestWorldCoordinator.ReadinessError;
        private readonly int _previousLoads = DSPGame.LoadCalls;
        private readonly int _previousDispatches = DSPGame.DispatchCalls;
        private readonly string? _previousLoadedName = DSPGame.LoadedName;
        private readonly Action? _previousBeforeLoad = DSPGame.BeforeLoad;
        private readonly PausedSaveTestWorld? _previousWorld = GameMain.data;
        private readonly long _previousTick = GameMain.gameTick;
        private readonly FlightStartTestPlanet _previousPlanet = GameMain.localPlanet;
        private readonly FlightStartTestPlayer _previousPlayer = GameMain.mainPlayer;
        private readonly bool _previousPaused = GameMain.isPaused;
        internal GameSessionTracker Sessions { get; } = new();
        internal NormalGameActionCoordinator NormalActions { get; }
        internal FlightCheckpointStore Store { get; }
        internal FlightCheckpointTicket Ticket { get; }
        internal VoyagerJourneyRecord Record { get; }
        internal FlightCheckpointReloadCoordinator Coordinator { get; }
        internal string Handoff => Path.Combine(_root, "handoff");
        internal string TicketPath => Path.Combine(Handoff, "flight-checkpoint.json");
        internal string JourneyPath => Path.Combine(Handoff, "voyager-journey.json");
        internal string PrimaryPath => GameSave.SavePath(Identity);
        internal int SyntheticPrimaryProofWrites { get; private set; }
        internal Fixture(bool returnLeg = false, bool persistJourney = true, bool returnedHome = false,
            string recoveryPhase = "recovery_required", bool originalWindowExpired = false)
        {
            if (recoveryPhase != "active" && recoveryPhase != "recovery_required" && recoveryPhase != "outcome_unknown")
                throw new ArgumentException("Use an actual active or interrupted journey phase.", nameof(recoveryPhase));
            Directory.CreateDirectory(_root);
            GameSave.Root = _root;
            TestWorldCoordinator.ReadinessError = null;
            DSPGame.LoadCalls = DSPGame.DispatchCalls = 0;
            DSPGame.LoadedName = null;
            DSPGame.BeforeLoad = null;
            try
            {
                Store = ReopenStore();
                NormalActions = new NormalGameActionCoordinator(Store, Sessions);
                var journeyId = Guid.NewGuid().ToString("N");
                Ticket = Store.CreateDraft(Identity, SourceSessionId, 1, 101, 0,
                    "synthetic-player", "synthetic-catalogue", 1, 2, journeyId);
                var outboundSavedTick = returnLeg ? 50 : 100;
                WriteSave(Ticket.OwnedSaveName, returnLeg ? 40 : 90);
                WriteSave(Ticket.CheckpointSaveName, outboundSavedTick);
                Store.PersistCompletedCheckpoint(Ticket, outboundSavedTick);
                var issued = originalWindowExpired ? DateTimeOffset.UtcNow.AddDays(-2) : DateTimeOffset.UtcNow.AddMinutes(-1);
                Record = new VoyagerJourneyRecord { JourneyId = journeyId, OwnedSaveName = Identity, GameVersion = Version,
                    Phase = "outbound", HomePlanetId = 101, HomeStarId = 1, DestinationStarId = 2,
                    ActiveActionId = OriginalFlightId, ActiveCheckpointId = Ticket.CheckpointId,
                    StartedAtGameTick = outboundSavedTick, SpentGameTicks = 0, MaximumGameTicks = 36000, ReturnReserveGameTicks = 18000,
                    ReturnEnergyFloorJoules = 20000000d, WithdrawalReserveJoules = 10000000d,
                    ReturnCoreEnergyMinimumJoules = 8000000d, ExtraWarperReserve = 1,
                    IssuedAtUtc = issued, DeadlineUtc = issued.AddMinutes(30) };
                if (persistJourney) Assert.True(Store.TryPersistVoyagerJourney(Record, out var firstRejection), firstRejection);
                Assert.True(Store.TryMarkAttemptStarted(Ticket.CheckpointId, OriginalFlightId, outboundSavedTick + 1, out _));
                var activeFlightId = OriginalFlightId;
                if (returnLeg)
                {
                    // Advance the real linked store through normal outbound completion
                    // and its covering primary, before creating a separate return checkpoint.
                    Assert.True(Store.TryMarkFlightSucceeded(Ticket.CheckpointId, OriginalFlightId, 90, out _));
                    WriteSave(Identity, 90);
                    Assert.True(Store.TryRetireAfterPrimarySave(Identity, 90, out var retired, out _));
                    Assert.True(retired);
                    Record.Phase = "remote_landed";
                    Record.RemotePlanetId = 201;
                    Record.LastLandedGameTick = 90;
                    Record.SpentGameTicks = 40;
                    if (persistJourney) Assert.True(Store.TryPersistVoyagerJourney(Record, out var landedRejection), landedRejection);
                    Ticket = Store.CreateDraft(Identity, SourceSessionId, 2, 201, 101,
                        "synthetic-return-player", "synthetic-return-catalogue", 2, 1, journeyId);
                    WriteSave(Ticket.CheckpointSaveName, 100);
                    Store.PersistCompletedCheckpoint(Ticket, 100);
                    activeFlightId = "44444444-4444-4444-8444-444444444444";
                    Record.Phase = "return";
                    Record.ReturnLeg = true;
                    Record.ActiveActionId = activeFlightId;
                    Record.ActiveCheckpointId = Ticket.CheckpointId;
                    Record.SpentGameTicks = 50;
                    if (persistJourney) Assert.True(Store.TryPersistVoyagerJourney(Record, out var returnRejection), returnRejection);
                    Assert.True(Store.TryMarkAttemptStarted(Ticket.CheckpointId, activeFlightId, 101, out _));
                }
                if (returnedHome)
                {
                    Assert.True(returnLeg);
                    Assert.True(Store.TryMarkFlightSucceeded(Ticket.CheckpointId, activeFlightId, HomeLandingGameTick, out _));
                    Record.Phase = "home_awaiting_save";
                    Record.LastLandedGameTick = HomeLandingGameTick;
                }
                else
                {
                    if (recoveryPhase != "active")
                        Assert.True(Store.TryMarkRecoveryRequired(Ticket.CheckpointId, activeFlightId, 120, out _));
                    Record.Phase = recoveryPhase == "active" ? (returnLeg ? "return" : "outbound") : recoveryPhase;
                }
                Record.SpentGameTicks = (returnedHome ? HomeLandingGameTick : 120) - Record.StartedAtGameTick;
                // Match production TryAccountVoyagerLeg: this total already
                // includes progress through this tick under this exact flight.
                Record.LastAccountedGameTick = returnedHome ? HomeLandingGameTick : 120;
                Record.LastAccountedFlightActionId = activeFlightId;
                if (persistJourney) Assert.True(Store.TryPersistVoyagerJourney(Record, out var rejection), rejection);
                Sessions.IsCurrentSessionOwned = true;
                Sessions.OwnedSaveName = Identity;
                Sessions.SessionId = SourceSessionId;
                Sessions.Snapshot.GameLoaded = Sessions.Snapshot.OwnedBySpherewright = true;
                Sessions.Snapshot.SessionId = SourceSessionId;
                Sessions.Snapshot.SaveName = Identity;
                Sessions.Snapshot.LocalPlanetId = returnedHome ? Record.HomePlanetId : Ticket.OriginPlanetId;
                Sessions.Snapshot.GameTick = returnedHome ? HomeLandingGameTick : 120;
                Sessions.Snapshot.GameVersion = Version;
                Sessions.Snapshot.WritesAllowed = true;
                NormalActions.FailedFlightCheckpointId = Ticket.CheckpointId;
                Coordinator = new FlightCheckpointReloadCoordinator(true, 60, 30, 32, Sessions, Store, NormalActions);
            }
            catch { Dispose(); throw; }
        }

        internal FlightCheckpointStore ReopenStore() => new("synthetic-bridge", Version, new ManualLogSource(), Handoff);
        internal FlightCheckpointReloadResult DispatchReload()
        {
            var prepared = Coordinator.PrepareOnMainThread(new PrepareFlightCheckpointReloadRequest { ReloadToken = Ticket.ReloadToken });
            Assert.True(prepared.Success, prepared.Error?.Message);
            var committed = Coordinator.CommitOnMainThread(new CommitFlightCheckpointReloadRequest
            { PlanToken = prepared.Value!.PlanToken, IdempotencyKey = Guid.NewGuid().ToString("D") });
            Assert.True(committed.Success, committed.Error?.Message);
            Assert.True(committed.Value!.Accepted);
            return committed.Value;
        }
        internal ActionResultSnapshot ReadAction(string id)
        {
            Assert.True(Coordinator.TryGetActionResultOnMainThread(id, out var result));
            Assert.NotNull(result);
            if (!result!.Terminal && Store.VoyagerJourney?.ReturnOnlyTask is not null)
            {
                GameMain.gameTick++;
                NormalActions.RunNativeIdleGuardForTest();
                Assert.True(Coordinator.TryGetActionResultOnMainThread(id, out result));
            }
            return result!;
        }
        internal void AdoptExactCheckpoint()
        {
            var adoptedSession = Guid.NewGuid().ToString("D");
            Sessions.SessionId = Sessions.Snapshot.SessionId = adoptedSession;
            Sessions.Snapshot.GameLoaded = Sessions.Snapshot.OwnedBySpherewright = true;
            Sessions.Snapshot.SaveName = Ticket.OwnedSaveName;
            Sessions.Snapshot.LocalPlanetId = Ticket.OriginPlanetId;
            Sessions.Snapshot.GameTick = Ticket.SavedGameTick + 1;
            Sessions.Snapshot.CurrentSessionLoadedFromFlightCheckpoint = true;
            Sessions.Snapshot.FlightCheckpointId = Ticket.CheckpointId;
            Sessions.FinishExpectedFlightCheckpointAdoptionForTest();
            Sessions.BindCheckpointNativeWorldFromSnapshotForTest(Store);
            NormalActions.ObserveBoundNative501ContractForTest();
        }
        internal void MarkCheckpointRecovered()
        {
            const string reloadAction = "33333333-3333-4333-8333-333333333333";
            Assert.True(Store.TryMarkReloadStarted(Ticket.CheckpointId, reloadAction, out _));
            Assert.True(Store.TryMarkReloadReconciled(Ticket.CheckpointId, reloadAction, out _));
        }
        internal void FailWrites() => File.WriteAllText(Path.Combine(Handoff, ".test-only-fail-secure-write"), "synthetic write failure");
        internal void AllowWrites() => File.Delete(Path.Combine(Handoff, ".test-only-fail-secure-write"));
        internal void WriteSyntheticPrimaryProof(long tick, string? embeddedIdentity = null, int nativeBuild = 29151)
        {
            WriteSave(Identity, tick, embeddedIdentity, nativeBuild);
            SyntheticPrimaryProofWrites++;
        }
        internal string PrimaryFingerprint(string protectedIdentity = Identity)
        {
            using var lease = OwnedSaveRecoveryLease.Open(PrimaryPath, protectedIdentity);
            return lease.Fingerprint;
        }
        internal bool CompleteNormalSave(string fingerprint, out string rejection) => Store.TryCompleteVoyagerAfterOwnedSave(
            Record.JourneyId, NormalSaveActionId, SourceSessionId, 101, 150, fingerprint, out rejection);
        internal static void WriteSave(string name, long tick, string? embeddedIdentity = null, int nativeBuild = 29151)
        {
            using var prefix = OwnedSavePrefixReaderTests.Fixture(identity: embeddedIdentity ?? Identity, patch: 23,
                versionPatch: 35, versionBuild: nativeBuild, gameDescVersion: 10);
            File.WriteAllBytes(GameSave.SavePath(name), prefix.ToArray());
            using var stream = new FileStream(GameSave.SavePath(name), FileMode.Open, FileAccess.Write);
            stream.Position = 36;
            using var writer = new BinaryWriter(stream);
            writer.Write(tick);
        }
        public void Dispose()
        {
            Sessions.Dispose();
            GameMain.data = _previousWorld; GameMain.gameTick = _previousTick;
            GameMain.localPlanet = _previousPlanet; GameMain.mainPlayer = _previousPlayer; GameMain.isPaused = _previousPaused;
            DSPGame.BeforeLoad = null;
            GameSave.Root = _previousRoot;
            TestWorldCoordinator.ReadinessError = _previousReadiness;
            DSPGame.LoadCalls = _previousLoads;
            DSPGame.DispatchCalls = _previousDispatches;
            DSPGame.LoadedName = _previousLoadedName;
            DSPGame.BeforeLoad = _previousBeforeLoad;
            var full = Path.GetFullPath(_root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(full).StartsWith("spherewright-voyager-journey-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to remove a fixture outside its exact GUID temporary directory.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
