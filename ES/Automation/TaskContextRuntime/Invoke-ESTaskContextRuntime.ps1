[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Create','Get','VerifySources','SubmitEvidence','Evaluate','Complete','SetDelivery','Transition','Integrity')]
    [string]$Action,
    [Parameter(Mandatory=$true)][string]$InputPath,
    [string]$ProjectRoot='.'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($InputPath)-or$InputPath-match '(^|[\/])\.\.([\/]|$)'){throw 'InputPath must be project-relative.'}
$inputFull=[IO.Path]::GetFullPath((Join-Path $root $InputPath))
if(-not$inputFull.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not(Test-Path -LiteralPath $inputFull -PathType Leaf)){throw 'InputPath is missing or escapes ProjectRoot.'}
$inputRelative=$inputFull.Substring($root.Length).TrimStart('\','/')
$current=$root
foreach($segment in $inputRelative.Split(@([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar),[StringSplitOptions]::RemoveEmptyEntries)){
    $current=Join-Path $current $segment
    $item=Get-Item -LiteralPath $current -Force
    if($item.LinkType-or($item.Attributes-band[IO.FileAttributes]::ReparsePoint)-ne0){throw 'InputPath cannot traverse a reparse point.'}
}
$input=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($inputFull))|ConvertFrom-Json -ErrorAction Stop
Import-Module (Join-Path $PSScriptRoot 'ESTaskContextRuntime.psm1') -Force
$focusSchemaModule=Join-Path $PSScriptRoot '..\Contracts\ESJsonSchemaLite.psm1'
Import-Module $focusSchemaModule -Force
$common=@{ProjectRoot=$root;StoreRoot=if([string]::IsNullOrWhiteSpace([string]$input.storeRoot)){'ES/Output/TaskContextRuntime'}else{[string]$input.storeRoot};TaskId=[string]$input.taskId}
$optionalClaims=if($null-eq$input.PSObject.Properties['optionalClaims']){@()}else{@($input.optionalClaims)}
$optionalClaimVerifiers=if($null-eq$input.PSObject.Properties['optionalClaimVerifiers']){$null}else{$input.optionalClaimVerifiers}
$requiredClaimVerifiers=@{}
if($null -ne $input.PSObject.Properties['requiredClaimVerifiers'] -and $null -ne $input.requiredClaimVerifiers){
    foreach($property in $input.requiredClaimVerifiers.PSObject.Properties){$requiredClaimVerifiers[[string]$property.Name]=[string]$property.Value}
}
$focusContext=if($null -eq $input.PSObject.Properties['focusContext']){$null}else{$input.focusContext}
$focusRuntimeRequest=$null
if($Action -eq 'Create' -and $null -ne $focusContext){
    $focusSchemaPath=Join-Path $PSScriptRoot '..\Contracts\es-task-focus-context-v1.schema.json'
    $focusSchemaErrors=@(Test-ESJsonSchemaValue -SchemaPath $focusSchemaPath -Value $focusContext)
    if($focusSchemaErrors.Count -gt 0){throw ('focusContext schema validation failed: ' + ($focusSchemaErrors -join '; '))}
    Import-Module (Join-Path $PSScriptRoot '..\TaskFocusContext\ESTaskFocusRuntimeAdapter.psm1') -Force
    foreach($scope in @($input.requestedSourceScope)){
        $mappedFocusRequest=New-ESTaskContextRuntimeRequestFromFocus -FocusContext $focusContext -TaskId ([string]$input.taskId) -RoutePlanPath ([string]$input.routePlanPath) -GoalRevisionPath ([string]$input.goalRevisionPath) -AcceptanceProfileId ([string]$input.acceptanceProfileId) -OutcomeEvaluatorId ([string]$input.outcomeEvaluatorId) -RequiredClaims @($input.requiredClaims) -RequiredClaimVerifiers $requiredClaimVerifiers -RequestedSourceScope ([string]$scope) -IdempotencyKey ([string]$input.idempotencyKey)
        if($null -eq $focusRuntimeRequest){$focusRuntimeRequest=$mappedFocusRequest}elseif($mappedFocusRequest.focusScopeHash -cne $focusRuntimeRequest.focusScopeHash){throw 'FocusContext identity changed across requested source scopes.'}
    }
}
$interactionSessionId=if($null-eq$input.PSObject.Properties['interactionSessionId']){$null}else{[string]$input.interactionSessionId}
switch($Action){
    'Create'{
        $createParams=@{ProjectRoot=$common.ProjectRoot;StoreRoot=$common.StoreRoot;TaskId=$common.TaskId;PlanHash=[string]$input.planHash;RoutePlanPath=[string]$input.routePlanPath;GoalRevisionPath=[string]$input.goalRevisionPath;AcceptanceProfileId=[string]$input.acceptanceProfileId;OutcomeEvaluatorId=[string]$input.outcomeEvaluatorId;RequiredClaim=@($input.requiredClaims);RequiredClaimVerifier=$requiredClaimVerifiers;MaxEvidenceAgeHours=[int]$input.maxEvidenceAgeHours;AllowUnverifiedClaims=[bool]$input.allowUnverifiedClaims;RequestedSourceScope=@($input.requestedSourceScope);IdempotencyKey=[string]$input.idempotencyKey}
        if($null -ne $input.PSObject.Properties['taskBindingPath'] -and -not [string]::IsNullOrWhiteSpace([string]$input.taskBindingPath)){$createParams.TaskBindingPath=[string]$input.taskBindingPath}
        if($null -ne $focusRuntimeRequest){$createParams.FocusContextId=$focusRuntimeRequest.focusContextId;$createParams.FocusRevision=[int]$focusRuntimeRequest.focusRevision;$createParams.FocusProposalHash=[string]$focusRuntimeRequest.focusProposalHash;$createParams.FocusReceiptHash=if($null -eq $focusRuntimeRequest.focusReceiptHash){$null}else{[string]$focusRuntimeRequest.focusReceiptHash};$createParams.FocusScopeHash=[string]$focusRuntimeRequest.focusScopeHash}
        if(@($optionalClaims).Count -gt 0){$createParams.OptionalClaim=@($optionalClaims);if($null -ne $optionalClaimVerifiers){$createParams.OptionalClaimVerifier=$optionalClaimVerifiers}}
        if(-not [string]::IsNullOrWhiteSpace($interactionSessionId)){$createParams.InteractionSessionId=$interactionSessionId}
        $result=New-ESTaskContextTask @createParams
    }
    'Get'{$result=Get-ESTaskContextState @common -VerifyIntegrity:([bool]$input.verifyIntegrity)}
    'VerifySources'{$result=Confirm-ESTaskSourceScope @common -ExpectedTaskRevision ([int]$input.expectedTaskRevision) -ExpectedContextVersion ([int]$input.expectedContextVersion) -IdempotencyKey ([string]$input.idempotencyKey)}
    'SubmitEvidence'{$result=Submit-ESTaskEvidenceSet @common -EvidenceSetPath ([string]$input.evidenceSetPath) -ExpectedTaskRevision ([int]$input.expectedTaskRevision) -ExpectedContextVersion ([int]$input.expectedContextVersion) -IdempotencyKey ([string]$input.idempotencyKey)}
    'Evaluate'{
        $required=@('schemaVersion','contractId','contractHash','recordType','storeRoot','taskId','expectedTaskRevision','expectedContextVersion','idempotencyKey')
        $actual=@($input.PSObject.Properties|ForEach-Object{[string]$_.Name})
        foreach($name in $required){if($actual-cnotcontains$name){throw "EvaluationRequest is missing required property: $name"}}
        foreach($name in $actual){if($required-cnotcontains$name){throw "EvaluationRequest contains an unsupported property: $name"}}
        if([int]$input.schemaVersion-ne1-or[string]$input.recordType-cne'EvaluationRequest'){throw 'EvaluationRequest identity is invalid.'}
        $result=New-ESTaskEvaluationRecord @common -ContractId ([string]$input.contractId) -ContractHash ([string]$input.contractHash) -ExpectedTaskRevision ([int]$input.expectedTaskRevision) -ExpectedContextVersion ([int]$input.expectedContextVersion) -IdempotencyKey ([string]$input.idempotencyKey)
    }
    'Complete'{$result=Complete-ESTaskContextTask @common -ExpectedTaskRevision ([int]$input.expectedTaskRevision) -ExpectedContextVersion ([int]$input.expectedContextVersion) -IdempotencyKey ([string]$input.idempotencyKey)}
    'SetDelivery'{$result=Set-ESTaskDeliveryAcceptance @common -DeliveryAcceptance ([string]$input.deliveryAcceptance) -ExpectedTaskRevision ([int]$input.expectedTaskRevision) -ExpectedContextVersion ([int]$input.expectedContextVersion) -IdempotencyKey ([string]$input.idempotencyKey)}
    'Transition'{$result=Invoke-ESTaskContextTransition @common -Transition ([string]$input.transition) -ExpectedTaskRevision ([int]$input.expectedTaskRevision) -ExpectedContextVersion ([int]$input.expectedContextVersion) -IdempotencyKey ([string]$input.idempotencyKey)}
    'Integrity'{$result=Test-ESTaskContextIntegrity @common}
}
$result|ConvertTo-Json -Depth 40
