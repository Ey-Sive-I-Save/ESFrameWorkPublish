Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$moduleRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'ESABCDOrchestrator.psm1') -Force

function Assert-Hash([string]$Value,[string]$Name){if([string]::IsNullOrWhiteSpace($Value)-or$Value-notmatch'^[a-f0-9]{64}$'){throw "$Name must be a lowercase SHA-256 hash."}}
function Assert-Id([string]$Value,[string]$Name){if([string]::IsNullOrWhiteSpace($Value)-or$Value-notmatch'^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$'){throw "$Name is invalid."}}

function Get-ESABCDLearningHashInput($Candidate){$x=[ordered]@{};foreach($p in $Candidate.PSObject.Properties){if($p.Name-ne'candidateHash'){$x[$p.Name]=$p.Value}};return $x}

function New-ESABCDLearningCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Store,[Parameter(Mandatory)][string]$CycleId,[Parameter(Mandatory)][string]$KnowledgeId,
        [Parameter(Mandatory)][string]$TargetPath,[Parameter(Mandatory)][string[]]$RouteKeys,
        [Parameter(Mandatory)]$SourceRefs,[Parameter(Mandatory)][string]$VerifierId,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$VerifierDefinitionHash
    )
    $eligibility=Test-ESABCDCompletionEligibility $Store $CycleId
    if(-not$eligibility.eligible){throw "LEARNING_REQUIRES_VERIFIED_CYCLE:$($eligibility.reasonCode)"}
    $cycle=$Store.cycles[$CycleId];$verification=$Store.verifications[$CycleId]
    Assert-Id $KnowledgeId 'KnowledgeId'; if([string]::IsNullOrWhiteSpace($TargetPath)-or$TargetPath-match'(^|[/\\])\.\.([/\\]|$)'-or[IO.Path]::IsPathRooted($TargetPath)){throw 'TargetPath must be project-relative and contained.'}
    if(@($RouteKeys).Count -lt 1){throw 'RouteKeys must be non-empty.'};foreach($r in @($RouteKeys)){if($r-notmatch'^[a-z0-9][a-z0-9._-]{0,80}$'){throw "RouteKey is invalid: $r"}}
    if(@($SourceRefs).Count -lt 1){throw 'SourceRefs must be non-empty.'}
    foreach($s in @($SourceRefs)){if($null-eq$s.path-or$null-eq$s.sha256-or$null-eq$s.role){throw 'SourceRef requires path, sha256 and role.'};$sourcePath=[string]$s.path;if([IO.Path]::IsPathRooted($sourcePath)-or$sourcePath-match'(^|[/\\])\.\.([/\\]|$)'-or$sourcePath-match'[*?]'){throw 'SourceRef.path must be project-relative and contained.'};if([string]$s.role -notin @('finding','verification','contract','required-read','evidence')){throw 'SourceRef.role is invalid.'};Assert-Hash ([string]$s.sha256) 'SourceRef.sha256'}
    Assert-Id $VerifierId 'VerifierId';Assert-Hash $VerifierDefinitionHash 'VerifierDefinitionHash'
    $seed=[ordered]@{taskId=[string]$Store.taskId;cycleId=$CycleId;bindingHash=[string]$Store.taskBindingRef.bindingHash;verificationReceiptRef=[string]$verification.verificationReceiptRef;knowledgeId=$KnowledgeId;targetPath=$TargetPath;routeKeys=@($RouteKeys)}
    $candidateId='abcd-lc-'+(Get-ESABCDHash $seed).Substring(0,32)
    $candidate=[ordered]@{
        schemaVersion=1;contractId='es://automation/contracts/abcd/learning-candidate/v1';recordType='ABCDLearningCandidate';candidateId=$candidateId;status='candidate';taskId=[string]$Store.taskId;cycleId=$CycleId;attemptNo=[int]$cycle.attemptNo
        taskBindingRef=[pscustomobject][ordered]@{bindingId=[string]$Store.taskBindingRef.bindingId;bindingHash=[string]$Store.taskBindingRef.bindingHash};routePlanHash=[string]$Store.routePlanHash;sourceScopeHash=[string]$Store.sourceScopeHash
        findingReceiptRef=[string]$cycle.findingReceiptRef;verificationReceiptRef=[string]$verification.verificationReceiptRef;failureClass=[string]$cycle.failureClass;decision=[string]$cycle.decision;sourceRefs=@($SourceRefs)
        knowledgeTarget=[pscustomobject][ordered]@{knowledgeId=$KnowledgeId;targetPath=$TargetPath;routeKeys=@($RouteKeys)}
        validation=[pscustomobject][ordered]@{state='pending';verifierId=$VerifierId;verifierDefinitionHash=$VerifierDefinitionHash.ToLowerInvariant();sourceStableAtEnd=$false;promotionAllowed=$false}
        candidateHash=$null;nonClaims=@('candidate-only','not-authoritative-knowledge','no-automatic-promotion','runtime-not-proven')
    }
    $result=[pscustomobject]$candidate
    $result.candidateHash=Get-ESABCDHash (Get-ESABCDLearningHashInput $result)
    return $result
}

