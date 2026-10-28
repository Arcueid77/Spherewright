using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Resources;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed class LogisticsVesselObservationToolsTests
{
    [Fact]
    public async Task ExactResidentStationOptInMapsOnceWithoutAnyActionOrAutomaticSelection()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, StationProxy>(); var proxy = (StationProxy)bridge;
        var result = await SpherewrightTools.InspectFactoryEntityAsync(bridge, "synthetic-session", 204, 19,
            ownedRemoteLogisticsStation: true);
        Assert.False(result.IsError);
        Assert.Equal(1, proxy.Calls); Assert.True(proxy.Request!.OwnedRemoteLogisticsStation);
        Assert.Equal(204, proxy.Request.PlanetId); Assert.Equal(19, proxy.Request.ObjectId);
        Assert.Empty(proxy.Request.MaterialInventoryObjectIds); Assert.Null(proxy.Request.Geometry);
        await SpherewrightTools.InspectFactoryEntityAsync(bridge, "synthetic-session", 104, 19);
        Assert.Equal(2, proxy.Calls); Assert.False(proxy.Request.OwnedRemoteLogisticsStation);
    }

    [Theory]
    [InlineData("prebuild")] [InlineData("zero")] [InlineData("geometry")] [InlineData("material")]
    public async Task RemoteFlagCannotExpandToGeometryMaterialSelectionOrNegativeObjects(string invalid)
    {
        var bridge = DispatchProxy.Create<IBridgeClient, StationProxy>(); var proxy = (StationProxy)bridge;
        var result = await SpherewrightTools.InspectFactoryEntityAsync(bridge, "synthetic-session", 204,
            invalid == "prebuild" ? -1 : invalid == "zero" ? 0 : 19,
            materialInventoryObjectIds: invalid == "material" ? new[] { 19 } : null,
            geometry: invalid == "geometry" ? new FactoryGeometryReadRequest() : null, ownedRemoteLogisticsStation: true);
        Assert.True(result.IsError); Assert.Equal(0, proxy.Calls);
    }

    [Theory]
    [InlineData("missing_detail")] [InlineData("wrong_planet")] [InlineData("wrong_object")]
    public async Task MixedOlderOrMismatchedPluginCannotConfirmTheRemoteObservation(string mismatch)
    {
        var bridge = DispatchProxy.Create<IBridgeClient, StationProxy>(); var proxy = (StationProxy)bridge;
        proxy.Mismatch = mismatch;
        var result = await SpherewrightTools.InspectFactoryEntityAsync(bridge, "synthetic-session", 204, 19, ownedRemoteLogisticsStation: true);
        Assert.True(result.IsError); Assert.Equal(1, proxy.Calls);
    }

    [Fact]
    public void ExistingToolRemainsReadOnlyWithOptionalFalseFlagAndExplicitCoverageLimit()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, StationProxy>();
        var services = new ServiceCollection(); services.AddSingleton<IBridgeClient>(bridge);
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().Select(t => t.ProtocolTool).ToList();
        Assert.Equal(74, tools.Count);
        var tool = Assert.Single(tools, t => t.Name == "spherewright_inspect_factory_entity");
        Assert.True(tool.Annotations!.ReadOnlyHint);
        var schema = tool.InputSchema; var flag = schema.GetProperty("properties").GetProperty("ownedRemoteLogisticsStation");
        Assert.Equal("boolean", flag.GetProperty("type").GetString());
        Assert.False(flag.GetProperty("default").GetBoolean());
        Assert.DoesNotContain(schema.GetProperty("required").EnumerateArray(), p => p.GetString() == "ownedRemoteLogisticsStation");
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.InspectFactoryEntityAsync))!;
        var text = method.GetParameters().Single(p => p.Name == "ownedRemoteLogisticsStation").GetCustomAttribute<DescriptionAttribute>()!.Description;
        Assert.Contains("READ-ONLY", text); Assert.Contains("at most32", text); Assert.Contains("complete/truncated/unknown", text);
        Assert.Contains("not route progress or ETA", text); Assert.Contains("does not prove delivery", text);
    }

    [Fact]
    public void RemoteReadExceptionIsDiscoverableWithoutGrantingRemoteWritesOrHiddenFactoryLoading()
    {
        var method = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.InspectFactoryEntityAsync))!;
        var description = method.GetCustomAttribute<DescriptionAttribute>()!.Description;
        Assert.Contains("Without ownedRemoteLogisticsStation, other stars fail closed", description);
        Assert.Contains("rejects geometry/material cuts and grants no remote writes", description);
        var planet = method.GetParameters().Single(p => p.Name == "planetId").GetCustomAttribute<DescriptionAttribute>()!.Description;
        Assert.Contains("another star", planet); Assert.Contains("no load/create or remote write scope", planet);
    }

    [Fact]
    public void ExistingConstructionToolsAdvertiseDurableOriginalBatchAndActualDroneIdle()
    {
        static string Description(string method) => typeof(SpherewrightTools).GetMethod(method)!
            .GetCustomAttribute<DescriptionAttribute>()!.Description;
        var prepare = Description(nameof(SpherewrightTools.PrepareBuildAsync));
        Assert.Contains("up to12 belts2001", prepare);
        Assert.Contains("or1..8 of2301/2104/2201/2203/2205/2011", prepare);
        Assert.Contains("does not increase the caller approved material allowance", prepare);
        Assert.Contains("within30seconds", prepare);
        Assert.Contains("pending objects must never be recreated", prepare);
        var commit = Description(nameof(SpherewrightTools.CommitBuildAsync));
        Assert.Contains("whole-batch debit", commit); Assert.Contains("real Native factory pass", commit);
        Assert.Contains("actual owned-drone idle", commit);
        var progress = Description(nameof(SpherewrightTools.GetBlueprintBuildsAsync));
        Assert.Contains("constructionControl", progress); Assert.Contains("prelaunch cache", progress);
        var resume = Description(nameof(SpherewrightTools.PrepareBlueprintBuildAsync));
        Assert.Contains("without another Create call", resume); Assert.Contains("original work window", resume);
    }

    [Fact]
    public void EmbeddedGuideKeepsTheRestrictedNativePremisesAndNeverRenewsConstructionOrReturnTime()
    {
        Assert.Equal("spherewright://agent/playbooks/opening-movement-v1", AgentPlaybookResources.OpeningMovementUri);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var rule in new[]
        {
            "ownedRemoteLogisticsStation=true", "1+extraWarperReserve", "finite-cost branch",
            "one whole player-and-drone tick", "GameMain.Pause", "Never pause a replacement world",
            "actual dry Walk", "do not auto-resume, load or replay",
            "zero configured fleet slots", "Empty default ammo slot1", "3600 ticks/60 seconds",
            "fresh resume grants no new minute", "real subsequent Native construction pass",
            "clear original prelaunch cache", "physicalIdleVerified", "one measured whole-batch debit",
            "Preserve unknown original action/build/checkpoint",
        }) Assert.Contains(rule, guide, StringComparison.Ordinal);
    }

    public class StationProxy : DispatchProxy
    {
        public int Calls { get; private set; }
        public InspectFactoryEntityRequest? Request { get; private set; }
        public string? Mismatch { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != nameof(IBridgeClient.InspectFactoryEntityAsync))
                throw new InvalidOperationException("Only the exact read is permitted by this fixture.");
            Calls++; Request = (InspectFactoryEntityRequest)args![1]!;
            return Task.FromResult(BridgeCallResult<FactoryEntitySnapshot>.Succeeded(new()
            {
                SessionId = "synthetic-session", PlanetId = Mismatch == "wrong_planet" ? 999 : Request.PlanetId,
                ObjectId = Mismatch == "wrong_object" ? 999 : Request.ObjectId, ObjectKind = FactoryObjectKinds.Entity,
                LogisticsStation = new() { ActiveVessels = Mismatch == "missing_detail" ? null : new LogisticsActiveVesselObservation() },
            }));
        }
    }
}
