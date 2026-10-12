[CmdletBinding()]
param(
    [string]$DotnetPath = 'dotnet',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'SpherewrightPackageSurface.ps1')
$taskAssembly = Join-Path $taskRepo "src/Spherewright.Mcp/bin/$Configuration/net8.0/Spherewright.Mcp.dll"
if (-not (Test-Path -LiteralPath $taskAssembly -PathType Leaf)) { throw 'Build the source MCP cohort first.' }
$taskGuide = Get-Content -LiteralPath (Join-Path $taskRepo 'docs/agent-playbook.md') -Raw
$taskStart = [Diagnostics.ProcessStartInfo]::new()
$taskStart.FileName = $DotnetPath
$taskStart.Arguments = '"' + $taskAssembly + '"'
$taskStart.WorkingDirectory = $taskRepo
$taskStart.UseShellExecute = $false
$taskStart.CreateNoWindow = $true
$taskStart.RedirectStandardInput = $true
$taskStart.RedirectStandardOutput = $true
$taskStart.RedirectStandardError = $true
$taskProcess = [Diagnostics.Process]::new()
$taskProcess.StartInfo = $taskStart
$taskStarted = $false

function Invoke-SourceMetadataRequest {
    param([int]$Id, [string]$Method, [object]$Parameters)
    $taskMessage = @{jsonrpc='2.0'; id=$Id; method=$Method; params=$Parameters} | ConvertTo-Json -Depth 12 -Compress
    $taskProcess.StandardInput.WriteLine($taskMessage)
    $taskProcess.StandardInput.Flush()
    $taskRead = $taskProcess.StandardOutput.ReadLineAsync()
    if (-not $taskRead.Wait(10000)) { throw "Source MCP metadata request timed out: $Method" }
    $taskResponse = $taskRead.Result | ConvertFrom-Json
    if ($taskResponse.id -ne $Id -or -not $taskResponse.PSObject.Properties['result']) { throw "Invalid source MCP protocol response: $Method" }
    return $taskResponse.result
}

try {
    if (-not $taskProcess.Start()) { throw 'Source MCP could not start.' }
    $taskStarted = $true
    $taskErrors = $taskProcess.StandardError.ReadToEndAsync()
    $taskInit = Invoke-SourceMetadataRequest 1 'initialize' @{
        protocolVersion='2025-06-18'; capabilities=@{}; clientInfo=@{name='spherewright-source-metadata'; version='1.0.0'}
    }
    $taskProcess.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $taskTools = @( (Invoke-SourceMetadataRequest 2 'tools/list' @{}).tools )
    $taskResources = @( (Invoke-SourceMetadataRequest 3 'resources/list' @{}).resources )
    $taskMatches = @($taskResources | Where-Object { $_.uri -ceq 'spherewright://agent/playbooks/opening-movement-v1' })
    if ($taskMatches.Count -ne 1) { throw 'Expected exactly one advertised Agent playbook.' }
    $taskReadback = Invoke-SourceMetadataRequest 4 'resources/read' @{uri=$taskMatches[0].uri}
    $taskText = [string]@($taskReadback.contents)[0].text
    Assert-SpherewrightPackageSurface -Version ([version]$taskInit.serverInfo.version) -Tools $taskTools `
        -Resources $taskResources -PackagedPlaybook $taskGuide -ResourcePlaybook $taskText
    $taskProcess.StandardInput.Close()
    $taskExtraOutput = $taskProcess.StandardOutput.ReadToEndAsync()
    if (-not $taskProcess.WaitForExit(5000) -or $taskProcess.ExitCode -ne 0) { throw 'Source MCP did not exit normally after stdin closed.' }
    if (-not $taskExtraOutput.Wait(5000) -or -not [string]::IsNullOrWhiteSpace($taskExtraOutput.Result)) { throw 'Unexpected non-protocol stdout.' }
    if (-not $taskErrors.Wait(5000)) { throw 'Source MCP stderr did not finish draining.' }
    [pscustomobject]@{
        scope='source metadata only; no Bridge/gameplay requests, ZIP or cross-computer validation'
        version=$taskInit.serverInfo.version; tools=$taskTools.Count; resources=$taskResources.Count
        playbookCharacters=$taskText.Length; playbookMatches=$true; exitCode=$taskProcess.ExitCode; extraStdoutCharacters=$taskExtraOutput.Result.Length
    } | ConvertTo-Json
}
finally {
    # This process was created here and is only the source MCP probe, never DSP.
    if ($taskStarted -and -not $taskProcess.HasExited) { $taskProcess.Kill() }
    $taskProcess.Dispose()
}
