[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ReportPath = 'ES/Output/StaticReplay/es-abcd-self-iteration.json'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDSelfIteration.psm1') -Force
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDOrchestrator.psm1') -Force
Import-Module (Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force

$results = [Collections.Generic.List[object]]::new()
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; [void]$results.Add([pscustomobject]@{ case = $Name; status = 'passed'; finding = $null }) }
    catch { [void]$results.Add([pscustomobject]@{ case = $Name; status = 'failed'; finding = $_.Exception.Message }) }
}
function Assert-True([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Assert-Schema($Value) {
    $schemaPath = Join-Path $root 'ES/Automation/Contracts/es-abcd-self-iteration-v1.schema.json'
    $errors = @(Test-ESJsonSchemaValue -SchemaPath $schemaPath -Value $Value)
    if ($errors.Count) { throw ($errors -join '; ') }
}
function Hash([string]$Character) { return (($Character * 64) -join '') }
function New-Store([string]$TaskId, [int]$MaxRounds = 2) {
    New-ESABCDOrchestrationStore -TaskId $TaskId -TaskBindingId ($TaskId + '.binding') -TaskBindingHash (Hash '1') -AuthorizationRef 'auth.current-user' -RoutePlanHash (Hash '0') -SourceScopeHash (Hash '0') -MaxRounds $MaxRounds -AttemptsPerRound 2
}

$sourcePath = 'ES/Automation/ABCD/ESABCDSelfIteration.psm1'
$sourceHash = (Get-FileHash -LiteralPath (Join-Path $root $sourcePath) -Algorithm SHA256).Hash.ToLowerInvariant()
$receiptHash1 = Hash '2'
$receiptHash2 = Hash '3'

Case 'deterministic-candidate-generation' {
    $s = New-Store 'self-determinism'
    $a = @(New-ESABCDDeterministicCandidateProposals -Store $s -RoundNo 1 -Seed 'fixed-seed' -CandidateHints @('alpha', 'beta') -MaxCandidates 3)
    $b = @(New-ESABCDDeterministicCandidateProposals -Store $s -RoundNo 1 -Seed 'fixed-seed' -CandidateHints @('alpha', 'beta') -MaxCandidates 3)
    Assert-True ((Get-ESABCDSelfIterationHash $a) -ceq (Get-ESABCDSelfIterationHash $b)) 'same seed did not produce same candidate set'
    Assert-True (@($a | Select-Object -ExpandProperty snapshotHash | Sort-Object -Unique).Count -eq 3) 'candidate snapshots are not isolated'
}

Case 'positive-divergence-audit-iteration-learning' {
    $s = New-Store 'self-positive'
    $r = Invoke-ESABCDSelfIteration -Store $s -Seed 'positive-seed' -FindingReceiptRef 'finding/self-positive' -FailureClass 'evidence' -Decision 'retry' -VerificationStatuses @('passed', 'passed') -VerificationReceiptRefs @('receipt-self-1', 'receipt-self-2') -VerificationReceiptHashes @($receiptHash1, $receiptHash2) -CandidateHints @('safe-baseline', 'alternate-route') -MaxCandidatesPerRound 2 -EmitLearningCandidate -KnowledgeId 'abcd.knowledge.self-iteration' -TargetPath 'ES/Automation/Candidates/self-iteration.json' -RouteKeys @('abcd', 'learning') -SourceRefs @([pscustomobject][ordered]@{ path = $sourcePath; sha256 = $sourceHash; role = 'contract' }) -LearningVerifierId 'learning-verifier.v1' -LearningVerifierDefinitionHash (Hash '4')
    Assert-True ($r.status -eq 'budget-exhausted') 'bounded positive run did not terminate at round budget'
    Assert-True ($r.rounds.Count -eq 2 -and $r.learningCandidates.Count -eq 2) 'positive run did not emit one learning candidate per verified round'
    Assert-Schema $r
    $integrity = Test-ESABCDSelfIterationReceipt -Receipt $r -Store $s
    Assert-True ($integrity.status -eq 'passed') 'positive receipt failed integrity check'
}

Case 'failed-verification-stays-review-and-no-learning' {
    $s = New-Store 'self-failed-verification'
    $r = Invoke-ESABCDSelfIteration -Store $s -Seed 'failed-seed' -FindingReceiptRef 'finding/self-failed' -FailureClass 'source' -Decision 'replan' -VerificationStatuses @('failed', 'passed') -VerificationReceiptRefs @($null, 'receipt-self-recovered') -VerificationReceiptHashes @($null, $receiptHash1) -MaxCandidatesPerRound 2 -CandidateHints @('source-fix', 'fallback')
    Assert-True ($r.rounds[0].controllerStatus -eq 'review' -and -not $r.rounds[0].advanced) 'failed verification advanced or claimed completion'
    Assert-True ($r.learningCandidates.Count -eq 0) 'unrequested or unverified learning candidate was emitted'
    Assert-True (-not (Test-ESABCDCompletionEligibility $s $r.rounds[0].cycleId).eligible) 'failed verification entered completion eligibility'
}

Case 'round-and-attempt-budget-fail-closed' {
    $s = New-Store 'self-budget'
    $before = $s.events.Count
    try { Invoke-ESABCDSelfIteration -Store $s -Seed 'budget-seed' -FindingReceiptRef 'finding/budget' -FailureClass 'route' -Decision 'retry' -VerificationStatuses @('passed', 'passed', 'passed') -VerificationReceiptRefs @('receipt-a', 'receipt-b', 'receipt-c') -VerificationReceiptHashes @($receiptHash1, $receiptHash1, $receiptHash1) | Out-Null; throw 'round budget accepted'
    } catch { Assert-True ($_.Exception.Message -like '*SELF_ITERATION_ROUND_BUDGET_INVALID*') 'wrong round budget failure' }
    Assert-True ($s.events.Count -eq $before) 'budget rejection mutated the event store'
    try { Invoke-ESABCDSelfIteration -Store $s -Seed 'budget-seed' -FindingReceiptRef 'finding/budget' -FailureClass 'route' -Decision 'retry' -VerificationStatuses @('passed') -VerificationReceiptRefs @('receipt-a') -VerificationReceiptHashes @($receiptHash1) -MaxCandidatesPerRound 17 | Out-Null; throw 'candidate budget accepted'
    } catch { Assert-True ($_.Exception.Message -match 'MaxCandidatesPerRound|parameter|SELF_ITERATION') 'wrong candidate budget failure' }
}

Case 'structural-audit-is-independent-and-rejects-tamper' {
    $s = New-Store 'self-audit'
    $candidates = @(New-ESABCDDeterministicCandidateProposals -Store $s -RoundNo 1 -Seed 'audit-seed' -MaxCandidates 2)
    try { Invoke-ESABCDIndependentStructuralAudit -Store $s -CandidateProposals $candidates -AuditorRef 'auth.current-user' -VerifierRef 'self-iteration.verifier.v1' -AuthorizationProof 'proof' -VerifierDefinitionHash (Hash '5') | Out-Null; throw 'auditor authorization was accepted' }
    catch { Assert-True ($_.Exception.Message -like '*INDEPENDENCE_REQUIRED*') 'audit independence was not enforced' }
    $tampered = $candidates[1].snapshotHash
    $candidates[1].snapshotHash = $candidates[0].snapshotHash
    try { Invoke-ESABCDIndependentStructuralAudit -Store $s -CandidateProposals $candidates -AuditorRef 'self-iteration.auditor.v1' -VerifierRef 'self-iteration.verifier.v1' -AuthorizationProof 'proof' -VerifierDefinitionHash (Hash '5') | Out-Null; throw 'duplicate snapshot was accepted' }
    catch { Assert-True ($_.Exception.Message -like '*SNAPSHOT_NOT_ISOLATED*') 'snapshot tamper was not rejected' }
    $candidates[1].snapshotHash = $tampered
}

Case 'result-tamper-is-detected' {
    $s = New-Store 'self-result-tamper'
    $r = Invoke-ESABCDSelfIteration -Store $s -Seed 'tamper-seed' -FindingReceiptRef 'finding/tamper' -FailureClass 'evidence' -Decision 'retry' -VerificationStatuses @('passed') -VerificationReceiptRefs @('receipt-tamper') -VerificationReceiptHashes @($receiptHash1) -MaxCandidatesPerRound 1
    $r.resultHash = Hash 'f'
    $check = Test-ESABCDSelfIterationReceipt -Receipt $r -Store $s
    Assert-True ($check.status -eq 'failed' -and $check.issues -contains 'SELF_ITERATION_RESULT_HASH_MISMATCH') 'tampered result hash was accepted'
}

Case 'idempotency-replay-does-not-append' {
    $s = New-Store 'self-idempotency'
    $payload = [ordered]@{ roundNo = 1; roundBudget = 2 }
    $key = New-ESABCDIdempotencyKey -TaskId $s.taskId -CycleId 'self-cycle' -AttemptNo 1 -RoutePlanHash $s.routePlanHash
    $first = Add-ESABCDEvent -Store $s -EventType 'iteration-round-started' -Payload $payload -ExpectedTaskRevision $s.taskRevision -ExpectedContextVersion $s.contextVersion -IdempotencyKey $key
    $count = $s.events.Count
    $replay = Add-ESABCDEvent -Store $s -EventType 'iteration-round-started' -Payload $payload -ExpectedTaskRevision 1 -ExpectedContextVersion 1 -IdempotencyKey $key
    Assert-True ($first.status -eq 'appended' -and $replay.status -eq 'replayed' -and $s.events.Count -eq $count) 'idempotency replay appended a duplicate'
    try { Add-ESABCDEvent -Store $s -EventType 'iteration-round-started' -Payload ([ordered]@{ roundNo = 2; roundBudget = 2 }) -ExpectedTaskRevision 1 -ExpectedContextVersion 1 -IdempotencyKey $key | Out-Null; throw 'idempotency conflict accepted' }
    catch { Assert-True ($_.Exception.Message -like '*IDEMPOTENCY_CONFLICT*') 'wrong idempotency conflict reason' }
}

$failed = @($results | Where-Object { $_.status -eq 'failed' })
$sourceRefs = @('ES/Automation/ABCD/ESABCDSelfIteration.psm1', 'ES/Automation/ABCD/Test-ESABCDSelfIteration.ps1', 'ES/Automation/Contracts/es-abcd-self-iteration-v1.schema.json', 'ES/Automation/ABCD/ESABCDOrchestrator.psm1', 'ES/Automation/ABCD/ESABCDDynamicController.psm1')
$sourceRefHashes = [ordered]@{}
foreach ($ref in $sourceRefs) { $sourceRefHashes[$ref] = (Get-FileHash -LiteralPath (Join-Path $root $ref) -Algorithm SHA256).Hash.ToLowerInvariant() }
$evidenceContractPath = Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$evidenceContractHash = (Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$planHash = Get-ESABCDSelfIterationHash ([ordered]@{ validator = 'Test-ESABCDSelfIteration'; sourceRefHashes = $sourceRefHashes; cases = @($results) })
$report = [ordered]@{
    schemaVersion = 1; validator = 'Test-ESABCDSelfIteration'; contractId = 'es://automation/contracts/abcd/self-iteration/v1'
    status = if ($failed.Count) { 'failed' } else { 'passed' }; caseCount = $results.Count; passedCount = @($results | Where-Object { $_.status -eq 'passed' }).Count; failedCount = $failed.Count; cases = @($results)
    staticStatus = if ($failed.Count) { 'static-failed' } else { 'static-passed' }; runtimeStatus = 'runtime-not-run'; evidenceLevel = 'S1'; capturedUtc = [DateTime]::UtcNow.ToString('o'); authorizationKind = 'read-only'; planHash = $planHash
    evidenceContractId = 'es.skill-evidence-receipt'; evidenceContractHash = $evidenceContractHash; skillName = 'es-agent-mechanism-replication'; case = 'self-iteration'; receiptPath = $ReportPath.Replace('\', '/'); sourceRefs = $sourceRefs; sourceRefHashes = $sourceRefHashes; toolId = 'es-abcd-self-iteration-validator'; unityVersion = 'not-run'
    claimsNotProven = @('Unity/Player behavior', 'Worker/host behavior', 'cross-process CAS', 'external authority', 'automatic Knowledge promotion')
}
$full = Join-Path $root $ReportPath
$parent = Split-Path -Parent $full
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
[IO.File]::WriteAllText($full, ($report | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 40
if ($failed.Count) { exit 1 }
