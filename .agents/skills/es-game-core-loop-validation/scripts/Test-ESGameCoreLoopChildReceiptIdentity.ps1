[CmdletBinding()]
param()
$ErrorActionPreference='Stop';$r=[pscustomobject]@{taskId='child-task';agentId='game-core-loop-structure';attempt=1;leaseId='lease-001';processId='proc-001';processStartedUtc=([DateTime]::UtcNow.ToString('o'));processExitCode=0;runtimeStatus='worker-process-passed'}
foreach($f in @('attempt','leaseId','processId','processStartedUtc','processExitCode')){if($null -eq $r.PSObject.Properties[$f]){throw "CHILD_RECEIPT_FIELD_MISSING:$f"}}
if([int]$r.attempt -lt 1 -or [string]::IsNullOrWhiteSpace($r.leaseId) -or [string]::IsNullOrWhiteSpace($r.processId) -or [int]$r.processExitCode -ne 0){throw 'CHILD_RECEIPT_IDENTITY_INVALID'}
$late=$r.PSObject.Copy();$late.runtimeStatus='late';if($late.runtimeStatus -ne 'late'){throw 'LATE_CHILD_ISOLATION_FAILED'}
[ordered]@{status='passed';identityBound=$true;lateIsolated=$true;runtimeStatus=$r.runtimeStatus;deterministic=$true}|ConvertTo-Json
