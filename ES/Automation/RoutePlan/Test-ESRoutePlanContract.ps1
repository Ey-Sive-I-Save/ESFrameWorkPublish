[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$RoutePlanSchemaPath='ES/Automation/Contracts/es-route-plan-v1.schema.json',
    [string]$RegistrySchemaPath='ES/Automation/Contracts/es-route-stage-registry-v1.schema.json',
    [string]$RegistryPath='ES/Automation/Contracts/es-route-stage.registry.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$scriptRoot=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $scriptRoot '..\..\..')).Path}
$strictUtf8=[Text.UTF8Encoding]::new($false,$true)
$root=(Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
function Full([string]$Path){[IO.Path]::GetFullPath((Join-Path $root $Path))}
function Read-Json([string]$Path){$strictUtf8.GetString([IO.File]::ReadAllBytes((Full $Path)))|ConvertFrom-Json -ErrorAction Stop}
function Clone($Value){$Value|ConvertTo-Json -Depth 40|ConvertFrom-Json}

Import-Module (Full 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force
Import-Module (Full 'ES/Automation/RoutePlan/ESRoutePlanContract.psm1') -Force
$routeSchema=Full $RoutePlanSchemaPath
$registrySchema=Full $RegistrySchemaPath
$registryFull=Full $RegistryPath
$registry=Read-Json $RegistryPath
$registryHash=(Get-FileHash -LiteralPath $registryFull -Algorithm SHA256).Hash.ToLowerInvariant()
$cases=[Collections.Generic.List[object]]::new()
function Add-Case([string]$Name,[bool]$Passed,[string[]]$Findings){
    [void]$cases.Add([pscustomobject][ordered]@{case=$Name;status=if($Passed){'passed'}else{'failed'};findings=@($Findings)})
}

function Test-RegistrySemantics($Value,[string[]]$SelectedSkills=@('es-skill-creator','es-skill-validator','es-static-deep-replay')){
    $errors=[Collections.Generic.List[string]]::new()
    $definitions=@($Value.stages|Where-Object{$SelectedSkills-ccontains[string]$_.skillName})
    foreach($skill in $SelectedSkills){
        $matches=@($definitions|Where-Object{[string]$_.skillName-ceq$skill-and@($_.profiles)-ccontains'governance'-and@($_.routeKeys|Where-Object{$_-in@('skill','validation','static-replay')}).Count-gt0})
        if($matches.Count-ne1){[void]$errors.Add("$skill must resolve to exactly one stage contract")}
    }
    $ids=@($definitions|ForEach-Object{[string]$_.stageContractId})
    if(@($ids|Sort-Object -Unique).Count-ne$ids.Count){[void]$errors.Add('stageContractId values are not unique')}
    $producer=@{}
    foreach($stage in $definitions){foreach($token in @($stage.produces)){
        if($producer.ContainsKey([string]$token)){[void]$errors.Add("duplicate product: $token")}else{$producer[[string]$token]=[string]$stage.stageContractId}
    }}
    $external=@($Value.externalInputs)
    $deps=@{}
    foreach($stage in $definitions){
        $set=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($token in @($stage.requires)){
            if($external-ccontains[string]$token){continue}
            if(-not$producer.ContainsKey([string]$token)){[void]$errors.Add("missing input: $token");continue}
            [void]$set.Add([string]$producer[[string]$token])
        }
        $deps[[string]$stage.stageContractId]=$set
    }
    $remaining=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($id in $ids){[void]$remaining.Add($id)}
    $depth=@{};$ordered=[Collections.Generic.List[string]]::new()
    while($remaining.Count-gt0){
        $ready=@($remaining|Where-Object{$id=$_;@($deps[$id]|Where-Object{-not$ordered.Contains($_)}).Count-eq0}|Sort-Object)
        if($ready.Count-eq0){[void]$errors.Add('dependency cycle');break}
        foreach($id in $ready){
            $parents=@($deps[$id]);$depth[$id]=if($parents.Count-eq0){0}else{1+($parents|ForEach-Object{[int]$depth[$_]}|Measure-Object -Maximum).Maximum}
            [void]$ordered.Add($id);[void]$remaining.Remove($id)
        }
    }
    foreach($stage in $definitions){
        $id=[string]$stage.stageContractId;if(-not$depth.ContainsKey($id)){continue};$d=[int]$depth[$id]
        if($d-gt[int]$Value.maxDepth){[void]$errors.Add("depth limit exceeded: $id")}
        if($d-gt[int]$Value.defaultMaxDepth){
            $auth=@($Value.depthAuthorizations|Where-Object{[string]$_.reasonCode-ceq[string]$stage.depthReasonCode-and[int]$_.authorizesDepth-eq$d-and@($_.profiles)-ccontains'governance'-and@($_.routeKeys|Where-Object{$_-in@('skill','validation','static-replay')}).Count-gt0})
            if($auth.Count-ne1){[void]$errors.Add("depth authorization missing: $id")}
        }elseif(-not[string]::IsNullOrEmpty([string]$stage.depthReasonCode)){[void]$errors.Add("depth reason misapplied: $id")}
    }
    @($errors)
}

$support=@(Test-ESJsonSchemaSupported -SchemaPath $routeSchema)+@(Test-ESJsonSchemaSupported -SchemaPath $registrySchema)
Add-Case 'schema-keyword-closure' ($support.Count-eq0) $support
$registryErrors=@(Test-ESJsonSchemaValue -SchemaPath $registrySchema -Value $registry)
Add-Case 'registry-structure' ($registryErrors.Count-eq0) $registryErrors
$semanticErrors=@(Test-RegistrySemantics $registry)
Add-Case 'registered-three-stage-route' ($semanticErrors.Count-eq0) $semanticErrors

$head=(git -C $root rev-parse HEAD).Trim().ToLowerInvariant()
$sourceRef=[ordered]@{projectPath=$RegistryPath;sha256=$registryHash}
$plan=[ordered]@{
    schemaVersion=1;contractId='es://automation/contracts/route-plan/v1';routePlanId=('route-'+('a'*32));routePlanHash=('b'*64)
    status='Ready';routeState='extension';evidenceState='closed';effect='review';profile='governance';scope='task-object';routeKeys=@('creator','skill','static-replay','validation')
    goalRevision=[ordered]@{goalId='goal-route';goalRevision='r1';revisionHash=('c'*64);projectPath='ES/Output/TaskContextRuntime/goals/goal-route/r1.json';artifactHash=('d'*64)}
    stages=@(
        [ordered]@{stageId='stage-01-es-skill-creator';stageContractId='es.route-stage.skill-creator.v1';skillName='es-skill-creator';depth=0;requires=@('goal-revision');produces=@('skill-candidate');failureConditions=@('candidate-write-denied','skill-contract-invalid');depthReasonCode='';executionStatus='not-executed'},
        [ordered]@{stageId='stage-02-es-skill-validator';stageContractId='es.route-stage.skill-validator.v1';skillName='es-skill-validator';depth=1;requires=@('skill-candidate');produces=@('static-validation');failureConditions=@('candidate-schema-invalid','validation-evidence-pending');depthReasonCode='';executionStatus='not-executed'},
        [ordered]@{stageId='stage-03-es-static-deep-replay';stageContractId='es.route-stage.static-deep-replay.v1';skillName='es-static-deep-replay';depth=2;requires=@('static-validation');produces=@('replay-receipt');failureConditions=@('replay-nondeterministic','source-snapshot-stale');depthReasonCode='ROUTE.DEPTH_2.STATIC_REPLAY_AFTER_VALIDATION';executionStatus='not-executed'}
    );maxDepth=2;budget=[ordered]@{maxReads=16}
    stopConditions=@([ordered]@{code='ROUTE.DEPTH_LIMIT';predicate='next depth exceeds budget';trigger='before next stage';outcome='stop-next-read';evidence=@($RegistryPath);recovery='reduce dependencies'})
    issues=@();snapshot=[ordered]@{head=$head;sourceRefs=@($sourceRef);sourceRefsHash=('e'*64);registryHash=$registryHash;coverage=[ordered]@{normalizationVersion='route-plan-canonical-v1';includes=@('route-stage-registry')}}
    compatibility=[ordered]@{legacyPlanStatus='Ready';projectionOnly=$true;productionRouteIntegrated=$false;globalP0Integrated=$false;executionAuthority='none'};executionEnabled=$false
}
$plan=Add-ESRoutePlanShadowIntegration -Core $plan -LegacyPlanStatus 'Ready'
$planErrors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value ([pscustomobject]$plan))
Add-Case 'representative-route-plan' ($planErrors.Count-eq0) $planErrors
Add-Case 'representative-shadow-candidate' ([string]$plan.shadowIntegration.candidateStatus-ceq'candidate-emitted'-and[string]$plan.shadowIntegration.decisionId-match'^route-decision-[a-f0-9]{32}$'-and[bool]$plan.shadowIntegration.verificationRequired-and-not[bool]$plan.shadowIntegration.stateChanged-and[string]$plan.shadowIntegration.rollbackAction-ceq'discard-shadow-candidate') $(if([string]$plan.shadowIntegration.candidateStatus-ceq'candidate-emitted'-and[string]$plan.shadowIntegration.decisionId-match'^route-decision-[a-f0-9]{32}$'-and[bool]$plan.shadowIntegration.verificationRequired-and-not[bool]$plan.shadowIntegration.stateChanged-and[string]$plan.shadowIntegration.rollbackAction-ceq'discard-shadow-candidate'){@()}else{@('selected governance/task-object shadow candidate was not emitted with the candidate-scoped rollback action')})

$cycle=Clone $registry;$cycle.stages[0].requires=@('replay-receipt');$errors=@(Test-RegistrySemantics $cycle);Add-Case 'dependency-cycle-negative' ($errors-ccontains'dependency cycle') $(if($errors-ccontains'dependency cycle'){@()}else{@('cycle was accepted')})
$missing=Clone $registry;$missing.stages[1].requires=@('missing-token');$errors=@(Test-RegistrySemantics $missing);Add-Case 'missing-input-negative' (@($errors|Where-Object{$_-like'missing input:*'}).Count-gt0) $(if(@($errors|Where-Object{$_-like'missing input:*'}).Count){@()}else{@('missing input was accepted')})
$duplicate=Clone $registry;$duplicate.stages[1].produces=@('skill-candidate');$errors=@(Test-RegistrySemantics $duplicate);Add-Case 'duplicate-product-negative' (@($errors|Where-Object{$_-like'duplicate product:*'}).Count-gt0) $(if(@($errors|Where-Object{$_-like'duplicate product:*'}).Count){@()}else{@('duplicate product was accepted')})
$errors=@(Test-RegistrySemantics $registry @('es-skill-creator','es-unregistered'));Add-Case 'unregistered-skill-negative' (@($errors|Where-Object{$_-like'es-unregistered*'}).Count-gt0) $(if(@($errors|Where-Object{$_-like'es-unregistered*'}).Count){@()}else{@('unregistered Skill was accepted')})
$noDepth=Clone $registry;$noDepth.depthAuthorizations=@();$errors=@(Test-RegistrySemantics $noDepth);Add-Case 'depth-membership-negative' (@($errors|Where-Object{$_-like'depth authorization missing:*'}).Count-gt0) $(if(@($errors|Where-Object{$_-like'depth authorization missing:*'}).Count){@()}else{@('unregistered depth 2 was accepted')})
$tooDeep=Clone $registry;$tooDeep.stages+= [pscustomobject][ordered]@{stageContractId='es.route-stage.final.v1';skillName='es-final';profiles=@('governance');routeKeys=@('skill');requires=@('replay-receipt');produces=@('final-receipt');failureConditions=@('final-failed');depthReasonCode=''};$errors=@(Test-RegistrySemantics $tooDeep @('es-skill-creator','es-skill-validator','es-static-deep-replay','es-final'));Add-Case 'depth-limit-negative' (@($errors|Where-Object{$_-like'depth limit exceeded:*'}).Count-gt0) $(if(@($errors|Where-Object{$_-like'depth limit exceeded:*'}).Count){@()}else{@('depth 3 was accepted')})
$execute=Clone ([pscustomobject]$plan);$execute.executionEnabled=$true;$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $execute);Add-Case 'route-plan-never-executes-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('executionEnabled=true was accepted')})
$production=Clone ([pscustomobject]$plan);$production.compatibility.productionRouteIntegrated=$true;$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $production);Add-Case 'production-takeover-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('production route takeover was accepted')})
$shadowProduction=Clone ([pscustomobject]$plan);$shadowProduction.shadowIntegration.productionRouteIntegrated=$true;$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $shadowProduction);Add-Case 'shadow-production-takeover-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('shadow production route takeover was accepted')})
$shadowGlobal=Clone ([pscustomobject]$plan);$shadowGlobal.shadowIntegration.globalP0Integrated=$true;$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $shadowGlobal);Add-Case 'shadow-global-p0-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('shadow global P0 takeover was accepted')})
$notSelected=Clone ([pscustomobject]$plan);$notSelected.profile='engineering';$notSelected=Add-ESRoutePlanShadowIntegration -Core $notSelected -LegacyPlanStatus 'Ready';$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $notSelected);Add-Case 'shadow-single-profile-not-selected' ($errors.Count-eq0-and[string]$notSelected.shadowIntegration.candidateStatus-ceq'not-selected'-and$null-eq$notSelected.shadowIntegration.decisionHash-and$null-eq$notSelected.shadowIntegration.decisionId) $(if($errors.Count-eq0-and[string]$notSelected.shadowIntegration.candidateStatus-ceq'not-selected'-and$null-eq$notSelected.shadowIntegration.decisionHash-and$null-eq$notSelected.shadowIntegration.decisionId){@()}else{@('non-selected Profile received a shadow decision or invalid projection')})
$badAlgorithm=Clone ([pscustomobject]$plan);$badAlgorithm.shadowIntegration.algorithmId='route-shadow-canonical-v0';$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $badAlgorithm);Add-Case 'shadow-algorithm-id-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('unregistered shadow algorithmId was accepted')})
$verificationDisabled=Clone ([pscustomobject]$plan);$verificationDisabled.shadowIntegration.verificationRequired=$false;$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $verificationDisabled);Add-Case 'shadow-verification-required-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('shadow candidate disabled independent verification')})
$illegalRollback=Clone ([pscustomobject]$plan);$illegalRollback.shadowIntegration.rollbackAction='mutate-legacy-plan';$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $illegalRollback);Add-Case 'shadow-rollback-action-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('shadow candidate expanded rollback into a legacy mutation')})
$observationRollback=Clone ([pscustomobject]$plan);$observationRollback.shadowIntegration.rollbackAction='discard-shadow-observation';$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $observationRollback);Add-Case 'shadow-observation-rollback-name-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('candidate contract accepted an observation-scoped rollback action')})
$producerSelfProof=Clone ([pscustomobject]$plan);$producerSelfProof.shadowIntegration|Add-Member -NotePropertyName decisionIdMatched -NotePropertyValue $true;$producerSelfProof.shadowIntegration|Add-Member -NotePropertyName bypassDetected -NotePropertyValue $false;$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $producerSelfProof);Add-Case 'shadow-producer-self-proof-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('producer-authored match or no-bypass claims were accepted')})
$missingObservation=Clone ([pscustomobject]$plan);$missingObservation.shadowIntegration.observationCodes=@('SHADOW.SCOPED_MATCH','SHADOW.NO_PRODUCTION_TAKEOVER');$threw=$false;try{Assert-ESRoutePlanShadowIntegration -RoutePlan $missingObservation}catch{$threw=$_.Exception.Message-like'*codes are incomplete or expanded*'};Add-Case 'shadow-observation-code-missing-negative' $threw $(if($threw){@()}else{@('missing rollback observation code was accepted')})
$expandedObservation=Clone ([pscustomobject]$plan);$expandedObservation.shadowIntegration.observationCodes+= 'SHADOW.PRODUCER_ASSERTED_MATCH';$threw=$false;try{Assert-ESRoutePlanShadowIntegration -RoutePlan $expandedObservation}catch{$threw=$_.Exception.Message-like'*codes are incomplete or expanded*'};Add-Case 'shadow-observation-code-expanded-negative' $threw $(if($threw){@()}else{@('unregistered producer observation code was accepted')})
$notSelectedWithDecision=Clone ([pscustomobject]$notSelected);$notSelectedWithDecision.shadowIntegration.decisionHash='0'*64;$notSelectedWithDecision.shadowIntegration.decisionId='route-decision-'+('0'*32);$threw=$false;try{Assert-ESRoutePlanShadowIntegration -RoutePlan $notSelectedWithDecision}catch{$threw=$_.Exception.Message-like'*expanded beyond its selected Profile/scope*'};Add-Case 'shadow-not-selected-decision-negative' $threw $(if($threw){@()}else{@('non-selected Profile carried a shadow decision')})
$stale=Clone ([pscustomobject]$plan);$stale.snapshot.registryHash=('0'*64);Add-Case 'registry-snapshot-drift-negative' ([string]$stale.snapshot.registryHash-cne$registryHash) $(if([string]$stale.snapshot.registryHash-cne$registryHash){@()}else{@('stale registry hash was not detected')})
$fakeHead=Clone ([pscustomobject]$plan);$fakeHead.snapshot.head='fixture';$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $fakeHead);Add-Case 'fake-head-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('non-SHA Git HEAD was accepted')})
$missingGoal=Clone ([pscustomobject]$plan);$missingGoal.goalRevision=$null;$errors=@(Test-ESJsonSchemaValue -SchemaPath $routeSchema -Value $missingGoal);Add-Case 'ready-requires-goal-negative' ($errors.Count-gt0) $(if($errors.Count){@()}else{@('Ready RoutePlan without GoalRevision was accepted')})

