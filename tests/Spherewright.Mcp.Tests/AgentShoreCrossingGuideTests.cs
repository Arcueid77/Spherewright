using System.ComponentModel;
using System.Reflection;
using Spherewright.Mcp.Resources;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed class AgentShoreCrossingGuideTests
{
    [Fact]
    public void EmbeddedGuideDistinguishesAReverifiedLandingFromUnknownWaterMidpoints()
    {
        var resource = AgentPlaybookResources.GetOpeningMovementPlaybook();
        Assert.Equal(AgentPlaybookResources.OpeningMovementUri, resource.Uri);
        Assert.Contains("previously verified Walk destination", resource.Text, StringComparison.Ordinal);
        Assert.Contains("specifically re-observed in the current owned world", resource.Text, StringComparison.Ordinal);
        Assert.Contains("single continuous native Move", resource.Text, StringComparison.Ordinal);
        Assert.Contains("Never create intermediate water targets", resource.Text, StringComparison.Ordinal);
        Assert.Contains("A building's presence alone is not a verified Walk landing", resource.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShoreCrossingGuidanceKeepsEnergyOutcomeAndUnknownEvidenceBoundaries()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("finite crossing and recovery reserve", guide, StringComparison.Ordinal);
        Assert.Contains("does not turn `unavailable` into dry-ground evidence", guide, StringComparison.Ordinal);
        Assert.Contains("not a general exception for unknown shores", guide, StringComparison.Ordinal);
        Assert.Contains("same action to terminal", guide, StringComparison.Ordinal);
        Assert.Contains("do not submit the same target again", guide, StringComparison.Ordinal);
        Assert.Contains("`movementState=Walk`", guide, StringComparison.Ordinal);
        Assert.Contains("not native pathfinding or route clearance", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortCrossingGuideRequiresCompleteEvidenceAndAnAboveWaterLandingMargin()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("fully observed short crossing", guide, StringComparison.Ordinal);
        Assert.Contains("water in transit", guide, StringComparison.Ordinal);
        Assert.Contains("`state=observed`", guide, StringComparison.Ordinal);
        Assert.Contains("`unknownSampleCount=0`", guide, StringComparison.Ordinal);
        Assert.Contains("`arcLengthMetres<=64`", guide, StringComparison.Ordinal);
        Assert.Contains("arcLengthMetres/(samples.Count-1)<=1", guide, StringComparison.Ordinal);
        Assert.Contains("at least4m", guide, StringComparison.Ordinal);
        Assert.Contains("`groundHit=true`, `waterHit=true`", guide, StringComparison.Ordinal);
        Assert.Contains("finite non-null `groundBelowWaterMetres<=-0.1`", guide, StringComparison.Ordinal);
        Assert.Contains("`arrivalTolerance=1.5`", guide, StringComparison.Ordinal);
        Assert.Contains("not a forced0.5m", guide, StringComparison.Ordinal);
        Assert.Contains("three-dimensional distance", guide, StringComparison.Ordinal);
        Assert.Contains("`2*arrivalTolerance+1`", guide, StringComparison.Ordinal);
        Assert.Contains("never widen a failed action's tolerance and replay it", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortCrossingGuideDoesNotPromoteShallowOrUnknownSamplesToWalkProof()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("not proof of Walk or route clearance", guide, StringComparison.Ordinal);
        Assert.Contains("shallow-water endpoint, missing sample, `partial` or `unavailable`", guide, StringComparison.Ordinal);
        Assert.Contains("check the evidence again, commit once, poll to terminal", guide, StringComparison.Ordinal);
        Assert.Contains("Stop on failure or non-ready arrival, retain the action", guide, StringComparison.Ordinal);
        Assert.Contains("not turn this into a candidate sweep or an automatic routing loop", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void LandingMarginDoesNotReplaceNativeBuildingPlacementChecks()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("Move approach rule, not a building-placement gate", guide, StringComparison.Ordinal);
        Assert.Contains("a successful movement preview does not approve its footprint", guide, StringComparison.Ordinal);
        Assert.Contains("within the current native construction range", guide, StringComparison.Ordinal);
        Assert.Contains("do not add a needless Move", guide, StringComparison.Ordinal);
        Assert.Contains("native whole-footprint ground, collision, technology and material checks remain mandatory", guide, StringComparison.Ordinal);
        Assert.Contains("`NeedGround` rejection must not be overridden using path samples", guide, StringComparison.Ordinal);
        Assert.Contains("power coverage at the actual `plannedPosition`", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtendedPreviewBudgetIsDiscoverableWithoutChangingCommitOrStableLandingAuthority()
    {
        var description = typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareMoveAsync))!
            .GetCustomAttribute<DescriptionAttribute>()!.Description;
        Assert.Contains("at most64m", description, StringComparison.Ordinal);
        Assert.Contains("at most65 samples", description, StringComparison.Ordinal);
        Assert.Contains("at most130 native downward rays", description, StringComparison.Ordinal);
        Assert.Contains("does not change existing hash/commit admission", description, StringComparison.Ordinal);
        Assert.Contains("not route clearance or guaranteed Walk", description, StringComparison.Ordinal);
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        Assert.Contains("≤64 m/65 samples/130 rays", guide, StringComparison.Ordinal);
        Assert.Contains("600 consecutive grounded low-speed ticks", guide, StringComparison.Ordinal);
        Assert.Contains("never widen a failed action's tolerance and replay it", guide, StringComparison.Ordinal);
    }
}
