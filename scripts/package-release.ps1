[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [ValidatePattern('^[A-Za-z0-9_]+$')]
    [string]$ThunderstoreTeam = 'Arcueid_77',
    [string]$DspDir,
    [string]$OutputDirectory,
    [string]$DotNetPath,
    [switch]$AllowDirty,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
function Convert-PackagedGuideLinks {
    param([string]$Text, [string]$SourcePath)
    $aliases = @{
        'docs/release-installation.md' = 'INSTALL.md'
        'docs/installation-recovery.md' = 'RECOVERY.md'
        'docs/agent-playbook.md' = 'AGENT-PLAYBOOK.md'
        'docs/release-installation.zh-CN.md' = 'release-installation.zh-CN.md'
        'docs/installation-recovery.zh-CN.md' = 'installation-recovery.zh-CN.md'
        'docs/agent-playbook.zh-CN.md' = 'agent-playbook.zh-CN.md'
        'LICENSE' = 'LICENSE'
        'LICENSE.zh-CN.md' = 'LICENSE.zh-CN.md'
    }
    return [regex]::Replace($Text, '(?<=\]\()(?<href>[^)\r\n]+)(?=\))', [Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $href = $match.Groups['href'].Value
        if ($href -match '^(?:[A-Za-z][A-Za-z0-9+.-]*:|#)') { return $href }
        $parts = [regex]::Match($href, '^(?<path>[^#?]*)(?<suffix>.*)$')
        $sourceDirectory = Split-Path -Parent (Join-Path $repoRoot $SourcePath)
        $resolved = [IO.Path]::GetFullPath((Join-Path $sourceDirectory ([Uri]::UnescapeDataString($parts.Groups['path'].Value))))
        $relative = [IO.Path]::GetRelativePath($repoRoot, $resolved).Replace('\', '/')
        if ($relative.StartsWith('../') -or -not (Test-Path -LiteralPath $resolved)) { throw 'A guide link escapes or is missing from the source repository.' }
        $suffix = $parts.Groups['suffix'].Value
        if ($aliases.ContainsKey($relative)) { return $aliases[$relative] + $suffix }
        $encoded = (($relative -split '/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
        return "https://github.com/Arcueid77/Spherewright/blob/$gitCommit/$encoded$suffix"
    })
}
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts'
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

if (-not $DotNetPath) {
    $localDotNet = Join-Path $repoRoot '.local\dotnet\dotnet.exe'
    $DotNetPath = if (Test-Path -LiteralPath $localDotNet -PathType Leaf) {
        $localDotNet
    } else {
        [string](Get-Command dotnet -ErrorAction Stop).Source
    }
}
$resolvedDotNet = [IO.Path]::GetFullPath($DotNetPath)
if (-not (Test-Path -LiteralPath $resolvedDotNet -PathType Leaf)) {
    throw "The .NET SDK executable was not found: $resolvedDotNet"
}

$gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $gitCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve the source Git commit.'
}
$gitStatus = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=normal)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the source worktree.'
}
$sourceDirty = $gitStatus.Count -gt 0
if ($sourceDirty -and -not $AllowDirty) {
    throw 'Release packaging requires a clean Git worktree. Use -AllowDirty only for a local preview package.'
}

$gameRefs = Join-Path $repoRoot '.local\game-refs\Assembly-CSharp.dll'
if (-not (Test-Path -LiteralPath $gameRefs -PathType Leaf)) {
    $syncScript = Join-Path $PSScriptRoot 'sync-game-refs.ps1'
    if ($DspDir) {
        & $syncScript -DspDir $DspDir | Out-Null
    } else {
        & $syncScript | Out-Null
    }
}

& $resolvedDotNet restore (Join-Path $repoRoot 'Spherewright.sln') --locked-mode --source 'https://api.nuget.org/v3/index.json'
if ($LASTEXITCODE -ne 0) {
    throw 'Locked full-solution restore failed.'
}
# Recompile after a developer's ordinary build: incremental outputs can retain
# CodeView records even when this invocation requests DebugType=None.
& $resolvedDotNet build (Join-Path $repoRoot 'Spherewright.sln') -c Release --no-restore --no-incremental -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw 'Release solution build failed.'
}

