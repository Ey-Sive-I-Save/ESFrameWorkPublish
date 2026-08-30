Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ESABCDPatchHash($Value) {
    $json = $Value | ConvertTo-Json -Depth 30 -Compress
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json))).Replace('-','').ToLowerInvariant()) }
    finally { $sha.Dispose() }
}

function Get-ESABCDCanonicalPatchPlanHash($Plan) {
    $canonical=[ordered]@{}
    foreach($p in $Plan.PSObject.Properties){ if($p.Name -notin @('planHash','createdUtc')){$canonical[$p.Name]=$p.Value} }
    Get-ESABCDPatchHash ([pscustomobject]$canonical)
}

function New-ESABCDCandidatePatchPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$CandidateEnvelope,
        [Parameter(Mandatory)][ValidateSet('DesignChange','RuntimeChange','DataMigration','ExternalSourceAdoption','PerformanceCritical','ReleaseCandidate')][string]$Scenario,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$CurrentHead,
        [Parameter(Mandatory)][string]$AuthorizationRef,
        [string[]]$SourceFiles = @(),
        [string[]]$AllowedWriteScopes = @()
    )
    if ([string]::IsNullOrWhiteSpace($AuthorizationRef)) { throw 'PATCH_AUTHORIZATION_REQUIRED' }
    if ([string]$CandidateEnvelope.Status -cne 'candidate') { throw 'PATCH_CANDIDATE_STATUS_REQUIRED' }
    if ([string]::IsNullOrWhiteSpace([string]$CandidateEnvelope.CandidateSetHash)) { throw 'PATCH_CANDIDATE_HASH_REQUIRED' }
    $files = @()
    foreach ($file in @($SourceFiles | Sort-Object -Unique)) {
        if ([string]::IsNullOrWhiteSpace($file)) { continue }
        $resolved = if ([IO.Path]::IsPathRooted($file)) { [IO.Path]::GetFullPath($file) } else { [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $file)) }
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw "PATCH_SOURCE_NOT_FOUND:$file" }
        $files += [pscustomobject][ordered]@{ path=$resolved; beforeHash=(Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant(); changeState='candidate-only' }
    }
    $plan = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/abcd/candidate-patch-plan/v1'
        planStatus = 'awaiting-abcd-audit'
        candidateStatus = 'candidate'
        scenario = $Scenario
        currentHead = $CurrentHead.ToLowerInvariant()
        authorizationRef = $AuthorizationRef
        candidateSetHash = [string]$CandidateEnvelope.CandidateSetHash
        generationMode = [string]$CandidateEnvelope.GenerationMode
        sourceFiles = @($files)
        allowedWriteScopes = @($AllowedWriteScopes | Sort-Object -Unique)
        effects = [ordered]@{ writesAllowed=$false; runtimeAllowed=$false; gitAllowed=$false; releaseAllowed=$false }
        rollback = [ordered]@{ available=$true; action='discard-candidate-patch-plan'; sourceHashesPinned=$true }
        requiresExplicitApply = $true
        auditRequired = $true
        candidateCount = @($CandidateEnvelope.Candidates).Count
        createdUtc = [DateTime]::UtcNow.ToString('o')
    }
    $plan.planHash = Get-ESABCDCanonicalPatchPlanHash ([pscustomobject]$plan)
    [pscustomobject]$plan
}

