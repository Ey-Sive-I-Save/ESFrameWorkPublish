[CmdletBinding()]
param(
  [string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
  [string]$ReportPath='ES/Output/StaticReplay/es-ai-collaboration-menu.json'
)
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-ai-collaboration-menu/static-replay.manifest.json' -ReportPath $ReportPath
