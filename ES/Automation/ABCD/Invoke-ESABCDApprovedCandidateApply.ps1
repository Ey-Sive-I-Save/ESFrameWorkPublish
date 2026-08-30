[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$CandidateEnvelopePath,
    [Parameter(Mandatory)][string]$CandidateId,
    [Parameter(Mandatory)][string]$FinalGatePath,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$ObservedHead,
    [Parameter(Mandatory)][string]$ApplyAuthorizationRef,
    [Parameter(Mandatory)][ValidateSet('DesignChange','RuntimeChange','DataMigration','ExternalSourceAdoption','PerformanceCritical','ReleaseCandidate')][string]$Scenario,
    [string[]]$SourceFiles=@(),
    [string[]]$AllowedWriteScopes=@(),
    [string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [switch]$Apply
)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDCapabilityDispatcher.psm1') -Force
$envFull=[IO.Path]::GetFullPath($CandidateEnvelopePath);$gateFull=[IO.Path]::GetFullPath($FinalGatePath)
if(-not(Test-Path -LiteralPath $envFull -PathType Leaf)){throw 'ABCD_APPROVAL_CANDIDATE_ENVELOPE_MISSING'};if(-not(Test-Path -LiteralPath $gateFull -PathType Leaf)){throw 'ABCD_APPROVAL_FINAL_GATE_MISSING'}
$envelope=Get-Content -LiteralPath $envFull -Raw -Encoding UTF8|ConvertFrom-Json;$gate=Get-Content -LiteralPath $gateFull -Raw -Encoding UTF8|ConvertFrom-Json
$candidate=@($envelope.candidates|Where-Object{[string]$_.candidateId -ceq $CandidateId}|Select-Object -First 1);if($candidate.Count -ne 1){throw 'ABCD_APPROVAL_CANDIDATE_ID_NOT_FOUND_OR_AMBIGUOUS'}
if(@($AllowedWriteScopes).Count -eq 0){throw 'ABCD_APPROVAL_ALLOWED_WRITE_SCOPE_REQUIRED'};if(@($SourceFiles).Count -eq 0){$SourceFiles=@($candidate[0].proposedChanges|ForEach-Object{[string]$_.path}|Where-Object{$_})}
$ctx=[pscustomobject][ordered]@{scope=($AllowedWriteScopes -join ';');authorization=$ApplyAuthorizationRef;candidateEnvelope=$envelope;candidate=$candidate[0];scenario=$Scenario;currentHead=$ObservedHead.ToLowerInvariant();authorizationRef=$ApplyAuthorizationRef;sourceFiles=$SourceFiles;allowedWriteScopes=$AllowedWriteScopes;projectRoot=$root}
$planResult=Invoke-ESABCDBoundedPatchCandidateAction -Context $ctx;$approval=New-ESABCDCandidateApprovalRequest -CandidateEnvelope $envelope -Candidate $candidate[0] -FinalGate $gate -Scenario $Scenario -CurrentHead $ObservedHead.ToLowerInvariant() -AuthorizationRef $ApplyAuthorizationRef -SourceFiles $SourceFiles -AllowedWriteScopes $AllowedWriteScopes -ProjectRoot $root;$plan=$approval.plan;$operations=$approval.operations;$request=$approval.request
$result=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/abcd/approved-candidate-bridge/v1';candidateId=$CandidateId;planHash=$plan.planHash;request=$request;candidatePlanCheck=$planResult;status='awaiting-explicit-apply';nonClaims=@('not-applied','no-Git','no-Unity-runtime','no-release')}
if($Apply){$result.apply=Invoke-ESABCDApprovedApplyRequest -ApplyRequest $request -PatchPlan $plan -Operations $operations -ObservedHead $ObservedHead.ToLowerInvariant() -ApplyAuthorizationRef $ApplyAuthorizationRef -Apply; $result.status='applied';$result.nonClaims=@('no-Git','no-Unity-runtime','no-release')}
$result|ConvertTo-Json -Depth 50
