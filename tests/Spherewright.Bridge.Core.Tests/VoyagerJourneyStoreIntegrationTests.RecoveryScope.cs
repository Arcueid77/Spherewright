using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    private static VoyagerRemoteResumeEvidence BindAccountingRecovery(Fixture f, out VoyagerRecoveryAuthorization authority)
    {
        var original = Copy(f.Record, r => { r.Phase = "remote_landed"; r.ReturnLeg = false;
            r.SpentGameTicks = 40; r.LastAccountedGameTick = 90; r.LastLandedGameTick = 90; });
        var proposal = new VoyagerRecoveryWindowProposal(DateTimeOffset.UtcNow.AddHours(4),
            VoyagerRecoveryWindowProposal.TaskScope, 18000, 300, additionalVoyagerGameTicks: 6000);
        var evidence = VoyagerRemoteResumeEvidence.CreateRemotePrimaryRecovery(PluginJson.Serialize(original),
            PluginJson.Serialize(f.Ticket), "synthetic-source", "synthetic-primary", 100, 100,
            new OwnedWorldGameplayJournalCheckpoint(), proposal, 120, "synthetic-exit", "synthetic-journal", true);
        authority = new("synthetic-resume", evidence.Fingerprint, "synthetic-digest", DateTimeOffset.UtcNow, proposal);
        f.Sessions.BindAccountingRecoveryForTest(evidence, authority);
        return evidence;
    }

    [Fact]
    public void RecoveredReturnAndCoveringHomeSaveDoNotLendTheirDebitOrAllowanceToTheNextVoyage()
    {
        using var f = new Fixture(returnLeg: true, returnedHome: true, maximumRoundTripWallSeconds: 43200);
        using var evidence = BindAccountingRecovery(f, out var authority);
        var original = evidence.Journey;
        Assert.Equal(60, f.Sessions.EffectiveVoyagerSpentGameTicks(original, 40));
        Assert.Equal(authority.Proposal.DeadlineUtc, f.Sessions.EffectiveVoyagerDeadline(original));
        Assert.Equal(original.MaximumGameTicks + 6000, f.Sessions.EffectiveVoyagerMaximumGameTicks(original));

        // Normal linked Store closure, not an edited/completed phase substitute.
        f.WriteSyntheticPrimaryProof(150);
        Assert.True(f.CompleteNormalSave(f.PrimaryFingerprint(), out var rejection), rejection);
        Assert.Equal("completed", f.Store.VoyagerJourney!.Phase);
        var closedBytes = File.ReadAllBytes(f.JourneyPath);
        var next = Copy(original, r => { r.JourneyId = Guid.NewGuid().ToString("N"); r.Phase = "outbound";
            r.StartedAtGameTick = 200; r.SpentGameTicks = 0; r.LastAccountedGameTick = 200;
            r.LastLandedGameTick = null; r.MaximumGameTicks = 108000; r.ReturnReserveGameTicks = 54000;
            r.IssuedAtUtc = DateTimeOffset.UtcNow; r.DeadlineUtc = r.IssuedAtUtc.AddHours(12); });
        Assert.Equal(7646, f.Sessions.EffectiveVoyagerSpentGameTicks(next, 7646));
        Assert.Equal(next.DeadlineUtc, f.Sessions.EffectiveVoyagerDeadline(next));
        Assert.Equal(108000, f.Sessions.EffectiveVoyagerMaximumGameTicks(next));
        Assert.Equal(closedBytes, File.ReadAllBytes(f.JourneyPath));
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData("world")] [InlineData("session")] [InlineData("save")]
    [InlineData("version")] [InlineData("action")] [InlineData("owned")]
    public void SameJourneyWithBrokenRecoveryContextStaysUnknown(string fault)
    {
        using var f = new Fixture(); using var evidence = BindAccountingRecovery(f, out _);
        f.Sessions.BreakAccountingRecoveryForTest(fault);
        Assert.Null(f.Sessions.EffectiveVoyagerSpentGameTicks(evidence.Journey, 80));
        Assert.Equal(evidence.Journey.DeadlineUtc, f.Sessions.EffectiveVoyagerDeadline(evidence.Journey));
    }

    [Theory]
    [InlineData("energy")] [InlineData("maximum")] [InlineData("wall_field")]
    [InlineData("deadline")] [InlineData("spent_regressed")] [InlineData("accounted_regressed")]
    public void MatchingIdCannotHideMutatedImmutableLimitsOrRegressedAccounting(string fault)
    {
        using var f = new Fixture(maximumRoundTripWallSeconds: 43200);
        using var evidence = BindAccountingRecovery(f, out _); var changed = evidence.Journey;
        switch (fault)
        {
            case "energy": changed.ReturnEnergyFloorJoules++; break;
            case "maximum": changed.MaximumGameTicks++; break;
            case "wall_field": changed.MaximumRoundTripWallSeconds = null; break;
            case "deadline": changed.DeadlineUtc = changed.DeadlineUtc.AddSeconds(1); break;
            case "spent_regressed": changed.SpentGameTicks--; break;
            case "accounted_regressed": changed.LastAccountedGameTick--; break;
        }
        Assert.Null(f.Sessions.EffectiveVoyagerSpentGameTicks(changed, 80));
    }

    [Fact]
    public void ReturnOnlyClockTakesPrecedenceWhileSameJourneyProgressDebitRemainsConservedOnce()
    {
        using var f = new Fixture(); using var evidence = BindAccountingRecovery(f, out _);
        var current = evidence.Journey;
        current.ReturnOnlyTask = new() { SessionId = f.Sessions.SessionId!, DeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(30) };
        Assert.Equal(current.ReturnOnlyTask.CurrentDeadlineUtc.AddSeconds(-VoyagerControlDeadlinePolicy.StopCleanupSeconds),
            f.Sessions.EffectiveVoyagerDeadline(current));
        Assert.Equal(current.MaximumGameTicks, f.Sessions.EffectiveVoyagerMaximumGameTicks(current));
        Assert.Equal(60, f.Sessions.EffectiveVoyagerSpentGameTicks(current, 40));
        Assert.Equal(60, f.Sessions.EffectiveVoyagerSpentGameTicks(current, 60));
        Assert.Equal(80, f.Sessions.EffectiveVoyagerSpentGameTicks(current, 80));
    }

    [Theory]
    [InlineData("save")] [InlineData("version")] [InlineData("owned")]
    public void AnUnrelatedIdDoesNotBypassCurrentOwnedIdentity(string fault)
    {
        using var f = new Fixture(); using var evidence = BindAccountingRecovery(f, out _);
        var next = Copy(evidence.Journey, r => r.JourneyId = Guid.NewGuid().ToString("N"));
        f.Sessions.BreakAccountingRecoveryForTest(fault);
        Assert.Null(f.Sessions.EffectiveVoyagerSpentGameTicks(next, 80));
    }

    [Theory]
    [InlineData(false, false, 80L)]
    [InlineData(true, false, null)]
    [InlineData(true, true, 80L)]
    public void AccountingScopeDistinguishesNoPrimaryDebitFromUnprovedSameJourney(bool primary, bool scope, long? expected)
    {
        Assert.Equal(expected, VoyagerRecoveryAccountingScopePolicy.ConservedSpent("same", "same", primary, scope,
            80, 40, 100, 120));
        Assert.Null(VoyagerRecoveryAccountingScopePolicy.ConservedSpent("new", "old", primary, scope,
            -1, 40, 100, 120));
    }
}
