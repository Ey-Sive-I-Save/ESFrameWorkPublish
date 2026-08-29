param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$modulePath = Join-Path $PSScriptRoot 'ESInteractionSessionBindingContract.psm1'
Import-Module $modulePath -Force

$script:passed = 0
$script:failed = 0
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Equal($Actual, $Expected, [string]$Message) { if ($Actual -cne $Expected) { throw "$Message expected=[$Expected] actual=[$Actual]" } }
function Invoke-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Host "PASS $Name" }
    catch { $script:failed++; Write-Host "FAIL $Name :: $($_.Exception.Message)" }
}
function Copy-Object($Value) { return ($Value | ConvertTo-Json -Depth 20 -Compress | ConvertFrom-Json) }

$hashA = 'a' * 64
$hashB = 'b' * 64
$hashC = 'c' * 64
$bindingId = 'isb-' + ('1' * 32)
$proof = New-ESInteractionAuthorityProofDocument -ProofId ('iap-' + ('2' * 32)) -BindingId $bindingId `
    -Authority ([ordered]@{sessionStateRootId='es-codex-session-state-v2';transcriptRootId='codex-home-sessions-v1';projectRootHash=$hashA;launchTokenHash=$hashB;resolutionMethod='registry-acceptance-process-ancestry-v1';normalizationVersion='interaction-authority-canonical-v1'}) `
    -Registry ([ordered]@{relativePath='sessions.json';snapshotLength=4096;sha256=$hashC;syncState='aligned';record=[ordered]@{recordId='record-1';sessionId=('a'*32);transcriptRelativePath='2026/08/rollout-a.jsonl';lifecycleStatus='Registered';launchPhase='ContextAccepted';registryContextAccepted=$true;recordHash=$hashA}}) `
    -Acceptance ([ordered]@{relativePath='acceptance-receipts/acceptance-a.json';sha256=$hashB;acceptedUtc='2026-08-27T01:00:30Z'}) `
    -Process ([ordered]@{pid=1234;processStartUtc='2026-08-27T01:00:00Z';ancestorChainHash=$hashC}) `
    -IssuedUtc '2026-08-27T01:01:00Z'
$receipt = New-ESInteractionSessionBindingReceiptDocument -BindingId $bindingId `
    -Scope ([ordered]@{taskId='task-1';goalRevisionHash=$hashA;routePlanHash=$hashB;profile='interaction-observation';scope='task-object'}) `
    -Session ([ordered]@{recordId='record-1';sessionId=('a'*32)}) `
    -Transcript ([ordered]@{relativePath='2026/08/rollout-a.jsonl';snapshotLength=50000000;prefixHash=$hashC;taskStartByteOffset=40000000}) `
    -AuthorityProofHash $proof.proofHash -IssuedUtc '2026-08-27T01:02:00Z'
$reference = New-ESInteractionBindingReference $receipt

function Test-Fixture($Ref=$reference, $Receipt=$receipt, $Proof=$proof) {
    Test-ESInteractionSessionBindingContract -Reference $Ref -Receipt $Receipt -AuthorityProof $Proof -ExpectedTaskId 'task-1' -ExpectedGoalRevisionHash $hashA -ExpectedRoutePlanHash $hashB -ExpectedProjectRootHash $hashA -ExpectedLaunchTokenHash $hashB -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T01:00:00Z' -ExpectedAncestorChainHash $hashC
}

Invoke-Case 'valid-three-layer-binding' {
    $result = Test-Fixture
    Assert-True $result.valid 'The valid binding was rejected.'
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.VALID' 'Valid reason code'
    Assert-True (-not $result.productionRouteIntegrated -and -not $result.globalP0Integrated) 'Static contract claimed production integration.'
}
Invoke-Case 'public-reference-has-exactly-two-fields' {
    Assert-Equal @($reference.PSObject.Properties).Count 2 'Reference field count'
    Assert-True ($null -eq $reference.PSObject.Properties['sessionId']) 'Raw sessionId leaked into the public reference.'
}
Invoke-Case 'receipt-has-exactly-eight-top-level-fields' {
    Assert-Equal @($receipt.PSObject.Properties).Count 8 'Receipt field count'
    Assert-True ($null -eq $receipt.PSObject.Properties['expiresUtc']) 'Receipt retained unnecessary expiry state.'
    Assert-True ($null -eq $receipt.transcript.PSObject.Properties['taskStartLine']) 'Receipt retained line-based transcript state.'
}
Invoke-Case 'canonical-hash-ignores-property-order' {
    $reordered = [pscustomobject][ordered]@{issuedUtc=$proof.issuedUtc;process=$proof.process;acceptance=$proof.acceptance;registry=$proof.registry;authority=$proof.authority;bindingId=$proof.bindingId;proofId=$proof.proofId;contractId=$proof.contractId}
    $actual = Get-ESInteractionBindingCanonicalHash $reordered
    Assert-Equal $actual $proof.proofHash 'Canonical proof hash'
}
Invoke-Case 'authority-constructor-rejects-invalid-shape' {
    $threw = $false
    try {
        New-ESInteractionAuthorityProofDocument -ProofId ('iap-' + ('2' * 32)) -BindingId $bindingId -Authority ([ordered]@{rootId='codex-sessions-local-v1'}) -Registry $proof.registry -Acceptance $proof.acceptance -Process $proof.process -IssuedUtc $proof.issuedUtc | Out-Null
    } catch { $threw = $_.Exception.Message -like 'AuthorityProof schema validation failed:*' }
    Assert-True $threw 'AuthorityProof constructor emitted an invalid document.'
}
Invoke-Case 'receipt-constructor-rejects-invalid-shape' {
    $threw = $false
    try {
        New-ESInteractionSessionBindingReceiptDocument -BindingId $bindingId -Scope ([ordered]@{taskId='task-1'}) -Session $receipt.session -Transcript $receipt.transcript -AuthorityProofHash $proof.proofHash -IssuedUtc $receipt.issuedUtc | Out-Null
    } catch { $threw = $_.Exception.Message -like 'InteractionSessionBindingReceipt schema validation failed:*' }
    Assert-True $threw 'Binding Receipt constructor emitted an invalid document.'
}
Invoke-Case 'missing-binding-is-claim-cap-not-global-block' {
    $result = Test-ESInteractionSessionBindingContract -Reference $null -Receipt $null -AuthorityProof $null -ExpectedTaskId 'task-1' -ExpectedGoalRevisionHash $hashA -ExpectedRoutePlanHash $hashB -ExpectedProjectRootHash $hashA -ExpectedLaunchTokenHash $hashB -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T01:00:00Z' -ExpectedAncestorChainHash $hashC
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.EVIDENCE_MISSING' 'Missing reason'
    Assert-Equal $result.effect 'claim-cap' 'Missing effect'
    Assert-Equal $result.outcome 'evidence-pending' 'Missing outcome'
    Assert-True (-not $result.globalP0Integrated) 'Missing optional evidence became global P0.'
}
Invoke-Case 'raw-session-id-in-reference-is-rejected' {
    $bad = Copy-Object $reference
    $bad | Add-Member -NotePropertyName sessionId -NotePropertyValue ('a'*32)
    $result = Test-Fixture -Ref $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.CONTRACT_INVALID' 'Raw session rejection'
}
Invoke-Case 'reference-hash-mismatch-is-rejected' {
    $bad = Copy-Object $reference; $bad.bindingHash = $hashA
    $result = Test-Fixture -Ref $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.RECEIPT_HASH_MISMATCH' 'Reference hash rejection'
}
Invoke-Case 'receipt-hash-mismatch-is-rejected' {
    $bad = Copy-Object $receipt; $bad.bindingHash = $hashA
    $result = Test-Fixture -Receipt $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.RECEIPT_HASH_MISMATCH' 'Receipt hash rejection'
}
Invoke-Case 'proof-hash-mismatch-is-rejected' {
    $bad = Copy-Object $proof; $bad.proofHash = $hashA
    $result = Test-Fixture -Proof $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.PROOF_HASH_MISMATCH' 'Proof hash rejection'
}
Invoke-Case 'binding-identity-mismatch-is-rejected' {
    $bad = Copy-Object $proof; $bad.bindingId = 'isb-' + ('3'*32); $bad.proofHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionAuthorityProofHashInput $bad)
    $result = Test-Fixture -Proof $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.IDENTITY_MISMATCH' 'Identity rejection'
}
Invoke-Case 'authority-proof-hash-mismatch-is-rejected' {
    $bad = Copy-Object $receipt; $bad.authorityProofHash = $hashA; $bad.bindingHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionBindingReceiptHashInput $bad)
    $badRef = New-ESInteractionBindingReference $bad
    $result = Test-Fixture -Ref $badRef -Receipt $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.AUTHORITY_HASH_MISMATCH' 'Authority hash rejection'
}
Invoke-Case 'cross-task-binding-is-rejected' {
    $result = Test-ESInteractionSessionBindingContract -Reference $reference -Receipt $receipt -AuthorityProof $proof -ExpectedTaskId 'task-2' -ExpectedGoalRevisionHash $hashA -ExpectedRoutePlanHash $hashB -ExpectedProjectRootHash $hashA -ExpectedLaunchTokenHash $hashB -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T01:00:00Z' -ExpectedAncestorChainHash $hashC
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.SCOPE_MISMATCH' 'Task scope rejection'
}
Invoke-Case 'cross-goal-binding-is-rejected' {
    $result = Test-ESInteractionSessionBindingContract -Reference $reference -Receipt $receipt -AuthorityProof $proof -ExpectedTaskId 'task-1' -ExpectedGoalRevisionHash $hashC -ExpectedRoutePlanHash $hashB -ExpectedProjectRootHash $hashA -ExpectedLaunchTokenHash $hashB -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T01:00:00Z' -ExpectedAncestorChainHash $hashC
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.SCOPE_MISMATCH' 'Goal scope rejection'
}
Invoke-Case 'cross-route-binding-is-rejected' {
    $result = Test-ESInteractionSessionBindingContract -Reference $reference -Receipt $receipt -AuthorityProof $proof -ExpectedTaskId 'task-1' -ExpectedGoalRevisionHash $hashA -ExpectedRoutePlanHash $hashC -ExpectedProjectRootHash $hashA -ExpectedLaunchTokenHash $hashB -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T01:00:00Z' -ExpectedAncestorChainHash $hashC
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.SCOPE_MISMATCH' 'Route scope rejection'
}
Invoke-Case 'cross-session-binding-is-rejected' {
    $bad = Copy-Object $receipt; $bad.session.sessionId = ('b'*32); $bad.bindingHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionBindingReceiptHashInput $bad)
    $badRef = New-ESInteractionBindingReference $bad
    $result = Test-Fixture -Ref $badRef -Receipt $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.SESSION_MISMATCH' 'Session rejection'
}
Invoke-Case 'cross-transcript-binding-is-rejected' {
    $bad = Copy-Object $receipt; $bad.transcript.relativePath = '2026/08/rollout-b.jsonl'; $bad.bindingHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionBindingReceiptHashInput $bad)
    $badRef = New-ESInteractionBindingReference $bad
    $result = Test-Fixture -Ref $badRef -Receipt $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.SESSION_MISMATCH' 'Transcript rejection'
}
Invoke-Case 'cross-project-authority-is-rejected' {
    $result = Test-ESInteractionSessionBindingContract -Reference $reference -Receipt $receipt -AuthorityProof $proof -ExpectedTaskId 'task-1' -ExpectedGoalRevisionHash $hashA -ExpectedRoutePlanHash $hashB -ExpectedProjectRootHash $hashC -ExpectedLaunchTokenHash $hashB -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T01:00:00Z' -ExpectedAncestorChainHash $hashC
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.AUTHORITY_CONTEXT_MISMATCH' 'Project authority rejection'
}
Invoke-Case 'cross-launch-authority-is-rejected' {
    $result = Test-ESInteractionSessionBindingContract -Reference $reference -Receipt $receipt -AuthorityProof $proof -ExpectedTaskId 'task-1' -ExpectedGoalRevisionHash $hashA -ExpectedRoutePlanHash $hashB -ExpectedProjectRootHash $hashA -ExpectedLaunchTokenHash $hashC -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T01:00:00Z' -ExpectedAncestorChainHash $hashC
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.AUTHORITY_CONTEXT_MISMATCH' 'Launch authority rejection'
}
Invoke-Case 'pid-reuse-identity-is-rejected' {
    $result = Test-ESInteractionSessionBindingContract -Reference $reference -Receipt $receipt -AuthorityProof $proof -ExpectedTaskId 'task-1' -ExpectedGoalRevisionHash $hashA -ExpectedRoutePlanHash $hashB -ExpectedProjectRootHash $hashA -ExpectedLaunchTokenHash $hashB -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T00:59:59Z' -ExpectedAncestorChainHash $hashC
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.AUTHORITY_CONTEXT_MISMATCH' 'PID reuse rejection'
}
Invoke-Case 'ancestor-chain-mismatch-is-rejected' {
    $result = Test-ESInteractionSessionBindingContract -Reference $reference -Receipt $receipt -AuthorityProof $proof -ExpectedTaskId 'task-1' -ExpectedGoalRevisionHash $hashA -ExpectedRoutePlanHash $hashB -ExpectedProjectRootHash $hashA -ExpectedLaunchTokenHash $hashB -ExpectedPid 1234 -ExpectedProcessStartUtc '2026-08-27T01:00:00Z' -ExpectedAncestorChainHash $hashA
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.AUTHORITY_CONTEXT_MISMATCH' 'Ancestor chain rejection'
}
Invoke-Case 'project-global-scope-is-rejected' {
    $bad = Copy-Object $receipt; $bad.scope.scope = 'project-global'; $bad.bindingHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionBindingReceiptHashInput $bad)
    $badRef = New-ESInteractionBindingReference $bad
    $result = Test-Fixture -Ref $badRef -Receipt $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.CONTRACT_INVALID' 'Global scope rejection'
}
Invoke-Case 'wrong-profile-is-rejected' {
    $bad = Copy-Object $receipt; $bad.scope.profile = 'completion'; $bad.bindingHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionBindingReceiptHashInput $bad)
    $badRef = New-ESInteractionBindingReference $bad
    $result = Test-Fixture -Ref $badRef -Receipt $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.CONTRACT_INVALID' 'Profile rejection'
}
Invoke-Case 'raw-launch-token-is-rejected' {
    $bad = Copy-Object $proof; $bad.authority | Add-Member -NotePropertyName launchToken -NotePropertyValue 'CodexLaunch:raw'
    $bad.proofHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionAuthorityProofHashInput $bad)
    $result = Test-Fixture -Proof $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.CONTRACT_INVALID' 'Raw token rejection'
}
Invoke-Case 'non-accepted-context-is-rejected' {
    $bad = Copy-Object $proof; $bad.registry.record.launchPhase = 'Failed'; $bad.proofHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionAuthorityProofHashInput $bad)
    $result = Test-Fixture -Proof $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.CONTRACT_INVALID' 'Context state rejection'
}
Invoke-Case 'task-offset-beyond-snapshot-is-rejected' {
    $bad = Copy-Object $receipt; $bad.transcript.taskStartByteOffset = 50000001; $bad.bindingHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionBindingReceiptHashInput $bad)
    $badRef = New-ESInteractionBindingReference $bad
    $result = Test-Fixture -Ref $badRef -Receipt $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.TRANSCRIPT_RANGE_INVALID' 'Transcript range rejection'
}
Invoke-Case 'path-traversal-is-rejected' {
    $bad = Copy-Object $proof; $bad.registry.relativePath = '../sessions.json'; $bad.proofHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionAuthorityProofHashInput $bad)
    $result = Test-Fixture -Proof $bad
    Assert-True ($result.reasonCode -in @('INTERACTION_BINDING.CONTRACT_INVALID','INTERACTION_BINDING.PATH_NOT_NORMALIZED')) 'Traversal path was not rejected.'
}
Invoke-Case 'backslash-path-is-rejected' {
    $bad = Copy-Object $proof; $bad.acceptance.relativePath = 'receipts\acceptance-a.json'; $bad.proofHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionAuthorityProofHashInput $bad)
    $result = Test-Fixture -Proof $bad
    Assert-True ($result.reasonCode -in @('INTERACTION_BINDING.CONTRACT_INVALID','INTERACTION_BINDING.PATH_NOT_NORMALIZED')) 'Backslash path was not rejected.'
}
Invoke-Case 'proof-issued-after-receipt-is-rejected' {
    $bad = Copy-Object $proof; $bad.issuedUtc = '2026-08-27T01:03:00Z'; $bad.proofHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionAuthorityProofHashInput $bad)
    $badReceipt = Copy-Object $receipt; $badReceipt.authorityProofHash = $bad.proofHash; $badReceipt.bindingHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionBindingReceiptHashInput $badReceipt)
    $badRef = New-ESInteractionBindingReference $badReceipt
    $result = Test-Fixture -Ref $badRef -Receipt $badReceipt -Proof $bad
    Assert-Equal $result.reasonCode 'INTERACTION_BINDING.ISSUANCE_ORDER_INVALID' 'Issuance order rejection'
}

Write-Host "RESULT passed=$script:passed failed=$script:failed"
if ($script:failed -gt 0) { exit 1 }
