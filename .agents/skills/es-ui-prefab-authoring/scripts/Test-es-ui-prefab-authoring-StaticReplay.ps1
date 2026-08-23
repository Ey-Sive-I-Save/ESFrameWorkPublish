[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$shared=Join-Path $root '.agents/skills/es-static-deep-replay/scripts/Invoke-ESStaticDeepReplay.ps1'
$manifest='.agents/skills/es-ui-prefab-authoring/static-replay.manifest.json'
& powershell -NoProfile -File $shared -ProjectRoot $root -ManifestPath $manifest -ReportPath 'ES/Output/StaticReplay/es-ui-prefab-authoring.json'
exit $LASTEXITCODE
