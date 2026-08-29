[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ReportPath = 'ES/Output/StaticReplay/es-abcd-orchestration.json'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDOrchestrator.psm1') -Force
Import-Module (Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force

$results = [Collections.Generic.List[object]]::new()
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; [void]$results.Add([pscustomobject]@{ case = $Name; status = 'passed'; finding = $null }) }
    catch { [void]$results.Add([pscustomobject]@{ case = $Name; status = 'failed'; finding = $_.Exception.Message }) }
}
function Assert-True([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Assert-Equal($Actual, $Expected, [string]$Message) { if ([string]$Actual -cne [string]$Expected) { throw "$Message Expected=$Expected Actual=$Actual" } }
function Assert-Schema([string]$Path, $Value) { $errors = @(Test-ESJsonSchemaValue -SchemaPath (Join-Path $root $Path) -Value $Value); if ($errors.Count) { throw ($errors -join '; ') } }

$zero = '0' * 64
$one = '1' * 64
$bindingHash = '2' * 64
$verifierDefinitionHash = '3' * 64
$verificationReceiptHash = '4' * 64
$store = New-ESABCDOrchestrationStore -TaskId 'abcd-task' -TaskBindingId 'binding-abcd' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one -MaxRounds 2 -AttemptsPerRound 3

function Add-TestAudit($s,[string]$branch,[string]$verdict='pass') {
    Add-ESABCDAuditRecord -Store $s -BranchId $branch -AuditorRef 'auditor.static.v1' -Verdict $verdict -EvidenceRefs @('evidence/a') -VerifierRef 'verifier.static.v1' -AuthorizationProof 'auth-proof/a' -VerifierDefinitionHash $verifierDefinitionHash -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion
}

Case 'divergence-audit-selection-correction-verification' {
    $r = Start-ESABCDIterationRound $store $store.taskRevision $store.contextVersion; Assert-Equal $r.status 'appended' 'round append'; Assert-Schema 'ES/Automation/Contracts/es-abcd-orchestration-event-v1.schema.json' $r.event
    $r = Add-ESABCDCandidate $store 'branch-a' $null $zero 'assumption-a' 'verify-a' $store.taskRevision $store.contextVersion; Assert-Equal $r.status 'appended' 'candidate append'
    $r = Add-ESABCDCandidate $store 'branch-b' 'branch-a' $one 'assumption-b' 'verify-b' $store.taskRevision $store.contextVersion; Assert-Equal $r.status 'appended' 'candidate append 2'
    $r = Prune-ESABCDCandidate $store 'branch-b' 'higher risk delta' $store.taskRevision $store.contextVersion; Assert-Equal $r.status 'appended' 'prune append'
    $r = Backtrack-ESABCDCandidate $store 'branch-b' 'branch-a' 'pruned candidate failed predicate' $store.taskRevision $store.contextVersion; Assert-Equal $r.status 'appended' 'backtrack append'
    $r = Add-TestAudit $store 'branch-a'; Assert-Equal $r.status 'appended' 'audit append'
    $r = Select-ESABCDDecision $store 'branch-a' 'retry' 'full' $store.taskRevision $store.contextVersion; Assert-Equal $r.status 'appended' 'decision append'
    $r = Start-ESABCDCorrectionCycle $store 'cycle-1' 'finding/a' 'evidence' 'retry' 'full' 1 $store.taskRevision $store.contextVersion; Assert-Equal $r.status 'appended' 'cycle append'
    $r = Add-ESABCDVerificationReceipt -Store $store -CycleId 'cycle-1' -VerificationStatus 'passed' -VerificationReceiptRef 'receipt-verify-1' -VerificationReceiptHash $verificationReceiptHash -ExpectedTaskRevision $store.taskRevision -ExpectedContextVersion $store.contextVersion; Assert-Equal $r.status 'appended' 'verification append'
    $r = Advance-ESABCDIterationRound $store 'cycle-1' $store.taskRevision $store.contextVersion; Assert-Equal $r.status 'appended' 'advance append'
    $integrity = Test-ESABCDEventStoreIntegrity $store; Assert-Equal $integrity.status 'passed' 'event chain integrity'
    $eligibility = Test-ESABCDCompletionEligibility $store 'cycle-1'; Assert-True $eligibility.eligible 'verified cycle must be eligible'
}

Case 'stale-cas-is-rejected' {
    try { Add-ESABCDCandidate $store 'branch-stale' $null $zero 'stale' 'verify' 1 1 | Out-Null; throw 'stale CAS accepted' }
    catch { Assert-True ($_.Exception.Message -like '*CAS_STALE*') 'wrong stale CAS reason' }
}

Case 'idempotency-replay-and-conflict' {
    $before = $store.events.Count
    $r1 = Start-ESABCDCorrectionCycle $store 'cycle-1' 'finding/a' 'evidence' 'retry' 'full' 1 1 1
    Assert-Equal $r1.status 'replayed' 'same cycle must replay'
    Assert-Equal $store.events.Count $before 'replay appended a duplicate event'
    try {
        Add-ESABCDEvent $store 'correction-cycle-started' ([ordered]@{ cycleId = 'cycle-1'; findingReceiptRef = 'different'; failureClass = 'evidence'; decision = 'retry' }) $store.taskRevision $store.contextVersion $r1.event.idempotencyKey | Out-Null
        throw 'idempotency conflict accepted'
    } catch { Assert-True ($_.Exception.Message -like '*IDEMPOTENCY_CONFLICT*') 'wrong idempotency conflict reason' }
}

Case 'audit-and-branch-guards' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-guards' -TaskBindingId 'binding-guards' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'root' $null $zero 'assumption' 'verify' $s.taskRevision $s.contextVersion | Out-Null
    try { Add-ESABCDAuditRecord -Store $s -BranchId 'root' -AuditorRef 'auditor' -Verdict 'pass' -EvidenceRefs @('e') -VerifierRef '' -AuthorizationProof '' -VerifierDefinitionHash $verifierDefinitionHash -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null; throw 'audit accepted missing verifier proof' } catch { Assert-True ($_.Exception.Message -match 'AUDIT_EVIDENCE_MISSING|VerifierRef|AuthorizationProof') 'missing verifier proof was accepted' }
    Add-TestAudit $s 'root' | Out-Null
    Prune-ESABCDCandidate $s 'root' 'prune once' $s.taskRevision $s.contextVersion | Out-Null
    try { Prune-ESABCDCandidate $s 'root' 'prune twice' $s.taskRevision $s.contextVersion | Out-Null; throw 'duplicate prune accepted' } catch { Assert-True ($_.Exception.Message -match 'BRANCH_STATE_INVALID_FOR_PRUNE|BRANCH_NOT_OPEN') 'wrong duplicate prune reason' }
}

Case 'review-audit-cannot-select-or-cycle' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-review-guard' -TaskBindingId 'binding-review-guard' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'root' $null $zero 'assumption' 'verify' $s.taskRevision $s.contextVersion | Out-Null
    Add-TestAudit $s 'root' 'review' | Out-Null
    try { Select-ESABCDDecision -Store $s -BranchId 'root' -Decision 'retry' -ClaimLevel 'claim-cap' -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null; throw 'review audit selected' }
    catch { Assert-True ($_.Exception.Message -like '*AUDIT_PASS_REQUIRED*') 'review audit was selectable' }
}

