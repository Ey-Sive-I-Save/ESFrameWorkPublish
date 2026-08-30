[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,
    [ValidateSet('Check')]
    [string]$Mode = 'Check',
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "ProjectRoot does not exist: $ProjectRoot" }

function Invoke-Check([string]$Id, [scriptblock]$Action) {
    try {
        $global:LASTEXITCODE = 0
        $output = & $Action 2>&1 | Out-String
        $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
        if ($exitCode -ne 0) {
            [pscustomobject]@{ id = $Id; status = 'failed'; exitCode = $exitCode; output = $output.Trim() }
        } else {
            [pscustomobject]@{ id = $Id; status = 'passed'; exitCode = 0; output = $output.Trim() }
        }
    }
    catch {
        [pscustomobject]@{ id = $Id; status = 'failed'; exitCode = 1; output = $_.Exception.Message }
    }
}

$spaceScripts = Join-Path $root '.agents/skills/es-ai-space-organization/scripts'
$governanceScripts = Join-Path $root '.agents/skills/es-skill-governance/scripts'
$checks = @()
$checks += Invoke-Check 'aispace-authority' { & (Join-Path $spaceScripts 'Test-ESAISpaceAuthority.ps1') -ProjectRoot $root }
$checks += Invoke-Check 'local-temp-policy' { & (Join-Path $spaceScripts 'Test-ESLocalTempPolicy.ps1') -ProjectRoot $root }
$checks += Invoke-Check 'aispace-bindings' { python (Join-Path $governanceScripts 'Test-ESSkillAISpaceBindings.py') --project-root $root }
$checks += Invoke-Check 'skill-relation-registry' { python (Join-Path $governanceScripts 'Test-ESSkillRelationRegistry.py') --project-root $root }

$bindingPath = Join-Path $root '.agents/SKILL_AISPACE_BINDINGS.json'
$binding = Get-Content -LiteralPath $bindingPath -Encoding UTF8 -Raw | ConvertFrom-Json
$skills = @($binding.skills | ForEach-Object { $_.skillName } | Sort-Object -Unique)
$missing = @($skills | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $root ('.agents/skills/{0}/SKILL.md' -f $_))) -or
    -not (Test-Path -LiteralPath (Join-Path $root ('.agents/skills/{0}/governance.json' -f $_)))
})
$checks += [pscustomobject]@{
    id = 'bound-skill-entrypoints'; status = if ($missing.Count -eq 0) { 'passed' } else { 'failed' }
    exitCode = if ($missing.Count -eq 0) { 0 } else { 1 }
    output = if ($missing.Count -eq 0) { "validated $($skills.Count) bound Skills" } else { "missing: $($missing -join ', ')" }
}

$result = [ordered]@{
    schemaVersion = 1
    toolchain = 'es-aispace-toolchain'
    mode = $Mode
    projectRoot = $root
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    checks = @($checks)
    status = if (@($checks | Where-Object status -eq 'failed').Count -eq 0) { 'passed' } else { 'failed' }
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Unity import behavior','Runtime loading','release behavior')
}

if ($ReportPath) {
    if ([IO.Path]::IsPathRooted($ReportPath)) { throw 'ReportPath must be project-relative' }
    $fullReport = Join-Path $root $ReportPath
    $parent = Split-Path -Parent $fullReport
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($fullReport, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $result.reportPath = $ReportPath
}

$result | ConvertTo-Json -Depth 8
if ($result.status -eq 'failed') { exit 1 }
