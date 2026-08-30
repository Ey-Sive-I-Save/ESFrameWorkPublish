[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,

    [string]$OutputRoot = '',

    [string]$MappingPath = '',
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$SourceToken = '',
    [string[]]$SourceTextTokens = @(),
    [string]$SourceRevision = '',
    [ValidateRange(1, 1000000)][int]$MaxFiles = 10000,
    [ValidateRange(1, 2147483647)][long]$MaxBytes = 536870912,
    [switch]$DryRun,
    [switch]$RenamePathSegments,
    [switch]$CopyToOutput,

    [string]$DeveloperName = '',
    [string[]]$LegacyDeveloperTokens = @()
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Resolve-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd('\')
}

function Get-FullPathForCreate([string]$Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Test-PathWithin([string]$Child, [string]$Parent) {
    $childFull = ([IO.Path]::GetFullPath($Child)).TrimEnd('\')
    $parentFull = ([IO.Path]::GetFullPath($Parent)).TrimEnd('\')
    return $childFull.Equals($parentFull, [StringComparison]::OrdinalIgnoreCase) -or
        $childFull.StartsWith($parentFull + '\', [StringComparison]::OrdinalIgnoreCase)
}

$sourceFull = Resolve-FullPath $SourceRoot
$projectFull = Resolve-FullPath $ProjectRoot
if ($CopyToOutput -and [string]::IsNullOrWhiteSpace($OutputRoot)) { throw 'CopyToOutput requires an explicit OutputRoot.' }
$inPlace = -not $CopyToOutput -and [string]::IsNullOrWhiteSpace($OutputRoot)
$outputFull = if ($inPlace) { $sourceFull } else { Get-FullPathForCreate $OutputRoot }
$temporaryMapPath = $null
if ([string]::IsNullOrWhiteSpace($MappingPath)) {
    if ($inPlace -and $DryRun) {
        $temporaryMapPath = Join-Path ([IO.Path]::GetTempPath()) ('es-dry-run-' + [Guid]::NewGuid().ToString('N') + '.json')
        $MappingPath = $temporaryMapPath
    } elseif ($inPlace) { $MappingPath = Join-Path $sourceFull '.es-migration\es-symbol-map.json' }
    else {
        $outputName = [IO.Path]::GetFileName($outputFull)
        if ([string]::IsNullOrWhiteSpace($outputName)) { throw 'OutputRoot must have a final directory name.' }
        $MappingPath = Join-Path (Split-Path -Parent $outputFull) ($outputName + '.es-symbol-map.json')
    }
}
$mapFull = Get-FullPathForCreate $MappingPath

# Preflight every path before the generator can write its external map.  This
# prevents an invalid output path from causing a partial project-side artifact.
if (Test-PathWithin $sourceFull $projectFull) { throw "SourceRoot must be outside the protected project root: $projectFull" }
if (-not $inPlace -and (Test-PathWithin $outputFull $projectFull)) { throw "OutputRoot must be outside the protected project root: $projectFull" }
if (-not $inPlace -and (Test-PathWithin $outputFull $sourceFull)) { throw "OutputRoot must not be inside SourceRoot: $sourceFull" }
if (Test-PathWithin $mapFull $projectFull) { throw "MappingPath must be outside the protected project root: $projectFull" }
if ((Test-PathWithin $mapFull $sourceFull) -and -not (Test-PathWithin $mapFull (Join-Path $sourceFull '.es-migration'))) { throw "MappingPath inside SourceRoot is only allowed below .es-migration: $sourceFull" }

$generator = Join-Path $PSScriptRoot 'New-ESTransparentSymbolMap.ps1'
$remapper = Join-Path $PSScriptRoot 'Invoke-ESTransparentNamespaceRemap.ps1'
$generatorParams = @{
    SourceRoot = $sourceFull
    OutputMapPath = $mapFull
    ProjectRoot = $projectFull
    MaxFiles = $MaxFiles
    MaxBytes = $MaxBytes
}
if (-not [string]::IsNullOrWhiteSpace($SourceToken)) { $generatorParams.SourceToken = $SourceToken }
if ($SourceTextTokens.Count -gt 0) { $generatorParams.SourceTextTokens = $SourceTextTokens }
if (-not [string]::IsNullOrWhiteSpace($SourceRevision)) { $generatorParams.SourceRevision = $SourceRevision }
$mapReceipt = & $generator @generatorParams | ConvertFrom-Json
$map = Get-Content -LiteralPath $mapFull -Raw -Encoding UTF8 | ConvertFrom-Json
$effectiveRevision = if (-not [string]::IsNullOrWhiteSpace($SourceRevision)) { $SourceRevision } else { [string]$map.source.revision }

$remapperParams = @{
    SourceRoot = $sourceFull
    OutputRoot = $outputFull
    MappingPath = $mapFull
    ProjectRoot = $projectFull
    SourceRevision = $effectiveRevision
    MaxFiles = $MaxFiles
    MaxBytes = $MaxBytes
    DeveloperName = $DeveloperName
    LegacyDeveloperTokens = $LegacyDeveloperTokens
}
if ($RenamePathSegments) { $remapperParams.RenamePathSegments = $true }
if ($inPlace) { $remapperParams.InPlace = $true; $remapperParams.WholeRepository = $true }
if ($CopyToOutput) { $remapperParams.WholeRepository = $false }
if ($DryRun) { $remapperParams.DryRun = $true }
$remapResult = & $remapper @remapperParams | ConvertFrom-Json
$remapReceipt = if ($DryRun) { $remapResult.receipt } else { $remapResult }
$remapManifest = if ($DryRun) { $remapResult.manifest } else {
    $manifestPath = if ($inPlace) { Join-Path $sourceFull '.es-migration\es-remap-manifest.json' } else { Join-Path $outputFull 'es-remap-manifest.json' }
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Remap manifest is missing after publish: $manifestPath" }
    Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
$status = if ($DryRun) { 'not-run' } elseif ([string]$remapReceipt.status -eq 'passed') { 'passed' } else { 'failed' }
$summaryNonClaims = [Collections.Generic.List[string]]::new()
if (-not $inPlace) { [void]$summaryNonClaims.Add('No target project files were written.') }
[void]$summaryNonClaims.Add('No license clearance is granted.')
[void]$summaryNonClaims.Add('Lexical remap is not AST, compiler, or semantic-equivalence proof.')
[void]$summaryNonClaims.Add('No Unity or Runtime compatibility is proven.')
[void]$summaryNonClaims.Add('Git history and LICENSE/NOTICE are not rewritten by default.')

$summary = [ordered]@{
    schemaVersion = 1
    skillName = 'es-open-source-migration'
    case = 'automatic-transparent-namespace-remap'
    status = $status
    evidenceLevel = 'static'
    sourceRootName = [IO.Path]::GetFileName($sourceFull)
    sourceLocatorPolicy = 'external explicit source root; mutable absolute source path is not persisted in receipts'
    outputRoot = $outputFull
    mode = if ($inPlace) { 'in-place-whole-repository' } else { 'external-output' }
    mappingPath = $mapFull
    sourceRevision = $effectiveRevision
    sourceTreeSha256 = [string]$map.source.treeSha256
    mapPlanHash = [string]$mapReceipt.planHash
    remapPlanHash = [string]$remapReceipt.planHash
    developerName = [string]$remapReceipt.developerName
    identityHardeningApplied = [bool]$remapReceipt.identityHardeningApplied
    mapSymbolCount = @($map.symbols).Count
    mapReceipt = $mapReceipt
    remap = [ordered]@{
        supportedFileCount = $remapManifest.source.supportedFileCount
        changedFileCount = $remapManifest.counts.changedFiles
        excludedLicensedTreeFileCount = $remapManifest.counts.excludedLicensedTreeFiles
        excludedTreeReferenceFileCount = $remapManifest.boundaryFindings.excludedTreeReferenceFileCount
        receipt = $remapReceipt
    }
    nonClaims = @($summaryNonClaims)
}
$summary | ConvertTo-Json -Depth 12
if ($temporaryMapPath) {
    $temporaryReceipt = [IO.Path]::ChangeExtension($temporaryMapPath, '.receipt.json')
    if (Test-Path -LiteralPath $temporaryMapPath) { Remove-Item -LiteralPath $temporaryMapPath -Force }
    if (Test-Path -LiteralPath $temporaryReceipt) { Remove-Item -LiteralPath $temporaryReceipt -Force }
}
if ($status -eq 'failed') { exit 1 }
