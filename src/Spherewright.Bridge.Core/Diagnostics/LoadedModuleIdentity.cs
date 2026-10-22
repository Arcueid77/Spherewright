using System.Reflection;

namespace Spherewright.Bridge.Core.Diagnostics;

/// <summary>Identity of the loaded CLR module, without reading its disk path or game state.</summary>
public sealed class LoadedModuleIdentity
{
    public string? Name { get; private set; }
    public string? ModuleVersionId { get; private set; }
    public string? InformationalVersion { get; private set; }
    public string? AssemblyVersion { get; private set; }
    public string? CaptureError { get; private set; }

    public static LoadedModuleIdentity Capture(Assembly assembly)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
        var result = new LoadedModuleIdentity();
        try
        {
            var name = assembly.GetName();
            result.Name = Bounded(name.Name);
            result.AssemblyVersion = Bounded(name.Version?.ToString());
            var mvid = assembly.ManifestModule.ModuleVersionId;
            result.ModuleVersionId = mvid == Guid.Empty ? null : mvid.ToString("D");
            result.InformationalVersion = Bounded(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        }
        catch (Exception error)
        { result.CaptureError = error.GetType().Name; }
        return result;
    }

    public string ToLogFields() => $"name={Name ?? "unknown"} mvid={ModuleVersionId ?? "unknown"}"
        + $" informationalVersion={InformationalVersion ?? "unknown"} assemblyVersion={AssemblyVersion ?? "unknown"}"
        + $" captureError={CaptureError ?? "none"}";

    private static string? Bounded(string? value) => string.IsNullOrWhiteSpace(value) || value!.Length > 256
        || value.IndexOfAny(new[] { '\r', '\n' }) >= 0 ? null : value;
}
