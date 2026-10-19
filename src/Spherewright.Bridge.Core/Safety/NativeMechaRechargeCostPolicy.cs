namespace Spherewright.Bridge.Core.Safety;

// Detached metadata, not item availability, world exploration or permission.
public sealed class NativeRechargeItemFacts
{
    public int ItemId { get; set; }
    public float ReactorInc { get; set; }
    public bool Productive { get; set; }
}

public sealed class NativeMechaRechargeFacts
{
    public NativeMechaBackgroundCostFacts? Background { get; set; }
    public bool StableCostInputs { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public double? CoreCapacityJoules { get; set; }
    public double? ReactorPowerGenerationWatts { get; set; }
    public float? ShieldRechargeSpeed { get; set; }
    public int? CurrentReactorItemId { get; set; }
    public int? CurrentReactorItemInc { get; set; }
    public bool CurrentSelectionObserved { get; set; }
    public NativeRechargeItemFacts? CurrentSelection { get; set; }
    public IReadOnlyList<NativeRechargeItemFacts?>? ItemCatalog { get; set; }
    public IReadOnlyList<double>? IncTableMilli { get; set; }
    public IReadOnlyList<double>? AccTableMilli { get; set; }
    public string? CaptureBlockerCode { get; set; }
}

public sealed class NativeMechaRechargeCost
{
    public bool Proved { get; internal set; }
    public string? BlockerCode { get; internal set; }
    public double? WeaponPowerUpperWatts { get; internal set; }
    public double? ShieldRechargeEnergyPerNativeTickUpperJoules { get; internal set; }
    public double? LaserRechargeEnergyPerNativeTickUpperJoules { get; internal set; }
    public double? PerNativeTickEnergyUpperJoules { get; internal set; }
}

public static class NativeMechaRechargeCostPolicy
{
    public const int MaximumItemCount = 8192;
    public const int MaximumTableLength = 64;
    // Bounds the UNCLAMPED Native shield expression too: its separate
    // (long)(x+.01)*60 display side effect stays below60*2^50 < Int64.MaxValue.
    // Also below the integer precision boundary; not just a debit-after-clamp.
    private const double MaximumDebit = 1125899906842624d; // 2^50 joules

    public static NativeMechaRechargeCost Evaluate(NativeMechaRechargeFacts? f)
    {
        if (f is null) return Blocked("native_independent_cost_metadata_unobserved");
        if (f.CaptureBlockerCode is not null) return Blocked(f.CaptureBlockerCode);
        var background = BackgroundBlocker(f.Background);
        if (background is not null) return Blocked(background);
        if (!f.StableCostInputs) return Blocked("native_independent_cost_stability_unproved");
        if (!Known(f.CoreEnergyJoules) || !Known(f.CoreCapacityJoules) || f.CoreCapacityJoules <= 0d
            || f.CoreEnergyJoules > f.CoreCapacityJoules)
            return Blocked("native_independent_core_ratio_unproved");
        if (!Known(f.ReactorPowerGenerationWatts)) return Blocked("native_independent_reactor_power_unproved");
        if (!f.ShieldRechargeSpeed.HasValue || !Finite(f.ShieldRechargeSpeed.Value))
            return Blocked("native_independent_shield_speed_unproved");
        if (!f.CurrentReactorItemId.HasValue || f.CurrentReactorItemId < 0 || !f.CurrentReactorItemInc.HasValue
            || f.CurrentReactorItemInc < 0 || f.CurrentReactorItemInc > 10 || !f.CurrentSelectionObserved
            || f.CurrentSelection is { } selected && (f.CurrentReactorItemId == 0 || selected.ItemId != f.CurrentReactorItemId))
            return Blocked("native_independent_current_fuel_unproved");
        if (!TableKnown(f.IncTableMilli) || !TableKnown(f.AccTableMilli))
            return Blocked("native_independent_fuel_tables_unproved");
        if (f.ItemCatalog is null || f.ItemCatalog.Count < 1 || f.ItemCatalog.Count > MaximumItemCount)
            return Blocked("native_independent_item_catalog_unproved");
        var generation = f.ReactorPowerGenerationWatts!.Value;
        var maximum = generation * .5d; // Native's no-prototype weapon fallback.
        if (!Finite(maximum)) return Blocked("native_independent_recharge_arithmetic_unproved");
        var ids = new HashSet<int>();
        foreach (var item in f.ItemCatalog)
        {
            if (item is null || item.ItemId < 1 || !ids.Add(item.ItemId))
                return Blocked("native_independent_item_catalog_unproved");
            if (!TryAccumulate(item, generation, f.IncTableMilli!, f.AccTableMilli!, ref maximum))
                return Blocked("native_independent_fuel_factor_unproved");
        }
        // Select may legitimately return an entry outside dataArray or null.
        // Neither possibility is silently omitted from the Native getter set.
        if (f.CurrentSelection is { } current
            && !TryAccumulate(current, generation, f.IncTableMilli!, f.AccTableMilli!, ref maximum))
            return Blocked("native_independent_fuel_factor_unproved");

        // Candidate arithmetic follows Native's float addition, then at most
        // three binary64 operations. Relative1e-12 plus1W widens their error
        // well beyond gamma4, including tiny/subnormal values. It is not g.
        var power = maximum + Math.Max(1d, maximum * 1e-12d);
        var speed = Math.Max(3d, f.ShieldRechargeSpeed!.Value);
        var shield = power * VoyagerLocalWalkReservePolicy.NativeTickSeconds * speed;
        var laser = power * VoyagerLocalWalkReservePolicy.NativeTickSeconds * 1.8d;
        if (!Finite(power) || !Finite(shield) || !Finite(laser) || shield > MaximumDebit || laser > MaximumDebit)
            return Blocked("native_independent_recharge_arithmetic_unproved");
        // Both debits are paid even if their buffers are currently full or the
        // switches/CD currently suppress them. Native casts positive x+.01 to
        // long; this range and1J cover that rounding without crediting damage.
        shield += 1d; laser += 1d;
        var total = shield + laser;
        if (!Finite(total)) return Blocked("native_independent_recharge_arithmetic_unproved");
        return new NativeMechaRechargeCost { Proved = true, WeaponPowerUpperWatts = power,
            ShieldRechargeEnergyPerNativeTickUpperJoules = shield,
            LaserRechargeEnergyPerNativeTickUpperJoules = laser, PerNativeTickEnergyUpperJoules = total };
    }

