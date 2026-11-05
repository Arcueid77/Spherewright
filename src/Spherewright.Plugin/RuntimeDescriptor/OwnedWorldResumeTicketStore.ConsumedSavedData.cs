using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class OwnedWorldResumeTicketStore
{
    // New data-only identity generation. A consumed execution token is never
    // revived and no inference about its original terminal outcome is made.
    internal bool TryGetConsumedCurrentSavedData(string token,long exactTick,out OwnedWorldResumeTicket? ticket,
        out string fingerprint,out string rejection)
    {
        ticket=null;fingerprint=string.Empty;rejection="consumed_saved_data_identity_unproved";
        try
        {
            if(string.IsNullOrWhiteSpace(token) || token.Length>256 || exactTick<0)return false;
            var hash=HashToken(token);var handoff=Path.GetDirectoryName(_handoffTicketPath)!;
            var attempt=ReadExactFileAccessPair<OwnedWorldReauthorizationAttempt>(
                GetReauthorizationAttemptPath(_runtimeDirectory,hash),GetReauthorizationAttemptPath(handoff,hash));
            var consumed=ReadExactFileAccessPair<OwnedWorldResumeConsumptionTombstone>(
                GetTombstonePath(_runtimeDirectory,hash),GetTombstonePath(handoff,hash));
            if(attempt?.Ticket is not {} previous || consumed is null || attempt.Version!=1 || consumed.Version!=1
                || attempt.State!="load_may_have_started_reconciliation_required" || consumed.ResumeTokenHash!=hash
                || !FixedTimeEquals(previous.ResumeToken,token) || previous.Version!=TicketVersion
                || previous.GameVersion!=_gameVersion || consumed.GameVersion!=_gameVersion
                || !Guid.TryParse(attempt.ActionId,out _) || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(attempt.ConfirmationDigest)
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(attempt.ProvenanceFingerprint)
                || attempt.RecordedAtUtc==default || consumed.ConsumedAtUtc<attempt.RecordedAtUtc
                || consumed.ConsumedAtUtc>DateTimeOffset.UtcNow || previous.MinimumGameTick<0
                || previous.ObservedProgressGameTickFloor.HasValue && (previous.SavedWorldOpenOnly!=true
                    || previous.ObservedProgressGameTickFloor<previous.MinimumGameTick)
                || previous.ExpectedPlanetId<=0 || previous.SourceProcessId<=0 || string.IsNullOrEmpty(previous.SourceSessionId)
                || string.IsNullOrEmpty(previous.SourceBridgeInstanceId) || previous.GameplayJournalCheckpoint is null
                || string.IsNullOrWhiteSpace(previous.OwnedSaveName) || previous.OwnedSaveName is "." or ".."
                || Path.GetFileName(previous.OwnedSaveName)!=previous.OwnedSaveName
                || previous.OwnedSaveName.IndexOfAny(Path.GetInvalidFileNameChars())>=0
                || !TryValidateGameplayJournalContinuity(previous,true,out var journalHash,out rejection))return false;
            if(previous.SourceProcessId!=Process.GetCurrentProcess().Id)
            {
                try {using var p=Process.GetProcessById(previous.SourceProcessId);
                    if(!p.HasExited && p.ProcessName.Equals("DSPGAME",StringComparison.OrdinalIgnoreCase))return false;}
                catch(ArgumentException) { }
            }
            using var source=OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit),previous.OwnedSaveName);
            using var primary=OwnedSaveRecoveryLease.Open(GameSave.SavePath(previous.OwnedSaveName),previous.OwnedSaveName);
            var floor=Math.Max(previous.MinimumGameTick,previous.ObservedProgressGameTickFloor??previous.MinimumGameTick);
            if(!source.Prefix.MatchesExpectedIdentity || !primary.Prefix.MatchesExpectedIdentity
                || !source.Prefix.Peaceful || !primary.Prefix.Peaceful || source.Prefix.GameVersion!=_gameVersion
                || primary.Prefix.GameVersion!=_gameVersion || source.Prefix.GameTick!=exactTick
                || exactTick<floor || primary.Prefix.GameTick>exactTick)return false;
            var name="owned-world-resume-saved-data-"+hash+".json";
            var paths=new[]{Path.Combine(_runtimeDirectory,name),Path.Combine(handoff,name)};
            var derived=ReadExactFileAccessPair<SavedFileAccessDerivation>(paths[0],paths[1]);
            if(derived is not null)
            {
                var current=ReadExactFileAccessPair<OwnedWorldResumeTicket>(_ticketPath,_handoffTicketPath);
                if(derived.ParentConsumedTokenHash!=hash || derived.SourceFingerprint!=source.Fingerprint
                    || derived.PrimaryFingerprint!=primary.Fingerprint || derived.JournalHash!=journalHash
                    || current?.SavedWorldOpenOnly!=true || current.OwnedSaveName!=previous.OwnedSaveName
                    || current.MinimumGameTick!=exactTick || HashToken(current.ResumeToken)!=derived.FileAccessTokenHash)return false;
                return TryGetSavedWorldOpenProvenance(current.ResumeToken,out ticket,out fingerprint,out rejection);
            }
            if(paths.Any(File.Exists) || File.Exists(_ticketPath) || File.Exists(_handoffTicketPath))return false;
            var now=DateTimeOffset.UtcNow;
            var fresh=PluginJson.Deserialize<OwnedWorldResumeTicket>(PluginJson.Serialize(previous))!;
            fresh.ResumeToken=CreateToken();fresh.SavedWorldOpenOnly=true;fresh.MinimumGameTick=exactTick;
            fresh.ObservedProgressGameTickFloor=Math.Max(exactTick,floor);fresh.IssuedAtUtc=now;fresh.ExpiresAtUtc=now.AddHours(24);
            derived=new SavedFileAccessDerivation {ParentConsumedTokenHash=hash,FileAccessTokenHash=HashToken(fresh.ResumeToken),
                SourceFingerprint=source.Fingerprint,PrimaryFingerprint=primary.Fingerprint,JournalHash=journalHash,RecordedAtUtc=now};
            if(!TryPersistAtPath(paths[0],_runtimeDirectory,derived,"saved data identity derivation")
                || !TryPersistAtPath(paths[1],handoff,derived,"handoff saved data identity derivation"))return false;
            Persist(fresh);_currentTicket=fresh;_currentTicketPath=null;
            return TryGetSavedWorldOpenProvenance(fresh.ResumeToken,out ticket,out fingerprint,out rejection);
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidDataException
            or ArgumentException or Newtonsoft.Json.JsonException)
        {rejection="consumed_current_saved_data_unavailable:"+e.GetType().Name;return false;}
    }
}
