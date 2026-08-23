[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,[string]$ReportPath='ES/Output/StaticReplay/es-dependency-boundary-audit.json')
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-dependency-boundary-audit/static-replay.manifest.json' -ReportPath $ReportPath

