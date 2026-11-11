using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static VoyagerMechaWorkFacts CaptureVoyagerMechaWork(Player player)
    {
        var facts = new VoyagerMechaWorkFacts();
        var mecha = player?.mecha;
        var history = GameMain.history;
        if (mecha?.forge?.tasks is null || history?.techQueue is null || history.techQueue.Length > 128) return facts;
        facts.ForgeObserved = true;
        facts.ForgeTaskCount = mecha.forge.tasks.Count;
        facts.HistoryObserved = true;
        var emptySlotSeen = false;
        foreach (var id in history.techQueue)
        {
            if (id == 0) emptySlotSeen = true;
            else if (id < 0 || emptySlotSeen) return facts;
        }
        facts.QueuedTechnologyIds = history.techQueue.Where(id => id > 0).ToArray();
        if (history.currentTech != (facts.QueuedTechnologyIds.Length > 0 ? facts.QueuedTechnologyIds[0] : 0))
        { facts.QueuedTechnologyIds = null; return facts; }
        if (facts.ForgeTaskCount != 0 || facts.QueuedTechnologyIds.Length == 0) return facts;
        if (facts.QueuedTechnologyIds.Length > VoyagerMechaWorkQuiescencePolicy.MaximumQueuedTechnologies
            || history.currentTech > 0 && history.currentTech != facts.QueuedTechnologyIds[0]) return facts;
        var lab = mecha.lab;
        var package = player!.package;
        if (lab?.itemPoints?.items is null || !ReferenceEquals(lab.player, player) || !ReferenceEquals(lab.mecha, mecha)
            || !ReferenceEquals(lab.gameHistory, history) || package?.grids is null || package.size < 1
            || package.size > 512 || package.size > package.grids.Length
            || lab.itemPoints.items.Count > VoyagerMechaWorkQuiescencePolicy.MaximumBufferedItems) return facts;
        foreach (var pair in lab.itemPoints.items)
            if (pair.Key <= 0 || pair.Value < 0 || LDB.items.Select(pair.Key) is null) return facts;
        for (var i = 0; i < package.size; i++)
        {
            var grid = package.grids[i];
            if (grid.count < 0 || grid.itemId < 0 || (grid.count > 0 && LDB.items.Select(grid.itemId) is null)
                || grid.itemId == 0 && grid.count != 0) return facts;
        }
        facts.MechaLabBindingVerified = true;
        facts.ResearchBufferPointCounts = lab.itemPoints.items.Select(pair => pair.Value).ToArray();
        facts.QueuedResearch = new List<VoyagerQueuedResearchFacts>();
        foreach (var id in facts.QueuedTechnologyIds)
        {
            var tech = LDB.techs.Select(id);
            if (tech is null || tech.ID != id || !history.techStates.ContainsKey(id)
                || tech.Items is null || tech.ItemPoints is null || tech.Items.Length < 1
                || tech.Items.Length > VoyagerMechaWorkQuiescencePolicy.MaximumResearchItems
                || tech.ItemPoints.Length != tech.Items.Length || tech.Items.Any(item => LDB.items.Select(item) is null)
                || tech.UnlockRecipes is null || tech.UnlockRecipes.Length > 64
                || tech.UnlockRecipes.Any(recipe => recipe <= 0 || LDB.recipes.Select(recipe) is null)
                || tech.UnlockFunctions is null || tech.UnlockValues is null || tech.AddItems is null || tech.AddItemCounts is null
                || tech.UnlockFunctions.Length != tech.UnlockValues.Length || tech.AddItems.Length != tech.AddItemCounts.Length) return facts;
            facts.QueuedResearch.Add(new VoyagerQueuedResearchFacts
            {
                TechnologyId = id, NativeIdentityVerified = true, IsLabTechnology = tech.IsLabTech,
                RecipeUnlocksVerified = true, UnlockFunctionCount = tech.UnlockFunctions.Length,
                UnlockFunctionIds = tech.UnlockFunctions.ToArray(),
                UnlockFunctionValues = tech.UnlockValues.Select(value => (double)value).ToArray(),
                CompletionAwardCount = tech.AddItems.Length, RequiredItemIds = tech.Items.ToArray(),
                RequiredItemPointsPerHash = tech.ItemPoints.ToArray(),
                RequiredItemPackageCounts = tech.Items.Select(item => package.GetItemCount(item)).ToArray(),
            });
        }
        return facts;
    }

    private static bool VoyagerMechaWorkIdle(Player player) =>
        VoyagerMechaWorkQuiescencePolicy.Evaluate(CaptureVoyagerMechaWork(player)).Proved;
}
