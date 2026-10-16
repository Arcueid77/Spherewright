using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Journals;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Retain completed receipts across proven 29131→29145→29151 journal links; never resume unproven work.</summary>
public static class BlueprintProgressVersionPolicy
{
    public static bool CanRetainCompletedHistory(string storedVersion, string runtimeVersion,
        string sessionId, string journalId, GameplayJournalSnapshot? journal,
        IReadOnlyList<BlueprintBuildState>? builds)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(journalId)
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
        // A read does not stamp the document's version. Completed receipts can therefore
        // still carry 29131 after a previous read-only 29145 adoption. Require the exact
        // durable suffix rather than skipping a link or rewriting the original document.
        var first = journal.VersionTransitions.FindIndex(change => change.FromGameVersion == storedVersion);
        if (first < 0 || builds.Any(build => build.LastEvidenceGameTick < 0
            || build.LastEvidenceGameTick > journal.VersionTransitions[first].AdoptedAtGameTick)) return false;
        var current = storedVersion;
        for (var index = first; index < journal.VersionTransitions.Count; index++)
        {
            var change = journal.VersionTransitions[index];
            if (change.FromGameVersion != current || !IsCompletedHistoryHop(current, change.ToGameVersion)
                || change.AdoptedAtGameTick > journal.CapturedAtGameTick) return false;
            current = change.ToGameVersion;
        }
        return current == runtimeVersion;
    }

    private static bool IsCompletedHistoryHop(string source, string target) =>
        (source == OwnedWorldVersionCompatibilityPolicy.LatestPatchVersion
            && target == OwnedWorldVersionCompatibilityPolicy.NextPatchVersion)
        || (source == OwnedWorldVersionCompatibilityPolicy.NextPatchVersion
            && target == OwnedWorldVersionCompatibilityPolicy.CurrentNativeVersion);
}
