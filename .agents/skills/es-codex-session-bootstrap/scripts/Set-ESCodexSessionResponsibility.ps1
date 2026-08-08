[CmdletBinding()]
param(
    [string]$SessionId = '',
    [string]$RecordId = '',
    [string]$LaunchToken = '',
    [switch]$Current,
    [Parameter(Mandatory = $true)]
    [string]$NewResponsibilityKey,
    [int]$ExpectedRegistryRevision = -1,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')

$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$registryPath = Join-Path $localStateRoot 'sessions.json'
$registrySnapshot = Read-ESCodexSessionRegistry $registryPath
if ($registrySnapshot.requiresWriteUpgrade) { throw 'Responsibility binding requires authoritative registry schema v2. Run and review Repair -Apply first.' }

$newKey = $NewResponsibilityKey.Trim().ToLowerInvariant()
if ($newKey -notmatch '^[a-z0-9][a-z0-9._-]{1,63}$') {
    throw 'NewResponsibilityKey must contain 2-64 lowercase letters, digits, dots, underscores, or hyphens.'
}
$queryArguments = @{ RequireUnique = $true; StateRoot = $StateRoot }
if ($Current) { $queryArguments.Current = $true }
if (-not [string]::IsNullOrWhiteSpace($SessionId)) { $queryArguments.SessionId = $SessionId }
if (-not [string]::IsNullOrWhiteSpace($RecordId)) { $queryArguments.RecordId = $RecordId }
if (-not [string]::IsNullOrWhiteSpace($LaunchToken)) { $queryArguments.LaunchToken = $LaunchToken }
$resolved = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') @queryArguments
$target = $resolved.route
if ($null -eq $target) { throw 'Responsibility target could not be resolved uniquely.' }

$now = [DateTime]::UtcNow.ToString('o')
$responsibilityUpdate = [pscustomobject]@{ recordId = [string]$target.recordId; newKey = $newKey; updatedUtc = $now }
Invoke-ESCodexRegistryUpdate -Path $registryPath -ExpectedRevision $ExpectedRegistryRevision -Update {
    param($registry, $update)
    $record = @($registry.sessions | Where-Object { [string]$_.recordId -eq [string]$update.recordId } | Select-Object -First 1)[0]
    if ($null -eq $record) { throw "Responsibility target disappeared from registry: $($update.recordId)" }
    $conflicts = @($registry.sessions | Where-Object {
            [string]$_.recordId -ne [string]$update.recordId -and
            [string]$_.responsibilityKey -eq [string]$update.newKey -and
            [string]$_.lifecycleStatus -notin @('Closed', 'Lost')
        })
    if ($conflicts.Count -gt 0) {
        $details = @($conflicts | ForEach-Object { "$($_.recordId) | $($_.sessionId) | $($_.lifecycleStatus)" }) -join "`n"
        throw "Responsibility is already owned by another non-terminal session:`n$details"
    }
    $record.responsibilityKey = [string]$update.newKey
    $record.lastSeenUtc = [string]$update.updatedUtc
} -Argument $responsibilityUpdate | Out-Null

& (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') -RecordId ([string]$target.recordId) -RequireUnique -StateRoot $StateRoot
