[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$OutputPath = 'ES/Output/Governance/static-acceptance-coverage.json',
    # Deterministic regression hook for the outer fail-closed path.
    [Parameter(DontShow = $true)] [string]$FaultInjection = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ESPathBoundary.Common.ps1')
try {
    if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
        throw 'ProjectRoot must identify an existing directory.'
    }
    $root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw 'ProjectRoot must identify an existing directory.'
    }
    if ($FaultInjection -notin @('', 'after-sentinel')) {
        throw "Unknown FaultInjection value: $FaultInjection"
    }
    $outputTarget = Resolve-ESContainedRelativePath -Candidate $OutputPath -ContainerRoot $root -Label 'OutputPath'
    $skillsTarget = Resolve-ESContainedRelativePath -Candidate '.agents/skills' -ContainerRoot $root -Label 'SkillsRoot'
    if (-not (Test-Path -LiteralPath $skillsTarget.FullPath -PathType Container)) {
        throw 'ProjectRoot must contain .agents/skills.'
    }
} catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 2
}
$skillsRoot = $skillsTarget.FullPath
$invocationId = [Guid]::NewGuid().ToString('N')
$startedUtc = [DateTimeOffset]::UtcNow.ToString('o')
$claimsNotProven = @('responsibility-specific runtime behavior', 'Unity/editor/process behavior', 'performance and visual behavior')
$utf8NoBom = New-Object Text.UTF8Encoding($false)
$results = @()

