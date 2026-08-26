[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$SkillPath,
    [ValidateRange(1, 128)][int]$MaxSkills = 128,
    [switch]$IncludeNegativeCases,
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd('\', '/')
$rootPrefix = $root + [IO.Path]::DirectorySeparatorChar
$contractRelative = 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$bindingSchemaRelative = 'ES/Automation/Contracts/es-skill-evidence-binding-v1.schema.json'
$centralValidatorRelative = '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
$contractPath = Join-Path $root $contractRelative
$bindingSchemaPath = Join-Path $root $bindingSchemaRelative
$schemaModulePath = Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1'
$legacyGenericHash = '6200d8178982010bbdbae30a19b9de92f53ea0ca2fea47aa0ccefa3777fc0d94'
$legacyReceiptBeforeUtc = '2026-08-26T03:45:00Z'
$legacyReceiptAcceptanceEndsUtc = '2026-09-02T03:45:00Z'

Import-Module $schemaModulePath -Force

function Get-Hash([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Resolve-ProjectFile([string]$RelativePath, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '^[a-zA-Z]:' -or $RelativePath -match '^[\\/]{2}') {
        throw "$Label must be a project-relative path."
    }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $RelativePath))
    if (-not $full.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "$Label must resolve to an existing file inside ProjectRoot: $RelativePath"
    }
    $full
}

foreach ($requiredPath in @($contractPath, $bindingSchemaPath, $schemaModulePath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { throw "Missing central Evidence contract resource: $requiredPath" }
}
$unsupported = @(Test-ESJsonSchemaSupported -SchemaPath $bindingSchemaPath)
if ($unsupported.Count -gt 0) { throw "Binding schema uses unsupported keywords: $($unsupported -join '; ')" }
$unsupportedReceipt = @(Test-ESJsonSchemaSupported -SchemaPath $contractPath)
if ($unsupportedReceipt.Count -gt 0) { throw "Receipt schema uses unsupported keywords: $($unsupportedReceipt -join '; ')" }

$contractHash = Get-Hash $contractPath
$skillsRoot = Join-Path $root '.agents/skills'
$directories = if ($SkillPath) {
    @((Get-Item -LiteralPath (Resolve-Path -LiteralPath $SkillPath -ErrorAction Stop).Path -ErrorAction Stop))
} else {
    @(Get-ChildItem -LiteralPath $skillsRoot -Directory |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.md') -PathType Leaf } |
        Sort-Object Name)
}
$directories = @($directories)
if ($directories.Count -eq 0) { throw 'No direct Skill roots were selected.' }
if ($directories.Count -gt $MaxSkills) { throw "Selected Skill count exceeds MaxSkills: $($directories.Count) > $MaxSkills" }

function Test-Binding([IO.DirectoryInfo]$Directory, [object]$Binding) {
    $errors = [Collections.Generic.List[string]]::new()
    foreach ($schemaError in @(Test-ESJsonSchemaValue -SchemaPath $bindingSchemaPath -Value $Binding)) {
        [void]$errors.Add("schema: $schemaError")
    }
    if ($errors.Count -gt 0) { return @($errors) }

    if ([string]$Binding.skillName -cne $Directory.Name) { [void]$errors.Add('skillName does not match the direct Skill root.') }
    if ([string]$Binding.bindingId -cne "es.skill-evidence-binding.$($Directory.Name).v1") { [void]$errors.Add('bindingId is not the stable Skill binding identity.') }
    if ([string]$Binding.contract.path -cne $contractRelative) { [void]$errors.Add('contract.path does not reference the central contract.') }
    if ([string]$Binding.contract.hash -cne $contractHash) { [void]$errors.Add('central Evidence contract hash is stale.') }
    if ([string]$Binding.stableEntrypoint.centralValidatorPath -cne $centralValidatorRelative) { [void]$errors.Add('central validator path is not authoritative.') }
    if ([string]$Binding.compatibility.legacyReceiptBeforeUtc -cne $legacyReceiptBeforeUtc -or
        [string]$Binding.compatibility.legacyReceiptAcceptanceEndsUtc -cne $legacyReceiptAcceptanceEndsUtc) {
        [void]$errors.Add('legacy receipt compatibility window is not the registered bounded window.')
    }

    try {
        if ([string]$Binding.localContract.mode -ceq 'central-authoritative') {
            if ([string]$Binding.localContract.path -cne '' -or [string]$Binding.localContract.hash -cne '') {
                [void]$errors.Add('central-authoritative bindings cannot declare a local contract path or hash.')
            }
        } else {
        $localPath = [IO.Path]::GetFullPath([IO.Path]::Combine($Directory.FullName, [string]$Binding.localContract.path))
        $skillPrefix = $Directory.FullName.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        if (-not $localPath.StartsWith($skillPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $localPath -PathType Leaf)) {
            [void]$errors.Add('localContract.path escapes the Skill root or is missing.')
        } elseif ([string]$Binding.localContract.hash -cne (Get-Hash $localPath)) {
            [void]$errors.Add('local compatibility/extension contract hash is stale.')
        } elseif ([string]$Binding.localContract.mode -ceq 'compatibility-copy' -and [string]$Binding.localContract.hash -cne $legacyGenericHash) {
            [void]$errors.Add('compatibility-copy is reserved for the exact registered legacy generic contract.')
        }
        }
    } catch { [void]$errors.Add("localContract.path is invalid: $($_.Exception.Message)") }

    try {
        $entrypointPath = [IO.Path]::GetFullPath([IO.Path]::Combine($Directory.FullName, [string]$Binding.stableEntrypoint.path))
        $skillPrefix = $Directory.FullName.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        if (-not $entrypointPath.StartsWith($skillPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $entrypointPath -PathType Leaf)) {
            [void]$errors.Add('stableEntrypoint.path escapes the Skill root or is missing.')
        } elseif ([string]$Binding.stableEntrypoint.hash -cne (Get-Hash $entrypointPath)) {
            [void]$errors.Add('stable Evidence entrypoint hash is stale.')
        } else {
            $entrypointRaw = [IO.File]::ReadAllText($entrypointPath, [Text.UTF8Encoding]::new($false, $true))
            if ($entrypointRaw -notmatch 'Test-ESStrictEvidenceReceipt\.ps1') {
                [void]$errors.Add('stable Evidence entrypoint does not delegate to the central validator.')
            }
        }
    } catch { [void]$errors.Add("stableEntrypoint.path is invalid: $($_.Exception.Message)") }
    @($errors)
}

$results = [Collections.Generic.List[object]]::new()
foreach ($directory in $directories) {
    $bindingPath = Join-Path $directory.FullName 'evidence-contract.binding.json'
    $errors = @()
    if (-not (Test-Path -LiteralPath $bindingPath -PathType Leaf)) {
        $errors = @('binding file is missing.')
    } else {
        try {
            $bindingRaw = [IO.File]::ReadAllText($bindingPath, [Text.UTF8Encoding]::new($false, $true))
            $binding = $bindingRaw | ConvertFrom-Json -ErrorAction Stop
            $errors = @(Test-Binding $directory $binding)
        } catch { $errors = @($_.Exception.Message) }
    }
    [void]$results.Add([pscustomobject]@{
        skillName = $directory.Name
        status = if ($errors.Count -eq 0) { 'passed' } else { 'failed' }
        errors = $errors
    })
}

$negativeCases = [Collections.Generic.List[object]]::new()
if ($IncludeNegativeCases -and $results.Count -gt 0) {
    $directory = $directories[0]
    $bindingPath = Join-Path $directory.FullName 'evidence-contract.binding.json'
    $baseRaw = [IO.File]::ReadAllText($bindingPath, [Text.UTF8Encoding]::new($false, $true))
    $mutations = @(
        @{ id = 'forged-central-contract-hash'; apply = { param($x) $x.contract.hash = ('0' * 64) } },
        @{ id = 'skill-identity-mismatch'; apply = { param($x) $x.skillName = 'es-wrong-skill' } },
        @{ id = 'stale-local-contract-hash'; apply = { param($x) $x.localContract.hash = ('1' * 64) } },
        @{ id = 'stale-entrypoint-hash'; apply = { param($x) $x.stableEntrypoint.hash = ('2' * 64) } },
        @{ id = 'scope-expanding-local-path'; apply = { param($x) $x.localContract.mode = 'additive-extension'; $x.localContract.path = '../outside.md' } }
    )
    foreach ($mutation in $mutations) {
        $candidate = $baseRaw | ConvertFrom-Json
        & $mutation.apply $candidate
        $rejected = @(Test-Binding $directory $candidate).Count -gt 0
        [void]$negativeCases.Add([pscustomobject]@{ id = $mutation.id; status = if ($rejected) { 'passed' } else { 'failed' } })
    }
}

$failed = @($results | Where-Object status -ne 'passed')
$failedNegative = @($negativeCases | Where-Object status -ne 'passed')
$summary = [pscustomobject]@{
    schemaVersion = 1
    validator = 'es-skill-evidence-contract-bindings'
    status = if ($failed.Count -eq 0 -and $failedNegative.Count -eq 0) { 'passed' } else { 'failed' }
    contractPath = $contractRelative
    contractHash = $contractHash
    skillCount = $directories.Count
    passedCount = $directories.Count - $failed.Count
    failedCount = $failed.Count
    negativeCases = @($negativeCases)
    results = @($results)
}
if (-not $Quiet) { $summary | ConvertTo-Json -Depth 8 }
if ($summary.status -ne 'passed') { exit 1 }
