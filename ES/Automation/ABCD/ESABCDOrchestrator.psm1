Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ESABCDEvidence.psm1')
Import-Module (Join-Path $PSScriptRoot 'ESABCDAuthorityKernel.psm1')

$script:HashPattern = '^[a-f0-9]{64}$'
$script:IdPattern = '^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$'
$script:DecisionMap = [ordered]@{
    retry = 'retry-same-plan'
    replan = 'create-new-plan'
    branch = 'await-collaborator-choice'
    stop = 'stop-and-report'
}
$script:EventTypes = @('iteration-round-started','candidate-expanded','candidate-pruned','branch-backtracked','audit-recorded','branch-selected','correction-cycle-started','verification-recorded','iteration-advanced','iteration-stopped')

function ConvertTo-ESABCDCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) {
        return '{' + ((@($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object) | ForEach-Object { ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress), (ConvertTo-ESABCDCanonical $Value[$_])) }) -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object { ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress), (ConvertTo-ESABCDCanonical $_.Value)) }) -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) { return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESABCDCanonical $_ }) -join ',') + ']' }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESABCDHash($Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESABCDCanonical $Value)))).Replace('-', '').ToLowerInvariant()) }
    finally { $sha.Dispose() }
}

function Assert-ESABCDId([string]$Value, [string]$Name) { if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:IdPattern) { throw "$Name is invalid." } }
function Assert-ESABCDHash([string]$Value, [string]$Name) { if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:HashPattern) { throw "$Name is invalid." } }

function New-ESABCDOrchestrationStore {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TaskId,
        [Parameter(Mandatory)][string]$TaskBindingId,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$TaskBindingHash,
        [Parameter(Mandatory)][string]$AuthorizationRef,
        [ValidateRange(1, 2147483647)][int]$TaskRevision = 1,
        [ValidateRange(1, 2147483647)][int]$ContextVersion = 1,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$RoutePlanHash,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$SourceScopeHash,
        [ValidateRange(1, 256)][int]$MaxRounds = 8,
        [ValidateRange(1, 256)][int]$AttemptsPerRound = 3,
        [string]$ProjectRoot,
        [switch]$RequireImmutableSnapshots,
        [switch]$RequireVerificationReceiptEntity
    )
    Assert-ESABCDId $TaskId 'TaskId'
    Assert-ESABCDId $TaskBindingId 'TaskBindingId'; if ([string]::IsNullOrWhiteSpace($AuthorizationRef)) { throw 'AuthorizationRef is required.' }
    $storeSeed = [ordered]@{ taskId = $TaskId; taskBindingId = $TaskBindingId; taskBindingHash = $TaskBindingHash.ToLowerInvariant(); routePlanHash = $RoutePlanHash.ToLowerInvariant(); sourceScopeHash = $SourceScopeHash.ToLowerInvariant() }
    [pscustomobject][ordered]@{
        schemaVersion = 1; contractId = 'es://automation/contracts/abcd/orchestration/v1'; recordType = 'ABCDOrchestrationStore'
        storeId = 'abcd-store-' + (Get-ESABCDHash $storeSeed).Substring(0,32); taskId = $TaskId; taskBindingRef = [pscustomobject][ordered]@{ bindingId = $TaskBindingId; bindingHash = $TaskBindingHash.ToLowerInvariant() }; authorizationRef = $AuthorizationRef
        taskRevision = $TaskRevision; contextVersion = $ContextVersion; routePlanHash = $RoutePlanHash.ToLowerInvariant(); sourceScopeHash = $SourceScopeHash.ToLowerInvariant()
        maxRounds = $MaxRounds; attemptsPerRound = $AttemptsPerRound; currentRound = 0; attemptsUsed = @{}; stopped = $false; events = [Collections.Generic.List[object]]::new()
        idempotency = @{}; branches = @{}; audits = @{}; selected = @{}; selectedBranchId = $null; cycles = @{}; verifications = @{}; auditorRegistry = @{}; receiptRegistry = @{}; projectRoot = if ($ProjectRoot) { (Resolve-Path -LiteralPath $ProjectRoot).Path } else { $null }; requireImmutableSnapshots = [bool]$RequireImmutableSnapshots; requireVerificationReceiptEntity = ([bool]$RequireVerificationReceiptEntity -or -not [string]::IsNullOrWhiteSpace($ProjectRoot))
    }
}

function Resolve-ESABCDVerificationReceiptEntity {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$ReceiptRef,[Parameter(Mandatory)][string]$ReceiptHash)
    if (-not [bool]$Store.requireVerificationReceiptEntity) { return $null }
    if ([string]::IsNullOrWhiteSpace([string]$Store.projectRoot)) { throw 'VERIFICATION_RECEIPT_PROJECT_ROOT_REQUIRED' }
    try {
        return Read-ESABCDReceipt -ProjectRoot ([string]$Store.projectRoot) -Path $ReceiptRef -ExpectedReceiptHash $ReceiptHash
    } catch {
        $message = [string]$_.Exception.Message
        if ($message -match 'EVIDENCE_FILE_MISSING|EVIDENCE_PATH_INVALID|EVIDENCE_PATH_OUTSIDE_PROJECT|EVIDENCE_REPARSE_POINT') { throw 'VERIFICATION_RECEIPT_ENTITY_MISSING' }
        if ($message -match 'EVIDENCE_ARTIFACT_HASH_MISMATCH|EVIDENCE_RECEIPT_REF_HASH_MISMATCH|EVIDENCE_RECEIPT_HASH_MISMATCH') { throw 'VERIFICATION_RECEIPT_HASH_MISMATCH' }
        throw 'VERIFICATION_RECEIPT_ENTITY_INVALID'
    }
}

function Get-ESABCDSnapshot($Store) {
    [pscustomobject][ordered]@{ taskId = [string]$Store.taskId; taskRevision = [int]$Store.taskRevision; contextVersion = [int]$Store.contextVersion; routePlanHash = [string]$Store.routePlanHash; sourceScopeHash = [string]$Store.sourceScopeHash; currentRound = [int]$Store.currentRound }
}

function Save-ESABCDOrchestrationSnapshot {
    [CmdletBinding()]param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$Path)
    $full=[IO.Path]::GetFullPath($Path);$dir=[IO.Path]::GetDirectoryName($full);if(-not(Test-Path -LiteralPath $dir)){New-Item -ItemType Directory -Force -Path $dir|Out-Null}
    $snapshot=Get-ESABCDSnapshot $Store;$payload=[ordered]@{schemaVersion=1;format='es.abcd.orchestration.snapshot.v1';snapshot=$snapshot;snapshotHash=(Get-ESABCDHash $snapshot);savedUtc=[DateTime]::UtcNow.ToString('o')};$tmp=$full+'.tmp-'+[Guid]::NewGuid().ToString('N');[IO.File]::WriteAllText($tmp,($payload|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false));Move-Item -LiteralPath $tmp -Destination $full -Force;[pscustomobject][ordered]@{status='saved';path=$full;snapshotHash=$payload.snapshotHash}
}

