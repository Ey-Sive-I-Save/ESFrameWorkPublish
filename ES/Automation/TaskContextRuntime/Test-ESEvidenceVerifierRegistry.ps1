[CmdletBinding()]
param(
    [string]$RegistryPath,
    [string]$SchemaPath,
    [string]$SchemaModulePath,
    [string]$RuntimeModulePath
)

$ErrorActionPreference='Stop'
$scriptRoot=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($RegistryPath)){$RegistryPath=Join-Path $scriptRoot '..\Contracts\es-evidence-verifier.registry.json'}
if([string]::IsNullOrWhiteSpace($SchemaPath)){$SchemaPath=Join-Path $scriptRoot '..\Contracts\es-evidence-verifier-registry-v1.schema.json'}
if([string]::IsNullOrWhiteSpace($SchemaModulePath)){$SchemaModulePath=Join-Path $scriptRoot '..\Contracts\ESJsonSchemaLite.psm1'}
if([string]::IsNullOrWhiteSpace($RuntimeModulePath)){$RuntimeModulePath=Join-Path $scriptRoot 'ESTaskContextRuntime.psm1'}
$strictUtf8=[Text.UTF8Encoding]::new($false,$true)
Import-Module (Resolve-Path -LiteralPath $SchemaModulePath).Path -Force
Get-Module -Name ESTaskContextRuntime | Remove-Module -Force -ErrorAction Stop
$runtimeModules=@(Import-Module (Resolve-Path -LiteralPath $RuntimeModulePath).Path -Force -PassThru)
. (Join-Path $PSScriptRoot 'Test-ESTaskContextRoutePlanFixture.ps1')
$runtimeModule=Resolve-ESTestImportedModuleInstance -ImportedModules $runtimeModules -ExpectedPath $RuntimeModulePath -ModuleName 'ESTaskContextRuntime'
$baseRegistry=$strictUtf8.GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RegistryPath).Path))|ConvertFrom-Json -ErrorAction Stop
$evidenceContractPath=Join-Path $PSScriptRoot '..\Contracts\es-platform-evidence-v1.schema.json'
$evidenceContractHash=(Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$evaluationContractPath=Join-Path $PSScriptRoot '..\Contracts\es-evaluation-record-v1.schema.json'
$evaluationContractHash=(Get-FileHash -LiteralPath $evaluationContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-evidence-verifier-registry-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot|Out-Null
Initialize-ESTestRoutePlanRepository $testRoot
$mutableRegistryPath=Join-Path $testRoot 'registry.json'
$results=[Collections.Generic.List[object]]::new()

function Assert-Equal($Actual,$Expected,[string]$Message){if([string]$Actual-cne[string]$Expected){throw "$Message Expected=$Expected Actual=$Actual"}}
function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Invoke-Case([string]$Name,[scriptblock]$Body){try{&$Body;[void]$results.Add([pscustomobject]@{case=$Name;status='passed';finding=$null})}catch{[void]$results.Add([pscustomobject]@{case=$Name;status='failed';finding=$_.Exception.Message})}}
function Copy-Registry{$baseRegistry|ConvertTo-Json -Depth 20|ConvertFrom-Json}
function Set-Registry($Registry){[IO.File]::WriteAllText($mutableRegistryPath,($Registry|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));&$script:runtimeModule{param($path)$script:EvidenceVerifierRegistryPath=$path}$mutableRegistryPath}
function Test-RegistrySemantics{&$script:runtimeModule{Get-ESEvidenceVerifierRegistrySnapshot}|Out-Null}
function New-Fixture([string]$Name){$root=Join-Path $testRoot $Name;New-Item -ItemType Directory -Path $root|Out-Null;[IO.File]::WriteAllText((Join-Path $root 'source.txt'),'source',[Text.UTF8Encoding]::new($false));$root}
function New-VerifiedState([string]$Root){$goal=New-ESGoalRevision -ProjectRoot $Root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $Root -Goal $goal;$state=New-ESTaskContextTask -ProjectRoot $Root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create';Confirm-ESTaskSourceScope -ProjectRoot $Root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'verify'}
function Submit-Evidence([string]$Root,$State){$captured=[DateTime]::UtcNow.ToString('o');$artifact=[ordered]@{schemaVersion=1;claimId='source-integrity';sourceScopeHash=$State.verifiedSourceScopeHash;observations=@([ordered]@{path='source.txt';expectedSha256=[string]$State.verifiedSourceScope[0].sha256})};$artifactPath=Join-Path $Root 'artifact.json';[IO.File]::WriteAllText($artifactPath,($artifact|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$artifactHash=(Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant();$payload=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/platform-evidence/v1';contractHash=$evidenceContractHash;recordType='CandidateEvidenceSet';taskId='task';evidenceSetId='evidence';capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';candidateOutcome='passed';capturedUtc=$captured;sourceScopeHash=$State.verifiedSourceScopeHash;candidateEvidenceHash=$artifactHash;candidateProducerType='worker';artifactPath='artifact.json'});contradictions=@();sourceDrift=@();unverifiedClaims=@()};[IO.File]::WriteAllText((Join-Path $Root 'candidate.json'),($payload|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));Submit-ESTaskEvidenceSet -ProjectRoot $Root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath 'candidate.json' -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IdempotencyKey 'submit'}
function Complete-State([string]$Root,$State){Complete-ESTaskContextTask -ProjectRoot $Root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IdempotencyKey 'complete'}
function New-StaticReplayFixture([string]$Name,[string]$CandidateOutcome='passed',[switch]$OmitSourceFromScope,[switch]$StaticReplayBlocked){
    $root=Join-Path $testRoot $Name
    New-Item -ItemType Directory -Path $root|Out-Null
    Initialize-ESTestRoutePlanRepository $root
    $skillName='es-regression-fixture'
    $skillRoot='.agents/skills/'+$skillName
    $skillFull=Join-Path $root $skillRoot.Replace('/',[IO.Path]::DirectorySeparatorChar)
    foreach($directory in @('agents','references','scripts')){New-Item -ItemType Directory -Path (Join-Path $skillFull $directory)-Force|Out-Null}
    $cases=@('normal-input','invalid-input','denied-expansion','repeat-idempotency','hash-change-cache-invalidation','interruption-recovery','deterministic-output')
    $assertions=[ordered]@{};foreach($caseId in $cases){$assertions[$caseId]="fixture assertion $caseId"}
    $manifest=[ordered]@{schemaVersion=1;skillName=$skillName;sourceRoots=@($skillRoot);cases=$cases;staticClaims=@('deterministic-replay');runtimeClaimsNotProven=@('Runtime behavior');runtimeEscalation=[ordered]@{required=$false;reason='static fixture'};caseAssertions=$assertions;responsibilityProfile='engineering';responsibilityChecks=@('input-boundary');responsibilityScope='bounded regression fixture'}
    if($StaticReplayBlocked){$manifest.cases=@('normal-input')}
    $adapter="Responsibility profile: engineering`ninput-boundary`n"+($cases-join"`n")
    [IO.File]::WriteAllText((Join-Path $skillFull 'SKILL.md'),'evidence receipt invalid-input denied-expansion boundary',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $skillFull 'governance.json'),([ordered]@{staticDeepReplayRequired=$true;defaultVerificationOrder='StaticDeepReplay-first'}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $skillFull 'agents/openai.yaml'),"interface:`n  display_name: fixture",[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $skillFull 'references/static-replay-adapter.md'),$adapter,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $skillFull 'static-replay.manifest.json'),($manifest|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $skillFull 'scripts/Test-es-regression-fixture-StaticReplay.ps1'),"& `$shared -ManifestPath '.agents/skills/es-regression-fixture/static-replay.manifest.json' # Invoke-ESStaticDeepReplay.ps1",[Text.UTF8Encoding]::new($false))
    $contractRelative='ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
    $contractTarget=Join-Path $root $contractRelative.Replace('/',[IO.Path]::DirectorySeparatorChar)
    New-Item -ItemType Directory -Path (Split-Path -Parent $contractTarget)-Force|Out-Null
    [IO.File]::Copy((Resolve-Path (Join-Path $PSScriptRoot '..\Contracts\es-skill-evidence-receipt-v1.schema.json')).Path,$contractTarget,$true)
    $runnerRelative='.agents/skills/es-static-deep-replay/scripts/Invoke-ESStaticDeepReplay.ps1'
    $runnerTarget=Join-Path $root $runnerRelative.Replace('/',[IO.Path]::DirectorySeparatorChar)
    New-Item -ItemType Directory -Path (Split-Path -Parent $runnerTarget)-Force|Out-Null
    [IO.File]::Copy((Resolve-Path (Join-Path $PSScriptRoot '..\..\..\.agents\skills\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1')).Path,$runnerTarget,$true)
    $scope=@(Get-ChildItem -LiteralPath $skillFull -Recurse -File|ForEach-Object{$_.FullName.Substring($root.Length).TrimStart('\').Replace('\','/')})+@($contractRelative)
    if($OmitSourceFromScope){$scope=@($scope|Where-Object{$_-cne"$skillRoot/SKILL.md"})}
    $goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope $scope -AcceptanceIntent 'registered regression verifier' -Budget ([ordered]@{maxReads=64})
    $routePlan=New-ESTestRoutePlan -Root $root -Goal $goal
    $state=New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'regression.static' -RequiredClaimVerifier ([ordered]@{'regression.static'='platform.static-replay-v1'}) -RequestedSourceScope $scope -IdempotencyKey 'create'
    $state=Confirm-ESTaskSourceScope -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'verify'
    $request=[ordered]@{schemaVersion=1;claimId='regression.static';sourceScopeHash=[string]$state.verifiedSourceScopeHash;skillName=$skillName;manifestPath="$skillRoot/static-replay.manifest.json"}
    [IO.File]::WriteAllText((Join-Path $root 'regression-request.json'),($request|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    $requestHash=(Get-FileHash -LiteralPath (Join-Path $root 'regression-request.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $captured=[DateTime]::UtcNow.ToString('o')
    $candidate=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/platform-evidence/v1';contractHash=$evidenceContractHash;recordType='CandidateEvidenceSet';taskId='task';evidenceSetId='regression-evidence';capturedUtc=$captured;items=@([ordered]@{claimId='regression.static';candidateOutcome=$CandidateOutcome;capturedUtc=$captured;sourceScopeHash=[string]$state.verifiedSourceScopeHash;candidateEvidenceHash=$requestHash;candidateProducerType='skill';artifactPath='regression-request.json'});contradictions=@();sourceDrift=@();unverifiedClaims=@()}
    [IO.File]::WriteAllText((Join-Path $root 'candidate.json'),($candidate|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
    [pscustomobject]@{root=$root;state=$state}
}

Invoke-Case 'schema-supported-keyword-closure'{$errors=@(Test-ESJsonSchemaSupported -SchemaPath $SchemaPath);Assert-Equal $errors.Count 0 ($errors-join'; ')}
Invoke-Case 'current-registry-schema'{$errors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value $baseRegistry);Assert-Equal $errors.Count 0 ($errors-join'; ')}
Invoke-Case 'current-registry-semantics'{Set-Registry (Copy-Registry);Test-RegistrySemantics}
Invoke-Case 'duplicate-verifier-id-is-rejected'{$registry=Copy-Registry;$duplicate=$registry.verifiers[0]|ConvertTo-Json -Depth 20|ConvertFrom-Json;$registry.verifiers=@($registry.verifiers)+@($duplicate);Set-Registry $registry;$threw=$false;try{Test-RegistrySemantics}catch{$threw=$_.Exception.Message-eq'Evidence verifier registry contains duplicate verifierId values.'};Assert-True $threw 'Duplicate verifierId was accepted.'}
Invoke-Case 'unanchored-claim-pattern-is-rejected'{$registry=Copy-Registry;$registry.verifiers[0].claimIdPattern='source-integrity';Set-Registry $registry;$threw=$false;try{Test-RegistrySemantics}catch{$threw=$_.Exception.Message-like'Evidence verifier claimIdPattern must be fully anchored:*'};Assert-True $threw 'Unanchored claim pattern was accepted.'}
Invoke-Case 'duplicate-field-name-is-rejected'{$registry=Copy-Registry;$registry.verifiers[0].observationFields=@('path','path');Set-Registry $registry;$threw=$false;try{Test-RegistrySemantics}catch{$threw=$_.Exception.Message-like'Evidence verifier field list contains duplicates:*'};Assert-True $threw 'Duplicate verifier field was accepted.'}
Invoke-Case 'additional-definition-field-is-rejected'{$registry=Copy-Registry;$registry.verifiers[0]|Add-Member -NotePropertyName trustsProducer -NotePropertyValue $true;$errors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value $registry);Assert-True ($errors.Count-gt0) 'Additional verifier authority field passed schema.';Set-Registry $registry;$threw=$false;try{Test-RegistrySemantics}catch{$threw=$_.Exception.Message-like'Evidence verifier definition contains an unsupported property:*'};Assert-True $threw 'Additional verifier authority field passed runtime semantics.'}
Invoke-Case 'static-replay-execution-budget-is-required-and-bounded'{$registry=Copy-Registry;$registry.verifiers[1].PSObject.Properties.Remove('maxExecutionSeconds');Set-Registry $registry;$threw=$false;try{Test-RegistrySemantics}catch{$threw=$_.Exception.Message-eq'Static replay verifier field is missing: maxExecutionSeconds'};Assert-True $threw 'Static replay verifier without maxExecutionSeconds was accepted.';$registry=Copy-Registry;$registry.verifiers[1].maxOutputChars=1024;Set-Registry $registry;$threw=$false;try{Test-RegistrySemantics}catch{$threw=$_.Exception.Message-eq'Static replay verifier budget is invalid.'};Assert-True $threw 'Static replay verifier with an unsafe output budget was accepted.'}
Invoke-Case 'definition-drift-limits-current-completion'{
    $registry=Copy-Registry;Set-Registry $registry;$root=New-Fixture 'definition-drift';$state=New-VerifiedState $root;$state=Submit-Evidence $root $state
    $registry.verifiers[0].claimIdPattern='^source-integrity(?:[._-][A-Za-z0-9][A-Za-z0-9._-]{0,59}x)?$';Set-Registry $registry;$state=Complete-State $root $state;Assert-Equal $state.completionDecision 'undetermined' 'completionDecision'
    $eventPath=(Get-ChildItem -LiteralPath (Join-Path $root 'state/task/events') -File|Sort-Object Name|Select-Object -Last 1).FullName;$event=$strictUtf8.GetString([IO.File]::ReadAllBytes($eventPath))|ConvertFrom-Json;Assert-True (@($event.metadata.reasons)-contains'VerifierDefinitionDrift:source-integrity') 'VerifierDefinitionDrift reason was not recorded.'
}
Invoke-Case 'unrelated-registry-addition-does-not-block-bound-verifier'{
    $registry=Copy-Registry;Set-Registry $registry;$root=New-Fixture 'unrelated-addition';$state=New-VerifiedState $root;$state=Submit-Evidence $root $state
    $registry.verifiers+= [pscustomobject][ordered]@{verifierId='platform.other-v1';authority='TaskContextRuntime';artifactFormat='json';claimIdPattern='^other-claim$';requiredArtifactFields=@('schemaVersion','claimId','sourceScopeHash','observations');observationFields=@('path','expectedSha256');outcomePolicy='failed-if-any-hash-mismatch;unverified-if-empty;passed-if-all-match'};Set-Registry $registry;Test-RegistrySemantics;$state=Complete-State $root $state;Assert-Equal $state.completionDecision 'accepted' 'completionDecision after unrelated registry addition'
}
Invoke-Case 'static-replay-verifier-reruns-shared-validator'{
    Set-Registry (Copy-Registry);$fixture=New-StaticReplayFixture 'static-replay-positive';$state=Submit-ESTaskEvidenceSet -ProjectRoot $fixture.root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath 'candidate.json' -ExpectedTaskRevision $fixture.state.taskRevision -ExpectedContextVersion $fixture.state.contextVersion -IdempotencyKey 'submit';$item=$state.evidenceSet.items[0]
    Assert-Equal $item.outcome 'passed' 'derived regression outcome';Assert-Equal $item.verifierId 'platform.static-replay-v1' 'regression verifierId';Assert-True ([string]$item.evidenceHash-cne[string]$item.artifactHash) 'platform replay evidenceHash reused the candidate artifact hash';Assert-True (-not(Test-Path -LiteralPath (Join-Path $fixture.root 'ES/Output/TaskContextRuntime/VerifierScratch'))) 'static replay verifier scratch was retained'
    $record=New-ESTaskEvaluationRecord -ProjectRoot $fixture.root -StoreRoot 'state' -TaskId 'task' -ContractId 'es://automation/contracts/evaluation-record/v1' -ContractHash $evaluationContractHash -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'evaluate';$assertion=@($record.outcomeAssertions|Where-Object{[string]$_.claimId-ceq'regression.static'})|Select-Object -First 1;Assert-Equal $assertion.verifierId 'platform.static-replay-v1' 'EvaluationRecord regression verifier';Assert-Equal $assertion.outcome 'passed' 'EvaluationRecord regression outcome'
    $observation=Get-ESTaskCommercialObservation -ProjectRoot $fixture.root -StoreRoot 'state' -TaskId 'task';Assert-True $observation.regressionObserved 'commercial regression observation missing';Assert-True $observation.regressionPassed 'commercial regression pass was not derived';Assert-Equal $observation.regressionClaimCount 1 'commercial regression claim count'
}
Invoke-Case 'static-replay-verifier-rejects-candidate-outcome-forgery'{
    Set-Registry (Copy-Registry);$fixture=New-StaticReplayFixture 'static-replay-forged' 'failed';$threw=$false;try{Submit-ESTaskEvidenceSet -ProjectRoot $fixture.root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath 'candidate.json' -ExpectedTaskRevision $fixture.state.taskRevision -ExpectedContextVersion $fixture.state.contextVersion -IdempotencyKey 'submit'|Out-Null}catch{$threw=$_.Exception.Message-like'Candidate outcome does not match platform-derived artifact outcome:*'};Assert-True $threw 'forged regression candidate outcome was accepted'
}
Invoke-Case 'static-replay-verifier-stops-before-scope-expansion'{
    Set-Registry (Copy-Registry);$fixture=New-StaticReplayFixture 'static-replay-scope' 'passed' -OmitSourceFromScope;$threw=$false;try{Submit-ESTaskEvidenceSet -ProjectRoot $fixture.root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath 'candidate.json' -ExpectedTaskRevision $fixture.state.taskRevision -ExpectedContextVersion $fixture.state.contextVersion -IdempotencyKey 'submit'|Out-Null}catch{$threw=$_.Exception.Message-like'Static replay source is outside the verified sourceScope:*'};Assert-True $threw 'static replay verifier read outside the verified sourceScope'
}
Invoke-Case 'static-replay-verifier-derives-failed-regression'{
    Set-Registry (Copy-Registry);$fixture=New-StaticReplayFixture 'static-replay-blocked' 'failed' -StaticReplayBlocked;$global:LASTEXITCODE=0;$state=Submit-ESTaskEvidenceSet -ProjectRoot $fixture.root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath 'candidate.json' -ExpectedTaskRevision $fixture.state.taskRevision -ExpectedContextVersion $fixture.state.contextVersion -IdempotencyKey 'submit';Assert-Equal $state.evidenceSet.items[0].outcome 'failed' 'blocked StaticReplay outcome';Assert-Equal $LASTEXITCODE 0 'structured failed outcome contaminated process exit state'
    $record=New-ESTaskEvaluationRecord -ProjectRoot $fixture.root -StoreRoot 'state' -TaskId 'task' -ContractId 'es://automation/contracts/evaluation-record/v1' -ContractHash $evaluationContractHash -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'evaluate';Assert-Equal $record.decision 'rejected' 'failed regression decision';$observation=Get-ESTaskCommercialObservation -ProjectRoot $fixture.root -StoreRoot 'state' -TaskId 'task';Assert-True $observation.regressionObserved 'failed regression observation missing';Assert-True (-not$observation.regressionPassed) 'failed regression was reported passed';Assert-Equal $observation.regressionFailureCount 1 'failed regression count'
}

$failed=@($results|Where-Object{$_.status-eq'failed'})
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESEvidenceVerifierRegistry';status=if($failed.Count){'failed'}else{'passed'};caseCount=$results.Count;passedCount=@($results|Where-Object{$_.status-eq'passed'}).Count;failedCount=$failed.Count;cases=@($results);runtimeStatus='runtime-not-run';claimsNotProven=@('Production registry distribution','Unity or Worker Runtime','release acceptance')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
