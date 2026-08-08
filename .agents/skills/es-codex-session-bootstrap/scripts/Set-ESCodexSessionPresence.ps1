[CmdletBinding()]
param(
    [string]$SessionId = '',
    [string]$RecordId = '',
    [string]$LaunchToken = '',
    [switch]$Current,
    [Parameter(Mandatory = $true)]
    [ValidateSet('Unknown', 'Busy', 'Idle', 'Waiting')]
    [string]$Availability,
    [string]$ActivityKey = '',
    [string]$ActivitySummary = '',
    [ValidateRange(30, 86400)]
    [int]$TtlSeconds = 900,
    [int]$ExpectedRegistryRevision = -1,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')

$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$registryPath = Join-Path $localStateRoot 'sessions.json'
$registrySnapshot = Read-ESCodexSessionRegistry $registryPath
if ($registrySnapshot.requiresWriteUpgrade) { throw 'Presence updates require authoritative registry schema v2. Run and review Repair -Apply first.' }

$queryArguments = @{ RequireUnique = $true; StateRoot = $StateRoot }
if ($Current) { $queryArguments.Current = $true }
if (-not [string]::IsNullOrWhiteSpace($SessionId)) { $queryArguments.SessionId = $SessionId }
if (-not [string]::IsNullOrWhiteSpace($RecordId)) { $queryArguments.RecordId = $RecordId }
if (-not [string]::IsNullOrWhiteSpace($LaunchToken)) { $queryArguments.LaunchToken = $LaunchToken }
$resolved = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') @queryArguments
$target = $resolved.route
if ($null -eq $target) { throw 'Presence target could not be resolved uniquely.' }

$activityKeyValue = $ActivityKey.Trim()
if ($activityKeyValue.Length -gt 128) { throw 'ActivityKey must be 128 characters or fewer.' }
$activitySummaryValue = ($ActivitySummary -replace '[\r\n]+', ' ').Trim()
if ($activitySummaryValue.Length -gt 240) { throw 'ActivitySummary must be 240 characters or fewer.' }
$now = [DateTime]::UtcNow
$expires = if ($Availability -eq 'Unknown') { '' } else { $now.AddSeconds($TtlSeconds).ToString('o') }
$presenceUpdate = [pscustomobject]@{ recordId = [string]$target.recordId; availability = $Availability; updatedUtc = $now.ToString('o'); expiresUtc = $expires; activityKey = $activityKeyValue; activitySummary = $activitySummaryValue }
Invoke-ESCodexRegistryUpdate -Path $registryPath -ExpectedRevision $ExpectedRegistryRevision -Update {
    param($registry, $update)
    $record = @($registry.sessions | Where-Object { [string]$_.recordId -eq [string]$update.recordId } | Select-Object -First 1)[0]
    if ($null -eq $record) { throw "Presence target disappeared from registry: $($update.recordId)" }
    $record.availability = [string]$update.availability
    $record.availabilityUpdatedUtc = [string]$update.updatedUtc
    $record.availabilityExpiresUtc = [string]$update.expiresUtc
    $record.activityKey = [string]$update.activityKey
    $record.activitySummary = [string]$update.activitySummary
    $record.lastSeenUtc = [string]$update.updatedUtc
} -Argument $presenceUpdate | Out-Null

& (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') -RecordId ([string]$target.recordId) -RequireUnique -StateRoot $StateRoot
