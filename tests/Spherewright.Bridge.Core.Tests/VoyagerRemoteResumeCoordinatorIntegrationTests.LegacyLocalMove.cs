using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void LegacyJournalBindsRawBomAndAllBytesRatherThanOnlyTheValidatedText()
    {
        using var s = new Scenario();
        File.WriteAllBytes(s.JournalPath, System.Text.Encoding.UTF8.GetPreamble().Concat(File.ReadAllBytes(s.JournalPath)).ToArray());
        var request = s.SeedLegacyLocalMove();
        Assert.True(s.PrepareResult(request).Success);
        var root = JObject.Parse(request.LegacyLocalMoveUnknownEvidenceJson);
        root["journalSha256"] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(s.JournalPath)))).ToLowerInvariant();
        request.LegacyLocalMoveUnknownEvidenceJson = root.ToString(Formatting.None);
        request.ExpectedLegacyLocalMoveUnknownEvidenceHash = VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(request.LegacyLocalMoveUnknownEvidenceJson)!;
        Assert.False(s.PrepareResult(request).Success); Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void LegacyMoveBindsCallerAttestationWithoutChangingTheUnknownOrSuccessfulFlight()
    {
        using var s = new Scenario(); var r = s.SeedLegacyLocalMove();
        var journey = File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json"));
        var checkpoint = File.ReadAllBytes(Path.Combine(s.Handoff, "flight-checkpoint.json"));
        var raw = r.LegacyLocalMoveUnknownEvidenceJson;
        var p = s.Prepare(r);
        Assert.True(VoyagerLegacyLocalMoveUnknownPolicy.HasMatchingEcho(r, p));
        Assert.False(p.NativeOriginalOutcomeProved); Assert.False(p.PriorActionTerminalFailureAttested);
        Assert.Contains("caller-attested", p.ConfirmationPrompt);
        foreach (var path in s.ProtectedEvidencePaths.Append(s.RuntimeTicket).Append(s.HandoffTicket))
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        DSPGame.BeforeLoad = () => Assert.False(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        // The public request object is mutable; the prepared payload must not be.
        r.LegacyLocalMoveUnknownEvidenceJson = "changed after preview";
        r.ExpectedLegacyLocalMoveUnknownEvidenceHash = new string('0', 64);
        var accepted = s.Commit(p);
        Assert.True(accepted.Success, accepted.Error?.Message); Assert.Equal(1, DSPGame.LoadCalls);
        Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        var evidence = s.Sessions.RemoteEvidence!;
        Assert.Equal(raw, evidence.LegacyLocalMoveUnknownEvidenceJson);
        Assert.Equal(p.LegacyLocalMoveUnknownEvidenceHash, evidence.LegacyLocalMoveUnknownEvidenceHash);
        Assert.Equal(journey, File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json")));
        Assert.Equal(checkpoint, File.ReadAllBytes(Path.Combine(s.Handoff, "flight-checkpoint.json")));
        var original = JObject.Parse(JObject.Parse(raw)["actionResponseJson"]!.Value<string>()!)["result"]!;
        Assert.Equal("outcome_unknown", original["state"]!.Value<string>());
        Assert.False(original["succeeded"]!.Value<bool>());
    }

    [Theory]
    [InlineData("source")] [InlineData("attestation")] [InlineData("known_failed")]
    [InlineData("succeeded")] [InlineData("kind")] [InlineData("failure")]
    [InlineData("action_id")] [InlineData("session")] [InlineData("world")]
    [InlineData("version")] [InlineData("source_tick")] [InlineData("before_landing")]
    [InlineData("ticket_hash")] [InlineData("journey_hash")] [InlineData("cp_hash")]
    [InlineData("journal_hash")] [InlineData("missing_typed")] [InlineData("extra_field")]
    [InlineData("duplicate")] [InlineData("bare_action")]
    public void LegacyRejectsInconsistentOrUnattestedCapturesWithoutLoading(string fault)
    {
        using var s = new Scenario(); var r = s.SeedLegacyLocalMove();
        var root = JObject.Parse(r.LegacyLocalMoveUnknownEvidenceJson);
        var a = JObject.Parse(root["actionResponseJson"]!.Value<string>()!);
        var session = JObject.Parse(root["sessionResponseJson"]!.Value<string>()!);
        switch (fault)
        {
            case "source": root["sourceProductSha"] = new string('a', 40); break;
            case "attestation": root["captureAttested"] = false; break;
            case "known_failed": a["result"]!["state"] = "action_failed"; break;
            case "succeeded": a["result"]!["succeeded"] = true; break;
            case "kind": a["result"]!["actionKind"] = "build"; break;
            case "failure": a["result"]!["failureKind"] = "position_stalled"; break;
            case "action_id": a["result"]!["actionId"] = Guid.NewGuid().ToString(); break;
            case "session": a["result"]!["sessionId"] = "another-session"; break;
            case "world": session["result"]!["saveName"] = "another-world"; break;
            case "version": session["result"]!["gameVersion"] = "different-native"; break;
            case "source_tick": session["result"]!["gameTick"] = 12541; break;
            case "before_landing": a["result"]!["startedAtGameTick"] = 12499; break;
            case "ticket_hash": root["ticketSha256"] = new string('0', 64); break;
            case "journey_hash": root["journeySha256"] = new string('0', 64); break;
            case "cp_hash": root["checkpointSha256"] = new string('0', 64); break;
            case "journal_hash": root["journalSha256"] = new string('0', 64); break;
            case "missing_typed": ((JObject)session["result"]!).Remove("writesAllowed"); break;
            case "extra_field": root["path"] = "not-a-file-input"; break;
        }
        root["actionResponseJson"] = fault == "bare_action" ? a["result"]!.ToString(Formatting.None) : a.ToString(Formatting.None);
        root["sessionResponseJson"] = session.ToString(Formatting.None);
        r.LegacyLocalMoveUnknownEvidenceJson = root.ToString(Formatting.None);
        if (fault == "duplicate") r.LegacyLocalMoveUnknownEvidenceJson = r.LegacyLocalMoveUnknownEvidenceJson.Insert(1, "\"captureAttested\":true,");
        r.ExpectedLegacyLocalMoveUnknownEvidenceHash = VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(r.LegacyLocalMoveUnknownEvidenceJson)!;
        Assert.False(s.PrepareResult(r).Success); Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    [Theory]
    [InlineData("source")] [InlineData("primary")] [InlineData("checkpoint")]
    [InlineData("journey")] [InlineData("journal")] [InlineData("ticket")]
    public void LegacyCommitRechecksEveryNativeLeaseAndNeverConsumesStaleProof(string fault)
    {
        using var s = new Scenario(); var r = s.SeedLegacyLocalMove(); var p = s.Prepare(r);
        var checkpoint = PluginJson.Deserialize<Spherewright.Plugin.RuntimeDescriptor.FlightCheckpointTicket>(
            File.ReadAllText(Path.Combine(s.Handoff, "flight-checkpoint.json")))!;
        var path = fault switch
        {
            "source" => GameSave.SavePath(GameSave.LastExit), "primary" => GameSave.SavePath(Scenario.Identity),
            "checkpoint" => GameSave.SavePath(checkpoint.CheckpointSaveName),
            "journey" => Path.Combine(s.Handoff, "voyager-journey.json"), "journal" => s.JournalPath,
            _ => s.HandoffTicket,
        };
        File.AppendAllText(path, " ");
        Assert.False(s.Commit(p).Success); Assert.Equal(0, DSPGame.LoadCalls);
    }

    private sealed partial class Scenario
    {
        internal PrepareOwnedWorldResumeRequest SeedLegacyLocalMove()
        {
            Assert.True(Tickets.TryGetActiveTicket(Token, out var prior, out _));
            var actionId = Guid.NewGuid().ToString();
            Tickets.ArmFromQuarantinedOwnedSession(Identity, "synthetic-session", 201, 12530, actionId, prior!.GameplayJournalCheckpoint!);
            Token = Tickets.CurrentResumeToken!; WriteSave(GameSave.LastExit, 12540);
            var action = new ActionResultSnapshot { ActionId = actionId, ActionKind = "move", State = "outcome_unknown",
                Terminal = true, Succeeded = false, SessionId = "synthetic-session", PlanetId = 201,
                IdempotencyKey = Guid.NewGuid().ToString(), BeforeStateHash = "synthetic-before-state",
                StartedAtGameTick = 12501, CompletedAtGameTick = 12530, FailureKind = "voyager_local_native_tail_unproved" };
            var session = new SessionState { GameLoaded = true, OwnedBySpherewright = true, SessionId = "synthetic-session",
                SaveName = Identity, GameVersion = Version, GameTick = 12530, LastOwnedSaveGameTick = 12345,
                LocalPlanetId = 201, WritesAllowed = false, WriteHealth = WriteHealthStates.Quarantined,
                WriteQuarantineActionId = actionId };
            string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            var json = PluginJson.Serialize(new { sourceProductSha = VoyagerLegacyLocalMoveUnknownPolicy.LegacySourceSha,
                captureAttested = true, actionResponseJson = PluginJson.Serialize(new { success = true, result = action }),
                sessionResponseJson = PluginJson.Serialize(new { success = true, result = session }),
                ticketSha256 = Hash(RuntimeTicket), journeySha256 = Hash(Path.Combine(Handoff, "voyager-journey.json")),
                checkpointSha256 = Hash(Path.Combine(Handoff, "flight-checkpoint.json")), journalSha256 = Hash(JournalPath) });
            return new() { LegacyLocalMoveUnknownEvidenceJson = json,
                ExpectedLegacyLocalMoveUnknownEvidenceHash = VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(json)! };
        }
    }
}
