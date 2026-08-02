[CmdletBinding()]
param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$syncDirectory = Split-Path -Parent $PSCommandPath
$repository = (& git -C $syncDirectory rev-parse --show-toplevel).Trim()
$syncPath = Join-Path $syncDirectory 'DOCUMENT_SYNC.json'
$readerStandardPath = Join-Path $syncDirectory 'DOCUMENT_READER_STANDARD.md'
$sync = Get-Content -LiteralPath $syncPath -Raw -Encoding UTF8 | ConvertFrom-Json
$documentPath = Join-Path $repository $sync.document.path
$localLedgerConfiguration = $sync.localUpdateLedger
$excludePathspecs = @($sync.policy.excludeFromSourceSnapshot | ForEach-Object { ':(exclude)' + $_ })
$problems = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $readerStandardPath -PathType Leaf)) {
    $problems.Add('Reader presentation standard is missing: ES/Documentation/StaticSite/DOCUMENT_READER_STANDARD.md.')
}
if ([int]$sync.schemaVersion -ne 2) {
    $problems.Add('DOCUMENT_SYNC.json schemaVersion must be 2 for local update integration.')
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

$currentHead = (Get-GitText @('rev-parse', 'HEAD')).Trim()
$headDrift = $currentHead -ne $sync.baseline.head

$trackedPatch = Get-GitText (@('-c', 'core.safecrlf=false', 'diff', '--binary', $sync.baseline.head, '--', '.') + $excludePathspecs)
$trackedPatchHash = Get-TextSha256 $trackedPatch
$trackedDrift = $trackedPatchHash -ne $sync.baseline.trackedWorktreePatchSha256

$stagedPatch = Get-GitText (@('-c', 'core.safecrlf=false', 'diff', '--cached', '--binary', $sync.baseline.head, '--', '.') + $excludePathspecs)
$stagedPatchHash = Get-TextSha256 $stagedPatch
$stagedDrift = $stagedPatchHash -ne $sync.baseline.stagedSourcePatchSha256

$untrackedOutput = @(& git -C $repository -c core.quotePath=false ls-files --others --exclude-standard)
if ($LASTEXITCODE -ne 0) {
    throw 'Git command failed: git ls-files --others --exclude-standard'
}

$untrackedLines = @($untrackedOutput | Where-Object {
    $_ -and $_ -notlike 'ES/Documentation/StaticSite/*' -and $_ -notlike '.githooks/*'
})
$manifest = foreach ($relativePath in $untrackedLines) {
    $fullPath = Join-Path $repository $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Untracked source path is invalid: $relativePath"
    }

    '{0}`t{1}' -f $relativePath, (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
$untrackedHash = Get-TextSha256 ($manifest -join "`n")
$untrackedDrift = $manifest.Count -ne [int]$sync.baseline.untrackedSourceFileCount -or $untrackedHash -ne $sync.baseline.untrackedSourceManifestSha256

$documentHash = $null
$documentSize = $null
if (-not (Test-Path -LiteralPath $documentPath -PathType Leaf)) {
    $problems.Add("Document is missing: $($sync.document.path)")
}
else {
    $documentHash = (Get-FileHash -LiteralPath $documentPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $documentSize = (Get-Item -LiteralPath $documentPath).Length
    if ($documentHash -ne $sync.document.sha256) {
        $problems.Add('Document hash changed without a synchronized DOCUMENT_SYNC.json update.')
    }

    $document = Get-Content -LiteralPath $documentPath -Raw -Encoding UTF8
    $ids = [regex]::Matches($document, '\bid="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $references = [regex]::Matches($document, '\bhref="#([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $duplicates = @($ids | Group-Object | Where-Object Count -gt 1)
    $missing = @($references | Sort-Object -Unique | Where-Object { $_ -notin $ids })
    $external = [regex]::Matches($document, '(?i)<(?:script|link)\b[^>]+(?:src|href)="https?://').Count
    if ($duplicates.Count -ne 0) { $problems.Add('Document has duplicate HTML id values.') }
    if ($missing.Count -ne 0) { $problems.Add('Document has missing internal anchor targets.') }
    if ($external -ne 0) { $problems.Add('Document has external CSS or JavaScript dependencies.') }
    if ($document -notmatch 'sync_record: ES/Documentation/StaticSite/DOCUMENT_SYNC.md') { $problems.Add('Document is missing its human-readable sync-record pointer.') }
    if ($document -notmatch 'reader_standard: ES/Documentation/StaticSite/DOCUMENT_READER_STANDARD.md') { $problems.Add('Document is missing its reader-standard pointer.') }
    if ($document -notmatch 'local_update_ledger: ES/Documentation/StaticSite/DOCUMENT_LOCAL_UPDATE_LEDGER.md') { $problems.Add('Document is missing its local-update-ledger pointer.') }
}

function Resolve-RepositoryFile([string]$relativePath) {
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        throw 'A ledger path is empty.'
    }

    $repositoryRoot = [System.IO.Path]::GetFullPath($repository)
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $relativePath))
    $prefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Ledger path escapes the repository: $relativePath"
    }

    return $candidate
}