function Restore-ESABCDOrchestrationSnapshot {
    [CmdletBinding()]param([Parameter(Mandatory)][string]$Path)
    $full=[IO.Path]::GetFullPath($Path);$candidate=$full;$status='restored';if(-not(Test-Path -LiteralPath $candidate -PathType Leaf)){ $candidate=@(Get-ChildItem -LiteralPath ((Split-Path $full -Parent)) -Filter ((Split-Path $full -Leaf)+'.tmp-*') -File -ErrorAction SilentlyContinue|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1)[0].FullName;if([string]::IsNullOrWhiteSpace($candidate)){throw 'ABCD_SNAPSHOT_MISSING'};$status='recovered-from-temp' };$payload=Get-Content -LiteralPath $candidate -Raw -Encoding UTF8|ConvertFrom-Json;if([int]$payload.schemaVersion -ne 1 -or [string]$payload.format -cne 'es.abcd.orchestration.snapshot.v1'){throw 'ABCD_SNAPSHOT_FORMAT_INVALID'};if([string]$payload.snapshotHash -cne (Get-ESABCDHash $payload.snapshot)){throw 'ABCD_SNAPSHOT_HASH_MISMATCH'};[pscustomobject][ordered]@{status=$status;snapshot=$payload.snapshot;snapshotHash=[string]$payload.snapshotHash;path=$candidate}
}

function Test-ESABCDEventStoreIntegrity {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Store)
    $previous = $null; $sequence = 0; $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $expectedTaskRevision = $null
    $expectedContextVersion = $null
    foreach ($event in @($Store.events)) {
        $sequence++
        if ([int]$event.eventSequence -ne $sequence) { throw 'EVENT_SEQUENCE_GAP' }
        if ([string]$event.previousEventHash -cne [string]$previous) { throw 'EVENT_CHAIN_BROKEN' }
        if (-not $seen.Add([string]$event.idempotencyKey)) { throw 'EVENT_IDEMPOTENCY_DUPLICATE' }
        $payloadHash = Get-ESABCDHash $event.payload
        if ([string]$event.payloadHash -cne $payloadHash) { throw 'EVENT_PAYLOAD_HASH_MISMATCH' }
        $copy = [ordered]@{}; foreach ($p in $event.PSObject.Properties) { if ($p.Name -ne 'eventHash') { $copy[$p.Name] = $p.Value } }
        if ([string]$event.eventHash -cne (Get-ESABCDHash $copy)) { throw 'EVENT_HASH_MISMATCH' }
        if ($null -ne $expectedTaskRevision -and [int]$event.expectedTaskRevision -ne [int]$expectedTaskRevision) { throw 'EVENT_TASK_REVISION_CHAIN_BROKEN' }
        if ($null -ne $expectedContextVersion -and [int]$event.expectedContextVersion -ne [int]$expectedContextVersion) { throw 'EVENT_CONTEXT_VERSION_CHAIN_BROKEN' }
        if ([int]$event.observedTaskRevision -ne ([int]$event.expectedTaskRevision + 1)) { throw 'EVENT_TASK_REVISION_STEP_INVALID' }
        if ([int]$event.observedContextVersion -ne ([int]$event.expectedContextVersion + 1)) { throw 'EVENT_CONTEXT_VERSION_STEP_INVALID' }
        $expectedTaskRevision = [int]$event.observedTaskRevision
        $expectedContextVersion = [int]$event.observedContextVersion
        $previous = [string]$event.eventHash
    }
    if ($sequence -gt 0 -and ([int]$Store.taskRevision -ne [int]$expectedTaskRevision -or [int]$Store.contextVersion -ne [int]$expectedContextVersion)) { throw 'EVENT_STORE_PROJECTION_VERSION_MISMATCH' }
    # Rebuild the minimal projection from the immutable event stream and compare it
    # with the live projection. Hash-chain validity alone cannot detect ghost state.
    $rebuiltBranches = @{}; $rebuiltAudits = @{}; $rebuiltSelected = @{}; $rebuiltCycles = @{}; $rebuiltVerifications = @{}; $rebuiltRound = 0
    foreach ($event in @($Store.events)) {
        $p = $event.payload
        switch ([string]$event.eventType) {
            'iteration-round-started' { $rebuiltRound = [int]$p.roundNo }
            'candidate-expanded' { $rebuiltBranches[[string]$p.branchId] = [pscustomobject]$p }
            'candidate-pruned' { if ($rebuiltBranches.ContainsKey([string]$p.branchId)) { $rebuiltBranches[[string]$p.branchId].status = 'pruned' } }
            'branch-backtracked' { if ($rebuiltBranches.ContainsKey([string]$p.branchId)) { $rebuiltBranches[[string]$p.branchId].status = 'backtracked' } }
            'audit-recorded' { $rebuiltAudits[[string]$p.branchId] = [pscustomobject]$p }
            'branch-selected' { $rebuiltSelected[[string]$p.branchId] = [pscustomobject]$p }
            'correction-cycle-started' { $rebuiltCycles[[string]$p.cycleId] = [pscustomobject]$p }
            'verification-recorded' { $rebuiltVerifications[[string]$p.cycleId] = [pscustomobject]$p }
        }
    }
    if ([int]$Store.currentRound -ne $rebuiltRound) { throw 'EVENT_STORE_REPLAY_ROUND_MISMATCH' }
    $projectionPairs = @(
        [pscustomobject]@{ label = 'branches'; actual = $Store.branches; expected = $rebuiltBranches }
        [pscustomobject]@{ label = 'audits'; actual = $Store.audits; expected = $rebuiltAudits }
        [pscustomobject]@{ label = 'selected'; actual = $Store.selected; expected = $rebuiltSelected }
        [pscustomobject]@{ label = 'cycles'; actual = $Store.cycles; expected = $rebuiltCycles }
        [pscustomobject]@{ label = 'verifications'; actual = $Store.verifications; expected = $rebuiltVerifications }
    )
    foreach ($pair in $projectionPairs) {
        $actualKeys = (@($pair.actual.Keys | Sort-Object) -join '|')
        $expectedKeys = (@($pair.expected.Keys | Sort-Object) -join '|')
        if ($actualKeys -cne $expectedKeys) { throw ('EVENT_STORE_REPLAY_{0}_MISMATCH' -f ([string]$pair.label).ToUpperInvariant()) }
    }
    [pscustomobject][ordered]@{ status = 'passed'; eventCount = $sequence; lastEventHash = $previous; storeId = [string]$Store.storeId }
}

