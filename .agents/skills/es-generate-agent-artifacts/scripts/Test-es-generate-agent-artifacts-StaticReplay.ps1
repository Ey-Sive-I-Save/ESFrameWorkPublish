[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,[string]$ReportPath='ES/Output/StaticReplay/es-generate-agent-artifacts.json')
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-generate-agent-artifacts/static-replay.manifest.json' -ReportPath $ReportPath

