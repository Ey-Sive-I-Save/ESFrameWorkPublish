Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Capabilities = @(
    'bounded-tool-action', 'failure-recovery', 'branch-evaluation',
    'state-transition-guard', 'environment-trust-gate', 'audit-evidence-chain'
)
$script:InnovationStages = @('requirement-facts','player-outcomes','lexical-deanchor','seed-divergence','tree-expansion','global-convergence','interaction-graph','adaptive-weighting','player-replay','counterplay-audit','complexity-prune','candidate-tournament','final-decision')

$script:PatchPlanningPath = Join-Path $PSScriptRoot 'ESABCDPatchPlanning.psm1'

function Get-ESABCDCapabilityHash($Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $json = $Value | ConvertTo-Json -Compress -Depth 30; $bytes = [Text.Encoding]::UTF8.GetBytes($json); return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Get-ESABCDCapabilityCatalog { return @($script:Capabilities) }

function Test-ESABCDStateTransition {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CurrentStage,[Parameter(Mandatory)][string]$NextStage,[string[]]$AllowedStages=@($script:InnovationStages))
    $currentIndex=[array]::IndexOf($AllowedStages,$CurrentStage);$nextIndex=[array]::IndexOf($AllowedStages,$NextStage)
    $valid=($currentIndex -ge 0 -and $nextIndex -ge 0 -and ($nextIndex -eq $currentIndex -or $nextIndex -eq ($currentIndex+1)))
    [pscustomobject][ordered]@{status=if($valid){'passed'}else{'failed'};currentStage=$CurrentStage;nextStage=$NextStage;currentIndex=$currentIndex;nextIndex=$nextIndex;reason=if($valid){'transition-registered'}else{'transition-not-registered'}}
}

function New-ESABCDModelResponseInvoker {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Responses)
    $queue=@($Responses);if($queue.Count -eq 0){throw 'ABCD_MODEL_RESPONSE_QUEUE_EMPTY'}
    foreach($item in $queue){if(-not $item.PSObject.Properties['phase'] -or -not $item.PSObject.Properties['generationMode'] -or -not $item.PSObject.Properties['round']){throw 'ABCD_MODEL_RESPONSE_IDENTITY_FIELDS_REQUIRED'};if(-not ($item.PSObject.Properties['outputs'] -or $item.PSObject.Properties['output'])){throw 'ABCD_MODEL_RESPONSE_OUTPUT_REQUIRED'}}
    $used=[Collections.Generic.HashSet[int]]::new()
    $invoker={param($Context)
        $phase=[string]$Context.phase;$mode=[string]$Context.generationMode;$round=[int]$Context.round
        if([string]::IsNullOrWhiteSpace($phase)-or[string]::IsNullOrWhiteSpace($mode)){throw 'ABCD_MODEL_RESPONSE_REQUEST_IDENTITY_REQUIRED'}
        $index=-1
        for($i=0;$i -lt $queue.Count;$i++){if($used.Contains($i)){continue};$item=$queue[$i];if([string]$item.phase -ceq $phase -and [string]$item.generationMode -ceq $mode -and [int]$item.round -eq $round){$index=$i;break}}
        if($index -lt 0){throw "ABCD_MODEL_RESPONSE_NOT_FOUND:$phase/$mode/$round"};[void]$used.Add($index);$item=$queue[$index];if($item.PSObject.Properties['outputs']){return @($item.outputs)};return @($item.output)
    }.GetNewClosure()
    $invoker
}

