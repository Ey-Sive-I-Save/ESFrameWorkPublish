[CmdletBinding()]
param(
    [string]$CacheRoot='ES/Output/FileProjectionCache',
    [string]$ProjectRoot='.',
    [string]$OutputPath='ES/Output/ResourceReader/resource-reference-index.json'
)
$ErrorActionPreference='Stop'; $root=(Resolve-Path -LiteralPath $ProjectRoot).Path; $cache=(Resolve-Path -LiteralPath (Join-Path $root $CacheRoot)).Path
$map=@{}; $projectionCount=0; $edgeCount=0; $invalidCount=0
foreach($file in Get-ChildItem -LiteralPath $cache -Filter '*.projection' -File){
    try{$p=Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8|ConvertFrom-Json}catch{$invalidCount++;continue}
    if($p.detectedFormat -ne 'unityyaml' -or [string]::IsNullOrWhiteSpace($p.sourcePath)){continue}; $projectionCount++
    foreach($entry in @($p.entries)){
        if($null -eq $entry -or [string]::IsNullOrWhiteSpace($entry.stableId)){continue}
        foreach($guid in @($entry.dependencyGuids|ForEach-Object {[string]$_}|Where-Object {$_ -match '^[0-9a-fA-F]{32}$'}|Sort-Object -Unique)){
            $key=$guid.ToLowerInvariant(); if(-not $map.ContainsKey($key)){$map[$key]=New-Object Collections.Generic.List[object]}
            if(-not ($map[$key] | Where-Object {$_.sourcePath -eq $p.sourcePath -and $_.objectStableId -eq $entry.stableId})){ $map[$key].Add([ordered]@{sourcePath=$p.sourcePath;sourceSha256=$p.sourceSha256;objectStableId=$entry.stableId}) ; $edgeCount++ }
        }
    }
}
$refs=@($map.Keys|Sort-Object|ForEach-Object {[ordered]@{guid=$_;referenceCount=$map[$_].Count;references=@($map[$_]|Sort-Object sourcePath,objectStableId)}})
$out=[ordered]@{schemaVersion=1;indexId='es-resource-reader.reference-index.v1';capturedUtc=[DateTime]::UtcNow.ToString('o');projectionCount=$projectionCount;guidCount=$refs.Count;edgeCount=$edgeCount;invalidProjectionCount=$invalidCount;references=$refs;nonClaims=@('semantic completeness','Unity import','runtime behavior','release')}
$dest=Join-Path $root $OutputPath; New-Item -ItemType Directory -Force -Path (Split-Path $dest)|Out-Null; $out|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $dest -Encoding UTF8; $out|ConvertTo-Json -Depth 12
