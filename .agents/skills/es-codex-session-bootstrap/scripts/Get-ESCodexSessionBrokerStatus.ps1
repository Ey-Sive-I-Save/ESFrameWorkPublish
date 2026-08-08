[CmdletBinding()]
param(
    [switch]$ProbeAppServer,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')
$codex = Get-Command codex -ErrorAction SilentlyContinue
$version = if ($null -eq $codex) { '' } else { [string](& codex --version 2>$null) }
$daemonSupported = [Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$hookConfigPath = Join-Path $projectRoot '.codex\hooks.json'
$hookConfigPresent = Test-Path -LiteralPath $hookConfigPath -PathType Leaf
$hookScriptPath = Join-Path $PSScriptRoot 'Receive-ESCodexSessionMessageHook.ps1'
$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$registryReadable = $true
$registryError = ''
try { $registry = Read-ESCodexSessionRegistry (Join-Path $localStateRoot 'sessions.json') }
catch { $registryReadable = $false; $registryError = $_.Exception.Message; $registry = [pscustomobject]@{ sessions = @() } }
$hookActivations = @($registry.sessions | ForEach-Object { Test-ESCodexHookActivation $localStateRoot ([string]$_.recordId) ([string]$_.sessionId) $hookConfigPath $hookScriptPath })
$validHookActivations = @($hookActivations | Where-Object valid)
$eligibleHookSessionCount = @($registry.sessions | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.sessionId) -and [string]$_.lifecycleStatus -ne 'Closed' }).Count
$allEligibleHooksObserved = $eligibleHookSessionCount -gt 0 -and $validHookActivations.Count -eq $eligibleHookSessionCount
$probe = $null
$probeError = ''
if ($ProbeAppServer) {
    try { $probe = & (Join-Path $PSScriptRoot 'Test-ESCodexAppServerCapability.ps1') }
    catch { $probeError = $_.Exception.Message }
}
[pscustomobject][ordered]@{
    brokerContractVersion = 1
    platform = [Environment]::OSVersion.Platform.ToString()
    codexVersion = $version
    codexAvailable = $null -ne $codex
    registryReadable = $registryReadable
    registryError = $registryError
    appServerStdioExpected = $null -ne $codex
    appServerStdioProbed = $null -ne $probe
    appServerStdioProbe = $probe
    appServerStdioProbeError = $probeError
    managedDaemonSupported = $daemonSupported
    managedDaemonReason = if ($daemonSupported) { '' } else { 'This Codex CLI reports app-server daemon lifecycle as Unix-only on Windows.' }
    cooperativeMailboxSupported = $true
    turnBoundaryHookConfigured = $hookConfigPresent
    turnBoundaryHookConfigPath = $hookConfigPath
    turnBoundaryHookTrustVerified = $allEligibleHooksObserved
    anyTurnBoundaryHookObserved = $validHookActivations.Count -gt 0
    eligibleHookSessionCount = $eligibleHookSessionCount
    loadedAndObservedSessionCount = $validHookActivations.Count
    hookActivations = $hookActivations
    automaticBusyCompletionDeliveryConfigured = $hookConfigPresent
    automaticBusyCompletionDeliveryActive = $validHookActivations.Count -gt 0
    nextUserPromptDeliveryConfigured = $hookConfigPresent
    nextUserPromptDeliveryActive = $validHookActivations.Count -gt 0
    spontaneousIdleTuiWakeSupported = $false
    hookActivationNote = 'Project hooks require Codex trust review and a session reload; configuration presence is not proof that an existing window loaded or trusted them.'
    directExistingTuiInjectionSupported = $false
    directExistingTuiInjectionReason = 'No durable cross-process active-turn identity or supported Windows daemon attachment is proven for existing standalone TUI processes.'
    safestDeliveryMode = 'cooperative-mailbox'
    futureManagedMode = 'app-server-stdio-client'
}
