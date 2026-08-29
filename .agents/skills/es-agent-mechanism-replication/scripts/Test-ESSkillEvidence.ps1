[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [ValidateRange(1,8760)][int]$MaxEvidenceAgeHours = 168
)

$ErrorActionPreference = 'Stop'
$strict = Join-Path $PSScriptRoot '..\..\es-skill-governance\scripts\Test-ESStrictEvidenceReceipt.ps1'
if (-not (Test-Path -LiteralPath $strict -PathType Leaf)) { throw 'Shared strict evidence validator is missing.' }
& powershell -NoProfile -File $strict -SkillPath (Join-Path $PSScriptRoot '..') -EvidencePath $EvidencePath -ProjectRoot $ProjectRoot -MaxEvidenceAgeHours $MaxEvidenceAgeHours
exit $LASTEXITCODE
