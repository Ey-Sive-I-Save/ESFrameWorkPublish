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
$common=@{ProjectRoot=$root;StoreRoot=if([string]::IsNullOrWhiteSpace([string]$input.storeRoot)){'ES/Output/TaskContextRuntime'}else{[string]$input.storeRoot};TaskId=[string]$input.taskId}
$optionalClaims=if($null-eq$input.PSObject.Properties['optionalClaims']){@()}else{@($input.optionalClaims)}
$optionalClaimVerifiers=if($null-eq$input.PSObject.Properties['optionalClaimVerifiers']){$null}else{$input.optionalClaimVerifiers}
$interactionSessionId=if($null-eq$input.PSObject.Properties['interactionSessionId']){$null}else{[string]$input.interactionSessionId}
switch($Action){
    'Create'{$result=New-ESTaskContextTask @common -PlanHash ([string]$input.planHash) -RoutePlanPath ([string]$input.routePlanPath) -GoalRevisionPath ([string]$input.goalRevisionPath) -AcceptanceProfileId ([string]$input.acceptanceProfileId) -OutcomeEvaluatorId ([string]$input.outcomeEvaluatorId) -RequiredClaim @($input.requiredClaims) -RequiredClaimVerifier $input.requiredClaimVerifiers -OptionalClaim $optionalClaims -OptionalClaimVerifier $optionalClaimVerifiers -InteractionSessionId $interactionSessionId -MaxEvidenceAgeHours ([int]$input.maxEvidenceAgeHours) -AllowUnverifiedClaims:([bool]$input.allowUnverifiedClaims) -RequestedSourceScope @($input.requestedSourceScope) -IdempotencyKey ([string]$input.idempotencyKey)}
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
