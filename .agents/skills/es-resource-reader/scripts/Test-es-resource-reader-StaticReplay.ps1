[CmdletBinding()]
param([string]$ProjectRoot='.')
$ErrorActionPreference='Stop'
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-resource-reader/static-replay.manifest.json' -ReportPath 'ES/Output/StaticReplay/es-resource-reader.json'
exit $LASTEXITCODE
