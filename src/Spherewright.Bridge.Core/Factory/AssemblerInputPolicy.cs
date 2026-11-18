using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Factory;

public static class AssemblerInputPolicy
{
    public const int EvidenceVersion = 1;
    public const string NativeOperation = "PlanetFactory.InsertInto";

    // Native byte arguments are supplied in chunks of at most 25 items, each
    // carrying up to ten proliferator points per item without truncation.
    public static bool CanInsert(int itemId, int count, int packageCount, int inputCount, int inputInc,
        bool exactAssembler, bool requiredByRecipe, bool withinReach, bool emptyHand, bool packageIncValid) =>
        itemId > 0 && count >= 1 && count <= 100 && packageCount >= count
        && inputCount >= 0 && inputCount <= int.MaxValue - count
        && inputInc >= 0 && inputInc <= int.MaxValue - count * 10
        && exactAssembler && requiredByRecipe && withinReach && emptyHand && packageIncValid;

    public static bool BuffersConserved(IReadOnlyList<int>? beforeServed, IReadOnlyList<int>? afterServed,
        IReadOnlyList<int>? beforeInc, IReadOnlyList<int>? afterInc,
        IReadOnlyList<int>? beforeProduced, IReadOnlyList<int>? afterProduced, int inputIndex, int count, int inc)
    {
        if (beforeServed is null || afterServed is null || beforeInc is null || afterInc is null
            || beforeProduced is null || afterProduced is null || beforeServed.Count != afterServed.Count
            || beforeInc.Count != afterInc.Count || beforeProduced.Count != afterProduced.Count
            || inputIndex < 0 || inputIndex >= beforeServed.Count || inputIndex >= beforeInc.Count
            || count < 1 || inc < 0) return false;
        for (var i = 0; i < beforeServed.Count; i++)
            if ((long)afterServed[i] != (long)beforeServed[i] + (i == inputIndex ? count : 0)) return false;
        for (var i = 0; i < beforeInc.Count; i++)
            if ((long)afterInc[i] != (long)beforeInc[i] + (i == inputIndex ? inc : 0)) return false;
        for (var i = 0; i < beforeProduced.Count; i++)
            if (afterProduced[i] != beforeProduced[i]) return false;
        return true;
    }

    public static bool HasMatchingEcho(int entityId, int itemId, int count, PreparedNormalAction? prepared)
    {
        var plan = prepared?.AssemblerInput;
        return prepared is { Prepared: true, CommitAllowedNow: true, ActionKind: NormalActionKinds.Transfer } && plan is not null
            && entityId > 0 && itemId > 0 && count is >= 1 and <= 100 && prepared.DestinationObjectId == entityId
            && plan.EvidenceVersion == EvidenceVersion && plan.EntityId == entityId
            && plan.BuildingItemId > 0 && plan.RecipeId > 0 && plan.ItemId == itemId && plan.Count == count
            && plan.BeforeInputCount >= 0 && plan.NativeOperation == NativeOperation
            && prepared.ItemBudget.Count(row => row.ItemId == itemId && row.Count == count
                && row.Direction == TransferDirections.PlayerToAssemblerInput) == 1;
    }

    public static bool HasConservedResult(int entityId, int itemId, int count, ActionResultSnapshot? result)
    {
        var readback = result?.AssemblerInputReadback;
        return result is { Terminal: true, Succeeded: true, ActionKind: NormalActionKinds.Transfer }
            && readback is { EvidenceVersion: EvidenceVersion, ExactNativeBindingVerified: true,
                OtherBuffersPreserved: true, OtherPackageStockPreserved: true, ConservationVerified: true }
            && entityId > 0 && itemId > 0 && count is >= 1 and <= 100
            && readback.EntityId == entityId && readback.ItemId == itemId && readback.RequestedCount == count
            && readback.RecipeId > 0 && readback.CapturedAtGameTick == result.CompletedAtGameTick
            && readback.InputCountBefore >= 0 && readback.PackageCountAfter >= 0
            && readback.InputIncBefore >= 0 && readback.PackageIncAfter >= 0
            && (long)readback.InputCountAfter - readback.InputCountBefore == count
            && (long)readback.PackageCountBefore - readback.PackageCountAfter == count
            && (long)readback.InputIncAfter - readback.InputIncBefore == (long)readback.PackageIncBefore - readback.PackageIncAfter
            && readback.InputIncAfter >= readback.InputIncBefore && readback.PackageIncBefore >= readback.PackageIncAfter;
    }
}
