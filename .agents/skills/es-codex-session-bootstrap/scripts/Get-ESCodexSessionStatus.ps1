[CmdletBinding()]
param(
    [string]$SessionId = '',
    [string]$RecordId = '',
    [string]$TaskKey = '',
    [string]$ResponsibilityKey = '',
    [string]$TabTitle = '',
    [string]$LaunchToken = '',
    [switch]$IncludeClosed,
    [switch]$SkipUiObservation,
    [switch]$SkipReadinessRefresh,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexLaunchReadiness.ps1')

$fixedProjectRoot = 'F:\aaProject\ESFrameWorkPublish'
$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$registryPath = Join-Path $localStateRoot 'sessions.json'
$registry = Read-ESCodexSessionRegistry $registryPath
$allMessages = @(Find-ESCodexMessages $localStateRoot)
$hookConfigPath = Join-Path $fixedProjectRoot '.codex\hooks.json'
$hookScriptPath = Join-Path $PSScriptRoot 'Receive-ESCodexSessionMessageHook.ps1'
$launchStateRoot = Join-Path $localStateRoot 'launch-state'
$launchStates = @()
if (Test-Path -LiteralPath $launchStateRoot -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $launchStateRoot -File -Filter '*.json')) {
        try {
            $state = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            $state | Add-Member -NotePropertyName sourcePath -NotePropertyValue $file.FullName -Force
            $launchStates += $state
        }
        catch {
            $launchStates += [pscustomobject]@{ sourcePath = $file.FullName; invalid = $true; error = $_.Exception.Message }
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
    $parsed = [Guid]::Empty
    if (-not [Guid]::TryParse($SessionId.Trim(), [ref]$parsed)) { throw 'SessionId must be an exact Codex session UUID.' }
    $SessionId = $parsed.ToString()
}

$records = @($registry.sessions | Where-Object {
        ([string]::IsNullOrWhiteSpace($SessionId) -or [string]$_.sessionId -eq $SessionId) -and
        ([string]::IsNullOrWhiteSpace($RecordId) -or [string]$_.recordId -eq $RecordId) -and
        ([string]::IsNullOrWhiteSpace($TaskKey) -or [string]$_.taskKey -eq $TaskKey) -and
        ([string]::IsNullOrWhiteSpace($ResponsibilityKey) -or [string]$_.responsibilityKey -eq $ResponsibilityKey) -and
        ([string]::IsNullOrWhiteSpace($TabTitle) -or [string]$_.tabTitle -eq $TabTitle) -and
        ([string]::IsNullOrWhiteSpace($LaunchToken) -or [string]$_.launchToken -eq $LaunchToken) -and
        ($IncludeClosed -or [string]$_.lifecycleStatus -ne 'Closed')
    })

$uiAvailable = -not $SkipUiObservation
$uiError = if ($SkipUiObservation) { 'SkippedForRoutingQuery' } else { '' }
$visibleTabs = @()
try {
    if (-not $SkipUiObservation) { $visibleTabs = @(Get-ESCodexVisibleTerminalTabs) }
}
catch {
    $visibleTabs = @()
    $uiAvailable = $false
    $uiError = $_.Exception.Message
}

$observations = foreach ($record in $records) {
    $recordMessages = @($allMessages | Where-Object targetRecordId -eq ([string]$record.recordId))
    $pendingMessages = @($recordMessages | Where-Object effectiveStatus -in @('queued', 'accepted', 'turn_started', 'steered'))
    $oldestPendingMessage = @($pendingMessages | Sort-Object createdUtc | Select-Object -First 1)[0]
    $hookActivation = Test-ESCodexHookActivation $localStateRoot ([string]$record.recordId) ([string]$record.sessionId) $hookConfigPath $hookScriptPath
    $launchState = @($launchStates | Where-Object {
            (-not [string]::IsNullOrWhiteSpace([string]$record.launchToken) -and [string]$_.launchToken -eq [string]$record.launchToken) -or
            (-not [string]::IsNullOrWhiteSpace([string]$record.sessionId) -and [string]$_.sessionId -eq [string]$record.sessionId) -or
            (-not [string]::IsNullOrWhiteSpace([string]$record.taskFingerprint) -and [string]$_.taskFingerprint -eq [string]$record.taskFingerprint)
        } | Select-Object -First 1)[0]
    $effectiveProcessId = if ([int]$record.processId -gt 0) { [int]$record.processId } elseif ($null -ne $launchState) { [int](Get-ESCodexPropertyValue $launchState 'processId' 0) } else { 0 }
    $effectiveLaunchToken = if (-not [string]::IsNullOrWhiteSpace([string]$record.launchToken)) { [string]$record.launchToken } elseif ($null -ne $launchState) { [string](Get-ESCodexPropertyValue $launchState 'launchToken' '') } else { '' }
    $effectiveSnapshotDirectory = if (-not [string]::IsNullOrWhiteSpace([string]$record.handoffSnapshotDirectory)) { [string]$record.handoffSnapshotDirectory } elseif ($null -ne $launchState) { [string](Get-ESCodexPropertyValue $launchState 'handoffSnapshotDirectory' '') } else { '' }
    $effectiveTerminalMode = if (-not [string]::IsNullOrWhiteSpace([string]$record.terminalMode)) { [string]$record.terminalMode } elseif ($null -ne $launchState) { [string](Get-ESCodexPropertyValue $launchState 'terminalMode' '') } else { '' }
    $effectiveTerminalWindowName = if (-not [string]::IsNullOrWhiteSpace([string]$record.terminalWindowName)) { [string]$record.terminalWindowName } elseif ($null -ne $launchState) { [string](Get-ESCodexPropertyValue $launchState 'terminalWindowName' '') } else { '' }
    $effectiveWindowKey = if (-not [string]::IsNullOrWhiteSpace([string]$record.windowKey)) { [string]$record.windowKey } elseif ($null -ne $launchState) { [string](Get-ESCodexPropertyValue $launchState 'windowKey' '') } else { '' }
    $effectiveWtSession = if (-not [string]::IsNullOrWhiteSpace([string]$record.wtSession)) { [string]$record.wtSession } elseif ($null -ne $launchState) { [string](Get-ESCodexPropertyValue $launchState 'wtSession' '') } else { '' }
    $isClaimedExternal = [string]$record.lifecycleStatus -eq 'ClaimedExternal'
    $externalClaimProcessIdentityValid = $true
    if ($isClaimedExternal) {
        $externalClaimProcessId = [int](Get-ESCodexPropertyValue $record 'externalClaimProcessId' 0)
        $externalClaimStartedText = [string](Get-ESCodexPropertyValue $record 'externalClaimProcessStartedAtUtc' '')
        $externalClaimStartedAt = [DateTime]::MinValue
        if ($externalClaimProcessId -le 0 -or -not [DateTime]::TryParse($externalClaimStartedText, [ref]$externalClaimStartedAt)) {
            $externalClaimProcessIdentityValid = $false
        }
        else {
            try {
                $externalClaimProcess = Get-Process -Id $externalClaimProcessId -ErrorAction Stop
                $externalClaimProcessIdentityValid = -not $externalClaimProcess.HasExited -and
                    $externalClaimProcess.ProcessName.Equals('cmd', [StringComparison]::OrdinalIgnoreCase) -and
                    $externalClaimProcess.StartTime.ToUniversalTime().Ticks -eq $externalClaimStartedAt.ToUniversalTime().Ticks
            }
            catch { $externalClaimProcessIdentityValid = $false }
        }
        $effectiveProcessId = $externalClaimProcessId
    }
    $processAlive = if ($isClaimedExternal) { $externalClaimProcessIdentityValid } else { Test-ESCodexProcessAlive $effectiveProcessId }
    $recordedTerminalWindowProcessId = [int]$record.terminalWindowProcessId
    $launchStateTerminalWindowProcessId = if ($null -ne $launchState) {
        [int](Get-ESCodexPropertyValue $launchState 'terminalWindowProcessId' 0)
    }
    else { 0 }
    $effectiveTerminalWindowProcessId = if ($recordedTerminalWindowProcessId -gt 0) {
        $recordedTerminalWindowProcessId
    }
    elseif ($launchStateTerminalWindowProcessId -gt 0) {
        $launchStateTerminalWindowProcessId
    }
    else { 0 }
    $terminalWindowProcessIdentitySource = if ($recordedTerminalWindowProcessId -gt 0) {
        'Registry'
    }
    elseif ($launchStateTerminalWindowProcessId -gt 0) {
        'LaunchState'
    }
    else {
        'Unavailable'
    }
    if (-not $isClaimedExternal -and $effectiveTerminalWindowProcessId -le 0 -and $processAlive -and
        $effectiveTerminalMode -ne 'PlainCmd') {
        $observedTerminalWindowProcessId = Get-ESCodexTerminalHostProcessId $effectiveProcessId
        if ($observedTerminalWindowProcessId -gt 0) {
            $effectiveTerminalWindowProcessId = $observedTerminalWindowProcessId
            $terminalWindowProcessIdentitySource = 'ProcessAncestryObservation'
        }
    }
    $matchingTabs = @(if ($isClaimedExternal -or $effectiveTerminalMode -eq 'PlainCmd' -or
            [string]::IsNullOrWhiteSpace([string]$record.tabTitle) -or $effectiveTerminalWindowProcessId -le 0) {
            @()
        }
        else {
            @($visibleTabs | Where-Object {
                    $_.windowProcessId -eq $effectiveTerminalWindowProcessId -and
                    $_.title -eq ([string]$record.tabTitle)
                })
        })
    $envelopeExists = -not [string]::IsNullOrWhiteSpace([string]$record.envelopePath) -and (Test-Path -LiteralPath ([string]$record.envelopePath) -PathType Leaf)
    $snapshotExists = -not [string]::IsNullOrWhiteSpace($effectiveSnapshotDirectory) -and (Test-Path -LiteralPath $effectiveSnapshotDirectory -PathType Container)
    $launchPhase = [string](Get-ESCodexPropertyValue $record 'launchPhase' '')
    if ([string]::IsNullOrWhiteSpace($launchPhase) -and $null -ne $launchState) { $launchPhase = [string](Get-ESCodexPropertyValue $launchState 'launchPhase' '') }
    $promptObserved = [bool](Get-ESCodexPropertyValue $record 'promptObserved' $false) -or [bool](Get-ESCodexPropertyValue $launchState 'promptObserved' $false)
    $contextAccepted = [bool](Get-ESCodexPropertyValue $record 'contextAccepted' $false) -or [bool](Get-ESCodexPropertyValue $launchState 'contextAccepted' $false)
    $startupFailed = [bool](Get-ESCodexPropertyValue $record 'startupFailed' $false) -or [bool](Get-ESCodexPropertyValue $launchState 'startupFailed' $false)
    $startupTimedOut = [bool](Get-ESCodexPropertyValue $record 'startupTimedOut' $false) -or [bool](Get-ESCodexPropertyValue $launchState 'startupTimedOut' $false)
    $startupFailureReason = [string](Get-ESCodexPropertyValue $record 'startupFailureReason' '')
    $acceptanceReceiptPath = [string](Get-ESCodexPropertyValue $record 'acceptanceReceiptPath' '')
    $startupDiagnosticPath = [string](Get-ESCodexPropertyValue $record 'startupDiagnosticPath' (Get-ESCodexPropertyValue $launchState 'startupDiagnosticPath' ''))
    if (-not $SkipReadinessRefresh -and -not [string]::IsNullOrWhiteSpace($effectiveLaunchToken) -and $envelopeExists) {
        try {
            $readiness = Get-ESCodexLaunchReadiness `
                -LaunchToken $effectiveLaunchToken `
                -EnvelopePath ([string]$record.envelopePath) `
                -ProjectRoot ([string]$record.projectRoot) `
                -ReceiptRoot (Join-Path $localStateRoot 'acceptance-receipts') `
                -HistoryPath (Join-Path $env:USERPROFILE '.codex\history.jsonl') `
                -StartedAtUnix ([long](Get-ESCodexPropertyValue $launchState 'startedAtUnix' 0)) `
                -ExitMarkerPath $startupDiagnosticPath `
                -KnownSessionId ([string]$record.sessionId)
            $launchPhase = [string]$readiness.launchPhase
            $promptObserved = [bool]$readiness.promptObserved
            $contextAccepted = [bool]$readiness.contextAccepted
            $startupFailed = [bool]$readiness.startupFailed
            if (-not [string]::IsNullOrWhiteSpace([string]$readiness.failureReason)) { $startupFailureReason = [string]$readiness.failureReason }
            if (-not [string]::IsNullOrWhiteSpace([string]$readiness.acceptanceReceiptPath)) { $acceptanceReceiptPath = [string]$readiness.acceptanceReceiptPath }
        }
        catch {
            $startupFailed = $true
            $launchPhase = 'Failed'
            $startupFailureReason = $_.Exception.Message
        }
    }
    $authorityGaps = @()
    if ([int]$record.processId -le 0 -and $effectiveProcessId -gt 0) { $authorityGaps += 'processId' }
    if ([string]::IsNullOrWhiteSpace([string]$record.launchToken) -and -not [string]::IsNullOrWhiteSpace($effectiveLaunchToken)) { $authorityGaps += 'launchToken' }
    if ([string]::IsNullOrWhiteSpace([string]$record.handoffSnapshotDirectory) -and -not [string]::IsNullOrWhiteSpace($effectiveSnapshotDirectory)) { $authorityGaps += 'handoffSnapshotDirectory' }
    if ([string]::IsNullOrWhiteSpace([string]$record.terminalMode) -and $null -ne $launchState) { $authorityGaps += 'terminalIdentity' }
    if (-not $isClaimedExternal -and $effectiveTerminalMode -ne 'PlainCmd' -and $effectiveTerminalWindowProcessId -le 0) { $authorityGaps += 'terminalWindowProcessId' }
    elseif ($terminalWindowProcessIdentitySource -eq 'ProcessAncestryObservation') { $authorityGaps += 'terminalWindowProcessIdObserved' }
    $terminalMappingStatus = if ($isClaimedExternal) {
        if ($processAlive) { 'ClaimedExternalCmd' } else { 'ClaimedExternalCmdMissingOrReused' }
    }
    elseif ($effectiveTerminalMode -eq 'PlainCmd') {
        if ($processAlive) { 'ExactCmdProcess' } else { 'ProcessMissing' }
    }
    elseif (-not $uiAvailable) {
        'TerminalUiUnobserved'
    }
    elseif ($effectiveTerminalWindowProcessId -le 0) {
        'TerminalHostUnresolved'
    }
    elseif ($matchingTabs.Count -eq 1) {
        'UniqueTabInExactTerminalHost'
    }
    elseif ($matchingTabs.Count -eq 0) {
        'TabMissingInExactTerminalHost'
    }
    else {
        'AmbiguousTabInExactTerminalHost'
    }
    $status = if ([string]$record.lifecycleStatus -eq 'Closed') {
        'Closed'
    }
    elseif ($isClaimedExternal) {
        if ($processAlive) { 'ClaimedExternal' } else { 'ClaimedExternalProcessMissing' }
    }
    elseif ($startupFailed -or $launchPhase -eq 'Failed' -or [string]$record.lifecycleStatus -eq 'LaunchFailed') {
        'LaunchFailed'
    }
    elseif (-not $contextAccepted) {
        if (-not $processAlive) { 'PendingProcessLost' }
        elseif ($promptObserved -or $launchPhase -eq 'PromptObserved') { 'PendingAcceptance' }
        else { 'PendingPrompt' }
    }
    elseif ($uiAvailable -and $effectiveTerminalWindowProcessId -gt 0 -and $matchingTabs.Count -gt 1) {
        'AmbiguousTab'
    }
    elseif (-not $processAlive -and $uiAvailable -and $matchingTabs.Count -eq 0) {
        'Lost'
    }
    elseif (-not $processAlive) {
        'ProcessMissing'
    }
    elseif ($uiAvailable -and $effectiveTerminalMode -ne 'PlainCmd' -and $effectiveTerminalWindowProcessId -gt 0 -and $matchingTabs.Count -eq 0) {
        'TabMissing'
    }
    else {
        'Active'
    }
    [pscustomobject][ordered]@{
        status = $status
        recordId = [string]$record.recordId
        sessionId = [string]$record.sessionId
        responsibilityKey = [string]$record.responsibilityKey
        taskKey = [string]$record.taskKey
        tabTitle = [string]$record.tabTitle
        processId = $effectiveProcessId
        registeredProcessId = [int]$record.processId
        processAlive = $processAlive
        terminalMode = $effectiveTerminalMode
        terminalWindowName = $effectiveTerminalWindowName
        windowKey = $effectiveWindowKey
        terminalWindowProcessId = $effectiveTerminalWindowProcessId
        terminalWindowProcessIdentitySource = $terminalWindowProcessIdentitySource
        terminalMappingStatus = $terminalMappingStatus
        wtSession = $effectiveWtSession
        visibleTabCount = $matchingTabs.Count
        visibleWindows = @($matchingTabs | ForEach-Object { [pscustomobject]@{ windowProcessId = $_.windowProcessId; windowHandle = $_.windowHandle; windowTitle = $_.windowTitle } })
        launchToken = $effectiveLaunchToken
        envelopePath = [string]$record.envelopePath
        envelopeExists = $envelopeExists
        handoffSnapshotDirectory = $effectiveSnapshotDirectory
        snapshotExists = $snapshotExists
        launchStatePath = if ($null -eq $launchState) { '' } else { [string]$launchState.sourcePath }
        authorityGaps = @($authorityGaps)
        lifecycleStatus = [string]$record.lifecycleStatus
        launchPhase = $launchPhase
        promptObserved = $promptObserved
        contextAccepted = $contextAccepted
        startupFailed = $startupFailed
        startupTimedOut = $startupTimedOut
        startupFailureReason = $startupFailureReason
        acceptanceReceiptPath = $acceptanceReceiptPath
        startupDiagnosticPath = $startupDiagnosticPath
        externalClaimId = [string](Get-ESCodexPropertyValue $record 'externalClaimId' '')
        externalClaimState = [string](Get-ESCodexPropertyValue $record 'externalClaimState' '')
        externalClaimDirectory = [string](Get-ESCodexPropertyValue $record 'externalClaimDirectory' '')
        externalClaimProcessId = [int](Get-ESCodexPropertyValue $record 'externalClaimProcessId' 0)
        externalClaimProcessStartedAtUtc = [string](Get-ESCodexPropertyValue $record 'externalClaimProcessStartedAtUtc' '')
        externalClaimAcceptedAtUtc = [string](Get-ESCodexPropertyValue $record 'externalClaimAcceptedAtUtc' '')
        externalClaimProcessIdentityValid = $externalClaimProcessIdentityValid
        lastSeenUtc = [string]$record.lastSeenUtc
        closedAtUtc = [string]$record.closedAtUtc
        availability = [string]$record.availability
        availabilityUpdatedUtc = [string]$record.availabilityUpdatedUtc
        availabilityExpiresUtc = [string]$record.availabilityExpiresUtc
        activityKey = [string]$record.activityKey
        activitySummary = [string]$record.activitySummary
        acceptanceResponsibilityKey = [string]$record.acceptanceResponsibilityKey
        acceptanceOnCompletion = [bool]$record.acceptanceOnCompletion
        acceptanceBindingUpdatedUtc = [string]$record.acceptanceBindingUpdatedUtc
        lastAcceptanceRequestTurnId = [string]$record.lastAcceptanceRequestTurnId
        messageCount = $recordMessages.Count
        pendingMessageCount = $pendingMessages.Count
        messageStatusCounts = @($recordMessages | Group-Object effectiveStatus | ForEach-Object { [pscustomobject]@{ status = $_.Name; count = $_.Count } })
        oldestPendingMessageUtc = if ($null -eq $oldestPendingMessage) { '' } else { [string]$oldestPendingMessage.createdUtc }
        hookActivation = $hookActivation
        hookLoadedAndObserved = [bool]$hookActivation.valid
    }
}

$registeredEnvelopes = @($registry.sessions | ForEach-Object { [string]$_.envelopePath } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$registeredSnapshots = @($registry.sessions | ForEach-Object { [string]$_.handoffSnapshotDirectory } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$observedSnapshots = @($observations | ForEach-Object { [string]$_.handoffSnapshotDirectory } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$registeredLaunchStatePaths = @($observations | ForEach-Object { [string]$_.launchStatePath } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$envelopeRoot = Join-Path $localStateRoot 'envelopes'
$snapshotRoot = Join-Path $localStateRoot 'handoff-snapshots'
$orphanEnvelopes = if (Test-Path -LiteralPath $envelopeRoot -PathType Container) {
    @(Get-ChildItem -LiteralPath $envelopeRoot -File | Where-Object { $_.FullName -notin $registeredEnvelopes } | ForEach-Object FullName)
} else { @() }
$orphanSnapshots = if (Test-Path -LiteralPath $snapshotRoot -PathType Container) {
    @(Get-ChildItem -LiteralPath $snapshotRoot -Directory | Where-Object { $_.FullName -notin $registeredSnapshots -and $_.FullName -notin $observedSnapshots } | ForEach-Object FullName)
} else { @() }
$orphanLaunchStates = @($launchStates | Where-Object { [string]$_.sourcePath -notin $registeredLaunchStatePaths } | ForEach-Object { [pscustomobject]@{ path = [string]$_.sourcePath; invalid = [bool](Get-ESCodexPropertyValue $_ 'invalid' $false); error = [string](Get-ESCodexPropertyValue $_ 'error' '') } })

[pscustomobject][ordered]@{
    projectRoot = $fixedProjectRoot
    stateRoot = $localStateRoot
    registryPath = $registryPath
    registrySchemaVersion = [int]$registry.schemaVersion
    registrySourceSchemaVersion = [int]$registry.sourceSchemaVersion
    registryNeedsUpgrade = [bool]$registry.requiresWriteUpgrade
    registryRevision = [int]$registry.revision
    registryUpdatedUtc = [string]$registry.updatedUtc
    uiAvailable = $uiAvailable
    uiError = $uiError
    totalRegistered = @($registry.sessions).Count
    selectedCount = @($observations).Count
    statusCounts = @($observations | Group-Object status | ForEach-Object { [pscustomobject]@{ status = $_.Name; count = $_.Count } })
    sessions = @($observations)
    totalMessages = $allMessages.Count
    pendingMessages = @($allMessages | Where-Object effectiveStatus -in @('queued', 'accepted', 'turn_started', 'steered')).Count
    messageStatusCounts = @($allMessages | Group-Object effectiveStatus | ForEach-Object { [pscustomobject]@{ status = $_.Name; count = $_.Count } })
    hookLoadedAndObservedCount = @($observations | Where-Object hookLoadedAndObserved).Count
    orphanEnvelopes = @($orphanEnvelopes)
    orphanSnapshots = @($orphanSnapshots)
    orphanLaunchStates = @($orphanLaunchStates)
}