function Write-StrictJson([string]$Path,$Value){
    $parent=Split-Path -Parent $Path
    if(-not(Test-Path -LiteralPath $parent -PathType Container)){New-Item -ItemType Directory -Path $parent -Force|Out-Null}
    [IO.File]::WriteAllText($Path,($Value|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false))
}
function New-ArtifactFixture {
    $fixtureRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-route-plan-contract-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixtureRoot -Force|Out-Null
    $goalPath='goals/goal-route/r1.json'
    $goalCore=[ordered]@{schemaVersion=1;goalId='goal-route';goalRevision='r1';scope=@('source.txt');acceptanceIntent='static';status='frozen';budget=[ordered]@{maxReads=16};parentGoalRef=$null}
    $goalDocument=[ordered]@{}
    foreach($key in $goalCore.Keys){$goalDocument[$key]=$goalCore[$key]}
    $goalDocument.revisionHash=Get-ESRoutePlanCanonicalHash $goalCore
    $goalFull=Join-Path $fixtureRoot $goalPath
    Write-StrictJson $goalFull $goalDocument
    $registryTarget=Join-Path $fixtureRoot $RegistryPath
    $registryParent=Split-Path -Parent $registryTarget
    New-Item -ItemType Directory -Path $registryParent -Force|Out-Null
    [IO.File]::Copy($registryFull,$registryTarget,$true)
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'source.txt'),'route-plan-source',[Text.UTF8Encoding]::new($false))
    & git -C $fixtureRoot init -q 2>$null
    & git -C $fixtureRoot config user.name 'ES RoutePlan Fixture'
    & git -C $fixtureRoot config user.email 'route-plan-fixture@local.invalid'
    & git -C $fixtureRoot config core.autocrlf false
    & git -C $fixtureRoot add --all 2>$null
    & git -C $fixtureRoot commit -q --no-gpg-sign -m 'fixture snapshot' 2>$null
    if($LASTEXITCODE-ne0){throw 'failed to commit RoutePlan fixture'}
    $fixtureHead=((& git -C $fixtureRoot rev-parse HEAD 2>$null)|Select-Object -First 1).Trim().ToLowerInvariant()
    $goalArtifactHash=(Get-FileHash -LiteralPath $goalFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $fixtureRegistryHash=(Get-FileHash -LiteralPath $registryTarget -Algorithm SHA256).Hash.ToLowerInvariant()
    $sourceRefs=@(
        [ordered]@{projectPath=$RegistryPath;sha256=$fixtureRegistryHash},
        [ordered]@{projectPath=$goalPath;sha256=$goalArtifactHash}
    )|Sort-Object{[string]$_.projectPath}-CaseSensitive
    $core=[ordered]@{
        schemaVersion=1;contractId='es://automation/contracts/route-plan/v1';status='Ready';routeState='extension';evidenceState='closed';effect='review';profile='governance';scope='task-object'
        routeKeys=@('creator','skill','static-replay','validation')
        goalRevision=[ordered]@{goalId='goal-route';goalRevision='r1';revisionHash=[string]$goalDocument.revisionHash;projectPath=$goalPath;artifactHash=$goalArtifactHash}
        stages=@(
            [ordered]@{stageId='stage-01-es-skill-creator';stageContractId='es.route-stage.skill-creator.v1';skillName='es-skill-creator';depth=0;requires=@('goal-revision');produces=@('skill-candidate');failureConditions=@('candidate-write-denied','skill-contract-invalid');depthReasonCode='';executionStatus='not-executed'},
            [ordered]@{stageId='stage-02-es-skill-validator';stageContractId='es.route-stage.skill-validator.v1';skillName='es-skill-validator';depth=1;requires=@('skill-candidate');produces=@('static-validation');failureConditions=@('candidate-schema-invalid','validation-evidence-pending');depthReasonCode='';executionStatus='not-executed'},
            [ordered]@{stageId='stage-03-es-static-deep-replay';stageContractId='es.route-stage.static-deep-replay.v1';skillName='es-static-deep-replay';depth=2;requires=@('static-validation');produces=@('replay-receipt');failureConditions=@('replay-nondeterministic','source-snapshot-stale');depthReasonCode='ROUTE.DEPTH_2.STATIC_REPLAY_AFTER_VALIDATION';executionStatus='not-executed'}
        )
        maxDepth=2;budget=[ordered]@{maxReads=16}
        stopConditions=@([ordered]@{code='ROUTE.DEPTH_LIMIT';predicate='next depth exceeds budget';trigger='before next stage';outcome='stop-next-read';evidence=@($RegistryPath);recovery='reduce dependencies'})
        issues=@()
        snapshot=[ordered]@{head=$fixtureHead;sourceRefs=@($sourceRefs);sourceRefsHash=Get-ESRoutePlanCanonicalHash @($sourceRefs);registryHash=$fixtureRegistryHash;coverage=[ordered]@{normalizationVersion='route-plan-canonical-v1';includes=@('goal-revision-artifact','route-stage-registry')}}
        compatibility=[ordered]@{legacyPlanStatus='Ready';projectionOnly=$true;productionRouteIntegrated=$false;globalP0Integrated=$false;executionAuthority='none'}
        executionEnabled=$false
    }
    $core=Add-ESRoutePlanShadowIntegration -Core $core -LegacyPlanStatus 'Ready'
    $document=New-ESRoutePlanDocument -Core $core
    $planPath='route-plan.json'
    Write-StrictJson (Join-Path $fixtureRoot $planPath) $document
    [pscustomobject]@{Root=$fixtureRoot;PlanPath=$planPath;Plan=$document;Goal=[pscustomobject]@{goalId='goal-route';goalRevision='r1';goalRevisionHash=[string]$goalDocument.revisionHash;path=$goalPath;artifactHash=$goalArtifactHash};GoalFull=$goalFull;RegistryFull=$registryTarget;RegistryHash=$fixtureRegistryHash}
}
function Write-MutatedPlan($Fixture,[string]$FileName,[scriptblock]$Mutation,[switch]$PreserveShadow){
    $clone=Clone $Fixture.Plan
    & $Mutation $clone
    $core=Get-ESRoutePlanHashInput $clone
    if(-not$PreserveShadow){$core=Add-ESRoutePlanShadowIntegration -Core $core -LegacyPlanStatus ([string]$core.compatibility.legacyPlanStatus)}
    $document=New-ESRoutePlanDocument -Core $core
    Write-StrictJson (Join-Path $Fixture.Root $FileName) $document
    $FileName
}
function Invoke-ArtifactNegative([string]$Name,$Fixture,[string]$Path,[string]$MessagePattern){
    $threw=$false
    try{Resolve-ESRoutePlanArtifact -ProjectRoot $Fixture.Root -RoutePlanPath $Path -ExpectedGoal $Fixture.Goal -RequireReady|Out-Null}catch{$threw=$_.Exception.Message-like$MessagePattern}
    Add-Case $Name $threw $(if($threw){@()}else{@("RoutePlan negative was not rejected with $MessagePattern")})
}