function Invoke-ESABCDBoundedPatchCandidateAction {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Context)
    if (-not (Test-Path -LiteralPath $script:PatchPlanningPath -PathType Leaf)) { throw 'ABCD_PATCH_PLANNING_PROVIDER_MISSING' }
    foreach ($name in @('candidateEnvelope','candidate','scenario','currentHead','authorizationRef','sourceFiles','allowedWriteScopes')) {
        if (-not $Context.PSObject.Properties[$name]) { throw ('ABCD_PATCH_ACTION_INPUT_MISSING:' + $name) }
    }
    Import-Module $script:PatchPlanningPath -Force
    $plan = New-ESABCDCandidatePatchPlan -CandidateEnvelope $Context.candidateEnvelope -Scenario ([string]$Context.scenario) -CurrentHead ([string]$Context.currentHead) -AuthorizationRef ([string]$Context.authorizationRef) -SourceFiles @($Context.sourceFiles) -AllowedWriteScopes @($Context.allowedWriteScopes)
    $operations = Convert-ESABCDCandidateToPatchOperations -Candidate $Context.candidate -ProjectRoot ([string]$Context.projectRoot) -AllowedWriteScopes @($Context.allowedWriteScopes)
    $check = Test-ESABCDCandidatePatchPlan -Plan $plan
    if ([string]$check.status -cne 'passed') { throw ('ABCD_PATCH_ACTION_PLAN_INVALID:' + ($check.issues -join ',')) }
    [pscustomobject][ordered]@{ action='candidate-patch-plan'; status='candidate-only'; planHash=[string]$plan.planHash; operationCount=@($operations).Count; operations=@($operations | ForEach-Object { [pscustomobject][ordered]@{path=$_.path;beforeHash=$_.beforeHash;changeId=$_.changeId} }); effects=$plan.effects; requiresExplicitApply=[bool]$plan.requiresExplicitApply }
}

function New-ESABCDCandidateApprovalRequest {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$CandidateEnvelope,[Parameter(Mandatory)]$Candidate,[Parameter(Mandatory)]$FinalGate,[Parameter(Mandatory)][ValidateSet('DesignChange','RuntimeChange','DataMigration','ExternalSourceAdoption','PerformanceCritical','ReleaseCandidate')][string]$Scenario,[Parameter(Mandatory)][string]$CurrentHead,[Parameter(Mandatory)][string]$AuthorizationRef,[Parameter(Mandatory)][string[]]$SourceFiles,[Parameter(Mandatory)][string[]]$AllowedWriteScopes,[Parameter(Mandatory)][string]$ProjectRoot)
    Import-Module $script:PatchPlanningPath -Force
    $plan=New-ESABCDCandidatePatchPlan -CandidateEnvelope $CandidateEnvelope -Scenario $Scenario -CurrentHead $CurrentHead.ToLowerInvariant() -AuthorizationRef $AuthorizationRef -SourceFiles $SourceFiles -AllowedWriteScopes $AllowedWriteScopes
    $operations=Convert-ESABCDCandidateToPatchOperations -Candidate $Candidate -ProjectRoot $ProjectRoot -AllowedWriteScopes $AllowedWriteScopes
    $request=New-ESABCDApprovedApplyRequest -PatchPlan $plan -FinalGate $FinalGate -ObservedHead $CurrentHead.ToLowerInvariant() -ApplyAuthorizationRef $AuthorizationRef
    [pscustomobject][ordered]@{plan=$plan;operations=@($operations);request=$request}
}

function New-ESABCDCapabilityExecutionPlan {
    [CmdletBinding()]
    param(
        [string[]]$CapabilityRefs = @($script:Capabilities),
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][string]$Mode,
        [Parameter(Mandatory)][string]$SourceHash
    )
    $refs = @($CapabilityRefs | ForEach-Object { [string]$_ } | Select-Object -Unique)
    $unknown = @($refs | Where-Object { $_ -notin $script:Capabilities })
    if ($unknown.Count) { throw ('ABCD_CAPABILITY_UNKNOWN:' + ($unknown -join ',')) }
    $plan = [ordered]@{ schemaVersion = 1; contractId = 'es://automation/contracts/abcd/capability-execution-plan/v1'; planId = 'cap-plan-' + $RunId; runId = $RunId; mode = $Mode; sourceHash = $SourceHash; capabilityRefs = $refs; createdUtc = [DateTime]::UtcNow.ToString('o') }
    $plan.planHash = Get-ESABCDCapabilityHash $plan
    return [pscustomobject]$plan
}

