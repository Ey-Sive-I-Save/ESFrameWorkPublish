[CmdletBinding()]param()
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'ESABCDCapabilityDispatcher.psm1') -Force
$h=('b'*64);$plan=New-ESABCDCapabilityExecutionPlan -RunId 'cap-test' -Mode 'core-high-risk' -SourceHash $h
$ctx=[pscustomobject]@{scope='design';authorization='user:current';sourceHash=$h;stage='player-replay';currentStage='player-replay';nextStage='counterplay-audit';branchCount=3;environmentFingerprint='env:test';failureObserved=$false}
$run=Invoke-ESABCDCapabilityPlan -Plan $plan -Context $ctx
$valid=@($run.receipts|Where-Object{(Test-ESABCDCapabilityReceipt -Receipt $_ -Plan $plan).status -eq 'passed'}).Count -eq 6
$recoveryCtx=[pscustomobject]@{scope='design';authorization='user:current';sourceHash=$h;stage='tree-expansion';branchCount=2;environmentFingerprint='env:test';failureObserved=$true;retryBudget=1;recoveryAction='replan'}
$recovery=Invoke-ESABCDCapabilityPlan -Plan $plan -Context $recoveryCtx
$recoveryObserved=@($recovery.receipts|Where-Object{$_.capabilityId -eq 'failure-recovery' -and $_.status -eq 'executed' -and $_.observations -match 'bounded recovery'}).Count -eq 1
$unknownRejected=$false;try{New-ESABCDCapabilityExecutionPlan -RunId 'bad' -Mode 'core-high-risk' -SourceHash $h -CapabilityRefs @('not-registered')|Out-Null}catch{if($_.Exception.Message -like 'ABCD_CAPABILITY_UNKNOWN:*'){$unknownRejected=$true}else{throw}}
$tampered=$run.receipts[0].PSObject.Copy();$tampered.planHash=('c'*64);$tamperRejected=(Test-ESABCDCapabilityReceipt -Receipt $tampered -Plan $plan).status -eq 'failed'
$targetRel='ES/Automation/Contracts/es-abcd-capability-receipt-v1.schema.json';$target=Join-Path (Get-Location).Path $targetRel;$content=Get-Content -LiteralPath $target -Raw -Encoding UTF8
$candidate=[pscustomobject]@{candidateId='candidate-action';proposedChanges=@([pscustomobject]@{path=$targetRel;afterContent=$content;changeId='candidate-action-noop'})}
$envelope=[pscustomobject]@{Status='candidate';CandidateSetHash=('d'*64);GenerationMode='engineering';Candidates=@($candidate)}
$actionCtx=[pscustomobject]@{scope='ES/Automation/Contracts';authorization='user:current';candidateEnvelope=$envelope;candidate=$candidate;scenario='DesignChange';currentHead=('a'*40);authorizationRef='user:current';sourceFiles=@($targetRel);allowedWriteScopes=@('ES/Automation/Contracts');projectRoot=(Get-Location).Path}
$action=Invoke-ESABCDBoundedPatchCandidateAction -Context $actionCtx
$boundedActionPassed=([string]$action.status -ceq 'candidate-only' -and [int]$action.operationCount -eq 1 -and -not [bool]$action.effects.writesAllowed)
$evidencePath=Join-Path (Get-Location).Path 'ES/Output/StaticReplay/es-abcd-capability-recovery-test.json';$saved=Save-ESABCDCapabilityRecoveryReceipt -CapabilityEvidence $recovery.receipts -Path $evidencePath;$restored=Restore-ESABCDCapabilityRecoveryReceipt -Path $evidencePath -ExpectedEvidenceHash $saved.evidenceHash
$recoveryReplayPassed=([string]$restored.status -ceq 'restored' -and [string]$restored.evidenceHash -ceq [string]$saved.evidenceHash -and @($restored.evidence).Count -eq @($recovery.receipts).Count)
$transitionValid=(Test-ESABCDStateTransition -CurrentStage 'tree-expansion' -NextStage 'global-convergence').status -ceq 'passed';$transitionInvalid=(Test-ESABCDStateTransition -CurrentStage 'tree-expansion' -NextStage 'final-decision').status -ceq 'failed';$transitionGuardPassed=$transitionValid -and $transitionInvalid
$responseQueue=@([pscustomobject]@{phase='seed-selection';generationMode='creative-divergence';round=0;outputs=@([pscustomobject]@{content='seed'})},[pscustomobject]@{phase='tree-expansion';generationMode='creative-divergence';round=1;outputs=@([pscustomobject]@{content='branch'})});$responseInvoker=New-ESABCDModelResponseInvoker -Responses $responseQueue;$responseSeed=@(& $responseInvoker ([pscustomobject]@{phase='seed-selection';generationMode='creative-divergence';round=0}));$responseBranch=@(& $responseInvoker ([pscustomobject]@{phase='tree-expansion';generationMode='creative-divergence';round=1}));$responseAdapterPassed=([string]$responseSeed[0].content -ceq 'seed' -and [string]$responseBranch[0].content -ceq 'branch')
$all=$valid -and $recoveryObserved -and $unknownRejected -and $tamperRejected -and $boundedActionPassed -and $recoveryReplayPassed -and $transitionGuardPassed -and $responseAdapterPassed
[pscustomobject]@{status=if($all){'passed'}else{'failed'};receiptCount=$run.receiptCount;validReceipts=$valid;failureRecoveryObserved=$recoveryObserved;unknownCapabilityRejected=$unknownRejected;tamperedReceiptRejected=$tamperRejected;boundedPatchActionCandidateOnly=$boundedActionPassed;recoveryCrossProcessReplay=$recoveryReplayPassed;stateTransitionGuard=$transitionGuardPassed;providerResponseAdapter=$responseAdapterPassed;planHash=$plan.planHash}
if(-not $all){exit 1}
