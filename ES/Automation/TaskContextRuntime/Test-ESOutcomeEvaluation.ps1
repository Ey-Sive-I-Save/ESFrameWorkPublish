[CmdletBinding()]
param(
    [string]$RuntimeModulePath,
    [string]$EvaluationSchemaPath,
    [string]$EvaluatorRegistryPath,
    [string]$EvaluatorRegistrySchemaPath,
    [string]$SchemaModulePath
)

$ErrorActionPreference='Stop'
$scriptRoot=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($RuntimeModulePath)){$RuntimeModulePath=Join-Path $scriptRoot 'ESTaskContextRuntime.psm1'}
if([string]::IsNullOrWhiteSpace($EvaluationSchemaPath)){$EvaluationSchemaPath=Join-Path $scriptRoot '..\Contracts\es-evaluation-record-v1.schema.json'}
if([string]::IsNullOrWhiteSpace($EvaluatorRegistryPath)){$EvaluatorRegistryPath=Join-Path $scriptRoot '..\Contracts\es-outcome-evaluator.registry.json'}
if([string]::IsNullOrWhiteSpace($EvaluatorRegistrySchemaPath)){$EvaluatorRegistrySchemaPath=Join-Path $scriptRoot '..\Contracts\es-outcome-evaluator-registry-v1.schema.json'}
if([string]::IsNullOrWhiteSpace($SchemaModulePath)){$SchemaModulePath=Join-Path $scriptRoot '..\Contracts\ESJsonSchemaLite.psm1'}
$strictUtf8=[Text.UTF8Encoding]::new($false,$true)
Import-Module (Resolve-Path -LiteralPath $SchemaModulePath).Path -Force
Get-Module -Name ESTaskContextRuntime | Remove-Module -Force -ErrorAction Stop
$runtimeModules=@(Import-Module (Resolve-Path -LiteralPath $RuntimeModulePath).Path -Force -PassThru)
. (Join-Path $PSScriptRoot 'Test-ESTaskContextRoutePlanFixture.ps1')
$runtimeModule=Resolve-ESTestImportedModuleInstance -ImportedModules $runtimeModules -ExpectedPath $RuntimeModulePath -ModuleName 'ESTaskContextRuntime'
$evaluationContractHash=(Get-FileHash -LiteralPath $EvaluationSchemaPath -Algorithm SHA256).Hash.ToLowerInvariant()
$evidenceContractPath=Join-Path $PSScriptRoot '..\Contracts\es-platform-evidence-v1.schema.json'
$evidenceContractHash=(Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$baseRegistry=$strictUtf8.GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $EvaluatorRegistryPath).Path))|ConvertFrom-Json -ErrorAction Stop
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-outcome-evaluation-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot|Out-Null
Initialize-ESTestRoutePlanRepository $testRoot
$mutableRegistryPath=Join-Path $testRoot 'outcome-registry.json'
$results=[Collections.Generic.List[object]]::new()

