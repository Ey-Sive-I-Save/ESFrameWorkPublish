[CmdletBinding()]
param(
    [switch]$ProbeAppServer,
    [switch]$SkipUiObservation,
    [switch]$SkipReadinessRefresh,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$skillRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
$manifestPath = Join-Path $skillRoot 'session-product.json'
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')

$issues = [Collections.Generic.List[object]]::new()
function Add-DoctorIssue([string]$Code, [string]$Severity, [string]$Summary, [bool]$BlocksCommercialBaseline, [bool]$RequiresAuthorization, [string]$Command = '') {
    $issues.Add([pscustomobject][ordered]@{
            code = $Code
            severity = $Severity
            summary = $Summary
            blocksCommercialBaseline = $BlocksCommercialBaseline
            requiresAuthorization = $RequiresAuthorization
            command = $Command
        })
}

$manifest = $null
try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    Add-DoctorIssue 'ESCS-CODE-001' 'error' ('Product manifest is missing or invalid: ' + $_.Exception.Message) $true $false
}

$parserFailures = @()
foreach ($file in @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.ps1')) {
    $tokens = $null
    $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    foreach ($parseError in @($parseErrors)) {
        $parserFailures += [pscustomobject]@{ path = $file.FullName; line = $parseError.Extent.StartLineNumber; message = $parseError.Message }
    }
}
if ($parserFailures.Count -gt 0) {
    Add-DoctorIssue 'ESCS-CODE-002' 'error' "$($parserFailures.Count) PowerShell parser error(s) were found." $true $false
}

$platformSupported = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
if (-not $platformSupported) {
    Add-DoctorIssue 'ESCS-ENV-001' 'error' 'This project terminal integration is supported only on Windows.' $true $false
}
$powerShellCompatible = $PSVersionTable.PSVersion -ge [Version]'5.1'
if (-not $powerShellCompatible) {
    Add-DoctorIssue 'ESCS-ENV-002' 'error' 'PowerShell 5.1 or later is required.' $true $false
}
$codexCommand = Get-Command codex -ErrorAction SilentlyContinue
$codexVersion = if ($null -eq $codexCommand) { '' } else { [string](& codex --version 2>$null) }
if ($null -eq $codexCommand) {
    Add-DoctorIssue 'ESCS-ENV-003' 'error' 'Codex CLI was not found on PATH.' $true $false
}
$wtCommand = Get-Command wt.exe -ErrorAction SilentlyContinue
if ($null -eq $wtCommand) {
    Add-DoctorIssue 'ESCS-ENV-004' 'warning' 'Windows Terminal was not found; only PlainCmd fallback is available.' $false $false
}

$hookConfigPath = Join-Path $projectRoot '.codex\hooks.json'
$hookConfigValid = $false
$hookConfigError = ''
try {
    if (-not (Test-Path -LiteralPath $hookConfigPath -PathType Leaf)) { throw 'hooks.json is missing.' }
    $hookConfig = Get-Content -LiteralPath $hookConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $events = @($hookConfig.hooks.PSObject.Properties.Name)
    if ('Stop' -notin $events -or 'UserPromptSubmit' -notin $events) { throw 'Stop and UserPromptSubmit hooks are both required.' }
    $hookConfigValid = $true
}
catch {
    $hookConfigError = $_.Exception.Message
    Add-DoctorIssue 'ESCS-HOOK-001' 'error' ('Project hook configuration is invalid: ' + $hookConfigError) $true $false
}

$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$status = $null
$statusError = ''
$repair = $null
try {
    $repair = & (Join-Path $PSScriptRoot 'Repair-ESCodexSessionState.ps1') -SkipUiObservation:$SkipUiObservation -SkipReadinessRefresh:$SkipReadinessRefresh -StateRoot $localStateRoot
    $status = $repair.statusBefore
}
catch {
    $statusError = $_.Exception.Message
    Add-DoctorIssue 'ESCS-STATE-001' 'error' ('Authoritative registry inspection failed: ' + $statusError) $true $false
}
$applicableRepairCount = if ($null -eq $repair) { 0 } else { @($repair.proposedActions | Where-Object applicable).Count }
if ($null -ne $status -and $status.registryNeedsUpgrade) {
    Add-DoctorIssue 'ESCS-STATE-003' 'error' 'Authoritative registry requires schema v2 migration.' $true $true "& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Repair -Apply"
}
if ($applicableRepairCount -gt 0) {
    Add-DoctorIssue 'ESCS-STATE-004' 'warning' "$applicableRepairCount safe registry repair action(s) remain unapplied." $true $true "& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Repair -Apply"
}

$messageRepair = $null
try { $messageRepair = & (Join-Path $PSScriptRoot 'Repair-ESCodexSessionMessages.ps1') -StateRoot $localStateRoot }
catch {
    Add-DoctorIssue 'ESCS-MSG-001' 'error' ('Message reconciliation failed: ' + $_.Exception.Message) $true $false
}

