using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeSurfaceNavigationPolicyTests
{
    [Theory]
    [InlineData(51, true)] [InlineData(200, true)] [InlineData(50, false)] [InlineData(201, false)]
    [InlineData(double.NaN, false)] [InlineData(double.PositiveInfinity, false)]
    public void NativeThresholdsDetermineTheFundedFlySubset(double distance, bool expected)
        => Assert.Equal(expected, NativeSurfaceNavigationPolicy.CanFund(distance, 50, 200, 1000, 2000, 3600000, 0));

    [Theory]
    [InlineData(2000, 3600000, 0, true)]
    [InlineData(1999, 3600000, 0, false)]
    [InlineData(2000, 3599999, 0, false)]
    [InlineData(2000, 3600100, 100, true)]
    [InlineData(2000, 3600000, 100, false)]
    public void TheCompleteWindowAndProtectedFloorRemainFunded(double core, double loaded, double floor, bool expected)
        => Assert.Equal(expected, NativeSurfaceNavigationPolicy.CanFund(100, 50, 200, 1000, core, loaded, floor));

    [Theory]
    [InlineData("drift")][InlineData("preview")][InlineData("points")][InlineData("hash")]
    [InlineData("tolerance")]
    public void MixedModesCannotAcquireANavigationToken(string fault)
    {
        var request = Request();
        if (fault == "drift") request.AllowPassiveDrift = true;
        if (fault == "preview") request.PreviewWalkingRoute = true;
        if (fault == "points") request.WalkingPreviewPoints = new();
        if (fault == "hash") request.ExpectedWalkingGeometryHash = "synthetic";
        if (fault == "tolerance") request.ArrivalTolerance = 1.5f;
        Assert.NotNull(NativeSurfaceNavigationPolicy.ValidateRequest(request));
    }

    [Fact]
    public void ACompleteRequestBoundEchoIsRequired()
    {
        var request = Request();
        var prepared = Prepared(request);
        Assert.True(NativeSurfaceNavigationPolicy.ConfirmsEcho("synthetic", request, prepared));
        prepared.NativeSurfaceNavigation!.EnergyUpperPerTickJoules++;
        Assert.False(NativeSurfaceNavigationPolicy.ConfirmsEcho("synthetic", request, prepared));
    }

    [Theory]
    [InlineData("session")][InlineData("planet")][InlineData("target")][InlineData("evidence")]
    [InlineData("native_method")][InlineData("blocked")][InlineData("legacy")][InlineData("hash")]
    public void AChangedOrLegacyEchoWithholdsTheCapability(string fault)
    {
        var request = Request();
        var prepared = Prepared(request);
        var plan = prepared.NativeSurfaceNavigation!;
        if (fault == "session") plan.SessionId = "foreign";
        if (fault == "planet") plan.PlanetId++;
        if (fault == "target") plan.Target.Z++;
        if (fault == "evidence") plan.EvidenceVersion = 0;
        if (fault == "native_method") plan.NativeOperation = "MoveTo";
        if (fault == "blocked") prepared.CommitAllowedNow = false;
        if (fault == "legacy") prepared.NativeSurfaceNavigation = null;
        if (fault == "hash") prepared.ExpectedStateHash = "foreign";
        Assert.False(NativeSurfaceNavigationPolicy.ConfirmsEcho("synthetic", request, prepared));
    }

    [Fact]
    public void LegacyWalkArgumentsKeepTheirExistingAdmission()
    {
        var request = new PrepareMoveRequest { ArrivalTolerance = 1.5f };
        Assert.Null(NativeSurfaceNavigationPolicy.ValidateRequest(request));
        Assert.False(NativeSurfaceNavigationPolicy.ConfirmsEcho("synthetic", request, new()));
    }

    private static PrepareMoveRequest Request() => new()
    {
        UseNativeNavigation = true, PlanetId = 104, ArrivalTolerance = 4, Target = new() { X = 200 },
    };

    private static PreparedNormalAction Prepared(PrepareMoveRequest request)
    {
        var plan = new NativeSurfaceNavigationPlan
        {
            SessionId = "synthetic", PlanetId = request.PlanetId, PlayerActionStateHash = "synthetic-action",
            Target = new() { X = request.Target.X }, ArrivalToleranceMetres = request.ArrivalTolerance,
            ArcDistanceMetres = 100, NativeFlyThresholdMetres = 50, NativeSailThresholdMetres = 200,
            MaximumGameTicks = 3600, LandingReserveGameTicks = 900,
            EnergyUpperPerTickJoules = 1000, RequiredLoadedEnergyJoules = 3600000,
        };
        plan.BindingHash = NativeSurfaceNavigationPolicy.Hash(plan.SessionId, plan.PlayerActionStateHash, plan);
        return new() { Prepared = true, CommitAllowedNow = true, PlanToken = "synthetic-token",
            ActionKind = NormalActionKinds.Move, ExpectedStateHash = plan.BindingHash, NativeSurfaceNavigation = plan };
    }
}
