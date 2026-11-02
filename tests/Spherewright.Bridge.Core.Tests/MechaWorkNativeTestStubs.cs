using Spherewright.Bridge.Core.Safety;

// TEST ONLY Native object substitutes; the capture and policy are production.
#pragma warning disable CS0649
namespace Spherewright.Plugin.Game;

internal sealed partial class Mecha
{
    internal TestForge forge = new();
    internal TestMechaLab? lab;
}
internal sealed partial class Player { internal TestPackage package = new(); }
internal sealed class TestForge { internal List<object> tasks = new(); }
internal sealed class TestMechaLab
{
    internal Player? player;
    internal Mecha? mecha;
    internal TestHistory? gameHistory;
    internal TestResearchPoints itemPoints = new();
}
internal sealed class TestResearchPoints { internal Dictionary<int, int> items = new(); }
internal sealed class TestPackage
{
    internal int size = 40;
    internal TestPackageGrid[] grids = new TestPackageGrid[40];
    internal int GetItemCount(int id) => grids.Take(size).Where(g => g.itemId == id).Sum(g => g.count);
}
internal struct TestPackageGrid { internal int itemId, count; }
internal static class LDB
{
    internal static TestProtoSet<TestResearchTech> techs = new();
    internal static TestProtoSet<TestWorkProto> items = new(), recipes = new();
}
internal sealed class TestProtoSet<T> where T : class
{
    internal Dictionary<int, T> rows = new();
    internal T? Select(int id) => rows.TryGetValue(id, out var value) ? value : null;
}
internal sealed class TestWorkProto { internal int ID; }
internal sealed class TestResearchTech
{
    internal int ID;
    internal bool IsLabTech = true;
    internal int[] Items = [], ItemPoints = [], UnlockRecipes = [], UnlockFunctions = [], AddItems = [], AddItemCounts = [];
    internal double[] UnlockValues = [];
}
internal sealed partial class NormalGameActionCoordinator
{
    internal static VoyagerMechaWorkDecision CaptureMechaWorkForTest(Player player) =>
        VoyagerMechaWorkQuiescencePolicy.Evaluate(CaptureVoyagerMechaWork(player));
}
#pragma warning restore CS0649
