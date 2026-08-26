[CmdletBinding()]
param([string]$ProjectRoot,[string]$ReportPath='ES/Output/StaticReplay/es-task-context-runtime.json')
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path}
$validators=@(
    'Test-ESGoalV1.ps1',
    'Test-ESRoutePlanContract.ps1',
    'Test-ESTaskContextRuntimeIntegrationPolicy.ps1',
    'Test-ESPlatformEvidenceContract.ps1',
    'Test-ESEvidenceVerifierRegistry.ps1',
    'Test-ESOutcomeEvaluation.ps1',
    'Test-ESTaskContextRuntime.ps1',
    'Test-ESTaskContextRuntimeSchema.ps1',
    'Test-ESTaskContextEvaluationAdapter.ps1'
    'Test-ESCommercialEvaluation.ps1'
)
foreach($validatorName in $validators){
    $validator=Join-Path $PSScriptRoot $validatorName
    $powershell=Join-Path $PSHOME 'powershell.exe'
    $output=& $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $validator -ProjectRoot $ProjectRoot
    if($LASTEXITCODE-ne0){$output;exit $LASTEXITCODE}
}
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-task-context-runtime/static-replay.manifest.json' -ReportPath $ReportPath
