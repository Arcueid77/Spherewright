using Spherewright.Contracts.Journals;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Researched, directional native migration; never a wildcard version bypass.</summary>
public static class OwnedWorldVersionCompatibilityPolicy
{
    public const int MaximumDurableTransitions = 5;
    public const string SourceVersion = "0.10.34.28529";
    public const string TargetVersion = "0.10.35.29057";
    public const string TargetPatchVersion = "0.10.35.29088";
    public const string CurrentPatchVersion = "0.10.35.29104";
    public const string LatestPatchVersion = "0.10.35.29131";
    public const string NextPatchVersion = "0.10.35.29145";
    public const string CurrentNativeVersion = "0.10.35.29151";

    public static bool IsResearchedTarget(string version) =>
        version == TargetVersion || version == TargetPatchVersion || version == CurrentPatchVersion
        || version == LatestPatchVersion || version == NextPatchVersion || version == CurrentNativeVersion;

    public static bool IsSupportedMigration(string source, string target) =>
        (source == SourceVersion && (target == TargetVersion || target == TargetPatchVersion))
        || (source == TargetPatchVersion && target == CurrentPatchVersion)
        || (source == CurrentPatchVersion && target == LatestPatchVersion)
        || (source == LatestPatchVersion && target == NextPatchVersion)
        || (source == NextPatchVersion && target == CurrentNativeVersion);

    public static bool AllowsReauthorization(string source, string target) =>
        !string.IsNullOrWhiteSpace(source) && (source == target || IsSupportedMigration(source, target));

    // GameVersion remains the journal's origin. Only an explicit durable transition
    // advances its effective version; an old document alone cannot authorize new runtime use.
    public static bool JournalMatches(string origin, IReadOnlyList<GameplayJournalVersionTransition>? transitions,
        long entryCount, string expected)
    {
        if (string.IsNullOrWhiteSpace(origin) || transitions is null || entryCount < 0) return false;
        if (transitions.Count > MaximumDurableTransitions) return false;
        var current = origin;
        long previousTick = -1;
        long previousSequence = -1;
        foreach (var change in transitions)
        {
            if (change is null || change.FromGameVersion != current
                || !IsSupportedMigration(change.FromGameVersion, change.ToGameVersion)
                || change.AdoptedAtGameTick <= previousTick
                || change.DurableThroughSequence < previousSequence
                || change.DurableThroughSequence < 0 || change.DurableThroughSequence > entryCount
                || !DateTimeOffset.TryParse(change.RecordedAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out _)) return false;
            current = change.ToGameVersion;
            previousTick = change.AdoptedAtGameTick;
            previousSequence = change.DurableThroughSequence;
        }
        return current == expected;
    }

    public static bool CanAppendTransition(string origin,
        IReadOnlyList<GameplayJournalVersionTransition>? transitions, long entryCount,
        string source, string target, GameplayJournalVersionTransition? candidate)
    {
        if (candidate is null || transitions is null || transitions.Count >= MaximumDurableTransitions
            || !IsSupportedMigration(source, target)
            || !JournalMatches(origin, transitions, entryCount, source)) return false;
        var proposed = transitions.ToList();
        proposed.Add(candidate);
        return JournalMatches(origin, proposed, entryCount, target);
    }
}
