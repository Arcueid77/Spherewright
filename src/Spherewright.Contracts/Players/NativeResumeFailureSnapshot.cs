namespace Spherewright.Contracts.Players;

/// <summary>First failed decision's copied facts. Never a new Native query or safety certificate.</summary>
public sealed class NativeResumeFailureSnapshot
{
    public string Code { get; set; } = string.Empty;
    public string FirstFailedPredicate { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public long? CapturedAtGameTick { get; set; }
    public DateTimeOffset? CapturedAtUtc { get; set; }
    public string FactsState { get; set; } = "not_observed";
    /// <summary>true, false, unknown, or not_reached; missing observations never mean false.</summary>
    public Dictionary<string, string> Predicates { get; set; } = new();
    public Dictionary<string, string> Observations { get; set; } = new();
    /// <summary>Same-decision raw numbers; null is unknown, including a nonfinite source.</summary>
    public Dictionary<string, double?> Values { get; set; } = new();
    public bool SafetyCertified => false;
    public NativeResumeFailureSnapshot Copy() => new()
    {
        Code = Code, FirstFailedPredicate = FirstFailedPredicate, Stage = Stage,
        CapturedAtGameTick = CapturedAtGameTick, CapturedAtUtc = CapturedAtUtc, FactsState = FactsState,
        Predicates = new(Predicates), Observations = new(Observations), Values = new(Values),
    };
}
