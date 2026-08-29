[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$PartPath,
    [string]$ReportPath = 'ES/Output/ABC/abc-part-validation.json',
    [string]$CoreReplayReportPath = 'ES/Output/ABC/abc-core-static-replay.json',
    [string]$PartReplayReportPath = 'ES/Output/ABC/abc-part-static-replay.json',
    [string]$Head
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$modulePath = Join-Path $PSScriptRoot 'ESAbcPartToolchain.psm1'
Import-Module -Name $modulePath -Force
$schemaModulePath = Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1'
Import-Module -Name $schemaModulePath -Force
$evidenceContractPath = Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
if (-not (Test-Path -LiteralPath $evidenceContractPath -PathType Leaf)) { throw 'Central evidence receipt contract is missing' }
$evidenceContractHash = (Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()

$issues = New-Object 'System.Collections.Generic.List[string]'
$partFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $PartPath -MustExist
$part = Read-ESAbcJson -Path $partFull
$core = Get-ESAbcCoreContract -ProjectRoot $root
$routeRegistry = Get-ESAbcRouteStageRegistry -ProjectRoot $root
$modeRegistry = Get-ESAbcModeRegistry -ProjectRoot $root
$authorityRegistry = Get-ESAbcPartAuthorityRegistry -ProjectRoot $root
$semantic = Test-ESAbcPartContractObject -Part $part -Core $core -RouteRegistry $routeRegistry -ModeRegistry $modeRegistry -AuthorityRegistry $authorityRegistry

$schemaPath = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath 'ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json' -MustExist
$partSchemaPath = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath 'ES/Automation/Contracts/es-ai-abc-part-v1.schema.json' -MustExist
$authoringSchemaPath = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath 'ES/Automation/Contracts/es-ai-abc-part-authoring-request-v1.schema.json' -MustExist
$routeSchemaPath = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath 'ES/Automation/Contracts/es-route-stage-registry-v1.schema.json' -MustExist
try {
    $schemaSupportedErrors = @()
    foreach ($schemaFullPath in @($schemaPath, $partSchemaPath, $authoringSchemaPath, $routeSchemaPath)) { $schemaSupportedErrors += @(Test-ESJsonSchemaSupported -SchemaPath $schemaFullPath) }
    if ($schemaSupportedErrors.Count -gt 0) { throw ('unsupported schema keyword: ' + ($schemaSupportedErrors -join '; ')) }
    $partSchemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $partSchemaPath -Value $part)
    $routeSchemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $routeSchemaPath -Value $routeRegistry)
    if ($partSchemaErrors.Count -gt 0) { throw ('Part schema mismatch: ' + ($partSchemaErrors -join '; ')) }
    if ($routeSchemaErrors.Count -gt 0) { throw ('RouteStage schema mismatch: ' + ($routeSchemaErrors -join '; ')) }
    $schemaReadable = $true
} catch {
    $schemaReadable = $false
    [void]$issues.Add('one or more ABC schemas are invalid JSON: ' + $_.Exception.Message)
}

if ([string]::IsNullOrWhiteSpace($Head)) { $Head = Get-ESAbcCurrentHead -ProjectRoot $root }
$interfaceReplays = @()
$interface = [pscustomobject][ordered]@{ status = 'blocked'; issues = @('interface replay was not generated'); replayCount = 0 }
if ([string]$semantic.status -eq 'passed' -and $schemaReadable) {
    try {
        $interfaceReplays = @(New-ESAbcInterfaceReplay -Part $part -Core $core -ProjectRoot $root -ReportPath $ReportPath -Head $Head)
        $interfaceSchemaErrors = New-Object 'System.Collections.Generic.List[string]'
        foreach ($interfaceReplay in $interfaceReplays) {
            foreach ($schemaError in @(Test-ESJsonSchemaValue -SchemaPath $schemaPath -Value ([pscustomobject]$interfaceReplay))) { [void]$interfaceSchemaErrors.Add([string]$schemaError) }
        }
        if ($interfaceSchemaErrors.Count -gt 0) { throw ('ABCC interface schema mismatch: ' + ($interfaceSchemaErrors.ToArray() -join '; ')) }
        $interface = Test-ESAbcInterfaceReplays -Replays $interfaceReplays -Part $part
    } catch {
        [void]$issues.Add('ABCC interface replay generation failed: ' + $_.Exception.Message)
    }
}

$coreReplayStatus = 'blocked'
$partReplayStatus = 'blocked'
$coreReplayFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $CoreReplayReportPath
$partReplayFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $PartReplayReportPath
$coreScript = Join-Path $root '.agents/skills/es-ai-abc-core/scripts/Test-es-ai-abc-core-StaticReplay.ps1'
$partScript = Join-Path $root '.agents/skills/es-weapon-abc-part/scripts/Test-es-weapon-abc-part-StaticReplay.ps1'
try {
    & $coreScript -ProjectRoot $root -ReportPath $CoreReplayReportPath | Out-Null
    $coreExit = $LASTEXITCODE
    if (Test-Path -LiteralPath $coreReplayFull -PathType Leaf) {
        $coreReplay = Read-ESAbcJson -Path $coreReplayFull
        $coreReplayStatus = if ($coreExit -eq 0 -and [string]$coreReplay.status -eq 'passed') { 'passed' } else { 'blocked' }
    } else { [void]$issues.Add('ABCC Core replay did not produce a receipt') }
} catch { [void]$issues.Add('ABCC Core replay failed: ' + $_.Exception.Message) }
try {
    & $partScript -ProjectRoot $root -ReportPath $PartReplayReportPath | Out-Null
    $partExit = $LASTEXITCODE
    if (Test-Path -LiteralPath $partReplayFull -PathType Leaf) {
        $partReplay = Read-ESAbcJson -Path $partReplayFull
        $partReplayStatus = if ($partExit -eq 0 -and [string]$partReplay.status -eq 'passed') { 'passed' } else { 'blocked' }
    } else { [void]$issues.Add('Weapon ABCP replay did not produce a receipt') }
} catch { [void]$issues.Add('Weapon ABCP replay failed: ' + $_.Exception.Message) }

$reportFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $ReportPath
$sourceRefs = @(
    $PartPath.Replace('\', '/'),
    'ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json',
    'ES/Automation/Contracts/ESJsonSchemaLite.psm1',
    'ES/Automation/Contracts/es-ai-abc-part-v1.schema.json',
    'ES/Automation/Contracts/es-ai-abc-part-authoring-request-v1.schema.json',
    'ES/Automation/Contracts/es-ai-abc-core-v1.json',
    'ES/Automation/Contracts/es-weapon-abc-part-authority-v1.json',
    'ES/Automation/Contracts/es-route-stage.registry.json',
    'ES/Automation/Contracts/es-route-stage-registry-v1.schema.json',
    'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json',
    '.agents/skills/es-weapon-abc-part/scripts/Test-ESAbcPartContract.ps1',
    '.agents/skills/es-weapon-abc-part/governance.json',
    '.agents/skills/es-weapon-abc-part/scripts/ESAbcPartToolchain.psm1',
    $CoreReplayReportPath.Replace('\', '/'),
    $PartReplayReportPath.Replace('\', '/')
)
$sourceRefHashes = [ordered]@{}
foreach ($sourceRef in $sourceRefs) {
    try {
        $sourceFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $sourceRef -MustExist
        $sourceRefHashes[$sourceRef] = (Get-FileHash -LiteralPath $sourceFull -Algorithm SHA256).Hash.ToLowerInvariant()
    } catch {
        [void]$issues.Add('sourceRef unavailable: ' + $sourceRef)
    }
}
$allChecks = @($semantic.checks)
$allChecks += [pscustomobject][ordered]@{ id = 'interface-replay'; status = [string]$interface.status; detail = if ([string]$interface.status -eq 'passed') { 'ABCC interface replay normalized every declared capability' } else { ($interface.issues -join '; ') } }
$allChecks += [pscustomobject][ordered]@{ id = 'abcc-core-static-replay'; status = $coreReplayStatus; detail = 'ABCC Core StaticDeepReplay receipt is required for ABCP acceptance' }
$allChecks += [pscustomobject][ordered]@{ id = 'weapon-abcp-static-replay'; status = $partReplayStatus; detail = 'Weapon ABCP StaticDeepReplay receipt is required for ABCP acceptance' }
$allChecks += [pscustomobject][ordered]@{ id = 'abc-system-used'; status = if ($coreReplayStatus -eq 'passed' -and $interfaceReplays.Count -gt 0) { 'passed' } else { 'blocked' }; detail = 'ABCP validation is bound to ABCC Core and A-to-B interface replay' }
$allIssues = @($issues) + @($semantic.issues) + @($interface.issues)
$overallPassed = ($schemaReadable -and [string]$semantic.status -eq 'passed' -and [string]$interface.status -eq 'passed' -and $coreReplayStatus -eq 'passed' -and $partReplayStatus -eq 'passed' -and $allIssues.Count -eq 0)
$result = [ordered]@{
    schemaVersion = 1
    evidenceContractId = 'es.skill-evidence-receipt'
    evidenceContractHash = $evidenceContractHash
    skillName = 'es-weapon-abc-part'
    case = 'ABCP-ABC-system-validation'
    status = if ($overallPassed) { 'passed' } else { 'blocked' }
    evidenceLevel = 'S1'
    receiptPath = $ReportPath.Replace('\', '/')
    sourceRefs = @($sourceRefs)
    sourceRefHashes = $sourceRefHashes
    toolId = 'es-abc-part-validator'
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    timestampUtc = [DateTime]::UtcNow.ToString('o')
    unityVersion = 'not-run (static validation)'
    authorizationKind = 'read-only'
    executionEnabled = $false
    abcSystemUsed = $true
    partPath = $PartPath.Replace('\', '/')
    partId = [string]$part.partId
    semanticStatus = [string]$semantic.status
    interfaceReplayStatus = [string]$interface.status
    interfaceReplayCount = $interfaceReplays.Count
    abccCoreReplay = [ordered]@{ status = $coreReplayStatus; receiptPath = $CoreReplayReportPath.Replace('\', '/') }
    weaponAbcpReplay = [ordered]@{ status = $partReplayStatus; receiptPath = $PartReplayReportPath.Replace('\', '/') }
    checks = @($allChecks)
    issues = @($allIssues)
    runtimeStatus = 'runtime-not-run'
    overallVerdict = if ($overallPassed) { 'ABCPStaticAcceptedThroughABCC' } else { 'ABCPStaticBlocked' }
    claimsNotProven = @('Unity/Runtime behavior', 'Prefab import, firing, damage, input, performance, Player, IL2CPP or release acceptance')
    nextAction = if ($overallPassed) { 'Runtime requires separate explicit authorization and fresh receipts.' } else { 'Resolve the listed ABC semantic, route, schema or replay issue before Runtime.' }
}
Write-ESAbcJson -Path $reportFull -Value $result
$result | ConvertTo-Json -Depth 20
if (-not $overallPassed) { exit 1 }
exit 0