function Write-ESStaticAcceptanceReport($Report) {
    $currentTarget = Resolve-ESContainedRelativePath -Candidate $outputTarget.RelativePath -ContainerRoot $root -Label 'OutputPath'
    $output = $currentTarget.FullPath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
    $output = (Resolve-ESContainedRelativePath -Candidate $currentTarget.RelativePath -ContainerRoot $root -Label 'OutputPath').FullPath
    $temporary = "$output.tmp-$([Guid]::NewGuid().ToString('N'))"
    try {
        [IO.File]::WriteAllText($temporary, ($Report | ConvertTo-Json -Depth 12), $utf8NoBom)
        Move-Item -LiteralPath $temporary -Destination $output -Force
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Resolve-ESProjectContainedSkillReference([string]$Candidate, [string]$SkillRoot, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Candidate) -or $Candidate -ne $Candidate.Trim()) {
        throw "$Label must be a non-empty relative path without surrounding whitespace."
    }
    if ([IO.Path]::IsPathRooted($Candidate) -or $Candidate -match '^[a-zA-Z]:' -or $Candidate -match '^[\\/]{2}' -or $Candidate.Contains(':')) {
        throw "$Label must be a relative path without an alternate data stream."
    }

    $combined = [IO.Path]::GetFullPath([IO.Path]::Combine($SkillRoot, $Candidate.Replace('/', '\')))
    $rootPrefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $combined.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label escapes ProjectRoot."
    }
    $projectRelative = $combined.Substring($rootPrefix.Length)
    return Resolve-ESContainedRelativePath -Candidate $projectRelative -ContainerRoot $root -Label $Label
}

$sentinel = [ordered]@{
    schemaVersion = 1
    toolId = 'es-static-acceptance-coverage'
    invocationId = $invocationId
    startedUtc = $startedUtc
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    profile = 'StaticReview'
    mutatesSources = $false
    startsRuntime = $false
    status = 'blocked'
    phase = 'in-progress'
    skillCount = 0
    coveredSkillCount = 0
    blockedSkillCount = 0
    results = @()
    validatorFindings = @('validation has not completed')
    claimsNotProven = $claimsNotProven
}
try {
    Write-ESStaticAcceptanceReport -Report $sentinel
} catch {
    Write-Error "Could not replace the previous static acceptance report with the current invocation sentinel: $($_.Exception.Message)" -ErrorAction Continue
    exit 1
}

try {
if ($FaultInjection -eq 'after-sentinel') { throw 'Injected failure after current-invocation sentinel.' }
foreach ($directory in @(Get-ChildItem -LiteralPath $skillsRoot -Directory | Sort-Object Name)) {
    try {
        $skillTarget = Resolve-ESContainedRelativePath -Candidate $directory.Name -ContainerRoot $skillsRoot -Label "Skill root '$($directory.Name)'"
        $skillPath = $skillTarget.FullPath
    } catch {
        $results += [pscustomobject][ordered]@{
            skill = $directory.Name
            status = 'blocked'
            profile = ''
            acceptanceId = ''
            specializedCaseCount = 0
            evidenceArtifactCount = 0
            findings = @($_.Exception.Message)
        }
        continue
    }
    $manifestPath = Join-Path $skillPath 'static-replay.manifest.json'
    $skillMdPath = Join-Path $skillPath 'SKILL.md'
    $result = [ordered]@{
        skill = $directory.Name
        status = 'passed'
        profile = ''
        acceptanceId = ''
        specializedCaseCount = 0
        evidenceArtifactCount = 0
        findings = @()
    }
    if (-not (Test-Path -LiteralPath $skillMdPath -PathType Leaf)) { $result.status = 'blocked'; $result.findings += 'missing SKILL.md' }
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { $result.status = 'blocked'; $result.findings += 'missing static-replay.manifest.json'; $results += [pscustomobject]$result; continue }
    try { $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { $result.status = 'blocked'; $result.findings += 'invalid static-replay.manifest.json'; $results += [pscustomobject]$result; continue }
    if ($null -eq $manifest -or $manifest -isnot [pscustomobject]) {
        $result.status = 'blocked'
        $result.findings += 'invalid static-replay.manifest.json'
        $results += [pscustomobject]$result
        continue
    }

    $profileProperty = $manifest.PSObject.Properties['responsibilityProfile']
    if ($null -ne $profileProperty) { $result.profile = [string]$profileProperty.Value }
    if ([string]::IsNullOrWhiteSpace($result.profile)) { $result.status = 'blocked'; $result.findings += 'missing responsibilityProfile' }

    $runtimeEscalationProperty = $manifest.PSObject.Properties['runtimeEscalation']
    if ($null -eq $runtimeEscalationProperty -or $null -eq $runtimeEscalationProperty.Value) {
        $result.status = 'blocked'
        $result.findings += 'missing runtimeEscalation'
    } else {
        $reasonProperty = $runtimeEscalationProperty.Value.PSObject.Properties['reason']
        $reason = if ($null -eq $reasonProperty) { '' } else { [string]$reasonProperty.Value }
        if ($reason -notmatch '\S') { $result.status = 'blocked'; $result.findings += 'runtimeEscalation.reason is empty' }
    }

    $runtimeClaims = @()
    $runtimeClaimsProperty = $manifest.PSObject.Properties['runtimeClaimsNotProven']
    if ($null -ne $runtimeClaimsProperty) {
        $runtimeClaims = @(@($runtimeClaimsProperty.Value) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    }
    if ($runtimeClaims.Count -eq 0) { $result.status = 'blocked'; $result.findings += 'runtimeClaimsNotProven must not be empty' }

    $cases = @()
    $artifacts = @()
    $rawArtifacts = @()
    $specializedProperty = $manifest.PSObject.Properties['specializedAcceptance']
    if ($null -eq $specializedProperty -or $null -eq $specializedProperty.Value) {
        $result.status = 'blocked'
        $result.findings += 'missing specializedAcceptance'
    }
    else {
        $specialized = $specializedProperty.Value
        $acceptanceIdProperty = $specialized.PSObject.Properties['id']
        if ($null -ne $acceptanceIdProperty) { $result.acceptanceId = [string]$acceptanceIdProperty.Value }
        $casesProperty = $specialized.PSObject.Properties['requiredStaticCases']
        if ($null -ne $casesProperty) {
            $cases = @(@($casesProperty.Value) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
        }
        $artifactsProperty = $specialized.PSObject.Properties['evidenceArtifacts']
        if ($null -ne $artifactsProperty) {
            $rawArtifacts = @($artifactsProperty.Value)
            $artifacts = @($rawArtifacts | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
        }
        if ([string]::IsNullOrWhiteSpace($result.acceptanceId)) { $result.status = 'blocked'; $result.findings += 'missing specialized acceptance id' }
    }
    $result.specializedCaseCount = $cases.Count
    $result.evidenceArtifactCount = $artifacts.Count
    if ($cases.Count -eq 0) { $result.status = 'blocked'; $result.findings += 'no responsibility-specific static cases' }
    if ($artifacts.Count -eq 0) { $result.status = 'blocked'; $result.findings += 'no evidence artifacts' }

    $guidanceProperty = $manifest.PSObject.Properties['specializedGuidanceRef']
    $guidanceRef = if ($null -eq $guidanceProperty) { '' } else { [string]$guidanceProperty.Value }
    if ([string]::IsNullOrWhiteSpace($guidanceRef)) { $result.status = 'blocked'; $result.findings += 'missing specializedGuidanceRef' }
    else {
        try {
            $guidancePath = (Resolve-ESContainedRelativePath -Candidate $guidanceRef -ContainerRoot $skillPath -Label 'specializedGuidanceRef').FullPath
            if (-not (Test-Path -LiteralPath $guidancePath -PathType Leaf)) { $result.status = 'blocked'; $result.findings += "missing guidance: $guidanceRef" }
        } catch {
            $result.status = 'blocked'
            $result.findings += "invalid specializedGuidanceRef: $guidanceRef"
        }
    }
    foreach ($artifact in $rawArtifacts) {
        $artifactRef = [string]$artifact
        if ([string]::IsNullOrWhiteSpace($artifactRef)) {
            $result.status = 'blocked'
            $result.findings += 'empty evidence artifact reference'
            continue
        }
        try {
            $artifactPath = (Resolve-ESProjectContainedSkillReference -Candidate $artifactRef -SkillRoot $skillPath -Label 'evidence artifact').FullPath
            if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) { $result.status = 'blocked'; $result.findings += "missing evidence artifact: $artifactRef" }
        } catch {
            $result.status = 'blocked'
            $result.findings += "invalid evidence artifact path: $artifactRef"
        }
    }
    $results += [pscustomobject]$result
}
$blocked = @($results | Where-Object status -eq 'blocked').Count
$report = [ordered]@{
    schemaVersion = 1
    toolId = 'es-static-acceptance-coverage'
    invocationId = $invocationId
    startedUtc = $startedUtc
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    profile = 'StaticReview'
    mutatesSources = $false
    startsRuntime = $false
    status = if ($blocked -eq 0) { 'passed' } else { 'blocked' }
    phase = 'completed'
    skillCount = $results.Count
    coveredSkillCount = @($results | Where-Object status -eq 'passed').Count
    blockedSkillCount = $blocked
    results = $results
    validatorFindings = @()
    claimsNotProven = $claimsNotProven
}
Write-ESStaticAcceptanceReport -Report $report
$report | ConvertTo-Json -Depth 12
if ($blocked -gt 0) { exit 1 }
exit 0
} catch {
    $failureMessage = $_.Exception.Message
    $failureBlocked = @($results | Where-Object status -eq 'blocked').Count
    $failureReport = [ordered]@{
        schemaVersion = 1
        toolId = 'es-static-acceptance-coverage'
        invocationId = $invocationId
        startedUtc = $startedUtc
        generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        profile = 'StaticReview'
        mutatesSources = $false
        startsRuntime = $false
        status = 'blocked'
        phase = 'failed'
        skillCount = $results.Count
        coveredSkillCount = @($results | Where-Object status -eq 'passed').Count
        blockedSkillCount = $failureBlocked
        results = $results
        validatorFindings = @("unexpected validator failure: $failureMessage")
        claimsNotProven = $claimsNotProven
    }
    try {
        Write-ESStaticAcceptanceReport -Report $failureReport
        $failureReport | ConvertTo-Json -Depth 12
    } catch {
        Write-Error "Static acceptance coverage failed and its current-invocation blocked report could not be finalized; the in-progress sentinel remains authoritative: $($_.Exception.Message)" -ErrorAction Continue
        exit 1
    }
    Write-Error "Static acceptance coverage failed closed: $failureMessage" -ErrorAction Continue
    exit 1
}
