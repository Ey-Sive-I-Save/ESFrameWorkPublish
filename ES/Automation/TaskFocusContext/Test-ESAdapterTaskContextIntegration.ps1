[CmdletBinding()]param()
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'ESAdapterEvidenceBridge.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\TaskContextRuntime\ESTaskContextRuntime.psm1') -Force
. (Join-Path $PSScriptRoot '..\TaskContextRuntime\Test-ESTaskContextRoutePlanFixture.ps1')
$root=Join-Path ([IO.Path]::GetTempPath()) ('es-adapter-task-context-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $root|Out-Null;Initialize-ESTestRoutePlanRepository $root;[IO.File]::WriteAllText((Join-Path $root 'source.txt'),'adapter-source',[Text.UTF8Encoding]::new($false))
$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal-adapter' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'adapter integration' -Budget ([ordered]@{maxReads=8})
$route=New-ESTestRoutePlan -Root $root -Goal $goal
$s=New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'adapter-task' -PlanHash $route.routePlanHash -RoutePlanPath $route.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'
$s=Confirm-ESTaskSourceScope -ProjectRoot $root -StoreRoot 'state' -TaskId $s.taskId -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'verify'
$adapterIds=@('es-adapter.langgraph.v1','es-adapter.letta.v1','es-adapter.openhands.v1','es-adapter.swe-agent.v1');$submitted=@();foreach($adapterId in $adapterIds){$e=New-ESAdapterEvidenceSet -TaskId $s.taskId -AdapterId $adapterId -AdapterVersion '0.1.0' -Observation @{eventType='AdapterObservation';adapterId=$adapterId;focusRevision=1};$s=Submit-ESAdapterEvidenceToTaskContext -ProjectRoot $root -StoreRoot 'state' -TaskId $s.taskId -AdapterEvidence $e -SourceScopeHash $s.verifiedSourceScopeHash -ClaimId 'adapter-observation' -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey ('adapter-evidence-'+$adapterId);$submitted+=$adapterId}
$eventCount=@(Get-ChildItem -LiteralPath (Join-Path $root 'state/adapter-task/events') -File -Filter '*.json').Count
$ok=($s.taskRevision -eq 6 -and $s.evidenceSet.items.Count -eq 1 -and $s.evidenceSet.items[0].candidateProducerType -eq 'adapter' -and $eventCount -eq 6 -and @($submitted).Count -eq 4)
[pscustomobject]@{schemaVersion=1;validator='Test-ESAdapterTaskContextIntegration';status=$(if($ok){'passed'}else{'failed'});taskRevision=$s.taskRevision;contextVersion=$s.contextVersion;submittedAdapters=@($submitted);evidenceInputMode=$s.evidenceSet.inputContractMode;candidateProducerType=$s.evidenceSet.items[0].candidateProducerType;eventCount=$eventCount;fixtureRoot=$root;runtimeStatus='runtime-not-run';claimsNotProven=@('External framework runtime','Unity/host runtime','release acceptance')}|ConvertTo-Json -Depth 10
if(-not$ok){exit 1}