function Invoke-ESABCDCapabilityProvider {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CapabilityId, [Parameter(Mandatory)]$Context)
    $status = 'executed'; $reason = 'provider-completed'; $observations = @()
    switch ($CapabilityId) {
        'bounded-tool-action' {
            if ([string]::IsNullOrWhiteSpace([string]$Context.scope) -or [string]::IsNullOrWhiteSpace([string]$Context.authorization)) { $status = 'review'; $reason = 'scope-or-authorization-missing' }
            else { $observations += 'bounded scope and authorization validated'; if ($Context.PSObject.Properties['ActionInvoker'] -and $null -ne $Context.ActionInvoker) { $actionResult = & $Context.ActionInvoker $Context; if ($null -ne $actionResult -and $actionResult.PSObject.Properties['status'] -and [string]$actionResult.status -cne 'candidate-only') { throw 'ABCD_BOUNDED_ACTION_EFFECT_ESCAPED' }; $observations += 'action-invoker-consumed' } elseif ($Context.PSObject.Properties['candidateEnvelope'] -and $Context.PSObject.Properties['candidate']) { $actionResult = Invoke-ESABCDBoundedPatchCandidateAction -Context $Context; $observations += ('candidate patch plan generated: ' + [string]$actionResult.planHash) } }
        }
        'failure-recovery' {
            if ($Context.PSObject.Properties['failureObserved'] -and $Context.failureObserved) {
                if (-not $Context.PSObject.Properties['retryBudget'] -or [int]$Context.retryBudget -lt 1) { $status = 'blocked'; $reason = 'retry-budget-missing' }
                else { $observations += 'failure observed; bounded recovery decision evaluated'; $observations += ('recovery action: ' + $(if ($Context.PSObject.Properties['recoveryAction']) { [string]$Context.recoveryAction } else { 'replan' })) }
            } else { $status = 'not-applicable'; $reason = 'no-observable-failure' }
        }
        'branch-evaluation' {
            $count = if ($Context.PSObject.Properties['branchCount']) { [int]$Context.branchCount } else { 0 }
            if ($count -lt 2) { $status = 'review'; $reason = 'branch-set-empty-or-singleton' } else { $observations += ('finite branch set evaluated: ' + $count) }
        }
        'state-transition-guard' {
            if ($Context.PSObject.Properties['currentStage'] -and $Context.PSObject.Properties['nextStage']) { $transition=Test-ESABCDStateTransition -CurrentStage ([string]$Context.currentStage) -NextStage ([string]$Context.nextStage) -AllowedStages $(if($Context.PSObject.Properties['allowedStages']){@($Context.allowedStages)}else{@($script:InnovationStages)}); if([string]$transition.status -cne 'passed'){$status='blocked';$reason='illegal-state-transition'}else{$observations += ('transition checked: ' + $Context.currentStage + ' -> ' + $Context.nextStage)} } else { $status = 'not-applicable'; $reason = 'no-lifecycle-transition-requested' }
        }
        'environment-trust-gate' {
            if ($Context.PSObject.Properties['environmentFingerprint'] -and -not [string]::IsNullOrWhiteSpace([string]$Context.environmentFingerprint)) { $observations += 'environment fingerprint present' } else { $status = 'degraded'; $reason = 'environment-fingerprint-unproven' }
        }
        'audit-evidence-chain' {
            if ([string]::IsNullOrWhiteSpace([string]$Context.sourceHash) -or [string]::IsNullOrWhiteSpace([string]$Context.stage)) { $status = 'review'; $reason = 'audit-input-missing' } else { $observations += 'source hash and stage evidence bound' }
        }
    }
    [pscustomobject][ordered]@{ capabilityId = $CapabilityId; status = $status; reason = $reason; observations = @($observations) }
}

function Test-ESABCDCapabilityReceipt {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Receipt, [Parameter(Mandatory)]$Plan)
    $required = @('receiptId','planHash','runId','capabilityId','status','inputHash','resultHash','capturedUtc')
    $missing = @($required | Where-Object { -not $Receipt.PSObject.Properties[$_] })
    $ok = ($missing.Count -eq 0 -and [string]$Receipt.planHash -ceq [string]$Plan.planHash -and [string]$Receipt.runId -ceq [string]$Plan.runId -and [string]$Receipt.capabilityId -in @($Plan.capabilityRefs))
    [pscustomobject][ordered]@{ status = if ($ok) { 'passed' } else { 'failed' }; missing = @($missing); reason = if ($ok) { 'receipt-valid' } else { 'capability-receipt-invalid' } }
}

