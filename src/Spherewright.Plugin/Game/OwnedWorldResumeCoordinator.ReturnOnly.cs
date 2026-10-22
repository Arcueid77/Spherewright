using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class OwnedWorldResumeCoordinator
{
    private bool TryResolveReturnOnlyPayload(OwnedWorldResumeTicket ticket, PrepareOwnedWorldResumeRequest request,
        string provenance, out OwnedWorldResumePlanPayload? payload, out OwnedSaveRecoveryLease? lease,
        out OwnedSaveRecoveryLease? primary, out string rejection)
    {
        payload = null; lease = primary = null; rejection = "return_only_continuation_source_unproved";
        _returnOnlyContinuationFailure = null;
        if (ticket.SourceProcessId != Process.GetCurrentProcess().Id && IsLiveDspProcess(ticket.SourceProcessId))
        { rejection = "return_only_source_process_still_running"; return false; }
        if (!_sessions.TryOpenReturnOnlyContinuationSource(_tickets, ticket.ResumeToken, request.RecoveryDeadlineUtc,
            out var source, out var failure))
        { rejection = failure?.Code ?? rejection; _returnOnlyContinuationFailure = failure?.Copy(); return false; }
        if (source!.CredentialProvenance != provenance || request.ExpectedRecoveryGameTick != source.Source.Prefix.GameTick)
        {
            var e = new NativeResumeDecisionEvidence("return_only_continuation_prepare", null, DateTimeOffset.UtcNow, true);
            e.Number("disclosed_source_tick", source.Source.Prefix.GameTick); e.Number("expected_source_tick", request.ExpectedRecoveryGameTick);
            e.Require("exact_expected_source", false); _returnOnlyContinuationFailure = e.Failure(rejection);
            source.Dispose(); return false;
        }
        var native = VoyagerRemoteResumeEvidence.CreateReturnOnlyContinuation(source);
        lease = source.Source; primary = source.Primary;
        payload = new(ticket, OwnedWorldResumeSourceKind.LastExit, request, lease, provenance, _sessions.Revision,
            primaryFingerprint: source.Primary.Fingerprint, remoteEvidence: native, remoteJournalLease: source.Journal);
        return true;
    }

    private Spherewright.Contracts.Players.NativeResumeFailureSnapshot? _returnOnlyContinuationFailure;
}
