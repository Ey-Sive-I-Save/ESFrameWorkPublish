[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$EvidencePath,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
)
$ErrorActionPreference='Stop'
$central = Join-Path $ProjectRoot '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
& powershell -NoProfile -File $central -SkillPath (Join-Path $ProjectRoot '.agents/skills/es-game-core-loop-validation') -EvidencePath $EvidencePath -ProjectRoot $ProjectRoot
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
