[CmdletBinding()]
param(
    [string]$ReportsRoot='ES/Output/Benchmarks',
    [string]$OutputPath='ES/Output/Benchmarks/resource-collection-perf-trend.json'
)
$ErrorActionPreference='Stop';$rows=[Collections.Generic.List[object]]::new()
foreach($file in @(Get-ChildItem -LiteralPath (Resolve-Path $ReportsRoot) -Filter '*.json' -File|Sort-Object Name)){
    try{$j=Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8|ConvertFrom-Json}catch{continue}
    if($j.benchmarkId -ne 'es-resource-collection.batch-perf.v1' -or $null -eq $j.cold){continue}
    $name=$file.BaseName;$format='unknown';$count=[int]$j.cold.fileCount
    if($name -match 'format-([^\.]+)$'){$format=$Matches[1]}elseif($name -match 'fixture-'){$format='fixture'}
    [void]$rows.Add([ordered]@{report=$file.Name;format=$format;fileCount=$count;packageId=[string]$j.packageId;autoParallel=[bool]$j.autoParallel;coldFilesPerSecond=[double]$j.cold.filesPerSecond;incrementalFilesPerSecond=[double]$j.incremental.filesPerSecond;speedupRatio=[double]$j.speedupRatio;failedCount=[int]$j.cold.failedCount})
}
$ordered=@($rows.ToArray() | ForEach-Object { [pscustomobject]$_ } | Sort-Object { "$($_.format)|$($_.fileCount)|$($_.report)" })
$out=[ordered]@{schemaVersion=1;trendId='es-resource-collection.perf-trend.v1';generatedUtc=[DateTime]::UtcNow.ToString('o');environment=[ordered]@{os=[Environment]::OSVersion.VersionString;processorCount=[Environment]::ProcessorCount;powershell=$PSVersionTable.PSVersion.ToString()};reportCount=$ordered.Count;reports=$ordered;nonClaims=@('Unity runtime','AssetDatabase timing','release')}
$dir=Split-Path -Parent $OutputPath;if($dir -and !(Test-Path $dir)){New-Item -ItemType Directory -Path $dir -Force|Out-Null};$out|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $OutputPath -Encoding UTF8;$out|ConvertTo-Json -Depth 8
