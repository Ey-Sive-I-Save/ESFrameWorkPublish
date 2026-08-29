<#
.SYNOPSIS
  Refreshes the project-local AIWarnings/AIKnowledge freshness snapshot.
.DESCRIPTION
  Read scope is limited to the declared AIWarnings and AIKnowledge files below ProjectRoot.
  The only write target is SnapshotPath. Existing dates are preserved when content hashes are unchanged.
.NOTES
  Precision: UTC date (yyyy-MM-dd). Exit 0 on a completed write; terminating input/path/schema errors are non-zero.
  Idempotency: repeated runs with unchanged inputs and AsOfDate produce identical snapshot bytes.
  Recovery: rerun from the current files; no partial source edits are performed.
###>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$SnapshotPath = 'Documentation/AIKnowledge/AIKnowledgeFreshness.json',
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')][string]$AsOfDate
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
# Read-only project-relative boundary contract: every snapshot path is checked
# with GetFullPath/StartsWith and cannot escape ProjectRoot.
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)

function Resolve-ContainedPath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "Snapshot path must be project-relative: $RelativePath"
    }
    $joined = Join-Path -Path $root -ChildPath ([string]$RelativePath)
    Write-Verbose ("Resolving freshness path: [$joined]")
    $candidate = [IO.Path]::GetFullPath($joined)
    $prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Snapshot path escapes project root: $RelativePath" }
    return $candidate
}

function Get-RelativePath([string]$FullPath) {
    $prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    return $FullPath.Substring($prefix.Length).Replace('\', '/')
}

function Get-RawSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-ScopedFiles {
    $items = [Collections.Generic.List[object]]::new()
    $warningRoot = Join-Path $root 'Assets/Plugins/ES/AIWarnings'
    $entryRoot = Join-Path $root 'Documentation/AIKnowledge/entries'
    foreach ($file in @(Get-ChildItem -LiteralPath $warningRoot -Recurse -File -ErrorAction Stop | Where-Object { $_.Extension -ieq '.md' } | Sort-Object FullName)) {
        $items.Add([pscustomobject]@{ path = Get-RelativePath $file.FullName; kind = 'AIWarning'; file = $file })
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $entryRoot -Recurse -File -ErrorAction Stop | Where-Object { $_.Extension -ieq '.md' } | Sort-Object FullName)) {
        $items.Add([pscustomobject]@{ path = Get-RelativePath $file.FullName; kind = 'KnowledgeEntry'; file = $file })
    }
    $routeCatalog = @(Get-ChildItem -LiteralPath $warningRoot -Recurse -File -Filter 'AIWarningsRouteCatalog.json' -ErrorAction Stop | Sort-Object FullName)
    if ($routeCatalog.Count -ne 1) { throw "Expected exactly one AIWarningsRouteCatalog.json, found $($routeCatalog.Count)" }
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
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Freshness scope file is missing: $($spec.path)" }
        $items.Add([pscustomobject]@{ path = $spec.path; kind = $spec.kind; file = (Get-Item -LiteralPath $full -Force) })
    }
    return @($items | Sort-Object path -Unique)
}

$dateText = if ([string]::IsNullOrWhiteSpace($AsOfDate)) { [DateTime]::UtcNow.ToString('yyyy-MM-dd') } else { $AsOfDate }
try { $asOf = [DateTime]::ParseExact($dateText, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal).Date } catch { throw "AsOfDate must be yyyy-MM-dd: $dateText" }

$snapshotFull = Resolve-ContainedPath $SnapshotPath
$previous = @{}
$previousAsOf = $null
if (Test-Path -LiteralPath $snapshotFull -PathType Leaf) {
    try {
        $snapshotText = $strictUtf8.GetString([IO.File]::ReadAllBytes($snapshotFull))
        $snapshot = $snapshotText | ConvertFrom-Json
        if ([int]$snapshot.schemaVersion -ne 1 -or $snapshot.policy.staleAfterDays -ne 7) { throw 'unsupported snapshot schema or staleAfterDays' }
        $previousAsOf = [DateTime]::ParseExact([string]$snapshot.asOfDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture).Date
        foreach ($item in @($snapshot.items)) { $previous[[string]$item.path] = $item }
    } catch { throw "Cannot read existing freshness snapshot: $($_.Exception.Message)" }
}
if ($null -ne $previousAsOf -and $asOf -lt $previousAsOf) { throw "AsOfDate cannot move backwards from $($previousAsOf.ToString('yyyy-MM-dd')) to $dateText" }

