[CmdletBinding()]
param(
    [switch]$Apply,
    [ValidateRange(1, 3650)]
    [int]$RetentionDays = 30,
    [switch]$DeleteTerminalMessages,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')
$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$messageRoot = [IO.Path]::GetFullPath((Get-ESCodexMessageRoot $localStateRoot)).TrimEnd('\')
$messages = @(Find-ESCodexMessages $localStateRoot)
$cutoff = [DateTime]::UtcNow.AddDays(-$RetentionDays)
$actions = @()
foreach ($message in $messages) {
    if ($message.effectiveStatus -eq 'expired' -and $message.status -ne 'expired') {
        $actions += [pscustomobject]@{ action = 'MarkExpired'; messageId = $message.messageId; reason = 'TTL elapsed'; destructive = $false }
    }
    $updated = [DateTime]::MinValue
    $oldEnough = [DateTime]::TryParse([string]$message.statusUpdatedUtc, [ref]$updated) -and $updated.ToUniversalTime() -le $cutoff
    if ($DeleteTerminalMessages -and $oldEnough -and $message.effectiveStatus -in @('completed', 'failed', 'expired')) {
        $actions += [pscustomobject]@{ action = 'DeleteTerminalMessage'; messageId = $message.messageId; reason = "Terminal for at least $RetentionDays days"; destructive = $true }
    }
}

$applied = @()
if ($Apply) {
    foreach ($action in $actions) {
        if ($action.action -eq 'MarkExpired') {
            $current = Read-ESCodexMessage $localStateRoot $action.messageId
            if ($current.effectiveStatus -eq 'expired' -and $current.status -ne 'expired') {
                Set-ESCodexMessageStatus $localStateRoot $action.messageId 'expired' $current.stateRevision '' 'TTL elapsed' | Out-Null
                $applied += $action
            }
            continue
        }
        if ($action.action -eq 'DeleteTerminalMessage') {
            $paths = Get-ESCodexMessagePaths $localStateRoot $action.messageId
            foreach ($path in @($paths.requestPath, $paths.statePath)) {
                $fullPath = [IO.Path]::GetFullPath($path)
                if (-not $fullPath.StartsWith($messageRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Message cleanup path escaped state root: $fullPath" }
                if (Test-Path -LiteralPath $fullPath -PathType Leaf) { Remove-Item -LiteralPath $fullPath -Force }
            }
            $applied += $action
        }
    }
}

[pscustomobject][ordered]@{
    mode = 'MessageRepair'
    dryRun = -not $Apply
    stateRoot = $localStateRoot
    retentionDays = $RetentionDays
    deleteTerminalMessagesRequested = [bool]$DeleteTerminalMessages
    messageCount = $messages.Count
    plannedCount = $actions.Count
    destructivePlannedCount = @($actions | Where-Object destructive).Count
    appliedCount = $applied.Count
    actions = $actions
    applied = $applied
}
