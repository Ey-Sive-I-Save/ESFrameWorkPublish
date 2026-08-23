[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,[string]$ReportPath='ES/Output/StaticReplay/es-use-ai-command.json')
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-use-ai-command/static-replay.manifest.json' -ReportPath $ReportPath

