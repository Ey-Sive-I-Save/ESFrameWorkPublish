[CmdletBinding()]
param(
    [string]$RecordId = '',
    [string]$SessionId = '',
    [string]$LaunchToken = '',
    [switch]$Current,
    [Parameter(Mandatory = $true)][string]$AcceptanceResponsibilityKey,
    [switch]$Disable,
    [int]$ExpectedRegistryRevision = -1,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$registryPath = Join-Path $localStateRoot 'sessions.json'
$snapshot = Read-ESCodexSessionRegistry $registryPath
if ($snapshot.requiresWriteUpgrade) { throw 'Acceptance binding requires authoritative registry schema v2. Run and review Repair -Apply first.' }
$key = $AcceptanceResponsibilityKey.Trim().ToLowerInvariant()
if ($key -notmatch '^[a-z0-9][a-z0-9._-]{1,63}$') { throw 'AcceptanceResponsibilityKey must contain 2-64 lowercase letters, digits, dots, underscores, or hyphens.' }
$queryArguments = @{ RequireUnique = $true; StateRoot = $localStateRoot }
if ($Current) { $queryArguments.Current = $true }
if (-not [string]::IsNullOrWhiteSpace($RecordId)) { $queryArguments.RecordId = $RecordId }
if (-not [string]::IsNullOrWhiteSpace($SessionId)) { $queryArguments.SessionId = $SessionId }
if (-not [string]::IsNullOrWhiteSpace($LaunchToken)) { $queryArguments.LaunchToken = $LaunchToken }
$owner = (& (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') @queryArguments).route
$target = $null
if (-not $Disable) {
    $target = (& (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') -ResponsibilityKey $key -RequireUnique -StateRoot $localStateRoot).route
    if ([string]$owner.recordId -eq [string]$target.recordId) { throw 'A session cannot bind itself as its acceptance target.' }
}
$update = [pscustomobject]@{ recordId = [string]$owner.recordId; targetKey = $key; targetRecordId = if ($null -eq $target) { '' } else { [string]$target.recordId }; enabled = -not $Disable; updatedUtc = [DateTime]::UtcNow.ToString('o') }
Invoke-ESCodexRegistryUpdate -Path $registryPath -ExpectedRevision $ExpectedRegistryRevision -Update {
    param($registry, $context)
    $record = @($registry.sessions | Where-Object recordId -eq $context.recordId | Select-Object -First 1)[0]
    if ($null -eq $record) { throw "Acceptance binding owner disappeared: $($context.recordId)" }
    if ($context.enabled) {
        $targetMatches = @($registry.sessions | Where-Object { [string]$_.responsibilityKey -eq $context.targetKey -and [string]$_.lifecycleStatus -notin @('Closed', 'Lost', 'PendingProcessLost') })
        if ($targetMatches.Count -ne 1 -or [string]$targetMatches[0].recordId -ne $context.targetRecordId) { throw "Acceptance target changed while binding was being committed: $($context.targetKey)" }
    }
    $record.acceptanceResponsibilityKey = if ($context.enabled) { $context.targetKey } else { '' }
    $record.acceptanceOnCompletion = [bool]$context.enabled
    $record.acceptanceBindingUpdatedUtc = $context.updatedUtc
    if (-not $context.enabled) { $record.lastAcceptanceRequestTurnId = '' }
} -Argument $update | Out-Null
$route = (& (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') -RecordId $owner.recordId -RequireUnique -StateRoot $localStateRoot).route
[pscustomobject][ordered]@{ acceptanceBindingContractVersion = 1; enabled = [bool]$update.enabled; owner = $route; acceptanceResponsibilityKey = [string]$route.acceptanceResponsibilityKey }
