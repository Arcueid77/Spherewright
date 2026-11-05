using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // CompletionTopologyHash includes the current collider pool handle. Native
    // does not serialize that handle; a cold load creates a different pool.
    // Never rewrite the original hash or use it as cross-load identity. Only a
    // full exact paid-site proof may bind a replacement in this Native cohort.
    private Dictionary<string, SavedBeltSourceTopology> _savedBeltSourceTopologies = new();

    private bool BindSavedEmptyBeltSourceTopologies(Player player, PlanetFactory factory, string? journeyId,
        IReadOnlyList<BlueprintBuildState> saved, NativeResumeDecisionEvidence? evidence)
    {
        if (string.IsNullOrEmpty(_sessions.SessionId)
            || !SavedFactorySiteMatches(player, factory, journeyId, saved, evidence)) return false;
        var current = new Dictionary<string, SavedBeltSourceTopology>();
        foreach (var build in saved.Where(b => b.Site.PlanetId == factory.planetId
                     && b.Objects.Any(o => o.SubmittedAtGameTick.HasValue)
                     && b.StructuredConstruction?.EmptyBeltSource is not null))
        {
            var source = build.StructuredConstruction!.EmptyBeltSource!;
            if (!TryBeltSourceTopology(factory, source.ObjectId, out var topology, out _, nativeCompletionRotation: true)
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(topology)) return false;
            var rebound = new SavedBeltSourceTopology(GameMain.data!, factory, _sessions.SessionId!,
                build.StructuredConstruction.ImmutableHash(), source.CompletionTopologyHash, topology);
            if (_savedBeltSourceTopologies.TryGetValue(build.BuildId, out var previous)
                && !previous.Matches(GameMain.data, factory, _sessions.SessionId, build, topology)) return false;
            current.Add(build.BuildId, rebound);
        }
        _savedBeltSourceTopologies = current;
        return true;
    }

    private bool SavedEmptyBeltSourceTopologyMatches(PlanetFactory factory, BlueprintBuildState build, string topology)
    {
        var source = build.StructuredConstruction?.EmptyBeltSource;
        return source is not null && (BeltSourceReusePolicy.SameEvidence(source.CompletionTopologyHash, topology)
            || _sessions.IsCurrentSessionOwned && ReferenceEquals(factory, GameMain.localPlanet?.factory)
                && _savedBeltSourceTopologies.TryGetValue(build.BuildId, out var current)
                && current.Matches(GameMain.data, factory, _sessions.SessionId, build, topology));
    }

    private sealed class SavedBeltSourceTopology
    {
        private readonly object _world;
        private readonly PlanetFactory _factory;
        private readonly string _session, _binding, _original, _current;
        internal SavedBeltSourceTopology(object world, PlanetFactory factory, string session,
            string binding, string original, string current)
        { _world=world; _factory=factory; _session=session; _binding=binding; _original=original; _current=current; }
        internal bool Matches(object? world, PlanetFactory factory, string? session, BlueprintBuildState build, string current)
            => ReferenceEquals(world,_world) && ReferenceEquals(factory,_factory) && session==_session
                && build.StructuredConstruction is { EmptyBeltSource: { } source } binding
                && _binding==binding.ImmutableHash() && _original==source.CompletionTopologyHash && _current==current;
    }
}
