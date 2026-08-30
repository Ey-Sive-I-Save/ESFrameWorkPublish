[CmdletBinding()]
param([Parameter(Mandatory)][string]$PlanPath,[Parameter(Mandatory)][string[]]$ReceiptPath,[string]$OutputPath)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
function Read-Json([string]$p){if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw "RECEIPT_INPUT_MISSING:$p"};Get-Content -LiteralPath $p -Raw -Encoding UTF8|ConvertFrom-Json}
$plan=Read-Json $PlanPath;$receipts=@($ReceiptPath|ForEach-Object{Read-Json $_});$requiredLayers=@('structure','implementation','presentation','performance');$findings=[Collections.Generic.List[object]]::new();$seen=@{}
foreach($r in $receipts){
  $key="$( [string]$r.taskId )/$( [string]$r.agentId )/$( [string]$r.layer )"
  if($seen.ContainsKey($key)){$findings.Add([pscustomobject]@{code='AGENT_RESULT_CONFLICT';detail="duplicate identity $key"})}else{$seen[$key]=$r}
  if([string]$r.taskId -cne [string]$plan.taskId){$findings.Add([pscustomobject]@{code='TASK_CONTEXT_BINDING_MISMATCH';detail=$key})}
  if([string]$r.planHash -cne [string]$plan.planHash){$findings.Add([pscustomobject]@{code='PLAN_HASH_MISMATCH';detail=$key})}
  if([string]$r.sourceSnapshotHash -cne [string]$plan.sourceSnapshotHash){$findings.Add([pscustomobject]@{code='SOURCE_SNAPSHOT_MISMATCH';detail=$key})}
  if([string]$r.runtimeStatus -in @('cancelled','late') -or [string]$r.status -eq 'late'){$findings.Add([pscustomobject]@{code='LATE_RESULT_ISOLATED';detail=$key})}
  foreach($f in @('taskId','layer','entryPoint','expected','observed','status','sourceHash','runtimeStatus','claimsNotProven','planHash','sourceSnapshotHash')){if($null -eq $r.PSObject.Properties[$f] -or [string]::IsNullOrWhiteSpace([string]$r.$f)){$findings.Add([pscustomobject]@{code='AGENT_RECEIPT_MISSING';detail="$key missing $f"})}}
  if([string]$r.sourceHash -notmatch '^[a-f0-9]{64}$'){$findings.Add([pscustomobject]@{code='SOURCE_HASH_INVALID';detail=$key})}
  if([string]$r.runtimeStatus -notin @('runtime-not-run','runtime-passed','cancelled','late')){$findings.Add([pscustomobject]@{code='RUNTIME_STATUS_INVALID';detail=$key})}
  if([string]$r.status -notin @('passed','failed','blocked','unverifiable','late','cancelled')){$findings.Add([pscustomobject]@{code='RECEIPT_STATUS_INVALID';detail=$key})}elseif([string]$r.status -in @('failed','blocked','unverifiable')){$findings.Add([pscustomobject]@{code='LAYER_NOT_PASSED';detail=$key})}
}
foreach($layer in $requiredLayers){if(-not @($receipts|Where-Object{[string]$_.layer -eq $layer -and [string]$_.status -notin @('late','cancelled')})){$findings.Add([pscustomobject]@{code='AGENT_RECEIPT_MISSING';detail="layer $layer"})}}
$runtimeMissing=@($receipts|Where-Object{[string]$_.runtimeStatus -eq 'runtime-not-run'}).Count;$decision=if($findings.Count){'unverifiable'}elseif($runtimeMissing -gt 0){'partial'}else{'aligned'}
$result=[ordered]@{schemaVersion=1;resultType='es-game-core-loop-evidence-join';taskId=[string]$plan.taskId;planHash=[string]$plan.planHash;sourceSnapshotHash=[string]$plan.sourceSnapshotHash;receiptCount=$receipts.Count;requiredAgentCount=@($plan.requiredAgents).Count;decision=$decision;findings=@($findings);runtimeStatus=if($runtimeMissing){'runtime-not-run'}else{'runtime-passed'};claimsNotProven=@($plan.nonClaims);recovery=if($findings.Count){@('replan','isolate-invalid-or-late-receipts')}else{@()};authority='ABCD-final-decision'}
$json=$result|ConvertTo-Json -Depth 20;if([string]::IsNullOrWhiteSpace($OutputPath)){$json}else{$full=[IO.Path]::GetFullPath($OutputPath);$parent=Split-Path -Parent $full;if($parent){New-Item -ItemType Directory -Force -Path $parent|Out-Null};[IO.File]::WriteAllText($full,$json,[Text.UTF8Encoding]::new($false));$result}
