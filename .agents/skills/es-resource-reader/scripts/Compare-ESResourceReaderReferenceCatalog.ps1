[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BaselineCatalogPath,
    [Parameter(Mandatory=$true)][string]$CurrentCatalogPath,
    [string]$ProjectRoot='.',
    [string]$OutputPath='ES/Output/ResourceReader/resource-reference-catalog-diff.json',
    [int]$MaxItems=256
)
$ErrorActionPreference='Stop'
if($MaxItems -lt 1 -or $MaxItems -gt 4096){throw 'MaxItems must be between 1 and 4096'}
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
function Read-Catalog([string]$path){
    $full=(Resolve-Path -LiteralPath $path).Path
    $obj=Get-Content -LiteralPath $full -Raw -Encoding UTF8|ConvertFrom-Json
    if($obj.catalogId -ne 'es-resource-reader.reference-catalog.v1'){throw "invalid catalog: $path"}
    return $obj
}
function Get-Entries($catalog){
    $map=@{}
    foreach($group in @($catalog.references)){foreach($item in @($group.references)){
        $key="$($group.guid)|$($item.sourceId)|$($item.sourcePath)|$($item.objectStableId)"
        $map[$key]=[ordered]@{guid=$group.guid;sourceId=$item.sourceId;sourcePath=$item.sourcePath;objectStableId=$item.objectStableId;sourceSha256=$item.sourceSha256}
    }}
    return $map
}
$baseline=Read-Catalog $BaselineCatalogPath;$current=Read-Catalog $CurrentCatalogPath
$before=Get-Entries $baseline;$after=Get-Entries $current
$added=@();$removed=@();$changed=@()
foreach($key in $after.Keys){if(-not $before.ContainsKey($key)){$added+=$after[$key]}elseif($before[$key].sourceSha256 -ne $after[$key].sourceSha256){$changed+=[ordered]@{guid=$after[$key].guid;sourceId=$after[$key].sourceId;sourcePath=$after[$key].sourcePath;objectStableId=$after[$key].objectStableId;beforeSha256=$before[$key].sourceSha256;afterSha256=$after[$key].sourceSha256}}}
foreach($key in $before.Keys){if(-not $after.ContainsKey($key)){$removed+=$before[$key]}}
$sort={param($x) "$($x.guid)|$($x.sourceId)|$($x.sourcePath)|$($x.objectStableId)"}
$out=[ordered]@{
    schemaVersion=1;diffId='es-resource-reader.reference-catalog-diff.v1';capturedUtc=[DateTime]::UtcNow.ToString('o')
    baselineSha256=(Get-FileHash -LiteralPath (Resolve-Path -LiteralPath $BaselineCatalogPath) -Algorithm SHA256).Hash.ToLowerInvariant()
    currentSha256=(Get-FileHash -LiteralPath (Resolve-Path -LiteralPath $CurrentCatalogPath) -Algorithm SHA256).Hash.ToLowerInvariant()
    addedCount=$added.Count;removedCount=$removed.Count;changedCount=$changed.Count;maxItems=$MaxItems
    added=@($added|Sort-Object $sort|Select-Object -First $MaxItems)
    removed=@($removed|Sort-Object $sort|Select-Object -First $MaxItems)
    changed=@($changed|Sort-Object $sort|Select-Object -First $MaxItems)
    nonClaims=@('Unity import','runtime behavior','release')
}
$dest=Join-Path $root $OutputPath;New-Item -ItemType Directory -Force -Path (Split-Path $dest)|Out-Null;$out|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $dest -Encoding UTF8;$out|ConvertTo-Json -Depth 12
