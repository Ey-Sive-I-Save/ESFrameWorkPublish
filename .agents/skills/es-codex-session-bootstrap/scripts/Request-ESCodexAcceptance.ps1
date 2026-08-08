[CmdletBinding()]
param(
    [string]$AcceptanceResponsibilityKey = 'engineering-acceptance',
    [string]$RequesterRecordId = '',
    [string]$RequesterSessionId = '',
    [string]$RequesterLaunchToken = '',
    [switch]$Current,
    [string]$Body = 'Task complete. Please perform engineering acceptance and reply with the verdict, evidence, and blockers.',
    [string]$IdempotencyKey = '',
    [ValidateSet('low', 'normal', 'high')]
    [string]$Priority = 'high',
    [ValidateRange(30, 86400)]
    [int]$TtlSeconds = 1800,
    [ValidateRange(0, 60)]
    [int]$WaitSeconds = 60,
    [ValidateRange(250, 10000)]
    [int]$PollMilliseconds = 1000,
    [switch]$NoWaitForReply,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')

$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
if ([string]::IsNullOrWhiteSpace($Body)) { $Body = 'Task complete. Please perform engineering acceptance and reply with the verdict, evidence, and blockers.' }
$requesterArguments = @{ RequireUnique = $true; StateRoot = $localStateRoot }
if ($Current) { $requesterArguments.Current = $true }
if (-not [string]::IsNullOrWhiteSpace($RequesterRecordId)) { $requesterArguments.RecordId = $RequesterRecordId }
if (-not [string]::IsNullOrWhiteSpace($RequesterSessionId)) { $requesterArguments.SessionId = $RequesterSessionId }
if (-not [string]::IsNullOrWhiteSpace($RequesterLaunchToken)) { $requesterArguments.LaunchToken = $RequesterLaunchToken }
if (-not $Current -and $requesterArguments.Keys.Count -eq 2) { throw 'RequestAcceptance requires -Current or an exact requester RecordId/SessionId/LaunchToken.' }
$requesterQuery = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') @requesterArguments
$requester = $requesterQuery.route

$targetKey = $AcceptanceResponsibilityKey.Trim().ToLowerInvariant()
if ($targetKey -notmatch '^[a-z0-9][a-z0-9._-]{1,63}$') { throw 'AcceptanceResponsibilityKey must contain 2-64 lowercase letters, digits, dots, underscores, or hyphens.' }
$deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
$targetQuery = $null
$targetRoute = $null
$targetHistory = [Collections.Generic.List[object]]::new()
$targetReady = $false
do {
    $targetQuery = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') -ResponsibilityKey $targetKey -RequireUnique -StateRoot $localStateRoot
    $targetRoute = $targetQuery.route
    $targetHistory.Add([pscustomobject]@{ observedUtc = [DateTime]::UtcNow.ToString('o'); status = [string]$targetRoute.status; waitState = [string]$targetRoute.waitState; availability = [string]$targetRoute.effectiveAvailability })
    if ([string]$targetRoute.waitState -eq 'Ready') { $targetReady = $true; break }
    if ([string]$targetRoute.waitState -in @('Terminal', 'Unavailable')) { break }
    if ([DateTime]::UtcNow -ge $deadline) { break }
    Start-Sleep -Milliseconds $PollMilliseconds
} while ($true)

if (-not $targetReady -and [string]$targetRoute.waitState -in @('Busy', 'Pending')) {
    [pscustomobject][ordered]@{
        acceptanceContractVersion = 1
        sent = $false
        timedOut = $true
        reason = 'Acceptance window is still producing or completing a turn.'
        requester = $requester
        target = $targetRoute
        targetHistory = $targetHistory.ToArray()
        nextPollAfterMs = $PollMilliseconds
        nextCommand = "Start-ESCodexSession.ps1 -Mode RequestAcceptance -AcceptanceResponsibilityKey $targetKey -RequesterRecordId $($requester.recordId)"
    }
    return
}
if (-not $targetReady -and [string]$targetRoute.waitState -eq 'Terminal') { throw "Acceptance responsibility is terminal: $targetKey" }
if (-not $targetReady -and [string]$targetRoute.waitState -eq 'Unavailable') { throw "Acceptance responsibility is unavailable: $targetKey" }

$correlationId = [Guid]::NewGuid().ToString()
$idempotencyValue = if ([string]::IsNullOrWhiteSpace($IdempotencyKey)) { 'acceptance-request:' + $correlationId } else { $IdempotencyKey }
$sent = & (Join-Path $PSScriptRoot 'Send-ESCodexSessionMessage.ps1') `
    -RecordId ([string]$targetRoute.recordId) `
    -Body $Body `
    -IdempotencyKey $idempotencyValue `
    -Priority $Priority `
    -TtlSeconds $TtlSeconds `
    -RequestKind acceptance-request `
    -CorrelationId $correlationId `
    -ExpectsReply `
    -ReplyToRecordId ([string]$requester.recordId) `
    -ReplyToSessionId ([string]$requester.sessionId) `
    -StateRoot $localStateRoot
$requestMessageId = [string]$sent.message.messageId

$reply = $null
$replyTimedOut = $false
$replyHistory = [Collections.Generic.List[object]]::new()
if (-not $NoWaitForReply) {
    $replyDeadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
    do {
        $targetQuery = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') -RecordId ([string]$targetRoute.recordId) -RequireUnique -StateRoot $localStateRoot
        $targetRoute = $targetQuery.route
        $replyHistory.Add([pscustomobject]@{ observedUtc = [DateTime]::UtcNow.ToString('o'); waitState = [string]$targetRoute.waitState; availability = [string]$targetRoute.effectiveAvailability })
        $replies = @(Find-ESCodexMessages $localStateRoot -TargetRecordId ([string]$requester.recordId) -InReplyToMessageId $requestMessageId)
        if ($replies.Count -gt 0) { $reply = $replies[0]; break }
        if ([DateTime]::UtcNow -ge $replyDeadline) { $replyTimedOut = $true; break }
        Start-Sleep -Milliseconds $PollMilliseconds
    } while ($true)
}

[pscustomobject][ordered]@{
    acceptanceContractVersion = 1
    sent = $true
    timedOut = $replyTimedOut
    requester = $requester
    target = $targetRoute
    targetHistory = $targetHistory.ToArray()
    request = $sent.message
    requestMessageId = $requestMessageId
    correlationId = $correlationId
    deliveryPlan = $sent.deliveryPlan
    reply = $reply
    replyHistory = $replyHistory.ToArray()
    nextPollAfterMs = if ($replyTimedOut) { $PollMilliseconds } else { 0 }
    nextCommand = if ($replyTimedOut) { "Start-ESCodexSession.ps1 -Mode AcceptanceStatus -MessageId $requestMessageId -RequesterRecordId $($requester.recordId)" } else { '' }
}
