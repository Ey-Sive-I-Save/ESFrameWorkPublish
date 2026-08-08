[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MessageId,
    [Parameter(Mandatory = $true)]
    [ValidateSet('accepted', 'turn_started', 'steered', 'completed', 'failed', 'expired')]
    [string]$Status,
    [string]$AcceptedByRecordId = '',
    [string]$Note = '',
    [int]$ExpectedStateRevision = -1,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')
$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
Set-ESCodexMessageStatus $localStateRoot $MessageId $Status $ExpectedStateRevision $AcceptedByRecordId $Note
