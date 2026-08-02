[CmdletBinding()]
param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$syncDirectory = Split-Path -Parent $PSCommandPath
$repository = (& git -C $syncDirectory rev-parse --show-toplevel).Trim()
$syncPath = Join-Path $syncDirectory 'DOCUMENT_SYNC.json'
$ledgerPath = Join-Path $syncDirectory 'DOCUMENT_LOCAL_UPDATE_LEDGER.json'
$summaryPath = Join-Path $syncDirectory 'DOCUMENT_LOCAL_UPDATE_LEDGER.md'
$problems = [System.Collections.Generic.List[string]]::new()

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

function Test-IndexContains([string]$relativePath, [string[]]$stagedPaths) {
    $target = Normalize-RepositoryPath $relativePath
    return @($stagedPaths | Where-Object { (Normalize-RepositoryPath $_).Equals($target, [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
}

function Test-NoUnstagedDifference([string]$relativePath) {
    & git -C $repository diff --quiet -- $relativePath
    return $LASTEXITCODE -eq 0
}

$sync = Get-Content -LiteralPath $syncPath -Raw -Encoding UTF8 | ConvertFrom-Json
$ledger = Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
$summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8
$documentPath = Join-Path $repository $sync.document.path
$excludePathspecs = @($sync.policy.excludeFromSourceSnapshot | ForEach-Object { ':(exclude)' + $_ })

if ([int]$sync.schemaVersion -ne 2) { $problems.Add('DOCUMENT_SYNC.json schemaVersion must be 2.') }
if ([int]$ledger.schemaVersion -ne 1) { $problems.Add('DOCUMENT_LOCAL_UPDATE_LEDGER.json schemaVersion must be 1.') }
if (-not (Test-Path -LiteralPath $documentPath -PathType Leaf)) {
    $problems.Add("Document is missing: $($sync.document.path)")
}
else {
    $documentHash = (Get-FileHash -LiteralPath $documentPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($documentHash -ne [string]$sync.document.sha256) {
        $problems.Add('Document hash changed without a synchronized DOCUMENT_SYNC.json update.')
    }
}

$ledgerHash = (Get-FileHash -LiteralPath $ledgerPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($ledgerHash -ne [string]$sync.localUpdateLedger.manifestSha256) {
    $problems.Add('Local update ledger changed without a synchronized DOCUMENT_SYNC.json manifest hash.')
}
if ($summary -notmatch [regex]::Escape([string]$ledger.batch.id)) {
    $problems.Add('Human-readable local update summary does not name the active batch id.')
}

$head = (Get-GitText @('rev-parse', 'HEAD')).Trim()
$stagedPatch = Get-GitText (@('-c', 'core.safecrlf=false', 'diff', '--cached', '--binary', 'HEAD', '--', '.') + $excludePathspecs)
$stagedManifest = Get-GitText (@('-c', 'core.quotePath=false', 'diff', '--cached', '--name-status', '--diff-filter=ACDMRTUXB', 'HEAD', '--', '.') + $excludePathspecs)
$stagedSourcePathText = Get-GitText (@('-c', 'core.quotePath=false', 'diff', '--cached', '--name-only', '--diff-filter=ACDMRTUXB', 'HEAD', '--', '.') + $excludePathspecs)
$stagedSourcePaths = @($stagedSourcePathText -split "`n" | Where-Object { $_ })
$allStagedPathText = Get-GitText @('-c', 'core.quotePath=false', 'diff', '--cached', '--name-only', '--diff-filter=ACDMRTUXB', 'HEAD', '--', '.')
$allStagedPaths = @($allStagedPathText -split "`n" | Where-Object { $_ })

$htmlIsStaged = Test-IndexContains ([string]$sync.document.path) $allStagedPaths
if ($htmlIsStaged -and [string]$ledger.batch.status -notin @('ready-for-html', 'integrated')) {
    $problems.Add("HTML cannot be committed while the local batch status is $([string]$ledger.batch.status).")
}

if ($stagedSourcePaths.Count -gt 0) {
    $gate = $ledger.commitGate
    if ($null -eq $gate -or [string]$gate.mode -ne 'staged-only') {
        $problems.Add('No staged-only commit fingerprint exists. Run Prepare-DocumentStagedBatch.ps1 -EntryId <id>.')
    }
    else {
        if ([string]$gate.head -ne $head) { $problems.Add('The staged batch was prepared for a different HEAD.') }
        if ([string]$gate.stagedSourcePatchSha256 -ne (Get-TextSha256 $stagedPatch)) { $problems.Add('The staged source patch changed after batch preparation.') }
        if ([string]$gate.stagedSourceManifestSha256 -ne (Get-TextSha256 $stagedManifest)) { $problems.Add('The staged source file manifest changed after batch preparation.') }
        if ([int]$gate.stagedSourceFileCount -ne $stagedSourcePaths.Count) { $problems.Add('The staged source file count changed after batch preparation.') }

        $entryIds = @($gate.entryIds | Where-Object { $_ })
        if ($entryIds.Count -eq 0) {
            $problems.Add('The staged batch has no ledger entry ids.')
        }
        else {
            $selectedEntries = @()
            foreach ($id in $entryIds) {
                $matches = @($ledger.entries | Where-Object { [string]$_.id -eq [string]$id })
                if ($matches.Count -ne 1) {
                    $problems.Add("Staged batch ledger entry must exist exactly once: $id")
                    continue
                }
                $entry = $matches[0]
                $selectedEntries += $entry
                if ($summary -notmatch [regex]::Escape([string]$id)) { $problems.Add("Staged batch entry is absent from the human-readable summary: $id") }
                if ([string]$entry.status -notin @('documented', 'ready-for-regression', 'ready-for-html', 'integrated')) { $problems.Add("Staged batch entry is incomplete: $id ($([string]$entry.status))") }
                if ([string]$entry.analysis.status -ne 'complete') { $problems.Add("Staged batch entry needs completed analysis: $id") }
                if (@($entry.sourcePaths | Where-Object { $_ }).Count -eq 0) { $problems.Add("Staged batch entry needs sourcePaths: $id") }
                if (@($entry.evidencePaths | Where-Object { $_ }).Count -eq 0) { $problems.Add("Staged batch entry needs evidencePaths: $id") }
                if (@($entry.html.targets | Where-Object { $_ }).Count -eq 0) { $problems.Add("Staged batch entry needs HTML targets: $id") }
            }

            $coveredSourcePaths = @($selectedEntries | ForEach-Object { $_.sourcePaths } | Where-Object { $_ })
            foreach ($path in $stagedSourcePaths) {
                if (-not (Test-PathCovered $path $coveredSourcePaths)) {
                    $problems.Add("Staged source path is not covered by the selected ledger entries: $path")
                }
            }
        }
    }

    $requiredLedgerFiles = @(
        'ES/Documentation/StaticSite/DOCUMENT_LOCAL_UPDATE_LEDGER.json',
        'ES/Documentation/StaticSite/DOCUMENT_SYNC.json'
    )
    foreach ($requiredPath in $requiredLedgerFiles) {
        if (-not (Test-IndexContains $requiredPath $allStagedPaths)) {
            $problems.Add("Staged source changes require the synchronized ledger file in the same commit: $requiredPath")
        }
        elseif (-not (Test-NoUnstagedDifference $requiredPath)) {
            $problems.Add("The staged ledger file differs from its working copy; stage it again: $requiredPath")
        }
    }
}

if ($problems.Count -gt 0) {
    if (-not $Quiet) {
        Write-Host 'Staged document batch validation failed:' -ForegroundColor Red
        $problems | ForEach-Object { Write-Host "- $_" -ForegroundColor Red }
    }
    exit 1
}

if (-not $Quiet) {
    if ($stagedSourcePaths.Count -eq 0) {
        Write-Host 'Staged document batch validation passed: no staged source files.' -ForegroundColor Green
    }
    else {
        Write-Host "Staged document batch validation passed for $($stagedSourcePaths.Count) source files." -ForegroundColor Green
    }
}
