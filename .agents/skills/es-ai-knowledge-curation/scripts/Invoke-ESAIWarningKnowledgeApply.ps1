[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$CandidatePath,
    [string]$EntryBodyPath,
    [string]$IndexPatchPath,
    [switch]$Apply,
    [string]$ReceiptPath
)
$ErrorActionPreference='Stop'
$strict=[Text.UTF8Encoding]::new($false,$true); $plain=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
function Resolve-P([string]$p,[string]$label){if([string]::IsNullOrWhiteSpace($p)-or[IO.Path]::IsPathRooted($p)){throw "${label}_PATH_NOT_PROJECT_RELATIVE"};$f=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$p));if(!$f.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "${label}_PATH_OUTSIDE_PROJECT"};$f}
function HashBytes([byte[]]$b){$s=[Security.Cryptography.SHA256]::Create();try{([BitConverter]::ToString($s.ComputeHash($b))).Replace('-','').ToLowerInvariant()}finally{$s.Dispose()}}
function HF([string]$p){HashBytes([IO.File]::ReadAllBytes($p))}
function BodyH([string]$t){$n=$t-replace "`r`n","`n"-replace "`r","`n";$l=@($n-split "`n"|%{if($_-match '(?i)^\s*`?EntryBodyHash`?\s*\p{P}'){$null}else{$_.TrimEnd(' ',"`t")}}|?{$_ -ne $null});while($l.Count-and[string]::IsNullOrWhiteSpace($l[-1])){if($l.Count-eq 1){$l=@();break};$l=@($l[0..($l.Count-2)])};HashBytes([Text.Encoding]::UTF8.GetBytes($(if($l.Count){($l-join "`n")+"`n"}else{"`n"})))}
function Emit($state,$findings,$candHash,$tx){$o=[ordered]@{schemaVersion=1;recordType='AIWarningKnowledgeApplyReceipt';state=$state;candidateId=[string]$candidate.candidateId;candidateHash=$candHash;transactionExecuted=$tx;formalRegistration=if($tx){'applied'}else{'not-run'};findings=@($findings);nonClaims=@('Apply requires explicit -Apply and complete body/index inputs.','CAS and cooperative lock do not cover non-cooperating writers, process termination, machine failure or power loss.')}|ConvertTo-Json -Depth 20;if($ReceiptPath){$rf=Resolve-P $ReceiptPath 'RECEIPT';[IO.Directory]::CreateDirectory((Split-Path $rf))|Out-Null;[IO.File]::WriteAllText($rf,$o,$plain)}$o}
$find=[Collections.Generic.List[object]]::new();$cf=Resolve-P $CandidatePath 'CANDIDATE';$candidate=$strict.GetString([IO.File]::ReadAllBytes($cf))|ConvertFrom-Json;$ch=HF $cf
if($candidate.replay.candidateOnly-ne $true-or$candidate.replay.applyRequired-ne $true){$find.Add(@{code='APPLY_BOUNDARY_INVALID';message='Candidate is not candidate-only/apply-required.'})}
$wf=Resolve-P ([string]$candidate.sourceSnapshot.warningPath) 'WARNING';if(!(Test-Path -LiteralPath $wf -PathType Leaf)){$find.Add(@{code='WARNING_NOT_FOUND';message=$candidate.sourceSnapshot.warningPath})}elseif((HF $wf)-ne[string]$candidate.sourceSnapshot.warningHash){$find.Add(@{code='WARNING_HASH_DRIFT';message='Warning changed after candidate snapshot.'})}
if([string]::IsNullOrWhiteSpace($EntryBodyPath)-or[string]::IsNullOrWhiteSpace($IndexPatchPath)){if($Apply){$find.Add(@{code='APPLY_INPUT_REQUIRED';message='-Apply requires -EntryBodyPath and -IndexPatchPath.'})}}
$entry=Resolve-P ([string]$candidate.proposedEntry.targetPath) 'ENTRY';$index=Resolve-P 'Documentation/AIKnowledge/KnowledgeIndex.yaml' 'INDEX'
if($candidate.match.decision-eq'new'-and[ string]::IsNullOrWhiteSpace($EntryBodyPath)){$find.Add(@{code='ENTRY_CONTENT_REQUIRED';message='candidate-created requires Markdown body.'})}
if($EntryBodyPath){$eb=Resolve-P $EntryBodyPath 'ENTRY_BODY';if(!(Test-Path -LiteralPath $eb -PathType Leaf)){$find.Add(@{code='ENTRY_BODY_NOT_FOUND';message=$EntryBodyPath})}else{try{$entryText=$strict.GetString([IO.File]::ReadAllBytes($eb));if($candidate.proposedEntry.expectedHashes.entryBodyHash-and(BodyH $entryText)-ne[string]$candidate.proposedEntry.expectedHashes.entryBodyHash){$find.Add(@{code='ENTRY_BODY_HASH_MISMATCH';message='Supplied body does not match candidate expected EntryBodyHash.'})}}catch{$find.Add(@{code='ENTRY_BODY_UTF8_INVALID';message=$_.Exception.Message})}}}
if($IndexPatchPath){$ip=Resolve-P $IndexPatchPath 'INDEX_PATCH';if(!(Test-Path -LiteralPath $ip -PathType Leaf)){$find.Add(@{code='INDEX_PATCH_NOT_FOUND';message=$IndexPatchPath})}else{try{$patchText=$strict.GetString([IO.File]::ReadAllBytes($ip));$kid=[string]$candidate.proposedEntry.knowledgeId;if($patchText -notmatch [regex]::Escape($kid)){$find.Add(@{code='INDEX_PATCH_KNOWLEDGE_ID_MISMATCH';message='Index patch does not contain candidate KnowledgeId.'})};foreach($rk in @($candidate.proposedEntry.routeKeys)){if($patchText -notmatch [regex]::Escape([string]$rk)){$find.Add(@{code='INDEX_PATCH_ROUTE_MISMATCH';message="Index patch does not contain candidate RouteKey: $rk"})}};$warnPath=[string]$candidate.sourceSnapshot.warningPath;if($patchText -notmatch [regex]::Escape($warnPath)){$find.Add(@{code='INDEX_PATCH_SOURCE_MISMATCH';message='Index patch does not contain candidate Warning source path.'})}}catch{$find.Add(@{code='INDEX_PATCH_UTF8_INVALID';message=$_.Exception.Message})}}}
if(!$Apply){Emit $(if($find.Count){'blocked'}else{'preview-ready'}) $find $ch $false;return}
if($find.Count){Emit 'blocked' $find $ch $false;return}
$lockPath=Join-Path $root 'ES/Automation/Candidates/AIWarningKnowledge/.apply.lock';[IO.Directory]::CreateDirectory((Split-Path $lockPath))|Out-Null;$lock=$null;$written=@();$backups=@()
try{$lock=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
  # Re-read CAS while holding lock.
  if((HF $wf)-ne[string]$candidate.sourceSnapshot.warningHash){throw 'WARNING_HASH_DRIFT_UNDER_LOCK'}
  $targets=@(@{dest=$entry;src=(Resolve-P $EntryBodyPath 'ENTRY_BODY')},@{dest=$index;src=(Resolve-P $IndexPatchPath 'INDEX_PATCH')})
  foreach($t in $targets){$d=[string]$t.dest;$tmp="$d.apply-$([Guid]::NewGuid().ToString('N')).tmp";$bak="$d.apply-$([Guid]::NewGuid().ToString('N')).bak";$exists=Test-Path -LiteralPath $d;if($exists){[IO.File]::Copy($d,$bak,$true);$backups+=,$bak};[IO.File]::Copy([string]$t.src,$tmp,$true);$written+=,@{dest=$d;tmp=$tmp;exists=$exists;bak=$bak};}
  foreach($w in $written){if($w.exists){[IO.File]::Replace($w.tmp,$w.dest,$w.bak,$true)}else{[IO.File]::Move($w.tmp,$w.dest)}}
  foreach($b in $backups){Remove-Item -LiteralPath $b -Force -ErrorAction SilentlyContinue}
  Emit 'applied' @() $ch $true
}catch{foreach($w in $written){if(Test-Path -LiteralPath $w.dest){Remove-Item -LiteralPath $w.dest -Force -ErrorAction SilentlyContinue};if(Test-Path -LiteralPath $w.tmp){Remove-Item -LiteralPath $w.tmp -Force -ErrorAction SilentlyContinue}};foreach($b in $backups){$dest=$b -replace '\.apply-[0-9a-f]+\.bak$','';if(Test-Path -LiteralPath $b){[IO.File]::Copy($b,$dest,$true);Remove-Item -LiteralPath $b -Force -ErrorAction SilentlyContinue}};Emit 'blocked' @(@{code='APPLY_TRANSACTION_FAILED';message=$_.Exception.Message}) $ch $false}finally{if($lock){$lock.Dispose()}}

