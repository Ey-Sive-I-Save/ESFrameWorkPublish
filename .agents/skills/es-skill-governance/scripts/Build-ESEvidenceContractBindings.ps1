[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string[]]$SkillName,
    [ValidateRange(1, 128)][int]$MaxSkills = 128,
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd('\', '/')
$skillsRoot = Join-Path $root '.agents/skills'
$contractRelative = 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$contractPath = Join-Path $root $contractRelative
$centralValidatorRelative = '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
$legacyGenericHash = '6200d8178982010bbdbae30a19b9de92f53ea0ca2fea47aa0ccefa3777fc0d94'
$legacyReceiptBeforeUtc = '2026-08-26T03:45:00Z'
$legacyReceiptAcceptanceEndsUtc = '2026-09-02T03:45:00Z'

function Get-Hash([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) {
    throw "Central Skill evidence contract is missing: $contractRelative"
}

$skillDirectories = @(Get-ChildItem -LiteralPath $skillsRoot -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.md') -PathType Leaf } |
    Sort-Object Name)
if ($SkillName) {
    $requested = @($SkillName | Sort-Object -Unique)
    $skillDirectories = @($skillDirectories | Where-Object { $requested -contains $_.Name })
    $missing = @($requested | Where-Object { @($skillDirectories.Name) -notcontains $_ })
    if ($missing.Count -gt 0) { throw "Unknown Skill name(s): $($missing -join ', ')" }
}
if ($skillDirectories.Count -eq 0) { throw 'No direct Skill roots were selected.' }
if ($skillDirectories.Count -gt $MaxSkills) { throw "Selected Skill count exceeds MaxSkills: $($skillDirectories.Count) > $MaxSkills" }

$contractHash = Get-Hash $contractPath
$results = [Collections.Generic.List[object]]::new()
foreach ($directory in $skillDirectories) {
    $localContractPath = Join-Path $directory.FullName 'references/evidence-receipt-contract.md'
    $entrypointPath = Join-Path $directory.FullName 'scripts/Test-ESSkillEvidence.ps1'
    if (-not (Test-Path -LiteralPath $entrypointPath -PathType Leaf)) { throw "Missing stable evidence entrypoint: $($directory.Name)" }

    $entrypointRaw = [IO.File]::ReadAllText($entrypointPath, [Text.UTF8Encoding]::new($false, $true))
    if ($entrypointRaw -notmatch 'Test-ESStrictEvidenceReceipt\.ps1') {
        throw "Stable entrypoint does not delegate to the central validator: $($directory.Name)"
    }

    $hasLocalContract = Test-Path -LiteralPath $localContractPath -PathType Leaf
    $localHash = if ($hasLocalContract) { Get-Hash $localContractPath } else { '' }
    $binding = [ordered]@{
        schemaVersion = 1
        bindingId = "es.skill-evidence-binding.$($directory.Name).v1"
        skillName = $directory.Name
        contract = [ordered]@{
            id = 'es.skill-evidence-receipt'
            version = '1'
            path = $contractRelative
            hash = $contractHash
        }
        localContract = [ordered]@{
            path = if ($hasLocalContract) { 'references/evidence-receipt-contract.md' } else { '' }
            hash = $localHash
            mode = if (-not $hasLocalContract) { 'central-authoritative' } elseif ($localHash -eq $legacyGenericHash) { 'compatibility-copy' } else { 'additive-extension' }
        }
        stableEntrypoint = [ordered]@{
            path = 'scripts/Test-ESSkillEvidence.ps1'
            hash = Get-Hash $entrypointPath
            mode = 'central-delegate'
            centralValidatorPath = $centralValidatorRelative
        }
        compatibility = [ordered]@{
            legacyReadable = $true
            legacyReceiptBeforeUtc = $legacyReceiptBeforeUtc
            legacyReceiptAcceptanceEndsUtc = $legacyReceiptAcceptanceEndsUtc
            newReceiptBinding = 'required'
            retirementState = 'not-authorized'
        }
    }
    $json = ($binding | ConvertTo-Json -Depth 8) + [Environment]::NewLine
    $bindingPath = Join-Path $directory.FullName 'evidence-contract.binding.json'
    $current = if (Test-Path -LiteralPath $bindingPath -PathType Leaf) {
        [IO.File]::ReadAllText($bindingPath, [Text.UTF8Encoding]::new($false, $true))
    } else { $null }
    $state = if ($current -ceq $json) { 'current' } elseif ($null -eq $current) { 'missing' } else { 'stale' }
    if ($Apply -and $state -ne 'current') {
        [IO.File]::WriteAllText($bindingPath, $json, [Text.UTF8Encoding]::new($false))
        $state = 'written'
    }
    [void]$results.Add([pscustomobject]@{
        skillName = $directory.Name
        bindingPath = ".agents/skills/$($directory.Name)/evidence-contract.binding.json"
        localContractMode = $binding.localContract.mode
        state = $state
    })
}

[pscustomobject]@{
    schemaVersion = 1
    generator = 'es-skill-evidence-binding-builder'
    apply = [bool]$Apply
    contractPath = $contractRelative
    contractHash = $contractHash
    selectedCount = $skillDirectories.Count
    currentCount = @($results | Where-Object state -eq 'current').Count
    writtenCount = @($results | Where-Object state -eq 'written').Count
    pendingCount = @($results | Where-Object state -in @('missing', 'stale')).Count
    results = @($results)
} | ConvertTo-Json -Depth 8
