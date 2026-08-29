[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,[string]$ReportPath='ES/Output/StaticReplay/es-ai-abc-core.json')
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
if(-not(Test-Path -LiteralPath $shared -PathType Leaf)){throw "StaticDeepReplay engine not found: $shared"}
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-ai-abc-core/static-replay.manifest.json' -ReportPath $ReportPath
exit $LASTEXITCODE
