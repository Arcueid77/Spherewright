namespace Spherewright.Bridge.Core.Safety;

public sealed class MechaWarperSupplyFacts
{
    public bool OwnedBindingVerified { get; set; }
    public bool CurrentPlanetVerified { get; set; }
    public bool PlayerStateHashFresh { get; set; }
    public bool IsAlive { get; set; }
    public bool IsGrounded { get; set; }
    public bool ControllerEnabled { get; set; }
    public bool NativeGuideRunning { get; set; }
    public bool IsIdle { get; set; }
    public bool ChamberIsFiltered { get; set; }
    public int ChamberSlotCount { get; set; }
    public int ChamberGridCount { get; set; }
    public int ChamberBannedGridCount { get; set; }
    public int ChamberItemId { get; set; }
    public int ChamberFilterItemId { get; set; }
    public int ChamberStackSize { get; set; }
    public int PackageCount { get; set; }
    public int PackageInc { get; set; }
    public int ChamberCount { get; set; }
    public int ChamberInc { get; set; }
    public bool AutoReplenishWarper { get; set; }
}

public sealed class MechaWarperSupplyDecision
{
    public bool Allowed { get; internal set; }
    public int ExactNativeCount { get; internal set; }
    public string? BlockerCode { get; internal set; }
    public string? Message { get; internal set; }
}

public sealed class MechaWarperSupplyVerification
{
    public bool Succeeded { get; internal set; }
    public int? ActualStored { get; internal set; }
    public string? BlockerCode { get; internal set; }
}

// DSP29151 Mecha ctor/SetForNewGame: one Filtered slot, SetFilterDirect(0,1210,20).
// AutoReplenishWarper independently calls TakeItem(1210,20) then AddItem(1210,n,0,1,inc).
// This policy never changes the setting, native inventory or the number of native calls.
public static class MechaWarperSupplyPolicy
{
    public const int WarperItemId = 1210;
    public const int NativeOneStackCount = 20;

    public static MechaWarperSupplyDecision Evaluate(MechaWarperSupplyFacts facts, int requestedCount)
    {
        if (facts is null) throw new ArgumentNullException(nameof(facts));
        if (!facts.OwnedBindingVerified || !facts.CurrentPlanetVerified || !facts.PlayerStateHashFresh)
            return Reject("warper_supply_binding_not_fresh", "The exact owned world/current planet and inspected player hash must still match.");
        if (!facts.IsAlive || !facts.IsGrounded || !facts.ControllerEnabled || facts.NativeGuideRunning || !facts.IsIdle)
            return Reject("warper_supply_grounded_idle_required", "Supply requires an alive grounded idle player with ordinary native control enabled.");
        if (!HasNativeChamberShape(facts))
            return Reject("warper_supply_native_chamber_required", "Only the native one-slot Filtered 1210 chamber with capacity 20 and no bans is supported.");
        if (facts.ChamberCount != 0 || facts.ChamberInc != 0)
            return Reject("warper_supply_empty_chamber_required", "Only an empty native chamber is supported; partial top-up is not prepared.");
        if (facts.PackageCount <= 0 || facts.PackageInc < 0)
            return Reject("warper_supply_package_empty_or_invalid", "The real package must contain a positive valid 1210 count/inc balance.");
        var exactCount = Math.Min(NativeOneStackCount, facts.PackageCount);
        if (requestedCount != exactCount)
            return new MechaWarperSupplyDecision { ExactNativeCount = exactCount, BlockerCode = "warper_supply_exact_count_required",
                Message = $"The native empty-chamber call moves exactly {exactCount} item(s); use that count after inspection." };
        return new MechaWarperSupplyDecision { Allowed = true, ExactNativeCount = exactCount };
    }

    public static string Fingerprint(MechaWarperSupplyFacts facts)
    {
        if (facts is null) throw new ArgumentNullException(nameof(facts));
        return CanonicalStateHash.Combine("mecha-warper-empty-native-v1", facts.OwnedBindingVerified, facts.CurrentPlanetVerified,
            facts.PlayerStateHashFresh, facts.IsAlive, facts.IsGrounded, facts.ControllerEnabled, facts.NativeGuideRunning, facts.IsIdle,
            facts.ChamberIsFiltered, facts.ChamberSlotCount, facts.ChamberGridCount, facts.ChamberBannedGridCount,
            facts.ChamberItemId, facts.ChamberFilterItemId, facts.ChamberStackSize,
            facts.PackageCount, facts.PackageInc, facts.ChamberCount, facts.ChamberInc, facts.AutoReplenishWarper);
    }

    public static MechaWarperSupplyVerification VerifyReadback(MechaWarperSupplyFacts before,
        MechaWarperSupplyFacts after, int preparedCount, bool? nativeReturnedSuccess)
    {
        if (before is null) throw new ArgumentNullException(nameof(before));
        if (after is null) throw new ArgumentNullException(nameof(after));
        var preparation = Evaluate(before, preparedCount);
        if (!preparation.Allowed || !HasNativeChamberShape(after) || after.PackageCount < 0 || after.PackageInc < 0
            || after.ChamberCount < 0 || after.ChamberCount > NativeOneStackCount || after.ChamberInc < 0)
            return new MechaWarperSupplyVerification { BlockerCode = "warper_supply_readback_invalid" };
        var stored = after.ChamberCount - before.ChamberCount;
        var result = new MechaWarperSupplyVerification { ActualStored = stored };
        if (nativeReturnedSuccess != true)
            result.BlockerCode = "warper_supply_native_result_unknown";
        else if (!after.OwnedBindingVerified || !after.CurrentPlanetVerified || !after.IsAlive || !after.IsGrounded
            || !after.ControllerEnabled || after.NativeGuideRunning || !after.IsIdle || before.AutoReplenishWarper != after.AutoReplenishWarper)
            result.BlockerCode = "warper_supply_binding_or_setting_changed";
        else if (stored != preparedCount || (long)before.PackageCount - after.PackageCount != preparedCount
            || (long)before.PackageCount + before.ChamberCount != (long)after.PackageCount + after.ChamberCount
            || (long)before.PackageInc + before.ChamberInc != (long)after.PackageInc + after.ChamberInc)
            result.BlockerCode = "warper_supply_conservation_unproved";
        else result.Succeeded = true;
        return result;
    }

    private static bool HasNativeChamberShape(MechaWarperSupplyFacts facts) => facts.ChamberIsFiltered
        && facts.ChamberSlotCount == 1 && facts.ChamberGridCount == 1 && facts.ChamberBannedGridCount == 0
        && facts.ChamberItemId == WarperItemId && facts.ChamberFilterItemId == WarperItemId && facts.ChamberStackSize == NativeOneStackCount;

    private static MechaWarperSupplyDecision Reject(string code, string message) => new MechaWarperSupplyDecision { BlockerCode = code, Message = message };
}
