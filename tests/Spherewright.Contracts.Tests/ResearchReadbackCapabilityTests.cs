using System.Text.Json;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class ResearchReadbackCapabilityTests
{
    [Fact]
    public void APlanFromAnOlderPluginDoesNotAdvertiseOrdinaryEnqueueProof()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var legacy = JsonSerializer.Deserialize<PreparedNormalAction>(
            "{\"prepared\":true,\"actionKind\":\"select-research\",\"commitAllowedNow\":true}", options);
        Assert.NotNull(legacy);
        Assert.True(legacy.Prepared);
        Assert.True(legacy.CommitAllowedNow);
        Assert.Equal("select-research", legacy.ActionKind);
        Assert.Equal(0, legacy.ResearchQueueReadbackVersion);
    }

    [Fact]
    public void NewCapabilitySurvivesTheWireWithoutChangingTheActionKind()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var native = new PreparedNormalAction { Prepared = true, ActionKind = "select-research", ResearchQueueReadbackVersion = 1 };
        var text = JsonSerializer.Serialize(native, options);
        using var document = JsonDocument.Parse(text);
        Assert.Equal(1, document.RootElement.GetProperty("researchQueueReadbackVersion").GetInt32());
        var copy = JsonSerializer.Deserialize<PreparedNormalAction>(text, options)!;
        Assert.Equal("select-research", copy.ActionKind);
        Assert.Equal(1, copy.ResearchQueueReadbackVersion);
    }
}