function Assert-ESABCDTransition {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$EventType,[Parameter(Mandatory)]$Payload)
    if ($Store.stopped -and $EventType -ne 'iteration-stopped') { throw 'ITERATION_ALREADY_STOPPED' }
    switch ($EventType) {
        'iteration-round-started' {
            if ([int]$Store.currentRound -ge [int]$Store.maxRounds) { throw 'ROUND_BUDGET_EXHAUSTED' }
            if ($null -eq $Payload.roundNo -or [int]$Payload.roundNo -ne ([int]$Store.currentRound + 1)) { throw 'ROUND_SEQUENCE_INVALID' }
        }
        'candidate-expanded' {
            if ([int]$Store.currentRound -lt 1) { throw 'ROUND_REQUIRED' }
            foreach ($n in @('branchId','snapshotHash','changedAssumption','verificationPredicate')) { if ($null -eq $Payload.$n) { throw "CANDIDATE_$($n.ToUpperInvariant())_REQUIRED" } }
        }
        'candidate-pruned' {
            if ($null -eq $Payload.branchId -or -not $Store.branches.ContainsKey([string]$Payload.branchId)) { throw 'BRANCH_MISSING' }
            if ([string]$Store.branches[[string]$Payload.branchId].status -ne 'open') { throw 'BRANCH_NOT_OPEN' }
            if ([string]::IsNullOrWhiteSpace([string]$Payload.reason)) { throw 'PRUNE_REASON_REQUIRED' }
        }
        'branch-backtracked' {
            if ($null -eq $Payload.branchId -or $null -eq $Payload.targetBranchId) { throw 'BACKTRACK_BRANCH_REQUIRED' }
            if (-not $Store.branches.ContainsKey([string]$Payload.branchId) -or -not $Store.branches.ContainsKey([string]$Payload.targetBranchId)) { throw 'BRANCH_MISSING' }
            if ([string]$Payload.branchId -ceq [string]$Payload.targetBranchId) { throw 'BACKTRACK_SELF_TARGET' }
            if ([string]$Store.branches[[string]$Payload.targetBranchId].status -eq 'pruned') { throw 'BACKTRACK_TARGET_PRUNED' }
            $cursor = [string]$Payload.branchId; $isAncestor = $false; $guard = 0
            while ($Store.branches.ContainsKey($cursor) -and $guard -lt 256) {
                $cursor = [string]$Store.branches[$cursor].parentBranchId; $guard++
                if ($cursor -ceq [string]$Payload.targetBranchId) { $isAncestor = $true; break }
                if ([string]::IsNullOrWhiteSpace($cursor)) { break }
            }
            if (-not $isAncestor) { throw 'BACKTRACK_TARGET_NOT_ANCESTOR' }
            if ([string]$Store.branches[[string]$Payload.branchId].status -eq 'backtracked') { throw 'BRANCH_ALREADY_BACKTRACKED' }
            if ([string]::IsNullOrWhiteSpace([string]$Payload.reason)) { throw 'BACKTRACK_REASON_REQUIRED' }
        }
        'audit-recorded' {
            if ($null -eq $Payload.branchId -or -not $Store.branches.ContainsKey([string]$Payload.branchId)) { throw 'BRANCH_MISSING' }
            if ([string]$Store.branches[[string]$Payload.branchId].status -eq 'pruned') { throw 'AUDIT_PRUNED_BRANCH' }
            if ($Store.audits.ContainsKey([string]$Payload.branchId)) { throw 'AUDIT_DUPLICATE' }
            foreach ($n in @('auditorRef','verifierRef','verifierDefinitionHash','authorizationProof','evidenceRefs','verdict')) { if ($null -eq $Payload.$n) { throw "AUDIT_$($n.ToUpperInvariant())_REQUIRED" } }
        }
        'branch-selected' {
            if ($null -eq $Payload.branchId -or -not $Store.audits.ContainsKey([string]$Payload.branchId)) { throw 'AUDIT_REQUIRED' }
            if ([string]$Store.audits[[string]$Payload.branchId].verdict -ne 'pass') { throw 'AUDIT_PASS_REQUIRED' }
            if (-not $Store.branches.ContainsKey([string]$Payload.branchId) -or [string]$Store.branches[[string]$Payload.branchId].status -ne 'open') { throw 'BRANCH_NOT_SELECTABLE' }
            if ($Store.selected.ContainsKey([string]$Payload.branchId)) { throw 'DECISION_DUPLICATE' }
            foreach ($n in @('decision','nextAction','claimLevel')) { if ($null -eq $Payload.$n) { throw "DECISION_$($n.ToUpperInvariant())_REQUIRED" } }
        }
        'correction-cycle-started' {
            if ($null -eq $Payload.cycleId -or $Store.cycles.ContainsKey([string]$Payload.cycleId)) { throw 'CYCLE_DUPLICATE' }
            if ($null -eq $Payload.branchId -or -not $Store.selected.ContainsKey([string]$Payload.branchId)) { throw 'DECISION_REQUIRED_BEFORE_CYCLE' }
            foreach ($n in @('findingReceiptRef','failureClass','decision','nextAction','attemptNo','claimLevel')) { if ($null -eq $Payload.$n) { throw "CYCLE_$($n.ToUpperInvariant())_REQUIRED" } }
        }
        'verification-recorded' {
            if ($null -eq $Payload.cycleId -or -not $Store.cycles.ContainsKey([string]$Payload.cycleId)) { throw 'CYCLE_MISSING' }
            if ($Store.verifications.ContainsKey([string]$Payload.cycleId)) { throw 'VERIFICATION_DUPLICATE' }
            if ([string]$Payload.taskId -cne [string]$Store.taskId -or [string]$Payload.routePlanHash -cne [string]$Store.routePlanHash -or [string]$Payload.sourceScopeHash -cne [string]$Store.sourceScopeHash) { throw 'VERIFICATION_CONTEXT_MISMATCH' }
            if ($null -eq $Payload.taskBindingRef -or [string]$Payload.taskBindingRef.bindingId -cne [string]$Store.taskBindingRef.bindingId -or [string]$Payload.taskBindingRef.bindingHash -cne [string]$Store.taskBindingRef.bindingHash) { throw 'VERIFICATION_BINDING_MISMATCH' }
            if ([string]$Payload.verificationStatus -eq 'passed' -and ([string]::IsNullOrWhiteSpace([string]$Payload.verificationReceiptRef) -or [string]$Payload.verificationReceiptHash -notmatch '^[a-f0-9]{64}$')) { throw 'VERIFICATION_RECEIPT_REQUIRED' }
            if ([bool]$Store.requireVerificationReceiptEntity -and [string]$Payload.verificationStatus -eq 'passed' -and [string]$Payload.verificationReceiptArtifactHash -notmatch '^[a-f0-9]{64}$') { throw 'VERIFICATION_RECEIPT_ENTITY_REQUIRED' }
        }
        'iteration-advanced' {
            if ($null -eq $Payload.cycleId -or -not $Store.verifications.ContainsKey([string]$Payload.cycleId)) { throw 'VERIFICATION_REQUIRED_BEFORE_ADVANCE' }
            if ([string]$Store.verifications[[string]$Payload.cycleId].verificationStatus -ne 'passed') { throw 'VERIFICATION_NOT_PASSED' }
            if ([string]$Store.cycles[[string]$Payload.cycleId].decision -eq 'stop') { throw 'STOPPED_CYCLE_CANNOT_ADVANCE' }
        }
        'iteration-stopped' { if ([string]::IsNullOrWhiteSpace([string]$Payload.reason)) { throw 'STOP_REASON_REQUIRED' } }
    }
}

