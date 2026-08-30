[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SourceRoot)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=(Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd('\')
$dir=Join-Path $root '.es-migration\batches'
$receipts=@(Get-ChildItem -LiteralPath $dir -Filter '*.json' -File)
$seen=@{};$historical=@();$blocking=@();$total=0
foreach($file in $receipts){
  $r=Get-Content -Raw -Encoding UTF8 -LiteralPath $file.FullName|ConvertFrom-Json
  if([int]$r.fileCount -gt 50){$historical+="$($file.Name): fileCount > 50"}
  foreach($row in @($r.rows)){
    $rel=[string]$row.relativePath;$total++
    if($seen.ContainsKey($rel)){$historical+="$($file.Name): duplicate path $rel"}else{$seen[$rel]=$file.Name}
    $full=[IO.Path]::GetFullPath((Join-Path $root $rel.Replace('/','\')))
    if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){$blocking+="$($file.Name): path escape $rel"}
    if($rel -match '(^|[\/])\.git([\/]|$)|(^|[\/])src/pro([\/]|$)|(^|[\/])(LICENSE|NOTICE)'){$blocking+="$($file.Name): protected path $rel"}
  }
}
$status=if($blocking.Count -gt 0){'blocked'}elseif($historical.Count -gt 0){'review'}else{'passed'}
[ordered]@{schemaVersion=1;validator='es-migration-batch-ledger';status=$status;receiptCount=$receipts.Count;totalRows=$total;uniquePaths=$seen.Count;blockingIssues=$blocking;historicalIssues=$historical;issues=@($blocking+$historical);policy='New batches must use explicit paths and reject previously processed paths; historical duplicate/oversized receipts remain auditable but do not block new work.';generatedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 8
