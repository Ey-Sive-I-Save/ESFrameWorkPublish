[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ProjectRoot,
    [string]$OutputPath = 'ES/Output/Governance/static-acceptance-coverage.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$skillsRoot = Join-Path $root '.agents/skills'
$results = @()

foreach ($directory in @(Get-ChildItem -LiteralPath $skillsRoot -Directory | Sort-Object Name)) {
    $skillPath = $directory.FullName
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
    $result.profile = [string]$manifest.responsibilityProfile
    if ([string]::IsNullOrWhiteSpace($result.profile)) { $result.status = 'blocked'; $result.findings += 'missing responsibilityProfile' }
    if (-not $manifest.runtimeEscalation) { $result.status = 'blocked'; $result.findings += 'missing runtimeEscalation' }
    elseif ([string]$manifest.runtimeEscalation.reason -notmatch '\S') { $result.status = 'blocked'; $result.findings += 'runtimeEscalation.reason is empty' }
    if (@($manifest.runtimeClaimsNotProven).Count -eq 0) { $result.status = 'blocked'; $result.findings += 'runtimeClaimsNotProven must not be empty' }
    if (-not $manifest.specializedAcceptance) { $result.status = 'blocked'; $result.findings += 'missing specializedAcceptance' }
    else {
        $result.acceptanceId = [string]$manifest.specializedAcceptance.id
        $cases = @($manifest.specializedAcceptance.requiredStaticCases)
        $artifacts = @($manifest.specializedAcceptance.evidenceArtifacts)
        $result.specializedCaseCount = $cases.Count
        $result.evidenceArtifactCount = $artifacts.Count
        if ([string]::IsNullOrWhiteSpace($result.acceptanceId)) { $result.status = 'blocked'; $result.findings += 'missing specialized acceptance id' }
        if ($cases.Count -eq 0) { $result.status = 'blocked'; $result.findings += 'no responsibility-specific static cases' }
        if ($artifacts.Count -eq 0) { $result.status = 'blocked'; $result.findings += 'no evidence artifacts' }
        $guidanceRef = [string]$manifest.specializedGuidanceRef
        if ([string]::IsNullOrWhiteSpace($guidanceRef)) { $result.status = 'blocked'; $result.findings += 'missing specializedGuidanceRef' }
        elseif (-not (Test-Path -LiteralPath (Join-Path $skillPath ($guidanceRef.Replace('/', '\'))) -PathType Leaf)) { $result.status = 'blocked'; $result.findings += "missing guidance: $guidanceRef" }
        foreach ($artifact in $artifacts) {
            $artifactPath = Join-Path $skillPath ([string]$artifact.Replace('/', '\'))
            if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) { $result.status = 'blocked'; $result.findings += "missing evidence artifact: $artifact" }
        }
    }
    $results += [pscustomobject]$result
}
$blocked = @($results | Where-Object status -eq 'blocked').Count
$report = [ordered]@{
    schemaVersion = 1
    toolId = 'es-static-acceptance-coverage'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    profile = 'StaticReview'
    mutatesSources = $false
    startsRuntime = $false
    status = if ($blocked -eq 0) { 'passed' } else { 'blocked' }
    skillCount = $results.Count
    coveredSkillCount = @($results | Where-Object status -eq 'passed').Count
    blockedSkillCount = $blocked
    results = $results
    claimsNotProven = @('responsibility-specific runtime behavior', 'Unity/editor/process behavior', 'performance and visual behavior')
}
$output = Join-Path $root ($OutputPath.Replace('/', '\'))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$temporary = "$output.tmp-$([Guid]::NewGuid().ToString('N'))"
try { [IO.File]::WriteAllText($temporary, ($report | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false))); Move-Item -LiteralPath $temporary -Destination $output -Force }
finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
$report | ConvertTo-Json -Depth 12
