using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void CoveringPrimaryRetiresReloadCapabilityButKeepsSuccessfulOutboundAsBoundHistoricalEvidence()
    {
        using var s = new Scenario();
        var journeyBytes = File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json"));
        s.WriteSave(Scenario.Identity, 12510);
        Assert.True(s.Checkpoints.TryRetireAfterPrimarySave(Scenario.Identity, 12510, out var retired, out _));
        Assert.True(retired);
        var cp = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(Path.Combine(s.Handoff, "flight-checkpoint.json")))!;
        Assert.False(s.Checkpoints.TryGetActiveTicket(cp.ReloadToken, out _, out _));
        // Reopen the production Store as a new Host would. Native Save/Export
        // and Unity callbacks remain outside this filesystem/coordinator test.
        s.ReopenCoveredWitness(cp.GameVersion);
        Assert.False(s.Sessions.RemoteResumeCheckpoints!.TryGetActiveTicket(cp.ReloadToken, out _, out _));
        var result = s.PrepareResult(new PrepareOwnedWorldResumeRequest());
        Assert.True(result.Success, result.Error?.Message);
        var prepared = result.Value!;
        Assert.True(prepared.UserConfirmationRequired); Assert.False(prepared.CommitAllowedNow);
        Assert.Equal(12510, prepared.CandidateGameTick);
        Assert.Equal(12510, prepared.SuccessfulFlightWitnessRetiredAtGameTick);
        Assert.Contains("historical witness only", prepared.ConfirmationPrompt);
        Assert.Equal(journeyBytes, File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json")));
        Assert.True(s.Commit(prepared).Success);
        Assert.Equal(1, DSPGame.LoadCalls); Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        Assert.False(s.Checkpoints.TryGetActiveTicket(cp.ReloadToken, out _, out _));
    }

    [Fact]
    public void FailedRemoteContinuationBindsTheCoveringPrimaryWithoutRenewingTheOriginalJourney()
    {
        using var s = new Scenario(expiredJourney: true);
        var request = s.SeedZeroProgressReauthorization();
        var journey = File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json"));
        var prior = s.PriorConsumptionPaths.Select(File.ReadAllBytes).ToArray();
        s.WriteSave(Scenario.Identity, 12501);
        Assert.True(s.Checkpoints.TryRetireAfterPrimarySave(Scenario.Identity, 12501, out var retired, out _));
        Assert.True(retired);
        var cp = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(Path.Combine(s.Handoff, "flight-checkpoint.json")))!;
        s.ReopenCoveredWitness(cp.GameVersion);
        request.ExpectedRemoteRecoveryContextFingerprint = s.CurrentOwnerContext();
        var prepared = s.Prepare(request);
        Assert.True(prepared.FailedRemoteLastExitReauthorizationUsed);
        Assert.Equal(12501, prepared.SuccessfulFlightWitnessRetiredAtGameTick);
        Assert.Equal(s.OriginalDeadline, prepared.OriginalJourneyDeadlineUtc);
        Assert.True(s.Commit(prepared).Success);
        Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        Assert.Equal(journey, File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json")));
        for (var i = 0; i < prior.Length; i++) Assert.Equal(prior[i], File.ReadAllBytes(s.PriorConsumptionPaths[i]));
        Assert.False(s.Sessions.RemoteResumeCheckpoints!.TryGetActiveTicket(cp.ReloadToken, out _, out _));
    }

    [Fact]
    public void APreparedHistoricalWitnessCannotBorrowChangedRetirementBytesAtCommit()
    {
        using var s = new Scenario();
        s.WriteSave(Scenario.Identity, 12510);
        Assert.True(s.Checkpoints.TryRetireAfterPrimarySave(Scenario.Identity, 12510, out _, out _));
        var prepared = s.Prepare();
        var path = Path.Combine(s.Handoff, "flight-checkpoint.json");
        var cp = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(path))!;
        cp.RetiredAtUtc = cp.RetiredAtUtc!.Value.AddTicks(-1);
        File.WriteAllText(path, PluginJson.Serialize(cp));
        s.ReopenCoveredWitness(cp.GameVersion);
        Assert.False(s.Commit(prepared).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    [Theory]
    [InlineData("newer_primary")] [InlineData("retirement_after_primary")]
    [InlineData("retirement_before_success")] [InlineData("no_success")]
    [InlineData("reload_pending")] [InlineData("reload_action")]
    [InlineData("missing_retirement_tick")] [InlineData("missing_retirement_time")]
    [InlineData("failed_attempt")] [InlineData("mixed_failure")]
    [InlineData("success_before_checkpoint")]
    [InlineData("expired_ticket")]
    public void AContradictoryRetiredWitnessNeverDispatchesALoad(string fault)
    {
        using var s = new Scenario();
        s.WriteSave(Scenario.Identity, fault == "newer_primary" ? 12511 : 12510);
        Assert.True(s.Checkpoints.TryRetireAfterPrimarySave(Scenario.Identity, 12510, out var retired, out _)); Assert.True(retired);
        if (fault != "newer_primary")
        {
            var path = Path.Combine(s.Handoff, "flight-checkpoint.json");
            var cp = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(path))!;
            switch (fault)
            {
                case "retirement_after_primary": cp.RetiredAtGameTick = 12511; break;
                case "retirement_before_success": cp.RetiredAtGameTick = cp.SuccessfulFlightAtGameTick - 1; break;
                case "no_success": cp.SuccessfulFlightAtGameTick = null; break;
                case "reload_pending": cp.ReloadPending = true; break;
                case "reload_action": cp.ReloadActionId = Guid.NewGuid().ToString(); break;
                case "missing_retirement_tick": cp.RetiredAtGameTick = null; break;
                case "missing_retirement_time": cp.RetiredAtUtc = null; break;
                case "failed_attempt":
                    cp.LifecycleState = FlightCheckpointLifecycleStates.RecoveryRequired;
                    cp.SuccessfulFlightAtGameTick = null; cp.RecoveryRequiredAtGameTick = 12500; break;
                case "mixed_failure": cp.RecoveryRequiredAtGameTick = 12500; break;
                case "success_before_checkpoint": cp.SuccessfulFlightAtGameTick = cp.SavedGameTick - 1; break;
                case "expired_ticket": s.ReplaceCurrentTicket(t => t.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)); break;
            }
            File.WriteAllText(path, PluginJson.Serialize(cp));
            s.ReopenCoveredWitness(cp.GameVersion);
        }
        Assert.False(s.PrepareResult(new()).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    private sealed partial class Scenario
    {
        internal void ReopenCoveredWitness(string nativeVersion) =>
            Sessions.RemoteResumeCheckpoints = new FlightCheckpointStore("test-bridge", nativeVersion,
                new BepInEx.Logging.ManualLogSource(), Handoff);
    }
}
