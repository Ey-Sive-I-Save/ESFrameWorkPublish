[CmdletBinding()]
param([string]$SkillRoot = (Join-Path (Get-Location) '.agents/skills/es-game-core-loop-validation'))
$ErrorActionPreference='Stop';$sm=Join-Path $SkillRoot 'references/bindings/state-machine.binding.json';$invoke=Join-Path $SkillRoot 'scripts/Invoke-ESGameCoreLoopStateMachine.ps1'
$ok=& $invoke -StateMachinePath $sm -CurrentState intake -Event advance -Outputs @{goalRevision='g';authorization='a';scope='s'}|ConvertFrom-Json
if($ok.to -ne 'authority-locked'){throw 'STATE_MACHINE_VALID_ADVANCE_FAILED'}
$special=& $invoke -StateMachinePath $sm -CurrentState fanout-running -Event cancel -Outputs @{cancelReceipt='cancel'}|ConvertFrom-Json
if($special.transitionType -ne 'cancel' -or $special.to -ne 'closed'){throw 'STATE_MACHINE_CANCEL_FAILED'}
$badEvent=$false;try{& $invoke -StateMachinePath $sm -CurrentState intake -Event cancel -Outputs @{cancelReceipt='cancel'}|Out-Null}catch{$badEvent=$true};if(-not $badEvent){throw 'STATE_MACHINE_SOURCE_GUARD_FAILED'}
$failed=$false;try{& $invoke -StateMachinePath $sm -CurrentState intake -Event advance -Outputs @{goalRevision='g'}|Out-Null}catch{$failed=$true};if(-not $failed){throw 'STATE_MACHINE_REQUIRED_OUTPUT_NOT_ENFORCED'}
$failure=& $invoke -StateMachinePath $sm -CurrentState fanout-running -Event failure -Outputs @{failureReceipt='failed'}|ConvertFrom-Json
if($failure.to -ne 'evidence-joined'){throw 'STATE_MACHINE_FAILURE_EDGE_FAILED'}
$conflict=& $invoke -StateMachinePath $sm -CurrentState evidence-joined -Event conflict -Outputs @{conflictReceipt='conflict'}|ConvertFrom-Json
if($conflict.to -ne 'authority-locked'){throw 'STATE_MACHINE_CONFLICT_EDGE_FAILED'}
$stale=& $invoke -StateMachinePath $sm -CurrentState snapshot-frozen -Event staleSnapshot -Outputs @{staleReceipt='stale'}|ConvertFrom-Json
if($stale.to -ne 'snapshot-frozen'){throw 'STATE_MACHINE_STALE_EDGE_FAILED'}
$missingSpecial=$false;try{& $invoke -StateMachinePath $sm -CurrentState evidence-joined -Event conflict -Outputs @{}|Out-Null}catch{$missingSpecial=$true};if(-not $missingSpecial){throw 'STATE_MACHINE_SPECIAL_RECEIPT_NOT_ENFORCED'}
[ordered]@{status='passed';normal='intake->authority-locked';special=@('failure->evidence-joined','conflict->authority-locked','staleSnapshot->snapshot-frozen','cancel->closed');negative=@('missing-required-output-rejected','missing-special-receipt-rejected');deterministic=$true}|ConvertTo-Json -Depth 8