$fixture=New-ArtifactFixture
$knownVector=Get-ESRoutePlanCanonicalHash ([ordered]@{b=2;a=1})
Add-Case 'canonical-hash-known-vector' ($knownVector-ceq'43258cff783fe7036d8a43033f830adfc60ec037382473548ac742b888292777') $(if($knownVector-ceq'43258cff783fe7036d8a43033f830adfc60ec037382473548ac742b888292777'){@()}else{@("unexpected canonical hash: $knownVector")})
$binding=$null
try{$binding=Resolve-ESRoutePlanArtifact -ProjectRoot $fixture.Root -RoutePlanPath $fixture.PlanPath -ExpectedGoal $fixture.Goal -RequireReady;Add-Case 'real-artifact-snapshot-replay' ($binding.routePlanHash-ceq[string]$fixture.Plan.routePlanHash) $(if($binding.routePlanHash-ceq[string]$fixture.Plan.routePlanHash){@()}else{@('real RoutePlan binding mismatch')})}catch{Add-Case 'real-artifact-snapshot-replay' $false @($_.Exception.Message)}
$shadowReplayPassed = $null -ne $binding -and
    [string]$binding.shadowCandidateStatus -ceq 'candidate-emitted' -and
    [string]$binding.shadowObservationStatus -ceq 'verified' -and
    [string]$binding.shadowDecisionId -ceq [string]$fixture.Plan.shadowIntegration.decisionId -and
    [bool]$binding.shadowDecisionIdMatched -and -not [bool]$binding.shadowBypassDetected -and
    [string]$binding.shadowRollbackState -ceq 'available' -and
    [string]$binding.shadowRollbackAction -ceq 'discard-shadow-candidate'