$broker = $null
try { $broker = & (Join-Path $PSScriptRoot 'Get-ESCodexSessionBrokerStatus.ps1') -ProbeAppServer:$ProbeAppServer -StateRoot $localStateRoot }
catch {
    Add-DoctorIssue 'ESCS-BROKER-001' 'error' ('Broker capability inspection failed: ' + $_.Exception.Message) $true $false
}
if ($null -ne $broker -and -not $broker.turnBoundaryHookTrustVerified) {
    Add-DoctorIssue 'ESCS-HOOK-002' 'warning' "Hook activation is observed for $($broker.loadedAndObservedSessionCount) of $($broker.eligibleHookSessionCount) eligible session(s)." $false $false '/hooks'
}
if ($null -ne $broker -and -not $broker.spontaneousIdleTuiWakeSupported) {
    Add-DoctorIssue 'ESCS-HOST-001' 'info' 'A completely idle standalone TUI cannot be awakened without user input; use busy-completion or next-prompt delivery.' $false $false
}
if ($null -ne $broker -and -not $broker.directExistingTuiInjectionSupported) {
    Add-DoctorIssue 'ESCS-HOST-002' 'info' 'Direct injection into an existing standalone TUI is not supported; cooperative mailbox delivery remains the commercial baseline.' $false $false
}

$codeReady = $null -ne $manifest -and $parserFailures.Count -eq 0 -and $hookConfigValid
$environmentReady = $platformSupported -and $powerShellCompatible -and $null -ne $codexCommand
$stateReady = $null -ne $status -and -not $status.registryNeedsUpgrade -and $applicableRepairCount -eq 0
$cooperativeDeliveryReady = $codeReady -and $environmentReady -and $null -ne $broker -and $broker.cooperativeMailboxSupported
$commercialBaselineReady = $cooperativeDeliveryReady -and $stateReady
$fleetOperationalReady = $commercialBaselineReady -and [bool]$broker.turnBoundaryHookTrustVerified
$managedDirectDeliveryReady = $commercialBaselineReady -and [bool]$broker.directExistingTuiInjectionSupported

[pscustomobject][ordered]@{
    doctorContractVersion = 1
    product = if ($null -eq $manifest) { 'ES Codex Session Bootstrap' } else { [string]$manifest.product }
    productVersion = if ($null -eq $manifest) { '' } else { [string]$manifest.version }
    projectRoot = $projectRoot
    stateRoot = $localStateRoot
    codeReady = $codeReady
    environmentReady = $environmentReady
    stateReady = $stateReady
    cooperativeDeliveryReady = $cooperativeDeliveryReady
    commercialBaselineReady = $commercialBaselineReady
    fleetOperationalReady = $fleetOperationalReady
    managedDirectDeliveryReady = $managedDirectDeliveryReady
    hookDeliveryProfile = if ($null -eq $broker) { 'unavailable' } else { [string]$broker.hookDeliveryProfile }
    hookBlocksCooperativeBaseline = if ($null -eq $broker) { $true } else { [bool]$broker.hookBlocksCooperativeBaseline }
    compatibility = [pscustomobject]@{
        platform = [Environment]::OSVersion.Platform.ToString()
        platformSupported = $platformSupported
        powerShellVersion = $PSVersionTable.PSVersion.ToString()
        powerShellCompatible = $powerShellCompatible
        codexAvailable = $null -ne $codexCommand
        codexVersion = $codexVersion
        windowsTerminalAvailable = $null -ne $wtCommand
    }
    code = [pscustomobject]@{ manifestPath = $manifestPath; parserFailureCount = $parserFailures.Count; parserFailures = $parserFailures; hookConfigPath = $hookConfigPath; hookConfigValid = $hookConfigValid; hookConfigError = $hookConfigError }
    registry = [pscustomobject]@{ readable = $null -ne $status; error = $statusError; schemaVersion = if ($null -eq $status) { 0 } else { $status.registrySchemaVersion }; sourceSchemaVersion = if ($null -eq $status) { 0 } else { $status.registrySourceSchemaVersion }; needsUpgrade = if ($null -eq $status) { $true } else { $status.registryNeedsUpgrade }; revision = if ($null -eq $status) { 0 } else { $status.registryRevision }; registered = if ($null -eq $status) { 0 } else { $status.totalRegistered }; repairPlannedCount = if ($null -eq $repair) { 0 } else { @($repair.proposedActions).Count }; applicableRepairCount = $applicableRepairCount; corruptionMode = if ($null -eq $repair) { $false } else { [bool](Get-ESCodexPropertyValue $repair 'corruptionMode' $false) } }
    messages = [pscustomobject]@{ total = if ($null -eq $status) { 0 } else { $status.totalMessages }; pending = if ($null -eq $status) { 0 } else { $status.pendingMessages }; repairPlanned = if ($null -eq $messageRepair) { 0 } else { $messageRepair.plannedCount } }
    broker = $broker
    issues = $issues.ToArray()
}
