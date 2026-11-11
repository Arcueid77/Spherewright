using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerMechaWorkQuiescencePolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7200)]
    [InlineData(5760000)]
    public void NavigationPricesVerifiedDysonLatitudeResearchWithKnownRecipeMetadata(int bufferedPoints)
    {
        var facts = Good();
        facts.ResearchBufferPointCounts = new[] { bufferedPoints };
        var tech = facts.QueuedResearch![0];
        tech.UnlockFunctionCount = 1;
        tech.UnlockFunctionIds = new[] { 26 };
        tech.UnlockFunctionValues = new[] { 15d };
        tech.RequiredItemIds = new[] { 6001, 6002, 6003, 6004, 6005 };
        tech.RequiredItemPointsPerHash = new[] { 20, 20, 20, 20, 20 };
        tech.RequiredItemPackageCounts = new[] { 0, 0, 0, 0, 0 };

        Assert.True(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts,
            allowRecipeOnlyMechaResearch: true, allowDysonLatitudeResearch: true).Proved);
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts,
            allowRecipeOnlyMechaResearch: true).Proved);
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts).Proved);
        Assert.Equal(bufferedPoints, facts.ResearchBufferPointCounts[0]);
        Assert.Equal(new[] { 26 }, tech.UnlockFunctionIds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(27)]
    public void DysonOptInRejectsOtherNativeUnlockFunctions(int function)
    {
        var facts = Good();
        var tech = facts.QueuedResearch![0];
        tech.UnlockFunctionCount = 1;
        tech.UnlockFunctionIds = new[] { function };
        tech.UnlockFunctionValues = new[] { 15d };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts,
            allowDysonLatitudeResearch: true).Proved);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1d)]
    [InlineData(0d)]
    [InlineData(91d)]
    public void DysonLatitudeRequiresFiniteNativeIncrement(double value)
    {
        var facts = Good();
        var tech = facts.QueuedResearch![0];
        tech.UnlockFunctionCount = 1;
        tech.UnlockFunctionIds = new[] { 26 };
        tech.UnlockFunctionValues = new[] { value };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts,
            allowDysonLatitudeResearch: true).Proved);
    }

    [Fact]
    public void DysonLatitudeRetainsCompleteCoverageAndAwardChecks()
    {
        var facts = Good();
        var tech = facts.QueuedResearch![0];
        tech.UnlockFunctionCount = 1;
        tech.UnlockFunctionIds = new[] { 26 };
        tech.UnlockFunctionValues = new[] { 15d };
        Assert.True(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts,
            allowDysonLatitudeResearch: true).Proved);
        tech.CompletionAwardCount = 1;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts,
            allowDysonLatitudeResearch: true).Proved);
        tech.CompletionAwardCount = 0;
        tech.UnlockFunctionValues = null;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts,
            allowDysonLatitudeResearch: true).Proved);
        tech.UnlockFunctionValues = new[] { 15d };
        tech.UnlockFunctionIds = new[] { 26, 26 };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts,
            allowDysonLatitudeResearch: true).Proved);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3599)] [InlineData(3600)]
    [InlineData(46800)] [InlineData(int.MaxValue)]
    public void PricedRecipeOnlyNavigationResearchPreservesObservedBufferedPoints(int points)
    {
        var facts = Good();
        facts.ResearchBufferPointCounts = new[] { points };
        var decision = VoyagerMechaWorkQuiescencePolicy.Evaluate(facts, allowRecipeOnlyMechaResearch: true);
        Assert.True(decision.Proved);
        Assert.Equal(points == 0, decision.FactoryOnlyResearch);
        Assert.Equal(points, facts.ResearchBufferPointCounts[0]);
        Assert.Equal(points == 0, VoyagerMechaWorkQuiescencePolicy.Evaluate(facts).Proved);
    }

    [Theory]
    [InlineData(-1)] [InlineData(int.MinValue)]
    public void PricedResearchStillRejectsNegativeNativeBufferCounts(int points)
    {
        var facts = Good();
        facts.ResearchBufferPointCounts = new[] { points };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts, allowRecipeOnlyMechaResearch: true).Proved);
    }

    [Theory]
    [InlineData(0, true)] [InlineData(600, true)] [InlineData(-1, false)]
    public void PricedRecipeOnlyResearchIncludesAvailableMatrices(int count, bool expected)
    {
        var facts = Good();
        facts.QueuedResearch![0].RequiredItemPackageCounts![0] = count;
        var decision = VoyagerMechaWorkQuiescencePolicy.Evaluate(facts, allowRecipeOnlyMechaResearch: true);
        Assert.Equal(expected, decision.Proved);
        if (expected) Assert.Equal(count == 0, decision.FactoryOnlyResearch);
    }

    [Theory]
    [InlineData("forge")] [InlineData("binding")] [InlineData("effects")]
    [InlineData("award")] [InlineData("coverage")] [InlineData("prototype")]
    [InlineData("nonlab")] [InlineData("recipes")] [InlineData("points")]
    public void PricedResearchRetainsCompleteNativeEffectAndIdentityChecks(string fault)
    {
        var facts = Good();
        facts.ResearchBufferPointCounts = new[] { 46800 };
        switch (fault)
        {
            case "forge": facts.ForgeTaskCount = 1; break;
            case "binding": facts.MechaLabBindingVerified = false; break;
            case "effects": facts.QueuedResearch![0].UnlockFunctionCount = 1; break;
            case "award": facts.QueuedResearch![0].CompletionAwardCount = 1; break;
            case "coverage": facts.QueuedResearch = null; break;
            case "prototype": facts.QueuedResearch![0].NativeIdentityVerified = false; break;
            case "nonlab": facts.QueuedResearch![0].IsLabTechnology = false; break;
            case "recipes": facts.QueuedResearch![0].RecipeUnlocksVerified = false; break;
            case "points": facts.QueuedResearch![0].RequiredItemPointsPerHash = new[] { 0, 12 }; break;
        }
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(facts, allowRecipeOnlyMechaResearch: true).Proved);
    }

    private static VoyagerMechaWorkFacts Good() => new()
    {
        ForgeObserved = true, HistoryObserved = true, QueuedTechnologyIds = new[] { 42 },
        MechaLabBindingVerified = true, ResearchBufferPointCounts = Array.Empty<int>(),
        QueuedResearch = new List<VoyagerQueuedResearchFacts> { Tech(42) },
    };

    private static VoyagerQueuedResearchFacts Tech(int id) => new()
    {
        TechnologyId = id, NativeIdentityVerified = true, IsLabTechnology = true, RecipeUnlocksVerified = true,
        UnlockFunctionCount = 0, CompletionAwardCount = 0,
        RequiredItemIds = new[] { 6001, 6002 }, RequiredItemPointsPerHash = new[] { 12, 12 },
        RequiredItemPackageCounts = new[] { 0, 0 },
    };

    [Fact]
    public void RecipeOnlyFactoryResearchWithNoMechaInputsIsQuiescent()
    {
        var result = VoyagerMechaWorkQuiescencePolicy.Evaluate(Good());
        Assert.True(result.Proved);
        Assert.True(result.FactoryOnlyResearch);
    }

    [Fact]
    public void EmptyQueueRetainsExistingIdlePath()
    {
        var result = VoyagerMechaWorkQuiescencePolicy.Evaluate(new()
        { ForgeObserved = true, HistoryObserved = true, QueuedTechnologyIds = Array.Empty<int>() });
        Assert.True(result.Proved);
        Assert.False(result.FactoryOnlyResearch);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ForgeTaskOrUnknownCountNeverQualifies(int tasks)
    {
        var f = Good(); f.ForgeTaskCount = tasks;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }

    [Fact]
    public void MissingNativeBindingsAreUnknown()
    {
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(null).Proved);
        var f = Good(); f.ForgeObserved = false;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.HistoryObserved = false;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.MechaLabBindingVerified = false;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3599)]
    [InlineData(3600)]
    [InlineData(-1)]
    public void EvenFractionalBufferedResearchPointsBlock(int points)
    {
        var f = Good(); f.ResearchBufferPointCounts = new[] { points };
        Assert.Equal("mecha_research_buffer_not_empty", VoyagerMechaWorkQuiescencePolicy.Evaluate(f).BlockerCode);
    }

    [Fact]
    public void EmptyNativeEntriesMayRemainButUnknownBufferDoesNotQualify()
    {
        var f = Good(); f.ResearchBufferPointCounts = new[] { 0, 0 };
        Assert.True(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f.ResearchBufferPointCounts = null;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void AvailableOrInvalidPackageResearchCountBlocks(int count)
    {
        var f = Good(); f.QueuedResearch![0].RequiredItemPackageCounts![1] = count;
        Assert.Equal("mecha_research_inputs_available", VoyagerMechaWorkQuiescencePolicy.Evaluate(f).BlockerCode);
    }

    [Fact]
    public void EveryLaterQueuedTechnologyIsChecked()
    {
        var f = Good(); f.QueuedTechnologyIds = new[] { 42, 43 }; f.QueuedResearch!.Add(Tech(43));
        f.QueuedResearch[1].RequiredItemIds = new[] { 6004, 6005 };
        Assert.True(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f.QueuedResearch[1].RequiredItemPackageCounts![0] = 1;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(1, 0)]
    [InlineData(0, null)]
    [InlineData(0, 1)]
    public void MovementAttributeFunctionsAndItemAwardsNeverQualify(int? functions, int? awards)
    {
        var f = Good(); f.QueuedResearch![0].UnlockFunctionCount = functions;
        f.QueuedResearch[0].CompletionAwardCount = awards;
        Assert.Equal("queued_research_native_effects_unproved", VoyagerMechaWorkQuiescencePolicy.Evaluate(f).BlockerCode);
    }

    [Fact]
    public void IncompleteOrMismatchedQueuedCoverageBlocks()
    {
        var f = Good(); f.QueuedResearch = null;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedResearch!.Clear();
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedResearch![0].TechnologyId = 43;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }

    [Fact]
    public void UnknownPrototypeAndNonLabOrUnprovedRecipeMetadataBlock()
    {
        var f = Good(); f.QueuedResearch![0].NativeIdentityVerified = false;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedResearch![0].IsLabTechnology = false;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedResearch![0].RecipeUnlocksVerified = false;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }

    [Fact]
    public void FreeResearchMissingRequirementsAndMalformedPointsDoNotQualify()
    {
        var f = Good(); f.QueuedResearch![0].RequiredItemIds = Array.Empty<int>();
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedResearch![0].RequiredItemPointsPerHash = new[] { 0, 12 };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedResearch![0].RequiredItemPackageCounts = new[] { 0 };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedResearch![0].RequiredItemIds = new[] { 6001, 6001 };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }

    [Fact]
    public void MissingDuplicateInvalidOrOversizedQueuesBlock()
    {
        var f = Good(); f.QueuedTechnologyIds = null;
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedTechnologyIds = new[] { 42, 42 };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedTechnologyIds = new[] { 0 };
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        f = Good(); f.QueuedTechnologyIds = Enumerable.Range(1, 33).ToArray();
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }

    [Fact]
    public void ExcessBufferedCoverageIsUnknownAndProofDoesNotMutateFacts()
    {
        var f = Good();
        Assert.True(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
        Assert.Equal(new[] { 42 }, f.QueuedTechnologyIds);
        Assert.Equal(new[] { 0, 0 }, f.QueuedResearch![0].RequiredItemPackageCounts);
        f.ResearchBufferPointCounts = new int[65];
        Assert.False(VoyagerMechaWorkQuiescencePolicy.Evaluate(f).Proved);
    }
}
