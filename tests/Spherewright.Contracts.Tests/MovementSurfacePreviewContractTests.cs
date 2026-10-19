using System.Text.Json;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class MovementSurfacePreviewContractTests
{
    [Fact]
    public void LegacyPlanDoesNotClaimASurfaceObservation() => Assert.Null(JsonSerializer.Deserialize<PreparedNormalAction>("{}")!.SurfacePreview);

    [Theory]
    [InlineData("detected")]
    [InlineData("unknown")]
    [InlineData("not_detected")]
    public void AdditiveSurfaceEvidenceRoundTripsWithoutChangingAuthority(string risk)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var plan = new PreparedNormalAction
        {
            Prepared = true,
            PlanToken = "opaque",
            ExpectedStateHash = "same",
            SurfacePreview = new()
            { State = "partial", CapturedAtGameTick = 42, ShoreRisk = risk, UnknownSampleCount = 1, Samples = new() { new() { GroundHit = false } } }
        };
        var round = JsonSerializer.Deserialize<PreparedNormalAction>(JsonSerializer.Serialize(plan, options), options)!;
        Assert.Equal("opaque", round.PlanToken); Assert.Equal("same", round.ExpectedStateHash);
        Assert.Equal(risk, round.SurfacePreview!.ShoreRisk); Assert.Equal(42, round.SurfacePreview.CapturedAtGameTick);
        Assert.Equal(64, round.SurfacePreview.MaximumArcMetres);
        Assert.Equal(1, round.SurfacePreview.MaximumSampleSpacingMetres);
        Assert.Null(round.SurfacePreview.Samples[0].GroundAltitudeMetres);
        Assert.Equal("downward_samples_not_route_clearance", round.SurfacePreview.EvidenceScope);
    }

    [Fact]
    public void ExtendedDefaultDoesNotInventAnyObservedSamplesOrAuthority()
    {
        var preview = new MovementSurfacePreview();
        Assert.Equal(64, preview.MaximumArcMetres); Assert.Equal(1, preview.MaximumSampleSpacingMetres);
        Assert.Equal("unavailable", preview.State); Assert.Equal("unknown", preview.ShoreRisk);
        Assert.Null(preview.ArcLengthMetres); Assert.Null(preview.WaterItemId);
        Assert.Empty(preview.Samples); Assert.Equal(0, preview.RaycastCount);
    }

    [Theory]
    [InlineData(32)] [InlineData(64)]
    public void ExplicitOlderOrCurrentCapRemainsItsOwnObservationWithoutPromotingPartialOrMissingEvidence(int cap)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var preview = new MovementSurfacePreview
        {
            MaximumArcMetres = cap, State = "partial", UnknownSampleCount = 1,
            Samples = new() { new() { GroundHit = true, WaterHit = false, GroundAltitudeMetres = .2 } },
        };
        var copy = JsonSerializer.Deserialize<MovementSurfacePreview>(JsonSerializer.Serialize(preview, options), options)!;
        Assert.Equal(cap, copy.MaximumArcMetres); Assert.Equal("partial", copy.State);
        Assert.Equal(1, copy.UnknownSampleCount); Assert.Equal("unknown", copy.ShoreRisk);
        Assert.Null(copy.Samples[0].GroundBelowWaterMetres);
    }
}
