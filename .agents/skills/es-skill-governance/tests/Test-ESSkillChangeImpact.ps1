[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$evaluator = Join-Path $ProjectRoot '.agents/skills/es-skill-governance/scripts/Get-ESSkillChangeImpact.ps1'
$results = [Collections.Generic.List[object]]::new()
function Invoke-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; [void]$results.Add([pscustomobject]@{ name=$Name; status='passed' }) }
    catch { [void]$results.Add([pscustomobject]@{ name=$Name; status='failed'; detail=$_.Exception.Message }) }
}
Invoke-Case 'existing-governance-skill-is-major' {
    $o = & $evaluator -ProjectRoot $ProjectRoot -SkillPath (Join-Path $ProjectRoot '.agents/skills/es-skill-governance') | ConvertFrom-Json
    if ($o.skillChangeImpact -ne 'major' -or $o.revalidationRequired -ne $true -or $o.completionClaimAllowed -ne $false) { throw 'Governance Skill change must require revalidation.' }
    if (@($o.requiredStages) -notcontains 'catalog-registry') { throw 'Major changes must require Catalog/Registry.' }
}
Invoke-Case 'existing-bootstrap-skill-is-major' {
    $o = & $evaluator -ProjectRoot $ProjectRoot -SkillPath (Join-Path $ProjectRoot '.agents/skills/es-codex-session-bootstrap') | ConvertFrom-Json
    if ($o.skillChangeImpact -ne 'major' -or $o.decisionSource -ne 'derived') { throw 'Bootstrap change impact was not derived as major.' }
}
Invoke-Case 'path-boundary-fails-closed' {
    $failed = $false
    try { & $evaluator -ProjectRoot $ProjectRoot -SkillPath $ProjectRoot | Out-Null } catch { $failed = $true }
    if (-not $failed) { throw 'Non-Skill path must be rejected.' }
}
$failedCount = @($results | Where-Object status -eq 'failed').Count
$status = 'failed'; if ($failedCount -eq 0) { $status = 'passed' }
[pscustomobject]@{ schemaVersion=1; validator='es-skill-change-impact'; status=$status; findingCount=$failedCount; cases=$results.ToArray(); runtimeStatus='runtime-not-run' } | ConvertTo-Json -Depth 8
if ($failedCount -gt 0) { exit 1 }
