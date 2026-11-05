using Spherewright.Contracts.Sessions;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

// Real tracker and linked production-entry tests share held complete file
// leases; none of these checks claims that Native loading has occurred.
internal static class SavedWorldOpenLeaseHandoff
{
    internal static string? Validate(OwnedWorldResumeTicket ticket,OwnedSaveRecoveryLease? source,
        IDisposable? journal,OwnedSaveRecoveryLease? other,OwnedWorldResumeSourceKind kind,
        bool reauthorizing,string? action,long? knownProgress,string nativeVersion,bool exactLastExit)
    {
        if(source is null || journal is null || !reauthorizing || !Guid.TryParse(action,out _)
            || !knownProgress.HasValue || knownProgress<ticket.MinimumGameTick
            || ticket.GameplayJournalCheckpoint is null || !source.Prefix.MatchesExpectedIdentity
            || !source.Prefix.Peaceful || source.Prefix.GameVersion!=nativeVersion || ticket.GameVersion!=nativeVersion
            || other is not null && !other.Prefix.MatchesExpectedIdentity)
            return "saved_world_open_full_lease_handoff_unproved";
        if(!exactLastExit)return kind==OwnedWorldResumeSourceKind.OwnedPrimary
            ? null : "saved_world_open_primary_role_mismatch";
        if(kind!=OwnedWorldResumeSourceKind.LastExit || other is null || !other.Prefix.Peaceful
            || other.Prefix.GameVersion!=nativeVersion || source.Prefix.GameTick<knownProgress.Value
            || source.Prefix.GameTick<other.Prefix.GameTick)
            return "saved_world_open_exact_lastexit_role_or_coverage_unproved";
        return null;
    }
}
