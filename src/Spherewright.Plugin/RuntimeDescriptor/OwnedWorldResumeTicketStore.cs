using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Security;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed class OwnedWorldResumeTicketStore
{
    public string CurrentGameVersion => _gameVersion;
    private const int TicketVersion = 1;
    private readonly string _ticketPath;
    private readonly string _handoffTicketPath;
    private readonly string _runtimeDirectory;
    private readonly string _bridgeInstanceId;
    private readonly string _gameVersion;
    private readonly ManualLogSource _logger;
    private OwnedWorldResumeTicket? _currentTicket;
    private string? _currentTicketPath;

    public OwnedWorldResumeTicketStore(
        string configuredRuntimeDirectory,
        string bridgeInstanceId,
        string gameVersion,
        ManualLogSource logger)
        : this(RuntimeDescriptorPublisher.ResolveRuntimeDirectory(configuredRuntimeDirectory),
            Path.Combine(Path.GetDirectoryName(typeof(OwnedWorldResumeTicketStore).Assembly.Location)
                ?? throw new InvalidOperationException("The Spherewright Plugin directory is unavailable."), "runtime-handoff"),
            bridgeInstanceId, gameVersion, logger)
    {
    }

    internal OwnedWorldResumeTicketStore(string runtimeDirectory, string handoffDirectory,
        string bridgeInstanceId, string gameVersion, ManualLogSource logger)
    {
        _runtimeDirectory = runtimeDirectory;
        _ticketPath = Path.Combine(_runtimeDirectory, "owned-world-resume.json");
        WindowsCurrentUserSecurity.EnsureSecureDirectory(handoffDirectory);
        _handoffTicketPath = Path.Combine(handoffDirectory, "owned-world-resume.json");
        _bridgeInstanceId = bridgeInstanceId;
        _gameVersion = gameVersion;
        _logger = logger;
        _logger.LogInfo("Spherewright initialized the protected owned-world resume ticket store");
        _currentTicket = ReadFromDisk();
    }

    public string? CurrentResumeToken => _currentTicket?.ResumeToken;

    public bool HasCurrentTicket => _currentTicket is not null;

    public void ArmFromHealthySavedOwnedSession(
        string ownedSaveName,
        string sessionId,
        int planetId,
        long minimumGameTick,
        OwnedWorldGameplayJournalCheckpoint gameplayJournalCheckpoint)
    {
        Arm(
            ownedSaveName,
            sessionId,
            planetId,
            minimumGameTick,
            gameplayJournalCheckpoint,
            quarantineActionId: string.Empty);
        _logger.LogInfo("Spherewright armed a one-time exact planned-restart ticket from a healthy owned save");
    }

    public void ArmFromQuarantinedOwnedSession(
        string ownedSaveName,
        string sessionId,
        int planetId,
        long minimumGameTick,
        string quarantineActionId,
        OwnedWorldGameplayJournalCheckpoint gameplayJournalCheckpoint)
    {
        if (string.IsNullOrWhiteSpace(quarantineActionId))
        {
            throw new InvalidOperationException("A quarantined owned session requires its exact action identity.");
        }

        Arm(ownedSaveName, sessionId, planetId, minimumGameTick, gameplayJournalCheckpoint, quarantineActionId);
        _logger.LogInfo("Spherewright armed a one-time exact quarantine-recovery ticket");
    }

    private void Arm(
        string ownedSaveName,
        string sessionId,
        int planetId,
        long minimumGameTick,
        OwnedWorldGameplayJournalCheckpoint gameplayJournalCheckpoint,
        string quarantineActionId)
    {
        if (string.IsNullOrWhiteSpace(ownedSaveName)
            || string.IsNullOrWhiteSpace(sessionId)
            || planetId <= 0
            || minimumGameTick < 0
            || gameplayJournalCheckpoint is null
            || gameplayJournalCheckpoint.Version != OwnedWorldGameplayJournalCheckpoint.CurrentVersion
            || !string.Equals(
                gameplayJournalCheckpoint.JournalId,
                GameplayJournalIdentity.HashOwnedSaveIdentity(ownedSaveName),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(gameplayJournalCheckpoint.TrackingMode)
            || gameplayJournalCheckpoint.TrackingStartedAtGameTick < 0
            || gameplayJournalCheckpoint.MinimumDurableThroughSequence < 0)
        {
            throw new InvalidOperationException("A complete owned-session identity is required to arm restart-resume.");
        }

        var supersededTokens = CaptureReplicaTokens();
        var issuedAt = DateTimeOffset.UtcNow;
        var ticket = new OwnedWorldResumeTicket
        {
            Version = TicketVersion,
            ResumeToken = CreateToken(),
            OwnedSaveName = ownedSaveName,
            SourceSessionId = sessionId,
            SourceProcessId = Process.GetCurrentProcess().Id,
            SourceBridgeInstanceId = _bridgeInstanceId,
            GameVersion = _gameVersion,
            ExpectedPlanetId = planetId,
            MinimumGameTick = minimumGameTick,
            GameplayJournalCheckpoint = gameplayJournalCheckpoint,
            QuarantineActionId = quarantineActionId,
            IssuedAtUtc = issuedAt,
            ExpiresAtUtc = issuedAt.AddHours(24),
        };
        Persist(ticket);
        _currentTicket = ticket;
        _currentTicketPath = null;
        foreach (var supersededToken in supersededTokens.Where(token =>
                     !FixedTimeEquals(token, ticket.ResumeToken)))
        {
            if (!PersistConsumptionTombstone(supersededToken))
            {
                throw new IOException("A superseded resume-ticket generation could not be durably tombstoned.");
            }

            DeleteTicketReplicaIfMatching(_ticketPath, supersededToken);
            DeleteTicketReplicaIfMatching(_handoffTicketPath, supersededToken);
        }
    }

    public bool TryGetActiveTicket(
        string resumeToken,
        out OwnedWorldResumeTicket? ticket,
        out string rejection)
    {
        ticket = null;
        rejection = string.Empty;
        if (string.IsNullOrWhiteSpace(resumeToken))
        {
            rejection = "A resume token is required.";
            return false;
        }

        var candidate = _currentTicket ?? ReadFromDisk();
        if (candidate is null)
        {
            rejection = "No one-time owned-world resume ticket exists.";
            return false;
        }

        if (IsConsumed(candidate.ResumeToken)
            || candidate.Version != TicketVersion
            || !FixedTimeEquals(candidate.ResumeToken, resumeToken)
            || !string.Equals(candidate.GameVersion, _gameVersion, StringComparison.Ordinal)
            || candidate.ExpiresAtUtc <= DateTimeOffset.UtcNow
            || string.IsNullOrWhiteSpace(candidate.OwnedSaveName)
            || string.IsNullOrWhiteSpace(candidate.SourceSessionId)
            || candidate.ExpectedPlanetId <= 0
            || candidate.MinimumGameTick < 0)
        {
            rejection = "The one-time owned-world resume ticket is consumed, invalid, expired, or belongs to another game version.";
            return false;
        }

        if (!TryValidateGameplayJournalContinuity(candidate, out rejection))
        {
            return false;
        }

        _currentTicket = candidate;
        ticket = candidate;
        return true;
    }

    private bool TryValidateGameplayJournalContinuity(
        OwnedWorldResumeTicket ticket,
        out string rejection) => TryValidateGameplayJournalContinuity(ticket, false, out _, out rejection);

    private bool TryValidateGameplayJournalContinuity(
        OwnedWorldResumeTicket ticket,
        bool requireExactCheckpoint,
        out string journalFingerprint,
        out string rejection)
    {
        journalFingerprint = string.Empty;
        rejection = string.Empty;
        var checkpoint = ticket.GameplayJournalCheckpoint;
        if (checkpoint is null)
        {
            // Version-1 tickets issued before the continuity checkpoint was
            // introduced remain compatible. Every newly armed ticket carries
            // the checkpoint and therefore takes the strict path below.
            return !requireExactCheckpoint;
        }

        try
        {
            var identityHash = GameplayJournalIdentity.HashOwnedSaveIdentity(ticket.OwnedSaveName);
            if (checkpoint.Version != OwnedWorldGameplayJournalCheckpoint.CurrentVersion
                || !string.Equals(checkpoint.JournalId, identityHash, StringComparison.Ordinal))
            {
                rejection = "The protected gameplay journal checkpoint does not match the owned-world ticket.";
                return false;
            }

            var path = Path.Combine(_runtimeDirectory, "journals", $"gameplay-{identityHash}.json");
            if (!File.Exists(path))
            {
                rejection = "The protected gameplay journal required by this resume ticket is unavailable.";
                return false;
            }

            string json;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                if (requireExactCheckpoint && stream.Length > 16L * 1024 * 1024)
                    throw new IOException("Journal exceeds the bounded reauthorization limit.");
                json = reader.ReadToEnd();
            }
            var document = PluginJson.Deserialize<GameplayJournalDocument>(json);
            if (document is null
                || document.Version != 1
                || !string.Equals(document.OwnedSaveIdentityHash, identityHash, StringComparison.Ordinal)
                || !OwnedWorldVersionCompatibilityPolicy.JournalMatches(document.GameVersion,
                    document.VersionTransitions, document.Entries?.Count ?? -1, ticket.GameVersion)
                || document.Entries is null
                || document.Entries.Any(entry => entry is null)
                || !GameplayJournalContinuityPolicy.MatchesCheckpoint(
                    checkpoint.JournalId,
                    checkpoint.TrackingMode,
                    checkpoint.HistoricalCoverageComplete,
                    checkpoint.TrackingStartedAtGameTick,
                    checkpoint.MinimumDurableThroughSequence,
                    document.JournalId,
                    document.TrackingMode,
                    document.HistoricalCoverageComplete,
                    document.TrackingStartedAtGameTick,
                    document.Entries.Select(entry => entry.Sequence).ToArray()))
            {
                rejection = "The protected gameplay journal is missing, truncated, or does not match this resume ticket.";
                return false;
            }

            if (requireExactCheckpoint && document.Entries.Count != checkpoint.MinimumDurableThroughSequence)
            {
                rejection = "Journal progress differs from the saved checkpoint; reauthorization must not roll back later history.";
                return false;
            }
            journalFingerprint = HashToken(json);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is Newtonsoft.Json.JsonException
            || exception is ArgumentException)
        {
            rejection = $"The protected gameplay journal continuity check failed ({exception.GetType().Name}).";
            return false;
        }
    }

    public void Consume(string resumeToken)
    {
        var current = _currentTicket ?? ReadFromDisk();
        if (current is null || !FixedTimeEquals(current.ResumeToken, resumeToken))
        {
            return;
        }

        if (!PersistConsumptionTombstone(resumeToken, current.GameVersion))
        {
            _logger.LogError("Spherewright did not consume the owned-world resume ticket because no durable tombstone could be written");
            return;
        }

        _currentTicket = null;
        _currentTicketPath = null;
        DeleteTicketReplicaIfMatching(_ticketPath, resumeToken);
        DeleteTicketReplicaIfMatching(_handoffTicketPath, resumeToken);
    }

    // Unlike TryGetActiveTicket this grants no loading capability. It only authenticates
    // provenance for a fresh short-lived plan. Commit still requires its mode-specific
    // explicit conversation authority and exact disclosure digest, never implicit fallback.
    public bool TryGetExpiredPrimaryProvenance(string resumeToken,
        out OwnedWorldResumeTicket? ticket, out string fingerprint, out string rejection)
        => TryGetProtectedProvenance(resumeToken, false, out ticket, out fingerprint, out rejection);

    public bool TryGetFixedAutosaveProvenance(string resumeToken,
        out OwnedWorldResumeTicket? ticket, out string fingerprint, out string rejection)
        => TryGetProtectedProvenance(resumeToken, true, out ticket, out fingerprint, out rejection);

    public bool TryGetVersionChangedPrimaryProvenance(string resumeToken,
        out OwnedWorldResumeTicket? ticket, out string fingerprint, out string rejection)
        => TryGetProtectedProvenance(resumeToken, true, out ticket, out fingerprint, out rejection,
            versionChangedPrimary: true);

    private bool TryGetProtectedProvenance(string resumeToken, bool requireActiveCredential,
        out OwnedWorldResumeTicket? ticket, out string fingerprint, out string rejection,
        bool versionChangedPrimary = false)
    {
        ticket = null;
        fingerprint = string.Empty;
        rejection = "Protected recovery provenance is missing, changed, consumed, expired for this mode, or incomplete.";
        try
        {
            if (versionChangedPrimary && !requireActiveCredential) return false;
            if (string.IsNullOrWhiteSpace(resumeToken)) return false;
            // A malformed tombstone is still a refusal here; never resurrect consumed provenance.
            var hash = HashToken(resumeToken);
            if (File.Exists(GetTombstonePath(_runtimeDirectory, hash))
                || File.Exists(GetTombstonePath(Path.GetDirectoryName(_handoffTicketPath)!, hash))) return false;
            if (File.Exists(GetReauthorizationAttemptPath(_runtimeDirectory, hash))
                || File.Exists(GetReauthorizationAttemptPath(Path.GetDirectoryName(_handoffTicketPath)!, hash)))
            {
                rejection = "A previous reauthorization attempt needs manual reconciliation; automatic replay is forbidden.";
                return false;
            }
            foreach (var path in new[] { _ticketPath, _handoffTicketPath })
                if (!File.Exists(path) || new FileInfo(path).Length > 65536) return false;
            var runtime = ReadFromPath(_ticketPath);
            var handoff = ReadFromPath(_handoffTicketPath);
            if (runtime is null || handoff is null
                || !string.Equals(PluginJson.Serialize(runtime), PluginJson.Serialize(handoff), StringComparison.Ordinal)
                || (_currentTicket is not null && !string.Equals(PluginJson.Serialize(_currentTicket),
                    PluginJson.Serialize(runtime), StringComparison.Ordinal))
                || runtime.Version != TicketVersion || !FixedTimeEquals(runtime.ResumeToken, resumeToken)
                || !(versionChangedPrimary
                    ? OwnedWorldPrimaryMigrationPolicy.IsSupportedPair(runtime.GameVersion, _gameVersion)
                    : requireActiveCredential
                    ? string.Equals(runtime.GameVersion, _gameVersion, StringComparison.Ordinal)
                    : OwnedWorldVersionCompatibilityPolicy.AllowsReauthorization(runtime.GameVersion, _gameVersion))
                || string.IsNullOrWhiteSpace(runtime.OwnedSaveName)
                || runtime.OwnedSaveName == "." || runtime.OwnedSaveName == ".."
                || Path.GetFileName(runtime.OwnedSaveName) != runtime.OwnedSaveName
                || runtime.OwnedSaveName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || string.IsNullOrWhiteSpace(runtime.SourceSessionId)
                || runtime.SourceProcessId <= 0 || string.IsNullOrWhiteSpace(runtime.SourceBridgeInstanceId)
                || runtime.ExpectedPlanetId <= 0 || runtime.MinimumGameTick < 0
                || !(requireActiveCredential ? OwnedWorldReauthorizationPolicy.AllowsActiveProvenance(
                    string.IsNullOrWhiteSpace(runtime.QuarantineActionId), runtime.GameplayJournalCheckpoint is not null,
                    IsConsumed(resumeToken), runtime.IssuedAtUtc, runtime.ExpiresAtUtc, DateTimeOffset.UtcNow)
                    : OwnedWorldReauthorizationPolicy.AllowsExpiredProvenance(
                    string.IsNullOrWhiteSpace(runtime.QuarantineActionId), runtime.GameplayJournalCheckpoint is not null,
                    IsConsumed(resumeToken), runtime.IssuedAtUtc, runtime.ExpiresAtUtc, DateTimeOffset.UtcNow))) return false;
            if (!TryValidateGameplayJournalContinuity(runtime, true, out var journalHash, out rejection)) return false;
            ticket = runtime;
            fingerprint = CanonicalStateHash.Combine(versionChangedPrimary ? "active-version-primary-provenance-v1"
                : requireActiveCredential ? "active-fixed-autosave0-provenance-v1"
                : "expired-primary-provenance-v2", PluginJson.Serialize(runtime), journalHash, _gameVersion);
            rejection = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is System.Security.SecurityException)
        {
            rejection = "Protected recovery provenance could not be read safely; no load is allowed.";
            return false;
        }
    }

    public bool TryConsumeReauthorization(string resumeToken, string expectedFingerprint,
        string actionId, string confirmationDigest, bool requireActiveCredential = false,
        bool versionChangedPrimary = false)
    {
        if (!Guid.TryParse(actionId, out _) || string.IsNullOrWhiteSpace(confirmationDigest)
            || !TryGetProtectedProvenance(resumeToken, requireActiveCredential, out var ticket, out var currentFingerprint, out _, versionChangedPrimary)
            || !FixedTimeEquals(currentFingerprint, expectedFingerprint)) return false;
        var tokenHash = HashToken(resumeToken);
        var handoffDirectory = Path.GetDirectoryName(_handoffTicketPath)!;
        var attempt = new OwnedWorldReauthorizationAttempt
        {
            Ticket = ticket!, ActionId = actionId, ConfirmationDigest = confirmationDigest,
            ProvenanceFingerprint = expectedFingerprint, RecordedAtUtc = DateTimeOffset.UtcNow,
        };
        // Retain the original proof for human reconciliation even if the process dies
        // after write-ahead consumption and before native load/adoption/normal save.
        var runtimeRecorded = TryPersistAtPath(GetReauthorizationAttemptPath(_runtimeDirectory, tokenHash),
            _runtimeDirectory, attempt, "reauthorization attempt");
        var handoffRecorded = TryPersistAtPath(GetReauthorizationAttemptPath(handoffDirectory, tokenHash),
            handoffDirectory, attempt, "handoff reauthorization attempt");
        if (!runtimeRecorded || !handoffRecorded) return false;
        // Write-ahead consumption precedes the native loader. A crash cannot revive this approval.
        if (!PersistConsumptionTombstone(resumeToken, ticket!.GameVersion)) return false;
        _currentTicket = null;
        _currentTicketPath = null;
        DeleteTicketReplicaIfMatching(_ticketPath, resumeToken);
        DeleteTicketReplicaIfMatching(_handoffTicketPath, resumeToken);
        return true;
    }

    public FileStream OpenReauthorizationJournalLease(OwnedWorldResumeTicket ticket, string provenance,
        bool requireActiveCredential = false, bool versionChangedPrimary = false)
    {
        var identity = GameplayJournalIdentity.HashOwnedSaveIdentity(ticket.OwnedSaveName);
        var stream = new FileStream(Path.Combine(_runtimeDirectory, "journals", $"gameplay-{identity}.json"),
            FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (!TryGetProtectedProvenance(ticket.ResumeToken, requireActiveCredential, out _, out var fresh, out _, versionChangedPrimary)
                || !FixedTimeEquals(provenance, fresh))
                throw new IOException("The exact journal changed before its protected load lease.");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    private IReadOnlyList<string> CaptureReplicaTokens()
    {
        var tokens = new List<string>();
        if (_currentTicket is not null)
        {
            tokens.Add(_currentTicket.ResumeToken);
        }

        var runtime = ReadFromPath(_ticketPath);
        var handoff = ReadFromPath(_handoffTicketPath);
        if (runtime is not null)
        {
            tokens.Add(runtime.ResumeToken);
        }

        if (handoff is not null)
        {
            tokens.Add(handoff.ResumeToken);
        }

        return tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private void DeleteTicketReplicaIfMatching(string path, string resumeToken)
    {
        if (TicketPathMatchesToken(path, resumeToken))
        {
            DeleteTicketPath(path);
        }
    }

    private bool PersistConsumptionTombstone(string resumeToken, string? sourceGameVersion = null)
    {
        if (string.IsNullOrWhiteSpace(resumeToken))
        {
            return false;
        }

        var tokenHash = HashToken(resumeToken);
        var tombstone = new OwnedWorldResumeConsumptionTombstone
        {
            Version = 1,
            ResumeTokenHash = tokenHash,
            // Preserve source compatibility even if an older Plugin is later restarted.
            // New readers treat consumption as irreversible across all runtime versions.
            GameVersion = sourceGameVersion ?? _gameVersion,
            ConsumedAtUtc = DateTimeOffset.UtcNow,
        };
        var handoffDirectory = Path.GetDirectoryName(_handoffTicketPath)
            ?? throw new InvalidOperationException("The owned-world handoff directory is unavailable.");
        WindowsCurrentUserSecurity.EnsureSecureDirectory(_runtimeDirectory);
        WindowsCurrentUserSecurity.EnsureSecureDirectory(handoffDirectory);
        var runtimePersisted = TryPersistAtPath(
            GetTombstonePath(_runtimeDirectory, tokenHash),
            _runtimeDirectory,
            tombstone,
            "runtime consumption tombstone");
        var handoffPersisted = TryPersistAtPath(
            GetTombstonePath(handoffDirectory, tokenHash),
            handoffDirectory,
            tombstone,
            "handoff consumption tombstone");
        return runtimePersisted || handoffPersisted;
    }

    private void DeleteTicketPath(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            _logger.LogWarning($"Spherewright could not consume its owned-world resume ticket ({exception.GetType().Name})");
        }
    }

    private static bool TicketPathMatchesToken(string path, string resumeToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var candidate = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(path));
            return candidate is not null && FixedTimeEquals(candidate.ResumeToken, resumeToken);
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is Newtonsoft.Json.JsonException
            || exception is ArgumentException)
        {
            return false;
        }
    }

    private OwnedWorldResumeTicket? ReadFromDisk()
    {
        var runtimeTicket = ReadFromPath(_ticketPath);
        var handoffTicket = ReadFromPath(_handoffTicketPath);
        var candidates = new[]
            {
                new { Ticket = runtimeTicket, Path = _ticketPath, Priority = 1 },
                new { Ticket = handoffTicket, Path = _handoffTicketPath, Priority = 0 },
            }
            .Where(candidate => candidate.Ticket is not null && !IsConsumed(candidate.Ticket.ResumeToken))
            .OrderByDescending(candidate => candidate.Ticket!.IssuedAtUtc)
            .ThenByDescending(candidate => candidate.Priority)
            .ToArray();
        if (candidates.Length == 0)
        {
            _currentTicketPath = null;
            return null;
        }

        _currentTicketPath = candidates[0].Path;
        return candidates[0].Ticket;
    }

    private bool IsConsumed(string resumeToken)
    {
        if (string.IsNullOrWhiteSpace(resumeToken))
        {
            return false;
        }

        var tokenHash = HashToken(resumeToken);
        var handoffDirectory = Path.GetDirectoryName(_handoffTicketPath);
        return File.Exists(GetReauthorizationAttemptPath(_runtimeDirectory, tokenHash))
            || (!string.IsNullOrWhiteSpace(handoffDirectory)
                && File.Exists(GetReauthorizationAttemptPath(handoffDirectory!, tokenHash)))
            || TombstoneMatches(GetTombstonePath(_runtimeDirectory, tokenHash), tokenHash)
            || (!string.IsNullOrWhiteSpace(handoffDirectory)
                && TombstoneMatches(GetTombstonePath(handoffDirectory!, tokenHash), tokenHash));
    }

    private bool TombstoneMatches(string path, string tokenHash)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var tombstone = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(path));
            if (tombstone is null || tombstone.Version != 1 || !FixedTimeEquals(tombstone.ResumeTokenHash, tokenHash))
                _logger.LogWarning("Spherewright rejected malformed consumption evidence; the token remains fenced");
            return true; // Protected hash-addressed file presence is fail-closed, independent of runtime version.
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is Newtonsoft.Json.JsonException
            || exception is ArgumentException)
        {
            _logger.LogWarning($"Spherewright could not read an owned-world resume consumption tombstone ({exception.GetType().Name})");
            return true;
        }
    }

    private OwnedWorldResumeTicket? ReadFromPath(string path)
    {
        try
        {
            string json;
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                json = reader.ReadToEnd();
            }

            var ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(json);
            if (ticket is null)
            {
                _logger.LogWarning("Spherewright ignored an empty owned-world resume ticket payload");
                return null;
            }

            _logger.LogInfo("Spherewright loaded an owned-world restart-resume ticket from the protected runtime directory");
            return ticket;
        }
        catch (FileNotFoundException)
        {
            _logger.LogInfo("Spherewright found no owned-world resume ticket at a fixed protected path");
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogInfo("Spherewright found no owned-world resume directory at the fixed protected path");
            return null;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is Newtonsoft.Json.JsonException
            || exception is ArgumentException)
        {
            _logger.LogWarning($"Spherewright ignored an unreadable owned-world resume ticket ({exception.GetType().Name})");
            return null;
        }
    }

    private void Persist(OwnedWorldResumeTicket ticket)
    {
        WindowsCurrentUserSecurity.EnsureSecureDirectory(_runtimeDirectory);
        var handoffDirectory = Path.GetDirectoryName(_handoffTicketPath)
            ?? throw new InvalidOperationException("The owned-world handoff directory is unavailable.");
        WindowsCurrentUserSecurity.EnsureSecureDirectory(handoffDirectory);
        var runtimePersisted = TryPersistAtPath(
            _ticketPath,
            _runtimeDirectory,
            ticket,
            "runtime resume-ticket replica");
        var handoffPersisted = TryPersistAtPath(
            _handoffTicketPath,
            handoffDirectory,
            ticket,
            "handoff resume-ticket replica");
        if (!runtimePersisted && !handoffPersisted)
        {
            throw new IOException("No protected owned-world resume ticket replica could be persisted.");
        }

        if (!runtimePersisted || !handoffPersisted)
        {
            _logger.LogWarning("Spherewright armed the resume ticket with one durable replica; startup generation selection will prefer the newest surviving ticket");
        }
    }

    private static void PersistAtPath(
        string destinationPath,
        string directory,
        object payload)
    {
        var temporaryPath = Path.Combine(directory, $".owned-world-resume-{Guid.NewGuid():N}.tmp");
        var bytes = new UTF8Encoding(false).GetBytes(PluginJson.Serialize(payload));
        WindowsCurrentUserSecurity.WriteSecureNewFile(temporaryPath, bytes);
        try
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, null, true);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
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

    private bool TryPersistAtPath(
        string destinationPath,
        string directory,
        object payload,
        string replicaName)
    {
        try
        {
            PersistAtPath(destinationPath, directory, payload);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException)
        {
            _logger.LogWarning($"Spherewright could not persist its {replicaName} ({exception.GetType().Name})");
            return false;
        }
    }

    private static string GetTombstonePath(string directory, string tokenHash) =>
        Path.Combine(directory, $"owned-world-resume-consumed-{tokenHash}.json");

    private static string GetReauthorizationAttemptPath(string directory, string tokenHash) =>
        Path.Combine(directory, $"owned-world-reauthorization-attempt-{tokenHash}.json");

    private static string HashToken(string token)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token ?? string.Empty));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
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

