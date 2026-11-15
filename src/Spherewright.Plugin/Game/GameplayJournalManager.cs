using System.Globalization;
using System.Text;
using BepInEx.Logging;
using Spherewright.Bridge.Core.Journals;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Journals;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Security;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

internal sealed class GameplayJournalManager : IDisposable
{
    private const int DocumentVersion = 1;
    private const int ManualRecipeFeatureBase = 2140000;
    private const int LifetimeProductionTotalIndex = 6;
    private readonly string _journalDirectory;
    private readonly string _gameVersion;
    private readonly GameSessionTracker _sessions;
    private readonly ManualLogSource _logger;
    private string? _activeSessionId;
    private string? _activePath;
    private GameplayJournalDocument? _document;
    private GameplayFirstOccurrenceDetector? _detector;
    private long _lastScannedGameTick = -1;
    private long _durableThroughSequence;
    private bool _pendingPersist;
    private string? _persistenceError;

    public GameplayJournalManager(
        string configuredRuntimeDirectory,
        string gameVersion,
        GameSessionTracker sessions,
        ManualLogSource logger)
    {
        var runtimeDirectory = RuntimeDescriptorPublisher.ResolveRuntimeDirectory(configuredRuntimeDirectory);
        _journalDirectory = Path.Combine(runtimeDirectory, "journals");
        _gameVersion = gameVersion;
        _sessions = sessions;
        _logger = logger;
    }

    public void UpdateOnMainThread()
    {
        if (!_sessions.IsCurrentSessionOwned
            || string.IsNullOrWhiteSpace(_sessions.SessionId)
            || string.IsNullOrWhiteSpace(_sessions.OwnedSaveName)
            || GameMain.data is null)
        {
            ResetActiveSession();
            return;
        }

        if (!string.Equals(_activeSessionId, _sessions.SessionId, StringComparison.Ordinal))
        {
            AttachToCurrentOwnedSession();
        }

        if (_document is null || _detector is null)
        {
            return;
        }

        if (_pendingPersist && !TryPersist())
        {
            return;
        }

        var gameTick = GameMain.gameTick;
        if (gameTick == _lastScannedGameTick)
        {
            return;
        }

        _lastScannedGameTick = gameTick;
        var changed = false;
        var manualCounts = CaptureManualProductionCounts();
        foreach (var occurrence in _detector.ObserveManualCounts(manualCounts))
        {
            AddItemEntry(occurrence, "mecha-forge-feature-counter", gameTick);
            changed = true;
        }

        var productionLineCounts = CaptureProductionLineRegisterCounts();
        foreach (var occurrence in _detector.ObserveProductionLineCounts(productionLineCounts))
        {
            AddItemEntry(occurrence, "factory-production-register", gameTick);
            changed = true;
        }

        var history = GameMain.history;
        if (history is not null)
        {
            if (history.currentTech > 0)
            {
                changed |= TryAddResearchEntry(history.currentTech, gameTick);
            }

            if (history.techQueue is not null)
            {
                foreach (var techId in history.techQueue)
                {
                    if (techId > 0)
                    {
                        changed |= TryAddResearchEntry(techId, gameTick);
                    }
                }
            }
        }

        if (changed)
        {
            _pendingPersist = true;
            TryPersist();
        }
    }

