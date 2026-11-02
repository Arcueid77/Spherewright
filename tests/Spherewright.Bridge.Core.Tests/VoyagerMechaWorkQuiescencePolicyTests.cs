using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerMechaWorkQuiescencePolicyTests
{
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
