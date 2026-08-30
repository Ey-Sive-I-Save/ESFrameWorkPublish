[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$SourceRoot,
 [Parameter(Mandatory=$true)][string]$OutputPlanPath,
 [string]$MappingPath,
 [string[]]$RelativePaths,
 [ValidateRange(1,100)][int]$MaxFilesPerBatch=50,
 [ValidateRange(65536,10485760)][int64]$MaxBytesPerBatch=1048576
)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=(Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd('\')
$replacementRules=@();if($MappingPath){$map=Get-Content -Raw -Encoding UTF8 -LiteralPath $MappingPath|ConvertFrom-Json;$deny=@('transition','definition','version','source','mode','registry','provider','window','app');$replacementRules=@($map.textReplacements|Where-Object{$_.source -and $_.es -and $deny -notcontains ([string]$_.source).ToLowerInvariant()}|Sort-Object{$_.source.Length} -Descending)}
$processed=@{};$receiptDir=Join-Path $root '.es-migration\batches';if(Test-Path $receiptDir){Get-ChildItem -LiteralPath $receiptDir -Filter '*.json' -File|ForEach-Object{try{$r=Get-Content -Raw -Encoding UTF8 $_.FullName|ConvertFrom-Json;foreach($row in @($r.rows)){$processed[[string]$row.relativePath.Replace('\','/')]=[string]$_.Name}}catch{}}}
$candidates=@()
if($RelativePaths){$candidates=@($RelativePaths|ForEach-Object{Join-Path $root $_.Replace('/','\')})}
else{$candidates=@(Get-ChildItem -LiteralPath $root -Recurse -File -Force|ForEach-Object{$_.FullName})}
$rows=@();$skipped=@()
foreach($path in $candidates){
  $full=[IO.Path]::GetFullPath($path)
  if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path escapes SourceRoot: $path"}
  $rel=$full.Substring($root.Length+1).Replace('\','/')
  if($rel -match '(^|/)\.git(/|$)|(^|/)\.es-migration(/|$)|(^|/)src/pro(/|$)|(^|/)(LICENSE|NOTICE)'){ $skipped+=[ordered]@{relativePath=$rel;reason='protected-or-control-path'};continue }
  if($processed.ContainsKey($rel)){$skipped+=[ordered]@{relativePath=$rel;reason='already-processed';receipt=$processed[$rel]};continue}
  try{$raw=[IO.File]::ReadAllBytes($full);if($raw -contains 0){throw 'binary'};$text=[Text.UTF8Encoding]::new($false,$true).GetString($raw)}catch{$skipped+=[ordered]@{relativePath=$rel;reason='not-strict-utf8-text'};continue}
  if($replacementRules.Count -gt 0){$candidate=$text;foreach($rule in $replacementRules){$candidate=[regex]::Replace($candidate,'(?<!ES)'+[regex]::Escape([string]$rule.source),[string]$rule.es)};if($candidate -eq $text){$skipped+=[ordered]@{relativePath=$rel;reason='already-compliant'};continue}}
  elseif(-not [regex]::IsMatch($text,'(?i)(?:\bDyad\b|\bWillchen\b|ESESDYAD|EStransition-)')){$skipped+=[ordered]@{relativePath=$rel;reason='already-compliant'};continue}
  $parts=$rel.Split('/');if($parts.Count -gt 1){$moduleGroup=$parts[0]+'/'+$parts[1]}else{$moduleGroup=$parts[0]}
  $rows+=[ordered]@{relativePath=$rel;group=$moduleGroup;bytes=$raw.Length}
}
$batches=@();foreach($group in ($rows|Group-Object -Property {$_['group']}|Sort-Object Name)){$items=@($group.Group|Sort-Object {$_['relativePath']});$cursor=0;$part=1;while($cursor -lt $items.Count){$chunk=@();$sumBytes=[int64]0;while($cursor -lt $items.Count -and $chunk.Count -lt $MaxFilesPerBatch){$candidate=[int64]$items[$cursor].bytes;if($chunk.Count -gt 0 -and ($sumBytes+$candidate) -gt $MaxBytesPerBatch){break};$chunk+=$items[$cursor];$sumBytes+=$candidate;$cursor++};$safeName=($group.Name -replace '[^A-Za-z0-9_-]','-');$batches+=[ordered]@{batchId=('planned-{0}-{1:d2}' -f $safeName,$part);group=$group.Name;fileCount=$chunk.Count;relativePaths=@($chunk|ForEach-Object{$_['relativePath']});estimatedBytes=$sumBytes};$part++}}
$plan=[ordered]@{schemaVersion=1;planner='es-migration-batch-planner';sourceRootRelativeOnly=$true;maxFilesPerBatch=$MaxFilesPerBatch;maxBytesPerBatch=$MaxBytesPerBatch;deterministic=$true;grouping='top-level/second-level directory preserves assembly/document/module boundaries';fileCount=$rows.Count;batchCount=$batches.Count;batches=$batches;skipped=$skipped;generatedUtc=[DateTime]::UtcNow.ToString('o')}
$out=[IO.Path]::GetFullPath($OutputPlanPath);$parent=Split-Path -Parent $out;if($parent){[IO.Directory]::CreateDirectory($parent)|Out-Null};[IO.File]::WriteAllText($out,($plan|ConvertTo-Json -Depth 10),(New-Object Text.UTF8Encoding($false)));$plan|ConvertTo-Json -Depth 3
