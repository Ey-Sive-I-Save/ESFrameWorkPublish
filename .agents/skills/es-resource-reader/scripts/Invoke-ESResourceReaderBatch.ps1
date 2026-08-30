[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [int]$MaxFiles=256,
    [int]$MaxParallel=4,
    [string]$OutputPath='ES/Output/ResourceReader/batch.json',
    [string]$CacheRoot='ES/Output/ResourceReader/Cache'
)
$ErrorActionPreference='Stop'
$rootFull=(Resolve-Path -LiteralPath $Root).Path.TrimEnd('\','/')
$cacheFull=if([IO.Path]::IsPathRooted($CacheRoot)){[IO.Path]::GetFullPath($CacheRoot)}else{[IO.Path]::GetFullPath((Join-Path (Get-Location) $CacheRoot))}
$outputFull=if([IO.Path]::IsPathRooted($OutputPath)){[IO.Path]::GetFullPath($OutputPath)}else{[IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath))}
$files=@(Get-ChildItem -LiteralPath $rootFull -File -Recurse | Where-Object {
    $_.Length -le 104857600 -and
    $_.FullName -notlike ($cacheFull.TrimEnd('\')+'\*') -and
    $_.FullName -ne $outputFull
} | Select-Object -First $MaxFiles)
$readerPath=Join-Path $PSScriptRoot 'Invoke-ESResourceReader.ps1'
New-Item -ItemType Directory -Force -Path $cacheFull | Out-Null

function Get-SourceHash([string]$Path) {
    $sha=[Security.Cryptography.SHA256]::Create()
    try { $stream=[IO.File]::OpenRead($Path); try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant() } finally {$stream.Dispose()} }
    finally {$sha.Dispose()}
}

$sw=[Diagnostics.Stopwatch]::StartNew()
$csvFiles=@($files|Where-Object {$_.Extension.ToLowerInvariant() -in @('.csv','.tsv')})
$otherFiles=@($files|Where-Object {$_.Extension.ToLowerInvariant() -notin @('.csv','.tsv')})
$results=[Collections.Generic.List[object]]::new()
$csvManifest=[Collections.Generic.List[object]]::new()
$csvMeta=@{}
$cacheHitCount=0
foreach($f in $csvFiles){
    $kind=$f.Extension.TrimStart('.').ToLowerInvariant(); $hash=Get-SourceHash $f.FullName
    $key=($hash+'.'+$kind+'.rfc4180.batch.v1.1.json')
    $cachePath=Join-Path $cacheFull $key
    if(Test-Path -LiteralPath $cachePath -PathType Leaf){
        $cached=Get-Content -LiteralPath $cachePath -Raw -Encoding UTF8|ConvertFrom-Json
        if($cached.projectionVersion -eq 1 -and $cached.sourceSha256 -eq $hash -and $cached.parserId -eq ($kind+'.rfc4180.batch.v1')){[void]$results.Add($cached); $cacheHitCount++; continue}
    }
    [void]$csvManifest.Add([ordered]@{path=$f.FullName;format=$kind}); $csvMeta[$f.FullName]=[pscustomobject]@{hash=$hash;cachePath=$cachePath;kind=$kind}
}
if($csvManifest.Count -gt 0){
    $manifestPath=Join-Path $cacheFull ('batch-manifest-'+[guid]::NewGuid().ToString('N')+'.json')
    $csvManifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $manifestPath -Encoding UTF8
    try {
        $py=Join-Path $PSScriptRoot 'Parse-ESDelimitedBatch.py'
        $savedOutputEncoding=$OutputEncoding
        try {
            $OutputEncoding=[Text.UTF8Encoding]::new($false)
            $parsed=(& python.exe $py $manifestPath | Out-String)|ConvertFrom-Json
        } finally { $OutputEncoding=$savedOutputEncoding }
        foreach($item in @($parsed.items)){
            if($item.status -ne 'passed'){[void]$results.Add([pscustomobject]@{sourcePath=$item.path;errors=@($item.error);warnings=@()});continue}
            $meta=$csvMeta[$item.path]; $packet=[ordered]@{projectionVersion=1;sourcePath=$item.path;sourceSha256=$meta.hash;parserId=$item.parserId;detectedFormat=$meta.kind;summary=[ordered]@{sizeBytes=(Get-Item -LiteralPath $item.path).Length;extension=$meta.kind;rowCount=$item.summary.rowCount;columnCount=$item.summary.columnCount;headers=$item.summary.headers};entries=@($item.entries);warnings=@();errors=@();nonClaims=@('semantic completeness','Unity import','runtime behavior','network behavior');cacheKey=($meta.hash+':'+$item.parserId+':1')}
            $packet|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $meta.cachePath -Encoding UTF8; [void]$results.Add([pscustomobject]$packet)
        }
    } finally {Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue}
}

$worker={param($path,$reader)
    $obj=& $reader -Path $path | ConvertFrom-Json
    $obj
}
if($otherFiles.Count -gt 0){
    $pool=[RunspaceFactory]::CreateRunspacePool(1,[Math]::Max(1,$MaxParallel));$pool.Open();$pending=[Collections.Generic.List[object]]::new()
    foreach($f in $otherFiles){$ps=[PowerShell]::Create();$ps.RunspacePool=$pool;[void]$ps.AddScript($worker).AddArgument($f.FullName).AddArgument($readerPath);$pending.Add([pscustomobject]@{PS=$ps;Handle=$ps.BeginInvoke()})}
    foreach($job in $pending){try{$r=$job.PS.EndInvoke($job.Handle);foreach($x in $r){[void]$results.Add($x)}}finally{$job.PS.Dispose()}}
    $pool.Close();$pool.Dispose()
}
$sw.Stop();$ordered=@($results|Sort-Object sourcePath);$rate=if($sw.Elapsed.TotalSeconds -gt 0){[math]::Round($files.Count/$sw.Elapsed.TotalSeconds,2)}else{0}
$out=[ordered]@{schemaVersion=2;root=$rootFull;fileCount=$files.Count;csvBatchCount=$csvFiles.Count;cacheHitCount=$cacheHitCount;maxParallel=$MaxParallel;elapsedMilliseconds=[math]::Round($sw.Elapsed.TotalMilliseconds,3);filesPerSecond=$rate;results=$ordered;nonClaims=@('Unity import','runtime behavior','release behavior')}
$dir=Split-Path -Parent $OutputPath;if($dir -and -not(Test-Path $dir)){New-Item -ItemType Directory -Path $dir -Force|Out-Null};$out|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $OutputPath -Encoding UTF8;$out|ConvertTo-Json -Depth 12
