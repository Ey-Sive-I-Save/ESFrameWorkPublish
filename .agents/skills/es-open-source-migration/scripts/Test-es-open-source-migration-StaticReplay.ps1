[CmdletBinding()]
param(
  [string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\\..\\..\\..')).Path,
  [string]$ReportPath='ES/Output/StaticReplay/es-open-source-migration.json'
)
$shared=Join-Path $PSScriptRoot '..\\..\\es-static-deep-replay\\scripts\\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-open-source-migration/static-replay.manifest.json' -ReportPath $ReportPath
