[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ProjectRoot,
    [Parameter(Mandatory = $true)] [string]$SkillName
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
if ($SkillName -notmatch '^es-[a-z0-9-]{1,63}$') { throw "Invalid SkillName: $SkillName" }
$skillPath = Join-Path $root ('.agents/skills/{0}' -f $SkillName)
$bindingPath = Join-Path $root '.agents/SKILL_AISPACE_BINDINGS.json'
if (-not (Test-Path -LiteralPath (Join-Path $skillPath 'SKILL.md') -PathType Leaf)) { throw "Skill SKILL.md missing: $SkillName" }
if (-not (Test-Path -LiteralPath (Join-Path $skillPath 'governance.json') -PathType Leaf)) { throw "Skill governance.json missing: $SkillName" }
if (-not (Test-Path -LiteralPath $bindingPath -PathType Leaf)) { throw 'AISpace binding registry missing' }
$registry = Get-Content -LiteralPath $bindingPath -Encoding UTF8 -Raw | ConvertFrom-Json
$entry = @($registry.skills | Where-Object { [string]$_.skillName -eq $SkillName })
if ($entry.Count -ne 1) { throw "AISpace binding is mandatory and must resolve exactly once: $SkillName" }
$bindings = @($entry[0].bindings)
if ($bindings.Count -lt 1) { throw "AISpace binding list is empty: $SkillName" }
$expectedContract = '.agents/skills/{0}/governance.json' -f $SkillName
if ([string]$entry[0].skillContractRef -ne $expectedContract) { throw "AISpace binding does not point to the Skill governance contract: $SkillName" }
$invalid = @($bindings | Where-Object {
    [string]::IsNullOrWhiteSpace([string]$_.bindingId) -or
    [string]$_.pathTemplate -notmatch '^(ES/AISpace/Local/(<category>/<YYYYMMDD>|Cache/<YYYYMMDD>)/|ES/AISpace/Public/(<category>/<YYYYMMDD>|Skills/<YYYYMMDD>)/|Assets/ES/AISpace/Public/<category>/<YYYYMMDD>/)'
})
if ($invalid.Count -gt 0) { throw "AISpace binding violates canonical roots: $SkillName" }
[pscustomobject][ordered]@{ schemaVersion = 1; validator = 'es-skill-creator-aispace-registration'; status = 'passed'; skillName = $SkillName; bindingCount = $bindings.Count; authority = 'ES/AISpace/AISPACE_AUTHORITY.json'; workflowAuthority = '.agents/skills/es-ai-space-organization/SKILL.md'; runtimeStatus = 'runtime-not-run' } | ConvertTo-Json -Depth 6
