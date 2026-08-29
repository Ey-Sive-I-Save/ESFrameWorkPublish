Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:HashPattern = '^[a-f0-9]{64}$'
$script:IdPattern = '^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$'

function ConvertTo-ESCollaborationCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) {
        return '{' + ((@($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object) | ForEach-Object {
            ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress), (ConvertTo-ESCollaborationCanonical $Value[$_]))
        }) -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object {
            ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress), (ConvertTo-ESCollaborationCanonical $_.Value))
        }) -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) {
        return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESCollaborationCanonical $_ }) -join ',') + ']'
    }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESCollaborationHash($Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESCollaborationCanonical $Value)))).Replace('-', '').ToLowerInvariant())
    } finally { $sha.Dispose() }
}

function Assert-ESCollaborationId([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:IdPattern) { throw "$Name is invalid." }
}

function Assert-ESCollaborationHash([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:HashPattern) { throw "$Name is invalid." }
}

function New-ESCollaborationPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ParentTaskId,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$GoalRevisionHash,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$RoutePlanHash,
        [Parameter(Mandatory)][string[]]$ChildTaskIds,
        [object[]]$Dependencies = @(),
        [ValidateRange(1, 256)][int]$ConcurrencyBudget = 1,
        [ValidateSet('all-required', 'quorum', 'ordered')][string]$AggregationStrategy = 'all-required'
    )
    Assert-ESCollaborationId $ParentTaskId 'ParentTaskId'
    if (@($ChildTaskIds).Count -lt 1) { throw 'ChildTaskIds must not be empty.' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($child in @($ChildTaskIds)) {
        Assert-ESCollaborationId ([string]$child) 'ChildTaskId'
        if (-not $seen.Add([string]$child)) { throw 'ChildTaskIds must be unique.' }
        if ([string]$child -ceq $ParentTaskId) { throw 'ParentTaskId cannot also be a child.' }
    }
    if ($ConcurrencyBudget -gt @($ChildTaskIds).Count) { throw 'ConcurrencyBudget cannot exceed child count.' }
    $normalizedDependencies = @($Dependencies | ForEach-Object {
        $child = [string]$_.childTaskId
        Assert-ESCollaborationId $child 'Dependency.childTaskId'
        if ($ChildTaskIds -notcontains $child) { throw 'Dependency references an unknown child.' }
        $depends = @($_.dependsOn | ForEach-Object { Assert-ESCollaborationId ([string]$_) 'Dependency.dependsOn'; [string]$_ } | Sort-Object -Unique)
        if ($depends -contains $child) { throw 'A child cannot depend on itself.' }
        foreach ($dependency in $depends) { if ($ChildTaskIds -notcontains $dependency) { throw 'Dependency references an unknown child.' } }
        [ordered]@{ childTaskId = $child; dependsOn = $depends }
    } | Sort-Object childTaskId)
    $base = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/task-collaboration/plan/v1'
        parentTaskId = $ParentTaskId
        goalRevisionHash = $GoalRevisionHash.ToLowerInvariant()
        routePlanHash = $RoutePlanHash.ToLowerInvariant()
        childTaskIds = @($ChildTaskIds)
        dependencies = $normalizedDependencies
        concurrencyBudget = $ConcurrencyBudget
        aggregationStrategy = $AggregationStrategy
    }
    $base.collaborationPlanId = 'cplan-' + (Get-ESCollaborationHash $base).Substring(0, 32)
    $base.planHash = Get-ESCollaborationHash $base
    [pscustomobject]$base
}

