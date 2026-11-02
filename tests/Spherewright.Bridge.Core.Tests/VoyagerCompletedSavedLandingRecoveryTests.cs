using BepInEx.Logging;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Theory]
    [InlineData("valid")][InlineData("older_primary")][InlineData("other_remote")]
    [InlineData("changed_landing")][InlineData("refunded_time")][InlineData("changed_checkpoint")]
    public void CompletedSavedLandingCoversNormalSaveNewCredentialAndFreshRemotePrimaryRecovery(string fault)
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        var previousSave = GameSave.FlightStartSave;
        try
        {
            var accepted = s.Commit(s.Prepare(FailedLandingRequest())); Assert.True(accepted.Success, accepted.Error?.Message);
            Assert.True(s.Sessions.AdoptFailedLandingNativeShellForTest(s.Checkpoints, s.Tickets));
            GameMain.gameTick = 12512;
            Assert.True(s.Sessions.CompleteContactForTest(null));
            s.Sessions.BindFailedLandingResaveShellForTest(new NormalGameActionCoordinator(s.Checkpoints, s.Sessions));
            GameSave.FlightStartSave = name => { s.WriteSave(name, GameMain.gameTick); return true; };
            Assert.True(s.Sessions.TrySaveOwnedWorldNowOnMainThread(out var saveError), saveError);
            var newToken = s.Tickets.CurrentResumeToken!;
            Assert.NotEqual(s.Token, newToken);
            Assert.True(s.Tickets.TryGetActiveTicket(newToken, out var credential, out _));
            Assert.Equal(201, credential!.ExpectedPlanetId); Assert.Equal(12512, credential.MinimumGameTick);
            var cpPath = Path.Combine(s.Handoff, "flight-checkpoint.json");
            var cpBefore = File.ReadAllBytes(cpPath);
            Assert.Null(s.Checkpoints.CurrentTicket!.SuccessfulFlightAtGameTick);
            Assert.Equal("recovery_required", s.Checkpoints.CurrentTicket.LifecycleState);
            // Corruption is confined to synthetic test files and detected by
            // freshly reopened production stores, not repaired behind them.
            var jPath = Path.Combine(s.Handoff, "voyager-journey.json");
            var j = s.Checkpoints.VoyagerJourney!;
            switch (fault)
            {
                case "older_primary": s.WriteSave(Scenario.Identity, 12511); break;
                case "other_remote": j.RemotePlanetId = 202; break;
                case "changed_landing": j.LastLandedGameTick++; break;
                case "refunded_time": j.SpentGameTicks--; break;
                case "changed_checkpoint":
                    var cp = s.Checkpoints.CurrentTicket!; cp.LastAttemptActionId = Guid.NewGuid().ToString();
                    File.WriteAllText(cpPath, PluginJson.Serialize(cp)); break;
            }
            if (fault is "other_remote" or "changed_landing" or "refunded_time") File.WriteAllText(jPath, PluginJson.Serialize(j));
            s.Sessions.Dispose();
            var store = new FlightCheckpointStore("synthetic-reopened", j.GameVersion, new ManualLogSource(), s.Handoff);
            var tickets = new OwnedWorldResumeTicketStore(s.Runtime, s.Handoff, "synthetic-reopened", j.GameVersion, new ManualLogSource());
            using var session = new GameSessionTracker { RemoteResumeCheckpoints = store, RequiresRemoteResumeEvidence = true };
            var coordinator = new OwnedWorldResumeCoordinator(true, 30, 30, 16, session, tickets);
            var request = WindowRequest(); request.ResumeToken = newToken;
            request.RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery;
            request.MinimumRecoveryGameTick = request.ExpectedRecoveryGameTick = 12512;
            var prepared = coordinator.PrepareOnMainThread(request);
            if (fault == "valid") Assert.True(prepared.Success, prepared.Error?.Message);
            else Assert.False(prepared.Success);
            if (fault != "valid") return;
            var p = prepared.Value!;
            Assert.True(p.CompletedSavedLandingAdmissionUsed);
            Assert.Equal(j.ActiveActionId, p.OriginalFailedFlightActionId);
            Assert.True(VoyagerRemotePrimaryRecoveryPolicy.HasMatchingEcho(request, p));
            Assert.Contains("original outbound remains failed", p.ConfirmationPrompt);
            Assert.Null(p.SuccessfulFlightWitnessRetiredAtGameTick);
            var retired = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(cpPath))!;
            Assert.Null(retired.SuccessfulFlightAtGameTick);
            Assert.Equal("retired", retired.LifecycleState); // Native covering-primary retirement only.
            Assert.Equal(System.Text.Encoding.UTF8.GetString(cpBefore), store.VoyagerJourney!.FailedLandingAdmissions.Single().OriginalCheckpointJson);
            Assert.Equal(1, DSPGame.LoadCalls); // Prepare did not start another load.
        }
        finally { GameSave.FlightStartSave = previousSave; }
    }
}
