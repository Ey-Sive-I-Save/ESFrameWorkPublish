[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$CommercialModulePath,
    [string]$RuntimeModulePath,
    [string]$SchemaModulePath,
    [string]$ReportSchemaPath,
    [string]$RegistrySchemaPath
)
$ErrorActionPreference='Stop'
$scriptRoot=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $scriptRoot '..\..\..')).Path}
if([string]::IsNullOrWhiteSpace($CommercialModulePath)){$CommercialModulePath=Join-Path $scriptRoot 'ESCommercialEvaluation.psm1'}
if([string]::IsNullOrWhiteSpace($RuntimeModulePath)){$RuntimeModulePath=Join-Path $scriptRoot '..\TaskContextRuntime\ESTaskContextRuntime.psm1'}
if([string]::IsNullOrWhiteSpace($SchemaModulePath)){$SchemaModulePath=Join-Path $scriptRoot '..\Contracts\ESJsonSchemaLite.psm1'}
if([string]::IsNullOrWhiteSpace($ReportSchemaPath)){$ReportSchemaPath=Join-Path $scriptRoot '..\Contracts\es-commercial-evaluation-v1.schema.json'}
if([string]::IsNullOrWhiteSpace($RegistrySchemaPath)){$RegistrySchemaPath=Join-Path $scriptRoot '..\Contracts\es-commercial-metric-registry-v1.schema.json'}

