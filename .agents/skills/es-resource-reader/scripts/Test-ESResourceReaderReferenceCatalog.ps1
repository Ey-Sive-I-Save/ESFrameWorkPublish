[CmdletBinding()]
param([string]$CatalogPath='ES/Output/ResourceReader/resource-reference-catalog.json')
$ErrorActionPreference='Stop'
$obj=Get-Content -LiteralPath (Resolve-Path -LiteralPath $CatalogPath)-Raw -Encoding UTF8|ConvertFrom-Json
$issues=New-Object Collections.Generic.List[string]
if($obj.catalogId -ne 'es-resource-reader.reference-catalog.v1'){$issues.Add('invalid catalogId')}
$groups=@($obj.references)
if($obj.guidCount -ne $groups.Count){$issues.Add('guidCount mismatch')}
$edges=0;$previous=''
foreach($group in $groups){
    if($group.guid -notmatch '^[a-f0-9]{32}$'){$issues.Add('invalid guid')}
    if($group.referenceCount -ne @($group.references).Count){$issues.Add('referenceCount mismatch')}
    if($previous -and [string]::CompareOrdinal($previous,$group.guid)-ge 0){$issues.Add('guid groups not sorted')}
    $previous=$group.guid
    foreach($ref in @($group.references)){
        if([string]::IsNullOrWhiteSpace($ref.sourceId)-or [string]::IsNullOrWhiteSpace($ref.sourcePath)-or $ref.sourceSha256 -notmatch '^[a-f0-9]{64}$'){$issues.Add('invalid source reference')}
        $edges++
    }
}
if($edges -ne [int]$obj.edgeCount){$issues.Add('edgeCount mismatch')}
$conflicts=@($obj.conflicts)
if($obj.conflictCount -ne $conflicts.Count){$issues.Add('conflictCount mismatch')}
foreach($conflict in $conflicts){if($conflict.guid -notmatch '^[a-f0-9]{32}$' -or @($conflict.hashes).Count -lt 2){$issues.Add('invalid conflict entry')}}
if($null -eq $obj.changeSummary){$issues.Add('missing changeSummary')}else{foreach($field in @('baselinePresent','baselineSha256','addedCount','removedCount','changedCount')){if($null -eq $obj.changeSummary.PSObject.Properties[$field]){$issues.Add("changeSummary missing: $field")}}}
$out=[ordered]@{validator='Test-ESResourceReaderReferenceCatalog';valid=($issues.Count -eq 0);sourceCount=$obj.sourceCount;guidCount=$groups.Count;edgeCount=$edges;conflictCount=$conflicts.Count;changeSummary=$obj.changeSummary;issueCount=$issues.Count;issues=$issues;runtimeStatus='runtime-not-run'}
$out|ConvertTo-Json -Depth 8
if($issues.Count){exit 1}else{exit 0}
