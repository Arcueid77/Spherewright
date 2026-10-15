#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$ReportPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
$skip = @('.git', '.local', 'artifacts', 'bin', 'obj', 'TestResults', '.vs', '.idea', 'deps')
$queue = [Collections.Generic.Queue[string]]::new()
$queue.Enqueue($rootPath)
$documents = [Collections.Generic.List[object]]::new()
while ($queue.Count) {
    $directory = $queue.Dequeue()
    foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
        if ($entry.PSIsContainer) {
            if ($entry.Name -notin $skip -and -not ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) { $queue.Enqueue($entry.FullName) }
        } elseif ($entry.Extension -in @('.md', '.mdx')) {
            $documents.Add([pscustomobject]@{path=[IO.Path]::GetRelativePath($rootPath,$entry.FullName).Replace('\','/');full=$entry.FullName;text=[IO.File]::ReadAllText($entry.FullName)})
        }
    }
}
$errors = [Collections.Generic.List[object]]::new()
$inventory = [Collections.Generic.List[object]]::new()
$paired = 0; $inline = 0; $links = 0
function Add-DocError([string]$Path,[string]$Rule) { $errors.Add([pscustomobject]@{path=$Path;rule=$Rule}) }
function Get-HeadingIds([string]$Text) {
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $seen = @{}; $fence = $false
    foreach ($line in ($Text -split '\r?\n')) {
        if ($line -match '^\s*(?:```|~~~)') { $fence = -not $fence; continue }
        if ($fence) { continue }
        if ($line -match '^#{1,6}\s+(.+?)\s*#*\s*$') {
            $id = ($Matches[1] -replace '[^\p{L}\p{Nd}\p{M}_ -]','').Trim().ToLowerInvariant() -replace ' ','-'
            if ($seen.ContainsKey($id)) { $seen[$id]++; $id += '-' + $seen[$id] } else { $seen[$id]=0 }
            $null = $ids.Add($id)
        }
        foreach ($match in [regex]::Matches($line,'<a\s+(?:id|name)="([^"]+)"')) { $null=$ids.Add($match.Groups[1].Value) }
    }
    return ,$ids
}
foreach ($doc in $documents) {
    $isChinese = $doc.path.EndsWith('.zh-CN.md',[StringComparison]::Ordinal)
    $role = if ($isChinese) { 'zh-CN' } else { 'en' }
    if (-not $isChinese) {
        $companion = [IO.Path]::ChangeExtension($doc.full, '.zh-CN.md')
        if (Test-Path -LiteralPath $companion -PathType Leaf) {
            $paired++
            $zh = [IO.File]::ReadAllText($companion)
            if ($doc.text -notmatch '\.zh-CN\.md\)' -or $zh -notmatch '\[.*English.*\]\(') { Add-DocError $doc.path 'missing-language-navigation' }
            if ([regex]::Matches($zh,'\p{IsCJKUnifiedIdeographs}').Count -lt 20) { Add-DocError $doc.path 'incomplete-chinese-companion' }
            $critical='\b(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64}|0\.10\.\d+\.\d+)\b'
            $enValues=@([regex]::Matches($doc.text,$critical)|ForEach-Object {$_.Value.ToUpperInvariant()}|Sort-Object -Unique)
            $zhValues=@([regex]::Matches($zh,$critical)|ForEach-Object {$_.Value.ToUpperInvariant()}|Sort-Object -Unique)
            if (($enValues -join "`n") -cne ($zhValues -join "`n")) { Add-DocError $doc.path 'candidate-hash-or-native-version-translation-mismatch' }
        } elseif ($doc.text -match '(?m)^## English\s*$' -and $doc.text -match '(?m)^## 中文\s*$') {
            $inline++; $role='en+zh-CN'
        } else { Add-DocError $doc.path 'missing-full-language-companion' }
    } elseif ($doc.path -ne 'LICENSE.zh-CN.md') {
        $english=$doc.full -replace '\.zh-CN\.md$','.md'
        if (-not (Test-Path -LiteralPath $english -PathType Leaf)) { Add-DocError $doc.path 'orphan-chinese-companion' }
    }
    if ($doc.text -match '(?i)\bprivate (?:Spherewright-Lab|Lab repository|Sample consumer repository)\b') { Add-DocError $doc.path 'obsolete-private-lab-claim' }
    if ($doc.text -match '(?i)(?<![A-Za-z0-9])(?:[A-Z]:[\\/]Users[\\/]|/(?:Users|home)/)[^\s/\\]+') { Add-DocError $doc.path 'private-user-path' }
    if ($doc.text -match '\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,}|sk-(?:proj-)?[A-Za-z0-9_-]{20,})\b') { Add-DocError $doc.path 'credential-like-value' }
    $fence=$false
    foreach ($line in ($doc.text -split '\r?\n')) {
        if ($line -match '^\s*(?:```|~~~)') { $fence=-not $fence; continue }
        if ($fence) { continue }
        foreach ($match in [regex]::Matches($line,'\]\((?<target>[^)\r\n]+)\)')) {
            $target=$match.Groups['target'].Value.Trim().Trim('<','>')
            if ($target -match '^[A-Za-z][A-Za-z0-9+.-]*:') { continue }
            $target=($target -split '\s+"',2)[0]
            $pieces=$target -split '#',2
            $part=[Uri]::UnescapeDataString(($pieces[0] -split '\?',2)[0])
            $full=if($part){[IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $doc.full) $part))}else{$doc.full}
            if ($full -ine $rootPath -and -not $full.StartsWith($rootPath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { Add-DocError $doc.path 'escaping-relative-link'; continue }
            if (-not (Test-Path -LiteralPath $full)) { Add-DocError $doc.path 'broken-relative-link'; continue }
            if ($pieces.Count -eq 2 -and $pieces[1] -and [IO.Path]::GetExtension($full) -in @('.md','.mdx')) {
                $ids=Get-HeadingIds ([IO.File]::ReadAllText($full))
                if (-not $ids.Contains([Uri]::UnescapeDataString($pieces[1]))) { Add-DocError $doc.path 'broken-heading-anchor' }
            }
            $links++
        }
    }
    if ($fence) { Add-DocError $doc.path 'unclosed-code-fence' }
    $inventory.Add([pscustomobject]@{path=$doc.path;language=$role;sha256=(Get-FileHash -LiteralPath $doc.full -Algorithm SHA256).Hash})
}
if (-not (Test-Path -LiteralPath (Join-Path $rootPath 'LICENSE.zh-CN.md'))) { Add-DocError 'LICENSE' 'missing-explanatory-chinese-license' }
$report=[ordered]@{passed=($errors.Count -eq 0);markdownFiles=$documents.Count;fullLanguagePairs=$paired;inlineBilingualDocuments=$inline;relativeLinksAndAnchorsVerified=$links;errors=$errors.ToArray();files=$inventory.ToArray();scope='maintained product documentation; ignored/private outputs and submodules excluded; translation equivalence also requires human review'}
if($ReportPath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($ReportPath),($report|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))}
[pscustomobject]@{passed=$report.passed;markdownFiles=$report.markdownFiles;fullLanguagePairs=$paired;inlineBilingualDocuments=$inline;relativeLinksAndAnchorsVerified=$links;errors=$errors.ToArray()}|ConvertTo-Json -Depth 5
if($errors.Count){throw 'Documentation coverage, links or safety checks failed; inspect path/rule findings.'}
