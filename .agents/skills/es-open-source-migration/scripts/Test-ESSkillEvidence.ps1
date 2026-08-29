[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [ValidateRange(1, 8760)][int]$MaxEvidenceAgeHours = 168
)

$ErrorActionPreference = 'Stop'
$strict = Join-Path $ProjectRoot '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
if (-not (Test-Path -LiteralPath $strict -PathType Leaf)) { throw 'Shared strict evidence validator is missing.' }
& powershell -NoProfile -File $strict -SkillPath (Join-Path $ProjectRoot '.agents/skills/es-open-source-migration') -EvidencePath $EvidencePath -ProjectRoot $ProjectRoot -MaxEvidenceAgeHours $MaxEvidenceAgeHours
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
