using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void RetainedJourneyMatchesDifferentGetterCopiesWithExactCapturedContent()
    {
        using var s = new Scenario();
        using var evidence = OpenRetainedJourneyStoreEvidence(s);
        var retained = s.Checkpoints.VoyagerJourney!;
        var another = s.Checkpoints.VoyagerJourney!;

        Assert.NotSame(retained, another);
        Assert.Equal(PluginJson.Serialize(retained), PluginJson.Serialize(another));
        Assert.Equal(evidence.JourneyJson, PluginJson.Serialize(retained));
        Assert.True(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(retained, evidence));
        Assert.True(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(another, evidence));
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void RetainedCopyDriftIsRejectedEvenWhenActualStoreStillMatchesEvidence()
    {
        using var s = new Scenario();
        using var evidence = OpenRetainedJourneyStoreEvidence(s);
        var retained = s.Checkpoints.VoyagerJourney!;
        Assert.True(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(retained, evidence));

        retained.SpentGameTicks++;

        Assert.True(s.Checkpoints.RemoteResumeEvidenceStillMatches(evidence));
        Assert.False(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(retained, evidence));
    }

    [Theory]
    [InlineData("spent")]
    [InlineData("return-floor")]
    public void PersistedJourneyDriftRejectsTheUnchangedRetainedCopy(string change)
    {
        using var s = new Scenario();
        using var evidence = OpenRetainedJourneyStoreEvidence(s);
        var retained = s.Checkpoints.VoyagerJourney!;
        Assert.True(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(retained, evidence));
        // Release the original file leases only to exercise a real Store transition.
        // This content-comparison regression attempts no recovery or admission.
        evidence.Dispose();
        var changed = s.Checkpoints.VoyagerJourney!;
        if (change == "spent") changed.SpentGameTicks++;
        else changed.ReturnEnergyFloorJoules++;
        Assert.True(s.Checkpoints.TryPersistVoyagerJourney(changed, out var rejection), rejection);
        Assert.Equal(PluginJson.Serialize(changed),
            File.ReadAllText(Path.Combine(s.Handoff, "voyager-journey.json")));

        Assert.Equal(evidence.JourneyJson, PluginJson.Serialize(retained));
        Assert.False(s.Checkpoints.RemoteResumeEvidenceStillMatches(evidence));
        Assert.False(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(retained, evidence));
    }

    [Fact]
    public void PersistedCheckpointLifecycleDriftRejectsAnUnchangedJourney()
    {
        using var s = new Scenario();
        using var evidence = OpenRetainedJourneyStoreEvidence(s);
        var retained = s.Checkpoints.VoyagerJourney!;
        Assert.True(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(retained, evidence));
        evidence.Dispose();
        Assert.True(s.Checkpoints.TryRetireAfterPrimarySave(Scenario.Identity,
            retained.LastLandedGameTick!.Value, out var retired, out var rejection), rejection);
        Assert.True(retired);
        var checkpoint = PluginJson.Deserialize<FlightCheckpointTicket>(
            File.ReadAllText(Path.Combine(s.Handoff, "flight-checkpoint.json")))!;
        Assert.Equal(FlightCheckpointLifecycleStates.Retired, checkpoint.LifecycleState);

        Assert.Equal(evidence.JourneyJson, PluginJson.Serialize(s.Checkpoints.VoyagerJourney));
        Assert.False(s.Checkpoints.RemoteResumeEvidenceStillMatches(evidence));
        Assert.False(s.Checkpoints.RemoteResumeRetainedJourneyStillMatches(retained, evidence));
    }

    private static VoyagerRemoteResumeEvidence OpenRetainedJourneyStoreEvidence(Scenario s)
    {
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out var ticket, out var ticketRejection), ticketRejection);
        using var source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), Scenario.Identity);
        using var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(Scenario.Identity), Scenario.Identity);
        Assert.True(s.Checkpoints.TryOpenRemoteResumeEvidence(ticket!, source, primary,
            out var evidence, out var rejection), rejection);
        return evidence!;
    }
}
