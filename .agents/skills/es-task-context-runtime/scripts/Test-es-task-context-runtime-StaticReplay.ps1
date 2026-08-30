[CmdletBinding()]
param([string]$ProjectRoot,[string]$ReportPath='ES/Output/StaticReplay/es-task-context-runtime.json',[string]$ProgressPath='ES/Output/StaticReplay/es-task-context-runtime-progress.json',[ValidateRange(5,3600)][int]$ValidatorTimeoutSeconds=180)
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path}
$rootFull=[IO.Path]::GetFullPath($ProjectRoot)
$rootPrefix=$rootFull.TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
$progressFull=[IO.Path]::GetFullPath((Join-Path $rootFull $ProgressPath))
if(-not $progressFull.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'ProgressPath must remain within the project root.'}
$progressParent=Split-Path -Parent $progressFull
if(-not(Test-Path -LiteralPath $progressParent)){New-Item -ItemType Directory -Path $progressParent -Force|Out-Null}
$progress=@()
function Save-ReplayProgress { [IO.File]::WriteAllText($progressFull,([pscustomobject][ordered]@{schemaVersion=1;validator='Test-es-task-context-runtime-StaticReplay';updatedUtc=[DateTime]::UtcNow.ToString('o');items=@($progress)}|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false)) }
$validators=@(
    'Test-ESGoalV1.ps1',
    'Test-ESRoutePlanContract.ps1',
    'Test-ESTaskContextRuntimeIntegrationPolicy.ps1',
    'Test-ESPlatformEvidenceContract.ps1',
    'Test-ESEvidenceVerifierRegistry.ps1',
    'Test-ESOutcomeEvaluation.ps1',
    'Test-ESTaskContextRuntime.ps1',
    'Test-ESTaskContextRuntimeSchema.ps1',
    'Test-ESTaskContextEvaluationAdapter.ps1',
    'Test-ESCommercialEvaluation.ps1'
)
foreach($validatorName in $validators){
    $validator=Join-Path $PSScriptRoot $validatorName
    $powershell=Join-Path $PSHOME 'powershell.exe'
    $runId=[guid]::NewGuid().ToString('N')
    $stdout=Join-Path ([IO.Path]::GetTempPath()) ("es-replay-$runId.out")
    $stderr=Join-Path ([IO.Path]::GetTempPath()) ("es-replay-$runId.err")
    $progress += [pscustomobject]@{validator=$validatorName;status='running';startedUtc=[DateTime]::UtcNow.ToString('o');finishedUtc=$null;exitCode=$null}
    Save-ReplayProgress
    $process=Start-Process -FilePath $powershell -ArgumentList @('-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$validator,'-ProjectRoot',$ProjectRoot) -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    if(-not $process.WaitForExit($ValidatorTimeoutSeconds * 1000)){
        try{$process.Kill()}catch{Write-Verbose ("validator kill failed: {0}" -f $_.Exception.Message)}
        try{$process.WaitForExit(5000)}catch{Write-Verbose ("validator wait failed: {0}" -f $_.Exception.Message)}
        Remove-Item -LiteralPath $stdout,$stderr -Force -ErrorAction SilentlyContinue
        $progress[-1].status='timeout';$progress[-1].finishedUtc=[DateTime]::UtcNow.ToString('o');Save-ReplayProgress
        Write-Error ("validator-timeout: {0} exceeded {1}s" -f $validatorName,$ValidatorTimeoutSeconds)
        exit 124
    }
    $output=if(Test-Path -LiteralPath $stdout){Get-Content -LiteralPath $stdout -Raw -Encoding UTF8}else{''}
    $errorOutput=if(Test-Path -LiteralPath $stderr){Get-Content -LiteralPath $stderr -Raw -Encoding UTF8}else{''}
    Remove-Item -LiteralPath $stdout,$stderr -Force -ErrorAction SilentlyContinue
    $process.Refresh();$childExit=[int]$process.ExitCode
    if($childExit -ne 0){$progress[-1].status='failed';$progress[-1].exitCode=$childExit;$progress[-1].finishedUtc=[DateTime]::UtcNow.ToString('o');Save-ReplayProgress;$output; $errorOutput; exit $childExit}
    $progress[-1].status='passed';$progress[-1].exitCode=0;$progress[-1].finishedUtc=[DateTime]::UtcNow.ToString('o');Save-ReplayProgress
}
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-task-context-runtime/static-replay.manifest.json' -ReportPath $ReportPath
