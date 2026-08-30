[CmdletBinding()]param([Parameter(Mandatory=$true)][string]$SkillPath,[Parameter(Mandatory=$true)][string]$EvidencePath,[string]$ProjectRoot,[int]$MaxEvidenceAgeHours=168)
$root=if($ProjectRoot){(Resolve-Path $ProjectRoot).Path}else{(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path}
& powershell -NoProfile -File (Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1') -SkillPath $SkillPath -EvidencePath $EvidencePath -ProjectRoot $root -MaxEvidenceAgeHours $MaxEvidenceAgeHours
exit $LASTEXITCODE
