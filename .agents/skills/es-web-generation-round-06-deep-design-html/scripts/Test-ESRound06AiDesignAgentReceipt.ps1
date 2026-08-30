[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$TaskPath,
 [Parameter(Mandatory=$true)][string[]]$ReceiptPaths,
 [string]$ProjectRoot
)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path}else{$ProjectRoot=(Resolve-Path $ProjectRoot).Path}
function ReadJ([string]$p){if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw "blocked.round-06.ai-agent.missing-file:$p"};[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Resolve-Path $p).Path))|ConvertFrom-Json}
function Full([string]$p){if([IO.Path]::IsPathRooted($p)){return [IO.Path]::GetFullPath($p)};return [IO.Path]::GetFullPath((Join-Path $ProjectRoot $p))}
function Inside([string]$path,[string[]]$roots){$f=(Full $path).TrimEnd('\');foreach($r in $roots){$rr=(Full $r).TrimEnd('\');if($f.Equals($rr,[StringComparison]::OrdinalIgnoreCase) -or $f.StartsWith($rr+'\',[StringComparison]::OrdinalIgnoreCase)){return $true}};return $false}
function HashFile([string]$p){(Get-FileHash (Full $p) -Algorithm SHA256).Hash.ToLowerInvariant()}
function Require-Meaningful([object]$value,[string]$name,[int]$min=24){if([string]::IsNullOrWhiteSpace([string]$value) -or ([string]$value).Trim().Length -lt $min){throw "blocked.round-06.ai-agent.shallow-evidence:$name"}}
$task=ReadJ $TaskPath
if([string]$task.recordType -cne 'AiWebDesignTask'){throw 'blocked.round-06.ai-agent.invalid-task-record'}
if(@($task.upstreamRounds).Count -ne 5){throw 'blocked.round-06.ai-agent.upstream-rounds-incomplete'}
$roundNumbers=@($task.upstreamRounds|ForEach-Object{[int]$_.round});if(@($roundNumbers|Sort-Object -Unique).Count -ne 5 -or (1..5|Where-Object{$roundNumbers -notcontains $_}).Count -gt 0){throw 'blocked.round-06.ai-agent.upstream-rounds-not-1-to-5'}
foreach($u in @($task.upstreamRounds)){if(-not(Test-Path -LiteralPath (Full $u.path) -PathType Leaf)){throw "blocked.round-06.ai-agent.upstream-missing:$($u.path)"};if((HashFile $u.path) -cne ([string]$u.hash).ToLowerInvariant()){throw "blocked.round-06.ai-agent.upstream-hash-mismatch:$($u.path)"}}
if(@($task.allowedWriteRoots).Count -lt 1 -or @($task.requiredReads).Count -lt 1 -or @($task.requiredChecks).Count -lt 1){throw 'blocked.round-06.ai-agent.task-incomplete'}
$seen=@{};$out=@();$prev=$null
foreach($rp in $ReceiptPaths){
 $r=ReadJ $rp
 if([string]$r.recordType -cne 'AiWebDesignRevisionReceipt'){throw "blocked.round-06.ai-agent.invalid-receipt:$rp"}
 if([string]$r.taskId -cne [string]$task.taskId){throw "blocked.round-06.ai-agent.task-mismatch:$rp"}
 if($null -ne $seen[[int]$r.round]){throw "blocked.round-06.ai-agent.duplicate-round:$($r.round)"};$seen[[int]$r.round]=$true
 foreach($p in @($r.writes)+@($r.changedFiles)+@($r.artifactPath)){
  if([string]::IsNullOrWhiteSpace([string]$p)){continue}
  $isReceipt=([string]$p -match '(?i)(bootstrap|receipt|ai-revision).*\.json$') -or ([string]$p -eq [string]$rp)
  if(-not $isReceipt -and -not(Inside $p @($task.allowedWriteRoots))){throw "blocked.round-06.ai-agent.write-outside-allowlist:$p"}
 }
 if(@($r.reads).Count -lt 1 -or @($r.writes).Count -lt 1 -or @($r.changedFiles).Count -lt 1 -or @($r.changedRegions).Count -lt 1 -or @($r.nextHypotheses).Count -lt 1){throw "blocked.round-06.ai-agent.missing-evidence:$rp"}
 Require-Meaningful $r.designDecision 'designDecision'
 Require-Meaningful $r.implementationSummary 'implementationSummary'
 Require-Meaningful (($r.changedRegions|ConvertTo-Json -Compress)) 'changedRegions'
 Require-Meaningful (($r.nextHypotheses|ConvertTo-Json -Compress)) 'nextHypotheses'
 if($null -eq $r.PSObject.Properties['failureCauses'] -or [string]::IsNullOrWhiteSpace(($r.failureCauses|ConvertTo-Json -Compress))){throw "blocked.round-06.ai-agent.failure-causes-missing:$rp"}
 if([string]$r.designDecision -match '(?i)^(generated|implemented|done|completed|looks good)$' -or [string]$r.implementationSummary -match '(?i)^(generated|implemented|done|completed|looks good)$'){throw "blocked.round-06.ai-agent.generic-evidence:$rp"}
 $specialField=@{2='interactionEvidence';3='motionEvidence';4='contentEvidence';5='styleEvidence'}[[int]$r.round]
 if($specialField -and ($null -eq $r.PSObject.Properties[$specialField] -or $null -eq $r.$specialField -or [string]::IsNullOrWhiteSpace(($r.$specialField|ConvertTo-Json -Compress)))){throw "blocked.round-06.ai-agent.round-specific-evidence-missing:$($r.round)"}
 if([string]$r.designDecision -match '^(none|n/a|modelCalls|generated)$' -or [string]$r.implementationSummary -match '^(none|n/a|modelCalls|generated)$'){throw "blocked.round-06.ai-agent.fake-decision:$rp"}
 $artifact=Full $r.artifactPath;if(-not(Test-Path -LiteralPath $artifact -PathType Leaf)){throw "blocked.round-06.ai-agent.artifact-missing:$($r.artifactPath)"}
 $actual=HashFile $artifact;if($actual -cne ([string]$r.sourceHashAfter).ToLowerInvariant()){throw "blocked.round-06.ai-agent.after-hash-mismatch:$($r.artifactPath)"}
 if($null -ne $prev -and ([string]$r.sourceHashBefore).ToLowerInvariant() -ne $prev){throw "blocked.round-06.ai-agent.before-hash-chain:$($r.round)"}
 $prev=$actual;$out+=[ordered]@{round=[int]$r.round;receiptPath=(Full $rp);artifactPath=$artifact;artifactHash=$actual;changedFiles=@($r.changedFiles);changedRegions=@($r.changedRegions)}
}
if($out.Count -lt 1){throw 'blocked.round-06.ai-agent.no-real-revisions'}
[pscustomobject]@{status='admitted';recordType='AiWebDesignAgentAdmission';taskId=[string]$task.taskId;revisionCount=$out.Count;revisions=$out;nonClaims=@('does-not prove model identity','does-not prove browser/runtime/build success')}
