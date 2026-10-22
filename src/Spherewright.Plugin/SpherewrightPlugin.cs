using BepInEx;
using Spherewright.Contracts.Protocol;
using Spherewright.Contracts.Versioning;
using Spherewright.Plugin.Bootstrap;
using Spherewright.Plugin.Hosting;
using Spherewright.Bridge.Core.Diagnostics;

namespace Spherewright.Plugin;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("DSPGAME.exe")]
public sealed class SpherewrightPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "dev.spherewright.bridge";
    public const string PluginName = "Spherewright";
    public const string PluginVersion = SpherewrightProduct.CurrentVersion;

    private SpherewrightBridgeHost? _host;
    private InstallationStartupGuard? _installationGuard;

    private void Awake()
    {
        Logger.LogInfo("Spherewright plugin loaded");
        Logger.LogInfo($"Spherewright plugin version: {PluginVersion}");
        Logger.LogInfo($"Spherewright protocol version: {ProtocolConstants.CurrentVersion}");
        // Actual loaded metadata, not hashes borrowed from the install folder.
        // No Native game/physics call or health assertion is made here.
        foreach (var assembly in new[] { typeof(SpherewrightPlugin).Assembly, typeof(LoadedModuleIdentity).Assembly,
            typeof(ProtocolConstants).Assembly, typeof(GameMain).Assembly })
            Logger.LogInfo("Spherewright loaded module: " + LoadedModuleIdentity.Capture(assembly).ToLogFields());

        try
        {
            _installationGuard = InstallationStartupGuard.Acquire(
                Path.GetDirectoryName(typeof(SpherewrightPlugin).Assembly.Location)
                    ?? throw new InvalidOperationException("Spherewright assembly directory is unavailable."),
                Paths.BepInExRootPath);
            var configuration = SpherewrightConfiguration.Load(Config);
            _host = SpherewrightBridgeHost.Create(configuration, Logger, PluginVersion);
            Logger.LogInfo($"Spherewright writes configured: {(configuration.AllowWrites ? "enabled" : "disabled")}");
            Logger.LogInfo($"Spherewright user-save import configured: {(configuration.AllowUserSaveImport ? "enabled" : "disabled")}");

            if (!configuration.Enabled)
            {
                Logger.LogWarning("Spherewright bridge is disabled by configuration");
                StopHost();
                return;
            }

            _host.Start();
            Logger.LogInfo("Spherewright bridge started");
        }
        catch (Exception exception)
        {
            StopHost();
            Logger.LogError($"Spherewright bridge startup failed: {FormatExceptionChain(exception)}");
            Logger.LogError(exception.ToString());
        }
    }

    private void Update()
    {
        _host?.PumpMainThread();
    }

    private void OnDestroy()
    {
        StopHost();
    }

    private void OnDisable()
    {
        StopHost();
    }

    private void StopHost()
    {
        // Do not release the installer lease until the pipe/descriptor host is gone.
        _host?.Dispose();
        _host = null;
        _installationGuard?.Dispose();
        _installationGuard = null;
    }

    private static string FormatExceptionChain(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join(" -> ", messages);
    }
}
