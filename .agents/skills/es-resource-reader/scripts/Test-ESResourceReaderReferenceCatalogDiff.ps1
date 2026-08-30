[CmdletBinding()]
param([string]$DiffPath='ES/Output/ResourceReader/resource-reference-catalog-diff.json')
$ErrorActionPreference='Stop';$obj=Get-Content -LiteralPath (Resolve-Path -LiteralPath $DiffPath)-Raw -Encoding UTF8|ConvertFrom-Json;$issues=New-Object Collections.Generic.List[string]
if($obj.diffId -ne 'es-resource-reader.reference-catalog-diff.v1'){$issues.Add('invalid diffId')}
foreach($field in @('baselineSha256','currentSha256')){if($obj.$field -notmatch '^[a-f0-9]{64}$'){$issues.Add("invalid $field")}}
foreach($pair in @(@('addedCount','added'),@('removedCount','removed'),@('changedCount','changed'))){$count=[int]$obj.($pair[0]);$items=@($obj.($pair[1]));if($count -lt $items.Count){$issues.Add("$($pair[1]) exceeds count")};if($items.Count -gt [int]$obj.maxItems){$issues.Add("$($pair[1]) exceeds maxItems")}}
$out=[ordered]@{validator='Test-ESResourceReaderReferenceCatalogDiff';valid=($issues.Count -eq 0);addedCount=$obj.addedCount;removedCount=$obj.removedCount;changedCount=$obj.changedCount;issueCount=$issues.Count;issues=$issues;runtimeStatus='runtime-not-run'};$out|ConvertTo-Json -Depth 8;if($issues.Count){exit 1}else{exit 0}
