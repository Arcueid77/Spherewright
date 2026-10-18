using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Security;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    private const int TicketVersion = 2;
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromHours(24);
    private readonly string _ticketPath;
    private readonly string _bridgeInstanceId;
    private readonly string _gameVersion;
    private readonly ManualLogSource _logger;
    private FlightCheckpointTicket? _currentTicket;

    public FlightCheckpointStore(
        string bridgeInstanceId,
        string gameVersion,
        ManualLogSource logger)
        : this(bridgeInstanceId, gameVersion, logger, DefaultHandoffDirectory())
    {
    }

    internal FlightCheckpointStore(
        string bridgeInstanceId,
        string gameVersion,
        ManualLogSource logger,
        string handoffDirectory)
    {
        WindowsCurrentUserSecurity.EnsureSecureDirectory(handoffDirectory);
        _ticketPath = Path.Combine(handoffDirectory, "flight-checkpoint.json");
        _bridgeInstanceId = bridgeInstanceId;
        _gameVersion = gameVersion;
        _logger = logger;
        _currentTicket = ReadFromDisk();
        ReadVoyagerJourneyFromDisk();
        RetireIfCoveredByNewerPrimarySave();
        _logger.LogInfo("Spherewright initialized the protected reusable flight-checkpoint store");
    }

    private static string DefaultHandoffDirectory()
    {
        var pluginDirectory = Path.GetDirectoryName(typeof(FlightCheckpointStore).Assembly.Location)
            ?? throw new InvalidOperationException("The Spherewright Plugin directory is unavailable.");
        return Path.Combine(pluginDirectory, "runtime-handoff");
    }

    public FlightCheckpointTicket? CurrentTicket => IsReloadEligible(_currentTicket)
        ? _currentTicket
        : null;

    public bool HasCurrentTicket => CurrentTicket is not null;

    public bool HasUnreconciledReload => _currentTicket?.ReloadPending == true;

    public bool TryMarkReloadStarted(string checkpointId, string actionId, out string rejection)
    {
        var ticket = _currentTicket;
        if (!IsReloadEligible(ticket) || ticket is null || ticket.ReloadPending
            || ticket.CheckpointId != checkpointId || string.IsNullOrWhiteSpace(actionId))
        {
            rejection = "The exact checkpoint has an unresolved reload or is no longer eligible.";
            return false;
        }
        ticket.ReloadActionId = actionId;
        ticket.ReloadPending = true;
        return TryPersistLifecycle(ticket, "record the original checkpoint reload before native dispatch", out rejection);
    }

    public bool TryMarkReloadReconciled(string checkpointId, string actionId, out string rejection)
    {
        var ticket = _currentTicket;
        if (ticket is null || ticket.CheckpointId != checkpointId || ticket.ReloadActionId != actionId || !ticket.ReloadPending)
        {
            rejection = "Only the original pending checkpoint reload can be reconciled.";
            return false;
        }
        ticket.ReloadPending = false;
        ticket.LifecycleState = FlightCheckpointLifecycleStates.Recovered;
        if (TryPersistLifecycle(ticket, "reconcile the original checkpoint reload after exact new-session adoption", out rejection)) return true;
        ticket.ReloadPending = true;
        return false;
    }

    public FlightCheckpointTicket CreateDraft(
        string ownedSaveName,
        string sourceSessionId,
        long sourceRevision,
        int originPlanetId,
        int destinationPlanetId,
        string playerStateHash,
        string starSystemStateHash,
        int originStarId = 0,
        int destinationStarId = 0,
        string? voyagerJourneyId = null)
    {
        if (string.IsNullOrWhiteSpace(ownedSaveName)
            || string.IsNullOrWhiteSpace(sourceSessionId)
            || sourceRevision < 1
            || originPlanetId <= 0
            || (destinationPlanetId <= 0 && !(destinationPlanetId == 0
                && originStarId > 0 && destinationStarId > 0 && originStarId != destinationStarId
                && !string.IsNullOrWhiteSpace(voyagerJourneyId)))
            || originPlanetId == destinationPlanetId
            || string.IsNullOrWhiteSpace(playerStateHash)
            || string.IsNullOrWhiteSpace(starSystemStateHash))
        {
            throw new InvalidOperationException("A complete owned flight identity is required to create a checkpoint.");
        }

        if (voyagerJourneyId is not null && !Guid.TryParse(voyagerJourneyId, out _))
            throw new InvalidOperationException("A Voyager checkpoint requires its exact UUID journey identity.");

        var checkpointId = Guid.NewGuid().ToString("N");
        var issuedAt = DateTimeOffset.UtcNow;
        return new FlightCheckpointTicket
        {
            Version = TicketVersion,
            CheckpointId = checkpointId,
            ReloadToken = CreateToken(),
            CheckpointSaveName = "Spherewright_PreFlight_" + checkpointId,
            OwnedSaveName = ownedSaveName,
            SourceSessionId = sourceSessionId,
            SourceRevision = sourceRevision,
            SourceProcessId = Process.GetCurrentProcess().Id,
            SourceBridgeInstanceId = _bridgeInstanceId,
            GameVersion = _gameVersion,
            OriginPlanetId = originPlanetId,
            DestinationPlanetId = destinationPlanetId,
            OriginStarId = originStarId,
            DestinationStarId = destinationStarId,
            VoyagerJourneyId = voyagerJourneyId,
            PlayerStateHash = playerStateHash,
            StarSystemStateHash = starSystemStateHash,
            IssuedAtUtc = issuedAt,
            ExpiresAtUtc = issuedAt.Add(TicketLifetime),
            LifecycleState = FlightCheckpointLifecycleStates.Active,
        };
    }

    public void PersistCompletedCheckpoint(FlightCheckpointTicket ticket, long savedGameTick)
    {
        if (ticket is null || savedGameTick < 0)
        {
            throw new InvalidOperationException("A completed flight checkpoint and game tick are required.");
        }

        ticket.SavedGameTick = savedGameTick;
        using (var lease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.CheckpointSaveName), ticket.OwnedSaveName))
        {
            if (lease.Prefix.GameTick != savedGameTick || !lease.Prefix.Peaceful
                || !string.Equals(lease.Prefix.GameVersion, _gameVersion, StringComparison.Ordinal))
                throw new InvalidDataException("The completed checkpoint does not prove its exact owned identity, native version, peaceful mode and saved tick.");
            ticket.CheckpointFileFingerprint = lease.Fingerprint;
        }
        if (!IsStructurallyValid(ticket))
        {
            throw new InvalidOperationException("The completed flight-checkpoint identity is invalid.");
        }

        Persist(ticket);
        _currentTicket = ticket;
        _logger.LogInfo("Spherewright persisted an exact reusable pre-flight checkpoint ticket");
    }

    public bool TryMarkAttemptStarted(
        string checkpointId,
        string actionId,
        long gameTick,
        out string rejection)
    {
        rejection = string.Empty;
        var ticket = _currentTicket;
        if (!IsReloadEligible(ticket)
            || ticket is null
            || ticket.ReloadPending
            || !string.Equals(ticket.CheckpointId, checkpointId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(actionId)
            || gameTick < ticket.SavedGameTick)
        {
            rejection = "The flight attempt does not match the current reloadable checkpoint.";
            return false;
        }

        ticket.LifecycleState = FlightCheckpointLifecycleStates.Active;
        ticket.LastAttemptActionId = actionId;
        ticket.LastAttemptStartedAtGameTick = gameTick;
        ticket.NativeLaunchArmed = false;
        ticket.RecoveryRequiredAtGameTick = null;
        ticket.SuccessfulFlightAtGameTick = null;
        return TryPersistLifecycle(ticket, "start the bound flight attempt", out rejection);
    }

    public bool TryMarkRecoveryRequired(
        string checkpointId,
        string actionId,
        long gameTick,
        out string rejection)
    {
        rejection = string.Empty;
        var ticket = _currentTicket;
        if (!IsStructurallyValid(ticket)
            || ticket is null
            || ticket.ReloadPending
            || !string.Equals(ticket.CheckpointId, checkpointId, StringComparison.Ordinal)
            || IsSuccessfulOrRetired(ticket)
            || string.IsNullOrWhiteSpace(actionId)
            || !string.Equals(ticket.LastAttemptActionId, actionId, StringComparison.Ordinal)
            || gameTick < ticket.SavedGameTick)
        {
            rejection = "The failed flight does not match the current checkpoint lifecycle.";
            return false;
        }

        ticket.LifecycleState = FlightCheckpointLifecycleStates.RecoveryRequired;
        ticket.LastAttemptActionId = actionId;
        ticket.RecoveryRequiredAtGameTick = gameTick;
        return TryPersistLifecycle(ticket, "mark the bound flight as recovery-required", out rejection);
    }

    public bool TryMarkFlightSucceeded(
        string checkpointId,
        string actionId,
        long gameTick,
        out string rejection)
    {
        rejection = string.Empty;
        var ticket = _currentTicket;
        if (ticket is not null && IsStructurallyValid(ticket) && !ticket.ReloadPending
            && ticket.CheckpointId == checkpointId && ticket.LastAttemptActionId == actionId
            && ticket.LifecycleState == FlightCheckpointLifecycleStates.FlightSucceeded
            && ticket.SuccessfulFlightAtGameTick.HasValue && gameTick >= ticket.SuccessfulFlightAtGameTick.Value)
            return TryPersistLifecycle(ticket, "reconcile the original successful-flight seal", out rejection);
        if (!IsStructurallyValid(ticket)
            || ticket is null
            || ticket.ReloadPending
            || !string.Equals(ticket.CheckpointId, checkpointId, StringComparison.Ordinal)
            || IsSuccessfulOrRetired(ticket)
            || string.IsNullOrWhiteSpace(actionId)
            || !string.Equals(ticket.LastAttemptActionId, actionId, StringComparison.Ordinal)
            || gameTick < ticket.SavedGameTick)
        {
            rejection = "The successful flight does not match the current checkpoint lifecycle.";
            return false;
        }

        ticket.LifecycleState = FlightCheckpointLifecycleStates.FlightSucceeded;
        ticket.LastAttemptActionId = actionId;
        ticket.SuccessfulFlightAtGameTick = gameTick;
        ticket.RecoveryRequiredAtGameTick = null;
        return TryPersistLifecycle(ticket, "seal the successful flight before its primary save", out rejection);
    }

    public bool TryRetireAfterPrimarySave(
        string ownedSaveName,
        long savedGameTick,
        out bool retired,
        out string rejection)
    {
        retired = false;
        rejection = string.Empty;
        var ticket = _currentTicket;
        if (!IsStructurallyValid(ticket) || ticket is null)
        {
            return true;
        }

        if (!string.Equals(EffectiveLifecycle(ticket), FlightCheckpointLifecycleStates.FlightSucceeded, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(ticket.OwnedSaveName, ownedSaveName, StringComparison.Ordinal)
            || !ticket.SuccessfulFlightAtGameTick.HasValue
            || savedGameTick < ticket.SuccessfulFlightAtGameTick.Value)
        {
            rejection = "The primary save does not cover the successful flight checkpoint timeline.";
            return false;
        }

        ticket.LifecycleState = FlightCheckpointLifecycleStates.Retired;
        ticket.RetiredAtGameTick = savedGameTick;
        ticket.RetiredAtUtc = DateTimeOffset.UtcNow;
        if (!TryPersistLifecycle(ticket, "retire the checkpoint after the covering primary save", out rejection))
        {
            return false;
        }

        retired = true;
        _logger.LogInfo("Spherewright retired the successful flight checkpoint after a covering primary save");
        return true;
    }

    public bool TryGetActiveTicket(
        string reloadToken,
        out FlightCheckpointTicket? ticket,
        out string rejection)
    {
        ticket = null;
        rejection = string.Empty;
        if (string.IsNullOrWhiteSpace(reloadToken))
        {
            rejection = "A flight-checkpoint reload token is required.";
            return false;
        }

        var candidate = _currentTicket ?? ReadFromDisk();
        if (!IsReloadEligible(candidate)
            || candidate is null
            || candidate.ReloadPending
            || !FixedTimeEquals(candidate.ReloadToken, reloadToken))
        {
            rejection = "The flight-checkpoint ticket is missing, expired, retired, already succeeded, has an unresolved original reload, or belongs to another game version.";
            return false;
        }

        if (!TryValidatePrimaryCoverage(candidate, out rejection))
        {
            return false;
        }

        _currentTicket = candidate;
        ticket = candidate;
        return true;
    }

    public bool TryValidateCheckpointFile(FlightCheckpointTicket ticket, out string rejection)
    {
        rejection = string.Empty;
        if (!IsReloadEligible(ticket))
        {
            rejection = "The reusable flight-checkpoint ticket is invalid, expired, retired, or already succeeded.";
            return false;
        }

        if (!TryValidatePrimaryCoverage(ticket, out rejection))
        {
            return false;
        }

        try
        {
            using (OpenVerifiedCheckpointLease(ticket)) { }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is InvalidDataException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is NotSupportedException
            || exception is System.Security.SecurityException
            || exception is CryptographicException)
        {
            rejection = "The exact pre-flight save could not be verified; recovery is unavailable.";
            return false;
        }

        return true;
    }

    internal OwnedSaveRecoveryLease OpenVerifiedCheckpointLease(FlightCheckpointTicket ticket)
    {
        if (!IsReloadEligible(ticket) || !TryValidatePrimaryCoverage(ticket, out _))
            throw new InvalidDataException("The checkpoint is expired, retired or covered by a newer primary.");
        var lease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.CheckpointSaveName), ticket.OwnedSaveName);
        if (lease.Prefix.GameTick != ticket.SavedGameTick || !lease.Prefix.Peaceful
            || !string.Equals(lease.Prefix.GameVersion, _gameVersion, StringComparison.Ordinal)
            || !string.Equals(lease.Fingerprint, ticket.CheckpointFileFingerprint, StringComparison.Ordinal))
        {
            lease.Dispose();
            throw new InvalidDataException("The exact pre-flight save no longer matches its protected full-file evidence.");
        }
        return lease;
    }

    private bool IsStructurallyValid(FlightCheckpointTicket? ticket) =>
        ticket is not null
        && ticket.Version == TicketVersion
        && string.Equals(ticket.GameVersion, _gameVersion, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(ticket.CheckpointId)
        && !string.IsNullOrWhiteSpace(ticket.ReloadToken)
        && !string.IsNullOrWhiteSpace(ticket.CheckpointSaveName)
        && ticket.CheckpointSaveName.StartsWith("Spherewright_PreFlight_", StringComparison.Ordinal)
        && ticket.CheckpointSaveName.IndexOfAny(new[] { '/', '\\', ':' }) < 0
        && !string.IsNullOrWhiteSpace(ticket.OwnedSaveName)
        && !string.IsNullOrWhiteSpace(ticket.SourceSessionId)
        && ticket.SourceRevision >= 1
        && ticket.OriginPlanetId > 0
        && (ticket.DestinationPlanetId > 0 || (ticket.DestinationPlanetId == 0
            && ticket.OriginStarId > 0 && ticket.DestinationStarId > 0
            && ticket.OriginStarId != ticket.DestinationStarId && !string.IsNullOrWhiteSpace(ticket.VoyagerJourneyId)))
        && ticket.OriginPlanetId != ticket.DestinationPlanetId
        && ticket.SavedGameTick >= 0
        && !string.IsNullOrWhiteSpace(ticket.CheckpointFileFingerprint)
        && ticket.IssuedAtUtc != default
        && (ticket.ExpiresAtUtc == default || ticket.ExpiresAtUtc > ticket.IssuedAtUtc)
        && IsKnownLifecycle(ticket.LifecycleState)
        && !string.IsNullOrWhiteSpace(ticket.PlayerStateHash)
        && !string.IsNullOrWhiteSpace(ticket.StarSystemStateHash)
        && (ticket.VoyagerJourneyId is null || Guid.TryParse(ticket.VoyagerJourneyId, out _));

    private bool IsReloadEligible(FlightCheckpointTicket? ticket) =>
        IsStructurallyValid(ticket)
        && ticket is not null
        && EffectiveExpiresAt(ticket) > DateTimeOffset.UtcNow
        && !IsSuccessfulOrRetired(ticket);

    internal static bool IsRecoveryRequired(FlightCheckpointTicket ticket) =>
        string.Equals(
            EffectiveLifecycle(ticket),
            FlightCheckpointLifecycleStates.RecoveryRequired,
            StringComparison.Ordinal);

    internal static bool IsAttemptInFlight(FlightCheckpointTicket ticket) =>
        string.Equals(
            EffectiveLifecycle(ticket),
            FlightCheckpointLifecycleStates.Active,
            StringComparison.Ordinal);

    private static bool IsSuccessfulOrRetired(FlightCheckpointTicket ticket)
    {
        var lifecycle = EffectiveLifecycle(ticket);
        return string.Equals(lifecycle, FlightCheckpointLifecycleStates.FlightSucceeded, StringComparison.Ordinal)
            || string.Equals(lifecycle, FlightCheckpointLifecycleStates.Retired, StringComparison.Ordinal);
    }

    private static bool IsKnownLifecycle(string lifecycle) =>
        string.IsNullOrWhiteSpace(lifecycle)
        || string.Equals(lifecycle, FlightCheckpointLifecycleStates.Active, StringComparison.Ordinal)
        || string.Equals(lifecycle, FlightCheckpointLifecycleStates.RecoveryRequired, StringComparison.Ordinal)
        || string.Equals(lifecycle, FlightCheckpointLifecycleStates.FlightSucceeded, StringComparison.Ordinal)
        || string.Equals(lifecycle, FlightCheckpointLifecycleStates.Recovered, StringComparison.Ordinal)
        || string.Equals(lifecycle, FlightCheckpointLifecycleStates.Retired, StringComparison.Ordinal);

    private static string EffectiveLifecycle(FlightCheckpointTicket ticket) =>
        string.IsNullOrWhiteSpace(ticket.LifecycleState)
            ? FlightCheckpointLifecycleStates.Active
            : ticket.LifecycleState;

    private static DateTimeOffset EffectiveExpiresAt(FlightCheckpointTicket ticket) =>
        ticket.ExpiresAtUtc == default
            ? ticket.IssuedAtUtc.Add(TicketLifetime)
            : ticket.ExpiresAtUtc;

    private bool TryPersistLifecycle(
        FlightCheckpointTicket ticket,
        string operation,
        out string rejection)
    {
        _currentTicket = ticket;
        try
        {
            Persist(ticket);
            rejection = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException)
        {
            rejection = $"Spherewright could not {operation} ({exception.GetType().Name}).";
            _logger.LogError(rejection);
            return false;
        }
    }

    private FlightCheckpointTicket? ReadFromDisk()
    {
        try
        {
            string json;
            using (var stream = new FileStream(_ticketPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                json = reader.ReadToEnd();
            }

            var ticket = PluginJson.Deserialize<FlightCheckpointTicket>(json);
            if (!IsStructurallyValid(ticket))
            {
                _logger.LogWarning("Spherewright ignored an invalid reusable flight-checkpoint ticket");
                return null;
            }

            _logger.LogInfo("Spherewright loaded a reusable flight-checkpoint ticket from the protected handoff directory");
            return ticket;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is Newtonsoft.Json.JsonException
            || exception is ArgumentException)
        {
            _logger.LogWarning($"Spherewright ignored an unreadable flight-checkpoint ticket ({exception.GetType().Name})");
            return null;
        }
    }

    private void RetireIfCoveredByNewerPrimarySave()
    {
        var ticket = _currentTicket;
        if (!IsStructurallyValid(ticket)
            || ticket is null
            || string.Equals(EffectiveLifecycle(ticket), FlightCheckpointLifecycleStates.Retired, StringComparison.Ordinal))
        {
            return;
        }

        TryValidatePrimaryCoverage(ticket, out _);
    }

    // Both prepare and commit reach this fresh native comparison. A constructor-only
    // check would miss a normal primary save made after a failed flight in this process.
    private bool TryValidatePrimaryCoverage(FlightCheckpointTicket ticket, out string rejection)
    {
        rejection = string.Empty;
        try
        {
            GameSave.ReadHeader(ticket.OwnedSaveName, false, out var header);
            if (header is null || header.gameTick < 0)
            {
                rejection = "The exact primary save timeline is unavailable; checkpoint rollback safety is unknown.";
                return false;
            }

            if (header.gameTick <= ticket.SavedGameTick)
            {
                return true;
            }

            ticket.LifecycleState = FlightCheckpointLifecycleStates.Retired;
            ticket.RetiredAtGameTick = header.gameTick;
            ticket.RetiredAtUtc = DateTimeOffset.UtcNow;
            if (TryPersistLifecycle(
                    ticket,
                    "retire a checkpoint superseded by a newer exact primary save",
                    out _))
            {
                _logger.LogInfo("Spherewright retired a flight checkpoint whose exact primary save already covered a newer timeline");
            }

            rejection = "A newer exact primary save covers progress after this flight checkpoint; rollback is forbidden.";
            return false;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException)
        {
            _logger.LogWarning($"Spherewright could not compare the exact primary save with its flight checkpoint ({exception.GetType().Name})");
            rejection = "The exact primary save timeline could not be verified; checkpoint rollback safety is unknown.";
            return false;
        }
    }

    private void Persist(FlightCheckpointTicket ticket)
    {
        var directory = Path.GetDirectoryName(_ticketPath)
            ?? throw new InvalidOperationException("The flight-checkpoint handoff directory is unavailable.");
        WindowsCurrentUserSecurity.EnsureSecureDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".flight-checkpoint-{Guid.NewGuid():N}.tmp");
        var bytes = new UTF8Encoding(false).GetBytes(PluginJson.Serialize(ticket));
        WindowsCurrentUserSecurity.WriteSecureNewFile(temporaryPath, bytes);
        try
        {
            if (File.Exists(_ticketPath))
            {
                File.Replace(temporaryPath, _ticketPath, null, true);
            }
            else
            {
                File.Move(temporaryPath, _ticketPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string CreateToken()
    {
        var bytes = new byte[32];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        var difference = leftBytes.Length ^ rightBytes.Length;
        var length = Math.Max(leftBytes.Length, rightBytes.Length);
        for (var index = 0; index < length; index++)
        {
            var leftValue = index < leftBytes.Length ? leftBytes[index] : (byte)0;
            var rightValue = index < rightBytes.Length ? rightBytes[index] : (byte)0;
            difference |= leftValue ^ rightValue;
        }

        return difference == 0;
    }
}

internal sealed class FlightCheckpointTicket
{
    public int Version { get; set; }

    public string CheckpointId { get; set; } = string.Empty;

    public string ReloadToken { get; set; } = string.Empty;

    public string CheckpointSaveName { get; set; } = string.Empty;

    public string OwnedSaveName { get; set; } = string.Empty;

    public string SourceSessionId { get; set; } = string.Empty;

    public long SourceRevision { get; set; }

    public int SourceProcessId { get; set; }

    public string SourceBridgeInstanceId { get; set; } = string.Empty;

    public string GameVersion { get; set; } = string.Empty;

    public int OriginPlanetId { get; set; }

    public int DestinationPlanetId { get; set; }
    public int OriginStarId { get; set; }
    public int DestinationStarId { get; set; }
    public string? VoyagerJourneyId { get; set; }

    public long SavedGameTick { get; set; }

    public string CheckpointFileFingerprint { get; set; } = string.Empty;

    public string ReloadActionId { get; set; } = string.Empty;

    public bool ReloadPending { get; set; }
    public bool NativeLaunchArmed { get; set; }

    public string PlayerStateHash { get; set; } = string.Empty;

    public string StarSystemStateHash { get; set; } = string.Empty;

    public DateTimeOffset IssuedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string LifecycleState { get; set; } = string.Empty;

    public string LastAttemptActionId { get; set; } = string.Empty;

    public long? LastAttemptStartedAtGameTick { get; set; }

    public long? RecoveryRequiredAtGameTick { get; set; }

    public long? SuccessfulFlightAtGameTick { get; set; }

    public long? RetiredAtGameTick { get; set; }

    public DateTimeOffset? RetiredAtUtc { get; set; }
}

internal static class FlightCheckpointLifecycleStates
{
    public const string Active = "active";
    public const string RecoveryRequired = "recovery_required";
    public const string FlightSucceeded = "flight_succeeded";
    public const string Retired = "retired";

    public const string Recovered = "recovered";
}
