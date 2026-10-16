[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [Parameter(Mandatory)]
    [string]$PluginDirectory,
    [Parameter(Mandatory)]
    [string]$McpExecutablePath,
    [ValidatePattern('^[A-Za-z0-9_]+$')]
    [string]$TeamName = 'Arcueid_77',
    [ValidatePattern('^[A-Za-z0-9_]+$')]
    [string]$PackageName = 'Spherewright',
    [string]$SourceRoot,
    [string]$AssetsDirectory,
    [string]$OutputDirectory,
    [switch]$AllowDirty,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$toolRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (-not $SourceRoot) {
    $SourceRoot = $toolRoot
}
$resolvedSourceRoot = [IO.Path]::GetFullPath($SourceRoot)
if (-not $AssetsDirectory) {
    $AssetsDirectory = Join-Path $toolRoot 'packaging\thunderstore'
}
$resolvedAssetsDirectory = [IO.Path]::GetFullPath($AssetsDirectory)
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $toolRoot 'artifacts'
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$resolvedPluginDirectory = [IO.Path]::GetFullPath($PluginDirectory)
$resolvedMcpExecutablePath = [IO.Path]::GetFullPath($McpExecutablePath)

foreach ($directory in @($resolvedSourceRoot, $resolvedAssetsDirectory, $resolvedPluginDirectory)) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Required directory was not found: $directory"
    }
}
if (-not (Test-Path -LiteralPath $resolvedMcpExecutablePath -PathType Leaf)) {
    throw "Single-file MCP executable was not found: $resolvedMcpExecutablePath"
}
if ([IO.Path]::GetExtension($resolvedMcpExecutablePath) -cne '.exe') {
    throw 'The Thunderstore MCP payload must be a Windows executable.'
}

$gitCommit = (& git -C $resolvedSourceRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $gitCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve the source Git commit.'
}
$gitStatus = @(& git -C $resolvedSourceRoot status --porcelain=v1 --untracked-files=normal)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the source worktree.'
}
$sourceDirty = $gitStatus.Count -gt 0
if ($sourceDirty -and -not $AllowDirty) {
    throw 'Thunderstore packaging requires a clean source worktree. Use -AllowDirty only for a local preview package.'
}

$pluginFiles = @(
    'Spherewright.Plugin.dll',
    'Spherewright.Contracts.dll',
    'Spherewright.Bridge.Core.dll',
    'Newtonsoft.Json.dll'
)
foreach ($name in $pluginFiles) {
    $source = Join-Path $resolvedPluginDirectory $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Expected Plugin output is missing: $source"
    }
}

$contractsAssemblyPath = Join-Path $resolvedPluginDirectory 'Spherewright.Contracts.dll'
$contractsAssembly = [Reflection.Assembly]::LoadFrom($contractsAssemblyPath)
$productVersionType = $contractsAssembly.GetType('Spherewright.Contracts.Versioning.SpherewrightProduct', $true)
$productVersionField = $productVersionType.GetField('CurrentVersion', [Reflection.BindingFlags]'Public, Static')
$productVersion = [string]$productVersionField.GetRawConstantValue()
if (-not [string]::Equals($productVersion, $Version, [StringComparison]::Ordinal)) {
    throw "Package version $Version does not match the built product version $productVersion."
}

$iconPath = Join-Path $resolvedAssetsDirectory 'icon.png'
$readmeTemplatePath = Join-Path $resolvedAssetsDirectory 'README.md'
$changelogPath = Join-Path $resolvedAssetsDirectory "changelog\$Version.md"
foreach ($file in @($iconPath, $readmeTemplatePath, $changelogPath)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Required Thunderstore asset was not found: $file"
    }
}

$sourceLicensePath = Join-Path $resolvedSourceRoot 'LICENSE'
$sourcePlaybookPath = Join-Path $resolvedSourceRoot 'docs\agent-playbook.md'
$sourceChineseLicensePath = Join-Path $resolvedSourceRoot 'LICENSE.zh-CN.md'
$sourceChinesePlaybookPath = Join-Path $resolvedSourceRoot 'docs\agent-playbook.zh-CN.md'
foreach ($file in @($sourceLicensePath, $sourcePlaybookPath, $sourceChineseLicensePath, $sourceChinesePlaybookPath)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Required source release file was not found: $file"
    }
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$zipPath = Join-Path $outputRoot "Spherewright-$Version-thunderstore.zip"
$checksumPath = "$zipPath.sha256"
if (((Test-Path -LiteralPath $zipPath) -or (Test-Path -LiteralPath $checksumPath)) -and -not $Force) {
    throw "The versioned Thunderstore artifact already exists. Choose another version or pass -Force: $zipPath"
}

$stagingParent = Join-Path $toolRoot '.local\thunderstore-package'
New-Item -ItemType Directory -Path $stagingParent -Force | Out-Null
$stagingRoot = Join-Path $stagingParent ([guid]::NewGuid().ToString('N'))
$payloadDirectory = Join-Path $stagingRoot 'plugins'
New-Item -ItemType Directory -Path $payloadDirectory -Force | Out-Null

