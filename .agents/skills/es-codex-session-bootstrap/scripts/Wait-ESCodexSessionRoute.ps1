[CmdletBinding()]
param(
    [string]$SessionId = '',
    [string]$RecordId = '',
    [string]$ResponsibilityKey = '',
    [string]$LaunchToken = '',
    [switch]$Current,
    [ValidateSet('Ready', 'Active', 'Idle', 'Waiting', 'NotBusy', 'Terminal')]
    [string]$WaitFor = 'Ready',
    [ValidateRange(0, 60)]
    [int]$TimeoutSeconds = 30,
    [ValidateRange(250, 10000)]
    [int]$PollMilliseconds = 1000,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

$queryArguments = @{ RequireUnique = $true; StateRoot = $StateRoot }
if ($Current) { $queryArguments.Current = $true }
if (-not [string]::IsNullOrWhiteSpace($SessionId)) { $queryArguments.SessionId = $SessionId }
if (-not [string]::IsNullOrWhiteSpace($RecordId)) { $queryArguments.RecordId = $RecordId }
if (-not [string]::IsNullOrWhiteSpace($ResponsibilityKey)) { $queryArguments.ResponsibilityKey = $ResponsibilityKey }
if (-not [string]::IsNullOrWhiteSpace($LaunchToken)) { $queryArguments.LaunchToken = $LaunchToken }

$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$completed = $false
$lastQuery = $null
do {
    $lastQuery = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') @queryArguments
    $route = $lastQuery.route
    $completed = switch ($WaitFor) {
        'Ready' { [string]$route.waitState -eq 'Ready' }
        'Active' { [bool]$route.isRoutable }
        'Idle' { [string]$route.effectiveAvailability -eq 'Idle' }
        'Waiting' { [string]$route.effectiveAvailability -eq 'Waiting' }
        'NotBusy' { [string]$route.effectiveAvailability -in @('Idle', 'Waiting') }
        'Terminal' { [bool]$route.isTerminal }
    }
    if ($completed -or $stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) { break }
    Start-Sleep -Milliseconds $PollMilliseconds
} while ($true)
$stopwatch.Stop()

[pscustomobject][ordered]@{
    mode = 'Wait'
    waitFor = $WaitFor
    completed = $completed
    timedOut = -not $completed
    elapsedMilliseconds = [int]$stopwatch.Elapsed.TotalMilliseconds
    pollMilliseconds = $PollMilliseconds
    query = $lastQuery
}
