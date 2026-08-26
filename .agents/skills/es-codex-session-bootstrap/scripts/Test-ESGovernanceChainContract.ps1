[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$ContractPath
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$contractFull = if ([IO.Path]::IsPathRooted($ContractPath)) { [IO.Path]::GetFullPath($ContractPath) } else { [IO.Path]::GetFullPath((Join-Path $root $ContractPath)) }
function Fail-P0([string]$Code, [string]$Detail) {
    throw "ES-P0-${Code}: $Detail"
}
if (-not (Test-Path -LiteralPath $contractFull -PathType Leaf)) { Fail-P0 'CONTRACT-001' "Contract not found: $ContractPath" }
try {
    $json = [IO.File]::ReadAllText($contractFull, [Text.UTF8Encoding]::new($false, $true)) | ConvertFrom-Json
} catch { Fail-P0 'CONTRACT-001' 'Contract is not strict UTF-8 JSON.' }
if ([int]$json.schemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace([string]$json.planId)) { Fail-P0 'CONTRACT-001' 'schemaVersion and planId are required.' }
$requiredGlobal = @('defaultMode','aToDWritePolicy','forbiddenWithoutExplicitAuthorization','authority','runtimeClaims','unknownHandling')
foreach ($name in $requiredGlobal) { if ($null -eq $json.globalRules.PSObject.Properties[$name]) { Fail-P0 'CONTRACT-001' "Missing globalRules.$name." } }
$static = $json.staticValidation
if ($null -eq $static) { Fail-P0 'CONTRACT-001' 'staticValidation is required.' }
foreach ($name in @('required','keywordAuthorityRefs','requiredFields','verificationStates','globalKeywords','blockOverridesRequired','baselineRef','blockOverrides')) {
    if ($null -eq $static.PSObject.Properties[$name]) { Fail-P0 'CONTRACT-001' "Missing staticValidation.$name." }
}
if (-not [bool]$static.required -or -not [bool]$static.blockOverridesRequired) { Fail-P0 'CONTRACT-001' 'Static validation and block overrides must be mandatory.' }
$requiredStatic = @('validationKeywords','requiredChecks','negativeCases','evidenceReceipt','verificationState')
foreach ($field in $requiredStatic) { if (@($static.requiredFields) -notcontains $field) { Fail-P0 'CONTRACT-001' "Required static field omitted: $field." } }
$validStates = @('static-passed','runtime-not-run','runtime-passed','runtime-failed','blocked','stale')
foreach ($state in @($static.verificationStates)) { if ($validStates -notcontains [string]$state) { Fail-P0 'CONTRACT-001' "Unknown verification state: $state." } }
foreach ($reference in @($static.keywordAuthorityRefs)) {
    $refPath = Join-Path $root ([string]$reference)
    if (-not (Test-Path -LiteralPath $refPath -PathType Leaf)) { Fail-P0 'CONTRACT-002' "Keyword authority reference is missing: $reference" }
}
$baselinePath = Join-Path $root ([string]$static.baselineRef)
if (-not (Test-Path -LiteralPath $baselinePath -PathType Leaf)) { Fail-P0 'CONTRACT-002' "Static validation baseline is missing: $($static.baselineRef)" }
try { $baseline = [IO.File]::ReadAllText($baselinePath, [Text.UTF8Encoding]::new($false, $true)) | ConvertFrom-Json } catch { Fail-P0 'CONTRACT-002' 'Static validation baseline is not strict UTF-8 JSON.' }
if ([int]$baseline.schemaVersion -ne 1 -or $null -eq $baseline.requiredByBlock) { Fail-P0 'CONTRACT-002' 'Static validation baseline is malformed.' }
$blocks = @($json.blocks)
if ($blocks.Count -ne 12) { Fail-P0 'CONTRACT-001' "Expected 12 blocks, found $($blocks.Count)." }
$allSteps = @($blocks | ForEach-Object { @($_.steps) })
if ($allSteps.Count -ne 76 -or @($allSteps | Sort-Object -Unique).Count -ne 76 -or (@($allSteps | Sort-Object -Unique) -join ',') -ne ((1..76) -join ',')) { Fail-P0 'CONTRACT-001' 'Block steps must cover unique contiguous steps 1..76.' }
$blockIds = @($blocks | ForEach-Object { [string]$_.blockId })
foreach ($block in $blocks) {
    $id = [string]$block.blockId
    if ([string]::IsNullOrWhiteSpace($id)) { Fail-P0 'CONTRACT-001' 'Every block requires blockId.' }
    foreach ($name in @('objective','steps','inputs','allowedWrites','artifactPath','budget','stopConditions','evidenceLevel','runtimeStatus','rollback')) {
        if ($null -eq $block.PSObject.Properties[$name]) { Fail-P0 'CONTRACT-001' "Block $id is missing $name." }
    }
    $override = $static.blockOverrides.PSObject.Properties[$id].Value
    if ($null -eq $override) { Fail-P0 'CONTRACT-001' "Block $id has no staticValidation override." }
    foreach ($name in $requiredStatic) {
        $value = $override.PSObject.Properties[$name].Value
        if ($null -eq $value -or (@($value).Count -eq 0)) { Fail-P0 'CONTRACT-001' "Block $id has empty staticValidation.$name." }
    }
    $baselineBlock = $baseline.requiredByBlock.PSObject.Properties[$id].Value
    if ($null -eq $baselineBlock) { Fail-P0 'CONTRACT-002' "Block $id is missing from the static validation baseline." }
    foreach ($name in @('validationKeywords','requiredChecks','negativeCases')) {
        foreach ($requiredValue in @($baselineBlock.PSObject.Properties[$name].Value)) {
            if (@($override.PSObject.Properties[$name].Value | ForEach-Object { [string]$_ }) -notcontains [string]$requiredValue) { Fail-P0 'CONTRACT-002' "Block $id removed baseline $name value: $requiredValue" }
        }
    }
    if ($validStates -notcontains [string]$override.verificationState) { Fail-P0 'CONTRACT-001' "Block $id has invalid verificationState." }
}
foreach ($id in @('A','B','C','D')) {
    $block = @($blocks | Where-Object { [string]$_.blockId -eq $id })[0]
    if (@($block.allowedWrites | Where-Object { [string]$_ -ne 'candidate-report' }).Count -gt 0) { Fail-P0 'CONTRACT-003' "Block $id exceeds candidate-only writes." }
}
$kOverride = $static.blockOverrides.PSObject.Properties['K'].Value
if ($null -eq $kOverride -or @($kOverride.validationKeywords) -notcontains 'hard-block' -or @($kOverride.negativeCases) -notcontains 'score-overrides-block') { Fail-P0 'CONTRACT-004' 'Score policy must preserve hard blocks and reject score override.' }
if (@($json.globalRules.forbiddenWithoutExplicitAuthorization | Where-Object { [string]$_ -match '(?i)delete|route-change|catalog-update|registry-update|AGENTS-update' }).Count -lt 4) { Fail-P0 'CONTRACT-003' 'Global forbidden expansion list is incomplete.' }
$result = [ordered]@{
    validator = 'Test-ESGovernanceChainContract'
    contract = $ContractPath
    decisionStatus = 'Accepted'
    p0Status = 'passed'
    blockCount = $blocks.Count
    atomicStepCount = $allSteps.Count
    staticValidationOverrides = @($static.blockOverrides.PSObject.Properties).Count
    runtimeStatus = 'runtime-not-run'
    evidenceLevel = 'S1-static'
}
[pscustomobject]$result
