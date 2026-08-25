[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$skill=Join-Path $root '.agents/skills/es-ui-intent-authoring'
$python=Join-Path $skill 'scripts/self_test_ui_intent.py'
$env:PYTHONUTF8='1'
$env:PYTHONDONTWRITEBYTECODE='1'
$output=& python $python
if ($LASTEXITCODE -ne 0) { throw 'IntentSpec self-test failed' }
$shared=Join-Path $root '.agents/skills/es-static-deep-replay/scripts/Invoke-ESStaticDeepReplay.ps1'
$manifest='.agents/skills/es-ui-intent-authoring/static-replay.manifest.json'
& powershell -NoProfile -File $shared -ProjectRoot $root -ManifestPath $manifest -ReportPath 'ES/Output/StaticReplay/es-ui-intent-authoring.json'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output $output
exit 0
