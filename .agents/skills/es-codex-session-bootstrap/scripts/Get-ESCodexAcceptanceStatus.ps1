[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RequestMessageId,
    [string]$RequesterRecordId = '',
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')
$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$request = Read-ESCodexMessage $localStateRoot $RequestMessageId
if ($null -eq $request) { throw "Acceptance request was not found: $RequestMessageId" }
$replyTarget = if ([string]::IsNullOrWhiteSpace($RequesterRecordId)) { [string]$request.replyToRecordId } else { $RequesterRecordId }
$replies = @(Find-ESCodexMessages $localStateRoot -TargetRecordId $replyTarget -InReplyToMessageId $RequestMessageId)
[pscustomobject][ordered]@{
    acceptanceStatusContractVersion = 1
    request = $request
    replyCount = $replies.Count
    reply = if ($replies.Count -gt 0) { $replies[0] } else { $null }
    completed = $replies.Count -gt 0 -and [string]$replies[0].effectiveStatus -in @('queued', 'accepted', 'turn_started', 'steered', 'completed')
    terminal = [string]$request.effectiveStatus -in @('completed', 'failed', 'expired')
    waitingForReply = $replies.Count -eq 0 -and [string]$request.effectiveStatus -notin @('failed', 'expired')
}
