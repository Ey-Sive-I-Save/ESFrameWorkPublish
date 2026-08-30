[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [int]$MaxFiles=256,
    [int]$MaxParallel=4,
    [switch]$AutoParallel,
    [string]$SchedulePath='',
    [string]$StatePath='ES/Output/ResourceCollection/.perf-batch-state.json',
    [string]$OutputPath='ES/Output/Benchmarks/resource-collection-batch-perf.json'
)
$ErrorActionPreference='Stop'
$runner=Join-Path $PSScriptRoot 'Invoke-ESResourceCollectionBatch.ps1'
$runnerParams=@{Root=$Root;MaxFiles=$MaxFiles;MaxParallel=$MaxParallel;StatePath=$StatePath}
if($AutoParallel){$runnerParams.AutoParallel=$true}
if($SchedulePath){$runnerParams.SchedulePath=$SchedulePath}
$effectiveAutoParallel=[bool]$AutoParallel
$packageId=''
if($SchedulePath -and (Test-Path -LiteralPath $SchedulePath)){
    try{$scheduleJson=Get-Content -LiteralPath $SchedulePath -Raw -Encoding UTF8|ConvertFrom-Json;$effectiveAutoParallel=[bool]$scheduleJson.autoParallel;$packageId=[string]$scheduleJson.packageId}
    catch{throw "Invalid SchedulePath JSON: $($_.Exception.Message)"}
}
$cold=& $runner @runnerParams | ConvertFrom-Json
$warm=& $runner @runnerParams | ConvertFrom-Json
$speed=0
if($warm.elapsedMilliseconds -gt 0){$speed=[Math]::Round([double]$cold.elapsedMilliseconds/[double]$warm.elapsedMilliseconds,3)}
$out=[ordered]@{
    schemaVersion=1;benchmarkId='es-resource-collection.batch-perf.v1';root=(Resolve-Path $Root).Path;packageId=$packageId;statePath=$StatePath;maxFiles=$MaxFiles;maxParallel=$MaxParallel;autoParallel=$effectiveAutoParallel;schedulePath=$SchedulePath
    cold=[ordered]@{fileCount=$cold.fileCount;effectiveParallel=$cold.effectiveParallel;elapsedMilliseconds=$cold.elapsedMilliseconds;filesPerSecond=$cold.filesPerSecond;failedCount=$cold.failedCount}
    incremental=[ordered]@{fileCount=$warm.fileCount;effectiveParallel=$warm.effectiveParallel;elapsedMilliseconds=$warm.elapsedMilliseconds;filesPerSecond=$warm.filesPerSecond;hitRate=$warm.incrementalHitRate;failedCount=$warm.failedCount}
    speedupRatio=$speed;runtimeStatus='runtime-not-run';nonClaims=@('Unity import','AssetDatabase timing','runtime loading','release')
}
$dir=Split-Path -Parent $OutputPath
if($dir -and -not(Test-Path $dir)){New-Item -ItemType Directory -Path $dir -Force|Out-Null}
$out|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $OutputPath -Encoding UTF8
$out|ConvertTo-Json -Depth 10