try {
    Copy-Item -LiteralPath $iconPath -Destination (Join-Path $stagingRoot 'icon.png')
    Copy-Item -LiteralPath $changelogPath -Destination (Join-Path $stagingRoot 'CHANGELOG.md')
    Copy-Item -LiteralPath $sourceLicensePath -Destination (Join-Path $stagingRoot 'LICENSE')
    Copy-Item -LiteralPath $sourcePlaybookPath -Destination (Join-Path $stagingRoot 'AGENT-PLAYBOOK.md')
    Copy-Item -LiteralPath $sourceChinesePlaybookPath -Destination (Join-Path $stagingRoot 'agent-playbook.zh-CN.md')
    Copy-Item -LiteralPath $sourceChineseLicensePath -Destination (Join-Path $stagingRoot 'LICENSE.zh-CN.md')

    $readme = [IO.File]::ReadAllText($readmeTemplatePath)
    $readme = $readme.Replace('{{VERSION}}', $Version).Replace('{{TEAM_NAME}}', $TeamName)
    [IO.File]::WriteAllText((Join-Path $stagingRoot 'README.md'), $readme, [Text.UTF8Encoding]::new($false))

    foreach ($name in $pluginFiles) {
        Copy-Item -LiteralPath (Join-Path $resolvedPluginDirectory $name) -Destination (Join-Path $payloadDirectory $name)
    }
    Copy-Item -LiteralPath $resolvedMcpExecutablePath -Destination (Join-Path $payloadDirectory 'Spherewright.Mcp.exe')

    $thunderstoreManifest = [ordered]@{
        name = $PackageName
        version_number = $Version
        website_url = 'https://github.com/Arcueid77/Spherewright'
        description = 'MCP bridge for AI agents to observe and operate Icarus through normal DSP mechanics. / 让 AI 智能体通过 MCP 按正常机制观察并操作伊卡洛斯。'
        dependencies = @('xiaoye97-BepInEx-5.4.17')
    }
    $thunderstoreManifestJson = $thunderstoreManifest | ConvertTo-Json -Depth 4
    [IO.File]::WriteAllText((Join-Path $stagingRoot 'manifest.json'), $thunderstoreManifestJson, [Text.UTF8Encoding]::new($false))

    $manifestFiles = @(
        Get-ChildItem -LiteralPath $stagingRoot -File -Recurse |
            Sort-Object FullName |
            ForEach-Object {
                $relative = $_.FullName.Substring($stagingRoot.Length + 1).Replace([IO.Path]::DirectorySeparatorChar, '/')
                [ordered]@{
                    path = $relative
                    size = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )
    $sourceManifest = [ordered]@{
        schemaVersion = 1
        package = $PackageName
        team = $TeamName
        fullName = "$TeamName-$PackageName"
        version = $Version
        productVersion = $productVersion
        runtime = 'win-x64'
        sourceCommit = $gitCommit
        sourceDirty = $sourceDirty
        supportedDspVersion = '0.10.35.29151'
        supportedBepInExVersion = '5.4.17.0'
        runtimeBlackBoxTested = $false
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        files = $manifestFiles
    }
    $sourceManifestJson = $sourceManifest | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText((Join-Path $stagingRoot 'spherewright-manifest.json'), $sourceManifestJson, [Text.UTF8Encoding]::new($false))

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    if (Test-Path -LiteralPath $checksumPath) {
        Remove-Item -LiteralPath $checksumPath -Force
    }
    Compress-Archive -Path (Join-Path $stagingRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($checksumPath, "$zipHash  $([IO.Path]::GetFileName($zipPath))`n", [Text.UTF8Encoding]::new($false))

    $staticCheckJson = & (Join-Path $PSScriptRoot 'test-thunderstore-package.ps1') `
        -PackagePath $zipPath `
        -ExpectedVersion $Version `
        -TeamName $TeamName `
        -PackageName $PackageName `
        -ExpectedSourceCommit $gitCommit | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw 'Thunderstore static package validation failed.'
    }
    $staticCheck = $staticCheckJson | ConvertFrom-Json

    [pscustomobject]@{
        version = $Version
        team = $TeamName
        package = $PackageName
        fullName = "$TeamName-$PackageName"
        sourceCommit = $gitCommit
        sourceDirty = $sourceDirty
        zipPath = $zipPath
        sha256Path = $checksumPath
        sha256 = $zipHash
        fileCount = $manifestFiles.Count + 1
        staticStructureVerified = [bool]$staticCheck.staticStructureVerified
        runtimeBlackBoxTested = $false
    } | ConvertTo-Json -Depth 3
} finally {
    $resolvedStagingParent = [IO.Path]::GetFullPath($stagingParent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedStagingRoot = [IO.Path]::GetFullPath($stagingRoot)
    if ($resolvedStagingRoot.StartsWith($resolvedStagingParent, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedStagingRoot)) {
        Remove-Item -LiteralPath $resolvedStagingRoot -Recurse -Force
    }
}