function Add-ESABCDEvent {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$EventType,[Parameter(Mandatory)]$Payload,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion,[Parameter(Mandatory)][string]$IdempotencyKey)
    if ($script:EventTypes -notcontains $EventType) { throw "Unknown event type: $EventType" }
    if ($IdempotencyKey -notmatch '^abci-[a-f0-9]{64}$') { throw 'IdempotencyKey is invalid.' }
    $payloadHash = Get-ESABCDHash $Payload
    if ($Store.idempotency.ContainsKey($IdempotencyKey)) {
        $existing = $Store.idempotency[$IdempotencyKey]
        if ([string]$existing.payloadHash -cne $payloadHash -or [string]$existing.eventType -cne $EventType) { throw 'IDEMPOTENCY_CONFLICT' }
        return [pscustomobject][ordered]@{ status = 'replayed'; event = $existing }
    }
    # Correction attempts carry the mandatory task+cycle+attempt+route binding.
    # Legacy event types retain their deterministic event seeds for compatibility.
    if ($EventType -eq 'correction-cycle-started') {
        $allowedKeys = @(Get-ESABCDAllowedIdempotencyKeys -Store $Store -EventType $EventType -Payload $Payload)
        if ($allowedKeys.Count -gt 0 -and $allowedKeys -notcontains $IdempotencyKey) { throw 'IDEMPOTENCY_KEY_BINDING_INVALID' }
    }
    if ($ExpectedTaskRevision -ne [int]$Store.taskRevision -or $ExpectedContextVersion -ne [int]$Store.contextVersion) { throw 'CAS_STALE' }
    Assert-ESABCDTransition $Store $EventType $Payload
    $nextTaskRevision = [int]$Store.taskRevision + 1
    $nextContext = [int]$Store.contextVersion + 1
    $previousHash = if ($Store.events.Count -gt 0) { [string]$Store.events[$Store.events.Count - 1].eventHash } else { $null }
    $base = [ordered]@{ schemaVersion = 1; contractId = 'es://automation/contracts/abcd/orchestration-event/v1'; recordType = 'ABCDOrchestrationEvent'; eventId = $null; storeId = [string]$Store.storeId; taskId = [string]$Store.taskId; taskBindingRef = $Store.taskBindingRef; routePlanHash = [string]$Store.routePlanHash; sourceScopeHash = [string]$Store.sourceScopeHash; eventType = $EventType; eventSequence = $Store.events.Count + 1; previousEventHash = $previousHash; expectedTaskRevision = $ExpectedTaskRevision; expectedContextVersion = $ExpectedContextVersion; observedTaskRevision = $nextTaskRevision; observedContextVersion = $nextContext; idempotencyKey = $IdempotencyKey; authorizationRef = [string]$Store.authorizationRef; payloadHash = $payloadHash; payload = $Payload }
    $base.eventId = 'abcdn-' + (Get-ESABCDHash ([ordered]@{ taskId = $base.taskId; sequence = $base.eventSequence; idempotencyKey = $IdempotencyKey })).Substring(0,32)
    $base.eventHash = Get-ESABCDHash $base
    $event = [pscustomobject]$base
    [void]$Store.events.Add($event); $Store.idempotency[$IdempotencyKey] = $event; $Store.taskRevision = $nextTaskRevision; $Store.contextVersion = $nextContext
    [pscustomobject][ordered]@{ status = 'appended'; event = $event }
}

function New-ESABCDIdempotencyKey([string]$TaskId,[string]$CycleId,[int]$AttemptNo,[string]$RoutePlanHash) {
    Assert-ESABCDId $TaskId 'TaskId'; Assert-ESABCDId $CycleId 'CycleId'; Assert-ESABCDHash $RoutePlanHash 'RoutePlanHash'
    $seed = [ordered]@{ taskId = $TaskId; cycleId = $CycleId; attemptNo = $AttemptNo; routePlanHash = $RoutePlanHash.ToLowerInvariant() }
    return 'abci-' + (Get-ESABCDHash $seed)
}

function Get-ESABCDAllowedIdempotencyKeys {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$EventType,[Parameter(Mandatory)]$Payload)
    $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $seed = $null
    switch ($EventType) {
        'iteration-round-started' { $seed = [ordered]@{ taskId=$Store.taskId; event='round'; round=[int]$Payload.roundNo; routePlanHash=$Store.routePlanHash } }
        'candidate-expanded' { $seed = [ordered]@{ taskId=$Store.taskId; event='candidate'; branchId=$Payload.branchId; snapshotHash=$Payload.snapshotHash } }
        'candidate-pruned' { $seed = [ordered]@{ taskId=$Store.taskId; event='prune'; branchId=$Payload.branchId; reason=$Payload.reason } }
        'branch-backtracked' { $seed = [ordered]@{ taskId=$Store.taskId; event='backtrack'; branchId=$Payload.branchId; target=$Payload.targetBranchId; reason=$Payload.reason } }
        'audit-recorded' { $seed = [ordered]@{ taskId=$Store.taskId; event='audit'; branchId=$Payload.branchId; evidenceRefs=@($Payload.evidenceRefs) } }
        'branch-selected' { $seed = [ordered]@{ taskId=$Store.taskId; event='decision'; branchId=$Payload.branchId; decision=$Payload.decision } }
        'correction-cycle-started' { if ($Payload.cycleId -and $Payload.attemptNo) { [void]$keys.Add((New-ESABCDIdempotencyKey ([string]$Store.taskId) ([string]$Payload.cycleId) ([int]$Payload.attemptNo) ([string]$Store.routePlanHash))) } }
        'verification-recorded' { $seed = [ordered]@{ taskId=$Store.taskId; event='verification'; cycleId=$Payload.cycleId; status=$Payload.verificationStatus; receipt=$Payload.verificationReceiptRef; receiptHash=$Payload.verificationReceiptHash } }
        'iteration-advanced' { $seed = [ordered]@{ taskId=$Store.taskId; event='advance'; round=$Store.currentRound; cycleId=$Payload.cycleId } }
        'iteration-stopped' { $seed = [ordered]@{ taskId=$Store.taskId; event='stop'; round=$Store.currentRound; reason=$Payload.reason } }
    }
    if ($null -ne $seed) { [void]$keys.Add(('abci-' + (Get-ESABCDHash $seed))) }
    return @($keys)
}