function New-ESChildTaskRegistry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ParentTaskId,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$ParentTaskRevision,
        [Parameter(Mandatory)]$CollaborationPlan,
        [ValidateRange(1, 2147483647)][int]$RegistryRevision = 1
    )
    Assert-ESCollaborationId $ParentTaskId 'ParentTaskId'
    Assert-ESCollaborationHash ([string]$CollaborationPlan.planHash) 'CollaborationPlan.planHash'
    if ([string]$CollaborationPlan.parentTaskId -cne $ParentTaskId) { throw 'ChildTaskRegistry parent mismatch.' }
    $children = @($CollaborationPlan.childTaskIds | ForEach-Object -Begin { $ordinal = 0 } -Process {
        $ordinal++
        [ordered]@{ childTaskId = [string]$_; ordinal = $ordinal }
    })
    $base = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/task-collaboration/child-registry/v1'
        registryId = 'ctr-' + (Get-ESCollaborationHash ([ordered]@{ parentTaskId = $ParentTaskId; planHash = $CollaborationPlan.planHash; revision = $RegistryRevision })).Substring(0, 32)
        parentTaskId = $ParentTaskId
        parentTaskRevision = $ParentTaskRevision
        collaborationPlanId = [string]$CollaborationPlan.collaborationPlanId
        collaborationPlanHash = [string]$CollaborationPlan.planHash
        registryRevision = $RegistryRevision
        children = $children
    }
    $base.registryHash = Get-ESCollaborationHash $base
    [pscustomobject]$base
}

function New-ESLeaseClaim {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TaskId,
        [Parameter(Mandatory)][string]$WorkerId,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$ExpectedTaskRevision,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$ExpectedContextVersion,
        [ValidateRange(1, 3600)][int]$LeaseDurationSeconds = 60,
        [DateTime]$IssuedUtc = [DateTime]::UtcNow
    )
    Assert-ESCollaborationId $TaskId 'TaskId'; Assert-ESCollaborationId $WorkerId 'WorkerId'
    $issued = $IssuedUtc.ToUniversalTime(); $expires = $issued.AddSeconds($LeaseDurationSeconds)
    $base = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/task-collaboration/lease-cas/v1'
        recordType = 'TaskLeaseClaim'
        leaseId = 'lease-' + (Get-ESCollaborationHash ([ordered]@{ taskId = $TaskId; workerId = $WorkerId; taskRevision = $ExpectedTaskRevision; contextVersion = $ExpectedContextVersion; issuedUtc = $issued.ToString('o') })).Substring(0, 32)
        taskId = $TaskId
        workerId = $WorkerId
        cas = [ordered]@{ expectedTaskRevision = $ExpectedTaskRevision; expectedContextVersion = $ExpectedContextVersion }
        issuedUtc = $issued.ToString('o')
        expiresUtc = $expires.ToString('o')
    }
    $base.leaseTokenHash = Get-ESCollaborationHash $base
    $base.claimHash = Get-ESCollaborationHash $base
    [pscustomobject]$base
}

function Test-ESLeaseCas {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$LeaseClaim,
        [Parameter(Mandatory)][int]$CurrentTaskRevision,
        [Parameter(Mandatory)][int]$CurrentContextVersion,
        [DateTime]$NowUtc = [DateTime]::UtcNow
    )
    $now = $NowUtc.ToUniversalTime()
    $status = 'claimed'; $reasonCode = 'LEASE_ACTIVE'; $canSubmit = $true
    if ($CurrentTaskRevision -ne [int]$LeaseClaim.cas.expectedTaskRevision -or $CurrentContextVersion -ne [int]$LeaseClaim.cas.expectedContextVersion) {
        $status = 'stale'; $reasonCode = 'CAS_STALE'; $canSubmit = $false
    } elseif ($now -ge [DateTime]::Parse([string]$LeaseClaim.expiresUtc).ToUniversalTime()) {
        $status = 'expired'; $reasonCode = 'LEASE_EXPIRED'; $canSubmit = $false
    }
    [pscustomobject][ordered]@{
        schemaVersion = 1; contractId = 'es://automation/contracts/task-collaboration/lease-cas/v1'; recordType = 'LeaseCasObservation'
        leaseId = [string]$LeaseClaim.leaseId; taskId = [string]$LeaseClaim.taskId; status = $status; reasonCode = $reasonCode
        expectedTaskRevision = [int]$LeaseClaim.cas.expectedTaskRevision; expectedContextVersion = [int]$LeaseClaim.cas.expectedContextVersion
        observedTaskRevision = $CurrentTaskRevision; observedContextVersion = $CurrentContextVersion; canSubmitResult = $canSubmit
    }
}

