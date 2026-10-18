using System.Text;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Security;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

// One active bounded journey, beside the existing per-leg rollback checkpoint.
// This external reserve survives the normal outbound checkpoint's retirement.
internal sealed partial class FlightCheckpointStore
{
    private VoyagerJourneyRecord? _voyagerJourney;
    private bool _voyagerJourneyHardReadError;
    public string? VoyagerJourneyLoadError { get; private set; }
    public VoyagerJourneyRecord? VoyagerJourney => _voyagerJourney is null ? null
        : PluginJson.Deserialize<VoyagerJourneyRecord>(PluginJson.Serialize(_voyagerJourney));
    private string VoyagerJourneyPath => Path.Combine(Path.GetDirectoryName(_ticketPath)!, "voyager-journey.json");

    public bool TryPersistVoyagerJourney(VoyagerJourneyRecord record, out string rejection, bool exactRecovery = false,
        bool normalSaveProof = false, bool unstartedFlightProof = false)
    {
        rejection = string.Empty;
        if (_voyagerJourneyHardReadError)
        {
            rejection = "Preserve the unreadable original journey; a fresh record cannot overwrite its unknown reserve.";
            return false;
        }
        if (!ValidVoyagerJourney(record))
        {
            rejection = "The exact finite Voyager journey/reserve identity is incomplete.";
            return false;
        }
        var temporary = Path.Combine(Path.GetDirectoryName(_ticketPath)!, ".voyager-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            // Compare with the durable original before overwriting it. A read error,
            // external replacement or removal is not a new empty journey.
            if (_voyagerJourney is not null)
            {
                var disk = ReadVoyagerRecord();
                if (PluginJson.Serialize(disk) != PluginJson.Serialize(_voyagerJourney))
                    throw new InvalidDataException("The protected original journey changed outside its action.");
            }
            else if (File.Exists(VoyagerJourneyPath))
                throw new InvalidDataException("An unexamined journey already exists.");
            if (!ValidVoyagerTransition(_voyagerJourney, record, exactRecovery, normalSaveProof, unstartedFlightProof))
            {
                rejection = "A live journey cannot be replaced, extend its deadline, undo spent time or reduce its original return reserve.";
                return false;
            }
            WindowsCurrentUserSecurity.EnsureSecureDirectory(Path.GetDirectoryName(_ticketPath)!);
            WindowsCurrentUserSecurity.WriteSecureNewFile(temporary, new UTF8Encoding(false).GetBytes(PluginJson.Serialize(record)));
            if (File.Exists(VoyagerJourneyPath)) File.Replace(temporary, VoyagerJourneyPath, null, true);
            else File.Move(temporary, VoyagerJourneyPath);
            _voyagerJourney = PluginJson.Deserialize<VoyagerJourneyRecord>(PluginJson.Serialize(record));
            VoyagerJourneyLoadError = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is Newtonsoft.Json.JsonException)
        {
            if (exception is InvalidDataException || exception is FileNotFoundException || exception is DirectoryNotFoundException
                || exception is Newtonsoft.Json.JsonException)
                _voyagerJourneyHardReadError = true;
            rejection = "The protected Voyager reserve could not be persisted (" + exception.GetType().Name + ").";
            VoyagerJourneyLoadError = rejection;
            return false;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public bool TryReconcileVoyagerRecovery(FlightCheckpointTicket checkpoint, out string rejection, long? sourceGameTick = null)
    {
        rejection = string.Empty;
        if (string.IsNullOrEmpty(checkpoint.VoyagerJourneyId)) return true;
        var journey = VoyagerJourney;
        if (journey is null || journey.JourneyId != checkpoint.VoyagerJourneyId
            || journey.ActiveCheckpointId != checkpoint.CheckpointId
            || journey.ActiveActionId != checkpoint.LastAttemptActionId
            || checkpoint.ReloadPending || checkpoint.LifecycleState != FlightCheckpointLifecycleStates.Recovered
            || _currentTicket is null || PluginJson.Serialize(checkpoint) != PluginJson.Serialize(_currentTicket)
            || string.IsNullOrWhiteSpace(checkpoint.ReloadActionId)
            || checkpoint.OwnedSaveName != journey.OwnedSaveName || checkpoint.GameVersion != journey.GameVersion
            || (journey.ReturnLeg ? checkpoint.OriginPlanetId != journey.RemotePlanetId
                || checkpoint.DestinationPlanetId != journey.HomePlanetId || checkpoint.OriginStarId != journey.DestinationStarId
                || checkpoint.DestinationStarId != journey.HomeStarId
                : checkpoint.OriginPlanetId != journey.HomePlanetId || checkpoint.OriginStarId != journey.HomeStarId
                || checkpoint.DestinationStarId != journey.DestinationStarId))
        {
            rejection = "The original recovered checkpoint does not match its active Voyager journey/action.";
            return false;
        }
        // Outbound recovery restores home and closes that failed journey. A remote
        // return recovery keeps the existing return reserve and original deadline.
        if (journey.Phase == (journey.ReturnLeg ? "remote_landed" : "recovered_closed"))
            return TryPersistVoyagerJourney(journey, out rejection, exactRecovery: true);
        if (sourceGameTick.HasValue)
        {
            var baseline = journey.LastAccountedFlightActionId == checkpoint.LastAttemptActionId
                ? journey.LastAccountedGameTick : checkpoint.LastAttemptStartedAtGameTick ?? checkpoint.SavedGameTick;
            if (sourceGameTick.Value < baseline)
            { rejection = "The original reload's source tick cannot move its accounted journey timeline backwards."; return false; }
            journey.SpentGameTicks += sourceGameTick.Value - baseline;
            journey.LastAccountedGameTick = sourceGameTick.Value;
            journey.LastAccountedFlightActionId = checkpoint.LastAttemptActionId;
        }
        else
        {
            // A persisted failure tick is only a lower bound: the old process may
            // have kept running before its real exit. Without its source tick,
            // preserve the checkpoint recovery but forfeit the original remaining
            // journey allowance. This is conservative accounting, not observed time.
            journey.SpentGameTicks = Math.Max(journey.SpentGameTicks, journey.MaximumGameTicks);
            journey.SpentGameTicksIsConservative = true;
        }
        journey.Phase = journey.ReturnLeg ? "remote_landed" : "recovered_closed";
        if (journey.ReturnLeg) journey.LastLandedGameTick = checkpoint.SavedGameTick;
        return TryPersistVoyagerJourney(journey, out rejection, exactRecovery: true);
    }

    public bool TryArmVoyagerNativeLaunch(string checkpointId, string actionId, out string rejection)
    {
        var ticket = _currentTicket;
        if (ticket is null || ticket.CheckpointId != checkpointId || ticket.LastAttemptActionId != actionId
            || string.IsNullOrEmpty(ticket.VoyagerJourneyId) || ticket.ReloadPending || ticket.NativeLaunchArmed
            || ticket.LifecycleState != FlightCheckpointLifecycleStates.Active)
        { rejection = "The exact original Voyager launch cannot be armed twice or without its completed checkpoint."; return false; }
        ticket.NativeLaunchArmed = true;
        return TryPersistLifecycle(ticket, "arm the original Voyager launch before native movement", out rejection);
    }

    public bool TryCloseVoyagerBeforeNativeLaunch(string journeyId, string actionId, out string rejection)
    {
        var journey = VoyagerJourney;
        var ticket = _currentTicket;
        if (journey is null || journey.JourneyId != journeyId || journey.ActiveActionId != actionId
            || (journey.Phase != "outbound" && journey.Phase != "return")
            || ticket is null || journey.ActiveCheckpointId != ticket.CheckpointId || ticket.VoyagerJourneyId != journeyId
            || ticket.NativeLaunchArmed || ticket.ReloadPending
            || !string.IsNullOrEmpty(ticket.LastAttemptActionId) && ticket.LastAttemptActionId != actionId)
        { rejection = "The original checkpoint cannot prove that this failed journey never armed native movement."; return false; }
        journey.Phase = journey.ReturnLeg ? "remote_landed" : "prelaunch_failed";
        if (journey.ReturnLeg) journey.LastLandedGameTick = ticket.SavedGameTick;
        return TryPersistVoyagerJourney(journey, out rejection, unstartedFlightProof: true);
    }

    public bool TryCompleteVoyagerAfterOwnedSave(string journeyId, string saveActionId, string sessionId,
        int actualHomePlanetId, long lastOwnedSaveGameTick, string expectedFileFingerprint, out string rejection)
    {
        rejection = string.Empty;
        var journey = VoyagerJourney;
        if (journey is null || journey.JourneyId != journeyId || (journey.Phase != "home_awaiting_save" && journey.Phase != "completed")
            || actualHomePlanetId != journey.HomePlanetId || !journey.LastLandedGameTick.HasValue
            || lastOwnedSaveGameTick < journey.LastLandedGameTick.Value || !Guid.TryParse(saveActionId, out _)
            || string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(expectedFileFingerprint))
        { rejection = "Only an exact normally completed owned Save at the returned home can close this journey."; return false; }
        try
        {
            using var lease = Spherewright.Bridge.Core.Safety.OwnedSaveRecoveryLease.Open(GameSave.SavePath(journey.OwnedSaveName), journey.OwnedSaveName);
            if (!lease.Prefix.Peaceful || lease.Prefix.GameVersion != _gameVersion
                || lease.Prefix.GameTick != lastOwnedSaveGameTick || lease.Fingerprint != expectedFileFingerprint)
            { rejection = "The original normal Save's full-file proof no longer matches; header tick alone cannot close the journey."; return false; }
            if (journey.Phase == "completed")
            {
                if (_voyagerJourneyHardReadError || PluginJson.Serialize(ReadVoyagerRecord()) != PluginJson.Serialize(journey))
                    throw new InvalidDataException("The completed original journey's durable proof disappeared or changed.");
                return journey.CompletedSaveActionId == saveActionId && journey.CompletedSaveSessionId == sessionId
                    && journey.CompletedSaveGameTick == lastOwnedSaveGameTick && journey.CompletedSaveFingerprint == expectedFileFingerprint;
            }
            journey.Phase = "completed";
            journey.CompletedSaveGameTick = lastOwnedSaveGameTick;
            journey.CompletedSaveActionId = saveActionId;
            journey.CompletedSaveSessionId = sessionId;
            journey.CompletedSaveFingerprint = lease.Fingerprint;
            return TryPersistVoyagerJourney(journey, out rejection, normalSaveProof: true);
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException
            || exception is UnauthorizedAccessException || exception is ArgumentException || exception is Newtonsoft.Json.JsonException)
        {
            rejection = "The exact normally saved owned file could not prove Voyager closure (" + exception.GetType().Name + ").";
            if (journey.Phase == "completed")
            {
                _voyagerJourneyHardReadError = true;
                VoyagerJourneyLoadError = rejection;
            }
            return false;
        }
    }

    private void ReadVoyagerJourneyFromDisk()
    {
        if (!File.Exists(VoyagerJourneyPath) && !Directory.Exists(VoyagerJourneyPath))
        {
            if (!string.IsNullOrEmpty(_currentTicket?.VoyagerJourneyId))
            {
                _voyagerJourneyHardReadError = true;
                VoyagerJourneyLoadError = "The original Voyager checkpoint has lost its protected journey reserve; preserve its original action.";
            }
            return;
        }
        try
        {
            _voyagerJourney = ReadVoyagerRecord();
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is Newtonsoft.Json.JsonException)
        {
            _voyagerJourneyHardReadError = true;
            VoyagerJourneyLoadError = "The protected Voyager journey is unreadable; preserve the original action and reserve.";
            _logger.LogError(VoyagerJourneyLoadError + " (" + exception.GetType().Name + ")");
        }
    }

    private VoyagerJourneyRecord ReadVoyagerRecord()
    {
        using var stream = new FileStream(VoyagerJourneyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 65536) throw new InvalidDataException("Journey record exceeds its finite bound.");
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var record = PluginJson.Deserialize<VoyagerJourneyRecord>(reader.ReadToEnd());
        if (!ValidVoyagerJourney(record)) throw new InvalidDataException("Journey identity is invalid.");
        return record!;
    }

    private static bool ValidVoyagerTransition(VoyagerJourneyRecord? old, VoyagerJourneyRecord next, bool exactRecovery,
        bool normalSaveProof, bool unstartedFlightProof)
    {
        if (old is null) return next.Phase == "outbound" && !next.ReturnLeg && next.SpentGameTicks == 0;
        if (old.JourneyId != next.JourneyId)
            return (old.Phase == "completed" || old.Phase == "recovered_closed" || old.Phase == "prelaunch_failed")
                && next.Phase == "outbound" && !next.ReturnLeg && next.SpentGameTicks == 0;
        if (old.Phase == "completed" || old.Phase == "recovered_closed" || old.Phase == "prelaunch_failed")
            return PluginJson.Serialize(old) == PluginJson.Serialize(next);
        var phaseAllowed = old.Phase == next.Phase
            || (old.Phase == "outbound" && (next.Phase == "remote_landed" || next.Phase == "recovery_required" || next.Phase == "outcome_unknown"))
            || (old.Phase == "remote_landed" && next.Phase == "return" && next.ReturnLeg)
            || (old.Phase == "return" && (next.Phase == "home_awaiting_save" || next.Phase == "recovery_required" || next.Phase == "outcome_unknown"))
            || (normalSaveProof && old.Phase == "home_awaiting_save" && next.Phase == "completed")
            || (unstartedFlightProof && old.Phase == "outbound" && next.Phase == "prelaunch_failed" && !next.ReturnLeg)
            || (unstartedFlightProof && old.Phase == "return" && next.Phase == "remote_landed" && next.ReturnLeg)
            || (exactRecovery && (old.Phase == "recovery_required" || old.Phase == "outcome_unknown" || old.Phase == "outbound" || old.Phase == "return")
                && (next.Phase == "remote_landed" && old.ReturnLeg || next.Phase == "recovered_closed" && !old.ReturnLeg));
        if (!phaseAllowed || old.ReturnLeg && !next.ReturnLeg) return false;
        return old.OwnedSaveName == next.OwnedSaveName && old.GameVersion == next.GameVersion
            && old.HomePlanetId == next.HomePlanetId && old.HomeStarId == next.HomeStarId
            && old.DestinationStarId == next.DestinationStarId && old.StartedAtGameTick == next.StartedAtGameTick
            && old.MaximumGameTicks == next.MaximumGameTicks && old.ReturnReserveGameTicks == next.ReturnReserveGameTicks
            && old.IssuedAtUtc == next.IssuedAtUtc && old.DeadlineUtc == next.DeadlineUtc
            && next.SpentGameTicks >= old.SpentGameTicks && next.ReturnEnergyFloorJoules >= old.ReturnEnergyFloorJoules
            && next.WithdrawalReserveJoules >= old.WithdrawalReserveJoules
            && next.ReturnCoreEnergyMinimumJoules >= old.ReturnCoreEnergyMinimumJoules
            && next.ExtraWarperReserve >= old.ExtraWarperReserve
            && ((old.Phase != "completed" && old.Phase != "recovered_closed") || next.Phase == old.Phase);
    }

    private bool ValidVoyagerJourney(VoyagerJourneyRecord? r) => r is not null && r.Version == 1
        && Guid.TryParse(r.JourneyId, out _) && !string.IsNullOrWhiteSpace(r.OwnedSaveName)
        && r.GameVersion == _gameVersion && r.HomePlanetId > 0 && r.HomeStarId > 0 && r.DestinationStarId > 0
        && r.HomeStarId != r.DestinationStarId && r.MaximumGameTicks >= 7200 && r.MaximumGameTicks <= 108000
        && r.ReturnReserveGameTicks >= 3600 && r.ReturnReserveGameTicks < r.MaximumGameTicks
        && r.SpentGameTicks >= 0
        && r.StartedAtGameTick >= 0 && r.DeadlineUtc > r.IssuedAtUtc && r.IssuedAtUtc != default
        && r.DeadlineUtc - r.IssuedAtUtc <= TimeSpan.FromHours(2)
        && FinitePositive(r.ReturnEnergyFloorJoules) && FinitePositive(r.WithdrawalReserveJoules)
        && FinitePositive(r.ReturnCoreEnergyMinimumJoules) && r.ExtraWarperReserve >= 0 && r.ExtraWarperReserve <= 18
        && new[] { "outbound", "remote_landed", "return", "home_awaiting_save", "completed", "recovery_required", "outcome_unknown", "recovered_closed", "prelaunch_failed" }.Contains(r.Phase)
        && (r.Phase != "completed" || (r.CompletedSaveGameTick.HasValue && r.LastLandedGameTick.HasValue && r.CompletedSaveGameTick >= r.LastLandedGameTick
            && Guid.TryParse(r.CompletedSaveActionId, out _) && !string.IsNullOrEmpty(r.CompletedSaveSessionId)
            && !string.IsNullOrEmpty(r.CompletedSaveFingerprint)));

    private static bool FinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
}

internal sealed class VoyagerJourneyRecord
{
    public int Version { get; set; } = 1;
    public string JourneyId { get; set; } = string.Empty;
    public string OwnedSaveName { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string Phase { get; set; } = "outbound";
    public int HomePlanetId { get; set; }
    public int HomeStarId { get; set; }
    public int DestinationStarId { get; set; }
    public int? RemotePlanetId { get; set; }
    public bool ReturnLeg { get; set; }
    public string ActiveActionId { get; set; } = string.Empty;
    public string ActiveCheckpointId { get; set; } = string.Empty;
    public long StartedAtGameTick { get; set; }
    public long SpentGameTicks { get; set; }
    public long LastAccountedGameTick { get; set; }
    public string LastAccountedFlightActionId { get; set; } = string.Empty;
    public bool SpentGameTicksIsConservative { get; set; }
    public long MaximumGameTicks { get; set; }
    public long ReturnReserveGameTicks { get; set; }
    public double ReturnEnergyFloorJoules { get; set; }
    public double WithdrawalReserveJoules { get; set; }
    public double ReturnCoreEnergyMinimumJoules { get; set; }
    public int ExtraWarperReserve { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
    public long? LastLandedGameTick { get; set; }
    public long? CompletedSaveGameTick { get; set; }
    public string? CompletedSaveActionId { get; set; }
    public string? CompletedSaveSessionId { get; set; }
    public string? CompletedSaveFingerprint { get; set; }
}
