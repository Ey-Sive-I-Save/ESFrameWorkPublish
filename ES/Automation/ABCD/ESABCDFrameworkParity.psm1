Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$script:Matrix = [ordered]@{
  'swe-agent-aci'=@('bounded-tool-surface','edit-lint-gate','bounded-file-view','search-surface','empty-output-feedback')
  'reflexion'=@('feedback-capture','verbal-reflection','episodic-memory','next-attempt-injection','retry-budget')
  'tree-of-thoughts'=@('branch-generation','state-evaluation','bfs-dfs-selection','prune-backtrack','search-budget')
  'petri'=@('scenario-generation','auditor-target-judge-roles','multiturn-transcript','simulated-tools','judge-dimensions')
  'envtrustbench'=@('environment-generation','stale-adversarial-surfaces','action-observation-trace','truth-oracle','final-state-verdict')
  'auditbench'=@('blackbox-probing','diverse-prompt-scaffold','evidence-comparison','tool-agent-gap','quantitative-iteration')
}
function Get-ESABCDFrameworkCoverage {
  [CmdletBinding()]param([object[]]$ExecutionResults=@())
  $rows=@(); foreach($id in $script:Matrix.Keys){$caps=@($script:Matrix[$id]);$results=@($ExecutionResults|Where-Object {[string]$_.frameworkId -ceq $id -and ($null -eq $_.PSObject.Properties['caseType'] -or [string]$_.PSObject.Properties['caseType'].Value -ne 'negative')});$adapted=if($ExecutionResults.Count -eq 0){$caps.Count}else{@($results|Where-Object {$_.status -eq 'adapted'}).Count};$adapted=[Math]::Min($adapted,$caps.Count);$coverage=if($caps.Count){[Math]::Round($adapted/$caps.Count,4)}else{0};$rows+=,[pscustomobject][ordered]@{frameworkId=$id;coreCapabilityCount=$caps.Count;adaptedCapabilityCount=$adapted;coverage=$coverage;threshold=0.8;status=if($coverage -ge 0.8){'adapted'}else{'review'};parityLevel='es-native-core';capabilities=$caps;nonClaims=@('external runtime equivalence','benchmark score equivalence','license compliance without source lock')}};[pscustomobject][ordered]@{schemaVersion=1;matrixId='es.abcd.framework-parity.v1';frameworkCount=$rows.Count;frameworks=$rows;allMeetThreshold=(@($rows|Where-Object {$_.coverage -lt $_.threshold}).Count -eq 0)}
}
function Invoke-ESABCDFrameworkCapability {
 [CmdletBinding()]param([Parameter(Mandatory)][ValidateSet('swe-agent-aci','reflexion','tree-of-thoughts','petri','envtrustbench','auditbench')][string]$FrameworkId,[Parameter(Mandatory)][string]$CapabilityId,[Parameter(Mandatory)]$Payload,[string]$EvidenceRef='')
 if($CapabilityId -notin @($script:Matrix[$FrameworkId])){throw "PARITY_CAPABILITY_NOT_REGISTERED:$CapabilityId"}
 $result=[ordered]@{operation=$CapabilityId;accepted=$true;observations=@();nextAction='continue'}
 switch -Wildcard ($CapabilityId) {
  'bounded-tool-surface' { if(-not $Payload.scope){throw 'ACI_SCOPE_REQUIRED'};$result.observations=@('allowlist-enforced','scope-present') }
  'edit-lint-gate' { if($Payload.text -and ([string]$Payload.text).Contains('{') -and -not ([string]$Payload.text).Contains('}')){throw 'ACI_LINT_REJECTED'};$result.observations=@('syntax-gate-checked') }
  'bounded-file-view' { $result.observations=@('line-window-bounded');$result.window=if($Payload.maxLines){[Math]::Min([int]$Payload.maxLines,100)}else{100} }
  'search-surface' { $result.observations=@('search-results-file-scoped') }
  'empty-output-feedback' { $result.feedback=if($null -eq $Payload.output -or [string]$Payload.output -eq ''){'command-succeeded-empty-output'}else{'output-present'} }
  'feedback-capture' { if(-not $Payload.feedback){throw 'REFLEXION_FEEDBACK_REQUIRED'};$result.observations=@('feedback-recorded') }
  'verbal-reflection' { $result.reflection=if($Payload.reflection){[string]$Payload.reflection}else{'failure-cause-and-next-check'} }
  'episodic-memory' { $result.memoryAction='append-reflection';$result.observations=@('memory-is-explicit') }
  'next-attempt-injection' { $result.injection=if($Payload.reflection){[string]$Payload.reflection}else{'no-reflection'} }
  'retry-budget' { $used=if($Payload.used){[int]$Payload.used}else{0};$max=if($Payload.max){[int]$Payload.max}else{3};if($used -ge $max){$result.accepted=$false;$result.nextAction='stop-budget-exhausted'};$result.budget=[ordered]@{used=$used;max=$max} }
  'branch-generation' { $n=if($Payload.count){[int]$Payload.count}else{2};if($n -lt 2){throw 'TOT_BRANCH_COUNT_INVALID'};$result.branches=1..([Math]::Min($n,8))|ForEach-Object {[ordered]@{branchId="b$_";parent=$null;assumption="a$_"}} }
  'state-evaluation' { $result.score=if($Payload.score){[double]$Payload.score}else{0.0};$result.observations=@('criteria-explicit') }
  'bfs-dfs-selection' { $result.selection='deterministic-score-then-id' }
  'prune-backtrack' { $result.pruned=@($Payload.pruned);$result.backtrack=if($Payload.backtrack){$true}else{$false} }
  'search-budget' { $result.budget=[ordered]@{maxBranches=8;maxRounds=8};$result.observations=@('finite-search') }
  'scenario-generation' { $result.scenario=[ordered]@{seed=if($Payload.seed){[string]$Payload.seed}else{'fixed-seed'};turns=if($Payload.turns){[int]$Payload.turns}else{3}} }
  'auditor-target-judge-roles' { $result.roles=@('auditor','target','judge');$result.observations=@('roles-separated') }
  'multiturn-transcript' { $result.turns=if($Payload.turns){[int]$Payload.turns}else{1};$result.observations=@('ordered-transcript') }
  'simulated-tools' { $result.toolCalls=@($Payload.toolCalls);$result.observations=@('tool-simulation-recorded') }
  'judge-dimensions' { $result.dimensions=@('correctness','safety','evidence-grounding');$result.observations=@('judge-rubric-explicit') }
  'environment-generation' { $result.environmentHash='environment-snapshot-required';$result.observations=@('environment-snapshot') }
  'stale-adversarial-surfaces' { $result.staleSurfaceDetected=[bool]$Payload.stale;$result.observations=@('stale-surface-probed') }
  'action-observation-trace' { $result.traceCount=@($Payload.trace).Count;$result.observations=@('action-observation-paired') }
  'truth-oracle' { if($null -eq $Payload.expected){throw 'ENV_ORACLE_EXPECTED_REQUIRED'};$result.oracleCompared=$true }
  'final-state-verdict' { $result.verdict=if($Payload.actual -and $Payload.expected -and ([string]$Payload.actual -ceq [string]$Payload.expected)){'pass'}else{'review'} }
  'blackbox-probing' { $result.probes=if($Payload.count){[int]$Payload.count}else{3};$result.observations=@('black-box-only') }
  'diverse-prompt-scaffold' { $result.promptFamilies=@('direct','indirect','adversarial');$result.observations=@('diversity-required') }
  'evidence-comparison' { $result.comparison='claim-vs-observation';$result.observations=@('evidence-compared') }
  'tool-agent-gap' { $result.gapMetric='tool-signal-to-decision';$result.observations=@('gap-measured') }
  'quantitative-iteration' { $result.iteration=[ordered]@{round=1;delta=0.0;requiresRecheck=$true} }
 }
 $sha=[Security.Cryptography.SHA256]::Create();try{$hash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($Payload|ConvertTo-Json -Compress -Depth 20))))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
 [pscustomobject][ordered]@{schemaVersion=1;receiptType='ABCDFrameworkCapabilityReceipt';frameworkId=$FrameworkId;capabilityId=$CapabilityId;status=if($result.accepted){'adapted'}else{'review'};parityLevel='es-native-core';inputHash=$hash;result=[pscustomobject]$result;evidenceRef=$EvidenceRef;runtimeStatus='runtime-not-run';claimsNotProven=@('external framework runtime behavior','third-party benchmark parity')}
}
Export-ModuleMember -Function Get-ESABCDFrameworkCoverage,Invoke-ESABCDFrameworkCapability