Case 'backtracked-branch-cannot-be-audited' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-backtrack-audit' -TaskBindingId 'binding-backtrack-audit' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'a' $null $zero 'a' 'verify-a' $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'b' 'a' $one 'b' 'verify-b' $s.taskRevision $s.contextVersion | Out-Null
    Backtrack-ESABCDCandidate $s 'b' 'a' 'return to a' $s.taskRevision $s.contextVersion | Out-Null
    try { Add-TestAudit $s 'b' | Out-Null; throw 'backtracked branch audited' }
    catch { Assert-True ($_.Exception.Message -like '*BRANCH_NOT_AUDITABLE*') 'backtracked branch was auditable' }
}

Case 'audit-independence-is-enforced' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-audit-independence' -TaskBindingId 'binding-audit-independence' -TaskBindingHash $bindingHash -AuthorizationRef 'same-principal' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'root' $null $zero 'assumption' 'verify' $s.taskRevision $s.contextVersion | Out-Null
    try { Add-ESABCDAuditRecord -Store $s -BranchId 'root' -AuditorRef 'same-principal' -Verdict 'pass' -EvidenceRefs @('evidence/a') -VerifierRef 'same-principal' -AuthorizationProof 'same-principal' -VerifierDefinitionHash $verifierDefinitionHash -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null; throw 'non-independent audit accepted' }
    catch { Assert-True ($_.Exception.Message -like '*AUDIT_INDEPENDENCE_REQUIRED*') 'audit independence was not enforced' }
}

