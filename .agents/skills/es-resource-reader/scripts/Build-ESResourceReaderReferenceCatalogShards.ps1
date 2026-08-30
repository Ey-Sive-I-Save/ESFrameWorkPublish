[CmdletBinding()]
param(
    [string]$CatalogPath='ES/Output/ResourceReader/resource-reference-catalog.json',
    [string]$ProjectRoot='.',
    [string]$OutputDirectory='ES/Output/ResourceReader/reference-shards',
    [ValidateSet(1,2)][int]$PrefixLength=1
)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;$catalogFull=(Resolve-Path -LiteralPath $CatalogPath).Path
$catalog=Get-Content -LiteralPath $catalogFull -Raw -Encoding UTF8|ConvertFrom-Json
if($catalog.catalogId -ne 'es-resource-reader.reference-catalog.v1'){throw 'invalid reference catalog'}
$outDir=Join-Path $root $OutputDirectory;New-Item -ItemType Directory -Force -Path $outDir|Out-Null
$prefixes=if($PrefixLength -eq 1){0..15|ForEach-Object {[Convert]::ToString($_,16)}}else{0..255|ForEach-Object {[Convert]::ToString($_,16).PadLeft(2,'0')}}
$manifestShards=@()
foreach($prefix in $prefixes){
    $groups=@($catalog.references|Where-Object {([string]$_.guid).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)})
    $edgeCount=0;foreach($g in $groups){$edgeCount += @($g.references).Count}
    $fileName='shard-'+$prefix+'.json';$full=Join-Path $outDir $fileName
    $shard=[ordered]@{schemaVersion=1;shardId='es-resource-reader.reference-catalog-shard.v1';catalogSha256=(Get-FileHash -LiteralPath $catalogFull -Algorithm SHA256).Hash.ToLowerInvariant();prefix=$prefix;guidCount=$groups.Count;edgeCount=$edgeCount;references=$groups;nonClaims=@('semantic completeness','Unity import','runtime behavior','release')}
    $shard|ConvertTo-Json -Depth 14|Set-Content -LiteralPath $full -Encoding UTF8
    $manifestShards += [ordered]@{prefix=$prefix;path=($OutputDirectory.TrimEnd('/','\')+'/'+$fileName).Replace('\','/');guidCount=$groups.Count;edgeCount=$edgeCount;sha256=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$manifest=[ordered]@{schemaVersion=1;manifestId='es-resource-reader.reference-catalog-shards.v1';catalogPath=$CatalogPath.Replace('\','/');catalogSha256=(Get-FileHash -LiteralPath $catalogFull -Algorithm SHA256).Hash.ToLowerInvariant();prefixLength=$PrefixLength;shardCount=$manifestShards.Count;shards=$manifestShards;nonClaims=@('semantic completeness','Unity import','runtime behavior','release')}
$manifestPath=Join-Path $root ($OutputDirectory.TrimEnd('/','\')+'/manifest.json');$manifest|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $manifestPath -Encoding UTF8;$manifest|ConvertTo-Json -Depth 12
