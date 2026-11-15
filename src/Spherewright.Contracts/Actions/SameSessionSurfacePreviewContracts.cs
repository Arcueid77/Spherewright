using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Actions;

public sealed class SameSessionSurfacePreviewRequest
{
    public int PlanetId { get; set; }
    public string OriginalActionId { get; set; } = string.Empty;
    public long ExpectedRevision { get; set; }
    public string ExpectedPlayerStateHash { get; set; } = string.Empty;
    public Vector3Snapshot? Target { get; set; }
    public int StateHashVersion { get; set; } = 1;
}

// A detached read-only observation. It cannot be committed or used as a
// promise that the future Native controller will reach the target.
public sealed class SameSessionSurfacePreviewSnapshot
{
    public int EvidenceVersion { get; set; }
    public string State { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public int PlanetId { get; set; }
    public long Revision { get; set; }
    public string? PlayerStateHash { get; set; }
    public string OriginalActionId { get; set; } = string.Empty;
    public long SourceGameTick { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Vector3Snapshot? Target { get; set; }
    public string? SourceFingerprint { get; set; }
    public int MaximumWorkGameTicks { get; set; }
    public double MaximumWallSeconds { get; set; }
    public bool? ReadOnly { get; set; }
    public bool? Executable { get; set; }
    public bool? CommitAllowedNow { get; set; }
    public bool? NativeResumePerformed { get; set; }
    public bool? OriginalOutcomeChanged { get; set; }
}