    public GameCallResult<GameplayJournalSnapshot> CaptureOnMainThread(string? requestedSessionId)
    {
        UpdateOnMainThread();
        if (!_sessions.GameLoaded)
        {
            return GameCallResult<GameplayJournalSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.GameNotLoaded,
                "No ordinary game is loaded.",
                true,
                "Load the exact owned world and retry."));
        }

        if (!_sessions.IsCurrentSessionOwned)
        {
            return GameCallResult<GameplayJournalSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.SessionNotOwned,
                "Gameplay journals are private to a Spherewright-owned world.",
                false,
                "Resume the exact owned world through its protected provenance flow."));
        }

        if (string.IsNullOrWhiteSpace(requestedSessionId)
            || !string.Equals(requestedSessionId, _sessions.SessionId, StringComparison.Ordinal))
        {
            return GameCallResult<GameplayJournalSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "The requested journal session is stale.",
                true,
                "Read the current session state and retry with its session ID."));
        }

        if (_document is null)
        {
            return GameCallResult<GameplayJournalSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The per-save gameplay journal is unavailable.",
                true,
                _persistenceError is null
                    ? "Retry after the owned world finishes loading."
                    : "Inspect the protected journal directory and Plugin log before continuing milestone work."));
        }

        return GameCallResult<GameplayJournalSnapshot>.Succeeded(new GameplayJournalSnapshot
        {
            OriginGameVersion = _document.GameVersion,
            CurrentGameVersion = _gameVersion,
            VersionTransitions = _document.VersionTransitions.Select(change => new GameplayJournalVersionTransition
            {
                FromGameVersion = change.FromGameVersion, ToGameVersion = change.ToGameVersion,
                AdoptedAtGameTick = change.AdoptedAtGameTick, DurableThroughSequence = change.DurableThroughSequence,
                RecordedAtUtc = change.RecordedAtUtc,
            }).ToList(),
            SessionId = _sessions.SessionId!,
            JournalId = _document.JournalId,
            TrackingMode = _document.TrackingMode,
            HistoricalCoverageComplete = _document.HistoricalCoverageComplete,
            CreatedAtActualTime = _document.CreatedAtActualTime,
            TrackingStartedAtGameTick = _document.TrackingStartedAtGameTick,
            TrackingStartedAtGameTime = _document.TrackingStartedAtGameTime,
            CapturedAtGameTick = GameMain.gameTick,
            DurableThroughSequence = _durableThroughSequence,
            PersistencePending = _pendingPersist,
            PersistenceError = _persistenceError,
            Entries = _document.Entries.Select(CloneEntry).ToList(),
        });
    }

    public OwnedWorldGameplayJournalCheckpoint CaptureResumeCheckpointOnMainThread()
    {
        UpdateOnMainThread();
        return CaptureDurableCheckpointOnMainThread();
    }

    // No UpdateOnMainThread here: prepare must not scan or persist a pending
    // Journal. A later frame may make it quiescent without granting recovery.
    internal OwnedWorldGameplayJournalCheckpoint CaptureQuiescentSurfaceCheckpointOnMainThread()
    {
        var sequences = _document?.Entries is { } entries && !entries.Any(entry => entry is null)
            ? entries.Select(entry => entry.Sequence).ToArray() : null;
        if (!GameplayJournalContinuityPolicy.QuiescentCheckpointReady(_sessions.IsCurrentSessionOwned,
            !string.IsNullOrWhiteSpace(_sessions.SessionId) && _activeSessionId == _sessions.SessionId,
            GameMain.isPaused, GameMain.isRunning, GameMain.gameTick, _lastScannedGameTick,
            _pendingPersist, _persistenceError, _durableThroughSequence, sequences))
            throw new InvalidOperationException("same_session_surface_live_journal_not_quiescent");
        return CaptureDurableCheckpointOnMainThread();
    }

    private OwnedWorldGameplayJournalCheckpoint CaptureDurableCheckpointOnMainThread()
    {
        if (!_sessions.IsCurrentSessionOwned
            || string.IsNullOrWhiteSpace(_sessions.SessionId)
            || !string.Equals(_activeSessionId, _sessions.SessionId, StringComparison.Ordinal)
            || _document is null
            || _document.Entries is null
            || _document.Entries.Any(entry => entry is null)
            || _pendingPersist
            || !string.IsNullOrWhiteSpace(_persistenceError))
        {
            throw new InvalidOperationException("The protected gameplay journal is not durably ready for restart-resume.");
        }

        var sequences = _document.Entries.Select(entry => entry.Sequence).ToArray();
        var durableThroughSequence = sequences.Length == 0 ? 0L : sequences[sequences.Length - 1];
        if (_durableThroughSequence != durableThroughSequence
            || !GameplayJournalContinuityPolicy.HasContinuousSequence(sequences))
        {
            throw new InvalidDataException("The protected gameplay journal does not have one continuous durable sequence.");
        }

        return new OwnedWorldGameplayJournalCheckpoint
        {
            Version = OwnedWorldGameplayJournalCheckpoint.CurrentVersion,
            JournalId = _document.JournalId,
            TrackingMode = _document.TrackingMode,
            HistoricalCoverageComplete = _document.HistoricalCoverageComplete,
            TrackingStartedAtGameTick = _document.TrackingStartedAtGameTick,
            MinimumDurableThroughSequence = _durableThroughSequence,
        };
    }

    public void Dispose()
    {
        if (_pendingPersist)
        {
            TryPersist();
        }

        ResetActiveSession();
    }

    private void AttachToCurrentOwnedSession()
    {
        ResetActiveSession();
        var sessionId = _sessions.SessionId!;
        var identityHash = GameplayJournalIdentity.HashOwnedSaveIdentity(_sessions.OwnedSaveName!);
        var journalId = identityHash;
        var path = Path.Combine(_journalDirectory, $"gameplay-{journalId}.json");
        var expectedResumeCheckpoint = _sessions.PendingResumeGameplayJournalCheckpoint;
        var sourceGameVersion = _sessions.PendingResumeSourceGameVersion ?? _gameVersion;
        var migrating = sourceGameVersion != _gameVersion;
        try
        {
            WindowsCurrentUserSecurity.EnsureSecureDirectory(_journalDirectory);
            GameplayJournalDocument? document = null;
            if (File.Exists(path))
            {
                document = PluginJson.Deserialize<GameplayJournalDocument>(File.ReadAllText(path));
                if (document is null
                    || document.Version != DocumentVersion
                    || !string.Equals(document.JournalId, journalId, StringComparison.Ordinal)
                    || !string.Equals(document.OwnedSaveIdentityHash, identityHash, StringComparison.Ordinal)
                    || !OwnedWorldVersionCompatibilityPolicy.JournalMatches(document.GameVersion,
                        document.VersionTransitions, document.Entries?.Count ?? -1, sourceGameVersion)
                    || document.Entries is null
                    || document.Entries.Any(entry => entry is null)
                    || !GameplayJournalContinuityPolicy.HasContinuousSequence(
                        document.Entries.Select(entry => entry.Sequence).ToArray()))
                {
                    throw new InvalidDataException("The protected gameplay journal identity did not match the owned save.");
                }
            }

            if (expectedResumeCheckpoint is not null
                && (document is null
                    || !GameplayJournalContinuityPolicy.MatchesCheckpoint(
                        expectedResumeCheckpoint.JournalId,
                        expectedResumeCheckpoint.TrackingMode,
                        expectedResumeCheckpoint.HistoricalCoverageComplete,
                        expectedResumeCheckpoint.TrackingStartedAtGameTick,
                        expectedResumeCheckpoint.MinimumDurableThroughSequence,
                        document.JournalId,
                        document.TrackingMode,
                        document.HistoricalCoverageComplete,
                        document.TrackingStartedAtGameTick,
                        document.Entries.Select(entry => entry.Sequence).ToArray())))
            {
                throw new InvalidDataException("The protected gameplay journal is missing or older than the resume ticket checkpoint.");
            }

            if (document is null)
            {
                document = CreateDocument(journalId, identityHash);
            }

            if (migrating)
            {
                if (expectedResumeCheckpoint is null || !_sessions.CanMigratePendingResumeJournal(sourceGameVersion)
                    || document.Entries.Count != expectedResumeCheckpoint.MinimumDurableThroughSequence)
                    throw new InvalidDataException("The exact journal cannot prove the authorized version transition.");
                var transition = new GameplayJournalVersionTransition
                {
                    FromGameVersion = sourceGameVersion, ToGameVersion = _gameVersion,
                    AdoptedAtGameTick = GameMain.gameTick,
                    DurableThroughSequence = expectedResumeCheckpoint.MinimumDurableThroughSequence,
                    RecordedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                };
                if (!OwnedWorldVersionCompatibilityPolicy.CanAppendTransition(document.GameVersion,
                    document.VersionTransitions, document.Entries.Count, sourceGameVersion, _gameVersion, transition))
                    throw new InvalidDataException("The exact journal cannot prove the authorized version transition.");
                document.VersionTransitions.Add(transition);
            }

            _activeSessionId = sessionId;
            _activePath = path;
            _document = document;
            _detector = CreateDetector(document);
            _lastScannedGameTick = -1;
            var existedOnDisk = File.Exists(path);
            _durableThroughSequence = existedOnDisk
                ? document.Entries.Select(entry => entry.Sequence).DefaultIfEmpty(0L).Max()
                : 0L;
            _pendingPersist = !existedOnDisk || migrating;
            _persistenceError = null;
            if (migrating)
                _sessions.ReleaseJournalLeaseForVersionMigration(expectedResumeCheckpoint!, sourceGameVersion);
            if (_pendingPersist && !TryPersist())
            {
                if (migrating) throw new IOException("The version transition could not be persisted; primary saving remains forbidden.");
                return;
            }

            if (expectedResumeCheckpoint is not null)
            {
                _sessions.ConfirmResumeGameplayJournalContinuityOnMainThread(expectedResumeCheckpoint);
            }

            _logger.LogInfo("Spherewright attached the protected per-save gameplay journal");
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is InvalidDataException
            || exception is Newtonsoft.Json.JsonException
            || exception is ArgumentException)
        {
            _activeSessionId = sessionId;
            _activePath = null;
            _document = null;
            _detector = null;
            _persistenceError = exception.GetType().Name;
            if (expectedResumeCheckpoint is not null)
            {
                _sessions.RejectResumeGameplayJournalContinuityOnMainThread(
                    "The protected per-save gameplay journal could not prove the resume ticket's durable checkpoint. Restore the exact journal backup before retrying this ticket.");
            }
            _logger.LogError($"Spherewright gameplay journal attachment failed ({_persistenceError})");
        }
    }

    private GameplayJournalDocument CreateDocument(string journalId, string identityHash)
    {
        var fromNewGame = _sessions.CurrentOwnedSessionStartedAsNewGame;
        var gameTick = GameMain.gameTick;
        var document = new GameplayJournalDocument
        {
            Version = DocumentVersion,
            JournalId = journalId,
            OwnedSaveIdentityHash = identityHash,
            GameVersion = _gameVersion,
            TrackingMode = fromNewGame
                ? GameplayJournalTrackingModes.FromNewGame
                : GameplayJournalTrackingModes.AttachedExistingSave,
            HistoricalCoverageComplete = fromNewGame,
            CreatedAtActualTime = FormatActualTime(DateTimeOffset.Now),
            TrackingStartedAtGameTick = gameTick,
            TrackingStartedAtGameTime = FormatGameTime(gameTick),
        };

        if (!fromNewGame)
        {
            var manualCounts = CaptureManualProductionCounts();
            document.HistoricalManualItemIds = manualCounts
                .Where(pair => pair.Value > 0)
                .Select(pair => pair.Key)
                .Distinct()
                .OrderBy(itemId => itemId)
                .ToList();

            var lifetimeProduction = CaptureLifetimeProductionCounts();
            document.HistoricalProductionLineItemIds = lifetimeProduction
                .Where(pair => pair.Value - GetCount(manualCounts, pair.Key) > 0)
                .Select(pair => pair.Key)
                .Distinct()
                .OrderBy(itemId => itemId)
                .ToList();

            document.HistoricalResearchIds = CaptureHistoricalResearchIds();
        }

        return document;
    }

    private static GameplayFirstOccurrenceDetector CreateDetector(GameplayJournalDocument document)
    {
        var recordedManual = document.Entries
            .Where(entry => string.Equals(entry.Kind, GameplayJournalEventKinds.ManualItemFirst, StringComparison.Ordinal))
            .Select(entry => entry.ItemId);
        var recordedProduction = document.Entries
            .Where(entry => string.Equals(entry.Kind, GameplayJournalEventKinds.ProductionLineItemFirst, StringComparison.Ordinal))
            .Select(entry => entry.ItemId);
        var recordedResearch = document.Entries
            .Where(entry => string.Equals(entry.Kind, GameplayJournalEventKinds.TechnologyFirstSelected, StringComparison.Ordinal)
                || string.Equals(entry.Kind, GameplayJournalEventKinds.UpgradeFirstSelected, StringComparison.Ordinal))
            .Select(entry => entry.TechId);
        return new GameplayFirstOccurrenceDetector(
            document.HistoricalManualItemIds.Concat(recordedManual),
            document.HistoricalProductionLineItemIds.Concat(recordedProduction),
            document.HistoricalResearchIds.Concat(recordedResearch));
    }

    private void AddItemEntry(GameplayItemFirstOccurrence occurrence, string source, long gameTick)
    {
        _document!.Entries.Add(new GameplayJournalEntry
        {
            Sequence = _document.Entries.Count + 1L,
            Kind = occurrence.Kind,
            ItemId = occurrence.ItemId,
            Name = LDB.items.Select(occurrence.ItemId)?.name ?? string.Empty,
            ObservedCount = occurrence.ObservedCount,
            ActualTime = FormatActualTime(DateTimeOffset.Now),
            GameTick = gameTick,
            GameTime = FormatGameTime(gameTick),
            Source = source,
        });
    }

    private bool TryAddResearchEntry(int techId, long gameTick)
    {
        if (!_detector!.TryObserveResearchSelection(techId))
        {
            return false;
        }

        var tech = LDB.techs.Select(techId);
        if (tech is null)
        {
            return false;
        }

        _document!.Entries.Add(new GameplayJournalEntry
        {
            Sequence = _document.Entries.Count + 1L,
            Kind = tech.page == 0
                ? GameplayJournalEventKinds.TechnologyFirstSelected
                : GameplayJournalEventKinds.UpgradeFirstSelected,
            TechId = techId,
            Name = tech.name ?? string.Empty,
            ObservedCount = 1,
            ActualTime = FormatActualTime(DateTimeOffset.Now),
            GameTick = gameTick,
            GameTime = FormatGameTime(gameTick),
            Source = "normal-research-queue",
        });
        return true;
    }

    private static Dictionary<int, long> CaptureManualProductionCounts()
    {
        var result = new Dictionary<int, long>();
        var history = GameMain.history;
        if (history is null || LDB.recipes?.dataArray is null)
        {
            return result;
        }

        foreach (var recipe in LDB.recipes.dataArray)
        {
            if (recipe is null || recipe.ID <= 0 || !recipe.Handcraft)
            {
                continue;
            }

            var completedCycles = history.GetFeatureValue(ManualRecipeFeatureBase + recipe.ID);
            if (completedCycles <= 0 || recipe.Results is null || recipe.ResultCounts is null)
            {
                continue;
            }

            var outputCount = Math.Min(recipe.Results.Length, recipe.ResultCounts.Length);
            for (var index = 0; index < outputCount; index++)
            {
                var itemId = recipe.Results[index];
                var count = checked((long)completedCycles * recipe.ResultCounts[index]);
                if (itemId > 0 && count > 0)
                {
                    AddCount(result, itemId, count);
                }
            }
        }

        return result;
    }

    private static Dictionary<int, long> CaptureProductionLineRegisterCounts()
    {
        var result = new Dictionary<int, long>();
        var factoryStats = GameMain.data?.statistics?.production?.factoryStatPool;
        if (factoryStats is null)
        {
            return result;
        }

        foreach (var factoryStat in factoryStats)
        {
            var register = factoryStat?.productRegister;
            if (register is null || LDB.items?.dataArray is null)
            {
                continue;
            }

            foreach (var item in LDB.items.dataArray)
            {
                var itemId = item?.ID ?? 0;
                if (itemId <= 0 || itemId >= register.Length)
                {
                    continue;
                }

                if (register[itemId] > 0)
                {
                    AddCount(result, itemId, register[itemId]);
                }
            }
        }

        return result;
    }

    private static Dictionary<int, long> CaptureLifetimeProductionCounts()
    {
        var result = new Dictionary<int, long>();
        var factoryStats = GameMain.data?.statistics?.production?.factoryStatPool;
        if (factoryStats is null)
        {
            return result;
        }

        foreach (var factoryStat in factoryStats)
        {
            var productPool = factoryStat?.productPool;
            if (productPool is null)
            {
                continue;
            }

            foreach (var product in productPool)
            {
                if (product?.itemId > 0
                    && product.total is not null
                    && product.total.Length > LifetimeProductionTotalIndex
                    && product.total[LifetimeProductionTotalIndex] > 0)
                {
                    AddCount(result, product.itemId, product.total[LifetimeProductionTotalIndex]);
                }
            }
        }

        return result;
    }

    private static List<int> CaptureHistoricalResearchIds()
    {
        var result = new HashSet<int>();
        var history = GameMain.history;
        if (history?.techStates is not null)
        {
            foreach (var pair in history.techStates)
            {
                var state = pair.Value;
                if (pair.Key > 0
                    && (state.unlocked || state.hashUploaded > 0 || state.unlockTick > 0))
                {
                    result.Add(pair.Key);
                }
            }
        }

        if (history?.currentTech > 0)
        {
            result.Add(history.currentTech);
        }

        if (history?.techQueue is not null)
        {
            foreach (var techId in history.techQueue)
            {
                if (techId > 0)
                {
                    result.Add(techId);
                }
            }
        }

        return result.OrderBy(techId => techId).ToList();
    }

    private bool TryPersist()
    {
        if (_document is null || string.IsNullOrWhiteSpace(_activePath))
        {
            return false;
        }

        var temporaryPath = Path.Combine(
            _journalDirectory,
            $".gameplay-{Guid.NewGuid():N}.tmp");
        try
        {
            var content = new UTF8Encoding(false).GetBytes(PluginJson.Serialize(_document));
            WindowsCurrentUserSecurity.WriteSecureNewFile(temporaryPath, content);
            if (File.Exists(_activePath))
            {
                File.Replace(temporaryPath, _activePath, null, true);
            }
            else
            {
                File.Move(temporaryPath, _activePath);
            }

            _pendingPersist = false;
            _persistenceError = null;
            _durableThroughSequence = _document.Entries
                .Select(entry => entry.Sequence)
                .DefaultIfEmpty(0L)
                .Max();
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException)
        {
            _pendingPersist = true;
            _persistenceError = exception.GetType().Name;
            _logger.LogError($"Spherewright gameplay journal persistence failed ({_persistenceError})");
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                _logger.LogWarning($"Spherewright could not remove a journal temporary file ({exception.GetType().Name})");
            }
        }
    }

    private void ResetActiveSession()
    {
        _activeSessionId = null;
        _activePath = null;
        _document = null;
        _detector = null;
        _lastScannedGameTick = -1;
        _durableThroughSequence = 0L;
        _pendingPersist = false;
        _persistenceError = null;
    }

    private static void AddCount(IDictionary<int, long> counts, int itemId, long count)
    {
        var existing = counts.TryGetValue(itemId, out var current) ? current : 0L;
        counts[itemId] = checked(existing + count);
    }

    private static long GetCount(IReadOnlyDictionary<int, long> counts, int itemId)
    {
        return counts.TryGetValue(itemId, out var count) ? count : 0L;
    }

    private static string FormatActualTime(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string FormatGameTime(long gameTick)
    {
        var totalSeconds = Math.Max(0L, gameTick) / 60L;
        var days = totalSeconds / 86400L;
        var hours = totalSeconds % 86400L / 3600L;
        var minutes = totalSeconds % 3600L / 60L;
        var seconds = totalSeconds % 60L;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:D3}d {1:D2}:{2:D2}:{3:D2}",
            days,
            hours,
            minutes,
            seconds);
    }

    private static GameplayJournalEntry CloneEntry(GameplayJournalEntry entry)
    {
        return new GameplayJournalEntry
        {
            Sequence = entry.Sequence,
            Kind = entry.Kind,
            ItemId = entry.ItemId,
            TechId = entry.TechId,
            Name = entry.Name,
            ObservedCount = entry.ObservedCount,
            ActualTime = entry.ActualTime,
            GameTick = entry.GameTick,
            GameTime = entry.GameTime,
            Source = entry.Source,
        };
    }
}

internal sealed class GameplayJournalDocument
{
    public int Version { get; set; }

    public string JournalId { get; set; } = string.Empty;

    public string OwnedSaveIdentityHash { get; set; } = string.Empty;

    public string GameVersion { get; set; } = string.Empty;

    public List<GameplayJournalVersionTransition> VersionTransitions { get; set; } = new List<GameplayJournalVersionTransition>();

    public string TrackingMode { get; set; } = string.Empty;

    public bool HistoricalCoverageComplete { get; set; }

    public string CreatedAtActualTime { get; set; } = string.Empty;

    public long TrackingStartedAtGameTick { get; set; }

    public string TrackingStartedAtGameTime { get; set; } = string.Empty;

    public List<int> HistoricalManualItemIds { get; set; } = new List<int>();

    public List<int> HistoricalProductionLineItemIds { get; set; } = new List<int>();

    public List<int> HistoricalResearchIds { get; set; } = new List<int>();

    public List<GameplayJournalEntry> Entries { get; set; } = new List<GameplayJournalEntry>();
}
