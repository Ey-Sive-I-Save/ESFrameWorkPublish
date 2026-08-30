[CmdletBinding()]
param([string]$ProjectRoot='.',[int]$MaxFiles=128,[string]$OutputPath='ES/Output/Benchmarks/resource-reader-perf.json')
$ErrorActionPreference='Stop'; $root=(Resolve-Path $ProjectRoot).Path
$files=@(& rg --files $root | Where-Object {$_ -notmatch '\\(Library|Temp|Logs|obj|bin)\\'} | Select-Object -First $MaxFiles)
$samples=New-Object Collections.Generic.List[object]
foreach($file in $files){$sw=[Diagnostics.Stopwatch]::StartNew(); $size=(Get-Item -LiteralPath $file).Length; $null=[IO.File]::OpenRead($file).Close(); $sw.Stop(); $samples.Add([pscustomobject]@{extension=[IO.Path]::GetExtension($file).ToLowerInvariant();sizeBytes=$size;elapsedMs=[math]::Max(0.01,$sw.Elapsed.TotalMilliseconds)})}
$groups=@($samples|Group-Object extension|ForEach-Object { $a=@($_.Group|ForEach-Object elapsedMs|Sort-Object); $bytes=($_.Group|Measure-Object sizeBytes -Sum).Sum; [ordered]@{extension=$_.Name;fileCount=$_.Count;totalBytes=$bytes;p50Ms=$a[[math]::Floor(($a.Count-1)*0.5)];p95Ms=$a[[math]::Floor(($a.Count-1)*0.95)];throughputMiBPerSec=[math]::Round(($bytes/1MB)/([math]::Max(0.001,(($_.Group|Measure-Object elapsedMs -Sum).Sum)/1000)),2)}})
$out=[ordered]@{schemaVersion=1;benchmarkId='es-resource-reader.perf.v1';capturedUtc=[DateTime]::UtcNow.ToString('o');root=$root;fileCount=$samples.Count;groups=$groups;nonClaims=@('parser semantic latency','Unity runtime','release')}
$dest=Join-Path $root $OutputPath; New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null; $out|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $dest -Encoding UTF8; $out|ConvertTo-Json -Depth 6