$ledgerIssues = [System.Collections.Generic.List[string]]::new()
$entryIssues = [System.Collections.Generic.List[string]]::new()
$ledger = $null
$ledgerSummary = $null
$ledgerSnapshotMatches = $false
$ledgerDocumentMatches = $false
$ledgerEntriesComplete = $false
$ledgerBatchStatus = $null

if ($null -eq $localLedgerConfiguration) {
    $ledgerIssues.Add('DOCUMENT_SYNC.json is missing localUpdateLedger configuration.')
}
else {
    try {
        $ledgerManifestPath = Resolve-RepositoryFile ([string]$localLedgerConfiguration.manifestPath)
        $ledgerSummaryPath = Resolve-RepositoryFile ([string]$localLedgerConfiguration.summaryPath)
        if (-not (Test-Path -LiteralPath $ledgerManifestPath -PathType Leaf)) {
            $ledgerIssues.Add("Local update ledger is missing: $($localLedgerConfiguration.manifestPath).")
        }
        if (-not (Test-Path -LiteralPath $ledgerSummaryPath -PathType Leaf)) {
            $ledgerIssues.Add("Local update summary is missing: $($localLedgerConfiguration.summaryPath).")
        }

        if ($ledgerIssues.Count -eq 0) {
            $ledgerManifestHash = (Get-FileHash -LiteralPath $ledgerManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($ledgerManifestHash -ne [string]$localLedgerConfiguration.manifestSha256) {
                $ledgerIssues.Add('Local update ledger changed without a synchronized DOCUMENT_SYNC.json manifest hash.')
            }

            $ledger = Get-Content -LiteralPath $ledgerManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $ledgerSummary = Get-Content -LiteralPath $ledgerSummaryPath -Raw -Encoding UTF8
            if ([int]$ledger.schemaVersion -ne 1) {
                $ledgerIssues.Add('Local update ledger schemaVersion must be 1.')
            }

            $ledgerBatchStatus = [string]$ledger.batch.status
            $allowedBatchStatuses = @('collecting', 'ready-for-regression', 'ready-for-html', 'integrated')
            if ($allowedBatchStatuses -notcontains $ledgerBatchStatus) {
                $ledgerIssues.Add("Local update batch has an invalid status: $ledgerBatchStatus.")
            }
            if ([string]$ledger.batch.id -ne [string]$localLedgerConfiguration.batchId) {
                $ledgerIssues.Add('Local update batch id differs from DOCUMENT_SYNC.json.')
            }
            if ($ledgerBatchStatus -ne [string]$localLedgerConfiguration.status) {
                $ledgerIssues.Add('Local update batch status differs from DOCUMENT_SYNC.json.')
            }
            if ([string]$localLedgerConfiguration.htmlWritePolicy -ne 'batch-only-after-ready-for-html') {
                $ledgerIssues.Add('Local update ledger htmlWritePolicy must require a ready-for-html batch.')
            }
            if ($ledgerSummary -notmatch [regex]::Escape([string]$ledger.batch.id)) {
                $ledgerIssues.Add('Human-readable local update summary does not name the active batch id.')
            }

            $snapshot = $ledger.batch.sourceSnapshot
            $ledgerSnapshotMatches = $null -ne $snapshot -and
                [string]$snapshot.head -eq $currentHead -and
                [string]$snapshot.trackedWorktreePatchSha256 -eq $trackedPatchHash -and
                [string]$snapshot.stagedSourcePatchSha256 -eq $stagedPatchHash -and
                [string]$snapshot.untrackedSourceManifestSha256 -eq $untrackedHash -and
                [int]$snapshot.untrackedSourceFileCount -eq $manifest.Count
            if (-not $ledgerSnapshotMatches) {
                $ledgerIssues.Add('Local update ledger snapshot does not match the current source worktree.')
            }

            $documentBefore = $ledger.batch.documentBeforeIntegration
            $ledgerDocumentMatches = $null -ne $documentHash -and $null -ne $documentBefore -and
                [string]$documentBefore.sha256 -eq $documentHash -and
                [int64]$documentBefore.sizeBytes -eq $documentSize
            if ($ledgerBatchStatus -ne 'integrated' -and -not $ledgerDocumentMatches) {
                $ledgerIssues.Add('HTML changed while the local batch is not integrated; restore the intake document or complete the batch integration.')
            }

            $entries = @($ledger.entries)
            if ($entries.Count -eq 0) {
                $entryIssues.Add('Local update batch has no entries.')
            }
            foreach ($entry in $entries) {
                $entryId = [string]$entry.id
                if ([string]::IsNullOrWhiteSpace($entryId)) {
                    $entryIssues.Add('A local update entry has no id.')
                    continue
                }

                if ($ledgerSummary -notmatch [regex]::Escape($entryId)) {
                    $entryIssues.Add("Local update entry $entryId is absent from the human-readable summary.")
                }
                if ([string]$entry.summary -match '^\s*$' -or ([string]$entry.summary).Length -lt 24) {
                    $entryIssues.Add("Local update entry $entryId needs an independently useful behavior summary.")
                }
                if (@($entry.sourcePaths | Where-Object { $_ }).Count -eq 0) {
                    $entryIssues.Add("Local update entry $entryId needs sourcePaths.")
                }
                if (@($entry.evidencePaths | Where-Object { $_ }).Count -eq 0) {
                    $entryIssues.Add("Local update entry $entryId needs evidencePaths.")
                }
                if ([string]$entry.status -notin @('documented', 'ready-for-regression', 'ready-for-html', 'integrated')) {
                    $entryIssues.Add("Local update entry $entryId is not ready to defer HTML integration: $([string]$entry.status).")
                }
                if ([string]$entry.analysis.status -ne 'complete') {
                    $entryIssues.Add("Local update entry $entryId needs completed analysis.")
                }
                if (@($entry.html.targets | Where-Object { $_ }).Count -eq 0) {
                    $entryIssues.Add("Local update entry $entryId needs explicit HTML targets.")
                }

                if ($ledgerBatchStatus -eq 'ready-for-html') {
                    $regressionStatus = [string]$entry.regression.status
                    $acceptedGap = $regressionStatus -eq 'accepted-with-known-gaps' -and @($entry.regression.knownGaps | Where-Object { $_ }).Count -gt 0
                    if ($regressionStatus -ne 'passed' -and -not $acceptedGap) {
                        $entryIssues.Add("Local update entry $entryId is not ready for HTML regression integration.")
                    }
                }
            }

            $ledgerEntriesComplete = $entryIssues.Count -eq 0
            if ($ledgerBatchStatus -eq 'ready-for-html' -and [string]$ledger.integration.status -ne 'ready') {
                $ledgerIssues.Add('A ready-for-html batch must set integration.status to ready.')
            }
            if ($ledgerBatchStatus -eq 'integrated') {
                if ([string]$ledger.integration.status -ne 'integrated') {
                    $ledgerIssues.Add('An integrated batch must set integration.status to integrated.')
                }
                if ($null -eq $ledger.integration.htmlAfterIntegration -or [string]$ledger.integration.htmlAfterIntegration.sha256 -ne [string]$sync.document.sha256) {
                    $ledgerIssues.Add('An integrated batch must record the final HTML sha256.')
                }
                if ([string]$snapshot.head -ne [string]$sync.baseline.head -or
                    [string]$snapshot.trackedWorktreePatchSha256 -ne [string]$sync.baseline.trackedWorktreePatchSha256 -or
                    [string]$snapshot.stagedSourcePatchSha256 -ne [string]$sync.baseline.stagedSourcePatchSha256 -or
                    [string]$snapshot.untrackedSourceManifestSha256 -ne [string]$sync.baseline.untrackedSourceManifestSha256 -or
                    [int]$snapshot.untrackedSourceFileCount -ne [int]$sync.baseline.untrackedSourceFileCount) {
                    $ledgerIssues.Add('An integrated batch must match the advanced DOCUMENT_SYNC baseline.')
                }
            }
        }
    }
    catch {
        $ledgerIssues.Add("Cannot validate local update ledger: $($_.Exception.Message)")
    }
}

$sourceDrift = $headDrift -or $trackedDrift -or $stagedDrift -or $untrackedDrift
$canDeferSourceDrift = $ledgerIssues.Count -eq 0 -and $ledgerEntriesComplete -and $ledgerSnapshotMatches -and $ledgerDocumentMatches -and $ledgerBatchStatus -in @('collecting', 'ready-for-regression', 'ready-for-html')
if ($sourceDrift -and -not $canDeferSourceDrift) {
    if ($headDrift) {
        $problems.Add("HEAD changed from $($sync.baseline.head) to $currentHead; update documentation and advance DOCUMENT_SYNC.json.")
    }
    if ($trackedDrift) { $problems.Add('Tracked source worktree diff changed; documentation synchronization is stale.') }
    if ($stagedDrift) { $problems.Add('Staged source diff changed; documentation synchronization is stale.') }
    if ($untrackedDrift) { $problems.Add('Untracked source manifest changed; documentation synchronization is stale.') }
    $ledgerIssues | ForEach-Object { $problems.Add($_) }
    $entryIssues | ForEach-Object { $problems.Add($_) }
}
elseif ($sourceDrift -and -not $Quiet) {
    Write-Host "Local update batch $($ledger.batch.id) covers the current source drift; HTML integration is intentionally deferred." -ForegroundColor Yellow
}

if ($problems.Count -gt 0) {
    if (-not $Quiet) {
        Write-Host 'DOCUMENT_SYNC validation failed:' -ForegroundColor Red
        $problems | ForEach-Object { Write-Host "- $_" -ForegroundColor Red }
    }

    exit 1
}

if (-not $Quiet) {
    Write-Host "DOCUMENT_SYNC validation passed for $($sync.document.path)." -ForegroundColor Green
}