function Save-ESABCDCapabilityRecoveryReceipt {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$CapabilityEvidence,[Parameter(Mandatory)][string]$Path)
    $full=[IO.Path]::GetFullPath($Path);$dir=[IO.Path]::GetDirectoryName($full)
    if([string]::IsNullOrWhiteSpace($dir)){throw 'ABCD_CAPABILITY_RECEIPT_PATH_INVALID'}
    if(-not(Test-Path -LiteralPath $dir -PathType Container)){New-Item -ItemType Directory -Force -Path $dir|Out-Null}
    $payload=[ordered]@{schemaVersion=1;format='es.abcd.capability-evidence.snapshot.v1';evidence=$CapabilityEvidence;evidenceHash=Get-ESABCDCapabilityHash $CapabilityEvidence;savedUtc=[DateTime]::UtcNow.ToString('o')}
    $tmp=$full+'.tmp-'+[Guid]::NewGuid().ToString('N');[IO.File]::WriteAllText($tmp,($payload|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false));Move-Item -LiteralPath $tmp -Destination $full -Force
    [pscustomobject][ordered]@{status='saved';path=$full;evidenceHash=$payload.evidenceHash}
}

function Restore-ESABCDCapabilityRecoveryReceipt {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path,[string]$ExpectedEvidenceHash='')
    $full=[IO.Path]::GetFullPath($Path);if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw 'ABCD_CAPABILITY_RECEIPT_SNAPSHOT_MISSING'}
    $payload=Get-Content -LiteralPath $full -Raw -Encoding UTF8|ConvertFrom-Json
    if([int]$payload.schemaVersion -ne 1 -or [string]$payload.format -cne 'es.abcd.capability-evidence.snapshot.v1'){throw 'ABCD_CAPABILITY_RECEIPT_SNAPSHOT_FORMAT_INVALID'}
    $actual=Get-ESABCDCapabilityHash $payload.evidence;if([string]$payload.evidenceHash -cne $actual){throw 'ABCD_CAPABILITY_RECEIPT_SNAPSHOT_HASH_MISMATCH'}
    if(-not [string]::IsNullOrWhiteSpace($ExpectedEvidenceHash) -and $ExpectedEvidenceHash -cne $actual){throw 'ABCD_CAPABILITY_RECEIPT_EXPECTED_HASH_MISMATCH'}
    [pscustomobject][ordered]@{status='restored';path=$full;evidence=$payload.evidence;evidenceHash=$actual;savedUtc=[string]$payload.savedUtc}
}

function Invoke-ESABCDCapabilityPlan {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Plan, [Parameter(Mandatory)]$Context)
    $receipts = @()
    foreach ($capabilityId in @($Plan.capabilityRefs)) {
        $input = [ordered]@{ capabilityId = $capabilityId; planHash = $Plan.planHash; context = $Context }
        $result = Invoke-ESABCDCapabilityProvider -CapabilityId $capabilityId -Context $Context
        $receipt = [ordered]@{ schemaVersion = 1; contractId = 'es://automation/contracts/abcd/capability-receipt/v1'; receiptId = 'cap-receipt-' + (Get-ESABCDCapabilityHash $input).Substring(0, 24); planHash = $Plan.planHash; runId = $Plan.runId; capabilityId = $capabilityId; status = $result.status; reason = $result.reason; observations = @($result.observations); inputHash = Get-ESABCDCapabilityHash $input; resultHash = Get-ESABCDCapabilityHash $result; capturedUtc = [DateTime]::UtcNow.ToString('o'); runtimeStatus = 'runtime-not-run'; nonClaims = @('no-Unity-runtime-claim','no-release-claim') }
        $check = Test-ESABCDCapabilityReceipt -Receipt ([pscustomobject]$receipt) -Plan $Plan
        if ($check.status -ne 'passed') { throw ('ABCD_CAPABILITY_RECEIPT_INVALID:' + $capabilityId) }
        $receipts += [pscustomobject]$receipt
    }
    [pscustomobject][ordered]@{ status = 'executed'; planHash = $Plan.planHash; runId = $Plan.runId; receipts = @($receipts); executedCapabilities = @($receipts | ForEach-Object capabilityId); receiptCount = $receipts.Count }
}

Export-ModuleMember -Function Get-ESABCDCapabilityCatalog,Test-ESABCDStateTransition,New-ESABCDModelResponseInvoker,New-ESABCDCapabilityExecutionPlan,Invoke-ESABCDCapabilityPlan,Test-ESABCDCapabilityReceipt,Invoke-ESABCDBoundedPatchCandidateAction,New-ESABCDCandidateApprovalRequest,Save-ESABCDCapabilityRecoveryReceipt,Restore-ESABCDCapabilityRecoveryReceipt
