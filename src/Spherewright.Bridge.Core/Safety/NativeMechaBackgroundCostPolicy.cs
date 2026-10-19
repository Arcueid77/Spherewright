namespace Spherewright.Bridge.Core.Safety;

// Copied Native fields only. A local movement/construction certificate does
// not include combat, shield charging, armed turrets or configured fleets.
// Native fleetCount counts configured slots, not active/working craft. This
// deliberately limited first subset requires zero configured slots; it never
// rewrites a player's military configuration to make the premise true.
public sealed class NativeMechaBackgroundCostFacts
{
    public long? EnergyShieldCapacity { get; set; }
    public bool? EnergyShieldRechargeEnabled { get; set; }
    public bool? LaserActive { get; set; }
    public int? AmmoItemId { get; set; }
    public int? AmmoSelectSlot { get; set; }
    public int? AmmoRateOfFire { get; set; }
    public int? AmmoEnergyCost { get; set; }
    public bool? AmmoStorageEmptyAndUnfiltered { get; set; }
    public int? GroundFleetCount { get; set; }
    public int? SpaceFleetCount { get; set; }
}

public static class NativeMechaBackgroundCostPolicy
{
    public static bool IndependentCombatCoreDebitsExcluded(NativeMechaBackgroundCostFacts? f) =>
        f is not null && f.EnergyShieldCapacity.HasValue && f.EnergyShieldCapacity >= 0
        && f.EnergyShieldRechargeEnabled.HasValue
        && (f.EnergyShieldCapacity == 0 || f.EnergyShieldRechargeEnabled == false)
        && f.LaserActive == false
        // InitFreeMode selects empty slot1. Empty, unfiltered Native storage
        // cannot auto-load on a package-add event; ROF0 returns before firing
        // callbacks/LoadAmmo. Slot0 alone would not clear an old chamber.
        && f.AmmoItemId == 0 && f.AmmoSelectSlot.HasValue && f.AmmoSelectSlot >= 0
        && f.AmmoStorageEmptyAndUnfiltered == true && f.AmmoRateOfFire == 0 && f.AmmoEnergyCost == 0
        && f.GroundFleetCount == 0 && f.SpaceFleetCount == 0;
}
