[CmdletBinding()]
param(
    [switch]$RefreshSnapshot,
    [ValidateSet('collecting', 'ready-for-regression', 'ready-for-html')]
    [string]$BatchStatus
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$syncDirectory = Split-Path -Parent $PSCommandPath
$repository = (& git -C $syncDirectory rev-parse --show-toplevel).Trim()
$syncPath = Join-Path $syncDirectory 'DOCUMENT_SYNC.json'
$ledgerPath = Join-Path $syncDirectory 'DOCUMENT_LOCAL_UPDATE_LEDGER.json'

if (-not $RefreshSnapshot) {
    throw 'Use -RefreshSnapshot after recording entries. This helper never advances the reviewed source baseline or edits the HTML.'
}

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

$sync = Get-Content -LiteralPath $syncPath -Raw -Encoding UTF8 | ConvertFrom-Json
$ledger = Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
$excludePathspecs = @($sync.policy.excludeFromSourceSnapshot | ForEach-Object { ':(exclude)' + $_ })
$head = (Get-GitText @('rev-parse', 'HEAD')).Trim()
$baselineHead = [string]$sync.baseline.head
if ([string]::IsNullOrWhiteSpace($baselineHead)) {
    throw 'DOCUMENT_SYNC.json baseline.head is required to refresh the local update snapshot.'
}

# The verifier compares source state with the document baseline, not with HEAD.
# Keep this helper on that same comparison basis so pushed commits can be deferred safely.
$trackedPatch = Get-GitText (@('-c', 'core.safecrlf=false', 'diff', '--binary', $baselineHead, '--', '.') + $excludePathspecs)
$stagedPatch = Get-GitText (@('-c', 'core.safecrlf=false', 'diff', '--cached', '--binary', $baselineHead, '--', '.') + $excludePathspecs)
$untrackedLines = @(& git -C $repository -c core.quotePath=false ls-files --others --exclude-standard | Where-Object {
    $_ -and $_ -notlike 'ES/Documentation/StaticSite/*' -and $_ -notlike '.githooks/*'
})
$manifest = foreach ($relativePath in $untrackedLines) {
    $fullPath = Join-Path $repository $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Untracked source path is invalid: $relativePath"
    }

    '{0}`t{1}' -f $relativePath, (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

$ledger.batch.sourceSnapshot.head = $head
$snapshotBasis = [ordered]@{
    comparison = 'document-baseline-to-current-worktree'
    baselineHead = $baselineHead
}
$ledger.batch.sourceSnapshot | Add-Member -NotePropertyName 'basis' -NotePropertyValue $snapshotBasis -Force
$ledger.batch.sourceSnapshot.trackedWorktreePatchSha256 = Get-TextSha256 $trackedPatch
$ledger.batch.sourceSnapshot.stagedSourcePatchSha256 = Get-TextSha256 $stagedPatch
$ledger.batch.sourceSnapshot.untrackedSourceManifestSha256 = Get-TextSha256 ($manifest -join "`n")
$ledger.batch.sourceSnapshot.untrackedSourceFileCount = $manifest.Count
$ledger.batch.updatedAt = (Get-Date).ToString('yyyy-MM-dd')
if ($BatchStatus) {
    $ledger.batch.status = $BatchStatus
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$ledgerJson = $ledger | ConvertTo-Json -Depth 16
[System.IO.File]::WriteAllText($ledgerPath, $ledgerJson + [Environment]::NewLine, $utf8NoBom)

$sync.localUpdateLedger.batchId = $ledger.batch.id
$sync.localUpdateLedger.status = $ledger.batch.status
$sync.localUpdateLedger.manifestSha256 = (Get-FileHash -LiteralPath $ledgerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$syncJson = $sync | ConvertTo-Json -Depth 16
[System.IO.File]::WriteAllText($syncPath, $syncJson + [Environment]::NewLine, $utf8NoBom)

Write-Host "Refreshed local update batch $($ledger.batch.id) at $($ledger.batch.updatedAt)." -ForegroundColor Green
Write-Host 'The reviewed source baseline and HTML were not changed.' -ForegroundColor Yellow