    public static string? BackgroundBlocker(NativeMechaBackgroundCostFacts? f)
    {
        if (f is null) return "native_independent_background_unobserved";
        if (!f.EnergyShieldCapacity.HasValue || f.EnergyShieldCapacity < 0 || !f.EnergyShieldRechargeEnabled.HasValue)
            return "native_independent_shield_metadata_unproved";
        if (!f.LaserActive.HasValue) return "native_independent_laser_metadata_unproved";
        if (!f.AmmoItemId.HasValue) return "native_independent_ammo_chamber_unobserved";
        if (f.AmmoItemId != 0) return "native_independent_ammo_chamber_not_empty";
        if (!f.AmmoSelectSlot.HasValue || f.AmmoSelectSlot < 0) return "native_independent_ammo_slot_unproved";
        if (!f.AmmoStorageEmptyAndUnfiltered.HasValue) return "native_independent_ammo_storage_unobserved";
        if (f.AmmoStorageEmptyAndUnfiltered != true) return "native_independent_ammo_storage_not_empty_unfiltered";
        if (!f.AmmoRateOfFire.HasValue || !f.AmmoEnergyCost.HasValue) return "native_independent_ammo_fire_cost_unobserved";
        if (f.AmmoRateOfFire != 0 || f.AmmoEnergyCost != 0) return "native_independent_ammo_fire_cost_not_zero";
        if (!f.GroundFleetCount.HasValue) return "native_independent_ground_fleet_unobserved";
        if (f.GroundFleetCount != 0) return "native_independent_ground_fleet_not_zero";
        if (!f.SpaceFleetCount.HasValue) return "native_independent_space_fleet_unobserved";
        if (f.SpaceFleetCount != 0) return "native_independent_space_fleet_not_zero";
        return null;
    }

    private static bool TryAccumulate(NativeRechargeItemFacts item, double generation,
        IReadOnlyList<double> inc, IReadOnlyList<double> acc, ref double maximum)
    {
        if (float.IsNaN(item.ReactorInc) || float.IsInfinity(item.ReactorInc)) return false;
        var nativeFactor = item.ReactorInc + 1f; // Deliberately float before double.
        if (float.IsNaN(nativeFactor) || float.IsInfinity(nativeFactor) || nativeFactor < 0f) return false;
        for (var i = 0; i <= 10; i++)
        {
            var factor = (double)nativeFactor;
            if (i > 0) factor *= 1d + (item.Productive ? inc[i] : acc[i]);
            var power = generation * factor;
            if (!Finite(factor) || !Finite(power)) return false;
            maximum = Math.Max(maximum, power);
        }
        return true;
    }

    private static bool TableKnown(IReadOnlyList<double>? values)
    {
        if (values is null || values.Count < 11 || values.Count > MaximumTableLength) return false;
        for (var i = 0; i <= 10; i++) if (!Finite(values[i]) || !Finite(1d + values[i])) return false;
        return true;
    }
    private static bool Known(double? x) => x.HasValue && Finite(x.Value);
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0d;
    private static NativeMechaRechargeCost Blocked(string code) => new() { BlockerCode = code };
}
