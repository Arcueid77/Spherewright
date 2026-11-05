using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Security;
using Spherewright.Plugin.Transport;
using System.Text;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    private string SpaceReturnRecordPath(string action,string suffix)
    {
        if(!Guid.TryParse(action,out _))throw new ArgumentException("Exact space-return action required.");
        return Path.Combine(Path.GetDirectoryName(_ticketPath)!,"stopped-space-return-"+action+suffix+".json");
    }
    internal bool TryBindSavedHomeReturnPredecessor(string action,long currentSavedTick,bool knownTerminalFailure,
        out StoppedSpaceReturnRecord? predecessor,out long remaining,out string rejection)
    {
        predecessor=null;remaining=0;rejection="saved_home_return_predecessor_unproved";
        if(!knownTerminalFailure || !Guid.TryParse(action,out _))return false;
        try
        {
            var path=SpaceReturnRecordPath(action,"-start");
            if(!File.Exists(path) || new FileInfo(path).Length>131072 || File.Exists(SpaceReturnRecordPath(action,"-completed")))return false;
            var record=PluginJson.Deserialize<StoppedSpaceReturnRecord>(File.ReadAllText(path));var j=VoyagerJourney;
            if(record is null || j is null || j.Phase!="recovery_required" || record.ActionId!=action
                || record.OriginalLedger!=PluginJson.Serialize(j) || record.SourceFailedActionId!=j.LastAccountedFlightActionId
                || record.SourceTick<j.StartedAtGameTick || currentSavedTick<record.SourceTick
                || record.PriorSpent!=Math.Max(j.SpentGameTicks,record.SourceTick-j.StartedAtGameTick)
                || record.MaximumTicks<3600 || record.MaximumTicks>j.ReturnReserveGameTicks
                || record.MaximumTicks>j.MaximumGameTicks-record.PriorSpent || string.IsNullOrEmpty(record.SessionId))return false;
            // Original action stays failed/consumed. This is a new saved-source
            // safety continuation, conserving every elapsed tick since that entry.
            var elapsed=currentSavedTick-record.SourceTick;
            if(elapsed>=record.MaximumTicks)return false;
            remaining=record.MaximumTicks-elapsed;
            if(remaining<3600)return false;
            predecessor=record;rejection=string.Empty;return true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or Newtonsoft.Json.JsonException)
        {rejection="saved_home_return_predecessor_unavailable:"+e.GetType().Name;return false;}
    }
    internal void RecordStoppedSpaceReturnStart(StoppedSpaceReturnRecord record)
    {
        var j=VoyagerJourney;
        if(j is null || j.Phase!="recovery_required" || PluginJson.Serialize(j)!=record.OriginalLedger
            || record.SourceFailedActionId!=j.LastAccountedFlightActionId || !Guid.TryParse(record.ActionId,out _)
            || string.IsNullOrEmpty(record.SessionId) || record.SourceTick<j.LastAccountedGameTick
            || record.SourceTick<j.StartedAtGameTick
            || record.PriorSpent!=Math.Max(j.SpentGameTicks,record.SourceTick-j.StartedAtGameTick)
            || record.MaximumTicks<3600 || record.MaximumTicks>j.ReturnReserveGameTicks
            || record.MaximumTicks>j.MaximumGameTicks-record.PriorSpent || record.DeadlineUtc<=DateTimeOffset.UtcNow
            || record.DeadlineUtc>DateTimeOffset.UtcNow.AddHours(6))
            throw new InvalidOperationException("The original stopped-space reserve changed before task admission.");
        if(record.PredecessorActionId is {} parent)
        {
            if(!TryBindSavedHomeReturnPredecessor(parent,record.SourceTick,record.PredecessorKnownTerminalFailed,
                out var previous,out var remaining,out _) || record.MaximumTicks>remaining
                || record.PredecessorFingerprint!=CanonicalStateHash.Combine(PluginJson.Serialize(previous)))
                throw new InvalidOperationException("Saved home-return continuation changed its original remaining scope.");
        }
        WindowsCurrentUserSecurity.WriteSecureNewFile(SpaceReturnRecordPath(record.ActionId,"-start"),
            new UTF8Encoding(false).GetBytes(PluginJson.Serialize(record)));
    }
    internal bool CompleteStoppedSpaceHomeReturn(StoppedSpaceReturnRecord record,long landedTick,string primaryPath,out string rejection)
    {
        rejection="stopped_space_home_save_or_original_ledger_unproved";
        try
        {
            var stored=PluginJson.Deserialize<StoppedSpaceReturnRecord>(File.ReadAllText(SpaceReturnRecordPath(record.ActionId,"-start")))!;
            if(PluginJson.Serialize(stored)!=PluginJson.Serialize(record))return false;
            var j=VoyagerJourney;
            if(j is null || PluginJson.Serialize(j)!=record.OriginalLedger || landedTick<record.SourceTick
                || landedTick-record.SourceTick>record.MaximumTicks || DateTimeOffset.UtcNow>=record.DeadlineUtc)return false;
            using var proof=OwnedSaveRecoveryLease.Open(primaryPath,j.OwnedSaveName);
            if(!proof.Prefix.MatchesExpectedIdentity || !proof.Prefix.Peaceful || proof.Prefix.GameVersion!=j.GameVersion
                || proof.Prefix.GameTick!=landedTick)return false;
            var completed=new {record.ActionId,record.SourceFailedActionId,record.SourceTick,landedTick,
                sourceLedgerFingerprint=CanonicalStateHash.Combine(record.OriginalLedger),primaryFingerprint=proof.Fingerprint,
                actualReturnTicks=landedTick-record.SourceTick,spentAfter=record.PriorSpent+landedTick-record.SourceTick,
                originalReturnReserve=j.ReturnReserveGameTicks,originalDeadlineUtc=j.DeadlineUtc,record.DeadlineUtc,
                checkpointReloaded=false,originalFailureRetained=true};
            WindowsCurrentUserSecurity.WriteSecureNewFile(SpaceReturnRecordPath(record.ActionId,"-completed"),
                new UTF8Encoding(false).GetBytes(PluginJson.Serialize(completed)));
            j.Phase="recovered_closed";j.SpentGameTicks=record.PriorSpent+landedTick-record.SourceTick;
            j.LastAccountedGameTick=landedTick;j.LastLandedGameTick=landedTick;
            j.CompletedSaveGameTick=landedTick;j.CompletedSaveActionId=record.ActionId;
            j.CompletedSaveSessionId=record.SessionId;j.CompletedSaveFingerprint=proof.Fingerprint;
            return TryPersistVoyagerJourney(j,out rejection,exactRecovery:true);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {rejection="stopped_space_home_persistence_failed:"+error.GetType().Name;return false;}
    }
}

internal sealed class StoppedSpaceReturnRecord
{
    public string ActionId {get;set;}=string.Empty;
    public string SessionId {get;set;}=string.Empty;
    public string SourceFailedActionId {get;set;}=string.Empty;
    public string OriginalLedger {get;set;}=string.Empty;
    public long SourceTick {get;set;}
    public long PriorSpent {get;set;}
    public long MaximumTicks {get;set;}
    public DateTimeOffset DeadlineUtc {get;set;}
    public string? PredecessorActionId {get;set;}
    public string? PredecessorFingerprint {get;set;}
    public bool PredecessorKnownTerminalFailed {get;set;}
}
