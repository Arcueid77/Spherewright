using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Fact]
    public async Task NativeNavigationForwardsTheExactRequestAndRequiresTheFundedEcho()
    {
        var plan = new NativeSurfaceNavigationPlan
        {
            SessionId = "nav-session", PlayerActionStateHash = "synthetic-action", PlanetId = 104,
            Target = new() { X = 200 }, ArcDistanceMetres = 100, NativeFlyThresholdMetres = 50,
            NativeSailThresholdMetres = 200, ArrivalToleranceMetres = 4, MaximumGameTicks = 3600,
            LandingReserveGameTicks = 900, EnergyUpperPerTickJoules = 1000, RequiredLoadedEnergyJoules = 3600000,
        };
        plan.BindingHash = NativeSurfaceNavigationPolicy.Hash(plan.SessionId, plan.PlayerActionStateHash, plan);
        var bridge = new FakeBridgeClient(SuccessResult()) { MovePlan = new()
        {
            Prepared = true, CommitAllowedNow = true, PlanToken = "synthetic-navigation-token",
            ActionKind = NormalActionKinds.Move, ExpectedStateHash = plan.BindingHash, NativeSurfaceNavigation = plan,
        }};
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "nav-session", 104, 200, 0, 0,
            "synthetic-player", arrivalTolerance: 4, useNativeNavigation: true);
        Assert.False(result.IsError);
        Assert.True(bridge.LastMoveRequest!.UseNativeNavigation);
        Assert.False(bridge.LastMoveRequest.PreviewWalkingRoute);
        Assert.Equal(4, bridge.LastMoveRequest.ArrivalTolerance);
    }

    [Fact]
    public async Task LegacyWalkPlanCannotExposeANavigationToken()
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { MovePlan = new()
        {
            Prepared = true, CommitAllowedNow = true, PlanToken = "legacy-navigation-token", ActionKind = NormalActionKinds.Move,
        }};
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "nav-session", 104, 200, 0, 0,
            "synthetic-player", arrivalTolerance: 4, useNativeNavigation: true);
        Assert.True(result.IsError);
        Assert.DoesNotContain("legacy-navigation-token", result.StructuredContent!.Value.GetRawText());
    }

    [Theory]
    [InlineData(true, false, 4)] [InlineData(false, true, 4)] [InlineData(false, false, 1.5f)]
    public async Task InvalidNavigationArgumentsStopBeforeTheBridge(bool drift, bool preview, float tolerance)
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "nav-session", 104, 200, 0, 0,
            "synthetic-player", arrivalTolerance: tolerance, allowPassiveDrift: drift,
            previewWalkingRoute: preview, useNativeNavigation: true);
        Assert.True(result.IsError);
        Assert.Null(bridge.LastMoveRequest);
    }
}
