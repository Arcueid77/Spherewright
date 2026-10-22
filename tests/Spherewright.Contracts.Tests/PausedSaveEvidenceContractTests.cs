using System.Text.Json;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class PausedSaveEvidenceContractTests
{
    [Fact]
    public void ErrorAndActionCarryDetachedUnknownFirstFailureWithoutTurningItIntoACertificate()
    {
        var original = new NativeResumeFailureSnapshot { Code = "STALE_STATE", Stage = "paused_owned_save",
            FirstFailedPredicate = "native_pause", FactsState = "observed",
            Predicates = new() { ["native_pause"] = "unknown", ["native_save"] = "not_reached" },
            Values = new() { ["native_file_tick"] = null } };
        var error = BridgeError.Create("STALE_STATE", "synthetic refusal", false, "Keep paused.");
        error.NativeFirstFailure = original.Copy();
        var action = new ActionResultSnapshot { NativeFirstFailure = original.Copy() };
        original.Values["native_file_tick"] = 1;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var errorCopy = JsonSerializer.Deserialize<BridgeError>(JsonSerializer.Serialize(error, options), options)!;
        var actionCopy = JsonSerializer.Deserialize<ActionResultSnapshot>(JsonSerializer.Serialize(action, options), options)!;
        foreach (var cut in new[] { errorCopy.NativeFirstFailure!, actionCopy.NativeFirstFailure! })
        {
            Assert.Null(cut.CapturedAtGameTick); Assert.Null(cut.Values["native_file_tick"]);
            Assert.Equal("unknown", cut.Predicates["native_pause"]);
            Assert.Equal("not_reached", cut.Predicates["native_save"]); Assert.False(cut.SafetyCertified);
        }
        Assert.False(JsonSerializer.Deserialize<NativeResumeFailureSnapshot>("{\"safetyCertified\":true}", options)!.SafetyCertified);
    }
}
