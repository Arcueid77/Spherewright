using System.Text;
using BepInEx.Logging;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

[CollectionDefinition("Owned resume native substitutes", DisableParallelization = true)]
public sealed class OwnedResumeNativeSubstitutesCollection { }

[Collection("Owned resume native substitutes")]
public sealed class OwnedWorldAutosaveCoordinatorIntegrationTests
{
    private const string Identity = "synthetic-autosave-owned-identity";
    private const string Version = "0.10.35.29088";
    private const long SavedTick = 12345;
    private const long CandidateTick = 20000;
    private const string ExpiredMode = OwnedWorldResumeModes.ReauthorizeExpiredAutosave0;
    private const string ActiveMode = OwnedWorldResumeModes.ReauthorizeFixedAutosave0;
    private const string MigrationMode = OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary;
    private const string PrimaryExpiredMode = OwnedWorldResumeModes.ReauthorizeExpiredPrimary;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActivePrimaryMigrationOnlyPreparesPairSpecificDisclosureBeforeLaterConfirmation(bool latestMigration)
    {
        using var fixture = new Fixture(MigrationMode, latestMigration);
        var plan = fixture.Prepare();
        Assert.Equal(latestMigration ? 6 : 5, plan.RecoveryEvidenceVersion);
        Assert.Equal(latestMigration ? "0.10.35.29131" : "0.10.35.29104", plan.SourceGameVersion);
        Assert.Equal(latestMigration ? "0.10.35.29145" : "0.10.35.29131", plan.TargetGameVersion);
        Assert.Contains($"from {plan.SourceGameVersion} to {plan.TargetGameVersion}", plan.ConfirmationPrompt);
        Assert.Contains($"from {plan.SourceGameVersion} to {plan.TargetGameVersion}", plan.CompletionCondition);
        Assert.Equal(SavedTick, plan.CandidateGameTick);
        Assert.True(Spherewright.Bridge.Core.Safety.OwnedWorldPrimaryMigrationPolicy.HasMatchingEcho(plan));
        var denied = fixture.Coordinator.CommitOnMainThread(new()
        { PlanToken = plan.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(), ConfirmationDigest = plan.ConfirmationDigest });
        Assert.Equal(BridgeErrorCodes.UserConfirmationRequired, denied.Error!.Code);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActivePrimaryMigrationConsumesAndLocksExactPrimaryBeforeTheOneLoaderCall(bool latestMigration)
    {
        using var fixture = new Fixture(MigrationMode, latestMigration);
        var request = fixture.Confirmed(fixture.Prepare());
        DSPGame.BeforeLoad = () =>
        {
            Assert.False(fixture.HasProvenance());
            Assert.Throws<IOException>(() => File.Open(fixture.PrimaryPath, FileMode.Open, FileAccess.Write).Dispose());
        };
        var committed = fixture.Coordinator.CommitOnMainThread(request);
        Assert.True(committed.Success, committed.Error?.Message);
        Assert.Equal(Identity, DSPGame.LoadedName);
        Assert.True(fixture.Sessions.Reauthorizing);
        Assert.False(fixture.Sessions.FixedAutosave);
        Assert.Equal(latestMigration ? "0.10.35.29131" : "0.10.35.29104", fixture.Sessions.SourceLease!.Prefix.GameVersion);
        Assert.Equal(SavedTick, fixture.Sessions.SourceLease.Prefix.GameTick);
        Assert.NotNull(fixture.Sessions.JournalLease);
        Assert.True(fixture.Coordinator.CommitOnMainThread(request).Value!.IdempotentReplay);
        Assert.Equal(1, DSPGame.LoadCalls);
        fixture.ReopenStore();
        Assert.False(fixture.NewCoordinator().PrepareOnMainThread(fixture.Request()).Success);
    }

    [Theory]
    [InlineData(false, "primary-file")]
    [InlineData(false, "journal")]
    [InlineData(false, "revision")]
    [InlineData(false, "digest")]
    [InlineData(true, "primary-file")]
    [InlineData(true, "journal")]
    [InlineData(true, "revision")]
    [InlineData(true, "digest")]
    public void ActivePrimaryMigrationRejectsStaleEvidenceOrWrongConfirmationWithoutConsumption(bool latestMigration, string change)
    {
        using var fixture = new Fixture(MigrationMode, latestMigration);
        var request = fixture.Confirmed(fixture.Prepare());
        switch (change)
        {
            case "primary-file": var bytes = File.ReadAllBytes(fixture.PrimaryPath); bytes[^1]++; File.WriteAllBytes(fixture.PrimaryPath, bytes); break;
            case "journal": fixture.WriteJournal("changed valid content"); break;
            case "revision": fixture.Sessions.Revision++; break;
            case "digest": request.ConfirmationDigest = "different-plan"; break;
        }
        Assert.False(fixture.Coordinator.CommitOnMainThread(request).Success);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData(OwnedWorldResumeModes.Default)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeExpiredPrimary)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeFixedAutosave0)]
    public void NewActiveMigrationCannotBecomeDefaultExpiredOrAutosaveAuthority(string mode)
    {
        using var fixture = new Fixture(MigrationMode, latestMigration: true);
        var request = new PrepareOwnedWorldResumeRequest { ResumeToken = fixture.Token, RecoveryMode = mode };
        if (mode == OwnedWorldResumeModes.ReauthorizeFixedAutosave0)
        {
            request.MinimumRecoveryGameTick = CandidateTick - 10;
            request.ExpectedRecoveryGameTick = CandidateTick;
        }
        Assert.False(fixture.Coordinator.PrepareOnMainThread(request).Success);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void NewActiveMigrationNeverFallsBackWhenTheExactPrimaryIsMissing()
    {
        using var fixture = new Fixture(MigrationMode, latestMigration: true);
        File.Move(fixture.PrimaryPath, GameSave.SavePath(GameSave.LastExit));
        Assert.False(fixture.Coordinator.PrepareOnMainThread(fixture.Request()).Success);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void NewActiveMigrationNativeFailureAfterConsumptionCannotReplayOrReviveTheCredential()
    {
        using var fixture = new Fixture(MigrationMode, latestMigration: true);
        var request = fixture.Confirmed(fixture.Prepare());
        DSPGame.BeforeLoad = () => throw new IOException("test-only native loader failure");
        Assert.False(fixture.Coordinator.CommitOnMainThread(request).Success);
        fixture.ReopenStore();
        Assert.False(fixture.HasProvenance());
        Assert.False(fixture.NewCoordinator().PrepareOnMainThread(fixture.Request()).Success);
        Assert.Null(fixture.Sessions.SourceLease);
        Assert.Null(fixture.Sessions.JournalLease);
    }

    [Theory]
    [InlineData(ExpiredMode)]
    [InlineData(ActiveMode)]
    public void PrepareDisclosesWithoutLoadOrConsumptionAndCommitNeedsModeSpecificConversationAuthority(string mode)
    {
        using var fixture = new Fixture(mode);
        var plan = fixture.Prepare();
        Assert.True(plan.UserConfirmationRequired);
        Assert.False(plan.CommitAllowedNow);
        Assert.Equal(CandidateTick, plan.CandidateGameTick);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(fixture.HasProvenance());
        var rejected = fixture.Coordinator.CommitOnMainThread(new CommitOwnedWorldResumeRequest
        {
            PlanToken = plan.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            ConfirmationDigest = plan.ConfirmationDigest,
        });
        Assert.False(rejected.Success);
        Assert.Equal(BridgeErrorCodes.UserConfirmationRequired, rejected.Error!.Code);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData(ExpiredMode)]
    [InlineData(ActiveMode)]
    public void CommitConsumesBeforeExactLoaderAndIdempotencyDoesNotLoadTwice(string mode)
    {
        using var fixture = new Fixture(mode);
        var request = fixture.Confirmed(fixture.Prepare());
        DSPGame.BeforeLoad = () =>
        {
            Assert.False(fixture.HasProvenance());
            Assert.Throws<IOException>(() => File.Open(fixture.PrimaryPath, FileMode.Open, FileAccess.Write).Dispose());
        };
        var result = fixture.Coordinator.CommitOnMainThread(request);
        Assert.True(result.Success, result.Error?.Message);
        Assert.True(result.Value!.Accepted);
        Assert.Equal(GameSave.AutoSave0, DSPGame.LoadedName);
        Assert.Equal(1, DSPGame.LoadCalls);
        Assert.True(fixture.Sessions.Reauthorizing);
        Assert.True(fixture.Sessions.FixedAutosave);
        Assert.False(fixture.HasActiveProvenance());
        Assert.False(fixture.HasExpiredProvenance());
        Assert.Equal(CandidateTick, fixture.Sessions.SourceLease!.Prefix.GameTick);
        Assert.NotNull(fixture.Sessions.JournalLease);
        Assert.NotNull(fixture.Sessions.PrimaryLease);
        Assert.True(fixture.Coordinator.CommitOnMainThread(request).Value!.IdempotentReplay);
        Assert.Equal(1, DSPGame.LoadCalls);
        Assert.False(fixture.NewCoordinator().PrepareOnMainThread(fixture.Request()).Success);
        Assert.True(fixture.Coordinator.TryGetActionResultOnMainThread(result.Value.ActionId, out var pending));
        Assert.False(pending!.Terminal); // Fake session has not adopted or saved anything.
    }

    [Theory]
    [InlineData(ExpiredMode, "file")]
    [InlineData(ExpiredMode, "journal")]
    [InlineData(ExpiredMode, "revision")]
    [InlineData(ExpiredMode, "slot-missing")]
    [InlineData(ExpiredMode, "primary-file")]
    [InlineData(ExpiredMode, "primary-identity")]
    [InlineData(ExpiredMode, "primary-newer-than-ticket")]
    [InlineData(ActiveMode, "file")]
    [InlineData(ActiveMode, "journal")]
    [InlineData(ActiveMode, "revision")]
    [InlineData(ActiveMode, "slot-missing")]
    [InlineData(ActiveMode, "primary-file")]
    [InlineData(ActiveMode, "primary-identity")]
    [InlineData(ActiveMode, "primary-newer-than-ticket")]
    public void PreparedEvidenceDriftNeverLoadsOrConsumes(string mode, string change)
    {
        using var fixture = new Fixture(mode);
        var request = fixture.Confirmed(fixture.Prepare());
        switch (change)
        {
            case "file":
                var bytes = File.ReadAllBytes(fixture.CandidatePath);
                bytes[^1]++;
                File.WriteAllBytes(fixture.CandidatePath, bytes);
                break;
            case "journal": fixture.WriteJournal("changed valid content"); break;
            case "revision": fixture.Sessions.Revision++; break;
            case "slot-missing": File.Move(fixture.CandidatePath, GameSave.SavePath("_autosave_1")); break;
            case "primary-file":
                var primaryBytes = File.ReadAllBytes(fixture.PrimaryPath);
                primaryBytes[^1]++;
                File.WriteAllBytes(fixture.PrimaryPath, primaryBytes);
                break;
            case "primary-identity": Fixture.WriteSave(fixture.PrimaryPath, "different-world", SavedTick); break;
            case "primary-newer-than-ticket": Fixture.WriteSave(fixture.PrimaryPath, Identity, CandidateTick - 1); break;
        }
        var result = fixture.Coordinator.CommitOnMainThread(request);
        Assert.False(result.Success);
        Assert.Equal(BridgeErrorCodes.StaleState, result.Error!.Code);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData(ExpiredMode)]
    [InlineData(ActiveMode)]
    public void MissingFixedSlotNeverFallsBackToPrimaryLastExitOrAnotherAutosave(string mode)
    {
        using var fixture = new Fixture(mode);
        File.Copy(fixture.CandidatePath, GameSave.SavePath(Identity), overwrite: true);
        File.Copy(fixture.CandidatePath, GameSave.SavePath(GameSave.LastExit));
        File.Move(fixture.CandidatePath, GameSave.SavePath("_autosave_1"));
        Assert.False(fixture.Coordinator.PrepareOnMainThread(fixture.Request()).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(fixture.HasProvenance());
    }

    [Theory]
    [InlineData(ExpiredMode)]
    [InlineData(ActiveMode)]
    public void NativeFailureAfterConsumptionDoesNotReviveProvenanceAndReleasesLeases(string mode)
    {
        using var fixture = new Fixture(mode);
        var request = fixture.Confirmed(fixture.Prepare());
        DSPGame.BeforeLoad = () =>
        {
            Assert.False(fixture.HasProvenance());
            throw new IOException("test-only native loader failure");
        };
        Assert.False(fixture.Coordinator.CommitOnMainThread(request).Success);
        fixture.ReopenStore(); // Actual on-disk tombstones/attempts, not just memory state.
        Assert.False(fixture.HasProvenance());
        Assert.False(fixture.NewCoordinator().PrepareOnMainThread(fixture.Request()).Success);
        Assert.Null(fixture.Sessions.SourceLease);
        Assert.Null(fixture.Sessions.JournalLease);
        Assert.Null(fixture.Sessions.PrimaryLease);
    }

    [Theory]
    [InlineData(ExpiredMode, "missing")]
    [InlineData(ExpiredMode, "corrupt")]
    [InlineData(ExpiredMode, "identity")]
    [InlineData(ExpiredMode, "unreadable")]
    [InlineData(ExpiredMode, "newer-than-ticket")]
    [InlineData(ActiveMode, "missing")]
    [InlineData(ActiveMode, "corrupt")]
    [InlineData(ActiveMode, "identity")]
    [InlineData(ActiveMode, "unreadable")]
    [InlineData(ActiveMode, "newer-than-ticket")]
    public void UnknownOrDifferentOriginalPrimaryCannotBecomeAnOverwriteTarget(string mode, string change)
    {
        using var fixture = new Fixture(mode);
        FileStream? lockedPrimary = null;
        try
        {
            switch (change)
            {
                case "missing": File.Delete(fixture.PrimaryPath); break;
                case "corrupt": File.WriteAllText(fixture.PrimaryPath, "not a save"); break;
                case "identity": Fixture.WriteSave(fixture.PrimaryPath, "different-world", SavedTick); break;
                case "newer-than-ticket": Fixture.WriteSave(fixture.PrimaryPath, Identity, CandidateTick - 1); break;
                case "unreadable": lockedPrimary = File.Open(fixture.PrimaryPath, FileMode.Open, FileAccess.Read, FileShare.None); break;
            }
            Assert.False(fixture.Coordinator.PrepareOnMainThread(fixture.Request()).Success);
            Assert.True(fixture.HasProvenance());
            Assert.Equal(0, DSPGame.LoadCalls);
        }
        finally { lockedPrimary?.Dispose(); }
    }

    [Theory]
    [InlineData(ExpiredMode, false, true)]
    [InlineData(ActiveMode, true, false)]
    public void FixedAndExpiredModesNeverAcceptEachOthersProvenance(
        string mode, bool activeExpected, bool expiredExpected)
    {
        using var fixture = new Fixture(mode);
        Assert.Equal(activeExpected, fixture.HasActiveProvenance());
        Assert.Equal(expiredExpected, fixture.HasExpiredProvenance());
    }

    [Theory]
    [InlineData(ExpiredMode, "missing-confirmation")]
    [InlineData(ExpiredMode, "wrong-digest")]
    [InlineData(ActiveMode, "missing-confirmation")]
    [InlineData(ActiveMode, "wrong-digest")]
    public void MissingOrIncorrectCommitAuthorityDoesNotConsumeOrLoad(string mode, string change)
    {
        using var fixture = new Fixture(mode);
        var request = fixture.Confirmed(fixture.Prepare());
        if (change == "missing-confirmation") request.UserConfirmedInConversation = false;
        else request.ConfirmationDigest += "-changed";

        var result = fixture.Coordinator.CommitOnMainThread(request);

        Assert.False(result.Success);
        Assert.Equal(BridgeErrorCodes.UserConfirmationRequired, result.Error!.Code);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void ActiveProvenanceRequiresBothUnchangedTicketReplicas()
    {
        using var fixture = new Fixture(ActiveMode);
        fixture.ChangeTicketReplica(handoff: true, ticket => ticket.ExpiresAtUtc = ticket.ExpiresAtUtc.AddMinutes(1));

        Assert.False(fixture.HasActiveProvenance());
        Assert.False(fixture.Coordinator.PrepareOnMainThread(fixture.Request()).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void ActivePreparedPlanRejectsAnyCredentialExpiryChangeBeforeLoad()
    {
        using var fixture = new Fixture(ActiveMode);
        var request = fixture.Confirmed(fixture.Prepare());
        fixture.ChangeTicketReplica(handoff: false, ticket => ticket.ExpiresAtUtc = ticket.ExpiresAtUtc.AddMinutes(1));
        fixture.ChangeTicketReplica(handoff: true, ticket => ticket.ExpiresAtUtc = ticket.ExpiresAtUtc.AddMinutes(1));

        var result = fixture.Coordinator.CommitOnMainThread(request);

        Assert.False(result.Success);
        Assert.Equal(BridgeErrorCodes.StaleState, result.Error!.Code);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(fixture.HasTicketReplicas()); // A changed, unconsumed credential is not silently renewed or consumed.
    }

    [Theory]
    [InlineData("journal-missing")]
    [InlineData("quarantine")]
    [InlineData("consumed")]
    public void ActiveTicketStoreRejectsMissingJournalQuarantinedOrConsumedProvenance(string change)
    {
        using var fixture = new Fixture(ActiveMode);
        switch (change)
        {
            case "journal-missing":
                File.Delete(fixture.JournalPath);
                break;
            case "quarantine":
                var quarantineAction = Guid.NewGuid().ToString("D");
                fixture.ChangeTicketReplica(handoff: false, ticket => ticket.QuarantineActionId = quarantineAction);
                fixture.ChangeTicketReplica(handoff: true, ticket => ticket.QuarantineActionId = quarantineAction);
                break;
            case "consumed":
                var plan = fixture.Prepare();
                var result = fixture.Coordinator.CommitOnMainThread(fixture.Confirmed(plan));
                Assert.True(result.Success, result.Error?.Message);
                break;
        }

        fixture.ReopenStore();
        Assert.False(fixture.HasActiveProvenance());
        Assert.Equal(change == "consumed" ? 1 : 0, DSPGame.LoadCalls);
    }

    [Fact]
    public void Expired29145PrimaryPreparesTheExact29151DisclosureWithoutTouchingOriginalEvidence()
    {
        using var fixture = new Fixture(PrimaryExpiredMode, currentNativeMigration: true);
        var primary = File.ReadAllBytes(fixture.PrimaryPath);
        var journal = File.ReadAllBytes(fixture.JournalPath);
        var runtimeTicket = File.ReadAllBytes(fixture.TicketReplicaPath(false));
        var handoffTicket = File.ReadAllBytes(fixture.TicketReplicaPath(true));

        var plan = fixture.Prepare();

        Assert.Equal(2, plan.RecoveryEvidenceVersion);
        Assert.Equal(PrimaryExpiredMode, plan.RecoveryMode);
        Assert.Equal("0.10.35.29145", plan.SourceGameVersion);
        Assert.Equal("0.10.35.29151", plan.TargetGameVersion);
        Assert.Equal(SavedTick, plan.MinimumGameTick);
        Assert.Equal(SavedTick, plan.CandidateGameTick);
        Assert.True(plan.ExactEmbeddedIdentityVerified);
        Assert.True(plan.UserConfirmationRequired);
        Assert.False(plan.CommitAllowedNow);
        Assert.True(Spherewright.Bridge.Core.Safety.OwnedWorldReauthorizationPolicy.HasMatchingEcho(plan));
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.Equal(primary, File.ReadAllBytes(fixture.PrimaryPath));
        Assert.Equal(journal, File.ReadAllBytes(fixture.JournalPath));
        Assert.Equal(runtimeTicket, File.ReadAllBytes(fixture.TicketReplicaPath(false)));
        Assert.Equal(handoffTicket, File.ReadAllBytes(fixture.TicketReplicaPath(true)));
    }

    [Theory]
    [InlineData("missing-confirmation")]
    [InlineData("wrong-digest")]
    public void Expired29145PrimaryRequiresSubsequentMatchingConfirmationBeforeConsumption(string fault)
    {
        using var fixture = new Fixture(PrimaryExpiredMode, currentNativeMigration: true);
        var runtimeTicket = File.ReadAllBytes(fixture.TicketReplicaPath(false));
        var handoffTicket = File.ReadAllBytes(fixture.TicketReplicaPath(true));
        var journal = File.ReadAllBytes(fixture.JournalPath);
        var primary = File.ReadAllBytes(fixture.PrimaryPath);
        var request = fixture.Confirmed(fixture.Prepare());
        if (fault == "missing-confirmation") request.UserConfirmedInConversation = false;
        else request.ConfirmationDigest += "-wrong";

        var result = fixture.Coordinator.CommitOnMainThread(request);

        Assert.False(result.Success);
        Assert.Equal(BridgeErrorCodes.UserConfirmationRequired, result.Error!.Code);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.Equal(runtimeTicket, File.ReadAllBytes(fixture.TicketReplicaPath(false)));
        Assert.Equal(handoffTicket, File.ReadAllBytes(fixture.TicketReplicaPath(true)));
        Assert.Equal(journal, File.ReadAllBytes(fixture.JournalPath));
        Assert.Equal(primary, File.ReadAllBytes(fixture.PrimaryPath));
    }

    [Fact]
    public void Expired29145PrimaryConsumesExactProvenanceBeforeTheSingle29151LoadAndRejectsRekeying()
    {
        using var fixture = new Fixture(PrimaryExpiredMode, currentNativeMigration: true);
        var originalTicket = File.ReadAllText(fixture.TicketReplicaPath(false));
        var originalJournal = File.ReadAllBytes(fixture.JournalPath);
        var originalPrimary = File.ReadAllBytes(fixture.PrimaryPath);
        var request = fixture.Confirmed(fixture.Prepare());
        DSPGame.BeforeLoad = () =>
        {
            fixture.AssertDurableConsumption(originalTicket, request.ConfirmationDigest);
            Assert.False(fixture.HasProvenance());
            Assert.Throws<IOException>(() => File.Open(fixture.PrimaryPath, FileMode.Open, FileAccess.Write).Dispose());
            Assert.Throws<IOException>(() => File.Open(fixture.JournalPath, FileMode.Open, FileAccess.Write).Dispose());
            Assert.Equal(originalPrimary, File.ReadAllBytes(fixture.PrimaryPath));
            Assert.Equal(originalJournal, File.ReadAllBytes(fixture.JournalPath));
        };

        var result = fixture.Coordinator.CommitOnMainThread(request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.True(result.Value!.Accepted);
        Assert.Equal(Identity, DSPGame.LoadedName);
        Assert.Equal(1, DSPGame.LoadCalls);
        Assert.True(fixture.Sessions.Reauthorizing);
        Assert.False(fixture.Sessions.FixedAutosave);
        Assert.Null(fixture.Sessions.PrimaryLease);
        Assert.Equal("0.10.35.29145", fixture.Sessions.SourceLease!.Prefix.GameVersion);
        Assert.Equal(SavedTick, fixture.Sessions.SourceLease.Prefix.GameTick);
        Assert.NotNull(fixture.Sessions.JournalLease);
        var replay = fixture.Coordinator.CommitOnMainThread(request);
        Assert.True(replay.Success);
        Assert.True(replay.Value!.IdempotentReplay);
        Assert.Equal(result.Value.ActionId, replay.Value.ActionId);
        var rekeyed = fixture.Coordinator.CommitOnMainThread(new()
        {
            PlanToken = request.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            UserConfirmedInConversation = true, ConfirmationDigest = request.ConfirmationDigest,
        });
        Assert.False(rekeyed.Success);
        Assert.Equal(BridgeErrorCodes.PlanNotFound, rekeyed.Error!.Code);
        Assert.Equal(1, DSPGame.LoadCalls);
        fixture.ReopenStore();
        Assert.False(fixture.HasProvenance());
        Assert.False(fixture.NewCoordinator().PrepareOnMainThread(fixture.Request()).Success);
        Assert.True(fixture.Coordinator.TryGetActionResultOnMainThread(result.Value.ActionId, out var pending));
        Assert.False(pending!.Terminal); // The fake loader does not prove adoption, Journal transition or a normal save.
    }

    [Theory]
    [InlineData("primary")]
    [InlineData("journal")]
    [InlineData("ticket-replica")]
    [InlineData("revision")]
    public void Expired29145PrimaryRevalidatesEveryPreparedBindingBeforeConsumption(string fault)
    {
        using var fixture = new Fixture(PrimaryExpiredMode, currentNativeMigration: true);
        var request = fixture.Confirmed(fixture.Prepare());
        switch (fault)
        {
            case "primary":
                var changed = File.ReadAllBytes(fixture.PrimaryPath);
                changed[^1]++;
                File.WriteAllBytes(fixture.PrimaryPath, changed);
                break;
            case "journal": fixture.WriteJournal("changed valid content"); break;
            case "ticket-replica": fixture.ChangeTicketReplica(true, ticket => ticket.ExpiresAtUtc = ticket.ExpiresAtUtc.AddMinutes(1)); break;
            case "revision": fixture.Sessions.Revision++; break;
        }
        var primary = File.ReadAllBytes(fixture.PrimaryPath);
        var journal = File.ReadAllBytes(fixture.JournalPath);
        var runtimeTicket = File.ReadAllBytes(fixture.TicketReplicaPath(false));
        var handoffTicket = File.ReadAllBytes(fixture.TicketReplicaPath(true));

        var result = fixture.Coordinator.CommitOnMainThread(request);

        Assert.False(result.Success);
        Assert.Equal(BridgeErrorCodes.StaleState, result.Error!.Code);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.Equal(primary, File.ReadAllBytes(fixture.PrimaryPath));
        Assert.Equal(journal, File.ReadAllBytes(fixture.JournalPath));
        Assert.Equal(runtimeTicket, File.ReadAllBytes(fixture.TicketReplicaPath(false)));
        Assert.Equal(handoffTicket, File.ReadAllBytes(fixture.TicketReplicaPath(true)));
    }

    [Fact]
    public void Expired29145PrimaryNeverFallsBackToLastExitOrAutosaveFor29151()
    {
        using var fixture = new Fixture(PrimaryExpiredMode, currentNativeMigration: true);
        Fixture.WriteSave(fixture.CandidatePath, Identity, CandidateTick, 29145);
        File.Move(fixture.PrimaryPath, GameSave.SavePath(GameSave.LastExit));

        Assert.False(fixture.Coordinator.PrepareOnMainThread(fixture.Request()).Success);
        Assert.True(fixture.HasProvenance());
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void Expired29145PrimaryNativeFailurePreservesConsumptionAndCannotReplayOn29151()
    {
        using var fixture = new Fixture(PrimaryExpiredMode, currentNativeMigration: true);
        var originalTicket = File.ReadAllText(fixture.TicketReplicaPath(false));
        var primary = File.ReadAllBytes(fixture.PrimaryPath);
        var journal = File.ReadAllBytes(fixture.JournalPath);
        var request = fixture.Confirmed(fixture.Prepare());
        var attempts = 0;
        DSPGame.BeforeLoad = () =>
        {
            attempts++;
            fixture.AssertDurableConsumption(originalTicket, request.ConfirmationDigest);
            Assert.Throws<IOException>(() => File.Open(fixture.PrimaryPath, FileMode.Open, FileAccess.Write).Dispose());
            Assert.Throws<IOException>(() => File.Open(fixture.JournalPath, FileMode.Open, FileAccess.Write).Dispose());
            throw new IOException("test-only current-native loader failure");
        };

        var result = fixture.Coordinator.CommitOnMainThread(request);

        Assert.False(result.Success);
        Assert.Equal(BridgeErrorCodes.ActionFailed, result.Error!.Code);
        Assert.Equal(1, attempts);
        Assert.Equal(0, DSPGame.LoadCalls);
        fixture.ReopenStore();
        Assert.False(fixture.HasProvenance());
        Assert.False(fixture.NewCoordinator().PrepareOnMainThread(fixture.Request()).Success);
        Assert.False(fixture.Coordinator.CommitOnMainThread(request).Success);
        Assert.False(fixture.Coordinator.CommitOnMainThread(new()
        {
            PlanToken = request.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            UserConfirmedInConversation = true, ConfirmationDigest = request.ConfirmationDigest,
        }).Success);
        Assert.Equal(1, attempts);
        Assert.Null(fixture.Sessions.SourceLease);
        Assert.Null(fixture.Sessions.JournalLease);
        Assert.Equal(primary, File.ReadAllBytes(fixture.PrimaryPath));
        Assert.Equal(journal, File.ReadAllBytes(fixture.JournalPath));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "Spherewright", "autosave-coordinator", Guid.NewGuid().ToString("N"));
        private string Runtime => Path.Combine(_root, "runtime");
        private string Handoff => Path.Combine(_root, "handoff");
        private string JournalId => GameplayJournalIdentity.HashOwnedSaveIdentity(Identity);
        internal string CandidatePath => GameSave.SavePath(GameSave.AutoSave0);
        internal string PrimaryPath => GameSave.SavePath(Identity);
        internal string Mode { get; }
        private readonly bool _latestMigration;
        private readonly bool _currentNativeMigration;
        private string SourceVersion => _currentNativeMigration ? "0.10.35.29145"
            : Mode == MigrationMode ? (_latestMigration ? "0.10.35.29131" : "0.10.35.29104") : Version;
        private string _runtimeVersion = Version;
        internal OwnedWorldResumeTicketStore Store { get; private set; } = null!;
        internal string Token { get; }
        internal GameSessionTracker Sessions { get; } = new();
        internal OwnedWorldResumeCoordinator Coordinator { get; }

        internal Fixture(string mode = ExpiredMode, bool latestMigration = false, bool currentNativeMigration = false)
        {
            if (currentNativeMigration && (mode != PrimaryExpiredMode || latestMigration))
                throw new InvalidOperationException("The current-native fixture represents only exact expired-primary provenance.");
            Mode = mode;
            _latestMigration = latestMigration;
            _currentNativeMigration = currentNativeMigration;
            _runtimeVersion = SourceVersion;
            Directory.CreateDirectory(Runtime);
            Directory.CreateDirectory(Handoff);
            GameSave.Root = Path.Combine(_root, "saves");
            Directory.CreateDirectory(GameSave.Root);
            DSPGame.LoadCalls = 0;
            DSPGame.LoadedName = null;
            DSPGame.BeforeLoad = null;
            TestWorldCoordinator.ReadinessError = null;
            ReopenStore();
            Store.ArmFromHealthySavedOwnedSession(Identity, "synthetic-session", 104, SavedTick,
                new OwnedWorldGameplayJournalCheckpoint
                {
                    Version = 1, JournalId = JournalId, TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave,
                    HistoricalCoverageComplete = false, TrackingStartedAtGameTick = SavedTick, MinimumDurableThroughSequence = 2,
                });
            Token = Store.CurrentResumeToken!;
            WriteJournal("original");
            var now = DateTimeOffset.UtcNow;
            var active = Mode is ActiveMode or MigrationMode;
            var issuedAt = active ? now.AddMinutes(-1) : now.AddDays(-2);
            var expiresAt = active ? now.AddDays(1) : issuedAt.AddDays(1);
            foreach (var directory in new[] { Runtime, Handoff })
            {
                var path = Path.Combine(directory, "owned-world-resume.json");
                var ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(path))!;
                ticket.IssuedAtUtc = issuedAt;
                ticket.ExpiresAtUtc = expiresAt;
                File.WriteAllText(path, PluginJson.Serialize(ticket));
            }
            if (_currentNativeMigration) _runtimeVersion = "0.10.35.29151";
            else if (Mode == MigrationMode) _runtimeVersion = _latestMigration ? "0.10.35.29145" : "0.10.35.29131";
            ReopenStore();
            WriteSave(CandidatePath, Identity, CandidateTick);
            WriteSave(PrimaryPath, Identity, SavedTick, _currentNativeMigration ? 29145 : Mode == MigrationMode ? (_latestMigration ? 29131 : 29104) : 29088);
            Coordinator = NewCoordinator();
        }

        internal static void WriteSave(string path, string identity, long gameTick, int versionBuild = 29088)
        {
            using var stream = OwnedSavePrefixReaderTests.Fixture(identity: identity,
                patch: 23, versionPatch: 35, versionBuild: versionBuild, gameDescVersion: 10);
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                stream.Position = 36;
                writer.Write(gameTick);
                writer.Write(DateTime.UtcNow.Ticks);
            }
            File.WriteAllBytes(path, stream.ToArray());
        }

        internal void ReopenStore() => Store = new(Runtime, Handoff, "synthetic-bridge", _runtimeVersion, new ManualLogSource());
        internal OwnedWorldResumeCoordinator NewCoordinator() => new(true, 60, 10, 16, Sessions, Store);
        internal bool HasActiveProvenance() => Store.TryGetFixedAutosaveProvenance(Token, out _, out _, out _);
        internal bool HasExpiredProvenance() => Store.TryGetExpiredPrimaryProvenance(Token, out _, out _, out _);
        internal bool HasProvenance() => Mode == MigrationMode
            ? Store.TryGetVersionChangedPrimaryProvenance(Token, out _, out _, out _)
            : Mode == ActiveMode ? HasActiveProvenance() : HasExpiredProvenance();
        internal string TicketReplicaPath(bool handoff) => Path.Combine(handoff ? Handoff : Runtime, "owned-world-resume.json");
        internal string JournalPath => Path.Combine(Runtime, "journals", "gameplay-" + JournalId + ".json");
        internal bool HasTicketReplicas() => File.Exists(TicketReplicaPath(false)) && File.Exists(TicketReplicaPath(true));
        internal void ChangeTicketReplica(bool handoff, Action<OwnedWorldResumeTicket> change)
        {
            var path = TicketReplicaPath(handoff);
            var ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(path))!;
            change(ticket);
            File.WriteAllText(path, PluginJson.Serialize(ticket));
        }
        internal PrepareOwnedWorldResumeRequest Request() => new()
        {
            ResumeToken = Token, RecoveryMode = Mode,
            MinimumRecoveryGameTick = Mode is MigrationMode or PrimaryExpiredMode ? null : CandidateTick - 10,
            ExpectedRecoveryGameTick = Mode is MigrationMode or PrimaryExpiredMode ? null : CandidateTick,
        };
        internal PreparedOwnedWorldResumePlan Prepare()
        {
            var result = Coordinator.PrepareOnMainThread(Request());
            Assert.True(result.Success, result.Error?.Message);
            return result.Value!;
        }
        internal CommitOwnedWorldResumeRequest Confirmed(PreparedOwnedWorldResumePlan plan) => new()
        {
            PlanToken = plan.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            UserConfirmedInConversation = true, ConfirmationDigest = plan.ConfirmationDigest,
        };
        internal void AssertDurableConsumption(string originalTicket, string confirmationDigest)
        {
            foreach (var directory in new[] { Runtime, Handoff })
            {
                Assert.False(File.Exists(Path.Combine(directory, "owned-world-resume.json")));
                var attemptPath = Assert.Single(Directory.GetFiles(directory, "owned-world-reauthorization-attempt-*.json"));
                var attempt = PluginJson.Deserialize<OwnedWorldReauthorizationAttempt>(File.ReadAllText(attemptPath))!;
                Assert.Equal(originalTicket, PluginJson.Serialize(attempt.Ticket));
                Assert.Equal(confirmationDigest, attempt.ConfirmationDigest);
                Assert.True(Guid.TryParse(attempt.ActionId, out _));
                var tombstonePath = Assert.Single(Directory.GetFiles(directory, "owned-world-resume-consumed-*.json"));
                var tombstone = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(tombstonePath))!;
                Assert.Equal(1, tombstone.Version);
                Assert.Equal("0.10.35.29145", tombstone.GameVersion);
            }
        }
        internal void WriteJournal(string creationMarker)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
            File.WriteAllText(JournalPath, PluginJson.Serialize(new GameplayJournalDocument
            {
                Version = 1, JournalId = JournalId, OwnedSaveIdentityHash = JournalId,
                GameVersion = _currentNativeMigration || _latestMigration ? "0.10.34.28529" : SourceVersion,
                VersionTransitions = _currentNativeMigration ? new()
                {
                    new() { FromGameVersion = "0.10.34.28529", ToGameVersion = "0.10.35.29088",
                        AdoptedAtGameTick = SavedTick - 4, DurableThroughSequence = 2, RecordedAtUtc = "2026-09-24T00:00:00Z" },
                    new() { FromGameVersion = "0.10.35.29088", ToGameVersion = "0.10.35.29104",
                        AdoptedAtGameTick = SavedTick - 3, DurableThroughSequence = 2, RecordedAtUtc = "2026-09-29T00:00:00Z" },
                    new() { FromGameVersion = "0.10.35.29104", ToGameVersion = "0.10.35.29131",
                        AdoptedAtGameTick = SavedTick - 2, DurableThroughSequence = 2, RecordedAtUtc = "2026-10-13T00:00:00Z" },
                    new() { FromGameVersion = "0.10.35.29131", ToGameVersion = "0.10.35.29145",
                        AdoptedAtGameTick = SavedTick - 1, DurableThroughSequence = 2, RecordedAtUtc = "2026-10-15T00:00:00Z" },
                } : _latestMigration ? new()
                {
                    new() { FromGameVersion = "0.10.34.28529", ToGameVersion = "0.10.35.29088",
                        AdoptedAtGameTick = SavedTick - 3, DurableThroughSequence = 2, RecordedAtUtc = "2026-09-24T00:00:00Z" },
                    new() { FromGameVersion = "0.10.35.29088", ToGameVersion = "0.10.35.29104",
                        AdoptedAtGameTick = SavedTick - 2, DurableThroughSequence = 2, RecordedAtUtc = "2026-09-29T00:00:00Z" },
                    new() { FromGameVersion = "0.10.35.29104", ToGameVersion = "0.10.35.29131",
                        AdoptedAtGameTick = SavedTick - 1, DurableThroughSequence = 2, RecordedAtUtc = "2026-10-13T00:00:00Z" },
                } : new(),
                TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave, HistoricalCoverageComplete = false,
                TrackingStartedAtGameTick = SavedTick, CreatedAtActualTime = creationMarker,
                Entries = new() { new() { Sequence = 1 }, new() { Sequence = 2 } },
            }));
        }
        public void Dispose()
        {
            Sessions.Dispose();
            DSPGame.BeforeLoad = null;
            Directory.Delete(_root, recursive: true); // Only this test's newly created GUID-owned tree.
        }
    }
}
