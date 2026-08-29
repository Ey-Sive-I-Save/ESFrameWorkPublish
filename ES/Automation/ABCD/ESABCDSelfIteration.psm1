Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'ESABCDOrchestrator.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ESABCDDynamicController.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ESABCDLearning.psm1') -Force

$script:DecisionMap = [ordered]@{
    retry = 'retry-same-plan'
    replan = 'create-new-plan'
    branch = 'await-collaborator-choice'
    stop = 'stop-and-report'
}
$script:FailureClasses = @('input', 'source', 'route', 'capability', 'environment', 'evidence')
$script:HashPattern = '^[a-f0-9]{64}$'
$script:IdPattern = '^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$'

function ConvertTo-ESABCDSelfIterationCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) {
        return '{' + ((@($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object) | ForEach-Object {
            ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress), (ConvertTo-ESABCDSelfIterationCanonical $Value[$_]))
        }) -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object {
            ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress), (ConvertTo-ESABCDSelfIterationCanonical $_.Value))
        }) -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) {
        return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESABCDSelfIterationCanonical $_ }) -join ',') + ']'
    }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESABCDSelfIterationHash($Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESABCDSelfIterationCanonical $Value)))).Replace('-', '').ToLowerInvariant())
    } finally { $sha.Dispose() }
}

function Assert-ESABCDSelfIterationId([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:IdPattern) { throw "$Name is invalid." }
}

function Assert-ESABCDSelfIterationHash([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:HashPattern) { throw "$Name must be a lowercase SHA-256 hash." }
}

function Get-ESABCDSelfIterationHashInput($Value) {
    $copy = [ordered]@{}
    foreach ($property in $Value.PSObject.Properties) {
        if ($property.Name -notin @('resultHash', 'planHash')) { $copy[$property.Name] = $property.Value }
    }
    return $copy
}

function New-ESABCDDeterministicCandidateProposals {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Store,
        [Parameter(Mandatory)][ValidateRange(1, 256)][int]$RoundNo,
        [Parameter(Mandatory)][string]$Seed,
        [string[]]$CandidateHints,
        [ValidateRange(1, 16)][int]$MaxCandidates = 3
    )
    if ([string]::IsNullOrWhiteSpace($Seed) -or $Seed.Length -gt 256) { throw 'SELF_ITERATION_SEED_INVALID' }
    if ($RoundNo -ne ([int]$Store.currentRound + 1)) { throw 'SELF_ITERATION_ROUND_SEQUENCE_INVALID' }
    if ($RoundNo -gt [int]$Store.maxRounds) { throw 'SELF_ITERATION_ROUND_BUDGET_EXHAUSTED' }
    $hintCount = if ($null -eq $CandidateHints) { 0 } else { @($CandidateHints).Count }
    if ($hintCount -gt 64) { throw 'SELF_ITERATION_HINT_BUDGET_EXCEEDED' }
    $defaultHints = @('baseline-contract', 'alternate-route', 'reduced-capability')
    $proposals = [Collections.Generic.List[object]]::new()
    $seenSnapshots = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($index = 0; $index -lt $MaxCandidates; $index++) {
        $hint = if ($hintCount -gt 0) { [string]$CandidateHints[$index % $hintCount] } else { $defaultHints[$index % $defaultHints.Count] }
        if ([string]::IsNullOrWhiteSpace($hint) -or $hint.Length -gt 256) { throw 'SELF_ITERATION_HINT_INVALID' }
        $seedInput = [ordered]@{ taskId = [string]$Store.taskId; roundNo = $RoundNo; candidateNo = ($index + 1); seed = $Seed; hint = $hint; routePlanHash = [string]$Store.routePlanHash; sourceScopeHash = [string]$Store.sourceScopeHash }
        $snapshotHash = Get-ESABCDSelfIterationHash $seedInput
        if (-not $seenSnapshots.Add($snapshotHash)) { throw 'SELF_ITERATION_SNAPSHOT_COLLISION' }
        $branchId = 'si-r{0}-b{1}' -f $RoundNo, ($index + 1)
        Assert-ESABCDSelfIterationId $branchId 'BranchId'
        [void]$proposals.Add([pscustomobject][ordered]@{
            branchId = $branchId
            parentBranchId = $null
            snapshotHash = $snapshotHash
            changedAssumption = ('{0}; seed={1}; candidate={2}' -f $hint, $Seed, ($index + 1))
            verificationPredicate = 'snapshot-hash-equals:' + $snapshotHash
            generationIndex = $index
            generationHash = Get-ESABCDSelfIterationHash $seedInput
        })
    }
    return @($proposals)
}

