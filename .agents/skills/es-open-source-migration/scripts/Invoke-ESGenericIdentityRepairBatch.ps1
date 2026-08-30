[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$SourceRoot,
 [Parameter(Mandatory=$true)][string[]]$RelativePaths,
 [string]$BatchId=('generic-repair-'+(Get-Date -Format 'yyyyMMdd-HHmmss')),
 [switch]$DryRun
)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
if(@($RelativePaths).Count -gt 50){throw "Batch exceeds resource limit: $(@($RelativePaths).Count) files (maximum 50). Split the batch."}
$root=(Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd('\');$rows=@();$changed=0;$bytes=0
function Get-Sha256([byte[]]$data){$h=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($h.ComputeHash($data))).Replace('-','').ToLowerInvariant()}finally{$h.Dispose()}}
foreach($rel in $RelativePaths){
 $path=[IO.Path]::GetFullPath((Join-Path $root $rel.Replace('/','\')))
 if(-not $path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path escapes SourceRoot: $rel"}
 if($rel -match '(^|[\/])\.git([\/]|$)|(^|[\/])src/pro([\/]|$)|(^|[\/])(LICENSE|NOTICE)'){throw "Protected path denied: $rel"}
 if(-not (Test-Path -LiteralPath $path -PathType Leaf)){throw "Batch file missing: $rel"}
 $raw=[IO.File]::ReadAllBytes($path);if($raw -contains 0){throw "Not strict UTF-8 text: $rel"}
 try{$text=[Text.UTF8Encoding]::new($false,$true).GetString($raw)}catch{throw "Not strict UTF-8 text: $rel"}
 $next=[regex]::Replace($text,'(?<![A-Za-z])EStransition(?=-)','transition')
 $next=[regex]::Replace($next,'(?i)(?:ES){2,}(?=DYAD)','ES')
 $afterBytes=[Text.UTF8Encoding]::new($false).GetBytes($next);$beforeSha=Get-Sha256 $raw;$afterSha=Get-Sha256 $afterBytes;$did=$beforeSha -ne $afterSha
 if($did -and -not $DryRun){[IO.File]::WriteAllBytes($path,$afterBytes)}
 if($did){$changed++;$bytes+=$afterBytes.Length}
 $rows+=[ordered]@{relativePath=$rel;changed=$did;beforeSha256=$beforeSha;afterSha256=$afterSha;bytes=$afterBytes.Length}
}
[ordered]@{schemaVersion=1;batchId=$BatchId;status=if($DryRun){'dry-run'}else{'complete'};fileCount=@($RelativePaths).Count;changedFiles=$changed;changedBytes=$bytes;resourceLimit=50;protectedExcluded=@('.git','LICENSE*','NOTICE*','src/pro/**');rows=$rows;generatedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 8
