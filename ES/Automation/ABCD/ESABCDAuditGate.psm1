Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Profiles = [ordered]@{
    DesignChange = [ordered]@{ required = @('bounded-tool-action','branch-evaluation','audit-evidence-chain'); minimumEvidence = 'S1'; negative = @('invalid-input','authority-conflict','stale-source','forged-receipt') }
    RuntimeChange = [ordered]@{ required = @('state-transition-guard','environment-trust-gate','audit-evidence-chain'); minimumEvidence = 'S4'; negative = @('illegal-transition','environment-mismatch','runtime-failure','recovery') }
    DataMigration = [ordered]@{ required = @('state-transition-guard','audit-evidence-chain'); minimumEvidence = 'S2'; negative = @('schema-drift','replay-divergence','rollback') }
    ExternalSourceAdoption = [ordered]@{ required = @('environment-trust-gate','bounded-tool-action','audit-evidence-chain'); minimumEvidence = 'S1'; negative = @('license-missing','source-drift','untrusted-content','copy-boundary') }
    PerformanceCritical = [ordered]@{ required = @('branch-evaluation','environment-trust-gate','audit-evidence-chain'); minimumEvidence = 'S4'; negative = @('allocation-regression','budget-exhausted','timing-regression','runtime-not-run') }
    ReleaseCandidate = [ordered]@{ required = @('bounded-tool-action','state-transition-guard','environment-trust-gate','audit-evidence-chain'); minimumEvidence = 'S5'; negative = @('missing-artifact','stale-head','rollback','release-mismatch') }
}
function Get-ESABCDValidationProfile {
 [CmdletBinding()]param([Parameter(Mandatory)][ValidateSet('low','medium','high','critical')][string]$Risk)
 $map=[ordered]@{
  low=[ordered]@{mode='fast-path';maxReads=3;maxBranches=0;deepAudit=$false;required=@('input-boundary','schema-shape')}
  medium=[ordered]@{mode='bounded-path';maxReads=8;maxBranches=3;deepAudit=$false;required=@('input-boundary','deterministic-replay','evidence-contract')}
  high=[ordered]@{mode='deep-path';maxReads=20;maxBranches=5;deepAudit=$true;required=@('authority-routing','permission-boundary','rollback','evidence-contract','deterministic-replay')}
  critical=[ordered]@{mode='final-gate';maxReads=40;maxBranches=8;deepAudit=$true;required=@('authority-routing','permission-boundary','rollback','interruption-recovery','evidence-contract','deterministic-replay')}
 }
 [pscustomobject][ordered]@{risk=$Risk;profile=$map[$Risk];skipFullTraversal=($Risk -in @('low','medium'));requiresFinalGate=($Risk -in @('high','critical'))}
}

function ConvertTo-ESABCDAuditCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) { return '{' + ((@($Value.Keys | ForEach-Object {[string]$_} | Sort-Object) | ForEach-Object { ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress),(ConvertTo-ESABCDAuditCanonical $Value[$_])) }) -join ',') + '}' }
    if ($Value -is [pscustomobject]) { return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object { ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress),(ConvertTo-ESABCDAuditCanonical $_.Value)) }) -join ',') + '}' }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) { return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESABCDAuditCanonical $_ }) -join ',') + ']' }
    return ([string]$Value | ConvertTo-Json -Compress)
}
function Get-ESABCDAuditHash($Value) { $sha=[Security.Cryptography.SHA256]::Create(); try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESABCDAuditCanonical $Value)))).Replace('-','').ToLowerInvariant()) } finally {$sha.Dispose()} }

function New-ESABCDAuditPlan {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateSet('DesignChange','RuntimeChange','DataMigration','ExternalSourceAdoption','PerformanceCritical','ReleaseCandidate')][string]$Scenario,[Parameter(Mandatory)][string]$SubjectId,[Parameter(Mandatory)][string]$CurrentHead,[Parameter(Mandatory)][string]$AuthorizationRef,[string[]]$SourceRefs=@(),[string[]]$AllowedWriteScopes=@(),[string[]]$RequestedCapabilities=@())
    if ($CurrentHead -notmatch '^[a-f0-9]{40}$') { throw 'AUDIT_CURRENT_HEAD_INVALID' }
    if ([string]::IsNullOrWhiteSpace($SubjectId) -or [string]::IsNullOrWhiteSpace($AuthorizationRef)) { throw 'AUDIT_SUBJECT_AUTHORIZATION_REQUIRED' }
    $profile=$script:Profiles[$Scenario]
    $plan=[ordered]@{ schemaVersion=1; contractId='es://automation/contracts/abcd/audit-plan/v1'; planId=$null; scenario=$Scenario; subjectId=$SubjectId; currentHead=$CurrentHead.ToLowerInvariant(); authorizationRef=$AuthorizationRef; sourceRefs=@($SourceRefs|Sort-Object -Unique); allowedWriteScopes=@($AllowedWriteScopes|Sort-Object -Unique); requestedCapabilities=@($RequestedCapabilities|Sort-Object -Unique); requiredCapabilities=@($profile.required); requiredNegativeCases=@($profile.negative); minimumEvidenceLevel=[string]$profile.minimumEvidence; divergencePolicy=[ordered]@{ finite=$true; maxBranches=8; requireDistinctAssumptions=$true; requireRollbackPlan=$true; selection='deterministic-with-independent-audit' }; authorityPolicy=[ordered]@{ userAuthorizationRequired=$true; projectRulesReadOnly=$true; knowledgeNonAuthoritative=$true; sourceRefsMustBeCurrent=$true }; completionPolicy=[ordered]@{ acceptedOnlyIfAllRequiredEvidence=$true; staleEvidenceBlocks=$true; runtimeNotRunIsNonClaim=$true; independentAuditRequired=$true }; createdUtc=[DateTime]::UtcNow.ToString('o') }
    $plan.planId='audit-plan-'+(Get-ESABCDAuditHash $plan).Substring(0,24)
    [pscustomobject]$plan
}

