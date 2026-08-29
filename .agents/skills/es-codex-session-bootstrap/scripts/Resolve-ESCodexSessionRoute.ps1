[CmdletBinding()]
param(
    [string]$SessionId = '',
    [string]$RecordId = '',
    [string]$TaskKey = '',
    [string]$ResponsibilityKey = '',
    [string]$LaunchToken = '',
    [switch]$Current,
    [switch]$IncludeClosed,
    [switch]$RequireUnique,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')

if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
    $parsedSessionId = [Guid]::Empty
    if (-not [Guid]::TryParse($SessionId.Trim(), [ref]$parsedSessionId)) { throw 'SessionId must be an exact Codex session UUID.' }
    $SessionId = $parsedSessionId.ToString()
}
if (-not [string]::IsNullOrWhiteSpace($RecordId) -and $RecordId -notmatch '^[a-fA-F0-9]{32}$') {
    throw 'RecordId must be an exact 32-character hexadecimal registry ID.'
}
if (-not $Current -and @($SessionId, $RecordId, $TaskKey, $ResponsibilityKey, $LaunchToken | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }).Count -eq 0) {
    throw 'Query requires -Current, SessionId, RecordId, TaskKey, ResponsibilityKey, or LaunchToken.'
}

$context = if ($Current) { Get-ESCodexCurrentProcessContext $LaunchToken } else { $null }
$currentTokens = @()
if ($null -ne $context) { $currentTokens = @($context.launchTokens) }
$statusLaunchToken = if (-not [string]::IsNullOrWhiteSpace($LaunchToken)) {
    $LaunchToken
}
elseif ($currentTokens.Count -eq 1) {
    [string]($currentTokens[0])
}
else {
    ''
}
$hasNarrowStatusSelector = -not [string]::IsNullOrWhiteSpace($SessionId) -or
    -not [string]::IsNullOrWhiteSpace($RecordId) -or
    -not [string]::IsNullOrWhiteSpace($TaskKey) -or
    -not [string]::IsNullOrWhiteSpace($ResponsibilityKey) -or
    -not [string]::IsNullOrWhiteSpace($statusLaunchToken)
$skipReadinessRefresh = $true
if ($hasNarrowStatusSelector) { $skipReadinessRefresh = $false }
$statusArguments = @{
    IncludeClosed = $IncludeClosed
    SkipUiObservation = $true
    SkipReadinessRefresh = $skipReadinessRefresh
    StateRoot = $StateRoot
    SessionId = $SessionId
    RecordId = $RecordId
    TaskKey = $TaskKey
    ResponsibilityKey = $ResponsibilityKey
    LaunchToken = $statusLaunchToken
}
$status = & (Join-Path $PSScriptRoot 'Get-ESCodexSessionStatus.ps1') @statusArguments
$usedCurrentTokenPrefilter = $Current -and -not [string]::IsNullOrWhiteSpace($statusLaunchToken)
if ($usedCurrentTokenPrefilter -and @($status.sessions).Count -eq 0) {
    $status = & (Join-Path $PSScriptRoot 'Get-ESCodexSessionStatus.ps1') `
        -IncludeClosed:$IncludeClosed `
        -SkipUiObservation `
        -SkipReadinessRefresh `
        -StateRoot $StateRoot
}
$candidates = @($status.sessions)
$resolutionEvidence = @()

if ($Current) {
    $tokenMatches = @($candidates | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.launchToken) -and [string]$_.launchToken -in @($context.launchTokens) })
    if ($tokenMatches.Count -gt 0) {
        $candidates = $tokenMatches
        $resolutionEvidence += 'launchToken'
    }
    else {
        $wtMatches = if ([string]::IsNullOrWhiteSpace([string]$context.wtSession)) { @() } else { @($candidates | Where-Object { [string]$_.wtSession -eq [string]$context.wtSession }) }
        if ($wtMatches.Count -gt 0) {
            $candidates = $wtMatches
            $resolutionEvidence += 'WT_SESSION'
        }
        else {
            $context = Get-ESCodexCurrentProcessContext $LaunchToken -IncludeProcessAncestry
            $processMatches = @($candidates | Where-Object { [int]$_.processId -gt 0 -and [int]$_.processId -in @($context.ancestorProcessIds) })
            $candidates = $processMatches
            if ($processMatches.Count -gt 0) { $resolutionEvidence += 'processAncestry' }
        }
    }
}

$candidates = @($candidates | Where-Object {
        ([string]::IsNullOrWhiteSpace($SessionId) -or [string]$_.sessionId -eq $SessionId) -and
        ([string]::IsNullOrWhiteSpace($RecordId) -or [string]$_.recordId -eq $RecordId) -and
        ([string]::IsNullOrWhiteSpace($TaskKey) -or [string]$_.taskKey -eq $TaskKey) -and
        ([string]::IsNullOrWhiteSpace($ResponsibilityKey) -or [string]$_.responsibilityKey -eq $ResponsibilityKey) -and
        ([string]::IsNullOrWhiteSpace($LaunchToken) -or [string]$_.launchToken -eq $LaunchToken)
    })

