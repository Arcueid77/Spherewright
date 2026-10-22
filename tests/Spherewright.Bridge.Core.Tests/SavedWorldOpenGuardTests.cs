using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class SavedWorldOpenGuardTests
{
    [Theory]
    [InlineData("native-complete", "native_load_completed")]
    [InlineData("paused", "native_paused")]
    [InlineData("tick", "exact_saved_tick")]
    [InlineData("identity", "owned_identity")]
    [InlineData("planet", "owned_planet")]
    [InlineData("world", "world_observed")]
    public void TheSameNativeEventExplainsFailureWithoutLaterResampling(string fault, string predicate)
    {
        var guard = NewGuard();
        Assert.False(guard.ObserveLoad(fault == "world" ? null : new object(), fault != "native-complete",
            fault != "paused", fault == "tick" ? 73 : 72, fault != "identity", fault != "planet"));
        var first = guard.FirstFailure!;
        Assert.Equal(predicate, first.FirstFailedPredicate);
        Assert.Equal("saved_world_open_load_complete", first.Stage);
        Assert.Equal(fault == "tick" ? 73 : 72, first.CapturedAtGameTick);
        Assert.Equal("false", first.Predicates[predicate]);
        first.Predicates[predicate] = "changed detached copy";
        Assert.False(guard.ObserveLoad(new object(), true, true, 72, true, true));
        Assert.Equal("false", guard.FirstFailure!.Predicates[predicate]);
        Assert.False(guard.Completed);
    }

    [Fact]
    public void UnknownPauseIsUnknownAndUnexecutedChecksRemainNotReached()
    {
        var guard = NewGuard();
        Assert.False(guard.ObserveLoad(new object(), true, null, 72, true, true));
        Assert.Equal("unknown", guard.FirstFailure!.Predicates["native_paused"]);
        Assert.Equal("not_reached", guard.FirstFailure.Predicates["owned_identity"]);
        Assert.False(guard.ConfirmJournal(new object(), 72, true, () => throw new Exception("must not execute")));
    }

    [Fact]
    public void JournalBeforeLoadAndDuplicateNativeCompletionStayClosed()
    {
        var early = NewGuard(); var writes = 0;
        Assert.False(early.ConfirmJournal(new object(), 72, true, () => writes++));
        Assert.Equal("native_load_observed", early.FirstFailure!.FirstFailedPredicate);
        Assert.Equal(0, writes);
        var duplicate = NewGuard(); var world = new object();
        Assert.True(duplicate.ObserveLoad(world, true, true, 72, true, true));
        Assert.False(duplicate.ObserveLoad(world, true, true, 72, true, true));
        Assert.Equal("single_load_observation", duplicate.FirstFailure!.FirstFailedPredicate);
    }

    [Theory]
    [InlineData("ownership", "same_loaded_world")]
    [InlineData("tick", "exact_saved_tick")]
    [InlineData("journal", "journal_confirmed")]
    [InlineData("credential", "open_only_credential_persisted")]
    public void AdoptionCannotPassAfterOwnershipProgressJournalOrPersistenceLoss(string fault, string predicate)
    {
        var guard = NewGuard(); var world = new object(); var called = 0;
        Assert.True(guard.ObserveLoad(world, true, true, 72, true, true));
        Assert.False(guard.ConfirmJournal(fault == "ownership" ? new object() : world, fault == "tick" ? 73 : 72,
            fault != "journal", () => { called++; if (fault == "credential") throw new IOException("synthetic"); }));
        Assert.Equal(predicate, guard.FirstFailure!.FirstFailedPredicate);
        Assert.Equal(fault == "credential" ? 1 : 0, called);
        Assert.False(guard.Completed);
    }

    [Theory]
    [InlineData("deadline")]
    [InlineData("charging")]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("context")]
    [InlineData("confirmation")]
    [InlineData("loss-without-exact-bounds")]
    public void SavedOpeningCannotBeUsedToSmuggleOldTaskAuthority(string fault)
    {
        var request = new PrepareOwnedWorldResumeRequest { RecoveryMode = OwnedWorldResumeModes.OpenSavedPrimary };
        switch (fault)
        {
            case "deadline": request.RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(3); break;
            case "charging": request.MaximumChargeGameTicks = 1; break;
            case "completed": request.PriorActionKnownTerminalCompleted = true; break;
            case "failed": request.PriorActionKnownTerminalFailed = true; break;
            case "context": request.ExpectedRemoteRecoveryContextFingerprint = "sha256:" + new string('a', 64); break;
            case "confirmation": request.UserConfirmedInConversation = true; break;
            case "loss-without-exact-bounds": request.AcceptUnstoredProgressLoss = true; break;
        }
        Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(request));
    }

    private static SavedWorldOpenGuard NewGuard() => new(Guid.NewGuid().ToString(), 72);
}
