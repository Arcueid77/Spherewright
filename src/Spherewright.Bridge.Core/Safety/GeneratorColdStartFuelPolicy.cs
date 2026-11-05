using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Safety;

public sealed class GeneratorColdStartFuelFacts
{
    public int BuildingItemId { get; set; }
    public int RequestedCount { get; set; }
    public int PackageCount { get; set; }
    public int RequestedFuelItemId { get; set; }
    public bool ExactNativeComponent { get; set; }
    public bool NativeFuelAdmitted { get; set; }
    public bool ZeroIncPackageFuel { get; set; }
    public bool EmptyPlayerHand { get; set; }
    public bool WithinBuildArea { get; set; }
    public int BufferedFuelId { get; set; }
    public int BufferedFuelCount { get; set; }
    public int BufferedFuelInc { get; set; }
    public long BufferedFuelHeat { get; set; }
    public int CurrentFuelId { get; set; }
    public long LoadedFuelEnergy { get; set; }
    public long CapacityEnergyPerTick { get; set; }
    public long GeneratedEnergyPerTick { get; set; }
}

public static class GeneratorColdStartFuelPolicy
{
    public const int EvidenceVersion = 1;
    public const string NativeOperation = "PlanetFactory.InsertInto";
    public static bool HasMatchingEcho(int entityId, int itemId, int count, PreparedNormalAction? p) =>
        p is { Prepared: true, ActionKind: NormalActionKinds.Transfer, GeneratorColdStartFuel: { } q }
        && entityId > 0 && itemId > 0 && count >= 1 && count <= 10 && p.DestinationObjectId == entityId
        && q.EvidenceVersion == EvidenceVersion && q.EntityId == entityId && q.BuildingItemId is 2204 or 2211
        && q.ItemId == itemId && q.Count == count && q.EmptyUnlitGenerator && q.NativeOperation == NativeOperation
        && p.ItemBudget.Count(x => x.ItemId == itemId && x.Count == count && x.Direction == TransferDirections.PlayerToGeneratorFuel) == 1;
    public static string? Blocker(GeneratorColdStartFuelFacts f)
    {
        if (f.BuildingItemId is not (2204 or 2211) || !f.ExactNativeComponent || !f.NativeFuelAdmitted)
            return "generator_cold_start_native_identity_or_fuel_unproved";
        if (f.RequestedFuelItemId <= 0 || f.RequestedCount < 1 || f.RequestedCount > 10 || f.PackageCount < f.RequestedCount
            || !f.ZeroIncPackageFuel || !f.EmptyPlayerHand || !f.WithinBuildArea)
            return "generator_cold_start_exact_source_or_range_unproved";
        // Native can consume its last heat exactly, then keep curFuelId on
        // subsequent zero-demand ticks. That ID is not remaining fuel/heat.
        // Admit only the same accepted fuel identity, still with no buffered
        // fuel, heat, capacity or generation. Never erase the retained ID.
        if (f.BufferedFuelId != 0 || f.BufferedFuelCount != 0 || f.BufferedFuelInc != 0 || f.BufferedFuelHeat != 0
            || f.CurrentFuelId < 0 || f.CurrentFuelId != 0 && f.CurrentFuelId != f.RequestedFuelItemId
            || f.LoadedFuelEnergy != 0 || f.CapacityEnergyPerTick != 0 || f.GeneratedEnergyPerTick != 0)
            return "generator_cold_start_empty_unlit_generator_required";
        return null;
    }
}