function Start-ESABCDIterationRound {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    if ([int]$Store.currentRound -ge [int]$Store.maxRounds) { throw 'ROUND_BUDGET_EXHAUSTED' }
    $round = [int]$Store.currentRound + 1; $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'round'; round = $round; routePlanHash = $Store.routePlanHash }))
    $r = Add-ESABCDEvent $Store 'iteration-round-started' ([ordered]@{ roundNo = $round; roundBudget = $Store.attemptsPerRound }) $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.currentRound = $round }
    return $r
}

function Add-ESABCDCandidate {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$BranchId,[string]$ParentBranchId,[Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$SnapshotHash,[Parameter(Mandatory)][string]$ChangedAssumption,[Parameter(Mandatory)][string]$VerificationPredicate,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion,[string]$SnapshotPath,[string]$SnapshotArtifactHash)
    if ($ExpectedTaskRevision -ne [int]$Store.taskRevision -or $ExpectedContextVersion -ne [int]$Store.contextVersion) { throw 'CAS_STALE' }
    Assert-ESABCDId $BranchId 'BranchId'; if ($Store.branches.ContainsKey($BranchId)) { throw 'BRANCH_DUPLICATE' }
    if (-not [string]::IsNullOrWhiteSpace($ParentBranchId) -and -not $Store.branches.ContainsKey($ParentBranchId)) { throw 'BRANCH_PARENT_MISSING' }
    if (-not [string]::IsNullOrWhiteSpace($ParentBranchId) -and [string]$Store.branches[$ParentBranchId].snapshotHash -ceq $SnapshotHash.ToLowerInvariant()) { throw 'BRANCH_SNAPSHOT_NOT_ISOLATED' }
    if (@($Store.branches.Values | Where-Object { [string]$_.snapshotHash -ceq $SnapshotHash.ToLowerInvariant() }).Count -gt 0) { throw 'BRANCH_SNAPSHOT_ALREADY_USED' }
    if ($Store.requireImmutableSnapshots -and [string]::IsNullOrWhiteSpace($SnapshotPath)) { throw 'IMMUTABLE_SNAPSHOT_REQUIRED' }
    $snapshotRef = $null
    if (-not [string]::IsNullOrWhiteSpace($SnapshotPath)) {
        if ([string]::IsNullOrWhiteSpace([string]$Store.projectRoot)) { throw 'SNAPSHOT_PROJECT_ROOT_REQUIRED' }
        $snapshot = Read-ESABCDImmutableSnapshot -ProjectRoot ([string]$Store.projectRoot) -Path $SnapshotPath -SnapshotHash $SnapshotHash
        if (-not [string]::IsNullOrWhiteSpace($SnapshotArtifactHash) -and [string]$snapshot.artifactHash -cne $SnapshotArtifactHash.ToLowerInvariant()) { throw 'SNAPSHOT_ARTIFACT_HASH_MISMATCH' }
        $snapshotRef = [ordered]@{ path = [string]$snapshot.path; snapshotHash = [string]$snapshot.snapshotHash; artifactHash = [string]$snapshot.artifactHash; snapshotId = [string]$snapshot.snapshotId }
    }
    $branch = [ordered]@{ branchId = $BranchId; parentBranchId = if ($ParentBranchId) { $ParentBranchId } else { $null }; snapshotHash = $SnapshotHash.ToLowerInvariant(); snapshotRef = $snapshotRef; changedAssumption = $ChangedAssumption; verificationPredicate = $VerificationPredicate; status = 'open' }
    $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'candidate'; branchId = $BranchId; snapshotHash = $SnapshotHash }))
    $r = Add-ESABCDEvent $Store 'candidate-expanded' $branch $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.branches[$BranchId] = [pscustomobject]$branch }
    return $r
}

function Prune-ESABCDCandidate {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$BranchId,[Parameter(Mandatory)][string]$Reason,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    if (-not $Store.branches.ContainsKey($BranchId)) { throw 'BRANCH_MISSING' }; if ([string]::IsNullOrWhiteSpace($Reason)) { throw 'PRUNE_REASON_REQUIRED' }
    if ([string]$Store.branches[$BranchId].status -ne 'open') { throw 'BRANCH_STATE_INVALID_FOR_PRUNE' }
    $payload = [ordered]@{ branchId = $BranchId; reason = $Reason; disposition = 'pruned' }
    $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'prune'; branchId = $BranchId; reason = $Reason }))
    $r = Add-ESABCDEvent $Store 'candidate-pruned' $payload $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.branches[$BranchId].status = 'pruned' }
    return $r
}

function Backtrack-ESABCDCandidate {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$BranchId,[Parameter(Mandatory)][string]$TargetBranchId,[Parameter(Mandatory)][string]$Reason,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    if (-not $Store.branches.ContainsKey($BranchId) -or -not $Store.branches.ContainsKey($TargetBranchId)) { throw 'BRANCH_MISSING' }; if ($BranchId -ceq $TargetBranchId) { throw 'BACKTRACK_SELF_FORBIDDEN' }; if ([string]::IsNullOrWhiteSpace($Reason)) { throw 'BACKTRACK_REASON_REQUIRED' }
    $cursor = $BranchId; $isAncestor = $false; $guard = 0
    while ($Store.branches.ContainsKey($cursor) -and $guard -lt 256) { $cursor = [string]$Store.branches[$cursor].parentBranchId; $guard++; if ($cursor -ceq $TargetBranchId) { $isAncestor = $true; break }; if ([string]::IsNullOrWhiteSpace($cursor)) { break } }
    if (-not $isAncestor) { throw 'BACKTRACK_TARGET_NOT_ANCESTOR' }
    if ([string]$Store.branches[$BranchId].status -notin @('open','pruned')) { throw 'BRANCH_STATE_INVALID_FOR_BACKTRACK' }
    if ([string]$Store.branches[$TargetBranchId].status -ne 'open') { throw 'BACKTRACK_TARGET_NOT_OPEN' }
    $payload = [ordered]@{ branchId = $BranchId; targetBranchId = $TargetBranchId; reason = $Reason; disposition = 'backtracked' }
    $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'backtrack'; branchId = $BranchId; target = $TargetBranchId; reason = $Reason }))
    $r = Add-ESABCDEvent $Store 'branch-backtracked' $payload $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.branches[$BranchId].status = 'backtracked' }
    return $r
}

