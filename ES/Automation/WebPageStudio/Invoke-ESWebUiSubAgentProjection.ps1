[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AggregatePath,
    [string]$ParentTaskId = 'web-ui-parent',
    [string]$FocusContextId = 'web-ui-focus',
    [int]$FocusRevision = 1,
    [string]$FocusProposalHash = ('0' * 64),
    [string]$FocusScopeHash = ('0' * 64),
    [string]$FocusReceiptPath = '',
    [string]$ExpectedFocusReceiptHash = '',
    [int]$TaskRevision = 1,
    [int]$ContextVersion = 1,
    [ValidateRange(1,8)][int]$ConcurrencyBudget = 4
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')).TrimEnd('\') + '\';$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
foreach($inputPath in @(@('AggregatePath',$AggregatePath),@('FocusReceiptPath',$FocusReceiptPath))){if(-not [string]::IsNullOrWhiteSpace([string]$inputPath[1])){$raw=[string]$inputPath[1];$candidate=if([IO.Path]::IsPathRooted($raw)){[IO.Path]::GetFullPath($raw)}else{[IO.Path]::GetFullPath((Join-Path (Get-Location) $raw))};if(-not ($candidate.StartsWith($projectRoot,[StringComparison]::OrdinalIgnoreCase) -or $candidate.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase))){throw "$($inputPath[0])_OUTSIDE_ALLOWED_SNAPSHOT_ROOT"}}}
$modulePath = Join-Path $PSScriptRoot '..\TaskCollaboration\ESTaskCollaborationContracts.psm1'
Import-Module $modulePath -Force
$aggregate = Get-Content -Raw -Encoding UTF8 -LiteralPath $AggregatePath | ConvertFrom-Json
if ([string]$aggregate.recordType -cne 'WebPageStudioUiEvidenceAggregate') { throw 'WEB_UI_AGGREGATE_REQUIRED' }
if ($FocusProposalHash -notmatch '^[a-f0-9]{64}$' -or $FocusScopeHash -notmatch '^[a-f0-9]{64}$') { throw 'FOCUS_HASH_INVALID' }
$focusVerification = [ordered]@{ status = 'not-verified'; path = $null; fileSha256 = $null; receiptHash = $null; findings = @('FOCUS_RECEIPT_NOT_PROVIDED') }
if ($FocusReceiptPath) {
    if (-not (Test-Path -LiteralPath $FocusReceiptPath -PathType Leaf)) { throw 'FOCUS_RECEIPT_MISSING' }
    $focusRaw = Get-Content -Raw -Encoding UTF8 -LiteralPath $FocusReceiptPath
    $focus = $focusRaw | ConvertFrom-Json
    $focusFileHash = (Get-FileHash -LiteralPath $FocusReceiptPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($ExpectedFocusReceiptHash -and $ExpectedFocusReceiptHash.ToLowerInvariant() -cne $focusFileHash) { throw 'FOCUS_RECEIPT_HASH_DRIFT' }
    $focusVerification = [ordered]@{ status = 'verified'; path = $FocusReceiptPath; fileSha256 = $focusFileHash; receiptHash = if($focus.PSObject.Properties['receiptHash']){[string]$focus.receiptHash}else{$null}; findings = @() }
    if ($focus.PSObject.Properties['receiptHash']) {
        $focusInput = [ordered]@{}; foreach ($property in $focus.PSObject.Properties) { if ($property.Name -ne 'receiptHash') { $focusInput[$property.Name] = $property.Value } }
        if ([string]$focus.receiptHash -notmatch '^[a-f0-9]{64}$' -or (Get-ESCollaborationHash $focusInput) -cne [string]$focus.receiptHash) { $focusVerification.status = 'blocked'; $focusVerification.findings += 'FOCUS_RECEIPT_HASH_INVALID' }
    }
    foreach ($pair in @(@('focusContextId',$FocusContextId),@('focusRevision',$FocusRevision),@('focusProposalHash',$FocusProposalHash),@('focusScopeHash',$FocusScopeHash))) {
        if ($focus.PSObject.Properties[$pair[0]] -and [string]$focus.($pair[0]) -cne [string]$pair[1]) { $focusVerification.status = 'blocked'; $focusVerification.findings += 'FOCUS_IDENTITY_MISMATCH' }
    }
}
$children = @($aggregate.layers | ForEach-Object { 'web-ui.' + [string]$_.layer })
$zero = '0' * 64
$plan = New-ESCollaborationPlan -ParentTaskId $ParentTaskId -GoalRevisionHash $zero -RoutePlanHash $zero -ChildTaskIds $children -ConcurrencyBudget $ConcurrencyBudget -AggregationStrategy 'all-required'
$registry = New-ESChildTaskRegistry -ParentTaskId $ParentTaskId -ParentTaskRevision $TaskRevision -CollaborationPlan $plan
$captured = [DateTime]::Parse('2026-01-01T00:00:00Z').ToUniversalTime()
$envelopes = @()
$evidenceIdentities = @()
foreach ($layer in @($aggregate.layers)) {
    $childId = 'web-ui.' + [string]$layer.layer
    # A projection must not promote runtime-not-run/review/stale evidence to a
    # candidate. ResultEnvelope v1 has no not-run status, so preserve the
    # conservative meaning as a terminal failed envelope with a stable code.
    $layerStatus = [string]$layer.status
    $layerRuntimeStatus = if ($layer.PSObject.Properties['runtimeStatus']) { [string]$layer.runtimeStatus } else { $layerStatus }
    $status = if ($layerRuntimeStatus -eq 'runtime-passed' -and $layerStatus -in @('passed','accepted','runtime-passed')) { 'candidate' } else { 'failed' }
    $error = if ($status -eq 'failed') {
        switch ([string]$layer.status) {
            'blocked' { 'WEB_UI_LAYER_BLOCKED' }
            'failed' { 'WEB_UI_LAYER_FAILED' }
            'stale' { 'WEB_UI_LAYER_STALE' }
            'review' { 'WEB_UI_LAYER_REVIEW_REQUIRED' }
            default { 'WEB_UI_RUNTIME_NOT_RUN' }
        }
    } else { $null }
    $outputHash = if ([string]$layer.receiptHash -match '^[a-f0-9]{64}$') { [string]$layer.receiptHash } else { Get-ESCollaborationHash ([ordered]@{ layer = [string]$layer.layer; status = $layerStatus; runtimeStatus = $layerRuntimeStatus }) }
    $lease = New-ESLeaseClaim -TaskId $childId -WorkerId ('web-ui-projection.' + [string]$layer.layer) -ExpectedTaskRevision $TaskRevision -ExpectedContextVersion $ContextVersion -IssuedUtc $captured
    $refs = [string[]]@()
    if ($layer.receiptPath -and [string]$layer.receiptHash -match '^[a-f0-9]{64}$') {
        $refs = [string[]]@(([string]$layer.receiptPath) + '|sha256=' + [string]$layer.receiptHash)
    }
    $innerReceiptHash = $null
    if ($layer.receiptPath -and (Test-Path -LiteralPath ([string]$layer.receiptPath) -PathType Leaf)) { $innerReceiptHash = [string](Get-Content -Raw -Encoding UTF8 -LiteralPath ([string]$layer.receiptPath) | ConvertFrom-Json).receiptHash }
    $evidenceIdentities += [ordered]@{ layer = [string]$layer.layer; path = $layer.receiptPath; sha256 = $layer.receiptHash; receiptHash = $innerReceiptHash; receiptId = $layer.receiptId }
    $envelopes += New-ESResultEnvelope -ParentTaskId $ParentTaskId -ChildTaskId $childId -CollaborationPlanHash $plan.planHash -TaskRevision $TaskRevision -ContextVersion $ContextVersion -Attempt 1 -LeaseClaim $lease -ResultStatus $status -OutputHash $outputHash -EvidenceRefs $refs -ErrorCode $error -IdempotencyKey ('web-ui:' + [string]$layer.layer) -CapturedUtc $captured
}
$parent = Invoke-ESParentAggregation -CollaborationPlan $plan -ChildTaskRegistry $registry -ResultEnvelopes $envelopes
$verificationResults = @($aggregate.layers | ForEach-Object {
    [ordered]@{ layer = [string]$_.layer; validatorStatus = [string]$_.status; runtimeStatus = if ($_.PSObject.Properties['runtimeStatus']) { [string]$_.runtimeStatus } else { [string]$_.status }; receiptPath = if ($_.receiptPath) { [string]$_.receiptPath } else { $null }; receiptSha256 = if ([string]$_.receiptHash -match '^[a-f0-9]{64}$') { [string]$_.receiptHash } else { $null } }
})
$verificationHash = Get-ESCollaborationHash $verificationResults
[ordered]@{
    schemaVersion = 1; recordType = 'WebPageStudioSubAgentProjection'; parentTaskId = $ParentTaskId
    sourceAggregateId = [string]$aggregate.aggregateId; focusBinding = [ordered]@{ contextId = $FocusContextId; revision = $FocusRevision; proposalHash = $FocusProposalHash; scopeHash = $FocusScopeHash; taskRevision = $TaskRevision; contextVersion = $ContextVersion; verification = $focusVerification }
    executionPlan = [ordered]@{
        serialStages = @('static-preparation','evidence-aggregation')
        parallelStage = [ordered]@{ name = 'layer-evidence'; childTaskIds = $children; concurrencyBudget = $ConcurrencyBudget; cancellation = 'lease-cas'; aggregation = 'all-required' }
        verificationStage = [ordered]@{ name = 'layer-validation'; validatorKeys = @('network','preview','visual','release'); dependsOn = 'layer-evidence'; output = 'validated-receipt-identities'; resultsHash = $verificationHash; requiredBefore = 'evidence-aggregation' }
        dependencies = @([ordered]@{ stage = 'layer-evidence'; dependsOn = 'static-preparation' }, [ordered]@{ stage = 'layer-validation'; dependsOn = 'layer-evidence' }, [ordered]@{ stage = 'evidence-aggregation'; dependsOn = 'layer-validation' })
    }
    collaborationPlan = $plan; childTaskRegistry = $registry; resultEnvelopes = @($envelopes); verificationResults = $verificationResults; verificationHash = $verificationHash; aggregationInput = [ordered]@{ dependsOn = 'layer-validation'; consumesVerificationHash = $verificationHash; resultEnvelopeCount = @($envelopes).Count }; parentAggregation = $parent; evidenceIdentities = @($evidenceIdentities); evidenceReferences = @($evidenceIdentities | ForEach-Object { [ordered]@{ layer = $_.layer; path = $_.path; sha256 = $_.sha256; receiptHash = $_.receiptHash; receiptId = $_.receiptId } })
    nonClaims = @('projection-only; no worker dispatch','child results remain candidate/failed','does-not-declare-completion','runtimeStatus-remains-runtime-not-run')
} | ConvertTo-Json -Depth 20
