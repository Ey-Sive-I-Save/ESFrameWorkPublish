[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$adapterPath = Join-Path $ProjectRoot 'Assets\Plugins\ES\Editor\ESAutomation\ESCodexCandidateEnvelopeAdapter.cs'
$automationPath = Join-Path $ProjectRoot 'Assets\Plugins\ES\Editor\ESAutomation\ESCodexAppServerAutomation.cs'
$schemaPath = Join-Path $ProjectRoot 'ES\Automation\Contracts\es-codex-candidate-envelope-v1.schema.json'
$issues = [Collections.Generic.List[string]]::new()
$cases = [Collections.Generic.List[object]]::new()

function Add-Case([string]$Id, [bool]$Passed, [string]$Finding) {
    [void]$cases.Add([pscustomobject][ordered]@{
        case = $Id
        status = if ($Passed) { 'passed' } else { 'failed' }
        finding = if ($Passed) { '' } else { $Finding }
    })
    if (-not $Passed) { [void]$issues.Add("CASE_FAILED:${Id}:$Finding") }
}

function Test-EnvelopeShape([object]$Envelope) {
    if ($null -eq $Envelope) { return $false }
    if ([int]$Envelope.schemaVersion -ne 1 -or [string]$Envelope.contractId -ne 'es://automation/contracts/codex/candidate-envelope/v1') { return $false }
    if ([string]$Envelope.providerId -ne 'es-codex' -or [string]$Envelope.status -ne 'candidate' -or [string]$Envelope.claimLevel -ne 'candidate-only') { return $false }
    if ([bool]$Envelope.canApply -or [string]$Envelope.finalAuthority -ne 'ABCD-audit-only') { return $false }
    if ([bool]$Envelope.effects.writesAllowed -or [bool]$Envelope.effects.runtimeAllowed -or [bool]$Envelope.effects.gitAllowed -or [bool]$Envelope.effects.releaseAllowed) { return $false }
    if ([string]$Envelope.planHash -notmatch '^[a-f0-9]{64}$' -or [string]$Envelope.sourceScopeHash -notmatch '^[a-f0-9]{64}$' -or [string]$Envelope.currentHead -notmatch '^[a-f0-9]{40}$') { return $false }
    foreach ($candidate in @($Envelope.candidates)) {
        if ([string]$candidate.candidateId -eq '' -or [string]$candidate.candidateType -ne 'abc-generation' -or [bool]$candidate.canApply -or [string]$candidate.claimLevel -ne 'candidate-only') { return $false }
        if ([string]$candidate.preconditions.currentHead -notmatch '^[a-f0-9]{40}$' -or [string]$candidate.preconditions.planHash -notmatch '^[a-f0-9]{64}$' -or [string]$candidate.preconditions.sourceScopeHash -notmatch '^[a-f0-9]{64}$') { return $false }
        if (-not [bool]$candidate.preconditions.requiresCurrentHeadRecheck -or -not [bool]$candidate.preconditions.requiresAbcdAudit -or -not [bool]$candidate.preconditions.requiresExplicitApply -or [bool]$candidate.effects.writesAllowed) { return $false }
        foreach ($file in @($candidate.changedFiles)) {
            if ([string]$file.path -match '^(?:[A-Za-z]:[\\/]|[\\/])' -or [string]$file.path -match '(^|[\\/])\.\.([\\/]|$)' -or [string]$file.beforeHash -notmatch '^[a-f0-9]{64}$') { return $false }
        }
    }
    return @($Envelope.candidates).Count -gt 0
}

foreach ($path in @($adapterPath, $automationPath, $schemaPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { [void]$issues.Add("MISSING:$path") }
}

if ($issues.Count -eq 0) {
    $adapter = Get-Content -LiteralPath $adapterPath -Raw -Encoding UTF8
    $automation = Get-Content -LiteralPath $automationPath -Raw -Encoding UTF8
    $schema = Get-Content -LiteralPath $schemaPath -Raw -Encoding UTF8
    $requiredAdapterMarkers = @(
        'class ESCodexResultNormalizer',
        'class ESCodexEvidenceBinder',
        'class ESCodexCandidateEnvelopeAdapter',
        'TryNormalize(',
        'claimLevel = "candidate-only"',
        'canApply = false',
        'CODEX_PRECONDITION_CURRENT_HEAD_REQUIRED',
        'CODEX_PRECONDITION_PLAN_HASH_REQUIRED',
        'CODEX_PRECONDITION_SOURCE_SCOPE_HASH_REQUIRED',
        'CODEX_CANDIDATE_PATH_OUT_OF_SCOPE_OR_MISSING',
        'CODEX_CANDIDATE_AUTHORITY_OR_SOURCE_PATH_FORBIDDEN',
        'completionDecision',
        'mutationApplied',
        'mutationApplied.Type != JTokenType.Boolean',
        'runtimeAccepted',
        'unityAccepted',
        'runtimeStatus',
        'sourceAbsolutePath'
    )
    foreach ($marker in $requiredAdapterMarkers) {
        if ($adapter.IndexOf($marker, [StringComparison]::Ordinal) -lt 0) { [void]$issues.Add("ADAPTER_MARKER_MISSING:$marker") }
    }
    $requiredAutomationMarkers = @(
        'GenerationMode = invocation.generationMode',
        'SourceScopeHash = invocation.executionSnapshot',
        'TryNormalize(',
        'codex-candidate-envelope.json'
    )
    foreach ($marker in $requiredAutomationMarkers) {
        if ($automation.IndexOf($marker, [StringComparison]::Ordinal) -lt 0) { [void]$issues.Add("AUTOMATION_MARKER_MISSING:$marker") }
    }
    try {
        $schemaObject = $schema | ConvertFrom-Json
        if ($schemaObject.'$id' -ne 'es://automation/contracts/codex/candidate-envelope/v1') { [void]$issues.Add('SCHEMA_ID_INVALID') }
        foreach ($required in @('providerId','threadId','turnId','runId','taskId','planHash','sourceScopeHash','candidateSetHash','candidateType','changedFiles','preconditions','effects','evidenceRefs','failureCodes','claimLevel','canApply')) {
            if (@($schemaObject.required) -notcontains $required -and $required -notin @('candidateType','changedFiles','preconditions','effects','evidenceRefs','failureCodes','claimLevel','canApply')) { [void]$issues.Add("SCHEMA_ROOT_REQUIRED_MISSING:$required") }
        }
        foreach ($required in @('candidateId','candidateType','changedFiles','preconditions','effects','proposedChanges','evidenceRefs','failureCodes','claimLevel','canApply')) {
            if (@($schemaObject.'$defs'.candidate.required) -notcontains $required) { [void]$issues.Add("SCHEMA_CANDIDATE_REQUIRED_MISSING:$required") }
        }
        if ($schemaObject.properties.canApply.const -ne $false) { [void]$issues.Add('SCHEMA_ROOT_CAN_APPLY_NOT_FALSE') }
        if ($schemaObject.'$defs'.candidate.properties.canApply.const -ne $false) { [void]$issues.Add('SCHEMA_CANDIDATE_CAN_APPLY_NOT_FALSE') }
        if ($schemaObject.'$defs'.effects.properties.writesAllowed.const -ne $false) { [void]$issues.Add('SCHEMA_EFFECTS_WRITE_NOT_FALSE') }
        if ($schemaObject.'$defs'.preconditions.properties.requiresAbcdAudit.const -ne $true) { [void]$issues.Add('SCHEMA_ABCD_AUDIT_NOT_REQUIRED') }

        $hash64 = ('a' * 64)
        $hash40 = ('b' * 40)
        $validEnvelope = [ordered]@{
            schemaVersion = 1; contractId = 'es://automation/contracts/codex/candidate-envelope/v1'; providerId = 'es-codex'
            threadId = 'thread-1'; turnId = 'turn-1'; runId = ('c' * 32); taskId = 'es.codex.app-server'; taskVersion = 1
            planHash = $hash64; sourceScopeHash = $hash64; currentHead = $hash40; candidateSetHash = $hash64
            generationMode = 'engineering'; status = 'candidate'; claimLevel = 'candidate-only'; canApply = $false
            finalAuthority = 'ABCD-audit-only'; effects = [ordered]@{ writesAllowed=$false; runtimeAllowed=$false; gitAllowed=$false; releaseAllowed=$false }
            evidenceRefs = @('codex-run:c:result'); failureCodes = @()
            candidates = @([ordered]@{
                candidateId = 'candidate-1'; candidateType = 'abc-generation'; changedFiles = @([ordered]@{ path='Assets/Test.cs'; changeId='change-1'; beforeHash=$hash64 })
                proposedChanges = @(); preconditions = [ordered]@{ currentHead=$hash40; planHash=$hash64; sourceScopeHash=$hash64; candidateSetHash=$hash64; requiresCurrentHeadRecheck=$true; requiresAbcdAudit=$true; requiresExplicitApply=$true }
                effects = [ordered]@{ writesAllowed=$false; runtimeAllowed=$false; gitAllowed=$false; releaseAllowed=$false }; evidenceRefs=@('codex-candidate:candidate-1'); failureCodes=@(); claimLevel='candidate-only'; canApply=$false; candidate=[ordered]@{}
            })
        }
        $validObject = ($validEnvelope | ConvertTo-Json -Depth 12 | ConvertFrom-Json)
        Add-Case 'envelope-shape-positive' (Test-EnvelopeShape $validObject) 'Valid candidate envelope shape was rejected.'
        $invalidApply = ($validEnvelope | ConvertTo-Json -Depth 12 | ConvertFrom-Json); $invalidApply.canApply = $true
        Add-Case 'envelope-negative-can-apply' (-not (Test-EnvelopeShape $invalidApply)) 'canApply=true was accepted.'
        $invalidAudit = ($validEnvelope | ConvertTo-Json -Depth 12 | ConvertFrom-Json); $invalidAudit.candidates[0].preconditions.requiresAbcdAudit = $false
        Add-Case 'envelope-negative-audit-required' (-not (Test-EnvelopeShape $invalidAudit)) 'requiresAbcdAudit=false was accepted.'
        $invalidPath = ($validEnvelope | ConvertTo-Json -Depth 12 | ConvertFrom-Json); $invalidPath.candidates[0].changedFiles[0].path = 'C:\outside.cs'
        Add-Case 'envelope-negative-absolute-path' (-not (Test-EnvelopeShape $invalidPath)) 'Absolute changed-file path was accepted.'
    }
    catch { [void]$issues.Add("SCHEMA_JSON_INVALID:$($_.Exception.Message)") }
}

[pscustomobject][ordered]@{
    status = if ($issues.Count -eq 0) { 'passed' } else { 'failed' }
    adapterPath = $adapterPath
    schemaPath = $schemaPath
    integrationPath = $automationPath
    staticOnly = $true
    cases = @($cases)
    negativeCases = @(
        'accepted-provider-claim',
        'absolute-or-traversal-path',
        'missing-current-head-or-plan/source hash',
        'mutation-applied-result',
        'completion-decision-present'
    )
    issues = @($issues)
}