function Add-ESABCDAuditRecord {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$BranchId,[Parameter(Mandatory)][string]$AuditorRef,[Parameter(Mandatory)][ValidateSet('pass','fail','review')][string]$Verdict,[Parameter(Mandatory)][object[]]$EvidenceRefs,[Parameter(Mandatory)][string]$VerifierRef,[Parameter(Mandatory)][string]$AuthorizationProof,[Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$VerifierDefinitionHash,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    if (-not $Store.branches.ContainsKey($BranchId)) { throw 'BRANCH_MISSING' }; if ([string]$Store.branches[$BranchId].status -ne 'open') { throw 'BRANCH_NOT_AUDITABLE' }; if ([string]::IsNullOrWhiteSpace($AuditorRef) -or @($EvidenceRefs).Count -lt 1 -or [string]::IsNullOrWhiteSpace($VerifierRef) -or [string]::IsNullOrWhiteSpace($AuthorizationProof)) { throw 'AUDIT_EVIDENCE_MISSING' }; if ($AuditorRef -ceq $VerifierRef -or $AuditorRef -ceq [string]$Store.authorizationRef -or $VerifierRef -ceq [string]$Store.authorizationRef) { throw 'AUDIT_INDEPENDENCE_REQUIRED' }
    if ([bool]$Store.requireVerificationReceiptEntity) {
        if (@($EvidenceRefs | Where-Object { $_ -is [string] }).Count -gt 0) { throw 'AUDIT_EVIDENCE_ENTITY_REQUIRED' }
        try { [void](Assert-ESABCDEvidenceReferences -ProjectRoot ([string]$Store.projectRoot) -References $EvidenceRefs) } catch { throw 'AUDIT_EVIDENCE_ENTITY_INVALID' }
    }
    $audit = [ordered]@{ branchId = $BranchId; auditorRef = $AuditorRef; verifierRef = $VerifierRef; verifierDefinitionHash = $VerifierDefinitionHash.ToLowerInvariant(); authorizationProof = $AuthorizationProof; verdict = $Verdict; evidenceRefs = @($EvidenceRefs) }
    $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'audit'; branchId = $BranchId; evidenceRefs = @($EvidenceRefs) }))
    $r = Add-ESABCDEvent $Store 'audit-recorded' $audit $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.audits[$BranchId] = [pscustomobject]$audit }
    return $r
}

function Select-ESABCDDecision {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$BranchId,[Parameter(Mandatory)][ValidateSet('retry','replan','branch','stop')][string]$Decision,[ValidateSet('full','claim-cap')][string]$ClaimLevel = 'full',[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    if (-not $Store.audits.ContainsKey($BranchId)) { throw 'AUDIT_REQUIRED' }
    if ([string]$Store.audits[$BranchId].verdict -ne 'pass') { throw 'AUDIT_PASS_REQUIRED' }
    $payload = [ordered]@{ branchId = $BranchId; decision = $Decision; nextAction = $script:DecisionMap[$Decision]; claimLevel = $ClaimLevel }
    $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'decision'; branchId = $BranchId; decision = $Decision }))
    $r = Add-ESABCDEvent $Store 'branch-selected' $payload $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.selected[$BranchId] = [pscustomobject]$payload; $Store.selectedBranchId = $BranchId }
    return $r
}

function Start-ESABCDCorrectionCycle {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$CycleId,[Parameter(Mandatory)][string]$FindingReceiptRef,[Parameter(Mandatory)][ValidateSet('input','source','route','capability','environment','evidence')][string]$FailureClass,[Parameter(Mandatory)][ValidateSet('retry','replan','branch','stop')][string]$Decision,[ValidateSet('full','claim-cap')][string]$ClaimLevel = 'full',[Parameter(Mandatory)][int]$AttemptNo,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    Assert-ESABCDId $CycleId 'CycleId'; if ([string]::IsNullOrWhiteSpace($FindingReceiptRef)) { throw 'FINDING_RECEIPT_REQUIRED' }; if ($AttemptNo -lt 1 -or $AttemptNo -gt [int]$Store.attemptsPerRound) { throw 'ATTEMPT_BUDGET_EXHAUSTED' }
    if (-not $Store.selected.ContainsKey([string]$Store.selectedBranchId)) { throw 'DECISION_REQUIRED_BEFORE_CYCLE' }
    $selectedDecision = [string]$Store.selected[[string]$Store.selectedBranchId].decision
    if ($selectedDecision -cne $Decision) { throw 'DECISION_CYCLE_MISMATCH' }
    if ($Decision -eq 'branch' -and $ClaimLevel -eq 'full') { $ClaimLevel = 'claim-cap' }
    $key = New-ESABCDIdempotencyKey ([string]$Store.taskId) $CycleId $AttemptNo ([string]$Store.routePlanHash)
    $payload = [ordered]@{ cycleId = $CycleId; branchId = [string]$Store.selectedBranchId; findingReceiptRef = $FindingReceiptRef; failureClass = $FailureClass; decision = $Decision; nextAction = $script:DecisionMap[$Decision]; attemptNo = $AttemptNo; claimLevel = $ClaimLevel; routePlanHash = [string]$Store.routePlanHash; sourceScopeHash = [string]$Store.sourceScopeHash; verificationReceiptRef = $null; verificationReceiptHash = $null }
    # Resolve a duplicate cycle before budget/state checks so retries replay safely.
    if ($Store.idempotency.ContainsKey($key)) { return Add-ESABCDEvent $Store 'correction-cycle-started' $payload $ExpectedTaskRevision $ExpectedContextVersion $key }
    $roundKey = [string]$Store.currentRound
    if (-not $Store.attemptsUsed.ContainsKey($roundKey)) { $Store.attemptsUsed[$roundKey] = 0 }
    if ([int]$Store.attemptsUsed[$roundKey] -ge [int]$Store.attemptsPerRound) { throw 'ATTEMPT_BUDGET_EXHAUSTED' }
    if ($AttemptNo -ne ([int]$Store.attemptsUsed[$roundKey] + 1)) { throw 'ATTEMPT_SEQUENCE_INVALID' }
    $r = Add-ESABCDEvent $Store 'correction-cycle-started' $payload $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.cycles[$CycleId] = [pscustomobject]$payload; $Store.attemptsUsed[$roundKey] = [int]$Store.attemptsUsed[$roundKey] + 1 }
    return $r
}