$records = [Collections.Generic.List[object]]::new()
foreach ($item in Get-ScopedFiles) {
    $hash = Get-RawSha256 $item.file.FullName
    $bytes = $item.file.Length
    $old = if ($previous.ContainsKey($item.path)) { $previous[$item.path] } else { $null }
    $changed = $null -eq $old -or [string]$old.contentHash -cne $hash
    if ($changed) {
        if ($null -eq $old) {
            $candidateDate = $item.file.LastWriteTimeUtc.Date
            if ($candidateDate -gt $asOf) { $candidateDate = $asOf }
            $lastDate = $candidateDate
            $dateSource = 'filesystem-bootstrap'
        } else {
            $lastDate = $asOf
            $dateSource = 'hash-change-observed'
        }
    } else {
        try { $lastDate = [DateTime]::ParseExact([string]$old.lastModifiedDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture).Date } catch { throw "Invalid lastModifiedDate for $($item.path)" }
        $dateSource = [string]$old.dateSource
    }
    if ($lastDate -gt $asOf) { throw "lastModifiedDate is later than asOfDate for $($item.path)" }
    $ageDays = (New-TimeSpan -Start $lastDate -End $asOf).Days
    $records.Add([ordered]@{
        path = $item.path
        kind = $item.kind
        bytes = [int64]$bytes
        contentHash = $hash
        lastModifiedDate = $lastDate.ToString('yyyy-MM-dd')
        dateSource = $dateSource
        ageDays = $ageDays
        status = if ($ageDays -gt 7) { 'stale' } else { 'current' }
    })
}

$current = @($records | Where-Object status -eq 'current').Count
$stale = @($records | Where-Object status -eq 'stale').Count
$routePathForOutput = @($records | Where-Object { $_['kind'] -eq 'AIWarningsRouteCatalog' } | ForEach-Object { $_['path'] })[0]
$result = [ordered]@{
    schemaVersion = 1
    snapshotKind = 'esframework-ai-freshness'
    asOfDate = $asOf.ToString('yyyy-MM-dd')
    generatedOnDate = $asOf.ToString('yyyy-MM-dd')
    policy = [ordered]@{ precision = 'date'; timezone = 'UTC'; staleAfterDays = 7; stalePredicate = 'ageDays > staleAfterDays'; hashAlgorithm = 'SHA-256'; hashBasis = 'raw-file-bytes' }
    scope = [ordered]@{
        roots = @('Assets/Plugins/ES/AIWarnings/**/*.md', 'Documentation/AIKnowledge/entries/**/*.md')
        fixedFiles = @('Documentation/AIKnowledge/README.md', 'Documentation/AIKnowledge/AIBRAIN_ENTRY.md', 'Documentation/AIKnowledge/KnowledgeIndex.yaml', 'Documentation/AIKnowledge/AIWarningsDomainInventory.yaml', $routePathForOutput)
        excludedGeneratedFiles = @('Documentation/AIKnowledge/AIWarningsGeneratedInventory.json')
    }
    summary = [ordered]@{ total = $records.Count; current = $current; stale = $stale; drift = 0 }
    items = @($records | Sort-Object path)
}
$parent = Split-Path -Parent $snapshotFull
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$json = $result | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($snapshotFull, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    action = 'updated'
    snapshotPath = $SnapshotPath.Replace('\', '/')
    asOfDate = $result.asOfDate
    total = $records.Count
    current = $current
    stale = $stale
    idempotent = $true
} | ConvertTo-Json -Depth 4
