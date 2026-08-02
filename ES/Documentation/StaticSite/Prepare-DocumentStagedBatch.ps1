[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$EntryId,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$syncDirectory = Split-Path -Parent $PSCommandPath
$repository = (& git -C $syncDirectory rev-parse --show-toplevel).Trim()
$syncPath = Join-Path $syncDirectory 'DOCUMENT_SYNC.json'
$ledgerPath = Join-Path $syncDirectory 'DOCUMENT_LOCAL_UPDATE_LEDGER.json'

function Get-TextSha256([string]$value) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
    }
    finally {
        $sha.Dispose()
    }
}

function Get-GitText([string[]]$arguments) {
    $lines = @(& git -C $repository @arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($arguments -join ' ')"
    }

    return ($lines -join "`n")
}

function Normalize-RepositoryPath([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return '' }
    $normalized = $path.Replace('\', '/').Trim()
    while ($normalized.StartsWith('./', [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }
    return $normalized.TrimEnd('/')
}

function Test-PathCovered([string]$path, [string[]]$sourcePaths) {
    $normalizedPath = Normalize-RepositoryPath $path
    foreach ($sourcePath in $sourcePaths) {
        $normalizedSource = Normalize-RepositoryPath $sourcePath
        if ([string]::IsNullOrWhiteSpace($normalizedSource)) { continue }
        if ($normalizedPath.Equals($normalizedSource, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        if ($normalizedPath.StartsWith($normalizedSource + '/', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

$sync = Get-Content -LiteralPath $syncPath -Raw -Encoding UTF8 | ConvertFrom-Json
$ledger = Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
$excludePathspecs = @($sync.policy.excludeFromSourceSnapshot | ForEach-Object { ':(exclude)' + $_ })
$head = (Get-GitText @('rev-parse', 'HEAD')).Trim()
$stagedPatch = Get-GitText (@('-c', 'core.safecrlf=false', 'diff', '--cached', '--binary', 'HEAD', '--', '.') + $excludePathspecs)
$stagedManifest = Get-GitText (@('-c', 'core.quotePath=false', 'diff', '--cached', '--name-status', '--diff-filter=ACDMRTUXB', 'HEAD', '--', '.') + $excludePathspecs)
$stagedPathText = Get-GitText (@('-c', 'core.quotePath=false', 'diff', '--cached', '--name-only', '--diff-filter=ACDMRTUXB', 'HEAD', '--', '.') + $excludePathspecs)
$stagedPaths = @($stagedPathText -split "`n" | Where-Object { $_ })

if ($stagedPaths.Count -eq 0) {
    throw 'No staged source files were found. Stage one coherent source batch before preparing its ledger fingerprint.'
}

$requestedIds = @($EntryId | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
if ($requestedIds.Count -eq 0) {
    throw 'At least one non-empty -EntryId is required.'
}

$selectedEntries = @()
foreach ($id in $requestedIds) {
    $matches = @($ledger.entries | Where-Object { [string]$_.id -eq $id })
    if ($matches.Count -ne 1) {
        throw "Ledger entry must exist exactly once: $id"
    }
    $entry = $matches[0]
    if ([string]$entry.status -notin @('documented', 'ready-for-regression', 'ready-for-html', 'integrated')) {
        throw "Ledger entry is not complete enough for a staged commit: $id ($([string]$entry.status))"
    }
    if ([string]$entry.analysis.status -ne 'complete') {
        throw "Ledger entry needs completed analysis: $id"
    }
    if (@($entry.sourcePaths | Where-Object { $_ }).Count -eq 0) {
        throw "Ledger entry needs sourcePaths: $id"
    }
    if (@($entry.evidencePaths | Where-Object { $_ }).Count -eq 0) {
        throw "Ledger entry needs evidencePaths: $id"
    }
    if (@($entry.html.targets | Where-Object { $_ }).Count -eq 0) {
        throw "Ledger entry needs HTML targets: $id"
    }
    $selectedEntries += $entry
}

$coveredSourcePaths = @($selectedEntries | ForEach-Object { $_.sourcePaths } | Where-Object { $_ })
$uncovered = @($stagedPaths | Where-Object { -not (Test-PathCovered $_ $coveredSourcePaths) })
if ($uncovered.Count -gt 0) {
    throw "Staged source paths are not covered by the selected ledger entries:`n$($uncovered -join "`n")"
}

$commitGate = [pscustomobject][ordered]@{
    mode = 'staged-only'
    preparedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK')
    head = $head
    stagedSourcePatchSha256 = Get-TextSha256 $stagedPatch
    stagedSourceManifestSha256 = Get-TextSha256 $stagedManifest
    stagedSourceFileCount = $stagedPaths.Count
    entryIds = $requestedIds
}
$ledger | Add-Member -NotePropertyName commitGate -NotePropertyValue $commitGate -Force

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$ledgerJson = $ledger | ConvertTo-Json -Depth 16
[System.IO.File]::WriteAllText($ledgerPath, $ledgerJson + [Environment]::NewLine, $utf8NoBom)

$sync.localUpdateLedger.manifestSha256 = (Get-FileHash -LiteralPath $ledgerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$syncJson = $sync | ConvertTo-Json -Depth 16
[System.IO.File]::WriteAllText($syncPath, $syncJson + [Environment]::NewLine, $utf8NoBom)

if (-not $Quiet) {
    Write-Host "Prepared staged source batch: $($stagedPaths.Count) files." -ForegroundColor Green
    Write-Host "Ledger entries: $($requestedIds -join ', ')" -ForegroundColor Green
    Write-Host 'Now stage DOCUMENT_LOCAL_UPDATE_LEDGER.json and DOCUMENT_SYNC.json before committing.' -ForegroundColor Yellow
}
