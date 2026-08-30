[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$utf8 = New-Object Text.UTF8Encoding($false, $true)
$issues = [System.Collections.Generic.List[string]]::new()
$checks = [System.Collections.Generic.List[object]]::new()

function Resolve-ProjectFile([string]$relativePath) {
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $relativePath.Replace('/', '\')))
    $prefix = $root.TrimEnd([char]92, [char]47) + [char]92
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
        $full -ne $root) { throw "Path escapes ProjectRoot: $relativePath" }
    return $full
}

function Relative-ProjectPath([string]$fullPath) {
    return $fullPath.Substring($root.Length + 1).Replace('\', '/')
}

function Read-Strict([string]$relativePath) {
    $full = Resolve-ProjectFile $relativePath
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "Missing required file: $relativePath"
    }
    return [IO.File]::ReadAllText($full, $utf8)
}

function Add-Check([string]$id, [bool]$passed, [string]$detail) {
    $checks.Add([pscustomobject][ordered]@{
        id = $id
        status = if ($passed) { 'passed' } else { 'blocked' }
        detail = $detail
    })
    if (-not $passed) { $issues.Add("${id}: $detail") }
}

$authority = $null
$authorityRelative = 'ES/AISpace/AISPACE_AUTHORITY.json'
try {
    $authority = Read-Strict $authorityRelative | ConvertFrom-Json
} catch {
    $issues.Add("authority-identity: $($_.Exception.Message)")
}

$canonicalRelative = 'ES/AISpace/README.md'
$canonicalText = ''
try { $canonicalText = Read-Strict $canonicalRelative } catch { $issues.Add("canonical-body: $($_.Exception.Message)") }

if ($null -ne $authority) {
    $authorityIdentityPassed = ([int]$authority.schemaVersion -eq 1 -and [string]$authority.authorityId -eq 'es-aispace-root.v1' -and [string]$authority.concept -eq 'AISpace' -and [string]$authority.status -eq 'active' -and [string]$authority.canonicalEntry -eq $canonicalRelative -and [string]$authority.canonicalBody -eq $canonicalRelative)
    Add-Check 'authority-identity' $authorityIdentityPassed 'one active identity points to the canonical body'

    $workflow = $authority.workflowAuthority
    $contract = $null
    try { $contract = Read-Strict 'ES/Automation/Contracts/es-userspace-profile-v1.json' | ConvertFrom-Json } catch { }
    $workflowPassed = ($null -ne $workflow -and [string]$workflow.skillName -eq 'es-ai-space-organization' -and
        [string]$workflow.skillPath -eq '.agents/skills/es-ai-space-organization/SKILL.md' -and
        [string]$workflow.registrationContract -eq 'ES/Automation/Contracts/es-userspace-profile-v1.json' -and
        [string]$workflow.commandId -eq 'userspace.profile.manage' -and
        [string]$workflow.documentationRole -eq 'pointer-only' -and $null -ne $contract -and
        [string]$contract.skillName -eq [string]$workflow.skillName -and
        [string]$contract.workflowAuthority -eq [string]$workflow.skillPath -and
        [string]$contract.documentationRole -eq 'pointer-only')
    Add-Check 'skill-workflow-authority' $workflowPassed 'Skill is the machine-checked workflow authority; contract and documentation are subordinate'

    $requiredDiscovery = @($authority.discoveryEntrypoints | ForEach-Object { [string]$_ })
    $discoveryPassed = $true
    foreach ($relative in $requiredDiscovery) {
        try {
            $text = Read-Strict $relative
            if ($text -notmatch [regex]::Escape($canonicalRelative)) {
                $discoveryPassed = $false
                $issues.Add("discovery-closure: $relative does not point to $canonicalRelative")
            }
        } catch {
            $discoveryPassed = $false
            $issues.Add("discovery-closure: $($_.Exception.Message)")
        }
    }
    Add-Check 'discovery-closure' $discoveryPassed 'all declared discovery entrypoints point to the canonical body'

    $bodyPolicy = $authority.bodyPolicy
    $bodyPolicyPassed = ($null -ne $bodyPolicy -and [string]$bodyPolicy.mode -eq 'single-canonical-body' -and [string]$bodyPolicy.canonicalBody -eq $canonicalRelative)
    Add-Check 'non-redundant-body' $bodyPolicyPassed 'body policy names one canonical body'

    $competitionPolicy = $authority.competitionPolicy
    $competitionPassed = ($null -ne $competitionPolicy -and [bool]$competitionPolicy.runtimeLease -eq $false -and [bool]$competitionPolicy.lastWriteWins -eq $false -and -not [string]::IsNullOrWhiteSpace([string]$competitionPolicy.staleWriteAction))
    Add-Check 'no-runtime-competition' $competitionPassed 'runtime lease and last-write-wins are disabled'

    $unityExit = @($authority.nonCompetingRoots | Where-Object { [string]$_.path -eq 'Assets/ES/AISpace/Public' })
    $unityPassed = ($unityExit.Count -eq 1 -and [bool]$unityExit[0].mayDeclareAISpaceRoot -eq $false -and [string]$unityExit[0].role -eq 'unity-import-exit')
    Add-Check 'no-competing-root' $unityPassed 'Unity path is declared as an import exit only'
}

$headingMatches = @()
if ($canonicalText) {
    $markdownRoot = Resolve-ProjectFile 'ES/AISpace'
    $headingMatches = @(Get-ChildItem -LiteralPath $markdownRoot -Recurse -File -Filter '*.md' |
        ForEach-Object {
            $text = [IO.File]::ReadAllText($_.FullName, $utf8)
            if ($text -match '(?m)^# ES AI Space\s*$') { $_ }
        })
}
$singleHeading = ($headingMatches.Count -eq 1 -and (Relative-ProjectPath $headingMatches[0].FullName) -eq $canonicalRelative)
Add-Check 'single-root-heading' $singleHeading 'only the canonical body declares the AISpace root heading'

try {
    $tempReadme = Read-Strict 'ES/AISpace/Public/TempReadme.md'
    $tempPointer = ($tempReadme -match [regex]::Escape($canonicalRelative) -and $tempReadme -match 'AISpace' -and $tempReadme -match 'Public')
    Add-Check 'temp-pointer-only' $tempPointer 'TempReadme is a pointer and does not carry the old body'
} catch { Add-Check 'temp-pointer-only' $false $_.Exception.Message }

try {
    $assetPointer = Read-Strict 'Assets/ES/AISpace/Public/README.md'
    $assetPointerValid = $false
    if ($assetPointer -match [regex]::Escape($canonicalRelative) -and $assetPointer -match 'Unity') {
        $assetPointerValid = $true
    }
    Add-Check 'unity-exit-pointer' $assetPointerValid 'Unity public README points back to AISpace and states its exit role'
} catch { Add-Check 'unity-exit-pointer' $false $_.Exception.Message }

try {
    $skillRegistry = Read-Strict 'ES/AISpace/Public/Skills/registry.json' | ConvertFrom-Json
    $registryValid = ([string]$skillRegistry.authority -eq 'derived-navigation' -and [string]$skillRegistry.outputPath -eq 'ES/AISpace/Public/Skills/registry.json' -and [string]$skillRegistry.purpose -match 'never grants execution permission')
    Add-Check 'derived-registry-only' $registryValid 'Skill registry remains a derived navigation projection'
} catch { Add-Check 'derived-registry-only' $false $_.Exception.Message }

$duplicateIdentity = @()
$aispaceRoot = Resolve-ProjectFile 'ES/AISpace'
foreach ($jsonFile in @(Get-ChildItem -LiteralPath $aispaceRoot -Recurse -File -Filter '*.json')) {
    $text = [IO.File]::ReadAllText($jsonFile.FullName, $utf8)
    if ($text -match '"authorityId"\s*:\s*"es-aispace-root\.v1"') {
        $duplicateIdentity += (Relative-ProjectPath $jsonFile.FullName)
    }
}
$identityOnly = ($duplicateIdentity.Count -eq 1 -and $duplicateIdentity[0] -eq $authorityRelative)
Add-Check 'unique-machine-identity' $identityOnly 'the root authority identity is declared only once'

$status = if ($issues.Count -eq 0) { 'passed' } else { 'failed' }
[ordered]@{
    schemaVersion = 1
    validator = 'es-aispace-authority'
    status = $status
    authorityPath = $authorityRelative
    canonicalBody = $canonicalRelative
    checks = @($checks)
    issues = @($issues)
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Unity import behavior', 'runtime locking', 'multi-process coordination', 'release behavior')
} | ConvertTo-Json -Depth 8

if ($status -ne 'passed') { exit 1 }
