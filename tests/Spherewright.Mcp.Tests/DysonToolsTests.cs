using System.Text.Json;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Fact]
    public async Task DesignPreviewCopiesItsGraphAndRequiresAnExplicitPluginResult()
    {
        var request = new Spherewright.Contracts.Celestial.DysonDesignPreviewRequest
        {
            ExpectedDesignStateHash = "synthetic-design",
            Steps = new() { new() { LocalId = -1, Structure = new()
            {
                Kind = "node", LayerId = 1, LocalPosition = new() { X = 11000 },
            }}},
        };
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.GetDysonStateAsync(bridge, "synthetic", 104, layerId: 1, designPreview: request);
        Assert.True(result.IsError);
        Assert.NotSame(request, bridge.LastDysonRequest!.DesignPreview);
        request.Steps[0].Structure.LocalPosition!.X++;
        Assert.Equal(11000, bridge.LastDysonRequest.DesignPreview!.Steps[0].Structure.LocalPosition!.X);
        Assert.Null(bridge.LastConfigureRequest);
    }

    [Fact]
    public async Task DysonToolForwardsTheExactOwnedScopeAndReturnsTokenlessData()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.GetDysonStateAsync(bridge, "synthetic-dyson-session", 104, layerId: 2);
        Assert.False(result.IsError);
        Assert.Equal("synthetic-dyson-session", bridge.LastSessionId);
        Assert.Equal(104, bridge.LastDysonRequest!.PlanetId);
        Assert.Equal(2, bridge.LastDysonRequest.LayerId);
        Assert.Equal(1, bridge.LastDysonRequest.StateHashVersion);
        var value = result.StructuredContent!.Value.GetProperty("result");
        Assert.True(value.GetProperty("readOnly").GetBoolean());
        Assert.False(value.GetProperty("executable").GetBoolean());
        Assert.False(value.GetProperty("commitAllowedNow").GetBoolean());
        Assert.DoesNotContain("planToken", value.GetRawText());
        Assert.Equal(JsonValueKind.Null, value.GetProperty("structurePoints").ValueKind);
    }

    [Theory]
    [InlineData(-1, 1)] [InlineData(11, 1)] [InlineData(0, 0)] [InlineData(0, 2)]
    public async Task InvalidDysonScopeStopsBeforeTheBridge(int layerId, int version)
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.GetDysonStateAsync(bridge, "synthetic", 104, layerId, version);
        Assert.True(result.IsError);
        Assert.Null(bridge.LastDysonRequest);
    }
}
