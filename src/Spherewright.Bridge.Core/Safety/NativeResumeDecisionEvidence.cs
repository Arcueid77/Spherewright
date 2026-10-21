using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// A bounded, detached decision recorder. Callers supply values they already
// sampled for the decision; this class cannot invoke Native or read a clock.
public sealed class NativeResumeDecisionEvidence
{
    // Observer loss/cancellation has no new Native sample. Never borrow a
    // previous successful151/501 snapshot as the failure instant's facts.
    public static NativeResumeFailureSnapshot UnobservedFailure(string code, string stage, long? tick,
        DateTimeOffset? utc, string? predicate = null)
        => new NativeResumeDecisionEvidence(stage, tick, utc, false).Failure(code, predicate);

    private readonly NativeResumeFailureSnapshot _sample;
    private string? _first;
    public NativeResumeDecisionEvidence(string stage, long? tick, DateTimeOffset? utc, bool factsObserved)
    {
        _sample = new() { Stage = stage, CapturedAtGameTick = tick, CapturedAtUtc = utc,
            FactsState = factsObserved ? "observed" : "not_observed" };
    }
    public void Number(string name, double? value)
    {
        Bound(_sample.Values.Count, _sample.Values.ContainsKey(name));
        _sample.Values[name] = value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) ? value : null;
    }
    public void Observe(string name, bool? value)
    {
        Bound(_sample.Observations.Count, _sample.Observations.ContainsKey(name));
        _sample.Observations[name] = value.HasValue ? value.Value ? "true" : "false" : "unknown";
    }
    public void Stage(string stage) => _sample.Stage = stage;
    public void NotReached(params string[] names)
    {
        foreach (var name in names)
        {
            Bound(_sample.Predicates.Count, _sample.Predicates.ContainsKey(name));
            if (!_sample.Predicates.ContainsKey(name)) _sample.Predicates[name] = "not_reached";
        }
    }
    public bool Require(string name, bool? value)
    {
        Bound(_sample.Predicates.Count, _sample.Predicates.ContainsKey(name));
        _sample.Predicates[name] = value.HasValue ? value.Value ? "true" : "false" : "unknown";
        if (value != true) _first ??= name;
        return value == true;
    }
    public NativeResumeFailureSnapshot Failure(string code, string? predicate = null)
    {
        var result = _sample.Copy(); result.Code = code; result.FirstFailedPredicate = _first ?? predicate ?? code;
        return result;
    }
    private static void Bound(int count, bool exists)
    {
        if (!exists && count >= 96) throw new InvalidOperationException("Native resume decision evidence exceeds its finite bound.");
    }
}
