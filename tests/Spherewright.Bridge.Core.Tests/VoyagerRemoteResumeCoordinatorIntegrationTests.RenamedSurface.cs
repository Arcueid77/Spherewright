using System.Security.Cryptography;
using System.Text;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void RenamedFixedExitCanPrepareAndDispatchANewSurfaceRecoveryWithoutChangingOriginalFiles()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true, nativeArmed: true);
        var request = PrepareRenamedSurface(s);
        var originals = s.ProtectedEvidencePaths.ToDictionary(path => path, File.ReadAllBytes);
        var plan = s.PrepareResult(request);
        Assert.True(plan.Success, $"{plan.Error?.Code}: {plan.Error?.Message}");
        Assert.True(plan.Value!.RenamedLastExitIdentityRestorationPlanned);
        Assert.True(plan.Value.SuccessfulOutboundSurfaceSourceUsed);
        Assert.False(plan.Value.CompletedSavedLandingAdmissionUsed);
        Assert.Equal(request.ExpectedRenamedLastExitContentHash, plan.Value.ExpectedRenamedLastExitContentHash);
        Assert.Equal(12528, plan.Value.CandidateGameTick);
        Assert.Equal(0, plan.Value.DiscardedUnstoredProgressGameTicks);
        Assert.Contains("saved under a different name", plan.Value.ConfirmationPrompt);
        Assert.Equal(0, DSPGame.LoadCalls);
        var accepted = s.Commit(plan.Value);
        Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(1, DSPGame.LoadCalls);
        Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        Assert.True(s.Sessions.RemoteEvidence!.RenamedLastExitIdentityRestoration);
        foreach (var original in originals) Assert.Equal(original.Value, File.ReadAllBytes(original.Key));
        // The loader and Unity world here are substitutes, not live arrival.
    }

    [Theory]
    [InlineData("world")] [InlineData("hash")] [InlineData("tick")] [InlineData("same_name")]
    [InlineData("loss")] [InlineData("missing_hash")]
    public void RenamedFixedExitRejectsDifferentWorldOrUnprovedCurrentFileBeforeLoad(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true, nativeArmed: true);
        var request = PrepareRenamedSurface(s, fault);
        var plan = s.PrepareResult(request);
        Assert.False(plan.Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void RenamedSourceIsRecheckedAtCommitAndCannotDispatchAfterItChanges()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true, nativeArmed: true);
        var request = PrepareRenamedSurface(s);
        var plan = s.PrepareResult(request); Assert.True(plan.Success, plan.Error?.Message);
        WriteWorldIdentitySave(GameSave.LastExit, "synthetic-renamed-world", 12528, 74);
        Assert.False(s.Commit(plan.Value!).Success);
        Assert.Equal(0, DSPGame.LoadCalls);
    }

    private static PrepareOwnedWorldResumeRequest PrepareRenamedSurface(Scenario s, string? fault = null)
    {
        var request = s.PrimaryRecoveryRequest(0);
        WriteWorldIdentitySave(Scenario.Identity, Scenario.Identity, 12510, 73);
        WriteWorldIdentitySave(GameSave.LastExit, fault == "same_name" ? Scenario.Identity : "synthetic-renamed-world",
            fault == "tick" ? 12527 : 12528, fault == "world" ? 74 : 73);
        request.SurfaceRecoveryTarget = new Vector3Snapshot { X = 200, Y = 2, Z = 0 };
        request.SurfaceRecoveryExpectedSourceGameTick = 12528;
        request.RestoreRenamedLastExitIdentity = true;
        using var hash = SHA256.Create();
        request.ExpectedRenamedLastExitContentHash = "sha256:" + BitConverter.ToString(hash.ComputeHash(
            File.ReadAllBytes(GameSave.SavePath(GameSave.LastExit)))).Replace("-", "").ToLowerInvariant();
        if (fault == "hash") request.ExpectedRenamedLastExitContentHash = "sha256:" + new string('0', 64);
        if (fault == "missing_hash") request.ExpectedRenamedLastExitContentHash = string.Empty;
        if (fault == "loss") request.AcceptUnstoredProgressLoss = true;
        return request;
    }

    private static void WriteWorldIdentitySave(string slot, string identity, long tick, int seed)
    {
        using var stream = OwnedSavePrefixReaderTests.Fixture(identity: identity, versionPatch: 35,
            versionBuild: 29151, patch: 23, gameDescVersion: 10);
        stream.Position = 0;
        var prefix = Spherewright.Bridge.Core.Safety.OwnedSavePrefixReader.Read(stream, identity);
        stream.SetLength(prefix.PrefixEnd); stream.Position = prefix.PrefixEnd;
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(638000000000000000L);
            writer.Write(0); writer.Write(10); writer.Write(35); writer.Write(29151);
            writer.Write(20200403); writer.Write(seed); writer.Write(64); writer.Write(0); writer.Write(1);
            stream.Position = 36; writer.Write(tick); stream.Position = 6; writer.Write(stream.Length);
        }
        File.WriteAllBytes(GameSave.SavePath(slot), stream.ToArray());
    }
}