Import-Module (Resolve-Path -LiteralPath $SchemaModulePath).Path -Force
Get-Module ESCommercialEvaluation,ESTaskContextRuntime|Remove-Module -Force -ErrorAction SilentlyContinue
$runtimeModules=@(Import-Module (Resolve-Path -LiteralPath $RuntimeModulePath).Path -Force -PassThru)
$commercialModules=@(Import-Module (Resolve-Path -LiteralPath $CommercialModulePath).Path -Force -PassThru)
. (Join-Path $scriptRoot '..\TaskContextRuntime\Test-ESTaskContextRoutePlanFixture.ps1')
$runtimeModule=Resolve-ESTestImportedModuleInstance -ImportedModules $runtimeModules -ExpectedPath $RuntimeModulePath -ModuleName 'ESTaskContextRuntime'
$commercialModule=Resolve-ESTestImportedModuleInstance -ImportedModules $commercialModules -ExpectedPath $CommercialModulePath -ModuleName 'ESCommercialEvaluation'
$strictUtf8=[Text.UTF8Encoding]::new($false,$true)
$evidenceContractPath=Join-Path $scriptRoot '..\Contracts\es-platform-evidence-v1.schema.json'
$evidenceContractHash=(Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$evaluationContractPath=Join-Path $scriptRoot '..\Contracts\es-evaluation-record-v1.schema.json'
$evaluationContractHash=(Get-FileHash -LiteralPath $evaluationContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$registrySource=Join-Path $scriptRoot '..\Contracts\es-route-stage.registry.json'
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-commercial-evaluation-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot|Out-Null
Initialize-ESTestRoutePlanRepository $testRoot
$results=[Collections.Generic.List[object]]::new()

function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Assert-Equal($Actual,$Expected,[string]$Message){if([string]$Actual-cne[string]$Expected){throw "$Message Expected=$Expected Actual=$Actual"}}
function Invoke-Case([string]$Name,[scriptblock]$Body){try{&$Body;[void]$results.Add([pscustomobject]@{case=$Name;status='passed';finding=$null})}catch{[void]$results.Add([pscustomobject]@{case=$Name;status='failed';finding=$_.Exception.Message})}}
function Write-Json([string]$Path,$Value){[IO.File]::WriteAllText($Path,($Value|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false))}
function Get-Metric($Report,[string]$Id){return @($Report.metrics|Where-Object{[string]$_.metricId-ceq$Id})|Select-Object -First 1}

function New-FixedRoutePlan($Goal,[string]$FileName,[string]$Head){
    $registryProjectPath='ES/Automation/Contracts/es-route-stage.registry.json'
    $registryFull=Join-Path $testRoot ($registryProjectPath.Replace('/',[IO.Path]::DirectorySeparatorChar))
    $goalFull=Join-Path $testRoot ([string]$Goal.path).Replace('/',[IO.Path]::DirectorySeparatorChar)
    $goalArtifactHash=(Get-FileHash -LiteralPath $goalFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $registryHash=(Get-FileHash -LiteralPath $registryFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $sourceRefs=@([ordered]@{projectPath=[string]$Goal.path;sha256=$goalArtifactHash},[ordered]@{projectPath=$registryProjectPath;sha256=$registryHash})|Sort-Object{[string]$_.projectPath}-CaseSensitive
    $snapshot=[ordered]@{head=$Head;sourceRefs=@($sourceRefs);sourceRefsHash=Get-ESRoutePlanCanonicalHash @($sourceRefs);registryHash=$registryHash;coverage=[ordered]@{normalizationVersion='route-plan-canonical-v1';includes=@('goal-revision-artifact','route-stage-registry')}}
    $core=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/route-plan/v1';status='Ready';routeState='core';evidenceState='closed';effect='review';profile='governance';scope='task-object';routeKeys=@('creator','skill');goalRevision=[ordered]@{goalId=[string]$Goal.goalId;goalRevision=[string]$Goal.goalRevision;revisionHash=[string]$Goal.goalRevisionHash;projectPath=[string]$Goal.path;artifactHash=$goalArtifactHash};stages=@([ordered]@{stageId='stage-01-es-skill-creator';stageContractId='es.route-stage.skill-creator.v1';skillName='es-skill-creator';depth=0;requires=@('goal-revision');produces=@('skill-candidate');failureConditions=@('candidate-write-denied','skill-contract-invalid');depthReasonCode='';executionStatus='not-executed'});maxDepth=0;budget=[ordered]@{maxReads=8};stopConditions=@([ordered]@{code='ROUTE.DEPTH_LIMIT';predicate='next depth exceeds budget';trigger='before next stage';outcome='stop-next-read';evidence=@($registryProjectPath);recovery='reduce route depth'});issues=@();snapshot=$snapshot;compatibility=[ordered]@{legacyPlanStatus='Ready';projectionOnly=$true;productionRouteIntegrated=$false;globalP0Integrated=$false;executionAuthority='none'};executionEnabled=$false}
    $core=Add-ESRoutePlanShadowIntegration -Core $core -LegacyPlanStatus 'Ready'
    $payload=New-ESRoutePlanDocument -Core $core
    Write-Json (Join-Path $testRoot $FileName) $payload
    return [pscustomobject]@{path=$FileName;routePlanHash=$payload.routePlanHash}
}

function New-Candidate([string]$TaskId,$State,[string]$SourcePath,[bool]$Pass,[string]$Suffix){
    $captured=[DateTime]::UtcNow.ToString('o')
    $expected=if($Pass){[string]$State.verifiedSourceScope[0].sha256}else{'0'*64}
    $artifactName="artifact-$TaskId-$Suffix.json"
    $candidateName="candidate-$TaskId-$Suffix.json"
    $artifact=[ordered]@{schemaVersion=1;claimId='source-integrity';sourceScopeHash=$State.verifiedSourceScopeHash;observations=@([ordered]@{path=$SourcePath;expectedSha256=$expected})}
    Write-Json (Join-Path $testRoot $artifactName) $artifact
    $artifactHash=(Get-FileHash -LiteralPath (Join-Path $testRoot $artifactName) -Algorithm SHA256).Hash.ToLowerInvariant()
    $candidate=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/platform-evidence/v1';contractHash=$evidenceContractHash;recordType='CandidateEvidenceSet';taskId=$TaskId;evidenceSetId="evidence-$Suffix";capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';candidateOutcome=if($Pass){'passed'}else{'failed'};capturedUtc=$captured;sourceScopeHash=$State.verifiedSourceScopeHash;candidateEvidenceHash=$artifactHash;candidateProducerType='worker';artifactPath=$artifactName});contradictions=@();sourceDrift=@();unverifiedClaims=@()}
    Write-Json (Join-Path $testRoot $candidateName) $candidate
    return $candidateName
}

function New-Task([string]$TaskId,$Goal,$Plan,[string]$SourcePath,[bool]$Pass){
    $state=New-ESTaskContextTask -ProjectRoot $testRoot -StoreRoot 'state' -TaskId $TaskId -PlanHash $Plan.routePlanHash -RoutePlanPath $Plan.path -GoalRevisionPath $Goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope $SourcePath -IdempotencyKey 'create'
    $state=Confirm-ESTaskSourceScope -ProjectRoot $testRoot -StoreRoot 'state' -TaskId $TaskId -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'verify'
    $candidate=New-Candidate $TaskId $state $SourcePath $Pass 'first'
    $state=Submit-ESTaskEvidenceSet -ProjectRoot $testRoot -StoreRoot 'state' -TaskId $TaskId -EvidenceSetPath $candidate -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'submit-first'
    $record=New-ESTaskEvaluationRecord -ProjectRoot $testRoot -StoreRoot 'state' -TaskId $TaskId -ContractId 'es://automation/contracts/evaluation-record/v1' -ContractHash $evaluationContractHash -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'evaluate-first'
    return [pscustomobject]@{state=$state;record=$record;sourcePath=$SourcePath}
}
function Add-RegisteredRegressionAssertion([string]$TaskId,$Task,[ValidateSet('passed','failed')][string]$Outcome='passed'){
    $path=Join-Path $testRoot "state/$TaskId/evaluations/$($Task.record.evaluationId).json"
    $record=$strictUtf8.GetString([IO.File]::ReadAllBytes($path))|ConvertFrom-Json
    $record.outcomeAssertions=@($record.outcomeAssertions)+@([pscustomobject][ordered]@{recordType='OutcomeAssertion';assertionId='regression-static';claimId='regression.static';verifierId='platform.static-replay-v1';outcome=$Outcome;evidenceHash=if($Outcome-ceq'passed'){('a'*64)}else{('b'*64)};sourceScopeHash=[string]$Task.state.verifiedSourceScopeHash})
    $record.recordHash=&$script:runtimeModule{param($value)Get-ESObjectHash (Get-ESEvaluationRecordHashInput $value)}$record
    Write-Json $path $record
}

try{
    [IO.File]::WriteAllText((Join-Path $testRoot 'source-a.txt'),'a',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $testRoot 'source-b.txt'),'b',[Text.UTF8Encoding]::new($false))
    $registryTarget=Join-Path $testRoot 'ES\Automation\Contracts\es-route-stage.registry.json';New-Item -ItemType Directory -Path (Split-Path -Parent $registryTarget)-Force|Out-Null;[IO.File]::Copy((Resolve-Path $registrySource).Path,$registryTarget,$true)
    $goalA=New-ESGoalRevision -ProjectRoot $testRoot -StoreRoot 'state' -GoalId 'goal-a' -GoalRevision 'r1' -Scope @('source-a.txt') -AcceptanceIntent 'commercial metric fixture A' -Budget ([ordered]@{maxReads=8})
    $goalB=New-ESGoalRevision -ProjectRoot $testRoot -StoreRoot 'state' -GoalId 'goal-b' -GoalRevision 'r1' -Scope @('source-b.txt') -AcceptanceIntent 'commercial metric fixture B' -Budget ([ordered]@{maxReads=8})
    $head=Initialize-ESTestGitSnapshot $testRoot
    $planA=New-FixedRoutePlan $goalA 'route-plan-a.json' $head
    $planB=New-FixedRoutePlan $goalB 'route-plan-b.json' $head
    $task1=New-Task 'task-a1' $goalA $planA 'source-a.txt' $true
    $task2=New-Task 'task-a2' $goalA $planA 'source-a.txt' $true
    $task3=New-Task 'task-b1' $goalB $planB 'source-b.txt' $false
    Add-RegisteredRegressionAssertion 'task-a1' $task1
    $candidate=New-Candidate 'task-b1' $task3.state 'source-b.txt' $true 'recovery'
    $recoveredState=Submit-ESTaskEvidenceSet -ProjectRoot $testRoot -StoreRoot 'state' -TaskId 'task-b1' -EvidenceSetPath $candidate -ExpectedTaskRevision $task3.state.taskRevision -ExpectedContextVersion $task3.state.contextVersion -IdempotencyKey 'submit-recovery'
    $recoveredRecord=New-ESTaskEvaluationRecord -ProjectRoot $testRoot -StoreRoot 'state' -TaskId 'task-b1' -ContractId 'es://automation/contracts/evaluation-record/v1' -ContractHash $evaluationContractHash -ExpectedTaskRevision $recoveredState.taskRevision -ExpectedContextVersion $recoveredState.contextVersion -IdempotencyKey 'evaluate-recovery'
    Add-RegisteredRegressionAssertion 'task-b1' ([pscustomobject]@{state=$recoveredState;record=$recoveredRecord}) 'failed'

    Invoke-Case 'schema-supported-keyword-closure'{Assert-Equal @(Test-ESJsonSchemaSupported -SchemaPath $ReportSchemaPath).Count 0 'report schema keywords';Assert-Equal @(Test-ESJsonSchemaSupported -SchemaPath $RegistrySchemaPath).Count 0 'registry schema keywords'}
    Invoke-Case 'registry-structure-and-semantics'{$snapshot=Get-ESCommercialMetricRegistrySnapshot;Assert-Equal $snapshot.registry.metrics.Count 9 'metric count';Assert-True ([string]$snapshot.registryHash-match'^[a-f0-9]{64}$') 'registry hash'}
    $before=@(@('task-a1','task-a2','task-b1')|ForEach-Object{Get-ESTaskContextState -ProjectRoot $testRoot -StoreRoot 'state' -TaskId $_ -VerifyIntegrity})
    $report=New-ESCommercialEvaluationReport -ProjectRoot $testRoot -StoreRoot 'state' -TaskId @('task-b1','task-a2','task-a1') -MinimumStableRuns 2
    $after=@(@('task-a1','task-a2','task-b1')|ForEach-Object{Get-ESTaskContextState -ProjectRoot $testRoot -StoreRoot 'state' -TaskId $_ -VerifyIntegrity})
    Invoke-Case 'report-schema-and-scope'{$errors=@(Test-ESJsonSchemaValue -SchemaPath $ReportSchemaPath -Value $report);Assert-Equal $errors.Count 0 ($errors-join'; ');Assert-Equal $report.scope 'task-cohort' 'report scope';Assert-True (@($report.taskObservations|Where-Object{$_.scope-ne'task-object'}).Count-eq0) 'task scope expanded'}
    Invoke-Case 'success-and-stability-derived'{$success=Get-Metric $report 'successRate';$stable=Get-Metric $report 'stableSuccessRate';Assert-Equal $success.state 'closed' 'success state';Assert-Equal $success.value 1 'success value';Assert-Equal $stable.state 'closed' 'stable state';Assert-Equal $stable.value 1 'stable value';Assert-Equal $stable.denominator 1 'stable denominator'}
    Invoke-Case 'hard-violation-and-recovery-derived'{$hard=Get-Metric $report 'hardViolationRate';$recovery=Get-Metric $report 'recoveryRate';Assert-Equal $hard.state 'closed' 'hard state';Assert-Equal $hard.numerator 1 'hard numerator';Assert-Equal $hard.denominator 3 'hard denominator';Assert-Equal $recovery.state 'closed' 'recovery state';Assert-Equal $recovery.numerator 1 'recovery numerator';Assert-Equal $recovery.denominator 1 'recovery denominator'}
    Invoke-Case 'external-telemetry-is-pending-null'{foreach($id in @('claimOverstatementRate','humanCorrectionRate','meanCost')){$metric=Get-Metric $report $id;Assert-Equal $metric.state 'evidence-pending' "$id state";Assert-True ($null-eq$metric.value) "$id false zero"}}
    Invoke-Case 'verified-correction-observations-close-the-rate'{$metric=&$commercialModule{param($values)New-ESHumanCorrectionMetric $values}@([pscustomobject]@{correctionObservationClosed=$true;humanCorrectionObserved=$true},[pscustomobject]@{correctionObservationClosed=$true;humanCorrectionObserved=$false},[pscustomobject]@{correctionObservationClosed=$false;humanCorrectionObserved=$null});Assert-Equal $metric.state 'closed' 'correction state';Assert-Equal $metric.value 0.5 'correction value';Assert-Equal $metric.numerator 1 'correction numerator';Assert-Equal $metric.denominator 2 'correction denominator';Assert-Equal $metric.sourceAuthority 'TaskContextRuntime' 'correction authority'}
    Invoke-Case 'registered-regression-assertion-is-aggregated'{$metric=Get-Metric $report 'regressionPassRate';Assert-Equal $metric.state 'closed' 'regression state';Assert-Equal $metric.value 0.5 'regression value';Assert-Equal $metric.numerator 1 'regression numerator';Assert-Equal $metric.denominator 2 'regression denominator';Assert-Equal $metric.sourceAuthority 'TaskContextRuntime' 'regression authority'}
    Invoke-Case 'latency-is-platform-derived'{$latency=Get-Metric $report 'meanLatency';Assert-Equal $latency.state 'closed' 'latency state';Assert-True ([double]$latency.value-ge0) 'latency value'}
    Invoke-Case 'aggregation-does-not-mutate-lifecycle'{for($i=0;$i-lt$before.Count;$i++){Assert-Equal $after[$i].taskRevision $before[$i].taskRevision 'TaskRevision changed';Assert-Equal $after[$i].contextVersion $before[$i].contextVersion 'ContextVersion changed';Assert-Equal $after[$i].taskStatus $before[$i].taskStatus 'TaskStatus changed'}}
    Invoke-Case 'same-snapshot-is-deterministic'{$repeat=New-ESCommercialEvaluationReport -ProjectRoot $testRoot -StoreRoot 'state' -TaskId @('task-a1','task-a2','task-b1') -MinimumStableRuns 2;Assert-Equal $repeat.sourceSnapshotHash $report.sourceSnapshotHash 'sourceSnapshotHash';Assert-Equal $repeat.reportId $report.reportId 'reportId';Assert-Equal (($repeat.metrics|ConvertTo-Json -Depth 10 -Compress)) (($report.metrics|ConvertTo-Json -Depth 10 -Compress)) 'metric values'}
    Invoke-Case 'duplicate-task-id-is-rejected'{$threw=$false;try{New-ESCommercialEvaluationReport -ProjectRoot $testRoot -StoreRoot 'state' -TaskId @('task-a1','task-a1')|Out-Null}catch{$threw=$_.Exception.Message-eq'Commercial evaluation rejects duplicate TaskId values.'};Assert-True $threw 'duplicate TaskId accepted'}
    Invoke-Case 'single-run-does-not-claim-stability'{$single=New-ESCommercialEvaluationReport -ProjectRoot $testRoot -StoreRoot 'state' -TaskId @('task-a1') -MinimumStableRuns 2;$metric=Get-Metric $single 'stableSuccessRate';Assert-Equal $metric.state 'evidence-pending' 'single stability state';Assert-True ($null-eq$metric.value) 'single run claimed stable success'}
    Invoke-Case 'tampered-evaluation-is-rejected'{$path=Join-Path $testRoot "state/task-a1/evaluations/$($task1.record.evaluationId).json";$record=$strictUtf8.GetString([IO.File]::ReadAllBytes($path))|ConvertFrom-Json;$record.decision='rejected';Write-Json $path $record;$threw=$false;try{New-ESCommercialEvaluationReport -ProjectRoot $testRoot -StoreRoot 'state' -TaskId @('task-a1')|Out-Null}catch{$threw=$_.Exception.Message-like'*EvaluationRecord hash mismatch*'};Assert-True $threw 'tampered EvaluationRecord entered metrics'}
}finally{
    if(Test-Path -LiteralPath $testRoot){
        $resolvedTestRoot=(Resolve-Path -LiteralPath $testRoot).Path
        $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        if(-not$resolvedTestRoot.StartsWith($tempRoot+'\',[StringComparison]::OrdinalIgnoreCase)-or(Split-Path -Leaf $resolvedTestRoot)-notlike'es-commercial-evaluation-*'){throw 'Refusing to remove an unexpected commercial evaluation fixture path.'}
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$failed=@($results|Where-Object{$_.status-eq'failed'})
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESCommercialEvaluation';status=if($failed.Count){'failed'}else{'passed'};caseCount=$results.Count;passedCount=@($results|Where-Object{$_.status-eq'passed'}).Count;failedCount=$failed.Count;cases=@($results);runtimeStatus='runtime-not-run';claimsNotProven=@('registered cost and claim-audit sources','production Codex transcript coverage','Unity or Worker Runtime','production route integration','Release acceptance')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
