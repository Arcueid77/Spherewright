using Newtonsoft.Json.Linq;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Theory]
    [InlineData("default", 54000)]
    [InlineData("open_saved_primary", 54000)]
    [InlineData("resume_return_only_task", 54000)]
    [InlineData("authorize_remote_primary_recovery", -1)]
    [InlineData("authorize_remote_primary_recovery", 108001)]
    public void AdditionalBudgetRejectsImplicitModeAndInvalidAmount(string mode, long extra)
    {
        var request = WindowRequest(); request.RecoveryMode = mode; request.AdditionalVoyagerGameTicks = extra;
        Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(request));
    }

    [Fact]
    public void AdditionalBudgetIsDigestBoundAndNotAddedToAnEarlierProposal()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        s.WriteSave(Scenario.Identity, 12510);
        var request = WindowRequest(); request.RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery;
        request.AdditionalVoyagerGameTicks = 54000;
        var first = s.Prepare(request);
        request.AdditionalVoyagerGameTicks = 54001;
        var second = s.Prepare(request);
        Assert.NotEqual(first.ConfirmationDigest, second.ConfirmationDigest);
        Assert.Equal(162000, first.EffectiveJourneyMaximumGameTicks);
        Assert.Equal(162001, second.EffectiveJourneyMaximumGameTicks);
        Assert.False(s.Commit(new CommitOwnedWorldResumeRequest {
            PlanToken = first.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            UserConfirmedInConversation = true, ConfirmationDigest = second.ConfirmationDigest }).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void LegacyProposalShapeAndOriginalCapAreRetained()
    {
        var old = new VoyagerRecoveryWindowProposal(DateTimeOffset.UtcNow.AddMinutes(30),
            VoyagerRecoveryWindowProposal.TaskScope, 18000, 300);
        var json = PluginJson.Serialize(old);
        Assert.DoesNotContain("additionalVoyagerGameTicks", json);
        Assert.Equal(0, PluginJson.Deserialize<VoyagerRecoveryWindowProposal>(json)!.AdditionalVoyagerGameTicks);
        Assert.Equal(108000, VoyagerRecoveryWindowPolicy.EffectiveMaximumGameTicks(108000, old));
        Assert.Equal(162000, VoyagerRecoveryWindowPolicy.EffectiveMaximumGameTicks(108000,
            new(old.DeadlineUtc, old.Scope, 18000, 300, 54000)));
        Assert.Null(VoyagerRecoveryWindowPolicy.EffectiveMaximumGameTicks(long.MaxValue,
            new(old.DeadlineUtc, old.Scope, 18000, 300, 1)));
    }

    [Fact]
    public void NewFiniteBudgetFromPrimaryCanPrepareWithoutRefundingExhaustedOriginalAllowance()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var journey = s.Checkpoints.VoyagerJourney!;
        journey.SpentGameTicks = journey.MaximumGameTicks - journey.ReturnReserveGameTicks - 13 - 10;
        Assert.True(s.Checkpoints.TryPersistVoyagerJourney(journey, out var rejection), rejection);
        s.WriteSave(Scenario.Identity, 12510);
        var originals = s.ProtectedEvidencePaths.ToDictionary(p => p, File.ReadAllBytes);
        var request = WindowRequest(); request.RecoveryMode = OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery;
        var wire = JObject.Parse(PluginJson.Serialize(request));
        wire["additionalVoyagerGameTicks"] = 54000;
        request = PluginJson.Deserialize<PrepareOwnedWorldResumeRequest>(wire.ToString())!;
        var result = s.PrepareResult(request);
        Assert.True(result.Success, result.Error?.Message);
        var p = JObject.Parse(PluginJson.Serialize(result.Value!));
        Assert.Equal(54000, p.Value<long>("additionalVoyagerGameTicks"));
        Assert.Equal(108000, p.Value<long>("originalJourneyMaximumGameTicks"));
        Assert.Equal(162000, p.Value<long>("effectiveJourneyMaximumGameTicks"));
        Assert.True(result.Value!.UserConfirmationRequired);
        Assert.False(result.Value.CommitAllowedNow);
        Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var original in originals) Assert.Equal(original.Value, File.ReadAllBytes(original.Key));
        var committed = s.Commit(result.Value);
        Assert.True(committed.Success, committed.Error?.Message);
        Assert.Equal(1, DSPGame.LoadCalls);
        Assert.Equal(journey.SpentGameTicks, s.Checkpoints.VoyagerJourney!.SpentGameTicks);
        Assert.Equal(journey.MaximumGameTicks, s.Checkpoints.VoyagerJourney.MaximumGameTicks);
        Assert.Equal(journey.DeadlineUtc, s.Checkpoints.VoyagerJourney.DeadlineUtc);
        Assert.Equal(54000, JObject.Parse(PluginJson.Serialize(s.Sessions.RecoveryAuthorization!.Proposal))
            .Value<long>("additionalVoyagerGameTicks"));
    }
}
