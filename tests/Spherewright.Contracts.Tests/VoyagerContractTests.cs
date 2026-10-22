using System.Text.Json;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class VoyagerContractTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void ReturnOnlyRecoveryNeverInventsAGrantOrRemainingBudgetFromMissingFields()
    {
        var legacy = JsonSerializer.Deserialize<PrepareFlightCheckpointReloadRequest>("{}", CamelCase)!;
        Assert.Null(legacy.ReturnOnlyTask);
        Assert.False(new CommitFlightCheckpointReloadRequest().UserConfirmedInConversation);
        Assert.Null(new SessionState().ReturnOnlyRecovery);
        var missing = JsonSerializer.Deserialize<VoyagerReturnOnlyTaskState>("{}", CamelCase)!;
        Assert.Null(missing.RemainingGameTicks);
        Assert.Null(missing.FirstFailure);
        var observedZero = JsonSerializer.Deserialize<VoyagerReturnOnlyTaskState>(
            "{\"remainingGameTicks\":0,\"maximumGameTicks\":54000,\"originalMaximumGameTicks\":108000}", CamelCase)!;
        Assert.Equal(0, observedZero.RemainingGameTicks);
        Assert.Equal(54000, observedZero.MaximumGameTicks);
        Assert.Equal(108000, observedZero.OriginalMaximumGameTicks);
    }

    [Fact]
    public void UnknownCatalogueOmitsNoEvidenceByInventingEmptyObservedResources()
    {
        var catalogue = new VoyagerStarCatalogSnapshot
        {
            Stars = new() { new VoyagerStarSnapshot { StarId = 2, Name = "Synthetic star" } },
        };
        var restored = JsonSerializer.Deserialize<VoyagerStarCatalogSnapshot>(JsonSerializer.Serialize(catalogue, CamelCase), CamelCase)!;
        var star = Assert.Single(restored.Stars);
        Assert.Equal("unknown", star.ResourceVisibility);
        Assert.Null(star.Resources);
        Assert.Null(star.Planets);
        Assert.False(star.PlanetsVisible);
        Assert.False(star.ResourceSummaryComplete);
        Assert.False(restored.Prepared);
        Assert.False(restored.Executable);
        Assert.False(restored.CommitAllowedNow);
    }

    [Fact]
    public void ExplicitNativeZeroSurvivesRoundTripWithoutFillingMissingMeasurements()
    {
        var resource = new VoyagerResourceSummary { VeinType = 8, ItemId = 1011, Amount = 0L, VeinCount = 0 };
        var restored = JsonSerializer.Deserialize<VoyagerResourceSummary>(JsonSerializer.Serialize(resource, CamelCase), CamelCase)!;
        Assert.Equal(0L, restored.Amount);
        Assert.Equal(0, restored.VeinCount);
        Assert.Null(restored.RatePerSecond);
        var missing = JsonSerializer.Deserialize<VoyagerResourceSummary>("{\"veinType\":8,\"itemId\":1011}", CamelCase)!;
        Assert.Null(missing.Amount);
        Assert.Null(missing.VeinCount);
        Assert.Null(missing.RatePerSecond);
    }

    [Fact]
    public void RoutePreviewCarriesFiniteWindowAndSeparateReturnReserveWithoutPlanToken()
    {
        var request = new PreviewVoyagerRouteRequest
        {
            PlanetId = 104, DestinationPlanetId = 201, ExpectedPlayerStateHash = "player", ExpectedCatalogStateHash = "catalogue",
            MaximumFlightGameTicks = 108000, ReturnReserveGameTicks = 54000, MinimumCoreEnergyRatio = 0.9d, ExtraWarperReserve = 2,
        };
        var copied = JsonSerializer.Deserialize<PreviewVoyagerRouteRequest>(JsonSerializer.Serialize(request, CamelCase), CamelCase)!;
        Assert.Equal(request.MaximumFlightGameTicks, copied.MaximumFlightGameTicks);
        Assert.Equal(request.ReturnReserveGameTicks, copied.ReturnReserveGameTicks);
        Assert.Equal(request.ExpectedCatalogStateHash, copied.ExpectedCatalogStateHash);
        Assert.Equal(request.MinimumCoreEnergyRatio, copied.MinimumCoreEnergyRatio);
        Assert.Equal(2, copied.ExtraWarperReserve);
        var result = new VoyagerRouteBudgetSnapshot { BudgetComputed = true, BudgetSufficient = true, RouteStateHash = "route" };
        var json = JsonSerializer.Serialize(result, CamelCase);
        var restored = JsonSerializer.Deserialize<VoyagerRouteBudgetSnapshot>(json, CamelCase)!;
        Assert.True(restored.BudgetSufficient);
        Assert.False(restored.Prepared);
        Assert.False(restored.Executable);
        Assert.False(restored.CommitAllowedNow);
        Assert.False(restored.LocalFlightReserveIsControllerProof);
        Assert.Equal("warp_entries_and_finite_warp_ticks_only", restored.CoreBufferScope);
        Assert.DoesNotContain("planToken", json, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotencyKey", json, StringComparison.Ordinal);
    }

    [Fact]
    public void OmittedPreviewFieldsNeverClaimComputedBudgetOrMovementAuthority()
    {
        var result = JsonSerializer.Deserialize<VoyagerRouteBudgetSnapshot>("{}", CamelCase)!;
        Assert.False(result.BudgetComputed);
        Assert.False(result.BudgetSufficient);
        Assert.False(result.Prepared);
        Assert.False(result.Executable);
        Assert.False(result.CommitAllowedNow);
        Assert.Empty(result.Blockers);
        Assert.Equal(string.Empty, result.RouteStateHash);
    }
}
