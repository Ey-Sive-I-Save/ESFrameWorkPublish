[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceRoot,
    [string]$ManifestPath = '',
    [string]$MappingPath = '',
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Resolve-FullPath([string]$Path) { return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd('\') }
function Test-PathWithin([string]$Child, [string]$Parent) {
    $childFull = ([IO.Path]::GetFullPath($Child)).TrimEnd('\')
    $parentFull = ([IO.Path]::GetFullPath($Parent)).TrimEnd('\')
    return $childFull.Equals($parentFull, [StringComparison]::OrdinalIgnoreCase) -or $childFull.StartsWith($parentFull + '\', [StringComparison]::OrdinalIgnoreCase)
}
function Get-RelativePath([string]$Root, [string]$Path) { return $Path.Substring($Root.Length).TrimStart('\').Replace('\', '/') }
function Get-FileSha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create(); $stream = [IO.File]::OpenRead($Path)
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}
function Get-StrictUtf8Text([string]$Path) { return ([Text.UTF8Encoding]::new($false, $true)).GetString([IO.File]::ReadAllBytes($Path)) }
function Write-StrictUtf8Text([string]$Path, [string]$Text) { [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false)) }
function Test-ExcludedPath([string]$RelativePath) {
    $p = $RelativePath.ToLowerInvariant()
    return $p -eq '.git' -or $p.StartsWith('.git/') -or $p.StartsWith('node_modules/') -or $p.StartsWith('bin/') -or $p.StartsWith('obj/') -or $p.StartsWith('dist/') -or $p.StartsWith('build/') -or $p.StartsWith('out/') -or $p -eq '.es-migration' -or $p.StartsWith('.es-migration/') -or $p.StartsWith('src/pro/') -or $p -eq 'src/pro'
}

$sourceFull = Resolve-FullPath $SourceRoot
$projectFull = Resolve-FullPath $ProjectRoot
if (Test-PathWithin $sourceFull $projectFull) { throw "SourceRoot must be outside the protected project root: $projectFull" }
$control = Join-Path $sourceFull '.es-migration'
if ([string]::IsNullOrWhiteSpace($ManifestPath)) { $ManifestPath = Join-Path $control 'es-remap-manifest.json' }
if ([string]::IsNullOrWhiteSpace($MappingPath)) { $MappingPath = Join-Path $control 'es-symbol-map.json' }
$manifestFull = [IO.Path]::GetFullPath($ManifestPath).TrimEnd('\')
$mappingFull = [IO.Path]::GetFullPath($MappingPath).TrimEnd('\')
if (-not (Test-PathWithin $manifestFull $control) -or -not (Test-PathWithin $mappingFull $control)) { throw 'Restore inputs must remain below SourceRoot/.es-migration.' }
if (-not (Test-Path -LiteralPath $manifestFull -PathType Leaf) -or -not (Test-Path -LiteralPath $mappingFull -PathType Leaf)) { throw 'Restore manifest and mapping are required.' }
$manifest = Get-Content -LiteralPath $manifestFull -Raw -Encoding UTF8 | ConvertFrom-Json
$mapping = Get-Content -LiteralPath $mappingFull -Raw -Encoding UTF8 | ConvertFrom-Json
$receiptPath = Join-Path $control 'es-remap-receipt.json'
if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { throw 'Accepted in-place receipt is required before restore.' }
$receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]$receipt.status -ne 'passed' -or [string]$manifest.status -ne 'written-in-place') { throw 'Only an accepted in-place result can be restored.' }

$reverseIdentifier = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
foreach ($rule in @($mapping.symbols)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$rule.source) -and -not [string]::IsNullOrWhiteSpace([string]$rule.es)) { $reverseIdentifier[[string]$rule.es] = [string]$rule.source }
}
$reverseFree = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
foreach ($rule in @($mapping.textReplacements)) {
    if ([string]$rule.source -notmatch '^[\p{L}_][\p{L}\p{N}_]*$' -and -not [string]::IsNullOrWhiteSpace([string]$rule.es)) { $reverseFree[[string]$rule.es] = [string]$rule.source }
}
$freePatterns = @($reverseFree.Keys | Sort-Object @{Expression={([string]$_).Length};Descending=$true}, @{Expression={[string]$_};Ascending=$true} | ForEach-Object { [Regex]::Escape([string]$_) })
$freeRegex = if ($freePatterns.Count -gt 0) { [Regex]::new('(?<![\p{L}\p{N}_])(?:' + ($freePatterns -join '|') + ')(?![\p{L}\p{N}_])', [Text.RegularExpressions.RegexOptions]::CultureInvariant -bor [Text.RegularExpressions.RegexOptions]::Compiled) } else { $null }
$tokenRegex = [Regex]::new('[\p{L}_][\p{L}\p{N}_]*', [Text.RegularExpressions.RegexOptions]::CultureInvariant -bor [Text.RegularExpressions.RegexOptions]::Compiled)
function Restore-Text([string]$Text) {
    $result = $tokenRegex.Replace($Text, [Text.RegularExpressions.MatchEvaluator]{ param($m) $value=[string]$m.Value; if($reverseIdentifier.ContainsKey($value)){return $reverseIdentifier[$value]}; return $value })
    if ($null -ne $freeRegex) { $result = $freeRegex.Replace($result, [Text.RegularExpressions.MatchEvaluator]{ param($m) $value=[string]$m.Value; if($reverseFree.ContainsKey($value)){return $reverseFree[$value]}; return $value }) }
    return $result
}

$rows = @($manifest.files)
if ($rows.Count -eq 0) { throw 'In-place manifest has no files to restore.' }
$expectedCurrent = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($row in $rows) {
    $currentRelative = [string]$row.outputRelativePath
    if (-not $expectedCurrent.Add($currentRelative)) { throw "Duplicate transformed path in manifest: $currentRelative" }
    $currentPath = [IO.Path]::GetFullPath((Join-Path $sourceFull $currentRelative.Replace('/', '\')))
    if (-not (Test-PathWithin $currentPath $sourceFull) -or -not (Test-Path -LiteralPath $currentPath -PathType Leaf)) { throw "Transformed file is missing: $currentRelative" }
    if ((Get-FileSha256 $currentPath) -ne ([string]$row.outputSha256).ToLowerInvariant()) { throw "Transformed file drift: $currentRelative" }
}
$actualCurrent = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -File | ForEach-Object { $r=Get-RelativePath $sourceFull $_.FullName; if(-not (Test-ExcludedPath $r)){$r} })
if ($actualCurrent.Count -ne $expectedCurrent.Count) { throw "Current file set drift: expected $($expectedCurrent.Count), found $($actualCurrent.Count)" }
foreach ($r in $actualCurrent) { if (-not $expectedCurrent.Contains([string]$r)) { throw "Unexpected current file: $r" } }

$staging = Join-Path $control ('.restore-' + [Guid]::NewGuid().ToString('N'))
$backup = Join-Path $staging '__current'
New-Item -ItemType Directory -Path $backup -Force | Out-Null
try {
    foreach ($row in $rows) {
        $currentRelative = [string]$row.outputRelativePath
        $originalRelative = [string]$row.sourceRelativePath
        $currentPath = Join-Path $sourceFull $currentRelative.Replace('/', '\')
        $stagedPath = Join-Path $staging $originalRelative.Replace('/', '\')
        $stagedDirectory = Split-Path -Parent $stagedPath
        if (-not (Test-Path -LiteralPath $stagedDirectory)) { New-Item -ItemType Directory -Path $stagedDirectory -Force | Out-Null }
        if ([string]$row.kind -eq 'binary') { [IO.File]::Copy($currentPath, $stagedPath, $true) }
        else { Write-StrictUtf8Text $stagedPath (Restore-Text (Get-StrictUtf8Text $currentPath)) }
    }
    foreach ($row in $rows) {
        $currentRelative = [string]$row.outputRelativePath
        $currentPath = Join-Path $sourceFull $currentRelative.Replace('/', '\')
        $backupPath = Join-Path $backup $currentRelative.Replace('/', '\')
        $backupDirectory = Split-Path -Parent $backupPath
        if (-not (Test-Path -LiteralPath $backupDirectory)) { New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null }
        Move-Item -LiteralPath $currentPath -Destination $backupPath -Force
    }
    foreach ($row in $rows) {
        $originalRelative = [string]$row.sourceRelativePath
        $stagedPath = Join-Path $staging $originalRelative.Replace('/', '\')
        $destinationPath = Join-Path $sourceFull $originalRelative.Replace('/', '\')
        $destinationDirectory = Split-Path -Parent $destinationPath
        if (-not (Test-Path -LiteralPath $destinationDirectory)) { New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null }
        Move-Item -LiteralPath $stagedPath -Destination $destinationPath -Force
    }
    Remove-Item -LiteralPath $staging -Recurse -Force
    $mapReceipt = Join-Path $control 'es-symbol-map.receipt.json'
    foreach ($path in @($manifestFull, $receiptPath, $mappingFull, $mapReceipt, (Join-Path $control 'es-remap-journal.json'))) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force } }
    $restoreReceipt = [ordered]@{ schemaVersion=1; skillName='es-open-source-migration'; case='restore-transparent-namespace-remap'; status='passed'; sourceRootName=[IO.Path]::GetFileName($sourceFull); restoredFileCount=$rows.Count; restoredUtc=[DateTime]::UtcNow.ToString('o'); nonClaims=@('Restore uses the accepted in-place manifest; Git history and LICENSE/NOTICE are untouched.') }
    Write-StrictUtf8Text (Join-Path $control 'es-restore-receipt.json') ($restoreReceipt | ConvertTo-Json -Depth 8)
    $restoreReceipt | ConvertTo-Json -Depth 8
} catch {
    if (Test-Path -LiteralPath $staging) { throw "Restore failed; transaction staging retained at $staging. $($_.Exception.Message)" }
    throw
}