Case 'attempt-sequence-is-enforced' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-attempt-order' -TaskBindingId 'binding-attempt-order' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one -AttemptsPerRound 2
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'root' $null $zero 'assumption' 'verify' $s.taskRevision $s.contextVersion | Out-Null
    Add-TestAudit $s 'root' | Out-Null
    Select-ESABCDDecision -Store $s -BranchId 'root' -Decision 'retry' -ClaimLevel 'claim-cap' -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null
    try { Start-ESABCDCorrectionCycle -Store $s -CycleId 'cycle-out-of-order' -FindingReceiptRef 'finding' -FailureClass 'source' -Decision 'retry' -ClaimLevel 'claim-cap' -AttemptNo 2 -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null; throw 'out-of-order attempt accepted' }
    catch { Assert-True ($_.Exception.Message -like '*ATTEMPT_SEQUENCE_INVALID*') 'attempt order was not enforced' }
}

Case 'backtrack-requires-ancestor-and-isolated-snapshot' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-backtrack-guards' -TaskBindingId 'binding-backtrack-guards' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'a' $null $zero 'a' 'verify-a' $s.taskRevision $s.contextVersion | Out-Null
    try { Add-ESABCDCandidate $s 'same' 'a' $zero 'same' 'verify-same' $s.taskRevision $s.contextVersion | Out-Null; throw 'same snapshot child accepted' }
    catch { Assert-True ($_.Exception.Message -like '*BRANCH_SNAPSHOT_NOT_ISOLATED*') 'same snapshot child was accepted' }
    Add-ESABCDCandidate $s 'b' $null $one 'b' 'verify-b' $s.taskRevision $s.contextVersion | Out-Null
    try { Backtrack-ESABCDCandidate $s 'b' 'a' 'sibling backtrack' $s.taskRevision $s.contextVersion | Out-Null; throw 'sibling backtrack accepted' }
    catch { Assert-True ($_.Exception.Message -like '*BACKTRACK_TARGET_NOT_ANCESTOR*') 'sibling backtrack was accepted' }
}

Case 'completion-binding-integrity' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-binding-gate' -TaskBindingId 'binding-gate' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'root' $null $zero 'assumption' 'verify' $s.taskRevision $s.contextVersion | Out-Null
    Add-TestAudit $s 'root' | Out-Null
    Select-ESABCDDecision -Store $s -BranchId 'root' -Decision 'retry' -ClaimLevel 'full' -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null
    Start-ESABCDCorrectionCycle -Store $s -CycleId 'cycle-gate' -FindingReceiptRef 'finding' -FailureClass 'evidence' -Decision 'retry' -ClaimLevel 'full' -AttemptNo 1 -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null
    Add-ESABCDVerificationReceipt -Store $s -CycleId 'cycle-gate' -VerificationStatus 'passed' -VerificationReceiptRef 'receipt' -VerificationReceiptHash $verificationReceiptHash -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null
    $s.verifications['cycle-gate'].taskBindingRef.bindingHash = 'f' * 64
    $eligibility = Test-ESABCDCompletionEligibility $s 'cycle-gate'; Assert-True (-not $eligibility.eligible) 'tampered verification binding entered completion'; Assert-Equal $eligibility.reasonCode 'VERIFICATION_BINDING_MISMATCH' 'wrong binding mismatch reason'
}

