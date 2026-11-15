using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Mcp.Tools;
using Spherewright.Mcp.BridgeClient;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SwarmOrbitRequestIsCopiedAndRequiresExactPluginEcho(bool previewOnly)
    {
        var spec = new DysonSwarmOrbitRequest { Radius = 8000, InclinationDegrees = 30, LongitudeDegrees = 40, PreviewOnly = previewOnly };
        var plan = OrbitPlan(spec.Copy());
        var bridge = new FakeBridgeClient(SuccessResult()) { ConfigurePlan = new()
        {
            Prepared = !previewOnly, CommitAllowedNow = !previewOnly, PlanToken = previewOnly ? "" : "synthetic-orbit-token",
            ActionKind = NormalActionKinds.ConfigureBuilding, ExpectedStateHash = plan.BindingHash, PlannedDysonSwarmOrbit = plan,
        }};
        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(bridge, "synthetic-session", 7, 0, 0,
            plan.BeforeStateHash, mode: BuildingConfigurationModes.DysonSwarmOrbit, dysonSwarmOrbit: spec);
        Assert.False(result.IsError); Assert.NotSame(spec, bridge.LastConfigureRequest!.DysonSwarmOrbit);
        spec.Radius++;
        Assert.Equal(8000, bridge.LastConfigureRequest.DysonSwarmOrbit!.Radius);
    }

    [Theory]
    [InlineData("missing_plan")] [InlineData("wrong_echo")] [InlineData("wrong_mode")]
    public async Task InconsistentOrbitPlansNeverExposeACommitToken(string fault)
    {
        var spec = new DysonSwarmOrbitRequest { Radius = 8000, InclinationDegrees = 30, LongitudeDegrees = 40 };
        var plan = OrbitPlan(spec.Copy());
        var prepared = new PreparedNormalAction { Prepared = true, CommitAllowedNow = true, PlanToken = "synthetic-orbit-token",
            ActionKind = NormalActionKinds.ConfigureBuilding, ExpectedStateHash = plan.BindingHash, PlannedDysonSwarmOrbit = plan };
        if (fault == "missing_plan") prepared.PlannedDysonSwarmOrbit = null;
        if (fault == "wrong_echo") plan.Orbit.Radius++;
        var bridge = new FakeBridgeClient(SuccessResult()) { ConfigurePlan = prepared };
        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(bridge, "synthetic-session", 7, 0, 0,
            plan.BeforeStateHash, mode: fault == "wrong_mode" ? BuildingConfigurationModes.Production : BuildingConfigurationModes.DysonSwarmOrbit,
            dysonSwarmOrbit: spec);
        Assert.True(result.IsError); Assert.DoesNotContain("synthetic-orbit-token", result.StructuredContent!.Value.GetRawText());
        if (fault == "wrong_mode") Assert.Null(bridge.LastConfigureRequest);
    }

    [Fact]
    public async Task SdkBindsOrbitJsonToTheActualConfigureTool()
    {
        var spec = new DysonSwarmOrbitRequest { Radius = 8000, InclinationDegrees = 30, LongitudeDegrees = 40 };
        var plan = OrbitPlan(spec.Copy());
        var bridge = new FakeBridgeClient(SuccessResult()) { ConfigurePlan = new()
        {
            Prepared = true, CommitAllowedNow = true, PlanToken = "synthetic-orbit-token",
            ActionKind = NormalActionKinds.ConfigureBuilding, ExpectedStateHash = plan.BindingHash, PlannedDysonSwarmOrbit = plan,
        }};
        var services = new ServiceCollection(); services.AddSingleton<IBridgeClient>(bridge);
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>(), t => t.ProtocolTool.Name == "spherewright_prepare_configure_building");
        Assert.True(tool.ProtocolTool.InputSchema.GetProperty("properties").TryGetProperty("dysonSwarmOrbit", out _));
        await using var server = McpServer.Create(new StreamServerTransport(Stream.Null, Stream.Null), new McpServerOptions(), null, provider);
        var parameters = new CallToolRequestParams { Name = tool.ProtocolTool.Name, Arguments = new Dictionary<string, JsonElement>
        {
            ["sessionId"] = JsonSerializer.SerializeToElement("synthetic-session"), ["planetId"] = JsonSerializer.SerializeToElement(7),
            ["entityId"] = JsonSerializer.SerializeToElement(0), ["recipeId"] = JsonSerializer.SerializeToElement(0),
            ["expectedFactoryStateHash"] = JsonSerializer.SerializeToElement(plan.BeforeStateHash),
            ["mode"] = JsonSerializer.SerializeToElement(BuildingConfigurationModes.DysonSwarmOrbit),
            ["dysonSwarmOrbit"] = JsonSerializer.SerializeToElement(new { radius = 8000, inclinationDegrees = 30, longitudeDegrees = 40, previewOnly = false }),
        }};
        var context = new RequestContext<CallToolRequestParams>(server, new JsonRpcRequest { Id = new RequestId(1), Method = "tools/call" }, parameters)
        { Services = provider };
        var result = await tool.InvokeAsync(context);
        Assert.False(result.IsError, result.StructuredContent?.GetRawText());
        Assert.Equal(8000, bridge.LastConfigureRequest!.DysonSwarmOrbit!.Radius);
        Assert.Equal(30, bridge.LastConfigureRequest.DysonSwarmOrbit.InclinationDegrees);
        Assert.Equal(40, bridge.LastConfigureRequest.DysonSwarmOrbit.LongitudeDegrees);
        Assert.Equal("synthetic-orbit-token", result.StructuredContent!.Value.GetProperty("result").GetProperty("planToken").GetString());
    }

    private static DysonSwarmOrbitPlan OrbitPlan(DysonSwarmOrbitRequest spec)
    {
        var plan = new DysonSwarmOrbitPlan { EvidenceVersion = 1, SessionId = "synthetic-session", PlanetId = 7, StarId = 2,
            Orbit = spec, BeforeStateHash = "sha256:" + new string('a', 64), PlannedOrbitId = 2,
            NativeRadius = 8000, NativeRotation = new() { W = 1 }, NativeUp = new() { Y = 1 }, ExistingActiveSails = 2,
            ExistingInFlightSails = 1, NativeChecksComplete = true };
        plan.BindingHash = DysonSwarmOrbitPolicy.BindingHash(plan);
        return plan;
    }
}
