[CmdletBinding()]
param([string]$ModulePath)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ModulePath)){$ModulePath=Join-Path $PSScriptRoot 'ESTaskContextRuntime.psm1'}
Import-Module $ModulePath -Force
. (Join-Path $PSScriptRoot 'Test-ESTaskContextRoutePlanFixture.ps1')
$evidenceContractPath=Join-Path $PSScriptRoot '..\Contracts\es-platform-evidence-v1.schema.json'
$evidenceContractId='es://automation/contracts/platform-evidence/v1'
$evidenceContractHash=(Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-task-context-runtime-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
Initialize-ESTestRoutePlanRepository $testRoot
$results=[System.Collections.Generic.List[object]]::new()

function Assert-Equal($Actual,$Expected,[string]$Message){if([string]$Actual -cne [string]$Expected){throw "$Message Expected=$Expected Actual=$Actual"}}
function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Invoke-Case([string]$Name,[scriptblock]$Body){
    try{& $Body;[void]$results.Add([pscustomobject]@{case=$Name;status='passed';finding=$null})}
    catch{[void]$results.Add([pscustomobject]@{case=$Name;status='failed';finding=$_.Exception.Message})}
}
function New-Fixture([string]$Name){
    $root=Join-Path $testRoot $Name;New-Item -ItemType Directory -Path $root|Out-Null
    [IO.File]::WriteAllText((Join-Path $root 'source.txt'),"source:$Name",[Text.UTF8Encoding]::new($false))
    return $root
}
function New-State([string]$Root,[string]$TaskId='task'){
    $goal=New-ESGoalRevision -ProjectRoot $Root -StoreRoot 'state' -GoalId ('goal-' + $TaskId) -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8;maxSeconds=30})
    $routePlan=New-ESTestRoutePlan -Root $Root -Goal $goal
    New-ESTaskContextTask -ProjectRoot $Root -StoreRoot 'state' -TaskId $TaskId -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'
}
function Confirm-State([string]$Root,$State,[string]$Key='verify'){
    Confirm-ESTaskSourceScope -ProjectRoot $Root -StoreRoot 'state' -TaskId $State.taskId -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IdempotencyKey $Key
}
function Write-Evidence([string]$Root,$State,[string]$Outcome='passed',[int]$AgeHours=0,[bool]$CriticalContradiction=$false,[string[]]$UnverifiedClaims=@(),[bool]$Legacy=$false){
    $captured=[DateTime]::UtcNow.AddHours(-$AgeHours).ToString('o')
    $contradictions=@()
    if($CriticalContradiction){$contradictions=@([ordered]@{critical=$true;description='test contradiction'})}
    $artifactName='artifact-' + [Guid]::NewGuid().ToString('N') + '.json'
    [object[]]$observations = @()
    if($Outcome -ne 'unverified'){$observations += [pscustomobject][ordered]@{path='source.txt';expectedSha256=if($Outcome -eq 'passed'){[string]$State.verifiedSourceScope[0].sha256}else{'0'*64}}}
    $artifactPayload=[ordered]@{schemaVersion=1;claimId='source-integrity';sourceScopeHash=$State.verifiedSourceScopeHash;observations=$observations}
    $artifactFull=Join-Path $Root $artifactName
    [IO.File]::WriteAllText($artifactFull,($artifactPayload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $artifactHash=(Get-FileHash -LiteralPath $artifactFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $payload=if($Legacy){
        [ordered]@{schemaVersion=1;taskId=[string]$State.taskId;evidenceSetId=('evidence-' + [Guid]::NewGuid().ToString('N').Substring(0,12));capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';outcome=$Outcome;capturedUtc=$captured;sourceScopeHash=$State.verifiedSourceScopeHash;evidenceHash=$artifactHash;producerType='platform';artifactPath=$artifactName});contradictions=$contradictions;sourceDrift=@();unverifiedClaims=@($UnverifiedClaims)}
    }else{
        [ordered]@{schemaVersion=1;contractId=$evidenceContractId;contractHash=$evidenceContractHash;recordType='CandidateEvidenceSet';taskId=[string]$State.taskId;evidenceSetId=('evidence-' + [Guid]::NewGuid().ToString('N').Substring(0,12));capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';candidateOutcome=$Outcome;capturedUtc=$captured;sourceScopeHash=$State.verifiedSourceScopeHash;candidateEvidenceHash=$artifactHash;candidateProducerType='platform';artifactPath=$artifactName});contradictions=$contradictions;sourceDrift=@();unverifiedClaims=@($UnverifiedClaims)}
    }
    $name='evidence-' + [Guid]::NewGuid().ToString('N') + '.json'
    [IO.File]::WriteAllText((Join-Path $Root $name),($payload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    return $name
}
function Submit-State([string]$Root,$State,[string]$Outcome='passed',[int]$AgeHours=0,[bool]$CriticalContradiction=$false,[string[]]$UnverifiedClaims=@(),[string]$Key='evidence'){
    $path=Write-Evidence $Root $State $Outcome $AgeHours $CriticalContradiction $UnverifiedClaims
    Submit-ESTaskEvidenceSet -ProjectRoot $Root -StoreRoot 'state' -TaskId $State.taskId -EvidenceSetPath $path -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IdempotencyKey $Key
}
function Complete-State([string]$Root,$State,[string]$Key='complete'){
    Complete-ESTaskContextTask -ProjectRoot $Root -StoreRoot 'state' -TaskId $State.taskId -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IdempotencyKey $Key
}
function New-AcceptedState([string]$Root){$s=New-State $Root;$s=Confirm-State $Root $s;$s=Submit-State $Root $s;$s=Complete-State $Root $s;return $s}

Invoke-Case 'accepted-transition' {
    $root=New-Fixture 'accepted';$s=New-AcceptedState $root
    Assert-Equal $s.taskStatus 'Completed' 'TaskStatus';Assert-Equal $s.contextStatus 'Frozen' 'ContextStatus';Assert-Equal $s.completionDecision 'accepted' 'completionDecision';Assert-Equal $s.deliveryAcceptance 'pending' 'deliveryAcceptance'
    Assert-Equal $s.evidenceSet.items[0].producerType 'platform' 'normalized producerType';Assert-Equal $s.evidenceSet.items[0].verifierId 'platform.file-hash-manifest-v1' 'selected verifierId'
    Assert-Equal $s.evidenceSet.inputContractMode 'canonical-v1' 'input contract mode';Assert-Equal $s.evidenceSet.contractHash $s.acceptanceProfile.evidenceContractHash 'evidence contract binding'
    $integrity=Test-ESTaskContextIntegrity -ProjectRoot $root -StoreRoot 'state' -TaskId 'task';Assert-Equal $integrity.status 'passed' 'integrity'
}
Invoke-Case 'legacy-candidate-evidence-is-projected' {
    $root=New-Fixture 'legacy-candidate';$s=New-State $root;$s=Confirm-State $root $s;$path=Write-Evidence $root $s 'passed' 0 $false @() $true
    $s=Submit-ESTaskEvidenceSet -ProjectRoot $root -StoreRoot 'state' -TaskId $s.taskId -EvidenceSetPath $path -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'legacy-evidence'
    Assert-Equal $s.evidenceSet.inputContractMode 'legacy-task-context-v1' 'legacy input contract mode';$s=Complete-State $root $s 'legacy-complete';Assert-Equal $s.completionDecision 'accepted' 'legacy completion decision'
}
Invoke-Case 'rejected-transition' {
    $root=New-Fixture 'rejected';$s=New-State $root;$s=Confirm-State $root $s;$s=Submit-State $root $s 'failed';$s=Complete-State $root $s
    Assert-Equal $s.completionDecision 'rejected' 'completionDecision';Assert-Equal $s.taskStatus 'Blocked' 'TaskStatus';Assert-Equal $s.contextStatus 'Live' 'ContextStatus'
}
Invoke-Case 'undetermined-transition' {
    $root=New-Fixture 'undetermined';$s=New-State $root;$s=Confirm-State $root $s;$s=Complete-State $root $s
    Assert-Equal $s.completionDecision 'undetermined' 'completionDecision';Assert-Equal $s.taskStatus 'Active' 'TaskStatus';Assert-Equal $s.contextStatus 'Live' 'ContextStatus'
}
Invoke-Case 'missing-optional-observation-does-not-block-completion' {
    $root=New-Fixture 'optional-observation';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal-optional' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal
    $s=New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -OptionalClaim 'interaction-correction' -OptionalClaimVerifier ([ordered]@{'interaction-correction'='platform.codex-transcript-slice-v1'}) -InteractionSessionId ('a'*32) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'
    $s=Confirm-State $root $s;$s=Submit-State $root $s;$s=Complete-State $root $s
    Assert-Equal $s.completionDecision 'accepted' 'optional observation changed completionDecision';Assert-Equal $s.taskStatus 'Completed' 'optional observation changed TaskStatus'
}
Invoke-Case 'transcript-observation-requires-frozen-session' {
    $root=New-Fixture 'optional-session-missing';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal-optional-session' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal;$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -OptionalClaim 'interaction-correction' -OptionalClaimVerifier ([ordered]@{'interaction-correction'='platform.codex-transcript-slice-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message-eq'Transcript observation claims require a frozen InteractionSessionId.'}
    Assert-True $threw 'A transcript observation claim without a frozen session was accepted.'
}
Invoke-Case 'missing-route-plan-is-rejected' {
    $root=New-Fixture 'route-plan-missing';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -RoutePlanPath 'missing-route-plan.json' -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message-like'RoutePlan project file is missing:*'}
    Assert-True $threw 'A task without a RoutePlan artifact was accepted.';Assert-True (-not(Test-Path -LiteralPath (Join-Path $root 'state/task'))) 'Rejected RoutePlan create wrote partial task state.'
}
Invoke-Case 'forged-route-plan-hash-is-rejected' {
    $root=New-Fixture 'route-plan-forged-hash';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal
    $payload=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($routePlan.fullPath))|ConvertFrom-Json;$payload.routePlanHash='0'*64;[IO.File]::WriteAllText($routePlan.fullPath,($payload|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false));$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message-eq'RoutePlan hash mismatch.'}
    Assert-True $threw 'A forged RoutePlan hash was accepted.'
}
Invoke-Case 'free-plan-hash-cannot-bypass-route-plan' {
    $root=New-Fixture 'route-plan-free-hash';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal;$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash ('f'*64) -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message-eq'PlanHash must equal the platform-verified RoutePlan hash.'}
    Assert-True $threw 'A caller-selected PlanHash bypassed the RoutePlan binding.'
}
Invoke-Case 'route-plan-goal-mismatch-is-rejected' {
    $root=New-Fixture 'route-plan-goal-mismatch';$goalA=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal-a' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'a' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goalA
    $goalB=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal-b' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'b' -Budget ([ordered]@{maxReads=8});$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goalB.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message-eq'RoutePlan GoalRevision binding mismatch.'}
    Assert-True $threw 'A RoutePlan was rebound to another GoalRevision.'
}
Invoke-Case 'executable-route-plan-is-rejected' {
    $root=New-Fixture 'route-plan-executable';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal
    $payload=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($routePlan.fullPath))|ConvertFrom-Json;$payload.executionEnabled=$true;[IO.File]::WriteAllText($routePlan.fullPath,($payload|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false));$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message-like'RoutePlan schema validation failed:*'}
    Assert-True $threw 'A RoutePlan with executionEnabled=true was accepted.'
}
Invoke-Case 'route-plan-artifact-tamper-before-completion-is-undetermined' {
    $root=New-Fixture 'route-plan-artifact-tamper';$s=New-State $root;$s=Confirm-State $root $s;$s=Submit-State $root $s
    [IO.File]::AppendAllText((Join-Path $root ([string]$s.routePlan.routePlanPath)),[Environment]::NewLine,[Text.UTF8Encoding]::new($false));$s=Complete-State $root $s 'route-plan-tamper-complete'
    Assert-Equal $s.completionDecision 'undetermined' 'completionDecision after RoutePlan artifact tamper';Assert-Equal $s.taskStatus 'Active' 'TaskStatus after RoutePlan artifact tamper'
    $eventPath=(Get-ChildItem -LiteralPath (Join-Path $root 'state/task/events') -File|Sort-Object Name|Select-Object -Last 1).FullName;$event=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($eventPath))|ConvertFrom-Json;Assert-True (@($event.metadata.reasons)-contains'RoutePlanDrift') 'RoutePlanDrift reason was not recorded.'
}
Invoke-Case 'route-plan-registry-drift-before-completion-is-undetermined' {
    $root=New-Fixture 'route-plan-registry-drift';$s=New-State $root;$s=Confirm-State $root $s;$s=Submit-State $root $s
    [IO.File]::AppendAllText((Join-Path $root 'ES/Automation/Contracts/es-route-stage.registry.json'),[Environment]::NewLine,[Text.UTF8Encoding]::new($false));$s=Complete-State $root $s 'route-registry-drift-complete'
    Assert-Equal $s.completionDecision 'undetermined' 'completionDecision after RoutePlan Registry drift';Assert-Equal $s.contextStatus 'Live' 'ContextStatus after RoutePlan Registry drift'
}
Invoke-Case 'goal-revision-hash-tamper-is-rejected' {
    $root=New-Fixture 'goal-hash-tamper';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8})
    $routePlan=New-ESTestRoutePlan -Root $root -Goal $goal
    $full=Join-Path $root $goal.path;$payload=Get-Content -LiteralPath $full -Encoding UTF8 -Raw|ConvertFrom-Json;$payload.budget.maxReads=99
    [IO.File]::WriteAllText($full,($payload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message -eq 'GoalRevision hash mismatch.'}
    Assert-True $threw 'Tampered GoalRevision was accepted.'
}
Invoke-Case 'goal-revision-must-be-frozen' {
    $root=New-Fixture 'goal-not-frozen';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8})
    $routePlan=New-ESTestRoutePlan -Root $root -Goal $goal
    $full=Join-Path $root $goal.path;$payload=Get-Content -LiteralPath $full -Encoding UTF8 -Raw|ConvertFrom-Json;$payload.status='active'
    [IO.File]::WriteAllText($full,($payload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message -eq 'GoalRevision must be schemaVersion 1 and frozen.'}
    Assert-True $threw 'Non-frozen GoalRevision was accepted.'
}
Invoke-Case 'goal-revision-is-immutable' {
    $root=New-Fixture 'goal-immutable';$null=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$threw=$false
    try{New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=9})|Out-Null}catch{$threw=$_.Exception.Message -eq 'GoalRevision already exists with different content.'}
    Assert-True $threw 'GoalRevision content was overwritten.'
}
Invoke-Case 'goal-revision-drift-before-completion-is-undetermined' {
    $root=New-Fixture 'goal-drift-completion';$s=New-State $root;$s=Confirm-State $root $s;$s=Submit-State $root $s
    $goalFull=Join-Path $root ([string]$s.goalRevisionPath);$goalPayload=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($goalFull))|ConvertFrom-Json;$goalPayload.budget.maxReads=99
    [IO.File]::WriteAllText($goalFull,($goalPayload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$s=Complete-State $root $s 'goal-drift-complete'
    Assert-Equal $s.completionDecision 'undetermined' 'completionDecision after GoalRevision drift';Assert-Equal $s.taskStatus 'Active' 'TaskStatus after GoalRevision drift'
}
Invoke-Case 'unregistered-required-verifier-is-rejected' {
    $root=New-Fixture 'unregistered-verifier';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal;$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.missing-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message -like 'Evidence verifier is not registered exactly once:*'}
    Assert-True $threw 'Unregistered required verifier was accepted.'
}
Invoke-Case 'missing-required-verifier-binding-is-rejected' {
    $root=New-Fixture 'missing-verifier-binding';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal;$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message -eq 'Every required claim needs an explicit verifier binding.'}
    Assert-True $threw 'Required claim without an explicit verifier binding was accepted.'
}
Invoke-Case 'verifier-claim-scope-mismatch-is-rejected' {
    $root=New-Fixture 'verifier-claim-scope';$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $root -Goal $goal;$threw=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'compile-passed' -RequiredClaimVerifier ([ordered]@{'compile-passed'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$_.Exception.Message -eq 'Evidence verifier does not support required claim: compile-passed'}
    Assert-True $threw 'A source-integrity verifier was allowed to prove a compile claim.'
}
Invoke-Case 'delivery-rejection-does-not-rollback' {
    $root=New-Fixture 'delivery';$s=New-AcceptedState $root
    $receiptHash=$s.completionReceipt.receiptHash
    $s=Set-ESTaskDeliveryAcceptance -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -DeliveryAcceptance rejected -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'delivery-rejected'
    Assert-Equal $s.deliveryAcceptance 'rejected' 'deliveryAcceptance';Assert-Equal $s.completionDecision 'accepted' 'completionDecision';Assert-Equal $s.completionReceipt.receiptHash $receiptHash 'receiptHash'
}
Invoke-Case 'delivery-acceptance-is-final' {
    $root=New-Fixture 'delivery-final';$s=New-AcceptedState $root
    $s=Set-ESTaskDeliveryAcceptance -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -DeliveryAcceptance accepted -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'delivery-accepted'
    $threw=$false
    try{Set-ESTaskDeliveryAcceptance -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -DeliveryAcceptance rejected -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'delivery-overwrite'|Out-Null}catch{$threw=$_.Exception.Message -like 'Delivery acceptance is final*'}
    Assert-True $threw 'A final delivery acceptance was overwritten.'
}
Invoke-Case 'illegal-transition' {
    $root=New-Fixture 'illegal';$s=New-State $root;$threw=$false
    try{Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Archive -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'bad-archive'|Out-Null}catch{$threw=$true}
    Assert-True $threw 'Active task incorrectly archived.'
}
Invoke-Case 'cas-conflict' {
    $root=New-Fixture 'cas';$s=New-State $root;$threw=$false
    try{Confirm-ESTaskSourceScope -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision 99 -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'stale-cas'|Out-Null}catch{$threw=$_.Exception.Message -like 'CAS conflict:*'}
    Assert-True $threw 'Stale CAS was not rejected.'
}
Invoke-Case 'repeat-idempotency' {
    $root=New-Fixture 'idempotent';$s=New-State $root;$first=Confirm-State $root $s 'same-key';$second=Confirm-ESTaskSourceScope -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision 1 -ExpectedContextVersion 1 -IdempotencyKey 'same-key'
    Assert-Equal $second.taskRevision $first.taskRevision 'idempotent revision';$integrity=Test-ESTaskContextIntegrity -ProjectRoot $root -StoreRoot 'state' -TaskId 'task';Assert-Equal $integrity.eventCount 2 'event count'
}
Invoke-Case 'idempotency-key-reuse-conflict' {
    $root=New-Fixture 'idempotency-conflict';$s=New-State $root;$s=Confirm-State $root $s 'shared-key';$threw=$false
    try{Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Suspend -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'shared-key'|Out-Null}catch{$threw=$_.Exception.Message -eq 'IdempotencyKey is already bound to a different operation.'}
    Assert-True $threw 'Cross-operation idempotency key reuse was not rejected.'
}
Invoke-Case 'evidence-task-identity-mismatch' {
    $root=New-Fixture 'evidence-identity';$s=New-State $root;$s=Confirm-State $root $s;$path=Write-Evidence $root $s
    $payload=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Join-Path $root $path)))|ConvertFrom-Json;$payload.taskId='other-task';[IO.File]::WriteAllText((Join-Path $root $path),($payload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$threw=$false
    try{Submit-ESTaskEvidenceSet -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath $path -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'wrong-evidence-task'|Out-Null}catch{$threw=$_.Exception.Message -eq 'EvidenceSet taskId does not match the target task.'}
    Assert-True $threw 'Evidence for another task was accepted.'
}
Invoke-Case 'candidate-evidence-without-verifier-is-unverified' {
    $root=New-Fixture 'candidate-unverified';$s=New-State $root;$s=Confirm-State $root $s;$path=Write-Evidence $root $s
    $payload=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Join-Path $root $path)))|ConvertFrom-Json
    $payload.items[0].artifactPath=$null;$payload.items[0].candidateProducerType='worker'
    [IO.File]::WriteAllText((Join-Path $root $path),($payload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $s=Submit-ESTaskEvidenceSet -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath $path -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'candidate-unverified-submit'
    Assert-Equal $s.evidenceSet.items[0].outcome 'unverified' 'candidate outcome was trusted without a verifier'
    Assert-Equal $s.evidenceSet.items[0].verificationStatus 'unverified' 'candidate verification status'
    Assert-True ([string]$s.evidenceSet.items[0].evidenceHash -cne [string]$s.evidenceSet.items[0].candidateEvidenceHash) 'unverified candidate hash was copied as platform evidence hash'
    Assert-Equal $s.evidenceSet.items[0].producerType 'unverified' 'candidate producer identity was trusted without verification'
    Assert-Equal $s.evidenceSet.items[0].candidateProducerType 'worker' 'candidate producer identity was not preserved'
    $s=Complete-State $root $s 'candidate-unverified-complete';Assert-Equal $s.completionDecision 'undetermined' 'completionDecision'
}
Invoke-Case 'forged-evidence-hash-is-rejected' {
    $root=New-Fixture 'forged-evidence-hash';$s=New-State $root;$s=Confirm-State $root $s;$path=Write-Evidence $root $s
    $payload=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Join-Path $root $path)))|ConvertFrom-Json;$payload.items[0].candidateEvidenceHash=('0'*64)
    [IO.File]::WriteAllText((Join-Path $root $path),($payload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$threw=$false
    try{Submit-ESTaskEvidenceSet -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath $path -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'forged-hash'|Out-Null}catch{$threw=$_.Exception.Message -like 'Evidence artifact hash mismatch:*'}
    Assert-True $threw 'Forged evidence hash was accepted.'
}
Invoke-Case 'artifact-observation-outside-source-scope-is-rejected' {
    $root=New-Fixture 'artifact-scope';[IO.File]::WriteAllText((Join-Path $root 'outside.txt'),'outside',[Text.UTF8Encoding]::new($false));$s=New-State $root;$s=Confirm-State $root $s;$path=Write-Evidence $root $s
    $payload=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Join-Path $root $path)))|ConvertFrom-Json
    $artifactFull=Join-Path $root ([string]$payload.items[0].artifactPath);$artifact=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($artifactFull))|ConvertFrom-Json
    $artifact.observations[0].path='outside.txt';$artifact.observations[0].expectedSha256=(Get-FileHash -LiteralPath (Join-Path $root 'outside.txt') -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($artifactFull,($artifact|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$payload.items[0].candidateEvidenceHash=(Get-FileHash -LiteralPath $artifactFull -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $root $path),($payload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$threw=$false
    try{Submit-ESTaskEvidenceSet -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath $path -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'scope-observation'|Out-Null}catch{$threw=$_.Exception.Message -like 'Evidence observation is outside the verified sourceScope:*'}
    Assert-True $threw 'Artifact observation outside verified sourceScope was accepted.'
}
Invoke-Case 'artifact-tamper-is-detected-at-completion' {
    $root=New-Fixture 'artifact-tamper';$s=New-State $root;$s=Confirm-State $root $s;$s=Submit-State $root $s
    $artifact=Join-Path $root ([string]$s.evidenceSet.items[0].artifactPath)
    [IO.File]::WriteAllText($artifact,'{"schemaVersion":1,"claimId":"source-integrity","sourceScopeHash":"' + $s.verifiedSourceScopeHash + '","observations":[],"tampered":true}',[Text.UTF8Encoding]::new($false))
    $s=Complete-State $root $s 'artifact-tamper-complete';Assert-Equal $s.completionDecision 'undetermined' 'completionDecision after artifact tamper'
}
Invoke-Case 'candidate-outcome-mismatch-is-rejected' {
    $root=New-Fixture 'candidate-outcome-mismatch';$s=New-State $root;$s=Confirm-State $root $s;$path=Write-Evidence $root $s
    $payload=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Join-Path $root $path)))|ConvertFrom-Json;$payload.items[0].candidateOutcome='failed'
    [IO.File]::WriteAllText((Join-Path $root $path),($payload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$threw=$false
    try{Submit-ESTaskEvidenceSet -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath $path -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'outcome-mismatch'|Out-Null}catch{$threw=$_.Exception.Message -like 'Candidate outcome does not match platform-derived artifact outcome:*'}
    Assert-True $threw 'Candidate outcome mismatch was accepted.'
}
Invoke-Case 'source-drift-partial-invalidation' {
    $root=New-Fixture 'drift';$s=New-State $root;$s=Confirm-State $root $s;$s=Submit-State $root $s
    [IO.File]::WriteAllText((Join-Path $root 'source.txt'),'changed',[Text.UTF8Encoding]::new($false));$s=Complete-State $root $s
    Assert-Equal $s.completionDecision 'undetermined' 'completionDecision';Assert-Equal $s.contextStatus 'PartiallyInvalidated' 'ContextStatus'
}
Invoke-Case 'receipt-tamper' {
    $root=New-Fixture 'tamper';$s=New-AcceptedState $root;$receipt=Join-Path $root ('state/'+$s.completionReceipt.path)
    $obj=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($receipt))|ConvertFrom-Json;$obj.planHash=('c'*64);[IO.File]::WriteAllText($receipt,($obj|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $integrity=Test-ESTaskContextIntegrity -ProjectRoot $root -StoreRoot 'state' -TaskId 'task';Assert-Equal $integrity.status 'failed' 'tampered integrity'
}
Invoke-Case 'receipt-tamper-blocks-idempotent-retry' {
    $root=New-Fixture 'tamper-retry';$s=New-State $root;$s=Confirm-State $root $s;$s=Submit-State $root $s;$expectedRevision=[int]$s.taskRevision;$expectedContext=[int]$s.contextVersion;$s=Complete-State $root $s 'complete-retry'
    $receipt=Join-Path $root ('state/'+$s.completionReceipt.path);$obj=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($receipt))|ConvertFrom-Json;$obj.planHash=('c'*64);[IO.File]::WriteAllText($receipt,($obj|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$threw=$false
    try{Complete-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $expectedRevision -ExpectedContextVersion $expectedContext -IdempotencyKey 'complete-retry'|Out-Null}catch{$threw=$_.Exception.Message -eq 'Completion receipt hash mismatch.'}
    Assert-True $threw 'Tampered accepted Receipt did not block an idempotent completion retry.'
}
Invoke-Case 'orphan-receipt-is-not-authoritative' {
    $root=New-Fixture 'orphan';$s=New-State $root;$receiptRoot=Join-Path $root 'state/task/receipts';New-Item -ItemType Directory -Path $receiptRoot|Out-Null
    [IO.File]::WriteAllText((Join-Path $receiptRoot 'orphan.json'),'{}',[Text.UTF8Encoding]::new($false));$integrity=Test-ESTaskContextIntegrity -ProjectRoot $root -StoreRoot 'state' -TaskId 'task'
    Assert-Equal $integrity.status 'passed' 'integrity';Assert-Equal $integrity.orphanReceiptCount 1 'orphan count';Assert-Equal $integrity.orphanReceiptsAuthoritative $false 'orphan authority'
}
Invoke-Case 'interruption-recovery' {
    $root=New-Fixture 'recovery';$s=New-State $root;$s=Confirm-State $root $s;$s=Submit-State $root $s;$receiptRoot=Join-Path $root 'state/task/receipts';New-Item -ItemType Directory -Path $receiptRoot -Force|Out-Null
    [IO.File]::WriteAllText((Join-Path $receiptRoot 'interrupted.json'),'{}',[Text.UTF8Encoding]::new($false));$s=Complete-State $root $s;$integrity=Test-ESTaskContextIntegrity -ProjectRoot $root -StoreRoot 'state' -TaskId 'task'
    Assert-Equal $s.completionDecision 'accepted' 'completionDecision';Assert-Equal $integrity.status 'passed' 'integrity';Assert-Equal $integrity.orphanReceiptCount 1 'orphan count'
}
Invoke-Case 'reopen-new-revision' {
    $root=New-Fixture 'reopen';$s=New-AcceptedState $root;$oldRevision=[int]$s.taskRevision;$oldContext=[int]$s.contextVersion
    $s=Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Reopen -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'reopen'
    Assert-Equal $s.taskStatus 'Active' 'TaskStatus';Assert-Equal $s.contextStatus 'Live' 'ContextStatus';Assert-Equal $s.completionDecision 'undetermined' 'completionDecision';Assert-True ([int]$s.taskRevision-eq$oldRevision+1) 'TaskRevision did not advance.';Assert-True ([int]$s.contextVersion-eq$oldContext+1) 'ContextVersion did not advance.';Assert-True ($null-eq$s.completionReceipt) 'Current receipt projection was not cleared.'
}
Invoke-Case 'path-escape-denied' {
    $root=New-Fixture 'escape';$threw=$false
    $goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal-escape' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8})
    $routePlan=New-ESTestRoutePlan -Root $root -Goal $goal
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'escape' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope '../outside.txt' -IdempotencyKey 'create'|Out-Null}catch{$threw=$true}
    Assert-True $threw 'Path escape was not rejected.';Assert-True (-not(Test-Path -LiteralPath (Join-Path $root 'state/escape'))) 'Rejected create wrote partial task state.'
}
Invoke-Case 'reparse-point-denied' {
    $root=New-Fixture 'reparse';$outside=New-Fixture 'reparse-outside'
    $goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId 'goal-reparse' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8})
    $routePlan=New-ESTestRoutePlan -Root $root -Goal $goal;$junction=Join-Path $root 'linked';New-Item -ItemType Junction -Path $junction -Target $outside|Out-Null
    $sourceThrew=$false;$storeThrew=$false
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'source-link' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'linked/source.txt' -IdempotencyKey 'source-link'|Out-Null}catch{$sourceThrew=$_.Exception.Message -like '*reparse point*'}
    try{New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'linked/state' -TaskId 'store-link' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'store-link'|Out-Null}catch{$storeThrew=$_.Exception.Message -like '*reparse point*'}
    Assert-True $sourceThrew 'Source path reparse traversal was not rejected.';Assert-True $storeThrew 'StoreRoot reparse traversal was not rejected.'
}
Invoke-Case 'cli-input-reparse-denied' {
    $root=New-Fixture 'cli-input-reparse';$outside=New-Fixture 'cli-input-reparse-outside';$request=[ordered]@{storeRoot='state';taskId='task'}
    [IO.File]::WriteAllText((Join-Path $outside 'request.json'),($request|ConvertTo-Json),[Text.UTF8Encoding]::new($false));$junction=Join-Path $root 'input-link';New-Item -ItemType Junction -Path $junction -Target $outside|Out-Null;$threw=$false
    try{& (Join-Path $PSScriptRoot 'Invoke-ESTaskContextRuntime.ps1') -Action Get -InputPath 'input-link/request.json' -ProjectRoot $root|Out-Null}catch{$threw=$_.Exception.Message -like '*reparse point*'}
    Assert-True $threw 'CLI InputPath reparse traversal was not rejected.'
}
Invoke-Case 'quarantine-recovers-archived-context' {
    $root=New-Fixture 'quarantine';$s=New-AcceptedState $root
    $s=Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Archive -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'archive'
    $s=Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Quarantine -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'quarantine'
    $s=Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Recover -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'recover'
    Assert-Equal $s.contextStatus 'Archived' 'recovered ContextStatus'
}
Invoke-Case 'quarantine-recovers-partially-invalidated-context' {
    $root=New-Fixture 'quarantine-invalidated';$s=New-State $root
    $s=Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Invalidate -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'invalidate'
    $s=Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Quarantine -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'quarantine'
    $s=Invoke-ESTaskContextTransition -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -Transition Recover -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey 'recover'
    Assert-Equal $s.contextStatus 'PartiallyInvalidated' 'recovered ContextStatus'
}

$failed=@($results|Where-Object status -eq 'failed')
$report=[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESTaskContextRuntime';status=if($failed.Count){'failed'}else{'passed'};testRoot=$testRoot;caseCount=$results.Count;passedCount=@($results|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;cases=$results;runtimeStatus='runtime-not-run';claimsNotProven=@('Unity Runtime','Worker Runtime','adapter integration Runtime','release acceptance')}
$report|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