Case 'completion-requires-verification-receipt' {
    $new = New-ESABCDOrchestrationStore -TaskId 'abcd-task-2' -TaskBindingId 'binding-abcd-2' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $new $new.taskRevision $new.contextVersion | Out-Null
    Add-ESABCDCandidate $new 'root' $null $zero 'assumption' 'verify' $new.taskRevision $new.contextVersion | Out-Null
    Add-TestAudit $new 'root' | Out-Null
    Select-ESABCDDecision -Store $new -BranchId 'root' -Decision 'replan' -ClaimLevel 'full' -ExpectedTaskRevision $new.taskRevision -ExpectedContextVersion $new.contextVersion | Out-Null
    Start-ESABCDCorrectionCycle -Store $new -CycleId 'cycle-no-verification' -FindingReceiptRef 'finding/b' -FailureClass 'source' -Decision 'replan' -ClaimLevel 'full' -AttemptNo 1 -ExpectedTaskRevision $new.taskRevision -ExpectedContextVersion $new.contextVersion | Out-Null
    $eligibility = Test-ESABCDCompletionEligibility $new 'cycle-no-verification'; Assert-True (-not $eligibility.eligible) 'unverified cycle entered completion'
    Assert-Equal $eligibility.reasonCode 'VERIFICATION_MISSING' 'wrong completion reason'
}

Case 'forged-verification-projection-without-event-is-rejected' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-forged-projection' -TaskBindingId 'binding-forged-projection' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    Add-ESABCDCandidate $s 'root' $null $zero 'assumption' 'verify' $s.taskRevision $s.contextVersion | Out-Null
    Add-TestAudit $s 'root' | Out-Null
    Select-ESABCDDecision -Store $s -BranchId 'root' -Decision 'retry' -ClaimLevel 'full' -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null
    Start-ESABCDCorrectionCycle -Store $s -CycleId 'cycle-forged' -FindingReceiptRef 'finding' -FailureClass 'evidence' -Decision 'retry' -ClaimLevel 'full' -AttemptNo 1 -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null
    $s.verifications['cycle-forged'] = [pscustomobject][ordered]@{ cycleId = 'cycle-forged'; taskId = $s.taskId; taskBindingRef = $s.taskBindingRef; routePlanHash = $s.routePlanHash; sourceScopeHash = $s.sourceScopeHash; verificationStatus = 'passed'; verificationReceiptRef = 'receipt-forged'; verificationReceiptHash = $verificationReceiptHash; verificationReceiptArtifactHash = $null }
    $eligibility = Test-ESABCDCompletionEligibility $s 'cycle-forged'; Assert-True (-not $eligibility.eligible) 'forged verification projection entered completion'; Assert-Equal $eligibility.reasonCode 'VERIFICATION_EVENT_MISSING' 'wrong forged projection reason'
}

Case 'all-six-failure-classes-are-bounded' {
    $classes = @('input','source','route','capability','environment','evidence')
    foreach ($i in 0..($classes.Count-1)) {
        $s = New-ESABCDOrchestrationStore -TaskId ("task-{0}" -f $i) -TaskBindingId ("binding-{0}" -f $i) -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one -AttemptsPerRound 1
        Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
        Add-ESABCDCandidate $s 'root' $null $zero 'assumption' 'verify' $s.taskRevision $s.contextVersion | Out-Null
        Add-TestAudit $s 'root' | Out-Null
        Select-ESABCDDecision -Store $s -BranchId 'root' -Decision 'stop' -ClaimLevel 'claim-cap' -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion | Out-Null
        $c = "cycle-{0}" -f $i
        $r = Start-ESABCDCorrectionCycle -Store $s -CycleId $c -FindingReceiptRef ("finding/{0}" -f $i) -FailureClass $classes[$i] -Decision 'stop' -ClaimLevel 'claim-cap' -AttemptNo 1 -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion
        Assert-Equal $r.status 'appended' ("failure class {0}" -f $classes[$i])
    }
}

Case 'stop-is-explicit-and-replayable' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-stop' -TaskBindingId 'binding-stop' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    $r = Stop-ESABCDIteration $s 'unknown capability and insufficient evidence' $s.taskRevision $s.contextVersion
    Assert-Equal $r.status 'appended' 'stop append'; Assert-Equal $r.event.payload.nextAction 'stop-and-report' 'stop mapping'
    $before = $s.events.Count; $replay = Stop-ESABCDIteration $s 'unknown capability and insufficient evidence' $s.taskRevision $s.contextVersion
    Assert-Equal $replay.status 'replayed' 'stop replay'; Assert-Equal $s.events.Count $before 'stop duplicate event'
}

