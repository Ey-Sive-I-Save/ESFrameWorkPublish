[CmdletBinding()]
param(
    [switch]$RunPester,
    [switch]$ProbeAppServer
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$skillRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))

$pester = [pscustomobject]@{
    requested = [bool]$RunPester
    available = $null -ne (Get-Command Invoke-Pester -ErrorAction SilentlyContinue)
    total = 0
    passed = 0
    failed = 0
}
if ($RunPester -and $pester.available) {
    $testsPath = [IO.Path]::Combine($skillRoot, 'tests')
    $testResult = Invoke-Pester -Script $testsPath -PassThru
    $pester.total = [int]$testResult.TotalCount
    $pester.passed = [int]$testResult.PassedCount
    $pester.failed = [int]$testResult.FailedCount
}

$doctorScript = Join-Path $PSScriptRoot 'Get-ESCodexSessionDoctor.ps1'
$smokeScript = Join-Path $PSScriptRoot 'Test-ESCodexSessionOperationalFlow.ps1'
$doctor = & $doctorScript -ProbeAppServer:$ProbeAppServer
$operationalSmoke = & $smokeScript
$testsReady = -not $RunPester -or ($pester.available -and $pester.failed -eq 0)
$codeReady = [bool]$doctor.codeReady -and $testsReady -and [bool]$operationalSmoke.passed
$commercialBaselineReady = $codeReady -and [bool]$doctor.commercialBaselineReady
$fleetOperationalReady = $codeReady -and [bool]$doctor.fleetOperationalReady
$managedDirectDeliveryReady = $codeReady -and [bool]$doctor.managedDirectDeliveryReady

$limitations = [Collections.Generic.List[string]]::new()
if ($RunPester -and -not $pester.available) { $limitations.Add('PesterUnavailable') }
if ($RunPester -and $pester.failed -gt 0) { $limitations.Add('PesterNotPassing') }
foreach ($issue in @($doctor.issues)) { $limitations.Add([string]$issue.code) }

$nextActions = @($doctor.issues |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.command) } |
        ForEach-Object { [pscustomobject]@{ code = [string]$_.code; command = [string]$_.command; requiresAuthorization = [bool]$_.requiresAuthorization } } |
        Sort-Object code -Unique)

[pscustomobject][ordered]@{
    readinessContractVersion = 2
    projectRoot = $projectRoot
    skillRoot = $skillRoot
    productVersion = [string]$doctor.productVersion
    codeReady = $codeReady
    operationalReady = $fleetOperationalReady
    commercialBaselineReady = $commercialBaselineReady
    fleetOperationalReady = $fleetOperationalReady
    managedDirectDeliveryReady = $managedDirectDeliveryReady
    commercialClaimAllowed = $commercialBaselineReady
    commercialDeliveryProfile = 'cooperative-mailbox'
    parser = [pscustomobject]@{ passed = [int]$doctor.code.parserFailureCount -eq 0; failureCount = [int]$doctor.code.parserFailureCount; failures = @($doctor.code.parserFailures) }
    hookConfig = [pscustomobject]@{ path = [string]$doctor.code.hookConfigPath; valid = [bool]$doctor.code.hookConfigValid; error = [string]$doctor.code.hookConfigError; trustVerified = [bool]$doctor.broker.turnBoundaryHookTrustVerified }
    pester = $pester
    operationalSmoke = $operationalSmoke
    registry = [pscustomobject]@{ readable = [bool]$doctor.registry.readable; error = [string]$doctor.registry.error; sourceSchemaVersion = [int]$doctor.registry.sourceSchemaVersion; needsUpgrade = [bool]$doctor.registry.needsUpgrade; revision = [int]$doctor.registry.revision; registered = [int]$doctor.registry.registered; repairPlanned = [int]$doctor.registry.repairPlannedCount; repairApplicable = [int]$doctor.registry.applicableRepairCount; repairApplied = 0; corruptionMode = [bool]$doctor.registry.corruptionMode }
    messages = [pscustomobject]@{ total = [int]$doctor.messages.total; pending = [int]$doctor.messages.pending; repairPlanned = [int]$doctor.messages.repairPlanned; repairApplied = 0 }
    broker = $doctor.broker
    doctor = $doctor
    limitations = $limitations.ToArray()
    nextRequiredActions = $nextActions
}
