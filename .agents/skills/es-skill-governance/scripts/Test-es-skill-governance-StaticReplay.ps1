[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,[string]$ReportPath='ES/Output/StaticReplay/es-skill-governance.json')
$bindings=Join-Path $PSScriptRoot 'Test-ESEvidenceContractBindings.ps1'
& powershell -NoProfile -File $bindings -ProjectRoot $ProjectRoot -MaxSkills 128 -IncludeNegativeCases
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
$receiptContract=Join-Path $PSScriptRoot 'Test-ESStrictEvidenceReceiptContract.ps1'
& powershell -NoProfile -File $receiptContract -ProjectRoot $ProjectRoot
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-skill-governance/static-replay.manifest.json' -ReportPath $ReportPath