$now = [DateTime]::UtcNow
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$turnBoundaryHookConfigured = Test-Path -LiteralPath (Join-Path $projectRoot '.codex\hooks.json') -PathType Leaf
$hookConfigPath = Join-Path $projectRoot '.codex\hooks.json'
$hookScriptPath = Join-Path $PSScriptRoot 'Receive-ESCodexSessionMessageHook.ps1'
$routes = foreach ($candidate in $candidates) {
    $hookActivation = Test-ESCodexHookActivation ([string]$status.stateRoot) ([string]$candidate.recordId) ([string]$candidate.sessionId) $hookConfigPath $hookScriptPath
    $declaredAvailability = if ([string]::IsNullOrWhiteSpace([string]$candidate.availability)) { 'Unknown' } else { [string]$candidate.availability }
    $availabilityStale = $false
    $expiresUtc = [DateTime]::MinValue
    if (-not [string]::IsNullOrWhiteSpace([string]$candidate.availabilityExpiresUtc)) {
        $availabilityStale = -not [DateTime]::TryParse([string]$candidate.availabilityExpiresUtc, [ref]$expiresUtc) -or $expiresUtc.ToUniversalTime() -le $now
    }
    $effectiveAvailability = if ($availabilityStale) { 'Unknown' } else { $declaredAvailability }
    $isTerminal = [string]$candidate.status -in @('Closed', 'Lost', 'PendingProcessLost')
    $isRoutable = -not [string]::IsNullOrWhiteSpace([string]$candidate.sessionId) -and [bool]$candidate.processAlive -and [string]$candidate.status -eq 'Active'
    $canReceiveForward = $isRoutable -and $effectiveAvailability -in @('Idle', 'Waiting')
    $waitState = if ($isTerminal) {
        'Terminal'
    }
    elseif ([string]$candidate.status -like 'Pending*') {
        'Pending'
    }
    elseif (-not $isRoutable) {
        'Unavailable'
    }
    elseif ($effectiveAvailability -eq 'Busy') {
        'Busy'
    }
    elseif ($effectiveAvailability -in @('Idle', 'Waiting')) {
        'Ready'
    }
    else {
        'UnknownAvailability'
    }
    [pscustomobject][ordered]@{
        recordId = [string]$candidate.recordId
        bindingTargetId = 'record:' + [string]$candidate.recordId
        sessionId = [string]$candidate.sessionId
        messageTargetId = if ([string]::IsNullOrWhiteSpace([string]$candidate.sessionId)) { '' } else { 'session:' + [string]$candidate.sessionId }
        responsibilityKey = [string]$candidate.responsibilityKey
        taskKey = [string]$candidate.taskKey
        tabTitle = [string]$candidate.tabTitle
        status = [string]$candidate.status
        contextAccepted = [bool]$candidate.contextAccepted
        processId = [int]$candidate.processId
        processAlive = [bool]$candidate.processAlive
        isTerminal = $isTerminal
        isRoutable = $isRoutable
        canReceiveForward = $canReceiveForward
        canQueueMessage = -not $isTerminal -and -not [bool]$status.registryNeedsUpgrade
        canDirectDeliver = $false
        canBindResponsibility = -not $isTerminal -and -not [bool]$status.registryNeedsUpgrade
        canPublishPresence = -not $isTerminal -and -not [bool]$status.registryNeedsUpgrade
        directMessageDeliverySupported = $false
        cooperativeMailboxSupported = -not [bool]$status.registryNeedsUpgrade
        turnBoundaryHookConfigured = $turnBoundaryHookConfigured
        turnBoundaryHookTrustVerified = [bool]$hookActivation.valid
        turnBoundaryHookActivation = $hookActivation
        hookDeliveryProfile = if ([bool]$hookActivation.valid) { 'verified-target' } else { 'degraded-optional' }
        hookBlocksCooperativeBaseline = $false
        hookDegradationReason = if ([bool]$hookActivation.valid) { '' } else { 'Automatic turn-boundary delivery is unavailable; the cooperative mailbox remains the supported delivery path.' }
        autoDeliveryOnBusyCompletionConfigured = $turnBoundaryHookConfigured
        autoDeliveryOnNextPromptConfigured = $turnBoundaryHookConfigured
        canAutoDeliverOnBusyCompletion = [bool]$hookActivation.valid -and -not [bool]$status.registryNeedsUpgrade
        canAutoDeliverOnNextPrompt = [bool]$hookActivation.valid -and -not [bool]$status.registryNeedsUpgrade
        canWakeIdleTuiWithoutInput = $false
        deliveryNote = 'This route supports a durable cooperative mailbox after schema v2 migration, but it does not inject text into an active Codex TUI.'
        waitState = $waitState
        declaredAvailability = $declaredAvailability
        effectiveAvailability = $effectiveAvailability
        availabilityStale = $availabilityStale
        availabilityUpdatedUtc = [string]$candidate.availabilityUpdatedUtc
        availabilityExpiresUtc = [string]$candidate.availabilityExpiresUtc
        activityKey = [string]$candidate.activityKey
        activitySummary = [string]$candidate.activitySummary
        acceptanceResponsibilityKey = [string]$candidate.acceptanceResponsibilityKey
        acceptanceOnCompletion = [bool]$candidate.acceptanceOnCompletion
        nextPollAfterMs = if ($waitState -in @('Busy', 'Pending', 'UnknownAvailability')) { 1000 } else { 0 }
        registryRevision = [int]$status.registryRevision
        etag = Get-ESCodexStableId (([string]$status.registryRevision) + '|' + [string]$candidate.recordId + '|' + [string]$candidate.status + '|' + [string]$candidate.availabilityUpdatedUtc)
    }
}

$unique = @($routes).Count -eq 1
$result = [pscustomobject][ordered]@{
    routingContractVersion = 1
    mode = if ($RequireUnique) { 'Resolve' } else { 'Query' }
    currentRequested = [bool]$Current
    resolutionEvidence = @($resolutionEvidence)
    registryPath = [string]$status.registryPath
    registryRevision = [int]$status.registryRevision
    matchedCount = @($routes).Count
    unique = $unique
    route = if ($unique) { @($routes)[0] } else { $null }
    candidates = @($routes)
}
if ($RequireUnique -and -not $unique) {
    $result
    throw "Session route is not unique; matched $(@($routes).Count) records."
}
$result
