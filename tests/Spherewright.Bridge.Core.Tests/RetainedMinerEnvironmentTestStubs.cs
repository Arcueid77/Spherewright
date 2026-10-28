using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
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
    internal TestEntity[] entityPool = new TestEntity[64];
    internal TestPrebuild[] prebuildPool = new TestPrebuild[64];
    internal TestFactorySystem factorySystem = new();
    internal TestPowerSystem powerSystem = new();
    internal TestTransport transport = new();
    internal TestCargoTraffic cargoTraffic = new();
    internal object cargoContainer = new();
    internal bool TestGeometryMatches = true, TestConnectionsMatch = true;
}
internal struct TestEntity { internal int id, protoId, minerId, powerConId, powerNodeId, stationId, beltId; }
internal struct TestPrebuild { internal int id; }
internal sealed class TestFactorySystem { internal int minerCursor = 2; internal TestMiner[] minerPool = new TestMiner[4]; }
internal struct TestMiner { internal int id, entityId, pcId, productId, productCount; }
internal sealed class TestPowerSystem
{
    internal int consumerCursor = 2, netCursor = 1, nodeCursor = 1, genCursor = 1;
    internal TestConsumer[] consumerPool = new TestConsumer[4];
    internal TestPowerNode[] nodePool = new TestPowerNode[4];
    internal TestPowerNet?[] netPool = new TestPowerNet?[4];
}
internal struct TestPowerNode { internal int id, entityId; }
internal sealed class TestPowerNet
{
    internal List<TestNetworkNode> nodes = [];
    internal List<int> generators = [], accumulators = [], exchangers = [];
    internal long energyStored, energyServed, energyCapacity, energyExport;
}
internal sealed class TestNetworkNode
{
    internal int id, genId, accId, excId;
    internal List<TestNetworkNode> conns = [], lines = [];
}
internal struct TestConsumer { internal int id, entityId, networkId; }
internal enum ELogisticStorage { None, Supply, Demand }
internal enum IODir { None, Input, Output }
internal sealed class TestTransport { internal int stationCursor = 2; internal TestStation?[] stationPool = new TestStation?[4]; }
internal sealed class TestStation
{
    internal int id, entityId, planetId, gid, warperCount, idleShipCount, workShipCount, idleDroneCount, workDroneCount;
    internal long energy;
    internal bool isStellar = true, isCollector, isVeinCollector;
    internal TestStorage[] storage = new TestStorage[5];
    internal TestSlot[] slots = new TestSlot[12];
}
internal struct TestStorage { internal int itemId, count, inc, max, localOrder, remoteOrder; internal ELogisticStorage localLogic, remoteLogic; }
internal struct TestSlot { internal int beltId, counter, storageIdx; internal IODir dir; }
internal struct TestBelt { internal int id, entityId, segPathId, segIndex, segLength; }
internal sealed class TestCargoTraffic
{
    internal int beltCursor = 1, pathCursor = 1;
    internal TestBelt[] beltPool = new TestBelt[32];
    internal TestCargoPath?[] pathPool = new TestCargoPath?[16];
    internal TestCargoPath? GetCargoPath(int id) => pathPool[id];
}
internal sealed class TestCargoPath
{
    internal int id;
    internal bool closed;
    internal byte[] buffer = [];
    internal object cargoContainer = null!;
    internal BeltPathExportSnapshot Snapshot = null!;
}
// The Native Export/geometry adapter is substituted; the linked production
// whole-path membership/cargo policy and durable recovery predicate run below.
internal static class NativeBeltPathCapture
{
    internal static bool TryRead(TestCargoPath path, out BeltPathExportSnapshot? snapshot, out string? reason)
    { snapshot = path.Snapshot; reason = null; return snapshot is not null; }
}
internal sealed partial class NormalGameActionCoordinator
{
    private readonly BlueprintBuildStore _blueprints = null!;
    internal NormalGameActionCoordinator(BlueprintBuildStore store, GameSessionTracker sessions)
    { _blueprints = store; _sessions = sessions; }
    internal string? MinerEnvironmentForTest(Player player, string? journeyId, NativeResumeDecisionEvidence? evidence = null)
        => RemoteResumeChargeEnvironmentBlocker(player, journeyId, evidence);
    // These legacy single-miner regressions do not simulate the Native tower
    // or stop-only stage4 controller. Its motion/cost proof has separate tests.
    private BlueprintBuildState? RetainedUnpoweredRecoveryIls(Player p, PlanetFactory f, string? journeyId, NativeResumeDecisionEvidence? evidence) => null;
    private bool SavedConstructionTailMatches(Player p, string? journeyId) => false;
    internal static bool SavedTowerReceiptForTest(BlueprintBuildState b, string journeyId, long tick) => HasSavedPassiveTowerReceipt(b, journeyId, tick);
    private static bool StructuredEntityMatches(PlanetFactory factory, int id, BlueprintBuildState build, BlueprintSiteObject spec)
        => factory.TestGeometryMatches && id > 0 && id < factory.entityCursor && factory.entityPool[id].id == id
            && factory.entityPool[id].protoId == spec.ItemId
            && (spec.ItemId == 2301 ? factory.entityPool[id].minerId > 0
                : spec.ItemId == 2104 ? factory.entityPool[id].stationId > 0
                : spec.ItemId == 2001 ? factory.entityPool[id].beltId > 0 : factory.entityPool[id].powerNodeId > 0);
    private static bool StructuredConnectionsMatch(PlanetFactory factory, BlueprintBuildState build) => factory.TestConnectionsMatch;
    private static bool TryObserveOwnedConstructionDrones(PlanetFactory factory, ConstructionModuleComponent module,
        out bool idle, out List<int> activeIds)
    { idle = module.TestPhysicalIdle; activeIds = []; return true; }
}
