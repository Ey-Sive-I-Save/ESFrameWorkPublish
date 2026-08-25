[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$SkillPath
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$skill = (Resolve-Path -LiteralPath $SkillPath -ErrorAction Stop).Path
$skillsRoot = (Resolve-Path -LiteralPath (Join-Path $root '.agents/skills') -ErrorAction Stop).Path.TrimEnd('\','/')
$skillPrefix = $skillsRoot + [IO.Path]::DirectorySeparatorChar
if (-not $skill.StartsWith($skillPrefix, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Parent $skill) -ne $skillsRoot) { throw 'SkillPath must be a direct child of project .agents/skills.' }
$skillName = Split-Path -Leaf $skill
if ($skillName -notmatch '^es-[a-z0-9]+(?:-[a-z0-9]+)*$') { throw 'Skill directory name is invalid.' }
$rules = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../references/skill-change-impact-rules.json') -Raw -Encoding UTF8 | ConvertFrom-Json
function Get-Rel([string]$path) {
    $full = [IO.Path]::GetFullPath($path)
    if (-not $full.StartsWith($skillPrefix, [StringComparison]::OrdinalIgnoreCase)) { return $null }
    return $full.Substring($skillPrefix.Length).Replace('\','/')
}
function Get-Classification([string]$relative) {
    foreach ($pattern in @($rules.majorPathPatterns)) { if ($relative -match [string]$pattern) { return [pscustomobject]@{ class = 'major'; reason = "major-path:$pattern" } } }
    foreach ($pattern in @($rules.mediumPathPatterns)) { if ($relative -match [string]$pattern) { return [pscustomobject]@{ class = 'medium'; reason = "medium-path:$pattern" } } }
    foreach ($pattern in @($rules.smallPathPatterns)) { if ($relative -match [string]$pattern) { return [pscustomobject]@{ class = 'small'; reason = "small-path:$pattern" } } }
    return [pscustomobject]@{ class = 'medium'; reason = 'unclassified-skill-file' }
}
function Add-Changed([System.Collections.Generic.List[object]]$list, [string]$path, [string]$kind) {
    $relative = Get-Rel $path
    if ([string]::IsNullOrWhiteSpace($relative) -or @($list | Where-Object path -eq $relative).Count -gt 0) { return }
    $classification = Get-Classification $relative
    $content = ''
    if (Test-Path -LiteralPath $path -PathType Leaf) { $content = [IO.File]::ReadAllText($path, [Text.UTF8Encoding]::new($false,$true)) }
    $semanticReasons = [Collections.Generic.List[string]]::new()
    foreach ($pattern in @($rules.majorSemanticPatterns)) { if ($content -match [string]$pattern) { [void]$semanticReasons.Add('major-semantic-marker'); break } }
    if ($semanticReasons.Count -gt 0) { $classification = [pscustomobject]@{ class = 'major'; reason = 'major-semantic-marker' } }
    [void]$list.Add([ordered]@{ path = $relative; changeKind = $kind; impactClass = $classification.class; reasons = @($classification.reason) })
}
$changed = [Collections.Generic.List[object]]::new()
$gitProbe = [string]((& git -C $root rev-parse --is-inside-work-tree 2>$null) | Select-Object -First 1)
if ($LASTEXITCODE -ne 0 -or $gitProbe.Trim() -ne 'true') { throw 'ProjectRoot must be a Git worktree for change impact evaluation.' }
$status = @(& git -C $root status --short --untracked-files=all -- (Join-Path '.agents/skills' $skillName) 2>$null)
foreach ($line in $status) {
    $text = [string]$line
    if ($text.Length -lt 4) { continue }
    $kind = if ($text.Substring(0,2) -match 'D') { 'deleted' } elseif ($text.Substring(0,2) -match 'A|\?') { 'added' } elseif ($text.Substring(0,2) -match 'R') { 'renamed' } else { 'modified' }
    $relative = $text.Substring(3).Trim().Trim('"')
    $full = Join-Path $root ($relative.Replace('/','\'))
    if (Test-Path -LiteralPath $full -PathType Leaf) { Add-Changed $changed $full $kind }
}
$rank = @{ small = 1; medium = 2; major = 3 }
$impact = 'small'
foreach ($item in @($changed)) { if ($rank[$item.impactClass] -gt $rank[$impact]) { $impact = $item.impactClass } }
$definition = $rules.classes.PSObject.Properties[$impact].Value
$head = [string]((& git -C $root rev-parse HEAD 2>$null) | Select-Object -First 1)
$branch = [string]((& git -C $root branch --show-current 2>$null) | Select-Object -First 1)
$result = [ordered]@{
    schemaVersion = 1
    evaluator = 'es-skill-change-impact'
    ruleSetId = [string]$rules.ruleSetId
    projectRoot = $root
    skillName = $skillName
    skillPath = (Get-Rel $skill)
    branch = $branch.Trim()
    headSha = $head.Trim()
    changedFiles = @($changed.ToArray())
    skillChangeImpact = $impact
    revalidationRequired = [bool]$definition.revalidationRequired
    requiredStages = @($definition.requiredStages)
    decisionSource = 'derived'
    completionClaimAllowed = ($impact -eq 'small')
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Skill execution behavior','Unity/editor/runtime behavior','User acceptance')
}
$result | ConvertTo-Json -Depth 12
