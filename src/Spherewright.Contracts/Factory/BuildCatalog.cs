namespace Spherewright.Contracts.Factory;

public sealed class BuildCatalog
{
    public List<NativeStackingCatalogEntry>? Stacking { get; set; }
    public bool? StackingCatalogComplete { get; set; }
    public List<string>? StackingCatalogBlockers { get; set; }
    public int PlanetId { get; set; }

    public long Revision { get; set; }

    public Vector3Snapshot PlayerPosition { get; set; } = new Vector3Snapshot();

    public float PlayerBuildArea { get; set; }

    public bool SandboxToolsEnabled { get; set; }

    public List<BuildCatalogItem> Buildings { get; set; } = new List<BuildCatalogItem>();

    public List<BuildCatalogRecipe> Recipes { get; set; } = new List<BuildCatalogRecipe>();

    public BasicLineRecommendation? RecommendedBasicLine { get; set; }
}

public sealed class BuildCatalogItem
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int ModelIndex { get; set; }

    public int Grade { get; set; }

    public List<int> SupportedUpgradeTargetItemIds { get; set; } = new List<int>();

    public int BuildMode { get; set; }

    public bool Unlocked { get; set; }

    public bool Available { get; set; }

    public string RecipeType { get; set; } = string.Empty;

    public int SlotCount { get; set; }

    public float RoughRadius { get; set; }

    public float? PlacementRadius { get; set; }

    public float PowerConnectDistance { get; set; }

    public float PowerCoverRadius { get; set; }

    public int? ProductionSpeedRaw { get; set; }

    // Native catalog values, not measured rates. Null is unavailable, never zero-cost capacity.
    public int? BeltSpeedRaw { get; set; }
    public int? InserterSttRaw { get; set; }
    public int? InserterGrade { get; set; }

    public long? WorkEnergyPerTick { get; set; }

    public long? IdleEnergyPerTick { get; set; }
    public bool NativePowerProfileKnown { get; set; }
    public bool IsPowerConsumer { get; set; }
    public bool IsPowerNode { get; set; }
    public bool IsPowerCharger { get; set; }
    // Only native wind-forced generation is predictable from this local planet.
    // Null is unknown, not free generating capacity.
    public long? WindGenerationAtCurrentPlanetPerTick { get; set; }

    // Optional native base ratings for ordinary thermal/fusion generators only.
    // Not current generation, fuel inventory, or evidence of sustainable supply.
    public FuelPowerCatalogProfile? FuelPowerProfile { get; set; }
}

public sealed class FuelPowerCatalogProfile
{
    public long GenerationEnergyPerTick { get; set; }

    public long FuelEnergyUsePerTick { get; set; }

    public int FuelTypeMask { get; set; }
}

public sealed class BuildCatalogRecipe
{
    public int RecipeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string RecipeType { get; set; } = string.Empty;

    public bool Unlocked { get; set; }

    public int TimeSpend { get; set; }

    public List<BuildCatalogIngredient> Inputs { get; set; } = new List<BuildCatalogIngredient>();

    public List<BuildCatalogIngredient> Outputs { get; set; } = new List<BuildCatalogIngredient>();
}

public sealed class BuildCatalogIngredient
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public bool RawMaterial { get; set; }
}

public sealed class BasicLineRecommendation
{
    public int StorageItemId { get; set; }

    public int AssemblerItemId { get; set; }

    public int InserterItemId { get; set; }

    public int PowerGeneratorItemId { get; set; }

    public int RecipeId { get; set; }

    public int InputItemId { get; set; }

    public int OutputItemId { get; set; }
}