$contractsAssemblyPath = Join-Path $repoRoot 'src\Spherewright.Contracts\bin\Release\netstandard2.0\Spherewright.Contracts.dll'
$contractsAssembly = [Reflection.Assembly]::LoadFrom($contractsAssemblyPath)
$productVersionType = $contractsAssembly.GetType('Spherewright.Contracts.Versioning.SpherewrightProduct', $true)
$productVersionField = $productVersionType.GetField('CurrentVersion', [Reflection.BindingFlags]'Public, Static')
$productVersion = [string]$productVersionField.GetRawConstantValue()
if (-not [string]::Equals($productVersion, $Version, [StringComparison]::Ordinal)) {
    throw "Release version $Version does not match the product version $productVersion. Update the single product version source before packaging."
}

$stagingParent = Join-Path $repoRoot '.local\release-package'
New-Item -ItemType Directory -Path $stagingParent -Force | Out-Null
$stagingRoot = Join-Path $stagingParent ([guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $stagingRoot "Spherewright-$Version"
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

try {
    $mcpDirectory = Join-Path $packageRoot 'mcp'
    & $resolvedDotNet publish (Join-Path $repoRoot 'src\Spherewright.Mcp\Spherewright.Mcp.csproj') `
        -c Release -r $Runtime --self-contained true `
        --source 'https://api.nuget.org/v3/index.json' `
        -p:RestoreLockedMode=true -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false `
        -o $mcpDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Self-contained MCP publish failed.'
    }

    $thunderstoreMcpDirectory = Join-Path $stagingRoot 'thunderstore-mcp'
    & $resolvedDotNet publish (Join-Path $repoRoot 'src\Spherewright.Mcp\Spherewright.Mcp.csproj') `
        -c Release -r $Runtime --self-contained true `
        --source 'https://api.nuget.org/v3/index.json' `
        -p:RestoreLockedMode=true -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false `
        -o $thunderstoreMcpDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Single-file MCP publish for Thunderstore failed.'
    }
    $thunderstoreMcpExecutable = Join-Path $thunderstoreMcpDirectory 'Spherewright.Mcp.exe'
    if (-not (Test-Path -LiteralPath $thunderstoreMcpExecutable -PathType Leaf)) {
        throw "Single-file MCP publish did not produce the expected executable: $thunderstoreMcpExecutable"
    }

    $pluginOutput = Join-Path $repoRoot 'src\Spherewright.Plugin\bin\Release\net472'
    $pluginDirectory = Join-Path $packageRoot 'BepInEx\plugins\Spherewright'
    New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
    $pluginFiles = @(
        'Spherewright.Plugin.dll',
        'Spherewright.Contracts.dll',
        'Spherewright.Bridge.Core.dll',
        'Newtonsoft.Json.dll'
    )
    foreach ($name in $pluginFiles) {
        $source = Join-Path $pluginOutput $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Expected Plugin output is missing: $source"
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $pluginDirectory $name)
    }

    Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\install-release.ps1') -Destination (Join-Path $packageRoot 'install.ps1')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\locate-dsp.ps1') -Destination (Join-Path $packageRoot 'locate-dsp.ps1')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\recover-install.ps1') -Destination (Join-Path $packageRoot 'recover-install.ps1')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\Test-SpherewrightStagedMcp.ps1') -Destination (Join-Path $packageRoot 'Test-SpherewrightStagedMcp.ps1')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\SpherewrightInstallTransaction.ps1') -Destination (Join-Path $packageRoot 'SpherewrightInstallTransaction.ps1')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\SpherewrightInstallRecovery.ps1') -Destination (Join-Path $packageRoot 'SpherewrightInstallRecovery.ps1')
    foreach ($guide in @(
        @{source='docs/release-installation.md';target='INSTALL.md'},
        @{source='docs/installation-recovery.md';target='RECOVERY.md'},
        @{source='docs/release-installation.zh-CN.md';target='release-installation.zh-CN.md'},
        @{source='docs/installation-recovery.zh-CN.md';target='installation-recovery.zh-CN.md'},
        @{source='docs/agent-playbook.zh-CN.md';target='agent-playbook.zh-CN.md'}
    )) {
        $text = [IO.File]::ReadAllText((Join-Path $repoRoot $guide.source))
        $packagedText = Convert-PackagedGuideLinks -Text $text -SourcePath $guide.source
        [IO.File]::WriteAllText((Join-Path $packageRoot $guide.target), $packagedText, [Text.UTF8Encoding]::new($false))
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\agent-playbook.md') -Destination (Join-Path $packageRoot 'AGENT-PLAYBOOK.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $packageRoot 'LICENSE')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE.zh-CN.md') -Destination (Join-Path $packageRoot 'LICENSE.zh-CN.md')

    $manifestFiles = @(
        Get-ChildItem -LiteralPath $packageRoot -File -Recurse |
            Sort-Object FullName |
            ForEach-Object {
                $relative = $_.FullName.Substring($packageRoot.Length + 1).Replace([IO.Path]::DirectorySeparatorChar, '/')
                [ordered]@{
                    path = $relative
                    size = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )
    $manifest = [ordered]@{
        schemaVersion = 1
        package = 'Spherewright'
        version = $Version
        productVersion = $productVersion
        runtime = $Runtime
        sourceCommit = $gitCommit
        sourceDirty = $sourceDirty
        supportedDspVersion = '0.10.35.29151'
        supportedBepInExVersion = '5.4.17.0'
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        files = $manifestFiles
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText((Join-Path $packageRoot 'manifest.json'), $manifestJson, [Text.UTF8Encoding]::new($false))

    $zipPath = Join-Path $outputRoot "Spherewright-$Version-$Runtime.zip"
    $checksumPath = "$zipPath.sha256"
    if (((Test-Path -LiteralPath $zipPath) -or (Test-Path -LiteralPath $checksumPath)) -and -not $Force) {
        throw "The versioned release artifact already exists. Choose another version or pass -Force: $zipPath"
    }
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    if (Test-Path -LiteralPath $checksumPath) {
        Remove-Item -LiteralPath $checksumPath -Force
    }
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal

    $verificationRoot = Join-Path $stagingRoot 'verify'
    Expand-Archive -LiteralPath $zipPath -DestinationPath $verificationRoot
    $expandedRoot = Join-Path $verificationRoot "Spherewright-$Version"
    $expandedManifest = Get-Content -LiteralPath (Join-Path $expandedRoot 'manifest.json') -Raw | ConvertFrom-Json
    foreach ($entry in @($expandedManifest.files)) {
        $expandedFile = Join-Path $expandedRoot (([string]$entry.path) -replace '/', [IO.Path]::DirectorySeparatorChar)
        $expandedHash = (Get-FileHash -LiteralPath $expandedFile -Algorithm SHA256).Hash
        if (-not [string]::Equals($expandedHash, [string]$entry.sha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Packaged file verification failed: $($entry.path)"
        }
    }

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($checksumPath, "$zipHash  $([IO.Path]::GetFileName($zipPath))`n", [Text.UTF8Encoding]::new($false))

    & (Join-Path $PSScriptRoot 'test-release-package.ps1') -PackagePath $zipPath -ExpectedMinimumToolCount 64 | Out-Null

    $thunderstoreArguments = @{
        Version = $Version
        PluginDirectory = $pluginOutput
        McpExecutablePath = $thunderstoreMcpExecutable
        TeamName = $ThunderstoreTeam
        SourceRoot = $repoRoot
        OutputDirectory = $outputRoot
    }
    if ($AllowDirty) {
        $thunderstoreArguments.AllowDirty = $true
    }
    if ($Force) {
        $thunderstoreArguments.Force = $true
    }
    $thunderstoreJson = & (Join-Path $PSScriptRoot 'package-thunderstore.ps1') @thunderstoreArguments | Out-String
    $thunderstore = $thunderstoreJson | ConvertFrom-Json

    [pscustomobject]@{
        version = $Version
        runtime = $Runtime
        sourceCommit = $gitCommit
        sourceDirty = $sourceDirty
        zipPath = $zipPath
        sha256Path = $checksumPath
        sha256 = $zipHash
        fileCount = $manifestFiles.Count + 1
        verified = $true
        thunderstore = [ordered]@{
            team = [string]$thunderstore.team
            fullName = [string]$thunderstore.fullName
            zipPath = [string]$thunderstore.zipPath
            sha256Path = [string]$thunderstore.sha256Path
            sha256 = [string]$thunderstore.sha256
            fileCount = [int]$thunderstore.fileCount
            staticStructureVerified = [bool]$thunderstore.staticStructureVerified
            runtimeBlackBoxTested = [bool]$thunderstore.runtimeBlackBoxTested
        }
    } | ConvertTo-Json -Depth 3
} finally {
    $resolvedStagingParent = [IO.Path]::GetFullPath($stagingParent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedStagingRoot = [IO.Path]::GetFullPath($stagingRoot)
    if ($resolvedStagingRoot.StartsWith($resolvedStagingParent, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedStagingRoot)) {
        Remove-Item -LiteralPath $resolvedStagingRoot -Recurse -Force
    }
}