function Invoke-ESABCDIndependentStructuralAudit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Store,
        [Parameter(Mandatory)]$CandidateProposals,
        [Parameter(Mandatory)][string]$AuditorRef,
        [Parameter(Mandatory)][string]$VerifierRef,
        [Parameter(Mandatory)][string]$AuthorizationProof,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$VerifierDefinitionHash
    )
    if ([string]::IsNullOrWhiteSpace($AuditorRef) -or [string]::IsNullOrWhiteSpace($VerifierRef) -or [string]::IsNullOrWhiteSpace($AuthorizationProof)) { throw 'SELF_ITERATION_AUDIT_AUTH_REQUIRED' }
    if ($AuditorRef -ceq $VerifierRef -or $AuditorRef -ceq [string]$Store.authorizationRef -or $VerifierRef -ceq [string]$Store.authorizationRef) { throw 'SELF_ITERATION_AUDIT_INDEPENDENCE_REQUIRED' }
    $items = @($CandidateProposals)
    if ($items.Count -lt 1 -or $items.Count -gt 16) { throw 'SELF_ITERATION_CANDIDATE_BUDGET_INVALID' }
    $ids = @($items | ForEach-Object { [string]$_.branchId })
    $snapshots = @($items | ForEach-Object { [string]$_.snapshotHash })
    if ($ids.Count -ne @($ids | Sort-Object -Unique).Count) { throw 'SELF_ITERATION_BRANCH_DUPLICATE' }
    if ($snapshots.Count -ne @($snapshots | Sort-Object -Unique).Count) { throw 'SELF_ITERATION_SNAPSHOT_NOT_ISOLATED' }
    $audits = [Collections.Generic.List[object]]::new()
    foreach ($candidate in $items) {
        Assert-ESABCDSelfIterationId ([string]$candidate.branchId) 'BranchId'
        Assert-ESABCDSelfIterationHash ([string]$candidate.snapshotHash) 'SnapshotHash'
        if ([string]::IsNullOrWhiteSpace([string]$candidate.changedAssumption) -or [string]::IsNullOrWhiteSpace([string]$candidate.verificationPredicate)) { throw 'SELF_ITERATION_CANDIDATE_STRUCTURE_INVALID' }
        $predicate = [string]$candidate.verificationPredicate
        $predicatePassed = $false
        $evaluatedChecks = [Collections.Generic.List[string]]::new()
        if ($predicate -match '^snapshot-hash-equals:([a-f0-9]{64})$') {
            $predicatePassed = ([string]$candidate.snapshotHash -ceq $Matches[1]); [void]$evaluatedChecks.Add('snapshot-hash-equals')
        } else {
            [void]$evaluatedChecks.Add('predicate-unregistered')
        }
        $evidenceRef = 'self-iteration/structural/' + [string]$candidate.branchId
        [void]$audits.Add([pscustomobject][ordered]@{
            branchId = [string]$candidate.branchId
            auditorRef = $AuditorRef
            verifierRef = $VerifierRef
            verifierDefinitionHash = $VerifierDefinitionHash.ToLowerInvariant()
            authorizationProof = $AuthorizationProof
            verdict = if ($predicatePassed) { 'pass' } else { 'review' }
            evidenceRefs = @($evidenceRef)
            structuralChecks = @('branch-id', 'snapshot-hash', 'assumption', 'verification-predicate', 'independent-auditor')
            evaluatedChecks = @($evaluatedChecks)
            predicateResult = if ($predicatePassed) { 'passed' } else { 'unproven' }
            auditHash = Get-ESABCDSelfIterationHash ([ordered]@{ branchId = [string]$candidate.branchId; snapshotHash = [string]$candidate.snapshotHash; evidenceRef = $evidenceRef; verifierDefinitionHash = $VerifierDefinitionHash.ToLowerInvariant() })
        })
    }
    return @($audits)
}

