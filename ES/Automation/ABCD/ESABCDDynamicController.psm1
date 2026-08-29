Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ESABCDOrchestrator.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ESABCDEvidence.psm1') -Force
$script:DecisionMap = [ordered]@{ retry = 'retry-same-plan'; replan = 'create-new-plan'; branch = 'await-collaborator-choice'; stop = 'stop-and-report' }

function Invoke-ESABCDDynamicIteration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Store,
        [Parameter(Mandatory)]$CandidateProposals,
        [Parameter(Mandatory)]$AuditProposals,
        [Parameter(Mandatory)][string]$SelectedBranchId,
        [Parameter(Mandatory)][string]$FindingReceiptRef,
        [Parameter(Mandatory)][ValidateSet('input','source','route','capability','environment','evidence')][string]$FailureClass,
        [Parameter(Mandatory)][ValidateSet('retry','replan','branch','stop')][string]$Decision,
        [ValidateSet('full','claim-cap')][string]$ClaimLevel = 'full',
        [Parameter(Mandatory)][int]$AttemptNo,
        [Parameter(Mandatory)][ValidateSet('passed','failed','review')][string]$VerificationStatus,
        [string]$VerificationReceiptRef,
        [string]$VerificationReceiptHash
    )
    if ([int]$Store.currentRound -ge [int]$Store.maxRounds) { throw 'ROUND_BUDGET_EXHAUSTED' }
    if (@($CandidateProposals).Count -lt 1 -or @($CandidateProposals).Count -gt 64) { throw 'DYNAMIC_CANDIDATE_BUDGET_INVALID' }
    if (@($AuditProposals).Count -ne @($CandidateProposals).Count) { throw 'DYNAMIC_AUDIT_CARDINALITY_MISMATCH' }
    $candidateIds = @($CandidateProposals | ForEach-Object { [string]$_.branchId })
    $auditIds = @($AuditProposals | ForEach-Object { [string]$_.branchId })
    if ($candidateIds.Count -ne @($candidateIds | Sort-Object -Unique).Count -or $auditIds.Count -ne @($auditIds | Sort-Object -Unique).Count -or (@($auditIds | Where-Object { $_ -notin $candidateIds }).Count -gt 0) -or (@($candidateIds | Where-Object { $_ -notin $auditIds }).Count -gt 0)) { throw 'DYNAMIC_CANDIDATE_AUDIT_BRANCH_SET_MISMATCH' }
    # Validate the complete batch before appending the first event. This keeps a malformed
    # candidate/audit proposal from leaving a partially committed iteration round.
    $seenSnapshots = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($proposal in @($CandidateProposals)) {
        if ([string]::IsNullOrWhiteSpace([string]$proposal.branchId) -or [string]$proposal.snapshotHash -notmatch '^[a-f0-9]{64}$' -or [string]::IsNullOrWhiteSpace([string]$proposal.changedAssumption) -or [string]::IsNullOrWhiteSpace([string]$proposal.verificationPredicate)) { throw 'DYNAMIC_CANDIDATE_PROPOSAL_INVALID' }
        if ($Store.branches.ContainsKey([string]$proposal.branchId)) { throw 'DYNAMIC_BRANCH_ALREADY_EXISTS' }
        if (-not $seenSnapshots.Add(([string]$proposal.snapshotHash).ToLowerInvariant())) { throw 'DYNAMIC_SNAPSHOT_NOT_ISOLATED' }
        if (@($Store.branches.Values | Where-Object { [string]$_.snapshotHash -ceq ([string]$proposal.snapshotHash).ToLowerInvariant() }).Count -gt 0) { throw 'DYNAMIC_SNAPSHOT_ALREADY_USED' }
        if (-not [string]::IsNullOrWhiteSpace([string]$proposal.parentBranchId) -and -not $Store.branches.ContainsKey([string]$proposal.parentBranchId)) { throw 'DYNAMIC_PARENT_BRANCH_MISSING' }
    }
    foreach ($audit in @($AuditProposals)) {
        if ([string]::IsNullOrWhiteSpace([string]$audit.branchId) -or [string]$audit.verdict -notin @('pass','fail','review') -or @($audit.evidenceRefs).Count -lt 1 -or [string]::IsNullOrWhiteSpace([string]$audit.auditorRef) -or [string]::IsNullOrWhiteSpace([string]$audit.verifierRef) -or [string]::IsNullOrWhiteSpace([string]$audit.authorizationProof) -or [string]$audit.verifierDefinitionHash -notmatch '^[a-f0-9]{64}$') { throw 'DYNAMIC_AUDIT_PROPOSAL_INVALID' }
        if ([string]$audit.auditorRef -ceq [string]$audit.verifierRef -or [string]$audit.auditorRef -ceq [string]$Store.authorizationRef -or [string]$audit.verifierRef -ceq [string]$Store.authorizationRef) { throw 'DYNAMIC_AUDIT_INDEPENDENCE_REQUIRED' }
        if ([bool]$Store.requireVerificationReceiptEntity) { try { [void](Assert-ESABCDEvidenceReferences -ProjectRoot ([string]$Store.projectRoot) -References @($audit.evidenceRefs)) } catch { throw 'DYNAMIC_AUDIT_EVIDENCE_ENTITY_INVALID' } }
    }
    # When proposals expose evaluator outputs, selection is evidence-driven and
    # deterministic; callers cannot silently override the ranked winner.
    $scored = @($CandidateProposals | Where-Object { $null -ne $_.PSObject.Properties['selectionScore'] })
    if ($scored.Count -gt 0) {
        if ($scored.Count -ne @($CandidateProposals).Count) { throw 'DYNAMIC_SELECTION_SCORE_SET_INCOMPLETE' }
        foreach ($p in $scored) { $parsedScore = 0.0; if (-not [double]::TryParse([string]$p.selectionScore, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$parsedScore)) { throw 'DYNAMIC_SELECTION_SCORE_INVALID' } }
        $winner = $CandidateProposals | Sort-Object @{Expression={[double]$_.selectionScore};Descending=$true}, @{Expression={if($null -ne $_.PSObject.Properties['riskDelta']){[double]$_.riskDelta}else{[double]::PositiveInfinity}}}, @{Expression={[string]$_.branchId}} | Select-Object -First 1
        if ([string]$winner.branchId -cne $SelectedBranchId) { throw 'DYNAMIC_SELECTED_BRANCH_NOT_DETERMINISTIC_WINNER' }
    }
    if ($SelectedBranchId -notin $candidateIds) { throw 'DYNAMIC_SELECTED_BRANCH_NOT_IN_BATCH' }
    $events = [Collections.Generic.List[object]]::new()
    $start = Start-ESABCDIterationRound -Store $Store -ExpectedTaskRevision $Store.taskRevision -ExpectedContextVersion $Store.contextVersion
    [void]$events.Add($start.event)
    foreach ($proposal in @($CandidateProposals)) {
        if ([string]::IsNullOrWhiteSpace([string]$proposal.branchId) -or [string]$proposal.snapshotHash -notmatch '^[a-f0-9]{64}$' -or [string]::IsNullOrWhiteSpace([string]$proposal.changedAssumption) -or [string]::IsNullOrWhiteSpace([string]$proposal.verificationPredicate)) { throw 'DYNAMIC_CANDIDATE_PROPOSAL_INVALID' }
        $snapshotPath = if ($null -ne $proposal.PSObject.Properties['snapshotPath']) { [string]$proposal.snapshotPath } else { $null }
        $snapshotArtifactHash = if ($null -ne $proposal.PSObject.Properties['snapshotArtifactHash']) { [string]$proposal.snapshotArtifactHash } else { $null }
        $r = Add-ESABCDCandidate -Store $Store -BranchId ([string]$proposal.branchId) -ParentBranchId ([string]$proposal.parentBranchId) -SnapshotHash ([string]$proposal.snapshotHash) -SnapshotPath $snapshotPath -SnapshotArtifactHash $snapshotArtifactHash -ChangedAssumption ([string]$proposal.changedAssumption) -VerificationPredicate ([string]$proposal.verificationPredicate) -ExpectedTaskRevision $Store.taskRevision -ExpectedContextVersion $Store.contextVersion
        [void]$events.Add($r.event)
    }
    foreach ($audit in @($AuditProposals)) {
        if ([string]::IsNullOrWhiteSpace([string]$audit.branchId) -or [string]$audit.verdict -notin @('pass','fail','review') -or @($audit.evidenceRefs).Count -lt 1 -or [string]::IsNullOrWhiteSpace([string]$audit.verifierRef) -or [string]::IsNullOrWhiteSpace([string]$audit.authorizationProof) -or [string]$audit.verifierDefinitionHash -notmatch '^[a-f0-9]{64}$') { throw 'DYNAMIC_AUDIT_PROPOSAL_INVALID' }
        $r = Add-ESABCDAuditRecord -Store $Store -BranchId ([string]$audit.branchId) -AuditorRef ([string]$audit.auditorRef) -Verdict ([string]$audit.verdict) -EvidenceRefs @($audit.evidenceRefs) -VerifierRef ([string]$audit.verifierRef) -AuthorizationProof ([string]$audit.authorizationProof) -VerifierDefinitionHash ([string]$audit.verifierDefinitionHash) -ExpectedTaskRevision $Store.taskRevision -ExpectedContextVersion $Store.contextVersion
        [void]$events.Add($r.event)
    }
    if (-not $Store.audits.ContainsKey($SelectedBranchId) -or [string]$Store.audits[$SelectedBranchId].verdict -ne 'pass') { throw 'DYNAMIC_SELECTED_BRANCH_NOT_AUDITED_PASS' }
    $selected = Select-ESABCDDecision -Store $Store -BranchId $SelectedBranchId -Decision $Decision -ClaimLevel $ClaimLevel -ExpectedTaskRevision $Store.taskRevision -ExpectedContextVersion $Store.contextVersion
    [void]$events.Add($selected.event)
    $cycleId = 'cycle-round-{0}-attempt-{1}' -f $Store.currentRound, $AttemptNo
    $cycle = Start-ESABCDCorrectionCycle -Store $Store -CycleId $cycleId -FindingReceiptRef $FindingReceiptRef -FailureClass $FailureClass -Decision $Decision -ClaimLevel $ClaimLevel -AttemptNo $AttemptNo -ExpectedTaskRevision $Store.taskRevision -ExpectedContextVersion $Store.contextVersion
    [void]$events.Add($cycle.event)
    $verification = Add-ESABCDVerificationReceipt -Store $Store -CycleId $cycleId -VerificationStatus $VerificationStatus -VerificationReceiptRef $VerificationReceiptRef -VerificationReceiptHash $VerificationReceiptHash -ExpectedTaskRevision $Store.taskRevision -ExpectedContextVersion $Store.contextVersion
    [void]$events.Add($verification.event)
    $advanced = $false
    if ($VerificationStatus -eq 'passed' -and $Decision -in @('retry','replan')) {
        $advance = Advance-ESABCDIterationRound -Store $Store -CycleId $cycleId -ExpectedTaskRevision $Store.taskRevision -ExpectedContextVersion $Store.contextVersion
        [void]$events.Add($advance.event); $advanced = $true
    } elseif ($Decision -eq 'stop') {
        $stop = Stop-ESABCDIteration -Store $Store -Reason 'ABCD dynamic controller received explicit stop decision.' -ExpectedTaskRevision $Store.taskRevision -ExpectedContextVersion $Store.contextVersion
        [void]$events.Add($stop.event)
    }
    [pscustomobject][ordered]@{ status = if ($Store.stopped) { 'stopped' } elseif ($advanced) { 'advanced' } else { 'review' }; cycleId = $cycleId; selectedBranchId = $SelectedBranchId; nextAction = $script:DecisionMap[$Decision]; advanced = $advanced; eventCount = $events.Count; events = @($events); snapshot = Get-ESABCDSnapshot $Store; eligibility = Test-ESABCDCompletionEligibility $Store $cycleId }
}

Export-ModuleMember -Function Invoke-ESABCDDynamicIteration
