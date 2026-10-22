using Spherewright.Bridge.Core.Diagnostics;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class LoadedModuleIdentityTests
{
    [Fact]
    public void IdentityComesFromTheLoadedModuleAndDoesNotPublishItsInstallationPath()
    {
        var assembly = typeof(LoadedModuleIdentity).Assembly;
        var identity = LoadedModuleIdentity.Capture(assembly);
        Assert.Equal(assembly.GetName().Name, identity.Name);
        Assert.Equal(assembly.ManifestModule.ModuleVersionId.ToString("D"), identity.ModuleVersionId);
        Assert.Null(identity.CaptureError);
        var fields = identity.ToLogFields();
        Assert.Contains("mvid=" + identity.ModuleVersionId, fields);
        Assert.DoesNotContain(assembly.Location, fields);
        Assert.DoesNotContain(Path.GetDirectoryName(assembly.Location)!, fields);
        Assert.DoesNotContain("healthy", fields, StringComparison.OrdinalIgnoreCase);
    }
}
