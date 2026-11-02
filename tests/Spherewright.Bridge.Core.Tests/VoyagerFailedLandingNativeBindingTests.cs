using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Theory]
    [InlineData("valid")][InlineData("pending")][InlineData("other_session")][InlineData("other_action")]
    [InlineData("other_planet")][InlineData("other_identity")][InlineData("closed_source")]
    [InlineData("changed_accounting")][InlineData("invented_landing")]
    public void FailedLandingNativeBindingNeedsTheNewAdoptedSessionAndOriginalSavedAccounting(string fault)
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        var oldData = GameMain.data; var oldPlayer = GameMain.mainPlayer; var oldPlanet = GameMain.localPlanet;
        var oldTick = GameMain.gameTick; var oldPaused = GameMain.isPaused;
        try
        {
            Assert.True(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, FailedLandingRequest(), out var source, out _));
            using var evidence = VoyagerRemoteResumeEvidence.CreateFailedLandingRecovery(source!);
            var action = Guid.NewGuid().ToString(); var session = Guid.NewGuid().ToString();
            Assert.True(s.Checkpoints.TryRegisterFailedLandingAdmission(source!, action, true, LandingDigest, DateTimeOffset.UtcNow, out _));
            evidence.BindRegisteredFailedLandingJourney(PluginJson.Serialize(s.Checkpoints.VoyagerJourney));
            if (fault != "pending") Assert.True(s.Checkpoints.TryBindFailedLandingAdmissionSession(action, session, 12510, out _));
            var snapshot = s.Sessions.Snapshot;
            snapshot.SessionId = session; snapshot.OwnedBySpherewright = true; snapshot.SaveName = Scenario.Identity;
            snapshot.LocalPlanetId = 201; snapshot.GameTick = 12510; snapshot.GameVersion = source!.Journey.GameVersion;
            s.Sessions.BindCheckpointNativeWorldFromSnapshotForTest(s.Checkpoints);
            var adoption = new VoyagerRemoteResumeAdoption
            {
                World = GameMain.data, Player = GameMain.mainPlayer!, SessionId = session,
                ResumeActionId = action, Evidence = evidence,
            };
            var j = s.Checkpoints.VoyagerJourney!;
            switch (fault)
            {
                case "other_session": adoption.SessionId = Guid.NewGuid().ToString(); break;
                case "other_action": adoption.ResumeActionId = Guid.NewGuid().ToString(); break;
                case "other_planet": GameMain.localPlanet!.id++; break;
                case "other_identity": GameMain.data!.gameName = "foreign-world"; break;
                case "closed_source": evidence.Dispose(); break;
                case "changed_accounting": j.LastAccountedGameTick++; break;
                case "invented_landing": j.LastLandedGameTick = 12510; break;
            }
            var native = new NormalGameActionCoordinator(s.Checkpoints, s.Sessions);
            Assert.Equal(fault == "valid", native.FailedLandingBindingForTest(adoption, j));
            Assert.Null(s.Checkpoints.VoyagerJourney!.LastLandedGameTick);
            Assert.Equal(0, DSPGame.LoadCalls);
        }
        finally
        {
            GameMain.data = oldData; GameMain.mainPlayer = oldPlayer; GameMain.localPlanet = oldPlanet;
            GameMain.gameTick = oldTick; GameMain.isPaused = oldPaused;
        }
    }
}
