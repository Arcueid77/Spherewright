using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class OwnedWorldResumeCoordinator
{
    private Spherewright.Contracts.Players.NativeResumeFailureSnapshot? _failedLandingSourceFailure;
    private bool TryResolveFailedLandingPayload(OwnedWorldResumeTicket ticket, PrepareOwnedWorldResumeRequest request,
        string provenance, out OwnedWorldResumePlanPayload? payload, out OwnedSaveRecoveryLease? lease,
        out OwnedSaveRecoveryLease? primary, out string rejection)
    {
        payload = null; lease = primary = null; rejection = "failed_landing_source_unproved";
        _failedLandingSourceFailure = null;
        if (ticket.SourceProcessId != Process.GetCurrentProcess().Id && IsLiveDspProcess(ticket.SourceProcessId))
        { rejection = "failed_landing_source_process_still_running"; return false; }
        if (!_sessions.TryOpenFailedLandingSource(_tickets, ticket.ResumeToken, request, out var source, out var failure))
        { rejection = failure?.Code ?? rejection; _failedLandingSourceFailure = failure?.Copy(); return false; }
        if (source!.CredentialProvenance != provenance)
        { source.Dispose(); rejection = "failed_landing_credential_provenance_changed"; return false; }
        var native = VoyagerRemoteResumeEvidence.CreateFailedLandingRecovery(source);
        lease = source.Source; primary = source.Primary;
        payload = new(ticket, OwnedWorldResumeSourceKind.LastExit, request, lease, provenance, _sessions.Revision,
            primaryFingerprint: source.Primary.Fingerprint, remoteEvidence: native, remoteJournalLease: source.Journal);
        return true;
    }
}
