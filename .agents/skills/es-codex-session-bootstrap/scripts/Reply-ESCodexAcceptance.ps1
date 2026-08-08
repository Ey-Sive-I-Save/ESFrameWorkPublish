[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RequestMessageId,
    [Parameter(Mandatory = $true)][string]$Body,
    [string]$ResponderRecordId = '',
    [string]$ResponderSessionId = '',
    [switch]$Current,
    [ValidateSet('low', 'normal', 'high')][string]$Priority = 'high',
    [ValidateRange(30, 86400)][int]$TtlSeconds = 1800,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')
$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$request = Read-ESCodexMessage $localStateRoot $RequestMessageId
if ($null -eq $request) { throw "Acceptance request was not found: $RequestMessageId" }
if (-not $request.expectsReply -or [string]$request.requestKind -ne 'acceptance-request') { throw "Message is not an acceptance request: $RequestMessageId" }
if ([string]::IsNullOrWhiteSpace($request.replyToRecordId)) { throw "Acceptance request has no reply target: $RequestMessageId" }
$responderArguments = @{ RequireUnique = $true; StateRoot = $localStateRoot }
if ($Current) { $responderArguments.Current = $true }
if (-not [string]::IsNullOrWhiteSpace($ResponderRecordId)) { $responderArguments.RecordId = $ResponderRecordId }
if (-not [string]::IsNullOrWhiteSpace($ResponderSessionId)) { $responderArguments.SessionId = $ResponderSessionId }
if (-not $Current -and $responderArguments.Keys.Count -eq 2) { throw 'ReplyAcceptance requires -Current or an exact responder RecordId/SessionId.' }
$responder = (& (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') @responderArguments).route
$replyIdempotency = 'acceptance-reply:' + $RequestMessageId
$reply = & (Join-Path $PSScriptRoot 'Send-ESCodexSessionMessage.ps1') `
    -RecordId $request.replyToRecordId `
    -Body $Body `
    -IdempotencyKey $replyIdempotency `
    -Priority $Priority `
    -TtlSeconds $TtlSeconds `
    -RequestKind acceptance-response `
    -CorrelationId $request.correlationId `
    -InReplyToMessageId $RequestMessageId `
    -StateRoot $localStateRoot
[pscustomobject][ordered]@{ acceptanceReplyContractVersion = 1; sent = $true; responder = $responder; targetRecordId = [string]$request.replyToRecordId; inReplyToMessageId = $RequestMessageId; reply = $reply.message; deliveryPlan = $reply.deliveryPlan }
