[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)
$ErrorActionPreference='Stop'
$root=Resolve-Path $ProjectRoot
$base=Join-Path $root '.agents/skills/es-ai-interaction-governance'
$fail=@()
foreach($f in @('SKILL.md','references/interaction-governance-contract.md','references/intent-contract.schema.json','references/evaluation-profiles.json','references/next-step-behavior-tree.json','scripts/ESInteractionPathPolicy.ps1','scripts/Invoke-ESInteractionAssessment.ps1','scripts/Invoke-ESContextCollection.ps1','scripts/Test-ESContextCollection.ps1','scripts/Resolve-ESNextStepSelection.ps1','scripts/Invoke-ESInteractionEvidenceAssessment.ps1','scripts/Convert-CodexTranscriptToEvidence.ps1','scripts/Invoke-ESInteractionCloseout.ps1','scripts/Invoke-ESInteractionCloseoutHook.ps1','scripts/Test-ESIntentContract.ps1','tests/evidence-aligned.json','tests/evidence-misaligned.json','tests/evidence-accepted.json')){if(!(Test-Path (Join-Path $base $f))){$fail+="missing:$f"}}
$p=Get-Content (Join-Path $base 'references/evaluation-profiles.json') -Raw -Encoding utf8|ConvertFrom-Json
$t=Get-Content (Join-Path $base 'references/next-step-behavior-tree.json') -Raw -Encoding utf8|ConvertFrom-Json
$cc=$t.contextCollection
if($null -eq $cc -or $cc.mode -ne 'bounded-opt-in' -or $cc.neverAutoExecute -ne $true){$fail+='context-collection:missing-opt-in-contract'}
foreach($k in @('skills','knowledgeEntries','aiwarningsP0')){if($null -eq $cc.maxReads.$k -or [int]$cc.maxReads.$k -lt 1){$fail+="context-collection:invalid-maxReads:$k"}}
foreach($k in @('taskKey','planHash','selection','readSet','sourceHashes','stale','nonClaims')){if(-not (@($cc.record) -contains $k)){$fail+="context-collection:missing-record:$k"}}
if(-not (@($cc.options) -contains 'skill-knowledge-aiwarnings')){$fail+='context-collection:missing-combined-option'}
$ids=@($t.rules|% id);if(($ids|Sort-Object -Unique).Count -ne $ids.Count){$fail+='duplicate-rule-id'}
if(@($t.rules|?{!$_.requiresUserChoice}).Count){$fail+='rule-without-user-choice'}
if([int]$t.maxSuggestions -gt 3 -or [int]$t.maxSuggestions -lt 1){$fail+='invalid-max-suggestions'}
$numberedAssessment=Get-Content (Join-Path $base 'scripts/Invoke-ESInteractionAssessment.ps1') -Raw -Encoding utf8
if($numberedAssessment -notmatch 'number=\$option' -or $numberedAssessment -notmatch 'userInput=\[string\]\$option'){$fail+='next-step-numbering-missing'}
foreach($name in @('default','engineering','handover','release','minimal')){if($null -eq $p.profiles.$name){$fail+="missing-profile:$name"}}
foreach($name in @('default','engineering','handover','release','minimal')){ $d=$p.profiles.$name.dimensionWeights; foreach($k in @('prompt','intent','evidence')){if($null -eq $d.$k){$fail+="missing-dimension-weight:$name.$k"}} }
$contract=Get-Content (Join-Path $base 'references/interaction-governance-contract.md') -Raw -Encoding utf8
foreach($k in @('intentAlignmentScore','evidenceQualityScore','calibrationScore','confidenceScore','overallScore','scoreSource')){if($contract -notmatch [regex]::Escape($k)){$fail+="contract-missing:$k"}}
if($contract -notmatch 'complete icon score line'){$fail+='contract-missing:compact-score-line'}
if($contract -notmatch 'LOW_SCORE_RISK'){$fail+='contract-missing:low-score-risk'}
if($contract -notmatch 'observationMetrics' -or (Get-Content (Join-Path $base 'scripts/Convert-CodexTranscriptToEvidence.ps1') -Raw -Encoding utf8) -notmatch 'elapsedMs'){$fail+='contract-missing:observation-metrics'}
$evidenceAssessment=Join-Path $base 'scripts/Invoke-ESInteractionEvidenceAssessment.ps1'
try {
  $aligned=@(& $evidenceAssessment -InputPath (Join-Path $base 'tests/evidence-aligned.json') 2>&1 | ConvertFrom-Json)
  if($aligned.status -ne 'aligned' -or $aligned.correctionState -ne 'none' -or $aligned.feedbackLoop.userAcceptanceObserved -or @($aligned.diagnosticCodes|Where-Object{$_ -eq 'runtime-not-observed'}).Count){$fail+='evidence-regression:aligned-feedback-loop'}
  $misaligned=@(& $evidenceAssessment -InputPath (Join-Path $base 'tests/evidence-misaligned.json') 2>&1 | ConvertFrom-Json)
  if($misaligned.status -ne 'misaligned' -or $misaligned.feedbackLoop.correctionCount -ne 1 -or $misaligned.feedbackLoop.userAcceptanceObserved -or @($misaligned.diagnosticCodes|Where-Object{$_ -eq 'prior-intent-correction'}).Count -ne 1 -or @($misaligned.diagnosticCodes|Where-Object{$_ -eq 'acceptance-not-observed'}).Count -ne 1){$fail+='evidence-regression:correction-not-accepted'}
  $accepted=@(& $evidenceAssessment -InputPath (Join-Path $base 'tests/evidence-accepted.json') 2>&1 | ConvertFrom-Json)
  if($accepted.status -ne 'aligned' -or $accepted.correctionState -ne 'accepted-followup' -or !$accepted.feedbackLoop.userAcceptanceObserved -or @($accepted.diagnosticCodes|Where-Object{$_ -eq 'acceptance-not-observed'}).Count){$fail+='evidence-regression:accepted-followup'}
} catch {$fail+='evidence-regression:error'}
$assessment=Get-Content (Join-Path $base 'scripts/Invoke-ESInteractionAssessment.ps1') -Raw -Encoding utf8
foreach($needle in @('$TaskStarted','$TaskKind','$RouteStatus','$ContextFreshness','$RiskLevel','$AlreadyCollected','recommendationReasons','suppressedBy','decisionSource')){if($assessment -notmatch [regex]::Escape($needle)){$fail+="assessment-missing:context-collection:$needle"}}
function Assert-ContextCase([string]$Name,[hashtable]$CaseInput,[bool]$ExpectSuggestion,[string]$ExpectedSource='derived'){
  try {
    $out=@(& (Join-Path $base 'scripts/Invoke-ESInteractionAssessment.ps1') -PromptText $CaseInput.Prompt -TaskStarted:$CaseInput.Started -TaskKind $CaseInput.Kind -RouteStatus $CaseInput.Route -ContextFreshness $CaseInput.Fresh -RiskLevel $CaseInput.Risk -AlreadyCollected:$CaseInput.Collected -ContextCollectionRecommended:$CaseInput.Recommended -AllowTestOverride:$CaseInput.Override 2>&1) -join "`n"
    $obj=$out|ConvertFrom-Json
    $hit=@($obj.nextSteps|? id -eq 'offer-context-collection').Count -gt 0
    $suppressionExpected=@($CaseInput.Suppressions)
    if($hit -ne $ExpectSuggestion -or [string]$obj.decisionSource -cne $ExpectedSource -or (@($obj.suppressedBy) -join ',') -cne ($suppressionExpected -join ',')){$fail+="context-case:$Name"}
  } catch {$fail+="context-case-error:$Name"}
}
Assert-ContextCase 'ambiguous-route' @{Prompt='route ambiguous task';Started=$true;Kind='read-only';Route='ambiguous';Fresh='fresh';Risk='low';Collected=$false;Recommended=$false;Override=$false;Suppressions=@()} $true
Assert-ContextCase 'stale-knowledge' @{Prompt='refresh stale project facts';Started=$true;Kind='read-only';Route='resolved';Fresh='stale';Risk='low';Collected=$false;Recommended=$false;Override=$false;Suppressions=@()} $true
Assert-ContextCase 'high-risk-write' @{Prompt='write and release change';Started=$true;Kind='write';Route='resolved';Fresh='fresh';Risk='high';Collected=$false;Recommended=$false;Override=$false;Suppressions=@()} $true
Assert-ContextCase 'not-recommended' @{Prompt='answer simple read-only question';Started=$true;Kind='read-only';Route='resolved';Fresh='fresh';Risk='low';Collected=$false;Recommended=$true;Override=$false;Suppressions=@()} $false
Assert-ContextCase 'already-fresh' @{Prompt='already routed and fresh';Started=$true;Kind='read-only';Route='resolved';Fresh='fresh';Risk='low';Collected=$true;Recommended=$true;Override=$false;Suppressions=@('already-collected')} $false
Assert-ContextCase 'not-started-ambiguous' @{Prompt='host has not started';Started=$false;Kind='read-only';Route='ambiguous';Fresh='fresh';Risk='low';Collected=$false;Recommended=$true;Override=$false;Suppressions=@('task-not-started')} $false
Assert-ContextCase 'collected-stale-high-risk' @{Prompt='already collected high risk task';Started=$true;Kind='write';Route='resolved';Fresh='stale';Risk='high';Collected=$true;Recommended=$true;Override=$false;Suppressions=@('already-collected')} $false
Assert-ContextCase 'explicit-test-override' @{Prompt='fixture override';Started=$true;Kind='read-only';Route='resolved';Fresh='fresh';Risk='low';Collected=$false;Recommended=$true;Override=$true;Suppressions=@()} $true 'test-override'
$closeout=Get-Content (Join-Path $base 'scripts/Invoke-ESInteractionCloseout.ps1') -Raw -Encoding utf8
if($closeout -notmatch '\$SessionPath' -or $closeout -notmatch '\$SessionId' -or $closeout -notmatch 'Convert-CodexTranscriptToEvidence'){$fail+='closeout-missing:fast-transcript-path'}
if($contract -notmatch 'low-loss fast path' -or $contract -notmatch 'newest readable snapshot'){$fail+='contract-missing:fast-transcript-path'}
foreach($k in @('mustPreserve','allowedTransitions','forbiddenTransitions','acceptanceSignals','counterexamples','intentAlignmentStatus','executionDecision','revision')){if($contract -notmatch [regex]::Escape($k)){$fail+="intent-contract-missing:$k"}}
$intentValidator=Join-Path $base 'scripts/Test-ESIntentContract.ps1'
if(Test-Path -LiteralPath $intentValidator){
  $cases=@{
    'intent-contract-positive'=@('tests/intent-aligned.json','positive',$false)
    'intent-contract-denied'=@('tests/intent-misaligned.json','denied-expansion',$false)
    'intent-contract-idempotency'=@('tests/intent-aligned.json','repeat-idempotency',$false)
    'intent-contract-recovery'=@('tests/intent-recovery.json','interruption-recovery',$false)
  }
  foreach($name in $cases.Keys){$v=$cases[$name]; try{$fixture=Join-Path $base -ChildPath ([string]$v[0]); $out=@(& $intentValidator -ContractPath $fixture -Case ([string]$v[1]) 2>&1); $joined=$out -join "`n"; if($joined -notmatch '"status"\s*:\s*"passed"'){$fail+="${name}:failed"}}catch{$fail+="${name}:error"}}
  try{$invalidOut=@(& $intentValidator -ContractPath (Join-Path $base 'tests/intent-invalid.json') -Case invalid-input 2>&1); $invalidJoined=$invalidOut -join "`n"; if($invalidJoined -notmatch '"status"\s*:\s*"failed"'){$fail+='intent-contract-invalid:not-rejected'}}catch{$fail+='intent-contract-invalid:validator-error'}
}
$hook=Get-Content (Join-Path $base 'scripts/Invoke-ESInteractionCloseoutHook.ps1') -Raw -Encoding utf8
if($hook -notmatch 'transcript_path' -or $hook -notmatch 'stop_hook_active' -or $hook -notmatch 'transcript-path-not-absolute' -or $hook -notmatch 'missing-explicit-scope' -or $hook -notmatch 'closeout-script-not-allowlisted' -or $hook -notmatch "decision='block'"){$fail+='hook-missing:bounded-transcript-guard'}
$escapeTarget=Join-Path $root ('interaction-path-escape-'+[Guid]::NewGuid().ToString('N')+'.json')
$assessmentRunner=Join-Path $base 'scripts/Invoke-ESInteractionAssessment.ps1'
$reportEscapeRejected=$false
try{& $assessmentRunner -PromptText 'bounded report path test' -ReportPath $escapeTarget *> $null}catch{$reportEscapeRejected=$true}
if(-not $reportEscapeRejected -or (Test-Path -LiteralPath $escapeTarget)){$fail+='path-boundary:assessment-report-escape'}
$evidenceRunner=Join-Path $base 'scripts/Invoke-ESInteractionEvidenceAssessment.ps1'
$evidenceEscapeRejected=$false
try{& $evidenceRunner -InputPath (Join-Path $base 'tests/evidence-aligned.json') -ReportPath $escapeTarget *> $null}catch{$evidenceEscapeRejected=$true}
if(-not $evidenceEscapeRejected -or (Test-Path -LiteralPath $escapeTarget)){$fail+='path-boundary:evidence-report-escape'}
$transcriptPath=Join-Path ([IO.Path]::GetTempPath()) ('es-interaction-hook-'+[Guid]::NewGuid().ToString('N')+'.jsonl')
[IO.File]::WriteAllText($transcriptPath,"{}"+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
try{
  $converter=Join-Path $base 'scripts/Convert-CodexTranscriptToEvidence.ps1'
  $converterEscapeRejected=$false
  try{& $converter -SessionPath $transcriptPath -OutputPath $escapeTarget *> $null}catch{$converterEscapeRejected=$true}
  if(-not $converterEscapeRejected -or (Test-Path -LiteralPath $escapeTarget)){$fail+='path-boundary:converter-output-escape'}
  $hookRunner=Join-Path $base 'scripts/Invoke-ESInteractionCloseoutHook.ps1'
  $hookPayload=[ordered]@{hook_event_name='Stop';stop_hook_active=$false;transcript_path=$transcriptPath;allow_writes=$false;allow_runtime=$false;last_assistant_message='evidence-first closeout'}|ConvertTo-Json -Compress
  $hookOutput=@(& $hookRunner -InputJson $hookPayload -CloseoutScriptPath (Join-Path $base 'scripts/Test-ESInteractionGovernance.ps1')) -join "`n"
  if($hookOutput -notmatch 'closeout-script-not-allowlisted'){$fail+='hook-boundary:arbitrary-script-not-rejected'}
} finally {
  if(Test-Path -LiteralPath $transcriptPath){Remove-Item -LiteralPath $transcriptPath -Force}
}
$hooksPath=Join-Path $root '.codex/hooks.json'
if(Test-Path -LiteralPath $hooksPath){
  try {$hooks=Get-Content $hooksPath -Raw -Encoding utf8|ConvertFrom-Json; $stopCommands=@($hooks.hooks.Stop.hooks.command)+@($hooks.hooks.Stop | ForEach-Object { @($_.hooks)|ForEach-Object command }); if(-not ($stopCommands -match 'Invoke-ESInteractionCloseoutHook\.ps1')){$fail+='hook-config-missing:closeout-stop'} } catch {$fail+='hook-config-invalid'}
} else {$fail+='hook-config-missing:.codex/hooks.json'}
$status=if($fail.Count){'blocked'}else{'passed'}
[pscustomobject]@{schemaVersion=1;validator='es-ai-interaction-governance';status=$status;profileCount=@($p.profiles.PSObject.Properties).Count;ruleCount=$ids.Count;findingCount=$fail.Count;findings=$fail;runtimeStatus='runtime-not-run';claimsNotProven=@('Semantic quality beyond deterministic signals','Runtime behavior')}|ConvertTo-Json -Depth 5
if($fail.Count){exit 1}
exit 0