Add-Case 'real-artifact-shadow-replay' $shadowReplayPassed $(if($shadowReplayPassed){@()}else{@('real RoutePlan shadow candidate did not replay into a verified observation')})

$newSnapshot=Clone $fixture.Plan
$newSnapshot.snapshot.head='0'*40
$newSnapshot=Add-ESRoutePlanShadowIntegration -Core $newSnapshot -LegacyPlanStatus 'Ready'
Add-Case 'new-snapshot-new-shadow-decision-id' ([string]$newSnapshot.shadowIntegration.decisionId-cne[string]$fixture.Plan.shadowIntegration.decisionId) $(if([string]$newSnapshot.shadowIntegration.decisionId-cne[string]$fixture.Plan.shadowIntegration.decisionId){@()}else{@('shadow decisionId was reused across snapshots')})

$forged=Clone $fixture.Plan;$forged.routePlanHash='0'*64;Write-StrictJson (Join-Path $fixture.Root 'forged-hash.json') $forged
Invoke-ArtifactNegative 'forged-route-plan-hash-negative' $fixture 'forged-hash.json' '*RoutePlan hash mismatch*'
$path=Write-MutatedPlan $fixture 'missing-goal-ref.json' {param($p);$p.snapshot.sourceRefs=@($p.snapshot.sourceRefs|Where-Object{[string]$_.projectPath-cne[string]$p.goalRevision.projectPath});$p.snapshot.sourceRefsHash=Get-ESRoutePlanCanonicalHash @($p.snapshot.sourceRefs)}
Invoke-ArtifactNegative 'missing-goal-source-ref-negative' $fixture $path '*GoalRevision is not bound exactly once*'
$path=Write-MutatedPlan $fixture 'missing-registry-ref.json' {param($p);$p.snapshot.sourceRefs=@($p.snapshot.sourceRefs|Where-Object{[string]$_.projectPath-cne$RegistryPath});$p.snapshot.sourceRefsHash=Get-ESRoutePlanCanonicalHash @($p.snapshot.sourceRefs)}
Invoke-ArtifactNegative 'missing-registry-source-ref-negative' $fixture $path '*Registry is not bound exactly once*'
$path=Write-MutatedPlan $fixture 'forged-source-ref.json' {param($p);$p.snapshot.sourceRefs[0].sha256='0'*64;$p.snapshot.sourceRefsHash=Get-ESRoutePlanCanonicalHash @($p.snapshot.sourceRefs)}
Invoke-ArtifactNegative 'forged-source-ref-negative' $fixture $path '*SourceRef drift*'
$path=Write-MutatedPlan $fixture 'unregistered-stage.json' {param($p);$p.stages[2].stageContractId='es.route-stage.unregistered.v1'}
Invoke-ArtifactNegative 'unregistered-stage-artifact-negative' $fixture $path '*not registered exactly once*'
$path=Write-MutatedPlan $fixture 'profile-mismatch.json' {param($p);$p.profile='engineering'}
Invoke-ArtifactNegative 'stage-profile-artifact-negative' $fixture $path '*Profile mismatch*'
$path=Write-MutatedPlan $fixture 'route-key-mismatch.json' {param($p);$p.routeKeys=@('unrelated')}
Invoke-ArtifactNegative 'stage-route-key-artifact-negative' $fixture $path '*routeKey mismatch*'
$path=Write-MutatedPlan $fixture 'fake-current-head.json' {param($p);$p.snapshot.head='0'*40}
Invoke-ArtifactNegative 'git-head-replay-negative' $fixture $path '*Git HEAD drift*'
$path=Write-MutatedPlan $fixture 'shadow-id-mismatch.json' {param($p);$p.shadowIntegration.decisionId='route-decision-'+('0'*32)} -PreserveShadow
Invoke-ArtifactNegative 'shadow-decision-id-mismatch-negative' $fixture $path '*shadow decisionId or snapshot binding mismatch*'
$path=Write-MutatedPlan $fixture 'shadow-legacy-status-drift.json' {param($p);$p.shadowIntegration.legacyPlanStatusAfter='Blocked'} -PreserveShadow
Invoke-ArtifactNegative 'shadow-legacy-status-drift-negative' $fixture $path '*changed or mismatched the legacy plan status*'