function Test-ESABCDAuditPlan { param([Parameter(Mandatory)]$Plan)
    $issues=[Collections.Generic.List[string]]::new(); if ([int]$Plan.schemaVersion -ne 1){[void]$issues.Add('AUDIT_SCHEMA_VERSION_INVALID')}; if ([string]$Plan.currentHead -notmatch '^[a-f0-9]{40}$'){[void]$issues.Add('AUDIT_CURRENT_HEAD_INVALID')}; if (@($Plan.requiredCapabilities).Count -lt 1){[void]$issues.Add('AUDIT_REQUIRED_CAPABILITIES_MISSING')}; if (-not [bool]$Plan.divergencePolicy.finite -or [int]$Plan.divergencePolicy.maxBranches -lt 2){[void]$issues.Add('AUDIT_DIVERGENCE_POLICY_INVALID')}; if (-not [bool]$Plan.authorityPolicy.userAuthorizationRequired -or -not [bool]$Plan.authorityPolicy.knowledgeNonAuthoritative){[void]$issues.Add('AUDIT_AUTHORITY_POLICY_INVALID')}; if (-not [bool]$Plan.completionPolicy.independentAuditRequired){[void]$issues.Add('AUDIT_INDEPENDENT_AUDIT_REQUIRED')}; [pscustomobject][ordered]@{status=if($issues.Count){'failed'}else{'passed'};issues=@($issues);planHash=(Get-ESABCDAuditHash $Plan)}
}

function Test-ESABCDAuditSourceRegistry { param([Parameter(Mandatory)]$Registry)
    $issues=[Collections.Generic.List[string]]::new(); $required=@('swe-agent-aci','reflexion','tree-of-thoughts','petri','envtrustbench','auditbench'); foreach($id in $required){$s=@($Registry.sources|Where-Object {[string]$_.mechanismId -ceq $id});if($s.Count -ne 1){[void]$issues.Add("SOURCE_MECHANISM_MISSING:$id")}}
    if(-not [bool]$Registry.bindingPolicy.unfrozenSourceIsReferenceOnly){[void]$issues.Add('SOURCE_UNFROZEN_POLICY_MISSING')}; if(-not [bool]$Registry.bindingPolicy.sourceDriftInvalidatesParity){[void]$issues.Add('SOURCE_DRIFT_POLICY_MISSING')}; if([string]::IsNullOrWhiteSpace([string]$Registry.bindingPolicy.sourceHashMethod)){[void]$issues.Add('SOURCE_HASH_METHOD_MISSING')}; foreach($s in @($Registry.sources)){if([string]::IsNullOrWhiteSpace([string]$s.sourceHash)){[void]$issues.Add("SOURCE_HASH_MISSING:$($s.mechanismId)")}}; [pscustomobject][ordered]@{status=if($issues.Count){'failed'}else{'passed'};issues=@($issues);registryHash=(Get-ESABCDAuditHash $Registry)}
}

function New-ESABCDAuthorityGraph { param([Parameter(Mandatory)]$Plan,[Parameter(Mandatory)][string[]]$Authorities)
    $nodes=@(); foreach($a in @($Authorities|Sort-Object -Unique)){ $kind=if($a -match '^user:'){'user-authorization'}elseif($a -match '^project:'){'project-rule'}elseif($a -match '^source:'){'source'}elseif($a -match '^evidence:'){'evidence'}else{'unclassified'}; $nodes+=,[pscustomobject][ordered]@{authorityId=$a;kind=$kind;canAuthorizeWrite=($kind -eq 'user-authorization');canAuthorizeAcceptance=($kind -in @('user-authorization','project-rule','evidence'));readOnly=($kind -in @('project-rule','source','evidence'))} }
    [pscustomobject][ordered]@{schemaVersion=1;graphId='authority-'+(Get-ESABCDAuditHash $nodes).Substring(0,24);planId=[string]$Plan.planId;nodes=@($nodes);edges=@([pscustomobject]@{from='user-authorization';to='audit-plan';relation='authorizes-scope'},[pscustomobject]@{from='project-rule';to='audit-plan';relation='constrains'},[pscustomobject]@{from='evidence';to='final-gate';relation='proves'});forbidden=@('knowledge-authorizes-write','self-authored-receipt-authorizes-acceptance','stale-source-authorizes-change')}
}

