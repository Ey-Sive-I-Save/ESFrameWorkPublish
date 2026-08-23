[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SkillPath,
    [Parameter(Mandatory=$true)][string]$EvidencePath,
    [string]$ProjectRoot,
    [ValidateRange(1,8760)][int]$MaxEvidenceAgeHours=168
)
$ErrorActionPreference='Stop'
$skill=(Resolve-Path -LiteralPath $SkillPath -ErrorAction Stop).Path
$root=if($ProjectRoot){(Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path}else{Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skill))}
$strict=Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
if(-not (Test-Path -LiteralPath $strict -PathType Leaf)){throw 'Shared strict evidence validator is missing'}
& powershell -NoProfile -File $strict -SkillPath $skill -EvidencePath $EvidencePath -ProjectRoot $root -MaxEvidenceAgeHours $MaxEvidenceAgeHours
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