$goalBytes=[IO.File]::ReadAllBytes($fixture.GoalFull)
try{[IO.File]::AppendAllText($fixture.GoalFull,"`n",[Text.UTF8Encoding]::new($false));Invoke-ArtifactNegative 'goal-artifact-drift-negative' $fixture $fixture.PlanPath '*SourceRef drift*'}finally{[IO.File]::WriteAllBytes($fixture.GoalFull,$goalBytes)}
$registryBytes=[IO.File]::ReadAllBytes($fixture.RegistryFull)
try{[IO.File]::AppendAllText($fixture.RegistryFull,"`n",[Text.UTF8Encoding]::new($false));Invoke-ArtifactNegative 'registry-artifact-drift-negative' $fixture $fixture.PlanPath '*SourceRef drift*'}finally{[IO.File]::WriteAllBytes($fixture.RegistryFull,$registryBytes)}
try{
    $fixtureRegistry=Read-Json $RegistryPath
    $fixtureRegistry.depthAuthorizations=@()
    Write-StrictJson $fixture.RegistryFull $fixtureRegistry
    $changedRegistryHash=(Get-FileHash -LiteralPath $fixture.RegistryFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $path=Write-MutatedPlan $fixture 'depth-auth-missing.json' {param($p);$p.snapshot.registryHash=$changedRegistryHash;foreach($ref in $p.snapshot.sourceRefs){if([string]$ref.projectPath-ceq$RegistryPath){$ref.sha256=$changedRegistryHash}};$p.snapshot.sourceRefsHash=Get-ESRoutePlanCanonicalHash @($p.snapshot.sourceRefs)}
    Invoke-ArtifactNegative 'depth-authorization-artifact-negative' $fixture $path '*depth authorization is missing*'
}finally{[IO.File]::WriteAllBytes($fixture.RegistryFull,$registryBytes)}

$coordinator=$strictUtf8.GetString([IO.File]::ReadAllBytes((Full 'Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs')))
$bridge=$strictUtf8.GetString([IO.File]::ReadAllBytes((Full 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs')))
$wiring=@('BuildReadOnlyRoutePlan','BuildRouteShadowIntegration','ComputeRouteShadowDecisionHash','read-only-shadow','shadowIntegration','RouteStageRegistryPath','routePlan = plan.routePlan','routePlan.routeKeys','public readonly List<string> routeKeys','executionEnabled = false','productionRouteIntegrated = false','globalP0Integrated = false')|Where-Object{$coordinator-notmatch[regex]::Escape($_)}
if($bridge-notmatch'goalRevisionPath'-or$bridge-notmatch'routeProfileId'){$wiring+= 'Bridge optional Goal/Profile binding'}
Add-Case 'aibrain-read-only-wiring' (@($wiring).Count-eq0) @($wiring)
$taskRuntime=$strictUtf8.GetString([IO.File]::ReadAllBytes((Full 'ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1')))
$taskFixture=$strictUtf8.GetString([IO.File]::ReadAllBytes((Full 'ES/Automation/TaskContextRuntime/Test-ESTaskContextRoutePlanFixture.ps1')))
$sharedMissing=@()
if($taskRuntime-notmatch'Resolve-ESRoutePlanArtifact'-or$taskRuntime-notmatch'RoutePlanModulePath'){$sharedMissing+='TaskContextRuntime does not consume the shared RoutePlan module'}
if($taskFixture-notmatch'New-ESRoutePlanDocument'-or$taskFixture-match'ConvertTo-ESTestCanonicalJson'){$sharedMissing+='TaskContext fixture duplicates or bypasses the shared canonical implementation'}
Add-Case 'shared-canonical-caller-closure' ($sharedMissing.Count-eq0) @($sharedMissing)

$failed=@($cases|Where-Object status -eq 'failed')
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESRoutePlanContract';status=if($failed.Count){'failed'}else{'passed'};caseCount=$cases.Count;passedCount=@($cases|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;cases=@($cases);registryHash=$registryHash;shadowIntegration='source-wired-read-only';shadowProfile='governance';shadowScope='task-object';productionRouteIntegrated=$false;globalP0Integrated=$false;runtimeStatus='runtime-not-run';claimsNotProven=@('Unity serialization and planTask Runtime','production route adoption','global P0 integration')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
