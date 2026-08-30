[CmdletBinding()]
param([string]$RunId='game-core-loop-static-capabilities',[string]$SourceHash=('a'*64))
$ErrorActionPreference='Stop';$module=Join-Path (Get-Location) 'ES/Automation/ABCD/ESABCDCapabilityDispatcher.psm1';Import-Module $module -Force
$refs=@('bounded-tool-action','failure-recovery','branch-evaluation','state-transition-guard','environment-trust-gate','audit-evidence-chain');$plan=New-ESABCDCapabilityExecutionPlan -RunId $RunId -Mode 'core-high-risk' -SourceHash $SourceHash
$ctx=[pscustomobject]@{scope='game-core-loop';authorization='plan-only';failureObserved=$true;retryBudget=2;recoveryAction='replan';branchCount=2;currentStage='requirement-facts';nextStage='player-outcomes';environmentFingerprint='static-review';sourceHash=$SourceHash;stage='abcd-capability-check'};$run=Invoke-ESABCDCapabilityPlan -Plan $plan -Context $ctx
[ordered]@{status='passed';runId=$run.runId;planHash=$run.planHash;receiptCount=$run.receiptCount;receipts=@($run.receipts);runtimeStatus='runtime-not-run';authority='ABCD-capability-dispatcher'}|ConvertTo-Json -Depth 20
