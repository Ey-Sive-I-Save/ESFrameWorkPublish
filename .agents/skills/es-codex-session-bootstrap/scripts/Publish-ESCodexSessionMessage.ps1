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
    [switch]$RequireReady,
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
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')

$bodyValue = $Body.Trim()
if ([string]::IsNullOrWhiteSpace($bodyValue)) { throw 'Message Body cannot be empty.' }
if ($bodyValue.Length -gt 8000) { throw 'Message Body must be 8000 characters or fewer.' }
$idempotencyValue = $IdempotencyKey.Trim()
if ($idempotencyValue.Length -gt 128) { throw 'IdempotencyKey must be 128 characters or fewer.' }

$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$query = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') -SessionId $SessionId -RecordId $RecordId -ResponsibilityKey $ResponsibilityKey -RequireUnique -StateRoot $localStateRoot
$route = $query.route
if ($RequireReady -and [string]$route.waitState -ne 'Ready') { throw "Message target is not Ready: $($route.waitState)" }
$expectedRevision = if ($ExpectedRegistryRevision -ge 0) { $ExpectedRegistryRevision } else { [int]$query.registryRevision }

$mutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexSessionMessagePublishV1')
$acquired = $false
try {
    $acquired = $mutex.WaitOne(5000)
    if (-not $acquired) { throw 'Timed out waiting for the Codex message publisher mutex.' }
    $registry = Read-ESCodexSessionRegistry (Join-Path $localStateRoot 'sessions.json')
    if ($registry.requiresWriteUpgrade) { throw 'Message publishing requires authoritative registry schema v2. Run and review Repair -Apply first.' }
    if ([int]$registry.revision -ne $expectedRevision) { throw "Registry revision conflict. Expected $expectedRevision but found $([int]$registry.revision). Refresh Resolve and retry." }
    $allMessages = @(Find-ESCodexMessages $localStateRoot)
    $pendingMessages = @($allMessages | Where-Object effectiveStatus -in @('queued', 'accepted', 'turn_started', 'steered'))
    if ($allMessages.Count -ge 1000) { throw 'Message store quota reached (1000 requests). Run MessageRepair and review its cleanup plan.' }
    if (@($pendingMessages | Where-Object targetRecordId -eq ([string]$route.recordId)).Count -ge 100) { throw 'Target pending-message quota reached (100). Resolve or expire existing messages first.' }
    $messageRoot = Get-ESCodexMessageRoot $localStateRoot
    if (Test-Path -LiteralPath $messageRoot -PathType Container) {
        $messageBytes = [long](Get-ChildItem -LiteralPath $messageRoot -Recurse -File | Measure-Object Length -Sum).Sum
        if ($messageBytes -ge 16777216) { throw 'Message store size quota reached (16 MiB). Run MessageRepair and review its cleanup plan.' }
    }
    if (-not [string]::IsNullOrWhiteSpace($idempotencyValue)) {
        $existing = @(Find-ESCodexMessages $localStateRoot -IdempotencyKey $idempotencyValue | Where-Object targetRecordId -eq ([string]$route.recordId))
        if ($existing.Count -gt 1) { throw "Duplicate idempotency authority detected: $idempotencyValue" }
        if ($existing.Count -eq 1) { return $existing[0] }
    }
    $messageId = [Guid]::NewGuid().ToString()
    $paths = Get-ESCodexMessagePaths $localStateRoot $messageId
    $now = [DateTime]::UtcNow
    $request = [ordered]@{
        schemaVersion = 1
        messageId = $messageId
        idempotencyKey = $idempotencyValue
        targetRecordId = [string]$route.recordId
        targetSessionId = [string]$route.sessionId
        targetResponsibilityKey = [string]$route.responsibilityKey
        targetRegistryRevision = $expectedRevision
        priority = $Priority
        body = $bodyValue
        bodyDigest = Get-ESCodexStableId $bodyValue
        createdUtc = $now.ToString('o')
        expiresUtc = $now.AddSeconds($TtlSeconds).ToString('o')
        deliveryMode = 'cooperative-mailbox'
        requestKind = $RequestKind
        correlationId = if ([string]::IsNullOrWhiteSpace($CorrelationId)) { $messageId } else { $CorrelationId.Trim() }
        expectsReply = [bool]$ExpectsReply
        replyToRecordId = $ReplyToRecordId.Trim()
        replyToSessionId = $ReplyToSessionId.Trim()
        inReplyToMessageId = $InReplyToMessageId.Trim()
    }
    $state = [ordered]@{ schemaVersion = 1; messageId = $messageId; revision = 1; status = 'queued'; updatedUtc = $now.ToString('o'); acceptedByRecordId = ''; note = '' }
    Write-ESCodexCreateOnlyJson $paths.requestPath $request
    try { Write-ESCodexCreateOnlyJson $paths.statePath $state }
    catch { throw "Message request was created but initial state creation failed: $messageId. $($_.Exception.Message)" }
    Read-ESCodexMessage $localStateRoot $messageId
}
finally {
    if ($acquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
