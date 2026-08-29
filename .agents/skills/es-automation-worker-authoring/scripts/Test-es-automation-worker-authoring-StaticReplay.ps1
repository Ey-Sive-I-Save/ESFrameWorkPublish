[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,[string]$ReportPath='ES/Output/StaticReplay/es-automation-worker-authoring.json')
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
$strictRaw=& (Join-Path $PSScriptRoot 'Test-ESWorkerContractStrictness.ps1') -ProjectRoot $ProjectRoot
$strict=$strictRaw|ConvertFrom-Json
if([string]$strict.status -ne 'passed'){throw 'Worker strictness fixtures failed.'}
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-automation-worker-authoring/static-replay.manifest.json' -ReportPath $ReportPath

