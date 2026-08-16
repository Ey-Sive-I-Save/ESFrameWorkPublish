[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,

    [Parameter(Mandatory = $true, Position = 0, ParameterSetName = 'Query')]
    [string]$Query,

    [Parameter(Mandatory = $true, ParameterSetName = 'ExactPath')]
    [string]$CommandPath,

    [ValidateRange(1, 6)]
    [int]$MaxResults = 6,

    [ValidateSet('all', 'information', 'review', 'controlled-execution', 'candidate-generation', 'handover')]
    [string]$Role = 'all',

    [ValidateSet('all', 'L1', 'L2', 'L3')]
    [string]$RiskLevel = 'all',

    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$allowedRoles = @('information', 'review', 'controlled-execution', 'candidate-generation', 'handover')
$allowedWriteModes = @('read-only', 'scoped-write', 'candidate-only', 'documentation-write', 'external-run')

function Get-Sha256([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-Score($Entry, [string[]]$Terms) {
    $score = 0
    $title = ([string]$Entry.title).ToLowerInvariant()
    $summary = ([string]$Entry.summary).ToLowerInvariant()
    $keywords = ([string]$Entry.keywords).ToLowerInvariant()
    $id = ([string]$Entry.id).ToLowerInvariant()
    foreach ($term in $Terms) {
        if ([string]::IsNullOrWhiteSpace($term)) { continue }
        $needle = $term.ToLowerInvariant()
        if ($title.Contains($needle)) { $score += 12 }
        if ($keywords.Contains($needle)) { $score += 7 }
        if ($summary.Contains($needle)) { $score += 4 }
        if ($id.Contains($needle)) { $score += 2 }
    }
    return $score
}

function Test-ReparsePointPath([string]$Root, [string]$Target) {
    $relative = $Target.Substring($Root.Length).TrimStart([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    $current = $Root
    foreach ($segment in $relative.Split(@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar), [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            return $true
        }
    }
    return $false
}

function Test-ProjectRelativeCommandPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path)) {
        return $false
    }
    $normalized = $Path.Replace('\', '/').Trim()
    if (-not $normalized.StartsWith('Assets/Plugins/ES/AICommands/', [StringComparison]::Ordinal) -or -not $normalized.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    foreach ($segment in $normalized.Split('/')) {
        if ([string]::IsNullOrEmpty($segment) -or $segment -eq '.' -or $segment -eq '..') {
            return $false
        }
    }
    return $true
}

function Test-ContractEntry($Entry, [hashtable]$Ids, [hashtable]$Paths, [string]$ProjectRoot, [string]$CommandRoot) {
    if ($null -eq $Entry) { throw 'AICommand directory contains a null entry.' }
    $id = [string]$Entry.id
    $path = [string]$Entry.path
    $title = [string]$Entry.title
    $summary = [string]$Entry.summary
    $keywords = [string]$Entry.keywords
    $role = [string]$Entry.role
    $writeMode = [string]$Entry.writeMode
    $riskLevel = [string]$Entry.riskLevel

    if ($id -notmatch '^[a-z0-9][a-z0-9.-]{2,79}$' -or $Ids.ContainsKey($id)) {
        throw "AICommand directory has an invalid or duplicate id: $id"
    }
    if (
        [string]::IsNullOrWhiteSpace($title) -or $title.Trim() -ne $title -or $title.Length -gt 80 -or
        [string]::IsNullOrWhiteSpace($summary) -or $summary.Trim() -ne $summary -or $summary.Length -gt 240 -or
        [string]::IsNullOrWhiteSpace($keywords) -or $keywords.Trim() -ne $keywords -or $keywords.Length -gt 320
    ) {
        throw "AICommand directory metadata is empty, padded, or too long: $id"
    }
    if ($allowedRoles -notcontains $role -or $allowedWriteModes -notcontains $writeMode -or $riskLevel -notin @('L1', 'L2', 'L3')) {
        throw "AICommand directory has an unsupported role, write mode, or risk level: $id"
    }
    if (
        (($role -in @('information', 'review')) -and $writeMode -ne 'read-only') -or
        ($role -eq 'candidate-generation' -and $writeMode -ne 'candidate-only') -or
        ($role -eq 'handover' -and $writeMode -ne 'documentation-write') -or
        ($role -eq 'controlled-execution' -and $writeMode -notin @('scoped-write', 'external-run'))
    ) {
        throw "AICommand directory role/writeMode conflict: $id"
    }
    if (
        $path -ne $path.Replace('\', '/') -or $path -notmatch '^Assets/Plugins/ES/AICommands/.+\.md$' -or
        $path.Contains('//') -or $path.Contains(':')
    ) {
        throw "AICommand directory has an invalid contract path: $id"
    }
    $segments = $path.Split('/')
    if ($segments | Where-Object { $_ -eq '.' -or $_ -eq '..' -or $_.Length -eq 0 }) {
        throw "AICommand directory has a contract path traversal: $id"
    }
    if ($Paths.ContainsKey($path)) { throw "AICommand directory has a duplicate contract path: $path" }

    $fullPath = [IO.Path]::GetFullPath((Join-Path $ProjectRoot ($path.Replace('/', [IO.Path]::DirectorySeparatorChar))))
    if (
        -not $fullPath.StartsWith($CommandRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $fullPath -PathType Leaf) -or
        (Test-ReparsePointPath $ProjectRoot $fullPath)
    ) {
        throw "AICommand directory points outside the managed root or through a reparse point: $id"
    }
    $Ids[$id] = $true
    $Paths[$path] = $true
}

$resolvedRoot = [IO.Path]::GetFullPath($ProjectRoot)
$catalogPath = Join-Path $resolvedRoot 'Assets\Plugins\ES\AICommands\AICommandCatalog.json'
if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "AICommand directory does not exist: $catalogPath"
}
if (Test-ReparsePointPath $resolvedRoot $catalogPath) {
    throw 'AICommand directory is located behind a junction or symlink.'
}

$bytes = [IO.File]::ReadAllBytes($catalogPath)
try {
    $catalogText = $strictUtf8.GetString($bytes)
}
catch {
    throw "AICommand directory is not strict UTF-8: $($_.Exception.Message)"
}

try {
    $catalog = $catalogText | ConvertFrom-Json
}
catch {
    throw "AICommand directory JSON is invalid: $($_.Exception.Message)"
}
if ($null -eq $catalog -or $catalog.schemaVersion -ne 1 -or $null -eq $catalog.commands) {
    throw 'AICommand directory schemaVersion must be 1 and contain commands.'
}
$commandRoot = [IO.Path]::GetFullPath((Join-Path $resolvedRoot 'Assets\Plugins\ES\AICommands'))
$ids = @{}
$paths = @{}
foreach ($entry in $catalog.commands) {
    Test-ContractEntry $entry $ids $paths $resolvedRoot $commandRoot
}

$selectionMode = 'query'
if ($PSCmdlet.ParameterSetName -eq 'ExactPath') {
    $selectionMode = 'exact-path'
    $normalizedPath = ([string]$CommandPath).Replace('\', '/').Trim()
    if (-not (Test-ProjectRelativeCommandPath $normalizedPath)) {
        throw 'CommandPath must be a traversal-free AICommand project-relative Markdown path.'
    }
    $matches = foreach ($entry in $catalog.commands) {
        if ($Role -ne 'all' -and $entry.role -ne $Role) { continue }
        if ($RiskLevel -ne 'all' -and $entry.riskLevel -ne $RiskLevel) { continue }
        if ([string]$entry.path -ne $normalizedPath) { continue }
        [pscustomobject]@{
            id        = [string]$entry.id
            title     = [string]$entry.title
            summary   = [string]$entry.summary
            role      = [string]$entry.role
            riskLevel = [string]$entry.riskLevel
            writeMode = [string]$entry.writeMode
            keywords  = [string]$entry.keywords
            path      = [string]$entry.path
            score     = 0
        }
    }
    if (@($matches).Count -ne 1) {
        throw 'CommandPath is not one current AICommand catalog contract after role/risk filtering.'
    }
}
else {
    $terms = @($Query.Trim().Split(@(' ', "`t", "`r", "`n"), [StringSplitOptions]::RemoveEmptyEntries))
    if ($terms.Count -eq 0) {
        throw 'Query must contain at least one non-whitespace term.'
    }

    $matches = foreach ($entry in $catalog.commands) {
        if ($Role -ne 'all' -and $entry.role -ne $Role) { continue }
        if ($RiskLevel -ne 'all' -and $entry.riskLevel -ne $RiskLevel) { continue }
        $score = Get-Score $entry $terms
        if ($score -le 0) { continue }
        [pscustomobject]@{
            id        = [string]$entry.id
            title     = [string]$entry.title
            summary   = [string]$entry.summary
            role      = [string]$entry.role
            riskLevel = [string]$entry.riskLevel
            writeMode = [string]$entry.writeMode
            keywords  = [string]$entry.keywords
            path      = [string]$entry.path
            score     = $score
        }
    }
}

$matched = @($matches | Sort-Object @{ Expression = 'score'; Descending = $true }, title)
$candidates = @($matched | Select-Object -First $MaxResults)
$result = [pscustomobject]@{
    schemaVersion = 1
    selectionMode = $selectionMode
    query = if ($selectionMode -eq 'exact-path') { $normalizedPath } else { $Query.Trim() }
    catalogPath = 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
    catalogSha256 = Get-Sha256 $bytes
    totalContracts = @($catalog.commands).Count
    matchedCount = $matched.Count
    returnedCount = $candidates.Count
    candidates = $candidates
    selectionRule = 'This result is discovery-only. Read exactly one selected Markdown contract in full and recompute its SHA-256 before relying on it.'
}

if ($Json) {
    $result | ConvertTo-Json -Depth 5
    exit 0
}

"AICommand candidates: $($result.returnedCount)/$($result.matchedCount) matched, total $($result.totalContracts) | catalog SHA-256: $($result.catalogSha256)"
foreach ($candidate in $result.candidates) {
    "[$($candidate.score)] $($candidate.title) | $($candidate.role) | $($candidate.riskLevel) | $($candidate.writeMode)"
    "  $($candidate.summary)"
    "  $($candidate.path)"
}
if ($result.candidates.Count -eq 0) {
    'No matching contract. Refine the terms or use the explicit catalog path only when a full browse is necessary.'
}
