using System.Diagnostics;
using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeSurfaceNavigationHashCompatibilityTests
{
    private const string ExpectedHash = "sha256:b816fa65beb815a71457a141e48ffb18375b5382fbc59aaaa34306c9a0bebdb1";

    private static NativeSurfaceNavigationPlan Plan() => new()
    {
        PlanetId = 402,
        SessionId = "synthetic-cross-runtime-session",
        PlayerActionStateHash = "synthetic-player-action",
        Target = new() { X = (float)123.456789, Y = (float)-98.7654321, Z = (float)45.6789123 },
        ArcDistanceMetres = 100.123456789123456,
        NativeFlyThresholdMetres = 50,
        NativeSailThresholdMetres = 200,
        ArrivalToleranceMetres = 4,
        MaximumGameTicks = 3600,
        LandingReserveGameTicks = 900,
        EnergyUpperPerTickJoules = 123456.789123456,
        RequiredLoadedEnergyJoules = 123456.789123456 * 3600,
    };

    private static string Hash(NativeSurfaceNavigationPlan plan)
        => NativeSurfaceNavigationPolicy.Hash(plan.SessionId, plan.PlayerActionStateHash, plan);

    [Fact]
    public void CurrentRuntimeMatchesThePortableBindingVector() => Assert.Equal(ExpectedHash, Hash(Plan()));

    [Fact]
    public void JsonSignedZeroNormalizationPreservesTheBinding()
    {
        var plan = Plan();
        plan.Target.X = 0;
        var positive = Hash(plan);
        plan.Target.X = BitConverter.Int32BitsToSingle(int.MinValue);
        plan.ProtectedEnergyFloorJoules = BitConverter.Int64BitsToDouble(long.MinValue);
        Assert.Equal(positive, Hash(plan));
    }

    [Fact]
    public void OneBitEnergyChangesRemainBound()
    {
        var plan = Plan();
        var original = Hash(plan);
        plan.EnergyUpperPerTickJoules = Math.BitIncrement(plan.EnergyUpperPerTickJoules);
        Assert.NotEqual(original, Hash(plan));
    }

    [Fact]
    public async Task ClassicWindowsSerializedPlanPassesTheCurrentRuntimeEcho()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "spherewright-navigation-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var script = Path.Combine(directory, "hash.ps1");
            await File.WriteAllTextAsync(script, """
                param([string]$ContractsPath, [string]$CorePath, [string]$NewtonsoftPath)
                $ErrorActionPreference='Stop'
                Add-Type -Path $ContractsPath
                Add-Type -Path $CorePath
                Add-Type -Path $NewtonsoftPath
                $plan=New-Object Spherewright.Contracts.Actions.NativeSurfaceNavigationPlan
                $plan.PlanetId=402
                $plan.SessionId='synthetic-cross-runtime-session'
                $plan.PlayerActionStateHash='synthetic-player-action'
                $plan.Target.X=[single]123.456789
                $plan.Target.Y=[single]-98.7654321
                $plan.Target.Z=[single]45.6789123
                $plan.ArcDistanceMetres=[double]100.123456789123456
                $plan.NativeFlyThresholdMetres=50
                $plan.NativeSailThresholdMetres=200
                $plan.ArrivalToleranceMetres=4
                $plan.MaximumGameTicks=3600
                $plan.LandingReserveGameTicks=900
                $plan.EnergyUpperPerTickJoules=[double]123456.789123456
                $plan.RequiredLoadedEnergyJoules=$plan.EnergyUpperPerTickJoules*3600
                $plan.BindingHash=[Spherewright.Bridge.Core.Safety.NativeSurfaceNavigationPolicy]::Hash($plan.SessionId,$plan.PlayerActionStateHash,$plan)
                $prepared=New-Object Spherewright.Contracts.Actions.PreparedNormalAction
                $prepared.Prepared=$true
                $prepared.CommitAllowedNow=$true
                $prepared.PlanToken='synthetic-classic-token'
                $prepared.ActionKind='move'
                $prepared.ExpectedStateHash=$plan.BindingHash
                $prepared.NativeSurfaceNavigation=$plan
                [Console]::WriteLine([Newtonsoft.Json.JsonConvert]::SerializeObject($prepared))
                """);
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", script,
                "-ContractsPath", typeof(NativeSurfaceNavigationPlan).Assembly.Location,
                "-CorePath", typeof(NativeSurfaceNavigationPolicy).Assembly.Location,
                "-NewtonsoftPath", Path.Combine(AppContext.BaseDirectory, "classic-runtime", "Newtonsoft.Json.dll") })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("The owned classic-runtime hash probe exceeded its finite timeout.");
            }
            Assert.True(process.ExitCode == 0, await stderr);
            var prepared = JsonSerializer.Deserialize<PreparedNormalAction>(await stdout,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal(ExpectedHash, prepared!.NativeSurfaceNavigation!.BindingHash);
            var plan = Plan();
            var request = new PrepareMoveRequest
            {
                PlanetId = plan.PlanetId,
                UseNativeNavigation = true,
                Target = plan.Target,
                ArrivalTolerance = plan.ArrivalToleranceMetres,
            };
            Assert.True(NativeSurfaceNavigationPolicy.ConfirmsEcho(plan.SessionId, request, prepared));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
