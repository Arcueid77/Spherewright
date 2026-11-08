namespace Spherewright.Contracts.Factory;

public sealed class BlueprintSiteRequest
{
    public Vector3Snapshot Position { get; set; } = new Vector3Snapshot();
    public int QuarterTurns { get; set; }
    public string ExpectedPlayerStateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
}

// A copied, bounded site assessment. Even a clear assessment is not a construction token.
public sealed class BlueprintSiteSnapshot
{
    public string Phase { get; set; } = "blueprint_site_preview";
    public bool Executable { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int PlanetId { get; set; }
    public long Revision { get; set; }
    public long CapturedAtGameTick { get; set; }
    public string BlueprintHash { get; set; } = string.Empty;
    public string AssessmentHash { get; set; } = string.Empty;
    public Vector3Snapshot Position { get; set; } = new Vector3Snapshot();
    public int QuarterTurns { get; set; }
    public bool NativeCheckPerformed { get; set; }
    public bool NativeCheckPassed { get; set; }
    public bool InventorySufficient { get; set; }
    public bool TechnologySatisfied { get; set; }
    public int NativeBlueprintObjectLimit { get; set; }
    public List<BlueprintSiteObject> Objects { get; set; } = new List<BlueprintSiteObject>();
    public List<FoundryInventoryBudget> ConstructionItems { get; set; } = new List<FoundryInventoryBudget>();
    public List<BlueprintPlanConnection> Connections { get; set; } = new List<BlueprintPlanConnection>();
    public List<string> Blockers { get; set; } = new List<string>();
    public FoundryPowerAssessment? Power { get; set; }
    public List<string> Limitations { get; set; } = new List<string>
    {
        "Only new objects: no covering, upgrading, dismantling or reconfiguring existing objects.",
        "Inserters must have both endpoints inside the selected module; no implicit external matching. Free belt ends stay free.",
        "Normal blueprint technology, whole item budget, native terrain/tropic/collision checks and fresh prepare/commit remain required.",
        "Construction feasibility is not proof of power, ongoing external input, output capacity or sustained production.",
    };
}

public sealed class BlueprintSiteObject
{
    public int? NativeModelIndex { get; set; }
    public bool ShouldSerializeNativeModelIndex() => NativeModelIndex.HasValue;
    public int Index { get; set; }
    public int ItemId { get; set; }
    public int RecipeId { get; set; }
    public int FilterItemId { get; set; }
    public Vector3Snapshot Position { get; set; } = new Vector3Snapshot();
    public Vector3Snapshot Position2 { get; set; } = new Vector3Snapshot();
    public QuaternionSnapshot Rotation { get; set; } = new QuaternionSnapshot();
    public QuaternionSnapshot Rotation2 { get; set; } = new QuaternionSnapshot();
    public float Tilt { get; set; }
    public int InputOffset { get; set; }
    public int OutputOffset { get; set; }
    public int[] Parameters { get; set; } = Array.Empty<int>();
    public string NativeCondition { get; set; } = "not_checked";
    // Diagnostics from translated native device slots, not overrides of native conditions.
    // Negative means the connected device faces away from this sorter. Null for belt/open ends.
    public float? InputEndpointFacingDot { get; set; }
    public float? OutputEndpointFacingDot { get; set; }
    public int? OccupiedObjectId { get; set; }
    public List<int> Dependencies { get; set; } = new List<int>();
}

// Belt-side sorter slots are virtual (-1) in native blueprints. Their budget is bounded
// separately; actual slots must be proved from both objects after native construction.
public sealed class BlueprintPlanConnection
{
    public int FromIndex { get; set; }
    public int FromSlot { get; set; }
    public int ToIndex { get; set; }
    public int ToSlot { get; set; }
}
