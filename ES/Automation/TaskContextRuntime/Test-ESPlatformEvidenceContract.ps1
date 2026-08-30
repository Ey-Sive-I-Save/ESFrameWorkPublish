[CmdletBinding()]
param(
    [string]$SchemaPath,
    [string]$SchemaModulePath,
    [string]$RuntimeModulePath
)

$ErrorActionPreference='Stop'
$scriptRoot=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($SchemaPath)){$SchemaPath=Join-Path $scriptRoot '..\Contracts\es-platform-evidence-v1.schema.json'}
if([string]::IsNullOrWhiteSpace($SchemaModulePath)){$SchemaModulePath=Join-Path $scriptRoot '..\Contracts\ESJsonSchemaLite.psm1'}
if([string]::IsNullOrWhiteSpace($RuntimeModulePath)){$RuntimeModulePath=Join-Path $scriptRoot 'ESTaskContextRuntime.psm1'}
Import-Module (Resolve-Path -LiteralPath $SchemaModulePath).Path -Force
Get-Module -Name ESTaskContextRuntime | Remove-Module -Force -ErrorAction Stop
$runtimeModules=@(Import-Module (Resolve-Path -LiteralPath $RuntimeModulePath).Path -Force -PassThru)
. (Join-Path $PSScriptRoot 'Test-ESTaskContextRoutePlanFixture.ps1')
$runtimeModule=Resolve-ESTestImportedModuleInstance -ImportedModules $runtimeModules -ExpectedPath $RuntimeModulePath -ModuleName 'ESTaskContextRuntime'
$contractId='es://automation/contracts/platform-evidence/v1'
$contractHash=(Get-FileHash -LiteralPath $SchemaPath -Algorithm SHA256).Hash.ToLowerInvariant()
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-platform-evidence-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot|Out-Null
Initialize-ESTestRoutePlanRepository $testRoot
$results=[Collections.Generic.List[object]]::new()

