[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$IndexPath,
    [string[]]$SourceId,
    [string]$OutputPath='ES/Output/ResourceReader/resource-catalog.json'
)
$ErrorActionPreference='Stop'; if($SourceId.Count -gt 0 -and $SourceId.Count -ne $IndexPath.Count){throw 'SourceId count must match IndexPath count.'}
$items=@(); $seen=@{}; for($i=0;$i -lt $IndexPath.Count;$i++){
    $path=(Resolve-Path -LiteralPath $IndexPath[$i]).Path; $index=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop
    if($index.indexId -ne 'es-resource-reader.resource-index.v1'){throw "Unsupported index: $path"}
    $id=if($SourceId.Count -gt 0 -and $SourceId[$i]){$SourceId[$i]}else{[IO.Path]::GetFileNameWithoutExtension($path)}
    if($id -notmatch '^[A-Za-z0-9._-]{1,64}$'){throw "Unsafe SourceId: $id"}
    foreach($entry in @($index.items)){
        if($null -eq $entry){continue}; $key="$id|$($entry.sourcePath)"; if($seen.ContainsKey($key)){continue}; $seen[$key]=$true
        $items+=[ordered]@{globalKey=$key;sourceId=$id;sourceIndexPath=$path;sourcePath=$entry.sourcePath;sourceSha256=$entry.sourceSha256;detectedFormat=$entry.detectedFormat;parserId=$entry.parserId;parserVersion=$entry.parserVersion;byteCount=$entry.byteCount;entryCount=$entry.entryCount;status=$entry.status}
    }
}
$items=@($items|Sort-Object globalKey);$out=[ordered]@{schemaVersion=1;catalogId='es-resource-reader.resource-catalog.v1';capturedUtc=[DateTime]::UtcNow.ToString('o');sourceCount=$IndexPath.Count;itemCount=$items.Count;sources=@($IndexPath|ForEach-Object{(Resolve-Path -LiteralPath $_).Path});items=$items;nonClaims=@('source freshness beyond index capture','Unity import','runtime loading','release readiness')};$dest=Join-Path (Resolve-Path '.').Path $OutputPath;New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest)|Out-Null;[IO.File]::WriteAllText($dest,($out|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)));$out|ConvertTo-Json -Depth 12
