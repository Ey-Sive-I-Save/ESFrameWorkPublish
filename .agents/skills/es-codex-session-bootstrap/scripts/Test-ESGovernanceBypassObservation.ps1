[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$ObservationPath,
    [string]$ContractPath = '.agents/skills/es-codex-session-bootstrap/references/governance-bypass-observation.contract.json'
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Resolve-ProjectRelative([string]$Root, [string]$Path, [string]$Label) {
    if ([IO.Path]::IsPathRooted($Path)) { throw "ES-GOV-BYPASS-003: $Label must be project-relative" }
    $full = [IO.Path]::GetFullPath((Join-Path $Root $Path))
    $prefix = $Root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "ES-GOV-BYPASS-003: $Label escapes ProjectRoot" }
    return $full
}
function Resolve-ObservationPath([string]$Root, [string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return Resolve-ProjectRelative $Root $Path 'ObservationPath'
}
function Read-StrictJson([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "ES-GOV-BYPASS-001: $Label not found: $Path" }
    try { return [IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path, [Text.UTF8Encoding]::new($false, $true)) | ConvertFrom-Json } catch { throw "ES-GOV-BYPASS-001: $Label is not strict UTF-8 JSON" }
}
function Has-Property($Object, [string]$Name) { return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name] }
function Add-Error([System.Collections.Generic.List[string]]$Errors, [string]$Message) { $Errors.Add($Message) }

$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$observationFull = Resolve-ObservationPath $root $ObservationPath
$contractFull = Resolve-ProjectRelative $root $ContractPath 'ContractPath'
$observation = Read-StrictJson $observationFull 'Bypass observation'
$contract = Read-StrictJson $contractFull 'Bypass observation contract'
$errors = [System.Collections.Generic.List[string]]::new()

if ([int]$contract.schemaVersion -ne 1 -or [string]$contract.contractId -ne 'es-governance-bypass-observation-v1') { Add-Error $errors 'invalid bypass observation contract identity' }
foreach ($field in @($contract.requiredFields)) { if (-not (Has-Property $observation ([string]$field))) { Add-Error $errors "missing observation field: $field" } }
if ([string]$observation.routeMode -ne 'read-only-bypass-observation') { Add-Error $errors 'routeMode must be read-only-bypass-observation' }
if ([string]$observation.profile -ne [string]$contract.allowedProfile -or [string]$observation.scope -ne [string]$contract.allowedScope -or [string]$observation.scopeKind -ne [string]$contract.allowedScopeKind) { Add-Error $errors 'observation is outside the single allowed Profile/scope' }
if ([string]$observation.decisionIdAlgorithm -ne [string]$contract.decisionIdAlgorithm) { Add-Error $errors 'decisionId algorithm source is not the registered helper contract' }
if ([string]$observation.helperPath -ne [string]$contract.helperPath) { Add-Error $errors 'helperPath is not the registered decisionId helper' }
if ([bool]$observation.productionRouteIntegrated) { Add-Error $errors 'productionRouteIntegrated must remain false for a bypass observation' }
if ([bool]$observation.globalP0Integrated) { Add-Error $errors 'globalP0Integrated must remain false for a bypass observation' }
if ([string]$observation.decisionIdExpected -notmatch '^decision-[0-9a-fA-F]{24,64}$' -or [string]$observation.decisionIdObserved -notmatch '^decision-[0-9a-fA-F]{24,64}$') { Add-Error $errors 'decisionId expected and observed values must use the governed format' }
if ([string]$observation.decisionIdExpected -ne [string]$observation.decisionIdObserved) { Add-Error $errors 'decisionId mismatch observed' }
if ([bool]$observation.bypassDetected) { Add-Error $errors 'helper bypass detected' }
$projectionContainers = [System.Collections.Generic.List[object]]::new()
foreach ($field in @($contract.forbiddenProjectionFields)) {
    if (Has-Property $observation ([string]$field)) {
        Add-Error $errors "forbidden cross-contract projection field at observation root: $field"
    }
}
if (Has-Property $observation 'automationProjection' -and $null -ne $observation.automationProjection) {
    Add-Error $errors 'Automation to governance projection is forbidden by the isolation contract'
    $projectionContainers.Add($observation.automationProjection)
}
foreach ($container in $projectionContainers) {
    foreach ($field in @($contract.forbiddenProjectionFields)) {
        if (Has-Property $container ([string]$field)) { Add-Error $errors "forbidden cross-contract projection field: $field" }
    }
}
if (@($contract.allowedObservationStates | ForEach-Object { [string]$_ }) -notcontains [string]$observation.observationState) { Add-Error $errors 'observationState is not registered' }
if (@($contract.allowedRollbackStates | ForEach-Object { [string]$_ }) -notcontains [string]$observation.rollbackState) { Add-Error $errors 'rollbackState is not registered' }
$allowedCodes = @($contract.observationCodes | ForEach-Object { [string]$_ })
foreach ($event in @($observation.observations)) {
    if (-not (Has-Property $event 'code') -or $allowedCodes -notcontains [string]$event.code) { Add-Error $errors 'observation contains an unregistered code' }
}

$status = if ($errors.Count -eq 0) { 'Accepted' } else { 'Rejected' }
[pscustomobject][ordered]@{
    validator = 'Test-ESGovernanceBypassObservation'
    observationStatus = $status
    decisionStatus = $status
    acceptanceScope = [ordered]@{ profile = [string]$observation.profile; scope = [string]$observation.scope; scopeKind = [string]$observation.scopeKind }
    effect = [string]$contract.failureEffect
    bypassDetected = [bool]$observation.bypassDetected
    decisionIdMatched = [string]$observation.decisionIdExpected -eq [string]$observation.decisionIdObserved
    rollbackState = [string]$observation.rollbackState
    productionRouteIntegrated = [bool]$observation.productionRouteIntegrated
    globalP0Integrated = [bool]$observation.globalP0Integrated
    errors = @($errors)
    runtimeStatus = 'runtime-not-run'
}
