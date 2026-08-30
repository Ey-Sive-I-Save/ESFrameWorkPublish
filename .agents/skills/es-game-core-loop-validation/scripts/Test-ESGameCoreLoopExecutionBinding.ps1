[CmdletBinding()]
param()
$ErrorActionPreference='Stop';$root=Split-Path -Parent $PSScriptRoot;$x=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'references/bindings/execution.binding.json')|ConvertFrom-Json;$required=@('intake','authority-locked','snapshot-frozen','evidence-joined','final-decision','closed');$found=@($x.operations|ForEach-Object state)
foreach($s in $required){if($found -notcontains $s){throw "EXECUTION_STATE_MAPPING_MISSING:$s"}}
foreach($f in @('taskId','expectedTaskRevision','expectedContextVersion','idempotencyKey')){if(@($x.requestRequirements) -notcontains $f){throw "EXECUTION_REQUEST_FIELD_MISSING:$f"}}
foreach($op in @($x.operations|Where-Object authorization -eq 'explicit-user')){if(-not $op.casRequired){throw "EXPLICIT_OPERATION_WITHOUT_CAS:$($op.action)"}}
$allowed=@('Create','Get','VerifySources','SubmitEvidence','Evaluate','Complete','SetDelivery','Transition','Integrity');foreach($op in $x.operations){if($op.facadeAction -and $allowed -notcontains [string]$op.facadeAction){throw "FACADE_ACTION_UNSUPPORTED:$($op.facadeAction)"}};if(@($x.operations|Where-Object {$_.function -eq 'Invoke-ESTaskContextTransition'}).Count -lt 5){throw 'RECOVERY_TRANSITION_BINDING_INCOMPLETE'}
if($x.runtimeGates.unity -ne 'explicit-user' -or $x.runtimeGates.playmode -ne 'explicit-user' -or $x.runtimeGates.profiler -ne 'explicit-user'){throw 'RUNTIME_GATE_NOT_EXPLICIT'}
if($x.failureRecovery.casConflict -ne 're-read-and-replan' -or $x.failureRecovery.stalePlan -ne 'reject-and-regenerate'){throw 'RECOVERY_POLICY_INCOMPLETE'}
[ordered]@{status='passed';operationCount=@($x.operations).Count;explicitOperations=@($x.operations|Where-Object authorization -eq 'explicit-user'|ForEach-Object action);casBoundExplicitOperations=@($x.operations|Where-Object {$_.authorization -eq 'explicit-user' -and $_.casRequired}|ForEach-Object action);runtimeGates=$x.runtimeGates;runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 8