function Assert-Equal($Actual,$Expected,[string]$Message){if([string]$Actual-cne[string]$Expected){throw "$Message Expected=$Expected Actual=$Actual"}}
function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Invoke-Case([string]$Name,[scriptblock]$Body){try{&$Body;[void]$results.Add([pscustomobject]@{case=$Name;status='passed';finding=$null})}catch{[void]$results.Add([pscustomobject]@{case=$Name;status='failed';finding=$_.Exception.Message})}}
function New-Fixture([string]$Name){$root=Join-Path $testRoot $Name;New-Item -ItemType Directory -Path $root|Out-Null;[IO.File]::WriteAllText((Join-Path $root 'source.txt'),'source',[Text.UTF8Encoding]::new($false));$root}
function New-State([string]$Root){$goal=New-ESGoalRevision -ProjectRoot $Root -StoreRoot 'state' -GoalId 'goal' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static' -Budget ([ordered]@{maxReads=8});$routePlan=New-ESTestRoutePlan -Root $Root -Goal $goal;$state=New-ESTaskContextTask -ProjectRoot $Root -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create';Confirm-ESTaskSourceScope -ProjectRoot $Root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'verify'}
function New-CandidatePayload([string]$Root,$State,[bool]$Legacy=$false,[string]$ContractHash=$script:contractHash){
    $captured=[DateTime]::UtcNow.ToString('o')
    $artifactName='artifact.json'
    $artifact=[ordered]@{schemaVersion=1;claimId='source-integrity';sourceScopeHash=$State.verifiedSourceScopeHash;observations=@([ordered]@{path='source.txt';expectedSha256=[string]$State.verifiedSourceScope[0].sha256})}
    $artifactFull=Join-Path $Root $artifactName
    [IO.File]::WriteAllText($artifactFull,($artifact|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $artifactHash=(Get-FileHash -LiteralPath $artifactFull -Algorithm SHA256).Hash.ToLowerInvariant()
    if($Legacy){return [ordered]@{schemaVersion=1;taskId='task';evidenceSetId='evidence';capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';outcome='passed';capturedUtc=$captured;sourceScopeHash=$State.verifiedSourceScopeHash;evidenceHash=$artifactHash;producerType='worker';artifactPath=$artifactName});contradictions=@();sourceDrift=@();unverifiedClaims=@()}}
    [ordered]@{schemaVersion=1;contractId=$script:contractId;contractHash=$ContractHash;recordType='CandidateEvidenceSet';taskId='task';evidenceSetId='evidence';capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';candidateOutcome='passed';capturedUtc=$captured;sourceScopeHash=$State.verifiedSourceScopeHash;candidateEvidenceHash=$artifactHash;candidateProducerType='worker';artifactPath=$artifactName});contradictions=@();sourceDrift=@();unverifiedClaims=@()}
}
function Write-Payload([string]$Root,$Payload){$path=Join-Path $Root 'candidate.json';[IO.File]::WriteAllText($path,($Payload|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));'candidate.json'}
function Submit-Payload([string]$Root,$State,$Payload,[string]$Key='submit'){$path=Write-Payload $Root $Payload;Submit-ESTaskEvidenceSet -ProjectRoot $Root -StoreRoot 'state' -TaskId 'task' -EvidenceSetPath $path -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IdempotencyKey $Key}

$captured=[DateTime]::UtcNow.ToString('o')
$staticCanonical=[ordered]@{schemaVersion=1;contractId=$contractId;contractHash=$contractHash;recordType='CandidateEvidenceSet';taskId='task';evidenceSetId='evidence';capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';candidateOutcome='passed';capturedUtc=$captured;sourceScopeHash=('a'*64);candidateEvidenceHash=('b'*64);candidateProducerType='worker';artifactPath='artifact.json'});contradictions=@();sourceDrift=@();unverifiedClaims=@()}
$staticLegacy=[ordered]@{schemaVersion=1;taskId='task';evidenceSetId='evidence';capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';outcome='passed';capturedUtc=$captured;sourceScopeHash=('a'*64);evidenceHash=('b'*64);producerType='worker';artifactPath='artifact.json'});contradictions=@();sourceDrift=@();unverifiedClaims=@()}

Invoke-Case 'schema-supported-keyword-closure' {$errors=@(Test-ESJsonSchemaSupported -SchemaPath $SchemaPath);Assert-Equal $errors.Count 0 ($errors-join'; ')}
Invoke-Case 'canonical-candidate-schema' {$errors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -DefinitionName 'candidateEvidenceSet' -Value ([pscustomobject]$staticCanonical));Assert-Equal $errors.Count 0 ($errors-join'; ')}
Invoke-Case 'legacy-candidate-schema' {$errors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -DefinitionName 'legacyCandidateEvidenceSet' -Value ([pscustomobject]$staticLegacy));Assert-Equal $errors.Count 0 ($errors-join'; ')}
Invoke-Case 'schema-rejects-additional-candidate-field' {$bad=$staticCanonical|ConvertTo-Json -Depth 20|ConvertFrom-Json;$bad.items[0]|Add-Member -NotePropertyName trustedOutcome -NotePropertyValue 'passed';$errors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -DefinitionName 'candidateEvidenceSet' -Value $bad);Assert-True ($errors.Count-gt0) 'Additional candidate field passed the central schema.'}
Invoke-Case 'schema-cache-invalidates-on-content-change' {$copy=Join-Path $testRoot 'cache-schema.json';Copy-Item -LiteralPath $SchemaPath -Destination $copy;$before=@(Test-ESJsonSchemaSupported -SchemaPath $copy);Assert-Equal $before.Count 0 'Initial copied schema';$schema=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($copy))|ConvertFrom-Json;$schema.'$defs'.hash|Add-Member -NotePropertyName unsupportedKeyword -NotePropertyValue $true;[IO.File]::WriteAllText($copy,($schema|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false));$after=@(Test-ESJsonSchemaSupported -SchemaPath $copy);Assert-True ($after.Count-gt0) 'Schema cache reused stale content after the file changed.'}
Invoke-Case 'external-schema-reference-cannot-expand-directory' {$refRoot=Join-Path $testRoot 'ref-root';$refChild=Join-Path $refRoot 'child';New-Item -ItemType Directory -Path $refChild|Out-Null;$outside=[ordered]@{'$schema'='https://json-schema.org/draft/2020-12/schema';'$id'='es://outside';'$defs'=[ordered]@{value=[ordered]@{type='string'}}};$inside=[ordered]@{'$schema'='https://json-schema.org/draft/2020-12/schema';'$id'='es://inside';'$ref'='../outside.json#/$defs/value'};[IO.File]::WriteAllText((Join-Path $refRoot 'outside.json'),($outside|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$insidePath=Join-Path $refChild 'inside.json';[IO.File]::WriteAllText($insidePath,($inside|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$errors=@(Test-ESJsonSchemaSupported -SchemaPath $insidePath);Assert-True (@($errors|Where-Object{$_-like'*must remain in the current contract directory*'}).Count-gt0) 'External schema reference escaped its contract directory.'}
Invoke-Case 'canonical-runtime-normalization' {$root=New-Fixture 'canonical';$state=New-State $root;$state=Submit-Payload $root $state (New-CandidatePayload $root $state);Assert-Equal $state.evidenceSet.inputContractMode 'canonical-v1' 'inputContractMode';Assert-Equal $state.evidenceSet.items[0].producerType 'platform' 'normalized producer';Assert-Equal $state.evidenceSet.items[0].candidateProducerType 'worker' 'candidate producer';$state=Complete-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'complete';Assert-Equal $state.completionDecision 'accepted' 'completionDecision';$receiptPath=Join-Path (Join-Path $root 'state') ([string]$state.completionReceipt.path);$receipt=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($receiptPath))|ConvertFrom-Json;Assert-Equal $receipt.evidenceContractHash $contractHash 'receipt evidence contract hash'}
Invoke-Case 'legacy-runtime-projection' {$root=New-Fixture 'legacy';$state=New-State $root;$state=Submit-Payload $root $state (New-CandidatePayload $root $state $true);Assert-Equal $state.evidenceSet.inputContractMode 'legacy-task-context-v1' 'inputContractMode';Assert-Equal $state.evidenceSet.items[0].candidateProducerType 'worker' 'candidate producer'}
Invoke-Case 'forged-contract-hash-is-rejected' {$root=New-Fixture 'forged-contract';$state=New-State $root;$threw=$false;try{Submit-Payload $root $state (New-CandidatePayload $root $state $false ('0'*64))|Out-Null}catch{$threw=$_.Exception.Message-eq'CandidateEvidenceSet contractHash does not match the platform contract.'};Assert-True $threw 'Forged contract hash was accepted.'}
Invoke-Case 'runtime-rejects-additional-candidate-field' {$root=New-Fixture 'extra-field';$state=New-State $root;$payload=New-CandidatePayload $root $state;$payload.items[0].trustedOutcome='passed';$threw=$false;try{Submit-Payload $root $state $payload|Out-Null}catch{$threw=$_.Exception.Message-like'CandidateEvidence contains an unsupported property:*'};Assert-True $threw 'Additional candidate authority field was accepted.'}
Invoke-Case 'runtime-rejects-missing-candidate-field' {$root=New-Fixture 'missing-field';$state=New-State $root;$payload=New-CandidatePayload $root $state;$payload.items[0].Remove('candidateEvidenceHash');$threw=$false;try{Submit-Payload $root $state $payload|Out-Null}catch{$threw=$_.Exception.Message-eq'CandidateEvidence is missing required property: candidateEvidenceHash'};Assert-True $threw 'CandidateEvidence without its hash was accepted.'}
Invoke-Case 'legacy-accepted-receipt-remains-verifiable' {
    $root=New-Fixture 'legacy-receipt';$store=Join-Path $root 'state';$receiptDirectory=Join-Path $store 'task/receipts';New-Item -ItemType Directory -Path $receiptDirectory|Out-Null
    $receiptBase=[ordered]@{schemaVersion=1;receiptId=[Guid]::NewGuid().ToString('N');taskId='task';taskRevision=4;contextVersion=2;planHash=('a'*64);goalRevisionHash=('b'*64);acceptanceProfileHash=('c'*64);evidenceSetHash=('d'*64);verifiedSourceScopeHash=('e'*64);completionDecision='accepted';issuedUtc=[DateTime]::UtcNow.ToString('o')}
    $receipt=[ordered]@{};foreach($key in $receiptBase.Keys){$receipt[$key]=$receiptBase[$key]};$receipt.receiptHash=&$runtimeModule{param($value)Get-ESObjectHash (Get-ESReceiptHashInput $value)}([pscustomobject]$receiptBase)
    $relative='task/receipts/'+$receipt.receiptId+'.json';[IO.File]::WriteAllText((Join-Path $store ($relative.Replace('/',[IO.Path]::DirectorySeparatorChar))),($receipt|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $state=[pscustomobject][ordered]@{taskId='task';taskRevision=4;contextVersion=2;planHash=('a'*64);goalRevisionHash=('b'*64);acceptanceProfile=[pscustomobject]@{profileHash=('c'*64)};evidenceSet=[pscustomobject]@{evidenceSetHash=('d'*64)};verifiedSourceScopeHash=('e'*64);completionReceipt=[pscustomobject]@{path=$relative;receiptHash=$receipt.receiptHash}}
    $paths=[pscustomobject]@{ProjectRoot=$root;StoreRoot=$store};$verified=&$runtimeModule{param($runtimePaths,$runtimeState)Test-ESBoundReceipt $runtimePaths $runtimeState}$paths $state;Assert-Equal $verified.receiptHash $receipt.receiptHash 'legacy receipt hash'
}
Invoke-Case 'frozen-contract-drift-limits-completion' {
    $isolated=Join-Path $testRoot 'isolated';$automationDir=Join-Path $isolated 'ES\Automation';$runtimeDir=Join-Path $automationDir 'TaskContextRuntime';$contractsDir=Join-Path $automationDir 'Contracts';$routePlanDir=Join-Path $automationDir 'RoutePlan';$evaluationDir=Join-Path $automationDir 'Evaluation';$abcdDir=Join-Path $automationDir 'ABCD';$aiDir=Join-Path $automationDir 'AI';$runnerDir=Join-Path $isolated '.agents\skills\es-static-deep-replay\scripts';New-Item -ItemType Directory -Path $runtimeDir,$contractsDir,$routePlanDir,$evaluationDir,$abcdDir,$aiDir,$runnerDir|Out-Null
    Copy-Item -LiteralPath $RuntimeModulePath -Destination (Join-Path $runtimeDir 'ESTaskContextRuntime.psm1')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\ABCD\ESABCDAuthorityKernel.psm1') -Destination (Join-Path $abcdDir 'ESABCDAuthorityKernel.psm1')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\AI\ESAuthorityDecisionPolicy.psm1') -Destination (Join-Path $aiDir 'ESAuthorityDecisionPolicy.psm1')
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $SchemaPath) 'es-authority-ai-decision-policy-v1.json') -Destination (Join-Path $contractsDir 'es-authority-ai-decision-policy-v1.json')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\RoutePlan\ESRoutePlanContract.psm1') -Destination (Join-Path $routePlanDir 'ESRoutePlanContract.psm1')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\..\.agents\skills\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1') -Destination (Join-Path $runnerDir 'Invoke-ESStaticDeepReplay.ps1')
    Copy-Item -LiteralPath $SchemaPath -Destination (Join-Path $contractsDir 'es-platform-evidence-v1.schema.json')
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $SchemaPath) 'es-evidence-verifier.registry.json') -Destination (Join-Path $contractsDir 'es-evidence-verifier.registry.json')
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $SchemaPath) 'es-task-transcript-slice-v1.schema.json') -Destination (Join-Path $contractsDir 'es-task-transcript-slice-v1.schema.json')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\Evaluation\ESTranscriptCorrectionObservation.psm1') -Destination (Join-Path $evaluationDir 'ESTranscriptCorrectionObservation.psm1')
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $SchemaPath) 'es-outcome-evaluator.registry.json') -Destination (Join-Path $contractsDir 'es-outcome-evaluator.registry.json')
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $SchemaPath) 'es-evaluation-record-v1.schema.json') -Destination (Join-Path $contractsDir 'es-evaluation-record-v1.schema.json')
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $SchemaPath) 'es-route-plan-v1.schema.json') -Destination (Join-Path $contractsDir 'es-route-plan-v1.schema.json')
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $SchemaPath) 'es-route-stage.registry.json') -Destination (Join-Path $contractsDir 'es-route-stage.registry.json')
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $SchemaPath) 'es-route-stage-registry-v1.schema.json') -Destination (Join-Path $contractsDir 'es-route-stage-registry-v1.schema.json')
    Copy-Item -LiteralPath $SchemaModulePath -Destination (Join-Path $contractsDir 'ESJsonSchemaLite.psm1')
    Remove-Module ESTaskContextRuntime -Force -ErrorAction SilentlyContinue;Import-Module (Join-Path $runtimeDir 'ESTaskContextRuntime.psm1') -Force
    $script:contractHash=(Get-FileHash -LiteralPath (Join-Path $contractsDir 'es-platform-evidence-v1.schema.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $root=New-Fixture 'contract-drift';$state=New-State $root;$state=Submit-Payload $root $state (New-CandidatePayload $root $state)
    [IO.File]::AppendAllText((Join-Path $contractsDir 'es-platform-evidence-v1.schema.json'),[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
    $state=Complete-ESTaskContextTask -ProjectRoot $root -StoreRoot 'state' -TaskId 'task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'complete'
    Assert-Equal $state.completionDecision 'undetermined' 'completionDecision after contract drift'
    $eventPath=(Get-ChildItem -LiteralPath (Join-Path $root 'state/task/events') -File|Sort-Object Name|Select-Object -Last 1).FullName;$event=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($eventPath))|ConvertFrom-Json;Assert-True (@($event.metadata.reasons)-contains'EvidenceContractDrift') 'EvidenceContractDrift reason was not recorded.'
}

$failed=@($results|Where-Object { $_.status -eq 'failed' })
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESPlatformEvidenceContract';status=if($failed.Count){'failed'}else{'passed'};caseCount=$results.Count;passedCount=@($results|Where-Object { $_.status -eq 'passed' }).Count;failedCount=$failed.Count;cases=@($results);contractId=$contractId;contractHash=$contractHash;runtimeStatus='runtime-not-run';claimsNotProven=@('Production route integration','Unity or Worker Runtime','release acceptance')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
