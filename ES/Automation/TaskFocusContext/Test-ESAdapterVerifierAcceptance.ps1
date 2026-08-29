[CmdletBinding()]param()
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'ESAdapterEvidenceBridge.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\TaskContextRuntime\ESTaskContextRuntime.psm1') -Force
. (Join-Path $PSScriptRoot '..\TaskContextRuntime\Test-ESTaskContextRoutePlanFixture.ps1')
$root=Join-Path ([IO.Path]::GetTempPath()) ('es-adapter-verifier-acceptance-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root|Out-Null;Initialize-ESTestRoutePlanRepository $root
[IO.File]::WriteAllText((Join-Path $root 'source.txt'),'adapter-source',[Text.UTF8Encoding]::new($false))
$adapterId='es-adapter.langgraph.v1';$claimId='adapter.'+$adapterId
$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal-adapter-verifier' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'adapter verifier acceptance' -Budget ([ordered]@{maxReads=8})
$route=New-ESTestRoutePlan -Root $root -Goal $goal
$s=New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'adapter-verifier-task' -PlanHash $route.routePlanHash -RoutePlanPath $route.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim $claimId -RequiredClaimVerifier ([ordered]@{$claimId='es-adapter-observation-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'
$s=Confirm-ESTaskSourceScope -ProjectRoot $root -StoreRoot 'state' -TaskId $s.taskId -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'verify'
$e=New-ESAdapterEvidenceSet -TaskId $s.taskId -AdapterId $adapterId -AdapterVersion '0.1.0' -Observation @{eventType='AdapterObservation';adapterId=$adapterId;focusRevision=1}
$s=Submit-ESAdapterEvidenceToTaskContext -ProjectRoot $root -StoreRoot 'state' -TaskId $s.taskId -AdapterEvidence $e -SourceScopeHash $s.verifiedSourceScopeHash -ClaimId $claimId -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'adapter-evidence'
Import-Module (Join-Path $PSScriptRoot '..\TaskContextRuntime\ESTaskContextRuntime.psm1') -Force
$item=$s.evidenceSet.items[0];$completion=Complete-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId $s.taskId -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'complete'
$ok=([string]$item.outcome -eq 'passed' -and [string]$item.verifierId -eq 'es-adapter-observation-v1' -and [string]$item.verificationStatus -eq 'verified' -and [string]$completion.completionDecision -eq 'accepted')
[pscustomobject]@{schemaVersion=1;validator='Test-ESAdapterVerifierAcceptance';status=$(if($ok){'passed'}else{'failed'});claimId=$claimId;verifierId=$item.verifierId;verificationStatus=$item.verificationStatus;completionDecision=$completion.completionDecision;taskRevision=$completion.taskRevision;runtimeStatus='runtime-not-run';claimsNotProven=@('External framework runtime','Unity/host runtime','release acceptance')}|ConvertTo-Json -Depth 10
if(-not$ok){exit 1}
