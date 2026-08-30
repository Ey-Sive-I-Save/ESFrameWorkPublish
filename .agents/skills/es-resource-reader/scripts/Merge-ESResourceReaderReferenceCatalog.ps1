[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]]$IndexPaths,
    [string[]]$SourceIds,
    [string]$ProjectRoot='.',
    [string]$OutputPath='ES/Output/ResourceReader/resource-reference-catalog.json',
    [string]$PreviousCatalogPath=''
)
$ErrorActionPreference='Stop'
if($SourceIds -and $SourceIds.Count -ne $IndexPaths.Count){throw 'SourceIds count must match IndexPaths count'}
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$map=@{}
$sourceCount=0
$edgeCount=0
for($i=0;$i -lt $IndexPaths.Count;$i++){
    $path=(Resolve-Path -LiteralPath $IndexPaths[$i]).Path
    $id=if($SourceIds){$SourceIds[$i]}else{[IO.Path]::GetFileNameWithoutExtension($path)}
    if([string]::IsNullOrWhiteSpace($id)){throw 'sourceId cannot be empty'}
    $index=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
    if($index.indexId -ne 'es-resource-reader.reference-index.v1'){throw "invalid reference index: $path"}
    $sourceCount++
    foreach($group in @($index.references)){
        if($null -eq $group){continue}
        $guid=([string]$group.guid).ToLowerInvariant()
        if($guid -notmatch '^[0-9a-f]{32}$'){continue}
        if(-not $map.ContainsKey($guid)){$map[$guid]=New-Object Collections.Generic.List[object]}
        foreach($ref in @($group.references)){
            if($null -eq $ref){continue}
            $exists=$map[$guid]|Where-Object {$_.sourceId -eq $id -and $_.sourcePath -eq $ref.sourcePath -and $_.objectStableId -eq $ref.objectStableId}
            if(-not $exists){
                $map[$guid].Add([ordered]@{sourceId=$id;sourcePath=$ref.sourcePath;sourceSha256=$ref.sourceSha256;objectStableId=$ref.objectStableId})
                $edgeCount++
            }
        }
    }
}
$references=@($map.Keys|Sort-Object|ForEach-Object {[ordered]@{guid=$_;referenceCount=$map[$_].Count;references=@($map[$_]|Sort-Object sourceId,sourcePath,objectStableId)}})
$conflicts=@($references|ForEach-Object {
    $items=@($_.references)
    $hashes=@($items|ForEach-Object {[string]$_.sourceSha256}|Where-Object {$_}|Sort-Object -Unique)
    $paths=@($items|ForEach-Object {[string]$_.sourcePath}|Where-Object {$_}|Sort-Object -Unique)
    if($hashes.Count -gt 1 -or $paths.Count -gt 1){[ordered]@{guid=$_.guid;sourceCount=@($items|ForEach-Object {$_.sourceId}|Sort-Object -Unique).Count;hashCount=$hashes.Count;hashes=$hashes;paths=$paths}}
})
$currentKeys=@{}
foreach($group in $references){foreach($item in @($group.references)){$key="$($group.guid)|$($item.sourceId)|$($item.sourcePath)|$($item.objectStableId)";$currentKeys[$key]=[string]$item.sourceSha256}}
$added=0;$removed=0;$changed=0;$baselineHash='';$baselinePresent=$false
if(-not [string]::IsNullOrWhiteSpace($PreviousCatalogPath)){
    $previousFull=Join-Path $root $PreviousCatalogPath
    if(Test-Path -LiteralPath $previousFull -PathType Leaf){
        $baselinePresent=$true;$baselineHash=(Get-FileHash -LiteralPath $previousFull -Algorithm SHA256).Hash.ToLowerInvariant()
        $previous=Get-Content -LiteralPath $previousFull -Raw -Encoding UTF8|ConvertFrom-Json
        $previousKeys=@{}
        foreach($group in @($previous.references)){foreach($item in @($group.references)){$key="$($group.guid)|$($item.sourceId)|$($item.sourcePath)|$($item.objectStableId)";$previousKeys[$key]=[string]$item.sourceSha256}}
        foreach($key in $currentKeys.Keys){if(-not $previousKeys.ContainsKey($key)){$added++}elseif($previousKeys[$key] -ne $currentKeys[$key]){$changed++}}
        foreach($key in $previousKeys.Keys){if(-not $currentKeys.ContainsKey($key)){$removed++}}
    }
}
$out=[ordered]@{
    schemaVersion=1;catalogId='es-resource-reader.reference-catalog.v1';capturedUtc=[DateTime]::UtcNow.ToString('o');sourceCount=$sourceCount;guidCount=$references.Count;edgeCount=$edgeCount
    conflictCount=$conflicts.Count;conflicts=$conflicts
    changeSummary=[ordered]@{baselinePresent=$baselinePresent;baselineSha256=$baselineHash;addedCount=$added;removedCount=$removed;changedCount=$changed}
    references=$references;nonClaims=@('semantic completeness','Unity import','runtime behavior','release')
}
$dest=Join-Path $root $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path $dest)|Out-Null
$out|ConvertTo-Json -Depth 14|Set-Content -LiteralPath $dest -Encoding UTF8
$out|ConvertTo-Json -Depth 14
