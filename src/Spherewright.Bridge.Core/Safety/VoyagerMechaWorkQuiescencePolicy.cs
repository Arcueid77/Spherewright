namespace Spherewright.Bridge.Core.Safety;

// Copied complete Native facts only. This policy never pauses research,
// changes a queue or spends/returns matrix items.
// Navigation prices mecha research separately. Its additional Dyson opt-in
// verifies the Native latitude-only effect before permitting queued research.
public sealed class VoyagerMechaWorkFacts
{
    public bool ForgeObserved { get; set; }
    public int ForgeTaskCount { get; set; }
    public bool HistoryObserved { get; set; }
    public bool ResearchQueuePaused { get; set; }
    public int[]? QueuedTechnologyIds { get; set; }
    public bool MechaLabBindingVerified { get; set; }
    public int[]? ResearchBufferPointCounts { get; set; }
    public List<VoyagerQueuedResearchFacts>? QueuedResearch { get; set; }
}

public sealed class VoyagerQueuedResearchFacts
{
    public int TechnologyId { get; set; }
    public bool NativeIdentityVerified { get; set; }
    public bool IsLabTechnology { get; set; }
    public bool RecipeUnlocksVerified { get; set; }
    public int? UnlockFunctionCount { get; set; }
    public int[]? UnlockFunctionIds { get; set; }
    public double[]? UnlockFunctionValues { get; set; }
    public int? CompletionAwardCount { get; set; }
    public int[]? RequiredItemIds { get; set; }
    public int[]? RequiredItemPointsPerHash { get; set; }
    public int[]? RequiredItemPackageCounts { get; set; }
}

public sealed class VoyagerMechaWorkDecision
{
    public bool Proved { get; internal set; }
    public bool FactoryOnlyResearch { get; internal set; }
    public bool PausedResearch { get; internal set; }
    public string? BlockerCode { get; internal set; }
}

public static class VoyagerMechaWorkQuiescencePolicy
{
    public const int MaximumQueuedTechnologies = 32;
    public const int MaximumResearchItems = 16;
    public const int MaximumBufferedItems = 64;

    public static VoyagerMechaWorkDecision Evaluate(VoyagerMechaWorkFacts? facts,
        bool allowRecipeOnlyMechaResearch = false, bool allowDysonLatitudeResearch = false)
    {
        VoyagerMechaWorkDecision Reject(string code) => new() { BlockerCode = code };
        var pricedResearch = allowRecipeOnlyMechaResearch || allowDysonLatitudeResearch;
        if (facts is null || !facts.ForgeObserved || facts.ForgeTaskCount != 0 || !facts.HistoryObserved
            || facts.QueuedTechnologyIds is null || facts.QueuedTechnologyIds.Length > MaximumQueuedTechnologies
            || facts.QueuedTechnologyIds.Any(id => id <= 0)
            || facts.QueuedTechnologyIds.Distinct().Count() != facts.QueuedTechnologyIds.Length)
            return Reject("native_mecha_work_unproved");
        // Preserve the existing empty-queue path. The additive path below is
        // deliberately narrower than arbitrary factory research.
        if (facts.QueuedTechnologyIds.Length == 0)
            return facts.ResearchQueuePaused ? Reject("paused_research_queue_empty") : new() { Proved = true };
        if (!facts.MechaLabBindingVerified || facts.ResearchBufferPointCounts is null
            || facts.ResearchBufferPointCounts.Length > MaximumBufferedItems
            || facts.ResearchBufferPointCounts.Any(count => count < 0
                || !pricedResearch && !facts.ResearchQueuePaused && count != 0))
            return Reject("mecha_research_buffer_not_empty");
        if (facts.QueuedResearch is null || facts.QueuedResearch.Count != facts.QueuedTechnologyIds.Length)
            return Reject("queued_research_coverage_unknown");
        var factoryOnly = !facts.ResearchQueuePaused && facts.ResearchBufferPointCounts.All(count => count == 0);
        for (var i = 0; i < facts.QueuedTechnologyIds.Length; i++)
        {
            var tech = facts.QueuedResearch[i];
            var dysonLatitudeOnly = allowDysonLatitudeResearch && tech is not null
                && tech.UnlockFunctionCount == 1
                && tech.UnlockFunctionIds is { Length: 1 } functions && functions[0] == 26
                && tech.UnlockFunctionValues is { Length: 1 } values
                && !double.IsNaN(values[0]) && !double.IsInfinity(values[0])
                && values[0] > 0 && values[0] <= 90;
            var nativeEffectsKnown = tech is not null && tech.UnlockFunctionCount is >= 0
                && tech.CompletionAwardCount is >= 0;
            var effectsIdle = facts.ResearchQueuePaused || tech is not null
                && (tech.UnlockFunctionCount == 0 || dysonLatitudeOnly) && tech.CompletionAwardCount == 0;
            if (tech is null || tech.TechnologyId != facts.QueuedTechnologyIds[i] || !tech.NativeIdentityVerified
                || !tech.IsLabTechnology || !tech.RecipeUnlocksVerified
                || !nativeEffectsKnown || !effectsIdle)
                return Reject("queued_research_native_effects_unproved");
            var ids = tech.RequiredItemIds;
            var points = tech.RequiredItemPointsPerHash;
            var stock = tech.RequiredItemPackageCounts;
            if (ids is null || points is null || stock is null || ids.Length < 1 || ids.Length > MaximumResearchItems
                || points.Length != ids.Length || stock.Length != ids.Length
                || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length || points.Any(count => count <= 0))
                return Reject("queued_research_inputs_unknown");
            if (stock.Any(count => count < 0 || (!pricedResearch || facts.ResearchQueuePaused) && count != 0))
                return Reject("mecha_research_inputs_available");
            factoryOnly &= stock.All(count => count == 0);
        }
        return new() { Proved = true, FactoryOnlyResearch = factoryOnly, PausedResearch = facts.ResearchQueuePaused };
    }
}