internal sealed class OwnedWorldResumeTicket
{
    public int Version { get; set; }

    public string ResumeToken { get; set; } = string.Empty;

    public string OwnedSaveName { get; set; } = string.Empty;

    public string SourceSessionId { get; set; } = string.Empty;

    public int SourceProcessId { get; set; }

    public string SourceBridgeInstanceId { get; set; } = string.Empty;

    public string GameVersion { get; set; } = string.Empty;

    public int ExpectedPlanetId { get; set; }

    public long MinimumGameTick { get; set; }

    public OwnedWorldGameplayJournalCheckpoint? GameplayJournalCheckpoint { get; set; }

    public string QuarantineActionId { get; set; } = string.Empty;

    public DateTimeOffset IssuedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}

internal sealed class OwnedWorldReauthorizationAttempt
{
    public int Version { get; set; } = 1;
    public string State { get; set; } = "load_may_have_started_reconciliation_required";
    public OwnedWorldResumeTicket Ticket { get; set; } = null!;
    public string ActionId { get; set; } = string.Empty;
    public string ConfirmationDigest { get; set; } = string.Empty;
    public string ProvenanceFingerprint { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
}

internal sealed class OwnedWorldGameplayJournalCheckpoint
{
    public const int CurrentVersion = 1;

    public int Version { get; set; }

    public string JournalId { get; set; } = string.Empty;

    public string TrackingMode { get; set; } = string.Empty;

    public bool HistoricalCoverageComplete { get; set; }

    public long TrackingStartedAtGameTick { get; set; }

    public long MinimumDurableThroughSequence { get; set; }
}

internal sealed class OwnedWorldResumeConsumptionTombstone
{
    public int Version { get; set; }

    public string ResumeTokenHash { get; set; } = string.Empty;

    public string GameVersion { get; set; } = string.Empty;

    public DateTimeOffset ConsumedAtUtc { get; set; }
}
