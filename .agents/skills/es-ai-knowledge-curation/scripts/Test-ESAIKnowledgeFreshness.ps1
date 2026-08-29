<#
.SYNOPSIS
  Performs a read-only date-granularity freshness screening.
.DESCRIPTION
  Reads the project-local freshness snapshot and current scoped files. It never writes files or starts external systems.
.NOTES
  Exit 0 means the snapshot and current hashes are structurally valid; stale items are reported as screeningStatus=attention.
  Exit 1 means drift, missing records, unsafe paths, or malformed input. Rerun after refreshing the snapshot or restoring inputs.
###>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$SnapshotPath = 'Documentation/AIKnowledge/AIKnowledgeFreshness.json',
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')][string]$AsOfDate
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$findings = [Collections.Generic.List[object]]::new()

function Add-Finding([string]$Code, [string]$Path, [string]$Message) { $findings.Add([pscustomobject]@{ code = $Code; path = $Path.Replace('\', '/'); message = $Message }) }
function Resolve-ContainedPath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|[\\/])\.\.([\\/]|$)') { Add-Finding 'PATH_EXPANSION_DENIED' $RelativePath 'path must be project-relative'; return $null }
    $candidate = [IO.Path]::GetFullPath((Join-Path $root $RelativePath))
    $prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { Add-Finding 'PATH_EXPANSION_DENIED' $RelativePath 'path escapes project root'; return $null }
    return $candidate
}
function Get-RelativePath([string]$FullPath) { $prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar; return $FullPath.Substring($prefix.Length).Replace('\', '/') }
function Get-RawSha256([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Get-ScopedFiles {
    $items = [Collections.Generic.List[object]]::new()
    $warningRoot = Join-Path $root 'Assets/Plugins/ES/AIWarnings'
    $entryRoot = Join-Path $root 'Documentation/AIKnowledge/entries'
    foreach ($file in @(Get-ChildItem -LiteralPath $warningRoot -Recurse -File -ErrorAction Stop | Where-Object { $_.Extension -ieq '.md' } | Sort-Object FullName)) { $items.Add([pscustomobject]@{ path = Get-RelativePath $file.FullName; kind = 'AIWarning'; file = $file }) }
    foreach ($file in @(Get-ChildItem -LiteralPath $entryRoot -Recurse -File -ErrorAction Stop | Where-Object { $_.Extension -ieq '.md' } | Sort-Object FullName)) { $items.Add([pscustomobject]@{ path = Get-RelativePath $file.FullName; kind = 'KnowledgeEntry'; file = $file }) }
    $routeCatalog = @(Get-ChildItem -LiteralPath $warningRoot -Recurse -File -Filter 'AIWarningsRouteCatalog.json' -ErrorAction Stop | Sort-Object FullName)
    if ($routeCatalog.Count -ne 1) { Add-Finding 'SCOPE_ROUTE_CATALOG_INVALID' 'Assets/Plugins/ES/AIWarnings' "expected exactly one AIWarningsRouteCatalog.json, found $($routeCatalog.Count)"; return @($items) }
    $routeCatalogPath = Get-RelativePath $routeCatalog[0].FullName
    $fixed = @(
        @{ path = 'Documentation/AIKnowledge/README.md'; kind = 'KnowledgeGovernance' },
        @{ path = 'Documentation/AIKnowledge/AIBRAIN_ENTRY.md'; kind = 'KnowledgeGovernance' },
        @{ path = 'Documentation/AIKnowledge/KnowledgeIndex.yaml'; kind = 'KnowledgeIndex' },
        @{ path = 'Documentation/AIKnowledge/AIWarningsDomainInventory.yaml'; kind = 'KnowledgeGovernance' },
        @{ path = $routeCatalogPath; kind = 'AIWarningsRouteCatalog' }
    )
    foreach ($spec in $fixed) {
        $full = Resolve-ContainedPath $spec.path
        if ($null -ne $full -and (Test-Path -LiteralPath $full -PathType Leaf)) { $items.Add([pscustomobject]@{ path = $spec.path; kind = $spec.kind; file = (Get-Item -LiteralPath $full -Force) }) } else { Add-Finding 'SCOPE_FILE_MISSING' $spec.path 'freshness scope file does not exist' }
    }
    return @($items | Sort-Object path -Unique)
}

$snapshotFull = Resolve-ContainedPath $SnapshotPath
$snapshot = $null
if ($null -ne $snapshotFull -and (Test-Path -LiteralPath $snapshotFull -PathType Leaf)) {
    try { $snapshot = $strictUtf8.GetString([IO.File]::ReadAllBytes($snapshotFull)) | ConvertFrom-Json } catch { Add-Finding 'SNAPSHOT_INVALID' $SnapshotPath $_.Exception.Message }
} else { Add-Finding 'SNAPSHOT_MISSING' $SnapshotPath 'freshness snapshot does not exist' }

$dateText = if ([string]::IsNullOrWhiteSpace($AsOfDate)) { [DateTime]::UtcNow.ToString('yyyy-MM-dd') } else { $AsOfDate }
try { $asOf = [DateTime]::ParseExact($dateText, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture).Date } catch { Add-Finding 'DATE_FORMAT_INVALID' $SnapshotPath "asOfDate must be yyyy-MM-dd: $dateText"; $asOf = [DateTime]::UtcNow.Date }
if ($null -ne $snapshot -and ([int]$snapshot.schemaVersion -ne 1 -or [string]$snapshot.snapshotKind -ne 'esframework-ai-freshness')) { Add-Finding 'SNAPSHOT_SCHEMA_INVALID' $SnapshotPath 'unsupported snapshot schema or kind' }
if ($null -ne $snapshot -and $snapshot.policy.staleAfterDays -ne 7) { Add-Finding 'POLICY_INVALID' $SnapshotPath 'staleAfterDays must remain 7' }
$currentScope = if ($null -ne $snapshot -and $null -ne $snapshotFull) { @(Get-ScopedFiles) } else { @() }

$snapshotItems = @{}
if ($null -ne $snapshot) {
    foreach ($item in @($snapshot.items)) {
        $path = [string]$item.path
        if ($snapshotItems.ContainsKey($path)) { Add-Finding 'DUPLICATE_PATH' $path 'snapshot path is duplicated' } else { $snapshotItems[$path] = $item }
        if ($path -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($path)) { Add-Finding 'PATH_EXPANSION_DENIED' $path 'snapshot item path is unsafe' }
        if ([string]$item.lastModifiedDate -notmatch '^\d{4}-\d{2}-\d{2}$') { Add-Finding 'DATE_FORMAT_INVALID' $path 'lastModifiedDate must be yyyy-MM-dd' }
    }
}

$currentPaths = @{}
$screenItems = [Collections.Generic.List[object]]::new()
foreach ($current in $currentScope) {
    $currentPaths[$current.path] = $true
    $item = if ($snapshotItems.ContainsKey($current.path)) { $snapshotItems[$current.path] } else { $null }
    if ($null -eq $item) { Add-Finding 'RECORD_MISSING' $current.path 'current scoped file has no freshness record'; continue }
    $actualHash = Get-RawSha256 $current.file.FullName
    $drift = [string]$item.contentHash -cne $actualHash
    try { $lastDate = [DateTime]::ParseExact([string]$item.lastModifiedDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture).Date } catch { continue }
    if ($lastDate -gt $asOf) { Add-Finding 'DATE_IN_FUTURE' $current.path 'lastModifiedDate is later than asOfDate' }
    $ageDays = if ($lastDate -le $asOf) { (New-TimeSpan -Start $lastDate -End $asOf).Days } else { 0 }
    $status = if ($drift) { 'drift' } elseif ($ageDays -gt 7) { 'stale' } else { 'current' }
    if ($drift) { Add-Finding 'HASH_DRIFT' $current.path 'current file hash differs from freshness snapshot; refresh and recheck' }
    $screenItems.Add([ordered]@{ path = $current.path; kind = $current.kind; contentHash = $actualHash; lastModifiedDate = [string]$item.lastModifiedDate; ageDays = $ageDays; status = $status })
}
foreach ($path in @($snapshotItems.Keys | Sort-Object)) { if (-not $currentPaths.ContainsKey($path)) { Add-Finding 'RECORDED_FILE_MISSING' $path 'snapshot record has no current scoped file' } }

$orderedFindings = @($findings | Sort-Object code, path, message)
$currentCount = @($screenItems | Where-Object status -eq 'current').Count
$staleCount = @($screenItems | Where-Object status -eq 'stale').Count
$driftCount = @($screenItems | Where-Object status -eq 'drift').Count
$screeningStatus = if ($orderedFindings.Count -gt 0) { 'blocked' } elseif ($driftCount -gt 0) { 'drift' } elseif ($staleCount -gt 0) { 'attention' } else { 'fresh' }
$result = [ordered]@{
    validator = 'es-ai-knowledge-freshness'
    status = if ($orderedFindings.Count -eq 0) { 'passed' } else { 'blocked' }
    screeningStatus = $screeningStatus
    asOfDate = $asOf.ToString('yyyy-MM-dd')
    staleAfterDays = 7
    counts = [ordered]@{ total = $screenItems.Count; current = $currentCount; stale = $staleCount; drift = $driftCount; findings = $orderedFindings.Count }
    staleItems = @($screenItems | Where-Object status -eq 'stale' | Sort-Object path)
    driftItems = @($screenItems | Where-Object status -eq 'drift' | Sort-Object path)
    findings = $orderedFindings
    claimsNotProven = @('semantic correctness or review completion', 'Unity/editor/process behavior', 'Profiler/Player/IL2CPP/release behavior')
}
$result | ConvertTo-Json -Depth 8
if ($orderedFindings.Count -gt 0) { exit 1 }
exit 0