function New-ESResultEnvelope {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ParentTaskId,
        [Parameter(Mandatory)][string]$ChildTaskId,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$CollaborationPlanHash,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$TaskRevision,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$ContextVersion,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$Attempt,
        [Parameter(Mandatory)]$LeaseClaim,
        [Parameter(Mandatory)][ValidateSet('candidate', 'failed', 'cancelled')][string]$ResultStatus,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$OutputHash,
        [string[]]$EvidenceRefs = @(),
        [string]$ErrorCode,
        [string]$IdempotencyKey,
        [DateTime]$CapturedUtc = [DateTime]::UtcNow
    )
    Assert-ESCollaborationId $ParentTaskId 'ParentTaskId'; Assert-ESCollaborationId $ChildTaskId 'ChildTaskId'
    Assert-ESCollaborationHash $CollaborationPlanHash 'CollaborationPlanHash'
    if ([string]$LeaseClaim.taskId -cne $ChildTaskId) { throw 'ResultEnvelope Lease task mismatch.' }
    if ([int]$LeaseClaim.cas.expectedTaskRevision -ne $TaskRevision -or [int]$LeaseClaim.cas.expectedContextVersion -ne $ContextVersion) { throw 'ResultEnvelope CAS mismatch.' }
    $base = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/task-collaboration/result-envelope/v1'
        recordType = 'CandidateResultEnvelope'
        envelopeId = $null
        parentTaskId = $ParentTaskId
        childTaskId = $ChildTaskId
        collaborationPlanHash = $CollaborationPlanHash.ToLowerInvariant()
        taskRevision = $TaskRevision
        contextVersion = $ContextVersion
        attempt = $Attempt
        leaseId = [string]$LeaseClaim.leaseId
        resultStatus = $ResultStatus
        outputHash = $OutputHash.ToLowerInvariant()
        evidenceRefs = @($EvidenceRefs)
        errorCode = if ([string]::IsNullOrWhiteSpace($ErrorCode)) { $null } else { $ErrorCode }
        idempotencyKey = if ([string]::IsNullOrWhiteSpace($IdempotencyKey)) { $null } else { $IdempotencyKey }
        capturedUtc = $CapturedUtc.ToUniversalTime().ToString('o')
    }
    if ([string]::IsNullOrWhiteSpace($IdempotencyKey)) { $base.idempotencyKey = 'result-' + (Get-ESCollaborationHash $base).Substring(0, 32) }
    $base.envelopeId = 'result-' + (Get-ESCollaborationHash $base).Substring(0, 32)
    $base.resultHash = Get-ESCollaborationHash $base
    [pscustomobject]$base
}

