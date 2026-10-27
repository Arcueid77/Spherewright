using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    internal static bool RemoteResumeLeaseHandoffMatches(OwnedWorldResumeTicket ticket,
        OwnedSaveRecoveryLease? source, OwnedSaveRecoveryLease? primary, IDisposable? journal,
        VoyagerRemoteResumeEvidence evidence, string? actionId, OwnedWorldResumeSourceKind sourceKind,
        bool reauthorizingPrimary, bool fixedAutosave)
        => !evidence.IsDisposed && !reauthorizingPrimary && !fixedAutosave
            && (sourceKind == OwnedWorldResumeSourceKind.OwnedPrimary && evidence.RemotePrimaryRecovery
                || sourceKind == OwnedWorldResumeSourceKind.LastExit && (!evidence.RemotePrimaryRecovery
                    || source is not null && primary is not null
                    && (source.Prefix.GameTick == ticket.MinimumGameTick
                        || source.Prefix.GameTick<ticket.MinimumGameTick && evidence.AcceptedProgressLossGameTicks>0
                            && evidence.KnownPrimaryProgressTick>=ticket.MinimumGameTick
                            && evidence.KnownPrimaryProgressTick-source.Prefix.GameTick==evidence.AcceptedProgressLossGameTicks)
                    && source.Prefix.GameTick > primary.Prefix.GameTick
                    && (evidence.KnownPrimaryProgressTick <= source.Prefix.GameTick
                        || evidence.AcceptedProgressLossGameTicks>0
                            && evidence.KnownPrimaryProgressTick-source.Prefix.GameTick==evidence.AcceptedProgressLossGameTicks)))
            && Guid.TryParse(actionId, out _) && source is not null && primary is not null && journal is not null
            && ticket.GameplayJournalCheckpoint is not null
            && (evidence.RemotePrimaryRecovery || !string.IsNullOrEmpty(ticket.QuarantineActionId))
            && source.Fingerprint == evidence.SourceFingerprint && primary.Fingerprint == evidence.PrimaryFingerprint;
}
