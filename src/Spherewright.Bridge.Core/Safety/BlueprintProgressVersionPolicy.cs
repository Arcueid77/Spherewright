using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Journals;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Retain completed receipts across only the researched 29131→29145 adoption; never resume unproven work.</summary>
public static class BlueprintProgressVersionPolicy
{
    public static bool CanRetainCompletedHistory(string storedVersion, string runtimeVersion,
        string sessionId, string journalId, GameplayJournalSnapshot? journal,
        IReadOnlyList<BlueprintBuildState>? builds)
    {
        if (storedVersion != OwnedWorldVersionCompatibilityPolicy.LatestPatchVersion
            || runtimeVersion != OwnedWorldVersionCompatibilityPolicy.NextPatchVersion
            || string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(journalId)
            || journal is null || journal.SessionId != sessionId || journal.JournalId != journalId
            || journal.CurrentGameVersion != runtimeVersion || journal.PersistencePending
            || !string.IsNullOrWhiteSpace(journal.PersistenceError) || journal.CapturedAtGameTick < 0
            || journal.Entries is null || journal.Entries.Any(entry => entry is null)
            || journal.DurableThroughSequence != journal.Entries.Count
            || !GameplayJournalContinuityPolicy.HasContinuousSequence(journal.Entries.Select(entry => entry.Sequence).ToArray())
            || !OwnedWorldVersionCompatibilityPolicy.JournalMatches(journal.OriginGameVersion,
                journal.VersionTransitions, journal.Entries.Count, runtimeVersion)
            || journal.VersionTransitions.Count == 0 || builds is null
            || builds.Any(build => build is null || build.Phase != "completed"
                || build.LastEvidenceGameTick > journal.CapturedAtGameTick
                || build.Objects is null || build.Objects.Any(item => item is null || item.State != BlueprintObjectStates.Completed)))
            return false;
        var latest = journal.VersionTransitions[journal.VersionTransitions.Count - 1];
        return latest.FromGameVersion == storedVersion && latest.ToGameVersion == runtimeVersion
            && latest.AdoptedAtGameTick <= journal.CapturedAtGameTick;
    }
}