function Test-ESABCDLearningCandidate {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Store,[Parameter(Mandatory)]$Candidate,[string]$ProjectRoot='.')
    if([string]$Candidate.contractId-ne'es://automation/contracts/abcd/learning-candidate/v1'){throw 'LEARNING_CONTRACT_MISMATCH'}
    Assert-Hash ([string]$Candidate.candidateHash) 'CandidateHash'
    if((Get-ESABCDHash (Get-ESABCDLearningHashInput $Candidate))-cne[string]$Candidate.candidateHash){throw 'LEARNING_CANDIDATE_HASH_MISMATCH'}
    $eligibility=Test-ESABCDCompletionEligibility $Store ([string]$Candidate.cycleId);if(-not$eligibility.eligible){throw "LEARNING_CYCLE_NO_LONGER_ELIGIBLE:$($eligibility.reasonCode)"}
    if([string]$Candidate.taskId-cne[string]$Store.taskId-or[string]$Candidate.routePlanHash-cne[string]$Store.routePlanHash-or[string]$Candidate.sourceScopeHash-cne[string]$Store.sourceScopeHash){throw 'LEARNING_SCOPE_MISMATCH'}
    if([string]$Candidate.taskBindingRef.bindingId-cne[string]$Store.taskBindingRef.bindingId-or[string]$Candidate.taskBindingRef.bindingHash-cne[string]$Store.taskBindingRef.bindingHash){throw 'LEARNING_BINDING_MISMATCH'}
    $root=(Resolve-Path -LiteralPath $ProjectRoot).Path;$sourceDrift=[Collections.Generic.List[string]]::new()
    foreach($s in @($Candidate.sourceRefs)){ $sourcePath=[string]$s.path;if([IO.Path]::IsPathRooted($sourcePath)-or$sourcePath-match'(^|[/\\])\.\.([/\\]|$)'-or$sourcePath-match'[*?]'-or[string]$s.role -notin @('finding','verification','contract','required-read','evidence')){[void]$sourceDrift.Add($sourcePath);continue};$full=[IO.Path]::GetFullPath((Join-Path $root $sourcePath));$prefix=$root.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar;if(-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){[void]$sourceDrift.Add($sourcePath);continue};if(-not(Test-Path -LiteralPath $full -PathType Leaf)){[void]$sourceDrift.Add($sourcePath);continue};$item=Get-Item -LiteralPath $full -Force;if($item.LinkType-or(($item.Attributes-band[IO.FileAttributes]::ReparsePoint)-ne 0)){[void]$sourceDrift.Add($sourcePath);continue};$actual=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant();if($actual-cne[string]$s.sha256){[void]$sourceDrift.Add($sourcePath)} }
    [pscustomobject][ordered]@{candidateId=[string]$Candidate.candidateId;status=if($sourceDrift.Count){'stale'}else{'validated'};sourceStableAtEnd=($sourceDrift.Count-eq0);promotionAllowed=$false;sourceDrift=@($sourceDrift);claimLevel='claim-cap';nonClaims=@($Candidate.nonClaims)}
}

Export-ModuleMember -Function New-ESABCDLearningCandidate,Test-ESABCDLearningCandidate,Get-ESABCDLearningHashInput