function Select-ESABCDSelfIterationCandidate {
    param([Parameter(Mandatory)]$CandidateProposals, [Parameter(Mandatory)]$AuditProposals)
    $audits = @($AuditProposals | Where-Object { [string]$_.verdict -eq 'pass' })
    if ($audits.Count -ne @($CandidateProposals).Count) { throw 'SELF_ITERATION_AUDIT_PASS_REQUIRED' }
    $candidateMap = @{}
    foreach ($candidate in @($CandidateProposals)) { $candidateMap[[string]$candidate.branchId] = $candidate }
    $selected = $audits | Sort-Object @{ Expression = { [string]$candidateMap[[string]$_.branchId].snapshotHash } }, @{ Expression = { [string]$_.branchId } } | Select-Object -First 1
    if ($null -eq $selected) { throw 'SELF_ITERATION_NO_SELECTABLE_CANDIDATE' }
    return [string]$selected.branchId
}

function Get-ESABCDSelfIterationReceiptHashInput($Receipt) {
    $copy = [ordered]@{}
    foreach ($property in $Receipt.PSObject.Properties) { if ($property.Name -ne 'resultHash') { $copy[$property.Name] = $property.Value } }
    return $copy
}

function Invoke-ESABCDSelfIteration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Store,
        [Parameter(Mandatory)][string]$Seed,
        [Parameter(Mandatory)][string]$FindingReceiptRef,
        [Parameter(Mandatory)][ValidateSet('input', 'source', 'route', 'capability', 'environment', 'evidence')][string]$FailureClass,
        [Parameter(Mandatory)][ValidateSet('retry', 'replan', 'branch', 'stop')][string]$Decision,
        [Parameter(Mandatory)][ValidateSet('passed', 'failed', 'review')][string[]]$VerificationStatuses,
        [string[]]$VerificationReceiptRefs,
        [string[]]$VerificationReceiptHashes,
        [string[]]$CandidateHints,
        [ValidateRange(1, 16)][int]$MaxCandidatesPerRound = 3,
        [string]$AuditorRef = 'self-iteration.auditor.v1',
        [string]$VerifierRef = 'self-iteration.verifier.v1',
        [string]$AuthorizationProof = 'self-iteration.authorization.v1',
        [ValidatePattern('^[a-f0-9]{64}$')][string]$VerifierDefinitionHash = ('0' * 64),
        [switch]$EmitLearningCandidate,
        [string]$KnowledgeId,
        [string]$TargetPath,
        [string[]]$RouteKeys,
        $SourceRefs,
        [string]$LearningVerifierId,
        [ValidatePattern('^[a-f0-9]{64}$')][string]$LearningVerifierDefinitionHash
    )
    if ([string]::IsNullOrWhiteSpace($FindingReceiptRef)) { throw 'SELF_ITERATION_FINDING_RECEIPT_REQUIRED' }
    if ([string]::IsNullOrWhiteSpace($Seed) -or $Seed.Length -gt 256) { throw 'SELF_ITERATION_SEED_INVALID' }
    $statuses = [Collections.Generic.List[string]]::new(); foreach ($statusValue in @($VerificationStatuses)) { if ($null -ne $statusValue) { [void]$statuses.Add([string]$statusValue) } }
    if ($statuses.Count -lt 1 -or $statuses.Count -gt [int]$Store.maxRounds) { throw 'SELF_ITERATION_ROUND_BUDGET_INVALID' }
    $maxAttempts = [int]$Store.maxRounds * [int]$Store.attemptsPerRound
    if ($statuses.Count -gt $maxAttempts) { throw 'SELF_ITERATION_ATTEMPT_BUDGET_INVALID' }
    $refs = [Collections.Generic.List[object]]::new(); foreach ($refValue in @($VerificationReceiptRefs)) { [void]$refs.Add($refValue) }
    $hashes = [Collections.Generic.List[object]]::new(); foreach ($hashValue in @($VerificationReceiptHashes)) { [void]$hashes.Add($hashValue) }
    if ($refs.Count -gt $statuses.Count -or $hashes.Count -gt $statuses.Count) { throw 'SELF_ITERATION_VERIFICATION_ARRAY_INVALID' }
    for ($i = 0; $i -lt $statuses.Count; $i++) {
        $status = [string]$statuses[$i]
        $ref = if ($i -lt $refs.Count) { [string]$refs[$i] } else { $null }
        $hash = if ($i -lt $hashes.Count) { [string]$hashes[$i] } else { $null }
        if ($status -eq 'passed') {
            if ([string]::IsNullOrWhiteSpace($ref) -or $ref -notmatch '^receipt-[A-Za-z0-9][A-Za-z0-9._:-]{2,127}$') { throw 'SELF_ITERATION_VERIFICATION_RECEIPT_REQUIRED' }
            Assert-ESABCDSelfIterationHash $hash 'VerificationReceiptHash'
        } elseif (-not [string]::IsNullOrWhiteSpace($ref) -or -not [string]::IsNullOrWhiteSpace($hash)) {
            throw 'SELF_ITERATION_FAILED_VERIFICATION_RECEIPT_FORBIDDEN'
        }
    }
    if ($Decision -eq 'branch') { throw 'SELF_ITERATION_BRANCH_REQUIRES_COLLABORATOR_SELECTION' }
    if ($EmitLearningCandidate) {
        if ([string]::IsNullOrWhiteSpace($KnowledgeId) -or [string]::IsNullOrWhiteSpace($TargetPath) -or @($RouteKeys).Count -lt 1 -or @($SourceRefs).Count -lt 1 -or [string]::IsNullOrWhiteSpace($LearningVerifierId)) { throw 'SELF_ITERATION_LEARNING_INPUT_REQUIRED' }
        Assert-ESABCDSelfIterationHash $LearningVerifierDefinitionHash 'LearningVerifierDefinitionHash'
    }

    $rounds = [Collections.Generic.List[object]]::new()
    $events = [Collections.Generic.List[object]]::new()
    $learningCandidates = [Collections.Generic.List[object]]::new()
    $usedAttempts = 0
    $stoppedReason = $null
    for ($i = 0; $i -lt $statuses.Count; $i++) {
        if ([int]$Store.currentRound -ge [int]$Store.maxRounds) { $stoppedReason = 'SELF_ITERATION_ROUND_BUDGET_EXHAUSTED'; break }
        $roundNo = [int]$Store.currentRound + 1
        $proposals = @(New-ESABCDDeterministicCandidateProposals -Store $Store -RoundNo $roundNo -Seed $Seed -CandidateHints $CandidateHints -MaxCandidates $MaxCandidatesPerRound)
        $audits = @(Invoke-ESABCDIndependentStructuralAudit -Store $Store -CandidateProposals $proposals -AuditorRef $AuditorRef -VerifierRef $VerifierRef -AuthorizationProof $AuthorizationProof -VerifierDefinitionHash $VerifierDefinitionHash)
        $selectedBranchId = Select-ESABCDSelfIterationCandidate -CandidateProposals $proposals -AuditProposals $audits
        $cycleClaimLevel = if ($Decision -eq 'branch') { 'claim-cap' } else { 'full' }
        $r = Invoke-ESABCDDynamicIteration -Store $Store -CandidateProposals $proposals -AuditProposals $audits -SelectedBranchId $selectedBranchId -FindingReceiptRef $FindingReceiptRef -FailureClass $FailureClass -Decision $Decision -ClaimLevel $cycleClaimLevel -AttemptNo 1 -VerificationStatus ([string]$statuses[$i]) -VerificationReceiptRef $(if ($i -lt $refs.Count) { [string]$refs[$i] } else { $null }) -VerificationReceiptHash $(if ($i -lt $hashes.Count) { [string]$hashes[$i] } else { $null })
        foreach ($event in @($r.events)) { [void]$events.Add($event) }
        $usedAttempts++
        $learningIds = [Collections.Generic.List[string]]::new()
        if ($EmitLearningCandidate -and [string]$statuses[$i] -eq 'passed' -and [string]$Decision -ne 'stop') {
            $candidate = New-ESABCDLearningCandidate -Store $Store -CycleId ([string]$r.cycleId) -KnowledgeId $KnowledgeId -TargetPath $TargetPath -RouteKeys $RouteKeys -SourceRefs $SourceRefs -VerifierId $LearningVerifierId -VerifierDefinitionHash $LearningVerifierDefinitionHash
            $learningRoot = if ($Store.PSObject.Properties.Name -contains 'projectRoot' -and -not [string]::IsNullOrWhiteSpace([string]$Store.projectRoot)) { [string]$Store.projectRoot } else { (Get-Location).Path }
            $validation = Test-ESABCDLearningCandidate -Store $Store -Candidate $candidate -ProjectRoot $learningRoot
            if ([string]$validation.status -ne 'validated') { throw 'SELF_ITERATION_LEARNING_SOURCE_NOT_STABLE' }
            [void]$learningCandidates.Add($candidate)
            [void]$learningIds.Add([string]$candidate.candidateId)
        }
        [void]$rounds.Add([pscustomobject][ordered]@{
            roundNo = $roundNo
            attemptNo = 1
            candidateProposals = @($proposals)
            auditProposals = @($audits)
            selectedBranchId = $selectedBranchId
            cycleId = [string]$r.cycleId
            verificationStatus = [string]$statuses[$i]
            verificationReceiptRef = if ($i -lt $refs.Count) { [string]$refs[$i] } else { $null }
            nextAction = [string]$r.nextAction
            controllerStatus = [string]$r.status
            advanced = [bool]$r.advanced
            learningCandidateIds = @($learningIds)
        })
        if ([string]$r.status -eq 'stopped') { $stoppedReason = 'ABCD_DECISION_STOP'; break }
        if ([string]$statuses[$i] -ne 'passed') {
            if ($Decision -eq 'stop' -or $i -ge ($statuses.Count - 1)) { $stoppedReason = 'VERIFICATION_NOT_PASSED'; break }
            # retry/replan consume the next bounded round; the next invocation re-reads the store CAS.
            continue
        }
    }
    if ($null -eq $stoppedReason -and [int]$Store.currentRound -ge [int]$Store.maxRounds) { $stoppedReason = 'SELF_ITERATION_ROUND_BUDGET_EXHAUSTED' }
    if ($null -eq $stoppedReason -and $rounds.Count -lt $statuses.Count) { $stoppedReason = 'SELF_ITERATION_ATTEMPT_BUDGET_EXHAUSTED' }
    $lastRound = if ($rounds.Count -gt 0) { $rounds[$rounds.Count - 1] } else { $null }
    $status = if ($null -eq $lastRound) { 'not-started' } elseif ([string]$lastRound.controllerStatus -eq 'stopped') { 'stopped' } elseif ([string]$lastRound.verificationStatus -ne 'passed') { 'review' } elseif ($stoppedReason -eq 'SELF_ITERATION_ROUND_BUDGET_EXHAUSTED') { 'budget-exhausted' } else { 'completed' }
    $candidateHintArray = [Collections.Generic.List[string]]::new(); foreach ($hintValue in @($CandidateHints)) { if ($null -ne $hintValue) { [void]$candidateHintArray.Add([string]$hintValue) } }
    $plan = [ordered]@{ seed = $Seed; failureClass = $FailureClass; decision = $Decision; maxRounds = [int]$Store.maxRounds; attemptsPerRound = [int]$Store.attemptsPerRound; maxTotalAttempts = $maxAttempts; maxCandidatesPerRound = $MaxCandidatesPerRound; candidateHints = $candidateHintArray.ToArray(); decisionMap = $script:DecisionMap; failureClasses = $script:FailureClasses }
    $receipt = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/abcd/self-iteration/v1'
        recordType = 'ABCDSelfIterationReceipt'
        iterationId = 'si-' + (Get-ESABCDSelfIterationHash ([ordered]@{ taskId = [string]$Store.taskId; seed = $Seed; routePlanHash = [string]$Store.routePlanHash })).Substring(0, 32)
        taskId = [string]$Store.taskId
        taskBindingRef = $Store.taskBindingRef
        routePlanHash = [string]$Store.routePlanHash
        sourceScopeHash = [string]$Store.sourceScopeHash
        plan = $plan
        planHash = Get-ESABCDSelfIterationHash $plan
        rounds = @($rounds)
        usedRounds = $rounds.Count
        usedAttempts = $usedAttempts
        status = $status
        stoppedReason = $stoppedReason
        nextAction = if ($null -ne $lastRound) { [string]$lastRound.nextAction } else { 'stop-and-report' }
        eventCount = $events.Count
        eventHashes = @($events | ForEach-Object { [string]$_.eventHash })
        learningCandidates = @($learningCandidates)
        nonClaims = @('static-orchestration-only', 'no-runtime-claim', 'no-release-claim', 'no-automatic-knowledge-promotion', 'verification-inputs-must-be-issued-by-independent-verifier')
        resultHash = $null
    }
    $receipt.resultHash = Get-ESABCDSelfIterationHash (Get-ESABCDSelfIterationReceiptHashInput ([pscustomobject]$receipt))
    return [pscustomobject]$receipt
}

