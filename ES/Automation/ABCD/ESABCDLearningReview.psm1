Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-ESABCDLearningReviewCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) { return '{' + ((@($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object) | ForEach-Object { ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress), (ConvertTo-ESABCDLearningReviewCanonical $Value[$_])) }) -join ',') + '}' }
    if ($Value -is [pscustomobject]) { return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object { ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress), (ConvertTo-ESABCDLearningReviewCanonical $_.Value)) }) -join ',') + '}' }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) { return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESABCDLearningReviewCanonical $_ }) -join ',') + ']' }
    return ([string]$Value | ConvertTo-Json -Compress)
}
function Get-ESABCDLearningReviewHash($Value) { $sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESABCDLearningReviewCanonical $Value)))).Replace('-','').ToLowerInvariant())}finally{$sha.Dispose()} }
function Get-ESABCDLearningReviewHashInput($Review) { $x=[ordered]@{};foreach($p in $Review.PSObject.Properties){if($p.Name -ne 'reviewHash'){$x[$p.Name]=$p.Value}};return $x }
function Assert-ReviewHash([string]$Value,[string]$Name){if($Value -notmatch '^[a-f0-9]{64}$'){throw "$Name must be a lowercase SHA-256 hash."}}

function New-ESABCDLearningReview {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Candidate,[Parameter(Mandatory)]$ValidationResult,[Parameter(Mandatory)][string]$ReviewerId,[Parameter(Mandatory)][ValidateSet('task-acceptance-owner','knowledge-reviewer','independent-auditor')][string]$ReviewerRole,[Parameter(Mandatory)][string]$AuthorizationProof)
    if ([string]$Candidate.status -notin @('candidate','review','validated')) { throw 'LEARNING_CANDIDATE_STATUS_INVALID' }
    Assert-ReviewHash ([string]$Candidate.candidateHash) 'CandidateHash'; if ([string]$ValidationResult.status -ne 'validated' -or $ValidationResult.sourceStableAtEnd -ne $true) { throw 'LEARNING_REVIEW_REQUIRES_VALIDATED_STABLE_CANDIDATE' }
    if ([string]$Candidate.validation.promotionAllowed -ne 'False' -and $Candidate.validation.promotionAllowed -ne $false) { throw 'LEARNING_PROMOTION_BOUNDARY_INVALID' }
    if ([string]::IsNullOrWhiteSpace($ReviewerId) -or [string]::IsNullOrWhiteSpace($AuthorizationProof)) { throw 'LEARNING_REVIEW_AUTHORIZATION_REQUIRED' }
    $validationHash=Get-ESABCDLearningReviewHash $ValidationResult
    $seed=[ordered]@{candidateId=[string]$Candidate.candidateId;candidateHash=[string]$Candidate.candidateHash;validationHash=$validationHash;reviewerId=$ReviewerId}
    $review=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/abcd/learning-review/v1';recordType='ABCDLearningReview';reviewId=('abcd-lr-'+(Get-ESABCDLearningReviewHash $seed).Substring(0,32));candidateRef=[ordered]@{candidateId=[string]$Candidate.candidateId;candidateHash=[string]$Candidate.candidateHash};sourceRefs=@($Candidate.sourceRefs);validation=[ordered]@{state='validated';verifierId=[string]$Candidate.validation.verifierId;verifierDefinitionHash=[string]$Candidate.validation.verifierDefinitionHash;sourceStableAtEnd=$true;validationHash=$validationHash};reviewer=[ordered]@{reviewerId=$ReviewerId;role=$ReviewerRole;authorizationProof=$AuthorizationProof};promotion=[ordered]@{decision='await-human-review';promotionAllowed=$false;requiresExplicitApply=$true};reviewHash=$null;nonClaims=@('candidate-only','no-automatic-knowledge-promotion','no-runtime-or-release-claim')}
    $result=[pscustomobject]$review;$result.reviewHash=Get-ESABCDLearningReviewHash (Get-ESABCDLearningReviewHashInput $result);return $result
}

function Test-ESABCDLearningReview {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Review,[Parameter(Mandatory)][string]$ProjectRoot)
    $issues=[Collections.Generic.List[string]]::new();try{Assert-ReviewHash ([string]$Review.reviewHash) 'ReviewHash';if((Get-ESABCDLearningReviewHash (Get-ESABCDLearningReviewHashInput $Review))-cne[string]$Review.reviewHash){[void]$issues.Add('REVIEW_HASH_MISMATCH')}}catch{[void]$issues.Add($_.Exception.Message)}
    if([string]$Review.promotion.promotionAllowed -ne 'False' -and $Review.promotion.promotionAllowed -ne $false){[void]$issues.Add('PROMOTION_MUST_REMAIN_DISABLED')};if($Review.promotion.requiresExplicitApply -ne $true){[void]$issues.Add('EXPLICIT_APPLY_REQUIRED')}
    $root=(Resolve-Path -LiteralPath $ProjectRoot).Path;$drift=[Collections.Generic.List[string]]::new();foreach($s in @($Review.sourceRefs)){$path=[string]$s.path;if([IO.Path]::IsPathRooted($path)-or$path-match'(^|[/\\])\.\.([/\\]|$)'){[void]$drift.Add($path);continue};$full=Join-Path $root $path;if(-not(Test-Path -LiteralPath $full -PathType Leaf)){[void]$drift.Add($path);continue};if((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()-cne[string]$s.sha256){[void]$drift.Add($path)}}
    if($drift.Count){[void]$issues.Add('SOURCE_DRIFT')}
    [pscustomobject][ordered]@{reviewId=[string]$Review.reviewId;status=if($issues.Count){'stale'}else{'review'};promotionAllowed=$false;sourceStableAtEnd=($drift.Count-eq0);issueCount=$issues.Count;issues=@($issues);sourceDrift=@($drift);nonClaims=@($Review.nonClaims)}
}
Export-ModuleMember -Function New-ESABCDLearningReview,Test-ESABCDLearningReview,Get-ESABCDLearningReviewHash,Get-ESABCDLearningReviewHashInput
