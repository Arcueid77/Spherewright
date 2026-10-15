[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath,
    [ValidateRange(1, 1000)]
    [int]$ExpectedMinimumToolCount = 1,
    [switch]$RequireBilingualGuides
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
. (Join-Path $PSScriptRoot 'SpherewrightPackageSurface.ps1')
$resolvedPackage = [IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $resolvedPackage -PathType Leaf)) {
    throw "Release package not found: $resolvedPackage"
}

$checksumPath = "$resolvedPackage.sha256"
if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
    throw "Release checksum sidecar not found: $checksumPath"
}
$checksumLine = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
$expectedZipHash = ($checksumLine -split '\s+')[0]
$actualZipHash = (Get-FileHash -LiteralPath $resolvedPackage -Algorithm SHA256).Hash
if (-not [string]::Equals($actualZipHash, $expectedZipHash, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release zip SHA-256 does not match its sidecar.'
}

$smokeParent = [IO.Path]::GetFullPath((Join-Path $repoRoot '.local\release-smoke'))
New-Item -ItemType Directory -Path $smokeParent -Force | Out-Null
$smokeRoot = Join-Path $smokeParent ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot | Out-Null
$process = $null

try {
    Expand-Archive -LiteralPath $resolvedPackage -DestinationPath $smokeRoot
    $topLevel = @(Get-ChildItem -LiteralPath $smokeRoot -Directory)
    if ($topLevel.Count -ne 1) {
        throw 'Release zip must contain exactly one top-level package directory.'
    }
    $packageRoot = $topLevel[0].FullName
    $manifestPath = Join-Path $packageRoot 'manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($RequireBilingualGuides) {
        foreach ($guide in @('release-installation.zh-CN.md', 'installation-recovery.zh-CN.md', 'agent-playbook.zh-CN.md', 'LICENSE.zh-CN.md')) {
            $guidePath = Join-Path $packageRoot $guide
            if (-not (Test-Path -LiteralPath $guidePath -PathType Leaf) -or
                [IO.File]::ReadAllText($guidePath) -notmatch '[\p{IsCJKUnifiedIdeographs}]' -or
                @($manifest.files | Where-Object { $_.path -ceq $guide }).Count -ne 1) {
                throw "A required complete Chinese guide or its integrity entry is missing: $guide"
            }
        }
        foreach ($markdown in Get-ChildItem -LiteralPath $packageRoot -File -Filter '*.md') {
            $fence = $false
            foreach ($line in ([IO.File]::ReadAllText($markdown.FullName) -split '\r?\n')) {
                if ($line -match '^\s*(?:```|~~~)') { $fence = -not $fence; continue }
                if ($fence) { continue }
                foreach ($link in [regex]::Matches($line, '\]\(([^)\r\n]+)\)')) {
                    $href = $link.Groups[1].Value.Trim().Trim('<','>')
                    if ($href -match '^(?:[A-Za-z][A-Za-z0-9+.-]*:|#)') { continue }
                    $relative = [Uri]::UnescapeDataString(($href -split '[#?]',2)[0])
                    $target = [IO.Path]::GetFullPath((Join-Path $packageRoot $relative))
                    if (-not $target.StartsWith($packageRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
                        -not (Test-Path -LiteralPath $target)) { throw "Broken or escaping packaged documentation link in $($markdown.Name)." }
                }
            }
        }
    }
    if (-not [string]::Equals([string]$manifest.productVersion, [string]$manifest.version, [StringComparison]::Ordinal)) {
        throw 'The release manifest product version does not match its package version.'
    }
    foreach ($entry in @($manifest.files)) {
        $relativePath = [string]$entry.path
        if ([string]::IsNullOrWhiteSpace($relativePath) -or [IO.Path]::IsPathRooted($relativePath)) {
            throw 'The release manifest contains an unsafe file path.'
        }
        $candidate = [IO.Path]::GetFullPath((Join-Path $packageRoot ($relativePath -replace '/', [IO.Path]::DirectorySeparatorChar)))
        $packagePrefix = $packageRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $candidate.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The release manifest contains a path outside the package.'
        }
        $actualFileHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
        if (-not [string]::Equals($actualFileHash, [string]$entry.sha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Release file SHA-256 mismatch: $relativePath"
        }
    }

    $executable = Join-Path $packageRoot 'mcp\Spherewright.Mcp.exe'
    $packagedPlaybookPath = Join-Path $packageRoot 'AGENT-PLAYBOOK.md'
    if (-not (Test-Path -LiteralPath $packagedPlaybookPath -PathType Leaf)) {
        throw 'The packaged Agent playbook is missing.'
    }
    $packagedPlaybook = Get-Content -LiteralPath $packagedPlaybookPath -Raw
    if ($packagedPlaybook -notmatch 'do not submit the same target again' `
        -or $packagedPlaybook -notmatch 'four targets' `
        -or $packagedPlaybook -notmatch 'each direction \*\*once\*\*' `
        -or $packagedPlaybook -notmatch 'host timeout is not completion' `
        -or $packagedPlaybook -notmatch 'Do not declare a production line complete' `
        -or $packagedPlaybook -notmatch 'recovery_required') {
        throw 'The packaged Agent playbook is missing required core-operation rules.'
    }
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executable
    $startInfo.WorkingDirectory = Split-Path -Parent $executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'The packaged MCP executable did not start.'
    }
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $initialize = [ordered]@{
        jsonrpc = '2.0'
        id = 1
        method = 'initialize'
        params = [ordered]@{
            protocolVersion = '2025-06-18'
            capabilities = @{}
            clientInfo = [ordered]@{
                name = 'spherewright-release-smoke'
                version = '0.1.0'
            }
        }
    } | ConvertTo-Json -Depth 8 -Compress
    $process.StandardInput.WriteLine($initialize)
    $process.StandardInput.Flush()
    $initializeTask = $process.StandardOutput.ReadLineAsync()
    if (-not $initializeTask.Wait(10000)) {
        throw 'The packaged MCP initialize request timed out.'
    }
    $initializeResponse = $initializeTask.Result | ConvertFrom-Json
    if ([int]$initializeResponse.id -ne 1 -or -not $initializeResponse.result.serverInfo) {
        throw 'The packaged MCP returned an invalid initialize response.'
    }
    $serverVersion = [Version]([string]$initializeResponse.result.serverInfo.version)
    $serverProductVersion = "$($serverVersion.Major).$($serverVersion.Minor).$($serverVersion.Build)"
    $manifestVersionCore = ([string]$manifest.productVersion -split '-', 2)[0]
    if (-not [string]::Equals($serverProductVersion, $manifestVersionCore, [StringComparison]::Ordinal)) {
        throw "The packaged MCP version $serverProductVersion does not match product version core $manifestVersionCore."
    }

    $initialized = @{ jsonrpc = '2.0'; method = 'notifications/initialized' } | ConvertTo-Json -Compress
    $listTools = @{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} } | ConvertTo-Json -Depth 5 -Compress
    $process.StandardInput.WriteLine($initialized)
    $process.StandardInput.WriteLine($listTools)
    $process.StandardInput.Flush()
    $listTask = $process.StandardOutput.ReadLineAsync()
    if (-not $listTask.Wait(10000)) {
        throw 'The packaged MCP tools/list request timed out.'
    }
    $listResponse = $listTask.Result | ConvertFrom-Json
    $tools = @($listResponse.result.tools)
    if ($tools.Count -lt $ExpectedMinimumToolCount) {
        throw "The packaged MCP exposed only $($tools.Count) tools; expected at least $ExpectedMinimumToolCount."
    }

    $listResources = @{ jsonrpc = '2.0'; id = 3; method = 'resources/list'; params = @{} } | ConvertTo-Json -Depth 5 -Compress
    $process.StandardInput.WriteLine($listResources)
    $process.StandardInput.Flush()
    $resourceListTask = $process.StandardOutput.ReadLineAsync()
    if (-not $resourceListTask.Wait(10000)) {
        throw 'The packaged MCP resources/list request timed out.'
    }
    $resourceListResponse = $resourceListTask.Result | ConvertFrom-Json
    $playbookResource = @($resourceListResponse.result.resources) |
        Where-Object { $_.uri -eq 'spherewright://agent/playbooks/opening-movement-v1' } |
        Select-Object -First 1
    if (-not $playbookResource) {
        throw 'The packaged MCP did not advertise the opening-movement Agent playbook resource.'
    }

    $readResource = @{
        jsonrpc = '2.0'
        id = 4
        method = 'resources/read'
        params = @{ uri = [string]$playbookResource.uri }
    } | ConvertTo-Json -Depth 5 -Compress
    $process.StandardInput.WriteLine($readResource)
    $process.StandardInput.Flush()
    $resourceReadTask = $process.StandardOutput.ReadLineAsync()
    if (-not $resourceReadTask.Wait(10000)) {
        throw 'The packaged MCP resources/read request timed out.'
    }
    $resourceReadResponse = $resourceReadTask.Result | ConvertFrom-Json
    $resourceText = [string]@($resourceReadResponse.result.contents)[0].text
    if ($resourceText -notmatch 'do not submit the same target again' `
        -or $resourceText -notmatch 'about \*\*5 m\*\*' `
        -or $resourceText -notmatch 'four targets' `
        -or $resourceText -notmatch 'prepare_harvest' `
        -or $resourceText -notmatch 'Do not declare a production line complete' `
        -or $resourceText -notmatch 'recovery_required') {
        throw 'The packaged MCP returned an incomplete core-operation Agent playbook.'
    }

    Assert-SpherewrightPackageSurface -Version $serverVersion -Tools $tools `
        -Resources @($resourceListResponse.result.resources) -PackagedPlaybook $packagedPlaybook -ResourcePlaybook $resourceText
    $process.StandardInput.Close()
    $remainingStdout = $process.StandardOutput.ReadToEndAsync()
    if (-not $process.WaitForExit(5000)) { throw 'The packaged MCP did not stop after stdin closed.' }
    if ($process.ExitCode -ne 0) { throw "The packaged MCP exited with code $($process.ExitCode)." }
    if (-not $remainingStdout.Wait(5000) -or -not [string]::IsNullOrWhiteSpace($remainingStdout.Result)) {
        throw 'Unexpected stdout after the final protocol response.'
    }
    if (-not $stderrTask.Wait(5000)) { throw 'The packaged MCP stderr did not finish draining.' }

    [pscustomobject]@{
        version = [string]$manifest.version
        productVersion = [string]$manifest.productVersion
        zipSha256 = $actualZipHash.ToLowerInvariant()
        manifestFileCount = @($manifest.files).Count
        protocolVersion = [string]$initializeResponse.result.protocolVersion
        serverName = [string]$initializeResponse.result.serverInfo.name
        serverVersion = [string]$initializeResponse.result.serverInfo.version
        toolCount = $tools.Count
        resourceCount = @($resourceListResponse.result.resources).Count
        hasOpeningMovementPlaybook = $true
        packagedPlaybookBytes = (Get-Item -LiteralPath $packagedPlaybookPath).Length
        hasSessionState = $tools.name -contains 'spherewright_get_session_state'
        hasStationConfiguration = $tools.name -contains 'spherewright_prepare_configure_building'
        packageSurfaceMatchesVersion = $true
        packagedAndEmbeddedPlaybookMatch = $true
        exitCode = $process.ExitCode
        stderrCharacters = $stderrTask.Result.Length
        verified = $true
    } | ConvertTo-Json -Depth 3
} finally {
    if ($process -and -not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit(5000) | Out-Null
    }
    if ($process) {
        $process.Dispose()
    }

    $smokePrefix = $smokeParent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedSmokeRoot = [IO.Path]::GetFullPath($smokeRoot)
    if ($resolvedSmokeRoot.StartsWith($smokePrefix, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedSmokeRoot)) {
        Remove-Item -LiteralPath $resolvedSmokeRoot -Recurse -Force
    }
}