function Add-ESABCDVerificationReceipt {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$CycleId,[Parameter(Mandatory)][ValidateSet('passed','failed','review')][string]$VerificationStatus,[string]$VerificationReceiptRef,[string]$VerificationReceiptHash,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    if (-not $Store.cycles.ContainsKey($CycleId)) { throw 'CYCLE_MISSING' }
    if ($VerificationStatus -eq 'passed' -and [string]::IsNullOrWhiteSpace($VerificationReceiptRef)) { throw 'VERIFICATION_RECEIPT_REQUIRED' }
    if ($VerificationStatus -eq 'passed' -and [string]$VerificationReceiptHash -notmatch '^[a-f0-9]{64}$') { throw 'VERIFICATION_RECEIPT_HASH_REQUIRED' }
    if ($VerificationStatus -ne 'passed' -and -not [string]::IsNullOrWhiteSpace($VerificationReceiptRef)) { throw 'FAILED_VERIFICATION_CANNOT_CLAIM_RECEIPT' }
    if ($VerificationStatus -ne 'passed' -and -not [string]::IsNullOrWhiteSpace($VerificationReceiptHash)) { throw 'FAILED_VERIFICATION_CANNOT_CLAIM_HASH' }
    $entity = if ($VerificationStatus -eq 'passed') { Resolve-ESABCDVerificationReceiptEntity $Store $VerificationReceiptRef $VerificationReceiptHash } else { $null }
    $payload = [ordered]@{ cycleId = $CycleId; taskId = [string]$Store.taskId; taskBindingRef = [pscustomobject][ordered]@{ bindingId = [string]$Store.taskBindingRef.bindingId; bindingHash = [string]$Store.taskBindingRef.bindingHash }; routePlanHash = [string]$Store.routePlanHash; sourceScopeHash = [string]$Store.sourceScopeHash; verificationStatus = $VerificationStatus; verificationReceiptRef = if ($VerificationReceiptRef) { $VerificationReceiptRef } else { $null }; verificationReceiptHash = if ($VerificationReceiptHash) { $VerificationReceiptHash.ToLowerInvariant() } else { $null }; verificationReceiptArtifactHash = if ($null -ne $entity) { [string]$entity.sha256 } else { $null } }
    $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'verification'; cycleId = $CycleId; status = $VerificationStatus; receipt = $VerificationReceiptRef; receiptHash = $VerificationReceiptHash }))
    $r = Add-ESABCDEvent $Store 'verification-recorded' $payload $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.verifications[$CycleId] = [pscustomobject]$payload }
    return $r
}

function Advance-ESABCDIterationRound {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$CycleId,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    if (-not $Store.verifications.ContainsKey($CycleId) -or [string]$Store.verifications[$CycleId].verificationStatus -cne 'passed') { throw 'VERIFICATION_REQUIRED_BEFORE_ADVANCE' }
    $payload = [ordered]@{ roundNo = [int]$Store.currentRound; cycleId = $CycleId; nextAction = 'next-round-or-stop' }
    $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'advance'; round = $Store.currentRound; cycleId = $CycleId }))
    return Add-ESABCDEvent $Store 'iteration-advanced' $payload $ExpectedTaskRevision $ExpectedContextVersion $key
}

function Stop-ESABCDIteration {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$Reason,[Parameter(Mandatory)][int]$ExpectedTaskRevision,[Parameter(Mandatory)][int]$ExpectedContextVersion)
    if ([string]::IsNullOrWhiteSpace($Reason)) { throw 'STOP_REASON_REQUIRED' }
    $payload = [ordered]@{ roundNo = [int]$Store.currentRound; reason = $Reason; nextAction = 'stop-and-report' }
    $key = 'abci-' + (Get-ESABCDHash ([ordered]@{ taskId = $Store.taskId; event = 'stop'; round = $Store.currentRound; reason = $Reason }))
    if ($Store.stopped -and -not $Store.idempotency.ContainsKey($key)) { throw 'ITERATION_ALREADY_STOPPED' }
    $r = Add-ESABCDEvent $Store 'iteration-stopped' $payload $ExpectedTaskRevision $ExpectedContextVersion $key
    if ($r.status -eq 'appended') { $Store.stopped = $true }
    return $r
}

