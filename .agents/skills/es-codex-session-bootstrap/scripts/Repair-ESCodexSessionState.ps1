[CmdletBinding()]
param(
    [string]$SessionId = '',
    [string]$ResponsibilityKey = '',
    [switch]$Apply,
    [switch]$SkipUiObservation,
    [switch]$SkipReadinessRefresh,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')

$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$registryPath = Join-Path $localStateRoot 'sessions.json'
$historyPath = Join-Path $env:USERPROFILE '.codex\history.jsonl'
$statusArguments = @{ IncludeClosed = $true; SkipUiObservation = $SkipUiObservation; SkipReadinessRefresh = $SkipReadinessRefresh; StateRoot = $localStateRoot }
if (-not [string]::IsNullOrWhiteSpace($SessionId)) { $statusArguments.SessionId = $SessionId }
if (-not [string]::IsNullOrWhiteSpace($ResponsibilityKey)) { $statusArguments.ResponsibilityKey = $ResponsibilityKey }
$status = $null
try { $status = & (Join-Path $PSScriptRoot 'Get-ESCodexSessionStatus.ps1') @statusArguments }
catch {
    $authorityError = $_.Exception.Message
    if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) { throw }
    $rawBytes = [IO.File]::ReadAllBytes($registryPath)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $sourceHash = ([BitConverter]::ToString($sha.ComputeHash($rawBytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    try { $rawRegistry = [Text.Encoding]::UTF8.GetString($rawBytes) | ConvertFrom-Json }
    catch { throw "Codex session registry is not valid JSON and cannot be mechanically repaired: $registryPath" }
    $rawSessions = @(Get-ESCodexPropertyValue $rawRegistry 'sessions' @())
    $corruptionActions = @()
    for ($index = 0; $index -lt $rawSessions.Count; $index++) {
        $item = $rawSessions[$index]
        $identityValues = @('launchToken', 'sessionId', 'taskFingerprint', 'taskKey', 'tabTitle') | ForEach-Object { [string](Get-ESCodexPropertyValue $item $_ '') }
        if (@($identityValues | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -eq 0) {
            $corruptionActions += [pscustomobject][ordered]@{
                action = 'RemoveIdentityEmptyPlaceholder'
                recordId = [string](Get-ESCodexPropertyValue $item 'recordId' '')
                sessionId = ''
                sourceIndex = $index
                reason = 'Record has no launch, session, task, or tab identity and cannot own authority.'
                applicable = $true
            }
        }
    }
    foreach ($group in @($rawSessions | Group-Object { [string](Get-ESCodexPropertyValue $_ 'recordId' '') } | Where-Object { $_.Count -gt 1 -and -not [string]::IsNullOrWhiteSpace($_.Name) })) {
        $allIndexesArePlaceholders = @($corruptionActions | Where-Object recordId -eq $group.Name).Count -eq $group.Count
        if (-not $allIndexesArePlaceholders) {
            $corruptionActions += [pscustomobject][ordered]@{ action = 'ManualDuplicateAuthorityReview'; recordId = $group.Name; sessionId = ''; sourceIndex = -1; reason = 'Duplicate recordId includes at least one identity-bearing record.'; applicable = $false }
        }
    }
    $appliedCorruptionActions = @()
    $backupPath = ''
    if ($Apply) {
        $applicable = @($corruptionActions | Where-Object applicable)
        if ($applicable.Count -eq 0) { throw "Registry corruption has no mechanically safe repair action: $authorityError" }
        $mutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexSessionRegistryV2')
        $acquired = $false
        try {
            $acquired = $mutex.WaitOne(5000)
            if (-not $acquired) { throw 'Timed out waiting for the Codex session registry mutex.' }
            $currentBytes = [IO.File]::ReadAllBytes($registryPath)
            $currentSha = [Security.Cryptography.SHA256]::Create()
            try { $currentHash = ([BitConverter]::ToString($currentSha.ComputeHash($currentBytes))).Replace('-', '').ToLowerInvariant() }
            finally { $currentSha.Dispose() }
            if ($currentHash -ne $sourceHash) { throw 'Registry changed after the corruption plan was created. Re-run Repair.' }
            $removeIndexes = @($applicable | ForEach-Object { [int]$_.sourceIndex })
            $kept = for ($index = 0; $index -lt $rawSessions.Count; $index++) { if ($index -notin $removeIndexes) { $rawSessions[$index] } }
            $normalized = @($kept | ForEach-Object { ConvertTo-ESCodexSessionRecord $_ })
            $duplicateIds = @($normalized | Group-Object recordId | Where-Object Count -gt 1)
            if ($duplicateIds.Count -gt 0) { throw 'Safe placeholder removal would leave duplicate authoritative record IDs; no write was performed.' }
            $backupRoot = Join-Path $localStateRoot 'repair-backups'
            [void][IO.Directory]::CreateDirectory($backupRoot)
            $backupPath = Join-Path $backupRoot ('sessions-corrupt-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '.json')
            $backupStream = [IO.File]::Open($backupPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
            try { $backupStream.Write($currentBytes, 0, $currentBytes.Length); $backupStream.Flush($true) }
            finally { $backupStream.Dispose() }
            $repairedRegistry = [pscustomobject]@{ schemaVersion = 2; sourceSchemaVersion = 2; requiresWriteUpgrade = $false; revision = [int](Get-ESCodexPropertyValue $rawRegistry 'revision' 0); updatedUtc = [string](Get-ESCodexPropertyValue $rawRegistry 'updatedUtc' ''); sessions = $normalized }
            Save-ESCodexSessionRegistry $registryPath $repairedRegistry
            $appliedCorruptionActions = $applicable
        }
        finally { if ($acquired) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
    }
    [pscustomobject][ordered]@{
        mode = 'Repair'
        corruptionMode = $true
        dryRun = -not [bool]$Apply
        registryPath = $registryPath
        authorityError = $authorityError
        sourceSha256 = $sourceHash
        backupPath = $backupPath
        statusBefore = $null
        proposedActions = $corruptionActions
        appliedActions = $appliedCorruptionActions
        requiresExplicitApply = -not [bool]$Apply
    }
    return
}

$actions = @()
if ($status.registryNeedsUpgrade) {
    $actions += [pscustomobject][ordered]@{
        action = 'UpgradeAuthoritativeRegistrySchema'
        recordId = ''
        sessionId = ''
        reason = "Persist normalized registry schema v2 from source schema $($status.registrySourceSchemaVersion)."
        applicable = $true
    }
}
foreach ($session in @($status.sessions)) {
    $identityEmpty = [string]::IsNullOrWhiteSpace([string]$session.sessionId) -and
        [string]::IsNullOrWhiteSpace([string]$session.launchToken) -and
        [string]::IsNullOrWhiteSpace([string]$session.taskKey) -and
        [string]::IsNullOrWhiteSpace([string]$session.tabTitle)
    if ($identityEmpty) {
        $actions += [pscustomobject][ordered]@{
            action = 'RemoveIdentityEmptyPlaceholder'
            recordId = [string]$session.recordId
            sessionId = ''
            reason = 'Record has no launch, session, task, or tab identity and cannot own authority.'
            applicable = $true
        }
        continue
    }
    if (@($session.authorityGaps).Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$session.launchStatePath)) {
        $actions += [pscustomobject][ordered]@{
            action = 'HydrateAuthorityFromLaunchState'
            recordId = [string]$session.recordId
            sessionId = [string]$session.sessionId
            reason = 'Missing authoritative fields: ' + (@($session.authorityGaps) -join ', ')
            sourcePath = [string]$session.launchStatePath
            applicable = $true
        }
    }
    if ([string]::IsNullOrWhiteSpace([string]$session.sessionId) -and $session.status -in @('PendingRegistration', 'PendingPrompt', 'PendingAcceptance', 'PendingProcessLost')) {
        $resolvedSessionId = Find-ESCodexSessionIdByToken $historyPath ([string]$session.launchToken)
        if (-not [string]::IsNullOrWhiteSpace($resolvedSessionId)) {
            $actions += [pscustomobject][ordered]@{
                action = 'RegisterResolvedSessionId'
                recordId = [string]$session.recordId
                sessionId = $resolvedSessionId
                reason = 'Launch token was found in Codex history.'
                applicable = $true
            }
        }
        else {
            $actions += [pscustomobject][ordered]@{
                action = 'KeepPendingForReview'
                recordId = [string]$session.recordId
                sessionId = ''
                reason = 'No exact session ID was found for the launch token.'
                applicable = $false
            }
        }
    }
    if ($session.status -in @('Lost', 'ProcessMissing', 'PendingProcessLost')) {
        $actions += [pscustomobject][ordered]@{
            action = 'MarkProcessLost'
            recordId = [string]$session.recordId
            sessionId = [string]$session.sessionId
            reason = 'The registered process is no longer alive.'
            applicable = $true
        }
    }
    if ($session.status -in @('AmbiguousTab', 'TabMissing')) {
        $actions += [pscustomobject][ordered]@{
            action = 'ManualTabIdentityReview'
            recordId = [string]$session.recordId
            sessionId = [string]$session.sessionId
            reason = 'Visible terminal identity cannot be repaired safely from title alone.'
            applicable = $false
        }
    }
}
foreach ($path in @($status.orphanEnvelopes)) {
    $actions += [pscustomobject][ordered]@{ action = 'ReviewOrphanEnvelope'; recordId = ''; sessionId = ''; reason = $path; applicable = $false }
}
foreach ($path in @($status.orphanSnapshots)) {
    $actions += [pscustomobject][ordered]@{ action = 'ReviewOrphanSnapshot'; recordId = ''; sessionId = ''; reason = $path; applicable = $false }
}
foreach ($item in @($status.orphanLaunchStates)) {
    $actions += [pscustomobject][ordered]@{ action = 'ReviewOrphanLaunchState'; recordId = ''; sessionId = ''; reason = [string]$item.path; applicable = $false }
}

$applied = @()
$backupPath = ''
if ($Apply) {
    $applicableActions = @($actions | Where-Object applicable)
    if ($applicableActions.Count -gt 0) {
        if (@($applicableActions | Where-Object action -eq 'RemoveIdentityEmptyPlaceholder').Count -gt 0) {
            $backupRoot = Join-Path $localStateRoot 'repair-backups'
            [void][IO.Directory]::CreateDirectory($backupRoot)
            $backupPath = Join-Path $backupRoot ('sessions-before-placeholder-removal-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '.json')
            $sourceBytes = [IO.File]::ReadAllBytes($registryPath)
            $backupStream = [IO.File]::Open($backupPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
            try { $backupStream.Write($sourceBytes, 0, $sourceBytes.Length); $backupStream.Flush($true) }
            finally { $backupStream.Dispose() }
        }
        $repairUpdateContext = [pscustomobject]@{ actions = @($applicableActions) }
        Invoke-ESCodexRegistryUpdate -Path $registryPath -Update {
            param($registry, $context)
            foreach ($action in @($context.actions)) {
                if ($action.action -eq 'UpgradeAuthoritativeRegistrySchema') { continue }
                if ($action.action -eq 'RemoveIdentityEmptyPlaceholder') {
                    $registry.sessions = @($registry.sessions | Where-Object { [string]$_.recordId -ne [string]$action.recordId })
                    continue
                }
                $record = @($registry.sessions | Where-Object { [string]$_.recordId -eq [string]$action.recordId } | Select-Object -First 1)[0]
                if ($null -eq $record) { throw "Repair target disappeared from registry: $($action.recordId)" }
                switch ($action.action) {
                    'HydrateAuthorityFromLaunchState' {
                        $launchState = Get-Content -LiteralPath ([string]$action.sourcePath) -Raw -Encoding UTF8 | ConvertFrom-Json
                        foreach ($name in @('processId', 'terminalLauncherProcessId', 'launchToken', 'handoffSnapshotDirectory', 'commandWrapperPath', 'terminalMode', 'terminalWindowName', 'windowKey', 'wtSession', 'launchPhase', 'promptObserved', 'contextAccepted', 'startupFailed', 'startupTimedOut', 'startupFailureReason', 'acceptanceReceiptPath', 'startupDiagnosticPath')) {
                            $value = Get-ESCodexPropertyValue $launchState $name $null
                            if ($null -ne $value -and -not ([string]$value -eq '')) { Set-ESCodexPropertyValue $record $name $value }
                        }
                    }
                    'RegisterResolvedSessionId' {
                        $conflict = @($registry.sessions | Where-Object { [string]$_.sessionId -eq [string]$action.sessionId -and [string]$_.recordId -ne [string]$record.recordId })
                        if ($conflict.Count -gt 0) { throw "Resolved SessionId already belongs to another registry record: $($action.sessionId)" }
                        $record.sessionId = [string]$action.sessionId
                        $record.lifecycleStatus = if ([bool](Get-ESCodexPropertyValue $record 'contextAccepted' $false)) { 'Registered' } else { 'PendingAcceptance' }
                    }
                    'MarkProcessLost' {
                        $record.processId = 0
                        $record.lifecycleStatus = 'Lost'
                    }
                }
                $record.lastRepairUtc = [DateTime]::UtcNow.ToString('o')
                $record.lastSeenUtc = [DateTime]::UtcNow.ToString('o')
            }
        } -Argument $repairUpdateContext | Out-Null
        $applied = @($applicableActions)
    }
}

[pscustomobject][ordered]@{
    mode = 'Repair'
    dryRun = -not [bool]$Apply
    registryPath = $registryPath
    backupPath = $backupPath
    statusBefore = $status
    proposedActions = @($actions)
    appliedActions = @($applied)
    requiresExplicitApply = -not [bool]$Apply
}