function Convert-ESABCDCandidateToPatchOperations {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Candidate,
        [string]$ProjectRoot = (Get-Location).Path,
        [string[]]$AllowedWriteScopes = @()
    )
    $changes = @($Candidate.proposedChanges)
    if ($changes.Count -eq 0) { throw 'PATCH_PROPOSAL_CHANGES_EMPTY' }
    if ($changes.Count -gt 32) { throw 'PATCH_PROPOSAL_FILE_COUNT_EXCEEDED' }
    $operations = @()
    $seenPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($change in $changes) {
        $relative = [string]$change.path
        $segments = $relative.Replace('\','/').Split('/')
        if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or @($segments | Where-Object { $_ -eq '..' }).Count -gt 0) { throw 'PATCH_PROPOSAL_PATH_MUST_BE_PROJECT_RELATIVE' }
        $path = [IO.Path]::GetFullPath((Join-Path $ProjectRoot $relative))
        if (-not $seenPaths.Add($relative)) { throw "PATCH_PROPOSAL_DUPLICATE_PATH:$relative" }
        $allowed = @($AllowedWriteScopes | Where-Object {
            $scope = [IO.Path]::GetFullPath((Join-Path $ProjectRoot ([string]$_)))
            $path.Equals($scope, [StringComparison]::OrdinalIgnoreCase) -or $path.StartsWith($scope.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        if (-not $allowed) { throw "PATCH_PROPOSAL_PATH_OUT_OF_SCOPE:$relative" }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "PATCH_PROPOSAL_TARGET_NOT_FOUND:$relative" }
        $content = [string]$change.afterContent
        if ($content.Length -gt 2000000) { throw "PATCH_PROPOSAL_CONTENT_TOO_LARGE:$relative" }
        $operations += [pscustomobject][ordered]@{ path=$path; beforeHash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(); afterContent=$content; changeId=([string]$change.changeId) }
    }
    @($operations | Sort-Object path -Unique)
}

function Test-ESABCDCandidatePatchPlan {
    param([Parameter(Mandatory)]$Plan)
    $issues = [Collections.Generic.List[string]]::new()
    if ([string]$Plan.planStatus -cne 'awaiting-abcd-audit') { [void]$issues.Add('PATCH_PLAN_NOT_AWAITING_AUDIT') }
    if ([string]$Plan.candidateStatus -cne 'candidate') { [void]$issues.Add('PATCH_PLAN_CANDIDATE_NOT_CANDIDATE') }
    if (-not [bool]$Plan.requiresExplicitApply -or -not [bool]$Plan.auditRequired) { [void]$issues.Add('PATCH_PLAN_AUTHORITY_POLICY_INVALID') }
    if ([bool]$Plan.effects.writesAllowed -or [bool]$Plan.effects.runtimeAllowed -or [bool]$Plan.effects.gitAllowed) { [void]$issues.Add('PATCH_PLAN_EFFECTS_ESCAPED') }
    if (-not [bool]$Plan.rollback.available -or -not [bool]$Plan.rollback.sourceHashesPinned) { [void]$issues.Add('PATCH_PLAN_ROLLBACK_MISSING') }
    [pscustomobject][ordered]@{ status=if($issues.Count){'failed'}else{'passed'}; issues=@($issues); planHash=Get-ESABCDCanonicalPatchPlanHash $Plan }
}

function New-ESABCDApprovedApplyRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$PatchPlan,
        [Parameter(Mandatory)]$FinalGate,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$ObservedHead,
        [Parameter(Mandatory)][string]$ApplyAuthorizationRef
    )
    $check = Test-ESABCDCandidatePatchPlan -Plan $PatchPlan
    if ($check.status -ne 'passed') { throw 'PATCH_PLAN_INVALID_FOR_APPROVAL' }
    if ([string]$FinalGate.status -cne 'accepted' -or [string]$FinalGate.decisionStatus -cne 'Accepted') { throw 'ABCD_FINAL_GATE_NOT_ACCEPTED' }
    if ([string]$ObservedHead -cne [string]$PatchPlan.currentHead) { throw 'PATCH_HEAD_DRIFT' }
    if ([string]$FinalGate.planHash -cne [string]$PatchPlan.planHash) { throw 'PATCH_PLAN_HASH_MISMATCH' }
    if ([string]$FinalGate.candidateSetHash -and [string]$FinalGate.candidateSetHash -cne [string]$PatchPlan.candidateSetHash) { throw 'PATCH_CANDIDATE_HASH_MISMATCH' }
    if ([string]::IsNullOrWhiteSpace($ApplyAuthorizationRef)) { throw 'APPLY_AUTHORIZATION_REQUIRED' }
    $request = [ordered]@{
        schemaVersion=1; contractId='es://automation/contracts/abcd/approved-apply-request/v1'; requestStatus='awaiting-explicit-apply';
        patchPlanHash=[string]$PatchPlan.planHash; finalGateId=[string]$FinalGate.gateId; finalGateHash=Get-ESABCDPatchHash $FinalGate;
        currentHead=[string]$ObservedHead; applyAuthorizationRef=$ApplyAuthorizationRef; oneShot=$true;
        effects=[ordered]@{writesAllowed=$true; runtimeAllowed=$false; gitAllowed=$false; releaseAllowed=$false};
        rollback=[ordered]@{available=$true; action='discard-or-revert-patch'; sourceHashesPinned=$true}; createdUtc=[DateTime]::UtcNow.ToString('o')
    }
    $request.requestHash = Get-ESABCDPatchHash $request
    [pscustomobject]$request
}

function Test-ESABCDApprovedApplyRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$ApplyRequest,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$ObservedHead,
        [Parameter(Mandatory)][string]$ExpectedApplyAuthorizationRef,
        [switch]$AlreadyConsumed
    )
    $issues = [Collections.Generic.List[string]]::new()
    if ([string]$ApplyRequest.requestStatus -cne 'awaiting-explicit-apply') { [void]$issues.Add('APPLY_REQUEST_STATUS_INVALID') }
    if (-not [bool]$ApplyRequest.oneShot) { [void]$issues.Add('APPLY_REQUEST_NOT_ONE_SHOT') }
    if ($AlreadyConsumed) { [void]$issues.Add('APPLY_REQUEST_ALREADY_CONSUMED') }
    if ([string]$ApplyRequest.currentHead -cne $ObservedHead.ToLowerInvariant()) { [void]$issues.Add('APPLY_REQUEST_HEAD_DRIFT') }
    if ([string]$ApplyRequest.applyAuthorizationRef -cne $ExpectedApplyAuthorizationRef) { [void]$issues.Add('APPLY_REQUEST_AUTHORIZATION_MISMATCH') }
    if (-not [bool]$ApplyRequest.effects.writesAllowed -or [bool]$ApplyRequest.effects.runtimeAllowed -or [bool]$ApplyRequest.effects.gitAllowed -or [bool]$ApplyRequest.effects.releaseAllowed) { [void]$issues.Add('APPLY_REQUEST_EFFECT_PROJECTION_INVALID') }
    if (-not [bool]$ApplyRequest.rollback.available -or -not [bool]$ApplyRequest.rollback.sourceHashesPinned) { [void]$issues.Add('APPLY_REQUEST_ROLLBACK_INVALID') }
    [pscustomobject][ordered]@{ status=if($issues.Count){'failed'}else{'passed'}; issues=@($issues); requestHash=Get-ESABCDPatchHash $ApplyRequest }
}

function Invoke-ESABCDApprovedApplyRequest {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]$ApplyRequest,
        [Parameter(Mandatory)]$PatchPlan,
        [Parameter(Mandatory)]$Operations,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$ObservedHead,
        [Parameter(Mandatory)][string]$ApplyAuthorizationRef,
        [switch]$Apply
    )
    if (-not $Apply) { throw 'APPLY_SWITCH_REQUIRED' }
    $requestCheck = Test-ESABCDApprovedApplyRequest -ApplyRequest $ApplyRequest -ObservedHead $ObservedHead -ExpectedApplyAuthorizationRef $ApplyAuthorizationRef
    if ($requestCheck.status -ne 'passed') { throw ('APPLY_REQUEST_REJECTED:' + (@($requestCheck.issues) -join ',')) }
    if ([string]$ApplyRequest.patchPlanHash -cne [string]$PatchPlan.planHash) { throw 'PATCH_PLAN_HASH_MISMATCH' }
    if (@($Operations).Count -eq 0) { throw 'PATCH_OPERATIONS_EMPTY' }
    $results = @()
    foreach ($operation in @($Operations)) {
        $path = [IO.Path]::GetFullPath([string]$operation.path)
        $scopeAllowed = @($PatchPlan.allowedWriteScopes | Where-Object {
            $scope = [IO.Path]::GetFullPath([string]$_)
            $path.Equals($scope, [StringComparison]::OrdinalIgnoreCase) -or $path.StartsWith($scope.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        if (-not $scopeAllowed) { throw "APPLY_PATH_OUT_OF_SCOPE:$path" }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "APPLY_TARGET_NOT_FOUND:$path" }
        $before = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($before -cne [string]$operation.beforeHash) { throw "APPLY_BEFORE_HASH_MISMATCH:$path" }
        $content = [string]$operation.afterContent
        $backup = $path + '.abcd-backup-' + $ApplyRequest.requestHash.Substring(0, 12)
        if ($PSCmdlet.ShouldProcess($path, 'Apply approved ABCD patch')) {
            [IO.File]::Copy($path, $backup, $false)
            $temp = $path + '.abcd-tmp-' + [Guid]::NewGuid().ToString('N')
            [IO.File]::WriteAllText($temp, $content, [Text.UTF8Encoding]::new($false))
            [IO.File]::Replace($temp, $path, $null)
        }
        $after = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() } else { $before }
        $results += [pscustomobject][ordered]@{ path=$path; beforeHash=$before; afterHash=$after; rollbackPath=$backup; applied=$true }
    }
    [pscustomobject][ordered]@{ schemaVersion=1; receiptType='abcd-approved-apply-receipt'; requestHash=$ApplyRequest.requestHash; patchPlanHash=$PatchPlan.planHash; currentHead=$ObservedHead; status='applied'; operations=@($results); capturedUtc=[DateTime]::UtcNow.ToString('o'); receiptHash=(Get-ESABCDPatchHash $results) }
}

