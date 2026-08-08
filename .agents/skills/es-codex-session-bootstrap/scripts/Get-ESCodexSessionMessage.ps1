[CmdletBinding()]
param(
    [string]$MessageId = '',
    [string]$IdempotencyKey = '',
    [string]$TargetRecordId = '',
    [string]$CorrelationId = '',
    [string]$InReplyToMessageId = '',
    [switch]$PendingOnly,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')
$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$messages = @(Find-ESCodexMessages $localStateRoot -MessageId $MessageId -IdempotencyKey $IdempotencyKey -TargetRecordId $TargetRecordId -CorrelationId $CorrelationId -InReplyToMessageId $InReplyToMessageId)
if ($PendingOnly) { $messages = @($messages | Where-Object effectiveStatus -in @('queued', 'accepted', 'turn_started', 'steered')) }
[pscustomobject][ordered]@{
    messageContractVersion = 1
    cooperativeMailboxSupported = $true
    deliveryMode = 'cooperative-mailbox'
    directCodexInjectionSupported = $false
    matchedCount = $messages.Count
    messages = $messages
}
