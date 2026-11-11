using System.Text.Json;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
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
