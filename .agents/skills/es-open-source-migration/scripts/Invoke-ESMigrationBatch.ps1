[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$SourceRoot,
 [Parameter(Mandatory=$true)][string]$MappingPath,
 [Parameter(Mandatory=$true)][string[]]$RelativePaths,
 [string]$BatchId = ('batch-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
 [switch]$DryRun,
 [switch]$AllowExistingPath
)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=(Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd('\')
$map=Get-Content -Raw -Encoding UTF8 -LiteralPath $MappingPath|ConvertFrom-Json
# Generic language/UI tokens are not project identity and must never gain an ES prefix.
$genericDenylist=@('transition','definition','version','source','mode','registry','provider','window','app')
$repls=@($map.textReplacements|Where-Object {$_.source -and $_.es -and $genericDenylist -notcontains ([string]$_.source).ToLowerInvariant()})|Sort-Object {$_.source.Length} -Descending
$receiptDir=Join-Path $root '.es-migration\batches';$existing=@{};if(Test-Path $receiptDir){Get-ChildItem $receiptDir -Filter '*.json' -File|ForEach-Object{try{$old=Get-Content -Raw -Encoding UTF8 $_.FullName|ConvertFrom-Json;foreach($row in @($old.rows)){$existing[[string]$row.relativePath]=$_.Name}}catch{}}}
$rows=@();$changed=0;$bytes=0
foreach($rel in $RelativePaths){
 $path=[IO.Path]::GetFullPath((Join-Path $root $rel.Replace('/','\')))
 if(-not $path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path escapes SourceRoot: $rel"}
 if(-not (Test-Path -LiteralPath $path -PathType Leaf)){throw "Batch file missing: $rel"}
 if($existing.ContainsKey($rel) -and -not $AllowExistingPath){throw "Path already processed by $($existing[$rel]): $rel. Use -AllowExistingPath only for an explicit repair batch."}
 $raw=[IO.File]::ReadAllBytes($path);$text=$null
 try{$text=[Text.Encoding]::UTF8.GetString($raw);if($text.IndexOf([char]0) -ge 0){throw 'binary'}}catch{throw "Not strict UTF-8 text: $rel"}
 $before=[BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash($raw)).Replace('-','').ToLowerInvariant();$next=$text
 foreach($r in $repls){$pattern='(?<!ES)'+[regex]::Escape([string]$r.source);$next=[regex]::Replace($next,$pattern,[string]$r.es)}
 $afterBytes=[Text.UTF8Encoding]::new($false).GetBytes($next);$after=[BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash($afterBytes)).Replace('-','').ToLowerInvariant()
 $did=$before -ne $after;if($did){$changed++;$bytes+=$afterBytes.Length;if(-not $DryRun){[IO.File]::WriteAllBytes($path,$afterBytes)}}
 $rows += [ordered]@{relativePath=$rel;changed=$did;beforeSha256=$before;afterSha256=$after;bytes=$afterBytes.Length}
}
[ordered]@{schemaVersion=1;batchId=$BatchId;status=if($DryRun){'dry-run'}else{'complete'};sourceRootRelativeOnly=$true;fileCount=$rows.Count;changedFiles=$changed;changedBytes=$bytes;allowExistingPath=[bool]$AllowExistingPath;rows=$rows;protectedExcluded=@('.git','LICENSE*','NOTICE*','src/pro/**');generatedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 8