function Assert-Equal($Actual,$Expected,[string]$Message){if([string]$Actual-cne[string]$Expected){throw "$Message Expected=$Expected Actual=$Actual"}}
function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Invoke-Case([string]$Name,[scriptblock]$Body){try{&$Body;[void]$results.Add([pscustomobject]@{case=$Name;status='passed';finding=$null})}catch{[void]$results.Add([pscustomobject]@{case=$Name;status='failed';finding=$_.Exception.Message})}}
function Copy-Object($Value){$Value|ConvertTo-Json -Depth 40|ConvertFrom-Json}
function Set-OutcomeRegistryPath([string]$Path){
    $expected=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path)
    $actual=&$script:runtimeModule{param($path)$script:OutcomeEvaluatorRegistryPath=$path;[IO.Path]::GetFullPath($script:OutcomeEvaluatorRegistryPath)}$expected
    if(-not[string]::Equals($actual,$expected,[StringComparison]::OrdinalIgnoreCase)){throw "Outcome evaluator registry path was not applied. Expected=$expected Actual=$actual"}
}
function Set-OutcomeRegistry($Registry){[IO.File]::WriteAllText($mutableRegistryPath,($Registry|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false));Set-OutcomeRegistryPath $mutableRegistryPath}
function Restore-OutcomeRegistry{Set-OutcomeRegistryPath $EvaluatorRegistryPath}
function Test-OutcomeRegistrySemantics{&$script:runtimeModule{Get-ESOutcomeEvaluatorRegistrySnapshot}|Out-Null}
function New-Fixture([string]$Name){$root=Join-Path $testRoot $Name;New-Item -ItemType Directory -Path $root|Out-Null;[IO.File]::WriteAllText((Join-Path $root 'source.txt'),'source',[Text.UTF8Encoding]::new($false));$root}
function New-State([string]$Root,[string]$TaskId='task'){
    $goal=New-ESGoalRevision -ProjectRoot $Root -StoreRoot 'state' -GoalId ('goal-'+$TaskId) -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static source integrity' -Budget ([ordered]@{maxReads=8})
    $routePlan=New-ESTestRoutePlan -Root $Root -Goal $goal
    New-ESTaskContextTask -ProjectRoot $Root -StoreRoot 'state' -TaskId $TaskId -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'
}
function Confirm-State([string]$Root,$State){Confirm-ESTaskSourceScope -ProjectRoot $Root -StoreRoot 'state' -TaskId $State.taskId -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IdempotencyKey 'verify'}
function Submit-State([string]$Root,$State,[bool]$CriticalContradiction=$false){
    $captured=[DateTime]::UtcNow.ToString('o')
    $artifact=[ordered]@{schemaVersion=1;claimId='source-integrity';sourceScopeHash=$State.verifiedSourceScopeHash;observations=@([ordered]@{path='source.txt';expectedSha256=[string]$State.verifiedSourceScope[0].sha256})}
    [IO.File]::WriteAllText((Join-Path $Root 'artifact.json'),($artifact|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $artifactHash=(Get-FileHash -LiteralPath (Join-Path $Root 'artifact.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $contradictions=@();if($CriticalContradiction){$contradictions=@([ordered]@{critical=$true;description='authoritative contradiction fixture'})}
    $payload=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/platform-evidence/v1';contractHash=$evidenceContractHash;recordType='CandidateEvidenceSet';taskId=[string]$State.taskId;evidenceSetId='evidence';capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';candidateOutcome='passed';capturedUtc=$captured;sourceScopeHash=$State.verifiedSourceScopeHash;candidateEvidenceHash=$artifactHash;candidateProducerType='worker';artifactPath='artifact.json'});contradictions=$contradictions;sourceDrift=@();unverifiedClaims=@()}
    [IO.File]::WriteAllText((Join-Path $Root 'candidate.json'),($payload|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
    Submit-ESTaskEvidenceSet -ProjectRoot $Root -StoreRoot 'state' -TaskId $State.taskId -EvidenceSetPath 'candidate.json' -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IdempotencyKey 'submit'
}
function Invoke-Evaluate([string]$Root,$State,[string]$Key='evaluate'){
    &$script:runtimeModule {
        param($projectRoot,$taskId,$contractHash,$taskRevision,$contextVersion,$idempotencyKey)
        New-ESTaskEvaluationRecord -ProjectRoot $projectRoot -StoreRoot 'state' -TaskId $taskId -ContractId 'es://automation/contracts/evaluation-record/v1' -ContractHash $contractHash -ExpectedTaskRevision $taskRevision -ExpectedContextVersion $contextVersion -IdempotencyKey $idempotencyKey
    } $Root ([string]$State.taskId) $evaluationContractHash ([int]$State.taskRevision) ([int]$State.contextVersion) $Key
}

try {
    Invoke-Case 'schema-supported-keyword-closure'{$errors=@(Test-ESJsonSchemaSupported -SchemaPath $EvaluationSchemaPath);Assert-Equal $errors.Count 0 ($errors-join'; ')}
    Invoke-Case 'registry-schema-supported-keyword-closure'{$errors=@(Test-ESJsonSchemaSupported -SchemaPath $EvaluatorRegistrySchemaPath);Assert-Equal $errors.Count 0 ($errors-join'; ')}
    Invoke-Case 'current-registry-schema'{$errors=@(Test-ESJsonSchemaValue -SchemaPath $EvaluatorRegistrySchemaPath -Value $baseRegistry);Assert-Equal $errors.Count 0 ($errors-join'; ')}
    Invoke-Case 'current-registry-semantics'{Restore-OutcomeRegistry;Test-OutcomeRegistrySemantics}
    Invoke-Case 'duplicate-evaluator-id-is-rejected'{$registry=Copy-Object $baseRegistry;$registry.evaluators=@($registry.evaluators)+@(Copy-Object $registry.evaluators[0]);Set-OutcomeRegistry $registry;$threw=$false;try{Test-OutcomeRegistrySemantics}catch{$threw=$_.Exception.Message-eq'Outcome evaluator registry contains duplicate evaluatorId values.'};Assert-True $threw 'Duplicate OutcomeEvaluatorId was accepted.'}
    Invoke-Case 'unanchored-profile-pattern-is-rejected'{$registry=Copy-Object $baseRegistry;$registry.evaluators[0].profileIdPattern='static';Set-OutcomeRegistry $registry;$threw=$false;try{Test-OutcomeRegistrySemantics}catch{$threw=$_.Exception.Message-like'Outcome evaluator profileIdPattern must be fully anchored:*'};Assert-True $threw 'Unanchored profile pattern was accepted.'}
    Invoke-Case 'project-global-evaluator-scope-is-rejected'{$registry=Copy-Object $baseRegistry;$registry.evaluators[0].scopeType='project-global';Set-OutcomeRegistry $registry;$threw=$false;try{Test-OutcomeRegistrySemantics}catch{$threw=$_.Exception.Message-like'Outcome evaluator contract is unsupported:*'};Assert-True $threw 'Project-global OutcomeEvaluator scope was accepted.'}
    Invoke-Case 'candidate-authority-field-is-rejected-in-evaluator-registry'{$registry=Copy-Object $baseRegistry;$registry.evaluators[0]|Add-Member -NotePropertyName trustsCandidateOutcome -NotePropertyValue $true;$schemaErrors=@(Test-ESJsonSchemaValue -SchemaPath $EvaluatorRegistrySchemaPath -Value $registry);Assert-True ($schemaErrors.Count-gt0) 'Candidate authority field passed registry schema.';Set-OutcomeRegistry $registry;$threw=$false;try{Test-OutcomeRegistrySemantics}catch{$threw=$_.Exception.Message-like'Outcome evaluator definition contains an unsupported property:*'};Assert-True $threw 'Candidate authority field passed registry runtime semantics.'}
    Invoke-Case 'evaluation-request-schema'{$request=[pscustomobject][ordered]@{schemaVersion=1;contractId='es://automation/contracts/evaluation-record/v1';contractHash=$evaluationContractHash;recordType='EvaluationRequest';storeRoot='state';taskId='task';expectedTaskRevision=1;expectedContextVersion=1;idempotencyKey='evaluate'};$errors=@(Test-ESJsonSchemaValue -SchemaPath $EvaluationSchemaPath -DefinitionName 'evaluationRequest' -Value $request);Assert-Equal $errors.Count 0 ($errors-join'; ')}
    Invoke-Case 'missing-explicit-outcome-evaluator-is-rejected'{Restore-OutcomeRegistry;$root=New-Fixture 'missing-evaluator';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal;$threw=$false;try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId '' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$true};Assert-True $threw 'Empty OutcomeEvaluatorId was accepted.'}
    Invoke-Case 'unregistered-outcome-evaluator-is-rejected'{Restore-OutcomeRegistry;$root=New-Fixture 'unknown-evaluator';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal;$threw=$false;try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.unknown-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message-like'Outcome evaluator is not registered exactly once:*'};Assert-True $threw 'Unknown OutcomeEvaluatorId was accepted.'}
    Invoke-Case 'advisory-evaluation-is-platform-derived-and-non-mutating'{
        $root=New-Fixture 'advisory';$state=New-State $root;$state=Confirm-State $root $state;$state=Submit-State $root $state;$record=Invoke-Evaluate $root $state
        Assert-Equal $record.decision 'accepted' 'decision';Assert-Equal $record.decisionScope 'task-object' 'decisionScope';Assert-Equal $record.evidenceState 'closed' 'evidenceState';Assert-Equal $record.purpose 'advisory' 'purpose'
        $after=Get-ESTaskContextState -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -VerifyIntegrity;Assert-Equal $after.taskRevision $state.taskRevision 'TaskRevision changed';Assert-Equal $after.contextVersion $state.contextVersion 'ContextVersion changed';Assert-Equal $after.taskStatus 'Active' 'TaskStatus changed'
        $schemaErrors=@(Test-ESJsonSchemaValue -SchemaPath $EvaluationSchemaPath -DefinitionName 'evaluationRecord' -Value $record);Assert-Equal $schemaErrors.Count 0 ($schemaErrors-join'; ')
    }
    Invoke-Case 'evaluation-exact-retry-is-idempotent'{$root=New-Fixture 'idempotent';$state=New-State $root;$first=Invoke-Evaluate $root $state;$second=Invoke-Evaluate $root $state;Assert-Equal $second.recordHash $first.recordHash 'recordHash';$count=@(Get-ChildItem -LiteralPath (Join-Path $root 'state/task/evaluations') -File).Count;Assert-Equal $count 1 'evaluation file count'}
    Invoke-Case 'new-evaluation-key-produces-new-snapshot-identity'{$root=New-Fixture 'new-key';$state=New-State $root;$first=Invoke-Evaluate $root $state 'evaluate-1';$second=Invoke-Evaluate $root $state 'evaluate-2';Assert-True ([string]$first.evaluationId-cne[string]$second.evaluationId) 'New evaluation key reused the prior evaluationId.';Assert-True ([string]$first.recordHash-cne[string]$second.recordHash) 'New evaluation snapshot reused the prior recordHash.'}
    Invoke-Case 'forged-evaluation-contract-hash-is-rejected'{$root=New-Fixture 'forged-contract';$state=New-State $root;$threw=$false;try{New-ESTaskEvaluationRecord -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ContractId 'es://automation/contracts/evaluation-record/v1' -ContractHash ('0'*64) -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'evaluate'|Out-Null}catch{$threw=$_.Exception.Message-eq'EvaluationRequest contract binding does not match the platform contract.'};Assert-True $threw 'Forged Evaluation contract hash was accepted.'}
    Invoke-Case 'cli-rejects-cross-contract-projection-fields'{
        $root=New-Fixture 'cross-contract';$state=New-State $root;$request=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/evaluation-record/v1';contractHash=$evaluationContractHash;recordType='EvaluationRequest';storeRoot='state';taskId='task';expectedTaskRevision=[int]$state.taskRevision;expectedContextVersion=[int]$state.contextVersion;idempotencyKey='evaluate';automationStatus='Accepted';governanceHash=('b'*64);evidenceScope='Runtime'}
        [IO.File]::WriteAllText((Join-Path $root 'request.json'),($request|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false));$threw=$false
        try{& (Join-Path $PSScriptRoot 'Invoke-ESTaskContextRuntime.ps1') -Action Evaluate -InputPath 'request.json' -ProjectRoot $root|Out-Null}catch{$threw=$_.Exception.Message-like'EvaluationRequest contains an unsupported property:*'}
        Assert-True $threw 'Automation status/hash/scope fields were projected into /eval.'
    }
    Invoke-Case 'source-drift-is-scoped-claim-cap'{$root=New-Fixture 'source-drift';$state=New-State $root;$state=Confirm-State $root $state;$state=Submit-State $root $state;[IO.File]::WriteAllText((Join-Path $root 'source.txt'),'changed',[Text.UTF8Encoding]::new($false));$record=Invoke-Evaluate $root $state;Assert-Equal $record.decision 'undetermined' 'decision';Assert-Equal $record.evidenceState 'stale' 'evidenceState';Assert-True (@($record.failureRecords|Where-Object{$_.scope-ne'task-object'-or$_.completionImpact-ne'claim-cap'}).Count-eq0) 'Source drift expanded beyond task claim-cap.'}
    Invoke-Case 'critical-contradiction-blocks-only-task-completion'{$root=New-Fixture 'contradiction';$state=New-State $root;$state=Confirm-State $root $state;$state=Submit-State $root $state $true;$record=Invoke-Evaluate $root $state;Assert-Equal $record.decision 'rejected' 'decision';Assert-True (@($record.failureRecords|Where-Object{$_.code-eq'EVAL.CRITICAL_CONTRADICTION'-and$_.scope-eq'task-object'-and$_.completionImpact-eq'task-completion-block'}).Count-eq1) 'Critical contradiction was not scoped to task completion.';Assert-True (@($record.failureRecords|Where-Object{$_.scope-eq'project-global'}).Count-eq0) 'Failure expanded to project-global.'}
    Invoke-Case 'outcome-evaluator-definition-drift-is-recorded-not-globalized'{
        Restore-OutcomeRegistry;$root=New-Fixture 'evaluator-drift';$state=New-State $root;$registry=Copy-Object $baseRegistry;$registry.evaluators[0].acceptedRequires+= 'new-requirement';Set-OutcomeRegistry $registry
        $currentEvaluator=&$script:runtimeModule{Get-ESOutcomeEvaluatorDefinition 'platform.task-context-outcome-v1'}
        Assert-True ([string]$currentEvaluator.definitionHash-cne[string]$state.acceptanceProfile.outcomeEvaluatorDefinitionHash) 'Outcome evaluator drift fixture did not change the registered definition hash.'
        $record=Invoke-Evaluate $root $state
        Assert-Equal $record.decision 'undetermined' 'decision';Assert-True (@($record.failureRecords|Where-Object{$_.code-eq'EVAL.OUTCOME_EVALUATOR_DRIFT'-and$_.scope-eq'task-object'}).Count-eq1) 'Evaluator drift was not recorded with task scope.'
    }
    Invoke-Case 'completion-receipt-binds-evaluation-record'{
        Restore-OutcomeRegistry;$root=New-Fixture 'completion-binding';$state=New-State $root;$state=Confirm-State $root $state;$state=Submit-State $root $state;$state=Complete-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'complete'
        $receipt=$strictUtf8.GetString([IO.File]::ReadAllBytes((Join-Path (Join-Path $root 'state') ([string]$state.completionReceipt.path))))|ConvertFrom-Json;Assert-True (-not[string]::IsNullOrWhiteSpace([string]$receipt.evaluationRecordPath)) 'Receipt lacks EvaluationRecord path.'
        $integrity=Test-ESTaskContextIntegrity -ProjectRoot $root -StoreRoot 'state' -TaskId 'task';Assert-Equal $integrity.status 'passed' 'integrity';Assert-Equal $integrity.orphanCompletionEvaluationCount 0 'orphan completion evaluation count'
    }
    Invoke-Case 'evaluation-record-tamper-breaks-completion-integrity'{
        Restore-OutcomeRegistry;$root=New-Fixture 'evaluation-tamper';$state=New-State $root;$state=Confirm-State $root $state;$state=Submit-State $root $state;$state=Complete-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'complete'
        $receipt=$strictUtf8.GetString([IO.File]::ReadAllBytes((Join-Path (Join-Path $root 'state') ([string]$state.completionReceipt.path))))|ConvertFrom-Json;$evaluationPath=Join-Path (Join-Path $root 'state') ([string]$receipt.evaluationRecordPath);$record=$strictUtf8.GetString([IO.File]::ReadAllBytes($evaluationPath))|ConvertFrom-Json;$record.decision='undetermined';[IO.File]::WriteAllText($evaluationPath,($record|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false));$integrity=Test-ESTaskContextIntegrity -ProjectRoot $root -StoreRoot 'state' -TaskId 'task';Assert-Equal $integrity.status 'failed' 'tampered integrity'
    }
    Invoke-Case 'runtime-and-release-remain-non-claims'{$root=New-Fixture 'non-claims';$state=New-State $root;$record=Invoke-Evaluate $root $state;Assert-True (@($record.nonClaims|Where-Object{$_-like'*Runtime or Release*'}).Count-eq1) 'Runtime/Release non-claim is missing.';Assert-True ([string]$record.evidenceState-cne'runtime-not-run') 'Runtime status was flattened into evidenceState.'}
} finally {
    Restore-OutcomeRegistry
}

$failed=@($results|Where-Object{$_.status-eq'failed'})
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESOutcomeEvaluation';status=if($failed.Count){'failed'}else{'passed'};caseCount=$results.Count;passedCount=@($results|Where-Object{$_.status-eq'passed'}).Count;failedCount=$failed.Count;cases=@($results);runtimeStatus='runtime-not-run';claimsNotProven=@('Production /eval bridge integration','Unity or Worker Runtime','Release acceptance')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
