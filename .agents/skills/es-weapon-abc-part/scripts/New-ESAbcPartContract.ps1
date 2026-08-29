[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$RequestPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [switch]$Force,
    [string]$ReceiptPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$root = [IO.Path]::GetFullPath($root).TrimEnd('\', '/')
$rootPrefix = $root + [IO.Path]::DirectorySeparatorChar
$rootProbe = [IO.Path]::GetFullPath([IO.Path]::Combine($root, '.es-abc-root-probe'))
if (-not $rootProbe.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'ProjectRoot probe escapes ProjectRoot.' }
$modulePath = Join-Path $PSScriptRoot 'ESAbcPartToolchain.psm1'
Import-Module -Name $modulePath -Force
$schemaModulePath = Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1'
Import-Module -Name $schemaModulePath -Force
$evidenceContractFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json' -MustExist
$evidenceContractHash = (Get-FileHash -LiteralPath $evidenceContractFull -Algorithm SHA256).Hash.ToLowerInvariant()

$requestFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $RequestPath -MustExist
$outputFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $OutputPath
$core = Get-ESAbcCoreContract -ProjectRoot $root
$routeRegistry = Get-ESAbcRouteStageRegistry -ProjectRoot $root
$modeRegistry = Get-ESAbcModeRegistry -ProjectRoot $root
$authorityRegistry = Get-ESAbcPartAuthorityRegistry -ProjectRoot $root
$request = Read-ESAbcJson -Path $requestFull
$requestSchemaFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath 'ES/Automation/Contracts/es-ai-abc-part-authoring-request-v1.schema.json' -MustExist
$requestSchemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $requestSchemaFull -Value $request)
if ($requestSchemaErrors.Count -gt 0) { throw ('Authoring request schema validation failed: ' + ($requestSchemaErrors -join '; ')) }
$part = New-ESAbcPartContractObject -Request $request -Core $core -RouteRegistry $routeRegistry -ModeRegistry $modeRegistry -AuthorityRegistry $authorityRegistry
$json = $part | ConvertTo-Json -Depth 24

if (Test-Path -LiteralPath $outputFull -PathType Container) { throw "OutputPath identifies a directory: $OutputPath" }
if (Test-Path -LiteralPath $outputFull -PathType Leaf) {
    $existing = [IO.File]::ReadAllText($outputFull, (New-Object Text.UTF8Encoding($false, $true)))
    $existingJson = $existing | ConvertFrom-Json -ErrorAction Stop
    $existingCanonical = $existingJson | ConvertTo-Json -Depth 24
    if ($existingCanonical -eq $json) {
        $writeStatus = 'reused'
    } elseif (-not $Force) {
        throw "OutputPath already contains a different Part contract; pass -Force only when replacement is intended: $OutputPath"
    } else {
        Write-ESAbcJson -Path $outputFull -Value $part
        $writeStatus = 'replaced'
    }
} else {
    Write-ESAbcJson -Path $outputFull -Value $part
    $writeStatus = 'created'
}

$receiptSourceRefs = @(
    $RequestPath.Replace('\', '/'),
    'ES/Automation/Contracts/ESJsonSchemaLite.psm1',
    'ES/Automation/Contracts/es-ai-abc-part-authoring-request-v1.schema.json',
    'ES/Automation/Contracts/es-ai-abc-part-v1.schema.json',
    'ES/Automation/Contracts/es-ai-abc-core-v1.json',
    'ES/Automation/Contracts/es-route-stage.registry.json',
    '.agents/skills/es-weapon-abc-part/scripts/New-ESAbcPartContract.ps1',
    '.agents/skills/es-weapon-abc-part/governance.json',
    $OutputPath.Replace('\', '/'),
    'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
)
$receiptSourceHashes = [ordered]@{}
foreach ($receiptSourceRef in $receiptSourceRefs) {
    $receiptSourceFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $receiptSourceRef -MustExist
    $receiptSourceHashes[$receiptSourceRef] = (Get-FileHash -LiteralPath $receiptSourceFull -Algorithm SHA256).Hash.ToLowerInvariant()
}
$result = [ordered]@{
    schemaVersion = 1
    evidenceContractId = 'es.skill-evidence-receipt'
    evidenceContractHash = $evidenceContractHash
    skillName = 'es-weapon-abc-part'
    case = 'ABCP-authoring'
    status = 'passed'
    evidenceLevel = 'S1'
    receiptPath = if ([string]::IsNullOrWhiteSpace($ReceiptPath)) { $null } else { $ReceiptPath.Replace('\', '/') }
    sourceRefs = @($receiptSourceRefs)
    sourceRefHashes = $receiptSourceHashes
    toolId = 'es-abc-part-authoring'
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    unityVersion = 'not-run'
    authorizationKind = 'read-only'
    executionEnabled = $false
    outputPath = $OutputPath.Replace('\', '/')
    writeStatus = $writeStatus
    coreRef = [string]$part.coreRef
    partId = [string]$part.partId
    claimsNotProven = @('Unity/Runtime behavior', 'Prefab import, firing, damage, performance, Player, IL2CPP or release acceptance')
}
if (-not [string]::IsNullOrWhiteSpace($ReceiptPath)) {
    $receiptFull = Resolve-ESAbcProjectPath -ProjectRoot $root -RelativePath $ReceiptPath
    Write-ESAbcJson -Path $receiptFull -Value $result
}
$result | ConvertTo-Json -Depth 12