function Test-ESABCDSelfIterationReceipt {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Receipt, [Parameter(Mandatory)]$Store)
    $issues = [Collections.Generic.List[string]]::new()
    try { Assert-ESABCDSelfIterationHash ([string]$Receipt.resultHash) 'ResultHash'; if ((Get-ESABCDSelfIterationHash (Get-ESABCDSelfIterationReceiptHashInput $Receipt)) -cne [string]$Receipt.resultHash) { [void]$issues.Add('SELF_ITERATION_RESULT_HASH_MISMATCH') } } catch { [void]$issues.Add($_.Exception.Message) }
    try { Assert-ESABCDSelfIterationHash ([string]$Receipt.planHash) 'PlanHash'; if ((Get-ESABCDSelfIterationHash $Receipt.plan) -cne [string]$Receipt.planHash) { [void]$issues.Add('SELF_ITERATION_PLAN_HASH_MISMATCH') } } catch { [void]$issues.Add($_.Exception.Message) }
    if ([string]$Receipt.taskId -cne [string]$Store.taskId) { [void]$issues.Add('SELF_ITERATION_TASK_MISMATCH') }
    if ([string]$Receipt.taskBindingRef.bindingId -cne [string]$Store.taskBindingRef.bindingId -or [string]$Receipt.taskBindingRef.bindingHash -cne [string]$Store.taskBindingRef.bindingHash) { [void]$issues.Add('SELF_ITERATION_BINDING_MISMATCH') }
    if ([string]$Receipt.routePlanHash -cne [string]$Store.routePlanHash -or [string]$Receipt.sourceScopeHash -cne [string]$Store.sourceScopeHash) { [void]$issues.Add('SELF_ITERATION_SCOPE_MISMATCH') }
    if ([int]$Receipt.plan.maxTotalAttempts -ne ([int]$Receipt.plan.maxRounds * [int]$Receipt.plan.attemptsPerRound)) { [void]$issues.Add('SELF_ITERATION_PLAN_BUDGET_INCONSISTENT') }
    if ([int]$Receipt.usedRounds -gt [int]$Receipt.plan.maxRounds -or [int]$Receipt.usedAttempts -gt [int]$Receipt.plan.maxTotalAttempts) { [void]$issues.Add('SELF_ITERATION_BUDGET_EXCEEDED') }
    if ([int]$Receipt.usedRounds -ne @($Receipt.rounds).Count -or [int]$Receipt.usedAttempts -ne @($Receipt.rounds).Count) { [void]$issues.Add('SELF_ITERATION_USAGE_COUNT_MISMATCH') }
    if ([int]$Receipt.eventCount -ne @($Receipt.eventHashes).Count) { [void]$issues.Add('SELF_ITERATION_EVENT_COUNT_MISMATCH') }
    $seenEvents = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($hash in @($Receipt.eventHashes)) { if (-not $seenEvents.Add([string]$hash)) { [void]$issues.Add('SELF_ITERATION_EVENT_DUPLICATE') } }
    $storeEventHashes = @($Store.events | ForEach-Object { [string]$_.eventHash })
    foreach ($hash in @($Receipt.eventHashes)) { if ($storeEventHashes -cnotcontains [string]$hash) { [void]$issues.Add('SELF_ITERATION_EVENT_NOT_IN_STORE') } }
    $expectedRound = 0
    foreach ($round in @($Receipt.rounds)) {
        $expectedRound++
        if ([int]$round.roundNo -ne $expectedRound -or [int]$round.attemptNo -ne 1) { [void]$issues.Add('SELF_ITERATION_ROUND_SEQUENCE_INVALID'); continue }
        $candidates = @($round.candidateProposals); $audits = @($round.auditProposals)
        if ($candidates.Count -lt 1 -or $candidates.Count -gt [int]$Receipt.plan.maxCandidatesPerRound -or $candidates.Count -ne $audits.Count) { [void]$issues.Add('SELF_ITERATION_ROUND_CARDINALITY_INVALID'); continue }
        $candidateMap = @{}; foreach ($candidate in $candidates) { $candidateMap[[string]$candidate.branchId] = $candidate }
        foreach ($audit in $audits) {
            if (-not $candidateMap.ContainsKey([string]$audit.branchId) -or [string]$audit.verdict -ne 'pass') { [void]$issues.Add('SELF_ITERATION_AUDIT_BRANCH_SET_INVALID'); continue }
            $candidate = $candidateMap[[string]$audit.branchId]
            $expectedEvidenceRef = 'self-iteration/structural/' + [string]$candidate.branchId
            $expectedAuditHash = Get-ESABCDSelfIterationHash ([ordered]@{ branchId = [string]$candidate.branchId; snapshotHash = [string]$candidate.snapshotHash; evidenceRef = $expectedEvidenceRef; verifierDefinitionHash = [string]$audit.verifierDefinitionHash })
            if ([string]$audit.auditHash -cne $expectedAuditHash -or @($audit.evidenceRefs) -cnotcontains $expectedEvidenceRef) { [void]$issues.Add('SELF_ITERATION_AUDIT_HASH_MISMATCH') }
        }
        if (-not $candidateMap.ContainsKey([string]$round.selectedBranchId) -or @($audits | Where-Object { [string]$_.branchId -ceq [string]$round.selectedBranchId -and [string]$_.verdict -eq 'pass' }).Count -ne 1) { [void]$issues.Add('SELF_ITERATION_SELECTION_INVALID') }
    }
    foreach ($candidate in @($Receipt.learningCandidates)) {
        try { if ((Get-ESABCDHash (Get-ESABCDLearningHashInput $candidate)) -cne [string]$candidate.candidateHash) { [void]$issues.Add('SELF_ITERATION_LEARNING_HASH_MISMATCH') } } catch { [void]$issues.Add('SELF_ITERATION_LEARNING_HASH_INVALID') }
        if ($candidate.validation.promotionAllowed -ne $false) { [void]$issues.Add('SELF_ITERATION_LEARNING_PROMOTION_FORBIDDEN') }
    }
    try { $integrity = Test-ESABCDEventStoreIntegrity $Store; if ([string]$integrity.status -ne 'passed') { [void]$issues.Add('SELF_ITERATION_EVENT_STORE_INVALID') } } catch { [void]$issues.Add($_.Exception.Message) }
    [pscustomobject][ordered]@{ receiptId = [string]$Receipt.iterationId; status = if ($issues.Count) { 'failed' } else { 'passed' }; issueCount = $issues.Count; issues = @($issues); staticStatus = if ($issues.Count) { 'static-failed' } else { 'static-passed' }; runtimeStatus = 'runtime-not-run' }
}

Export-ModuleMember -Function ConvertTo-ESABCDSelfIterationCanonical,Get-ESABCDSelfIterationHash,New-ESABCDDeterministicCandidateProposals,Invoke-ESABCDIndependentStructuralAudit,Select-ESABCDSelfIterationCandidate,Invoke-ESABCDSelfIteration,Test-ESABCDSelfIterationReceipt