function Invoke-ESParentAggregation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$CollaborationPlan,
        [Parameter(Mandatory)]$ChildTaskRegistry,
        [Parameter(Mandatory)][object[]]$ResultEnvelopes
    )
    Assert-ESCollaborationHash ([string]$CollaborationPlan.planHash) 'CollaborationPlan.planHash'
    Assert-ESCollaborationHash ([string]$ChildTaskRegistry.registryHash) 'ChildTaskRegistry.registryHash'
    if ([string]$ChildTaskRegistry.collaborationPlanHash -cne [string]$CollaborationPlan.planHash) { throw 'ParentAggregator plan hash mismatch.' }
    $observations = [Collections.Generic.List[object]]::new(); $conflicts = [Collections.Generic.List[string]]::new()
    foreach ($child in @($ChildTaskRegistry.children)) {
        $allMatches = @($ResultEnvelopes | Where-Object { [string]$_.parentTaskId -ceq [string]$CollaborationPlan.parentTaskId -and [string]$_.childTaskId -ceq [string]$child.childTaskId })
        $matches = @($allMatches | Where-Object { [string]$_.collaborationPlanHash -ceq [string]$CollaborationPlan.planHash })
        $planMismatched = @($allMatches | Where-Object { [string]$_.collaborationPlanHash -cne [string]$CollaborationPlan.planHash } | Select-Object -ExpandProperty resultHash -Unique)
        $byHash = @($matches | Group-Object resultHash)
        $terminalMatches = @($matches | Where-Object { [string]$_.resultStatus -in @('failed', 'cancelled') } | Sort-Object @{ Expression = { [int]$_.attempt }; Descending = $true }, resultHash)
        $maxAttempt = if ($matches.Count) { [int](@($matches | Measure-Object attempt -Maximum).Maximum) } else { 0 }
        $terminalAttempt = if ($terminalMatches.Count) { [int]$terminalMatches[0].attempt } else { 0 }
        $selectionAttempt = if ($terminalAttempt -gt 0) { $terminalAttempt } else { $maxAttempt }
        $latest = @($matches | Where-Object { [int]$_.attempt -eq [int]$selectionAttempt } | Sort-Object resultHash)
        $distinctLatest = @($latest | Select-Object -ExpandProperty resultHash -Unique)
        $quarantined = @($planMismatched + @($matches | Where-Object { [int]$_.attempt -ne [int]$selectionAttempt } | Select-Object -ExpandProperty resultHash -Unique) | Select-Object -Unique)
        $disposition = 'missing'; $selected = $null
        if ($distinctLatest.Count -gt 1) { $disposition = 'conflict'; [void]$conflicts.Add([string]$child.childTaskId) }
        elseif ($distinctLatest.Count -eq 1 -and $latest.Count -ge 1) {
            $selected = $latest[0]; $disposition = switch ([string]$selected.resultStatus) { 'candidate' { 'candidate' } 'failed' { 'failed' } 'cancelled' { 'cancelled' } default { 'invalid' } }
        }
        $observation = [pscustomobject][ordered]@{ childTaskId = [string]$child.childTaskId; ordinal = [int]$child.ordinal; attempt = [int]$selectionAttempt; disposition = $disposition; selectedResultHash = if ($selected) { [string]$selected.resultHash } else { $null }; evidenceRefs = $null; quarantinedResultHashes = @($quarantined); duplicateCount = [Math]::Max(0, $byHash.Count - $distinctLatest.Count) }
        if ($selected) { $observation.evidenceRefs = @($selected.evidenceRefs) } else { $observation.evidenceRefs = @() }
        $observation.duplicateCount = [Math]::Max(0, $latest.Count - $distinctLatest.Count)
        [void]$observations.Add($observation)
    }
    $dispositions = @($observations | ForEach-Object { [string]$_.disposition })
    $status = if ($conflicts.Count) { 'conflict' } elseif ($dispositions -contains 'invalid') { 'replan' } elseif ($dispositions -contains 'failed' -or $dispositions -contains 'cancelled' -or $dispositions -contains 'missing') { 'partial' } else { 'candidate' }
    $base = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/task-collaboration/parent-aggregation/v1'
        recordType = 'ParentAggregation'
        aggregationId = $null
        parentTaskId = [string]$CollaborationPlan.parentTaskId
        collaborationPlanId = [string]$CollaborationPlan.collaborationPlanId
        collaborationPlanHash = [string]$CollaborationPlan.planHash
        childRegistryHash = [string]$ChildTaskRegistry.registryHash
        aggregationStrategy = [string]$CollaborationPlan.aggregationStrategy
        status = $status
        children = @($observations)
        conflictChildTaskIds = @($conflicts | Sort-Object -Unique)
        completionDecisionRequired = $true
        nonClaims = @('ParentAggregation never declares Accepted or Completed.', 'Final completion remains owned by TaskContextRuntime completionDecision.')
    }
    $base.aggregationId = 'agg-' + (Get-ESCollaborationHash $base).Substring(0, 32)
    $base.aggregationHash = Get-ESCollaborationHash $base
    [pscustomobject]$base
}

Export-ModuleMember -Function ConvertTo-ESCollaborationCanonical,Get-ESCollaborationHash,New-ESCollaborationPlan,New-ESChildTaskRegistry,New-ESLeaseClaim,Test-ESLeaseCas,New-ESResultEnvelope,Invoke-ESParentAggregation
