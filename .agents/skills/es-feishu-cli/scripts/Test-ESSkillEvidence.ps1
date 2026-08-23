<#
.SYNOPSIS
Validates one es-feishu-cli evidence receipt with the shared strict validator.
.PARAMETER SkillPath
Path to this Skill. Read-only.
.PARAMETER EvidencePath
Receipt to validate. Read-only.
.PARAMETER ProjectRoot
Optional explicit project root. Read-only.
.PARAMETER MaxEvidenceAgeHours
Maximum accepted evidence age.
.NOTES
Does not write or invoke external services. Repeated validation is idempotent. Exit code follows the shared validator; preserve the rejected receipt for diagnosis and obtain fresh evidence before retrying.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SkillPath,
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [string]$ProjectRoot,
    [ValidateRange(1, 8760)][int]$MaxEvidenceAgeHours = 168
)

$ErrorActionPreference = 'Stop'
$skill = (Resolve-Path -LiteralPath $SkillPath -ErrorAction Stop).Path
$root = if ($ProjectRoot) {
    (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
} else {
    Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skill))
}
$strict = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
if (-not (Test-Path -LiteralPath $strict -PathType Leaf)) {
    throw 'Shared strict evidence validator is missing.'
}

& $strict -SkillPath $skill -EvidencePath $EvidencePath -ProjectRoot $root -MaxEvidenceAgeHours $MaxEvidenceAgeHours
