using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Errors;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Resources;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed class VoyagerToolsTests
{
    [Fact]
    public async Task CatalogMapsOwnedOriginAndPreservesUnknownVersusObservedZero()
    {
        var bridge = NewBridge();
        var proxy = Proxy(bridge);
        proxy.Catalog = new VoyagerStarCatalogSnapshot
        {
            SessionId = "owned-session", OriginPlanetId = 101, StateHash = "catalog-hash",
            Stars = new()
            {
                new() { StarId = 2, PlanetsVisible = false, Resources = null, Planets = null },
                new()
                {
                    StarId = 3, PlanetsVisible = true, ResourceVisibility = VoyagerResourceVisibility.Observed,
                    ResourceSummaryComplete = true, ResourceSummaryScope = "remaining_veins_and_native_gas_oceans",
                    Resources = new() { new() { ItemId = 1001, Amount = 0, VeinCount = 0 } },
                },
            },
        };
        using var cancellation = new CancellationTokenSource();

        var result = await SpherewrightTools.GetVoyagerStarCatalogAsync(bridge, "owned-session", 101,
            limit: 32, stateHashVersion: 1, cancellationToken: cancellation.Token);

        Assert.False(result.IsError);
        Assert.Equal(new[] { nameof(IBridgeClient.GetVoyagerStarCatalogAsync) }, proxy.Calls);
        Assert.Equal("owned-session", proxy.LastSessionId);
        var request = Assert.IsType<GetVoyagerStarCatalogRequest>(proxy.LastRequest);
        Assert.Equal(101, request.PlanetId);
        Assert.Equal(32, request.Limit);
        Assert.Equal(1, request.StateHashVersion);
        Assert.Equal(cancellation.Token, proxy.LastCancellationToken);
        var snapshot = result.StructuredContent!.Value.GetProperty("result");
        Assert.False(snapshot.GetProperty("prepared").GetBoolean());
        Assert.False(snapshot.GetProperty("executable").GetBoolean());
        Assert.False(snapshot.GetProperty("commitAllowedNow").GetBoolean());
        Assert.False(snapshot.TryGetProperty("planToken", out _));
        var stars = snapshot.GetProperty("stars");
        Assert.Equal("unknown", stars[0].GetProperty("resourceVisibility").GetString());
        Assert.Equal(JsonValueKind.Null, stars[0].GetProperty("resources").ValueKind);
        Assert.Equal(JsonValueKind.Null, stars[0].GetProperty("planets").ValueKind);
        Assert.Equal(0L, stars[1].GetProperty("resources")[0].GetProperty("amount").GetInt64());
        Assert.Equal("remaining_veins_and_native_gas_oceans", stars[1].GetProperty("resourceSummaryScope").GetString());
    }

    [Fact]
    public async Task RouteMapsWholeRoundTripAndReturnReserveWithoutPreparingExecution()
    {
        var bridge = NewBridge();
        var proxy = Proxy(bridge);
        proxy.Budget = new VoyagerRouteBudgetSnapshot
        {
            BudgetComputed = true, BudgetSufficient = true, DestinationVisible = true, AvailableWarpers = 8, RequiredWarpers = 6,
            OutboundEnergyBudgetJoules = 120000, ReturnEnergyBudgetJoules = 180000,
            LandingReserveJoules = 10000, WithdrawalReserveJoules = 20000,
            MaximumFlightGameTicks = 90000, OutboundMaximumGameTicks = 60000, ReturnReserveGameTicks = 30000,
            MinimumFlightGameTicksAtMaximumSpeed = null,
        };
        using var cancellation = new CancellationTokenSource();

        var result = await SpherewrightTools.PreviewVoyagerRouteAsync(bridge, "owned-session", 101, 201,
            "player-hash", "catalog-hash", stateHashVersion: 1, maximumFlightGameTicks: 90000,
            returnReserveGameTicks: 30000, minimumCoreEnergyRatio: 0.95d, extraWarperReserve: 4,
            cancellationToken: cancellation.Token);

        Assert.False(result.IsError);
        Assert.Equal(new[] { nameof(IBridgeClient.PreviewVoyagerRouteAsync) }, proxy.Calls);
        Assert.Equal("owned-session", proxy.LastSessionId);
        Assert.Equal(cancellation.Token, proxy.LastCancellationToken);
        var request = Assert.IsType<PreviewVoyagerRouteRequest>(proxy.LastRequest);
        Assert.Equal(101, request.PlanetId);
        Assert.Equal(201, request.DestinationPlanetId);
        Assert.Equal("player-hash", request.ExpectedPlayerStateHash);
        Assert.Equal("catalog-hash", request.ExpectedCatalogStateHash);
        Assert.Equal(1, request.StateHashVersion);
        Assert.Equal(90000, request.MaximumFlightGameTicks);
        Assert.Equal(30000, request.ReturnReserveGameTicks);
        Assert.Equal(0.95d, request.MinimumCoreEnergyRatio);
        Assert.Equal(4, request.ExtraWarperReserve);
        var budget = result.StructuredContent!.Value.GetProperty("result");
        Assert.True(budget.GetProperty("budgetComputed").GetBoolean());
        Assert.True(budget.GetProperty("budgetSufficient").GetBoolean());
        Assert.False(budget.GetProperty("prepared").GetBoolean());
        Assert.False(budget.GetProperty("executable").GetBoolean());
        Assert.False(budget.GetProperty("commitAllowedNow").GetBoolean());
        Assert.False(budget.TryGetProperty("planToken", out _));
        Assert.Equal(180000d, budget.GetProperty("returnEnergyBudgetJoules").GetDouble());
        Assert.Equal(30000, budget.GetProperty("returnReserveGameTicks").GetInt64());
        Assert.Equal(JsonValueKind.Null, budget.GetProperty("minimumFlightGameTicksAtMaximumSpeed").ValueKind);
    }

    [Theory]
    [InlineData(BridgeErrorCodes.StaleState)]
    [InlineData(BridgeErrorCodes.InvalidRequest)]
    public async Task NativeReadRejectionIsReturnedWithoutRetryOrFallback(string code)
    {
        var bridge = NewBridge();
        var proxy = Proxy(bridge);
        proxy.Error = BridgeError.Create(code, "Native evidence rejected the preview.", false, "Inspect the original current state.");

        var result = await SpherewrightTools.PreviewVoyagerRouteAsync(bridge, "owned-session", 101, 201,
            "stale-player", "stale-catalog");

        Assert.True(result.IsError);
        Assert.Equal(new[] { nameof(IBridgeClient.PreviewVoyagerRouteAsync) }, proxy.Calls);
        var payload = result.StructuredContent!.Value;
        Assert.Equal(code, payload.GetProperty("error").GetProperty("code").GetString());
        Assert.False(payload.GetProperty("error").GetProperty("retryable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("result").ValueKind);
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("executable")]
    [InlineData("commitAllowedNow")]
    public async Task ReadWithExecutionAuthorityFailsClosed(string authority)
    {
        var bridge = NewBridge();
        var proxy = Proxy(bridge);
        proxy.Catalog.Prepared = proxy.Budget.Prepared = authority == "prepared";
        proxy.Catalog.Executable = proxy.Budget.Executable = authority == "executable";
        proxy.Catalog.CommitAllowedNow = proxy.Budget.CommitAllowedNow = authority == "commitAllowedNow";

        var catalog = await SpherewrightTools.GetVoyagerStarCatalogAsync(bridge, "owned-session", 101);
        var budget = await SpherewrightTools.PreviewVoyagerRouteAsync(bridge, "owned-session", 101, 201, "player", "catalog");

        foreach (var result in new[] { catalog, budget })
        {
            Assert.True(result.IsError);
            Assert.Equal(BridgeErrorCodes.InternalError, result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal(JsonValueKind.Null, result.StructuredContent.Value.GetProperty("result").ValueKind);
        }
        Assert.Equal(2, proxy.Calls.Count);
    }

    [Fact]
    public void DiscoveryRequiresFreshBindingsAndAdvertisesReadOnlyNonExecutableSchemas()
    {
        var services = new ServiceCollection();
        services.AddSingleton(NewBridge());
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool).ToArray();
        foreach (var name in new[] { "spherewright_get_voyager_star_catalog", "spherewright_preview_voyager_route" })
        {
            var tool = Assert.Single(tools, tool => tool.Name == name);
            Assert.True(tool.Annotations!.ReadOnlyHint);
            Assert.False(tool.Annotations.DestructiveHint);
            Assert.True(tool.Annotations.IdempotentHint);
            Assert.False(tool.Annotations.OpenWorldHint);
            Assert.Contains("tokenless", tool.Description);
            Assert.Contains("executable=false", tool.Description);
            Assert.Contains("commitAllowedNow=false", tool.Description);
            var required = tool.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Contains("sessionId", required);
            Assert.Contains("planetId", required);
            var properties = tool.InputSchema.GetProperty("properties");
            Assert.False(properties.TryGetProperty("planToken", out _));
            Assert.False(properties.TryGetProperty("idempotencyKey", out _));
            Assert.Equal("integer", properties.GetProperty("stateHashVersion").GetProperty("type").GetString());
            if (name == "spherewright_preview_voyager_route")
            {
                Assert.Contains("destinationPlanetId", required);
                Assert.Contains("expectedPlayerStateHash", required);
                Assert.Contains("expectedCatalogStateHash", required);
                Assert.Equal("integer", properties.GetProperty("maximumFlightGameTicks").GetProperty("type").GetString());
                Assert.Equal("integer", properties.GetProperty("returnReserveGameTicks").GetProperty("type").GetString());
                Assert.Equal("number", properties.GetProperty("minimumCoreEnergyRatio").GetProperty("type").GetString());
            }
        }
        Assert.DoesNotContain(tools, tool => tool.Name.Contains("commit_voyager", StringComparison.Ordinal));
        Assert.DoesNotContain(tools, tool => tool.Name.Contains("prepare_voyager", StringComparison.Ordinal));
    }

    [Fact]
    public void EmbeddedVoyagerGuideRetainsResourceAndRecoveryBoundaries()
    {
        Assert.Equal("spherewright://agent/playbooks/opening-movement-v1", AgentPlaybookResources.OpeningMovementUri);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var rule in new[] { "spherewright_get_voyager_star_catalog", "spherewright_preview_voyager_route",
                     "maximumFlightGameTicks", "returnReserveGameTicks", "tokenless", "executable=false",
                     "newer primary", "Unexplored is not absent", "legitimate visible scan caches", "pending scans stay unknown",
                     "remaining_veins_and_native_gas_oceans", "zero vein row is not system-wide item absence",
                     "CheckpointFileFingerprint", "OwnedSaveRecoveryLease", "Reject legacy v1 flight tickets" })
        {
            Assert.Contains(rule, guide);
        }
    }

    private static IBridgeClient NewBridge() => DispatchProxy.Create<IBridgeClient, VoyagerBridgeProxy>();
    private static VoyagerBridgeProxy Proxy(IBridgeClient bridge) => (VoyagerBridgeProxy)(object)bridge;

    public class VoyagerBridgeProxy : DispatchProxy
    {
        public VoyagerStarCatalogSnapshot Catalog { get; set; } = new();
        public VoyagerRouteBudgetSnapshot Budget { get; set; } = new();
        public BridgeError? Error { get; set; }
        public List<string> Calls { get; } = new();
        public string? LastSessionId { get; private set; }
        public object? LastRequest { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);
            Calls.Add(targetMethod!.Name);
            LastSessionId = Assert.IsType<string>(args![0]);
            LastRequest = args[1];
            LastCancellationToken = Assert.IsType<CancellationToken>(args[2]);
            return targetMethod.Name switch
            {
                nameof(IBridgeClient.GetVoyagerStarCatalogAsync) => Task.FromResult(Error is null
                    ? BridgeCallResult<VoyagerStarCatalogSnapshot>.Succeeded(Catalog)
                    : BridgeCallResult<VoyagerStarCatalogSnapshot>.Failed(Error)),
                nameof(IBridgeClient.PreviewVoyagerRouteAsync) => Task.FromResult(Error is null
                    ? BridgeCallResult<VoyagerRouteBudgetSnapshot>.Succeeded(Budget)
                    : BridgeCallResult<VoyagerRouteBudgetSnapshot>.Failed(Error)),
                _ => throw new InvalidOperationException($"Unexpected game call from a read-only Voyager tool: {targetMethod.Name}"),
            };
        }
    }
}
