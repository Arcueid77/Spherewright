using System.ComponentModel;
using System.Text;
using BepInEx.Logging;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Journals;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Security;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

// Same current-user ACL + flush/atomic-replace pattern as Journal/Overseer. Only the
// current registered identity selects this file; no save picker, prefix test or input path.
internal sealed class BlueprintBuildStore
{
    private const int MaximumBuilds = 32;
    private const int MaximumBytes = 4 * 1024 * 1024;
    private readonly string _directory;
    private readonly string _gameVersion;
    private readonly GameSessionTracker _sessions;
    private readonly ManualLogSource _logger;
    private readonly Func<GameplayJournalSnapshot?>? _journalProvider;
    private string? _sessionId;
    private string? _path;
    private BlueprintBuildDocument? _document;
    private bool _failed;

    public BlueprintBuildStore(string runtimeDirectory, string gameVersion, GameSessionTracker sessions, ManualLogSource logger,
        Func<GameplayJournalSnapshot?>? journalProvider = null)
    {
        _directory = Path.Combine(RuntimeDescriptorPublisher.ResolveRuntimeDirectory(runtimeDirectory), "foundry");
        _gameVersion = gameVersion; _sessions = sessions; _logger = logger;
        _journalProvider = journalProvider;
    }

    public bool TryRead(out List<BlueprintBuildState> builds)
    {
        builds = new List<BlueprintBuildState>();
        if (!Attach()) return false;
        builds = Clone(_document!).Builds;
        return true;
    }

    public bool TryPut(BlueprintBuildState state)
    {
        if (!Attach()) return false;
        string? temporaryPath = null;
        try
        {
            state.Validate();
            var proposed = Clone(_document!);
            // Attach proved the exact durable migration and retained every completed receipt.
            // A read never rewrites the file; only the next ordinary durable update stamps the new version.
            proposed.GameVersion = _gameVersion;
            var index = proposed.Builds.FindIndex(b => b.BuildId == state.BuildId);
            if (index >= 0)
            {
                if (proposed.Builds[index].PlanHash != state.PlanHash
                    || proposed.Builds[index].Generation > state.Generation)
                    throw new InvalidDataException("Finite plan identity or generation regressed.");
                proposed.Builds[index] = state;
            }
            else
            {
                if (proposed.Builds.Count >= MaximumBuilds) return false; // No silent history eviction.
                proposed.Builds.Add(state);
            }
            var bytes = new UTF8Encoding(false).GetBytes(PluginJson.Serialize(proposed));
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Finite plan store size limit.");
            WindowsCurrentUserSecurity.EnsureSecureDirectory(_directory);
            temporaryPath = Path.Combine(_directory, ".blueprints-" + Guid.NewGuid().ToString("N") + ".tmp");
            WindowsCurrentUserSecurity.WriteSecureNewFile(temporaryPath, bytes);
            if (File.Exists(_path!)) File.Replace(temporaryPath, _path!, null, true);
            else File.Move(temporaryPath, _path!);
            _document = Clone(proposed); // Only publish after durable replacement, never pending memory.
            return true;
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            _failed = true;
            _logger.LogError("Spherewright finite construction persistence failed (" + exception.GetType().Name + ")");
            return false;
        }
        finally
        {
            try { if (temporaryPath is not null && File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (Exception exception) when (IsStorageError(exception))
            { _logger.LogWarning("Spherewright finite construction temporary-file cleanup failed (" + exception.GetType().Name + ")"); }
        }
    }

    private bool Attach()
    {
        if (!_sessions.IsCurrentSessionOwned || string.IsNullOrEmpty(_sessions.OwnedSaveName)
            || string.IsNullOrEmpty(_sessions.SessionId)) return false;
        if (_sessionId == _sessions.SessionId) return !_failed && _document is not null;
        _sessionId = _sessions.SessionId; _document = null; _path = null; _failed = false;
        try
        {
            WindowsCurrentUserSecurity.EnsureSecureDirectory(_directory);
            var identity = CanonicalStateHash.Combine("spherewright-blueprint-progress-owned-v1", _sessions.OwnedSaveName).Substring(7);
            _path = Path.Combine(_directory, "blueprints-" + identity + ".json");
            if (File.Exists(_path))
            {
                if (new FileInfo(_path).Length > MaximumBytes) throw new InvalidDataException("Finite plan store size limit.");
                _document = PluginJson.DeserializeReplacingCollections<BlueprintBuildDocument>(File.ReadAllText(_path));
                if (_document is null || _document.Version != 1 || _document.IdentityHash != identity
                    || _document.Builds is null
                    || _document.Builds.Count > MaximumBuilds || _document.Builds.Any(b => b is null)
                    || _document.Builds.Select(b => b.BuildId).Distinct().Count() != _document.Builds.Count)
                    throw new InvalidDataException("Finite plan store identity or bound is invalid.");
                foreach (var state in _document.Builds) state.Validate();
                if (_document.GameVersion != _gameVersion
                    && (_document.GameVersion != OwnedWorldVersionCompatibilityPolicy.LatestPatchVersion
                        || _gameVersion != OwnedWorldVersionCompatibilityPolicy.NextPatchVersion
                        || !BlueprintProgressVersionPolicy.CanRetainCompletedHistory(_document.GameVersion, _gameVersion,
                            _sessions.SessionId!, GameplayJournalIdentity.HashOwnedSaveIdentity(_sessions.OwnedSaveName!),
                            _journalProvider?.Invoke(), _document.Builds)))
                    throw new InvalidDataException("Finite progress requires its exact version or proven completed-history adoption.");
            }
            else _document = new BlueprintBuildDocument { IdentityHash = identity, GameVersion = _gameVersion };
            return true;
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            _failed = true; _document = null;
            _logger.LogError("Spherewright finite construction attachment failed (" + exception.GetType().Name + ")");
            return false;
        }
    }

    private static BlueprintBuildDocument Clone(BlueprintBuildDocument value) =>
        PluginJson.DeserializeReplacingCollections<BlueprintBuildDocument>(PluginJson.Serialize(value))!;
    private static bool IsStorageError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException
        || ex is InvalidDataException || ex is Newtonsoft.Json.JsonException || ex is ArgumentException
        || ex is ArithmeticException || ex is Win32Exception || ex is InvalidOperationException;
}

internal sealed class BlueprintBuildDocument
{
    public int Version { get; set; } = 1;
    public string IdentityHash { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public List<BlueprintBuildState> Builds { get; set; } = new List<BlueprintBuildState>();
}
