[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [string]$ExistingIndexPath='ES/Output/ResourceReader/resource-index.json',
    [int]$MaxFiles=256,
    [int]$MaxParallel=4,
    [string]$OutputPath='ES/Output/ResourceReader/resource-index.json'
)
$ErrorActionPreference='Stop'
$rootFull=(Resolve-Path -LiteralPath $Root).Path.TrimEnd('\','/')
$readerPath=Join-Path $PSScriptRoot 'Invoke-ESResourceReader.ps1'
$oldByPath=@{}
$existing=Join-Path (Resolve-Path '.').Path $ExistingIndexPath
if(Test-Path -LiteralPath $existing -PathType Leaf){
    try{$old=(Get-Content -LiteralPath $existing -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop); foreach($x in @($old.items)){if($x -and $x.sourcePath){$oldByPath[[string]$x.sourcePath]=$x}}}catch{throw 'Existing resource index is corrupt.'}
}
$files=@(Get-ChildItem -LiteralPath $rootFull -File -Recurse | Where-Object {$_.Length -le 104857600} | Sort-Object FullName | Select-Object -First $MaxFiles)
$states=@(); foreach($file in $files){$rel=$file.FullName.Substring($rootFull.Length).TrimStart('\','/').Replace('\','/'); $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); $oldItem=$oldByPath[$rel]; $states+=[pscustomobject]@{file=$file;relative=$rel;hash=$hash;old=$oldItem;changed=($null -eq $oldItem -or $oldItem.sourceSha256 -ne $hash)}}
$changed=@($states|Where-Object changed); $readerWorker={param($path,$reader)& $reader -Path $path|ConvertFrom-Json}; $pool=[RunspaceFactory]::CreateRunspacePool(1,[Math]::Max(1,$MaxParallel));$pool.Open();$pending=@()
foreach($state in $changed){$ps=[PowerShell]::Create();$ps.RunspacePool=$pool;[void]$ps.AddScript($readerWorker).AddArgument($state.file.FullName).AddArgument($readerPath);$pending+=[pscustomobject]@{ps=$ps;handle=$ps.BeginInvoke();state=$state}}
$parsed=@{}; foreach($job in $pending){try{$parsed[$job.state.relative]=$job.ps.EndInvoke($job.handle)|Select-Object -Last 1}finally{$job.ps.Dispose()}};$pool.Close();$pool.Dispose()
$items=@(); foreach($state in $states){$item=$null;if($state.changed){$item=$parsed[$state.relative];if($null -eq $item){throw "Reader returned no projection: $($state.relative)"};$summary=$item.summary;$item=[ordered]@{sourcePath=$state.relative;sourceSha256=$state.hash;detectedFormat=$item.detectedFormat;parserId=$item.parserId;parserVersion=if($item.parserVersion){$item.parserVersion}else{'1'};byteCount=if($summary.sizeBytes){$summary.sizeBytes}else{$state.file.Length};entryCount=@($item.entries).Count;objectCount=if($summary.objectCount){$summary.objectCount}else{0};dependencyCount=if($summary.dependencyCount){$summary.dependencyCount}else{0};warningCount=@($item.warnings).Count;errorCount=@($item.errors).Count;status=if(@($item.errors).Count -gt 0){'error'}else{'ready'}}}else{$item=$state.old};$items+=$item}
$items=@($items|Sort-Object sourcePath);$out=[ordered]@{schemaVersion=1;indexId='es-resource-reader.resource-index.v1';projectRoot=$rootFull;capturedUtc=[DateTime]::UtcNow.ToString('o');fileCount=$items.Count;maxFiles=$MaxFiles;maxParallel=$MaxParallel;reparsedCount=$changed.Count;reusedCount=($states.Count-$changed.Count);removedCount=($oldByPath.Keys|Where-Object { -not ($states.relative -contains $_) }).Count;items=$items;nonClaims=@('Unity import','runtime loading','network retrieval','release readiness')};$dest=Join-Path (Resolve-Path '.').Path $OutputPath;New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest)|Out-Null;[IO.File]::WriteAllText($dest,($out|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)));$out|ConvertTo-Json -Depth 12