function Test-ESABCDApplyReceipt {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Receipt,
        [Parameter(Mandatory)]$ApplyRequest,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$ObservedHead
    )
    $issues = [Collections.Generic.List[string]]::new()
    if ([string]$Receipt.receiptType -cne 'abcd-approved-apply-receipt') { [void]$issues.Add('APPLY_RECEIPT_TYPE_INVALID') }
    if ([string]$Receipt.requestHash -cne [string]$ApplyRequest.requestHash) { [void]$issues.Add('APPLY_RECEIPT_REQUEST_MISMATCH') }
    if ([string]$Receipt.currentHead -cne $ObservedHead.ToLowerInvariant()) { [void]$issues.Add('APPLY_RECEIPT_HEAD_DRIFT') }
    foreach ($item in @($Receipt.operations)) {
        if (-not (Test-Path -LiteralPath ([string]$item.path) -PathType Leaf)) { [void]$issues.Add('APPLY_RECEIPT_TARGET_MISSING:' + [string]$item.path); continue }
        $current = (Get-FileHash -LiteralPath ([string]$item.path) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ([string]$current -cne [string]$item.afterHash) { [void]$issues.Add('APPLY_RECEIPT_AFTER_HASH_MISMATCH:' + [string]$item.path) }
        if (-not (Test-Path -LiteralPath ([string]$item.rollbackPath) -PathType Leaf)) { [void]$issues.Add('APPLY_RECEIPT_ROLLBACK_MISSING:' + [string]$item.path) }
    }
    [pscustomobject][ordered]@{ status=if($issues.Count){'failed'}else{'passed'}; issues=@($issues); receiptHash=Get-ESABCDPatchHash $Receipt }
}

function Convert-ESABCDApplyReceiptToVerificationInput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Receipt,
        [Parameter(Mandatory)]$ApplyRequest,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$ObservedHead
    )
    $check = Test-ESABCDApplyReceipt -Receipt $Receipt -ApplyRequest $ApplyRequest -ObservedHead $ObservedHead
    $status = if ($check.status -eq 'passed' -and [string]$Receipt.status -ceq 'applied') { 'passed' } else { 'review' }
    [pscustomobject][ordered]@{
        verificationStatus = $status
        verificationReceiptRef = 'abcd-apply-receipt:' + [string]$Receipt.receiptHash
        verificationReceiptHash = [string]$check.receiptHash
        independentVerifier = 'es-abcd-apply-receipt-verifier'
        failureCodes = @($check.issues)
        learningPromotionAllowed = ($status -eq 'passed')
        nonClaims = @('Unity-runtime', 'gameplay-quality')
    }
}

Export-ModuleMember -Function New-ESABCDCandidatePatchPlan,Convert-ESABCDCandidateToPatchOperations,Test-ESABCDCandidatePatchPlan,New-ESABCDApprovedApplyRequest,Test-ESABCDApprovedApplyRequest,Invoke-ESABCDApprovedApplyRequest,Test-ESABCDApplyReceipt,Convert-ESABCDApplyReceiptToVerificationInput,Get-ESABCDPatchHash
