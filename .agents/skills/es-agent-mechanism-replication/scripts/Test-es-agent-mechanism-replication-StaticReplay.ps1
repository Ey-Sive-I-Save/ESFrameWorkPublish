[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$ReportPath = 'ES/Output/StaticReplay/es-agent-mechanism-replication.json',
    [switch]$VerifyNetwork
)

$acceptance = Join-Path $ProjectRoot 'ES/Automation/ABCD/Test-ESABCDStaticAcceptance.ps1'
if (-not (Test-Path -LiteralPath $acceptance -PathType Leaf)) {
    throw "Executable ABCD static acceptance is missing: $acceptance"
}
$acceptanceArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$acceptance,'-ProjectRoot',$ProjectRoot)
if ($VerifyNetwork) { $acceptanceArgs += '-VerifyNetwork' }
$null = & powershell @acceptanceArgs 2>&1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$shared = Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
if (-not (Test-Path -LiteralPath $shared -PathType Leaf)) {
    throw "StaticDeepReplay engine not found: $shared"
}
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-agent-mechanism-replication/static-replay.manifest.json' -ReportPath $ReportPath
exit $LASTEXITCODE