Case 'tampered-event-chain-is-rejected' {
    $s = New-ESABCDOrchestrationStore -TaskId 'task-tamper' -TaskBindingId 'binding-tamper' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one
    Start-ESABCDIterationRound $s $s.taskRevision $s.contextVersion | Out-Null
    $s.events[0].payload.roundNo = 99
    try { Test-ESABCDEventStoreIntegrity $s | Out-Null; throw 'tampered event accepted' }
    catch { Assert-True ($_.Exception.Message -like '*EVENT_PAYLOAD_HASH_MISMATCH*' -or $_.Exception.Message -like '*EVENT_HASH_MISMATCH*') 'wrong tamper reason' }
}

Case 'binding-reference-is-strongly-typed' {
    try { New-ESABCDOrchestrationStore -TaskId 'task-binding' -TaskBindingId 'binding-binding' -TaskBindingHash ('bad') -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one | Out-Null; throw 'invalid binding hash accepted' }
    catch { Assert-True ($_.Exception.Message -match 'TaskBindingHash|pattern') 'wrong binding validation reason' }
}

Case 'strict-verification-receipt-entity-is-bound-and-rechecked' {
    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('es-abcd-orchestration-receipt-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    $strict = New-ESABCDOrchestrationStore -TaskId 'task-strict-receipt' -TaskBindingId 'binding-strict-receipt' -TaskBindingHash $bindingHash -AuthorizationRef 'auth.current-user' -RoutePlanHash $zero -SourceScopeHash $one -ProjectRoot $fixtureRoot -RequireVerificationReceiptEntity
    Start-ESABCDIterationRound $strict $strict.taskRevision $strict.contextVersion | Out-Null
    Add-ESABCDCandidate $strict 'root' $null $zero 'assumption' 'verify' $strict.taskRevision $strict.contextVersion | Out-Null
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'evidence.txt'), 'strict-audit-evidence', [Text.UTF8Encoding]::new($false))
    $evidenceRef = [pscustomobject][ordered]@{ path = 'evidence.txt'; sha256 = (Get-FileHash -LiteralPath (Join-Path $fixtureRoot 'evidence.txt') -Algorithm SHA256).Hash.ToLowerInvariant() }
    Add-ESABCDAuditRecord -Store $strict -BranchId 'root' -AuditorRef 'auditor.static.v1' -Verdict 'pass' -EvidenceRefs @($evidenceRef) -VerifierRef 'verifier.static.v1' -AuthorizationProof 'auth-proof/a' -VerifierDefinitionHash $verifierDefinitionHash -ExpectedTaskRevision $strict.taskRevision -ExpectedContextVersion $strict.contextVersion | Out-Null
    Select-ESABCDDecision -Store $strict -BranchId 'root' -Decision 'retry' -ClaimLevel 'full' -ExpectedTaskRevision $strict.taskRevision -ExpectedContextVersion $strict.contextVersion | Out-Null
    Start-ESABCDCorrectionCycle -Store $strict -CycleId 'cycle-strict' -FindingReceiptRef 'finding' -FailureClass 'evidence' -Decision 'retry' -ClaimLevel 'full' -AttemptNo 1 -ExpectedTaskRevision $strict.taskRevision -ExpectedContextVersion $strict.contextVersion | Out-Null
    try { Add-ESABCDVerificationReceipt -Store $strict -CycleId 'cycle-strict' -VerificationStatus 'passed' -VerificationReceiptRef 'missing.json' -VerificationReceiptHash $verificationReceiptHash -ExpectedTaskRevision $strict.taskRevision -ExpectedContextVersion $strict.contextVersion | Out-Null; throw 'missing verification entity accepted' }
    catch { Assert-Equal $_.Exception.Message 'VERIFICATION_RECEIPT_ENTITY_MISSING' 'missing entity was not fail-closed' }
    $receiptPayload = [ordered]@{ schemaVersion = 1; recordType = 'ABCDVerificationReceipt'; status = 'passed'; taskId = 'task-strict-receipt'; observed = 'ok' }
    $receiptHash = Get-ESABCDHash $receiptPayload
    $receiptPayload.receiptHash = $receiptHash
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'receipt.json'), ($receiptPayload | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    try { Add-ESABCDVerificationReceipt -Store $strict -CycleId 'cycle-strict' -VerificationStatus 'passed' -VerificationReceiptRef 'receipt.json' -VerificationReceiptHash $verificationReceiptHash -ExpectedTaskRevision $strict.taskRevision -ExpectedContextVersion $strict.contextVersion | Out-Null; throw 'mismatched receipt hash accepted' }
    catch { Assert-Equal $_.Exception.Message 'VERIFICATION_RECEIPT_HASH_MISMATCH' 'receipt hash mismatch was not fail-closed' }
    Add-ESABCDVerificationReceipt -Store $strict -CycleId 'cycle-strict' -VerificationStatus 'passed' -VerificationReceiptRef 'receipt.json' -VerificationReceiptHash $receiptHash -ExpectedTaskRevision $strict.taskRevision -ExpectedContextVersion $strict.contextVersion | Out-Null
    $eligible = Test-ESABCDCompletionEligibility $strict 'cycle-strict'; Assert-True $eligible.eligible 'strict valid receipt was rejected'
    $receiptPayload.observed = 'tampered'; [IO.File]::WriteAllText((Join-Path $fixtureRoot 'receipt.json'), ($receiptPayload | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $tampered = Test-ESABCDCompletionEligibility $strict 'cycle-strict'; Assert-True (-not $tampered.eligible) 'tampered receipt remained eligible'; Assert-Equal $tampered.reasonCode 'VERIFICATION_RECEIPT_HASH_MISMATCH' 'wrong tamper reason'
}

$failed = @($results | Where-Object status -eq 'failed')
$capturedUtc = [DateTime]::UtcNow.ToString('o')
$sourceRefs = @('ES/Automation/ABCD/ESABCDOrchestrator.psm1','ES/Automation/ABCD/Test-ESABCDOrchestration.ps1','ES/Automation/Contracts/es-abcd-orchestration-event-v1.schema.json')
$sourceRefHashes = [ordered]@{}
foreach($ref in $sourceRefs){$sourceRefHashes[$ref]=(Get-FileHash -LiteralPath (Join-Path $root $ref) -Algorithm SHA256).Hash.ToLowerInvariant()}
$planHash = Get-ESABCDHash ([ordered]@{validator='Test-ESABCDOrchestration';sourceRefHashes=$sourceRefHashes;cases=@($results)})
$evidenceContractPath = Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$evidenceContractHash = (Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$report = [ordered]@{
    schemaVersion = 1; validator = 'Test-ESABCDOrchestration'; contractId = 'es://automation/contracts/abcd/orchestration/v1'
    status = if ($failed.Count) { 'failed' } else { 'passed' }; caseCount = $results.Count; passedCount = @($results | Where-Object status -eq 'passed').Count; failedCount = $failed.Count
    cases = @($results); runtimeStatus = 'runtime-not-run'; staticStatus = if($failed.Count){'static-failed'}else{'static-passed'}; evidenceLevel = 'S1'; capturedUtc = $capturedUtc; authorizationKind = 'read-only'; planHash = $planHash; evidenceContractId = 'es.skill-evidence-receipt'; evidenceContractHash = $evidenceContractHash; skillName = 'es-agent-mechanism-replication'; case = 'orchestration'; receiptPath = $ReportPath.Replace('\','/'); sourceRefs = $sourceRefs; sourceRefHashes = $sourceRefHashes; toolId = 'es-abcd-orchestration-validator'; unityVersion = 'not-run'; claimsNotProven = @('Unity/Player behavior','Worker/host behavior','network and release behavior')
    decisionMap = [ordered]@{ retry = 'retry-same-plan'; replan = 'create-new-plan'; branch = 'await-collaborator-choice'; stop = 'stop-and-report' }
    failureClasses = @('input','source','route','capability','environment','evidence'); invariants = @('fresh-CAS-before-submit','verification-receipt-required-for-completion','verification-receipt-entity-bound-and-rehashed','idempotency-key-binds-task-cycle-attempt-route','receipt-does-not-own-decision')
}
$full = Join-Path $root $ReportPath; $parent = Split-Path -Parent $full; if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
[IO.File]::WriteAllText($full, ($report | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 20
if ($failed.Count) { exit 1 }
