using System.Diagnostics;
using System.Text.Json;
using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class DysonStructureHashCompatibilityTests
{
    [Fact]
    public async Task ClassicWindowsFractionalNodePlanPassesTheCurrentRuntimeEcho()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "spherewright-dyson-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var script = Path.Combine(directory, "hash.ps1");
        try
        {
            await File.WriteAllTextAsync(script, """
                param([string]$ContractsPath, [string]$CorePath, [string]$NewtonsoftPath)
                $ErrorActionPreference='Stop'
                Add-Type -Path $ContractsPath
                Add-Type -Path $CorePath
                Add-Type -Path $NewtonsoftPath
                $structure=New-Object Spherewright.Contracts.Celestial.DysonStructureRequest
                $structure.Kind='node'; $structure.LayerId=1
                $structure.LocalPosition=New-Object Spherewright.Contracts.Factory.Vector3Snapshot
                $structure.LocalPosition.X=[single]7956.1753
                $structure.LocalPosition.Z=[single]836.2277
                $plan=New-Object Spherewright.Contracts.Celestial.DysonStructurePlan
                $plan.EvidenceVersion=1; $plan.SessionId='synthetic'; $plan.PlanetId=104; $plan.StarId=1
                $plan.BeforeDesignStateHash='synthetic-design-hash'
                $plan.NativeOperation='DysonSphereLayer.NewDysonNode'; $plan.PlannedObjectId=4
                $plan.UnlockedNodeLatitudeDegrees=15; $plan.NativeChecksComplete=$true
                $plan.RequiredStructurePoints=30; $plan.Structure=$structure
                $plan.BindingHash=[Spherewright.Bridge.Core.Celestial.DysonStructurePolicy]::BindingHash($plan,$null)
                $prepared=New-Object Spherewright.Contracts.Actions.PreparedNormalAction
                $prepared.Prepared=$true; $prepared.CommitAllowedNow=$true; $prepared.PlanToken='synthetic-token'
                $prepared.ActionKind='configure-building'; $prepared.ExpectedStateHash=$plan.BindingHash
                $prepared.PlannedDysonStructure=$plan
                [Console]::WriteLine([Newtonsoft.Json.JsonConvert]::SerializeObject($prepared))
                """);
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", script,
                "-ContractsPath", typeof(DysonStructurePlan).Assembly.Location,
                "-CorePath", typeof(DysonStructurePolicy).Assembly.Location,
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
            var request = new PrepareConfigureBuildingRequest
            {
                PlanetId = 104, Mode = BuildingConfigurationModes.DysonStructure,
                ExpectedFactoryStateHash = "synthetic-design-hash",
                DysonStructure = new DysonStructureRequest
                {
                    Kind = DysonStructureKinds.Node, LayerId = 1,
                    LocalPosition = new Vector3Snapshot { X = 7956.1753f, Z = 836.2277f },
                },
            };
            Assert.Equal("sha256:ab8fd46b0aa0fc57b943b8a5b607c2f8cea9493ee501f390074ab8735c753bba",
                prepared!.PlannedDysonStructure!.BindingHash);
            Assert.True(DysonStructurePolicy.ConfirmsPreparedEcho("synthetic", request, prepared));
        }
        finally
        {
            if (File.Exists(script)) File.Delete(script);
            Directory.Delete(directory);
        }
    }
}
