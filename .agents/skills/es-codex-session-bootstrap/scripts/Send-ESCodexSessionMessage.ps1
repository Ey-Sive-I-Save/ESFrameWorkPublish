[CmdletBinding()]
param(
    [string]$SessionId = '',
    [string]$RecordId = '',
    [string]$ResponsibilityKey = '',
    [Parameter(Mandatory = $true)]
    [string]$Body,
    [string]$IdempotencyKey = '',
    [ValidateSet('low', 'normal', 'high')]
    [string]$Priority = 'normal',
    [ValidateRange(30, 86400)]
    [int]$TtlSeconds = 900,
    [int]$ExpectedRegistryRevision = -1,
    [ValidateSet('message', 'acceptance-request', 'acceptance-response')]
    [string]$RequestKind = 'message',
    [string]$CorrelationId = '',
    [switch]$ExpectsReply,
    [string]$ReplyToRecordId = '',
    [string]$ReplyToSessionId = '',
    [string]$InReplyToMessageId = '',
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$localStateRoot = $StateRoot
$query = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') -SessionId $SessionId -RecordId $RecordId -ResponsibilityKey $ResponsibilityKey -RequireUnique -StateRoot $localStateRoot
$route = $query.route
if (-not [bool]$route.contextAccepted) {
    [pscustomobject][ordered]@{
        sendContractVersion = 1
        status = 'DeliveryBlocked'
        reasonCode = 'ContextNotAccepted'
        userMessage = 'Target window has not reached ContextAccepted. Responsibility information was not sent or queued, and no alternate window was used. Retry after initialization is accepted.'
        target = $route
        queued = $false
        accepted = $false
        completed = $false
        externalWakeRequired = $false
        directTuiInjectionAttempted = $false
    } | ConvertTo-Json -Depth 20
    exit 0
}
$publishArguments = @{
    RecordId = [string]$route.recordId
    Body = $Body
    IdempotencyKey = $IdempotencyKey
    Priority = $Priority
    TtlSeconds = $TtlSeconds
    ExpectedRegistryRevision = $ExpectedRegistryRevision
    RequestKind = $RequestKind
    CorrelationId = $CorrelationId
    ExpectsReply = $ExpectsReply
    ReplyToRecordId = $ReplyToRecordId
    ReplyToSessionId = $ReplyToSessionId
    InReplyToMessageId = $InReplyToMessageId
    StateRoot = $localStateRoot
}
$message = & (Join-Path $PSScriptRoot 'Publish-ESCodexSessionMessage.ps1') @publishArguments

$deliveryPlan = if (-not [bool]$route.turnBoundaryHookTrustVerified) {
    'MailboxOnlyUntilHookObserved'
}
elseif ([string]$route.waitState -eq 'Busy') {
    'StopHookAtBusyCompletion'
}
elseif ([bool]$route.isRoutable) {
    'NextUserPromptHook'
}
else {
    'MailboxUntilSessionReturns'
}
$externalWakeRequired = $deliveryPlan -in @('NextUserPromptHook', 'MailboxUntilSessionReturns', 'MailboxOnlyUntilHookObserved')
[pscustomobject][ordered]@{
    sendContractVersion = 1
    target = $route
    message = $message
    deliveryPlan = $deliveryPlan
    queued = [string]$message.effectiveStatus -eq 'queued'
    accepted = [string]$message.effectiveStatus -in @('accepted', 'turn_started', 'steered', 'completed')
    completed = [string]$message.effectiveStatus -eq 'completed'
    externalWakeRequired = $externalWakeRequired
    hookDeliveryProfile = [string]$route.hookDeliveryProfile
    hookDegraded = -not [bool]$route.turnBoundaryHookTrustVerified
    hookBlocksCooperativeBaseline = [bool]$route.hookBlocksCooperativeBaseline
    deliveryWarning = [string]$route.hookDegradationReason
    spontaneousIdleWakeAttempted = $false
    directTuiInjectionAttempted = $false
    nextStatusCommand = "Start-ESCodexSession.ps1 -Mode MessageStatus -MessageId $($message.messageId)"
}
