using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

// TEST ONLY Native pool/value substitutes. The environment predicate and owned
// durable store are linked production; geometry/drone helpers remain substitutes.
#pragma warning disable CS0649
namespace Spherewright.Plugin.Game;
internal static partial class GameMain { internal static TestHistory? history = new(); }
internal sealed class TestHistory { internal int currentTech; internal int[] techQueue = new int[32]; }
internal sealed partial class Player { internal PlanetFactory? factory; internal int planetId = 201; }
internal sealed partial class PlayerController { internal TestMotion actionWalk = new(), actionDrift = new(); }
internal sealed class TestMotion { internal UnityEngine.Vector3 rtsVelocity, moveVelocity; }
internal sealed partial class Mecha { internal ConstructionModuleComponent constructionModule = new(); }
internal sealed class TestTarget { internal int objectId; }
internal sealed class ConstructionModuleComponent
{
    internal int buildTargetTotalCount, repairTargetTotalCount, autoReconstructTargetTotalCount;
    internal TestTarget[] buildTargets = [], repairTargets = [], autoReconstructTargets = [];
    internal bool droneEnabled = true, TestPhysicalIdle = true;
}
internal sealed class PlanetFactory
{
    internal int planetId = 201, entityCount, entityCursor = 1, prebuildCount, prebuildCursor = 1;
    internal TestEntity[] entityPool = new TestEntity[4];
    internal TestPrebuild[] prebuildPool = new TestPrebuild[4];
    internal TestFactorySystem factorySystem = new();
    internal TestPowerSystem powerSystem = new();
    internal bool TestGeometryMatches = true, TestConnectionsMatch = true;
}
internal struct TestEntity { internal int id, protoId, minerId, powerConId; }
internal struct TestPrebuild { internal int id; }
internal sealed class TestFactorySystem { internal int minerCursor = 2; internal TestMiner[] minerPool = new TestMiner[4]; }
internal struct TestMiner { internal int id, entityId, pcId, productId, productCount; }
internal sealed class TestPowerSystem
{
    internal int consumerCursor = 2, netCursor = 1, nodeCursor = 1, genCursor = 1;
    internal TestConsumer[] consumerPool = new TestConsumer[4];
}
internal struct TestConsumer { internal int id, entityId, networkId; }
internal sealed partial class NormalGameActionCoordinator
{
    private readonly BlueprintBuildStore _blueprints = null!;
    internal NormalGameActionCoordinator(BlueprintBuildStore store, GameSessionTracker sessions)
    { _blueprints = store; _sessions = sessions; }
    internal string? MinerEnvironmentForTest(Player player, string? journeyId) => RemoteResumeChargeEnvironmentBlocker(player, journeyId);
    private static bool StructuredEntityMatches(PlanetFactory factory, int id, BlueprintBuildState build, BlueprintSiteObject spec)
        => factory.TestGeometryMatches && id > 0 && id < factory.entityCursor && factory.entityPool[id].id == id
            && factory.entityPool[id].protoId == spec.ItemId && factory.entityPool[id].minerId > 0;
    private static bool StructuredConnectionsMatch(PlanetFactory factory, BlueprintBuildState build) => factory.TestConnectionsMatch;
    private static bool TryObserveOwnedConstructionDrones(PlanetFactory factory, ConstructionModuleComponent module,
        out bool idle, out List<int> activeIds)
    { idle = module.TestPhysicalIdle; activeIds = []; return true; }
}
