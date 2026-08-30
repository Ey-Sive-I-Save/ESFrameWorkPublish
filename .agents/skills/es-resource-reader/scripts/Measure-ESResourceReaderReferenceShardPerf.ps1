[CmdletBinding()]
param(
    [string]$ManifestPath='ES/Output/ResourceReader/reference-shards/manifest.json',
    [string]$GuidPrefix='0',
    [int]$Iterations=100,
    [string]$ProjectRoot='.',
    [string]$OutputPath='ES/Output/Benchmarks/resource-reader-reference-shard-perf.json'
)
$ErrorActionPreference='Stop';if($Iterations -lt 5 -or $Iterations -gt 5000){throw 'Iterations must be between 5 and 5000'}
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;$manifestFull=(Resolve-Path -LiteralPath $ManifestPath).Path;$manifest=Get-Content -LiteralPath $manifestFull -Raw -Encoding UTF8|ConvertFrom-Json
$prefix=([string]$GuidPrefix).Trim().ToLowerInvariant();if($prefix -notmatch '^[0-9a-f]+$'){throw 'GuidPrefix must be hexadecimal'}
$entry=@($manifest.shards|Where-Object {[string]$_.prefix -eq $prefix.Substring(0,[Math]::Min($manifest.prefixLength,$prefix.Length))})|Select-Object -First 1;if($null -eq $entry){throw "No shard for prefix $prefix"}
$projectRoot=(Get-Item -LiteralPath (Split-Path $manifestFull -Parent)).Parent.Parent.Parent.Parent.FullName;$shardFull=Join-Path $projectRoot ([string]$entry.path)
$cold=New-Object Collections.Generic.List[double];$cached=New-Object Collections.Generic.List[double]
for($i=0;$i -lt $Iterations;$i++){$sw=[Diagnostics.Stopwatch]::StartNew();$obj=Get-Content -LiteralPath $shardFull -Raw -Encoding UTF8|ConvertFrom-Json;$sw.Stop();$cold.Add($sw.Elapsed.TotalMilliseconds);$sw.Restart();$null=@($obj.references|Where-Object {[string]$_.guid -like "$prefix*"});$sw.Stop();$cached.Add($sw.Elapsed.TotalMilliseconds)}
function Stats($values){$a=@($values|Sort-Object);[ordered]@{p50Ms=[math]::Round($a[[int]([math]::Floor(($a.Count-1)*0.50))],4);p95Ms=[math]::Round($a[[int]([math]::Floor(($a.Count-1)*0.95))],4);minMs=[math]::Round($a[0],4);maxMs=[math]::Round($a[$a.Count-1],4)}}
$out=[ordered]@{schemaVersion=1;benchmarkId='es-resource-reader.reference-shard-perf.v1';capturedUtc=[DateTime]::UtcNow.ToString('o');manifestPath=$ManifestPath.Replace('\','/');shardPath=$entry.path;guidPrefix=$prefix;iterations=$Iterations;coldRead=Stats $cold;cachedQuery=Stats $cached;speedupRatio=[math]::Round((($cold|Measure-Object -Average).Average)/(($cached|Measure-Object -Average).Average),2);runtimeStatus='runtime-not-run'}
$dest=Join-Path $root $OutputPath;New-Item -ItemType Directory -Force -Path (Split-Path $dest)|Out-Null;$out|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $dest -Encoding UTF8;$out|ConvertTo-Json -Depth 8