function Test-ESABCDAuthorityGraph { param([Parameter(Mandatory)]$Graph)
    $issues=[Collections.Generic.List[string]]::new(); if (@($Graph.nodes|Where-Object canAuthorizeWrite).Count -lt 1){[void]$issues.Add('AUTHORITY_USER_PROOF_MISSING')}; if (@($Graph.nodes|Where-Object {$_.kind -eq 'unclassified'}).Count -gt 0){[void]$issues.Add('AUTHORITY_NODE_UNCLASSIFIED')}; if (@($Graph.forbidden).Count -lt 3){[void]$issues.Add('AUTHORITY_FORBIDDEN_RULES_INCOMPLETE')}; [pscustomobject][ordered]@{status=if($issues.Count){'failed'}else{'passed'};issues=@($issues);graphHash=(Get-ESABCDAuditHash $Graph)}
}

function New-ESABCDFinalGateReceipt { param([Parameter(Mandatory)]$Plan,[Parameter(Mandatory)]$AuthorityGraph,[Parameter(Mandatory)]$Divergence,[Parameter(Mandatory)]$Audit,[Parameter(Mandatory)]$Verification,[object]$FrameworkCoverage,[string]$ObservedHead='',[ValidateSet('S1','S2','S3','S4','S5','S6')][string]$EvidenceLevel='S1')
    $issues=[Collections.Generic.List[string]]::new(); $p=Test-ESABCDAuditPlan $Plan; if($p.status-ne'passed'){[void]$issues.Add('AUDIT_PLAN_INVALID')}; $a=Test-ESABCDAuthorityGraph $AuthorityGraph; if($a.status-ne'passed'){[void]$issues.Add('AUTHORITY_GRAPH_INVALID')}; if([string]$ObservedHead -and [string]$ObservedHead -cne [string]$Plan.currentHead){[void]$issues.Add('AUDIT_HEAD_DRIFT')}; if([int]$EvidenceLevel.Substring(1) -lt [int]$Plan.minimumEvidenceLevel.Substring(1)){[void]$issues.Add('EVIDENCE_LEVEL_INSUFFICIENT')}; if(-not [bool]$Divergence.complete -or [int]$Divergence.distinctAssumptions -lt 2 -or -not [bool]$Divergence.rollbackVerified){[void]$issues.Add('DIVERGENCE_REQUIREMENTS_UNSATISFIED')}; if(-not [bool]$Audit.independent -or [string]$Audit.status -ne 'passed'){[void]$issues.Add('INDEPENDENT_AUDIT_REQUIRED')}; if([string]$Verification.status -ne 'passed' -or @($Verification.requiredCasesMissing).Count -gt 0 -or [bool]$Verification.stale){[void]$issues.Add('VERIFICATION_INCOMPLETE')}; if($null -ne $FrameworkCoverage -and -not [bool]$FrameworkCoverage.allMeetThreshold){[void]$issues.Add('FRAMEWORK_COVERAGE_BELOW_THRESHOLD')}; $status=if($issues.Count){'blocked'}else{'accepted'}; [pscustomobject][ordered]@{schemaVersion=1;contractId='es://automation/contracts/abcd/final-gate/v1';gateId='gate-'+(Get-ESABCDAuditHash ([ordered]@{plan=$Plan.planId;authority=$AuthorityGraph.graphId;status=$status;issues=@($issues)})).Substring(0,24);status=$status;decisionStatus=if($status-eq'accepted'){'Accepted'}else{'Blocked'};planId=[string]$Plan.planId;planHash=Get-ESABCDAuditHash $Plan;authorityGraphHash=Get-ESABCDAuditHash $AuthorityGraph;evidenceLevel=$EvidenceLevel;issues=@($issues);frameworkCoverage=$FrameworkCoverage;claimsNotProven=@('Unity/Player/IL2CPP/Release behavior')}
}

Export-ModuleMember -Function New-ESABCDAuditPlan,Test-ESABCDAuditPlan,Test-ESABCDAuditSourceRegistry,New-ESABCDAuthorityGraph,Test-ESABCDAuthorityGraph,New-ESABCDFinalGateReceipt,Get-ESABCDAuditHash,Get-ESABCDValidationProfile
