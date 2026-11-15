using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Theory]
    [InlineData(true, true, false, true)] [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)] [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)] [InlineData(false, false, true, false)]
    public void SurfaceControllerRequiresExactlyOneProvedSavedSourceRole(bool remote, bool savedLanding, bool successfulOutbound, bool expected) =>
        Assert.Equal(expected, VoyagerSurfaceRecoveryPolicy.HasExactSavedSource(remote, savedLanding, successfulOutbound));

    [Fact]
    public void ANoLossSavedSuccessfulOutboundCanPrepareOneDistinctSurfaceRecovery()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true, nativeArmed: true);
        var request = s.PrimaryRecoveryRequest(0);
        s.WriteSave(Scenario.Identity, 12528);
        request.SurfaceRecoveryTarget = new Vector3Snapshot { X = 200, Y = 2, Z = 0 };
        request.SurfaceRecoveryExpectedSourceGameTick = 12528;
        Assert.Null(request.MinimumRecoveryGameTick); Assert.Null(request.ExpectedRecoveryGameTick);
        var result = s.PrepareResult(request);
        Assert.True(result.Success, $"{result.Error?.Code}: {result.Error?.Message}; {result.Error?.Recovery}");
        Assert.True(VoyagerSurfaceRecoveryPolicy.HasMatchingEcho(request, result.Value));
        Assert.True(VoyagerRemotePrimaryRecoveryPolicy.HasMatchingEcho(request, result.Value!));
        Assert.True(result.Value!.SuccessfulOutboundSurfaceSourceUsed);
        Assert.Equal(12528, result.Value.SurfaceRecoveryExpectedSourceGameTick);
        Assert.False(result.Value.CompletedSavedLandingAdmissionUsed);
        Assert.Equal(12528, result.Value!.CandidateGameTick);
        Assert.Equal(0, result.Value.DiscardedUnstoredProgressGameTicks);
        Assert.False(result.Value.NativeOriginalOutcomeProved);
        Assert.Contains("no-loss saved remote world after a proved successful outbound", result.Value.ConfirmationPrompt);
        Assert.Equal(0, DSPGame.LoadCalls);
        var originals = s.ProtectedEvidencePaths.ToDictionary(path => path, File.ReadAllBytes);
        var commit = new CommitOwnedWorldResumeRequest { PlanToken = result.Value.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString(), UserConfirmedInConversation = true,
            ConfirmationDigest = result.Value.ConfirmationDigest };
        var accepted = s.Commit(commit);
        Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(1, DSPGame.LoadCalls); Assert.Equal(Scenario.Identity, DSPGame.LoadedName);
        Assert.True(s.Sessions.RemoteEvidence!.SuccessfulOutboundSurfaceSourceUsed);
        Assert.False(s.Sessions.RemoteEvidence.CompletedSavedLandingAdmissionUsed);
        foreach (var original in originals) Assert.Equal(original.Value, File.ReadAllBytes(original.Key));
        var replay = s.Commit(commit); Assert.True(replay.Success); Assert.True(replay.Value!.IdempotentReplay);
        Assert.Equal(1, DSPGame.LoadCalls);
        // These are synthetic file/loader substitutes; no live arrival or Move is claimed.
    }

    [Theory]
    [InlineData("unarmed")] [InlineData("approved_loss")] [InlineData("source_before_landing")]
    [InlineData("wrong_destination")] [InlineData("requested_other_boundary")] [InlineData("missing_boundary")]
    public void SuccessfulOutboundSurfaceSourceRejectsUnprovedOrLosingCasesBeforeLoad(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true, nativeArmed: fault != "unarmed");
        var request = s.PrimaryRecoveryRequest(fault == "approved_loss" ? 18 : 0);
        if (fault != "approved_loss") s.WriteSave(Scenario.Identity, fault == "source_before_landing" ? 12499 : 12528);
        request.SurfaceRecoveryTarget = new Vector3Snapshot { X = 200, Y = 2, Z = 0 };
        request.SurfaceRecoveryExpectedSourceGameTick = fault == "missing_boundary" ? null : fault == "requested_other_boundary" ? 12529 : fault == "approved_loss" ? 12510 : 12528;
        if (fault == "wrong_destination")
        {
            var path = Path.Combine(s.Handoff, "flight-checkpoint.json");
            var checkpoint = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(path))!;
            checkpoint.DestinationPlanetId++; File.WriteAllText(path, PluginJson.Serialize(checkpoint));
        }
        var result = s.PrepareResult(request);
        Assert.False(result.Success); Assert.Equal(0, DSPGame.LoadCalls);
        if (fault == "unarmed") Assert.Contains("successful_outbound_surface_no_loss_native_witness_required", result.Error!.Message);
        if (fault == "approved_loss") Assert.Contains("successful_outbound_surface_no_loss_native_witness_required", result.Error!.Message);
        if (fault == "missing_boundary") Assert.Contains("successful_outbound_surface_no_loss_native_witness_required", result.Error!.Message);
        if (fault == "requested_other_boundary") Assert.Contains("successful_outbound_surface_no_loss_native_witness_required", result.Error!.Message);
        if (fault is "unarmed" or "approved_loss" or "missing_boundary" or "requested_other_boundary")
            Assert.Equal("successful_surface_no_loss_armed_expected_tick", result.Error!.NativeFirstFailure!.FirstFailedPredicate);
        if (fault == "wrong_destination")
        {
            Assert.Contains("remote_resume_original_evidence_unavailable", result.Error!.Message);
        }
    }

    [Theory]
    [InlineData("valid")] [InlineData("wrong_destination")] [InlineData("wrong_action")] [InlineData("wrong_landing")]
    [InlineData("unarmed")] [InlineData("conservative")] [InlineData("known_newer_progress")]
    [InlineData("accepted_loss")] [InlineData("missing_target")]
    public void SuccessfulSurfaceSourceBindingRejectsChangedOriginalsWithoutRevivingThem(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true, nativeArmed: true);
        var journey = s.Checkpoints.VoyagerJourney!;
        var checkpointPath = Path.Combine(s.Handoff, "flight-checkpoint.json");
        var checkpoint = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(checkpointPath))!;
        if (fault == "wrong_destination") checkpoint.DestinationPlanetId++;
        if (fault == "wrong_action") checkpoint.LastAttemptActionId = Guid.NewGuid().ToString();
        if (fault == "wrong_landing") checkpoint.SuccessfulFlightAtGameTick++;
        if (fault == "unarmed") checkpoint.NativeLaunchArmed = false;
        if (fault == "conservative") journey.SpentGameTicksIsConservative = true;
        var proposal = new VoyagerRecoveryWindowProposal(DateTimeOffset.UtcNow.AddMinutes(55), VoyagerRecoveryWindowProposal.TaskScope,
            9000, 240, surfaceRecoveryTarget: fault == "missing_target" ? null : new Vector3Snapshot { X = 200, Y = 2, Z = 0 });
        using var evidence = VoyagerRemoteResumeEvidence.CreateRemotePrimaryRecovery(PluginJson.Serialize(journey),
            PluginJson.Serialize(checkpoint), "synthetic-source", "synthetic-primary", 12528, 12528,
            new OwnedWorldGameplayJournalCheckpoint(), proposal,
            fault is "known_newer_progress" or "accepted_loss" ? 12529 : 12528, "", "", fault == "accepted_loss");
        var originalFingerprint = evidence.Fingerprint;
        var bound = evidence.TryBindSuccessfulOutboundSurfaceSource();
        Assert.Equal(fault == "valid", bound);
        Assert.Equal(bound, evidence.SuccessfulOutboundSurfaceSourceUsed);
        Assert.False(evidence.CompletedSavedLandingAdmissionUsed);
        if (!bound) Assert.Equal(originalFingerprint, evidence.Fingerprint);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.False(s.Checkpoints.HasCurrentTicket);
    }

    [Fact]
    public void MismatchedLandingWitnessReleasesTheOriginalLeasesBeforeRefusing()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true, nativeArmed: true);
        var request = s.PrimaryRecoveryRequest(0); s.WriteSave(Scenario.Identity, 12528);
        var journey = s.Checkpoints.VoyagerJourney!;
        journey.LastLandedGameTick++; journey.LastAccountedGameTick++;
        Assert.True(s.Checkpoints.TryPersistVoyagerJourney(journey, out var rejection), rejection);
        request.SurfaceRecoveryTarget = new Vector3Snapshot { X = 200, Y = 2, Z = 0 };
        request.SurfaceRecoveryExpectedSourceGameTick = 12528;
        var result = s.PrepareResult(request);
        Assert.False(result.Success); Assert.Contains("successful_outbound_surface_exact_source_unproved", result.Error!.Message);
        Assert.Equal("successful_surface_exact_source", result.Error.NativeFirstFailure!.FirstFailedPredicate);
        Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.ProtectedEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Theory]
    [InlineData("saved_file")] [InlineData("journal")] [InlineData("original_flight")]
    public void SuccessfulOutboundSurfaceCommitRechecksThePreparedSourceBeforeDispatch(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true, nativeArmed: true);
        var request = s.PrimaryRecoveryRequest(0); s.WriteSave(Scenario.Identity, 12528);
        request.SurfaceRecoveryTarget = new Vector3Snapshot { X = 200, Y = 2, Z = 0 };
        request.SurfaceRecoveryExpectedSourceGameTick = 12528;
        var plan = s.Prepare(request);
        if (fault == "saved_file") s.WriteSave(Scenario.Identity, 12529);
        else if (fault == "journal") File.AppendAllText(s.JournalPath, "changed");
        else
        {
            var path = Path.Combine(s.Handoff, "flight-checkpoint.json");
            var checkpoint = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(path))!;
            checkpoint.NativeLaunchArmed = false; File.WriteAllText(path, PluginJson.Serialize(checkpoint));
        }
        var result = s.Commit(plan);
        Assert.False(result.Success); Assert.Equal(0, DSPGame.LoadCalls);
    }
}