function Test-ESABCDCompletionEligibility {
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$CycleId)
    $v = if ($Store.verifications.ContainsKey($CycleId)) { $Store.verifications[$CycleId] } else { $null }
    $cycle = if ($Store.cycles.ContainsKey($CycleId)) { $Store.cycles[$CycleId] } else { $null }
    $branchId = if ($null -ne $cycle) { [string]$cycle.branchId } else { $null }
    $audit = if ($branchId -and $Store.audits.ContainsKey($branchId)) { $Store.audits[$branchId] } else { $null }
    $decision = if ($branchId -and $Store.selected.ContainsKey($branchId)) { $Store.selected[$branchId] } else { $null }
    $bindingMatch = ($null -ne $v -and $null -ne $v.taskBindingRef -and (([string]$v.taskBindingRef.bindingId) -ceq ([string]$Store.taskBindingRef.bindingId)) -and (([string]$v.taskBindingRef.bindingHash) -ceq ([string]$Store.taskBindingRef.bindingHash)))
    $contextMatch = ($null -ne $v -and (([string]$v.taskId) -ceq ([string]$Store.taskId)) -and (([string]$v.routePlanHash) -ceq ([string]$Store.routePlanHash)) -and (([string]$v.sourceScopeHash) -ceq ([string]$Store.sourceScopeHash)))
    $eventStoreMatch = $true; $eventStoreReason = $null
    try {
        [void](Test-ESABCDEventStoreIntegrity $Store)
        $cycleEvent = @($Store.events | Where-Object { [string]$_.eventType -ceq 'correction-cycle-started' -and [string]$_.payload.cycleId -ceq $CycleId }) | Select-Object -Last 1
        if ($null -eq $cycleEvent) { $eventStoreMatch = $false; $eventStoreReason = 'CYCLE_EVENT_MISSING' }
        elseif ($null -eq $cycle -or (Get-ESABCDHash $cycle) -cne [string]$cycleEvent.payloadHash) { $eventStoreMatch = $false; $eventStoreReason = 'CYCLE_PROJECTION_MISMATCH' }
        if ($eventStoreMatch -and $null -ne $audit) {
            $auditEvent = @($Store.events | Where-Object { [string]$_.eventType -ceq 'audit-recorded' -and [string]$_.payload.branchId -ceq $branchId }) | Select-Object -Last 1
            if ($null -eq $auditEvent -or (Get-ESABCDHash $audit) -cne [string]$auditEvent.payloadHash) { $eventStoreMatch = $false; $eventStoreReason = 'AUDIT_PROJECTION_MISMATCH' }
        }
        if ($eventStoreMatch -and $null -ne $decision) {
            $decisionEvent = @($Store.events | Where-Object { [string]$_.eventType -ceq 'branch-selected' -and [string]$_.payload.branchId -ceq $branchId }) | Select-Object -Last 1
            if ($null -eq $decisionEvent -or (Get-ESABCDHash $decision) -cne [string]$decisionEvent.payloadHash) { $eventStoreMatch = $false; $eventStoreReason = 'DECISION_PROJECTION_MISMATCH' }
        }
        if ($eventStoreMatch -and $null -ne $v) {
            $verificationEvent = @($Store.events | Where-Object { [string]$_.eventType -ceq 'verification-recorded' -and [string]$_.payload.cycleId -ceq $CycleId }) | Select-Object -Last 1
            if ($null -eq $verificationEvent) { $eventStoreMatch = $false; $eventStoreReason = 'VERIFICATION_EVENT_MISSING' }
            elseif ((Get-ESABCDHash $v) -cne [string]$verificationEvent.payloadHash) { $eventStoreMatch = $false; $eventStoreReason = 'VERIFICATION_PROJECTION_MISMATCH' }
        }
    } catch {
        # Preserve the most actionable projection reason when a caller forged a
        # verification projection without appending its corresponding event.
        $verificationEventExists = @($Store.events | Where-Object { [string]$_.eventType -ceq 'verification-recorded' -and [string]$_.payload.cycleId -ceq $CycleId }).Count -gt 0
        $eventStoreMatch = $false
        $eventStoreReason = if ($null -ne $v -and -not $verificationEventExists) { 'VERIFICATION_EVENT_MISSING' } else { 'EVENT_STORE_INVALID' }
    }
    $entityMatch = $true; $entityReason = $null
    if ([bool]$Store.requireVerificationReceiptEntity -and $null -ne $v -and [string]$v.verificationStatus -ceq 'passed') {
        try {
            $entity = Resolve-ESABCDVerificationReceiptEntity $Store ([string]$v.verificationReceiptRef) ([string]$v.verificationReceiptHash)
            $entityMatch = ($null -ne $entity -and [string]$v.verificationReceiptArtifactHash -ceq [string]$entity.sha256)
            if (-not $entityMatch) { $entityReason = 'VERIFICATION_RECEIPT_HASH_MISMATCH' }
        } catch {
            $entityMatch = $false
            $entityReason = [string]$_.Exception.Message
        }
    }
    $claimCap = ($null -ne $decision -and [string]$decision.claimLevel -ceq 'claim-cap') -or ($null -ne $cycle -and [string]$cycle.claimLevel -ceq 'claim-cap')
    $eligible = ($null -ne $cycle -and $null -ne $audit -and (([string]$audit.verdict) -ceq 'pass') -and -not [string]::IsNullOrWhiteSpace([string]$audit.verifierRef) -and -not [string]::IsNullOrWhiteSpace([string]$audit.authorizationProof) -and $null -ne $decision -and (([string]$decision.nextAction) -notin @('stop-and-report','await-collaborator-choice')) -and -not $claimCap -and $null -ne $v -and (([string]$v.verificationStatus) -ceq 'passed') -and -not [string]::IsNullOrWhiteSpace([string]$v.verificationReceiptRef) -and (([string]$v.verificationReceiptHash) -match '^[a-f0-9]{64}$') -and $contextMatch -and $bindingMatch -and $entityMatch -and $eventStoreMatch)
    # Completion is a core-high-risk decision.  Route it through the same
    # kernel that selects capabilities so a future completion path cannot
    # accept a receipt without the six-capability closure and an explicit
    # authority result.
    $kernelEvidence = [pscustomobject][ordered]@{
        cycleId = $CycleId
        completionEligible = [bool]$eligible
        requiredCapabilities = @(Get-ESABCDCoreCapabilities)
        selectedCapabilities = @(Get-ESABCDCoreCapabilities)
        evidenceStatus = if ($eligible) { 'complete' } else { 'incomplete' }
    }
    $kernelMissing = if ($eligible) { @() } else { @('completionPrerequisite') }
    $authority = Resolve-ESABCDAuthorityDecision -Mode core-high-risk -Evidence $kernelEvidence -MissingFields $kernelMissing
    if ($eligible -and $authority.status -ne 'accepted') { $eligible = $false }
    $reason = if ($null -eq $cycle) { 'CYCLE_MISSING' } elseif ($null -eq $audit -or [string]$audit.verdict -ne 'pass') { 'AUDIT_PASS_REQUIRED' } elseif ($null -eq $decision) { 'DECISION_REQUIRED' } elseif ($claimCap) { 'CLAIM_CAP_BLOCKS_COMPLETION' } elseif ([string]$decision.nextAction -eq 'await-collaborator-choice') { 'COLLABORATOR_CHOICE_REQUIRED' } elseif ($null -eq $v) { 'VERIFICATION_MISSING' } elseif (-not $bindingMatch) { 'VERIFICATION_BINDING_MISMATCH' } elseif (-not $contextMatch) { 'VERIFICATION_CONTEXT_MISMATCH' } elseif ($entityReason) { $entityReason } elseif (-not $eventStoreMatch) { $eventStoreReason } elseif (-not $eligible) { 'VERIFICATION_BINDING_MISMATCH' } else { 'ELIGIBLE' }
    if (-not $eligible -and $reason -eq 'ELIGIBLE') { $reason = 'AUTHORITY_KERNEL_BLOCKED' }
    [pscustomobject][ordered]@{ cycleId = $CycleId; eligible = $eligible; reasonCode = $reason; authorityKernel = $authority }
}

Export-ModuleMember -Function ConvertTo-ESABCDCanonical,Get-ESABCDHash,New-ESABCDOrchestrationStore,Get-ESABCDSnapshot,Save-ESABCDOrchestrationSnapshot,Restore-ESABCDOrchestrationSnapshot,Test-ESABCDEventStoreIntegrity,New-ESABCDIdempotencyKey,Start-ESABCDIterationRound,Add-ESABCDCandidate,Prune-ESABCDCandidate,Backtrack-ESABCDCandidate,Add-ESABCDAuditRecord,Select-ESABCDDecision,Start-ESABCDCorrectionCycle,Add-ESABCDVerificationReceipt,Advance-ESABCDIterationRound,Stop-ESABCDIteration,Test-ESABCDCompletionEligibility,Add-ESABCDEvent
