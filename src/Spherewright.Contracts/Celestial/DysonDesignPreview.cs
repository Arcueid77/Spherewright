namespace Spherewright.Contracts.Celestial;

public sealed class DysonDesignPreviewRequest
{
    public string ExpectedDesignStateHash { get; set; } = string.Empty;
    public List<DysonDesignStep> Steps { get; set; } = new();

    public DysonDesignPreviewRequest Copy() => new()
    {
        ExpectedDesignStateHash = ExpectedDesignStateHash,
        Steps = Steps.Select(step => new DysonDesignStep { LocalId = step.LocalId, Structure = step.Structure.Copy() }).ToList(),
    };
}

public sealed class DysonDesignStep
{
    // Negative request-local ID. Later steps may reference an earlier new node
    // by this ID. Positive references always select existing Native nodes.
    public int LocalId { get; set; }
    public DysonStructureRequest Structure { get; set; } = new();
}

public sealed class DysonDesignPreview
{
    public int EvidenceVersion { get; set; } = 1;
    public string BeforeDesignStateHash { get; set; } = string.Empty;
    public string CandidateHash { get; set; } = string.Empty;
    public bool GeometryComplete { get; set; }
    public bool TechnologyAllowsNow { get; set; }
    public int RequiredNodeLatitudeDegrees { get; set; }
    public int? RequiredCarrierRockets { get; set; }
    public int? RequiredSolarSails { get; set; }
    public List<DysonDesignStepPreview> Steps { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public bool ReadOnly => true;
    public bool Executable => false;
    public bool CommitAllowedNow => false;
}

public sealed class DysonDesignStepPreview
{
    public int LocalId { get; set; }
    public DysonStructurePlan Plan { get; set; } = new();
}
